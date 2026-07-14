using System.Collections;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A fixed-capacity <b>least-recently-used (LRU) cache</b>: an <c>O(1)</c> get/put map that
/// automatically evicts the least-recently-used entry when a new key would push the count past
/// <see cref="Capacity"/>. Parameterized on a custom <see cref="IHashProvider{T}"/> so key hashing
/// devirtualizes and inlines.
/// </summary>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TValue">The type of the cached values.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute key hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
/// <remarks>
/// <para>
/// The BCL ships no bounded LRU cache. The idiomatic .NET LRU pairs a
/// <c>Dictionary&lt;TKey, LinkedListNode&lt;(TKey, TValue)&gt;&gt;</c> with a
/// <c>LinkedList&lt;(TKey, TValue)&gt;</c>, which heap-allocates a <c>LinkedListNode</c> per
/// insertion and chases pointers scattered across the managed heap. <see cref="LruCache{TKey, TValue, THasher}"/>
/// instead keeps its recency order in an <b>intrusive doubly-linked list threaded through fixed-size
/// node arrays</b> (allocated once, sized to <see cref="Capacity"/>) and an open-addressed
/// key&#8594;node-slot index. The node slot is stable across recency reordering, so a cache hit never
/// touches the index; after construction the hot get/put/evict path performs <b>no allocation at all</b>.
/// The documented BCL-beating workload is a hot, bounded cache under continuous eviction churn
/// (memoize the last <c>N</c> results), where the array-backed list wins on allocation and locality.
/// </para>
/// <para>
/// <b>Reads are mutating.</b> LRU semantics require a lookup to count as a use, so the indexer getter
/// and <see cref="TryGet(TKey, out TValue)"/> promote the entry to most-recently-used and therefore
/// invalidate any in-progress enumerator (matching "collection was modified" semantics). Use
/// <see cref="TryPeek(TKey, out TValue)"/> or <see cref="ContainsKey(TKey)"/> to inspect the cache
/// without disturbing recency order.
/// </para>
/// <para>This type is not thread-safe; concurrent callers must synchronize externally.</para>
/// </remarks>
public class LruCache<TKey, TValue, THasher>
    : IReadOnlyCollection<KeyValuePair<TKey, TValue?>>
    where THasher : struct, IHashProvider<TKey>
{
    // Sentinel for "no node" in the intrusive list / free stack.
    private const int NIL = -1;

    // Key -> node slot index. Dogfoods CelerityDictionary; because a node slot index is stable
    // across recency reordering, a cache hit (which only relinks the list) never mutates this map.
    private readonly CelerityDictionary<TKey, int, THasher> _index;

    // Fixed-size node storage (length == capacity). Occupied nodes form the MRU..LRU recency chain
    // via _prev/_next; free nodes form a singly-linked stack via _next (rooted at _freeHead).
    private readonly TKey[] _nodeKeys;
    private readonly TValue?[] _nodeValues;
    private readonly int[] _prev;
    private readonly int[] _next;

    private readonly int _capacity;
    private int _count;
    private int _head;      // most-recently-used node, or NIL when empty
    private int _tail;      // least-recently-used node, or NIL when empty
    private int _freeHead;  // top of the free-node stack, or NIL when full

    // Incremented on every structural mutation (insert, evict, remove, clear) and on every recency
    // reorder, so active enumerators can detect concurrent modification and throw.
    private int _version;

    /// <summary>
    /// Initializes a new empty cache that holds at most <paramref name="capacity"/> entries.
    /// </summary>
    /// <param name="capacity">The maximum number of entries the cache retains. Must be at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than 1.</exception>
    public LruCache(int capacity)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least 1.");

        _capacity = capacity;
        _nodeKeys = new TKey[capacity];
        _nodeValues = new TValue?[capacity];
        _prev = new int[capacity];
        _next = new int[capacity];

        InitFreeStack();
        _head = NIL;
        _tail = NIL;
        _count = 0;

        // Pre-size the index for the full capacity so it never resizes during steady-state churn.
        _index = new CelerityDictionary<TKey, int, THasher>(capacity);
        _index.EnsureCapacity(capacity);
    }

    /// <summary>
    /// Initializes a new cache with the given <paramref name="capacity"/> and primes it from
    /// <paramref name="source"/>. Pairs are inserted in enumeration order, so if the source yields
    /// more than <paramref name="capacity"/> distinct keys the earliest ones are evicted and the last
    /// <paramref name="capacity"/> survive as the most-recently-used entries. A later duplicate key
    /// overwrites the earlier value and promotes it to most-recently-used.
    /// </summary>
    /// <param name="capacity">The maximum number of entries the cache retains. Must be at least 1.</param>
    /// <param name="source">The key/value pairs to seed the cache with.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than 1.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public LruCache(int capacity, IEnumerable<KeyValuePair<TKey, TValue?>> source)
        : this(capacity)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (KeyValuePair<TKey, TValue?> pair in source)
            AddOrUpdate(pair.Key, pair.Value);
    }

    /// <summary>
    /// Gets the maximum number of entries the cache retains before evicting the least-recently-used one.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Gets the number of entries currently in the cache (never greater than <see cref="Capacity"/>).
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the value to get or set.</param>
    /// <returns>The value associated with <paramref name="key"/>.</returns>
    /// <remarks>
    /// The getter is a <b>use</b>: it promotes the entry to most-recently-used and throws
    /// <see cref="KeyNotFoundException"/> if the key is absent. The setter adds the key (evicting the
    /// least-recently-used entry first if the cache is full) or overwrites an existing value, and in
    /// either case promotes the entry to most-recently-used.
    /// </remarks>
    /// <exception cref="KeyNotFoundException">The key is not present (getter only).</exception>
    public TValue? this[TKey key]
    {
        get
        {
            if (!_index.TryGetValue(key, out int node))
                throw new KeyNotFoundException($"Key {key} not found.");
            if (MoveToHeadIfNeeded(node))
                _version++;
            return _nodeValues[node];
        }
        set => AddOrUpdate(key, value);
    }

    /// <summary>
    /// Attempts to get the value associated with <paramref name="key"/>, promoting the entry to
    /// most-recently-used on a hit.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">
    /// When this method returns, contains the cached value if the key was found; otherwise the default
    /// value of <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><c>true</c> if the key was found; otherwise <c>false</c>.</returns>
    /// <remarks>A hit reorders the recency list and therefore invalidates active enumerators.</remarks>
    public bool TryGet(TKey key, out TValue? value)
    {
        if (!_index.TryGetValue(key, out int node))
        {
            value = default;
            return false;
        }
        if (MoveToHeadIfNeeded(node))
            _version++;
        value = _nodeValues[node];
        return true;
    }

    /// <summary>
    /// Attempts to read the value associated with <paramref name="key"/> <b>without</b> changing its
    /// recency (a peek does not count as a use), so it does not disturb the eviction order or
    /// invalidate active enumerators.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">
    /// When this method returns, contains the cached value if the key was found; otherwise the default
    /// value of <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><c>true</c> if the key was found; otherwise <c>false</c>.</returns>
    public bool TryPeek(TKey key, out TValue? value)
    {
        if (!_index.TryGetValue(key, out int node))
        {
            value = default;
            return false;
        }
        value = _nodeValues[node];
        return true;
    }

    /// <summary>
    /// Determines whether <paramref name="key"/> is present in the cache. This is a peek: it does not
    /// change the entry's recency.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns><c>true</c> if the key is found; otherwise <c>false</c>.</returns>
    public bool ContainsKey(TKey key) => _index.ContainsKey(key);

    /// <summary>
    /// Adds <paramref name="key"/> with <paramref name="value"/> as the most-recently-used entry,
    /// evicting the least-recently-used entry first if the cache is at capacity, or overwrites the
    /// value of an existing key and promotes it to most-recently-used.
    /// </summary>
    /// <param name="key">The key to add or update.</param>
    /// <param name="value">The value to associate with <paramref name="key"/>.</param>
    public void AddOrUpdate(TKey key, TValue? value)
    {
        if (_index.TryGetValue(key, out int node))
        {
            _nodeValues[node] = value;
            if (MoveToHeadIfNeeded(node))
                _version++;
            return;
        }

        InsertNew(key, value);
        _version++;
    }

    /// <summary>
    /// Adds <paramref name="key"/> with <paramref name="value"/> as the most-recently-used entry,
    /// evicting the least-recently-used entry first if the cache is at capacity.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <exception cref="ArgumentException">An element with the same <paramref name="key"/> already exists.</exception>
    public void Add(TKey key, TValue? value)
    {
        if (!TryAdd(key, value))
            throw new ArgumentException($"An element with key {key} already exists.", nameof(key));
    }

    /// <summary>
    /// Attempts to add <paramref name="key"/> with <paramref name="value"/> as the most-recently-used
    /// entry, evicting the least-recently-used entry first if the cache is at capacity.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <returns>
    /// <c>true</c> if the entry was added; <c>false</c> if the key already exists (the cache is left
    /// unchanged, including its recency order).
    /// </returns>
    public bool TryAdd(TKey key, TValue? value)
    {
        if (_index.ContainsKey(key))
            return false;

        InsertNew(key, value);
        _version++;
        return true;
    }

    /// <summary>
    /// Removes the entry with the specified key from the cache.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <returns><c>true</c> if the entry was removed; <c>false</c> if the key was not found.</returns>
    public bool Remove(TKey key) => Remove(key, out _);

    /// <summary>
    /// Removes the entry with the specified key from the cache and returns its value.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <param name="value">
    /// When this method returns, contains the value that was associated with <paramref name="key"/>
    /// before removal if the key was found; otherwise the default value of <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><c>true</c> if the entry was removed; <c>false</c> if the key was not found.</returns>
    public bool Remove(TKey key, out TValue? value)
    {
        if (!_index.TryGetValue(key, out int node))
        {
            value = default;
            return false;
        }

        value = _nodeValues[node];
        _index.Remove(key);
        Unlink(node);
        FreeNode(node);
        _count--;
        _version++;
        return true;
    }

    /// <summary>
    /// Removes all entries from the cache. The backing storage (sized to <see cref="Capacity"/>) is retained.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        _index.Clear();
        Array.Clear(_nodeKeys, 0, _nodeKeys.Length);
        Array.Clear(_nodeValues, 0, _nodeValues.Length);
        InitFreeStack();
        _head = NIL;
        _tail = NIL;
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Reads the least-recently-used entry — the one the next insert-when-full would evict — without
    /// changing recency order.
    /// </summary>
    /// <param name="key">When this method returns, the LRU key if the cache is non-empty; otherwise the default.</param>
    /// <param name="value">When this method returns, the LRU value if the cache is non-empty; otherwise the default.</param>
    /// <returns><c>true</c> if the cache is non-empty; otherwise <c>false</c>.</returns>
    public bool TryPeekLeastRecentlyUsed(out TKey? key, out TValue? value)
    {
        if (_tail == NIL)
        {
            key = default;
            value = default;
            return false;
        }
        key = _nodeKeys[_tail];
        value = _nodeValues[_tail];
        return true;
    }

    /// <summary>
    /// Reads the most-recently-used entry without changing recency order.
    /// </summary>
    /// <param name="key">When this method returns, the MRU key if the cache is non-empty; otherwise the default.</param>
    /// <param name="value">When this method returns, the MRU value if the cache is non-empty; otherwise the default.</param>
    /// <returns><c>true</c> if the cache is non-empty; otherwise <c>false</c>.</returns>
    public bool TryPeekMostRecentlyUsed(out TKey? key, out TValue? value)
    {
        if (_head == NIL)
        {
            key = default;
            value = default;
            return false;
        }
        key = _nodeKeys[_head];
        value = _nodeValues[_head];
        return true;
    }

    /// <summary>
    /// Returns an allocation-free struct enumerator that yields each entry in
    /// <b>most-recently-used to least-recently-used</b> order. Enumeration is a peek: it does not
    /// change recency. If the cache is modified during enumeration — including by a mutating read
    /// (<see cref="TryGet(TKey, out TValue)"/> or the indexer getter) — <see cref="Enumerator.MoveNext"/>
    /// throws <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over this cache.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<KeyValuePair<TKey, TValue?>> IEnumerable<KeyValuePair<TKey, TValue?>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- internal recency-list / free-stack machinery -------------------------------------------

    private void InitFreeStack()
    {
        for (int i = 0; i < _capacity - 1; i++)
            _next[i] = i + 1;
        _next[_capacity - 1] = NIL;
        _freeHead = 0;
    }

    // Inserts a brand-new key. Reuses the LRU node's slot when the cache is full (evicting it),
    // otherwise pops a slot off the free stack. Caller guarantees the key is absent.
    private void InsertNew(TKey key, TValue? value)
    {
        int node;
        if (_count == _capacity)
        {
            // Evict the least-recently-used entry and recycle its slot in place (no free-stack churn).
            node = _tail;
            _index.Remove(_nodeKeys[node]);
            Unlink(node);
        }
        else
        {
            node = _freeHead;
            _freeHead = _next[node];
            _count++;
        }

        _nodeKeys[node] = key;
        _nodeValues[node] = value;
        LinkAtHead(node);
        _index[key] = node;
    }

    // Detaches a free node's storage and pushes its slot onto the free stack.
    private void FreeNode(int node)
    {
        _nodeKeys[node] = default!;
        _nodeValues[node] = default;
        _next[node] = _freeHead;
        _freeHead = node;
    }

    // Removes an occupied node from the MRU..LRU chain (leaves its storage untouched).
    private void Unlink(int node)
    {
        int p = _prev[node];
        int n = _next[node];
        if (p != NIL) _next[p] = n; else _head = n;
        if (n != NIL) _prev[n] = p; else _tail = p;
    }

    // Links an unlinked node at the head (most-recently-used) of the chain.
    private void LinkAtHead(int node)
    {
        _prev[node] = NIL;
        _next[node] = _head;
        if (_head != NIL) _prev[_head] = node;
        _head = node;
        if (_tail == NIL) _tail = node;
    }

    // Promotes an occupied node to most-recently-used. Returns false (no relink) when it is already
    // the head, so a getter of the freshest entry does not spuriously invalidate enumerators.
    private bool MoveToHeadIfNeeded(int node)
    {
        if (node == _head)
            return false;
        Unlink(node);
        LinkAtHead(node);
        return true;
    }

    /// <summary>
    /// A struct enumerator over a <see cref="LruCache{TKey, TValue, THasher}"/> that yields entries in
    /// most-recently-used to least-recently-used order. Because it is a struct, iterating it via
    /// <c>foreach</c> avoids the allocation a compiler-generated <c>IEnumerator&lt;T&gt;</c> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue?>>
    {
        private readonly LruCache<TKey, TValue, THasher> _cache;
        private readonly int _version;
        private int _node;
        private bool _started;
        private KeyValuePair<TKey, TValue?> _current;

        internal Enumerator(LruCache<TKey, TValue, THasher> cache)
        {
            _cache = cache;
            _version = cache._version;
            _node = NIL;
            _started = false;
            _current = default;
        }

        /// <summary>Gets the entry at the current position of the enumerator.</summary>
        public KeyValuePair<TKey, TValue?> Current => _current;

        object IEnumerator.Current => _current;

        /// <summary>
        /// Advances the enumerator to the next entry (toward least-recently-used).
        /// </summary>
        /// <returns><c>true</c> if the enumerator advanced to a new entry; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The cache was modified since the enumerator was created.</exception>
        public bool MoveNext()
        {
            if (_version != _cache._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            if (_started && _node == NIL)
                return false; // already exhausted

            _node = _started ? _cache._next[_node] : _cache._head;
            _started = true;

            if (_node == NIL)
            {
                _current = default;
                return false;
            }

            _current = new KeyValuePair<TKey, TValue?>(_cache._nodeKeys[_node], _cache._nodeValues[_node]);
            return true;
        }

        /// <summary>Resets the enumerator to its initial position, before the most-recently-used entry.</summary>
        /// <exception cref="InvalidOperationException">The cache was modified since the enumerator was created.</exception>
        public void Reset()
        {
            if (_version != _cache._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _node = NIL;
            _started = false;
            _current = default;
        }

        /// <summary>Releases any resources held by the enumerator. No-op for this type.</summary>
        public void Dispose() { }
    }
}
