using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// A <see cref="RankedDictionary{TKey, TValue, TComparer}"/> ordered by <see cref="Comparer{T}.Default"/> —
/// the convenience alias that closes over <see cref="DefaultComparer{T}"/>, exactly as
/// <see cref="BTreeDictionary{TKey, TValue}"/> fronts its comparer-parameterized form.
/// </summary>
/// <typeparam name="TKey">The key type. Must be orderable by <see cref="Comparer{T}.Default"/>.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
public class RankedDictionary<TKey, TValue> : RankedDictionary<TKey, TValue, DefaultComparer<TKey>>
{
    /// <summary>Initializes a new, empty dictionary ordered by <see cref="Comparer{T}.Default"/>.</summary>
    public RankedDictionary()
    {
    }

    /// <summary>
    /// Initializes a new dictionary ordered by <see cref="Comparer{T}.Default"/> and seeded with
    /// <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The entries to insert. Enumeration order does not affect the result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> contains a duplicate key.</exception>
    public RankedDictionary(IEnumerable<KeyValuePair<TKey, TValue>> source)
        : base(source)
    {
    }
}

/// <summary>
/// An <b>order-statistics dictionary</b>: a sorted map that also answers the two positional questions
/// <see cref="SortedDictionary{TKey, TValue}"/> cannot — <i>where does this key rank?</i> (<see cref="IndexOfKey"/>
/// for a key that is present, <see cref="CountLessThan"/> for the rank one <i>would</i> take) and <i>which entry
/// is k-th?</i> (<see cref="GetKeyAtIndex"/>, <see cref="GetValueAtIndex"/>, <see cref="GetEntryAtIndex"/>) — in
/// <c>O(log n)</c>, on a map that is still being inserted into and removed from, where
/// <see cref="SortedList{TKey, TValue}"/> answers them but pays <c>O(n)</c> per insert and remove. The map
/// counterpart of <see cref="RankedSet{T, TComparer}"/>.
/// </summary>
/// <typeparam name="TKey">The key type, ordered by <typeparamref name="TComparer"/>.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
/// <typeparam name="TComparer">
/// The comparer that defines the key order. Must be a value type implementing <see cref="IComparer{T}"/> so the
/// JIT can devirtualize and inline it — an interface-typed comparer would cost a virtual call for every key a
/// binary search inspects. Use <see cref="DefaultComparer{T}"/> (or the two-parameter
/// <see cref="RankedDictionary{TKey, TValue}"/> alias) for the natural order.
/// </typeparam>
/// <remarks>
/// <para>
/// <b>The BCL gap.</b> <see cref="SortedDictionary{TKey, TValue}"/> has no rank and no positional accessor, so
/// the answers are <c>map.ElementAt(k)</c> — <c>O(k)</c> — and an <c>O(n)</c> count.
/// <see cref="SortedList{TKey, TValue}"/> has exactly the positional surface (<c>IndexOfKey</c>,
/// <c>GetKeyAtIndex</c>, <c>GetValueAtIndex</c>, <c>RemoveAt</c>), and this type keeps its names, but every
/// insert and remove there memmoves its key and value arrays and is <c>O(n)</c>. No BCL map is fast on both
/// halves, and that is the workload this type is for: a live leaderboard keyed by score that reports a
/// player's rank and who holds the 500th place, an order book that reads its k-th price level and the size
/// resting there, a time-keyed window that reads its median entry. Outside the maps, a key-ordered
/// <c>ImmutableSortedSet&lt;KeyValuePair&lt;TKey, TValue&gt;&gt;</c> is <c>O(log n)</c> on both halves, through
/// subtree counts in its AVL nodes, at a heap node per entry; this type's benchmarks measure against that set's
/// <c>Builder</c> too.
/// </para>
/// <para>
/// <b>The layout</b> is <see cref="RankedSet{T, TComparer}"/>'s, with a value array beside each key array:
/// keys and values live in parallel jagged arrays of sorted buckets (<b>sqrt decomposition</b>), with a
/// Fenwick tree over the bucket lengths carrying the positional half. A lookup is a binary search over the
/// per-bucket maximum keys followed by one inside a single contiguous key array, which never touches a value;
/// <see cref="IndexOfKey"/> adds one prefix sum, and the positional accessors one binary-lifting descent.
/// </para>
/// <para>
/// <b>The cost, stated exactly.</b> <see cref="TryGetValue"/>, <see cref="ContainsKey"/>,
/// <see cref="IndexOfKey"/>, <see cref="CountLessThan"/>, the bounds and the positional accessors are
/// <c>O(log n)</c>, and so is an overwrite through the indexer or <see cref="SetValueAtIndex"/>.
/// <b>Inserting a new key and removing one are <c>O(√n)</c></b>, not <c>O(log n)</c>: the growing term is a
/// memmove of at most one bucket's keys and the same span of its values. The bucket capacity tracks
/// <c>√n</c>, which keeps the number of buckets — and so the cost of a split, merge or drop, which rebuilds
/// the Fenwick tree — at <c>Θ(√n)</c> and amortizes it to a constant per mutation. As with
/// <see cref="RankedSet{T, TComparer}"/>, bucket arrays are never narrowed as entries leave, so a map that has
/// contracted sharply pays its high-water size on mutation until <see cref="TrimExcess"/> or
/// <see cref="Clear"/> resets it.
/// </para>
/// <para>
/// <b>Where it wins and where it does not.</b> The documented win is the mixed workload above — interleaved
/// insert and remove with rank and select against the live map — where <see cref="SortedList{TKey, TValue}"/>
/// pays an <c>O(n)</c> memmove per mutation and <see cref="SortedDictionary{TKey, TValue}"/> a linear walk per
/// positional query. It is not a <see cref="BTreeDictionary{TKey, TValue, TComparer}"/> replacement: a map
/// that never asks a positional question should use that one, whose mutations are <c>O(log n)</c>. A small
/// map fits in cache and every option is competitive there. The measured ratios are in
/// <c>docs/api/collections.md</c>.
/// </para>
/// <para>
/// Key identity is <typeparamref name="TComparer"/>'s: two keys are the same key when the comparer orders them
/// equal. Whether a <c>null</c> key is legal is the comparer's decision too — under
/// <see cref="DefaultComparer{T}"/> it orders before every non-<c>null</c> key and is an ordinary key, unlike
/// <see cref="SortedDictionary{TKey, TValue}"/>, which rejects it. There is no out-of-band
/// <c>default(TKey)</c> slot as in the hash-based family. Overwriting the value of an existing key — through
/// the indexer or <see cref="SetValueAtIndex"/> — is not a structural change and does not invalidate active
/// enumerators, matching <see cref="Dictionary{TKey, TValue}"/>. This type is not thread-safe; concurrent
/// callers must synchronize externally.
/// </para>
/// <example>
/// <code>
/// // Leaderboard: score -> player.
/// var board = new RankedDictionary&lt;int, string&gt;();
/// board[120] = "ada";
/// board[340] = "brian";
/// board[90]  = "cleo";
/// board[500] = "dmitri";
///
/// Console.WriteLine(board.GetValueAtIndex(board.Count - 1)); // dmitri — the top score
/// Console.WriteLine(board.IndexOfKey(340));                  // 2      — two scores below it
/// Console.WriteLine(board.CountLessThan(300));               // 2      — 90 and 120
///
/// board.RemoveAt(0);                                         // drop the lowest score
/// Console.WriteLine(board.GetKeyAtIndex(0));                 // 120
/// </code>
/// </example>
/// </remarks>
public class RankedDictionary<TKey, TValue, TComparer>
    : IDictionary<TKey, TValue?>, IReadOnlyDictionary<TKey, TValue?>
    where TComparer : struct, IComparer<TKey>
{
    /// <summary>
    /// The smallest a bucket is ever made — <see cref="RankedSet{T, TComparer}"/>'s floor, for the same
    /// reason: the memmove an insert costs is half a bucket on average, a handful of cache lines at this size.
    /// </summary>
    private const int MinBucketCapacity = 512;

    /// <summary>The initial number of bucket slots. A power of two, as the Fenwick descent requires.</summary>
    private const int InitialSlots = 4;

    // The keys, in ascending order across buckets and inside each one, and the values beside them at the same
    // bucket and offset. Only [0, _bucketCount) is live, and only _lengths[i] of bucket i is meaningful; a
    // bucket's capacity is its key array's length, which its value array always matches. No live bucket is
    // ever empty, which is what lets a rank descent and the enumerators skip that case.
    private TKey[][] _keys = [];
    private TValue?[][] _values = [];
    private int[] _lengths = [];

    // The last live key of each bucket, kept out of line so locating the owning bucket is a binary search over
    // one contiguous array rather than a walk that dereferences a bucket per probe.
    private TKey[] _maxes = [];

    // A Fenwick tree over the bucket lengths, 1-based, of length slots + 1 where slots is _keys.Length and a
    // power of two: a prefix sum gives the number of entries before a bucket, and a binary-lifting descent
    // gives the bucket holding a rank.
    private int[] _tree = new int[1];

    private int _bucketCount;

    // Deliberately not `readonly`: an interface call through a readonly field of an unconstrained struct type
    // forces a defensive copy per call, which is the cost the struct-comparer design exists to remove.
    private TComparer _comparer;

    private int _count;

    // Bumped on every mutation that changes the set of keys or moves entries, so active enumerators detect
    // concurrent modification. A lookup, a failed TryAdd, a Remove of an absent key, and an overwrite of an
    // existing key's value are not mutations.
    private int _version;

    /// <summary>Initializes a new, empty dictionary ordered by <c>default(TComparer)</c>.</summary>
    public RankedDictionary()
    {
    }

    /// <summary>Initializes a new, empty dictionary ordered by <paramref name="comparer"/>.</summary>
    /// <param name="comparer">The comparer defining the key order.</param>
    public RankedDictionary(TComparer comparer)
    {
        _comparer = comparer;
    }

    /// <summary>
    /// Initializes a new dictionary ordered by <c>default(TComparer)</c> and seeded with
    /// <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The entries to insert. Enumeration order does not affect the result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> contains a duplicate key.</exception>
    public RankedDictionary(IEnumerable<KeyValuePair<TKey, TValue>> source)
        : this(source, default)
    {
    }

    /// <summary>
    /// Initializes a new dictionary ordered by <paramref name="comparer"/> and seeded with
    /// <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The entries to insert. Enumeration order does not affect the result.</param>
    /// <param name="comparer">The comparer defining the key order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> contains a duplicate key.</exception>
    public RankedDictionary(IEnumerable<KeyValuePair<TKey, TValue>> source, TComparer comparer)
    {
        ArgumentNullException.ThrowIfNull(source);

        _comparer = comparer;
        foreach (KeyValuePair<TKey, TValue> entry in source)
            Add(entry.Key, entry.Value);
    }

    /// <summary>Gets the comparer that defines this dictionary's key order.</summary>
    public TComparer Comparer => _comparer;

    /// <summary>Gets the number of entries in the dictionary.</summary>
    public int Count => _count;

    /// <summary>
    /// Gets or sets the value associated with <paramref name="key"/>, in <c>O(log n)</c>. The setter inserts
    /// the entry when the key is absent — <c>O(√n)</c> — and overwrites the value in place when it is present.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The value associated with <paramref name="key"/>.</returns>
    /// <exception cref="KeyNotFoundException">The getter was called with a key that is not present.</exception>
    public TValue this[TKey key]
    {
        get
        {
            if (!TryGetValue(key, out TValue? value))
                throw new KeyNotFoundException($"The key '{key}' was not present in the dictionary.");

            return value!;
        }
        set => Insert(key, value, overwrite: true);
    }

    /// <summary>Gets a view over the keys, in ascending order.</summary>
    public KeyCollection Keys => new KeyCollection(this);

    /// <summary>Gets a view over the values, ordered by their keys.</summary>
    public ValueCollection Values => new ValueCollection(this);

    /// <summary>Gets the entry with the smallest key, in <c>O(1)</c>.</summary>
    /// <returns>The first entry in key order.</returns>
    /// <exception cref="InvalidOperationException">The dictionary is empty.</exception>
    public KeyValuePair<TKey, TValue?> Min =>
        TryGetMin(out KeyValuePair<TKey, TValue?> entry)
            ? entry
            : throw new InvalidOperationException("The dictionary is empty.");

    /// <summary>Gets the entry with the largest key, in <c>O(1)</c>.</summary>
    /// <returns>The last entry in key order.</returns>
    /// <exception cref="InvalidOperationException">The dictionary is empty.</exception>
    public KeyValuePair<TKey, TValue?> Max =>
        TryGetMax(out KeyValuePair<TKey, TValue?> entry)
            ? entry
            : throw new InvalidOperationException("The dictionary is empty.");

    /// <summary>
    /// Adds an entry, throwing when <paramref name="key"/> is already present. <c>O(√n)</c> — see the type's
    /// remarks.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="value">The value to associate with <paramref name="key"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is already present.</exception>
    public void Add(TKey key, TValue? value)
    {
        if (!Insert(key, value, overwrite: false))
            throw new ArgumentException($"An entry with the key '{key}' already exists.", nameof(key));
    }

    /// <summary>
    /// Adds an entry only when <paramref name="key"/> is absent, in <c>O(√n)</c>. The non-throwing counterpart
    /// of <see cref="Add"/>; an existing entry is left untouched.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="value">The value to associate with <paramref name="key"/>.</param>
    /// <returns><c>true</c> if the entry was added; <c>false</c> if the key was already present.</returns>
    public bool TryAdd(TKey key, TValue? value) => Insert(key, value, overwrite: false);

    /// <summary>Looks up <paramref name="key"/>, in <c>O(log n)</c>.</summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">The associated value, or <c>default</c> when the key is absent.</param>
    /// <returns><c>true</c> if the key was found.</returns>
    public bool TryGetValue(TKey key, out TValue? value)
    {
        int bucket = LocateBucket(key);
        if (bucket != _bucketCount)
        {
            int offset = LowerBoundInBucket(_keys[bucket], _lengths[bucket], key, out bool found);
            if (found)
            {
                value = _values[bucket][offset];
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>Determines whether <paramref name="key"/> is present, in <c>O(log n)</c>.</summary>
    /// <param name="key">The key to look for.</param>
    /// <returns><c>true</c> if the key is present.</returns>
    public bool ContainsKey(TKey key) => TryGetValue(key, out _);

    /// <summary>
    /// Determines whether any entry holds <paramref name="value"/>, comparing with
    /// <see cref="EqualityComparer{T}.Default"/>. This is an <c>O(n)</c> scan — the map is indexed by key, not
    /// by value.
    /// </summary>
    /// <param name="value">The value to look for.</param>
    /// <returns><c>true</c> if at least one entry holds the value.</returns>
    public bool ContainsValue(TValue? value)
    {
        EqualityComparer<TValue?> comparer = EqualityComparer<TValue?>.Default;
        for (int bucket = 0; bucket < _bucketCount; bucket++)
        {
            TValue?[] values = _values[bucket];
            int length = _lengths[bucket];
            for (int i = 0; i < length; i++)
            {
                if (comparer.Equals(values[i], value))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the rank of <paramref name="key"/> — the number of keys that order before it — in
    /// <c>O(log n)</c>, or <c>-1</c> when it is absent. <see cref="SortedList{TKey, TValue}"/>'s name for the
    /// same question.
    /// </summary>
    /// <param name="key">The key to rank.</param>
    /// <returns>The zero-based position of <paramref name="key"/> in key order, or <c>-1</c>.</returns>
    public int IndexOfKey(TKey key)
    {
        int bucket = LocateBucket(key);
        if (bucket == _bucketCount)
            return -1;

        int offset = LowerBoundInBucket(_keys[bucket], _lengths[bucket], key, out bool found);
        return found ? PrefixSum(bucket) + offset : -1;
    }

    /// <summary>
    /// Counts the keys that order strictly before <paramref name="key"/>, in <c>O(log n)</c> — the rank
    /// <paramref name="key"/> would occupy, whether or not it is present. <see cref="IndexOfKey"/> answers the
    /// same question only for keys that are in the map.
    /// </summary>
    /// <param name="key">The key to bound by.</param>
    /// <returns>The number of keys smaller than <paramref name="key"/>.</returns>
    public int CountLessThan(TKey key)
    {
        int bucket = LocateBucket(key);
        if (bucket == _bucketCount)
            return _count;

        int offset = LowerBoundInBucket(_keys[bucket], _lengths[bucket], key, out _);
        return PrefixSum(bucket) + offset;
    }

    /// <summary>Counts the keys that order before <paramref name="key"/> or equal to it, in <c>O(log n)</c>.</summary>
    /// <param name="key">The key to bound by.</param>
    /// <returns>The number of keys not greater than <paramref name="key"/>.</returns>
    public int CountLessThanOrEqual(TKey key)
    {
        int bucket = LocateBucket(key);
        if (bucket == _bucketCount)
            return _count;

        int offset = LowerBoundInBucket(_keys[bucket], _lengths[bucket], key, out bool found);
        return PrefixSum(bucket) + offset + (found ? 1 : 0);
    }

    /// <summary>
    /// Gets the key at <paramref name="index"/> in ascending order — the <c>(index + 1)</c>-th smallest — in
    /// <c>O(log n)</c>.
    /// </summary>
    /// <param name="index">The zero-based rank of the entry.</param>
    /// <returns>The key occupying <paramref name="index"/> in key order.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or at least <see cref="Count"/>.
    /// </exception>
    public TKey GetKeyAtIndex(int index)
    {
        LocateChecked(index, out int bucket, out int offset);
        return _keys[bucket][offset];
    }

    /// <summary>Gets the value of the entry at <paramref name="index"/> in key order, in <c>O(log n)</c>.</summary>
    /// <param name="index">The zero-based rank of the entry.</param>
    /// <returns>The value of the entry occupying <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or at least <see cref="Count"/>.
    /// </exception>
    public TValue? GetValueAtIndex(int index)
    {
        LocateChecked(index, out int bucket, out int offset);
        return _values[bucket][offset];
    }

    /// <summary>
    /// Gets the whole entry at <paramref name="index"/> in key order, in <c>O(log n)</c> — one descent where
    /// <see cref="GetKeyAtIndex"/> followed by <see cref="GetValueAtIndex"/> would take two.
    /// </summary>
    /// <param name="index">The zero-based rank of the entry.</param>
    /// <returns>The entry occupying <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or at least <see cref="Count"/>.
    /// </exception>
    public KeyValuePair<TKey, TValue?> GetEntryAtIndex(int index)
    {
        LocateChecked(index, out int bucket, out int offset);
        return new KeyValuePair<TKey, TValue?>(_keys[bucket][offset], _values[bucket][offset]);
    }

    /// <summary>
    /// Replaces the value of the entry at <paramref name="index"/> in key order, in <c>O(log n)</c>. The key
    /// and its rank are unchanged, so — like an indexer overwrite — this does not invalidate active
    /// enumerators.
    /// </summary>
    /// <param name="index">The zero-based rank of the entry.</param>
    /// <param name="value">The new value.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or at least <see cref="Count"/>.
    /// </exception>
    public void SetValueAtIndex(int index, TValue? value)
    {
        LocateChecked(index, out int bucket, out int offset);
        _values[bucket][offset] = value;
    }

    /// <summary>Removes the entry with <paramref name="key"/>, in <c>O(√n)</c>.</summary>
    /// <param name="key">The key to remove.</param>
    /// <returns><c>true</c> if an entry was removed; <c>false</c> if the key was absent.</returns>
    public bool Remove(TKey key) => Remove(key, out _);

    /// <summary>
    /// Removes the entry with <paramref name="key"/> and returns the value it held, in <c>O(√n)</c>.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <param name="value">The removed value, or <c>default</c> when the key was absent.</param>
    /// <returns><c>true</c> if an entry was removed; <c>false</c> if the key was absent.</returns>
    public bool Remove(TKey key, out TValue? value)
    {
        int bucket = LocateBucket(key);
        if (bucket != _bucketCount)
        {
            int offset = LowerBoundInBucket(_keys[bucket], _lengths[bucket], key, out bool found);
            if (found)
            {
                value = _values[bucket][offset];
                Delete(bucket, offset);
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Removes the entry at <paramref name="index"/> in key order, in <c>O(√n)</c> — finding it is
    /// <c>O(log n)</c> and the removal shifts at most one bucket.
    /// </summary>
    /// <param name="index">The zero-based rank of the entry to remove.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or at least <see cref="Count"/>.
    /// </exception>
    public void RemoveAt(int index)
    {
        LocateChecked(index, out int bucket, out int offset);
        Delete(bucket, offset);
    }

    /// <summary>
    /// Removes every entry. The dictionary releases its buckets <i>and</i> the slot and index arrays behind
    /// them, so it is left exactly as a new one.
    /// </summary>
    /// <remarks>
    /// The release happens on a dictionary that is <i>already</i> empty too, since removal drops buckets but
    /// never narrows the arrays behind them. Doing so changes nothing observable, so it does not invalidate an
    /// enumerator; only a clear that really removed entries does.
    /// </remarks>
    public void Clear()
    {
        if (_count == 0)
        {
            if (_keys.Length != 0)
                ReleaseSlots();

            return;
        }

        ReleaseSlots();
        _count = 0;
        _version++;
    }

    // Drops the buckets and the parallel slot arrays behind them, putting the map back in the state a new one
    // is in. Shared by `Clear` and by the empty path of `TrimExcess`, which owe the same release.
    private void ReleaseSlots()
    {
        _keys = [];
        _values = [];
        _lengths = [];
        _maxes = [];
        _tree = new int[1];
        _bucketCount = 0;
    }

    /// <summary>
    /// Rebuilds the dictionary at the bucket capacity its <i>current</i> entry count calls for, in
    /// <c>O(n)</c>.
    /// </summary>
    /// <remarks>
    /// A bucket's arrays are never narrowed as entries leave, so a map that has contracted sharply keeps
    /// buckets sized for what it used to hold, and a mutation shifts one of those. This puts it back, and packs
    /// the buckets three-quarters full so there is room to insert without splitting immediately. Call it after
    /// a bulk removal. It invalidates active enumerators — the entries do not change, but they move — and is a
    /// no-op on an empty dictionary that holds no buckets.
    /// </remarks>
    public void TrimExcess()
    {
        if (_count == 0)
        {
            if (_keys.Length != 0)
                ReleaseSlots();

            return;
        }

        int capacity = TargetCapacity(_count);
        int fill = capacity - (capacity / 4);
        int needed = ((_count - 1) / fill) + 1;

        int slots = InitialSlots;
        while (slots < needed)
            slots <<= 1;

        var keys = new TKey[slots][];
        var values = new TValue?[slots][];
        var lengths = new int[slots];
        var maxes = new TKey[slots];

        // Repack by walking the old buckets in order, so nothing is materialized beyond the new arrays.
        int target = 0;
        int written = 0;
        TKey[] packedKeys = [];
        TValue?[] packedValues = [];

        for (int bucket = 0; bucket < _bucketCount; bucket++)
        {
            int length = _lengths[bucket];
            int read = 0;
            while (read < length)
            {
                // A new packed bucket is opened only when there is something to put in it, so a count that is
                // an exact multiple of the fill does not allocate a trailing pair of arrays it then discards.
                if (written == 0)
                {
                    packedKeys = new TKey[capacity];
                    packedValues = new TValue?[capacity];
                }

                int take = Math.Min(fill - written, length - read);
                Array.Copy(_keys[bucket], read, packedKeys, written, take);
                Array.Copy(_values[bucket], read, packedValues, written, take);
                read += take;
                written += take;

                if (written == fill)
                {
                    keys[target] = packedKeys;
                    values[target] = packedValues;
                    lengths[target] = written;
                    maxes[target] = packedKeys[written - 1];
                    target++;
                    written = 0;
                }
            }
        }

        if (written != 0)
        {
            keys[target] = packedKeys;
            values[target] = packedValues;
            lengths[target] = written;
            maxes[target] = packedKeys[written - 1];
            target++;
        }

        _keys = keys;
        _values = values;
        _lengths = lengths;
        _maxes = maxes;
        _bucketCount = target;
        _tree = new int[slots + 1];
        RebuildTree();
        _version++;
    }

    /// <summary>Gets the entry with the smallest key.</summary>
    /// <param name="entry">The first entry in key order, or <c>default</c> when the dictionary is empty.</param>
    /// <returns><c>true</c> if the dictionary is non-empty.</returns>
    public bool TryGetMin(out KeyValuePair<TKey, TValue?> entry)
    {
        if (_bucketCount == 0)
        {
            entry = default;
            return false;
        }

        entry = new KeyValuePair<TKey, TValue?>(_keys[0][0], _values[0][0]);
        return true;
    }

    /// <summary>Gets the entry with the largest key.</summary>
    /// <param name="entry">The last entry in key order, or <c>default</c> when the dictionary is empty.</param>
    /// <returns><c>true</c> if the dictionary is non-empty.</returns>
    public bool TryGetMax(out KeyValuePair<TKey, TValue?> entry)
    {
        if (_bucketCount == 0)
        {
            entry = default;
            return false;
        }

        int last = _bucketCount - 1;
        int offset = _lengths[last] - 1;
        entry = new KeyValuePair<TKey, TValue?>(_keys[last][offset], _values[last][offset]);
        return true;
    }

    /// <summary>
    /// Finds the first entry whose key is <b>greater than or equal to</b> <paramref name="key"/> — the
    /// <c>lower_bound</c> of the ordered containers — in <c>O(log n)</c>. An exact match is its own lower
    /// bound.
    /// </summary>
    /// <param name="key">The key to bound.</param>
    /// <param name="entry">The bounding entry, or <c>default</c> when every key is smaller.</param>
    /// <returns><c>true</c> if such an entry exists.</returns>
    public bool TryGetLowerBound(TKey key, out KeyValuePair<TKey, TValue?> entry)
    {
        int bucket = LocateBucket(key);
        if (bucket == _bucketCount)
        {
            entry = default;
            return false;
        }

        // The bucket's maximum is at least `key`, so the in-bucket lower bound cannot run off its end.
        int offset = LowerBoundInBucket(_keys[bucket], _lengths[bucket], key, out _);
        entry = new KeyValuePair<TKey, TValue?>(_keys[bucket][offset], _values[bucket][offset]);
        return true;
    }

    /// <summary>
    /// Finds the first entry whose key is <b>strictly greater than</b> <paramref name="key"/> — the
    /// <c>upper_bound</c> of the ordered containers — in <c>O(log n)</c>.
    /// </summary>
    /// <param name="key">The key to bound.</param>
    /// <param name="entry">The bounding entry, or <c>default</c> when no key is larger.</param>
    /// <returns><c>true</c> if such an entry exists.</returns>
    public bool TryGetUpperBound(TKey key, out KeyValuePair<TKey, TValue?> entry)
    {
        entry = default;

        int bucket = LocateBucket(key);
        if (bucket == _bucketCount)
            return false;

        int offset = LowerBoundInBucket(_keys[bucket], _lengths[bucket], key, out bool found);
        if (found)
            offset++;

        // An exact hit on the bucket's maximum pushes the bound into the next bucket, if there is one.
        if (offset == _lengths[bucket])
        {
            bucket++;
            if (bucket == _bucketCount)
                return false;

            offset = 0;
        }

        entry = new KeyValuePair<TKey, TValue?>(_keys[bucket][offset], _values[bucket][offset]);
        return true;
    }

    /// <summary>
    /// Enumerates, in ascending key order, every entry whose key lies in the half-open range
    /// <c>[fromInclusive, toExclusive)</c>. The scan seeks to the lower bound in <c>O(log n)</c> and then walks
    /// the buckets' contiguous arrays, so it costs <c>O(log n + k)</c> for <c>k</c> results.
    /// </summary>
    /// <param name="fromInclusive">The inclusive lower bound of the range.</param>
    /// <param name="toExclusive">The exclusive upper bound of the range.</param>
    /// <returns>An allocation-free enumerable over the matching entries.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="toExclusive"/> orders before <paramref name="fromInclusive"/>.
    /// </exception>
    public RangeEnumerable EnumerateRange(TKey fromInclusive, TKey toExclusive)
    {
        if (_comparer.Compare(fromInclusive, toExclusive) > 0)
            throw new ArgumentException(
                "toExclusive must not order before fromInclusive.", nameof(toExclusive));

        return new RangeEnumerable(this, fromInclusive, toExclusive);
    }

    /// <summary>
    /// Returns a struct enumerator over the entries in ascending key order. Because it is a struct that holds
    /// only a bucket and an offset, iterating it via <c>foreach</c> allocates nothing.
    /// </summary>
    /// <returns>A struct enumerator over this dictionary.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<KeyValuePair<TKey, TValue?>> IEnumerable<KeyValuePair<TKey, TValue?>>.GetEnumerator() =>
        GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Copies every entry, in ascending key order, into <paramref name="array"/> starting at
    /// <paramref name="arrayIndex"/>.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
    public void CopyTo(KeyValuePair<TKey, TValue?>[] array, int arrayIndex)
    {
        CopyToGuard.Validate(array, arrayIndex, _count, CopyToGuard.EntriesMessage);

        int i = arrayIndex;
        for (int bucket = 0; bucket < _bucketCount; bucket++)
        {
            TKey[] keys = _keys[bucket];
            TValue?[] values = _values[bucket];
            int length = _lengths[bucket];
            for (int offset = 0; offset < length; offset++)
                array[i++] = new KeyValuePair<TKey, TValue?>(keys[offset], values[offset]);
        }
    }

    // ── IDictionary / IReadOnlyDictionary explicit members ────────────────────
    // The primary surface stays strongly typed (KeyCollection / ValueCollection are structs, so the public
    // Keys/Values views allocate nothing); these adapters box a view only when the interface-typed one is asked
    // for.

    // The primary indexer returns the non-nullable TValue (the family-wide shape pinned by
    // IndexerReturnTypeTests); the interface-typed ones are declared TValue? and forward to it.
    TValue? IDictionary<TKey, TValue?>.this[TKey key]
    {
        get => this[key];
        set => this[key] = value!;
    }

    TValue? IReadOnlyDictionary<TKey, TValue?>.this[TKey key] => this[key];

    ICollection<TKey> IDictionary<TKey, TValue?>.Keys => Keys;

    ICollection<TValue?> IDictionary<TKey, TValue?>.Values => Values;

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue?>.Keys => Keys;

    IEnumerable<TValue?> IReadOnlyDictionary<TKey, TValue?>.Values => Values;

    bool ICollection<KeyValuePair<TKey, TValue?>>.IsReadOnly => false;

    void ICollection<KeyValuePair<TKey, TValue?>>.Add(KeyValuePair<TKey, TValue?> item) =>
        Add(item.Key, item.Value);

    bool ICollection<KeyValuePair<TKey, TValue?>>.Contains(KeyValuePair<TKey, TValue?> item) =>
        TryGetValue(item.Key, out TValue? value) && EqualityComparer<TValue?>.Default.Equals(value, item.Value);

    bool ICollection<KeyValuePair<TKey, TValue?>>.Remove(KeyValuePair<TKey, TValue?> item)
    {
        // ICollection<KVP> semantics: remove only when the *pair* matches, so a stale value must not delete
        // the current entry.
        if (!TryGetValue(item.Key, out TValue? value) ||
            !EqualityComparer<TValue?>.Default.Equals(value, item.Value))
        {
            return false;
        }

        return Remove(item.Key);
    }

    // ---- internals ---------------------------------------------------------------------------------

    // The index of the first bucket whose maximum is not smaller than `key` — the only bucket that can hold
    // it — or _bucketCount when `key` orders after every key.
    private int LocateBucket(TKey key)
    {
        int lo = 0;
        int hi = _bucketCount - 1;

        while (lo <= hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            if (_comparer.Compare(_maxes[mid], key) < 0)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        return lo;
    }

    // Binary search inside one bucket's keys. Returns the index of the match when `found`, and otherwise the
    // index of the first key greater than `key` — both the insertion point and the number of keys in this
    // bucket that order before `key`.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LowerBoundInBucket(TKey[] bucket, int length, TKey key, out bool found)
    {
        int lo = 0;
        int hi = length - 1;

        while (lo <= hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            int cmp = _comparer.Compare(bucket[mid], key);
            if (cmp == 0)
            {
                found = true;
                return mid;
            }

            if (cmp < 0)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        found = false;
        return lo;
    }

    // The single insertion entry point. Returns true when a new entry was added; false when the key was
    // already present, in which case `overwrite` decides whether its value was replaced.
    private bool Insert(TKey key, TValue? value, bool overwrite)
    {
        if (_bucketCount == 0)
        {
            CreateFirstBucket(key, value);
            return true;
        }

        // A key past every maximum belongs at the end of the last bucket. LocateBucket reports that as "no
        // bucket can hold it", which is the answer a lookup needs and not the one an insert does.
        int bucket = LocateBucket(key);
        if (bucket == _bucketCount)
            bucket--;

        int offset = LowerBoundInBucket(_keys[bucket], _lengths[bucket], key, out bool found);
        if (found)
        {
            // An in-place value overwrite is not a structural change, so — matching Dictionary<TKey, TValue>
            // and the rest of the family (issue #233) — it does not bump the version.
            if (overwrite)
                _values[bucket][offset] = value;

            return false;
        }

        if (_lengths[bucket] == _keys[bucket].Length)
        {
            // Growing a full bucket in place is free of structural work, so it is preferred while the bucket is
            // still smaller than what this many entries calls for. Only a bucket already at the target
            // capacity splits, which bounds the bucket count to O(sqrt n). See TargetCapacity.
            int targetCapacity = TargetCapacity(_count + 1);
            if (_keys[bucket].Length < targetCapacity)
            {
                GrowBucket(bucket, targetCapacity);
            }
            else
            {
                int half = SplitBucket(bucket);

                // The insertion point moves with the entries. An offset of exactly `half` stays on the left
                // half, appended at its end, which the split left room for.
                if (offset > half)
                {
                    bucket++;
                    offset -= half;
                }
            }
        }

        TKey[] keys = _keys[bucket];
        TValue?[] values = _values[bucket];
        int length = _lengths[bucket];
        Array.Copy(keys, offset, keys, offset + 1, length - offset);
        Array.Copy(values, offset, values, offset + 1, length - offset);
        keys[offset] = key;
        values[offset] = value;
        _lengths[bucket] = ++length;

        if (offset == length - 1)
            _maxes[bucket] = key;

        AddToTree(bucket, 1);
        _count++;
        _version++;
        return true;
    }

    private void CreateFirstBucket(TKey key, TValue? value)
    {
        if (_keys.Length == 0)
            AllocateSlots(InitialSlots);

        _keys[0] = new TKey[MinBucketCapacity];
        _values[0] = new TValue?[MinBucketCapacity];
        _keys[0][0] = key;
        _values[0][0] = value;
        _lengths[0] = 1;
        _maxes[0] = key;
        _bucketCount = 1;

        AddToTree(0, 1);
        _count = 1;
        _version++;
    }

    // The capacity a bucket is grown or split at, for a map of `count` entries: the smallest power of two
    // whose square is at least `count`, never below MinBucketCapacity. RankedSet<T, TComparer>.TargetCapacity
    // explains why this rule, rather than a fixed capacity, is what keeps the structural cost a constant per
    // mutation. The loop terminates without a clamp: `int` counts stop below 65536², so it stops at 65536.
    private static int TargetCapacity(int count)
    {
        int capacity = MinBucketCapacity;
        while ((long)capacity * capacity < count)
            capacity <<= 1;

        return capacity;
    }

    // Widens one bucket's two arrays in place. Nothing structural moves.
    private void GrowBucket(int bucket, int capacity)
    {
        int length = _lengths[bucket];

        var keys = new TKey[capacity];
        Array.Copy(_keys[bucket], keys, length);
        _keys[bucket] = keys;

        var values = new TValue?[capacity];
        Array.Copy(_values[bucket], values, length);
        _values[bucket] = values;
    }

    // Splits a full bucket into two half-full ones and returns the length of the left half. Every later bucket
    // shifts up one slot and the Fenwick tree is rebuilt — Theta(b), which TargetCapacity amortizes away.
    private int SplitBucket(int bucket)
    {
        if (_bucketCount == _keys.Length)
            AllocateSlots(_keys.Length * 2);

        int tail = _bucketCount - bucket - 1;
        Array.Copy(_keys, bucket + 1, _keys, bucket + 2, tail);
        Array.Copy(_values, bucket + 1, _values, bucket + 2, tail);
        Array.Copy(_lengths, bucket + 1, _lengths, bucket + 2, tail);
        Array.Copy(_maxes, bucket + 1, _maxes, bucket + 2, tail);

        TKey[] leftKeys = _keys[bucket];
        TValue?[] leftValues = _values[bucket];
        int capacity = leftKeys.Length;
        int half = capacity / 2;

        var rightKeys = new TKey[capacity];
        var rightValues = new TValue?[capacity];
        Array.Copy(leftKeys, half, rightKeys, 0, capacity - half);
        Array.Copy(leftValues, half, rightValues, 0, capacity - half);
        Array.Clear(leftKeys, half, capacity - half);
        Array.Clear(leftValues, half, capacity - half);

        _keys[bucket + 1] = rightKeys;
        _values[bucket + 1] = rightValues;
        _lengths[bucket + 1] = capacity - half;
        _maxes[bucket + 1] = _maxes[bucket];
        _lengths[bucket] = half;
        _maxes[bucket] = leftKeys[half - 1];
        _bucketCount++;

        RebuildTree();
        return half;
    }

    private void Delete(int bucket, int offset)
    {
        TKey[] keys = _keys[bucket];
        TValue?[] values = _values[bucket];
        int length = _lengths[bucket] - 1;
        Array.Copy(keys, offset + 1, keys, offset, length - offset);
        Array.Copy(values, offset + 1, values, offset, length - offset);
        keys[length] = default!;
        values[length] = default;
        _lengths[bucket] = length;
        AddToTree(bucket, -1);
        _count--;
        _version++;

        if (length == 0)
        {
            DropBucket(bucket);
            return;
        }

        if (offset == length)
            _maxes[bucket] = keys[length - 1];

        // Merge a thinned-out bucket into a neighbour only when the union stays at or below half the
        // destination's capacity, so a merge cannot be undone by the next insert. Both thresholds are read off
        // the destination bucket's own array, since capacities differ once the target has grown.
        if (length >= keys.Length / 4)
            return;

        if (bucket + 1 < _bucketCount && length + _lengths[bucket + 1] <= keys.Length / 2)
            MergeBuckets(bucket, bucket + 1);
        else if (bucket > 0 && _lengths[bucket - 1] + length <= _keys[bucket - 1].Length / 2)
            MergeBuckets(bucket - 1, bucket);
    }

    private void MergeBuckets(int left, int right)
    {
        Array.Copy(_keys[right], 0, _keys[left], _lengths[left], _lengths[right]);
        Array.Copy(_values[right], 0, _values[left], _lengths[left], _lengths[right]);
        _lengths[left] += _lengths[right];
        _maxes[left] = _maxes[right];
        DropBucket(right);
    }

    private void DropBucket(int bucket)
    {
        int tail = _bucketCount - bucket - 1;
        Array.Copy(_keys, bucket + 1, _keys, bucket, tail);
        Array.Copy(_values, bucket + 1, _values, bucket, tail);
        Array.Copy(_lengths, bucket + 1, _lengths, bucket, tail);
        Array.Copy(_maxes, bucket + 1, _maxes, bucket, tail);

        _bucketCount--;
        _keys[_bucketCount] = null!;
        _values[_bucketCount] = null!;
        _lengths[_bucketCount] = 0;
        _maxes[_bucketCount] = default!;

        RebuildTree();
    }

    // Grows the parallel bucket arrays and the Fenwick tree to `slots`, always a power of two — the descent in
    // Locate relies on the tree's length being one.
    private void AllocateSlots(int slots)
    {
        Array.Resize(ref _keys, slots);
        Array.Resize(ref _values, slots);
        Array.Resize(ref _lengths, slots);
        Array.Resize(ref _maxes, slots);
        _tree = new int[slots + 1];
        RebuildTree();
    }

    private void RebuildTree()
    {
        int slots = _tree.Length - 1;
        Array.Clear(_tree, 0, _tree.Length);
        for (int i = 0; i < _bucketCount; i++)
            _tree[i + 1] = _lengths[i];

        // In-place build: each node folds into its parent, so the whole tree costs O(b).
        for (int i = 1; i <= slots; i++)
        {
            int parent = i + (i & -i);
            if (parent <= slots)
                _tree[parent] += _tree[i];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddToTree(int bucket, int delta)
    {
        int slots = _tree.Length - 1;
        for (int i = bucket + 1; i <= slots; i += i & -i)
            _tree[i] += delta;
    }

    // The number of entries held by the buckets before `bucket`.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int PrefixSum(int bucket)
    {
        int sum = 0;
        for (int i = bucket; i > 0; i -= i & -i)
            sum += _tree[i];

        return sum;
    }

    // Validates a rank against the count, then descends to it — the shared front half of every positional
    // accessor, so each one throws the same exception for the same mistake.
    private void LocateChecked(int index, out int bucket, out int offset)
    {
        if ((uint)index >= (uint)_count)
            throw new ArgumentOutOfRangeException(nameof(index));

        Locate(index, out bucket, out offset);
    }

    // Binary lifting down the Fenwick tree: finds the bucket holding the entry of rank `index` and the offset
    // inside it, in O(log b) and without touching a key. The descent starts from the largest power of two that
    // does not exceed the *live* bucket count, not from the tree's capacity, so a contracted map does not pay
    // log2 of its high-water bucket count on every positional query. See RankedSet<T, TComparer>.Locate for
    // why the caller's 0 <= index < Count check is what lets each step skip a bounds test.
    private void Locate(int index, out int bucket, out int offset)
    {
        int position = 0;
        int remaining = index;

        for (int step = 1 << BitOperations.Log2((uint)_bucketCount); step > 0; step >>= 1)
        {
            int candidate = position + step;
            if (_tree[candidate] <= remaining)
            {
                position = candidate;
                remaining -= _tree[candidate];
            }
        }

        bucket = position;
        offset = remaining;
    }

    /// <summary>
    /// A struct enumerator over a <see cref="RankedDictionary{TKey, TValue, TComparer}"/>'s entries in
    /// ascending key order. Because it is a struct holding only a bucket and an offset, iterating it via
    /// <c>foreach</c> allocates nothing.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue?>>
    {
        private readonly RankedDictionary<TKey, TValue, TComparer> _dict;
        private readonly int _version;
        private int _bucket;
        private int _offset;
        private KeyValuePair<TKey, TValue?> _current;

        internal Enumerator(RankedDictionary<TKey, TValue, TComparer> dict)
        {
            _dict = dict;
            _version = dict._version;
            _bucket = 0;
            _offset = 0;
            _current = default;
        }

        /// <summary>Gets the entry at the current position of the enumerator.</summary>
        public readonly KeyValuePair<TKey, TValue?> Current => _current;

        readonly object IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next entry in key order.</summary>
        /// <returns><c>true</c> if there is a next entry; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The dictionary was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (_version != _dict._version)
                ThrowModified();

            if (_bucket < _dict._bucketCount)
            {
                // No live bucket is empty, so the cursor always points at an entry while it is in range.
                _current = new KeyValuePair<TKey, TValue?>(
                    _dict._keys[_bucket][_offset], _dict._values[_bucket][_offset]);

                if (++_offset == _dict._lengths[_bucket])
                {
                    _bucket++;
                    _offset = 0;
                }

                return true;
            }

            _current = default;
            return false;
        }

        /// <summary>Resets the enumerator to before the first entry.</summary>
        /// <exception cref="InvalidOperationException">The dictionary was modified during enumeration.</exception>
        public void Reset()
        {
            if (_version != _dict._version)
                ThrowModified();

            _bucket = 0;
            _offset = 0;
            _current = default;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }

    // Static, so an enumerator's throw path does not take the enumerator's address and stop the JIT promoting
    // its fields to registers.
    private static void ThrowModified() =>
        throw new InvalidOperationException("The dictionary was modified during enumeration.");

    /// <summary>
    /// The result of <see cref="EnumerateRange"/>: an allocation-free view over the entries of one key range,
    /// in ascending key order.
    /// </summary>
    public readonly struct RangeEnumerable : IEnumerable<KeyValuePair<TKey, TValue?>>
    {
        private readonly RankedDictionary<TKey, TValue, TComparer> _dict;
        private readonly TKey _from;
        private readonly TKey _toExclusive;

        internal RangeEnumerable(RankedDictionary<TKey, TValue, TComparer> dict, TKey from, TKey toExclusive)
        {
            _dict = dict;
            _from = from;
            _toExclusive = toExclusive;
        }

        /// <summary>Returns a struct enumerator over the entries in the range.</summary>
        /// <returns>A struct enumerator over the matching entries.</returns>
        public RangeEnumerator GetEnumerator() => new RangeEnumerator(_dict, _from, _toExclusive);

        IEnumerator<KeyValuePair<TKey, TValue?>> IEnumerable<KeyValuePair<TKey, TValue?>>.GetEnumerator() =>
            GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A struct enumerator over the entries of one key range of a
    /// <see cref="RankedDictionary{TKey, TValue, TComparer}"/>, in ascending key order.
    /// </summary>
    public struct RangeEnumerator : IEnumerator<KeyValuePair<TKey, TValue?>>
    {
        private readonly RankedDictionary<TKey, TValue, TComparer> _dict;
        private readonly TKey _from;
        private readonly TKey _toExclusive;
        private readonly int _version;
        private int _bucket;
        private int _offset;
        private KeyValuePair<TKey, TValue?> _current;
        private bool _finished;

        internal RangeEnumerator(RankedDictionary<TKey, TValue, TComparer> dict, TKey from, TKey toExclusive)
        {
            _dict = dict;
            _from = from;
            _toExclusive = toExclusive;
            _version = dict._version;
            _bucket = 0;
            _offset = 0;
            _current = default;
            _finished = false;
            Seek();
        }

        /// <summary>Gets the entry at the current position of the enumerator.</summary>
        public readonly KeyValuePair<TKey, TValue?> Current => _current;

        readonly object IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next entry in the range.</summary>
        /// <returns><c>true</c> if there is a next entry; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The dictionary was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (_version != _dict._version)
                ThrowModified();

            if (_finished || _bucket == _dict._bucketCount)
            {
                _current = default;
                return false;
            }

            TKey key = _dict._keys[_bucket][_offset];

            // The walk is ascending, so the first key at or past the upper bound ends the scan for good.
            if (_dict._comparer.Compare(key, _toExclusive) >= 0)
            {
                _finished = true;
                _current = default;
                return false;
            }

            _current = new KeyValuePair<TKey, TValue?>(key, _dict._values[_bucket][_offset]);

            if (++_offset == _dict._lengths[_bucket])
            {
                _bucket++;
                _offset = 0;
            }

            return true;
        }

        /// <summary>Resets the enumerator to before the first entry of the range.</summary>
        /// <exception cref="InvalidOperationException">The dictionary was modified during enumeration.</exception>
        public void Reset()
        {
            if (_version != _dict._version)
                ThrowModified();

            _bucket = 0;
            _offset = 0;
            _current = default;
            _finished = false;
            Seek();
        }

        // Positions the cursor at the range's lower bound, or marks the scan finished when every key orders
        // before it.
        private void Seek()
        {
            _bucket = _dict.LocateBucket(_from);
            if (_bucket == _dict._bucketCount)
            {
                _finished = true;
                return;
            }

            _offset = _dict.LowerBoundInBucket(_dict._keys[_bucket], _dict._lengths[_bucket], _from, out _);
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }

    /// <summary>
    /// A view over a <see cref="RankedDictionary{TKey, TValue, TComparer}"/>'s keys, in ascending order. It is
    /// a read-only <see cref="ICollection{T}"/>: the mutating members throw
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    public readonly struct KeyCollection : ICollection<TKey>
    {
        private readonly RankedDictionary<TKey, TValue, TComparer> _dict;

        internal KeyCollection(RankedDictionary<TKey, TValue, TComparer> dict) => _dict = dict;

        /// <summary>Gets the number of keys in the view (equal to the dictionary's count).</summary>
        public int Count => _dict._count;

        /// <summary>Gets a value indicating whether the view is read-only. Always <c>true</c>.</summary>
        public bool IsReadOnly => true;

        /// <summary>Returns an allocation-free struct enumerator over the keys, in ascending order.</summary>
        /// <returns>A struct enumerator over the keys.</returns>
        public Enumerator GetEnumerator() => new Enumerator(_dict);

        /// <summary>Determines whether the dictionary contains <paramref name="item"/> as a key.</summary>
        /// <param name="item">The key to look for.</param>
        /// <returns><c>true</c> if the key is present.</returns>
        public bool Contains(TKey item) => _dict.ContainsKey(item);

        /// <summary>
        /// Copies the keys, in ascending order, into <paramref name="array"/> starting at
        /// <paramref name="arrayIndex"/>.
        /// </summary>
        /// <param name="array">The destination array.</param>
        /// <param name="arrayIndex">The zero-based index at which copying begins.</param>
        /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.
        /// </exception>
        /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
        public void CopyTo(TKey[] array, int arrayIndex)
        {
            CopyToGuard.Validate(array, arrayIndex, _dict._count, CopyToGuard.KeysMessage);

            int i = arrayIndex;
            for (int bucket = 0; bucket < _dict._bucketCount; bucket++)
            {
                int length = _dict._lengths[bucket];
                Array.Copy(_dict._keys[bucket], 0, array, i, length);
                i += length;
            }
        }

        IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        void ICollection<TKey>.Add(TKey item) =>
            throw new NotSupportedException("The key view is read-only.");

        void ICollection<TKey>.Clear() =>
            throw new NotSupportedException("The key view is read-only.");

        bool ICollection<TKey>.Remove(TKey item) =>
            throw new NotSupportedException("The key view is read-only.");

        /// <summary>
        /// A struct enumerator over the keys of a <see cref="RankedDictionary{TKey, TValue, TComparer}"/>, in
        /// ascending order.
        /// </summary>
        public struct Enumerator : IEnumerator<TKey>
        {
            private RankedDictionary<TKey, TValue, TComparer>.Enumerator _inner;

            internal Enumerator(RankedDictionary<TKey, TValue, TComparer> dict) => _inner = dict.GetEnumerator();

            /// <summary>Gets the key at the current position of the enumerator.</summary>
            public readonly TKey Current => _inner.Current.Key;

            readonly object? IEnumerator.Current => Current;

            /// <summary>Advances the enumerator to the next key.</summary>
            /// <returns><c>true</c> if there is a next key; otherwise <c>false</c>.</returns>
            public bool MoveNext() => _inner.MoveNext();

            /// <summary>Resets the enumerator to before the first key.</summary>
            public void Reset() => _inner.Reset();

            /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
            public readonly void Dispose()
            {
            }
        }
    }

    /// <summary>
    /// A view over a <see cref="RankedDictionary{TKey, TValue, TComparer}"/>'s values, ordered by their keys.
    /// It is a read-only <see cref="ICollection{T}"/>: the mutating members throw
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    public readonly struct ValueCollection : ICollection<TValue?>
    {
        private readonly RankedDictionary<TKey, TValue, TComparer> _dict;

        internal ValueCollection(RankedDictionary<TKey, TValue, TComparer> dict) => _dict = dict;

        /// <summary>Gets the number of values in the view (equal to the dictionary's count).</summary>
        public int Count => _dict._count;

        /// <summary>Gets a value indicating whether the view is read-only. Always <c>true</c>.</summary>
        public bool IsReadOnly => true;

        /// <summary>Returns an allocation-free struct enumerator over the values, in key order.</summary>
        /// <returns>A struct enumerator over the values.</returns>
        public Enumerator GetEnumerator() => new Enumerator(_dict);

        /// <summary>Determines whether any entry holds <paramref name="item"/>. This is an <c>O(n)</c> scan.</summary>
        /// <param name="item">The value to look for.</param>
        /// <returns><c>true</c> if at least one entry holds the value.</returns>
        public bool Contains(TValue? item) => _dict.ContainsValue(item);

        /// <summary>
        /// Copies the values, in key order, into <paramref name="array"/> starting at
        /// <paramref name="arrayIndex"/>.
        /// </summary>
        /// <param name="array">The destination array.</param>
        /// <param name="arrayIndex">The zero-based index at which copying begins.</param>
        /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.
        /// </exception>
        /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
        public void CopyTo(TValue?[] array, int arrayIndex)
        {
            CopyToGuard.Validate(array, arrayIndex, _dict._count, CopyToGuard.ValuesMessage);

            int i = arrayIndex;
            for (int bucket = 0; bucket < _dict._bucketCount; bucket++)
            {
                int length = _dict._lengths[bucket];
                Array.Copy(_dict._values[bucket], 0, array, i, length);
                i += length;
            }
        }

        IEnumerator<TValue?> IEnumerable<TValue?>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        void ICollection<TValue?>.Add(TValue? item) =>
            throw new NotSupportedException("The value view is read-only.");

        void ICollection<TValue?>.Clear() =>
            throw new NotSupportedException("The value view is read-only.");

        bool ICollection<TValue?>.Remove(TValue? item) =>
            throw new NotSupportedException("The value view is read-only.");

        /// <summary>
        /// A struct enumerator over the values of a <see cref="RankedDictionary{TKey, TValue, TComparer}"/>, in
        /// key order.
        /// </summary>
        public struct Enumerator : IEnumerator<TValue?>
        {
            private RankedDictionary<TKey, TValue, TComparer>.Enumerator _inner;

            internal Enumerator(RankedDictionary<TKey, TValue, TComparer> dict) => _inner = dict.GetEnumerator();

            /// <summary>Gets the value at the current position of the enumerator.</summary>
            public readonly TValue? Current => _inner.Current.Value;

            readonly object? IEnumerator.Current => Current;

            /// <summary>Advances the enumerator to the next value.</summary>
            /// <returns><c>true</c> if there is a next value; otherwise <c>false</c>.</returns>
            public bool MoveNext() => _inner.MoveNext();

            /// <summary>Resets the enumerator to before the first value.</summary>
            public void Reset() => _inner.Reset();

            /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
            public readonly void Dispose()
            {
            }
        }
    }
}
