using System.Collections;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A fixed-capacity cache with <b>SIEVE eviction</b> (Zhang et al., NSDI 2024): an <c>O(1)</c>
/// get/put map whose hits set a per-entry <i>visited</i> bit instead of reordering anything, and
/// whose evictions sweep a "hand" over an insertion-ordered queue to drop the first entry that was
/// not used since the hand last passed it. Parameterized on a custom <see cref="IHashProvider{T}"/>
/// so key hashing devirtualizes and inlines.
/// </summary>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TValue">The type of the cached values.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute key hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
/// <remarks>
/// <para>
/// <b>The policy.</b> Entries sit in a queue in insertion order, newest at the head. A hit marks
/// the entry visited and leaves it where it is. When an insert finds the cache full, the hand —
/// which starts at the oldest entry and remembers where it stopped — walks toward the newest entry,
/// clearing each visited bit it passes, and evicts the first entry whose bit is already clear; past
/// the newest entry it wraps back to the oldest. The new entry goes in at the head, unvisited. So an
/// entry that is never read again after insertion is evicted on the hand's next pass, while one that
/// keeps being read survives every pass. On skewed workloads this gives a lower miss ratio than LRU:
/// on a seeded Zipf(1.0) trace of 1,000,000 requests over 100,000 keys at capacity 1,024 it misses
/// less often than <see cref="LruCache{TKey, TValue, THasher}"/>, which a test pins. LRU still wins
/// on workloads whose recency is the whole signal, such as a working set that drifts and then
/// returns, so measure against the real trace before switching.
/// </para>
/// <para>
/// <b>Reads are not structural.</b> Unlike <see cref="LruCache{TKey, TValue, THasher}"/>, whose
/// hits relink the entry at the head, a hit here writes one flag. The indexer getter,
/// <see cref="TryGet(TKey, out TValue)"/> and an <see cref="AddOrUpdate(TKey, TValue)"/> that
/// overwrites an existing key therefore leave the queue order and any active enumerator untouched.
/// Only an insert, an eviction, a successful <see cref="Remove(TKey)"/> and a non-empty <see cref="Clear"/>
/// invalidate enumerators. A hit does change which entry the next eviction drops, which is the point
/// of the policy, so <see cref="TryPeek(TKey, out TValue)"/> and <see cref="ContainsKey(TKey)"/> exist
/// to inspect the cache without counting as a use.
/// </para>
/// <para>
/// <b>Cost.</b> A hit or a miss is one key-index lookup plus <c>O(1)</c> work, and allocates nothing.
/// An eviction is <c>O(1)</c> amortized: every visited bit the hand clears was set by an earlier hit,
/// so the total sweep work never exceeds the inserts plus the uses (hits and overwrites). A <i>single</i> eviction can
/// still sweep the whole queue, when every entry was read since the hand last passed it. Node storage
/// (sized to <see cref="Capacity"/>) and the key index are allocated once at construction, so the hot
/// get/put/evict path performs no allocation.
/// </para>
/// <para>This type is not thread-safe; concurrent callers must synchronize externally.</para>
/// </remarks>
public class SieveCache<TKey, TValue, THasher>
    : IReadOnlyCollection<KeyValuePair<TKey, TValue?>>
    where THasher : struct, IHashProvider<TKey>
{
    // Sentinel for "no node" in the queue links, the free stack and the hand.
    private const int Nil = -1;

    // Key -> node slot index, chained through the node slots themselves. A node slot never moves
    // while its entry is resident, and a hit does not touch the queue at all, so only inserts,
    // evictions and removals mutate it. It owns each slot's key (_index.Keys, aliased as _keys).
    private readonly CacheKeyIndex<TKey, THasher> _index;
    private readonly TKey[] _keys;

    // Fixed-size node storage (length == capacity), keys aside. One array of structs, so a hit's value
    // read and visited-bit write land on the same cache line, as do the hand's bit test and link read.
    // Occupied nodes form the head (newest) .. tail (oldest) queue via Prev/Next; free nodes form a
    // singly-linked stack via Next, rooted at _freeHead.
    private readonly Node[] _nodes;

    private readonly int _capacity;
    private int _count;
    private int _head;      // newest node, or Nil when empty
    private int _tail;      // oldest node, or Nil when empty
    private int _hand;      // where the next eviction sweep starts, or Nil to start at the tail
    private int _freeHead;  // top of the free-node stack, or Nil when full

    // Incremented on every structural mutation (insert, evict, remove, non-empty clear), so active
    // enumerators can detect concurrent modification and throw. Hits deliberately do not bump it.
    private int _version;

    private struct Node
    {
        public TValue? Value;
        public int Prev;
        public int Next;
        public bool Visited;
    }

    /// <summary>
    /// Initializes a new empty cache that holds at most <paramref name="capacity"/> entries.
    /// </summary>
    /// <param name="capacity">The maximum number of entries the cache retains. Must be at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than 1.</exception>
    public SieveCache(int capacity)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least 1.");

        _capacity = capacity;
        _nodes = new Node[capacity];
        _index = new CacheKeyIndex<TKey, THasher>(capacity);
        _keys = _index.Keys;

        InitFreeStack();
        _head = Nil;
        _tail = Nil;
        _hand = Nil;
        _count = 0;
    }

    /// <summary>
    /// Initializes a new cache with the given <paramref name="capacity"/> and primes it from
    /// <paramref name="source"/>. Pairs are inserted in enumeration order through
    /// <see cref="AddOrUpdate(TKey, TValue)"/>, so a later duplicate key overwrites the earlier value
    /// and counts as a use of it, and a source with more than <paramref name="capacity"/> distinct
    /// keys evicts under the SIEVE policy as it goes.
    /// </summary>
    /// <param name="capacity">The maximum number of entries the cache retains. Must be at least 1.</param>
    /// <param name="source">The key/value pairs to seed the cache with.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than 1.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public SieveCache(int capacity, IEnumerable<KeyValuePair<TKey, TValue?>> source)
        : this(NullChecked(source, capacity))
    {
        foreach (KeyValuePair<TKey, TValue?> pair in source)
            AddOrUpdate(pair.Key, pair.Value);
    }

    // Runs as part of the chained-ctor argument expression so the null check beats the primary
    // ctor's capacity validation: a null source must surface as ArgumentNullException, not
    // ArgumentOutOfRangeException, even when the caller also passed an invalid capacity.
    private static int NullChecked(IEnumerable<KeyValuePair<TKey, TValue?>> source, int capacity)
    {
        ArgumentNullException.ThrowIfNull(source);
        return capacity;
    }

    /// <summary>
    /// Gets the maximum number of entries the cache retains before an insert evicts one.
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
    /// The getter is a <b>use</b>: it marks the entry visited, so the next eviction sweep spares it
    /// once, and throws <see cref="KeyNotFoundException"/> if the key is absent. The setter behaves
    /// like <see cref="AddOrUpdate(TKey, TValue)"/>. Neither a get nor an overwrite reorders the queue
    /// or invalidates active enumerators.
    /// </remarks>
    /// <exception cref="KeyNotFoundException">The key is not present (getter only).</exception>
    public TValue? this[TKey key]
    {
        get
        {
            int node = _index.Find(key);
            if (node < 0)
                throw new KeyNotFoundException($"Key {key} not found.");
            ref Node n = ref _nodes[node];
            n.Visited = true;
            return n.Value;
        }
        set => AddOrUpdate(key, value);
    }

    /// <summary>
    /// Attempts to get the value associated with <paramref name="key"/>, marking the entry visited
    /// on a hit.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">
    /// When this method returns, contains the cached value if the key was found; otherwise the default
    /// value of <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><c>true</c> if the key was found; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// A hit changes which entry the next eviction drops, but not the queue order, so it leaves
    /// active enumerators valid.
    /// </remarks>
    public bool TryGet(TKey key, out TValue? value)
    {
        int node = _index.Find(key);
        if (node < 0)
        {
            value = default;
            return false;
        }
        ref Node n = ref _nodes[node];
        n.Visited = true;
        value = n.Value;
        return true;
    }

    /// <summary>
    /// Attempts to read the value associated with <paramref name="key"/> <b>without</b> marking it
    /// visited (a peek does not count as a use), so it does not change which entry the next eviction
    /// drops.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">
    /// When this method returns, contains the cached value if the key was found; otherwise the default
    /// value of <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><c>true</c> if the key was found; otherwise <c>false</c>.</returns>
    public bool TryPeek(TKey key, out TValue? value)
    {
        int node = _index.Find(key);
        if (node < 0)
        {
            value = default;
            return false;
        }
        value = _nodes[node].Value;
        return true;
    }

    /// <summary>
    /// Determines whether <paramref name="key"/> is present in the cache. This is a peek: it does not
    /// mark the entry visited.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns><c>true</c> if the key is found; otherwise <c>false</c>.</returns>
    public bool ContainsKey(TKey key) => _index.Find(key) >= 0;

    /// <summary>
    /// Adds <paramref name="key"/> with <paramref name="value"/> as the newest, unvisited entry,
    /// evicting one entry under the SIEVE policy first if the cache is at capacity, or overwrites the
    /// value of an existing key and marks it visited.
    /// </summary>
    /// <param name="key">The key to add or update.</param>
    /// <param name="value">The value to associate with <paramref name="key"/>.</param>
    /// <remarks>
    /// An overwrite counts as a use, as a <see cref="TryGet(TKey, out TValue)"/> hit does, and like
    /// one it leaves the queue order and active enumerators untouched.
    /// </remarks>
    public void AddOrUpdate(TKey key, TValue? value)
    {
        int node = _index.Find(key);
        if (node >= 0)
        {
            ref Node n = ref _nodes[node];
            n.Value = value;
            n.Visited = true;
            return;
        }

        InsertNew(key, value);
        _version++;
    }

    /// <summary>
    /// Adds <paramref name="key"/> with <paramref name="value"/> as the newest, unvisited entry,
    /// evicting one entry under the SIEVE policy first if the cache is at capacity.
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
    /// Attempts to add <paramref name="key"/> with <paramref name="value"/> as the newest, unvisited
    /// entry, evicting one entry under the SIEVE policy first if the cache is at capacity.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <returns>
    /// <c>true</c> if the entry was added; <c>false</c> if the key already exists (the cache is left
    /// unchanged, including the existing entry's visited bit).
    /// </returns>
    public bool TryAdd(TKey key, TValue? value)
    {
        if (_index.Find(key) >= 0)
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
    /// <remarks>
    /// If the hand rests on the removed entry, it moves to the next-newer one, exactly as it would
    /// after evicting it, so the sweep resumes where it would have.
    /// </remarks>
    public bool Remove(TKey key, out TValue? value)
    {
        int node = _index.Find(key);
        if (node < 0)
        {
            value = default;
            return false;
        }

        value = _nodes[node].Value;
        _index.Remove(node);
        if (node == _hand)
            _hand = _nodes[node].Prev;
        Unlink(node);
        FreeNode(node);
        _count--;
        _version++;
        return true;
    }

    /// <summary>
    /// Removes all entries from the cache and resets the hand. The backing storage (sized to
    /// <see cref="Capacity"/>) is retained.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        _index.Clear();
        Array.Clear(_nodes, 0, _nodes.Length);
        InitFreeStack();
        _head = Nil;
        _tail = Nil;
        _hand = Nil;
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Returns an allocation-free struct enumerator that yields each entry in <b>newest-inserted to
    /// oldest-inserted</b> order. Enumeration is a peek: it does not mark entries visited.
    /// <see cref="Enumerator.MoveNext"/> throws <see cref="InvalidOperationException"/> when the entry
    /// set changed since the enumerator was taken: an insert, an eviction, a successful <see cref="Remove(TKey)"/>
    /// or a <see cref="Clear"/> of a non-empty cache. Hits and value overwrites change neither the
    /// entry set nor the order, so they leave active enumerators valid.
    /// </summary>
    /// <returns>A struct enumerator over this cache.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<KeyValuePair<TKey, TValue?>> IEnumerable<KeyValuePair<TKey, TValue?>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- internal queue / free-stack / hand machinery -------------------------------------------

    private void InitFreeStack()
    {
        for (int i = 0; i < _capacity - 1; i++)
            _nodes[i].Next = i + 1;
        _nodes[_capacity - 1].Next = Nil;
        _freeHead = 0;
    }

    // Inserts a brand-new key at the head, unvisited. Recycles the evicted node's slot when the
    // cache is full, otherwise pops a slot off the free stack. Caller guarantees the key is absent.
    private void InsertNew(TKey key, TValue? value)
    {
        int node;
        if (_count == _capacity)
        {
            node = Evict();
        }
        else
        {
            node = _freeHead;
            _freeHead = _nodes[node].Next;
            _count++;
        }

        _index.Insert(node, key);
        ref Node n = ref _nodes[node];
        n.Value = value;
        n.Visited = false;
        LinkAtHead(node);
    }

    // The SIEVE sweep. Starting at the hand (or the tail when the hand is unset), clear each visited
    // bit and step toward the head, wrapping to the tail past it; the first unvisited node is the
    // victim. The loop terminates within one full lap, since the lap clears every bit it passes. The
    // hand is left on the victim's newer neighbour, which survives the unlink. Returns the victim's
    // slot, unlinked and dropped from the index but with its storage not yet overwritten.
    private int Evict()
    {
        int node = _hand != Nil ? _hand : _tail;
        while (_nodes[node].Visited)
        {
            _nodes[node].Visited = false;
            node = _nodes[node].Prev;
            if (node == Nil)
                node = _tail;
        }

        _hand = _nodes[node].Prev;
        _index.Remove(node);
        Unlink(node);
        return node;
    }

    // Detaches a free node's value (the index already released its key) and pushes its slot onto
    // the free stack.
    private void FreeNode(int node)
    {
        ref Node n = ref _nodes[node];
        n.Value = default;
        n.Visited = false;
        n.Next = _freeHead;
        _freeHead = node;
    }

    // Removes an occupied node from the head..tail queue (leaves its storage untouched).
    private void Unlink(int node)
    {
        int p = _nodes[node].Prev;
        int n = _nodes[node].Next;
        if (p != Nil) _nodes[p].Next = n; else _head = n;
        if (n != Nil) _nodes[n].Prev = p; else _tail = p;
    }

    // Links an unlinked node at the head (newest end) of the queue.
    private void LinkAtHead(int node)
    {
        _nodes[node].Prev = Nil;
        _nodes[node].Next = _head;
        if (_head != Nil) _nodes[_head].Prev = node;
        _head = node;
        if (_tail == Nil) _tail = node;
    }

    /// <summary>
    /// A struct enumerator over a <see cref="SieveCache{TKey, TValue, THasher}"/> that yields entries
    /// in newest-inserted to oldest-inserted order. Because it is a struct, iterating it via
    /// <c>foreach</c> avoids the allocation a compiler-generated <c>IEnumerator&lt;T&gt;</c> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue?>>
    {
        private readonly SieveCache<TKey, TValue, THasher> _cache;
        private readonly int _version;
        private int _node;
        private bool _started;
        private KeyValuePair<TKey, TValue?> _current;

        internal Enumerator(SieveCache<TKey, TValue, THasher> cache)
        {
            _cache = cache;
            _version = cache._version;
            _node = Nil;
            _started = false;
            _current = default;
        }

        /// <summary>Gets the entry at the current position of the enumerator.</summary>
        public KeyValuePair<TKey, TValue?> Current => _current;

        object IEnumerator.Current => _current;

        /// <summary>
        /// Advances the enumerator to the next entry (toward the oldest-inserted one).
        /// </summary>
        /// <returns><c>true</c> if the enumerator advanced to a new entry; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The cache was modified since the enumerator was created.</exception>
        public bool MoveNext()
        {
            if (_version != _cache._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            if (_started && _node == Nil)
                return false; // already exhausted

            _node = _started ? _cache._nodes[_node].Next : _cache._head;
            _started = true;

            if (_node == Nil)
            {
                _current = default;
                return false;
            }

            _current = new KeyValuePair<TKey, TValue?>(_cache._keys[_node], _cache._nodes[_node].Value);
            return true;
        }

        /// <summary>Resets the enumerator to its initial position, before the newest entry.</summary>
        /// <exception cref="InvalidOperationException">The cache was modified since the enumerator was created.</exception>
        public void Reset()
        {
            if (_version != _cache._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _node = Nil;
            _started = false;
            _current = default;
        }

        /// <summary>Releases any resources held by the enumerator. No-op for this type.</summary>
        public void Dispose() { }
    }
}
