using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Statistical tests that the realized false-positive rate of <see cref="XorFilter{T,THasher}"/> stays near
/// its fixed <c>1/256 ≈ 0.39%</c> ceiling, and that there are never false negatives. The data sets are
/// deterministic (fixed disjoint integer ranges) so the measured rate is stable across runs.
/// </summary>
public class XorFilterFalsePositiveTests
{
    [Fact]
    public void RealizedFalsePositiveRate_StaysNearTheEightBitFloor()
    {
        const int n = 20_000;
        var filter = new XorFilter<int, Int32Murmur3Hasher>(Enumerable.Range(0, n).ToArray());

        // No false negatives for any element in the construction set.
        for (int i = 0; i < n; i++)
            Assert.True(filter.Contains(i), $"false negative for {i}");

        // Query a large disjoint set known to be absent and measure the hit rate.
        const int queries = 200_000;
        const int absentBase = 5_000_000;
        int falsePositives = 0;
        for (int i = 0; i < queries; i++)
        {
            if (filter.Contains(absentBase + i))
                falsePositives++;
        }

        double measured = falsePositives / (double)queries;

        // The theoretical rate is 1/256 ≈ 0.0039; allow generous headroom for sampling noise.
        Assert.Equal(1.0 / 256, filter.FalsePositiveRate);
        Assert.True(measured <= 0.008,
            $"measured false-positive rate {measured:F4} exceeded the ~0.4% xor-filter ceiling");
    }

    [Fact]
    public void StringFilter_NoFalseNegatives_AndBoundedFalsePositives()
    {
        const int n = 5_000;
        var present = new string[n];
        for (int i = 0; i < n; i++)
            present[i] = $"present-{i}";

        var filter = new XorFilter<string, StringMurmur3Hasher>(present);

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
        Assert.True(measured <= 0.008,
            $"measured string false-positive rate {measured:F4} exceeded the ~0.4% ceiling");
    }

    [Fact]
    public void ConstructionConverges_AcrossManySizes()
    {
        // Sweep a range of sizes to exercise the peel + reseed path; every element must round-trip.
        foreach (int n in new[] { 1, 2, 3, 10, 33, 100, 257, 1000, 4999 })
        {
            var filter = new XorFilter<int, Int32Murmur3Hasher>(Enumerable.Range(0, n).ToArray());
            Assert.Equal(n, filter.Count);
            for (int i = 0; i < n; i++)
                Assert.True(filter.Contains(i), $"false negative for {i} in a {n}-element filter");
        }
    }
}
