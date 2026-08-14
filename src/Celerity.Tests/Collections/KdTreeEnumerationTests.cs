using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration and <see cref="IReadOnlyList{T}"/> coverage for <see cref="KdTree{TValue}"/>: the struct
/// enumerator, both interface-typed enumerators, and the guarantee the type gets for free from being
/// immutable — an enumerator that can never be invalidated, because there is nothing that could invalidate it.
/// </summary>
public class KdTreeEnumerationTests
{
    private static SpatialPoint<int>[] Points(int count)
    {
        var points = new SpatialPoint<int>[count];
        for (int i = 0; i < count; i++)
            points[i] = new SpatialPoint<int>(i % 7, i / 7.0, i);

        return points;
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEveryStoredPoint_WhenIterated()
    {
        var tree = new KdTree<int>(Points(20));

        var seen = new List<int>();
        foreach (SpatialPoint<int> point in tree)
            seen.Add(point.Value);

        Assert.Equal(Enumerable.Range(0, 20), seen.OrderBy(v => v));
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenTheTreeIsEmpty()
    {
        var tree = new KdTree<int>([]);

        KdTree<int>.Enumerator enumerator = tree.GetEnumerator();

        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current.Value);
        enumerator.Dispose();
    }

    [Fact]
    public void Enumerator_ShouldClearCurrent_WhenItRunsPastTheEnd()
    {
        var tree = new KdTree<int>([new(1, 2, 42)]);

        KdTree<int>.Enumerator enumerator = tree.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(42, enumerator.Current.Value);
        Assert.Equal(1, enumerator.Current.X);
        Assert.False(enumerator.MoveNext());
        Assert.Equal(default, enumerator.Current.Value);
    }

    [Fact]
    public void Reset_ShouldRestartTheEnumeration_WhenCalledMidway()
    {
        var tree = new KdTree<int>(Points(5));

        KdTree<int>.Enumerator enumerator = tree.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        SpatialPoint<int> first = enumerator.Current;
        Assert.True(enumerator.MoveNext());

        enumerator.Reset();

        Assert.Equal(default, enumerator.Current.Value);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(first.Value, enumerator.Current.Value);
    }

    [Fact]
    public void GenericInterfaceEnumerator_ShouldYieldEveryStoredPoint_WhenIterated()
    {
        var tree = new KdTree<int>(Points(11));

        using IEnumerator<SpatialPoint<int>> enumerator = ((IEnumerable<SpatialPoint<int>>)tree).GetEnumerator();

        var seen = new List<int>();
        while (enumerator.MoveNext())
            seen.Add(enumerator.Current.Value);

        Assert.Equal(Enumerable.Range(0, 11), seen.OrderBy(v => v));
    }

    [Fact]
    public void NonGenericInterfaceEnumerator_ShouldYieldEveryStoredPoint_WhenIterated()
    {
        var tree = new KdTree<int>(Points(6));

        IEnumerator enumerator = ((IEnumerable)tree).GetEnumerator();

        var seen = new List<int>();
        while (enumerator.MoveNext())
            seen.Add(((SpatialPoint<int>)enumerator.Current!).Value);

        Assert.Equal(Enumerable.Range(0, 6), seen.OrderBy(v => v));
    }

    [Fact]
    public void ReadOnlyList_ShouldAgreeWithEnumeration_WhenBothAreRead()
    {
        IReadOnlyList<SpatialPoint<int>> tree = new KdTree<int>(Points(13));

        Assert.Equal(13, tree.Count);

        var indexed = new List<int>();
        for (int i = 0; i < tree.Count; i++)
            indexed.Add(tree[i].Value);

        Assert.Equal(indexed, tree.Select(p => p.Value));
    }

    [Fact]
    public void Enumerators_ShouldStayValidAcrossQueries_BecauseTheTreeIsImmutable()
    {
        var tree = new KdTree<int>(Points(30));

        KdTree<int>.Enumerator enumerator = tree.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        // There is no mutating member to interleave here — querying is the only thing a caller can do, and it
        // cannot bump a version because there is no version.
        Assert.True(tree.TryFindNearest(3, 2, out _));
        Assert.True(tree.CountWithin(3, 2, 10) > 0);

        int remaining = 1;
        while (enumerator.MoveNext())
            remaining++;

        Assert.Equal(30, remaining);
    }
}
