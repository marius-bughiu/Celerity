using System.Numerics;
using Celerity.Statistics;

namespace Celerity.Tests.Statistics;

/// <summary>
/// Behavioural tests for <see cref="RunningCovariance"/> — the co-moment recurrence against
/// hand-computed values, the undefined-statistic contract, the blocked span path against the
/// per-pair one (including the block boundaries, the scalar tail and the per-pair fallback), the
/// merge formulas, and the copy semantics the mutable-struct choice implies.
/// </summary>
public class RunningCovarianceTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void Default_ShouldBeAnEmptyAccumulator_WhenNeverAddedTo()
    {
        RunningCovariance fit = default;

        Assert.Equal(0, fit.Count);
        Assert.True(double.IsNaN(fit.MeanX));
        Assert.True(double.IsNaN(fit.MeanY));
        Assert.True(double.IsNaN(fit.VarianceX));
        Assert.True(double.IsNaN(fit.VarianceY));
        Assert.True(double.IsNaN(fit.PopulationVarianceX));
        Assert.True(double.IsNaN(fit.PopulationVarianceY));
        Assert.True(double.IsNaN(fit.StandardDeviationX));
        Assert.True(double.IsNaN(fit.StandardDeviationY));
        Assert.True(double.IsNaN(fit.Covariance));
        Assert.True(double.IsNaN(fit.PopulationCovariance));
        Assert.True(double.IsNaN(fit.Correlation));
        Assert.True(double.IsNaN(fit.Slope));
        Assert.True(double.IsNaN(fit.Intercept));
        Assert.True(double.IsNaN(fit.RSquared));
    }

    [Fact]
    public void Add_ShouldReportThePairItself_WhenOnlyOnePairWasAdded()
    {
        var fit = new RunningCovariance();
        fit.Add(3.5d, -2d);

        Assert.Equal(1, fit.Count);
        Assert.Equal(3.5d, fit.MeanX);
        Assert.Equal(-2d, fit.MeanY);
        Assert.Equal(0d, fit.PopulationVarianceX);
        Assert.Equal(0d, fit.PopulationVarianceY);
        Assert.Equal(0d, fit.PopulationCovariance);

        // One point defines no spread, no line and no correlation.
        Assert.True(double.IsNaN(fit.VarianceX));
        Assert.True(double.IsNaN(fit.VarianceY));
        Assert.True(double.IsNaN(fit.Covariance));
        Assert.True(double.IsNaN(fit.Correlation));
        Assert.True(double.IsNaN(fit.Slope));
        Assert.True(double.IsNaN(fit.Intercept));
    }

    [Fact]
    public void Add_ShouldMatchTheTextbookStatisticsOfASmallSample_WhenGivenKnownValues()
    {
        // x = 1..5, y = {2, 4, 5, 4, 5}: deviations (-2,-1,0,1,2) and (-2,0,1,0,1), so
        // Sxx = 10, Syy = 6, Sxy = 6.
        var fit = new RunningCovariance();
        fit.Add(1d, 2d);
        fit.Add(2d, 4d);
        fit.Add(3d, 5d);
        fit.Add(4d, 4d);
        fit.Add(5d, 5d);

        Assert.Equal(5, fit.Count);
        Assert.Equal(3d, fit.MeanX, Tolerance);
        Assert.Equal(4d, fit.MeanY, Tolerance);
        Assert.Equal(2.5d, fit.VarianceX, Tolerance);
        Assert.Equal(1.5d, fit.VarianceY, Tolerance);
        Assert.Equal(2d, fit.PopulationVarianceX, Tolerance);
        Assert.Equal(1.2d, fit.PopulationVarianceY, Tolerance);
        Assert.Equal(Math.Sqrt(2.5d), fit.StandardDeviationX, Tolerance);
        Assert.Equal(Math.Sqrt(1.5d), fit.StandardDeviationY, Tolerance);
        Assert.Equal(1.5d, fit.Covariance, Tolerance);
        Assert.Equal(1.2d, fit.PopulationCovariance, Tolerance);
        Assert.Equal(6d / Math.Sqrt(60d), fit.Correlation, Tolerance);
        Assert.Equal(0.6d, fit.Slope, Tolerance);
        Assert.Equal(2.2d, fit.Intercept, Tolerance);
        Assert.Equal(0.6d, fit.RSquared, Tolerance);
    }

    [Theory]
    [InlineData(3d, -7d)]
    [InlineData(-0.25d, 1_000d)]
    public void Add_ShouldRecoverAnExactLine_WhenEveryPairLiesOnIt(double slope, double intercept)
    {
        var perPair = new RunningCovariance();
        double[] xs = new double[700];
        double[] ys = new double[700];
        for (int i = 0; i < xs.Length; i++)
        {
            xs[i] = (i * 0.37d) - 50d;
            ys[i] = (slope * xs[i]) + intercept;
            perPair.Add(xs[i], ys[i]);
        }

        foreach (RunningCovariance fit in new[] { perPair, new RunningCovariance(xs, ys) })
        {
            Assert.Equal(slope, fit.Slope, 1e-9);
            Assert.Equal(intercept, fit.Intercept, 1e-6);
            Assert.Equal(Math.Sign(slope), fit.Correlation, 1e-12);
            Assert.InRange(fit.Correlation, -1d, 1d);
            Assert.Equal(1d, fit.RSquared, 1e-12);
        }
    }

    [Fact]
    public void Add_ShouldStayAccurate_WhereTheRunningSumsShortcutCollapses()
    {
        // A spread of a few units riding on means near 1e8, with y falling as x rises. The
        // four-sums shortcut subtracts two quantities that agree in their first fifteen digits.
        double[] xs = new double[1_000];
        double[] ys = new double[1_000];
        for (int i = 0; i < xs.Length; i++)
        {
            xs[i] = 1e8 + ((i % 10) * 0.37d);
            ys[i] = 1e8 - ((i % 10) * 0.37d) + ((i % 3) * 0.185d);
        }

        var perPair = new RunningCovariance();
        for (int i = 0; i < xs.Length; i++)
        {
            perPair.Add(xs[i], ys[i]);
        }

        var span = new RunningCovariance(xs, ys);
        double expected = TwoPassSlope(xs, ys);

        Assert.True(expected < 0d);
        Assert.Equal(expected, perPair.Slope, 1e-6);
        Assert.Equal(expected, span.Slope, 1e-9);

        // Not merely imprecise: the shortcut reports y rising with x.
        double naive = NaiveSlope(xs, ys);
        Assert.True(
            naive > 0d,
            $"The running-sums shortcut was expected to get the sign wrong here, but returned {naive}.");
    }

    [Fact]
    public void Add_ShouldKeepTheSpread_WhenTheNewMeanRoundsOntoTheIncomingValue()
    {
        // 1e16 + 1 is not representable, so the mean of {1e16 + 2, 1e16} rounds onto 1e16 itself.
        // An update built from the residual x - newMean then adds nothing; one built from the
        // delta before the update does not depend on how the mean rounded.
        var fit = new RunningCovariance();
        fit.Add(1e16 + 2d, 1d);
        fit.Add(1e16, 0d);

        var swapped = new RunningCovariance();
        swapped.Add(1d, 1e16 + 2d);
        swapped.Add(0d, 1e16);

        Assert.Equal(2d, fit.VarianceX);
        Assert.Equal(0.5d, fit.VarianceY);
        Assert.Equal(1d, fit.Covariance);
        Assert.Equal(0.5d, fit.Slope);
        Assert.Equal(2d, swapped.VarianceY);
        Assert.Equal(1d, swapped.Covariance);
        Assert.Equal(2d, swapped.Slope);
    }

    [Fact]
    public void Merge_ShouldNotOverflow_WhereOnlyTheUnweightedSquareWould()
    {
        // The merged co-moments are about 1.125e308, which fits; the raw squared delta between
        // the two shard means, 2.25e308, does not. The weight has to be applied before squaring.
        var left = new RunningCovariance();
        left.Add(0d, 0d);
        var right = new RunningCovariance();
        right.Add(1.5e154, 1.5e154);

        RunningCovariance merged = RunningCovariance.Combine(left, right);

        var sequential = new RunningCovariance();
        sequential.Add(0d, 0d);
        sequential.Add(1.5e154, 1.5e154);

        Assert.True(double.IsFinite(merged.Covariance));
        Assert.Equal(1d, merged.Slope, Tolerance);
        Assert.Equal(1d, merged.Correlation, Tolerance);
        Assert.Equal(sequential.Covariance, merged.Covariance, sequential.Covariance * 1e-12);
        Assert.Equal(1d, sequential.Slope, Tolerance);
    }

    [Fact]
    public void Add_ShouldKeepASubnormalCrossProduct_WhicheverDeltaIsLarger()
    {
        // Weighting the subnormal delta first would round 0.5 * double.Epsilon to zero before
        // the large delta could scale it back up.
        var fit = new RunningCovariance();
        fit.Add(0d, 0d);
        fit.Add(double.Epsilon, 1e154);

        var swapped = new RunningCovariance();
        swapped.Add(0d, 0d);
        swapped.Add(1e154, double.Epsilon);

        double expected = double.Epsilon * 5e153;
        Assert.True(expected > 0d);
        Assert.Equal(expected, fit.Covariance);
        Assert.Equal(expected, swapped.Covariance);
    }

    [Fact]
    public void SpanAdd_ShouldFallBackToPerPairAdds_WhenBlockSquaresOverflow()
    {
        // Neighbouring doubles near 1e170 are about 1.3e154 apart, so a block deviation can be a
        // whole ulp and its square overflows, while per-pair adds weight it by (n - 1) / n first.
        // The second shape puts the outlier in the scalar tail on any vector width.
        double a = 1e170;
        double b = Math.BitIncrement(a);
        double[][] shapes =
        [
            [a, b],
            [.. Enumerable.Repeat(a, Vector<double>.Count + 1), b],
        ];

        foreach (double[] values in shapes)
        {
            var perPair = new RunningCovariance();
            foreach (double value in values)
            {
                perPair.Add(value, value);
            }

            var span = new RunningCovariance(values, values);

            Assert.True(double.IsFinite(perPair.Covariance));
            Assert.Equal(perPair.Covariance, span.Covariance);
            Assert.Equal(perPair.VarianceX, span.VarianceX);
            Assert.Equal(1d, span.Slope);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SpanAdd_ShouldFallBackToPerPairAdds_WhenBlockSquaresUnderflow(bool underflowInY)
    {
        // Deviations of 1.5e-162 square to below half of double.Epsilon, so the block's sum of
        // squares is zero although the coordinate has spread. Per-pair adds keep a second moment
        // of double.Epsilon.
        double[] tiny = [0d, 3e-162];
        double[] ordinary = [0d, 1d];
        double[] xs = underflowInY ? ordinary : tiny;
        double[] ys = tiny;

        var perPair = new RunningCovariance();
        for (int i = 0; i < xs.Length; i++)
        {
            perPair.Add(xs[i], ys[i]);
        }

        var span = new RunningCovariance(xs, ys);

        // Sample statistics: with two pairs the population forms halve double.Epsilon to zero.
        Assert.Equal(double.Epsilon, perPair.VarianceY);
        Assert.Equal(perPair.VarianceX, span.VarianceX);
        Assert.Equal(perPair.VarianceY, span.VarianceY);
        Assert.Equal(perPair.Covariance, span.Covariance);
        Assert.Equal(perPair.Slope, span.Slope);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Merge_ShouldKeepASubnormalCrossProduct_WhicheverDeltaIsLarger(bool swap)
    {
        // A merge weight of 1/2 (two singletons) must go on the large delta: sqrt-scaling the
        // subnormal one rounds it back to double.Epsilon and overstates the result by 41%.
        (double, double) Pair(double a, double b) => swap ? (b, a) : (a, b);

        var left = new RunningCovariance();
        (double x0, double y0) = Pair(0d, 0d);
        left.Add(x0, y0);
        var right = new RunningCovariance();
        (double x1, double y1) = Pair(double.Epsilon, 1e154);
        right.Add(x1, y1);

        Assert.Equal(double.Epsilon * 5e153, RunningCovariance.Combine(left, right).Covariance);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Merge_ShouldWeightTheSmallerDelta_WhenTheWeightExceedsOne(bool swap)
    {
        // Two shards of 4 give a weight of 2. Weighting the large delta first would overflow
        // (2 * 1e308) where the cross term itself (1e308 * 0.5 * 2 = 1e308) fits.
        var left = new RunningCovariance();
        var right = new RunningCovariance();
        for (int i = 0; i < 4; i++)
        {
            left.Add(0d, 0d);
            if (swap)
            {
                right.Add(0.5d, 1e308);
            }
            else
            {
                right.Add(1e308, 0.5d);
            }
        }

        RunningCovariance merged = RunningCovariance.Combine(left, right);

        Assert.Equal(1e308 / 7d, merged.Covariance, 1e296);
    }

    [Fact]
    public void SpanAdd_ShouldFallBackToPerPairAdds_WhenBlockSquaresAreSubnormal()
    {
        // The block's sum of squares here is nonzero but subnormal, with too few significant
        // bits left to describe the spread: it reported correlation 0.72 for collinear pairs.
        double[] xs = [0d, 3.2e-162];
        double[] ys = [0d, 1d];

        var perPair = new RunningCovariance();
        perPair.Add(xs[0], ys[0]);
        perPair.Add(xs[1], ys[1]);

        var span = new RunningCovariance(xs, ys);

        Assert.Equal(1d, span.Correlation);
        Assert.Equal(perPair.VarianceX, span.VarianceX);
        Assert.Equal(perPair.Covariance, span.Covariance);
    }

    [Fact]
    public void Slope_ShouldBeNaN_WhenEveryXIsIdentical()
    {
        var fit = new RunningCovariance([4d, 4d, 4d], [1d, 2d, 9d]);

        Assert.Equal(0d, fit.VarianceX);
        Assert.Equal(0d, fit.Covariance);
        Assert.True(double.IsNaN(fit.Slope));
        Assert.True(double.IsNaN(fit.Intercept));
        Assert.True(double.IsNaN(fit.Correlation));
        Assert.True(double.IsNaN(fit.RSquared));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(300)]
    [InlineData(1_000)]
    public void SpanAdd_ShouldReportExactlyZeroSpread_WhenARepeatedValueHasNoExactMean(int length)
    {
        // 0.1 * n / n is not 0.1 for every n, so a block mean computed by division alone sits
        // an ulp off the repeated value and leaves a tiny, spurious variance behind.
        double[] xs = Enumerable.Repeat(0.1d, length).ToArray();
        double[] ys = Enumerable.Range(0, length).Select(i => 0.7d + i).ToArray();

        var fit = new RunningCovariance(xs, ys);
        var swapped = new RunningCovariance(ys, xs);

        Assert.Equal(0.1d, fit.MeanX);
        Assert.Equal(0d, fit.PopulationVarianceX);
        Assert.Equal(0d, fit.PopulationCovariance);
        Assert.True(double.IsNaN(fit.Slope));
        Assert.True(double.IsNaN(fit.Correlation));
        Assert.Equal(0d, swapped.PopulationVarianceY);
        Assert.Equal(0d, swapped.Slope);
    }

    [Fact]
    public void Correlation_ShouldBeNaN_ButTheLineDefined_WhenEveryYIsIdentical()
    {
        var fit = new RunningCovariance([1d, 2d, 3d], [5d, 5d, 5d]);

        Assert.Equal(0d, fit.Slope);
        Assert.Equal(5d, fit.Intercept);
        Assert.True(double.IsNaN(fit.Correlation));
        Assert.True(double.IsNaN(fit.RSquared));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(512)]
    [InlineData(1_027)]
    public void SpanAdd_ShouldAgreeWithPerPairAdds_AcrossBlockBoundaries(int length)
    {
        var random = new Random(length + 17);
        double[] xs = new double[length];
        double[] ys = new double[length];
        for (int i = 0; i < length; i++)
        {
            xs[i] = (random.NextDouble() * 200d) - 40d;
            ys[i] = (0.75d * xs[i]) + (random.NextDouble() * 30d);
        }

        var perPair = new RunningCovariance();
        for (int i = 0; i < length; i++)
        {
            perPair.Add(xs[i], ys[i]);
        }

        // Onto an accumulator that already holds pairs, so the block merge runs its general
        // update rather than adopting the first block outright.
        var seeded = new RunningCovariance([1d, 2d], [3d, 1d]);
        var seededPerPair = new RunningCovariance([1d, 2d], [3d, 1d]);
        seeded.Add(xs, ys);
        for (int i = 0; i < length; i++)
        {
            seededPerPair.Add(xs[i], ys[i]);
        }

        AssertClose(perPair, new RunningCovariance(xs, ys));
        AssertClose(seededPerPair, seeded);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(300, false)]
    [InlineData(301, true)]
    [InlineData(700, true)]
    public void SpanAdd_ShouldKeepThePairsBeforeABadOne_WhenACoordinateIsNotFinite(int bad, bool inY)
    {
        double[] xs = new double[800];
        double[] ys = new double[800];
        for (int i = 0; i < xs.Length; i++)
        {
            xs[i] = i;
            ys[i] = 2d * i;
        }

        (inY ? ys : xs)[bad] = double.NaN;

        var fit = new RunningCovariance();
        var thrown = Assert.Throws<ArgumentOutOfRangeException>(() => fit.Add(xs, ys));

        Assert.Equal(inY ? "y" : "x", thrown.ParamName);
        Assert.Equal(bad, fit.Count);
        if (bad >= 2)
        {
            Assert.Equal(2d, fit.Slope, Tolerance);
            Assert.Equal((bad - 1) / 2d, fit.MeanX, Tolerance);
        }
    }

    [Fact]
    public void SpanAdd_ShouldFallBackToPerPairAdds_WhenFiniteValuesOverflowABlockSum()
    {
        // Every value is finite, but the block's sum is not — so the span path must not use it.
        double[] xs = [double.MaxValue / 2d, double.MaxValue / 2d, double.MaxValue / 2d, 0d, 1d];
        double[] ys = [1d, 2d, 3d, 4d, 5d];

        var perPair = new RunningCovariance();
        for (int i = 0; i < xs.Length; i++)
        {
            perPair.Add(xs[i], ys[i]);
        }

        var span = new RunningCovariance(xs, ys);
        var swapped = new RunningCovariance(ys, xs);

        Assert.Equal(perPair.Count, span.Count);
        Assert.Equal(perPair.MeanX, span.MeanX);
        Assert.Equal(perPair.MeanY, span.MeanY);
        Assert.Equal(perPair.PopulationVarianceY, span.PopulationVarianceY);
        Assert.Equal(perPair.MeanX, swapped.MeanY);
        Assert.True(double.IsFinite(span.MeanX));
    }

    [Fact]
    public void SpanAdd_ShouldRejectMismatchedLengths_BeforeAccumulatingAnything()
    {
        var fit = new RunningCovariance();

        var thrown = Assert.Throws<ArgumentException>(() => fit.Add([1d, 2d, 3d], [1d, 2d]));

        Assert.Equal("y", thrown.ParamName);
        Assert.Equal(0, fit.Count);
    }

    [Fact]
    public void Merge_ShouldMatchASingleSequentialPass_WhenTheStreamIsSplit()
    {
        var random = new Random(5);
        double[] xs = new double[1_000];
        double[] ys = new double[1_000];
        for (int i = 0; i < xs.Length; i++)
        {
            xs[i] = random.NextDouble() * 10d;
            ys[i] = (random.NextDouble() * 4d) - xs[i];
        }

        var whole = new RunningCovariance(xs, ys);

        var left = new RunningCovariance(xs.AsSpan(0, 137), ys.AsSpan(0, 137));
        var right = new RunningCovariance();
        for (int i = 137; i < xs.Length; i++)
        {
            right.Add(xs[i], ys[i]);
        }

        left.Merge(right);

        AssertClose(whole, left);
    }

    [Fact]
    public void Merge_ShouldBeANoOp_WhenTheOtherAccumulatorIsEmpty()
    {
        var fit = new RunningCovariance([1d, 2d, 3d], [2d, 4d, 7d]);
        RunningCovariance before = fit;

        fit.Merge(default);

        Assert.Equal(before, fit);
    }

    [Fact]
    public void Merge_ShouldAdoptTheOtherAccumulator_WhenThisOneIsEmpty()
    {
        var other = new RunningCovariance([1d, 2d, 3d], [2d, 4d, 7d]);
        var fit = new RunningCovariance();

        fit.Merge(other);

        Assert.Equal(other, fit);
    }

    [Fact]
    public void Merge_ShouldBeCorrect_WhenAnAccumulatorIsMergedIntoItself()
    {
        var fit = new RunningCovariance([1d, 2d, 3d], [2d, 4d, 7d]);
        var expected = new RunningCovariance([1d, 2d, 3d, 1d, 2d, 3d], [2d, 4d, 7d, 2d, 4d, 7d]);

        fit.Merge(fit);

        AssertClose(expected, fit);
    }

    [Fact]
    public void Combine_ShouldLeaveBothOperandsUntouched()
    {
        var left = new RunningCovariance([1d, 2d], [1d, 3d]);
        var right = new RunningCovariance([3d, 4d], [4d, 9d]);
        RunningCovariance leftBefore = left;
        RunningCovariance rightBefore = right;

        RunningCovariance combined = RunningCovariance.Combine(left, right);

        Assert.Equal(leftBefore, left);
        Assert.Equal(rightBefore, right);
        AssertClose(new RunningCovariance([1d, 2d, 3d, 4d], [1d, 3d, 4d, 9d]), combined);
    }

    [Fact]
    public void Clear_ShouldRestoreTheEmptyState()
    {
        var fit = new RunningCovariance([1d, 2d, 3d], [2d, 4d, 7d]);

        fit.Clear();

        Assert.Equal(default, fit);
        Assert.True(double.IsNaN(fit.MeanX));
    }

    [Fact]
    public void Copy_ShouldBeAnIndependentSnapshot_BecauseTheAccumulatorIsAStruct()
    {
        var original = new RunningCovariance([1d, 2d], [1d, 2d]);
        RunningCovariance copy = original;

        copy.Add(10d, -10d);

        Assert.Equal(2, original.Count);
        Assert.Equal(3, copy.Count);
    }

    [Fact]
    public void ArrayElement_ShouldAccumulateInPlace_BecauseAnElementIsAlreadyARef()
    {
        var perBucket = new RunningCovariance[2];

        perBucket[1].Add(1d, 2d);
        perBucket[1].Add(2d, 4d);

        Assert.Equal(0, perBucket[0].Count);
        Assert.Equal(2d, perBucket[1].Slope, Tolerance);
    }

    [Fact]
    public void Add_ShouldThrow_WhenTheAccumulatorIsAlreadyFull()
    {
        RunningCovariance full = SaturatedCount();

        Assert.Equal(long.MaxValue, full.Count);
        Assert.Throws<InvalidOperationException>(() => full.Add(1d, 1d));
        Assert.Throws<InvalidOperationException>(() => full.Add([1d], [1d]));
        full.Add(ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty);
        Assert.Equal(long.MaxValue, full.Count);
    }

    [Fact]
    public void Merge_ShouldThrow_WhenTheCombinedCountWouldOverflow()
    {
        var doubling = new RunningCovariance();
        doubling.Add(7d, 3d);
        for (int i = 0; i < 62; i++)
        {
            doubling.Merge(doubling);
        }

        Assert.Equal(1L << 62, doubling.Count);
        Assert.Throws<ArgumentException>(() => doubling.Merge(doubling));

        // Rejected before anything was written.
        Assert.Equal(1L << 62, doubling.Count);
        Assert.Equal(7d, doubling.MeanX);
        Assert.Equal(3d, doubling.MeanY);
    }

    [Fact]
    public void SpanAdd_ShouldUseEveryVectorLane_AndTheScalarTail()
    {
        // A length one past a whole number of vectors exercises both loops of a block on any
        // vector width, and the reconstructed statistics must still be exact for a line.
        int length = (Vector<double>.Count * 3) + 1;
        double[] xs = new double[length];
        double[] ys = new double[length];
        for (int i = 0; i < length; i++)
        {
            xs[i] = i;
            ys[i] = 10d - (4d * i);
        }

        var fit = new RunningCovariance(xs, ys);

        Assert.Equal(-4d, fit.Slope, Tolerance);
        Assert.Equal(10d, fit.Intercept, Tolerance);
        Assert.Equal((length - 1) / 2d, fit.MeanX, Tolerance);
    }

    private static void AssertClose(RunningCovariance expected, RunningCovariance actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        Assert.Equal(expected.MeanX, actual.MeanX, Relative(expected.MeanX));
        Assert.Equal(expected.MeanY, actual.MeanY, Relative(expected.MeanY));
        Assert.Equal(expected.PopulationVarianceX, actual.PopulationVarianceX, Relative(expected.PopulationVarianceX));
        Assert.Equal(expected.PopulationVarianceY, actual.PopulationVarianceY, Relative(expected.PopulationVarianceY));
        Assert.Equal(expected.PopulationCovariance, actual.PopulationCovariance, Relative(expected.PopulationCovariance));
    }

    private static double Relative(double expected)
        => double.IsNaN(expected) ? 0d : (Math.Abs(expected) * 1e-9) + 1e-9;

    private static RunningCovariance SaturatedCount()
    {
        var power = new RunningCovariance();
        power.Add(7d, 3d);

        var total = new RunningCovariance();
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

    private static double TwoPassSlope(double[] xs, double[] ys)
    {
        double meanX = xs.Average();
        double meanY = ys.Average();
        double cross = 0d;
        double squares = 0d;
        for (int i = 0; i < xs.Length; i++)
        {
            cross += (xs[i] - meanX) * (ys[i] - meanY);
            squares += (xs[i] - meanX) * (xs[i] - meanX);
        }

        return cross / squares;
    }

    /// <summary>The four-running-sums shortcut this type exists to replace.</summary>
    private static double NaiveSlope(double[] xs, double[] ys)
    {
        double n = xs.Length;
        double sumX = 0d;
        double sumY = 0d;
        double sumXY = 0d;
        double sumXX = 0d;
        for (int i = 0; i < xs.Length; i++)
        {
            sumX += xs[i];
            sumY += ys[i];
            sumXY += xs[i] * ys[i];
            sumXX += xs[i] * xs[i];
        }

        return ((n * sumXY) - (sumX * sumY)) / ((n * sumXX) - (sumX * sumX));
    }
}
