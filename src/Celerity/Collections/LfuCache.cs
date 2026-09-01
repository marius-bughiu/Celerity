using System.Collections;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A fixed-capacity <b>least-frequently-used (LFU) cache</b>: an expected-<c>O(1)</c> get/put map that
/// automatically evicts the least-frequently-used entry when a new key would push the count past
/// <see cref="Capacity"/>, breaking ties between equally-frequent entries by least-recently-used. The
/// eviction bookkeeping itself is <c>O(1)</c> <i>worst case</i>; the qualifier on the whole operation
/// is the open-addressed key-index probe, as on every hash-backed collection here.
/// Parameterized on a custom <see cref="IHashProvider{T}"/> so key hashing devirtualizes and inlines.
/// </summary>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TValue">The type of the cached values.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute key hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
/// <remarks>
/// <para>
/// The BCL ships no bounded cache of any policy, and <see cref="LruCache{TKey, TValue, THasher}"/> is
/// this library's answer for the recency-ordered one. <see cref="LfuCache{TKey, TValue, THasher}"/> is
/// its frequency-ordered sibling, and it exists to cover the one failure mode recency cannot see:
/// <b>LRU is scan-vulnerable</b>. Because recency is the only thing an LRU measures, a single
/// sequential pass over <see cref="Capacity"/> cold keys evicts the entire hot set no matter how often
/// those hot keys were used, and every subsequent lookup misses until the cache is re-warmed. That is
/// the ordinary shape of a table scan, a backfill, or a crawler sharing a cache with steady-state
/// traffic. A frequency-ordered cache protects what has proven popular from that, and the guarantee is
/// worth stating precisely because it is <b>conditional</b>:
/// </para>
/// <para>
/// <b>A scan of any length costs at most one entry that has been used more than once.</b>
/// A one-shot scan key arrives at frequency 1, so it is outranked by anything read even twice, and
/// each cold key is dropped by the next cold key rather than by a popular entry. The "at most one" is
/// the boundary: if the cache is full and <i>nothing</i> is at frequency 1 when the scan starts, the
/// first cold key has no peer to displace and takes the least-recently-used of the lowest-frequency
/// residents; from the second cold key onward there always is one. With spare room, the cost is zero.
/// </para>
/// <para>
/// <b>Entries still at frequency 1 are not protected, and should not be.</b> An entry inserted and
/// never read again sits at exactly the frequency a scan key arrives at, so only the recency tie-break
/// separates them — and the resident is the older of the two, so it is taken first. A scan will
/// evict every such entry. That is not a defect in the policy, it is the policy: LFU protects what has
/// demonstrated reuse, and an entry used exactly once has demonstrated none. What it buys you is that
/// the popular set is not collateral damage, which under LRU it always is — an LRU loses its
/// <i>entire</i> working set to the same scan, however hot those entries were.
/// </para>
/// <para>
/// <b>Structure.</b> Entries live in <i>frequency buckets</i> — one bucket per distinct use count
/// currently present — and the buckets themselves are held in a doubly-linked list in ascending
/// frequency order. Within a bucket the entries form an intrusive recency chain, so the eviction
/// victim is always exactly defined: the least-recently-used entry of the lowest-frequency bucket.
/// Promotion, insertion and eviction each touch a constant number of links, so the <b>policy
/// bookkeeping</b> is <c>O(1)</c> <i>worst case</i> rather than amortized or logarithmic — that is the
/// part being contrasted with a sorted-structure LFU. A complete cache operation also probes the key
/// index, which is open-addressed with linear probing, so <b>end to end an operation is expected
/// <c>O(1)</c></b>, degrading with probe length on clustered or adversarial keys exactly as every
/// other hash-backed collection in this library does. This is the structure from Shah, Mitra and
/// Matani, <i>"An O(1) algorithm for implementing the LFU cache eviction scheme"</i>.
/// </para>
/// <para>
/// Storage mirrors <see cref="LruCache{TKey, TValue, THasher}"/>: the entry chains and the bucket list
/// are threaded through <b>fixed-size arrays allocated once at construction</b> (sized to
/// <see cref="Capacity"/>, which also bounds the number of simultaneously non-empty buckets), over an
/// open-addressed key&#8594;node-slot index. The node slot is stable across bucket movement, so a hit
/// never touches the index; after construction the hot get/put/evict path performs <b>no allocation at
/// all</b>.
/// </para>
/// <para>
/// <b>Reads are mutating.</b> LFU semantics require a lookup to count as a use, so the indexer getter
/// and <see cref="TryGet(TKey, out TValue)"/> increment the entry's frequency and therefore invalidate
/// any in-progress enumerator. Unlike <see cref="LruCache{TKey, TValue, THasher}"/>, there is no
/// exemption for an entry that is already at the front: raising a frequency from <c>f</c> to
/// <c>f + 1</c> always moves the entry to a different bucket, so <i>every</i> hit is a structural
/// change. Use <see cref="TryPeek(TKey, out TValue)"/>, <see cref="ContainsKey(TKey)"/> or
/// <see cref="TryGetFrequency(TKey, out long)"/> to inspect the cache without counting a use.
/// </para>
/// <para>
/// <b>The tradeoff, stated plainly.</b> Frequencies are <see cref="long"/> and <b>never age</b>. That
/// is classic LFU, and it means a key that was hot long ago can hold its slot indefinitely against a
/// key that is hot now. <see cref="LfuCache{TKey, TValue, THasher}"/> is therefore the right pick for a
/// <b>stable, skewed popularity distribution</b> — the Zipfian read cache — and for any workload where
/// scans must not evict the hot set. For recency-dominated or shifting workloads, where what was
/// popular yesterday is irrelevant today, <see cref="LruCache{TKey, TValue, THasher}"/> remains the
/// right pick. Neither is universally better; pick the one whose notion of "worth keeping" matches the
/// workload.
/// </para>
/// <para>
/// The counter is a <see cref="long"/> and is not clamped, which makes
/// <see cref="long.MaxValue"/> the ceiling on a single entry's use count. Reaching it is not
/// physically possible: it takes 2<sup>63</sup> hits on one key, which is close to three centuries of
/// continuous use at a billion hits a second. This is exactly why the counter is a
/// <see cref="long"/> and not an <see cref="int"/> — an <see cref="int"/> ceiling is a real bug, since
/// 2<sup>31</sup> hits is minutes of ordinary traffic.
/// </para>
/// <para>This type is not thread-safe; concurrent callers must synchronize externally.</para>
/// </remarks>
public class LfuCache<TKey, TValue, THasher>
    : IReadOnlyCollection<KeyValuePair<TKey, TValue?>>
    where THasher : struct, IHashProvider<TKey>
{
    // Sentinel for "no node" / "no bucket" in the intrusive lists and the free stacks.
    private const int Nil = -1;

    // Key -> node slot index. Dogfoods CelerityDictionary; because a node slot index is stable across
    // bucket movement, a cache hit (which only relinks the entry and bucket chains) never mutates this map.
    private readonly CelerityDictionary<TKey, int, THasher> _index;

    // Fixed-size entry storage (length == capacity). Occupied nodes belong to exactly one frequency
    // bucket and form that bucket's most-recently-used..least-recently-used chain via _prev/_next;
    // free nodes form a singly-linked stack via _next (rooted at _freeHead).
    private readonly TKey[] _nodeKeys;
    private readonly TValue?[] _nodeValues;
    private readonly int[] _nodeBucket;
    private readonly int[] _prev;
    private readonly int[] _next;

    // Fixed-size bucket storage. A non-empty bucket holds at least one of the at-most-Capacity entries,
    // so Capacity slots are always enough. Live buckets form an ascending-frequency chain via
    // _bucketPrev/_bucketNext (rooted at _minBucket, ending at _maxBucket); free buckets form a
    // singly-linked stack via _bucketNext (rooted at _bucketFreeHead).
    private readonly long[] _bucketFreq;
    private readonly int[] _bucketHead;
    private readonly int[] _bucketTail;
    private readonly int[] _bucketPrev;
    private readonly int[] _bucketNext;

    private readonly int _capacity;
    private int _count;
    private int _minBucket;      // lowest-frequency bucket (holds the eviction victim), or Nil when empty
    private int _maxBucket;      // highest-frequency bucket, or Nil when empty
    private int _freeHead;       // top of the free-node stack, or Nil when full
    private int _bucketFreeHead; // top of the free-bucket stack

    // Incremented on every structural mutation (insert, evict, remove, clear) and on every frequency
    // increment, so active enumerators can detect concurrent modification and throw.
    private int _version;

    /// <summary>
    /// Initializes a new empty cache that holds at most <paramref name="capacity"/> entries.
    /// </summary>
    /// <param name="capacity">The maximum number of entries the cache retains. Must be at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than 1.</exception>
    public LfuCache(int capacity)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least 1.");

        _capacity = capacity;
        _nodeKeys = new TKey[capacity];
        _nodeValues = new TValue?[capacity];
        _nodeBucket = new int[capacity];
        _prev = new int[capacity];
        _next = new int[capacity];

        _bucketFreq = new long[capacity];
        _bucketHead = new int[capacity];
        _bucketTail = new int[capacity];
        _bucketPrev = new int[capacity];
        _bucketNext = new int[capacity];

        InitFreeStacks();
        _minBucket = Nil;
        _maxBucket = Nil;
        _count = 0;

        // Pre-size the index for the full capacity so it never resizes during steady-state churn.
        _index = new CelerityDictionary<TKey, int, THasher>(capacity);
        _index.EnsureCapacity(capacity);
    }

    /// <summary>
    /// Initializes a new cache with the given <paramref name="capacity"/> and primes it from
    /// <paramref name="source"/>. Pairs are inserted in enumeration order, each arriving with a
    /// frequency of 1. A later duplicate key overwrites the earlier value and counts as a further use,
    /// raising that entry's frequency above the singletons around it — but only while the earlier
    /// occurrence is <i>still resident</i>. In a source longer than <paramref name="capacity"/> an
    /// early occurrence may already have been evicted, in which case the later one is a fresh
    /// frequency-1 insert and the earlier use is not carried over: an evicted entry keeps no history.
    /// </summary>
    /// <remarks>
    /// If the source is <b>duplicate-free</b> every key arrives at frequency 1, ties break by recency,
    /// and the effect is the familiar one: the earliest keys are evicted and the last
    /// <paramref name="capacity"/> survive. <b>With duplicates that no longer holds</b>, and
    /// deliberately so — a key repeated early reaches a frequency the later singletons never do, and
    /// outlives them. Seeding <c>{1, 1, 2, 3}</c> into a capacity of 2 keeps <c>1</c> and <c>3</c>,
    /// not the last two distinct keys.
    /// </remarks>
    /// <param name="capacity">The maximum number of entries the cache retains. Must be at least 1.</param>
    /// <param name="source">The key/value pairs to seed the cache with.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than 1.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public LfuCache(int capacity, IEnumerable<KeyValuePair<TKey, TValue?>> source)
        : this(capacity)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (KeyValuePair<TKey, TValue?> pair in source)
            AddOrUpdate(pair.Key, pair.Value);
    }

    /// <summary>
    /// Gets the maximum number of entries the cache retains before evicting the least-frequently-used one.
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
    /// The getter is a <b>use</b>: it increments the entry's frequency and throws
    /// <see cref="KeyNotFoundException"/> if the key is absent. The setter adds the key with a frequency
    /// of 1 (evicting the least-frequently-used entry first if the cache is full) or overwrites an
    /// existing value and counts as a use.
    /// </remarks>
    /// <exception cref="KeyNotFoundException">The key is not present (getter only).</exception>
    public TValue? this[TKey key]
    {
        get
        {
            if (!_index.TryGetValue(key, out int node))
                throw new KeyNotFoundException($"Key {key} not found.");
            Touch(node);
            _version++;
            return _nodeValues[node];
        }
        set => AddOrUpdate(key, value);
    }

    /// <summary>
    /// Attempts to get the value associated with <paramref name="key"/>, incrementing the entry's
    /// frequency on a hit.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">
    /// When this method returns, contains the cached value if the key was found; otherwise the default
    /// value of <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><c>true</c> if the key was found; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// A hit always moves the entry to a different frequency bucket and therefore always invalidates
    /// active enumerators. A miss changes nothing and leaves them valid.
    /// </remarks>
    public bool TryGet(TKey key, out TValue? value)
    {
        if (!_index.TryGetValue(key, out int node))
        {
            value = default;
            return false;
        }
        Touch(node);
        _version++;
        value = _nodeValues[node];
        return true;
    }

    /// <summary>
    /// Attempts to read the value associated with <paramref name="key"/> <b>without</b> counting a use,
    /// so it does not change the entry's frequency, does not disturb the eviction order, and does not
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
    /// count as a use and does not change the entry's frequency.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns><c>true</c> if the key is found; otherwise <c>false</c>.</returns>
    public bool ContainsKey(TKey key) => _index.ContainsKey(key);

    /// <summary>
    /// Reads the number of times <paramref name="key"/> has been used — one for the insert, plus one
    /// for every subsequent read through the indexer getter or <see cref="TryGet(TKey, out TValue)"/>
    /// and every overwrite through <see cref="AddOrUpdate(TKey, TValue)"/> or the indexer setter. This
    /// is itself a peek and does not count as a use.
    /// </summary>
    /// <param name="key">The key whose use count to read.</param>
    /// <param name="frequency">
    /// When this method returns, contains the entry's use count if the key was found; otherwise zero.
    /// </param>
    /// <returns><c>true</c> if the key was found; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// This has no <see cref="LruCache{TKey, TValue, THasher}"/> analogue: recency has no scalar to
    /// report, whereas frequency is exactly the quantity the eviction order is sorted on, so exposing it
    /// makes that order inspectable from a diagnostic or a test.
    /// </remarks>
    public bool TryGetFrequency(TKey key, out long frequency)
    {
        if (!_index.TryGetValue(key, out int node))
        {
            frequency = 0;
            return false;
        }
        frequency = _bucketFreq[_nodeBucket[node]];
        return true;
    }

    /// <summary>
    /// Adds <paramref name="key"/> with <paramref name="value"/> at a frequency of 1, evicting the
    /// least-frequently-used entry first if the cache is at capacity, or overwrites the value of an
    /// existing key and counts the write as a use.
    /// </summary>
    /// <param name="key">The key to add or update.</param>
    /// <param name="value">The value to associate with <paramref name="key"/>.</param>
    public void AddOrUpdate(TKey key, TValue? value)
    {
        if (_index.TryGetValue(key, out int node))
        {
            _nodeValues[node] = value;
            Touch(node);
            _version++;
            return;
        }

        InsertNew(key, value);
        _version++;
    }

    /// <summary>
    /// Adds <paramref name="key"/> with <paramref name="value"/> at a frequency of 1, evicting the
    /// least-frequently-used entry first if the cache is at capacity.
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
    /// Attempts to add <paramref name="key"/> with <paramref name="value"/> at a frequency of 1,
    /// evicting the least-frequently-used entry first if the cache is at capacity.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <returns>
    /// <c>true</c> if the entry was added; <c>false</c> if the key already exists (the cache is left
    /// unchanged, including every frequency — a rejected add is not a use).
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
    /// Removes the entry with the specified key from the cache, discarding its frequency.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <returns><c>true</c> if the entry was removed; <c>false</c> if the key was not found.</returns>
    public bool Remove(TKey key) => Remove(key, out _);

    /// <summary>
    /// Removes the entry with the specified key from the cache and returns its value. The frequency is
    /// discarded: re-adding the key later starts it back at 1.
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
        DetachFromBucket(node);
        FreeNode(node);
        _count--;
        _version++;
        return true;
    }

    /// <summary>
    /// Removes all entries from the cache, discarding every frequency. The backing storage (sized to
    /// <see cref="Capacity"/>) is retained.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        _index.Clear();
        Array.Clear(_nodeKeys, 0, _nodeKeys.Length);
        Array.Clear(_nodeValues, 0, _nodeValues.Length);
        InitFreeStacks();
        _minBucket = Nil;
        _maxBucket = Nil;
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Reads the least-frequently-used entry — the one the next insert-when-full would evict, which
    /// among equally-frequent entries is the least-recently-used of them — without counting a use.
    /// </summary>
    /// <param name="key">When this method returns, the victim key if the cache is non-empty; otherwise the default.</param>
    /// <param name="value">When this method returns, the victim value if the cache is non-empty; otherwise the default.</param>
    /// <returns><c>true</c> if the cache is non-empty; otherwise <c>false</c>.</returns>
    public bool TryPeekLeastFrequentlyUsed(out TKey? key, out TValue? value)
    {
        if (_minBucket == Nil)
        {
            key = default;
            value = default;
            return false;
        }
        int node = _bucketTail[_minBucket];
        key = _nodeKeys[node];
        value = _nodeValues[node];
        return true;
    }

    /// <summary>
    /// Reads the most-frequently-used entry — among equally-frequent entries the most-recently-used of
    /// them — without counting a use.
    /// </summary>
    /// <param name="key">When this method returns, the most-frequently-used key if the cache is non-empty; otherwise the default.</param>
    /// <param name="value">When this method returns, the most-frequently-used value if the cache is non-empty; otherwise the default.</param>
    /// <returns><c>true</c> if the cache is non-empty; otherwise <c>false</c>.</returns>
    public bool TryPeekMostFrequentlyUsed(out TKey? key, out TValue? value)
    {
        if (_maxBucket == Nil)
        {
            key = default;
            value = default;
            return false;
        }
        int node = _bucketHead[_maxBucket];
        key = _nodeKeys[node];
        value = _nodeValues[node];
        return true;
    }

    /// <summary>
    /// Returns an allocation-free struct enumerator that yields each entry in
    /// <b>most-frequently-used to least-frequently-used</b> order, and within one frequency in
    /// most-recently-used to least-recently-used order. Enumeration is a peek: it does not count as a
    /// use. <see cref="Enumerator.MoveNext"/> throws <see cref="InvalidOperationException"/> when the
    /// entry set or the frequency order changed since the enumerator was taken: an insert, an eviction,
    /// a <see cref="Remove(TKey)"/>, a <see cref="Clear"/>, or any read or write that counted as a use.
    /// Because raising a frequency always moves the entry to a different bucket, there is no
    /// already-at-the-front exemption of the kind <see cref="LruCache{TKey, TValue, THasher}"/> has —
    /// every hit invalidates. Overwriting the value of an entry is a use and therefore also invalidates,
    /// which is the one place this type is stricter than the rest of the library.
    /// </summary>
    /// <returns>A struct enumerator over this cache.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<KeyValuePair<TKey, TValue?>> IEnumerable<KeyValuePair<TKey, TValue?>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- internal bucket / entry-chain / free-stack machinery ------------------------------------

    private void InitFreeStacks()
    {
        for (int i = 0; i < _capacity - 1; i++)
        {
            _next[i] = i + 1;
            _bucketNext[i] = i + 1;
        }
        _next[_capacity - 1] = Nil;
        _bucketNext[_capacity - 1] = Nil;
        _freeHead = 0;
        _bucketFreeHead = 0;
    }

    // Inserts a brand-new key at frequency 1. Reuses the victim's slot when the cache is full
    // (evicting it), otherwise pops a slot off the free stack. Caller guarantees the key is absent.
    private void InsertNew(TKey key, TValue? value)
    {
        int node;
        if (_count == _capacity)
        {
            // Evict the least-frequently-used entry — the LRU end of the lowest-frequency bucket — and
            // recycle its slot in place (no free-stack churn).
            node = _bucketTail[_minBucket];
            _index.Remove(_nodeKeys[node]);
            DetachFromBucket(node);
        }
        else
        {
            node = _freeHead;
            _freeHead = _next[node];
            _count++;
        }

        _nodeKeys[node] = key;
        _nodeValues[node] = value;

        // At most _count - 1 entries are linked at this point, so at most _count - 1 <= _capacity - 1
        // buckets are live and AllocBucket always has a slot.
        if (_minBucket != Nil && _bucketFreq[_minBucket] == 1)
        {
            LinkIntoBucket(node, _minBucket);
        }
        else
        {
            int bucket = AllocBucket(1);
            LinkBucketAtFront(bucket);
            LinkIntoBucket(node, bucket);
        }

        _index[key] = node;
    }

    // Raises an entry's frequency by one, moving it to the bucket for f + 1 and creating that bucket if
    // no entry currently sits at f + 1.
    private void Touch(int node)
    {
        int bucket = _nodeBucket[node];
        long target = _bucketFreq[bucket] + 1;
        int higher = _bucketNext[bucket];
        bool soleOccupant = _bucketHead[bucket] == node && _bucketTail[bucket] == node;

        if (higher != Nil && _bucketFreq[higher] == target)
        {
            // The bucket for f + 1 already exists: move the entry into it as its most-recently-used.
            UnlinkFromBucket(node);
            LinkIntoBucket(node, higher);
            if (soleOccupant)
                FreeBucket(bucket);
            return;
        }

        if (soleOccupant)
        {
            // The entry is alone at f and nothing sits at f + 1, so the bucket it would move to is the
            // bucket it is already in. Relabel in place: no allocation, and no relink of the frequency
            // chain, because f + 1 is still strictly above the previous bucket and strictly below the
            // next one (which we just established is not f + 1).
            _bucketFreq[bucket] = target;
            return;
        }

        // The source bucket keeps at least one other entry, so at most _count - 1 <= _capacity - 1
        // buckets are live and AllocBucket always has a slot.
        int created = AllocBucket(target);
        LinkBucketAfter(created, bucket);
        UnlinkFromBucket(node);
        LinkIntoBucket(node, created);
    }

    // Removes an occupied node from its bucket's recency chain, freeing the bucket if it empties.
    private void DetachFromBucket(int node)
    {
        int bucket = _nodeBucket[node];
        bool soleOccupant = _bucketHead[bucket] == node && _bucketTail[bucket] == node;
        UnlinkFromBucket(node);
        if (soleOccupant)
            FreeBucket(bucket);
    }

    // Detaches a free node's storage and pushes its slot onto the free stack.
    private void FreeNode(int node)
    {
        _nodeKeys[node] = default!;
        _nodeValues[node] = default;
        _next[node] = _freeHead;
        _freeHead = node;
    }

    // Removes a node from its bucket's MRU..LRU chain (leaves its storage untouched).
    private void UnlinkFromBucket(int node)
    {
        int bucket = _nodeBucket[node];
        int p = _prev[node];
        int n = _next[node];
        if (p != Nil) _next[p] = n; else _bucketHead[bucket] = n;
        if (n != Nil) _prev[n] = p; else _bucketTail[bucket] = p;
    }

    // Links an unlinked node at the most-recently-used end of a bucket's chain.
    private void LinkIntoBucket(int node, int bucket)
    {
        _nodeBucket[node] = bucket;
        _prev[node] = Nil;
        _next[node] = _bucketHead[bucket];
        if (_bucketHead[bucket] != Nil) _prev[_bucketHead[bucket]] = node; else _bucketTail[bucket] = node;
        _bucketHead[bucket] = node;
    }

    // Pops a bucket slot off the free stack and initializes it as an empty bucket for `frequency`.
    private int AllocBucket(long frequency)
    {
        int bucket = _bucketFreeHead;
        _bucketFreeHead = _bucketNext[bucket];
        _bucketFreq[bucket] = frequency;
        _bucketHead[bucket] = Nil;
        _bucketTail[bucket] = Nil;
        return bucket;
    }

    // Unlinks an emptied bucket from the ascending-frequency chain and pushes its slot onto the free stack.
    private void FreeBucket(int bucket)
    {
        int p = _bucketPrev[bucket];
        int n = _bucketNext[bucket];
        if (p != Nil) _bucketNext[p] = n; else _minBucket = n;
        if (n != Nil) _bucketPrev[n] = p; else _maxBucket = p;
        _bucketNext[bucket] = _bucketFreeHead;
        _bucketFreeHead = bucket;
    }

    // Links a fresh bucket immediately above `after` in the ascending-frequency chain.
    private void LinkBucketAfter(int bucket, int after)
    {
        int n = _bucketNext[after];
        _bucketPrev[bucket] = after;
        _bucketNext[bucket] = n;
        _bucketNext[after] = bucket;
        if (n != Nil) _bucketPrev[n] = bucket; else _maxBucket = bucket;
    }

    // Links a fresh bucket at the bottom of the ascending-frequency chain (used only for frequency 1).
    private void LinkBucketAtFront(int bucket)
    {
        _bucketPrev[bucket] = Nil;
        _bucketNext[bucket] = _minBucket;
        if (_minBucket != Nil) _bucketPrev[_minBucket] = bucket; else _maxBucket = bucket;
        _minBucket = bucket;
    }

    /// <summary>
    /// A struct enumerator over a <see cref="LfuCache{TKey, TValue, THasher}"/> that yields entries in
    /// most-frequently-used to least-frequently-used order, and within one frequency in
    /// most-recently-used to least-recently-used order. Because it is a struct, iterating it via
    /// <c>foreach</c> avoids the allocation a compiler-generated <c>IEnumerator&lt;T&gt;</c> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue?>>
    {
        private readonly LfuCache<TKey, TValue, THasher> _cache;
        private readonly int _version;
        private int _bucket;
        private int _node;
        private bool _started;
        private KeyValuePair<TKey, TValue?> _current;

        internal Enumerator(LfuCache<TKey, TValue, THasher> cache)
        {
            _cache = cache;
            _version = cache._version;
            _bucket = Nil;
            _node = Nil;
            _started = false;
            _current = default;
        }

        /// <summary>Gets the entry at the current position of the enumerator.</summary>
        public KeyValuePair<TKey, TValue?> Current => _current;

        object IEnumerator.Current => _current;

        /// <summary>
        /// Advances the enumerator to the next entry (toward least-frequently-used).
        /// </summary>
        /// <returns><c>true</c> if the enumerator advanced to a new entry; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The cache was modified since the enumerator was created.</exception>
        public bool MoveNext()
        {
            if (_version != _cache._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            if (!_started)
            {
                _started = true;
                _bucket = _cache._maxBucket;
                _node = _bucket == Nil ? Nil : _cache._bucketHead[_bucket];
            }
            else
            {
                if (_bucket == Nil)
                    return false; // already exhausted

                _node = _cache._next[_node];
                if (_node == Nil)
                {
                    // A live bucket is never empty, so the next bucket down (if any) yields an entry.
                    _bucket = _cache._bucketPrev[_bucket];
                    _node = _bucket == Nil ? Nil : _cache._bucketHead[_bucket];
                }
            }

            if (_node == Nil)
            {
                _current = default;
                return false;
            }

            _current = new KeyValuePair<TKey, TValue?>(_cache._nodeKeys[_node], _cache._nodeValues[_node]);
            return true;
        }

        /// <summary>Resets the enumerator to its initial position, before the most-frequently-used entry.</summary>
        /// <exception cref="InvalidOperationException">The cache was modified since the enumerator was created.</exception>
        public void Reset()
        {
            if (_version != _cache._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _bucket = Nil;
            _node = Nil;
            _started = false;
            _current = default;
        }

        /// <summary>Releases any resources held by the enumerator. No-op for this type.</summary>
        public void Dispose() { }
    }
}
