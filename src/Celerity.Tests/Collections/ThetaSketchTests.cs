using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural tests for <see cref="ThetaSketch{T,THasher}"/>: construction, exact mode,
/// the switch into estimation mode, merging, bounds and clearing. The statistical accuracy of
/// the estimates is covered by <c>ThetaSketchAccuracyTests</c> and the set expressions by
/// <c>CompactThetaSketchTests</c>.
/// </summary>
public class ThetaSketchTests
{
    private const double FullThreshold = 9223372036854775808d; // 2^63

    // Publishes the key itself as the 64-bit hash, so a test controls each element's sample
    // value exactly: key x is retained as (x >> 1) + 1.
    internal struct IdentityHasher64 : IHashProvider<ulong>, IHashProvider64<ulong>
    {
        public int Hash(ulong key) => (int)key;

        public ulong Hash64(ulong key) => key;
    }

    // The element whose sample value is `value` under IdentityHasher64.
    internal static ulong KeyFor(ulong value) => (value - 1) << 1;

    // ── Construction ────────────────────────────────────────────────────────

    [Fact]
    public void NewSketch_IsEmptyAndExact()
    {
        var sketch = new ThetaSketch<int, Int32Murmur3Hasher>();

        Assert.True(sketch.IsEmpty);
        Assert.False(sketch.IsEstimationMode);
        Assert.Equal(0, sketch.RetainedCount);
        Assert.Equal(1d, sketch.Theta);
        Assert.Equal(0d, sketch.EstimateCardinality());
        Assert.Equal(0d, sketch.GetLowerBound(2));
        Assert.Equal(0d, sketch.GetUpperBound(2));
        Assert.Equal(ThetaSketch<int, Int32Murmur3Hasher>.DefaultPrecision, sketch.Precision);
        Assert.Equal(4096, sketch.NominalEntries);
        Assert.Equal(1d / 64d, sketch.StandardError);
    }

    [Theory]
    [InlineData(ThetaSketch<int, Int32Murmur3Hasher>.MinPrecision)]
    [InlineData(ThetaSketch<int, Int32Murmur3Hasher>.MaxPrecision)]
    public void Constructor_AcceptsTheBoundaryPrecisions(int precision)
    {
        var sketch = new ThetaSketch<int, Int32Murmur3Hasher>(precision);
        Assert.Equal(precision, sketch.Precision);
        Assert.Equal(1 << precision, sketch.NominalEntries);
    }

    [Theory]
    [InlineData(ThetaSketch<int, Int32Murmur3Hasher>.MinPrecision - 1)]
    [InlineData(ThetaSketch<int, Int32Murmur3Hasher>.MaxPrecision + 1)]
    public void Constructor_RejectsAnOutOfRangePrecision(int precision)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new ThetaSketch<int, Int32Murmur3Hasher>(precision));
        Assert.Equal("precision", ex.ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ThetaSketch<int, Int32Murmur3Hasher>([1, 2], precision));
    }

    [Fact]
    public void EnumerableConstructor_ChecksNullBeforePrecision()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new ThetaSketch<int, Int32Murmur3Hasher>(null!, 0));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void EnumerableConstructor_AddsEveryElement()
    {
        var sketch = new ThetaSketch<int, Int32Murmur3Hasher>([5, 1, 5, 3, 1], 6);
        Assert.Equal(6, sketch.Precision);
        Assert.Equal(3d, sketch.EstimateCardinality());
    }

    // ── Exact mode ──────────────────────────────────────────────────────────

    [Fact]
    public void ExactMode_CountsEveryDistinctElement_ThroughEveryTableGrowth()
    {
        // At precision 10 the table starts at 32 slots and doubles five times before reaching
        // 2k = 2048; the sketch stays exact until 1.5k = 1536 values.
        var sketch = new ThetaSketch<int, Int32Murmur3Hasher>(10);
        for (int i = 0; i < 1536; i++)
        {
            sketch.Add(i);
            sketch.Add(i); // a duplicate never changes the sketch
            Assert.Equal(i + 1, sketch.RetainedCount);
        }

        Assert.False(sketch.IsEstimationMode);
        Assert.Equal(1536d, sketch.EstimateCardinality());
        Assert.Equal(1536d, sketch.GetLowerBound(3));
        Assert.Equal(1536d, sketch.GetUpperBound(3));
    }

    [Fact]
    public void DefaultAndZero_AreOrdinaryElements()
    {
        var sketch = new ThetaSketch<int, Int32Murmur3Hasher>();
        sketch.Add(0);
        sketch.Add(default);
        sketch.Add(1);
        Assert.Equal(2d, sketch.EstimateCardinality());
    }

    // ── Estimation mode ─────────────────────────────────────────────────────

    [Fact]
    public void Overflowing_KeepsTheKSmallest_AndSetsTheThresholdBelowTheNext()
    {
        // Values 1..25 at precision 4 (k = 16): the 25th value overflows the 1.5k = 24 limit, so
        // the sketch keeps 1..16 and its inclusive threshold becomes 17 - 1 = 16.
        var sketch = new ThetaSketch<ulong, IdentityHasher64>(4);
        for (ulong v = 1; v <= 25; v++)
            sketch.Add(KeyFor(v));

        Assert.True(sketch.IsEstimationMode);
        Assert.False(sketch.IsEmpty);
        Assert.Equal(16, sketch.RetainedCount);
        Assert.Equal(16d / FullThreshold, sketch.Theta);
        Assert.Equal(16d / (16d / FullThreshold), sketch.EstimateCardinality());

        // Values above the threshold are rejected; values at or below it are already present.
        sketch.Add(KeyFor(17));
        sketch.Add(KeyFor(16));
        Assert.Equal(16, sketch.RetainedCount);
    }

    [Fact]
    public void Theta_MatchesAReferenceModel_OverARandomStream()
    {
        const int Precision = 5;
        const int K = 1 << Precision;
        var rng = new Random(20261126);
        var sketch = new ThetaSketch<ulong, IdentityHasher64>(Precision);

        // The reference keeps every distinct value at or below the threshold and, once it holds
        // more than 1.5k, keeps the k smallest and lowers the threshold below the (k + 1)-th.
        var retained = new SortedSet<ulong>();
        ulong threshold = 1UL << 63;

        for (int i = 0; i < 20_000; i++)
        {
            // A small value range so duplicates occur, both below and above the threshold.
            ulong key = (ulong)rng.NextInt64(0, 40_000) << 50;
            sketch.Add(key);

            ulong value = (key >> 1) + 1;
            if (value <= threshold && retained.Add(value) && retained.Count > K * 3 / 2)
            {
                ulong cut = retained.ElementAt(K);
                threshold = cut - 1;
                retained.RemoveWhere(v => v >= cut);
            }

            Assert.Equal(retained.Count, sketch.RetainedCount);
            Assert.Equal(threshold / FullThreshold, sketch.Theta);
        }

        Assert.True(sketch.IsEstimationMode);
    }

    // ── Bounds ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void Bounds_RejectAnUnsupportedNumberOfStandardDeviations(int standardDeviations)
    {
        var sketch = new ThetaSketch<int, Int32Murmur3Hasher>();
        Assert.Throws<ArgumentOutOfRangeException>(() => sketch.GetLowerBound(standardDeviations));
        Assert.Throws<ArgumentOutOfRangeException>(() => sketch.GetUpperBound(standardDeviations));
    }

    [Fact]
    public void Bounds_BracketTheEstimate_AndWidenWithTheConfidence()
    {
        var sketch = new ThetaSketch<int, Int32Murmur3Hasher>(8);
        for (int i = 0; i < 50_000; i++)
            sketch.Add(i);

        double estimate = sketch.EstimateCardinality();
        double previousLower = estimate, previousUpper = estimate;
        for (int z = 1; z <= 3; z++)
        {
            double lower = sketch.GetLowerBound(z);
            double upper = sketch.GetUpperBound(z);
            Assert.True(lower < previousLower && upper > previousUpper);
            Assert.True(lower >= sketch.RetainedCount);
            previousLower = lower;
            previousUpper = upper;
        }

        Assert.InRange(50_000d, sketch.GetLowerBound(3), sketch.GetUpperBound(3));
    }

    // ── UnionWith(ThetaSketch) ──────────────────────────────────────────────

    [Fact]
    public void UnionWith_RejectsNull()
    {
        var sketch = new ThetaSketch<int, Int32Murmur3Hasher>();
        Assert.Throws<ArgumentNullException>(() => sketch.UnionWith((ThetaSketch<int, Int32Murmur3Hasher>)null!));
        Assert.Throws<ArgumentNullException>(() => sketch.UnionWith((CompactThetaSketch<int, Int32Murmur3Hasher>)null!));
    }

    [Fact]
    public void UnionWith_Itself_DoesNothing()
    {
        var sketch = new ThetaSketch<int, Int32Murmur3Hasher>(Enumerable.Range(0, 100), 4);
        int retained = sketch.RetainedCount;
        double theta = sketch.Theta;

        sketch.UnionWith(sketch);

        Assert.Equal(retained, sketch.RetainedCount);
        Assert.Equal(theta, sketch.Theta);
    }

    [Fact]
    public void UnionWith_TwoExactSketches_IsExact()
    {
        var a = new ThetaSketch<int, Int32Murmur3Hasher>(Enumerable.Range(0, 300));
        var b = new ThetaSketch<int, Int32Murmur3Hasher>(Enumerable.Range(200, 300));

        a.UnionWith(b);

        Assert.False(a.IsEstimationMode);
        Assert.Equal(500d, a.EstimateCardinality());
        Assert.Equal(300d, b.EstimateCardinality()); // the operand is left alone
    }

    [Fact]
    public void UnionWith_KeepsEveryValueOfEitherOperandBelowTheResultThreshold()
    {
        // a is sampling at a finer rate than b, so the merge lowers a's threshold (pruning its
        // values above b's) and then adds b's values, rebuilding as it overflows.
        var a = new ThetaSketch<int, Int32Murmur3Hasher>(Enumerable.Range(0, 5_000), 7);
        var b = new ThetaSketch<int, Int32Murmur3Hasher>(Enumerable.Range(3_000, 40_000), 6);
        Assert.True(a.Theta > b.Theta);
        CompactThetaSketch<int, Int32Murmur3Hasher> compactA = a.Compact(), compactB = b.Compact();

        a.UnionWith(b);

        Assert.True(a.Theta <= b.Theta);
        Assert.Equal(7, a.Precision);
        Assert.True(a.RetainedCount <= (3 << 7) / 2);
        AssertSameValuesBelowTheCommonThreshold(CompactThetaSketch<int, Int32Murmur3Hasher>.Union(compactA, compactB), a.Compact());
        Assert.InRange(43_000d, a.GetLowerBound(3), a.GetUpperBound(3));
    }

    [Fact]
    public void UnionWith_ASketchSamplingAtAHigherRate_KeepsThisThreshold()
    {
        // b samples at a higher rate than a, so a keeps its threshold and admits only b's
        // values that fall under it.
        var a = new ThetaSketch<int, Int32Murmur3Hasher>(Enumerable.Range(0, 100_000), 5);
        var b = new ThetaSketch<int, Int32Murmur3Hasher>(Enumerable.Range(50_000, 1_000), 5);
        double theta = a.Theta;
        Assert.True(theta < b.Theta);

        a.UnionWith(b);

        Assert.True(a.Theta <= theta);
        Assert.InRange(100_000d, a.GetLowerBound(3), a.GetUpperBound(3));
    }

    // ── UnionWith(CompactThetaSketch) ───────────────────────────────────────

    [Fact]
    public void UnionWithCompact_StopsAtTheFirstValueAboveTheThreshold()
    {
        // The compact sketch is exact and holds values on both sides of a's threshold, so the
        // ascending merge admits a prefix and stops.
        var a = new ThetaSketch<ulong, IdentityHasher64>(4);
        for (ulong v = 1; v <= 25; v++)
            a.Add(KeyFor(v * 10)); // keeps 10..160, threshold 169

        var other = new ThetaSketch<ulong, IdentityHasher64>(10);
        for (ulong v = 1; v <= 200; v++)
            other.Add(KeyFor(v * 10 + 5)); // 15, 25, ..., 2005: exact

        a.UnionWith(other.Compact());

        // 10..160 and 15..165 hold 32 values, above 1.5k = 24, so the merge rebuilt to the 16
        // smallest (10, 15, ..., 85) with the threshold just below 90.
        Assert.Equal(16, a.RetainedCount);
        Assert.Equal(89d / FullThreshold, a.Theta);
    }

    [Fact]
    public void UnionWithCompact_OfASetExpression_AccumulatesIt()
    {
        var a = new ThetaSketch<int, Int32Murmur3Hasher>(Enumerable.Range(0, 1_000));
        var b = new ThetaSketch<int, Int32Murmur3Hasher>(Enumerable.Range(500, 1_000));
        var c = new ThetaSketch<int, Int32Murmur3Hasher>(Enumerable.Range(10_000, 100));

        CompactThetaSketch<int, Int32Murmur3Hasher> ab = CompactThetaSketch<int, Int32Murmur3Hasher>.Intersect(a.Compact(), b.Compact());
        c.UnionWith(ab);

        Assert.Equal(600d, c.EstimateCardinality()); // 500 shared + 100, all exact
    }

    [Fact]
    public void UnionWithCompact_OfAnEstimationModeEmptyResult_LowersTheThresholdOnly()
    {
        var a = new ThetaSketch<int, Int32Murmur3Hasher>(Enumerable.Range(0, 100_000), 4);
        var b = new ThetaSketch<int, Int32Murmur3Hasher>(Enumerable.Range(200_000, 100_000), 4);
        CompactThetaSketch<int, Int32Murmur3Hasher> none = CompactThetaSketch<int, Int32Murmur3Hasher>.Intersect(a.Compact(), b.Compact());
        Assert.Equal(0, none.RetainedCount);
        Assert.True(none.IsEstimationMode);

        var target = new ThetaSketch<int, Int32Murmur3Hasher>(4);
        target.UnionWith(none);

        Assert.False(target.IsEmpty);
        Assert.True(target.IsEstimationMode);
        Assert.Equal(0d, target.EstimateCardinality());
        Assert.True(target.GetUpperBound(2) > 0);
    }

    // ── Compact ─────────────────────────────────────────────────────────────

    [Fact]
    public void Compact_SnapshotsTheSketch_AndIsUnaffectedByLaterAdds()
    {
        var sketch = new ThetaSketch<int, Int32Murmur3Hasher>(Enumerable.Range(0, 10));
        CompactThetaSketch<int, Int32Murmur3Hasher> snapshot = sketch.Compact();

        sketch.Add(100);

        Assert.Equal(10, snapshot.RetainedCount);
        Assert.Equal(10d, snapshot.EstimateCardinality());
        Assert.Equal(11d, sketch.EstimateCardinality());
    }

    [Fact]
    public void Compact_OfAnEmptySketch_IsEmpty()
    {
        CompactThetaSketch<int, Int32Murmur3Hasher> compact = new ThetaSketch<int, Int32Murmur3Hasher>().Compact();
        Assert.True(compact.IsEmpty);
        Assert.Equal(0, compact.RetainedCount);
        Assert.Equal(1d, compact.Theta);
    }

    [Fact]
    public void Compact_CarriesTheThresholdAndCount()
    {
        var sketch = new ThetaSketch<int, Int32Murmur3Hasher>(Enumerable.Range(0, 100_000), 6);
        CompactThetaSketch<int, Int32Murmur3Hasher> compact = sketch.Compact();

        Assert.Equal(sketch.RetainedCount, compact.RetainedCount);
        Assert.Equal(sketch.Theta, compact.Theta);
        Assert.Equal(sketch.IsEstimationMode, compact.IsEstimationMode);
        Assert.Equal(sketch.EstimateCardinality(), compact.EstimateCardinality());
        Assert.Equal(sketch.GetLowerBound(2), compact.GetLowerBound(2));
        Assert.Equal(sketch.GetUpperBound(2), compact.GetUpperBound(2));
    }

    // ── Clear ───────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_ResetsToEmpty_AndTheSketchRefillsExactly()
    {
        var sketch = new ThetaSketch<int, Int32Murmur3Hasher>(Enumerable.Range(0, 100_000), 6);
        Assert.True(sketch.IsEstimationMode);

        sketch.Clear();

        Assert.True(sketch.IsEmpty);
        Assert.Equal(1d, sketch.Theta);
        Assert.Equal(0d, sketch.EstimateCardinality());

        for (int i = 0; i < 50; i++)
            sketch.Add(i);
        Assert.Equal(50d, sketch.EstimateCardinality());
    }

    // Two compact sketches hold the same values below the smaller of their thresholds exactly
    // when neither difference keeps anything (a difference restricts both operands to the
    // common threshold first).
    internal static void AssertSameValuesBelowTheCommonThreshold<T, THasher>(CompactThetaSketch<T, THasher> expected, CompactThetaSketch<T, THasher> actual)
        where THasher : struct, IHashProvider<T>
    {
        Assert.Equal(0, CompactThetaSketch<T, THasher>.Except(expected, actual).RetainedCount);
        Assert.Equal(0, CompactThetaSketch<T, THasher>.Except(actual, expected).RetainedCount);
    }
}
