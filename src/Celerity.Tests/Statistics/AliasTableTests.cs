using Celerity.Primitives;
using Celerity.Statistics;

namespace Celerity.Tests.Statistics;

/// <summary>
/// Behavioural tests for <see cref="AliasTable"/>: the distribution it draws from, the in-bucket decision on
/// both of its arms, the zero-weight guarantee, the documented rounding floor for a tiny positive weight,
/// scale invariance at the extremes of <see cref="double"/>, and seeded reproducibility.
/// </summary>
public class AliasTableTests
{
    /// <summary>A generator that cycles a fixed script of 64-bit words, to aim a draw at a chosen bucket and fraction.</summary>
    private struct ScriptedRandom : IRandomSource
    {
        private readonly ulong[] _script;
        private int _position;

        public ScriptedRandom(params ulong[] script)
        {
            _script = script;
            _position = 0;
        }

        public int Calls => _position;

        public ulong NextUInt64()
        {
            ulong value = _script[_position % _script.Length];
            _position++;
            return value;
        }
    }

    [Fact]
    public void Count_ShouldBeTheNumberOfWeights()
    {
        Assert.Equal(1, new AliasTable([5d]).Count);
        Assert.Equal(4, new AliasTable([1d, 0d, 2d, 3d]).Count);
    }

    [Fact]
    public void Sample_ShouldAlwaysReturnTheOnlyOutcome()
    {
        var table = new AliasTable([0.25d]);
        var rng = new Pcg32(3UL);
        for (int i = 0; i < 1_000; i++)
        {
            Assert.Equal(0, table.Sample(ref rng));
        }

        var edges = new ScriptedRandom(0UL, ulong.MaxValue, 1UL << 63);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(0, table.Sample(ref edges));
        }
    }

    [Fact]
    public void Sample_ShouldReturnTheBucketOwner_BelowTheThreshold_AndTheAlias_AboveIt()
    {
        // Weights 1 : 3 over two buckets. Outcome 0 owns half of bucket 0 and names outcome 1 as its alias.
        // Outcome 1 fills bucket 1 by itself.
        var table = new AliasTable([1d, 3d]);

        // The high word of x * 2 picks the bucket, and the low word's top half decides within it.
        var rng = new ScriptedRandom(
            0UL,                      // bucket 0, fraction 0: below the threshold, so the owner.
            (1UL << 63) - 1UL,        // bucket 0, fraction just under 1: above it, so the alias.
            1UL << 63,                // bucket 1, fraction 0: a full bucket.
            ulong.MaxValue);          // bucket 1, fraction just under 1: a full bucket returns its owner either way.

        Assert.Equal(0, table.Sample(ref rng));
        Assert.Equal(1, table.Sample(ref rng));
        Assert.Equal(1, table.Sample(ref rng));
        Assert.Equal(1, table.Sample(ref rng));
    }

    [Theory]
    [InlineData(new[] { 1d, 2d, 3d, 4d })]
    [InlineData(new[] { 4d, 3d, 2d, 1d })]
    [InlineData(new[] { 1d, 1d, 1d, 1d, 1d })]
    [InlineData(new[] { 10d, 0.5d, 0d, 3d, 0d, 7.25d, 1e-3d })]
    [InlineData(new[] { 1e-3d, 1d, 1e3d })]
    [InlineData(new[] { 0d, 0d, 1d })]
    [InlineData(new[] { 0.1d, 0.2d, 0.7d })]
    public void Sample_ShouldMatchTheWeights_OverAnEvenlySpacedSweepOfGeneratorOutputs(double[] weights)
    {
        AliasTableOracle.AssertSweepMatchesWeights(weights);
    }

    [Fact]
    public void Sample_ShouldMatchTheWeights_OverAThousandOutcomes_ByChiSquare()
    {
        const int Outcomes = 1_000;
        const int Draws = 2_000_000;

        var random = new Random(17);
        var weights = new double[Outcomes];
        double total = 0d;
        for (int i = 0; i < Outcomes; i++)
        {
            weights[i] = random.NextDouble() * random.NextDouble() + 0.01d;
            total += weights[i];
        }

        var table = new AliasTable(weights);
        var rng = new Pcg32(42UL);
        var counts = new long[Outcomes];
        for (int i = 0; i < Draws; i++)
        {
            counts[table.Sample(ref rng)]++;
        }

        double chiSquare = 0d;
        for (int i = 0; i < Outcomes; i++)
        {
            double expected = Draws * weights[i] / total;
            double delta = counts[i] - expected;
            chiSquare += delta * delta / expected;
        }

        // 999 degrees of freedom: mean 999 and standard deviation about 44.7. Six deviations above the mean
        // fails only a genuinely skewed sampler. The seed is fixed, so the run is deterministic.
        Assert.InRange(chiSquare, 0d, 999d + (6d * 44.7d));
    }

    [Fact]
    public void Sample_ShouldNeverReturnAZeroWeight()
    {
        double[] weights = [0d, 3d, 0d, 0d, 1d, 0d, 2d, -0d];
        var table = new AliasTable(weights);

        var rng = new Pcg32(11UL);
        var counts = new int[weights.Length];
        for (int i = 0; i < 200_000; i++)
        {
            counts[table.Sample(ref rng)]++;
        }

        for (int i = 0; i < weights.Length; i++)
        {
            if (weights[i] == 0d)
                Assert.Equal(0, counts[i]);
            else
                Assert.True(counts[i] > 0);
        }

        // The extremes of every bucket, too.
        for (ulong bucket = 0; bucket < (ulong)weights.Length; bucket++)
        {
            ulong start = (ulong)(((UInt128)bucket << 64) / (ulong)weights.Length) + 1UL;
            ulong end = (ulong)((((UInt128)(bucket + 1) << 64) - 1) / (ulong)weights.Length);
            var edges = new ScriptedRandom(start, end);
            Assert.NotEqual(0d, weights[table.Sample(ref edges)]);
            Assert.NotEqual(0d, weights[table.Sample(ref edges)]);
        }
    }

    [Fact]
    public void Sample_ShouldNeverDrawAPositiveWeight_WhoseShareRoundsBelowTheResolution()
    {
        // Two buckets of 2^32 units each. A 1e-12 share is about 0.009 of a unit and rounds to zero, while a
        // 1e-9 share is about 8.6 units and survives. Each outcome's own bucket is bucket 1, so x = 2^63 lands
        // in bucket 1 at fraction 0, the one position a surviving share must claim.
        var rounded = new AliasTable([1d, 1e-12d]);
        var surviving = new AliasTable([1d, 1e-9d]);

        var rng = new ScriptedRandom(1UL << 63);
        Assert.Equal(0, rounded.Sample(ref rng));

        rng = new ScriptedRandom(1UL << 63);
        Assert.Equal(1, surviving.Sample(ref rng));
    }

    [Fact]
    public void Constructor_ShouldAcceptWeightsWhoseSumOverflows_AndSubnormalWeights()
    {
        AliasTableOracle.AssertSweepMatchesWeights([double.MaxValue, double.MaxValue, double.MaxValue / 2d]);
        AliasTableOracle.AssertSweepMatchesWeights([double.Epsilon, double.Epsilon * 3d]);
    }

    [Theory]
    [InlineData(new[] { 1e-300d, 0.5e-300d })]
    [InlineData(new[] { 3e-300d, 1e-300d, 1e-300d, 1e-300d })]
    [InlineData(new[] { 1e-305d, 1e-306d, 0d })]
    [InlineData(new[] { 2.2250738585072014e-308d, 1.1125369292536007e-308d })]
    [InlineData(new[] { 1e-299d, 0.25e-299d })]
    [InlineData(new[] { 1e-300d, 0d })]
    public void Constructor_ShouldKeepTheDistribution_WhenTheLargestWeightIsTinyButNormal(double[] weights)
    {
        // Regression: a normal maximum below about 4e-299 once overflowed the scale factor to infinity, which
        // saturated every share and drew the positive weights uniformly (and, on net8.0 x64, could draw a
        // zero weight).
        AliasTableOracle.AssertSweepMatchesWeights(weights);
    }

    [Fact]
    public void Constructor_ShouldNotRetainTheWeightSpan()
    {
        double[] weights = [1d, 0d];
        var table = new AliasTable(weights);
        weights[0] = 0d;
        weights[1] = 1d;

        var rng = new Pcg32(5UL);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(0, table.Sample(ref rng));
        }
    }

    [Fact]
    public void Sample_ShouldBeReproducible_ForTheSameSeed()
    {
        var table = new AliasTable([5d, 1d, 3d, 0.5d, 8d]);
        var first = new Xoshiro256StarStar(99UL);
        var second = new Xoshiro256StarStar(99UL);
        for (int i = 0; i < 1_000; i++)
        {
            Assert.Equal(table.Sample(ref first), table.Sample(ref second));
        }
    }

    [Fact]
    public void SampleSpan_ShouldMatchRepeatedSingleDraws_AndConsumeOneValuePerElement()
    {
        var table = new AliasTable([2d, 7d, 1d, 4d]);
        var bulk = new Pcg32(8UL);
        var single = new Pcg32(8UL);

        var destination = new int[257];
        table.Sample(ref bulk, destination);
        foreach (int drawn in destination)
        {
            Assert.Equal(table.Sample(ref single), drawn);
        }

        Assert.Equal(single.NextUInt64(), bulk.NextUInt64());

        var counted = new ScriptedRandom(0UL);
        table.Sample(ref counted, new int[10]);
        Assert.Equal(10, counted.Calls);
    }

    [Fact]
    public void SampleSpan_ShouldLeaveTheGeneratorUntouched_ForAnEmptyDestination()
    {
        var table = new AliasTable([1d, 1d]);
        var rng = new ScriptedRandom(0UL);
        table.Sample(ref rng, Span<int>.Empty);
        Assert.Equal(0, rng.Calls);
    }

    [Fact]
    public void Sample_ShouldBeSafeFromConcurrentReaders_EachWithItsOwnGenerator()
    {
        var table = new AliasTable([1d, 2d, 3d, 4d]);
        var expected = new int[4][];
        for (int t = 0; t < 4; t++)
        {
            var rng = new WyRand((ulong)t);
            expected[t] = new int[10_000];
            table.Sample(ref rng, expected[t]);
        }

        var actual = new int[4][];
        Parallel.For(0, 4, t =>
        {
            var rng = new WyRand((ulong)t);
            actual[t] = new int[10_000];
            for (int i = 0; i < actual[t].Length; i++)
            {
                actual[t][i] = table.Sample(ref rng);
            }
        });

        for (int t = 0; t < 4; t++)
        {
            Assert.Equal(expected[t], actual[t]);
        }
    }
}

/// <summary>
/// The exactness oracle shared by the behavioural and the property tests. It drives a table with an evenly
/// spaced sweep of generator outputs instead of random ones. That makes each outcome's observed frequency
/// its exact probability up to the sweep's own spacing, with no sampling noise to budget for.
/// </summary>
internal static class AliasTableOracle
{
    private const int SweepBits = 20;

    /// <summary>A generator that walks <c>2^SweepBits</c> evenly spaced points of the 64-bit range.</summary>
    private struct GridRandom : IRandomSource
    {
        private ulong _k;

        public ulong NextUInt64() => _k++ << (64 - SweepBits);
    }

    public static void AssertSweepMatchesWeights(double[] weights)
    {
        var table = new AliasTable(weights);
        Assert.Equal(weights.Length, table.Count);

        const int Points = 1 << SweepBits;
        var rng = new GridRandom();
        var counts = new long[weights.Length];
        for (int i = 0; i < Points; i++)
        {
            counts[table.Sample(ref rng)]++;
        }

        double max = weights.Max();
        double total = weights.Sum(w => w / max);

        // Within one bucket, the sweep's count is off by at most one point, and so is its count below the
        // threshold. An outcome draws on at most n buckets, so its frequency is within 2n / Points of the
        // table's probability. The table, in turn, is within about 2^-32 of the weights.
        double tolerance = (2d * weights.Length / Points) + 1e-9d;
        for (int i = 0; i < weights.Length; i++)
        {
            double expected = weights[i] / max / total;
            double observed = (double)counts[i] / Points;
            Assert.InRange(observed, expected - tolerance, expected + tolerance);
            if (weights[i] == 0d)
                Assert.Equal(0L, counts[i]);
        }
    }
}
