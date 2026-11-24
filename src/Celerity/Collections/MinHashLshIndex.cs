using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A <strong>locality-sensitive hashing (LSH) index</strong> over <see cref="MinHashSketch{T,THasher}"/>s:
/// stores one sketch per key and, given a query sketch, returns the keys whose sets are
/// <em>likely</em> to be similar to it — without comparing the query against every stored sketch.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="MinHashSketch{T,THasher}"/> makes comparing two sets cheap, but finding the
/// near-duplicates of one set among <c>n</c> stored ones is still <c>n</c> calls to
/// <see cref="MinHashSketch{T,THasher}.EstimateJaccard"/>, and deduplicating a corpus is
/// <c>n²</c> of them. This index's query cost depends on the matching bucket hits, including
/// false-positive candidates: a query hashes <see cref="BandCount"/> bands and reads that many
/// buckets. It need not scan every stored sketch, but large buckets can still make a query costly.
/// </para>
/// <para>
/// This is the classic <em>banding</em> technique (Leskovec, Rajaraman &amp; Ullman, <em>Mining of
/// Massive Datasets</em>, ch. 3). The first <c>b · r</c> registers of a sketch are split into
/// <c>b</c> = <see cref="BandCount"/> bands of <c>r</c> = <see cref="RowsPerBand"/> registers
/// each, and each band is hashed to a bucket. Two sketches are <em>candidates</em> when they agree
/// on every register of at least one band. Under the model of independent MinHash registers,
/// a pair with Jaccard similarity <c>J</c> is returned with probability
/// <c>1 − (1 − J^r)^b</c> (<see cref="CandidateProbability"/>). This one-permutation index has
/// dependent registers, especially after densifying small sets, so that curve is a tuning model,
/// not a recall guarantee. The <see cref="MinHashLshIndex(double, int, IEqualityComparer{TKey})"/>
/// constructor chooses <c>b</c> and <c>r</c> around a threshold using the model's estimated
/// false-positive area below it plus false-negative area above it.
/// </para>
/// <para>
/// The answer is <strong>approximate in both directions</strong>. A pair above the threshold can
/// be missed, and a pair below it can be returned; the index does not verify candidates.
/// When the precision matters, check each candidate's sketch with
/// <see cref="MinHashSketch{T,THasher}.EstimateJaccard"/>, or its exact set, before acting on it.
/// </para>
/// <para>
/// <see cref="MinHashSketch{T,THasher}"/> is a one-permutation sketch, so a set with fewer elements
/// than registers leaves some registers empty, and two small sets would agree on an all-empty band
/// whatever they contain. The index therefore <em>densifies</em> the registers it bands: an empty
/// register borrows the value of the first non-empty register in a fixed pseudo-random order of its
/// own. This affine-probe variant is inspired by the densification literature (Shrivastava,
/// <em>Optimal Densification for Fast and Accurate Minwise Hashing</em>, ICML&#160;2017);
/// no variance-optimality guarantee is asserted for this variant. Under ideal random element
/// hashing without collisions, each register's agreement probability is <c>J</c>. The sketch
/// itself is not changed. For <c>m</c> registers, densifying takes expected <c>O(m^1.5)</c> work
/// under uniformly distributed occupied bins, with a deterministic <c>O(m²)</c> worst-case bound.
/// Sketches of the empty set have no values to borrow, so they all share every bucket and
/// are returned for each other, consistent with
/// <see cref="MinHashSketch{T,THasher}.EstimateJaccard"/> reporting <c>1</c> for two empty sketches.
/// </para>
/// <para>
/// <see cref="Add"/> bands the sketch <em>as it is at the time of the call</em>; changing the sketch
/// afterwards does not move its key. To re-index a changed sketch, <see cref="Remove"/> the key and
/// add it again. The index keeps no reference to the sketches. Per key it stores an 8-byte hash, two
/// links and a bucket entry for each band, so memory grows with <see cref="BandCount"/>: at the
/// 42 bands a 0.5 threshold chooses at the default precision it is about as large as the 2&#160;KB
/// sketch itself, and a higher threshold, which uses fewer bands, needs less.
/// </para>
/// <para>
/// Every sketch added or queried must have the index's <see cref="Precision"/>. Keys are compared
/// with the <see cref="IEqualityComparer{T}"/> passed to the constructor, or
/// <see cref="EqualityComparer{T}.Default"/>. The index is not thread-safe; concurrent queries with
/// no concurrent writer are safe, because a query does not modify the index.
/// </para>
/// </remarks>
/// <typeparam name="TKey">The type of the keys that identify the indexed sets.</typeparam>
/// <typeparam name="T">The element type of the indexed sketches.</typeparam>
/// <typeparam name="THasher">The element hasher of the indexed sketches.</typeparam>
public class MinHashLshIndex<TKey, T, THasher> : IReadOnlyCollection<TKey>
    where TKey : notnull
    where THasher : struct, IHashProvider<T>
{
    // How many steps of a register's own probe order are walked before falling back to a scan of
    // the non-empty registers. A sketch with at least one register in eight filled almost never
    // needs the fallback; an emptier one pays O(k) per empty register instead of O(m / k).
    private const int ProbeLimit = 8;

    // Above this many 8-byte slots a scratch buffer is rented instead of stack-allocated.
    private const int StackScratchLimit = 256;

    // _nodePrev value marking an entry slot that holds no key. A live node's prev is -1 (bucket
    // head) or a node index, never this.
    private const int FreeSlot = -2;

    private const ulong Golden = 0x9E3779B97F4A7C15UL;

    // Mixed into the band seeds of an empty sketch; see ComputeBandHashes.
    private const ulong EmptyTag = 0xD6E8FEB86659FD93UL;

    private readonly int _precision;
    private readonly int _bandCount;
    private readonly int _rowsPerBand;

    // Key -> entry slot.
    private readonly Dictionary<TKey, int> _entryOf;

    // Per band: band hash -> the first node of that bucket's chain. One map per band, so a node
    // reached from band j's map always belongs to band j.
    private readonly Dictionary<ulong, int>[] _heads;

    // Per entry slot: the key. Per node (entry * _bandCount + band): the band's hash and the chain
    // links. A free slot has _nodePrev[slot * _bandCount] == FreeSlot and keeps the next free slot
    // in _nodeNext[slot * _bandCount].
    private TKey[] _keys;
    private ulong[] _nodeHash;
    private int[] _nodeNext;
    private int[] _nodePrev;

    private int _used;       // entry slots ever handed out; slots at or past it are unused
    private int _freeHead;   // first free slot below _used, or -1
    private int _count;
    private int _version;

    /// <summary>
    /// Initializes a new, empty index whose band count and rows per band are chosen so that the
    /// candidate probability crosses from low to high around <paramref name="threshold"/>.
    /// </summary>
    /// <param name="threshold">
    /// The Jaccard similarity that separates the pairs a query should return from those it should
    /// not. Must be strictly between 0 and 1.
    /// </param>
    /// <param name="precision">
    /// The <see cref="MinHashSketch{T,THasher}.Precision"/> of every sketch the index will hold.
    /// Must be between <see cref="MinHashSketch{T,THasher}.MinPrecision"/> and
    /// <see cref="MinHashSketch{T,THasher}.MaxPrecision"/> inclusive.
    /// </param>
    /// <param name="keyComparer">
    /// The equality comparer for keys, or <c>null</c> for <see cref="EqualityComparer{T}.Default"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="threshold"/> is not strictly between 0 and 1, or <paramref name="precision"/>
    /// is out of range.
    /// </exception>
    /// <remarks>
    /// The parameters come from <see cref="GetOptimalParameters"/>, which weighs false positives and
    /// false negatives equally.
    /// </remarks>
    public MinHashLshIndex(
        double threshold,
        int precision = MinHashSketch<T, THasher>.DefaultPrecision,
        IEqualityComparer<TKey>? keyComparer = null)
        : this(GetOptimalParameters(threshold, precision), precision, keyComparer)
    {
    }

    /// <summary>
    /// Initializes a new, empty index with an explicit band count and rows per band.
    /// </summary>
    /// <param name="bandCount">The number of bands, <c>b</c>. Must be at least 1.</param>
    /// <param name="rowsPerBand">
    /// The number of registers in each band, <c>r</c>. Must be at least 1, and
    /// <c>bandCount · rowsPerBand</c> must not exceed the sketch's <c>2^precision</c> registers.
    /// </param>
    /// <param name="precision">
    /// The <see cref="MinHashSketch{T,THasher}.Precision"/> of every sketch the index will hold.
    /// Must be between <see cref="MinHashSketch{T,THasher}.MinPrecision"/> and
    /// <see cref="MinHashSketch{T,THasher}.MaxPrecision"/> inclusive.
    /// </param>
    /// <param name="keyComparer">
    /// The equality comparer for keys, or <c>null</c> for <see cref="EqualityComparer{T}.Default"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="precision"/> is out of range, <paramref name="bandCount"/> or
    /// <paramref name="rowsPerBand"/> is below 1, or their product exceeds the register count.
    /// </exception>
    public MinHashLshIndex(
        int bandCount,
        int rowsPerBand,
        int precision = MinHashSketch<T, THasher>.DefaultPrecision,
        IEqualityComparer<TKey>? keyComparer = null)
    {
        ThrowIfPrecisionOutOfRange(precision);
        int registers = 1 << precision;
        if (bandCount < 1 || bandCount > registers)
            throw new ArgumentOutOfRangeException(nameof(bandCount), bandCount,
                $"The band count must be between 1 and the register count ({registers}).");
        if (rowsPerBand < 1 || rowsPerBand > registers / bandCount)
            throw new ArgumentOutOfRangeException(nameof(rowsPerBand), rowsPerBand,
                $"The rows per band must be at least 1, and bands times rows must not exceed the register count ({registers}).");

        _precision = precision;
        _bandCount = bandCount;
        _rowsPerBand = rowsPerBand;
        _entryOf = new Dictionary<TKey, int>(keyComparer);
        _heads = new Dictionary<ulong, int>[bandCount];
        for (int band = 0; band < bandCount; band++)
            _heads[band] = new Dictionary<ulong, int>();
        _keys = Array.Empty<TKey>();
        _nodeHash = Array.Empty<ulong>();
        _nodeNext = Array.Empty<int>();
        _nodePrev = Array.Empty<int>();
        _freeHead = -1;
    }

    private MinHashLshIndex((int BandCount, int RowsPerBand) parameters, int precision, IEqualityComparer<TKey>? keyComparer)
        : this(parameters.BandCount, parameters.RowsPerBand, precision, keyComparer)
    {
    }

    /// <summary>Gets the precision every sketch in the index must have.</summary>
    public int Precision => _precision;

    /// <summary>Gets the number of bands, <c>b</c>.</summary>
    public int BandCount => _bandCount;

    /// <summary>Gets the number of registers in each band, <c>r</c>.</summary>
    public int RowsPerBand => _rowsPerBand;

    /// <summary>Gets the number of keys in the index.</summary>
    public int Count => _count;

    /// <summary>Gets the comparer used to compare keys.</summary>
    public IEqualityComparer<TKey> KeyComparer => _entryOf.Comparer;

    /// <summary>
    /// Returns the candidate probability under the ideal independent-MinHash model for Jaccard
    /// similarity <paramref name="jaccard"/>: <c>1 − (1 − J^r)^b</c>.
    /// </summary>
    /// <param name="jaccard">A Jaccard similarity in <c>[0, 1]</c>.</param>
    /// <returns>The model's candidate probability, in <c>[0, 1]</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="jaccard"/> is outside <c>[0, 1]</c>.</exception>
    /// <remarks>
    /// The index's one-permutation registers are dependent, especially after densifying small
    /// sets, so actual candidate rates can differ from this curve. It is not a recall guarantee.
    /// </remarks>
    public double CandidateProbability(double jaccard)
    {
        if (!(jaccard >= 0d && jaccard <= 1d))
            throw new ArgumentOutOfRangeException(nameof(jaccard), jaccard, "The Jaccard similarity must be between 0 and 1.");

        return Curve(jaccard, _bandCount, _rowsPerBand);
    }

    /// <summary>
    /// Chooses a band count and rows per band by minimising the numerically estimated error area
    /// around <paramref name="threshold"/> under the ideal independent-MinHash model.
    /// </summary>
    /// <param name="threshold">The Jaccard similarity to separate at. Must be strictly between 0 and 1.</param>
    /// <param name="precision">
    /// The sketch precision, which fixes the <c>2^precision</c> registers the bands share. Must be
    /// between <see cref="MinHashSketch{T,THasher}.MinPrecision"/> and
    /// <see cref="MinHashSketch{T,THasher}.MaxPrecision"/> inclusive.
    /// </param>
    /// <returns>The chosen band count <c>b</c> and rows per band <c>r</c>, with <c>b · r ≤ 2^precision</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="threshold"/> is not strictly between 0 and 1, or <paramref name="precision"/>
    /// is out of range.
    /// </exception>
    /// <remarks>
    /// Of the pairs with <c>b · r ≤ 2^precision</c> whose proxy knee <c>b^(-1/r)</c> is within
    /// 0.25 of the threshold, the one with the lowest numerically estimated false-positive area
    /// (the model's curve integrated over <c>[0, threshold]</c>) plus false-negative area
    /// (one minus the curve, integrated over
    /// <c>[threshold, 1]</c>) wins. This weights both areas equally over a uniform similarity
    /// axis. Adaptive integration has a finite depth limit, and the restricted search does not
    /// certify a global optimum or workload recall. The search takes several milliseconds at
    /// the default precision and grows with the register count, to a second or more at the
    /// maximum; to build many indexes with one layout, call it once and pass the result to the
    /// explicit constructor.
    /// </remarks>
    public static (int BandCount, int RowsPerBand) GetOptimalParameters(double threshold, int precision)
    {
        if (!(threshold > 0d && threshold < 1d))
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "The threshold must be strictly between 0 and 1.");
        ThrowIfPrecisionOutOfRange(precision);

        int registers = 1 << precision;
        double bestError = double.PositiveInfinity;
        int bestBands = 1;
        int bestRows = 1;
        for (int bands = 1; bands <= registers; bands++)
        {
            int maxRows = registers / bands;
            for (int rows = 1; rows <= maxRows; rows++)
            {
                // Restrict the search to layouts whose proxy knee (1/b)^(1/r) is near the
                // threshold. This pragmatic search bound is not a proof of global optimality.
                // The proxy rises with r, so the upper bound lets this inner loop stop early.
                double knee = Math.Pow(bands, -1d / rows);
                if (knee < threshold - KneeWindow)
                    continue;
                if (knee > threshold + KneeWindow)
                    break;

                double error = FalsePositiveArea(threshold, bands, rows) + FalseNegativeArea(threshold, bands, rows);
                if (error < bestError)
                {
                    bestError = error;
                    bestBands = bands;
                    bestRows = rows;
                }
            }
        }

        return (bestBands, bestRows);
    }

    /// <summary>
    /// Adds <paramref name="key"/> to the index, banded by <paramref name="sketch"/>'s current registers.
    /// </summary>
    /// <param name="key">The key identifying the set.</param>
    /// <param name="sketch">The set's sketch. Read, not kept; it must have the index's <see cref="Precision"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="sketch"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="sketch"/> has a different precision, or <paramref name="key"/> is already in the index.
    /// </exception>
    public void Add(TKey key, MinHashSketch<T, THasher> sketch)
    {
        if (!TryAdd(key, sketch))
            throw new ArgumentException("An entry with the same key already exists in the index.", nameof(key));
    }

    /// <summary>
    /// Adds <paramref name="key"/> to the index, banded by <paramref name="sketch"/>'s current
    /// registers, unless the key is already present.
    /// </summary>
    /// <param name="key">The key identifying the set.</param>
    /// <param name="sketch">The set's sketch. Read, not kept; it must have the index's <see cref="Precision"/>.</param>
    /// <returns><c>true</c> if the key was added; <c>false</c> if it was already present, in which case nothing changes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="sketch"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sketch"/> has a different precision.</exception>
    public bool TryAdd(TKey key, MinHashSketch<T, THasher> sketch)
    {
        ArgumentNullException.ThrowIfNull(key);
        ThrowIfIncompatible(sketch);

        if (_entryOf.ContainsKey(key))
            return false;

        // Everything that can throw for want of memory happens before the first write, so a
        // failed add leaves the index as it was.
        if (_freeHead < 0 && _used == _keys.Length)
            Grow();
        foreach (Dictionary<ulong, int> heads in _heads)
            heads.EnsureCapacity(heads.Count + 1);
        _entryOf.EnsureCapacity(_count + 1);

        ulong[]? rented = null;
        Span<ulong> hashes = _bandCount <= StackScratchLimit
            ? stackalloc ulong[StackScratchLimit]
            : (rented = ArrayPool<ulong>.Shared.Rent(_bandCount));
        hashes = hashes[.._bandCount];
        try
        {
            ComputeBandHashes(sketch.Registers, hashes);
            Link(key, hashes);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<ulong>.Shared.Return(rented);
        }

        return true;
    }

    // Takes a slot and threads its nodes onto the front of each band's bucket. Nothing here
    // allocates: TryAdd reserved the capacity.
    private void Link(TKey key, ReadOnlySpan<ulong> hashes)
    {
        // The key map goes first: it is the one step that calls out (to the key comparer), so if
        // that throws, no slot or bucket has been touched yet.
        int slot = _freeHead >= 0 ? _freeHead : _used;
        _entryOf.Add(key, slot);
        if (_freeHead >= 0)
            _freeHead = _nodeNext[slot * _bandCount];
        else
            _used++;

        int baseNode = slot * _bandCount;
        hashes.CopyTo(_nodeHash.AsSpan(baseNode, _bandCount));
        for (int band = 0; band < _bandCount; band++)
        {
            int node = baseNode + band;
            ref int head = ref CollectionsMarshal.GetValueRefOrAddDefault(_heads[band], hashes[band], out bool exists);
            int next = exists ? head : -1;
            _nodeNext[node] = next;
            _nodePrev[node] = -1;
            if (next >= 0)
                _nodePrev[next] = node;
            head = node;
        }

        _keys[slot] = key;
        _count++;
        _version++;
    }

    /// <summary>Removes <paramref name="key"/> from the index.</summary>
    /// <param name="key">The key to remove.</param>
    /// <returns><c>true</c> if the key was found and removed; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    public bool Remove(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!_entryOf.Remove(key, out int slot))
            return false;

        int baseNode = slot * _bandCount;
        for (int band = 0; band < _bandCount; band++)
        {
            int node = baseNode + band;
            int next = _nodeNext[node];
            int prev = _nodePrev[node];
            if (next >= 0)
                _nodePrev[next] = prev;

            if (prev >= 0)
                _nodeNext[prev] = next;
            else if (next >= 0)
                _heads[band][_nodeHash[node]] = next;
            else
                _heads[band].Remove(_nodeHash[node]);
        }

        _keys[slot] = default!;
        _nodePrev[baseNode] = FreeSlot;
        _nodeNext[baseNode] = _freeHead;
        _freeHead = slot;
        _count--;
        _version++;
        return true;
    }

    /// <summary>Determines whether the index contains <paramref name="key"/>.</summary>
    /// <param name="key">The key to locate.</param>
    /// <returns><c>true</c> if the key is in the index; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    public bool ContainsKey(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _entryOf.ContainsKey(key);
    }

    /// <summary>
    /// Removes every key. The capacity is kept. Clearing an empty index changes nothing and does
    /// not invalidate enumerators.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        _entryOf.Clear();
        foreach (Dictionary<ulong, int> heads in _heads)
            heads.Clear();
        if (RuntimeHelpers.IsReferenceOrContainsReferences<TKey>())
            Array.Clear(_keys, 0, _used);
        _used = 0;
        _freeHead = -1;
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Appends to <paramref name="results"/> each key whose sketch agrees with
    /// <paramref name="sketch"/> on every register of at least one band. Each key is appended once.
    /// </summary>
    /// <param name="sketch">The query sketch. It must have the index's <see cref="Precision"/>.</param>
    /// <param name="results">The collection the candidates are added to. Existing contents are kept.</param>
    /// <returns>
    /// The number of candidates passed to <paramref name="results"/>' <c>Add</c>, each once. For a
    /// destination that ignores a key it already holds, such as a <see cref="HashSet{T}"/> reused
    /// across queries, this can be more than the destination grew.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="sketch"/> or <paramref name="results"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sketch"/> has a different precision.</exception>
    /// <remarks>
    /// The cost is one densify-and-hash pass over the sketch, a walk of the matching buckets, and
    /// <c>O(h log h)</c> sorting for <c>h</c> total band hits. A key contributes one hit per matching
    /// band before deduplication, so the <c>O(h)</c> temporary hit buffer can be much larger than the
    /// number of distinct candidates; <c>h</c> is at most <c>Count · BandCount</c>. Small scratch
    /// buffers are stack-allocated; larger ones are rented and returned. With suitable buffers
    /// available in the pool, the query allocates no managed scratch, but a first, larger or
    /// contended query can allocate. The destination <paramref name="results"/> may also grow.
    /// Candidates are reported once each, in no particular order. Candidates are not verified; see
    /// the type remarks. A query does not modify the index, but <paramref name="results"/> must not
    /// be a collection whose <c>Add</c> modifies the index.
    /// </remarks>
    public int Query(MinHashSketch<T, THasher> sketch, ICollection<TKey> results)
    {
        ThrowIfIncompatible(sketch);
        ArgumentNullException.ThrowIfNull(results);

        if (_count == 0)
            return 0;

        ulong[]? rented = null;
        Span<ulong> query = _bandCount <= StackScratchLimit
            ? stackalloc ulong[StackScratchLimit]
            : (rented = ArrayPool<ulong>.Shared.Rent(_bandCount));
        query = query[.._bandCount];

        int[]? rentedHits = null;
        Span<int> hits = stackalloc int[StackScratchLimit];
        int hitCount = 0;
        int found = 0;
        try
        {
            ComputeBandHashes(sketch.Registers, query);

            // Every slot met in any matching bucket; a key that matches several bands appears once
            // per band and is deduplicated below.
            for (int band = 0; band < _bandCount; band++)
            {
                if (!_heads[band].TryGetValue(query[band], out int node))
                    continue;

                for (; node >= 0; node = _nodeNext[node])
                {
                    if (hitCount == hits.Length)
                        hits = GrowHits(hits, ref rentedHits);
                    hits[hitCount++] = node / _bandCount;
                }
            }

            Span<int> slots = hits[..hitCount];
            slots.Sort();
            int version = _version;
            for (int i = 0; i < slots.Length; i++)
            {
                if (i > 0 && slots[i] == slots[i - 1])
                    continue;

                results.Add(_keys[slots[i]]);
                found++;
                if (version != _version)
                    throw new InvalidOperationException("The index was modified while a query was adding results.");
            }
        }
        finally
        {
            if (rented is not null)
                ArrayPool<ulong>.Shared.Return(rented);
            if (rentedHits is not null)
                ArrayPool<int>.Shared.Return(rentedHits);
        }

        return found;
    }

    /// <summary>
    /// Returns the keys whose sketch agrees with <paramref name="sketch"/> on every register of at
    /// least one band, each once.
    /// </summary>
    /// <param name="sketch">The query sketch. It must have the index's <see cref="Precision"/>.</param>
    /// <returns>A new list of the candidate keys, in no particular order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sketch"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sketch"/> has a different precision.</exception>
    public List<TKey> Query(MinHashSketch<T, THasher> sketch)
    {
        var results = new List<TKey>();
        Query(sketch, results);
        return results;
    }

    /// <summary>Returns an enumerator over the keys in the index, in no particular order.</summary>
    /// <returns>A struct enumerator over the keys.</returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // Moves the hits into a larger pooled buffer, returning the previous pooled one.
    private static Span<int> GrowHits(Span<int> hits, ref int[]? rented)
    {
        int[] next = ArrayPool<int>.Shared.Rent(NextHitCapacity(hits.Length));
        hits.CopyTo(next);
        if (rented is not null)
            ArrayPool<int>.Shared.Return(rented);
        rented = next;
        return next;
    }

    // A query can collect at most one hit per node, so Array.MaxLength also bounds its hits.
    // Widen before doubling: a valid buffer of 2^30 hits must grow without wrapping negative.
    private static int NextHitCapacity(int current) => (int)Math.Min(2L * current, Array.MaxLength);

    private void Grow()
    {
        int capacity = NextCapacity(_keys.Length, _bandCount);

        // Allocate everything before replacing anything, so running out of memory part-way
        // leaves the index unchanged.
        TKey[] keys = new TKey[capacity];
        ulong[] nodeHash = new ulong[capacity * _bandCount];
        int[] nodeNext = new int[capacity * _bandCount];
        int[] nodePrev = new int[capacity * _bandCount];

        int usedNodes = _used * _bandCount;
        Array.Copy(_keys, keys, _used);
        Array.Copy(_nodeHash, nodeHash, usedNodes);
        Array.Copy(_nodeNext, nodeNext, usedNodes);
        Array.Copy(_nodePrev, nodePrev, usedNodes);
        _keys = keys;
        _nodeHash = nodeHash;
        _nodeNext = nodeNext;
        _nodePrev = nodePrev;
    }

    // Doubles the entry capacity, capped so the node arrays (capacity · bandCount) stay within
    // Array.MaxLength.
    [ExcludeFromCodeCoverage(Justification = "The cap only binds once the node arrays near Array.MaxLength elements, which no test can allocate.")]
    private static int NextCapacity(int current, int bandCount)
    {
        int maxEntries = Array.MaxLength / bandCount;
        long doubled = current == 0 ? 4 : (long)current * 2;
        if (current >= maxEntries)
            throw new InvalidOperationException("The index cannot hold any more keys at this band count.");
        return (int)Math.Min(doubled, maxEntries);
    }

    // Hashes each band of the densified registers into `destination`, one value per band.
    private void ComputeBandHashes(ReadOnlySpan<ulong> registers, Span<ulong> destination)
    {
        int banded = _bandCount * _rowsPerBand;
        ulong[]? rented = null;
        Span<ulong> dense = banded <= StackScratchLimit
            ? stackalloc ulong[StackScratchLimit]
            : (rented = ArrayPool<ulong>.Shared.Rent(banded));
        dense = dense[..banded];

        try
        {
            // An empty sketch has no hashes to band, so it is banded from a seed of its own: filling
            // its rows with any 64-bit constant would collide with a set whose one element hashes
            // to that constant.
            ulong tag = Densify(registers, dense, _precision) ? 0 : EmptyTag;
            for (int band = 0; band < destination.Length; band++)
            {
                ulong hash = Mix64((((ulong)band + 1) * Golden) ^ tag);
                ReadOnlySpan<ulong> rows = dense.Slice(band * _rowsPerBand, _rowsPerBand);
                for (int row = 0; row < rows.Length; row++)
                    hash = Mix64(hash ^ rows[row]);
                destination[band] = hash;
            }
        }
        finally
        {
            if (rented is not null)
                ArrayPool<ulong>.Shared.Return(rented);
        }
    }

    // Returns false, with dense filled by a constant, for a sketch with no filled register.
    // Otherwise fills dense[i] for i < dense.Length with the full 64-bit hash of the element register i holds
    // (its index in the top bits, its value below) or, when register i is empty, of the element in
    // the first non-empty register in register i's own probe order. That order is the
    // affine permutation t -> (a·t + c) mod m with a odd, both drawn from a hash of i, so it is the
    // same for every sketch and independent of their contents. Each output is the minimum under
    // a fixed hash order: its own bin first, then the probe order, then low hash within each bin.
    // Under ideal random collision-free element hashes the union's winner is symmetric over its
    // elements, so its membership in the intersection gives marginal agreement J. This does not
    // make different output registers independent. The donor is found either by walking the
    // order or, for a sparse sketch after ProbeLimit steps, as the filled register with the
    // smallest position in it, by inverting the permutation; both give the same register.
    private static bool Densify(ReadOnlySpan<ulong> registers, Span<ulong> dense, int precision)
    {
        if (!registers.ContainsAnyExcept(MinHashSketch<T, THasher>.EmptyRegister))
        {
            dense.Fill(MinHashSketch<T, THasher>.EmptyRegister);
            return false;
        }

        ulong mask = (1UL << precision) - 1;

        int[]? rented = null;
        Span<int> filled = registers.Length <= StackScratchLimit * 2
            ? stackalloc int[StackScratchLimit * 2]
            : (rented = ArrayPool<int>.Shared.Rent(registers.Length));

        try
        {
            // Both routes find the same donor, so the cheaper one can be chosen per sketch. With
            // uniformly distributed occupied bins, walking takes expected O(m / k) steps per
            // empty register and the scan O(k), so walk when k² ≥ m. The scan still tries a few
            // steps of the walk first, and lists the filled
            // registers only once one needs it. This gives expected O(m^1.5) work under that
            // occupancy model; an arbitrary occupied-bin pattern has only an O(m²) bound.
            long filledCount = registers.Length - registers.Count(MinHashSketch<T, THasher>.EmptyRegister);
            ulong walkLimit = filledCount * filledCount >= registers.Length ? ulong.MaxValue : ProbeLimit;
            int listed = -1;
            int valueBits = 64 - precision;
            for (int i = 0; i < dense.Length; i++)
            {
                int source = i;
                if (registers[i] == MinHashSketch<T, THasher>.EmptyRegister)
                {
                    source = ProbeDonor(registers, i, mask, walkLimit);
                    if (source < 0)
                    {
                        if (listed < 0)
                            listed = ListFilled(registers, filled);
                        source = FallbackDonor(filled[..listed], i, mask);
                    }
                }

                // The register a value sits in is the top bits of the element's hash, so put them
                // back: two elements in different registers can share the low bits, and a
                // borrowed value must still name the element it came from.
                dense[i] = ((ulong)source << valueBits) | registers[source];
            }
        }
        finally
        {
            if (rented is not null)
                ArrayPool<int>.Shared.Return(rented);
        }

        return true;
    }

    // Walks the first `limit` steps of register's order; -1 if every one is empty. The order is a
    // permutation, so an unbounded walk over a sketch with a filled register always finds one.
    private static int ProbeDonor(ReadOnlySpan<ulong> registers, int register, ulong mask, ulong limit)
    {
        (ulong multiplier, ulong offset) = Order(register);
        for (ulong step = 0; step < limit; step++)
        {
            int candidate = (int)((multiplier * step + offset) & mask);
            if (registers[candidate] != MinHashSketch<T, THasher>.EmptyRegister)
                return candidate;
        }

        return -1;
    }

    // The filled register earliest in register's order. Position of register j in the order is
    // (j − c) · a⁻¹ mod m; Newton's iteration doubles the correct low bits of an odd number's
    // inverse each step, so five steps from a reach all 64.
    private static int FallbackDonor(ReadOnlySpan<int> filled, int register, ulong mask)
    {
        (ulong multiplier, ulong offset) = Order(register);
        ulong inverse = multiplier;
        for (int round = 0; round < 5; round++)
            inverse *= 2 - multiplier * inverse;

        int donor = filled[0];
        ulong bestPosition = (((ulong)donor - offset) * inverse) & mask;
        for (int k = 1; k < filled.Length; k++)
        {
            int j = filled[k];
            ulong position = (((ulong)j - offset) * inverse) & mask;
            if (position < bestPosition)
            {
                bestPosition = position;
                donor = j;
            }
        }

        return donor;
    }

    private static int ListFilled(ReadOnlySpan<ulong> registers, Span<int> filled)
    {
        int count = 0;
        for (int j = 0; j < registers.Length; j++)
        {
            if (registers[j] != MinHashSketch<T, THasher>.EmptyRegister)
                filled[count++] = j;
        }

        return count;
    }

    // Register i's probe order is t -> (a·t + c) mod m, with a odd so the map is a permutation.
    private static (ulong Multiplier, ulong Offset) Order(int register)
    {
        ulong seed = Mix64(((ulong)register + 1) * Golden);
        return (seed | 1, seed >> 32);
    }

    private void ThrowIfIncompatible(MinHashSketch<T, THasher> sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        if (sketch.Precision != _precision)
            throw new ArgumentException(
                $"The sketch's precision ({sketch.Precision}) does not match the index's ({_precision}).", nameof(sketch));
    }

    private static void ThrowIfPrecisionOutOfRange(int precision)
    {
        if (precision < MinHashSketch<T, THasher>.MinPrecision || precision > MinHashSketch<T, THasher>.MaxPrecision)
            throw new ArgumentOutOfRangeException(nameof(precision), precision,
                $"Precision must be between {MinHashSketch<T, THasher>.MinPrecision} and {MinHashSketch<T, THasher>.MaxPrecision} inclusive.");
    }

    private static double Curve(double jaccard, int bands, int rows)
        => 1d - Math.Pow(1d - Math.Pow(jaccard, rows), bands);

    // Adaptive Simpson's rule. A fixed grid misses the knee of a steep curve (many rows, a
    // threshold near 0 or 1), which is exactly where the search has to tell layouts apart, so the
    // range is cut into Pieces and each is refined until two halves agree to within Tolerance or
    // MaxDepth halvings, the finest step being 1 / (Pieces · 2^MaxDepth) of the range.
    private const int Pieces = 16;

    // How far from the threshold a layout's crossing may sit and still be integrated.
    private const double KneeWindow = 0.25;
    private const int MaxDepth = 12;
    private const double Tolerance = 1e-10;

    private static double FalsePositiveArea(double threshold, int bands, int rows)
        => Area(0d, threshold, bands, rows, complement: false);

    private static double FalseNegativeArea(double threshold, int bands, int rows)
        => Area(threshold, 1d, bands, rows, complement: true);

    private static double Area(double from, double to, int bands, int rows, bool complement)
    {
        double width = (to - from) / Pieces;
        double total = 0d;
        for (int piece = 0; piece < Pieces; piece++)
        {
            double a = from + piece * width;
            double b = piece == Pieces - 1 ? to : a + width;
            double fa = Integrand(a, bands, rows, complement);
            double fb = Integrand(b, bands, rows, complement);
            double fm = Integrand((a + b) / 2, bands, rows, complement);
            double whole = (b - a) / 6 * (fa + 4 * fm + fb);
            total += Refine(a, b, fa, fm, fb, whole, Tolerance / Pieces, MaxDepth, bands, rows, complement);
        }

        return total;
    }

    private static double Refine(
        double a, double b, double fa, double fm, double fb, double whole,
        double tolerance, int depth, int bands, int rows, bool complement)
    {
        double m = (a + b) / 2;
        double flm = Integrand((a + m) / 2, bands, rows, complement);
        double frm = Integrand((m + b) / 2, bands, rows, complement);
        double left = (m - a) / 6 * (fa + 4 * flm + fm);
        double right = (b - m) / 6 * (fm + 4 * frm + fb);
        double delta = left + right - whole;
        if (depth == 0 || Math.Abs(delta) <= 15 * tolerance)
            return left + right + delta / 15;

        return Refine(a, m, fa, flm, fm, left, tolerance / 2, depth - 1, bands, rows, complement)
            + Refine(m, b, fm, frm, fb, right, tolerance / 2, depth - 1, bands, rows, complement);
    }

    private static double Integrand(double s, int bands, int rows, bool complement)
    {
        double p = Curve(s, bands, rows);
        return complement ? 1d - p : p;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Mix64(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>A struct enumerator over the keys of a <see cref="MinHashLshIndex{TKey,T,THasher}"/>.</summary>
    public struct Enumerator : IEnumerator<TKey>
    {
        private readonly MinHashLshIndex<TKey, T, THasher> _index;
        private readonly int _version;
        private int _slot;
        private TKey _current;

        internal Enumerator(MinHashLshIndex<TKey, T, THasher> index)
        {
            _index = index;
            _version = index._version;
            _slot = 0;
            _current = default!;
        }

        /// <summary>Gets the key at the current position of the enumerator.</summary>
        public readonly TKey Current => _current;

        readonly object IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next key.</summary>
        /// <returns><c>true</c> if there is a next key; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The index was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (_version != _index._version)
                throw new InvalidOperationException("The index was modified during enumeration.");

            while (_slot < _index._used)
            {
                int slot = _slot++;
                if (_index._nodePrev[slot * _index._bandCount] != FreeSlot)
                {
                    _current = _index._keys[slot];
                    return true;
                }
            }

            _current = default!;
            return false;
        }

        /// <summary>Resets the enumerator to before the first key.</summary>
        /// <exception cref="InvalidOperationException">The index was modified during enumeration.</exception>
        public void Reset()
        {
            if (_version != _index._version)
                throw new InvalidOperationException("The index was modified during enumeration.");

            _slot = 0;
            _current = default!;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }
}
