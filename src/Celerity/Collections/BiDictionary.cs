using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Celerity.Hashing;
using Celerity.Primitives;

namespace Celerity.Collections;

/// <summary>
/// A one-to-one hash dictionary that can be looked up by key and by value: every key maps to exactly
/// one value and every value belongs to exactly one key, so <see cref="ContainsValue"/>,
/// <see cref="TryGetKey"/> and <see cref="RemoveValue(TValue)"/> are <c>O(1)</c>, like their key-side
/// counterparts. <see cref="Inverse"/> exposes the same table as a read-only
/// <see cref="IReadOnlyDictionary{TValue, TKey}"/>. This is the shape of Guava's <c>HashBiMap</c> and
/// Apache Commons' <c>BidiMap</c>.
/// </summary>
/// <remarks>
/// <para>
/// .NET has no bidirectional dictionary. The usual hand-roll is a pair of
/// <see cref="Dictionary{TKey, TValue}"/> instances, one per direction, kept in sync by the caller. Every
/// write has to touch both, an overwrite has to remove the stale inverse entry first, and a second
/// <c>Add</c> that throws on a duplicate value leaves the two maps disagreeing unless the caller rolls
/// the first one back. This type does that bookkeeping inside one table, and rejects a write that would
/// give a value a second key.
/// </para>
/// <para>
/// Entries live in one dense array of structs, each holding the key, the value, and one link into each
/// of two hash-chain families: a key-bucket array chains entries by key hash and a
/// value-bucket array chains them by value hash. Each key and each value is stored once, where the
/// dictionary pair stores both twice, and no hash code is cached, where each dictionary caches one per
/// entry. A removal unlinks the entry from both chains and moves the last
/// entry into the hole, so the array never holds a gap and enumeration is a straight scan.
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Wins</b> wherever both directions are read: id ↔ name tables, protocol code ↔ enum maps,
/// handle ↔ object registries, column name ↔ ordinal maps. It stays consistent by construction and
/// retains less memory than the dictionary pair.
/// </description></item>
/// <item><description>
/// <b>Costs</b> a value-side hash and chain walk on every write. Because no hash is cached, a removal
/// re-hashes the removed value (or key) and the entry that fills the hole, and growth that adds
/// buckets re-hashes every entry, so a key or value type with an expensive hasher pays more for churn
/// than a dictionary does. When only the forward direction is read, use
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/> or
/// <see cref="SwissDictionary{TKey, TValue, THasher}"/>.
/// </description></item>
/// </list>
/// <para>
/// Enumeration follows insertion order until the first removal. A removal moves the newest entry into
/// the removed entry's place, so that entry is enumerated there from then on. When the order must
/// survive removals, use <see cref="LinkedDictionary{TKey, TValue, THasher}"/>.
/// </para>
/// <para>
/// The default key and the default value (<c>0</c>, a <see langword="null"/> reference, or a
/// <see cref="Nullable{T}"/> with no value) are stored in the table like any other, and are never
/// handed to a hasher: each is given hash code <c>0</c> directly, so a hasher that rejects
/// <see langword="null"/> is safe here too. Keys are compared with
/// <see cref="EqualityComparer{T}.Default"/> for <typeparamref name="TKey"/>, values with the one for
/// <typeparamref name="TValue"/>; each hasher must agree with its comparer.
/// </para>
/// </remarks>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TValue">The type of the values. Values are unique, like keys.</typeparam>
/// <typeparam name="TKeyHasher">
/// The hasher used to compute key hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
/// <typeparam name="TValueHasher">
/// The hasher used to compute value hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class BiDictionary<TKey, TValue, TKeyHasher, TValueHasher>
    : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
    where TKeyHasher : struct, IHashProvider<TKey>
    where TValueHasher : struct, IHashProvider<TValue>
{
    /// <summary>
    /// The default initial capacity of the dictionary if no capacity is specified.
    /// </summary>
    protected const int DefaultCapacity = 16;

    /// <summary>
    /// The default load factor of the dictionary if no load factor is specified.
    /// </summary>
    protected const float DefaultLoadFactor = 0.75f;

    // Sentinel for "no entry" in an index. Links are stored as entry + 1 instead (0 for none), the same
    // encoding as the buckets, so a freshly allocated bucket array already reads as all-empty and a
    // chain can be walked through one `ref int` whether it starts at a bucket or at an entry.
    private const int Nil = -1;

    private struct Entry
    {
        // No hash code is cached: an entry is only the two links and the pair, which is what keeps the
        // table smaller than a dictionary pair. A removal and a re-bucketing resize recompute the hashes they need.
        // The next entry + 1 in the same key bucket's chain and in the same value bucket's chain.
        public int KeyNext;
        public int ValueNext;

        public TKey Key;
        public TValue Value;
    }

    // Both bucket arrays have the same power-of-two length, so one mask serves both. Bucket b holds
    // (first entry of its chain) + 1, or 0 when the bucket is empty.
    private int[] _keyBuckets;
    private int[] _valueBuckets;
    private Entry[] _entries;
    private int _mask;
    private readonly float _loadFactor;

    // Entries [0, _count) are live; there are no gaps.
    private int _count;

    // Incremented on every structural mutation so active enumerators can
    // detect concurrent modification and throw, matching BCL semantics.
    private int _version;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="BiDictionary{TKey, TValue, TKeyHasher, TValueHasher}"/> class using the specified
    /// capacity and load factor.
    /// </summary>
    /// <param name="capacity">
    /// The initial number of hash buckets in each direction, automatically rounded up to the next power
    /// of two. The dictionary holds <c>capacity * loadFactor</c> entries (at least one) before it grows.
    /// </param>
    /// <param name="loadFactor">
    /// The number of entries per bucket the dictionary allows before it grows.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative, or <paramref name="loadFactor"/>
    /// is not in the open interval (0, 1).
    /// </exception>
    public BiDictionary(
        int capacity = DefaultCapacity,
        float loadFactor = DefaultLoadFactor)
        : this(capacity, loadFactor, entryCapacity: 0)
    {
    }

    // `entryCapacity` is the exact entry count to allocate when a caller knows it (a sized source);
    // otherwise the bucket count's load-factor capacity is used.
    private BiDictionary(int capacity, float loadFactor, int entryCapacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (!(loadFactor > 0f && loadFactor < 1f))
            throw new ArgumentOutOfRangeException(nameof(loadFactor), loadFactor, "Load factor must be between 0 (exclusive) and 1 (exclusive).");

        _loadFactor = loadFactor;
        int buckets = FastUtils.NextPowerOfTwo(capacity);
        _keyBuckets = new int[buckets];
        _valueBuckets = new int[buckets];
        _mask = buckets - 1;
        _entries = new Entry[Math.Max(entryCapacity, EntryCapacityFor(buckets, loadFactor))];
    }

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="BiDictionary{TKey, TValue, TKeyHasher, TValueHasher}"/> class that contains the
    /// key/value pairs copied from the specified <paramref name="source"/>.
    /// </summary>
    /// <param name="source">
    /// The collection whose key/value pairs are copied into the new dictionary.
    /// If <paramref name="source"/> implements <see cref="ICollection{T}"/>, its
    /// <c>Count</c> is used to size the backing storage so inserts do not resize.
    /// </param>
    /// <param name="capacity">
    /// The minimum initial number of hash buckets, rounded up to the next power of two. When
    /// the source's count is larger, the table is sized — including
    /// load-factor headroom — to hold the whole source without resizing.
    /// </param>
    /// <param name="loadFactor">
    /// The number of entries per bucket the dictionary allows before it grows.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative, or <paramref name="loadFactor"/>
    /// is not in the open interval (0, 1).
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> contains a duplicate key, or two keys with the same value.
    /// </exception>
    public BiDictionary(
        IEnumerable<KeyValuePair<TKey, TValue>> source,
        int capacity = DefaultCapacity,
        float loadFactor = DefaultLoadFactor)
        : this(
            InitialCapacityForSource(source, capacity, loadFactor),
            loadFactor,
            EntryCapacityForSource(source, capacity, loadFactor))
    {
        foreach (KeyValuePair<TKey, TValue> kvp in source)
        {
            Add(kvp.Key, kvp.Value);
        }
    }

    // Runs as part of the chained-ctor argument expression so the null check
    // beats the primary ctor's capacity / loadFactor validation: a null source
    // must surface as ArgumentNullException, not ArgumentOutOfRangeException
    // when the user also passed an invalid loadFactor. Mirrors the same helper
    // on CelerityDictionary (issue #27).
    private static int InitialCapacityForSource(
        IEnumerable<KeyValuePair<TKey, TValue>> source,
        int capacity,
        float loadFactor)
    {
        ArgumentNullException.ThrowIfNull(source);
        int count = (source as ICollection<KeyValuePair<TKey, TValue>>)?.Count ?? 0;

        if (count > 0 && loadFactor > 0f && loadFactor < 1f)
            count = BucketsFor(count, loadFactor);

        // A negative capacity passes through untouched so the primary ctor rejects it,
        // exactly as the capacity-only overload does (issue #460).
        return capacity < 0 ? capacity : Math.Max(capacity, count);
    }

    // The entry array a sized source needs: exactly its count, so the buckets' load-factor headroom
    // is not paid a second time in entries. Runs after InitialCapacityForSource, which has already
    // rejected a null source; an unsized source, or invalid arguments the ctor is about to reject,
    // leave the default sizing in place.
    private static int EntryCapacityForSource(
        IEnumerable<KeyValuePair<TKey, TValue>> source,
        int capacity,
        float loadFactor)
    {
        int count = (source as ICollection<KeyValuePair<TKey, TValue>>)?.Count ?? 0;
        return capacity >= 0 && loadFactor > 0f && loadFactor < 1f ? count : 0;
    }

    /// <summary>
    /// Gets the number of key/value pairs contained in the dictionary.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets a read-only view of this dictionary in the other direction, mapping each value to its key.
    /// The view is a lightweight struct over the same table: it reflects every later change, and
    /// looking up or iterating it does not allocate.
    /// </summary>
    public InverseView Inverse => new InverseView(this);

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// Throws a <see cref="KeyNotFoundException"/> if the key does not exist on get.
    /// Setting a key that is already present re-points it at the new value, which keeps the key's
    /// place in the enumeration; setting a new key appends it.
    /// </summary>
    /// <param name="key">The key of the value to get or set.</param>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when attempting to get a value for a key that is not found in the dictionary.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown on set when the value already belongs to a different key. The dictionary is unchanged.
    /// Use <see cref="ForceSet"/> to move the value to <paramref name="key"/> instead.
    /// </exception>
    /// <returns>The value associated with the specified key.</returns>
    public TValue this[TKey key]
    {
        get
        {
            int index = FindKey(key, KeyHashOf(key));
            if (index < 0)
                throw new KeyNotFoundException($"Key {key} not found.");

            return _entries[index].Value;
        }
        set
        {
            int keyHash = KeyHashOf(key);
            int valueHash = ValueHashOf(value);
            int keyIndex = FindKey(key, keyHash);
            int valueIndex = FindValue(value, valueHash);
            if (valueIndex >= 0)
            {
                // Already mapped to this very key: nothing to do. Mapped to another key: refuse, so the
                // dictionary stays one-to-one.
                if (valueIndex == keyIndex)
                    return;
                throw new ArgumentException($"The value {value} is already mapped to a different key.", nameof(value));
            }

            if (keyIndex >= 0)
            {
                // Re-pointing an existing key leaves the entry array as it is, so _version is left
                // untouched and active enumerators stay valid, matching BCL Dictionary<,>. See #233.
                Rebind(keyIndex, value, valueHash);
                return;
            }

            AddNew(key, value, keyHash, valueHash);
        }
    }

    /// <summary>
    /// Determines whether the specified key is present in the dictionary.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns><c>true</c> if the key is found; otherwise, <c>false</c>.</returns>
    public bool ContainsKey(TKey key) => FindKey(key, KeyHashOf(key)) >= 0;

    /// <summary>
    /// Determines whether the dictionary contains the specified value. Unlike
    /// <see cref="Dictionary{TKey, TValue}.ContainsValue(TValue)"/>, this is an <c>O(1)</c> hash lookup.
    /// </summary>
    /// <param name="value">The value to locate.</param>
    /// <returns><c>true</c> if some key maps to <paramref name="value"/>; otherwise, <c>false</c>.</returns>
    public bool ContainsValue(TValue value) => FindValue(value, ValueHashOf(value)) >= 0;

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">
    /// When this method returns, contains the value associated with <paramref name="key"/>
    /// if found; otherwise the default value of <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><c>true</c> if the key was found; otherwise, <c>false</c>.</returns>
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        int index = FindKey(key, KeyHashOf(key));
        if (index < 0)
        {
            value = default;
            return false;
        }

        value = _entries[index].Value;
        return true;
    }

    /// <summary>
    /// Attempts to get the key that maps to the specified value. This is an <c>O(1)</c> hash lookup.
    /// </summary>
    /// <param name="value">The value to look up.</param>
    /// <param name="key">
    /// When this method returns, contains the key that maps to <paramref name="value"/>
    /// if found; otherwise the default value of <typeparamref name="TKey"/>.
    /// </param>
    /// <returns><c>true</c> if the value was found; otherwise, <c>false</c>.</returns>
    public bool TryGetKey(TValue value, [MaybeNullWhen(false)] out TKey key)
    {
        int index = FindValue(value, ValueHashOf(value));
        if (index < 0)
        {
            key = default;
            return false;
        }

        key = _entries[index].Key;
        return true;
    }

    /// <summary>
    /// Gets the key that maps to the specified value. This is an <c>O(1)</c> hash lookup.
    /// </summary>
    /// <param name="value">The value to look up.</param>
    /// <returns>The key that maps to <paramref name="value"/>.</returns>
    /// <exception cref="KeyNotFoundException">No key maps to <paramref name="value"/>.</exception>
    public TKey GetKey(TValue value)
    {
        int index = FindValue(value, ValueHashOf(value));
        if (index < 0)
            throw new KeyNotFoundException($"Value {value} not found.");

        return _entries[index].Key;
    }

    /// <summary>
    /// Removes the entry with the specified key from the dictionary.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <returns>
    /// <c>true</c> if the element was successfully removed; otherwise, <c>false</c>.
    /// Also returns <c>false</c> if the key was not found.
    /// </returns>
    public bool Remove(TKey key) => Remove(key, out _);

    /// <summary>
    /// Removes the entry with the specified key from the dictionary and copies
    /// the removed value to the <paramref name="value"/> parameter.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <param name="value">
    /// When this method returns, contains the value that was associated with
    /// <paramref name="key"/> before removal if the key was found; otherwise the
    /// default value of <typeparamref name="TValue"/>.
    /// </param>
    /// <returns>
    /// <c>true</c> if the element was successfully removed; otherwise, <c>false</c>.
    /// Also returns <c>false</c> if the key was not found.
    /// </returns>
    public bool Remove(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        int keyHash = KeyHashOf(key);
        int index = FindKey(key, keyHash);
        if (index < 0)
        {
            value = default;
            return false;
        }

        value = _entries[index].Value;
        RemoveAt(index, keyHash, ValueHashOf(value));
        return true;
    }

    /// <summary>
    /// Removes the entry whose value is <paramref name="value"/>. This is an <c>O(1)</c> hash lookup.
    /// </summary>
    /// <param name="value">The value of the element to remove.</param>
    /// <returns>
    /// <c>true</c> if the element was successfully removed; <c>false</c> if no key maps to
    /// <paramref name="value"/>.
    /// </returns>
    public bool RemoveValue(TValue value) => RemoveValue(value, out _);

    /// <summary>
    /// Removes the entry whose value is <paramref name="value"/> and copies its key to the
    /// <paramref name="key"/> parameter. This is an <c>O(1)</c> hash lookup.
    /// </summary>
    /// <param name="value">The value of the element to remove.</param>
    /// <param name="key">
    /// When this method returns, contains the key that mapped to <paramref name="value"/> before
    /// removal if the value was found; otherwise the default value of <typeparamref name="TKey"/>.
    /// </param>
    /// <returns>
    /// <c>true</c> if the element was successfully removed; <c>false</c> if no key maps to
    /// <paramref name="value"/>.
    /// </returns>
    public bool RemoveValue(TValue value, [MaybeNullWhen(false)] out TKey key)
    {
        int valueHash = ValueHashOf(value);
        int index = FindValue(value, valueHash);
        if (index < 0)
        {
            key = default;
            return false;
        }

        key = _entries[index].Key;
        RemoveAt(index, KeyHashOf(key), valueHash);
        return true;
    }

    /// <summary>
    /// Adds the specified key and value to the dictionary.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when an element with the same <paramref name="key"/> already exists, or when
    /// <paramref name="value"/> already belongs to another key. The dictionary is unchanged.
    /// </exception>
    public void Add(TKey key, TValue value)
    {
        int keyHash = KeyHashOf(key);
        if (FindKey(key, keyHash) >= 0)
            throw new ArgumentException($"An element with key {key} already exists.", nameof(key));

        int valueHash = ValueHashOf(value);
        if (FindValue(value, valueHash) >= 0)
            throw new ArgumentException($"The value {value} is already mapped to a different key.", nameof(value));

        AddNew(key, value, keyHash, valueHash);
    }

    /// <summary>
    /// Attempts to add the specified key and value to the dictionary.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <returns>
    /// <c>true</c> if the key/value pair was added successfully; <c>false</c> if the key already
    /// exists or the value already belongs to a key (the dictionary is unchanged).
    /// </returns>
    public bool TryAdd(TKey key, TValue value)
    {
        // A duplicate on either side returns without touching the table — no resize, no _version
        // bump — so an active enumerator stays valid (mirrors the #92 ordering on the rest of the
        // family).
        int keyHash = KeyHashOf(key);
        if (FindKey(key, keyHash) >= 0)
            return false;

        int valueHash = ValueHashOf(value);
        if (FindValue(value, valueHash) >= 0)
            return false;

        AddNew(key, value, keyHash, valueHash);
        return true;
    }

    /// <summary>
    /// Maps <paramref name="key"/> to <paramref name="value"/>, first removing whichever entry held
    /// <paramref name="value"/> under a different key. This is Guava's <c>forcePut</c>: where the
    /// indexer refuses to give a value a second key, this moves the value instead.
    /// </summary>
    /// <param name="key">The key to set.</param>
    /// <param name="value">The value to map <paramref name="key"/> to.</param>
    /// <returns>
    /// <c>true</c> if another key's entry was removed to free <paramref name="value"/>;
    /// otherwise, <c>false</c>.
    /// </returns>
    public bool ForceSet(TKey key, TValue value)
    {
        int keyHash = KeyHashOf(key);
        int valueHash = ValueHashOf(value);
        int keyIndex = FindKey(key, keyHash);
        int valueIndex = FindValue(value, valueHash);
        bool evicted = false;
        if (valueIndex >= 0)
        {
            if (valueIndex == keyIndex)
                return false;

            RemoveAt(valueIndex, KeyHashOf(_entries[valueIndex].Key), valueHash);
            evicted = true;

            // RemoveAt filled the hole with the last entry, which was at the old Count - 1, now Count.
            // If that was the key's entry, it now lives at valueIndex.
            if (keyIndex == _count)
                keyIndex = valueIndex;
        }

        if (keyIndex >= 0)
            Rebind(keyIndex, value, valueHash);
        else
            AddNew(key, value, keyHash, valueHash);

        return evicted;
    }

    /// <summary>
    /// Removes all keys and values from the dictionary. The underlying
    /// capacity is preserved.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        Array.Clear(_keyBuckets);
        Array.Clear(_valueBuckets);
        Array.Clear(_entries, 0, _count);
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Ensures that the dictionary can hold at least <paramref name="capacity"/> entries without
    /// resizing, growing the backing table in a single step if it is currently smaller: the entry
    /// array to exactly <paramref name="capacity"/>, and the bucket arrays only if the load factor
    /// needs more of them. Pre-sizing
    /// before a bulk insert of a known size avoids the incremental rehashes an unsized dictionary
    /// would otherwise pay as it grows. The dictionary is never shrunk by this call.
    /// </summary>
    /// <param name="capacity">The minimum number of entries the dictionary must hold without resizing.</param>
    /// <returns>The number of entries the dictionary can now hold before the next resize.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public int EnsureCapacity(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        if (_entries.Length < capacity)
        {
            Resize(Math.Max(_keyBuckets.Length, BucketsFor(capacity, _loadFactor)), capacity);
            _version++;
        }

        return _entries.Length;
    }

    /// <summary>
    /// Reduces the backing table to the smallest valid size that still holds the current
    /// <see cref="Count"/> without resizing, reclaiming memory.
    /// </summary>
    public void TrimExcess() => TrimExcess(_count);

    /// <summary>
    /// Reduces (or grows) the backing table to exactly <paramref name="capacity"/> entries (at least
    /// one), with the fewest buckets the load factor allows for them.
    /// </summary>
    /// <param name="capacity">
    /// The number of entries to size the table for. Must be at least the current <see cref="Count"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is less than the current <see cref="Count"/>.
    /// </exception>
    public void TrimExcess(int capacity)
    {
        if (capacity < _count)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least the current Count.");

        int buckets = BucketsFor(capacity, _loadFactor);
        int entries = Math.Max(1, capacity);
        if (buckets != _keyBuckets.Length || entries != _entries.Length)
        {
            Resize(buckets, entries);
            _version++;
        }
    }

    /// <summary>
    /// Returns an allocation-free enumerator that yields each key/value pair. If the dictionary is
    /// modified during enumeration, <see cref="Enumerator.MoveNext"/> throws
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over this dictionary.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    /// <summary>
    /// Gets an enumerable view over the keys in the dictionary. The view is a lightweight struct and
    /// iterating it does not allocate.
    /// </summary>
    public KeyCollection Keys => new KeyCollection(this);

    /// <summary>
    /// Gets an enumerable view over the values in the dictionary. The view is a lightweight struct and
    /// iterating it does not allocate; its <see cref="ValueCollection.Contains"/> is an <c>O(1)</c>
    /// hash lookup.
    /// </summary>
    public ValueCollection Values => new ValueCollection(this);

    /// <summary>
    /// Copies every key/value pair into <paramref name="array"/> starting at
    /// <paramref name="arrayIndex"/>, in enumeration order.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        CopyToGuard.Validate(array, arrayIndex, _count, CopyToGuard.EntriesMessage);

        Entry[] entries = _entries;
        for (int i = 0; i < _count; i++)
            array[arrayIndex + i] = new KeyValuePair<TKey, TValue>(entries[i].Key, entries[i].Value);
    }

    /// <summary>
    /// A struct enumerator over a <see cref="BiDictionary{TKey, TValue, TKeyHasher, TValueHasher}"/>.
    /// Because it is a struct, iterating it via <c>foreach</c> avoids the allocation that a
    /// compiler-generated <c>IEnumerator&lt;T&gt;</c> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        private Cursor _cursor;

        internal Enumerator(BiDictionary<TKey, TValue, TKeyHasher, TValueHasher> dict) => _cursor = new Cursor(dict);

        /// <summary>
        /// Gets the key/value pair at the current position of the enumerator.
        /// </summary>
        public KeyValuePair<TKey, TValue> Current => new KeyValuePair<TKey, TValue>(_cursor.Key, _cursor.Value);

        object IEnumerator.Current => Current;

        /// <summary>
        /// Advances the enumerator to the next key/value pair.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the enumerator advanced to a new entry; <c>false</c>
        /// if it has passed the end of the dictionary.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the dictionary was modified since the enumerator was created.
        /// </exception>
        public bool MoveNext() => _cursor.MoveNext();

        /// <summary>
        /// Resets the enumerator to its initial position, before the first entry.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the dictionary was modified since the enumerator was created.
        /// </exception>
        public void Reset() => _cursor.Reset();

        /// <summary>
        /// Releases any resources held by the enumerator. No-op for this type.
        /// </summary>
        public void Dispose() { }
    }

    /// <summary>
    /// A struct enumerable view over the keys of a
    /// <see cref="BiDictionary{TKey, TValue, TKeyHasher, TValueHasher}"/>. Iterating it does
    /// not allocate; passing it through <see cref="IEnumerable{T}"/> will box the
    /// enumerator and is therefore not zero-allocation. It is a read-only
    /// <see cref="ICollection{T}"/>: the mutating members throw
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    public readonly struct KeyCollection : ICollection<TKey>
    {
        private readonly BiDictionary<TKey, TValue, TKeyHasher, TValueHasher> _dict;

        internal KeyCollection(BiDictionary<TKey, TValue, TKeyHasher, TValueHasher> dict) => _dict = dict;

        /// <summary>
        /// Gets the number of keys in the view (equal to the dictionary's count).
        /// </summary>
        public int Count => _dict._count;

        /// <summary>Gets a value indicating whether the view is read-only. Always <c>true</c>.</summary>
        public bool IsReadOnly => true;

        /// <summary>
        /// Returns an allocation-free struct enumerator over the keys.
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(_dict);

        /// <summary>Determines whether the dictionary contains <paramref name="item"/> as a key.</summary>
        /// <param name="item">The key to look for.</param>
        /// <returns><c>true</c> if the key is present.</returns>
        public bool Contains(TKey item) => _dict.ContainsKey(item);

        /// <summary>
        /// Copies the keys into <paramref name="array"/> starting at <paramref name="arrayIndex"/>,
        /// in enumeration order.
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

            Entry[] entries = _dict._entries;
            for (int i = 0; i < _dict._count; i++)
                array[arrayIndex + i] = entries[i].Key;
        }

        IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => new Enumerator(_dict);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_dict);

        void ICollection<TKey>.Add(TKey item) =>
            throw new NotSupportedException("The key view is read-only.");

        void ICollection<TKey>.Clear() =>
            throw new NotSupportedException("The key view is read-only.");

        bool ICollection<TKey>.Remove(TKey item) =>
            throw new NotSupportedException("The key view is read-only.");

        /// <summary>
        /// A struct enumerator over the keys of a
        /// <see cref="BiDictionary{TKey, TValue, TKeyHasher, TValueHasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TKey>
        {
            private Cursor _cursor;

            internal Enumerator(BiDictionary<TKey, TValue, TKeyHasher, TValueHasher> dict) => _cursor = new Cursor(dict);

            /// <summary>Gets the current key.</summary>
            public TKey Current => _cursor.Key;

            object? IEnumerator.Current => Current;

            /// <summary>Advances to the next key.</summary>
            /// <exception cref="InvalidOperationException">
            /// Thrown if the dictionary was modified since the enumerator was created.
            /// </exception>
            public bool MoveNext() => _cursor.MoveNext();

            /// <summary>Resets the enumerator to its initial position.</summary>
            /// <exception cref="InvalidOperationException">
            /// Thrown if the dictionary was modified since the enumerator was created.
            /// </exception>
            public void Reset() => _cursor.Reset();

            /// <summary>No-op.</summary>
            public void Dispose() { }
        }
    }

    /// <summary>
    /// A struct enumerable view over the values of a
    /// <see cref="BiDictionary{TKey, TValue, TKeyHasher, TValueHasher}"/>. Iterating it does
    /// not allocate; passing it through <see cref="IEnumerable{T}"/> will box the
    /// enumerator and is therefore not zero-allocation. It is a read-only
    /// <see cref="ICollection{T}"/>: the mutating members throw
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    public readonly struct ValueCollection : ICollection<TValue>
    {
        private readonly BiDictionary<TKey, TValue, TKeyHasher, TValueHasher> _dict;

        internal ValueCollection(BiDictionary<TKey, TValue, TKeyHasher, TValueHasher> dict) => _dict = dict;

        /// <summary>
        /// Gets the number of values in the view (equal to the dictionary's count).
        /// </summary>
        public int Count => _dict._count;

        /// <summary>Gets a value indicating whether the view is read-only. Always <c>true</c>.</summary>
        public bool IsReadOnly => true;

        /// <summary>
        /// Returns an allocation-free struct enumerator over the values.
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(_dict);

        /// <summary>
        /// Determines whether some key maps to <paramref name="item"/>. This is an <c>O(1)</c> hash
        /// lookup.
        /// </summary>
        /// <param name="item">The value to look for.</param>
        /// <returns><c>true</c> if the value is present.</returns>
        public bool Contains(TValue item) => _dict.ContainsValue(item);

        /// <summary>
        /// Copies the values into <paramref name="array"/> starting at <paramref name="arrayIndex"/>,
        /// in enumeration order.
        /// </summary>
        /// <param name="array">The destination array.</param>
        /// <param name="arrayIndex">The zero-based index at which copying begins.</param>
        /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.
        /// </exception>
        /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
        public void CopyTo(TValue[] array, int arrayIndex)
        {
            CopyToGuard.Validate(array, arrayIndex, _dict._count, CopyToGuard.ValuesMessage);

            Entry[] entries = _dict._entries;
            for (int i = 0; i < _dict._count; i++)
                array[arrayIndex + i] = entries[i].Value;
        }

        IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => new Enumerator(_dict);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_dict);

        void ICollection<TValue>.Add(TValue item) =>
            throw new NotSupportedException("The value view is read-only.");

        void ICollection<TValue>.Clear() =>
            throw new NotSupportedException("The value view is read-only.");

        bool ICollection<TValue>.Remove(TValue item) =>
            throw new NotSupportedException("The value view is read-only.");

        /// <summary>
        /// A struct enumerator over the values of a
        /// <see cref="BiDictionary{TKey, TValue, TKeyHasher, TValueHasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TValue>
        {
            private Cursor _cursor;

            internal Enumerator(BiDictionary<TKey, TValue, TKeyHasher, TValueHasher> dict) => _cursor = new Cursor(dict);

            /// <summary>Gets the current value.</summary>
            public TValue Current => _cursor.Value;

            object? IEnumerator.Current => Current;

            /// <summary>Advances to the next value.</summary>
            /// <exception cref="InvalidOperationException">
            /// Thrown if the dictionary was modified since the enumerator was created.
            /// </exception>
            public bool MoveNext() => _cursor.MoveNext();

            /// <summary>Resets the enumerator to its initial position.</summary>
            /// <exception cref="InvalidOperationException">
            /// Thrown if the dictionary was modified since the enumerator was created.
            /// </exception>
            public void Reset() => _cursor.Reset();

            /// <summary>No-op.</summary>
            public void Dispose() { }
        }
    }

    /// <summary>
    /// A read-only view of a <see cref="BiDictionary{TKey, TValue, TKeyHasher, TValueHasher}"/> in the
    /// other direction: its keys are the dictionary's values and its values are the dictionary's keys.
    /// It is a lightweight struct over the same table, so it reflects every later change to the
    /// dictionary, and every lookup is the same <c>O(1)</c> hash lookup the dictionary's value-side
    /// members use. Passing it through <see cref="IReadOnlyDictionary{TKey, TValue}"/> boxes it.
    /// </summary>
    public readonly struct InverseView : IReadOnlyDictionary<TValue, TKey>
    {
        private readonly BiDictionary<TKey, TValue, TKeyHasher, TValueHasher> _dict;

        internal InverseView(BiDictionary<TKey, TValue, TKeyHasher, TValueHasher> dict) => _dict = dict;

        /// <summary>
        /// Gets the number of entries in the view (equal to the dictionary's count).
        /// </summary>
        public int Count => _dict._count;

        /// <summary>
        /// Gets the key that maps to <paramref name="value"/> in the underlying dictionary.
        /// </summary>
        /// <param name="value">The value to look up.</param>
        /// <exception cref="KeyNotFoundException">No key maps to <paramref name="value"/>.</exception>
        /// <returns>The key that maps to <paramref name="value"/>.</returns>
        public TKey this[TValue value] => _dict.GetKey(value);

        /// <summary>
        /// Gets the view's keys: the underlying dictionary's values.
        /// </summary>
        public ValueCollection Keys => new ValueCollection(_dict);

        /// <summary>
        /// Gets the view's values: the underlying dictionary's keys.
        /// </summary>
        public KeyCollection Values => new KeyCollection(_dict);

        /// <summary>
        /// Determines whether some key of the underlying dictionary maps to <paramref name="value"/>.
        /// </summary>
        /// <param name="value">The value to locate.</param>
        /// <returns><c>true</c> if the value is present; otherwise, <c>false</c>.</returns>
        public bool ContainsKey(TValue value) => _dict.ContainsValue(value);

        /// <summary>
        /// Attempts to get the key that maps to <paramref name="value"/> in the underlying dictionary.
        /// </summary>
        /// <param name="value">The value to look up.</param>
        /// <param name="key">
        /// When this method returns, contains the key that maps to <paramref name="value"/> if found;
        /// otherwise the default value of <typeparamref name="TKey"/>.
        /// </param>
        /// <returns><c>true</c> if the value was found; otherwise, <c>false</c>.</returns>
        public bool TryGetValue(TValue value, [MaybeNullWhen(false)] out TKey key) => _dict.TryGetKey(value, out key);

        /// <summary>
        /// Returns an allocation-free enumerator that yields each value/key pair, in the dictionary's
        /// enumeration order.
        /// </summary>
        /// <returns>A struct enumerator over the view.</returns>
        public Enumerator GetEnumerator() => new Enumerator(_dict);

        IEnumerable<TValue> IReadOnlyDictionary<TValue, TKey>.Keys => Keys;

        IEnumerable<TKey> IReadOnlyDictionary<TValue, TKey>.Values => Values;

        IEnumerator<KeyValuePair<TValue, TKey>> IEnumerable<KeyValuePair<TValue, TKey>>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// A struct enumerator over an <see cref="InverseView"/>, yielding value/key pairs.
        /// </summary>
        public struct Enumerator : IEnumerator<KeyValuePair<TValue, TKey>>
        {
            private Cursor _cursor;

            internal Enumerator(BiDictionary<TKey, TValue, TKeyHasher, TValueHasher> dict) => _cursor = new Cursor(dict);

            /// <summary>Gets the value/key pair at the current position of the enumerator.</summary>
            public KeyValuePair<TValue, TKey> Current => new KeyValuePair<TValue, TKey>(_cursor.Value, _cursor.Key);

            object IEnumerator.Current => Current;

            /// <summary>Advances to the next value/key pair.</summary>
            /// <exception cref="InvalidOperationException">
            /// Thrown if the dictionary was modified since the enumerator was created.
            /// </exception>
            public bool MoveNext() => _cursor.MoveNext();

            /// <summary>Resets the enumerator to its initial position.</summary>
            /// <exception cref="InvalidOperationException">
            /// Thrown if the dictionary was modified since the enumerator was created.
            /// </exception>
            public void Reset() => _cursor.Reset();

            /// <summary>No-op.</summary>
            public void Dispose() { }
        }
    }

    // The position shared by every enumerator shape: a walk over the dense entry array that fails fast
    // on a structural change. Like the BCL enumerators, it copies the current pair out at each step, so
    // Current is default before the first MoveNext and after the last.
    private struct Cursor
    {
        private readonly BiDictionary<TKey, TValue, TKeyHasher, TValueHasher> _dict;
        private readonly int _version;

        // The entry array as of construction. Only a resize replaces it, and a resize bumps the
        // version, so MoveNext never reads an entry from a stale array.
        private readonly Entry[] _entries;
        private readonly int _count;
        private int _next;

        internal TKey Key;
        internal TValue Value;

        internal Cursor(BiDictionary<TKey, TValue, TKeyHasher, TValueHasher> dict)
        {
            _dict = dict;
            _version = dict._version;
            _entries = dict._entries;
            _count = dict._count;
            _next = 0;
            Key = default!;
            Value = default!;
        }

        internal bool MoveNext()
        {
            if (_version != _dict._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            if (_next < _count)
            {
                ref Entry entry = ref _entries[_next++];
                Key = entry.Key;
                Value = entry.Value;
                return true;
            }

            Key = default!;
            Value = default!;
            return false;
        }

        internal void Reset()
        {
            if (_version != _dict._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _next = 0;
            Key = default!;
            Value = default!;
        }
    }

    // IReadOnlyDictionary<TKey, TValue> explicit interface members. The primary
    // (non-interface) surface already covers the interface contract; these
    // forwarders only widen those members to the boxed IEnumerable<T> /
    // IEnumerator<T> shapes the interface requires.
    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;

    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;

    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // IDictionary<TKey, TValue> explicit interface members. Everything the mutable
    // interface adds over the read-only one is already public — Add, Remove, Clear,
    // the indexer, CopyTo — so these forwarders only reshape Keys / Values to
    // ICollection<T> and supply the ICollection<KeyValuePair<,>> members, leaving
    // every existing public signature untouched.
    ICollection<TKey> IDictionary<TKey, TValue>.Keys => Keys;

    ICollection<TValue> IDictionary<TKey, TValue>.Values => Values;

    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) =>
        Add(item.Key, item.Value);

    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) =>
        TryGetValue(item.Key, out TValue? value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);

    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
    {
        // ICollection<KVP> semantics: remove only when the *pair* matches, so a stale value must not
        // delete the current entry.
        int keyHash = KeyHashOf(item.Key);
        int index = FindKey(item.Key, keyHash);
        if (index < 0 || !EqualityComparer<TValue>.Default.Equals(_entries[index].Value, item.Value))
            return false;

        RemoveAt(index, keyHash, ValueHashOf(item.Value));
        return true;
    }

    // The number of entries a table of `buckets` buckets holds before it grows. Never zero, so even a
    // one-bucket table with a tiny load factor can take its first entry.
    private static int EntryCapacityFor(int buckets, float loadFactor) =>
        Math.Max(1, (int)(buckets * loadFactor));

    // The fewest buckets whose entry capacity holds `entries`. One bucket always holds one entry (see
    // EntryCapacityFor), so zero or one entry needs no load-factor headroom; past that, the table must
    // reach the load factor, which a tiny one makes large.
    private static int BucketsFor(int entries, float loadFactor) =>
        entries <= 1 ? 1 : FastUtils.MinTableSizeFor(entries, loadFactor);

    // default(TKey) / default(TValue) — a null reference, a Nullable<T> without a value, or 0 for an
    // integer — never reaches its hasher, which may not accept it. Hash code 0 puts it in the table like
    // any other. EmptySlot.Is costs one intrinsic comparison and never boxes, even in an unoptimized
    // build.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int KeyHashOf(TKey key) =>
        EmptySlot.Is(key) ? 0 : default(TKeyHasher).Hash(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ValueHashOf(TValue value) =>
        EmptySlot.Is(value) ? 0 : default(TValueHasher).Hash(value);

    // Returns the entry holding `key`, or Nil when it is absent. With no cached hash to pre-test, every
    // entry on the chain is compared; at the load factors the constructor admits a chain averages under
    // one entry.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindKey(TKey key, int hash)
    {
        Entry[] entries = _entries;
        int index = _keyBuckets[hash & _mask] - 1;
        while (index != Nil)
        {
            ref Entry entry = ref entries[index];
            if (EqualityComparer<TKey>.Default.Equals(entry.Key, key))
                return index;
            index = entry.KeyNext - 1;
        }
        return Nil;
    }

    // Returns the entry holding `value`, or Nil when it is absent.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindValue(TValue value, int hash)
    {
        Entry[] entries = _entries;
        int index = _valueBuckets[hash & _mask] - 1;
        while (index != Nil)
        {
            ref Entry entry = ref entries[index];
            if (EqualityComparer<TValue>.Default.Equals(entry.Value, value))
                return index;
            index = entry.ValueNext - 1;
        }
        return Nil;
    }

    // The link (a bucket, or a predecessor's KeyNext) that points at entry `index` in its key chain;
    // `keyHash` is the entry's key hash. The entry is always on the chain, so the walk ends on it.
    private ref int KeyLinkTo(int index, int keyHash)
    {
        ref int link = ref _keyBuckets[keyHash & _mask];
        while (link != index + 1)
            link = ref _entries[link - 1].KeyNext;
        return ref link;
    }

    // The link (a bucket, or a predecessor's ValueNext) that points at entry `index` in its value chain.
    private ref int ValueLinkTo(int index, int valueHash)
    {
        ref int link = ref _valueBuckets[valueHash & _mask];
        while (link != index + 1)
            link = ref _entries[link - 1].ValueNext;
        return ref link;
    }

    // Appends a pair the caller has just found absent on both sides, growing the table first when
    // every entry is in use.
    private void AddNew(TKey key, TValue value, int keyHash, int valueHash)
    {
        if (_count == _entries.Length)
            Grow();

        int index = _count;
        ref Entry entry = ref _entries[index];
        entry.Key = key;
        entry.Value = value;

        ref int keyBucket = ref _keyBuckets[keyHash & _mask];
        entry.KeyNext = keyBucket;
        keyBucket = index + 1;

        ref int valueBucket = ref _valueBuckets[valueHash & _mask];
        entry.ValueNext = valueBucket;
        valueBucket = index + 1;

        _count++;
        _version++;
    }

    // Re-points entry `index` at a value the caller has just found absent: moves the entry from its
    // old value chain to the new one. The key side and the entry's position are unchanged.
    private void Rebind(int index, TValue value, int valueHash)
    {
        ref Entry entry = ref _entries[index];
        ValueLinkTo(index, ValueHashOf(entry.Value)) = entry.ValueNext;

        entry.Value = value;
        ref int valueBucket = ref _valueBuckets[valueHash & _mask];
        entry.ValueNext = valueBucket;
        valueBucket = index + 1;
    }

    // Removes entry `index`, whose key and value hash to `keyHash` and `valueHash`, from both chain
    // families and fills its slot with the last entry, so the live entries stay dense in [0, Count).
    private void RemoveAt(int index, int keyHash, int valueHash)
    {
        Entry[] entries = _entries;
        KeyLinkTo(index, keyHash) = entries[index].KeyNext;
        ValueLinkTo(index, valueHash) = entries[index].ValueNext;

        int last = _count - 1;
        if (index != last)
        {
            // Re-aim whatever pointed at the last entry, in both chains, at the hole, then move it in.
            ref Entry moved = ref entries[last];
            KeyLinkTo(last, KeyHashOf(moved.Key)) = index + 1;
            ValueLinkTo(last, ValueHashOf(moved.Value)) = index + 1;
            entries[index] = moved;
        }

        // Release the key and value so the table keeps no reference alive.
        entries[last] = default;
        _count = last;
        _version++;
    }

    // Doubles the entry array, adding buckets only when the load factor needs them, so a table sized
    // exactly by EnsureCapacity or a sized source can often grow without re-hashing. The bucket
    // arrays cap at 2^30, which no int-indexed entry array this large could fill.
    private void Grow()
    {
        int entries = _entries.Length * 2;
        Resize(Math.Max(_keyBuckets.Length, BucketsFor(entries, _loadFactor)), entries);
    }

    // Moves the table to `buckets` buckets and `entryCapacity` entries. The live entries are copied in
    // the same order; when the bucket count changes, both chain families are re-threaded, hashing each
    // key and value again, and otherwise the links are still valid and nothing is hashed. The caller
    // guarantees `entryCapacity` is at least Count and within the buckets' load-factor capacity.
    private void Resize(int buckets, int entryCapacity)
    {
        var entries = new Entry[entryCapacity];
        Array.Copy(_entries, entries, _count);
        _entries = entries;
        if (buckets == _keyBuckets.Length)
            return;

        var keyBuckets = new int[buckets];
        var valueBuckets = new int[buckets];
        int mask = buckets - 1;

        for (int i = 0; i < _count; i++)
        {
            ref Entry entry = ref entries[i];

            ref int keyBucket = ref keyBuckets[KeyHashOf(entry.Key) & mask];
            entry.KeyNext = keyBucket;
            keyBucket = i + 1;

            ref int valueBucket = ref valueBuckets[ValueHashOf(entry.Value) & mask];
            entry.ValueNext = valueBucket;
            valueBucket = i + 1;
        }

        _keyBuckets = keyBuckets;
        _valueBuckets = valueBuckets;
        _mask = mask;
    }
}
