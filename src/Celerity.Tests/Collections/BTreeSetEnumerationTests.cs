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
    public void MoveNext_ShouldYieldEveryElementOnce_WhenAMissedRemoveWouldHaveRebalanced()
    {
        // Thinning the tree leaves children at MinKeys, so a removal descending through them has to top one up
        // before it can tell whether the element exists. A miss must not get that far: it does not bump the
        // version, so a rebalance would leave the live enumerator walking a stale path (issue #505).
        var set = new BTreeSet<int>();
        for (int i = 0; i < 50; i++)
            set.Add(i * 2);
        for (int i = 0; i < 50; i += 3)
            set.Remove(i * 2);

        int[] expected = set.ToArray();
        var enumerator = set.GetEnumerator();
        var seen = new List<int>();
        for (int i = 0; i < 17; i++)
        {
            Assert.True(enumerator.MoveNext());
            seen.Add(enumerator.Current);
        }

        Assert.False(set.Remove(53));

        while (enumerator.MoveNext())
            seen.Add(enumerator.Current);

        Assert.Equal(expected, seen);
        Assert.Equal(expected.Length, set.Count);
    }

    [Fact]
    public void MoveNext_ShouldNotThrow_WhenExceptWithRemovesNothing()
    {
        // ExceptWith routes every element of `other` through Remove; when all of them miss, the set is unchanged
        // and a live enumerator must neither throw nor see a rebalanced tree.
        var set = new BTreeSet<int>();
        for (int i = 0; i < 50; i++)
            set.Add(i * 2);
        for (int i = 0; i < 50; i += 3)
            set.Remove(i * 2);

        int[] expected = set.ToArray();
        var enumerator = set.GetEnumerator();
        var seen = new List<int>();
        for (int i = 0; i < 17; i++)
        {
            Assert.True(enumerator.MoveNext());
            seen.Add(enumerator.Current);
        }

        set.ExceptWith(Enumerable.Range(0, 50).Select(i => i * 2 + 1));

        while (enumerator.MoveNext())
            seen.Add(enumerator.Current);

        Assert.Equal(expected, seen);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Enumeration_ShouldStayIntact_WhenMissedRemovesLandAnywhereInAThinnedTree(int seed)
    {
        // Even elements only, then a random third removed so minimal nodes are scattered through every level;
        // every odd element is then a miss whose descent can cross a minimal child. Both the full and the range
        // enumerator must come through a burst of such misses unchanged and without throwing.
        var rand = new Random(seed);
        var set = new BTreeSet<int>();
        for (int i = 0; i < 2000; i++)
            set.Add(i * 2);
        for (int i = 0; i < 700; i++)
            set.Remove(rand.Next(2000) * 2);

        int[] expected = set.ToArray();
        for (int trial = 0; trial < 50; trial++)
        {
            int skip = rand.Next(expected.Length);
            var full = set.GetEnumerator();
            var seen = new List<int>();
            for (int i = 0; i < skip; i++)
            {
                Assert.True(full.MoveNext());
                seen.Add(full.Current);
            }

            int from = rand.Next(4000);
            int to = from + rand.Next(4000 - from + 1);
            int[] expectedRange = expected.Where(k => k >= from && k < to).ToArray();
            var range = set.EnumerateRange(from, to).GetEnumerator();
            var seenRange = new List<int>();
            int rangeSkip = rand.Next(expectedRange.Length + 1);
            for (int i = 0; i < rangeSkip; i++)
            {
                Assert.True(range.MoveNext());
                seenRange.Add(range.Current);
            }

            for (int miss = 0; miss < 20; miss++)
                Assert.False(set.Remove(rand.Next(-1, 4000) | 1));

            while (full.MoveNext())
                seen.Add(full.Current);
            while (range.MoveNext())
                seenRange.Add(range.Current);

            Assert.Equal(expected, seen);
            Assert.Equal(expectedRange, seenRange);
        }

        Assert.Equal(expected.Length, set.Count);
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
