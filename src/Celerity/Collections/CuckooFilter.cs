using System.Runtime.CompilerServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A space-efficient probabilistic set membership filter that — unlike <see cref="BloomFilter{T,THasher}"/> —
/// <strong>supports deletion</strong>, parameterized on a custom <see cref="IHashProvider{T}"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Like a Bloom filter, a cuckoo filter answers "is this element <em>possibly</em> in the set?" with
/// <strong>no false negatives</strong> (a <see cref="Contains"/> that returns <c>false</c> is always correct)
/// and a tunable, bounded false-positive probability. Instead of a bit array it stores a short
/// <em>fingerprint</em> of each element in a table of fixed-size buckets, which is what lets it answer the one
/// question a Bloom filter cannot: <see cref="Remove"/> deletes a single element without introducing false
/// negatives for the others. The BCL ships no probabilistic membership filter, so for membership-only
/// workloads this uses a small fraction of the memory of a <see cref="HashSet{T}"/> and never grows with
/// element size.
/// </para>
/// <para>
/// The structure is partial-key cuckoo hashing (Fan, Andersen, Kaminsky, Mitzenmacher — <em>"Cuckoo Filter:
/// Practically Better Than Bloom"</em>, CoNEXT&#160;2014). Each element has two candidate buckets,
/// <c>i1 = h(x)</c> and <c>i2 = i1 XOR h(fingerprint)</c>; because the bucket count is a power of two the XOR
/// is an involution, so <c>i1</c> can be recovered from <c>i2</c> and the stored fingerprint alone — no need to
/// keep the original key. An insert places the fingerprint in either candidate bucket; when both are full it
/// evicts a resident fingerprint and re-homes it in <em>its</em> alternate bucket, repeating up to a bounded
/// number of kicks. The fingerprint and the primary index are both derived from a <strong>single</strong>
/// <see cref="IHashProvider{T}.Hash"/> call avalanched into 64 bits (the SplitMix64 finalizer).
/// </para>
/// <para>
/// A lookup or a delete touches at most two buckets (≈ two cache lines) regardless of fill, and the fingerprint
/// layout is competitive-to-better than a Bloom filter on bytes-per-element at low target false-positive rates
/// — while additionally supporting deletion. The trade-offs versus <see cref="BloomFilter{T,THasher}"/>:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <strong>Insertion can fail when very full.</strong> Beyond a high load factor an insert may exhaust its
/// eviction budget. The filter then keeps the one homeless fingerprint in a single-entry victim cache and
/// reports itself <em>full</em>: further <see cref="Add"/> calls throw (use <see cref="TryAdd"/> for the
/// non-throwing form) until a <see cref="Remove"/> frees space. No previously inserted element is ever lost, so
/// the no-false-negatives guarantee holds even at the failure boundary.
/// </description></item>
/// <item><description>
/// <strong>Delete only what you inserted.</strong> Removing an element that was never added can, with the
/// filter's false-positive probability, delete a different element that shares its fingerprint and bucket —
/// introducing a false negative for <em>that</em> element. Only call <see cref="Remove"/> for elements you know
/// were added.
/// </description></item>
/// </list>
/// <para>
/// Because the filter stores fingerprints, not keys, it needs <strong>no out-of-band handling</strong> for
/// <c>default(T)</c> (a zero <c>int</c>, <see cref="System.Guid.Empty"/>, the empty string): those are hashed
/// like any other element. A <c>null</c> reference is mapped to a fixed base hash so the filter never invokes
/// the hasher with <c>null</c> (string hashers throw on <c>null</c>), matching the library's
/// out-of-band-<c>null</c> convention.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of elements tested for membership.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute element hashes. Must be a value type implementing <see cref="IHashProvider{T}"/>
/// so the JIT can devirtualize and inline it.
/// </typeparam>
public class CuckooFilter<T, THasher> where THasher : struct, IHashProvider<T>
{
    /// <summary>
    /// The default target false-positive probability used when a constructor does not specify one: 1%.
    /// </summary>
    public const double DEFAULT_FALSE_POSITIVE_RATE = 0.01;

    // Slots per bucket. Four is the paper's sweet spot: it reaches ~95% load before insertions start failing
    // while keeping a lookup to a single cache line per candidate bucket.
    private const int BucketSize = 4;

    // Upper bound on eviction relocations before an insert gives up and parks the homeless fingerprint in the
    // victim cache. 500 is the paper's recommended kick budget.
    private const int MaxKicks = 500;

    // Target load before sizing: bucketCount is rounded up to a power of two from n / (BucketSize · this), so
    // the realized table has headroom and insertions rarely reach the eviction bound.
    private const double TargetLoad = 0.94;

    private readonly ushort[] _data;          // bucketCount · BucketSize fingerprint slots; 0 == empty
    private readonly int _bucketCount;        // a power of two
    private readonly int _bucketMask;         // bucketCount - 1
    private readonly int _fingerprintBits;    // f, in [1, 16]
    private readonly uint _fingerprintMask;   // (1 << f) - 1
    private readonly int _capacity;           // expected element count, n
    private readonly double _falsePositiveRate; // target p
    private readonly THasher _hasher;

    private WyRand _rng;                       // eviction victim selection (deterministic seed)
    private int _count;                        // live stored fingerprints (buckets + victim)

    private bool _hasVictim;                   // an insert exhausted its kicks; the filter is full
    private ushort _victimFingerprint;
    private int _victimIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="CuckooFilter{T,THasher}"/> class sized for the specified
    /// expected element count and target false-positive rate.
    /// </summary>
    /// <param name="expectedItems">
    /// The number of distinct elements the filter is expected to hold. Drives the bucket count and fingerprint
    /// width; staying at or below it keeps the measured false-positive rate at or under
    /// <paramref name="falsePositiveRate"/>. Must be positive.
    /// </param>
    /// <param name="falsePositiveRate">
    /// The target probability (strictly between 0 and 1) that <see cref="Contains"/> returns <c>true</c> for an
    /// element that was never added. Smaller values widen the stored fingerprint. The achievable minimum is
    /// bounded by the 16-bit fingerprint ceiling (≈ <c>2·BucketSize / 2¹⁶</c>); a stricter request is clamped to
    /// that floor.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="expectedItems"/> is not positive, or <paramref name="falsePositiveRate"/> is not strictly
    /// between 0 and 1.
    /// </exception>
    public CuckooFilter(int expectedItems, double falsePositiveRate = DEFAULT_FALSE_POSITIVE_RATE)
    {
        if (expectedItems <= 0)
            throw new ArgumentOutOfRangeException(nameof(expectedItems), expectedItems, "Expected item count must be positive.");
        if (falsePositiveRate <= 0d || falsePositiveRate >= 1d || double.IsNaN(falsePositiveRate))
            throw new ArgumentOutOfRangeException(nameof(falsePositiveRate), falsePositiveRate, "False-positive rate must be between 0 (exclusive) and 1 (exclusive).");

        _capacity = expectedItems;
        _falsePositiveRate = falsePositiveRate;

        // Fingerprint width f = ceil(log2(2·BucketSize / p)): with BucketSize slots per bucket and two candidate
        // buckets, the per-lookup false-positive probability is ~2·BucketSize / 2^f. Clamp to [1, 16] so a
        // fingerprint fits a ushort (bit-packing narrower fingerprints is a future space optimization).
        double fExact = Math.Log2((2.0 * BucketSize) / falsePositiveRate);
        int f = (int)Math.Ceiling(fExact);
        if (f < 1) f = 1;
        if (f > 16) f = 16;
        _fingerprintBits = f;
        _fingerprintMask = (uint)((1 << f) - 1);

        // Bucket count, rounded up to a power of two so the alternate-bucket XOR stays in range.
        int desired = (int)Math.Ceiling(expectedItems / (BucketSize * TargetLoad));
        int buckets = FastUtils.NextPowerOfTwo(desired < 1 ? 1 : desired);
        _bucketCount = buckets;
        _bucketMask = buckets - 1;

        _data = new ushort[buckets * BucketSize];
        _hasher = default;
        // Fixed seed: eviction is internal bookkeeping, so a deterministic stream keeps behaviour reproducible
        // without affecting correctness.
        _rng = new WyRand(0x9E3779B97F4A7C15UL);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CuckooFilter{T,THasher}"/> class pre-populated with the
    /// elements of <paramref name="source"/> and sized to hold them at the specified target false-positive rate.
    /// </summary>
    /// <param name="source">
    /// The elements to add. The filter is sized from the source's element count (taken from
    /// <see cref="ICollection{T}.Count"/> when available, otherwise from a single counting pass), so the
    /// realized false-positive rate honors <paramref name="falsePositiveRate"/>.
    /// </param>
    /// <param name="falsePositiveRate">The target false-positive probability; see the primary constructor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="falsePositiveRate"/> is not strictly between 0 and 1.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The filter becomes full before every element is added (see <see cref="Add"/>).
    /// </exception>
    public CuckooFilter(IEnumerable<T> source, double falsePositiveRate = DEFAULT_FALSE_POSITIVE_RATE)
        : this(ExpectedItemsForSource(source), falsePositiveRate)
    {
        foreach (T item in source)
            Add(item);
    }

    // Runs as part of the chained-ctor argument expression so the null check beats the primary ctor's
    // falsePositiveRate validation: a null source must surface as ArgumentNullException, not
    // ArgumentOutOfRangeException, even when the caller also passed an out-of-range rate.
    private static int ExpectedItemsForSource(IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is ICollection<T> collection)
            return collection.Count > 0 ? collection.Count : 1;

        int count = 0;
        foreach (T _ in source)
            count++;
        return count > 0 ? count : 1;
    }

    /// <summary>
    /// Gets the number of elements currently stored in the filter. Unlike <see cref="BloomFilter{T,THasher}"/>'s
    /// insertion counter, this is a live count: <see cref="Add"/> increments it and <see cref="Remove"/>
    /// decrements it. A cuckoo filter cannot detect duplicates, so adding the same element twice counts twice.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets the expected element count the filter was sized for (the <c>expectedItems</c> constructor argument).
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Gets the number of buckets in the backing table, always a power of two. The table holds
    /// <c>BucketCount · 4</c> fingerprint slots.
    /// </summary>
    public int BucketCount => _bucketCount;

    /// <summary>
    /// Gets the width in bits of each stored fingerprint (<c>f</c>), in the range <c>[1, 16]</c>.
    /// </summary>
    public int FingerprintBits => _fingerprintBits;

    /// <summary>
    /// Gets the target false-positive probability the filter was sized for (the <c>falsePositiveRate</c>
    /// constructor argument).
    /// </summary>
    public double FalsePositiveRate => _falsePositiveRate;

    /// <summary>
    /// Gets the current fraction of the table's fingerprint slots that are occupied
    /// (<c>Count / (BucketCount · 4)</c>). Insertions begin to risk failure as this approaches ~0.95.
    /// </summary>
    public double LoadFactor => _count / (double)(_bucketCount * BucketSize);

    /// <summary>
    /// Gets a value indicating whether the filter is full: an insertion has exhausted its eviction budget and a
    /// fingerprint is parked in the single-entry victim cache. While <c>true</c>, <see cref="Add"/> throws and
    /// <see cref="TryAdd"/> returns <c>false</c>; a successful <see cref="Remove"/> clears it.
    /// </summary>
    public bool IsFull => _hasVictim;

    /// <summary>
    /// Adds an element to the filter. After this call <see cref="Contains"/> returns <c>true</c> for
    /// <paramref name="item"/>.
    /// </summary>
    /// <param name="item">
    /// The element to add. Adding the same element twice stores a second fingerprint copy and increments
    /// <see cref="Count"/> twice; a matching <see cref="Remove"/> removes one copy.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The filter is full — an insertion exhausted its eviction budget. This happens only when loaded well
    /// beyond <see cref="Capacity"/>; use <see cref="TryAdd"/> for the non-throwing form, or remove elements
    /// first.
    /// </exception>
    public void Add(T item)
    {
        if (!TryAdd(item))
            throw new InvalidOperationException("The cuckoo filter is full: an insertion exhausted its eviction budget. Remove elements or size the filter for more capacity.");
    }

    /// <summary>
    /// Attempts to add an element to the filter without throwing when it is full.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <returns>
    /// <c>true</c> if the element was stored; <c>false</c> if the filter is full (an insertion exhausted its
    /// eviction budget). On <c>false</c> the filter is unchanged and no previously added element is lost.
    /// </returns>
    public bool TryAdd(T item)
    {
        if (_hasVictim)
            return false;

        ComputeFingerprintAndIndex(item, out ushort fingerprint, out int i1);
        return AddFingerprint(i1, fingerprint);
    }

    /// <summary>
    /// Determines whether the filter <em>possibly</em> contains an element.
    /// </summary>
    /// <param name="item">The element to test.</param>
    /// <returns>
    /// <c>false</c> if <paramref name="item"/> was definitely never added (no false negatives); <c>true</c> if it
    /// was probably added — subject to the filter's false-positive rate.
    /// </returns>
    public bool Contains(T item)
    {
        ComputeFingerprintAndIndex(item, out ushort fingerprint, out int i1);
        int i2 = AltIndex(i1, fingerprint);

        if (BucketContains(i1, fingerprint) || BucketContains(i2, fingerprint))
            return true;

        // The victim's two candidate buckets are {_victimIndex, AltIndex(_victimIndex, fp)}; for a matching
        // fingerprint the queried {i1, i2} is the same pair, so testing either against _victimIndex suffices.
        return _hasVictim && _victimFingerprint == fingerprint && (i1 == _victimIndex || i2 == _victimIndex);
    }

    /// <summary>
    /// Removes one copy of an element from the filter.
    /// </summary>
    /// <param name="item">The element to remove.</param>
    /// <returns>
    /// <c>true</c> if a matching fingerprint was found and removed; <c>false</c> if the element was definitely
    /// never added.
    /// </returns>
    /// <remarks>
    /// Only remove elements you know were added. Because the filter stores fingerprints rather than keys,
    /// removing an element that was never added can — with the filter's false-positive probability — delete a
    /// different element that shares its fingerprint and a candidate bucket, introducing a false negative for
    /// that element.
    /// </remarks>
    public bool Remove(T item)
    {
        ComputeFingerprintAndIndex(item, out ushort fingerprint, out int i1);
        int i2 = AltIndex(i1, fingerprint);

        if (TryRemoveFromBucket(i1, fingerprint) || TryRemoveFromBucket(i2, fingerprint))
        {
            _count--;
            // A bucket slot just freed up; try to re-home the parked victim so the filter is usable again.
            RelocateVictimIfPossible();
            return true;
        }

        if (_hasVictim && _victimFingerprint == fingerprint && (i1 == _victimIndex || i2 == _victimIndex))
        {
            _hasVictim = false;
            _victimFingerprint = 0;
            _count--;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resets the filter to empty, clearing every fingerprint. The bucket count and fingerprint width are
    /// preserved.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        Array.Clear(_data, 0, _data.Length);
        _count = 0;
        _hasVictim = false;
        _victimFingerprint = 0;
    }

    /// <summary>
    /// Merges another filter into this one in place, so this filter afterwards reports <c>true</c> for every
    /// element either filter held. Both filters must have been constructed with identical geometry (same
    /// <see cref="BucketCount"/> and <see cref="FingerprintBits"/>).
    /// </summary>
    /// <param name="other">The filter to merge in. Left unmodified.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="other"/> has a different bucket count or fingerprint width, so the two filters are not
    /// compatible.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// This filter becomes full before every fingerprint from <paramref name="other"/> is absorbed. Some of
    /// <paramref name="other"/>'s fingerprints may already have been merged in; no previously stored element is
    /// lost.
    /// </exception>
    /// <remarks>
    /// Because a cuckoo filter cannot distinguish overlapping elements, the merged <see cref="Count"/> is the sum
    /// of both counts and so may exceed the number of distinct elements represented.
    /// </remarks>
    public void UnionWith(CuckooFilter<T, THasher> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other._bucketCount != _bucketCount || other._fingerprintBits != _fingerprintBits)
            throw new ArgumentException("The two filters must have the same bucket count and fingerprint width to be merged.", nameof(other));

        ushort[] otherData = other._data;
        for (int bucket = 0; bucket < other._bucketCount; bucket++)
        {
            int baseSlot = bucket * BucketSize;
            for (int slot = 0; slot < BucketSize; slot++)
            {
                ushort fp = otherData[baseSlot + slot];
                if (fp != 0 && !AddFingerprint(bucket, fp))
                    throw new InvalidOperationException("The cuckoo filter became full while merging; the destination has no room for every fingerprint.");
            }
        }

        if (other._hasVictim && !AddFingerprint(other._victimIndex, other._victimFingerprint))
            throw new InvalidOperationException("The cuckoo filter became full while merging; the destination has no room for every fingerprint.");
    }

    // ── internals ────────────────────────────────────────────────────────────────────────

    // Core insertion. 'index' is one of the fingerprint's two candidate buckets. Returns false only when the
    // eviction chain exhausts MaxKicks; in that case the one homeless fingerprint is parked in the victim cache
    // (the filter is then full) and the element is still counted — nothing is dropped, so there are no false
    // negatives even at the boundary.
    private bool AddFingerprint(int index, ushort fingerprint)
    {
        if (_hasVictim)
            return false;

        if (TryInsertIntoBucket(index, fingerprint))
        {
            _count++;
            return true;
        }

        int alt = AltIndex(index, fingerprint);
        if (TryInsertIntoBucket(alt, fingerprint))
        {
            _count++;
            return true;
        }

        // Both candidate buckets are full: evict. Invariant across the loop — 'current' is a homeless
        // fingerprint and 'index' is one of its candidate buckets, so the set {bucket contents} ∪ {current} is
        // preserved on every swap and no fingerprint is ever lost.
        index = (_rng.NextUInt64() & 1UL) == 0 ? index : alt;
        ushort current = fingerprint;
        for (int kick = 0; kick < MaxKicks; kick++)
        {
            int slot = (int)(_rng.NextUInt64() & (BucketSize - 1));
            int pos = index * BucketSize + slot;

            ushort evicted = _data[pos];
            _data[pos] = current;       // 'current' now sits in 'index', one of its candidate buckets
            current = evicted;          // the evicted fingerprint is now homeless
            index = AltIndex(index, current); // and its other candidate bucket

            if (TryInsertIntoBucket(index, current))
            {
                _count++;
                return true;
            }
        }

        // Out of kicks: park the homeless fingerprint. The element is represented (counted), the filter is full.
        _victimFingerprint = current;
        _victimIndex = index;
        _hasVictim = true;
        _count++;
        return true;
    }

    private void RelocateVictimIfPossible()
    {
        if (!_hasVictim)
            return;

        if (TryInsertIntoBucket(_victimIndex, _victimFingerprint) ||
            TryInsertIntoBucket(AltIndex(_victimIndex, _victimFingerprint), _victimFingerprint))
        {
            _hasVictim = false;
            _victimFingerprint = 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryInsertIntoBucket(int bucket, ushort fingerprint)
    {
        int baseSlot = bucket * BucketSize;
        for (int slot = 0; slot < BucketSize; slot++)
        {
            if (_data[baseSlot + slot] == 0)
            {
                _data[baseSlot + slot] = fingerprint;
                return true;
            }
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool BucketContains(int bucket, ushort fingerprint)
    {
        int baseSlot = bucket * BucketSize;
        for (int slot = 0; slot < BucketSize; slot++)
        {
            if (_data[baseSlot + slot] == fingerprint)
                return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryRemoveFromBucket(int bucket, ushort fingerprint)
    {
        int baseSlot = bucket * BucketSize;
        for (int slot = 0; slot < BucketSize; slot++)
        {
            if (_data[baseSlot + slot] == fingerprint)
            {
                _data[baseSlot + slot] = 0;
                return true;
            }
        }
        return false;
    }

    // The alternate bucket: i2 = i1 XOR (h(fingerprint) mod bucketCount). h(fingerprint) depends only on the
    // fingerprint, and bucketCount is a power of two, so this is an involution — AltIndex(AltIndex(i,fp),fp) == i
    // — which is what lets a lookup recover both candidate buckets from the stored fingerprint alone.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AltIndex(int index, ushort fingerprint)
    {
        uint h = fingerprint * 0x5BD1E995u; // MurmurHash2 mixing constant
        h ^= h >> 15;
        return index ^ (int)(h & (uint)_bucketMask);
    }

    // Derives a non-zero f-bit fingerprint and the primary bucket index from a single IHashProvider call by
    // avalanching the 32-bit base hash into 64 bits (the SplitMix64 finalizer) and splitting the result. A null
    // reference is mapped to a fixed base hash so the hasher (which may throw on null, e.g. the string hashers)
    // is never invoked with null — value-type defaults (0, Guid.Empty) are valid inputs and go through the
    // hasher normally. The typeof(T).IsValueType guard is a JIT-time constant, so the null check is compiled
    // away entirely for value-type instantiations (no boxing).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ComputeFingerprintAndIndex(T item, out ushort fingerprint, out int index)
    {
        int baseHash = (!typeof(T).IsValueType && item is null) ? 0 : _hasher.Hash(item);
        ulong mixed = Mix64((uint)baseHash);

        uint fp = (uint)mixed & _fingerprintMask;
        if (fp == 0)
            fp = 1; // 0 marks an empty slot, so it can never be a fingerprint value
        fingerprint = (ushort)fp;

        index = (int)((uint)(mixed >> 32) & (uint)_bucketMask);
    }

    // SplitMix64 finalizer seeded with the 32-bit base hash widened by the golden-ratio increment, so even a
    // zero base hash avalanches to a well-distributed 64-bit value.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Mix64(uint x)
    {
        ulong z = x + 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
