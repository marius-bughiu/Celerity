using System.Collections;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A fixed-capacity cache with <b>W-TinyLFU</b> eviction (Einziger, Eytan, Friedman and Manes, 2017;
/// the policy behind Java's Caffeine): a small LRU <i>window</i> that every new entry enters, in front
/// of a segmented-LRU <i>main</i> region guarded by a frequency filter, so an entry evicted from the
/// window replaces the main region's victim only if it has been accessed more often recently.
/// An <c>O(1)</c> get/put map, parameterized on a custom <see cref="IHashProvider{T}"/> so key hashing
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
/// <b>Why another policy.</b> Each of the library's other caches is right for one shape of traffic
/// and wrong for another. <see cref="LruCache{TKey, TValue, THasher}"/> sees only recency, so a scan
/// flushes its whole working set. <see cref="LfuCache{TKey, TValue, THasher}"/> survives the scan but
/// its counts never age, so yesterday's hot keys hold their slots against today's.
/// <see cref="SieveCache{TKey, TValue, THasher}"/> is cheap and good on skewed traffic, but it judges
/// a key only by what happened while that key was resident. W-TinyLFU remembers how often a key was
/// requested <i>whether or not it was resident</i>, forgets that history at a steady rate, and keeps a
/// recency window for bursts. On a loop over more keys than the cache holds, where each key returns
/// just after an LRU-ordered cache evicted it, the other three miss every request and this one keeps
/// most of the loop resident; on a stationary Zipf stream it is level with the best of them. The tests
/// pin both. Its window is a fixed 1% of capacity, so on traffic where recency is most of the signal
/// <see cref="LruCache{TKey, TValue, THasher}"/> can still miss less.
/// </para>
/// <para>
/// <b>The policy.</b> Capacity is split into a window of 1% (at least one entry) and a main region of
/// the rest, which is itself split into a <i>protected</i> segment of 80% and a <i>probation</i>
/// segment. A new key always enters the window, as its most-recently-used entry, so a key just put is
/// always resident. When the window overflows, its least-recently-used entry, the <i>candidate</i>,
/// moves to probation. Once the cache is full, the candidate must earn that move: it is compared with
/// probation's least-recently-used entry, the <i>victim</i>, by estimated frequency, and the one with
/// the lower estimate is evicted, the victim on a strict win for the candidate and the candidate on a
/// tie. A hit in probation promotes the entry to protected, demoting protected's least-recently-used
/// entry back to probation when protected is over its share; a hit in the window or in protected moves
/// the entry to the front of its segment.
/// </para>
/// <para>
/// <b>The frequency estimate</b> is a count-min sketch of 4-bit counters, eight bytes per unit of
/// capacity, that records every hit and every insert. Every counter is halved each time ten times
/// <see cref="Capacity"/> accesses have been recorded, so an access counts fully for one such period,
/// half for the next, and so on: the estimate is of <i>recent</i> frequency. A miss through
/// <see cref="TryGet(TKey, out TValue)"/> is not recorded, since the insert that follows it would
/// record it again; use <see cref="GetFrequencyEstimate(TKey)"/> to read the estimate for any key,
/// resident or not. Admission is deterministic: unlike Caffeine, there is no random admission of a
/// warm candidate, so a caller who controls the key stream can choose which keys collide in the sketch.
/// </para>
/// <para>
/// <b>Reads are mutating.</b> A hit reorders a segment, and a hit in probation moves the entry to
/// another segment, so the indexer getter, <see cref="TryGet(TKey, out TValue)"/> and an
/// <see cref="AddOrUpdate(TKey, TValue)"/> that overwrites invalidate active enumerators, except when
/// the entry is already the most-recently-used entry of the window or of the protected segment, where
/// nothing moves. Use <see cref="TryPeek(TKey, out TValue)"/> or <see cref="ContainsKey(TKey)"/> to
/// inspect the cache without counting a use.
/// </para>
/// <para>
/// <b>Cost.</b> Every operation is one key-index lookup plus <c>O(1)</c> list work; a hit or an
/// insert also updates four sketch counters, and an insert into a full cache reads eight. The halving
/// sweeps the sketch, eight bytes per unit of capacity, once per ten times <see cref="Capacity"/>
/// recorded accesses, so it is <c>O(1)</c> amortized. Node storage, the key index and the sketch are
/// allocated once at construction, so the hot get/put/evict path performs no allocation. The sketch
/// makes this the most expensive of the library's caches per operation: a hit costs several times what
/// an <see cref="LruCache{TKey, TValue, THasher}"/> hit does, which pays off when a miss costs more.
/// </para>
/// <para>This type is not thread-safe; concurrent callers must synchronize externally.</para>
/// </remarks>
public class TinyLfuCache<TKey, TValue, THasher>
    : IReadOnlyCollection<KeyValuePair<TKey, TValue?>>
    where THasher : struct, IHashProvider<TKey>
{
    // Sentinel for "no node" in the segment links and the free stack.
    private const int Nil = -1;

    // The segment a resident node belongs to, which is also the enumeration order.
    private const byte WindowSegment = 0;
    private const byte ProbationSegment = 1;
    private const byte ProtectedSegment = 2;
    private const int SegmentCount = 3;

    // Key -> node slot index, chained through the node slots themselves. A slot never moves while its
    // entry is resident, whichever segment it is in, so only inserts, evictions and removals touch it.
    // It owns each slot's key and records each slot's hash, which the sketch reuses.
    private readonly CacheKeyIndex<TKey, THasher> _index;
    private readonly TKey[] _keys;

    // Fixed-size node storage (length == capacity), keys aside. Each resident node is in exactly one of
    // the three segment lists, linked MRU (head) .. LRU (tail) via Prev/Next; free nodes form a
    // singly-linked stack via Next, rooted at _freeHead.
    private readonly Node[] _nodes;

    private readonly int _capacity;
    private readonly int _windowCapacity;
    private readonly int _mainCapacity;
    private readonly int _protectedCapacity;

    private FrequencySketch _sketch;
    private Segment _window;
    private Segment _probation;
    private Segment _protected;
    private int _count;
    private int _freeHead;

    // Incremented on every structural mutation (insert, evict, remove, non-empty clear, and any hit that
    // moves an entry), so active enumerators can detect concurrent modification and throw.
    private int _version;

    private struct Node
    {
        public TValue? Value;
        public int Prev;
        public int Next;
        public byte Segment;
    }

    private struct Segment
    {
        public int Head;
        public int Tail;
        public int Count;
    }

    /// <summary>
    /// Initializes a new empty cache that holds at most <paramref name="capacity"/> entries.
    /// </summary>
    /// <param name="capacity">The maximum number of entries the cache retains. Must be at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than 1.</exception>
    public TinyLfuCache(int capacity)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least 1.");

        _capacity = capacity;
        _windowCapacity = Math.Max(1, capacity / 100);
        _mainCapacity = capacity - _windowCapacity;
        _protectedCapacity = (int)(_mainCapacity * 4L / 5);

        _nodes = new Node[capacity];
        _index = new CacheKeyIndex<TKey, THasher>(capacity);
        _keys = _index.Keys;
        _sketch = new FrequencySketch(capacity);

        InitFreeStack();
        ResetSegments();
    }

    /// <summary>
    /// Initializes a new cache with the given <paramref name="capacity"/> and primes it from
    /// <paramref name="source"/>. Pairs are inserted in enumeration order through
    /// <see cref="AddOrUpdate(TKey, TValue)"/>, so a later duplicate key overwrites the earlier value
    /// and counts as a use of it, and a source with more than <paramref name="capacity"/> distinct
    /// keys evicts under the W-TinyLFU policy as it goes.
    /// </summary>
    /// <param name="capacity">The maximum number of entries the cache retains. Must be at least 1.</param>
    /// <param name="source">The key/value pairs to seed the cache with.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than 1.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public TinyLfuCache(int capacity, IEnumerable<KeyValuePair<TKey, TValue?>> source)
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
    /// The getter is a <b>use</b>: it records an access in the frequency sketch and moves the entry
    /// forward (promoting it from probation to protected), and throws <see cref="KeyNotFoundException"/>
    /// if the key is absent. The setter behaves like <see cref="AddOrUpdate(TKey, TValue)"/>.
    /// </remarks>
    /// <exception cref="KeyNotFoundException">The key is not present (getter only).</exception>
    public TValue? this[TKey key]
    {
        get
        {
            int node = _index.Find(key);
            if (node < 0)
                throw new KeyNotFoundException($"Key {key} not found.");
            if (OnHit(node))
                _version++;
            return _nodes[node].Value;
        }
        set => AddOrUpdate(key, value);
    }

    /// <summary>
    /// Attempts to get the value associated with <paramref name="key"/>. A hit is a use: it records an
    /// access in the frequency sketch and moves the entry forward.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">
    /// When this method returns, contains the cached value if the key was found; otherwise the default
    /// value of <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><c>true</c> if the key was found; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// A miss records nothing, since the insert a caller makes after a miss records the access. A hit
    /// invalidates active enumerators unless the entry is already the most-recently-used entry of the
    /// window or of the protected segment.
    /// </remarks>
    public bool TryGet(TKey key, out TValue? value)
    {
        int node = _index.Find(key);
        if (node < 0)
        {
            value = default;
            return false;
        }
        if (OnHit(node))
            _version++;
        value = _nodes[node].Value;
        return true;
    }

    /// <summary>
    /// Attempts to read the value associated with <paramref name="key"/> <b>without</b> counting a use:
    /// the frequency sketch and the segment order are left as they were.
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
    /// count a use.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns><c>true</c> if the key is found; otherwise <c>false</c>.</returns>
    public bool ContainsKey(TKey key) => _index.Find(key) >= 0;

    /// <summary>
    /// Returns the frequency sketch's estimate of how often <paramref name="key"/> was used recently,
    /// from <c>0</c> to <c>15</c>, whether or not the key is resident. This is a peek: it records
    /// nothing.
    /// </summary>
    /// <param name="key">The key to estimate.</param>
    /// <returns>
    /// The estimated number of recent uses. Collisions in the sketch can only overstate it, counters
    /// saturate at <c>15</c>, and every estimate halves each time ten times <see cref="Capacity"/>
    /// accesses have been recorded.
    /// </returns>
    /// <remarks>
    /// This is the number admission compares: when the cache is full, the entry leaving the window
    /// displaces probation's least-recently-used entry only if its estimate is strictly higher.
    /// </remarks>
    public int GetFrequencyEstimate(TKey key)
        => _sketch.Frequency(CacheKeyIndex<TKey, THasher>.HashOf(key));

    /// <summary>
    /// Adds <paramref name="key"/> with <paramref name="value"/> as the most-recently-used entry of the
    /// window, evicting one entry under the W-TinyLFU policy first if the cache is at capacity, or
    /// overwrites the value of an existing key and counts it as a use.
    /// </summary>
    /// <param name="key">The key to add or update.</param>
    /// <param name="value">The value to associate with <paramref name="key"/>.</param>
    /// <remarks>
    /// A new key is always resident afterwards: what the policy decides is which <i>older</i> entry
    /// leaves. An overwrite counts as a use, as a <see cref="TryGet(TKey, out TValue)"/> hit does.
    /// </remarks>
    public void AddOrUpdate(TKey key, TValue? value)
    {
        int hash = CacheKeyIndex<TKey, THasher>.HashOf(key);
        int node = _index.Find(key, hash);
        if (node >= 0)
        {
            _nodes[node].Value = value;
            if (OnHit(node))
                _version++;
            return;
        }

        InsertNew(key, hash, value);
        _version++;
    }

    /// <summary>
    /// Adds <paramref name="key"/> with <paramref name="value"/> as the most-recently-used entry of the
    /// window, evicting one entry under the W-TinyLFU policy first if the cache is at capacity.
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
    /// entry of the window, evicting one entry under the W-TinyLFU policy first if the cache is at
    /// capacity.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <returns>
    /// <c>true</c> if the entry was added; <c>false</c> if the key already exists (the cache is left
    /// unchanged, and no use is recorded).
    /// </returns>
    public bool TryAdd(TKey key, TValue? value)
    {
        int hash = CacheKeyIndex<TKey, THasher>.HashOf(key);
        if (_index.Find(key, hash) >= 0)
            return false;

        InsertNew(key, hash, value);
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
    /// The key's recorded frequency stays in the sketch, so a key removed and soon re-added is judged
    /// by its history, as an evicted one is.
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
        Unlink(node);
        FreeNode(node);
        _count--;
        _version++;
        return true;
    }

    /// <summary>
    /// Removes all entries from the cache and forgets every recorded frequency. The backing storage
    /// (sized to <see cref="Capacity"/>) is retained.
    /// </summary>
    /// <remarks>
    /// Clearing an empty cache still resets the frequency sketch, but changes no entry, so it leaves
    /// active enumerators valid.
    /// </remarks>
    public void Clear()
    {
        _sketch.Clear();
        if (_count == 0)
            return;

        _index.Clear();
        Array.Clear(_nodes);
        InitFreeStack();
        ResetSegments();
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Returns an allocation-free struct enumerator that yields every entry: the window, then
    /// probation, then the protected segment, each from most- to least-recently used. Enumeration is a
    /// peek: it records no use. <see cref="Enumerator.MoveNext"/> throws
    /// <see cref="InvalidOperationException"/> when the entries or their order changed since the
    /// enumerator was taken: an insert, an eviction, a successful <see cref="Remove(TKey)"/>, a
    /// <see cref="Clear"/> of a non-empty cache, or a use that moved an entry. Overwriting a value
    /// without moving its entry leaves active enumerators valid.
    /// </summary>
    /// <returns>A struct enumerator over this cache.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<KeyValuePair<TKey, TValue?>> IEnumerable<KeyValuePair<TKey, TValue?>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- internal segment / free-stack / admission machinery -------------------------------------

    private void InitFreeStack()
    {
        for (int i = 0; i < _capacity - 1; i++)
            _nodes[i].Next = i + 1;
        _nodes[_capacity - 1].Next = Nil;
        _freeHead = 0;
    }

    private void ResetSegments()
    {
        _window = new Segment { Head = Nil, Tail = Nil };
        _probation = new Segment { Head = Nil, Tail = Nil };
        _protected = new Segment { Head = Nil, Tail = Nil };
    }

    private ref Segment SegmentOf(byte segment)
    {
        if (segment == WindowSegment)
            return ref _window;
        if (segment == ProbationSegment)
            return ref _probation;
        return ref _protected;
    }

    // Records a use of a resident node and moves it forward. Returns whether any entry moved, which is
    // what decides whether active enumerators are invalidated.
    private bool OnHit(int node)
    {
        _sketch.Increment(_index.HashAt(node));

        byte segment = _nodes[node].Segment;
        if (segment != ProbationSegment)
        {
            ref Segment s = ref SegmentOf(segment);
            if (s.Head == node)
                return false;
            Unlink(ref s, node);
            LinkAtHead(ref s, node);
            return true;
        }

        // A second use while on probation: promote to protected. If that pushes protected over its
        // share, its least-recently-used entry drops back to the front of probation.
        Unlink(ref _probation, node);
        _nodes[node].Segment = ProtectedSegment;
        LinkAtHead(ref _protected, node);
        if (_protected.Count > _protectedCapacity)
        {
            int demoted = _protected.Tail;
            Unlink(ref _protected, demoted);
            _nodes[demoted].Segment = ProbationSegment;
            LinkAtHead(ref _probation, demoted);
        }
        return true;
    }

    // Inserts a brand-new key at the front of the window. Caller guarantees the key is absent.
    //
    // The window never exceeds its share and the main region only grows through window overflow, so
    // the main region never exceeds its share either. A full cache therefore holds exactly a full
    // window and a full main region, and because protected's share is strictly smaller than the main
    // region's, probation is non-empty whenever the main region has any share at all.
    private void InsertNew(TKey key, int hash, TValue? value)
    {
        int node;
        if (_count == _capacity)
        {
            node = EvictForInsert();
        }
        else
        {
            node = _freeHead;
            _freeHead = _nodes[node].Next;
            _count++;
            if (_window.Count == _windowCapacity)
                MoveToProbation(_window.Tail); // room to spare: the candidate needs no admission test
        }

        _sketch.Increment(hash);
        _index.Insert(node, key, hash);
        ref Node n = ref _nodes[node];
        n.Value = value;
        n.Segment = WindowSegment;
        LinkAtHead(ref _window, node);
    }

    // The admission decision. The window's least-recently-used entry (the candidate) has to leave it;
    // it displaces probation's least-recently-used entry (the victim) only if the sketch estimates it
    // strictly more frequent, and is otherwise evicted itself. With no main region at all (capacity
    // below 2) the candidate simply goes. Returns the evicted slot, unlinked and dropped from the index.
    private int EvictForInsert()
    {
        int candidate = _window.Tail;
        int evicted = candidate;
        if (_mainCapacity > 0)
        {
            int victim = _probation.Tail;
            if (_sketch.Frequency(_index.HashAt(candidate)) > _sketch.Frequency(_index.HashAt(victim)))
            {
                evicted = victim;
                MoveToProbation(candidate);
            }
        }

        _index.Remove(evicted);
        Unlink(evicted);
        return evicted;
    }

    private void MoveToProbation(int node)
    {
        Unlink(ref _window, node);
        _nodes[node].Segment = ProbationSegment;
        LinkAtHead(ref _probation, node);
    }

    // Detaches a free node's value (the index already released its key) and pushes its slot onto
    // the free stack.
    private void FreeNode(int node)
    {
        ref Node n = ref _nodes[node];
        n.Value = default;
        n.Next = _freeHead;
        _freeHead = node;
    }

    private void Unlink(int node) => Unlink(ref SegmentOf(_nodes[node].Segment), node);

    // Removes a node from its segment's list (leaves its storage untouched).
    private void Unlink(ref Segment s, int node)
    {
        int p = _nodes[node].Prev;
        int n = _nodes[node].Next;
        if (p != Nil) _nodes[p].Next = n; else s.Head = n;
        if (n != Nil) _nodes[n].Prev = p; else s.Tail = p;
        s.Count--;
    }

    // Links an unlinked node at the head (most-recently-used end) of a segment.
    private void LinkAtHead(ref Segment s, int node)
    {
        _nodes[node].Prev = Nil;
        _nodes[node].Next = s.Head;
        if (s.Head != Nil) _nodes[s.Head].Prev = node;
        s.Head = node;
        if (s.Tail == Nil) s.Tail = node;
        s.Count++;
    }

    /// <summary>
    /// A struct enumerator over a <see cref="TinyLfuCache{TKey, TValue, THasher}"/> that yields the
    /// window, then probation, then the protected segment, each from most- to least-recently used.
    /// Because it is a struct, iterating it via <c>foreach</c> avoids the allocation a
    /// compiler-generated <c>IEnumerator&lt;T&gt;</c> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue?>>
    {
        private readonly TinyLfuCache<TKey, TValue, THasher> _cache;
        private readonly int _version;
        private int _segment;
        private int _node;
        private KeyValuePair<TKey, TValue?> _current;

        internal Enumerator(TinyLfuCache<TKey, TValue, THasher> cache)
        {
            _cache = cache;
            _version = cache._version;
            _segment = -1;
            _node = Nil;
            _current = default;
        }

        /// <summary>Gets the entry at the current position of the enumerator.</summary>
        public KeyValuePair<TKey, TValue?> Current => _current;

        object IEnumerator.Current => _current;

        /// <summary>
        /// Advances the enumerator to the next entry.
        /// </summary>
        /// <returns><c>true</c> if the enumerator advanced to a new entry; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The cache was modified since the enumerator was created.</exception>
        public bool MoveNext()
        {
            if (_version != _cache._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            // _segment is -1 before the first call, 0..2 while walking, and SegmentCount once exhausted.
            Node[] nodes = _cache._nodes;
            _node = _node != Nil ? nodes[_node].Next : Nil;
            while (_node == Nil)
            {
                if (_segment >= SegmentCount - 1)
                {
                    _segment = SegmentCount;
                    _current = default;
                    return false;
                }
                _segment++;
                _node = _cache.SegmentOf((byte)_segment).Head;
            }

            _current = new KeyValuePair<TKey, TValue?>(_cache._keys[_node], nodes[_node].Value);
            return true;
        }

        /// <summary>Resets the enumerator to its initial position, before the first entry.</summary>
        /// <exception cref="InvalidOperationException">The cache was modified since the enumerator was created.</exception>
        public void Reset()
        {
            if (_version != _cache._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _segment = -1;
            _node = Nil;
            _current = default;
        }

        /// <summary>Releases any resources held by the enumerator. No-op for this type.</summary>
        public void Dispose() { }
    }
}
