using Celerity.Statistics;
using CsCheck;

namespace Celerity.Tests.Statistics;

/// <summary>
/// Differential coverage for <see cref="KllSketch{T}"/> against the exact oracle: every value it consumed,
/// sorted.
/// </summary>
/// <remarks>
/// <para>
/// The sketch's guarantee is probabilistic over its coin flips, so the accuracy sweep runs under fixed seeds
/// and is deterministic: a failure replays exactly. It measures the <em>normalized rank error</em> — how far,
/// as a fraction of the stream, the true rank of a reported value lies from the rank that was asked for —
/// across stream shapes that stress different parts of a compactor: random order, both sorted orders, a
/// heavy-tailed distribution, and a stream of twenty distinct values whose ties span thousands of ranks.
/// </para>
/// <para>
/// The CsCheck property draws streams, values of <see cref="KllSketch{T}.K"/> and merge splits freely, so
/// it asserts only what holds on every draw rather than a probabilistic bound: exact counts and extremes,
/// answers drawn from the stream and monotone in the quantile, ranks monotone in the value, an exact answer
/// while nothing has been compacted, and a bounded footprint.
/// </para>
/// </remarks>
public class KllSketchDifferentialTests
{
    public static TheoryData<string, int, ulong> AccuracyCases
    {
        get
        {
            var data = new TheoryData<string, int, ulong>();
            foreach (string shape in new[] { "uniform", "lognormal", "ascending", "descending", "ties" })
            {
                data.Add(shape, KllSketch<double>.DefaultK, 1UL);
                data.Add(shape, KllSketch<double>.DefaultK, 2UL);
                data.Add(shape, 50, 3UL);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(AccuracyCases))]
    public void GetQuantileAndGetRank_ShouldStayWithinTheRankError_AgainstASortedOracle(string shape, int k, ulong seed)
    {
        double[] values = Stream(shape, 200_000, (int)seed);
        var sketch = new KllSketch<double>(k, seed);
        sketch.Add(values);

        AssertWithinRankError(sketch, values, Bound(k), shape);
    }

    [Theory]
    [MemberData(nameof(AccuracyCases))]
    public void Merge_ShouldStayWithinTheRankError_AcrossUnevenShards(string shape, int k, ulong seed)
    {
        double[] values = Stream(shape, 200_000, (int)seed + 100);
        int[] cuts = { 0, 1_000, 51_000, 140_000, values.Length };
        var merged = new KllSketch<double>(k, seed);
        for (int shard = 0; shard < cuts.Length - 1; shard++)
        {
            var part = new KllSketch<double>(k, seed + (ulong)shard + 1);
            part.Add(values.AsSpan(cuts[shard], cuts[shard + 1] - cuts[shard]));
            merged.Merge(part);
        }

        Assert.Equal(values.Length, merged.Count);
        Assert.Equal(values.Min(), merged.Min);
        Assert.Equal(values.Max(), merged.Max);
        AssertWithinRankError(merged, values, Bound(k), shape + " merged");
    }

    [Fact]
    public void Sketch_ShouldHoldItsInvariants_ForAnyStreamKAndMergeSplit()
    {
        Gen.Select(Gen.Int[KllSketch<int>.MinK, 40], Gen.Int[0, 60].Array[0, 1_500], Gen.Int[0, 1_500], Gen.ULong)
            .Sample((k, values, cut, seed) =>
            {
                int split = Math.Min(cut, values.Length);
                var left = new KllSketch<int>(k, seed);
                var right = new KllSketch<int>(k, seed ^ 0x5555UL);
                left.Add(values.AsSpan(0, split));
                right.Add(values.AsSpan(split));
                left.Merge(right);
                AssertInvariants(left, values, k);

                var whole = new KllSketch<int>(k, seed);
                whole.Add(values);
                AssertInvariants(whole, values, k);
            });
    }

    // Generous against the measured worst case — about 1.1% at K = 200 over a million values — so the sweep
    // pins a regression rather than the luck of one seed. Halving K roughly doubles the error.
    private static double Bound(int k) => 4d / k;

    private static void AssertWithinRankError(KllSketch<double> sketch, double[] values, double bound, string context)
    {
        double[] sorted = (double[])values.Clone();
        Array.Sort(sorted);
        int n = sorted.Length;

        double previous = double.NegativeInfinity;
        for (int i = 0; i <= 200; i++)
        {
            double q = i / 200d;
            double reported = sketch.GetQuantile(q);
            Assert.True(reported >= previous, $"{context}: quantiles are not monotone at q={q}.");
            previous = reported;

            // The reported value occupies ranks [below, atOrBelow − 1]; the error is the distance from the
            // requested rank to that span.
            int below = LowerBound(sorted, reported);
            int atOrBelow = UpperBound(sorted, reported);
            Assert.True(atOrBelow > below, $"{context}: {reported} was never added.");
            double target = Math.Floor(q * (n - 1));
            double error = target < below ? below - target : target > atOrBelow - 1 ? target - (atOrBelow - 1) : 0d;
            Assert.True(error / n <= bound, $"{context}: q={q} reported {reported}, {error / n:P2} of the stream away.");

            double probe = sorted[(int)target];
            double rankError = Math.Abs(sketch.GetRank(probe) - ((double)UpperBound(sorted, probe) / n));
            Assert.True(rankError <= bound, $"{context}: GetRank({probe}) is {rankError:P2} off.");
        }
    }

    private static void AssertInvariants(KllSketch<int> sketch, int[] values, int k)
    {
        Assert.Equal(values.Length, sketch.Count);
        if (values.Length == 0)
        {
            Assert.True(sketch.IsEmpty);
            return;
        }

        int[] sorted = values.Order().ToArray();
        Assert.Equal(sorted[0], sketch.Min);
        Assert.Equal(sorted[^1], sketch.Max);

        // Nothing is compacted while the stream fits the first level, so every answer is exact.
        bool exact = values.Length <= k;
        if (exact)
        {
            Assert.Equal(values.Length, sketch.RetainedCount);
        }

        // A loose ceiling on the footprint: the level capacities sum to under 3K plus the floor per level.
        Assert.InRange(sketch.RetainedCount, 1, (3 * k) + (KllSketch<int>.MinK * 12));

        int previous = int.MinValue;
        for (int i = 0; i <= 20; i++)
        {
            double q = i / 20d;
            int reported = sketch.GetQuantile(q);
            Assert.True(reported >= previous);
            Assert.True(Array.BinarySearch(sorted, reported) >= 0);
            if (exact)
            {
                Assert.Equal(sorted[(int)(q * (values.Length - 1))], reported);
            }

            previous = reported;
        }

        double previousRank = 0d;
        for (int probe = -1; probe <= 61; probe++)
        {
            double rank = sketch.GetRank(probe);
            Assert.InRange(rank, previousRank, 1d);
            if (exact)
            {
                Assert.Equal(sorted.Count(v => v <= probe) / (double)values.Length, rank);
            }

            previousRank = rank;
        }

        Assert.Equal(1d, sketch.GetRank(sorted[^1]));
    }

    private static double[] Stream(string shape, int length, int seed)
    {
        var random = new Random(seed);
        var values = new double[length];
        for (int i = 0; i < length; i++)
        {
            values[i] = shape switch
            {
                "uniform" => random.NextDouble(),
                "lognormal" => 10d * Math.Exp(Math.Sqrt(-2d * Math.Log(1d - random.NextDouble())) * Math.Sin(2d * Math.PI * random.NextDouble())),
                "ascending" => i,
                "descending" => length - i,
                _ => random.Next(20),
            };
        }

        return values;
    }

    private static int LowerBound(double[] sorted, double value)
    {
        int lo = 0;
        int hi = sorted.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (sorted[mid] < value)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    private static int UpperBound(double[] sorted, double value)
    {
        int lo = 0;
        int hi = sorted.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (sorted[mid] <= value)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }
}
