using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Statistical tests that the realized relative error of
/// <see cref="HyperLogLog{T,THasher}.EstimateCardinality"/> stays within a small
/// multiple of the theoretical standard error across a range of cardinalities and key
/// shapes. The data sets are deterministic (fixed sequences) so the measured error is
/// stable across runs.
/// </summary>
public class HyperLogLogAccuracyTests
{
    [Theory]
    [InlineData(1_000)]
    [InlineData(10_000)]
    [InlineData(100_000)]
    [InlineData(1_000_000)]
    public void IntCardinality_EstimateWithinErrorBound(int n)
    {
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>(); // precision 14
        for (int i = 0; i < n; i++)
            hll.Add(i);

        long estimate = hll.EstimateCardinality();
        double relativeError = Math.Abs(estimate - n) / (double)n;

        // The 14-bit estimator has a ~0.81% standard error. Allow 3 standard errors
        // plus a small absolute slack to cover the small-range regime.
        double bound = hll.StandardError * 3 + 0.005;
        Assert.True(relativeError <= bound,
            $"n={n}: estimate {estimate} had relative error {relativeError:F4} > bound {bound:F4}");
    }

    [Theory]
    [InlineData(5_000)]
    [InlineData(50_000)]
    public void StringCardinality_EstimateWithinErrorBound(int n)
    {
        var hll = new HyperLogLog<string, StringMurmur3Hasher>();
        for (int i = 0; i < n; i++)
            hll.Add($"element-{i}");

        long estimate = hll.EstimateCardinality();
        double relativeError = Math.Abs(estimate - n) / (double)n;

        double bound = hll.StandardError * 3 + 0.01;
        Assert.True(relativeError <= bound,
            $"n={n}: string estimate {estimate} had relative error {relativeError:F4} > bound {bound:F4}");
    }

    [Fact]
    public void DuplicateHeavyStream_CountsOnlyDistinct()
    {
        // 1,000,000 adds but only 25,000 distinct values: the estimate must track the
        // distinct count, not the number of adds.
        const int distinct = 25_000;
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>();
        for (int i = 0; i < 1_000_000; i++)
            hll.Add(i % distinct);

        long estimate = hll.EstimateCardinality();
        double relativeError = Math.Abs(estimate - distinct) / (double)distinct;
        Assert.True(relativeError <= hll.StandardError * 3 + 0.01,
            $"distinct={distinct}: estimate {estimate} had relative error {relativeError:F4}");
    }

    [Fact]
    public void LongCardinality_EstimateWithinErrorBound()
    {
        const int n = 100_000;
        var hll = new HyperLogLog<long, Int64Murmur3Hasher>();
        for (long i = 0; i < n; i++)
            hll.Add(i * 2_147_483_647L + 1); // spread across the 64-bit range

        long estimate = hll.EstimateCardinality();
        double relativeError = Math.Abs(estimate - n) / (double)n;
        Assert.True(relativeError <= hll.StandardError * 3 + 0.005,
            $"long estimate {estimate} had relative error {relativeError:F4}");
    }
}
