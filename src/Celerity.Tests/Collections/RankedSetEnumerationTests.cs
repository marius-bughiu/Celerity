using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration and interface-surface coverage for <see cref="RankedSet{T, TComparer}"/>: the struct
/// enumerators and their version checks, the range view, and the three interfaces the type presents —
/// <see cref="ISet{T}"/>, <see cref="IReadOnlySet{T}"/> and <see cref="IReadOnlyList{T}"/>. It is the last of
/// those that no other set in this library implements, and the reason it can: a set that can select by rank
/// is a list.
/// </summary>
public class RankedSetEnumerationTests
{
    private static RankedSet<int> Seeded() => new([30, 10, 20]);

    [Fact]
    public void GetEnumerator_ShouldWalkTheElementsInAscendingOrder()
    {
        var set = Seeded();

        var seen = new List<int>();
        foreach (int item in set)
            seen.Add(item);

        Assert.Equal([10, 20, 30], seen);
    }

    [Fact]
    public void GetEnumerator_ShouldWalkEveryBucket_WhenTheSetSpansSeveralOfThem()
    {
        var set = new RankedSet<int>(Enumerable.Range(0, 1500));

        Assert.Equal(Enumerable.Range(0, 1500).ToArray(), set.ToArray());
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenTheSetIsEmpty()
    {
        var set = new RankedSet<int>();

        var enumerator = set.GetEnumerator();

        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);
        enumerator.Dispose();
    }

    [Fact]
    public void GetEnumerator_ShouldClearCurrent_WhenItRunsPastTheLastElement()
    {
        var set = Seeded();

        var enumerator = set.GetEnumerator();
        while (enumerator.MoveNext())
        {
        }

        Assert.Equal(0, enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Reset_ShouldRestartTheWalk()
    {
        var set = Seeded();

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        enumerator.Reset();

        Assert.Equal(0, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(10, enumerator.Current);
    }

    [Fact]
    public void MoveNextAndReset_ShouldThrowInvalidOperationException_WhenTheSetWasModified()
    {
        var set = Seeded();

        var moving = set.GetEnumerator();
        var resetting = set.GetEnumerator();
        set.Add(40);

        Assert.Throws<InvalidOperationException>(() => moving.MoveNext());
        Assert.Throws<InvalidOperationException>(resetting.Reset);
    }

    [Fact]
    public void NonGenericCurrent_ShouldReturnTheBoxedElement()
    {
        var set = Seeded();

        IEnumerator enumerator = ((IEnumerable)set).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(10, enumerator.Current);
    }

    [Fact]
    public void GenericGetEnumerator_ShouldWalkTheElements_WhenReachedThroughTheInterface()
    {
        IEnumerable<int> set = Seeded();

        Assert.Equal([10, 20, 30], set.ToArray());
    }

    // ---- the range view ----------------------------------------------------------------------------

    [Fact]
    public void RangeEnumerator_ShouldClearCurrent_WhenItReachesTheUpperBound()
    {
        var set = Seeded();

        var enumerator = set.EnumerateRange(10, 25).GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());

        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);

        // Once the upper bound has been crossed the scan stays finished.
        Assert.False(enumerator.MoveNext());
        enumerator.Dispose();
    }

    [Fact]
    public void RangeEnumerator_ShouldReset_ToTheStartOfTheRange()
    {
        var set = Seeded();

        var enumerator = set.EnumerateRange(20, 40).GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(20, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());

        enumerator.Reset();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(20, enumerator.Current);
    }

    [Fact]
    public void RangeEnumerator_ShouldStayFinished_AfterAResetPastTheLastElement()
    {
        var set = Seeded();

        var enumerator = set.EnumerateRange(40, 50).GetEnumerator();
        Assert.False(enumerator.MoveNext());

        enumerator.Reset();

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void RangeEnumerator_ShouldThrowInvalidOperationException_WhenTheSetWasModified()
    {
        var set = Seeded();

        var moving = set.EnumerateRange(10, 40).GetEnumerator();
        var resetting = set.EnumerateRange(10, 40).GetEnumerator();
        set.Add(50);

        Assert.Throws<InvalidOperationException>(() => moving.MoveNext());
        Assert.Throws<InvalidOperationException>(resetting.Reset);
    }

    [Fact]
    public void RangeEnumerable_ShouldWalkTheRange_WhenReachedThroughEitherInterface()
    {
        var set = Seeded();
        var range = set.EnumerateRange(10, 30);

        Assert.Equal([10, 20], ((IEnumerable<int>)range).ToArray());

        var untyped = new List<object?>();
        foreach (object? item in (IEnumerable)range)
            untyped.Add(item);

        Assert.Equal<object?[]>([10, 20], [.. untyped]);
    }

    [Fact]
    public void RangeEnumerator_ShouldReturnTheBoxedElement_ThroughTheNonGenericCurrent()
    {
        var set = Seeded();

        IEnumerator enumerator = ((IEnumerable)set.EnumerateRange(10, 40)).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(10, enumerator.Current);
    }

    // ---- the interface surfaces --------------------------------------------------------------------

    [Fact]
    public void IReadOnlyList_ShouldExposeTheSetPositionally()
    {
        IReadOnlyList<int> set = Seeded();

        Assert.Equal(3, set.Count);
        Assert.Equal(20, set[1]);
    }

    [Fact]
    public void ISetAdd_ShouldReportWhetherTheElementWasNew_RatherThanThrowing()
    {
        ISet<int> set = Seeded();

        Assert.True(set.Add(40));
        Assert.False(set.Add(40));
    }

    [Fact]
    public void ICollectionAdd_ShouldIgnoreADuplicate_RatherThanThrowing()
    {
        ICollection<int> set = Seeded();

        set.Add(40);
        set.Add(40);

        Assert.Equal(4, set.Count);
        Assert.False(set.IsReadOnly);
    }

    [Fact]
    public void CopyTo_ShouldWriteTheElementsInOrder()
    {
        var set = Seeded();
        var destination = new int[5];

        set.CopyTo(destination, 1);

        Assert.Equal([0, 10, 20, 30, 0], destination);
    }

    [Fact]
    public void SetAlgebra_ShouldMatchHashSetSemantics()
    {
        var set = new RankedSet<int>([1, 2, 3]);

        Assert.True(set.IsSubsetOf([1, 2, 3, 4]));
        Assert.True(set.IsProperSubsetOf([1, 2, 3, 4]));
        Assert.True(set.IsSupersetOf([1, 2]));
        Assert.True(set.IsProperSupersetOf([1, 2]));
        Assert.True(set.Overlaps([3, 9]));
        Assert.True(set.SetEquals([3, 2, 1]));

        set.UnionWith([4, 5]);
        Assert.Equal<int[]>([1, 2, 3, 4, 5], [.. set]);

        set.ExceptWith([1, 5]);
        Assert.Equal<int[]>([2, 3, 4], [.. set]);

        set.IntersectWith([3, 4, 9]);
        Assert.Equal<int[]>([3, 4], [.. set]);

        set.SymmetricExceptWith([4, 7]);
        Assert.Equal<int[]>([3, 7], [.. set]);
    }

    [Fact]
    public void SetAlgebra_ShouldCompareTheOtherSideWithDefaultEquality_EvenWhenTheOrderDisagrees()
    {
        // Family behaviour, pinned here rather than left to be discovered. Membership is the comparer's, but
        // the ISet<T> algebra materializes the right-hand side into a HashSet<T>, so the four members that ask
        // whether an element of *this* set is in `other` go through default equality — and the six that only
        // ever probe this set do not. A comparer that orders two values equal when the default equality
        // comparer does not is the only way to tell, and this is where the split becomes observable.
        var set = new RankedSet<string, CaseInsensitiveOrdinal>(["a"], default);

        Assert.True(set.Contains("A"));
        Assert.Equal(0, set.IndexOf("A"));

        // Probe this set, so they follow the comparer.
        Assert.True(set.Overlaps(["A"]));
        Assert.True(set.IsSupersetOf(["A"]));
        Assert.False(set.IsProperSupersetOf(["A"]));

        // Probe `other` through the HashSet, so they follow default equality.
        Assert.False(set.SetEquals(["A"]));
        Assert.False(set.IsSubsetOf(["A"]));

        set.IntersectWith(["A"]);
        Assert.Equal(0, set.Count);
    }

    /// <summary>Orders strings case-insensitively, so the order disagrees with default equality.</summary>
    private readonly struct CaseInsensitiveOrdinal : IComparer<string>
    {
        public int Compare(string? x, string? y) => string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
    }
}
