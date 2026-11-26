using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// The estimator and confidence-interval arithmetic shared by
/// <see cref="ThetaSketch{T,THasher}"/> and <see cref="CompactThetaSketch{T,THasher}"/>.
/// </summary>
/// <remarks>
/// A sketch is a retained count <c>n</c> and an inclusive threshold <c>T</c> on hash values
/// drawn uniformly from <c>[1, 2^63]</c>, with <c>p = T / 2^63</c>. The estimate is
/// <c>n / p</c>, unbiased by the K-Minimum-Values / theta-sketch result even though <c>T</c>
/// is chosen from the data. The confidence bounds model <c>n</c> as binomial in the true
/// cardinality <c>N</c> at rate <c>p</c>, the standard approximation for these sketches: exact
/// for a fixed threshold, and close for one set by an order statistic.
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

    // Below this many retained values the bounds come from exact Poisson tails; above it the
    // normal approximation is accurate and the Wilson form is used.
    private const int ExactTailLimit = 120;

    // One-sided tail probabilities of the standard normal at 1, 2 and 3 standard deviations,
    // indexed by the number of standard deviations.
    private static readonly double[] OneSidedTail = [0d, 0.15865525393145707, 0.022750131948179209, 0.0013498980316301035];

    internal static double LowerBound(int retained, ulong threshold, int standardDeviations)
    {
        ValidateStandardDeviations(standardDeviations);
        if (threshold == FullThreshold)
            return retained;

        double p = ToTheta(threshold);
        double lambda = retained <= ExactTailLimit
            ? PoissonLowerMean(retained, OneSidedTail[standardDeviations])
            : Wilson(retained, p, standardDeviations, -1d);

        // The retained values are distinct elements of the set, so the set has at least that
        // many; a bound on the mean can dip a hair below when p is close to 1.
        return Math.Max(retained, lambda / p);
    }

    internal static double UpperBound(int retained, ulong threshold, int standardDeviations)
    {
        ValidateStandardDeviations(standardDeviations);
        if (threshold == FullThreshold)
            return retained;

        double p = ToTheta(threshold);
        double lambda = retained <= ExactTailLimit
            ? PoissonUpperMean(retained, OneSidedTail[standardDeviations])
            : Wilson(retained, p, standardDeviations, 1d);
        return lambda / p;
    }

    private static void ValidateStandardDeviations(int standardDeviations)
    {
        if (standardDeviations is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(standardDeviations), standardDeviations,
                "The number of standard deviations must be 1, 2 or 3.");
    }

    // Inverts the normal approximation to the binomial (the Wilson score interval) for the
    // expected retained count λ = N·p: the λ that put the observed n exactly z standard
    // deviations away solve (n − λ)² = z²·λ·(1 − p), giving
    // λ = n + z²q/2 ± z·sqrt(n·q + z²q²/4) with q = 1 − p. Used only above ExactTailLimit
    // retained values, where the normal approximation holds.
    private static double Wilson(int retained, double p, int z, double sign)
    {
        double q = 1d - p;
        double z2q = (double)z * z * q;
        return retained + z2q / 2d + sign * z * Math.Sqrt(retained * q + z2q * q / 4d);
    }

    // Exact one-sided bounds on the mean of the retained count for small counts, where the
    // normal approximation badly overstates coverage: with a true count of 100 sampled at
    // θ = 0.001 (λ = 0.1), the Wilson interval excludes the truth whenever one value is retained,
    // covering 90% instead of 95%. The retained count is binomial with mean λ = N·p; its
    // Poisson limit has a variance λ at least the binomial's λ·(1 − p), so these bounds are
    // exact as p → 0 and conservative (wider) as p grows.
    //
    // Upper: the λ at which observing n or fewer has probability α. Lower: the λ at which
    // observing n or more has probability α, which is 0 when n = 0.
    private static double PoissonUpperMean(int n, double alpha) =>
        SolvePoissonMean(n, alpha, n + 10d * Math.Sqrt(n + 1d) + 20d);

    private static double PoissonLowerMean(int n, double alpha) =>
        n == 0 ? 0d : SolvePoissonMean(n - 1, 1d - alpha, n);

    // Bisects for the λ in [0, high] where P(X ≤ m) = target for X ~ Poisson(λ). The CDF falls
    // monotonically as λ grows, so the bracket shrinks toward the unique root.
    private static double SolvePoissonMean(int m, double target, double high)
    {
        double low = 0d;
        for (int iteration = 0; iteration < 100; iteration++)
        {
            double mid = (low + high) / 2d;
            if (PoissonCdf(m, mid) > target)
                low = mid;
            else
                high = mid;
        }

        return (low + high) / 2d;
    }

    private static double PoissonCdf(int m, double lambda)
    {
        double term = Math.Exp(-lambda);
        double sum = term;
        for (int i = 1; i <= m; i++)
        {
            term *= lambda / i;
            sum += term;
        }

        return sum;
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
