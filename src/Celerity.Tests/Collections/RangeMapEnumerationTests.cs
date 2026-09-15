using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// The enumeration surface of <see cref="RangeMap{TKey, TValue, TComparer}"/>: the whole-map struct
/// enumerator, the <see cref="RangeMap{TKey, TValue, TComparer}.EnumerateOverlapping"/> view, fail-fast on a
/// real write — including after the walk has finished — survival across every no-op write, <c>Reset</c>, and
/// the non-generic interface members.
/// </summary>
public class RangeMapEnumerationTests
{
    private static RangeMap<int, string> Sample()
    {
        var map = new RangeMap<int, string>();
        map.Set(0, 10, "a");
        map.Set(10, 20, "b");
        map.Set(30, 40, "c");
        map.Set(40, 45, "d");
        return map;
    }

    private static (int, int, string?)[] Flatten(IEnumerable<Interval<int, string>> ranges) =>
        ranges.Select(r => (r.Start, r.End, r.Value)).ToArray();

    // ---- the whole-map enumerator ----------------------------------------------------------------------

    [Fact]
    public void Enumerator_ShouldYieldRangesInAscendingOrder()
    {
        RangeMap<int, string> map = Sample();

        var seen = new List<(int, int, string?)>();
        foreach (Interval<int, string> range in map)
            seen.Add((range.Start, range.End, range.Value));

        Assert.Equal(new[] { (0, 10, (string?)"a"), (10, 20, "b"), (30, 40, "c"), (40, 45, "d") }, seen);
    }

    [Fact]
    public void Enumerator_ShouldBeReachableThroughEveryInterface()
    {
        RangeMap<int, string> map = Sample();

        IEnumerable<Interval<int, string>> generic = map;
        Assert.Equal(4, generic.Count());

        IEnumerator nonGeneric = ((IEnumerable)map).GetEnumerator();
        Assert.True(nonGeneric.MoveNext());
        var first = Assert.IsType<Interval<int, string>>(nonGeneric.Current);
        Assert.Equal(0, first.Start);
    }

    [Fact]
    public void Enumerator_ShouldReturnDefaultCurrent_OnceExhausted()
    {
        var map = new RangeMap<int, string>();
        map.Set(0, 1, "a");

        RangeMap<int, string, DefaultComparer<int>>.Enumerator e = map.GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.False(e.MoveNext());
        Assert.Equal(default, e.Current);
        Assert.False(e.MoveNext());
    }

    [Fact]
    public void Enumerator_Reset_ShouldRestartTheWalk()
    {
        RangeMap<int, string> map = Sample();

        IEnumerator<Interval<int, string>> e = map.GetEnumerator();
        while (e.MoveNext())
        {
        }

        e.Reset();
        Assert.True(e.MoveNext());
        Assert.Equal(0, e.Current.Start);
        e.Dispose();
    }

    [Fact]
    public void Enumerator_ShouldThrow_WhenTheMapIsWrittenDuringTheWalk()
    {
        RangeMap<int, string> map = Sample();
        RangeMap<int, string, DefaultComparer<int>>.Enumerator e = map.GetEnumerator();
        Assert.True(e.MoveNext());

        map.Set(100, 110, "z");

        var ex = Assert.Throws<InvalidOperationException>(() => e.MoveNext());
        Assert.Contains("modified", ex.Message);
    }

    [Fact]
    public void Enumerator_ShouldThrow_WhenTheMapIsWrittenAfterTheWalkFinished()
    {
        RangeMap<int, string> map = Sample();
        RangeMap<int, string, DefaultComparer<int>>.Enumerator e = map.GetEnumerator();
        while (e.MoveNext())
        {
        }

        map.Remove(0, 5);

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void Enumerator_Reset_ShouldThrow_AfterAWrite()
    {
        RangeMap<int, string> map = Sample();
        RangeMap<int, string, DefaultComparer<int>>.Enumerator e = map.GetEnumerator();

        map.Clear();

        Assert.Throws<InvalidOperationException>(() => e.Reset());
    }

    [Fact]
    public void Enumerator_ShouldSurviveEveryWriteThatChangesNothing()
    {
        RangeMap<int, string> map = Sample();
        RangeMap<int, string, DefaultComparer<int>>.Enumerator e = map.GetEnumerator();
        Assert.True(e.MoveNext());

        map.Set(2, 8, "a");       // already covered by an equal value
        map.Set(5, 5, "z");       // empty range
        map.Remove(20, 30);       // the gap: holds nothing
        map.Remove(7, 7);         // empty range
        Assert.False(map.Overlaps(20, 30));
        Assert.True(map.TryGetValue(3, out _));

        int remaining = 0;
        while (e.MoveNext())
            remaining++;

        Assert.Equal(3, remaining);
    }

    [Fact]
    public void Enumerator_ShouldSurviveClearingAnEmptyMap()
    {
        var map = new RangeMap<int, string>();
        RangeMap<int, string, DefaultComparer<int>>.Enumerator e = map.GetEnumerator();

        map.Clear();

        Assert.False(e.MoveNext());
    }

    // ---- EnumerateOverlapping --------------------------------------------------------------------------

    [Theory]
    [InlineData(-10, 0)]     // ends where the first range begins
    [InlineData(20, 30)]     // the gap, touching both seams
    [InlineData(45, 60)]     // begins where the last range ends
    [InlineData(25, 25)]     // empty window in the gap
    [InlineData(5, 5)]       // empty window inside a range
    [InlineData(10, 10)]     // empty window on a seam
    public void EnumerateOverlapping_ShouldYieldNothing_ForAWindowOverlappingNothing(int start, int end)
    {
        RangeMap<int, string> map = Sample();

        Assert.Empty(map.EnumerateOverlapping(start, end));
        Assert.False(map.Overlaps(start, end));
    }

    [Fact]
    public void EnumerateOverlapping_ShouldReportStraddlingRangesWhole()
    {
        RangeMap<int, string> map = Sample();

        Assert.Equal(
            new[] { (0, 10, (string?)"a"), (10, 20, "b") },
            Flatten(map.EnumerateOverlapping(5, 15)));
    }

    [Fact]
    public void EnumerateOverlapping_ShouldSkipTheRangeEndingExactlyAtTheWindowStart()
    {
        RangeMap<int, string> map = Sample();

        Assert.Equal(
            new[] { (10, 20, (string?)"b"), (30, 40, "c") },
            Flatten(map.EnumerateOverlapping(10, 35)));
    }

    [Fact]
    public void EnumerateOverlapping_ShouldIncludeTheRangeEndingExactlyAtTheWindowEnd()
    {
        RangeMap<int, string> map = Sample();

        Assert.Equal(
            new[] { (10, 20, (string?)"b"), (30, 40, "c") },
            Flatten(map.EnumerateOverlapping(15, 40)));
    }

    [Fact]
    public void EnumerateOverlapping_ShouldYieldEverything_ForAnUnboundedWindow()
    {
        RangeMap<int, string> map = Sample();

        Assert.Equal(Flatten(map), Flatten(map.EnumerateOverlapping(int.MinValue, int.MaxValue)));
    }

    [Fact]
    public void EnumerateOverlapping_ShouldYieldTheOneRangeContainingAWindow()
    {
        RangeMap<int, string> map = Sample();

        Assert.Equal(new[] { (30, 40, (string?)"c") }, Flatten(map.EnumerateOverlapping(32, 38)));
    }

    [Fact]
    public void EnumerateOverlapping_ShouldThrow_WhenEndOrdersBeforeStart()
    {
        RangeMap<int, string> map = Sample();

        var ex = Assert.Throws<ArgumentException>(() => map.EnumerateOverlapping(10, 5));
        Assert.Equal("end", ex.ParamName);
    }

    [Fact]
    public void OverlapEnumerator_ShouldStayExhausted_AndReturnDefaultCurrent()
    {
        RangeMap<int, string> map = Sample();
        RangeMap<int, string, DefaultComparer<int>>.OverlapEnumerator e = map.EnumerateOverlapping(32, 38).GetEnumerator();

        Assert.True(e.MoveNext());
        Assert.False(e.MoveNext());
        Assert.Equal(default, e.Current);
        Assert.False(e.MoveNext());
    }

    [Fact]
    public void OverlapEnumerator_Reset_ShouldRestartTheWalk()
    {
        RangeMap<int, string> map = Sample();
        IEnumerator<Interval<int, string>> e = map.EnumerateOverlapping(5, 35).GetEnumerator();

        int first = 0;
        while (e.MoveNext())
            first++;

        e.Reset();
        int second = 0;
        while (e.MoveNext())
            second++;

        Assert.Equal(3, first);
        Assert.Equal(first, second);
        e.Dispose();
    }

    [Fact]
    public void OverlapEnumerator_ShouldBeReachableThroughEveryInterface()
    {
        RangeMap<int, string> map = Sample();

        IEnumerable<Interval<int, string>> generic = map.EnumerateOverlapping(0, 100);
        Assert.Equal(4, generic.Count());

        IEnumerator nonGeneric = ((IEnumerable)map.EnumerateOverlapping(0, 100)).GetEnumerator();
        Assert.True(nonGeneric.MoveNext());
        var first = Assert.IsType<Interval<int, string>>(nonGeneric.Current);
        Assert.Equal("a", first.Value);
    }

    [Fact]
    public void OverlapEnumerator_ShouldThrow_WhenTheMapIsWritten()
    {
        RangeMap<int, string> map = Sample();
        RangeMap<int, string, DefaultComparer<int>>.OverlapEnumerator e = map.EnumerateOverlapping(0, 100).GetEnumerator();
        Assert.True(e.MoveNext());

        map.Set(50, 60, "z");

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
        Assert.Throws<InvalidOperationException>(() => e.Reset());
    }

    [Fact]
    public void OverlapEnumerator_ShouldSurviveAWriteThatChangesNothing()
    {
        RangeMap<int, string> map = Sample();
        RangeMap<int, string, DefaultComparer<int>>.OverlapEnumerator e = map.EnumerateOverlapping(0, 100).GetEnumerator();
        Assert.True(e.MoveNext());

        map.Set(31, 39, "c");
        Assert.False(map.Remove(20, 30));

        int remaining = 0;
        while (e.MoveNext())
            remaining++;

        Assert.Equal(3, remaining);
    }
}
