using Celerity.Statistics;

namespace Celerity.Tests.Statistics;

/// <summary>
/// Tests for the moves <see cref="DDSketch"/>'s bucket window makes as the value range widens:
/// growing the backing array, sliding the live window inside it in either direction, and — once
/// the bin budget is exhausted — collapsing the lowest buckets together.
/// </summary>
/// <remarks>
/// <para>
/// Every case is driven through the public surface, at a relative accuracy of <c>0.5</c> so
/// that <c>γ = 3</c> and <see cref="DDSketchTests.AtBucket"/> lands a value in a named bucket.
/// <c>BinCount</c> reports the width of the live window, so the window's position and extent
/// are observable without reaching inside.
/// </para>
/// <para>
/// The assertion that matters in each case is not the width: it is that every value added
/// before the move is still recoverable at its own rank afterwards. A shift that copies the
/// wrong slice, or fails to zero what it vacated, moves counts between buckets and shows up
/// there.
/// </para>
/// </remarks>
public class DDSketchBucketWindowTests
{
    private const double Gamma3Accuracy = 0.5d;

    [Fact]
    public void Window_ShouldPreserveEveryBucket_AcrossGrowthAndBothSlideDirections()
    {
        // The budget is wide enough that nothing can collapse: this exercises only the moves
        // that must be lossless.
        var sketch = new DDSketch(Gamma3Accuracy, 64);

        sketch.Add(DDSketchTests.AtBucket(100));
        Assert.Equal(1, sketch.BinCount);

        // Still inside the initial eight-slot array.
        sketch.Add(DDSketchTests.AtBucket(104));
        Assert.Equal(5, sketch.BinCount);

        // One past the array's end: the live window slides down inside it.
        sketch.Add(DDSketchTests.AtBucket(105));
        Assert.Equal(6, sketch.BinCount);

        // Below the window and wider than the array: it grows.
        sketch.Add(DDSketchTests.AtBucket(96));
        Assert.Equal(10, sketch.BinCount);

        // Below the window but already addressable: no move at all.
        sketch.Add(DDSketchTests.AtBucket(94));
        Assert.Equal(12, sketch.BinCount);

        // Below the window and below the array's start: the live window slides up inside it.
        sketch.Add(DDSketchTests.AtBucket(92));
        Assert.Equal(14, sketch.BinCount);

        Assert.False(sketch.HasCollapsed);
        Assert.Equal(6, sketch.Count);

        int[] buckets = [92, 94, 96, 100, 104, 105];
        for (int rank = 0; rank < buckets.Length; rank++)
        {
            double quantile = (double)rank / (buckets.Length - 1);
            double expected = DDSketchTests.AtBucket(buckets[rank]);
            QuantileGuarantee.Holds(
                expected,
                sketch.GetQuantile(quantile),
                Gamma3Accuracy,
                $"rank {rank}, bucket {buckets[rank]}");
        }
    }

    [Fact]
    public void Window_ShouldCollapseItsLowestBuckets_WhenTheBinBudgetIsExhausted()
    {
        var sketch = new DDSketch(Gamma3Accuracy, 4);

        sketch.Add(DDSketchTests.AtBucket(10));
        sketch.Add(DDSketchTests.AtBucket(12));
        Assert.Equal(3, sketch.BinCount);
        Assert.False(sketch.HasCollapsed);

        // Bucket 14 puts the floor at 11, so bucket 10 folds into 11.
        sketch.Add(DDSketchTests.AtBucket(14));
        Assert.True(sketch.HasCollapsed);
        Assert.Equal(4, sketch.BinCount);
        Assert.Equal(3, sketch.Count);

        // Bucket 100 puts the floor above the whole live window, which becomes one bucket.
        sketch.Add(DDSketchTests.AtBucket(100));
        Assert.Equal(4, sketch.BinCount);
        Assert.Equal(4, sketch.Count);

        // Bucket 50 is below the floor entirely and folds into the lowest live bucket.
        sketch.Add(DDSketchTests.AtBucket(50));
        Assert.Equal(4, sketch.BinCount);
        Assert.Equal(5, sketch.Count);

        // Everything is still counted, and the top of the distribution is still accurate — the
        // guarantee is only lost in the collapsed low tail, which is the point of collapsing
        // the low end rather than the high one.
        double top = DDSketchTests.AtBucket(100);
        Assert.Equal(top, sketch.GetQuantile(1d), Gamma3Accuracy * top);

        // And the low tail is now wrong by orders of magnitude, which HasCollapsed is what
        // warns a caller about.
        double lowTail = sketch.GetQuantile(0d);
        Assert.True(
            lowTail > DDSketchTests.AtBucket(50) * 10d,
            $"Expected the collapsed low tail to read far above its true value, got {lowTail}.");
    }

    [Fact]
    public void Window_ShouldSurviveAOneBinBudget_WhereEveryValueSharesABucket()
    {
        var sketch = new DDSketch(Gamma3Accuracy, 1);

        sketch.Add(DDSketchTests.AtBucket(3));
        sketch.Add(DDSketchTests.AtBucket(20));
        sketch.Add(DDSketchTests.AtBucket(1));

        Assert.Equal(3, sketch.Count);
        Assert.Equal(1, sketch.BinCount);
        Assert.True(sketch.HasCollapsed);
        Assert.Equal(sketch.GetQuantile(0d), sketch.GetQuantile(1d));
    }

    [Fact]
    public void Window_ShouldLeaveTheNegativeLadderIntact_WhenThePositiveOneCollapses()
    {
        // The two ladders carry separate budgets, so a positive range wide enough to exhaust
        // one must not evict anything from the other.
        var sketch = new DDSketch(Gamma3Accuracy, 4);

        sketch.Add(-DDSketchTests.AtBucket(4));
        sketch.Add(-DDSketchTests.AtBucket(3));
        sketch.Add(DDSketchTests.AtBucket(3));
        sketch.Add(DDSketchTests.AtBucket(40));

        Assert.Equal(4, sketch.Count);
        Assert.True(sketch.HasCollapsed);

        double lowest = -DDSketchTests.AtBucket(4);
        double second = -DDSketchTests.AtBucket(3);
        Assert.Equal(lowest, sketch.GetQuantile(0d), Gamma3Accuracy * -lowest);
        Assert.Equal(second, sketch.GetQuantile(1d / 3d), Gamma3Accuracy * -second);
    }

    [Fact]
    public void Merge_ShouldCarryEveryLiveBucket_WhenTheSourceWindowHasGaps()
    {
        var source = new DDSketch(Gamma3Accuracy, 64);
        source.Add(DDSketchTests.AtBucket(1));
        source.Add(DDSketchTests.AtBucket(9));
        source.Add(DDSketchTests.AtBucket(17));

        var target = new DDSketch(Gamma3Accuracy, 64);
        target.Add(DDSketchTests.AtBucket(5));

        target.Merge(source);

        Assert.Equal(4, target.Count);

        int[] buckets = [1, 5, 9, 17];
        for (int rank = 0; rank < buckets.Length; rank++)
        {
            double quantile = (double)rank / (buckets.Length - 1);
            double expected = DDSketchTests.AtBucket(buckets[rank]);
            Assert.Equal(expected, target.GetQuantile(quantile), Gamma3Accuracy * expected);
        }
    }
}
