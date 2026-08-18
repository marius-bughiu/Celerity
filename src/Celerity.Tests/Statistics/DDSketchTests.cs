using Celerity.Statistics;

namespace Celerity.Tests.Statistics;

/// <summary>
/// Behavioural tests for <see cref="DDSketch"/> — the relative-accuracy guarantee itself, the
/// three value regions (positive ladder, mirrored negative ladder, the separate zero counter),
/// the exact-versus-approximate split (<c>Count</c> / <c>Sum</c> / <c>Min</c> / <c>Max</c> are
/// exact; only the quantiles are bucketed), and merging.
/// </summary>
public class DDSketchTests
{
    [Fact]
    public void Constructor_ShouldUseTheDocumentedDefaults_WhenNoneAreGiven()
    {
        var sketch = new DDSketch();

        Assert.Equal(DDSketch.DefaultRelativeAccuracy, sketch.RelativeAccuracy);
        Assert.Equal(DDSketch.DefaultMaxBins, sketch.MaxBins);
        Assert.Equal(0, sketch.Count);
        Assert.Equal(0, sketch.BinCount);
        Assert.False(sketch.HasCollapsed);
    }

    [Fact]
    public void Constructor_ShouldKeepTheDefaultBinBudget_WhenOnlyTheAccuracyIsGiven()
    {
        var sketch = new DDSketch(0.005d);

        Assert.Equal(0.005d, sketch.RelativeAccuracy);
        Assert.Equal(DDSketch.DefaultMaxBins, sketch.MaxBins);
    }

    [Fact]
    public void EmptySketch_ShouldReportNothingRatherThanThrow()
    {
        var sketch = new DDSketch();

        Assert.Equal(0, sketch.Count);
        Assert.Equal(0d, sketch.Sum);
        Assert.True(double.IsNaN(sketch.Average));
        Assert.True(double.IsNaN(sketch.Min));
        Assert.True(double.IsNaN(sketch.Max));
        Assert.True(double.IsNaN(sketch.GetQuantile(0.5d)));
    }

    [Theory]
    [InlineData(0.001d)]
    [InlineData(0.01d)]
    [InlineData(0.1d)]
    public void GetQuantile_ShouldStayWithinTheRelativeAccuracy_AcrossFourDecades(double accuracy)
    {
        // Values spanning 0.5 to 5,000 — the shape a relative guarantee exists for, where an
        // absolute error bound would be meaningless at one end and useless at the other.
        double[] values = new double[10_000];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = 0.5d + i;
        }

        // The budget is sized to the range: 2048 bins cover four decades at 1% but not at
        // 0.1%, and the guarantee is only claimed while nothing has collapsed.
        var sketch = new DDSketch(accuracy, 16_384);
        sketch.Add(values);
        Assert.False(sketch.HasCollapsed);

        double[] sorted = [.. values.Order()];

        foreach (double quantile in new[] { 0d, 0.01d, 0.25d, 0.5d, 0.75d, 0.9d, 0.99d, 1d })
        {
            double expected = sorted[(int)(quantile * (sorted.Length - 1))];
            QuantileGuarantee.Holds(expected, sketch.GetQuantile(quantile), accuracy, $"q={quantile}");
        }
    }

    [Fact]
    public void GetQuantile_ShouldHoldTheGuaranteeAcrossTwelveDecades_WhereAnAbsoluteBoundCouldNot()
    {
        var sketch = new DDSketch(0.01d);
        double[] values = new double[13];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = Math.Pow(10d, i - 6);
            sketch.Add(values[i]);
        }

        for (int i = 0; i < values.Length; i++)
        {
            double quantile = (double)i / (values.Length - 1);
            QuantileGuarantee.Holds(values[i], sketch.GetQuantile(quantile), 0.01d, $"q={quantile}");
        }
    }

    [Fact]
    public void GetQuantile_ShouldOrderNegativesBeforeZerosBeforePositives()
    {
        var sketch = new DDSketch(0.01d);
        sketch.Add(-100d);
        sketch.Add(-1d);
        sketch.Add(0d);
        sketch.Add(1d);
        sketch.Add(100d);

        Assert.Equal(5, sketch.Count);
        Assert.Equal(-100d, sketch.GetQuantile(0d), 1d);
        Assert.Equal(-1d, sketch.GetQuantile(0.25d), 0.02d);
        Assert.Equal(0d, sketch.GetQuantile(0.5d));
        Assert.Equal(1d, sketch.GetQuantile(0.75d), 0.02d);
        Assert.Equal(100d, sketch.GetQuantile(1d), 1d);
    }

    [Fact]
    public void GetQuantile_ShouldWalkTheNegativeLadderToItsLowestBucket_WhenTheRankSitsThere()
    {
        // Negative-only, with gaps: the descending walk has to run past empty buckets and fall
        // out at the smallest live index rather than break early.
        var sketch = new DDSketch(0.5d);
        sketch.Add(-AtBucket(5));
        sketch.Add(-AtBucket(3));
        sketch.Add(-AtBucket(1));

        Assert.Equal(-AtBucket(5), sketch.GetQuantile(0d), Math.Abs(AtBucket(5)) * 0.5d);
        Assert.Equal(-AtBucket(1), sketch.GetQuantile(1d), Math.Abs(AtBucket(1)) * 0.5d);
        Assert.True(sketch.GetQuantile(0d) < sketch.GetQuantile(1d));
    }

    [Fact]
    public void GetQuantile_ShouldReturnZero_WhenTheRankFallsInTheZeroCounter()
    {
        var sketch = new DDSketch(0.01d);
        sketch.Add(-5d);
        sketch.Add(0d, 8L);
        sketch.Add(5d);

        Assert.Equal(10, sketch.Count);
        Assert.Equal(0d, sketch.GetQuantile(0.5d));
    }

    [Fact]
    public void Add_ShouldTrackCountSumMinAndMaxExactly_NotBucketed()
    {
        var sketch = new DDSketch(0.1d);
        sketch.Add(1.234d);
        sketch.Add(-7.5d);
        sketch.Add(0d);
        sketch.Add(1000.125d);

        Assert.Equal(4, sketch.Count);
        Assert.Equal(1.234d - 7.5d + 1000.125d, sketch.Sum, 1e-9);
        Assert.Equal((1.234d - 7.5d + 1000.125d) / 4d, sketch.Average, 1e-9);
        Assert.Equal(-7.5d, sketch.Min);
        Assert.Equal(1000.125d, sketch.Max);
    }

    [Fact]
    public void Add_ShouldCountAMultiplicityAsThatManyValues()
    {
        var sketch = new DDSketch(0.01d);
        sketch.Add(3d, 1_000L);
        sketch.Add(9d, 1L);

        Assert.Equal(1_001, sketch.Count);
        Assert.Equal(3d * 1_000d + 9d, sketch.Sum, 1e-9);
        Assert.Equal(3d, sketch.GetQuantile(0.5d), 0.03d);
        Assert.Equal(9d, sketch.GetQuantile(1d), 0.09d);
    }

    [Fact]
    public void GetQuantiles_ShouldMatchTheSingleQuantileQueries()
    {
        var sketch = new DDSketch(0.01d);
        for (int i = 1; i <= 1_000; i++)
        {
            sketch.Add(i);
        }

        double[] quantiles = [0.5d, 0.9d, 0.99d];
        Span<double> results = stackalloc double[3];
        sketch.GetQuantiles(quantiles, results);

        for (int i = 0; i < quantiles.Length; i++)
        {
            Assert.Equal(sketch.GetQuantile(quantiles[i]), results[i]);
        }
    }

    [Fact]
    public void Merge_ShouldEqualASketchFedTheWholeStream()
    {
        double[] left = new double[500];
        double[] right = new double[700];
        for (int i = 0; i < left.Length; i++)
        {
            left[i] = 1d + i;
        }

        for (int i = 0; i < right.Length; i++)
        {
            right[i] = -(1d + i);
        }

        var whole = new DDSketch(0.01d);
        whole.Add(left);
        whole.Add(right);
        whole.Add(0d, 3L);

        var merged = new DDSketch(0.01d);
        merged.Add(left);
        var other = new DDSketch(0.01d);
        other.Add(right);
        other.Add(0d, 3L);
        merged.Merge(other);

        Assert.Equal(whole.Count, merged.Count);
        Assert.Equal(whole.Sum, merged.Sum, 1e-9);
        Assert.Equal(whole.Min, merged.Min);
        Assert.Equal(whole.Max, merged.Max);

        foreach (double quantile in new[] { 0d, 0.1d, 0.5d, 0.9d, 1d })
        {
            Assert.Equal(whole.GetQuantile(quantile), merged.GetQuantile(quantile), 1e-9);
        }

        // The operand is left untouched.
        Assert.Equal(703, other.Count);
    }

    [Fact]
    public void Merge_ShouldBeANoOp_WhenTheOtherSketchIsEmpty()
    {
        var sketch = new DDSketch(0.01d);
        sketch.Add(42d);

        sketch.Merge(new DDSketch(0.01d));

        Assert.Equal(1, sketch.Count);
        Assert.Equal(42d, sketch.Max);
    }

    [Fact]
    public void Merge_ShouldAcceptADifferentBinBudget_WhenTheAccuracyMatches()
    {
        var wide = new DDSketch(0.01d, 4_096);
        var narrow = new DDSketch(0.01d, 16);
        narrow.Add(5d);
        narrow.Add(500d);

        Assert.True(narrow.HasCollapsed);

        wide.Merge(narrow);

        Assert.Equal(2, wide.Count);
        Assert.Equal(4_096, wide.MaxBins);

        // The narrow operand had already folded its low bucket away, and the wider budget
        // cannot unfold it — which is why the merge is documented as bucket-exact rather than
        // as replaying the operand's stream. The top of the range survived; the bottom did not,
        // and the inherited loss is reported rather than hidden.
        Assert.True(wide.HasCollapsed);
        QuantileGuarantee.Holds(500d, wide.GetQuantile(1d), 0.01d, "the surviving high bucket");
        Assert.True(
            wide.GetQuantile(0d) > 100d,
            $"the collapsed low value should read far above its true 5, got {wide.GetQuantile(0d)}.");
    }

    [Fact]
    public void Clear_ShouldEmptyTheSketchButKeepItsConfiguration()
    {
        var sketch = new DDSketch(0.02d, 64);
        sketch.Add(-1d);
        sketch.Add(0d);
        sketch.Add(1d);

        sketch.Clear();

        Assert.Equal(0, sketch.Count);
        Assert.Equal(0, sketch.BinCount);
        Assert.False(sketch.HasCollapsed);
        Assert.True(double.IsNaN(sketch.GetQuantile(0.5d)));
        Assert.Equal(0.02d, sketch.RelativeAccuracy);
        Assert.Equal(64, sketch.MaxBins);

        sketch.Add(10d);
        Assert.Equal(1, sketch.Count);
        Assert.Equal(10d, sketch.GetQuantile(0.5d), 0.2d);
    }

    [Fact]
    public void BinCount_ShouldGrowWithTheValueRangeRatherThanTheSampleCount()
    {
        var few = new DDSketch(0.01d);
        var many = new DDSketch(0.01d);

        for (int i = 0; i < 100; i++)
        {
            few.Add(1d + (i * 0.01d));
        }

        for (int i = 0; i < 100_000; i++)
        {
            many.Add(1d + ((i % 100) * 0.01d));
        }

        Assert.Equal(100_000, many.Count);
        Assert.Equal(few.BinCount, many.BinCount);
        Assert.True(many.BinCount < 100, $"Expected a handful of buckets, got {many.BinCount}.");
    }

    [Fact]
    public void GetQuantile_ShouldStayFinite_AtTheTopOfTheDoubleRange()
    {
        // double.MaxValue lands in bucket 35,488, whose g^i overflows: forming the power before
        // applying the accuracy multiplier returned infinity for a finite input. Evaluated in
        // log space the two exponents cancel and the representative is finite — and still
        // inside the relative bound, which is the contract that actually matters.
        // 1 to double.MaxValue is 308 decades, which needs 35,489 buckets — well past the
        // default budget, so the budget is sized to it rather than letting a collapse hide the
        // arithmetic under test.
        var sketch = new DDSketch(0.01d, 65_536);
        sketch.Add(double.MaxValue);
        sketch.Add(1d);
        Assert.False(sketch.HasCollapsed);

        double top = sketch.GetQuantile(1d);
        Assert.True(double.IsFinite(top), $"expected a finite quantile, got {top}.");
        QuantileGuarantee.Holds(double.MaxValue, top, 0.01d, "q=1 at the top of the double range");

        // And at the other end the clamp does bite: bucket 0's representative is 0.99, below
        // the smallest value actually seen, so it is pulled up to it.
        Assert.Equal(1d, sketch.GetQuantile(0d));
    }

    [Fact]
    public void GetQuantile_ShouldHoldTheGuaranteeDownToTheSubnormalFloor_AtTheDefaultAccuracy()
    {
        // The lower mirror of the double-range caveat. Below about 2.2e-308 consecutive
        // representable values are up to 100% apart, so a tight relative bound cannot be met by
        // *any* returned value — but a bucket that narrow holds a single representable value,
        // so the sketch returns it exactly and the bound holds all the way down. The error is
        // computed as a ratio rather than against `accuracy * expected`, because that product
        // underflows in this range and would compare against zero.
        double[] values =
        [
            double.Epsilon,
            3d * double.Epsilon,
            7d * double.Epsilon,
            1e-320,
            1e-315,
        ];

        var sketch = new DDSketch(0.01d, 1 << 20);
        sketch.Add(values);
        Assert.False(sketch.HasCollapsed);

        for (int i = 0; i < values.Length; i++)
        {
            double quantile = (double)i / (values.Length - 1);
            double actual = sketch.GetQuantile(quantile);
            double relativeError = Math.Abs(actual - values[i]) / values[i];

            Assert.True(
                relativeError <= 0.01d,
                $"q={quantile}: expected {values[i]:E3}, got {actual:E3} (relative {relativeError:G4}).");
        }
    }

    [Fact]
    public void GetQuantile_ShouldRankExactly_WhenMultiplicitiesPushTheCountPastWhatADoubleHolds()
    {
        // Count is 2^63 - 1 here, so `quantile * (Count - 1)` in double rounds 2^63 - 2 up to
        // 2^63. At the median that moves the rank from 2^62 - 1 — the last element of the first
        // bucket — to 2^62, the first of the second, and the answer from about 1 to about 100.
        var sketch = new DDSketch(0.01d);
        sketch.Add(1d, 1L << 62);
        sketch.Add(100d, (1L << 62) - 1);

        Assert.Equal(long.MaxValue, sketch.Count);

        double median = sketch.GetQuantile(0.5d);
        QuantileGuarantee.Holds(1d, median, 0.01d, "the median of a count that exceeds 2^53");

        // The element after it is in the second bucket, which is the boundary being pinned.
        double justAbove = sketch.GetQuantile(0.5000000000000002d);
        QuantileGuarantee.Holds(100d, justAbove, 0.01d, "one rank past the boundary");
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(5e-324)]
    [InlineData(1e-30)]
    [InlineData(1e-15)]
    public void GetQuantile_ShouldNameTheFirstBucket_ForVanishinglySmallQuantilesOfAHugeCount(double quantile)
    {
        // Zero, a subnormal, one far below the reciprocal of the count, and one merely small:
        // the four shapes the exact-rank decomposition separates, reachable only past 2^53
        // where that decomposition is used. All name rank zero, so all resolve to the smallest
        // bucket rather than throwing or overflowing a shift.
        var sketch = new DDSketch(0.01d);
        sketch.Add(1d, 1L << 60);
        sketch.Add(100d, 1L << 60);

        QuantileGuarantee.Holds(1d, sketch.GetQuantile(quantile), 0.01d, $"q={quantile}");
    }

    [Fact]
    public void GetQuantile_ShouldNameTheLastBucket_ForQuantileOneOfAHugeCount()
    {
        var sketch = new DDSketch(0.01d);
        sketch.Add(1d, 1L << 60);
        sketch.Add(100d, 1L << 60);

        QuantileGuarantee.Holds(100d, sketch.GetQuantile(1d), 0.01d, "q=1 past 2^53");
    }

    [Fact]
    public void GetQuantiles_ShouldThrow_WhenTheDestinationOverlapsTheQuantileList()
    {
        // Writing forward into a destination that starts inside the list would overwrite a
        // quantile before it is read — a wrong answer rather than a failure.
        var sketch = new DDSketch(0.01d);
        for (int i = 1; i <= 100; i++)
        {
            sketch.Add(i);
        }

        double[] buffer = [0.1d, 0.5d, 0.9d, 0d];

        Assert.Throws<ArgumentException>(
            () => sketch.GetQuantiles(buffer.AsSpan(0, 3), buffer.AsSpan(1, 3)));

        // Disjoint slices of one array are still accepted, matching Celerity.Sorting.
        double[] shared = [0.1d, 0.5d, 0d, 0d];
        sketch.GetQuantiles(shared.AsSpan(0, 2), shared.AsSpan(2, 2));
        Assert.Equal(sketch.GetQuantile(0.1d), shared[2]);
        Assert.Equal(sketch.GetQuantile(0.5d), shared[3]);
    }

    [Fact]
    public void Sum_ShouldReportInfinity_WhenTheRunningTotalOverflowsBeforeTheFinalSumWould()
    {
        // Compensation keeps what each addition dropped; it cannot resurrect a total that has
        // already left the range. These three values sum to double.MaxValue, but the running
        // total passes through infinity on the way and never comes back.
        var sketch = new DDSketch(0.01d);
        sketch.Add(double.MaxValue);
        sketch.Add(double.MaxValue);
        sketch.Add(-double.MaxValue);

        Assert.Equal(3, sketch.Count);
        Assert.Equal(double.PositiveInfinity, sketch.Sum);

        // The quantiles are read off the buckets and are unaffected either way.
        Assert.Equal(-double.MaxValue, sketch.Min);
        Assert.Equal(double.MaxValue, sketch.Max);
        QuantileGuarantee.Holds(double.MaxValue, sketch.GetQuantile(1d), 0.01d, "q=1");
    }

    [Fact]
    public void Sum_ShouldSurviveCancellationThatAPlainRunningTotalLoses()
    {
        // Not an exotic input: 1 falls below the spacing of 1e16, so a plain running total
        // absorbs it and the cancellation that follows has nothing left to give back. The
        // compensation term keeps what each addition dropped.
        var sketch = new DDSketch(0.01d);
        sketch.Add(1e16d);
        sketch.Add(1d);
        sketch.Add(-1e16d);

        Assert.Equal(3, sketch.Count);
        Assert.Equal(1d, sketch.Sum);
        Assert.Equal(1d / 3d, sketch.Average, 1e-12);
    }

    [Fact]
    public void Sum_ShouldMatchRepeatedSingleAdds_WhenTheBulkProductWouldOverflow()
    {
        // value * count can be infinite where adding the occurrences one at a time is not.
        // These two sketches must agree, and before the split they did not: the bulk form
        // reported infinity while the sequential one ends at double.MaxValue.
        var bulk = new DDSketch(0.01d);
        bulk.Add(-double.MaxValue);
        bulk.Add(double.MaxValue, 2);

        var sequential = new DDSketch(0.01d);
        sequential.Add(-double.MaxValue);
        sequential.Add(double.MaxValue);
        sequential.Add(double.MaxValue);

        Assert.Equal(double.MaxValue, sequential.Sum);
        Assert.Equal(sequential.Sum, bulk.Sum);
        Assert.Equal(sequential.Count, bulk.Count);
    }

    [Fact]
    public void Sum_ShouldSaturateWithoutSpinning_WhenAMultiplicityGenuinelyOverflows()
    {
        // The same split, on a count where the answer really is infinite. It has to stop as
        // soon as the total overflows rather than walking the multiplicity a chunk at a time.
        var sketch = new DDSketch(0.01d);
        sketch.Add(double.MaxValue, long.MaxValue);

        Assert.Equal(long.MaxValue, sketch.Count);
        Assert.Equal(double.PositiveInfinity, sketch.Sum);
        Assert.Equal(double.MaxValue, sketch.Max);
    }

    [Fact]
    public void Sum_ShouldStayCompensatedAcrossAMerge()
    {
        // Each operand carries its own correction, so the merged total is no worse conditioned
        // than one that consumed both streams.
        var left = new DDSketch(0.01d);
        left.Add(1e16d);
        left.Add(1d);

        var right = new DDSketch(0.01d);
        right.Add(-1e16d);
        right.Add(1d);

        left.Merge(right);

        Assert.Equal(4, left.Count);
        Assert.Equal(2d, left.Sum);
    }

    [Fact]
    public void SumAndAverage_ShouldOverflowTogether_WhenTheSumIsNotRepresentable()
    {
        // Pinning the documented limit rather than a fix. The sum of two double.MaxValues is
        // genuinely not a double, so Sum reporting infinity is the right answer; Average is
        // derived from it and inherits that, in the one case where the mean itself would have
        // been representable. Count, Min and Max are unaffected, and the quantiles — the thing
        // the type is actually for — are read off the buckets and never touch the sum.
        var sketch = new DDSketch(0.01d);
        sketch.Add(double.MaxValue);
        sketch.Add(double.MaxValue);

        Assert.Equal(2, sketch.Count);
        Assert.Equal(double.PositiveInfinity, sketch.Sum);
        Assert.Equal(double.PositiveInfinity, sketch.Average);
        Assert.Equal(double.MaxValue, sketch.Min);
        Assert.Equal(double.MaxValue, sketch.Max);
        QuantileGuarantee.Holds(double.MaxValue, sketch.GetQuantile(0.5d), 0.01d, "the median");
    }

    [Fact]
    public void GetQuantile_ShouldStayInsideTheObservedRange()
    {
        // A bucket representative can sit outside the values that landed in it; the true
        // quantile never can, so clamping to [Min, Max] can only move the answer closer.
        var sketch = new DDSketch(0.5d);
        for (int i = 1; i <= 50; i++)
        {
            sketch.Add(i * 3d);
        }

        foreach (double quantile in new[] { 0d, 0.25d, 0.5d, 0.75d, 1d })
        {
            double actual = sketch.GetQuantile(quantile);
            Assert.InRange(actual, sketch.Min, sketch.Max);
        }
    }

    [Fact]
    public void Add_ShouldThrow_WhenTheMultiplicityWouldOverflowTheExactCount()
    {
        var sketch = new DDSketch();
        sketch.Add(1d, long.MaxValue);

        Assert.Throws<ArgumentOutOfRangeException>(() => sketch.Add(2d));

        // Rejected before anything was mutated.
        Assert.Equal(long.MaxValue, sketch.Count);
        Assert.Equal(1d, sketch.Max);
    }

    [Fact]
    public void Merge_ShouldThrow_WhenTheCombinedCountWouldOverflow()
    {
        var left = new DDSketch(0.01d);
        left.Add(1d, long.MaxValue);

        var right = new DDSketch(0.01d);
        right.Add(2d);

        Assert.Throws<ArgumentException>(() => left.Merge(right));

        // And neither store was touched on the way to the throw.
        Assert.Equal(long.MaxValue, left.Count);
        Assert.Equal(1d, left.Max);
    }

    /// <summary>
    /// A positive value that lands in bucket <paramref name="index"/> when the sketch's
    /// relative accuracy is 0.5 (so <c>γ = 3</c>). Chosen away from a bucket boundary so no
    /// rounding of <c>log₃</c> can move it.
    /// </summary>
    internal static double AtBucket(int index) => 2d * Math.Pow(3d, index - 1);
}
