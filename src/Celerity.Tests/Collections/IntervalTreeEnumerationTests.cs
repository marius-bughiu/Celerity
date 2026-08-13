using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// The <see cref="IReadOnlyList{T}"/> surface of <see cref="IntervalTree{TKey, TValue, TComparer}"/>: the
/// indexer and its bounds, the struct enumerator and both interface enumerators, and the
/// <see cref="Interval{TKey, TValue}"/> entry type it hands back.
///
/// <para>
/// The tree is immutable, so unlike the mutable collections' enumeration suites there is no version to bump
/// and no concurrent-modification case to pin — an enumerator here cannot be invalidated, which is asserted
/// below rather than left implied.
/// </para>
/// </summary>
public class IntervalTreeEnumerationTests
{
    private static IntervalTree<int, string> Fixture() => new(
    [
        new Interval<int, string>(0, 100, "span"),
        new Interval<int, string>(10, 20, "first"),
        new Interval<int, string>(30, 40, "second"),
    ]);

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenTheTreeIsEmpty()
    {
        var tree = new IntervalTree<int, string>(Array.Empty<Interval<int, string>>());

        var seen = new List<Interval<int, string>>();
        foreach (var interval in tree)
            seen.Add(interval);

        Assert.Empty(seen);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEveryIntervalInStartOrder()
    {
        var tree = Fixture();

        var starts = new List<int>();
        foreach (var interval in tree)
            starts.Add(interval.Start);

        Assert.Equal(new[] { 0, 10, 30 }, starts);
    }

    [Fact]
    public void GetEnumerator_ShouldRestart_WhenReset()
    {
        var tree = Fixture();
        var enumerator = tree.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        enumerator.Reset();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current.Start);
        enumerator.Dispose();
    }

    [Fact]
    public void GetEnumerator_ShouldStayValid_WhenTheTreeIsQueriedDuringEnumeration()
    {
        var tree = Fixture();
        int seen = 0;

        foreach (var interval in tree)
        {
            // Nothing about a query mutates the tree, so this cannot invalidate the walk.
            Assert.True(tree.ContainsPoint(interval.Start));
            seen++;
        }

        Assert.Equal(3, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldReportDefault_WhenExhausted()
    {
        var tree = new IntervalTree<int, string>([new Interval<int, string>(1, 2, "only")]);
        var enumerator = tree.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current.Start);
        Assert.Null(enumerator.Current.Value);
    }

    [Fact]
    public void GenericEnumerator_ShouldYieldEveryInterval()
    {
        IEnumerable<Interval<int, string>> tree = Fixture();

        var starts = new List<int>();
        using (IEnumerator<Interval<int, string>> enumerator = tree.GetEnumerator())
        {
            while (enumerator.MoveNext())
                starts.Add(enumerator.Current.Start);
        }

        Assert.Equal(new[] { 0, 10, 30 }, starts);
    }

    [Fact]
    public void NonGenericEnumerator_ShouldYieldEveryInterval()
    {
        IEnumerable tree = Fixture();

        var starts = new List<int>();
        IEnumerator enumerator = tree.GetEnumerator();
        while (enumerator.MoveNext())
            starts.Add(((Interval<int, string>)enumerator.Current!).Start);

        enumerator.Reset();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(0, ((Interval<int, string>)enumerator.Current!).Start);

        Assert.Equal(new[] { 0, 10, 30 }, starts);
    }

    [Fact]
    public void ReadOnlyList_ShouldExposeCountAndIndexer()
    {
        IReadOnlyList<Interval<int, string>> tree = Fixture();

        Assert.Equal(3, tree.Count);
        Assert.Equal("span", tree[0].Value);
        Assert.Equal("first", tree[1].Value);
        Assert.Equal("second", tree[2].Value);
    }

    [Fact]
    public void Indexer_ShouldThrow_WhenTheIndexIsOutOfRange()
    {
        var tree = Fixture();

        Assert.Throws<ArgumentOutOfRangeException>(() => tree[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => tree[3]);
    }

    [Fact]
    public void Interval_ShouldExposeItsEndpointsAndValue()
    {
        var interval = new Interval<int, string>(3, 9, "payload");

        Assert.Equal(3, interval.Start);
        Assert.Equal(9, interval.End);
        Assert.Equal("payload", interval.Value);

        var (start, end, value) = interval;
        Assert.Equal(3, start);
        Assert.Equal(9, end);
        Assert.Equal("payload", value);
    }

    [Fact]
    public void Interval_ShouldRenderItsRangeAndValue_WhenFormatted()
    {
        Assert.Equal("[3, 9) = payload", new Interval<int, string>(3, 9, "payload").ToString());
    }
}
