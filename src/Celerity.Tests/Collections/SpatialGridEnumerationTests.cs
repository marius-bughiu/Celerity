using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration coverage for <see cref="SpatialGrid{TValue}"/>: the struct enumerator, both interface-typed
/// enumerators, and the invalidation rule this type has to get right precisely because — unlike
/// <see cref="KdTree{TValue}"/> — it is mutable.
///
/// <para>
/// The rule with a decision behind it is <see cref="SpatialGrid{TValue}.Move"/>: it changes an entry's
/// position but neither the set of entries nor the slot each one occupies, so the sequence an enumerator is
/// walking is unaffected and invalidating would be gratuitous. That is the family's own rule — an operation
/// that changes nothing about the sequence must not invalidate — and it is pinned here so a later change
/// cannot quietly tighten it.
/// </para>
/// </summary>
public class SpatialGridEnumerationTests
{
    private static SpatialGrid<int> Filled(int count)
    {
        var grid = new SpatialGrid<int>(0, 0, 10, 10, 1);
        for (int i = 0; i < count; i++)
            grid.Add(i % 10, i / 10.0, i);

        return grid;
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEveryLiveEntry_WhenIterated()
    {
        SpatialGrid<int> grid = Filled(20);

        var seen = new List<int>();
        foreach (SpatialPoint<int> point in grid)
            seen.Add(point.Value);

        Assert.Equal(Enumerable.Range(0, 20), seen.OrderBy(v => v));
    }

    [Fact]
    public void GetEnumerator_ShouldSkipVacatedSlots_WhenEntriesWereRemoved()
    {
        var grid = new SpatialGrid<int>(0, 0, 10, 10, 1);
        SpatialGridHandle first = grid.Add(1, 1, 1);
        grid.Add(2, 2, 2);
        SpatialGridHandle third = grid.Add(3, 3, 3);

        grid.Remove(first);
        grid.Remove(third);

        Assert.Equal([2], grid.Select(p => p.Value).ToArray());
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenTheGridIsEmpty()
    {
        var grid = new SpatialGrid<int>(0, 0, 10, 10, 1);

        SpatialGrid<int>.Enumerator enumerator = grid.GetEnumerator();

        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current.Value);
        enumerator.Dispose();
    }

    [Fact]
    public void Enumerator_ShouldClearCurrent_WhenItRunsPastTheEnd()
    {
        var grid = new SpatialGrid<int>(0, 0, 10, 10, 1);
        grid.Add(1, 2, 42);

        SpatialGrid<int>.Enumerator enumerator = grid.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(42, enumerator.Current.Value);
        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current.Value);
    }

    [Fact]
    public void Enumerator_ShouldRestart_WhenResetIsCalled()
    {
        SpatialGrid<int> grid = Filled(3);

        SpatialGrid<int>.Enumerator enumerator = grid.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());

        enumerator.Reset();

        Assert.Equal(0, enumerator.Current.Value);
        Assert.True(enumerator.MoveNext());
    }

    [Fact]
    public void GenericEnumerator_ShouldYieldEveryLiveEntry_WhenIteratedThroughTheInterface()
    {
        SpatialGrid<int> grid = Filled(5);

        IEnumerable<SpatialPoint<int>> sequence = grid;

        Assert.Equal(Enumerable.Range(0, 5), sequence.Select(p => p.Value).OrderBy(v => v));
        Assert.Equal(5, grid.Count);
    }

    [Fact]
    public void NonGenericEnumerator_ShouldYieldEveryLiveEntry_WhenIteratedThroughTheInterface()
    {
        SpatialGrid<int> grid = Filled(4);

        IEnumerator enumerator = ((IEnumerable)grid).GetEnumerator();

        var seen = new List<int>();
        while (enumerator.MoveNext())
            seen.Add(((SpatialPoint<int>)enumerator.Current!).Value);

        Assert.Equal(Enumerable.Range(0, 4), seen.OrderBy(v => v));
    }

    [Fact]
    public void Enumerator_ShouldThrowInvalidOperation_WhenAnEntryIsAddedDuringEnumeration()
    {
        SpatialGrid<int> grid = Filled(3);

        SpatialGrid<int>.Enumerator enumerator = grid.GetEnumerator();
        grid.Add(5, 5, 99);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Enumerator_ShouldThrowInvalidOperation_WhenAnEntryIsRemovedDuringEnumeration()
    {
        var grid = new SpatialGrid<int>(0, 0, 10, 10, 1);
        SpatialGridHandle handle = grid.Add(1, 1, 1);
        grid.Add(2, 2, 2);

        SpatialGrid<int>.Enumerator enumerator = grid.GetEnumerator();
        grid.Remove(handle);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void EnumeratorReset_ShouldThrowInvalidOperation_WhenTheGridWasModified()
    {
        SpatialGrid<int> grid = Filled(2);

        SpatialGrid<int>.Enumerator enumerator = grid.GetEnumerator();
        grid.Add(7, 7, 99);

        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void Enumerator_ShouldStayValid_WhenAnEntryIsMovedDuringEnumeration()
    {
        // Move changes a position, not the sequence: the same entries occupy the same slots, so an enumerator
        // is walking exactly what it was before. Invalidating here would be the gratuitous kind the family
        // rule forbids.
        var grid = new SpatialGrid<int>(0, 0, 10, 10, 1);
        SpatialGridHandle handle = grid.Add(1, 1, 1);
        grid.Add(2, 2, 2);

        SpatialGrid<int>.Enumerator enumerator = grid.GetEnumerator();
        grid.Move(handle, 9, 9);

        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Clear_ShouldInvalidateEnumerators_WhenItActuallyRemovedSomething()
    {
        SpatialGrid<int> grid = Filled(2);

        SpatialGrid<int>.Enumerator enumerator = grid.GetEnumerator();
        grid.Clear();

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Clear_ShouldKeepEnumeratorsValid_WhenTheGridWasAlreadyEmpty()
    {
        var grid = new SpatialGrid<int>(0, 0, 10, 10, 1);

        SpatialGrid<int>.Enumerator enumerator = grid.GetEnumerator();
        grid.Clear();

        Assert.False(enumerator.MoveNext());
    }
}
