using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural tests for <see cref="RangeMap{TKey, TValue, TComparer}"/>: the split at each edge of an
/// assignment, the merge with an equal neighbour, the no-op writes, the half-open seams, and the constructor
/// and argument contracts. The randomized reconciliation against a dense model lives in
/// <see cref="RangeMapDifferentialTests"/>; these pin each rule by name so a failure says which one broke.
/// </summary>
public class RangeMapTests
{
    private static (int Start, int End, string? Value)[] Ranges<TComparer>(RangeMap<int, string, TComparer> map)
        where TComparer : struct, IComparer<int> =>
        map.Select(r => (r.Start, r.End, r.Value)).ToArray();

    // ---- the empty map ---------------------------------------------------------------------------------

    [Fact]
    public void EmptyMap_ShouldMapNothing()
    {
        var map = new RangeMap<int, string>();

        Assert.Equal(0, map.Count);
        Assert.False(map.TryGetValue(0, out string? value));
        Assert.Null(value);
        Assert.False(map.ContainsKey(0));
        Assert.False(map.TryGetRange(0, out Interval<int, string> range));
        Assert.Equal(default, range);
        Assert.False(map.Overlaps(int.MinValue, int.MaxValue));
        Assert.Empty(map.EnumerateOverlapping(int.MinValue, int.MaxValue));
        Assert.Empty(map);
    }

    [Fact]
    public void Indexer_ShouldThrow_WhenNoRangeContainsTheKey()
    {
        var map = new RangeMap<int, string>();
        map.Set(0, 10, "a");

        Assert.Equal("a", map[0]);
        Assert.Equal("a", map[9]);
        var ex = Assert.Throws<KeyNotFoundException>(() => map[10]);
        Assert.Contains("10", ex.Message);
    }

    // ---- Set: the half-open seams ----------------------------------------------------------------------

    [Fact]
    public void Set_ShouldMapTheStartButNotTheEnd()
    {
        var map = new RangeMap<int, string>();
        map.Set(10, 20, "a");

        Assert.False(map.ContainsKey(9));
        Assert.True(map.ContainsKey(10));
        Assert.True(map.ContainsKey(19));
        Assert.False(map.ContainsKey(20));
        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void Set_ShouldKeepTouchingRangesWithDifferentValuesApart()
    {
        var map = new RangeMap<int, string>();
        map.Set(0, 10, "a");
        map.Set(10, 20, "b");

        Assert.Equal(new[] { (0, 10, (string?)"a"), (10, 20, "b") }, Ranges(map));
        Assert.Equal("a", map[9]);
        Assert.Equal("b", map[10]);
    }

    // ---- Set: overwriting and splitting ----------------------------------------------------------------

    [Fact]
    public void Set_ShouldSplitARangeThatContainsTheAssignment()
    {
        var map = new RangeMap<int, string>();
        map.Set(0, 30, "a");
        map.Set(10, 20, "b");

        Assert.Equal(new[] { (0, 10, (string?)"a"), (10, 20, "b"), (20, 30, "a") }, Ranges(map));
    }

    [Fact]
    public void Set_ShouldTrimARangeStraddlingTheLeftEdge()
    {
        var map = new RangeMap<int, string>();
        map.Set(0, 15, "a");
        map.Set(10, 20, "b");

        Assert.Equal(new[] { (0, 10, (string?)"a"), (10, 20, "b") }, Ranges(map));
    }

    [Fact]
    public void Set_ShouldTrimARangeStraddlingTheRightEdge()
    {
        var map = new RangeMap<int, string>();
        map.Set(15, 30, "a");
        map.Set(10, 20, "b");

        Assert.Equal(new[] { (10, 20, (string?)"b"), (20, 30, "a") }, Ranges(map));
    }

    [Fact]
    public void Set_ShouldReplaceEveryRangeItCovers()
    {
        var map = new RangeMap<int, string>();
        map.Set(0, 5, "a");
        map.Set(6, 8, "b");
        map.Set(8, 9, "c");
        map.Set(12, 20, "d");
        map.Set(25, 30, "e");

        map.Set(2, 15, "z");

        Assert.Equal(new[] { (0, 2, (string?)"a"), (2, 15, "z"), (15, 20, "d"), (25, 30, "e") }, Ranges(map));
    }

    [Fact]
    public void Set_ShouldReplaceARangeEndingExactlyAtTheAssignmentEnd()
    {
        var map = new RangeMap<int, string>();
        map.Set(5, 10, "a");
        map.Set(10, 12, "b");
        map.Set(0, 10, "z");

        Assert.Equal(new[] { (0, 10, (string?)"z"), (10, 12, "b") }, Ranges(map));
    }

    [Fact]
    public void Set_ShouldOverwriteAnIdenticalRangeWithADifferentValue()
    {
        var map = new RangeMap<int, string>();
        map.Set(0, 10, "a");
        map.Set(0, 10, "b");

        Assert.Equal(new[] { (0, 10, (string?)"b") }, Ranges(map));
    }

    [Fact]
    public void Set_ShouldFillAGapBetweenRanges()
    {
        var map = new RangeMap<int, string>();
        map.Set(0, 5, "a");
        map.Set(20, 25, "c");
        map.Set(10, 15, "b");

        Assert.Equal(new[] { (0, 5, (string?)"a"), (10, 15, "b"), (20, 25, "c") }, Ranges(map));
    }

    // ---- Set: merging equal neighbours -----------------------------------------------------------------

    [Fact]
    public void Set_ShouldMergeWithAnEqualRangeEndingAtItsStart()
    {
        var map = new RangeMap<int, string>();
        map.Set(0, 10, "a");
        map.Set(10, 20, "a");

        Assert.Equal(new[] { (0, 20, (string?)"a") }, Ranges(map));
    }

    [Fact]
    public void Set_ShouldMergeWithAnEqualRangeBeginningAtItsEnd()
    {
        var map = new RangeMap<int, string>();
        map.Set(10, 20, "a");
        map.Set(0, 10, "a");

        Assert.Equal(new[] { (0, 20, (string?)"a") }, Ranges(map));
    }

    [Fact]
    public void Set_ShouldMergeWithEqualRangesOnBothSides()
    {
        var map = new RangeMap<int, string>();
        map.Set(0, 10, "a");
        map.Set(20, 30, "a");
        map.Set(10, 20, "a");

        Assert.Equal(new[] { (0, 30, (string?)"a") }, Ranges(map));
        Assert.True(map.TryGetRange(15, out Interval<int, string> range));
        Assert.Equal((0, 30, (string?)"a"), (range.Start, range.End, range.Value));
    }

    [Fact]
    public void Set_ShouldMergeBackTheRemaindersOfAnEqualRangeItPartlyOverlaps()
    {
        var map = new RangeMap<int, string>();
        map.Set(0, 10, "a");
        map.Set(5, 15, "a");
        map.Set(-5, 3, "a");

        Assert.Equal(new[] { (-5, 15, (string?)"a") }, Ranges(map));
    }

    [Fact]
    public void Set_ShouldNotMergeWithAnEqualRangeAcrossAGap()
    {
        var map = new RangeMap<int, string>();
        map.Set(0, 10, "a");
        map.Set(11, 20, "a");

        Assert.Equal(2, map.Count);
        Assert.False(map.ContainsKey(10));
    }

    [Fact]
    public void Set_ShouldNotMergeWithADifferentValueOnEitherSide()
    {
        var map = new RangeMap<int, string>();
        map.Set(0, 10, "a");
        map.Set(20, 30, "c");
        map.Set(10, 20, "b");

        Assert.Equal(3, map.Count);
    }

    [Fact]
    public void Set_ShouldSplitOffTheDifferentValuedRemainderNextToAnEqualNeighbour()
    {
        // The assignment's right edge lands inside a different-valued range whose left part it overwrites.
        // The remainder begins exactly at the assignment's end but must not merge, because its value differs.
        var map = new RangeMap<int, string>();
        map.Set(0, 10, "b");
        map.Set(5, 8, "a");

        Assert.Equal(new[] { (0, 5, (string?)"b"), (5, 8, "a"), (8, 10, "b") }, Ranges(map));
    }

    [Fact]
    public void Set_ShouldTreatNullValuesAsEqualForMerging()
    {
        var map = new RangeMap<int, string>();
        map.Set(0, 10, null);
        map.Set(10, 20, null);

        Assert.Equal(new[] { (0, 20, (string?)null) }, Ranges(map));
        Assert.True(map.TryGetValue(5, out string? value));
        Assert.Null(value);
    }

    // ---- Set: the no-op writes -------------------------------------------------------------------------

    [Fact]
    public void Set_ShouldChangeNothing_ForAnEmptyRange()
    {
        var map = new RangeMap<int, string>();
        map.Set(5, 5, "a");

        Assert.Equal(0, map.Count);
        Assert.False(map.ContainsKey(5));
    }

    [Fact]
    public void Set_ShouldChangeNothing_WhenOneRangeAlreadyCoversItWithAnEqualValue()
    {
        var map = new RangeMap<int, string, DefaultComparer<int>>(default(DefaultComparer<int>), StringComparer.OrdinalIgnoreCase);
        map.Set(0, 10, "abc");

        using RangeMap<int, string, DefaultComparer<int>>.Enumerator live = map.GetEnumerator();
        map.Set(2, 8, "ABC");
        map.Set(0, 10, "Abc");

        // The held instance is kept, not replaced by an equal one, and the enumerator is still valid.
        Assert.Equal(new[] { (0, 10, (string?)"abc") }, Ranges(map));
        Assert.True(live.MoveNext());
    }

    [Theory]
    [InlineData(-5, 5)]    // starts before the covering range
    [InlineData(5, 15)]    // ends after it
    [InlineData(10, 12)]   // begins exactly where it ends
    [InlineData(20, 30)]   // lies entirely after it
    public void Set_ShouldWrite_WhenNoSingleRangeCoversTheAssignment(int start, int end)
    {
        var map = new RangeMap<int, string>();
        map.Set(0, 10, "a");

        using RangeMap<int, string, DefaultComparer<int>>.Enumerator live = map.GetEnumerator();
        map.Set(start, end, "a");

        Assert.Throws<InvalidOperationException>(() => live.MoveNext());
        for (int k = start; k < end; k++)
            Assert.Equal("a", map[k]);
    }

    [Fact]
    public void Set_MergedRange_ShouldCarryTheMostRecentlyStoredValue()
    {
        var map = new RangeMap<int, string, DefaultComparer<int>>(default(DefaultComparer<int>), StringComparer.OrdinalIgnoreCase);
        map.Set(0, 10, "abc");
        map.Set(20, 30, "abc");
        map.Set(10, 20, "ABC");

        Assert.Equal(new[] { (0, 30, (string?)"ABC") }, Ranges(map));
    }

    [Fact]
    public void Set_ShouldThrow_WhenEndOrdersBeforeStart()
    {
        var map = new RangeMap<int, string>();

        var ex = Assert.Throws<ArgumentException>(() => map.Set(10, 5, "a"));
        Assert.Equal("end", ex.ParamName);
        Assert.Equal(0, map.Count);
    }

    // ---- Remove ----------------------------------------------------------------------------------------

    [Fact]
    public void Remove_ShouldSplitARangeThatContainsIt()
    {
        var map = new RangeMap<int, string>();
        map.Set(0, 30, "a");

        Assert.True(map.Remove(10, 20));
        Assert.Equal(new[] { (0, 10, (string?)"a"), (20, 30, "a") }, Ranges(map));
    }

    [Fact]
    public void Remove_ShouldTrimStraddlersAndDropEverythingBetween()
    {
        var map = new RangeMap<int, string>();
        map.Set(0, 10, "a");
        map.Set(12, 14, "b");
        map.Set(16, 30, "c");

        Assert.True(map.Remove(5, 20));
        Assert.Equal(new[] { (0, 5, (string?)"a"), (20, 30, "c") }, Ranges(map));
    }

    [Fact]
    public void Remove_ShouldDropARangeEndingExactlyAtItsEnd()
    {
        var map = new RangeMap<int, string>();
        map.Set(0, 10, "a");
        map.Set(10, 20, "b");

        Assert.True(map.Remove(0, 10));
        Assert.Equal(new[] { (10, 20, (string?)"b") }, Ranges(map));
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenTheRangeHoldsNothing()
    {
        var map = new RangeMap<int, string>();
        Assert.False(map.Remove(0, 10));

        map.Set(0, 10, "a");
        map.Set(20, 30, "b");

        Assert.False(map.Remove(10, 20));   // the gap between them, touching both seams
        Assert.False(map.Remove(40, 50));   // past everything
        Assert.False(map.Remove(5, 5));     // empty
        Assert.Equal(2, map.Count);
    }

    [Fact]
    public void Remove_ShouldThrow_WhenEndOrdersBeforeStart()
    {
        var map = new RangeMap<int, string>();

        var ex = Assert.Throws<ArgumentException>(() => map.Remove(10, 5));
        Assert.Equal("end", ex.ParamName);
    }

    [Fact]
    public void Clear_ShouldRemoveEveryRange()
    {
        var map = new RangeMap<int, string>();
        map.Set(0, 10, "a");
        map.Set(20, 30, "b");

        map.Clear();

        Assert.Equal(0, map.Count);
        Assert.False(map.ContainsKey(5));
        Assert.Empty(map);
    }

    // ---- Overlaps and TryGetRange ----------------------------------------------------------------------

    [Fact]
    public void Overlaps_ShouldRespectTheHalfOpenSeams()
    {
        var map = new RangeMap<int, string>();
        map.Set(10, 20, "a");

        Assert.False(map.Overlaps(0, 10));    // ends where the range begins
        Assert.False(map.Overlaps(20, 30));   // begins where the range ends
        Assert.True(map.Overlaps(0, 11));
        Assert.True(map.Overlaps(19, 30));
        Assert.True(map.Overlaps(12, 13));
        Assert.True(map.Overlaps(0, 100));
        Assert.False(map.Overlaps(15, 15));   // an empty window overlaps nothing, even inside a range
    }

    [Fact]
    public void Overlaps_ShouldThrow_WhenEndOrdersBeforeStart()
    {
        var map = new RangeMap<int, string>();

        var ex = Assert.Throws<ArgumentException>(() => map.Overlaps(10, 5));
        Assert.Equal("end", ex.ParamName);
    }

    [Fact]
    public void TryGetRange_ShouldReturnTheWholeContainingRange()
    {
        var map = new RangeMap<int, string>();
        map.Set(0, 10, "a");
        map.Set(10, 20, "b");

        Assert.True(map.TryGetRange(10, out Interval<int, string> range));
        Assert.Equal((10, 20, (string?)"b"), (range.Start, range.End, range.Value));
        Assert.False(map.TryGetRange(20, out _));
        Assert.False(map.TryGetRange(-1, out _));
    }

    // ---- constructors ----------------------------------------------------------------------------------

    [Fact]
    public void SourceConstructor_ShouldApplyAssignmentsInOrder()
    {
        var map = new RangeMap<int, string>(new[]
        {
            new Interval<int, string>(0, 20, "a"),
            new Interval<int, string>(5, 10, "b"),
            new Interval<int, string>(10, 20, "a"),
            new Interval<int, string>(30, 30, "empty"),
        });

        Assert.Equal(new[] { (0, 5, (string?)"a"), (5, 10, "b"), (10, 20, "a") }, Ranges(map));
    }

    [Fact]
    public void SourceConstructor_ShouldAcceptAnUncountedSource()
    {
        var map = new RangeMap<int, int>(
            Enumerable.Range(0, 100).Select(i => new Interval<int, int>(i * 10, (i * 10) + 5, i)));

        Assert.Equal(100, map.Count);
        Assert.Equal(42, map[423]);
        Assert.False(map.ContainsKey(427));
    }

    [Fact]
    public void SourceConstructor_ShouldThrow_WhenAnIntervalIsInverted()
    {
        var source = new[] { new Interval<int, string>(0, 5, "a"), new Interval<int, string>(9, 3, "b") };

        var ex = Assert.Throws<ArgumentException>(() => new RangeMap<int, string>(source));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void ComparerConstructor_ShouldOrderKeysByTheSuppliedComparer()
    {
        // Descending: a range runs from a larger key down to a smaller one, and [10, 0) holds 10 through 1.
        var map = new RangeMap<int, string, DescendingInt>(default(DescendingInt));
        map.Set(10, 0, "a");
        map.Set(5, 3, "b");

        Assert.Equal("a", map[10]);
        Assert.Equal("b", map[5]);
        Assert.Equal("b", map[4]);
        Assert.Equal("a", map[3]);
        Assert.False(map.ContainsKey(0));
        Assert.False(map.ContainsKey(11));
        Assert.Equal(new[] { (10, 5, (string?)"a"), (5, 3, "b"), (3, 0, "a") }, Ranges(map));
        Assert.Throws<ArgumentException>(() => map.Set(0, 10, "x"));
    }

    [Fact]
    public void ComparerAndSourceConstructor_ShouldUseTheComparer()
    {
        var map = new RangeMap<int, string, DescendingInt>(
            new[] { new Interval<int, string>(10, 0, "a") }, default(DescendingInt));

        Assert.Equal(1, map.Count);
        Assert.Equal("a", map[1]);
        Assert.IsType<DescendingInt>(map.Comparer);
    }

    [Fact]
    public void ValueComparerConstructor_ShouldDefaultToEqualityComparerDefault_WhenNull()
    {
        var map = new RangeMap<int, string, DefaultComparer<int>>(default(DefaultComparer<int>), null);
        map.Set(0, 10, "a");
        map.Set(10, 20, "A");

        Assert.Equal(2, map.Count);
    }

    [Fact]
    public void Constructor_ShouldUseTheParameterlessComparer()
    {
        var map = new RangeMap<int, string, DefaultComparer<int>>();
        map.Set(0, 10, "a");

        Assert.Equal("a", map[0]);
    }

    // ---- key shapes ------------------------------------------------------------------------------------

    [Fact]
    public void NullKey_ShouldOrderBeforeEveryOtherKey()
    {
        var map = new RangeMap<string, int>();
        map.Set(null!, "m", 1);
        map.Set("m", "z", 2);

        Assert.Equal(1, map[null!]);
        Assert.Equal(1, map["apple"]);
        Assert.Equal(2, map["m"]);
        Assert.False(map.ContainsKey("z"));
    }

    [Fact]
    public void ContinuousKeys_ShouldMergeOnlyWhereRangesTouchExactly()
    {
        var map = new RangeMap<double, string>();
        map.Set(0.0, 1.5, "a");
        map.Set(1.5, 2.0, "a");
        map.Set(2.25, 3.0, "a");

        Assert.Equal(2, map.Count);
        Assert.True(map.TryGetRange(1.9, out Interval<double, string> range));
        Assert.Equal(0.0, range.Start);
        Assert.Equal(2.0, range.End);
        Assert.False(map.ContainsKey(2.1));
    }

    [Fact]
    public void ManyRanges_ShouldStayCorrectAcrossAMultiLevelTree()
    {
        // Alternating values keep every range distinct, so 5,000 of them build several B-tree levels.
        var map = new RangeMap<int, int>();
        for (int i = 0; i < 5000; i++)
            map.Set(i * 2, (i * 2) + 2, i % 2);

        Assert.Equal(5000, map.Count);
        Assert.Equal(1, map[2 * 1235]);   // range 1235 holds 1235 % 2

        // One write sweeps away most of them.
        map.Set(100, 9900, 7);
        Assert.Equal(50 + 1 + 50, map.Count);
        Assert.Equal(7, map[5000]);
        Assert.Equal(1, map[99]);      // range 49, [98, 100), untouched by the sweep
        Assert.Equal(0, map[9900]);    // range 4950, [9900, 9902), likewise
    }

    private readonly struct DescendingInt : IComparer<int>
    {
        public int Compare(int x, int y) => y.CompareTo(x);
    }
}
