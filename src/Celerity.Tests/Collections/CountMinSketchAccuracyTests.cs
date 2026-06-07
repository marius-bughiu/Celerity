using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Statistical tests that <see cref="CountMinSketch{T,THasher}.EstimateCount"/> never
/// underestimates and that its overestimate stays within the <c>epsilon · TotalCount</c>
/// error bound for all but a <c>delta</c> fraction of elements. The data sets are
/// deterministic (a fixed seed and fixed sequences) so the measured error is stable
/// across runs.
/// </summary>
public class CountMinSketchAccuracyTests
{
    [Theory]
    [InlineData(0.1)]
    [InlineData(0.01)]
    [InlineData(0.001)]
    public void ErrorStaysWithinBound_ForAllButDeltaFractionOfKeys(double epsilon)
    {
        const double delta = 0.01;
        const int distinctKeys = 2_000;
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>(epsilon, delta);
        var truth = new Dictionary<int, long>();

        // A skewed stream: low keys appear far more often than high keys, so the total
        // count is large and a few keys dominate it (the heavy-hitter regime CMS targets).
        var rand = new Random(42);
        for (int i = 0; i < 200_000; i++)
        {
            // Bias toward small keys: square of a uniform sample.
            double u = rand.NextDouble();
            int key = (int)(u * u * distinctKeys);
            sketch.Add(key);
            truth[key] = truth.GetValueOrDefault(key) + 1;
        }

        long total = sketch.TotalCount;
        double bound = epsilon * total;

        int overBound = 0;
        foreach (var (key, count) in truth)
        {
            long estimate = sketch.EstimateCount(key);
            // Hard guarantee: never underestimates.
            Assert.True(estimate >= count, $"underestimate for {key}: {estimate} < {count}");
            if (estimate - count > bound)
                overBound++;
        }

        // With probability >= 1 - delta a key's error is within the bound, so the fraction
        // exceeding it should not be much above delta. Allow generous slack for sampling.
        double overFraction = overBound / (double)truth.Count;
        Assert.True(overFraction <= delta + 0.05,
            $"epsilon={epsilon}: {overFraction:F4} of keys exceeded the error bound (delta={delta})");
    }

    [Fact]
    public void HeavyHitters_AreEstimatedAccurately()
    {
        // A handful of high-frequency keys mixed into a sea of noise: their estimates
        // should be within the error bound (the classic heavy-hitter use case).
        const double epsilon = 0.001;
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>(epsilon, 0.01);

        var heavy = new Dictionary<int, long>
        {
            [1] = 50_000,
            [2] = 30_000,
            [3] = 20_000,
        };
        foreach (var (key, count) in heavy)
            sketch.Add(key, count);

        // Noise: 100k single-occurrence keys.
        for (int i = 1000; i < 101_000; i++)
            sketch.Add(i);

        double bound = epsilon * sketch.TotalCount;
        foreach (var (key, count) in heavy)
        {
            long estimate = sketch.EstimateCount(key);
            Assert.True(estimate >= count, $"underestimate for heavy key {key}");
            Assert.True(estimate - count <= bound,
                $"heavy key {key}: error {estimate - count} exceeded bound {bound}");
        }
    }

    [Fact]
    public void StringFrequencies_NeverUnderestimate()
    {
        var sketch = new CountMinSketch<string, StringMurmur3Hasher>(0.001, 0.01);
        var truth = new Dictionary<string, long>();

        var rand = new Random(7);
        for (int i = 0; i < 50_000; i++)
        {
            string key = $"key-{rand.Next(0, 1000)}";
            long add = rand.Next(1, 5);
            sketch.Add(key, add);
            truth[key] = truth.GetValueOrDefault(key) + add;
        }

        double bound = 0.001 * sketch.TotalCount;
        int overBound = 0;
        foreach (var (key, count) in truth)
        {
            long estimate = sketch.EstimateCount(key);
            Assert.True(estimate >= count, $"underestimate for {key}");
            if (estimate - count > bound)
                overBound++;
        }
        Assert.True(overBound / (double)truth.Count <= 0.05,
            "too many string keys exceeded the error bound");
    }

    [Fact]
    public void LongFrequencies_NeverUnderestimate()
    {
        var sketch = new CountMinSketch<long, Int64Murmur3Hasher>(0.001, 0.01);
        var truth = new Dictionary<long, long>();

        for (long i = 0; i < 100_000; i++)
        {
            long key = (i % 500) * 2_147_483_647L + 1; // spread across the 64-bit range
            sketch.Add(key);
            truth[key] = truth.GetValueOrDefault(key) + 1;
        }

        foreach (var (key, count) in truth)
            Assert.True(sketch.EstimateCount(key) >= count, $"underestimate for {key}");
    }

    [Fact]
    public void AbsentKeys_AreNeverNegativeAndUsuallyZero()
    {
        // A query for a never-added key returns 0 unless a collision inflates it; with a
        // sketch whose grid is far wider than the added set (here 4096 columns × 5 rows for
        // only 500 keys), the per-counter load is low, so an absent key reads non-zero only
        // if every one of its rows happens to collide — vanishingly rare.
        const int n = 500;
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>(0.001, 0.01);
        for (int i = 0; i < n; i++)
            sketch.Add(i);

        int nonZero = 0;
        for (int i = 10_000_000; i < 10_010_000; i++)
        {
            long estimate = sketch.EstimateCount(i);
            Assert.True(estimate >= 0);
            if (estimate != 0)
                nonZero++;
        }

        // Collisions are rare in a sketch sized for 0.1% error, so almost all absent
        // queries read exactly zero.
        Assert.True(nonZero / 10_000.0 <= 0.05, "too many absent keys collided to non-zero");
    }
}
