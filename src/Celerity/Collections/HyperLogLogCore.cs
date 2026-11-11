using System.Numerics;
using System.Runtime.CompilerServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// The register arithmetic shared by <see cref="HyperLogLog{T, THasher}"/> and
/// <see cref="SlidingHyperLogLog{T, THasher}"/>: how a 64-bit hash picks a register and a rank,
/// and how a set of per-register ranks turns into a cardinality estimate.
/// </summary>
/// <remarks>
/// Both sketches route every element through exactly these functions, which is what makes a
/// sliding sketch's all-time estimate bit-identical to a plain <see cref="HyperLogLog{T, THasher}"/>
/// fed the same elements, and its windowed estimate bit-identical to one fed only the in-window
/// elements. Keep the floating-point evaluation order in <see cref="Estimate"/> unchanged: the
/// callers accumulate <c>sum</c> in register order and rely on getting the same bits back.
/// </remarks>
internal static class HyperLogLogCore
{
    // The size of the hash space reachable through a 32-bit IHashProvider<T>, and the
    // threshold above which the classical large-range correction is worth applying
    // (Flajolet et al. 2007 use 2^32 / 30). Both are used only on the 32-bit path.
    private const double TwoPow32 = 4294967296d;
    private const double LargeRangeThreshold = TwoPow32 / 30d;

    /// <summary>The register an element with this hash updates: its top <paramref name="precision"/> bits.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int RegisterOf(ulong hash, int precision) => (int)(hash >> (64 - precision));

    /// <summary>
    /// The rank an element with this hash records: one plus the number of leading zeros in the bits
    /// left after the register index. Always in <c>[1, 65 - precision]</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int RankOf(ulong hash, int precision)
    {
        // Shift the register bits out, then plant a sentinel bit just below the
        // remaining bit field so LeadingZeroCount never exceeds (64 - precision): the
        // maximum rank is (64 - precision) + 1 even when every remaining bit is zero.
        ulong remaining = (hash << precision) | (1UL << (precision - 1));
        return BitOperations.LeadingZeroCount(remaining) + 1;
    }

    /// <summary>
    /// Turns the harmonic sum <c>Σ 2^-register</c> over all <paramref name="registerCount"/> registers,
    /// and the number of registers still at zero, into a rounded cardinality estimate.
    /// </summary>
    /// <param name="sum">The sum of <c>2^-r</c> over every register value <c>r</c>, accumulated in register order.</param>
    /// <param name="zeroRegisters">How many registers hold zero.</param>
    /// <param name="registerCount">The register count <c>m</c>.</param>
    /// <param name="native64">Whether the hasher supplied a genuine 64-bit hash (no large-range correction).</param>
    internal static long Estimate(double sum, int zeroRegisters, int registerCount, bool native64)
    {
        int m = registerCount;
        double estimate = Alpha(m) * m * m / sum;

        // Small-range correction: when the raw estimate is small and registers remain
        // empty, linear counting on the empty-register fraction is far more accurate.
        if (estimate <= 2.5 * m && zeroRegisters != 0)
        {
            estimate = m * Math.Log((double)m / zeroRegisters);
        }
        else if (!native64 && estimate > LargeRangeThreshold)
        {
            // Large-range correction. A 32-bit hasher reaches only 2^32 distinct hashes,
            // so what the registers actually measure is the number of distinct *hashes*,
            // which saturates as the cardinality approaches 2^32: n distinct elements
            // produce only 2^32 · (1 − e^(−n/2^32)) distinct hashes. Inverting that
            // recovers n. Callers pass a JIT-time constant, so an estimator built on an
            // IHashProvider64<T> hasher compiles this branch away entirely.
            double saturatedFraction = estimate / TwoPow32;

            // At or past full saturation the inverse has no finite value — every hash has
            // been seen, and the true cardinality is unbounded from the registers alone.
            // Leaving the raw estimate is the only honest answer there.
            //
            // ln(1 - x) is evaluated as LogP1(-x) rather than Log(1 - x): forming `1 - x`
            // first cancels away the low bits of x as x approaches 1, exactly the
            // near-saturation regime where this correction diverges fastest and is most
            // sensitive to them. LogP1 takes -x directly and keeps them.
            if (saturatedFraction < 1d)
                estimate = -TwoPow32 * double.LogP1(-saturatedFraction);
        }

        return (long)Math.Round(estimate);
    }

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

    // SplitMix64 finalizer seeded with the 32-bit base hash widened by the golden-ratio
    // increment, so even a zero base hash avalanches to a well-distributed 64-bit value.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong Mix64(uint x)
    {
        ulong z = x + 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}

/// <summary>
/// Produces the 64-bit hash a HyperLogLog-family sketch derives its register and rank from.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <typeparam name="THasher">The sketch's hasher.</typeparam>
internal static class HyperLogLogHash<T, THasher> where THasher : struct, IHashProvider<T>
{
    // When THasher implements IHashProvider64<T> the hasher's own 64-bit result is used
    // as-is: the interface contract is that it is already avalanched, and re-mixing it
    // would only cost multiplies. Otherwise the 32-bit code is avalanched with the
    // SplitMix64 finalizer, a bijection on 64 bits, so distinct base hashes stay distinct
    // — but the reachable space is still 2^32, which is what the estimator's large-range
    // correction compensates for.
    //
    // A null reference is mapped to a fixed hash so the hasher (which may throw on null,
    // e.g. the string hashers) is never invoked with null; value-type defaults (0,
    // Guid.Empty) are valid inputs and go through the hasher normally. Both the
    // typeof(T).IsValueType guard and the IsNative64 test are JIT-time constants, so each
    // instantiation compiles down to a single straight-line path (and no per-call boxing: the
    // 64-bit arm reuses a single interface reference boxed once per instantiation).
    //
    // The 32-bit arm hashes through default(THasher), which is what every sketch's own
    // `_hasher = default` field held; Hash64Source documents why that equivalence is pinned.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong Hash64(T item)
    {
        if (!typeof(T).IsValueType && item is null)
            return HyperLogLogCore.Mix64(0);

        if (Hash64Source<T, THasher>.IsNative64)
            return Hash64Source<T, THasher>.Hash64(item);

        return HyperLogLogCore.Mix64((uint)default(THasher).Hash(item));
    }
}
