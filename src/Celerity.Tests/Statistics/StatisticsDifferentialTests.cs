using Celerity.Statistics;

namespace Celerity.Tests.Statistics;

/// <summary>
/// Reconciles the streaming types against the exact answers they approximate: <see cref="DDSketch"/>
/// against a sorted array of every value it consumed, and <see cref="RunningStatistics"/> against a
/// two-pass computation of the same moments.
/// </summary>
/// <remarks>
/// Each case is a pure function of its seed, so a failure reproduces exactly. The distributions
/// are chosen to hit the shapes a bucketed sketch can get wrong in ways a uniform stream would
/// hide: a heavy right tail (most buckets near the bottom, the quantile of interest at the top),
/// a stream that straddles zero, and one with heavy duplication (many values in one bucket).
/// </remarks>
public class StatisticsDifferentialTests
{
    public static TheoryData<string, int, double> SketchCases
    {
        get
        {
            var data = new TheoryData<string, int, double>();
            foreach (string shape in new[] { "uniform", "lognormal", "signed", "duplicated" })
            {
                foreach (int seed in new[] { 1, 2, 3 })
                {
                    foreach (double accuracy in new[] { 0.01d, 0.05d })
                    {
                        data.Add(shape, seed, accuracy);
                    }
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(SketchCases))]
    public void GetQuantile_ShouldStayWithinTheGuarantee_AgainstAnExactSortedOracle(
        string shape,
        int seed,
        double accuracy)
    {
        double[] values = Generate(shape, seed, 20_000);

        // A budget wide enough that nothing collapses: the guarantee is only claimed while
        // HasCollapsed is false, and this asserts it holds everywhere it is claimed.
        var sketch = new DDSketch(accuracy, 16_384);
        sketch.Add(values);
        Assert.False(sketch.HasCollapsed);

        double[] sorted = [.. values.Order()];

        foreach (double quantile in new[] { 0d, 0.001d, 0.05d, 0.25d, 0.5d, 0.75d, 0.95d, 0.999d, 1d })
        {
            double expected = sorted[(int)(quantile * (sorted.Length - 1))];
            QuantileGuarantee.Holds(
                expected,
                sketch.GetQuantile(quantile),
                accuracy,
                $"{shape}/{seed}/α={accuracy} q={quantile}");
        }
    }

    [Theory]
    [MemberData(nameof(SketchCases))]
    public void Merge_ShouldMatchAWholeStreamSketch_AcrossEveryShape(
        string shape,
        int seed,
        double accuracy)
    {
        double[] values = Generate(shape, seed, 20_000);

        var whole = new DDSketch(accuracy, 16_384);
        whole.Add(values);

        // Four shards, each fed a strided slice, so every shard sees the whole value range.
        DDSketch[] shards = [.. Enumerable.Range(0, 4).Select(_ => new DDSketch(accuracy, 16_384))];
        for (int i = 0; i < values.Length; i++)
        {
            shards[i % shards.Length].Add(values[i]);
        }

        var merged = new DDSketch(accuracy, 16_384);
        foreach (DDSketch shard in shards)
        {
            merged.Merge(shard);
        }

        Assert.Equal(whole.Count, merged.Count);
        Assert.Equal(whole.Min, merged.Min);
        Assert.Equal(whole.Max, merged.Max);

        foreach (double quantile in new[] { 0d, 0.1d, 0.5d, 0.9d, 1d })
        {
            Assert.Equal(whole.GetQuantile(quantile), merged.GetQuantile(quantile));
        }
    }

    [Theory]
    [InlineData("uniform", 1)]
    [InlineData("uniform", 2)]
    [InlineData("lognormal", 1)]
    [InlineData("lognormal", 2)]
    [InlineData("signed", 1)]
    [InlineData("duplicated", 1)]
    public void RunningStatistics_ShouldMatchATwoPassComputation(string shape, int seed)
    {
        double[] values = Generate(shape, seed, 5_000);

        var stats = new RunningStatistics(values);

        double mean = values.Average();
        double m2 = values.Sum(v => Math.Pow(v - mean, 2)) / values.Length;
        double m3 = values.Sum(v => Math.Pow(v - mean, 3)) / values.Length;
        double m4 = values.Sum(v => Math.Pow(v - mean, 4)) / values.Length;

        Assert.Equal(values.Length, stats.Count);
        double skewness = m3 / Math.Pow(m2, 1.5);
        double kurtosis = (m4 / (m2 * m2)) - 3d;

        Assert.Equal(mean, stats.Mean, Tolerance(mean, 1e-9));
        Assert.Equal(m2, stats.PopulationVariance, Tolerance(m2, 1e-8));
        Assert.Equal(skewness, stats.Skewness, Tolerance(skewness, 1e-5));
        Assert.Equal(kurtosis, stats.Kurtosis, Tolerance(kurtosis, 1e-5));
        Assert.Equal(values.Min(), stats.Min);
        Assert.Equal(values.Max(), stats.Max);
    }

    [Theory]
    [InlineData("uniform", 1)]
    [InlineData("lognormal", 2)]
    [InlineData("signed", 3)]
    public void RunningStatisticsMerge_ShouldMatchASequentialPass_AcrossUnevenShards(
        string shape,
        int seed)
    {
        double[] values = Generate(shape, seed, 5_000);

        var whole = new RunningStatistics(values);

        // Deliberately uneven splits: Chan's formulas weight each side by its own count, so an
        // even split would not distinguish them from a plain average.
        int[] bounds = [0, 17, 500, 1_800, values.Length];
        var merged = new RunningStatistics();
        for (int i = 1; i < bounds.Length; i++)
        {
            merged.Merge(new RunningStatistics(values.AsSpan(bounds[i - 1], bounds[i] - bounds[i - 1])));
        }

        Assert.Equal(whole.Count, merged.Count);
        Assert.Equal(whole.Mean, merged.Mean, Tolerance(whole.Mean, 1e-9));
        Assert.Equal(whole.PopulationVariance, merged.PopulationVariance, Tolerance(whole.PopulationVariance, 1e-8));
        Assert.Equal(whole.Skewness, merged.Skewness, Tolerance(whole.Skewness, 1e-5));
        Assert.Equal(whole.Kurtosis, merged.Kurtosis, Tolerance(whole.Kurtosis, 1e-5));
        Assert.Equal(whole.Min, merged.Min);
        Assert.Equal(whole.Max, merged.Max);
    }

    /// <summary>
    /// A relative tolerance with an absolute floor, so a statistic that legitimately sits near
    /// zero is not held to an impossible bound.
    /// </summary>
    private static double Tolerance(double expected, double relative)
        => (Math.Abs(expected) * relative) + 1e-6;

    /// <summary>
    /// Builds one of the four value shapes. Every shape avoids zero-crossing ties at a bucket
    /// boundary by construction, and "signed" deliberately does straddle zero so both ladders
    /// and the zero counter are exercised.
    /// </summary>
    private static double[] Generate(string shape, int seed, int count)
    {
        var random = new Random(seed);
        double[] values = new double[count];

        for (int i = 0; i < count; i++)
        {
            values[i] = shape switch
            {
                "uniform" => 1d + (random.NextDouble() * 999d),
                "lognormal" => Math.Exp(NextGaussian(random) * 2d),
                "signed" => (random.NextDouble() * 2_000d) - 1_000d,
                _ => 1d + (random.Next(0, 20) * 5d),
            };
        }

        return values;
    }

    private static double NextGaussian(Random random)
    {
        double u1 = 1d - random.NextDouble();
        double u2 = random.NextDouble();
        return Math.Sqrt(-2d * Math.Log(u1)) * Math.Sin(2d * Math.PI * u2);
    }
}
