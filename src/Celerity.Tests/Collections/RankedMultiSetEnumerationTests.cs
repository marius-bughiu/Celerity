using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration and interface-surface coverage for <see cref="RankedMultiSet{T, TComparer}"/>: the struct
/// enumerators and their version checks, the range view, and the two interfaces the type presents,
/// <see cref="ICollection{T}"/> and <see cref="IReadOnlyList{T}"/>. It is not an <see cref="ISet{T}"/>,
/// because it keeps duplicates.
/// </summary>
public class RankedMultiSetEnumerationTests
{
    private static RankedMultiSet<int> Seeded() => new([30, 10, 20, 10]);

    [Fact]
    public void GetEnumerator_ShouldWalkTheElementsInAscendingOrder_IncludingDuplicates()
    {
        var set = Seeded();

        var seen = new List<int>();
        foreach (int item in set)
            seen.Add(item);

        Assert.Equal([10, 10, 20, 30], seen);
    }

    [Fact]
    public void GetEnumerator_ShouldWalkEveryBucket_WhenTheMultiSetSpansSeveralOfThem()
    {
        int[] source = [.. Enumerable.Range(0, 1500).Select(i => i / 2)];
        var set = new RankedMultiSet<int>(source);

        Assert.Equal(source, set.ToArray());
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenTheMultiSetIsEmpty()
    {
        var enumerator = new RankedMultiSet<int>().GetEnumerator();

        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);
        enumerator.Dispose();
    }

    [Fact]
    public void GetEnumerator_ShouldClearCurrent_WhenItRunsPastTheLastElement()
    {
        var enumerator = Seeded().GetEnumerator();
        while (enumerator.MoveNext())
        {
        }

        Assert.Equal(0, enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Reset_ShouldRestartTheWalk()
    {
        var enumerator = Seeded().GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        enumerator.Reset();

        Assert.Equal(0, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(10, enumerator.Current);
    }

    [Fact]
    public void MoveNextAndReset_ShouldThrowInvalidOperationException_WhenTheMultiSetWasModified()
    {
        var set = Seeded();

        var moving = set.GetEnumerator();
        var resetting = set.GetEnumerator();

        // Adding a duplicate is a modification too: unlike a set, nothing is ever rejected.
        set.Add(10);

        Assert.Throws<InvalidOperationException>(() => moving.MoveNext());
        Assert.Throws<InvalidOperationException>(resetting.Reset);
    }

    [Fact]
    public void Enumerator_ShouldStayValid_AcrossLookupsAndAFailedRemove()
    {
        var set = Seeded();
        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        _ = set.Contains(20);
        _ = set.CountOf(10);
        _ = set[1];
        Assert.False(set.Remove(99));

        Assert.True(enumerator.MoveNext());
        Assert.Equal(10, enumerator.Current);
    }

    [Fact]
    public void NonGenericCurrent_ShouldReturnTheBoxedElement()
    {
        IEnumerator enumerator = ((IEnumerable)Seeded()).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(10, enumerator.Current);
    }

    [Fact]
    public void GenericGetEnumerator_ShouldWalkTheElements_WhenReachedThroughTheInterface()
    {
        IEnumerable<int> set = Seeded();

        Assert.Equal([10, 10, 20, 30], set.ToArray());
    }

    // ---- the range view ----------------------------------------------------------------------------

    [Fact]
    public void RangeEnumerator_ShouldClearCurrent_WhenItReachesTheUpperBound()
    {
        var enumerator = Seeded().EnumerateRange(10, 25).GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());

        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);
        Assert.False(enumerator.MoveNext());
        enumerator.Dispose();
    }

    [Fact]
    public void RangeEnumerator_ShouldReset_ToTheStartOfTheRange()
    {
        var enumerator = Seeded().EnumerateRange(20, 40).GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(20, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());

        enumerator.Reset();

        Assert.Equal(0, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(20, enumerator.Current);
    }

    [Fact]
    public void RangeEnumerator_ShouldStayFinished_AfterAResetPastTheLastElement()
    {
        var enumerator = Seeded().EnumerateRange(40, 50).GetEnumerator();
        Assert.False(enumerator.MoveNext());

        enumerator.Reset();

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void RangeEnumerator_ShouldThrowInvalidOperationException_WhenTheMultiSetWasModified()
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
        var range = Seeded().EnumerateRange(10, 30);

        Assert.Equal([10, 10, 20], ((IEnumerable<int>)range).ToArray());

        var untyped = new List<object?>();
        foreach (object? item in (IEnumerable)range)
            untyped.Add(item);

        Assert.Equal<object?[]>([10, 10, 20], [.. untyped]);
    }

    [Fact]
    public void RangeEnumerator_ShouldReturnTheBoxedElement_ThroughTheNonGenericCurrent()
    {
        IEnumerator enumerator = ((IEnumerable)Seeded().EnumerateRange(10, 40)).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(10, enumerator.Current);
    }

    // ---- the interface surfaces --------------------------------------------------------------------

    [Fact]
    public void IReadOnlyList_ShouldExposeTheMultiSetPositionally()
    {
        IReadOnlyList<int> list = Seeded();

        Assert.Equal(4, list.Count);
        Assert.Equal(10, list[1]);
        Assert.Equal(30, list[3]);
    }

    [Fact]
    public void ICollection_ShouldAddDuplicates_AndReportItIsWritable()
    {
        ICollection<int> collection = Seeded();

        collection.Add(10);

        Assert.False(collection.IsReadOnly);
        Assert.Equal(5, collection.Count);
        Assert.True(collection.Contains(10));
        Assert.True(collection.Remove(10));
        Assert.Equal([10, 10, 20, 30], collection.ToArray());
    }

    [Fact]
    public void CopyTo_ShouldWriteTheElementsInOrder_AtTheGivenOffset()
    {
        var set = new RankedMultiSet<int>(Enumerable.Range(0, 1500).Select(i => i / 3));
        var target = new int[1502];

        set.CopyTo(target, 2);

        Assert.Equal(0, target[0]);
        Assert.Equal(0, target[1]);
        Assert.Equal(set.ToArray(), target[2..]);
    }

    [Fact]
    public void CopyTo_ShouldWriteNothing_WhenTheMultiSetIsEmpty()
    {
        var target = new int[1];

        new RankedMultiSet<int>().CopyTo(target, 1);

        Assert.Equal([0], target);
    }

    [Fact]
    public void RankQueries_ShouldFollowTheComparer_WhenItDisagreesWithDefaultEquality()
    {
        var set = new RankedMultiSet<string, CaseInsensitiveOrdinal>(["b", "A", "a", "B"], default);

        // "A" and "a" are equal to this comparer, so they form one run in insertion order.
        Assert.Equal(["A", "a", "b", "B"], set.ToArray());
        Assert.Equal(2, set.CountOf("A"));
        Assert.Equal(2, set.IndexOf("B"));
        Assert.Equal(3, set.LastIndexOf("b"));
        Assert.True(set.Remove("a"));
        Assert.Equal(["a", "b", "B"], set.ToArray());
    }

    private readonly struct CaseInsensitiveOrdinal : IComparer<string>
    {
        public int Compare(string? x, string? y) => StringComparer.OrdinalIgnoreCase.Compare(x, y);
    }
}
