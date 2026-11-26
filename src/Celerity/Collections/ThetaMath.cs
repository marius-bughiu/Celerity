using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// The estimator and confidence-interval arithmetic shared by
/// <see cref="ThetaSketch{T,THasher}"/> and <see cref="CompactThetaSketch{T,THasher}"/>.
/// </summary>
/// <remarks>
/// A sketch is a retained count <c>n</c> and an inclusive threshold <c>T</c> on hash values
/// drawn uniformly from <c>[1, 2^63]</c>, so each distinct element is retained with
/// probability <c>p = T / 2^63</c> and <c>n</c> is binomial in the true cardinality
/// <c>N</c> with that rate. The estimate is <c>n / p</c>, which is unbiased.
/// </remarks>
internal static class ThetaMath
{
    // The threshold of a sketch that has not started sampling: every value in [1, 2^63] is at
    // or below it, so p = 1.
    internal const ulong FullThreshold = 1UL << 63;

    // 2^-63: exact in a double, so FullThreshold maps to a sampling rate of exactly 1.
    private const double InverseFullThreshold = 1d / 9223372036854775808d;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double ToTheta(ulong threshold) => threshold * InverseFullThreshold;

    internal static double Estimate(int retained, ulong threshold) => retained / ToTheta(threshold);

    internal static double LowerBound(int retained, ulong threshold, int standardDeviations)
    {
        (double centre, double spread, double p) = Interval(retained, threshold, standardDeviations);

        // The retained values are distinct elements of the set, so the set has at least that
        // many; the binomial bound can dip a hair below when p is close to 1.
        return Math.Max(retained, (centre - spread) / p);
    }

    internal static double UpperBound(int retained, ulong threshold, int standardDeviations)
    {
        (double centre, double spread, double p) = Interval(retained, threshold, standardDeviations);
        return (centre + spread) / p;
    }

    // Inverts the normal approximation to the binomial (the Wilson score interval) for the
    // expected retained count λ = N·p: the λ that put the observed n exactly z standard
    // deviations away solve (n − λ)² = z²·λ·(1 − p), giving
    // λ = n + z²q/2 ± z·sqrt(n·q + z²q²/4) with q = 1 − p. Unlike n/p ± z·sd it stays honest
    // when n is small, including an empty intersection (n = 0), whose upper bound is
    // z²q/p rather than zero. In exact mode q = 0 and both bounds collapse to n.
    private static (double Centre, double Spread, double P) Interval(int retained, ulong threshold, int standardDeviations)
    {
        if (standardDeviations is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(standardDeviations), standardDeviations,
                "The number of standard deviations must be 1, 2 or 3.");

        double p = ToTheta(threshold);
        double q = 1d - p;
        double z2q = (double)standardDeviations * standardDeviations * q;
        double centre = retained + z2q / 2d;
        double spread = standardDeviations * Math.Sqrt(retained * q + z2q * q / 4d);
        return (centre, spread, p);
    }

    // SplitMix64 finalizer seeded with the 32-bit base hash widened by the golden-ratio
    // increment, so even a zero base hash avalanches to a well-distributed 64-bit value. The
    // same function HyperLogLog and MinHashSketch widen a 32-bit hasher with.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong Mix64(uint x)
    {
        ulong z = x + 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
