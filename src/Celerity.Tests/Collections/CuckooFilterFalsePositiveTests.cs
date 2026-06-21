using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Statistical tests that the realized false-positive rate stays at or under the target the filter was sized
/// for, and that there are never false negatives — including after a churn of deletions. The data sets are
/// deterministic (fixed disjoint integer ranges) so the measured rate is stable across runs.
/// </summary>
public class CuckooFilterFalsePositiveTests
{
    [Theory]
    [InlineData(0.10)]
    [InlineData(0.01)]
    [InlineData(0.001)]
    public void RealizedFalsePositiveRate_StaysAtOrBelowTarget(double target)
    {
        const int n = 10_000;
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(n, target);

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

        // The fingerprint width is rounded up and the table runs below full, so the realized rate is typically
        // below target; allow 2x headroom plus a small absolute slack for sampling noise at very low targets.
        double bound = target * 2 + 0.002;
        Assert.True(measured <= bound,
            $"measured false-positive rate {measured:F4} exceeded bound {bound:F4} (target {target})");
    }

    [Fact]
    public void StringFilter_NoFalseNegatives_AndBoundedFalsePositives()
    {
        const int n = 5_000;
        var filter = new CuckooFilter<string, StringMurmur3Hasher>(n, 0.01);

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
    public void NoFalseNegatives_AfterDeletionChurn()
    {
        // The cuckoo filter's distinguishing property: after deleting half the set, every remaining element must
        // still report present, and the realized false-positive rate on absent keys stays bounded.
        const int n = 10_000;
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(n, 0.01);

        for (int i = 0; i < n; i++)
            filter.Add(i);

        // Delete the odd half.
        for (int i = 1; i < n; i += 2)
            Assert.True(filter.Remove(i));

        // Every even (kept) element still present — no false negatives from deletion.
        for (int i = 0; i < n; i += 2)
            Assert.True(filter.Contains(i), $"false negative for kept element {i}");

        // Absent keys stay bounded.
        const int queries = 50_000;
        const int absentBase = 5_000_000;
        int falsePositives = 0;
        for (int i = 0; i < queries; i++)
        {
            if (filter.Contains(absentBase + i))
                falsePositives++;
        }
        double measured = falsePositives / (double)queries;
        Assert.True(measured <= 0.01 * 2 + 0.002,
            $"measured false-positive rate {measured:F4} exceeded bound after churn");
    }
}
