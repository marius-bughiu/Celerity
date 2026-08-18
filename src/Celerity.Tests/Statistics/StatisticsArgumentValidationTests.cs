using Celerity.Statistics;

namespace Celerity.Tests.Statistics;

/// <summary>
/// The argument contracts of the <c>Celerity.Statistics</c> surface, gathered in one place:
/// what each entry point rejects, and — as importantly — what it deliberately accepts rather
/// than rejecting (zero, negatives, an empty span, a mismatched bin budget).
/// </summary>
public class StatisticsArgumentValidationTests
{
    [Theory]
    [InlineData(0d)]
    [InlineData(-0.01d)]
    [InlineData(1d)]
    [InlineData(1.5d)]
    [InlineData(double.NaN)]
    [InlineData(9e-7d)]
    public void DDSketchConstructor_ShouldThrow_WhenTheRelativeAccuracyIsOutOfRange(double accuracy)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DDSketch(accuracy));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(DDSketch.MaxBinBudget + 1)]
    public void DDSketchConstructor_ShouldThrow_WhenTheBinBudgetIsOutOfRange(int maxBins)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DDSketch(0.01d, maxBins));
    }

    [Fact]
    public void DDSketchConstructor_ShouldAcceptTheExtremesOfBothRanges()
    {
        var finest = new DDSketch(DDSketch.MinRelativeAccuracy, 1);
        Assert.Equal(DDSketch.MinRelativeAccuracy, finest.RelativeAccuracy);

        var widest = new DDSketch(0.999d, DDSketch.MaxBinBudget);
        Assert.Equal(DDSketch.MaxBinBudget, widest.MaxBins);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void DDSketchAdd_ShouldThrow_WhenTheValueIsNotFinite(double value)
    {
        var sketch = new DDSketch();

        Assert.Throws<ArgumentOutOfRangeException>(() => sketch.Add(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => sketch.Add(new[] { 1d, value }));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-5L)]
    public void DDSketchAdd_ShouldThrow_WhenTheMultiplicityIsNotPositive(long count)
    {
        var sketch = new DDSketch();

        Assert.Throws<ArgumentOutOfRangeException>(() => sketch.Add(1d, count));
    }

    [Theory]
    [InlineData(-0.0001d)]
    [InlineData(1.0001d)]
    [InlineData(double.NaN)]
    public void DDSketchGetQuantile_ShouldThrow_WhenTheQuantileIsOutOfRange(double quantile)
    {
        var sketch = new DDSketch();
        sketch.Add(1d);

        Assert.Throws<ArgumentOutOfRangeException>(() => sketch.GetQuantile(quantile));
    }

    [Fact]
    public void DDSketchGetQuantiles_ShouldThrow_WhenTheDestinationIsTooShort()
    {
        var sketch = new DDSketch();
        sketch.Add(1d);

        double[] quantiles = [0.5d, 0.9d];
        double[] destination = new double[1];

        Assert.Throws<ArgumentException>(() => sketch.GetQuantiles(quantiles, destination));
    }

    [Fact]
    public void DDSketchMerge_ShouldThrow_WhenTheOtherSketchIsNull()
    {
        var sketch = new DDSketch();

        Assert.Throws<ArgumentNullException>(() => sketch.Merge(null!));
    }

    [Fact]
    public void DDSketchMerge_ShouldThrow_WhenTheAccuracyDiffers()
    {
        var sketch = new DDSketch(0.01d);
        var other = new DDSketch(0.02d);
        other.Add(1d);

        Assert.Throws<ArgumentException>(() => sketch.Merge(other));
    }

    [Fact]
    public void DDSketchAdd_ShouldAcceptZeroAndNegatives_RatherThanRejectingThem()
    {
        var sketch = new DDSketch();

        sketch.Add(0d);
        sketch.Add(-1d);
        sketch.Add(-0d);

        Assert.Equal(3, sketch.Count);
    }

    [Fact]
    public void EmptySpans_ShouldBeANoOpEverywhere()
    {
        var sketch = new DDSketch();
        sketch.Add(ReadOnlySpan<double>.Empty);
        Assert.Equal(0, sketch.Count);

        sketch.GetQuantiles(ReadOnlySpan<double>.Empty, Span<double>.Empty);

        var stats = new RunningStatistics(ReadOnlySpan<double>.Empty);
        Assert.Equal(0, stats.Count);

        var sampler = new ReservoirSampler<int>(capacity: 4, seed: 1UL);
        sampler.Add(ReadOnlySpan<int>.Empty);
        Assert.Equal(0, sampler.TotalSeen);
    }
}
