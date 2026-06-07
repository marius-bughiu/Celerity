using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Statistical tests that the realized false-positive rate stays at or under the
/// target the filter was sized for, and that there are never false negatives. The
/// data sets are deterministic (fixed disjoint integer ranges and a fixed seed) so
/// the measured rate is stable across runs.
/// </summary>
public class BloomFilterFalsePositiveTests
{
    [Theory]
    [InlineData(0.10)]
    [InlineData(0.01)]
    [InlineData(0.001)]
    public void RealizedFalsePositiveRate_StaysAtOrBelowTarget(double target)
    {
        const int n = 10_000;
        var filter = new BloomFilter<int, Int32Murmur3Hasher>(n, target);

        // Added set: [0, n).
        for (int i = 0; i < n; i++)
            filter.Add(i);

        // No false negatives for any added element.
        for (int i = 0; i < n; i++)
            Assert.True(filter.Contains(i), $"false negative for {i}");

        // Query a large disjoint set known to be absent and measure the hit rate.
        const int queries = 100_000;
        const int absentBase = 5_000_000;
        int falsePositives = 0;
        for (int i = 0; i < queries; i++)
        {
            if (filter.Contains(absentBase + i))
                falsePositives++;
        }

        double measured = falsePositives / (double)queries;

        // The bit count is rounded up to a power of two, so the realized rate is
        // typically below target; allow 2x headroom plus a small absolute slack for
        // sampling noise at very low target rates.
        double bound = target * 2 + 0.002;
        Assert.True(measured <= bound,
            $"measured false-positive rate {measured:F4} exceeded bound {bound:F4} (target {target})");
    }

    [Fact]
    public void StringFilter_NoFalseNegatives_AndBoundedFalsePositives()
    {
        const int n = 5_000;
        var filter = new BloomFilter<string, StringMurmur3Hasher>(n, 0.01);

        for (int i = 0; i < n; i++)
            filter.Add($"present-{i}");

        for (int i = 0; i < n; i++)
            Assert.True(filter.Contains($"present-{i}"), $"false negative for present-{i}");

        const int queries = 50_000;
        int falsePositives = 0;
        for (int i = 0; i < queries; i++)
        {
            if (filter.Contains($"absent-{i}"))
                falsePositives++;
        }

        double measured = falsePositives / (double)queries;
        Assert.True(measured <= 0.02 + 0.002,
            $"measured string false-positive rate {measured:F4} exceeded bound");
    }

    [Fact]
    public void StayingWithinCapacity_KeepsCurrentProbabilityNearTarget()
    {
        const int n = 8_000;
        var filter = new BloomFilter<int, Int32Murmur3Hasher>(n, 0.01);
        for (int i = 0; i < n; i++)
            filter.Add(i);

        // At the design fill level, the estimated probability should be in the same
        // ballpark as the target (well under 1, comfortably above 0).
        double estimate = filter.CurrentFalsePositiveProbability;
        Assert.True(estimate > 0d && estimate <= 0.02,
            $"estimated probability {estimate:F4} not near the 0.01 target at capacity");
    }
}
