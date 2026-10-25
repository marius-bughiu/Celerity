using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration coverage for <see cref="BTreeList{T}"/>: the struct enumerator walks the leaf chain front to
/// back (including across leaves that splits and merges have relinked), honours the version guard on every
/// structural mutation, and deliberately does <b>not</b> treat an indexer set, a <c>Clear()</c> of an empty
/// list or a <c>Remove(T)</c> that finds nothing as one. The boxed <see cref="IEnumerable{T}"/> and
/// <see cref="IEnumerable"/> paths agree with the struct fast path.
/// </summary>
public class BTreeListEnumerationTests
{
    private static List<int> Enumerate(BTreeList<int> list)
    {
        var items = new List<int>();
        foreach (int x in list)
            items.Add(x);
        return items;
    }

    [Fact]
    public void EmptyList_ShouldYieldNothing()
    {
        var list = new BTreeList<int>();

        Assert.Empty(Enumerate(list));

        BTreeList<int>.Enumerator e = list.GetEnumerator();
        Assert.False(e.MoveNext());
        Assert.Equal(0, e.Current);
    }

    [Fact]
    public void Enumeration_ShouldYieldFrontToBack_AcrossEveryLeaf()
    {
        List<int> oracle = Enumerable.Range(0, 10_000).ToList();
        var list = new BTreeList<int>(oracle);

        Assert.Equal(oracle, Enumerate(list));
    }

    [Fact]
    public void Enumeration_ShouldFollowTheRelinkedLeafChain_AfterSplitsAndMerges()
    {
        var oracle = new List<int>();
        var list = new BTreeList<int>();
        for (int i = 0; i < 2_000; i++)
        {
            int at = i % 3 == 0 ? 0 : oracle.Count / 2;
            list.Insert(at, i);
            oracle.Insert(at, i);
        }
        Assert.Equal(oracle, Enumerate(list));

        for (int i = 0; i < 1_500; i++)
        {
            int at = i % 2 == 0 ? 0 : oracle.Count - 1;
            list.RemoveAt(at);
            oracle.RemoveAt(at);
        }
        Assert.Equal(oracle, Enumerate(list));
    }

    [Theory]
    [InlineData("Add")]
    [InlineData("InsertFront")]
    [InlineData("InsertEnd")]
    [InlineData("RemoveAt")]
    [InlineData("RemoveFound")]
    [InlineData("Clear")]
    public void StructuralMutation_ShouldInvalidateTheEnumerator(string mutation)
    {
        var list = new BTreeList<int>(Enumerable.Range(0, 300));
        BTreeList<int>.Enumerator e = list.GetEnumerator();
        Assert.True(e.MoveNext());

        switch (mutation)
        {
            case "Add": list.Add(-1); break;
            case "InsertFront": list.Insert(0, -1); break;
            case "InsertEnd": list.Insert(list.Count, -1); break;
            case "RemoveAt": list.RemoveAt(150); break;
            case "RemoveFound": Assert.True(list.Remove(299)); break;
            case "Clear": list.Clear(); break;
        }

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => e.MoveNext());
        Assert.Contains("Collection was modified", ex.Message);
    }

    [Fact]
    public void StructuralMutation_InsideForeach_ShouldThrow()
    {
        var list = new BTreeList<int>(new[] { 1, 2 });

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (int x in list)
                list.Add(x);
        });
    }

    [Fact]
    public void IndexerSet_ShouldNotInvalidateTheEnumerator_AndShouldBeSeenAhead()
    {
        // An in-place element replacement moves no leaf boundary, so it is not structural — the rule Deque<T>
        // and Rope follow, and the one place this type deliberately differs from List<T>.
        var list = new BTreeList<int>(Enumerable.Range(0, 300));
        var seen = new List<int>();

        foreach (int x in list)
        {
            if (seen.Count == 0)
            {
                list[0] = -1;     // behind the cursor
                list[200] = -200; // ahead of it, in another leaf
            }
            seen.Add(x);
        }

        Assert.Equal(300, seen.Count);
        Assert.Equal(0, seen[0]);
        Assert.Equal(-200, seen[200]);
    }

    [Fact]
    public void Clear_ShouldNotInvalidateTheEnumerator_WhenTheListIsAlreadyEmpty()
    {
        var list = new BTreeList<int>();
        BTreeList<int>.Enumerator neverPopulated = list.GetEnumerator();
        list.Clear();
        Assert.False(neverPopulated.MoveNext());

        // Drained by removal rather than by Clear(): still empty, so still a no-op.
        list.Add(1);
        list.RemoveAt(0);
        BTreeList<int>.Enumerator afterDrain = list.GetEnumerator();
        list.Clear();
        Assert.False(afterDrain.MoveNext());
    }

    [Fact]
    public void Remove_ShouldNotInvalidateTheEnumerator_WhenTheItemIsNotFound()
    {
        var list = new BTreeList<int>(new[] { 1, 2, 3 });
        BTreeList<int>.Enumerator e = list.GetEnumerator();
        Assert.True(e.MoveNext());

        Assert.False(list.Remove(42));

        Assert.True(e.MoveNext());
        Assert.Equal(2, e.Current);
    }

    [Fact]
    public void SearchAndRead_ShouldNotInvalidateTheEnumerator()
    {
        var list = new BTreeList<int>(new[] { 1, 2, 3 });
        BTreeList<int>.Enumerator e = list.GetEnumerator();
        Assert.True(e.MoveNext());

        _ = list[2];
        _ = list.IndexOf(3);
        _ = list.Contains(9);
        _ = list.ToArray();
        list.CopyTo(new int[3], 0);

        Assert.True(e.MoveNext());
        Assert.Equal(2, e.Current);
    }

    [Fact]
    public void Current_ShouldBeDefault_BeforeTheFirstMoveNextAndAfterTheEnd()
    {
        var list = new BTreeList<string>(new[] { "a" });
        BTreeList<string>.Enumerator e = list.GetEnumerator();

        Assert.Null(e.Current);
        Assert.True(e.MoveNext());
        Assert.Equal("a", e.Current);
        Assert.False(e.MoveNext());
        Assert.Null(e.Current);
    }

    [Fact]
    public void MoveNext_PastTheEnd_ShouldStayFalse_UntilTheListIsModified()
    {
        var list = new BTreeList<int>(Enumerable.Range(0, 200));
        BTreeList<int>.Enumerator e = list.GetEnumerator();
        while (e.MoveNext())
        {
        }

        Assert.False(e.MoveNext());
        Assert.False(e.MoveNext());

        // The version check runs ahead of the exhausted state, so a later modification is still reported.
        list.Add(200);
        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void Reset_ShouldRestartFromTheFront_WhenTheListIsUnchanged()
    {
        var list = new BTreeList<int>(Enumerable.Range(0, 300));
        BTreeList<int>.Enumerator e = list.GetEnumerator();
        for (int i = 0; i < 250; i++)
            Assert.True(e.MoveNext());
        Assert.Equal(249, e.Current);

        e.Reset();

        Assert.Equal(0, e.Current);
        var replay = new List<int>();
        while (e.MoveNext())
            replay.Add(e.Current);
        Assert.Equal(Enumerable.Range(0, 300), replay);
    }

    [Fact]
    public void Reset_ShouldThrow_WhenTheListWasModified()
    {
        var list = new BTreeList<int>(new[] { 1, 2 });
        BTreeList<int>.Enumerator e = list.GetEnumerator();
        Assert.True(e.MoveNext());

        list.RemoveAt(0);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => e.Reset());
        Assert.Contains("Collection was modified", ex.Message);
    }

    [Fact]
    public void NonGenericCurrent_ShouldBoxTheCurrentElement()
    {
        IEnumerator e = new BTreeList<int>(new[] { 7, 8 }).GetEnumerator();

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
        List<int> oracle = Enumerable.Range(0, 500).ToList();
        var list = new BTreeList<int>(oracle);

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
