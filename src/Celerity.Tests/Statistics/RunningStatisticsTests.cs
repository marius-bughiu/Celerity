using Celerity.Statistics;

namespace Celerity.Tests.Statistics;

/// <summary>
/// Behavioural tests for <see cref="RunningStatistics"/> — the moment recurrences against
/// hand-computed values, the undefined-statistic contract (every statistic that needs more
/// values than it has seen reports <see cref="double.NaN"/> rather than throwing), the merge
/// formulas against a single sequential pass, and the copy semantics the mutable-struct
/// choice implies.
/// </summary>
public class RunningStatisticsTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void Default_ShouldBeAnEmptyAccumulator_WhenNeverAddedTo()
    {
        RunningStatistics stats = default;

        Assert.Equal(0, stats.Count);
        Assert.Equal(0d, stats.Sum);
        Assert.True(double.IsNaN(stats.Mean));
        Assert.True(double.IsNaN(stats.Min));
        Assert.True(double.IsNaN(stats.Max));
        Assert.True(double.IsNaN(stats.Variance));
        Assert.True(double.IsNaN(stats.PopulationVariance));
        Assert.True(double.IsNaN(stats.StandardDeviation));
        Assert.True(double.IsNaN(stats.PopulationStandardDeviation));
        Assert.True(double.IsNaN(stats.Skewness));
        Assert.True(double.IsNaN(stats.Kurtosis));
    }

    [Fact]
    public void Constructor_ShouldProduceTheSameStateAsRepeatedAdds_WhenGivenASpan()
    {
        double[] values = [3d, 1d, 4d, 1d, 5d, 9d, 2d, 6d];

        var fromSpan = new RunningStatistics(values);

        var fromAdds = new RunningStatistics();
        foreach (double value in values)
        {
            fromAdds.Add(value);
        }

        Assert.Equal(fromAdds.Count, fromSpan.Count);
        Assert.Equal(fromAdds.Mean, fromSpan.Mean, Tolerance);
        Assert.Equal(fromAdds.Variance, fromSpan.Variance, Tolerance);
    }

    [Fact]
    public void Add_ShouldReportTheValueItself_WhenOnlyOneValueWasAdded()
    {
        var stats = new RunningStatistics();

        stats.Add(7.5d);

        Assert.Equal(1, stats.Count);
        Assert.Equal(7.5d, stats.Mean);
        Assert.Equal(7.5d, stats.Sum);
        Assert.Equal(7.5d, stats.Min);
        Assert.Equal(7.5d, stats.Max);
        Assert.Equal(0d, stats.PopulationVariance);
        Assert.True(double.IsNaN(stats.Variance));
    }

    [Fact]
    public void MinAndMax_ShouldTrackTheExtremes_WhenValuesArriveInEveryOrder()
    {
        // Ascending after the first value exercises the "greater than max" branch, descending
        // the "less than min" one, and the repeat of 5 neither.
        var stats = new RunningStatistics();
        stats.Add(5d);
        stats.Add(9d);
        stats.Add(1d);
        stats.Add(5d);

        Assert.Equal(1d, stats.Min);
        Assert.Equal(9d, stats.Max);
        Assert.Equal(4, stats.Count);
    }

    [Fact]
    public void Add_ShouldMatchTheTextbookMomentsOfASmallSample_WhenGivenKnownValues()
    {
        // 2, 4, 4, 4, 5, 5, 7, 9 — the standard worked example: mean 5, population variance 4.
        double[] values = [2d, 4d, 4d, 4d, 5d, 5d, 7d, 9d];
        var stats = new RunningStatistics(values);

        Assert.Equal(8, stats.Count);
        Assert.Equal(40d, stats.Sum, Tolerance);
        Assert.Equal(5d, stats.Mean, Tolerance);
        Assert.Equal(4d, stats.PopulationVariance, Tolerance);
        Assert.Equal(2d, stats.PopulationStandardDeviation, Tolerance);
        Assert.Equal(32d / 7d, stats.Variance, Tolerance);
        Assert.Equal(Math.Sqrt(32d / 7d), stats.StandardDeviation, Tolerance);
        Assert.Equal(Skewness(values), stats.Skewness, 1e-9);
        Assert.Equal(Kurtosis(values), stats.Kurtosis, 1e-9);
    }

    [Fact]
    public void Add_ShouldStayAccurate_WhereTheSumOfSquaresShortcutCollapses()
    {
        // The classic cancellation case: a tiny spread riding on a huge mean. The naive
        // one-pass formula loses every significant digit here and can even go negative.
        double[] values = [1e10 + 4d, 1e10 + 7d, 1e10 + 13d, 1e10 + 16d];

        var stats = new RunningStatistics(values);

        Assert.Equal(30d, stats.Variance, 1e-9);

        double naive = NaiveVariance(values);
        Assert.True(
            Math.Abs(naive - 30d) > 1e-3,
            $"The naive accumulator was expected to lose the answer here, but returned {naive}.");
    }

    [Fact]
    public void Skewness_ShouldBeNaN_WhenFewerThanThreeValuesWereAdded()
    {
        var stats = new RunningStatistics();
        stats.Add(1d);
        stats.Add(2d);

        Assert.True(double.IsNaN(stats.Skewness));
        Assert.True(double.IsNaN(stats.Kurtosis));
    }

    [Fact]
    public void Kurtosis_ShouldBeNaN_WhenFewerThanFourValuesWereAdded()
    {
        var stats = new RunningStatistics([1d, 2d, 6d]);

        Assert.False(double.IsNaN(stats.Skewness));
        Assert.True(double.IsNaN(stats.Kurtosis));
    }

    [Fact]
    public void ShapeStatistics_ShouldBeNaN_WhenEveryValueWasIdentical()
    {
        // No spread means no defined shape; reporting 0 would claim symmetry that is not there.
        var stats = new RunningStatistics([3d, 3d, 3d, 3d, 3d]);

        Assert.Equal(0d, stats.PopulationVariance);
        Assert.True(double.IsNaN(stats.Skewness));
        Assert.True(double.IsNaN(stats.Kurtosis));
    }

    [Fact]
    public void Skewness_ShouldBePositive_WhenTheTailRunsRight()
    {
        var stats = new RunningStatistics([1d, 1d, 1d, 1d, 2d, 2d, 3d, 20d]);

        Assert.True(stats.Skewness > 0d);
    }

    [Fact]
    public void Merge_ShouldMatchASingleSequentialPass_WhenTheStreamIsSplitInTwo()
    {
        double[] left = [1d, 2d, 3d, 4d, 5d, 6d, 7d];
        double[] right = [11d, 13d, 17d, 19d, 23d];

        var whole = new RunningStatistics([.. left, .. right]);

        var merged = new RunningStatistics(left);
        merged.Merge(new RunningStatistics(right));

        Assert.Equal(whole.Count, merged.Count);
        Assert.Equal(whole.Mean, merged.Mean, Tolerance);
        Assert.Equal(whole.Variance, merged.Variance, Tolerance);
        Assert.Equal(whole.Skewness, merged.Skewness, 1e-8);
        Assert.Equal(whole.Kurtosis, merged.Kurtosis, 1e-8);
        Assert.Equal(whole.Min, merged.Min);
        Assert.Equal(whole.Max, merged.Max);
    }

    [Fact]
    public void Merge_ShouldWidenTheExtremes_OnlyWhenTheOtherSideIsWider()
    {
        var inner = new RunningStatistics([4d, 5d, 6d]);

        var wider = new RunningStatistics([1d, 9d]);
        RunningStatistics widened = RunningStatistics.Combine(inner, wider);
        Assert.Equal(1d, widened.Min);
        Assert.Equal(9d, widened.Max);

        var narrower = new RunningStatistics([4.5d, 5.5d]);
        RunningStatistics unchanged = RunningStatistics.Combine(inner, narrower);
        Assert.Equal(4d, unchanged.Min);
        Assert.Equal(6d, unchanged.Max);
    }

    [Fact]
    public void Merge_ShouldBeANoOp_WhenTheOtherAccumulatorIsEmpty()
    {
        var stats = new RunningStatistics([1d, 2d, 3d]);

        stats.Merge(default);

        Assert.Equal(3, stats.Count);
        Assert.Equal(2d, stats.Mean, Tolerance);
    }

    [Fact]
    public void Merge_ShouldAdoptTheOtherAccumulator_WhenThisOneIsEmpty()
    {
        var stats = new RunningStatistics();

        stats.Merge(new RunningStatistics([1d, 2d, 3d, 4d]));

        Assert.Equal(4, stats.Count);
        Assert.Equal(2.5d, stats.Mean, Tolerance);
        Assert.Equal(1d, stats.Min);
        Assert.Equal(4d, stats.Max);
    }

    [Fact]
    public void Combine_ShouldLeaveBothOperandsUntouched()
    {
        var left = new RunningStatistics([1d, 2d]);
        var right = new RunningStatistics([3d, 4d]);

        RunningStatistics combined = RunningStatistics.Combine(left, right);

        Assert.Equal(2, left.Count);
        Assert.Equal(2, right.Count);
        Assert.Equal(4, combined.Count);
        Assert.Equal(2.5d, combined.Mean, Tolerance);
    }

    [Fact]
    public void Clear_ShouldRestoreTheEmptyState()
    {
        var stats = new RunningStatistics([1d, 2d, 3d]);

        stats.Clear();

        Assert.Equal(0, stats.Count);
        Assert.True(double.IsNaN(stats.Mean));

        stats.Add(10d);
        Assert.Equal(1, stats.Count);
        Assert.Equal(10d, stats.Mean);
    }

    [Fact]
    public void Copy_ShouldBeAnIndependentSnapshot_BecauseTheAccumulatorIsAStruct()
    {
        // Documented copy semantics: this is the footgun the type's remarks warn about, so it
        // is pinned rather than left to be rediscovered.
        var original = new RunningStatistics([1d, 2d]);
        RunningStatistics copy = original;

        copy.Add(99d);

        Assert.Equal(2, original.Count);
        Assert.Equal(3, copy.Count);
    }

    [Fact]
    public void ArrayElement_ShouldAccumulateInPlace_BecauseAnElementIsAlreadyARef()
    {
        RunningStatistics[] perBucket = new RunningStatistics[3];

        perBucket[1].Add(2d);
        perBucket[1].Add(4d);

        Assert.Equal(2, perBucket[1].Count);
        Assert.Equal(3d, perBucket[1].Mean, Tolerance);
        Assert.Equal(0, perBucket[0].Count);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Add_ShouldRejectNonFiniteValues_RatherThanAccumulatingThem(double value)
    {
        // The domain is the finite doubles, as it is for DDSketch in the same package. A
        // recurrence over deltas has no good answer for the alternatives: NaN poisons the
        // moments while leaving the extrema untouched (a comparison against NaN is false either
        // way), and a second infinity evaluates the infinity - infinity a first one survived.
        var stats = new RunningStatistics([1d, 2d, 3d]);

        Assert.Throws<ArgumentOutOfRangeException>(() => stats.Add(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => stats.Add(new[] { 4d, value }));

        // The values that were valid are still there, and the rejected one is not.
        Assert.Equal(4, stats.Count);
        Assert.Equal(1d, stats.Min);
        Assert.Equal(4d, stats.Max);
    }

    [Fact]
    public void Add_ShouldThrow_WhenTheAccumulatorIsAlreadyFull()
    {
        RunningStatistics full = SaturatedCount();

        Assert.Equal(long.MaxValue, full.Count);
        Assert.Throws<InvalidOperationException>(() => full.Add(1d));
        Assert.Equal(long.MaxValue, full.Count);
    }

    [Fact]
    public void Merge_ShouldThrow_WhenTheCombinedCountWouldOverflow()
    {
        // Merging an accumulator into itself doubles its count, so long.MaxValue is sixty-odd
        // calls away rather than unreachable — which is what makes the guard worth having.
        var doubling = new RunningStatistics();
        doubling.Add(7d);
        for (int i = 0; i < 62; i++)
        {
            doubling.Merge(doubling);
        }

        Assert.Equal(1L << 62, doubling.Count);
        Assert.Throws<ArgumentException>(() => doubling.Merge(doubling));

        // Rejected before anything was written.
        Assert.Equal(1L << 62, doubling.Count);
        Assert.Equal(7d, doubling.Mean);
    }

    /// <summary>
    /// An accumulator holding exactly <see cref="long.MaxValue"/> values, built as the sum of
    /// every power of two below it — which is what <c>2^63 - 1</c> is.
    /// </summary>
    private static RunningStatistics SaturatedCount()
    {
        var power = new RunningStatistics();
        power.Add(7d);

        var total = new RunningStatistics();
        for (int i = 0; i < 63; i++)
        {
            total.Merge(power);

            if (i < 62)
            {
                power.Merge(power);
            }
        }

        return total;
    }

    /// <summary>The two-pass population skewness, computed directly for comparison.</summary>
    private static double Skewness(double[] values)
    {
        double mean = values.Average();
        double m2 = values.Sum(v => (v - mean) * (v - mean)) / values.Length;
        double m3 = values.Sum(v => Math.Pow(v - mean, 3)) / values.Length;
        return m3 / Math.Pow(m2, 1.5);
    }

    /// <summary>The two-pass population excess kurtosis, computed directly for comparison.</summary>
    private static double Kurtosis(double[] values)
    {
        double mean = values.Average();
        double m2 = values.Sum(v => (v - mean) * (v - mean)) / values.Length;
        double m4 = values.Sum(v => Math.Pow(v - mean, 4)) / values.Length;
        return m4 / (m2 * m2) - 3d;
    }

    /// <summary>The one-pass shortcut this type exists to replace.</summary>
    private static double NaiveVariance(double[] values)
    {
        double sum = 0d;
        double sumOfSquares = 0d;
        foreach (double value in values)
        {
            sum += value;
            sumOfSquares += value * value;
        }

        double n = values.Length;
        return (sumOfSquares - sum * sum / n) / (n - 1d);
    }
}
