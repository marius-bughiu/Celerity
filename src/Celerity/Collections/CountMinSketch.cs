using System.Runtime.CompilerServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A space-efficient probabilistic frequency estimator — it estimates how many times
/// each element has been added to it — parameterized on a custom
/// <see cref="IHashProvider{T}"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// A Count-Min sketch answers "roughly how many times have I seen this element?" in a
/// fixed, small amount of memory that does <strong>not</strong> grow with the number of
/// distinct elements. Where a <see cref="Dictionary{TKey, TValue}"/> frequency table
/// must store every distinct key to count it — memory proportional to the number of
/// distinct elements — a Count-Min sketch stores only a fixed
/// <c>depth × width</c> grid of counters (a few kilobytes), trading exactness for a
/// bounded one-sided error: <see cref="EstimateCount"/> <strong>never underestimates</strong>
/// an element's true frequency, and with probability at least <c>1 − delta</c> it
/// overestimates by no more than <c>epsilon · TotalCount</c> (Cormode &amp; Muthukrishnan,
/// 2005).
/// </para>
/// <para>
/// The sketch sizes itself at construction from two error parameters: the relative error
/// factor <c>epsilon</c> drives the per-row counter count <c>w = ceil(e / epsilon)</c>
/// (rounded up to a power of two so a column index is a mask, not a modulo), and the
/// failure probability <c>delta</c> drives the row count <c>d = ceil(ln(1 / delta))</c>.
/// Each element is mapped to one counter per row; <see cref="Add(T, long)"/> increments those
/// <c>d</c> counters and <see cref="EstimateCount"/> returns the <em>minimum</em> across
/// them — because every counter that an element touches accumulates that element's full
/// count plus only non-negative contributions from colliding elements, the minimum is
/// the tightest of the <c>d</c> overestimates and can never fall below the truth.
/// </para>
/// <para>
/// The <c>d</c> counter columns for an element are derived from a <strong>single</strong>
/// <see cref="IHashProvider{T}"/> call by double hashing (Kirsch–Mitzenmacher): the
/// 32-bit base hash is avalanced into 64 bits whose two halves seed the recurrence
/// <c>g_i = h1 + i·h2</c> (the stride forced odd so the rows spread out), so adding more
/// rows costs arithmetic, not more <see cref="IHashProvider{T}.Hash"/> calls. Because the
/// sketch stores only counters there is no empty-slot sentinel, so unlike the hash-table
/// collections it needs no out-of-band handling for <c>default(T)</c> (a zero <c>int</c>,
/// <see cref="System.Guid.Empty"/>, …): those are hashed and counted like any other
/// element. A <c>null</c> reference is mapped to a fixed base hash so the sketch never
/// invokes the hasher with <c>null</c> (string hashers throw on <c>null</c>), matching the
/// library's out-of-band-<c>null</c> convention.
/// </para>
/// <para>
/// The sketch is add-and-estimate only: like a Bloom filter it has no <c>Remove</c>
/// (decrementing a counter could push an unrelated element's estimate below its true
/// frequency, breaking the never-underestimate guarantee). Use <see cref="Clear"/> to
/// reset, or <see cref="UnionWith"/> to merge two equally-sized sketches — the merge adds
/// counters elementwise, so the result is exactly the sketch that would have arisen from
/// adding both streams to one sketch.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of elements whose frequencies are estimated.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute element hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class CountMinSketch<T, THasher> where THasher : struct, IHashProvider<T>
{
    /// <summary>
    /// The default relative error factor used when a constructor does not specify one:
    /// 0.01 (estimates overshoot by at most ~1% of the total count, with the configured
    /// confidence).
    /// </summary>
    public const double DEFAULT_EPSILON = 0.01;

    /// <summary>
    /// The default failure probability used when a constructor does not specify one:
    /// 0.01 (the <c>epsilon</c> error bound holds with at least 99% confidence).
    /// </summary>
    public const double DEFAULT_DELTA = 0.01;

    private readonly long[] _counters;     // depth * width, row-major
    private readonly int _width;           // w, a power of two
    private readonly int _widthMask;       // w - 1
    private readonly int _depth;           // d
    private readonly double _epsilon;      // target relative error factor
    private readonly double _delta;        // target failure probability
    private readonly THasher _hasher;

    private long _totalCount;

    /// <summary>
    /// Initializes a new, empty instance of the <see cref="CountMinSketch{T,THasher}"/>
    /// class sized for the specified error parameters.
    /// </summary>
    /// <param name="epsilon">
    /// The relative error factor (strictly between 0 and 1). With probability at least
    /// <c>1 − <paramref name="delta"/></c>, <see cref="EstimateCount"/> overestimates an
    /// element's true frequency by no more than <c>epsilon · <see cref="TotalCount"/></c>.
    /// Smaller values widen each row (more counters), lowering the error.
    /// </param>
    /// <param name="delta">
    /// The failure probability (strictly between 0 and 1): the chance that an estimate
    /// exceeds the <paramref name="epsilon"/> error bound. Smaller values add rows,
    /// raising the confidence.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="epsilon"/> or <paramref name="delta"/> is not strictly between 0
    /// and 1 (or is <see cref="double.NaN"/>).
    /// </exception>
    public CountMinSketch(double epsilon = DEFAULT_EPSILON, double delta = DEFAULT_DELTA)
    {
        if (epsilon <= 0d || epsilon >= 1d || double.IsNaN(epsilon))
            throw new ArgumentOutOfRangeException(nameof(epsilon), epsilon, "Epsilon must be between 0 (exclusive) and 1 (exclusive).");
        if (delta <= 0d || delta >= 1d || double.IsNaN(delta))
            throw new ArgumentOutOfRangeException(nameof(delta), delta, "Delta must be between 0 (exclusive) and 1 (exclusive).");

        _epsilon = epsilon;
        _delta = delta;

        // Width w = ceil(e / epsilon), rounded up to a power of two so a column index is a
        // mask rather than a modulo (the extra columns only lower the realized error). The
        // cap mirrors the Bloom filter: clamp before the int cast so a tiny epsilon cannot
        // overflow. Because epsilon < 1, e / epsilon > e, so w is always at least 4.
        _width = FastUtils.NextPowerOfTwo((int)Math.Min(Math.Ceiling(Math.E / epsilon), 1 << 30));
        _widthMask = _width - 1;

        // Depth d = ceil(ln(1 / delta)), at least one row. Math.Max keeps it >= 1 even when
        // delta is so close to 1 that 1/delta rounds to exactly 1.0 (ln 1 = 0).
        _depth = Math.Max(1, (int)Math.Ceiling(Math.Log(1.0 / delta)));

        _counters = new long[_depth * _width];
        _hasher = default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CountMinSketch{T,THasher}"/> class
    /// sized for the specified error parameters and pre-populated by adding each element
    /// of <paramref name="source"/> once.
    /// </summary>
    /// <param name="source">
    /// The elements to add. Each occurrence increments the corresponding element's
    /// frequency by one, so duplicates in the source raise the estimated count.
    /// </param>
    /// <param name="epsilon">The relative error factor; see the primary constructor.</param>
    /// <param name="delta">The failure probability; see the primary constructor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="epsilon"/> or <paramref name="delta"/> is not strictly between 0
    /// and 1.
    /// </exception>
    public CountMinSketch(IEnumerable<T> source, double epsilon = DEFAULT_EPSILON, double delta = DEFAULT_DELTA)
        : this(NullCheckedEpsilon(source, epsilon), delta)
    {
        foreach (T item in source)
            Add(item);
    }

    // Runs as part of the chained-ctor argument expression so the null check beats the
    // primary ctor's epsilon/delta validation: a null source must surface as
    // ArgumentNullException, not ArgumentOutOfRangeException, even when the caller also
    // passed out-of-range error parameters.
    private static double NullCheckedEpsilon(IEnumerable<T> source, double epsilon)
    {
        ArgumentNullException.ThrowIfNull(source);
        return epsilon;
    }

    /// <summary>
    /// Gets the number of counters per row (<c>w</c>), always a power of two.
    /// </summary>
    public int Width => _width;

    /// <summary>
    /// Gets the number of rows (<c>d</c>) — the number of independent counters each
    /// element maps to, and the number of estimates minimized over.
    /// </summary>
    public int Depth => _depth;

    /// <summary>
    /// Gets the relative error factor the sketch was sized for (the <c>epsilon</c>
    /// constructor argument).
    /// </summary>
    public double Epsilon => _epsilon;

    /// <summary>
    /// Gets the failure probability the sketch was sized for (the <c>delta</c>
    /// constructor argument).
    /// </summary>
    public double Delta => _delta;

    /// <summary>
    /// Gets the total of all counts added since construction or the last
    /// <see cref="Clear"/> — the <c>L1</c> norm the <see cref="Epsilon"/> error bound is
    /// relative to.
    /// </summary>
    public long TotalCount => _totalCount;

    /// <summary>
    /// Adds one occurrence of an element, increasing its estimated frequency by one.
    /// </summary>
    /// <param name="item">The element to count.</param>
    public void Add(T item) => Add(item, 1);

    /// <summary>
    /// Adds <paramref name="count"/> occurrences of an element, increasing its estimated
    /// frequency by that amount.
    /// </summary>
    /// <param name="item">The element to count.</param>
    /// <param name="count">The number of occurrences to add. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not positive.</exception>
    /// <remarks>
    /// A counter (and <see cref="TotalCount"/>) that would exceed <see cref="long.MaxValue"/>
    /// saturates at <see cref="long.MaxValue"/> rather than wrapping to a negative value, so
    /// the never-underestimate guarantee holds even under counts no in-memory sketch could
    /// otherwise represent.
    /// </remarks>
    public void Add(T item, long count)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be positive.");

        ComputeHashes(item, out uint h1, out uint h2);

        long[] counters = _counters;
        int mask = _widthMask;
        int width = _width;
        uint combined = h1;
        for (int row = 0; row < _depth; row++)
        {
            int column = (int)(combined & (uint)mask);
            ref long counter = ref counters[row * width + column];
            counter = SaturatingAdd(counter, count);
            combined += h2;
        }

        _totalCount = SaturatingAdd(_totalCount, count);
    }

    /// <summary>
    /// Estimates how many times an element has been added.
    /// </summary>
    /// <param name="item">The element to query.</param>
    /// <returns>
    /// An estimate of <paramref name="item"/>'s frequency. The estimate is never less than
    /// the true count (no underestimates), and with probability at least
    /// <c>1 − <see cref="Delta"/></c> exceeds it by no more than
    /// <c>Epsilon · <see cref="TotalCount"/></c>. An element never added returns <c>0</c>
    /// unless collisions inflate it.
    /// </returns>
    public long EstimateCount(T item)
    {
        ComputeHashes(item, out uint h1, out uint h2);

        long[] counters = _counters;
        int mask = _widthMask;
        int width = _width;
        uint combined = h1;
        long min = long.MaxValue;
        for (int row = 0; row < _depth; row++)
        {
            int column = (int)(combined & (uint)mask);
            long value = counters[row * width + column];
            if (value < min)
                min = value;
            combined += h2;
        }

        return min;
    }

    /// <summary>
    /// Resets the sketch to empty, clearing every counter. The grid dimensions are
    /// preserved.
    /// </summary>
    public void Clear()
    {
        if (_totalCount == 0)
            return;

        Array.Clear(_counters, 0, _counters.Length);
        _totalCount = 0;
    }

    /// <summary>
    /// Merges another sketch into this one in place, so this sketch afterwards estimates
    /// frequencies over the combined input streams. Both sketches must have been
    /// constructed with identical parameters (same <see cref="Width"/> and
    /// <see cref="Depth"/>).
    /// </summary>
    /// <param name="other">The sketch to merge in. Left unmodified.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="other"/> has a different width or depth, so the two counter grids
    /// are not compatible.
    /// </exception>
    /// <remarks>
    /// The merge adds counters elementwise, which is exactly the counter state adding both
    /// streams to a single sketch would have produced — so the merged estimates have no
    /// error beyond a single sketch over the combined stream.
    /// </remarks>
    public void UnionWith(CountMinSketch<T, THasher> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other._width != _width || other._depth != _depth)
            throw new ArgumentException("The two sketches must have the same width and depth to be merged.", nameof(other));

        long[] counters = _counters;
        long[] otherCounters = other._counters;
        for (int i = 0; i < counters.Length; i++)
            counters[i] = SaturatingAdd(counters[i], otherCounters[i]);

        _totalCount = SaturatingAdd(_totalCount, other._totalCount);
    }

    // Derives two independent 32-bit hash lanes from a single IHashProvider call by
    // avalanching the 32-bit base hash into 64 bits (the SplitMix64 finalizer) and
    // splitting the result. A null reference is mapped to a fixed base hash so the hasher
    // (which may throw on null, e.g. the string hashers) is never invoked with null —
    // value-type defaults (0, Guid.Empty) are valid inputs and go through the hasher
    // normally. The typeof(T).IsValueType guard is a JIT-time constant, so the null check
    // is compiled away entirely for value-type instantiations (no boxing).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ComputeHashes(T item, out uint h1, out uint h2)
    {
        int baseHash = (!typeof(T).IsValueType && item is null) ? 0 : _hasher.Hash(item);
        ulong mixed = Mix64((uint)baseHash);
        h1 = (uint)mixed;
        h2 = (uint)(mixed >> 32);

        // h2 is the stride of the g_i = h1 + i·h2 recurrence; a zero stride would map every
        // row to the same column. Force it odd (and non-zero) so the d rows spread out.
        h2 |= 1u;
    }

    // Adds two non-negative longs, clamping to long.MaxValue instead of wrapping past it.
    // Counters and _totalCount are only ever increased by validated-positive counts (and by
    // each other in UnionWith), so both operands are always >= 0 — which means a sum that
    // comes back negative can only be an overflow of long.MaxValue. Clamping there keeps the
    // estimate at or above the true frequency: an unchecked wrap to a negative value would
    // make EstimateCount return less than the truth, breaking the never-underestimate
    // guarantee. A counter saturated at long.MaxValue is a count no in-memory sketch can
    // represent anyway, so the clamp loses no recoverable information.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long SaturatingAdd(long a, long b)
    {
        long sum = unchecked(a + b);
        return sum < 0 ? long.MaxValue : sum;
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
