using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Statistical tests that a <see cref="SlidingHyperLogLog{T,THasher}"/> windowed estimate stays within a
/// small multiple of the theoretical standard error of the <em>exact</em> in-window distinct count,
/// across window sizes and stream shapes. The streams are deterministic, so the measured error is
/// stable across runs. Mirrors <see cref="HyperLogLogAccuracyTests"/>, with an exact last-seen
/// dictionary as the oracle instead of a plain count.
/// </summary>
public class SlidingHyperLogLogAccuracyTests
{
    [Theory]
    [InlineData(10_000, 100)]
    [InlineData(100_000, 1_000)]
    [InlineData(1_000_000, 10_000)]
    public void SteadyStream_EveryWindow_EstimateWithinErrorBound(int events, int keySpaceDivisor)
    {
        // A stream that cycles through a large key space with a drifting hot set: every window
        // sees a different mix of fresh and repeated keys.
        int keySpace = events / keySpaceDivisor * 50;
        var sketch = new SlidingHyperLogLog<int, Int32Murmur3Hasher>(14);
        var lastSeen = new Dictionary<int, long>();
        var rng = new Random(events);
        for (int t = 0; t < events; t++)
        {
            int key = (t / 3 + rng.Next(keySpace)) % keySpace;
            sketch.Add(key, t);
            lastSeen[key] = t;
        }

        foreach (double fraction in new[] { 0.001, 0.01, 0.1, 0.5, 1.0 })
        {
            long since = events - (long)(events * fraction);
            long exact = lastSeen.Values.Count(v => v >= since);
            AssertWithinBound(exact, sketch.EstimateCardinality(since), sketch.StandardError, $"window {fraction:P1}");
        }
    }

    [Theory]
    [InlineData(5_000)]
    [InlineData(50_000)]
    public void StringKeys_EstimateWithinErrorBound(int n)
    {
        var sketch = new SlidingHyperLogLog<string, StringXxHash64Hasher>(14);
        for (int i = 0; i < n; i++)
            sketch.Add($"user-{i}", i);

        AssertWithinBound(n, sketch.EstimateCardinality(), sketch.StandardError, "all-time");
        AssertWithinBound(n / 2, sketch.EstimateCardinality(n / 2), sketch.StandardError, "second half");
    }

    [Fact]
    public void SequentialLongKeys_With64BitHasher_EstimateWithinErrorBound()
    {
        var sketch = new SlidingHyperLogLog<long, Int64WangHasher>();
        for (long i = 0; i < 200_000; i++)
            sketch.Add(i * 1_000_003, i);

        AssertWithinBound(100_000, sketch.EstimateCardinality(100_000), sketch.StandardError, "last 100k");
    }

    private static void AssertWithinBound(long exact, long estimate, double standardError, string label)
    {
        // Three standard errors plus a small absolute slack for the small-range regime, as in the
        // HyperLogLog accuracy suite.
        double relativeError = Math.Abs(estimate - exact) / (double)exact;
        double bound = standardError * 3 + 0.005;
        Assert.True(relativeError <= bound,
            $"{label}: exact {exact}, estimate {estimate}, relative error {relativeError:F4} > bound {bound:F4}");
    }
}
