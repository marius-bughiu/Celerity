using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration and <see cref="IReadOnlyList{T}"/> coverage for <see cref="RTree{TValue}"/>: the struct
/// enumerator, both interface-typed enumerators, and the guarantee the type gets for free from being
/// immutable — an enumerator that can never be invalidated, because there is nothing that could invalidate it.
/// </summary>
public class RTreeEnumerationTests
{
    private static SpatialBox<int>[] Boxes(int count)
    {
        var boxes = new SpatialBox<int>[count];
        for (int i = 0; i < count; i++)
        {
            double x = i % 7;
            double y = i / 7.0;
            boxes[i] = new SpatialBox<int>(x, y, x + 0.5, y + 0.25, i);
        }

        return boxes;
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEveryStoredBox_WhenIterated()
    {
        var tree = new RTree<int>(Boxes(20));

        var seen = new List<int>();
        foreach (SpatialBox<int> box in tree)
            seen.Add(box.Value);

        Assert.Equal(Enumerable.Range(0, 20), seen.OrderBy(v => v));
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenTheTreeIsEmpty()
    {
        var tree = new RTree<int>([]);

        RTree<int>.Enumerator enumerator = tree.GetEnumerator();

        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current.Value);
        enumerator.Dispose();
    }

    [Fact]
    public void Enumerator_ShouldClearCurrent_WhenItRunsPastTheEnd()
    {
        var tree = new RTree<int>([new SpatialBox<int>(1, 2, 3, 4, 42)]);

        RTree<int>.Enumerator enumerator = tree.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(42, enumerator.Current.Value);
        Assert.Equal(1, enumerator.Current.MinX);
        Assert.Equal(2, enumerator.Current.MinY);
        Assert.Equal(3, enumerator.Current.MaxX);
        Assert.Equal(4, enumerator.Current.MaxY);
        Assert.False(enumerator.MoveNext());
        Assert.Equal(default, enumerator.Current.Value);
    }

    [Fact]
    public void Reset_ShouldRestartTheEnumeration_WhenCalledMidway()
    {
        var tree = new RTree<int>(Boxes(5));

        RTree<int>.Enumerator enumerator = tree.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        SpatialBox<int> first = enumerator.Current;
        Assert.True(enumerator.MoveNext());

        enumerator.Reset();

        Assert.Equal(default, enumerator.Current.Value);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(first.Value, enumerator.Current.Value);
    }

    [Fact]
    public void GenericInterfaceEnumerator_ShouldYieldEveryStoredBox_WhenIterated()
    {
        var tree = new RTree<int>(Boxes(11));

        using IEnumerator<SpatialBox<int>> enumerator = ((IEnumerable<SpatialBox<int>>)tree).GetEnumerator();

        var seen = new List<int>();
        while (enumerator.MoveNext())
            seen.Add(enumerator.Current.Value);

        Assert.Equal(Enumerable.Range(0, 11), seen.OrderBy(v => v));
    }

    [Fact]
    public void NonGenericInterfaceEnumerator_ShouldYieldEveryStoredBox_WhenIterated()
    {
        var tree = new RTree<int>(Boxes(6));

        IEnumerator enumerator = ((IEnumerable)tree).GetEnumerator();

        var seen = new List<int>();
        while (enumerator.MoveNext())
            seen.Add(((SpatialBox<int>)enumerator.Current!).Value);

        Assert.Equal(Enumerable.Range(0, 6), seen.OrderBy(v => v));
    }

    [Fact]
    public void ReadOnlyList_ShouldAgreeWithEnumeration_WhenBothAreRead()
    {
        IReadOnlyList<SpatialBox<int>> tree = new RTree<int>(Boxes(13));

        Assert.Equal(13, tree.Count);

        var indexed = new List<int>();
        for (int i = 0; i < tree.Count; i++)
            indexed.Add(tree[i].Value);

        Assert.Equal(indexed, tree.Select(b => b.Value));
    }

    [Fact]
    public void Enumerators_ShouldStayValidAcrossQueries_BecauseTheTreeIsImmutable()
    {
        var tree = new RTree<int>(Boxes(30));

        RTree<int>.Enumerator enumerator = tree.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        // There is no mutating member to interleave here — querying is the only thing a caller can do, and it
        // cannot bump a version because there is no version.
        Assert.True(tree.ContainsOverlapping(0, 0, 7, 5));
        Assert.True(tree.CountAtPoint(0, 0) > 0);

        int remaining = 1;
        while (enumerator.MoveNext())
            remaining++;

        Assert.Equal(30, remaining);
    }

    [Fact]
    public void Deconstruct_ShouldYieldTheEdgesAndValue_WhenABoxIsDestructured()
    {
        var tree = new RTree<int>([new SpatialBox<int>(1, 2, 3, 4, 7)]);

        (double minX, double minY, double maxX, double maxY, int value) = tree[0];

        Assert.Equal(1, minX);
        Assert.Equal(2, minY);
        Assert.Equal(3, maxX);
        Assert.Equal(4, maxY);
        Assert.Equal(7, value);
        Assert.Equal("[1, 3] x [2, 4] = 7", tree[0].ToString());
    }
}
