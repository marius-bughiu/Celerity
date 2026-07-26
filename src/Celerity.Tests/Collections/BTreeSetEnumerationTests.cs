using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration coverage for <see cref="BTreeSet{T, TComparer}"/>: the in-order struct enumerator and its range
/// counterpart, the non-generic <see cref="IEnumerable"/> paths, <c>Reset</c>, and the version checks that
/// make a concurrent modification throw rather than silently yield a torn traversal.
/// </summary>
public class BTreeSetEnumerationTests
{
    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenSetIsEmpty()
    {
        var set = new BTreeSet<int>();

        var enumerator = set.GetEnumerator();

        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEveryElementInOrder_AcrossMultipleLevels()
    {
        var rand = new Random(1234);
        var set = new BTreeSet<int>();
        foreach (int value in Enumerable.Range(0, 3000).OrderBy(_ => rand.Next()))
            set.Add(value);

        var seen = new List<int>();
        foreach (int value in set)
            seen.Add(value);

        Assert.Equal(Enumerable.Range(0, 3000), seen);
    }

    [Fact]
    public void GetEnumerator_ShouldStayExhausted_AfterTheLastElement()
    {
        var set = new BTreeSet<int>(new[] { 1 });

        var enumerator = set.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);
    }

    [Fact]
    public void Reset_ShouldRestartTheTraversal()
    {
        var set = new BTreeSet<int>(Enumerable.Range(0, 100));

        var enumerator = set.GetEnumerator();
        for (int i = 0; i < 40; i++)
            Assert.True(enumerator.MoveNext());

        enumerator.Reset();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);
        enumerator.Dispose();
    }

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("clear")]
    public void MoveNext_ShouldThrow_WhenSetIsModifiedDuringEnumeration(string mutation)
    {
        var set = new BTreeSet<int>(Enumerable.Range(0, 100));

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        switch (mutation)
        {
            case "add":
                set.Add(1000);
                break;
            case "remove":
                set.Remove(50);
                break;
            default:
                set.Clear();
                break;
        }

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void MoveNext_ShouldNotThrow_WhenAFailedTryAddLeavesTheContentUnchanged()
    {
        var set = new BTreeSet<int>(Enumerable.Range(0, 200));

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.False(set.TryAdd(100));
        Assert.False(set.Remove(1000));

        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current);
    }

    [Fact]
    public void NonGenericEnumeration_ShouldYieldTheSameElements()
    {
        var set = new BTreeSet<int>(Enumerable.Range(0, 50));

        Assert.Equal(Enumerable.Range(0, 50), ((IEnumerable)set).Cast<int>());

        IEnumerator<int> generic = ((IEnumerable<int>)set).GetEnumerator();
        Assert.True(generic.MoveNext());
        Assert.Equal(0, generic.Current);
        generic.Dispose();
    }

    [Fact]
    public void NonGenericCurrent_ShouldExposeTheSameElement_OnBothEnumerators()
    {
        var set = new BTreeSet<int>(new[] { 1 });

        IEnumerator items = ((IEnumerable)set).GetEnumerator();
        Assert.True(items.MoveNext());
        Assert.Equal(1, items.Current);

        IEnumerator range = ((IEnumerable)set.EnumerateRange(0, 5)).GetEnumerator();
        Assert.True(range.MoveNext());
        Assert.Equal(1, range.Current);
    }

    [Fact]
    public void RangeEnumerator_ShouldStopAtTheUpperBound_AndSupportReset()
    {
        var set = new BTreeSet<int>(Enumerable.Range(0, 500));

        var enumerator = set.EnumerateRange(100, 105).GetEnumerator();

        var seen = new List<int>();
        while (enumerator.MoveNext())
            seen.Add(enumerator.Current);

        Assert.Equal(new[] { 100, 101, 102, 103, 104 }, seen);
        Assert.False(enumerator.MoveNext());

        enumerator.Reset();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(100, enumerator.Current);
        enumerator.Dispose();
    }

    [Fact]
    public void RangeEnumerator_ShouldThrow_WhenSetIsModifiedDuringEnumeration()
    {
        var set = new BTreeSet<int>(Enumerable.Range(0, 200));

        var enumerator = set.EnumerateRange(10, 100).GetEnumerator();
        Assert.True(enumerator.MoveNext());

        set.Add(500);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void RangeEnumerable_ShouldFlowThroughTheInterfacePaths()
    {
        var set = new BTreeSet<int>(Enumerable.Range(0, 100));

        IEnumerable<int> generic = set.EnumerateRange(10, 15);
        Assert.Equal(new[] { 10, 11, 12, 13, 14 }, generic);
        Assert.Equal(new[] { 10, 11, 12, 13, 14 }, ((IEnumerable)set.EnumerateRange(10, 15)).Cast<int>());
    }

    [Fact]
    public void RangeEnumeration_ShouldSeekCorrectly_WhenBoundsFallInsideInternalNodes()
    {
        var set = new BTreeSet<int>(Enumerable.Range(0, 3000));

        for (int from = 0; from < 3000; from += 97)
        {
            int to = Math.Min(from + 250, 3000);
            Assert.Equal(Enumerable.Range(from, to - from), set.EnumerateRange(from, to));
        }
    }
}
