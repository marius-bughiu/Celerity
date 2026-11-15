using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration of <see cref="PersistentSortedSet{T, TComparer}"/>: the struct enumerator over the whole set and
/// over a range, and the boxed generic and non-generic paths.
///
/// <para>
/// The set is immutable, so there is no version check to test and no way to invalidate an enumerator. What is
/// pinned is the state contract — before the first <c>MoveNext</c>, mid-walk, after the last, which must be
/// sticky — the climb out of a spent leaf at every level of a three-level tree, and a range walk that starts
/// from a leaf its lower bound has already run off the end of. The order is specified, so every assertion here
/// is ordered.
/// </para>
/// </summary>
public class PersistentSortedSetEnumerationTests
{
    // Three levels deep, so the walk climbs through a grandparent as well as a parent.
    private const int LargeCount = 5_000;

    private static PersistentSortedSet<int, DefaultComparer<int>> EmptySet =>
        PersistentSortedSet<int, DefaultComparer<int>>.Empty;

    private static PersistentSortedSet<int, DefaultComparer<int>> Build(int count) => new(Enumerable.Range(0, count));

    private static List<int> Drain(ref PersistentSortedSet<int, DefaultComparer<int>>.Enumerator enumerator)
    {
        var seen = new List<int>();
        while (enumerator.MoveNext())
            seen.Add(enumerator.Current);

        return seen;
    }

    [Fact]
    public void EmptySet_ShouldYieldNothing()
    {
        Assert.Empty(EmptySet);
        Assert.Empty(EmptySet.EnumerateRange(int.MinValue, int.MaxValue));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(1_025)]
    [InlineData(LargeCount)]
    public void Enumeration_ShouldYieldEveryElementInAscendingOrder(int count)
    {
        var seen = new List<int>();
        foreach (int item in Build(count))
            seen.Add(item);

        Assert.Equal(Enumerable.Range(0, count), seen);
    }

    [Fact]
    public void Current_ShouldBeDefault_BeforeTheFirstMoveNextAndAfterTheLast()
    {
        PersistentSortedSet<string, DefaultComparer<string>>.Enumerator enumerator =
            PersistentSortedSet<string, DefaultComparer<string>>.Empty.Add("one").GetEnumerator();
        Assert.Null(enumerator.Current);

        Assert.True(enumerator.MoveNext());
        Assert.Equal("one", enumerator.Current);

        Assert.False(enumerator.MoveNext());
        Assert.Null(enumerator.Current);
    }

    [Fact]
    public void MoveNext_ShouldKeepReturningFalse_OnceTheWalkIsOver()
    {
        PersistentSortedSet<int, DefaultComparer<int>>.Enumerator enumerator = Build(LargeCount).GetEnumerator();
        Assert.Equal(LargeCount, Drain(ref enumerator).Count);

        // A restart here would silently double every enumeration of the set.
        for (int i = 0; i < 5; i++)
        {
            Assert.False(enumerator.MoveNext());
            Assert.Equal(0, enumerator.Current);
        }
    }

    [Fact]
    public void Reset_ShouldRestartTheWalk()
    {
        PersistentSortedSet<int, DefaultComparer<int>>.Enumerator enumerator = Build(LargeCount).GetEnumerator();
        for (int i = 0; i < 1_000; i++)
            Assert.True(enumerator.MoveNext());

        enumerator.Reset();
        Assert.Equal(0, enumerator.Current);
        Assert.Equal(Enumerable.Range(0, LargeCount), Drain(ref enumerator));

        // And again from after the end, where the finished flag has to be cleared too.
        enumerator.Reset();
        Assert.Equal(Enumerable.Range(0, LargeCount), Drain(ref enumerator));
    }

    [Fact]
    public void Dispose_ShouldBeANoOp()
    {
        PersistentSortedSet<int, DefaultComparer<int>>.Enumerator enumerator = Build(4).GetEnumerator();

        enumerator.Dispose();
        enumerator.Dispose();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);
    }

    [Fact]
    public void RangeEnumerator_ShouldFollowTheSameStateContract()
    {
        PersistentSortedSet<int, DefaultComparer<int>>.Enumerator enumerator =
            Build(LargeCount).EnumerateRange(1_000, 1_100).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(1_000, enumerator.Current);

        enumerator.Reset();
        Assert.Equal(Enumerable.Range(1_000, 100), Drain(ref enumerator));
        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);

        enumerator.Reset();
        Assert.Equal(Enumerable.Range(1_000, 100), Drain(ref enumerator));
    }

    [Theory]
    [InlineData(40)]
    [InlineData(1_200)]
    public void EveryRange_ShouldMatchAFilteredScan(int count)
    {
        // Even elements, so a bound can sit on an element or in a gap. A lower bound in the gap after a leaf's
        // last element seeks into a leaf with nothing left in range, and the walk must step on to the next leaf.
        PersistentSortedSet<int, DefaultComparer<int>> set = EmptySet;
        for (int i = 0; i < count; i++)
            set = set.Add(2 * i);

        int step = count > 100 ? 7 : 1;
        for (int from = -1; from <= 2 * count + 1; from += step)
        {
            foreach (int width in new[] { 0, 1, 3, 70, 3 * count })
            {
                int to = from + width;
                IEnumerable<int> expected = Enumerable.Range(0, count).Select(i => 2 * i).Where(k => k >= from && k < to);
                Assert.Equal(expected, set.EnumerateRange(from, to));
            }
        }
    }

    [Fact]
    public void BoxedAndNonGenericEnumeration_ShouldYieldEveryElementInOrder()
    {
        PersistentSortedSet<int, DefaultComparer<int>> set = Build(100);

        var boxed = new List<int>();
        using (IEnumerator<int> enumerator = ((IEnumerable<int>)set).GetEnumerator())
        {
            while (enumerator.MoveNext())
                boxed.Add(enumerator.Current);
        }

        var nonGeneric = new List<int>();
        IEnumerator plain = ((IEnumerable)set).GetEnumerator();
        while (plain.MoveNext())
            nonGeneric.Add((int)plain.Current!);

        Assert.Equal(Enumerable.Range(0, 100), boxed);
        Assert.Equal(boxed, nonGeneric);
    }

    [Fact]
    public void RangeEnumerable_ShouldYieldTheRange_ThroughAllThreeEnumeratorPaths()
    {
        PersistentSortedSet<int, DefaultComparer<int>>.RangeEnumerable range = Build(200).EnumerateRange(20, 90);

        var direct = new List<int>();
        foreach (int item in range)
            direct.Add(item);

        var boxed = new List<int>();
        using (IEnumerator<int> enumerator = ((IEnumerable<int>)range).GetEnumerator())
        {
            while (enumerator.MoveNext())
                boxed.Add(enumerator.Current);
        }

        var nonGeneric = new List<int>();
        IEnumerator plain = ((IEnumerable)range).GetEnumerator();
        while (plain.MoveNext())
            nonGeneric.Add((int)plain.Current!);

        Assert.Equal(Enumerable.Range(20, 70), direct);
        Assert.Equal(direct, boxed);
        Assert.Equal(direct, nonGeneric);
    }

    [Fact]
    public void Enumeration_ShouldNotAllocate()
    {
        PersistentSortedSet<int, DefaultComparer<int>> set = Build(LargeCount);
        long sum = 0;

        void Walk()
        {
            foreach (int item in set)
                sum += item;
            foreach (int item in set.EnumerateRange(100, 4_000))
                sum += item;
        }

        Action walk = Walk;
        walk(); // warm every path before measuring

        Assert.Equal(0, AllocationProbe.Quietest(walk));
        Assert.True(sum > 0);
    }
}
