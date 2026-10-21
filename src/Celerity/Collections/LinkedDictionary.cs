using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Hashing;
using Celerity.Primitives;

namespace Celerity.Collections;

/// <summary>
/// A generic hash dictionary that remembers insertion order: it enumerates its entries in the order
/// their keys were first added, and keeps that order through any number of removals, at
/// <c>O(1)</c> per <see cref="Remove(TKey)"/>. It can also move an entry to either end
/// (<see cref="MoveToEnd"/>, <see cref="MoveToFront"/>) and read or remove the oldest and newest entries
/// (<see cref="TryGetFirst"/>, <see cref="TryRemoveFirst"/>, <see cref="TryGetLast"/>,
/// <see cref="TryRemoveLast"/>), all in <c>O(1)</c>.
/// </summary>
/// <remarks>
/// <para>
/// The .NET counterparts each give up half of this. <see cref="Dictionary{TKey, TValue}"/> only
/// enumerates in insertion order until the first removal: a freed entry is reused by the next add,
/// so the new key appears where the removed one was. .NET 9's <c>OrderedDictionary&lt;TKey, TValue&gt;</c>
/// keeps the order and adds index access, but stores its entries in one dense array, so
/// <c>Remove</c> shifts every later entry down and costs <c>O(n)</c>. This type is the shape of Java's
/// <c>LinkedHashMap</c> and Python's <c>OrderedDict</c>: <c>O(1)</c> removal and re-ordering, and no
/// index access.
/// </para>
/// <para>
/// Entries live in one array of structs, each holding the key, the value, the key's full hash, the
/// next entry in the same hash bucket, and the previous and next entries in insertion order. A lookup
/// walks a bucket's chain and touches one entry per step. A removal unlinks the entry from its chain
/// and from the order list, and puts the slot on a free list for the next add, so a remove/add churn
/// never grows the table. Enumeration follows the order list, so it costs <c>O(Count)</c> however many
/// entries have been removed.
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Wins</b> wherever the order must survive removals: removal-heavy queues keyed by id, dedup
/// that must keep first-seen order, and "touch to move to the end" recency lists. Against
/// <c>OrderedDictionary</c>, each removal is <c>O(1)</c> instead of <c>O(n)</c>.
/// </description></item>
/// <item><description>
/// <b>Costs</b> two extra <see cref="int"/> links per entry over a plain dictionary, and the work of
/// keeping them on every add and remove. When order does not matter, use
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/> or
/// <see cref="SwissDictionary{TKey, TValue, THasher}"/>.
/// </description></item>
/// </list>
/// <para>
/// Unlike the open-addressed dictionaries, this type stores the default key (<c>0</c>, a
/// <see langword="null"/> reference, or a <see cref="Nullable{T}"/> with no value) in the table like any
/// other key, so it keeps its place in the order. It still never hands that key to the hasher: the
/// default key is given hash code <c>0</c> directly, so a hasher that rejects <see langword="null"/> is
/// safe here too.
/// </para>
/// <para>
/// Overwriting the value of an existing key through the indexer leaves the key where it is in the order,
/// as both <c>LinkedHashMap</c> and <c>OrderedDict</c> do. To move it, call <see cref="MoveToEnd"/>, or
/// remove it and add it again.
/// </para>
/// </remarks>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TValue">The type of the values.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute key hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class LinkedDictionary<TKey, TValue, THasher>
    : IDictionary<TKey, TValue?>, IReadOnlyDictionary<TKey, TValue?>
    where THasher : struct, IHashProvider<TKey>
{
    /// <summary>
    /// The default initial capacity of the dictionary if no capacity is specified.
    /// </summary>
    protected const int DefaultCapacity = 16;

    /// <summary>
    /// The default load factor of the dictionary if no load factor is specified.
    /// </summary>
    protected const float DefaultLoadFactor = 0.75f;

    // Sentinel for "no entry", in a hash chain, the order list or the free list. Buckets store
    // entry + 1 instead, so a freshly allocated bucket array already reads as all-empty.
    private const int Nil = -1;

    private struct Entry
    {
        // The key's full hash, kept so a rebuild never calls the hasher and a reference-key lookup can
        // skip Equals on a hash mismatch.
        public int Hash;

        // The next entry in the same bucket's chain. A free entry reuses it as the free-list link.
        public int ChainNext;

        // The neighbours in insertion order.
        public int Prev;
        public int Next;

        public TKey Key;
        public TValue? Value;
    }

    // Bucket b holds (first entry of its chain) + 1, or 0 when the bucket is empty. Its length is a
    // power of two, so a hash picks its bucket with a mask.
    private int[] _buckets;
    private Entry[] _entries;
    private int _mask;
    private readonly float _loadFactor;

    // Entries [0, _used) have been handed out at least once; the free ones among them are chained
    // through ChainNext from _freeList. An add takes a free entry first, then the next unused one, and
    // only when both run out does the table grow.
    private int _used;
    private int _freeList = Nil;
    private int _count;

    // The oldest and newest entries in insertion order.
    private int _head = Nil;
    private int _tail = Nil;

    // Incremented on every structural mutation so active enumerators can
    // detect concurrent modification and throw, matching BCL semantics.
    private int _version;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkedDictionary{TKey,TValue,THasher}"/> class
    /// using the specified capacity and load factor.
    /// </summary>
    /// <param name="capacity">
    /// The initial number of hash buckets, automatically rounded up to the next power of two. The
    /// dictionary holds <c>capacity * loadFactor</c> entries (at least one) before it grows.
    /// </param>
    /// <param name="loadFactor">
    /// The number of entries per bucket the dictionary allows before it grows.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative, or <paramref name="loadFactor"/>
    /// is not in the open interval (0, 1).
    /// </exception>
    public LinkedDictionary(
        int capacity = DefaultCapacity,
        float loadFactor = DefaultLoadFactor)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (!(loadFactor > 0f && loadFactor < 1f))
            throw new ArgumentOutOfRangeException(nameof(loadFactor), loadFactor, "Load factor must be between 0 (exclusive) and 1 (exclusive).");

        _loadFactor = loadFactor;
        int buckets = FastUtils.NextPowerOfTwo(capacity);
        _buckets = new int[buckets];
        _mask = buckets - 1;
        _entries = new Entry[EntryCapacityFor(buckets, loadFactor)];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkedDictionary{TKey, TValue, THasher}"/>
    /// class that contains the key/value pairs copied from the specified
    /// <paramref name="source"/>, in the order <paramref name="source"/> enumerates them.
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
    /// <paramref name="source"/> contains one or more duplicate keys.
    /// </exception>
    public LinkedDictionary(
        IEnumerable<KeyValuePair<TKey, TValue>> source,
        int capacity = DefaultCapacity,
        float loadFactor = DefaultLoadFactor)
        : this(InitialCapacityForSource(source, capacity, loadFactor), loadFactor)
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
            count = FastUtils.MinTableSizeFor(count, loadFactor);

        // A negative capacity passes through untouched so the primary ctor rejects it,
        // exactly as the capacity-only overload does (issue #460).
        return capacity < 0 ? capacity : Math.Max(capacity, count);
    }

    /// <summary>
    /// Gets the number of key/value pairs contained in the dictionary.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// Throws a <see cref="KeyNotFoundException"/> if the key does not exist on get.
    /// Setting the value of a key that is already present keeps the key's place in the order;
    /// setting a new key appends it at the end.
    /// </summary>
    /// <param name="key">The key of the value to get or set.</param>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when attempting to get a value for a key that is not found in the dictionary.
    /// </exception>
    /// <returns>The value associated with the specified key.</returns>
    public TValue this[TKey key]
    {
        get
        {
            int index = Find(key, HashOf(key));
            if (index < 0)
                throw new KeyNotFoundException($"Key {key} not found.");

            return _entries[index].Value!;
        }
        set
        {
            int hash = HashOf(key);
            int index = Find(key, hash);
            if (index >= 0)
            {
                // Pure value overwrite of an existing key: no structural change,
                // so _version is left untouched and active enumerators stay
                // valid, matching BCL Dictionary<,>. See #233.
                _entries[index].Value = value;
                return;
            }

            AddNew(key, value, hash);
        }
    }

    /// <summary>
    /// Determines whether the specified key is present in the dictionary.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns><c>true</c> if the key is found; otherwise, <c>false</c>.</returns>
    public bool ContainsKey(TKey key) => Find(key, HashOf(key)) >= 0;

    /// <summary>
    /// Determines whether the dictionary contains the specified value.
    /// </summary>
    /// <param name="value">
    /// The value to locate. Equality is determined via
    /// <see cref="EqualityComparer{T}.Default"/>, matching BCL
    /// <see cref="Dictionary{TKey, TValue}.ContainsValue(TValue)"/> semantics.
    /// </param>
    /// <returns><c>true</c> if a matching value is found; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This operation is <c>O(n)</c> in <see cref="Count"/>: it walks the entries in insertion
    /// order, so removed entries cost nothing.
    /// </remarks>
    public bool ContainsValue(TValue? value)
    {
        var valueComparer = EqualityComparer<TValue?>.Default;
        Entry[] entries = _entries;
        for (int i = _head; i != Nil; i = entries[i].Next)
        {
            if (valueComparer.Equals(entries[i].Value, value))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">
    /// When this method returns, contains the value associated with <paramref name="key"/>
    /// if found; otherwise the default value of <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><c>true</c> if the key was found; otherwise, <c>false</c>.</returns>
    public bool TryGetValue(TKey key, out TValue? value)
    {
        int index = Find(key, HashOf(key));
        if (index < 0)
        {
            value = default;
            return false;
        }

        value = _entries[index].Value;
        return true;
    }

    /// <summary>
    /// Removes the value with the specified key from the dictionary. The other entries keep their
    /// relative order. This is an <c>O(1)</c> operation.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <returns>
    /// <c>true</c> if the element was successfully removed; otherwise, <c>false</c>.
    /// Also returns <c>false</c> if the key was not found.
    /// </returns>
    public bool Remove(TKey key) => Remove(key, out _);

    /// <summary>
    /// Removes the value with the specified key from the dictionary and copies
    /// the removed value to the <paramref name="value"/> parameter. The other entries keep their
    /// relative order. This is an <c>O(1)</c> operation.
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
    public bool Remove(TKey key, out TValue? value)
    {
        int hash = HashOf(key);
        Entry[] entries = _entries;
        int prev = Nil;
        int index = _buckets[hash & _mask] - 1;
        while (index != Nil)
        {
            ref Entry entry = ref entries[index];
            if (Matches(ref entry, key, hash))
            {
                value = entry.Value;
                RemoveEntry(index, prev);
                return true;
            }
            prev = index;
            index = entry.ChainNext;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Adds the specified key and value to the end of the dictionary.
    /// Throws <see cref="ArgumentException"/> if the key already exists.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when an element with the same <paramref name="key"/> already exists.
    /// </exception>
    public void Add(TKey key, TValue value)
    {
        if (!TryAdd(key, value))
            throw new ArgumentException($"An element with key {key} already exists.", nameof(key));
    }

    /// <summary>
    /// Attempts to add the specified key and value to the end of the dictionary.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <returns>
    /// <c>true</c> if the key/value pair was added successfully;
    /// <c>false</c> if the key already exists (the dictionary, and the key's place in the order,
    /// are unchanged).
    /// </returns>
    public bool TryAdd(TKey key, TValue value)
    {
        // One hash, one chain walk. A duplicate returns without touching the
        // table — no resize, no _version bump — so an active enumerator stays
        // valid (mirrors the #92 ordering on the rest of the family).
        int hash = HashOf(key);
        if (Find(key, hash) >= 0)
            return false;

        AddNew(key, value, hash);
        return true;
    }

    /// <summary>
    /// Moves the entry for <paramref name="key"/> to the end of the order, making it the newest
    /// entry. Its value is unchanged. This is an <c>O(1)</c> operation.
    /// </summary>
    /// <param name="key">The key of the entry to move.</param>
    /// <returns>
    /// <c>true</c> if the key is present (including when it was already the newest entry, in which
    /// case nothing changes and active enumerators stay valid); <c>false</c> if it is not.
    /// </returns>
    public bool MoveToEnd(TKey key)
    {
        int index = Find(key, HashOf(key));
        if (index < 0)
            return false;

        if (index != _tail)
        {
            Unlink(index);
            LinkLast(index);
            _version++;
        }
        return true;
    }

    /// <summary>
    /// Moves the entry for <paramref name="key"/> to the front of the order, making it the oldest
    /// entry. Its value is unchanged. This is an <c>O(1)</c> operation.
    /// </summary>
    /// <param name="key">The key of the entry to move.</param>
    /// <returns>
    /// <c>true</c> if the key is present (including when it was already the oldest entry, in which
    /// case nothing changes and active enumerators stay valid); <c>false</c> if it is not.
    /// </returns>
    public bool MoveToFront(TKey key)
    {
        int index = Find(key, HashOf(key));
        if (index < 0)
            return false;

        if (index != _head)
        {
            Unlink(index);
            LinkFirst(index);
            _version++;
        }
        return true;
    }

    /// <summary>
    /// Gets the oldest entry: the first one <see cref="GetEnumerator"/> yields.
    /// </summary>
    /// <param name="key">When this method returns <c>true</c>, the oldest entry's key.</param>
    /// <param name="value">When this method returns <c>true</c>, the oldest entry's value.</param>
    /// <returns><c>true</c> if the dictionary is not empty; otherwise, <c>false</c>.</returns>
    public bool TryGetFirst(out TKey key, out TValue? value) => TryGetAt(_head, out key, out value);

    /// <summary>
    /// Gets the newest entry: the last one <see cref="GetEnumerator"/> yields.
    /// </summary>
    /// <param name="key">When this method returns <c>true</c>, the newest entry's key.</param>
    /// <param name="value">When this method returns <c>true</c>, the newest entry's value.</param>
    /// <returns><c>true</c> if the dictionary is not empty; otherwise, <c>false</c>.</returns>
    public bool TryGetLast(out TKey key, out TValue? value) => TryGetAt(_tail, out key, out value);

    /// <summary>
    /// Removes and returns the oldest entry. Calling it in a loop drains the dictionary in
    /// first-in, first-out order. This is an <c>O(1)</c> operation.
    /// </summary>
    /// <param name="key">When this method returns <c>true</c>, the removed entry's key.</param>
    /// <param name="value">When this method returns <c>true</c>, the removed entry's value.</param>
    /// <returns><c>true</c> if an entry was removed; <c>false</c> if the dictionary was empty.</returns>
    public bool TryRemoveFirst(out TKey key, out TValue? value) => TryRemoveAt(_head, out key, out value);

    /// <summary>
    /// Removes and returns the newest entry. Calling it in a loop drains the dictionary in
    /// last-in, first-out order. This is an <c>O(1)</c> operation.
    /// </summary>
    /// <param name="key">When this method returns <c>true</c>, the removed entry's key.</param>
    /// <param name="value">When this method returns <c>true</c>, the removed entry's value.</param>
    /// <returns><c>true</c> if an entry was removed; <c>false</c> if the dictionary was empty.</returns>
    public bool TryRemoveLast(out TKey key, out TValue? value) => TryRemoveAt(_tail, out key, out value);

    /// <summary>
    /// Removes all keys and values from the dictionary. The underlying
    /// capacity is preserved.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        Array.Clear(_buckets);
        Array.Clear(_entries, 0, _used);
        _used = 0;
        _freeList = Nil;
        _head = Nil;
        _tail = Nil;
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Ensures that the dictionary can hold at least <paramref name="capacity"/> entries without
    /// resizing, growing the backing table in a single rebuild if it is currently smaller. Pre-sizing
    /// before a bulk insert of a known size avoids the incremental rehashes an unsized dictionary
    /// would otherwise pay as it grows. The dictionary is never shrunk by this call, and the order
    /// is preserved.
    /// </summary>
    /// <param name="capacity">The minimum number of entries the dictionary must hold without resizing.</param>
    /// <returns>The number of entries the dictionary can now hold before the next resize.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public int EnsureCapacity(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        // The smallest table that holds `capacity` entries is necessarily larger than the current
        // one whenever the current one holds fewer, so this rebuild always grows.
        if (_entries.Length < capacity)
        {
            Rebuild(FastUtils.MinTableSizeFor(capacity, _loadFactor));
            _version++;
        }

        return _entries.Length;
    }

    /// <summary>
    /// Reduces the backing table to the smallest valid size that still holds the current
    /// <see cref="Count"/> without resizing, reclaiming memory. The order is preserved.
    /// </summary>
    public void TrimExcess() => TrimExcess(_count);

    /// <summary>
    /// Reduces (or grows) the backing table to the smallest valid size that holds at least
    /// <paramref name="capacity"/> entries without resizing. The order is preserved.
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

        int buckets = FastUtils.MinTableSizeFor(capacity, _loadFactor);
        if (buckets != _buckets.Length)
        {
            Rebuild(buckets);
            _version++;
        }
    }

    /// <summary>
    /// Returns an allocation-free enumerator that yields each key/value pair in insertion order,
    /// oldest first (as rearranged by <see cref="MoveToEnd"/> and <see cref="MoveToFront"/>). If the
    /// dictionary is modified during enumeration, <see cref="Enumerator.MoveNext"/> throws
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over this dictionary.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    /// <summary>
    /// Gets an enumerable view over the keys in the dictionary, in insertion order. The view is a
    /// lightweight struct and iterating it does not allocate.
    /// </summary>
    public KeyCollection Keys => new KeyCollection(this);

    /// <summary>
    /// Gets an enumerable view over the values in the dictionary, in insertion order. The view is a
    /// lightweight struct and iterating it does not allocate.
    /// </summary>
    public ValueCollection Values => new ValueCollection(this);

    /// <summary>
    /// Copies every key/value pair into <paramref name="array"/> starting at
    /// <paramref name="arrayIndex"/>, in insertion order.
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
        foreach (KeyValuePair<TKey, TValue?> entry in this)
            array[i++] = entry;
    }

    /// <summary>
    /// A struct enumerator over a <see cref="LinkedDictionary{TKey, TValue, THasher}"/>, in insertion
    /// order. Because it is a struct, iterating it via <c>foreach</c> avoids the
    /// allocation that a compiler-generated <c>IEnumerator&lt;T&gt;</c> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue?>>
    {
        private readonly LinkedDictionary<TKey, TValue, THasher> _dict;
        private readonly int _version;

        // The entry array as of construction. Only a rebuild replaces it, and a rebuild bumps the
        // version, so MoveNext never reads an entry from a stale array.
        private readonly Entry[] _entries;
        private int _next;
        private KeyValuePair<TKey, TValue?> _current;

        internal Enumerator(LinkedDictionary<TKey, TValue, THasher> dict)
        {
            _dict = dict;
            _version = dict._version;
            _entries = dict._entries;
            _next = dict._head;
            _current = default;
        }

        /// <summary>
        /// Gets the key/value pair at the current position of the enumerator.
        /// </summary>
        public KeyValuePair<TKey, TValue?> Current => _current;

        object IEnumerator.Current => _current;

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
        public bool MoveNext()
        {
            if (_version != _dict._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            if (_next == Nil)
            {
                _current = default;
                return false;
            }

            // _next is always a live entry of _entries: the order list only links live entries, and
            // any change to it bumps the version checked above. The step is one dependent load per
            // entry, so skipping the bounds check is what keeps it close to a pointer chase.
            ref Entry entry = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_entries), (nint)(uint)_next);
            _current = new KeyValuePair<TKey, TValue?>(entry.Key, entry.Value);
            _next = entry.Next;
            return true;
        }

        /// <summary>
        /// Resets the enumerator to its initial position, before the first entry.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the dictionary was modified since the enumerator was created.
        /// </exception>
        public void Reset()
        {
            if (_version != _dict._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _next = _dict._head;
            _current = default;
        }

        /// <summary>
        /// Releases any resources held by the enumerator. No-op for this type.
        /// </summary>
        public void Dispose() { }
    }

    /// <summary>
    /// A struct enumerable view over the keys of a
    /// <see cref="LinkedDictionary{TKey, TValue, THasher}"/>, in insertion order. Iterating it does
    /// not allocate; passing it through <see cref="IEnumerable{T}"/> will box the
    /// enumerator and is therefore not zero-allocation. It is a read-only
    /// <see cref="ICollection{T}"/>: the mutating members throw
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    public readonly struct KeyCollection : ICollection<TKey>
    {
        private readonly LinkedDictionary<TKey, TValue, THasher> _dict;

        internal KeyCollection(LinkedDictionary<TKey, TValue, THasher> dict) => _dict = dict;

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
        /// in insertion order.
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
            foreach (KeyValuePair<TKey, TValue?> entry in _dict)
                array[i++] = entry.Key;
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
        /// <see cref="LinkedDictionary{TKey, TValue, THasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TKey>
        {
            private LinkedDictionary<TKey, TValue, THasher>.Enumerator _inner;

            internal Enumerator(LinkedDictionary<TKey, TValue, THasher> dict) => _inner = dict.GetEnumerator();

            /// <summary>Gets the current key.</summary>
            public TKey Current => _inner.Current.Key;

            object? IEnumerator.Current => _inner.Current.Key;

            /// <summary>Advances to the next key.</summary>
            public bool MoveNext() => _inner.MoveNext();

            /// <summary>Resets the enumerator to its initial position.</summary>
            public void Reset() => _inner.Reset();

            /// <summary>No-op.</summary>
            public void Dispose() => _inner.Dispose();
        }
    }

    /// <summary>
    /// A struct enumerable view over the values of a
    /// <see cref="LinkedDictionary{TKey, TValue, THasher}"/>, in insertion order. Iterating it does
    /// not allocate; passing it through <see cref="IEnumerable{T}"/> will box the
    /// enumerator and is therefore not zero-allocation. It is a read-only
    /// <see cref="ICollection{T}"/>: the mutating members throw
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    public readonly struct ValueCollection : ICollection<TValue?>
    {
        private readonly LinkedDictionary<TKey, TValue, THasher> _dict;

        internal ValueCollection(LinkedDictionary<TKey, TValue, THasher> dict) => _dict = dict;

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

        /// <summary>Determines whether any entry holds <paramref name="item"/>. This is an <c>O(n)</c> scan.</summary>
        /// <param name="item">The value to look for.</param>
        /// <returns><c>true</c> if at least one entry holds the value.</returns>
        public bool Contains(TValue? item) => _dict.ContainsValue(item);

        /// <summary>
        /// Copies the values into <paramref name="array"/> starting at <paramref name="arrayIndex"/>,
        /// in insertion order.
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
            foreach (KeyValuePair<TKey, TValue?> entry in _dict)
                array[i++] = entry.Value;
        }

        IEnumerator<TValue?> IEnumerable<TValue?>.GetEnumerator() => new Enumerator(_dict);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_dict);

        void ICollection<TValue?>.Add(TValue? item) =>
            throw new NotSupportedException("The value view is read-only.");

        void ICollection<TValue?>.Clear() =>
            throw new NotSupportedException("The value view is read-only.");

        bool ICollection<TValue?>.Remove(TValue? item) =>
            throw new NotSupportedException("The value view is read-only.");

        /// <summary>
        /// A struct enumerator over the values of a
        /// <see cref="LinkedDictionary{TKey, TValue, THasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TValue?>
        {
            private LinkedDictionary<TKey, TValue, THasher>.Enumerator _inner;

            internal Enumerator(LinkedDictionary<TKey, TValue, THasher> dict) => _inner = dict.GetEnumerator();

            /// <summary>Gets the current value.</summary>
            public TValue? Current => _inner.Current.Value;

            object? IEnumerator.Current => _inner.Current.Value;

            /// <summary>Advances to the next value.</summary>
            public bool MoveNext() => _inner.MoveNext();

            /// <summary>Resets the enumerator to its initial position.</summary>
            public void Reset() => _inner.Reset();

            /// <summary>No-op.</summary>
            public void Dispose() => _inner.Dispose();
        }
    }

    // IReadOnlyDictionary<TKey, TValue?> explicit interface members. The primary
    // (non-interface) surface already covers the interface contract; these
    // forwarders only widen those members to the boxed IEnumerable<T> /
    // IEnumerator<T> shapes the interface requires.
    TValue? IReadOnlyDictionary<TKey, TValue?>.this[TKey key] => this[key];

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue?>.Keys => Keys;

    IEnumerable<TValue?> IReadOnlyDictionary<TKey, TValue?>.Values => Values;

    IEnumerator<KeyValuePair<TKey, TValue?>> IEnumerable<KeyValuePair<TKey, TValue?>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // IDictionary<TKey, TValue?> explicit interface members. Everything the mutable
    // interface adds over the read-only one is already public — Add, Remove, Clear,
    // the indexer, CopyTo — so these forwarders only reshape Keys / Values to
    // ICollection<T> and supply the ICollection<KeyValuePair<,>> members, leaving
    // every existing public signature untouched.
    TValue? IDictionary<TKey, TValue?>.this[TKey key]
    {
        get => this[key];
        set => this[key] = value!;
    }

    ICollection<TKey> IDictionary<TKey, TValue?>.Keys => Keys;

    ICollection<TValue?> IDictionary<TKey, TValue?>.Values => Values;

    void IDictionary<TKey, TValue?>.Add(TKey key, TValue? value) => Add(key, value!);

    bool ICollection<KeyValuePair<TKey, TValue?>>.IsReadOnly => false;

    void ICollection<KeyValuePair<TKey, TValue?>>.Add(KeyValuePair<TKey, TValue?> item) =>
        Add(item.Key, item.Value!);

    bool ICollection<KeyValuePair<TKey, TValue?>>.Contains(KeyValuePair<TKey, TValue?> item) =>
        TryGetValue(item.Key, out TValue? value) && EqualityComparer<TValue?>.Default.Equals(value, item.Value);

    bool ICollection<KeyValuePair<TKey, TValue?>>.Remove(KeyValuePair<TKey, TValue?> item)
    {
        // ICollection<KVP> semantics: remove only when the *pair* matches, so a stale value must not
        // delete the current entry.
        if (!TryGetValue(item.Key, out TValue? value) ||
            !EqualityComparer<TValue?>.Default.Equals(value, item.Value))
        {
            return false;
        }

        return Remove(item.Key);
    }

    // The number of entries a table of `buckets` buckets holds before it grows. Never zero, so even a
    // one-bucket table with a tiny load factor can take its first entry.
    private static int EntryCapacityFor(int buckets, float loadFactor) =>
        Math.Max(1, (int)(buckets * loadFactor));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HashOf(TKey key)
    {
        // default(TKey) — a null reference, a Nullable<T> without a value, or 0 for an integer — never
        // reaches the hasher, which may not accept it. Hash code 0 puts it in the table like any other
        // key, so it keeps its place in the order. EmptySlot.Is costs one intrinsic comparison and
        // never boxes, even in an unoptimized build.
        if (EmptySlot.Is(key))
            return 0;
        return default(THasher).Hash(key);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Matches(ref Entry entry, TKey key, int hash) =>
        // For a value-type key the comparer is a JIT intrinsic, as cheap as the stored-hash test, so
        // the hash test only pays for itself on reference keys, whose Equals may be expensive.
        // typeof(TKey).IsValueType is a JIT-time constant, so each instantiation keeps one form.
        typeof(TKey).IsValueType
            ? EqualityComparer<TKey>.Default.Equals(entry.Key, key)
            : entry.Hash == hash && EqualityComparer<TKey>.Default.Equals(entry.Key, key);

    // Returns the entry holding `key`, or Nil when it is absent.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Find(TKey key, int hash)
    {
        Entry[] entries = _entries;
        int index = _buckets[hash & _mask] - 1;
        while (index != Nil)
        {
            ref Entry entry = ref entries[index];
            if (Matches(ref entry, key, hash))
                return index;
            index = entry.ChainNext;
        }
        return Nil;
    }

    // Appends a key the caller has just found absent, growing the table first when every entry is
    // in use.
    private void AddNew(TKey key, TValue? value, int hash)
    {
        int index;
        if (_freeList != Nil)
        {
            index = _freeList;
            _freeList = _entries[index].ChainNext;
        }
        else
        {
            if (_used == _entries.Length)
                Grow();
            index = _used++;
        }

        ref Entry entry = ref _entries[index];
        entry.Hash = hash;
        entry.Key = key;
        entry.Value = value;

        ref int bucket = ref _buckets[hash & _mask];
        entry.ChainNext = bucket - 1;
        bucket = index + 1;

        LinkLast(index);
        _count++;
        _version++;
    }

    // Removes entry `index`, whose predecessor in its hash chain is `chainPrev` (Nil when it heads the
    // chain), from the chain and the order list, and frees its slot.
    private void RemoveEntry(int index, int chainPrev)
    {
        ref Entry entry = ref _entries[index];
        if (chainPrev == Nil)
            _buckets[entry.Hash & _mask] = entry.ChainNext + 1;
        else
            _entries[chainPrev].ChainNext = entry.ChainNext;

        Unlink(index);

        // Release the key and value so the table keeps no reference alive, and push the slot onto
        // the free list for the next add.
        entry.Key = default!;
        entry.Value = default;
        entry.ChainNext = _freeList;
        _freeList = index;
        _count--;
        _version++;
    }

    private bool TryGetAt(int index, out TKey key, out TValue? value)
    {
        if (index == Nil)
        {
            key = default!;
            value = default;
            return false;
        }

        ref Entry entry = ref _entries[index];
        key = entry.Key;
        value = entry.Value;
        return true;
    }

    private bool TryRemoveAt(int index, out TKey key, out TValue? value)
    {
        if (!TryGetAt(index, out key, out value))
            return false;

        // Find the entry's predecessor in its hash chain. Chains average under one entry at the
        // load factors the constructor admits, so this walk is short.
        int chainPrev = Nil;
        int cursor = _buckets[_entries[index].Hash & _mask] - 1;
        while (cursor != index)
        {
            chainPrev = cursor;
            cursor = _entries[cursor].ChainNext;
        }

        RemoveEntry(index, chainPrev);
        return true;
    }

    // Takes entry `index` out of the order list, leaving its own links stale.
    private void Unlink(int index)
    {
        ref Entry entry = ref _entries[index];
        int prev = entry.Prev;
        int next = entry.Next;

        if (prev == Nil)
            _head = next;
        else
            _entries[prev].Next = next;

        if (next == Nil)
            _tail = prev;
        else
            _entries[next].Prev = prev;
    }

    private void LinkLast(int index)
    {
        ref Entry entry = ref _entries[index];
        entry.Prev = _tail;
        entry.Next = Nil;

        if (_tail == Nil)
            _head = index;
        else
            _entries[_tail].Next = index;
        _tail = index;
    }

    private void LinkFirst(int index)
    {
        ref Entry entry = ref _entries[index];
        entry.Prev = Nil;
        entry.Next = _head;

        // Only called for an entry that was not already the head, so the list it rejoins is
        // never empty.
        _entries[_head].Prev = index;
        _head = index;
    }

    // Doubles the bucket count until the entry capacity it buys exceeds Count. One doubling is
    // enough at ordinary load factors; a tiny one can need several before the truncated capacity
    // moves past one entry.
    private void Grow()
    {
        int buckets = _buckets.Length;
        do
        {
            buckets = FastUtils.DoubleCapacity(buckets);
        }
        while (EntryCapacityFor(buckets, _loadFactor) <= _count);

        Rebuild(buckets);
    }

    // Rebuilds the table at `buckets` buckets, copying the live entries in insertion order into a
    // fresh array. That compacts away every free slot and leaves entry i followed by entry i + 1,
    // so a later enumeration walks memory sequentially. The hasher is not called: each entry keeps
    // its hash. The caller guarantees the new entry capacity is at least Count.
    private void Rebuild(int buckets)
    {
        Entry[] oldEntries = _entries;
        var newBuckets = new int[buckets];
        var newEntries = new Entry[EntryCapacityFor(buckets, _loadFactor)];
        int mask = buckets - 1;

        int j = 0;
        for (int i = _head; i != Nil; i = oldEntries[i].Next)
        {
            ref Entry source = ref oldEntries[i];
            ref Entry target = ref newEntries[j];
            target.Hash = source.Hash;
            target.Key = source.Key;
            target.Value = source.Value;
            target.Prev = j - 1;
            target.Next = j + 1;

            ref int bucket = ref newBuckets[source.Hash & mask];
            target.ChainNext = bucket - 1;
            bucket = j + 1;
            j++;
        }

        if (j > 0)
            newEntries[j - 1].Next = Nil;

        _buckets = newBuckets;
        _entries = newEntries;
        _mask = mask;
        _used = j;
        _freeList = Nil;
        _head = j > 0 ? 0 : Nil;
        _tail = j - 1;
    }
}
