using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration coverage for <see cref="SegmentedList{T}"/>: the struct enumerator walks the segments front to
/// back (including a short last segment, retained segments past the count, and one-element segments),
/// honours the version guard on every structural mutation, and deliberately does <b>not</b> treat an indexer
/// set, a <c>Clear()</c> of an empty list, a <c>Remove(T)</c> that finds nothing, or an
/// <c>EnsureCapacity</c> / <c>TrimExcess</c> that changes no capacity as one. The boxed
/// <see cref="IEnumerable{T}"/> and <see cref="IEnumerable"/> paths agree with the struct fast path.
/// </summary>
public class SegmentedListEnumerationTests
{
    private static List<int> Enumerate(SegmentedList<int> list)
    {
        var items = new List<int>();
        foreach (int x in list)
            items.Add(x);
        return items;
    }

    [Fact]
    public void EmptyList_ShouldYieldNothing()
    {
        var list = new SegmentedList<int>();

        Assert.Empty(Enumerate(list));

        SegmentedList<int>.Enumerator e = list.GetEnumerator();
        Assert.False(e.MoveNext());
        Assert.Equal(0, e.Current);
    }

    [Fact]
    public void Enumeration_ShouldYieldFrontToBack_AcrossEverySegment()
    {
        List<int> oracle = Enumerable.Range(0, 3 * 16_384 + 17).ToList();
        var list = new SegmentedList<int>(oracle);

        Assert.Equal(oracle, Enumerate(list));
    }

    [Fact]
    public void Enumeration_ShouldStopAtTheCount_AndNotWalkRetainedSegments()
    {
        var list = new SegmentedList<SegmentedBig>();
        for (int i = 0; i < 13; i++)
            list.Add(SegmentedBig.Of(i));
        for (int i = 0; i < 6; i++)
            list.RemoveAt(list.Count - 1);

        // Seven elements, the second segment three-quarters full, a third segment retained behind it.
        Assert.Equal(16, list.Capacity);
        Assert.Equal(Enumerable.Range(0, 7), list.Select(b => b.Value));

        // And on an exact segment boundary, where the next segment is retained but empty.
        list.RemoveAt(6);
        list.RemoveAt(5);
        list.RemoveAt(4);
        Assert.Equal(Enumerable.Range(0, 4), list.Select(b => b.Value));
    }

    [Fact]
    public void Enumeration_ShouldVisitOneElementPerSegment_WhenTheElementExceedsTheBudget()
    {
        var list = new SegmentedList<SegmentedHuge>();
        for (int i = 0; i < 5; i++)
            list.Add(SegmentedHuge.Of(i));

        var seen = new List<int>();
        foreach (SegmentedHuge h in list)
            seen.Add(h.Value);

        Assert.Equal(Enumerable.Range(0, 5), seen);
    }

    [Fact]
    public void Enumeration_ShouldReflectShiftsAcrossSegments_AfterInsertsAndRemovals()
    {
        var list = new SegmentedList<SegmentedBig>();
        var oracle = new List<int>();
        for (int i = 0; i < 60; i++)
        {
            int at = i % 3 == 0 ? 0 : oracle.Count / 2;
            list.Insert(at, SegmentedBig.Of(i));
            oracle.Insert(at, i);
        }
        Assert.Equal(oracle, list.Select(b => b.Value));

        for (int i = 0; i < 45; i++)
        {
            int at = i % 2 == 0 ? 0 : oracle.Count / 3;
            list.RemoveAt(at);
            oracle.RemoveAt(at);
        }
        Assert.Equal(oracle, list.Select(b => b.Value));
    }

    [Theory]
    [InlineData("Add")]
    [InlineData("AddRange")]
    [InlineData("InsertFront")]
    [InlineData("InsertEnd")]
    [InlineData("RemoveAt")]
    [InlineData("RemoveFound")]
    [InlineData("Clear")]
    [InlineData("EnsureCapacityGrows")]
    [InlineData("TrimExcessReleases")]
    public void StructuralMutation_ShouldInvalidateTheEnumerator(string mutation)
    {
        var list = new SegmentedList<int>();
        for (int i = 0; i < 300; i++)
            list.Add(i);
        SegmentedList<int>.Enumerator e = list.GetEnumerator();
        Assert.True(e.MoveNext());

        switch (mutation)
        {
            case "Add": list.Add(-1); break;
            case "AddRange": list.AddRange(new[] { -1, -2 }); break;
            case "InsertFront": list.Insert(0, -1); break;
            case "InsertEnd": list.Insert(list.Count, -1); break;
            case "RemoveAt": list.RemoveAt(150); break;
            case "RemoveFound": Assert.True(list.Remove(299)); break;
            case "Clear": list.Clear(); break;
            case "EnsureCapacityGrows": list.EnsureCapacity(100_000); break;
            case "TrimExcessReleases": list.TrimExcess(); break;
        }

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => e.MoveNext());
        Assert.Contains("Collection was modified", ex.Message);
    }

    [Fact]
    public void StructuralMutation_InsideForeach_ShouldThrow()
    {
        var list = new SegmentedList<int>(new[] { 1, 2 });

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (int x in list)
                list.Add(x);
        });
    }

    [Fact]
    public void IndexerSet_ShouldNotInvalidateTheEnumerator_AndShouldBeSeenAhead()
    {
        // An in-place element replacement moves no segment boundary, so it is not structural — the rule
        // Deque<T> and BTreeList<T> follow, and the one place this type deliberately differs from List<T>.
        var list = new SegmentedList<SegmentedBig>();
        for (int i = 0; i < 12; i++)
            list.Add(SegmentedBig.Of(i));
        var seen = new List<int>();

        foreach (SegmentedBig b in list)
        {
            if (seen.Count == 0)
            {
                list[0] = SegmentedBig.Of(-1);    // behind the cursor
                list[9] = SegmentedBig.Of(-9);    // ahead of it, in another segment
            }
            seen.Add(b.Value);
        }

        Assert.Equal(12, seen.Count);
        Assert.Equal(0, seen[0]);
        Assert.Equal(-9, seen[9]);
    }

    [Fact]
    public void Clear_ShouldNotInvalidateTheEnumerator_WhenTheListIsAlreadyEmpty()
    {
        var list = new SegmentedList<int>();
        SegmentedList<int>.Enumerator neverPopulated = list.GetEnumerator();
        list.Clear();
        Assert.False(neverPopulated.MoveNext());

        // Drained by removal rather than by Clear(), with the segment retained: still a no-op.
        list.Add(1);
        list.RemoveAt(0);
        SegmentedList<int>.Enumerator afterDrain = list.GetEnumerator();
        list.Clear();
        Assert.False(afterDrain.MoveNext());
    }

    [Fact]
    public void Remove_ShouldNotInvalidateTheEnumerator_WhenTheItemIsNotFound()
    {
        var list = new SegmentedList<int>(new[] { 1, 2, 3 });
        SegmentedList<int>.Enumerator e = list.GetEnumerator();
        Assert.True(e.MoveNext());

        Assert.False(list.Remove(42));

        Assert.True(e.MoveNext());
        Assert.Equal(2, e.Current);
    }

    [Fact]
    public void CapacityCallsThatChangeNothing_ShouldNotInvalidateTheEnumerator()
    {
        var list = new SegmentedList<int>(new[] { 1, 2, 3, 4 });
        SegmentedList<int>.Enumerator e = list.GetEnumerator();
        Assert.True(e.MoveNext());

        list.EnsureCapacity(2);
        list.TrimExcess(); // the only segment is already exactly full

        Assert.True(e.MoveNext());
        Assert.Equal(2, e.Current);
    }

    [Fact]
    public void SearchAndRead_ShouldNotInvalidateTheEnumerator()
    {
        var list = new SegmentedList<int>(new[] { 1, 2, 3 });
        SegmentedList<int>.Enumerator e = list.GetEnumerator();
        Assert.True(e.MoveNext());

        _ = list[2];
        _ = list.IndexOf(3);
        _ = list.Contains(9);
        _ = list.ToArray();
        _ = list.GetSegment(0);
        list.CopyTo(new int[3], 0);

        Assert.True(e.MoveNext());
        Assert.Equal(2, e.Current);
    }

    [Fact]
    public void Current_ShouldBeDefault_BeforeTheFirstMoveNextAndAfterTheEnd()
    {
        var list = new SegmentedList<string>(new[] { "a" });
        SegmentedList<string>.Enumerator e = list.GetEnumerator();

        Assert.Null(e.Current);
        Assert.True(e.MoveNext());
        Assert.Equal("a", e.Current);
        Assert.False(e.MoveNext());
        Assert.Null(e.Current);
    }

    [Fact]
    public void MoveNext_PastTheEnd_ShouldStayFalse_UntilTheListIsModified()
    {
        var list = new SegmentedList<int>(Enumerable.Range(0, 16_384 + 5));
        SegmentedList<int>.Enumerator e = list.GetEnumerator();
        while (e.MoveNext())
        {
        }

        Assert.False(e.MoveNext());
        Assert.False(e.MoveNext());
        Assert.Equal(0, e.Current);

        // The version check runs ahead of the exhausted state, so a later modification is still reported.
        list.Add(200);
        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void Reset_ShouldRestartFromTheFront_WhenTheListIsUnchanged()
    {
        var list = new SegmentedList<SegmentedBig>();
        for (int i = 0; i < 11; i++)
            list.Add(SegmentedBig.Of(i));
        SegmentedList<SegmentedBig>.Enumerator e = list.GetEnumerator();
        for (int i = 0; i < 7; i++)
            Assert.True(e.MoveNext());
        Assert.Equal(6, e.Current.Value);

        e.Reset();

        Assert.Equal(0, e.Current.Value);
        var replay = new List<int>();
        while (e.MoveNext())
            replay.Add(e.Current.Value);
        Assert.Equal(Enumerable.Range(0, 11), replay);

        // And again after running off the end.
        e.Reset();
        Assert.True(e.MoveNext());
        Assert.Equal(0, e.Current.Value);
    }

    [Fact]
    public void Reset_ShouldThrow_WhenTheListWasModified()
    {
        var list = new SegmentedList<int>(new[] { 1, 2 });
        SegmentedList<int>.Enumerator e = list.GetEnumerator();
        Assert.True(e.MoveNext());

        list.RemoveAt(0);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => e.Reset());
        Assert.Contains("Collection was modified", ex.Message);
    }

    [Fact]
    public void NonGenericCurrent_ShouldBoxTheCurrentElement()
    {
        IEnumerator e = new SegmentedList<int>(new[] { 7, 8 }).GetEnumerator();

        Assert.True(e.MoveNext());
        Assert.Equal(7, e.Current);
        Assert.True(e.MoveNext());
        Assert.Equal(8, e.Current);
        Assert.False(e.MoveNext());
        Assert.Equal(0, e.Current);

        e.Reset();
        Assert.True(e.MoveNext());
        Assert.Equal(7, e.Current);
    }

    [Fact]
    public void BoxedEnumerables_ShouldAgreeWithTheStructPath()
    {
        List<int> oracle = Enumerable.Range(0, 16_384 + 500).ToList();
        var list = new SegmentedList<int>(oracle);

        IEnumerable<int> boxed = list;
        Assert.Equal(oracle, boxed.ToList());

        using (IEnumerator<int> generic = boxed.GetEnumerator())
        {
            Assert.True(generic.MoveNext());
            Assert.Equal(0, generic.Current);
        }

        var viaNonGeneric = new List<int>();
        foreach (object? x in (IEnumerable)list)
            viaNonGeneric.Add((int)x!);
        Assert.Equal(oracle, viaNonGeneric);
    }
}
