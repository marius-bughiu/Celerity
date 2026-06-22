using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

public class CountMinSketchTests
{
    [Fact]
    public void Add_ThenEstimate_ReturnsAtLeastTheTrueCount()
    {
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>();
        for (int i = 0; i < 5; i++)
            sketch.Add(42);

        Assert.True(sketch.EstimateCount(42) >= 5);
    }

    [Fact]
    public void Estimate_NeverUnderestimates_ForEveryAddedElement()
    {
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>();
        var truth = new Dictionary<int, int>();
        var rand = new Random(123);

        for (int i = 0; i < 5000; i++)
        {
            int key = rand.Next(0, 500);
            sketch.Add(key);
            truth[key] = truth.GetValueOrDefault(key) + 1;
        }

        foreach (var (key, count) in truth)
            Assert.True(sketch.EstimateCount(key) >= count, $"underestimate for {key}");
    }

    [Fact]
    public void EmptySketch_EstimatesZero()
    {
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>();
        Assert.Equal(0, sketch.EstimateCount(42));
        Assert.Equal(0, sketch.TotalCount);
    }

    [Fact]
    public void AddWithCount_IncrementsByThatAmount()
    {
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>();
        sketch.Add(7, 100);
        Assert.True(sketch.EstimateCount(7) >= 100);
        Assert.Equal(100, sketch.TotalCount);
    }

    [Fact]
    public void Add_AccumulatesAcrossBothOverloads()
    {
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>();
        sketch.Add(1);       // +1
        sketch.Add(1, 9);    // +9
        Assert.True(sketch.EstimateCount(1) >= 10);
        Assert.Equal(10, sketch.TotalCount);
    }

    [Fact]
    public void AddWithCount_Zero_Throws()
    {
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>();
        Assert.Throws<ArgumentOutOfRangeException>(() => sketch.Add(1, 0));
    }

    [Fact]
    public void AddWithCount_Negative_Throws()
    {
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>();
        Assert.Throws<ArgumentOutOfRangeException>(() => sketch.Add(1, -5));
    }

    [Fact]
    public void TotalCount_TracksSumOfAllCounts()
    {
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>();
        sketch.Add(1);
        sketch.Add(2, 3);
        sketch.Add(3, 6);
        Assert.Equal(1 + 3 + 6, sketch.TotalCount);
    }

    [Fact]
    public void ZeroIntElement_IsAnOrdinaryElement()
    {
        // Unlike the hash-table collections, a Count-Min sketch has no empty-slot
        // sentinel, so default(int) == 0 needs no out-of-band handling.
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>();
        sketch.Add(0, 4);
        Assert.True(sketch.EstimateCount(0) >= 4);
    }

    [Fact]
    public void GuidEmpty_IsAnOrdinaryElement()
    {
        var sketch = new CountMinSketch<Guid, GuidHasher>();
        sketch.Add(Guid.Empty, 2);
        Assert.True(sketch.EstimateCount(Guid.Empty) >= 2);
    }

    [Fact]
    public void NullReference_IsAddable_WithoutInvokingTheHasher()
    {
        // StringFnV1AHasher throws on null; CountMinSketch maps a null reference to a
        // fixed base hash so it never calls the hasher with null.
        var sketch = new CountMinSketch<string, StringFnV1AHasher>();
        sketch.Add(null!);
        sketch.Add(null!);
        Assert.True(sketch.EstimateCount(null!) >= 2);
    }

    [Fact]
    public void StringElements_AreCounted()
    {
        var sketch = new CountMinSketch<string, StringMurmur3Hasher>();
        sketch.Add("alice", 3);
        sketch.Add("bob");
        Assert.True(sketch.EstimateCount("alice") >= 3);
        Assert.True(sketch.EstimateCount("bob") >= 1);
    }

    [Fact]
    public void EmptyString_IsAnOrdinaryElement()
    {
        var sketch = new CountMinSketch<string, StringFnV1AHasher>();
        sketch.Add("", 5);
        Assert.True(sketch.EstimateCount("") >= 5);
    }

    [Fact]
    public void Clear_ResetsCountsAndTotal()
    {
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>();
        for (int i = 0; i < 50; i++)
            sketch.Add(i);

        sketch.Clear();

        Assert.Equal(0, sketch.TotalCount);
        for (int i = 0; i < 50; i++)
            Assert.Equal(0, sketch.EstimateCount(i));

        // Reusable after clear.
        sketch.Add(5, 2);
        Assert.True(sketch.EstimateCount(5) >= 2);
        Assert.Equal(2, sketch.TotalCount);
    }

    [Fact]
    public void Clear_OnEmptySketch_IsANoOp()
    {
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>();
        sketch.Clear();
        Assert.Equal(0, sketch.TotalCount);
    }

    // ---------------------------------------------------------------
    //  Sizing / properties
    // ---------------------------------------------------------------

    [Fact]
    public void Sizing_ChoosesPowerOfTwoWidthAndPositiveDepth()
    {
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>(0.01, 0.01);

        Assert.Equal(0.01, sketch.Epsilon);
        Assert.Equal(0.01, sketch.Delta);
        Assert.True(sketch.Width >= 4);
        // w must be a power of two.
        Assert.Equal(0, sketch.Width & (sketch.Width - 1));
        Assert.True(sketch.Depth >= 1);
    }

    [Fact]
    public void Sizing_SmallerEpsilon_WidensRows()
    {
        var loose = new CountMinSketch<int, Int32Murmur3Hasher>(0.1, 0.01);
        var tight = new CountMinSketch<int, Int32Murmur3Hasher>(0.001, 0.01);
        Assert.True(tight.Width > loose.Width);
    }

    [Fact]
    public void Sizing_SmallerDelta_AddsRows()
    {
        var loose = new CountMinSketch<int, Int32Murmur3Hasher>(0.01, 0.1);
        var tight = new CountMinSketch<int, Int32Murmur3Hasher>(0.01, 0.0001);
        Assert.True(tight.Depth > loose.Depth);
    }

    [Fact]
    public void Sizing_DeltaVeryCloseToOne_StillGivesAtLeastOneRow()
    {
        // When delta is so close to 1 that 1/delta rounds to exactly 1.0, ln(1/delta)
        // is 0 — the Math.Max floor keeps the depth at one usable row.
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>(0.5, 0.9999999999999999);
        Assert.True(sketch.Depth >= 1);
        sketch.Add(3, 7);
        Assert.True(sketch.EstimateCount(3) >= 7);
    }

    // ---------------------------------------------------------------
    //  IEnumerable constructor
    // ---------------------------------------------------------------

    [Fact]
    public void IEnumerableConstructor_CountsEachOccurrence()
    {
        var source = new[] { 1, 1, 1, 2, 2, 3 };
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>(source);

        Assert.Equal(6, sketch.TotalCount);
        Assert.True(sketch.EstimateCount(1) >= 3);
        Assert.True(sketch.EstimateCount(2) >= 2);
        Assert.True(sketch.EstimateCount(3) >= 1);
    }

    [Fact]
    public void IEnumerableConstructor_NonCollectionSource()
    {
        IEnumerable<int> source = Enumerable.Range(0, 2000).Where(i => true);
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>(source);
        Assert.Equal(2000, sketch.TotalCount);
        for (int i = 0; i < 2000; i++)
            Assert.True(sketch.EstimateCount(i) >= 1);
    }

    [Fact]
    public void IEnumerableConstructor_EmptySource_BuildsUsableSketch()
    {
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>(Array.Empty<int>());
        Assert.Equal(0, sketch.TotalCount);
        sketch.Add(1);
        Assert.True(sketch.EstimateCount(1) >= 1);
    }

    [Fact]
    public void IEnumerableConstructor_RespectsErrorParameters()
    {
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>(new[] { 1, 2, 3 }, 0.05, 0.05);
        Assert.Equal(0.05, sketch.Epsilon);
        Assert.Equal(0.05, sketch.Delta);
    }

    [Fact]
    public void IEnumerableConstructor_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new CountMinSketch<int, Int32Murmur3Hasher>((IEnumerable<int>)null!));
    }

    // Null source must beat out-of-range error parameters.
    [Fact]
    public void IEnumerableConstructor_NullSource_BeatsBadEpsilon()
    {
        Assert.Throws<ArgumentNullException>(
            () => new CountMinSketch<int, Int32Murmur3Hasher>((IEnumerable<int>)null!, 2.0));
    }

    // ---------------------------------------------------------------
    //  Constructor validation
    // ---------------------------------------------------------------

    [Fact]
    public void Constructor_Throws_WhenEpsilonIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CountMinSketch<int, Int32Murmur3Hasher>(0d, 0.01));
    }

    [Fact]
    public void Constructor_Throws_WhenEpsilonIsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CountMinSketch<int, Int32Murmur3Hasher>(1d, 0.01));
    }

    [Fact]
    public void Constructor_Throws_WhenEpsilonIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CountMinSketch<int, Int32Murmur3Hasher>(-0.1, 0.01));
    }

    [Fact]
    public void Constructor_Throws_WhenEpsilonExceedsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CountMinSketch<int, Int32Murmur3Hasher>(1.5, 0.01));
    }

    [Fact]
    public void Constructor_Throws_WhenEpsilonIsNaN()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CountMinSketch<int, Int32Murmur3Hasher>(double.NaN, 0.01));
    }

    [Fact]
    public void Constructor_Throws_WhenDeltaIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CountMinSketch<int, Int32Murmur3Hasher>(0.01, 0d));
    }

    [Fact]
    public void Constructor_Throws_WhenDeltaIsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CountMinSketch<int, Int32Murmur3Hasher>(0.01, 1d));
    }

    [Fact]
    public void Constructor_Throws_WhenDeltaIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CountMinSketch<int, Int32Murmur3Hasher>(0.01, -0.1));
    }

    [Fact]
    public void Constructor_Throws_WhenDeltaExceedsOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CountMinSketch<int, Int32Murmur3Hasher>(0.01, 1.5));
    }

    [Fact]
    public void Constructor_Throws_WhenDeltaIsNaN()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CountMinSketch<int, Int32Murmur3Hasher>(0.01, double.NaN));
    }

    // ---------------------------------------------------------------
    //  Grid-overflow guard (regression for the depth*width int overflow)
    // ---------------------------------------------------------------

    // A tiny epsilon clamps the width to 2^30; combined with an ordinary delta the row count
    // is >= 2, so depth * width overflows a 32-bit array length. Before the guard this threw a
    // confusing OverflowException (or allocated a wrong-sized grid); now it is a clear
    // ArgumentOutOfRangeException raised before any allocation.
    [Fact]
    public void Constructor_Throws_WhenTinyEpsilonAndDeltaOverflowTheGrid()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CountMinSketch<int, Int32Murmur3Hasher>(1e-9, 0.01));
    }

    // A delta small enough that 1/delta overflows to +Infinity saturates the double-to-int
    // depth cast to int.MaxValue on .NET Core 3.0+, so depth * width overflows for any width.
    // The guard computes the product in 64 bits, so it is caught rather than wrapping.
    [Fact]
    public void Constructor_Throws_WhenDeltaUnderflowsAndSaturatesDepth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CountMinSketch<int, Int32Murmur3Hasher>(0.5, double.Epsilon));
    }

    // The overflow guard blames the delta parameter (depth is the unbounded dimension; width
    // is already individually capped at 2^30).
    [Fact]
    public void Constructor_GridOverflow_NamesDeltaParameter()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new CountMinSketch<int, Int32Murmur3Hasher>(1e-9, 0.01));
        Assert.Equal("delta", ex.ParamName);
    }

    // Aggressive-but-representable parameters that stay under the 2^30 grid ceiling must still
    // build a working sketch — the guard rejects only genuinely oversized grids.
    [Fact]
    public void Constructor_AggressiveButValidParameters_BuildUsableSketch()
    {
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>(1e-4, 1e-6);
        Assert.True((long)sketch.Depth * sketch.Width <= (1 << 30));
        sketch.Add(7, 3);
        Assert.True(sketch.EstimateCount(7) >= 3);
    }

    // ---------------------------------------------------------------
    //  UnionWith
    // ---------------------------------------------------------------

    [Fact]
    public void UnionWith_MergesFrequencies()
    {
        var a = new CountMinSketch<int, Int32Murmur3Hasher>();
        var b = new CountMinSketch<int, Int32Murmur3Hasher>();

        a.Add(1, 5);
        b.Add(1, 7);
        b.Add(2, 3);

        a.UnionWith(b);

        Assert.True(a.EstimateCount(1) >= 12);
        Assert.True(a.EstimateCount(2) >= 3);
        Assert.Equal(15, a.TotalCount);
    }

    [Fact]
    public void UnionWith_LeavesOtherUnmodified()
    {
        var a = new CountMinSketch<int, Int32Murmur3Hasher>();
        var b = new CountMinSketch<int, Int32Murmur3Hasher>();
        a.Add(1, 2);
        b.Add(2, 4);

        a.UnionWith(b);

        Assert.Equal(4, b.TotalCount);
        Assert.True(b.EstimateCount(2) >= 4);
    }

    [Fact]
    public void UnionWith_Null_Throws()
    {
        var a = new CountMinSketch<int, Int32Murmur3Hasher>();
        Assert.Throws<ArgumentNullException>(() => a.UnionWith(null!));
    }

    [Fact]
    public void UnionWith_IncompatibleWidth_Throws()
    {
        var a = new CountMinSketch<int, Int32Murmur3Hasher>(0.1, 0.01);
        var b = new CountMinSketch<int, Int32Murmur3Hasher>(0.001, 0.01); // wider rows
        Assert.Throws<ArgumentException>(() => a.UnionWith(b));
    }

    [Fact]
    public void UnionWith_IncompatibleDepth_Throws()
    {
        var a = new CountMinSketch<int, Int32Murmur3Hasher>(0.01, 0.1);
        var b = new CountMinSketch<int, Int32Murmur3Hasher>(0.01, 0.0001); // more rows
        Assert.Throws<ArgumentException>(() => a.UnionWith(b));
    }

    // ---------------------------------------------------------------
    //  Counter overflow saturation (regression for #219)
    // ---------------------------------------------------------------

    [Fact]
    public void Add_CounterOverflow_SaturatesInsteadOfWrappingNegative()
    {
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>();

        sketch.Add(7, long.MaxValue);
        sketch.Add(7, 1); // would wrap a touched counter to long.MinValue if unchecked

        long estimate = sketch.EstimateCount(7);
        Assert.True(estimate >= 0, "estimate underflowed to a negative value");
        Assert.Equal(long.MaxValue, estimate);
    }

    [Fact]
    public void Add_TotalCountOverflow_SaturatesInsteadOfWrappingNegative()
    {
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>();

        sketch.Add(1, long.MaxValue);
        sketch.Add(2, long.MaxValue); // distinct element, so TotalCount itself overflows

        Assert.True(sketch.TotalCount >= 0, "TotalCount wrapped negative");
        Assert.Equal(long.MaxValue, sketch.TotalCount);
    }

    [Fact]
    public void Add_NeverUnderestimates_AfterSaturation()
    {
        // After a counter saturates, every subsequent estimate for that element must still
        // be >= its true (clamped) count — the never-underestimate guarantee must survive
        // the clamp.
        var sketch = new CountMinSketch<int, Int32Murmur3Hasher>();

        sketch.Add(99, long.MaxValue - 10);
        for (int i = 0; i < 100; i++)
            sketch.Add(99, 1); // pushes the counter past the ceiling

        Assert.Equal(long.MaxValue, sketch.EstimateCount(99));
    }

    [Fact]
    public void UnionWith_CounterOverflow_SaturatesInsteadOfWrappingNegative()
    {
        var a = new CountMinSketch<int, Int32Murmur3Hasher>();
        var b = new CountMinSketch<int, Int32Murmur3Hasher>();

        // Two aligned counters each holding more than half of long.MaxValue overflow when
        // summed during the merge.
        a.Add(5, long.MaxValue - 1);
        b.Add(5, long.MaxValue - 1);

        a.UnionWith(b);

        Assert.True(a.EstimateCount(5) >= 0, "merged estimate wrapped negative");
        Assert.Equal(long.MaxValue, a.EstimateCount(5));
        Assert.True(a.TotalCount >= 0, "merged TotalCount wrapped negative");
        Assert.Equal(long.MaxValue, a.TotalCount);
    }
}
