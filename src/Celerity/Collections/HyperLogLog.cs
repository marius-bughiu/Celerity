using System.Numerics;
using System.Runtime.CompilerServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A space-efficient probabilistic cardinality estimator — it counts the number of
/// <em>distinct</em> elements added to it — parameterized on a custom
/// <see cref="IHashProvider{T}"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// HyperLogLog answers "roughly how many distinct elements have I seen?" in a fixed,
/// tiny amount of memory that does <strong>not</strong> grow with the number of
/// elements. Where a <see cref="HashSet{T}"/> must store every distinct element to
/// count them — memory proportional to the cardinality — a HyperLogLog stores only an
/// array of <c>m = 2^precision</c> small byte registers (a few kilobytes), trading
/// exactness for a bounded <em>relative</em> error of roughly
/// <c>1.04 / sqrt(m)</c>. At the default precision of 14 that is about 0.8% error from
/// 16&#160;KB of registers, whether you count a thousand elements or a billion.
/// </para>
/// <para>
/// The algorithm hashes each element to 64 bits, routes it to one of the <c>m</c>
/// registers by its top <c>precision</c> bits, and records in that register the
/// largest "rank" — one plus the number of leading zeros — seen in the remaining bits.
/// A stream of <c>n</c> distinct elements fills the registers with a predictable
/// pattern of ranks, and the harmonic mean of <c>2^register</c> across all registers
/// recovers an estimate of <c>n</c> (Flajolet et&#160;al., 2007). The estimate applies
/// the standard small-range <em>linear counting</em> correction so that low
/// cardinalities — where many registers are still zero — are estimated accurately
/// rather than by the bias-prone raw formula. No large-range correction is needed
/// because the 64-bit hash space is far larger than any realistic cardinality.
/// </para>
/// <para>
/// The 64-bit hash is derived from a <strong>single</strong>
/// <see cref="IHashProvider{T}"/> call by avalanching the 32-bit base hash with the
/// SplitMix64 finalizer, so any existing hasher plugs in unchanged and adding an
/// element costs one <see cref="IHashProvider{T}.Hash"/> call. Because the estimator
/// stores only register ranks there is no empty-slot sentinel, so unlike the
/// hash-table collections it needs no out-of-band handling for <c>default(T)</c> (a
/// zero <c>int</c>, <see cref="System.Guid.Empty"/>, …): those are hashed and counted
/// like any other element. A <c>null</c> reference is mapped to a fixed base hash so
/// the estimator never invokes the hasher with <c>null</c> (string hashers throw on
/// <c>null</c>), matching the library's out-of-band-<c>null</c> convention.
/// </para>
/// <para>
/// HyperLogLog is add-and-estimate only: like a Bloom filter it cannot remove an
/// element or report whether a specific element was seen — it tracks only the
/// aggregate distinct count. Two estimators built with the same precision can be
/// merged with <see cref="UnionWith"/> to estimate the cardinality of the union of
/// their input streams.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of elements whose distinct count is estimated.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute element hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class HyperLogLog<T, THasher> where THasher : struct, IHashProvider<T>
{
    /// <summary>
    /// The default register-index precision used when a constructor does not specify
    /// one: 14, giving <c>m = 16384</c> registers (16&#160;KB) and a relative standard
    /// error of about 0.81%.
    /// </summary>
    public const int DEFAULT_PRECISION = 14;

    /// <summary>The minimum supported precision (<c>m = 16</c> registers).</summary>
    public const int MIN_PRECISION = 4;

    /// <summary>The maximum supported precision (<c>m = 65536</c> registers).</summary>
    public const int MAX_PRECISION = 16;

    private readonly byte[] _registers;
    private readonly int _precision;       // p
    private readonly int _registerCount;   // m = 2^p
    private readonly THasher _hasher;

    /// <summary>
    /// Initializes a new, empty instance of the <see cref="HyperLogLog{T,THasher}"/>
    /// class with the specified register-index precision.
    /// </summary>
    /// <param name="precision">
    /// The number of bits of each element's hash used to select a register. The
    /// estimator allocates <c>2^precision</c> one-byte registers, so larger values cost
    /// more memory but lower the relative standard error (≈ <c>1.04 / sqrt(2^precision)</c>).
    /// Must be between <see cref="MIN_PRECISION"/> and <see cref="MAX_PRECISION"/>
    /// inclusive.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="precision"/> is outside the inclusive range
    /// [<see cref="MIN_PRECISION"/>, <see cref="MAX_PRECISION"/>].
    /// </exception>
    public HyperLogLog(int precision = DEFAULT_PRECISION)
    {
        if (precision < MIN_PRECISION || precision > MAX_PRECISION)
            throw new ArgumentOutOfRangeException(nameof(precision), precision,
                $"Precision must be between {MIN_PRECISION} and {MAX_PRECISION} inclusive.");

        _precision = precision;
        _registerCount = 1 << precision;
        _registers = new byte[_registerCount];
        _hasher = default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HyperLogLog{T,THasher}"/> class
    /// with the specified precision and pre-populated with the distinct elements of
    /// <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The elements to add. Duplicates affect the estimate only
    /// as far as sampling noise; the estimator counts distinct values.</param>
    /// <param name="precision">The register-index precision; see the primary constructor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="precision"/> is outside the inclusive range
    /// [<see cref="MIN_PRECISION"/>, <see cref="MAX_PRECISION"/>].
    /// </exception>
    public HyperLogLog(IEnumerable<T> source, int precision = DEFAULT_PRECISION)
        : this(NullCheckedPrecision(source, precision))
    {
        foreach (T item in source)
            Add(item);
    }

    // Runs as part of the chained-ctor argument expression so the null check beats the
    // primary ctor's precision validation: a null source must surface as
    // ArgumentNullException, not ArgumentOutOfRangeException, even when the caller also
    // passed an out-of-range precision.
    private static int NullCheckedPrecision(IEnumerable<T> source, int precision)
    {
        ArgumentNullException.ThrowIfNull(source);
        return precision;
    }

    /// <summary>
    /// Gets the register-index precision (<c>p</c>) the estimator was constructed with.
    /// </summary>
    public int Precision => _precision;

    /// <summary>
    /// Gets the number of registers (<c>m = 2^precision</c>).
    /// </summary>
    public int RegisterCount => _registerCount;

    /// <summary>
    /// Gets the relative standard error of the estimate, ≈ <c>1.04 / sqrt(m)</c>. The
    /// realized error of <see cref="EstimateCardinality"/> stays near this for
    /// cardinalities above the small-range regime.
    /// </summary>
    public double StandardError => 1.04 / Math.Sqrt(_registerCount);

    /// <summary>
    /// Adds an element to the estimator, updating the register its hash routes to.
    /// </summary>
    /// <param name="item">The element to add. Adding an element already represented is
    /// a no-op for the estimate (a register only ever increases).</param>
    public void Add(T item)
    {
        ulong hash = Hash64(item);

        // Top `precision` bits select the register; the remaining bits supply the rank.
        int register = (int)(hash >> (64 - _precision));

        // Shift the register bits out, then plant a sentinel bit just below the
        // remaining bit field so LeadingZeroCount never exceeds (64 - precision): the
        // maximum rank is (64 - precision) + 1 even when every remaining bit is zero.
        ulong remaining = (hash << _precision) | (1UL << (_precision - 1));
        byte rank = (byte)(BitOperations.LeadingZeroCount(remaining) + 1);

        if (rank > _registers[register])
            _registers[register] = rank;
    }

    /// <summary>
    /// Estimates the number of distinct elements added since construction or the last
    /// <see cref="Clear"/>.
    /// </summary>
    /// <returns>
    /// The estimated cardinality, rounded to the nearest whole number. Subject to a
    /// relative standard error of about <see cref="StandardError"/>; an empty estimator
    /// returns <c>0</c> exactly.
    /// </returns>
    /// <remarks>
    /// This is an <c>O(m)</c> pass over the registers, where <c>m</c> is
    /// <see cref="RegisterCount"/>. The raw harmonic-mean estimate is replaced by
    /// <em>linear counting</em> in the small-cardinality regime (when the raw estimate
    /// is small and some registers are still zero), which is markedly more accurate
    /// there.
    /// </remarks>
    public long EstimateCardinality()
    {
        byte[] registers = _registers;
        int m = _registerCount;

        double sum = 0d;
        int zeroRegisters = 0;
        for (int i = 0; i < m; i++)
        {
            byte r = registers[i];
            sum += 1.0 / (1UL << r); // 2^-r; r <= 64 - precision + 1 < 64
            if (r == 0)
                zeroRegisters++;
        }

        double estimate = Alpha(m) * m * m / sum;

        // Small-range correction: when the raw estimate is small and registers remain
        // empty, linear counting on the empty-register fraction is far more accurate.
        if (estimate <= 2.5 * m && zeroRegisters != 0)
            estimate = m * Math.Log((double)m / zeroRegisters);

        return (long)Math.Round(estimate);
    }

    /// <summary>
    /// Merges another estimator into this one in place, so this estimator afterwards
    /// estimates the cardinality of the <em>union</em> of both input streams. Both
    /// estimators must have been constructed with the same <see cref="Precision"/>.
    /// </summary>
    /// <param name="other">The estimator to merge in. Left unmodified.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="other"/> has a different precision, so the two register arrays
    /// are not compatible.
    /// </exception>
    /// <remarks>
    /// The merge takes the per-register maximum, which is exactly the register state the
    /// combined stream would have produced — so unlike a Bloom-filter union the result
    /// has no extra error beyond the usual HyperLogLog estimate of the merged set.
    /// </remarks>
    public void UnionWith(HyperLogLog<T, THasher> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other._precision != _precision)
            throw new ArgumentException("The two estimators must have the same precision to be merged.", nameof(other));

        byte[] registers = _registers;
        byte[] otherRegisters = other._registers;
        for (int i = 0; i < registers.Length; i++)
        {
            if (otherRegisters[i] > registers[i])
                registers[i] = otherRegisters[i];
        }
    }

    /// <summary>
    /// Resets the estimator to empty, clearing every register. The precision and
    /// register count are preserved.
    /// </summary>
    public void Clear() => Array.Clear(_registers, 0, _registers.Length);

    // The bias-correction constant alpha_m for the harmonic-mean estimate. The three
    // small-m cases use the tabulated constants from the original paper; for m >= 128
    // the closed-form approximation applies.
    private static double Alpha(int m) => m switch
    {
        16 => 0.673,
        32 => 0.697,
        64 => 0.709,
        _ => 0.7213 / (1.0 + 1.079 / m),
    };

    // Derives a full 64-bit hash from a single IHashProvider call by avalanching the
    // 32-bit base hash with the SplitMix64 finalizer. The finalizer is a bijection on
    // 64 bits, so distinct base hashes stay distinct — the estimator sees no collisions
    // beyond the hasher's own. A null reference is mapped to a fixed base hash so the
    // hasher (which may throw on null, e.g. the string hashers) is never invoked with
    // null; value-type defaults (0, Guid.Empty) are valid inputs and go through the
    // hasher normally. The typeof(T).IsValueType guard is a JIT-time constant, so the
    // null check is compiled away entirely for value-type instantiations (no boxing).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ulong Hash64(T item)
    {
        int baseHash = (!typeof(T).IsValueType && item is null) ? 0 : _hasher.Hash(item);
        ulong z = (uint)baseHash + 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
