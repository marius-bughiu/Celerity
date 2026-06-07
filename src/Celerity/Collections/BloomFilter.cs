using System.Numerics;
using System.Runtime.CompilerServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A space-efficient probabilistic set membership filter, parameterized on a custom
/// <see cref="IHashProvider{T}"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// A Bloom filter answers the question "is this element <em>possibly</em> in the
/// set?" using a fixed-size bit array and a handful of hash functions. It trades
/// exactness for memory: a <see cref="Contains"/> that returns <c>false</c> is
/// always correct (the element was definitely never added — <strong>no false
/// negatives</strong>), while a <c>true</c> may be a <strong>false positive</strong>
/// with a tunable, bounded probability. In return it stores nothing but bits — no
/// keys, no buckets — so for membership-only workloads it uses a small fraction of
/// the memory of a <see cref="HashSet{T}"/> and never grows with element size.
/// </para>
/// <para>
/// The filter sizes itself at construction from the expected element count
/// <c>n</c> and the target false-positive rate <c>p</c>, using the standard optimal
/// formulas: bit count <c>m = ceil(-n·ln(p) / (ln 2)²)</c> (rounded up to a power of
/// two so the bit index is a mask, not a modulo) and hash count
/// <c>k = round((m/n)·ln 2)</c>. The <c>k</c> bit positions for an element are
/// derived from a single <see cref="IHashProvider{T}"/> call by double hashing
/// (Kirsch–Mitzenmacher): the 32-bit base hash is avalanched into 64 bits whose two
/// halves seed the recurrence <c>g_i = h1 + i·h2</c>, so adding more hash functions
/// costs arithmetic, not more <see cref="IHashProvider{T}.Hash"/> calls.
/// </para>
/// <para>
/// Because the filter stores only bits there is no empty-slot sentinel, so unlike
/// the hash-table collections it needs no out-of-band handling for <c>default(T)</c>
/// (a zero <c>int</c>, <see cref="System.Guid.Empty"/>, …): those are hashed and
/// added like any other element. A <c>null</c> reference is mapped to a fixed base
/// hash so the filter never invokes the hasher with <c>null</c> (string hashers
/// throw on <c>null</c>), matching the library's out-of-band-<c>null</c> convention.
/// </para>
/// <para>
/// The filter is add-and-test only: there is no <c>Remove</c>. Clearing a single
/// bit could erase membership for an unrelated element that hashed onto it, which
/// would introduce false negatives. Use <see cref="Clear"/> to reset the whole
/// filter, or a counting filter variant if per-element deletion is required.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of elements tested for membership.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute element hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class BloomFilter<T, THasher> where THasher : struct, IHashProvider<T>
{
    /// <summary>
    /// The default target false-positive probability used when a constructor does
    /// not specify one: 1%.
    /// </summary>
    public const double DEFAULT_FALSE_POSITIVE_RATE = 0.01;

    private const double Ln2 = 0.6931471805599453;       // ln(2)
    private const double Ln2Squared = 0.4804530139182014; // (ln 2)²

    private readonly ulong[] _bits;
    private readonly int _bitCount;            // m, a power of two
    private readonly int _mask;                // m - 1
    private readonly int _hashCount;           // k
    private readonly int _capacity;            // expected element count, n
    private readonly double _falsePositiveRate; // target p
    private readonly THasher _hasher;

    private int _count;

    /// <summary>
    /// Initializes a new instance of the <see cref="BloomFilter{T,THasher}"/> class
    /// sized for the specified expected element count and target false-positive rate.
    /// </summary>
    /// <param name="expectedItems">
    /// The number of distinct elements the filter is expected to hold. Drives the
    /// bit-array size and hash count; staying at or below it keeps the measured
    /// false-positive rate at or under <paramref name="falsePositiveRate"/>. Must be
    /// positive.
    /// </param>
    /// <param name="falsePositiveRate">
    /// The target probability (strictly between 0 and 1) that <see cref="Contains"/>
    /// returns <c>true</c> for an element that was never added, once the filter holds
    /// <paramref name="expectedItems"/> elements. Smaller values allocate more bits.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="expectedItems"/> is not positive, or
    /// <paramref name="falsePositiveRate"/> is not strictly between 0 and 1.
    /// </exception>
    public BloomFilter(int expectedItems, double falsePositiveRate = DEFAULT_FALSE_POSITIVE_RATE)
    {
        if (expectedItems <= 0)
            throw new ArgumentOutOfRangeException(nameof(expectedItems), expectedItems, "Expected item count must be positive.");
        if (falsePositiveRate <= 0d || falsePositiveRate >= 1d || double.IsNaN(falsePositiveRate))
            throw new ArgumentOutOfRangeException(nameof(falsePositiveRate), falsePositiveRate, "False-positive rate must be between 0 (exclusive) and 1 (exclusive).");

        _capacity = expectedItems;
        _falsePositiveRate = falsePositiveRate;

        // Optimal bit count m = -n·ln(p) / (ln 2)². Round up to a power of two so a
        // bit index is computed with a mask rather than a modulo; the extra bits
        // only lower the realized false-positive rate, never raise it.
        double mOptimal = -(expectedItems * Math.Log(falsePositiveRate)) / Ln2Squared;
        int m = FastUtils.NextPowerOfTwo((int)Math.Min(Math.Ceiling(mOptimal), 1 << 30));
        if (m < 64)
            m = 64; // one ulong word minimum

        _bitCount = m;
        _mask = m - 1;

        // Optimal hash count k = (m/n)·ln 2, at least one.
        int k = (int)Math.Round((m / (double)expectedItems) * Ln2);
        _hashCount = k < 1 ? 1 : k;

        _bits = new ulong[m >> 6]; // m is a power of two >= 64, so this divides evenly
        _hasher = default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BloomFilter{T,THasher}"/> class
    /// pre-populated with the elements of <paramref name="source"/> and sized to hold
    /// them at the specified target false-positive rate.
    /// </summary>
    /// <param name="source">
    /// The elements to add. The filter is sized from the source's element count
    /// (taken from <see cref="ICollection{T}.Count"/> when available, otherwise from a
    /// single counting pass), so the realized false-positive rate honors
    /// <paramref name="falsePositiveRate"/>.
    /// </param>
    /// <param name="falsePositiveRate">
    /// The target false-positive probability; see the primary constructor.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="falsePositiveRate"/> is not strictly between 0 and 1.
    /// </exception>
    public BloomFilter(IEnumerable<T> source, double falsePositiveRate = DEFAULT_FALSE_POSITIVE_RATE)
        : this(ExpectedItemsForSource(source), falsePositiveRate)
    {
        foreach (T item in source)
            Add(item);
    }

    // Runs as part of the chained-ctor argument expression so the null check beats
    // the primary ctor's falsePositiveRate validation: a null source must surface as
    // ArgumentNullException, not ArgumentOutOfRangeException, even when the caller
    // also passed an out-of-range rate.
    private static int ExpectedItemsForSource(IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is ICollection<T> collection)
            return collection.Count > 0 ? collection.Count : 1;

        // Unknown size: count in one pass. IEnumerable<T> is conventionally
        // re-enumerable, so the constructor's add pass walks it again. A minimum of
        // one keeps the primary ctor's positive-count contract.
        int count = 0;
        foreach (T _ in source)
            count++;
        return count > 0 ? count : 1;
    }

    /// <summary>
    /// Gets the number of times <see cref="Add"/> has been called since construction
    /// or the last <see cref="Clear"/>.
    /// </summary>
    /// <remarks>
    /// This is an insertion counter, <strong>not</strong> a distinct-element count: a
    /// Bloom filter cannot tell whether an element was already present, so adding the
    /// same element twice increments <see cref="Count"/> twice. For an estimate of
    /// the realized false-positive probability at the current fill level, use
    /// <see cref="CurrentFalsePositiveProbability"/>.
    /// </remarks>
    public int Count => _count;

    /// <summary>
    /// Gets the expected element count the filter was sized for (the
    /// <c>expectedItems</c> constructor argument).
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Gets the number of bits in the backing bit array (<c>m</c>), always a power of
    /// two.
    /// </summary>
    public int BitCount => _bitCount;

    /// <summary>
    /// Gets the number of hash functions applied per element (<c>k</c>).
    /// </summary>
    public int HashCount => _hashCount;

    /// <summary>
    /// Gets the target false-positive probability the filter was sized for (the
    /// <c>falsePositiveRate</c> constructor argument).
    /// </summary>
    public double FalsePositiveRate => _falsePositiveRate;

    /// <summary>
    /// Gets an estimate of the current false-positive probability based on how many
    /// bits are actually set: <c>(setBits / m)^k</c>.
    /// </summary>
    /// <remarks>
    /// This reflects the filter's <em>actual</em> fill rather than the target, so it
    /// can be used to detect over-filling: once it climbs well past
    /// <see cref="FalsePositiveRate"/> the filter is holding more than its expected
    /// element count and lookups are returning more false positives than intended.
    /// </remarks>
    public double CurrentFalsePositiveProbability
    {
        get
        {
            long setBits = 0;
            ulong[] bits = _bits;
            for (int i = 0; i < bits.Length; i++)
                setBits += BitOperations.PopCount(bits[i]);

            double ratio = setBits / (double)_bitCount;
            return Math.Pow(ratio, _hashCount);
        }
    }

    /// <summary>
    /// Adds an element to the filter. After this call <see cref="Contains"/> returns
    /// <c>true</c> for <paramref name="item"/>.
    /// </summary>
    /// <param name="item">The element to add. Adding the same element twice is a no-op
    /// for membership but still increments <see cref="Count"/>.</param>
    public void Add(T item)
    {
        ComputeHashes(item, out uint h1, out uint h2);

        ulong[] bits = _bits;
        int mask = _mask;
        uint combined = h1;
        for (int i = 0; i < _hashCount; i++)
        {
            int bitIndex = (int)(combined & (uint)mask);
            bits[bitIndex >> 6] |= 1UL << (bitIndex & 63);
            combined += h2;
        }

        _count++;
    }

    /// <summary>
    /// Determines whether the filter <em>possibly</em> contains an element.
    /// </summary>
    /// <param name="item">The element to test.</param>
    /// <returns>
    /// <c>false</c> if <paramref name="item"/> was definitely never added (no false
    /// negatives); <c>true</c> if it was probably added — subject to the filter's
    /// false-positive rate.
    /// </returns>
    public bool Contains(T item)
    {
        ComputeHashes(item, out uint h1, out uint h2);

        ulong[] bits = _bits;
        int mask = _mask;
        uint combined = h1;
        for (int i = 0; i < _hashCount; i++)
        {
            int bitIndex = (int)(combined & (uint)mask);
            if ((bits[bitIndex >> 6] & (1UL << (bitIndex & 63))) == 0)
                return false;
            combined += h2;
        }

        return true;
    }

    /// <summary>
    /// Resets the filter to empty, clearing every bit. The bit-array size and hash
    /// count are preserved.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        Array.Clear(_bits, 0, _bits.Length);
        _count = 0;
    }

    /// <summary>
    /// Merges another filter into this one in place, so this filter afterwards reports
    /// <c>true</c> for every element either filter held. Both filters must have been
    /// constructed with identical parameters (same <see cref="BitCount"/> and
    /// <see cref="HashCount"/>).
    /// </summary>
    /// <param name="other">The filter to merge in. Left unmodified.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="other"/> has a different bit count or hash count, so the two
    /// filters are not bitwise compatible.
    /// </exception>
    /// <remarks>
    /// Because a Bloom filter cannot distinguish overlapping elements, the merged
    /// <see cref="Count"/> is the sum of both insertion counters and so may exceed the
    /// number of distinct elements represented.
    /// </remarks>
    public void UnionWith(BloomFilter<T, THasher> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other._bitCount != _bitCount || other._hashCount != _hashCount)
            throw new ArgumentException("The two filters must have the same bit count and hash count to be merged.", nameof(other));

        ulong[] bits = _bits;
        ulong[] otherBits = other._bits;
        for (int i = 0; i < bits.Length; i++)
            bits[i] |= otherBits[i];

        _count += other._count;
    }

    // Derives two independent 32-bit hash lanes from a single IHashProvider call by
    // avalanching the 32-bit base hash into 64 bits (the SplitMix64 finalizer) and
    // splitting the result. A null reference is mapped to a fixed base hash so the
    // hasher (which may throw on null, e.g. the string hashers) is never invoked with
    // null — value-type defaults (0, Guid.Empty) are valid inputs and go through the
    // hasher normally. The typeof(T).IsValueType guard is a JIT-time constant, so the
    // null check is compiled away entirely for value-type instantiations (no boxing).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ComputeHashes(T item, out uint h1, out uint h2)
    {
        int baseHash = (!typeof(T).IsValueType && item is null) ? 0 : _hasher.Hash(item);
        ulong mixed = Mix64((uint)baseHash);
        h1 = (uint)mixed;
        h2 = (uint)(mixed >> 32);

        // h2 is the stride of the g_i = h1 + i·h2 recurrence; a zero stride would map
        // every hash function to the same bit. Force it odd (and non-zero) so the k
        // probes spread out.
        h2 |= 1u;
    }

    // SplitMix64 finalizer seeded with the 32-bit base hash widened by the golden-ratio
    // increment, so even a zero base hash avalanches to a well-distributed 64-bit value.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Mix64(uint x)
    {
        ulong z = x + 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
