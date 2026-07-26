using System.Collections;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// A <see cref="BTreeDictionary{TKey, TValue, TComparer}"/> ordered by <see cref="Comparer{T}.Default"/> —
/// the convenience alias that closes over <see cref="DefaultComparer{T}"/>, exactly as
/// <see cref="IntDictionary{TValue}"/> fronts <see cref="IntDictionary{TValue, THasher}"/>.
/// </summary>
/// <typeparam name="TKey">The key type. Must be orderable by <see cref="Comparer{T}.Default"/>.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
public class BTreeDictionary<TKey, TValue> : BTreeDictionary<TKey, TValue, DefaultComparer<TKey>>
{
    /// <summary>Initializes a new, empty dictionary ordered by <see cref="Comparer{T}.Default"/>.</summary>
    public BTreeDictionary()
    {
    }

    /// <summary>
    /// Initializes a new dictionary ordered by <see cref="Comparer{T}.Default"/> and seeded with
    /// <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The entries to insert. Enumeration order does not affect the result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> contains a duplicate key.</exception>
    public BTreeDictionary(IEnumerable<KeyValuePair<TKey, TValue>> source)
        : base(source)
    {
    }
}

/// <summary>
/// A <b>sorted dictionary backed by a B-tree</b>: keys are kept in ascending
/// <typeparamref name="TComparer"/> order across nodes that hold up to <c>31</c> keys each in flat arrays,
/// so a lookup visits <c>log₃₂(n)</c> nodes instead of chasing <c>log₂(n)</c> pointers. Adds the ordered
/// surface a hash table cannot answer — <see cref="Min"/>, <see cref="Max"/>,
/// <see cref="TryGetLowerBound"/>, <see cref="TryGetUpperBound"/>, <see cref="EnumerateRange"/>, and in-order
/// enumeration.
/// </summary>
/// <typeparam name="TKey">The key type, ordered by <typeparamref name="TComparer"/>.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
/// <typeparam name="TComparer">
/// The comparer that defines the key order. Must be a value type implementing <see cref="IComparer{TKey}"/>
/// so the JIT can devirtualize and inline it — an interface-typed comparer would cost a virtual call for
/// every key inspected inside a node. Use <see cref="DefaultComparer{T}"/> (or the two-parameter
/// <see cref="BTreeDictionary{TKey, TValue}"/> alias) for the natural order.
/// </typeparam>
/// <remarks>
/// <para>
/// The BCL has no B-tree. <see cref="SortedDictionary{TKey, TValue}"/> is a red-black tree: one heap object
/// per entry and roughly <c>log₂(n)</c> dependent pointer chases — about 20 potential cache misses at
/// <c>n = 1M</c> — for a single lookup. <see cref="SortedList{TKey, TValue}"/> is array-backed, so lookups
/// are a clean binary search but every insert in the middle memmoves the tail, which is <c>O(n)</c>.
/// <c>OrderedDictionary&lt;TKey, TValue&gt;</c> (.NET 9) is <i>insertion</i>-ordered and does not close the
/// gap at all.
/// </para>
/// <para>
/// This type keeps up to <c>31</c> keys per node in contiguous arrays, so the fan-out is <c>32</c> and the
/// same million entries sit about <c>4</c> node visits deep. Within a node the keys are one or two cache
/// lines the prefetcher handles well. In-order enumeration and <see cref="EnumerateRange"/> walk those
/// arrays rather than chasing successor pointers, and allocation drops from one object per entry to one node
/// — its key and value arrays, plus a child array when it is internal — per <c>31</c> entries.
/// </para>
/// <para>
/// The documented BCL-beating workload is a large ordered map under an <b>interleaved insert + lookup +
/// in-order range-scan</b> load: time-series keyed by timestamp, order books, LSM-style memtables — the
/// mixed pattern where <see cref="SortedDictionary{TKey, TValue}"/> pays the pointer chase and
/// <see cref="SortedList{TKey, TValue}"/> pays the <c>O(n)</c> shift. Where it does <i>not</i> win: small maps
/// (at a thousand entries the red-black tree's shallower-per-node work is competitive, and a
/// <see cref="SortedList{TKey, TValue}"/> of a few dozen entries is hard to beat); a delete-dominated load,
/// where rebalancing by borrow/merge measures a few percent behind
/// <see cref="SortedDictionary{TKey, TValue}"/>'s rotations; and any workload that never needs order — a hash
/// table answers those in <c>O(1)</c>, so reach for
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/> instead.
/// </para>
/// <para>
/// Unlike <see cref="SortedDictionary{TKey, TValue}"/>, a <c>null</c> key is legal:
/// <see cref="Comparer{T}.Default"/> orders <c>null</c> before every non-<c>null</c> key (a custom
/// <typeparamref name="TComparer"/> that rejects <c>null</c> overrides that). There is no out-of-band
/// <c>default(TKey)</c> slot as in the hash-based family — a value-type <c>default(TKey)</c> is an ordinary
/// key that sorts wherever the comparer puts it, so <c>0</c> follows every negative <see cref="int"/> rather
/// than coming first. This type is not thread-safe; concurrent callers must synchronize externally.
/// </para>
/// </remarks>
public class BTreeDictionary<TKey, TValue, TComparer>
    : IDictionary<TKey, TValue?>, IReadOnlyDictionary<TKey, TValue?>
    where TComparer : struct, IComparer<TKey>
{
    /// <summary>
    /// The B-tree minimum degree <c>t</c>. Every node except the root holds between <c>t - 1</c> and
    /// <c>2t - 1</c> keys, so the fan-out is up to <c>2t</c> children.
    /// </summary>
    /// <remarks>
    /// <c>t = 16</c> puts up to 31 keys in a node. For the 4- and 8-byte keys that dominate ordered-map use
    /// (<see cref="int"/>, <see cref="long"/>, <see cref="DateTime"/> ticks, object references) that is
    /// 124–248 bytes — two to four 64-byte cache lines, which the hardware prefetcher streams while the
    /// binary search inside the node is still running. Going wider flattens the tree further but makes each
    /// node's scan and each split/merge memmove longer; going narrower (t = 4, say) costs extra levels and
    /// therefore extra dependent loads, which is the very thing the B-tree exists to avoid. It also keeps a
    /// node's three arrays comfortably below the 85 KB large-object-heap threshold for any realistic key and
    /// value type.
    /// </remarks>
    private const int MinDegree = 16;

    /// <summary>The maximum number of keys a node may hold in steady state (<c>2t - 1</c>).</summary>
    private const int MaxKeys = (2 * MinDegree) - 1;

    /// <summary>The minimum number of keys every non-root node holds (<c>t - 1</c>).</summary>
    private const int MinKeys = MinDegree - 1;

    // Node arrays carry one slot of slack so a node can transiently hold MaxKeys + 1 keys after an insert,
    // before the parent (or the root fix-up) splits it. That is what lets insertion split bottom-up — only
    // when an entry was actually added — instead of preemptively splitting full nodes on the way down and
    // restructuring the tree even when the key turns out to already be present.
    private const int NodeKeyCapacity = MaxKeys + 1;
    private const int NodeChildCapacity = MaxKeys + 2;

    // Enumerator path-stack depth. Every non-root node has at least MinDegree children, so a tree of height h
    // holds at least 2 * MinDegree^(h-1) * MinKeys entries; at h = 8 that already exceeds int.MaxValue, which
    // Count could never reach. 16 frames is therefore double the reachable maximum.
    private const int MaxDepth = 16;

    // A B-tree node. Keys/Values are parallel and kept sorted; Children is null for a leaf and otherwise
    // holds Count + 1 subtrees, with Children[i] holding every key between Keys[i-1] and Keys[i].
    private sealed class Node
    {
        internal readonly TKey[] Keys = new TKey[NodeKeyCapacity];
        internal readonly TValue?[] Values = new TValue?[NodeKeyCapacity];
        internal Node?[]? Children;
        internal int Count;

        internal bool IsLeaf
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Children is null;
        }
    }

    // Null until the first insert, so an empty dictionary owns no node arrays at all.
    private Node? _root;

    // Deliberately not `readonly`: calling an interface method through a readonly field of an unconstrained
    // struct type forces the JIT to make a defensive copy per call, which is exactly the cost this type's
    // struct-comparer design exists to remove.
    private TComparer _comparer;

    private int _count;

    // Bumped on every mutation that changes the observable content, so active enumerators detect concurrent
    // modification. A lookup, a failed TryAdd, and a Remove of an absent key are not mutations.
    private int _version;

    /// <summary>Initializes a new, empty dictionary ordered by <c>default(TComparer)</c>.</summary>
    public BTreeDictionary()
        : this(default(TComparer))
    {
    }

    /// <summary>
    /// Initializes a new, empty dictionary ordered by <paramref name="comparer"/>. Use this overload when
    /// <typeparamref name="TComparer"/> carries state (a culture, a sort direction, a key selector).
    /// </summary>
    /// <param name="comparer">The comparer instance defining the key order.</param>
    public BTreeDictionary(TComparer comparer)
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
    public BTreeDictionary(IEnumerable<KeyValuePair<TKey, TValue>> source)
        : this(source, default)
    {
    }

    /// <summary>
    /// Initializes a new dictionary ordered by <paramref name="comparer"/> and seeded with
    /// <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The entries to insert. Enumeration order does not affect the result.</param>
    /// <param name="comparer">The comparer instance defining the key order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> contains a duplicate key.</exception>
    public BTreeDictionary(IEnumerable<KeyValuePair<TKey, TValue>> source, TComparer comparer)
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
    /// Gets or sets the value associated with <paramref name="key"/>. The setter inserts the entry when the
    /// key is absent and overwrites the value when it is present.
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

    /// <summary>
    /// Gets the entry with the smallest key, in <c>O(log n)</c>.
    /// </summary>
    /// <returns>The first entry in key order.</returns>
    /// <exception cref="InvalidOperationException">The dictionary is empty.</exception>
    public KeyValuePair<TKey, TValue?> Min =>
        TryGetMin(out KeyValuePair<TKey, TValue?> entry)
            ? entry
            : throw new InvalidOperationException("The dictionary is empty.");

    /// <summary>
    /// Gets the entry with the largest key, in <c>O(log n)</c>.
    /// </summary>
    /// <returns>The last entry in key order.</returns>
    /// <exception cref="InvalidOperationException">The dictionary is empty.</exception>
    public KeyValuePair<TKey, TValue?> Max =>
        TryGetMax(out KeyValuePair<TKey, TValue?> entry)
            ? entry
            : throw new InvalidOperationException("The dictionary is empty.");

    /// <summary>
    /// Adds an entry, throwing when <paramref name="key"/> is already present.
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
    /// Adds an entry only when <paramref name="key"/> is absent. The non-throwing counterpart of
    /// <see cref="Add"/>; an existing entry is left untouched.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="value">The value to associate with <paramref name="key"/>.</param>
    /// <returns><c>true</c> if the entry was added; <c>false</c> if the key was already present.</returns>
    public bool TryAdd(TKey key, TValue? value) => Insert(key, value, overwrite: false);

    /// <summary>
    /// Looks up <paramref name="key"/>, in <c>O(log n)</c>.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">The associated value, or <c>default</c> when the key is absent.</param>
    /// <returns><c>true</c> if the key was found.</returns>
    public bool TryGetValue(TKey key, out TValue? value)
    {
        Node? node = _root;
        while (node is not null)
        {
            int index = Find(node, key, out bool found);
            if (found)
            {
                value = node.Values[index];
                return true;
            }

            node = node.IsLeaf ? null : node.Children![index];
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
    /// <see cref="EqualityComparer{T}.Default"/>. This is an <c>O(n)</c> scan — the tree is indexed by key,
    /// not by value.
    /// </summary>
    /// <param name="value">The value to look for.</param>
    /// <returns><c>true</c> if at least one entry holds the value.</returns>
    public bool ContainsValue(TValue? value)
    {
        EqualityComparer<TValue?> comparer = EqualityComparer<TValue?>.Default;
        foreach (KeyValuePair<TKey, TValue?> entry in this)
        {
            if (comparer.Equals(entry.Value, value))
                return true;
        }

        return false;
    }

    /// <summary>Removes the entry with <paramref name="key"/>, in <c>O(log n)</c>.</summary>
    /// <param name="key">The key to remove.</param>
    /// <returns><c>true</c> if an entry was removed; <c>false</c> if the key was absent.</returns>
    public bool Remove(TKey key) => Remove(key, out _);

    /// <summary>
    /// Removes the entry with <paramref name="key"/> and returns the value it held.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <param name="value">The removed value, or <c>default</c> when the key was absent.</param>
    /// <returns><c>true</c> if an entry was removed; <c>false</c> if the key was absent.</returns>
    public bool Remove(TKey key, out TValue? value)
    {
        if (_root is null)
        {
            value = default;
            return false;
        }

        if (!RemoveFrom(_root, key, out value))
            return false;

        // The root is the one node allowed to fall below MinKeys. When it empties it is either dropped
        // (the tree is now empty) or replaced by its only child, which is how the tree loses a level.
        if (_root.Count == 0)
            _root = _root.IsLeaf ? null : _root.Children![0];

        _count--;
        _version++;
        return true;
    }

    /// <summary>Removes every entry. The tree releases all of its nodes.</summary>
    public void Clear()
    {
        if (_count == 0 && _root is null)
            return;

        _root = null;
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Gets the entry with the smallest key.
    /// </summary>
    /// <param name="entry">The first entry in key order, or <c>default</c> when the dictionary is empty.</param>
    /// <returns><c>true</c> if the dictionary is non-empty.</returns>
    public bool TryGetMin(out KeyValuePair<TKey, TValue?> entry)
    {
        Node? node = _root;
        if (node is null)
        {
            entry = default;
            return false;
        }

        while (!node.IsLeaf)
            node = node.Children![0]!;

        entry = new KeyValuePair<TKey, TValue?>(node.Keys[0], node.Values[0]);
        return true;
    }

    /// <summary>
    /// Gets the entry with the largest key.
    /// </summary>
    /// <param name="entry">The last entry in key order, or <c>default</c> when the dictionary is empty.</param>
    /// <returns><c>true</c> if the dictionary is non-empty.</returns>
    public bool TryGetMax(out KeyValuePair<TKey, TValue?> entry)
    {
        Node? node = _root;
        if (node is null)
        {
            entry = default;
            return false;
        }

        while (!node.IsLeaf)
            node = node.Children![node.Count]!;

        entry = new KeyValuePair<TKey, TValue?>(node.Keys[node.Count - 1], node.Values[node.Count - 1]);
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
        Node? node = _root;
        bool haveCandidate = false;
        entry = default;

        while (node is not null)
        {
            int index = Find(node, key, out bool found);
            if (found)
            {
                entry = new KeyValuePair<TKey, TValue?>(node.Keys[index], node.Values[index]);
                return true;
            }

            // Keys[index] is this node's first key above `key`; anything the search finds deeper is smaller
            // still, so each level can only improve the candidate.
            if (index < node.Count)
            {
                entry = new KeyValuePair<TKey, TValue?>(node.Keys[index], node.Values[index]);
                haveCandidate = true;
            }

            node = node.IsLeaf ? null : node.Children![index];
        }

        return haveCandidate;
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
        Node? node = _root;
        bool haveCandidate = false;
        entry = default;

        while (node is not null)
        {
            int index = Find(node, key, out bool found);

            // On an exact hit the bound lies strictly to the right of the matched key, so step past it: the
            // subtree between Keys[index] and Keys[index + 1] holds only larger keys.
            if (found)
                index++;

            if (index < node.Count)
            {
                entry = new KeyValuePair<TKey, TValue?>(node.Keys[index], node.Values[index]);
                haveCandidate = true;
            }

            node = node.IsLeaf ? null : node.Children![index];
        }

        return haveCandidate;
    }

    /// <summary>
    /// Enumerates, in ascending key order, every entry whose key lies in the half-open range
    /// <c>[fromInclusive, toExclusive)</c>. The scan seeks to the lower bound in <c>O(log n)</c> and then
    /// walks the tree's contiguous node arrays, so it costs <c>O(log n + k)</c> for <c>k</c> results rather
    /// than a full <c>O(n)</c> filter.
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
    /// Returns a struct enumerator over the entries in ascending key order. Because it is a struct, iterating
    /// it via <c>foreach</c> allocates nothing — the traversal path is held in an inline buffer.
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
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
    public void CopyTo(KeyValuePair<TKey, TValue?>[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        if (arrayIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex,
                "Array index must be non-negative.");
        if (array.Length - arrayIndex < _count)
            throw new ArgumentException(
                "The destination array has insufficient space for the entries.", nameof(array));

        int i = arrayIndex;
        foreach (KeyValuePair<TKey, TValue?> entry in this)
            array[i++] = entry;
    }

    // ── IDictionary / IReadOnlyDictionary explicit members ────────────────────
    // The primary surface stays strongly typed (KeyCollection / ValueCollection are structs, so the public
    // Keys/Values views allocate nothing); these adapters exist so a BTreeDictionary can be consumed through
    // the BCL interfaces, boxing the view only when someone actually asks for the interface-typed one.

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

    // Binary search for `key` among a node's Count keys. Returns the index of the match when `found`, and
    // otherwise the index of the first key greater than `key` — which doubles as the index of the child to
    // descend into. Binary search rather than a linear scan: at 31 keys it is 5 comparisons instead of up to
    // 31, and the whole key array is already in cache by the time the second probe issues.
    private int Find(Node node, TKey key, out bool found)
    {
        TKey[] keys = node.Keys;
        int lo = 0;
        int hi = node.Count - 1;

        while (lo <= hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            int cmp = _comparer.Compare(keys[mid], key);
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
    // already present (in which case `overwrite` decides whether its value was replaced).
    private bool Insert(TKey key, TValue? value, bool overwrite)
    {
        if (_root is null)
        {
            Node leaf = new Node();
            leaf.Keys[0] = key;
            leaf.Values[0] = value;
            leaf.Count = 1;
            _root = leaf;
            _count = 1;
            _version++;
            return true;
        }

        InsertOutcome outcome = InsertInto(_root, key, value, overwrite);

        // The root split: the tree grows by one level, and the promoted median becomes the new root's only
        // key. This is the only place the tree gets taller.
        if (outcome.SplitRight is not null)
        {
            Node newRoot = new Node { Children = new Node?[NodeChildCapacity], Count = 1 };
            newRoot.Keys[0] = outcome.SplitKey;
            newRoot.Values[0] = outcome.SplitValue;
            newRoot.Children![0] = _root;
            newRoot.Children[1] = outcome.SplitRight;
            _root = newRoot;
        }

        // An in-place value overwrite is not a structural change, so — matching BCL
        // Dictionary<TKey, TValue> and the rest of the family (issue #233) — it must not invalidate active
        // enumerators. Only a genuinely new entry bumps the version.
        if (!outcome.Added)
            return false;

        _count++;
        _version++;
        return true;
    }

    // What a subtree insertion reports back to its parent.
    private struct InsertOutcome
    {
        public bool Added;          // a new entry was created somewhere in this subtree
        public Node? SplitRight;    // non-null when this node overflowed and split
        public TKey SplitKey;       // the median promoted by that split
        public TValue? SplitValue;
    }

    // Bottom-up insertion: descend to the leaf, insert there, and let overflow propagate back up one level at
    // a time. Nodes carry a spare slot (NodeKeyCapacity == MaxKeys + 1) so an overfull node is legal until its
    // parent splits it on the way out. The alternative — splitting full nodes preemptively on the way down —
    // would restructure the tree even when the insert turns out to be a duplicate.
    private InsertOutcome InsertInto(Node node, TKey key, TValue? value, bool overwrite)
    {
        int index = Find(node, key, out bool found);
        if (found)
        {
            if (overwrite)
                node.Values[index] = value;

            return default;
        }

        if (node.IsLeaf)
        {
            InsertIntoNode(node, index, key, value, null);
        }
        else
        {
            InsertOutcome inner = InsertInto(node.Children![index]!, key, value, overwrite);
            if (!inner.Added)
                return inner;
            if (inner.SplitRight is null)
                return inner;

            InsertIntoNode(node, index, inner.SplitKey, inner.SplitValue, inner.SplitRight);
        }

        if (node.Count <= MaxKeys)
            return new InsertOutcome { Added = true };

        SplitNode(node, out TKey medianKey, out TValue? medianValue, out Node right);
        return new InsertOutcome
        {
            Added = true,
            SplitKey = medianKey,
            SplitValue = medianValue,
            SplitRight = right,
        };
    }

    // Inserts (key, value) at `index`, shifting the tail right. `rightChild`, when supplied, is the right
    // half of a split child and lands at Children[index + 1].
    private static void InsertIntoNode(Node node, int index, TKey key, TValue? value, Node? rightChild)
    {
        int count = node.Count;
        Array.Copy(node.Keys, index, node.Keys, index + 1, count - index);
        Array.Copy(node.Values, index, node.Values, index + 1, count - index);
        node.Keys[index] = key;
        node.Values[index] = value;

        if (rightChild is not null)
        {
            Node?[] children = node.Children!;
            Array.Copy(children, index + 1, children, index + 2, count - index);
            children[index + 1] = rightChild;
        }

        node.Count = count + 1;
    }

    // Splits an overfull node in two, handing its median back to the caller for promotion. The left half stays
    // in `node`; the right half is a fresh sibling.
    private static void SplitNode(Node node, out TKey medianKey, out TValue? medianValue, out Node right)
    {
        int count = node.Count;
        int median = count / 2;
        int rightCount = count - median - 1;

        medianKey = node.Keys[median];
        medianValue = node.Values[median];

        right = new Node { Count = rightCount };
        Array.Copy(node.Keys, median + 1, right.Keys, 0, rightCount);
        Array.Copy(node.Values, median + 1, right.Values, 0, rightCount);

        if (!node.IsLeaf)
        {
            right.Children = new Node?[NodeChildCapacity];
            Array.Copy(node.Children!, median + 1, right.Children, 0, rightCount + 1);
            Array.Clear(node.Children!, median + 1, rightCount + 1);
        }

        // Clearing the vacated slots matters for reference-typed keys and values: a stale slot would otherwise
        // keep an evicted object alive for as long as the node lives.
        Array.Clear(node.Keys, median, count - median);
        Array.Clear(node.Values, median, count - median);
        node.Count = median;
    }

    // Removes `key` from the subtree rooted at `node`, which the caller guarantees is either the root or holds
    // more than MinKeys keys — the invariant that lets a deletion never have to back up the tree.
    private bool RemoveFrom(Node node, TKey key, out TValue? value)
    {
        int index = Find(node, key, out bool found);

        if (found)
        {
            value = node.Values[index];

            if (node.IsLeaf)
                RemoveFromLeaf(node, index);
            else
                RemoveFromInternal(node, index);

            return true;
        }

        if (node.IsLeaf)
        {
            value = default;
            return false;
        }

        // Top the child up to more than MinKeys *before* descending, so the removal below never needs to
        // rebalance upwards.
        if (node.Children![index]!.Count == MinKeys)
        {
            Fill(node, index);

            // A merge with the left sibling folds the target child into index - 1 and shortens the parent, so
            // the index can now point past the last child. Only the rightmost position can end up there.
            if (index > node.Count)
                index--;
        }

        return RemoveFrom(node.Children![index]!, key, out value);
    }

    private static void RemoveFromLeaf(Node node, int index)
    {
        int last = node.Count - 1;
        Array.Copy(node.Keys, index + 1, node.Keys, index, last - index);
        Array.Copy(node.Values, index + 1, node.Values, index, last - index);
        node.Keys[last] = default!;
        node.Values[last] = default;
        node.Count = last;
    }

    // Deletes Keys[index] from an internal node by pulling up its in-order predecessor or successor — or, when
    // both flanking children are at the minimum, by merging them and deleting from the merged node.
    private void RemoveFromInternal(Node node, int index)
    {
        Node left = node.Children![index]!;
        Node right = node.Children[index + 1]!;

        if (left.Count > MinKeys)
        {
            Node cursor = left;
            while (!cursor.IsLeaf)
                cursor = cursor.Children![cursor.Count]!;

            TKey predecessor = cursor.Keys[cursor.Count - 1];
            node.Keys[index] = predecessor;
            node.Values[index] = cursor.Values[cursor.Count - 1];
            RemoveFrom(left, predecessor, out _);
        }
        else if (right.Count > MinKeys)
        {
            Node cursor = right;
            while (!cursor.IsLeaf)
                cursor = cursor.Children![0]!;

            TKey successor = cursor.Keys[0];
            node.Keys[index] = successor;
            node.Values[index] = cursor.Values[0];
            RemoveFrom(right, successor, out _);
        }
        else
        {
            // Both are minimal: fold key + right into left (2 * MinKeys + 1 == MaxKeys keys, so it still fits)
            // and delete from there.
            TKey key = node.Keys[index];
            Merge(node, index);
            RemoveFrom(left, key, out _);
        }
    }

    // Grows Children[index] past MinKeys by borrowing one key from a sibling, or by merging with one.
    private static void Fill(Node node, int index)
    {
        if (index > 0 && node.Children![index - 1]!.Count > MinKeys)
            BorrowFromPrevious(node, index);
        else if (index < node.Count && node.Children![index + 1]!.Count > MinKeys)
            BorrowFromNext(node, index);
        else if (index < node.Count)
            Merge(node, index);
        else
            Merge(node, index - 1);
    }

    // Rotates right: the parent's separator drops into the child's front, and the left sibling's last key
    // takes the parent's place.
    private static void BorrowFromPrevious(Node node, int index)
    {
        Node child = node.Children![index]!;
        Node sibling = node.Children[index - 1]!;

        Array.Copy(child.Keys, 0, child.Keys, 1, child.Count);
        Array.Copy(child.Values, 0, child.Values, 1, child.Count);
        child.Keys[0] = node.Keys[index - 1];
        child.Values[0] = node.Values[index - 1];

        if (!child.IsLeaf)
        {
            Array.Copy(child.Children!, 0, child.Children!, 1, child.Count + 1);
            child.Children![0] = sibling.Children![sibling.Count];
            sibling.Children[sibling.Count] = null;
        }

        node.Keys[index - 1] = sibling.Keys[sibling.Count - 1];
        node.Values[index - 1] = sibling.Values[sibling.Count - 1];
        sibling.Keys[sibling.Count - 1] = default!;
        sibling.Values[sibling.Count - 1] = default;

        child.Count++;
        sibling.Count--;
    }

    // Rotates left: the parent's separator is appended to the child, and the right sibling's first key takes
    // the parent's place.
    private static void BorrowFromNext(Node node, int index)
    {
        Node child = node.Children![index]!;
        Node sibling = node.Children[index + 1]!;

        child.Keys[child.Count] = node.Keys[index];
        child.Values[child.Count] = node.Values[index];

        if (!child.IsLeaf)
        {
            child.Children![child.Count + 1] = sibling.Children![0];
            Array.Copy(sibling.Children!, 1, sibling.Children!, 0, sibling.Count);
            sibling.Children[sibling.Count] = null;
        }

        node.Keys[index] = sibling.Keys[0];
        node.Values[index] = sibling.Values[0];

        Array.Copy(sibling.Keys, 1, sibling.Keys, 0, sibling.Count - 1);
        Array.Copy(sibling.Values, 1, sibling.Values, 0, sibling.Count - 1);
        sibling.Keys[sibling.Count - 1] = default!;
        sibling.Values[sibling.Count - 1] = default;

        child.Count++;
        sibling.Count--;
    }

    // Folds Children[index + 1] and the separator Keys[index] into Children[index], shrinking the parent by
    // one key and one child.
    private static void Merge(Node node, int index)
    {
        Node?[] children = node.Children!;
        Node left = children[index]!;
        Node right = children[index + 1]!;

        left.Keys[left.Count] = node.Keys[index];
        left.Values[left.Count] = node.Values[index];
        Array.Copy(right.Keys, 0, left.Keys, left.Count + 1, right.Count);
        Array.Copy(right.Values, 0, left.Values, left.Count + 1, right.Count);

        if (!left.IsLeaf)
            Array.Copy(right.Children!, 0, left.Children!, left.Count + 1, right.Count + 1);

        left.Count += right.Count + 1;

        int lastKey = node.Count - 1;
        Array.Copy(node.Keys, index + 1, node.Keys, index, lastKey - index);
        Array.Copy(node.Values, index + 1, node.Values, index, lastKey - index);
        Array.Copy(children, index + 2, children, index + 1, lastKey - index);
        node.Keys[lastKey] = default!;
        node.Values[lastKey] = default;
        children[node.Count] = null;
        node.Count = lastKey;
    }

    // The traversal path of an in-order walk: one (node, next key index) frame per level. Held inline in the
    // enumerator structs so a foreach over a B-tree allocates nothing.
    [InlineArray(MaxDepth)]
    private struct NodeBuffer
    {
        private Node? _element0;
    }

    [InlineArray(MaxDepth)]
    private struct IndexBuffer
    {
        private int _element0;
    }

    // The shared in-order cursor behind both enumerators. `Seek` decides where the walk starts: the leftmost
    // leaf for a full enumeration, or the lower bound of a key for a range scan.
    private struct Cursor
    {
        private NodeBuffer _nodes;
        private IndexBuffer _indices;
        private int _depth;

        internal void PushLeftmost(Node? node)
        {
            while (node is not null)
            {
                _nodes[_depth] = node;
                _indices[_depth] = 0;
                _depth++;
                node = node.IsLeaf ? null : node.Children![0];
            }
        }

        // Seeds the path at the first key >= `key`, skipping the subtrees that lie entirely below it.
        internal void SeekLowerBound(Node? node, TKey key, BTreeDictionary<TKey, TValue, TComparer> owner)
        {
            while (node is not null)
            {
                int index = owner.Find(node, key, out bool found);
                _nodes[_depth] = node;
                _indices[_depth] = index;
                _depth++;

                if (found || node.IsLeaf)
                    return;

                node = node.Children![index];
            }
        }

        internal void Reset() => _depth = 0;

        // Advances to the next entry in key order, or reports exhaustion.
        internal bool MoveNext(out TKey key, out TValue? value)
        {
            while (_depth > 0)
            {
                Node node = _nodes[_depth - 1]!;
                int index = _indices[_depth - 1];

                if (index < node.Count)
                {
                    key = node.Keys[index];
                    value = node.Values[index];
                    _indices[_depth - 1] = index + 1;

                    // Everything between this key and the next one lives in the child to its right.
                    if (!node.IsLeaf)
                        PushLeftmost(node.Children![index + 1]);

                    return true;
                }

                _depth--;
            }

            key = default!;
            value = default;
            return false;
        }
    }

    /// <summary>
    /// A struct enumerator over a <see cref="BTreeDictionary{TKey, TValue, TComparer}"/>'s entries in
    /// ascending key order. Because it is a struct and holds its traversal path inline, iterating it via
    /// <c>foreach</c> allocates nothing.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue?>>
    {
        private readonly BTreeDictionary<TKey, TValue, TComparer> _dict;
        private readonly int _version;
        private Cursor _cursor;
        private KeyValuePair<TKey, TValue?> _current;

        internal Enumerator(BTreeDictionary<TKey, TValue, TComparer> dict)
        {
            _dict = dict;
            _version = dict._version;
            _cursor = default;
            _current = default;
            _cursor.PushLeftmost(dict._root);
        }

        /// <summary>Gets the entry at the current position of the enumerator.</summary>
        public readonly KeyValuePair<TKey, TValue?> Current => _current;

        readonly object? IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next entry in key order.</summary>
        /// <returns><c>true</c> if there is a next entry; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The dictionary was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (_version != _dict._version)
                throw new InvalidOperationException("The dictionary was modified during enumeration.");

            if (!_cursor.MoveNext(out TKey key, out TValue? value))
            {
                _current = default;
                return false;
            }

            _current = new KeyValuePair<TKey, TValue?>(key, value);
            return true;
        }

        /// <summary>Resets the enumerator to before the first entry.</summary>
        /// <exception cref="InvalidOperationException">The dictionary was modified during enumeration.</exception>
        public void Reset()
        {
            if (_version != _dict._version)
                throw new InvalidOperationException("The dictionary was modified during enumeration.");

            _cursor.Reset();
            _cursor.PushLeftmost(_dict._root);
            _current = default;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }

    /// <summary>
    /// The result of <see cref="EnumerateRange"/>: an allocation-free view over the entries of one key range,
    /// in ascending order.
    /// </summary>
    public readonly struct RangeEnumerable : IEnumerable<KeyValuePair<TKey, TValue?>>
    {
        private readonly BTreeDictionary<TKey, TValue, TComparer> _dict;
        private readonly TKey _from;
        private readonly TKey _toExclusive;

        internal RangeEnumerable(BTreeDictionary<TKey, TValue, TComparer> dict, TKey from, TKey toExclusive)
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
    /// <see cref="BTreeDictionary{TKey, TValue, TComparer}"/>, in ascending key order.
    /// </summary>
    public struct RangeEnumerator : IEnumerator<KeyValuePair<TKey, TValue?>>
    {
        private readonly BTreeDictionary<TKey, TValue, TComparer> _dict;
        private readonly TKey _from;
        private readonly TKey _toExclusive;
        private readonly int _version;
        private Cursor _cursor;
        private KeyValuePair<TKey, TValue?> _current;
        private bool _finished;

        internal RangeEnumerator(BTreeDictionary<TKey, TValue, TComparer> dict, TKey from, TKey toExclusive)
        {
            _dict = dict;
            _from = from;
            _toExclusive = toExclusive;
            _version = dict._version;
            _cursor = default;
            _current = default;
            _finished = false;
            _cursor.SeekLowerBound(dict._root, from, dict);
        }

        /// <summary>Gets the entry at the current position of the enumerator.</summary>
        public readonly KeyValuePair<TKey, TValue?> Current => _current;

        readonly object? IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next entry in the range.</summary>
        /// <returns><c>true</c> if there is a next entry; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The dictionary was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (_version != _dict._version)
                throw new InvalidOperationException("The dictionary was modified during enumeration.");

            if (_finished || !_cursor.MoveNext(out TKey key, out TValue? value))
            {
                _current = default;
                return false;
            }

            // The walk is ascending, so the first key at or past the upper bound ends the scan for good.
            if (_dict._comparer.Compare(key, _toExclusive) >= 0)
            {
                _finished = true;
                _current = default;
                return false;
            }

            _current = new KeyValuePair<TKey, TValue?>(key, value);
            return true;
        }

        /// <summary>Resets the enumerator to before the first entry of the range.</summary>
        /// <exception cref="InvalidOperationException">The dictionary was modified during enumeration.</exception>
        public void Reset()
        {
            if (_version != _dict._version)
                throw new InvalidOperationException("The dictionary was modified during enumeration.");

            _cursor.Reset();
            _cursor.SeekLowerBound(_dict._root, _from, _dict);
            _current = default;
            _finished = false;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }

    /// <summary>
    /// A view over a <see cref="BTreeDictionary{TKey, TValue, TComparer}"/>'s keys, in ascending order. It is
    /// a read-only <see cref="ICollection{T}"/>: the mutating members throw
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    public readonly struct KeyCollection : ICollection<TKey>
    {
        private readonly BTreeDictionary<TKey, TValue, TComparer> _dict;

        internal KeyCollection(BTreeDictionary<TKey, TValue, TComparer> dict) => _dict = dict;

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
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is negative.</exception>
        /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
        public void CopyTo(TKey[] array, int arrayIndex)
        {
            ArgumentNullException.ThrowIfNull(array);
            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex,
                    "Array index must be non-negative.");
            if (array.Length - arrayIndex < _dict._count)
                throw new ArgumentException(
                    "The destination array has insufficient space for the keys.", nameof(array));

            int i = arrayIndex;
            foreach (KeyValuePair<TKey, TValue?> entry in _dict)
                array[i++] = entry.Key;
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
        /// A struct enumerator over the keys of a <see cref="BTreeDictionary{TKey, TValue, TComparer}"/>, in
        /// ascending order.
        /// </summary>
        public struct Enumerator : IEnumerator<TKey>
        {
            private BTreeDictionary<TKey, TValue, TComparer>.Enumerator _inner;

            internal Enumerator(BTreeDictionary<TKey, TValue, TComparer> dict) => _inner = dict.GetEnumerator();

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
    /// A view over a <see cref="BTreeDictionary{TKey, TValue, TComparer}"/>'s values, ordered by their keys.
    /// It is a read-only <see cref="ICollection{T}"/>: the mutating members throw
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    public readonly struct ValueCollection : ICollection<TValue?>
    {
        private readonly BTreeDictionary<TKey, TValue, TComparer> _dict;

        internal ValueCollection(BTreeDictionary<TKey, TValue, TComparer> dict) => _dict = dict;

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
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is negative.</exception>
        /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
        public void CopyTo(TValue?[] array, int arrayIndex)
        {
            ArgumentNullException.ThrowIfNull(array);
            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex,
                    "Array index must be non-negative.");
            if (array.Length - arrayIndex < _dict._count)
                throw new ArgumentException(
                    "The destination array has insufficient space for the values.", nameof(array));

            int i = arrayIndex;
            foreach (KeyValuePair<TKey, TValue?> entry in _dict)
                array[i++] = entry.Value;
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
        /// A struct enumerator over the values of a <see cref="BTreeDictionary{TKey, TValue, TComparer}"/>, in
        /// key order.
        /// </summary>
        public struct Enumerator : IEnumerator<TValue?>
        {
            private BTreeDictionary<TKey, TValue, TComparer>.Enumerator _inner;

            internal Enumerator(BTreeDictionary<TKey, TValue, TComparer> dict) => _inner = dict.GetEnumerator();

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
