using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// The public surface of <see cref="PersistentSortedSet{T, TComparer}"/>: construction, the read paths, the
/// ordered queries, the persistent operations, the set algebra and every documented guard.
///
/// <para>
/// The randomized reconciliation against a <see cref="SortedSet{T}"/> oracle lives in
/// <see cref="PersistentSortedSetDifferentialTests"/>. What is pinned here is what a caller can read from the
/// documentation: that an edit which changes something returns a <i>new</i> set, that one which changes nothing
/// hands back the receiver itself, that either way the receiver is left alone, that equality is the comparer's
/// throughout — the set algebra included — that a <c>null</c> element is an ordinary element under a comparer
/// that orders it, and that the exceptions are the ones the XML docs promise.
/// </para>
/// </summary>
public class PersistentSortedSetTests
{
    // Enough elements that the tree is three levels deep (a two-level tree tops out at 32 * 32 = 1,024), so the
    // reads and writes under test cross branch boundaries as well as leaf boundaries.
    private const int LargeCount = 5_000;

    // A case-insensitive order: "a" and "A" are one element. The comparer carries its configuration in its type,
    // as the set requires.
    internal readonly struct IgnoreCase : IComparer<string>
    {
        public int Compare(string? x, string? y) => StringComparer.OrdinalIgnoreCase.Compare(x, y);
    }

    private static PersistentSortedSet<int, DefaultComparer<int>> EmptySet =>
        PersistentSortedSet<int, DefaultComparer<int>>.Empty;

    private static PersistentSortedSet<int, DefaultComparer<int>> BuildRange(int count) =>
        new(Enumerable.Range(0, count));

    // ── Empty ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Empty_HasNoElements()
    {
        PersistentSortedSet<int, DefaultComparer<int>> set = EmptySet;

        Assert.Equal(0, set.Count);
        Assert.True(set.IsEmpty);
        Assert.False(set.Contains(1));
        Assert.False(set.TryGetMin(out _));
        Assert.False(set.TryGetMax(out _));
        Assert.False(set.TryGetLowerBound(1, out _));
        Assert.False(set.TryGetUpperBound(1, out _));
        Assert.False(set.TryGetFloor(1, out _));
        Assert.False(set.TryGetPredecessor(1, out _));
        Assert.Empty(set);
        Assert.Empty(set.EnumerateRange(0, 10));
    }

    [Fact]
    public void Empty_IsTheSameInstanceEveryTime()
    {
        Assert.Same(EmptySet, PersistentSortedSet<int, DefaultComparer<int>>.Empty);
    }

    [Fact]
    public void MinAndMax_ShouldThrow_WhenTheSetIsEmpty()
    {
        Assert.Throws<InvalidOperationException>(() => EmptySet.Min);
        Assert.Throws<InvalidOperationException>(() => EmptySet.Max);
    }

    [Fact]
    public void MinAndMax_ShouldReadTheEnds_OfAMultiLevelTree()
    {
        PersistentSortedSet<int, DefaultComparer<int>> set = BuildRange(LargeCount);

        Assert.Equal(0, set.Min);
        Assert.Equal(LargeCount - 1, set.Max);
        Assert.True(set.TryGetMin(out int min));
        Assert.True(set.TryGetMax(out int max));
        Assert.Equal((0, LargeCount - 1), (min, max));
    }

    // ── Construction ─────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ShouldSortAndDeduplicateAnUnsortedSource()
    {
        int[] source = [50, 3, 99, -4, 17, 0, 64, 3, 50, 50];

        var set = new PersistentSortedSet<int, DefaultComparer<int>>(source);

        Assert.Equal(source.Distinct().Order(), set);
        Assert.Equal(7, set.Count);
    }

    [Fact]
    public void Constructor_ShouldBuildALargeTree_FromAShuffledSource()
    {
        int[] source = Enumerable.Range(0, LargeCount).OrderBy(i => (i * 7919) % LargeCount).ToArray();

        var set = new PersistentSortedSet<int, DefaultComparer<int>>(source);

        Assert.Equal(LargeCount, set.Count);
        Assert.Equal(LargeCount, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(set));
        Assert.Equal(Enumerable.Range(0, LargeCount), set);
    }

    [Fact]
    public void Constructor_ShouldAcceptAnEmptySource()
    {
        var set = new PersistentSortedSet<int, DefaultComparer<int>>([]);

        Assert.True(set.IsEmpty);
        Assert.Empty(set);
        Assert.Equal(1, set.Add(1).Count);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTheSourceIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new PersistentSortedSet<int, DefaultComparer<int>>(null!));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldKeepTheFirstOfARunOfComparerEqualElements()
    {
        var set = new PersistentSortedSet<string, IgnoreCase>(["Apple", "apple", "APPLE", "banana"]);

        Assert.Equal(["Apple", "banana"], set);
    }

    // ── Reads ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Contains_ShouldFindEveryElement_AtEveryDepth_AndMissTheGaps()
    {
        var set = new PersistentSortedSet<int, DefaultComparer<int>>(Enumerable.Range(0, LargeCount).Select(i => 2 * i));

        for (int i = 0; i < 2 * LargeCount; i++)
            Assert.Equal(i % 2 == 0, set.Contains(i));

        Assert.False(set.Contains(-1));
        Assert.False(set.Contains(2 * LargeCount));
    }

    [Fact]
    public void TryGetValue_ShouldReturnTheStoredInstance_OnAHit()
    {
        var set = new PersistentSortedSet<string, IgnoreCase>(["Apple", "banana"]);

        Assert.True(set.TryGetValue("APPLE", out string actual));
        Assert.Equal("Apple", actual);
        Assert.True(set.Contains("BANANA"));
    }

    [Fact]
    public void TryGetValue_ShouldHandBackTheArgument_OnAMiss()
    {
        var set = new PersistentSortedSet<string, IgnoreCase>(["Apple"]);

        Assert.False(set.TryGetValue("cherry", out string actual));
        Assert.Equal("cherry", actual);
    }

    [Fact]
    public void NullElement_ShouldBeAnOrdinaryElement_UnderTheDefaultComparer()
    {
        PersistentSortedSet<string?, DefaultComparer<string?>> set =
            PersistentSortedSet<string?, DefaultComparer<string?>>.Empty.Add("b").Add(null).Add("a");

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(null));
        Assert.Null(set.Min);
        Assert.Equal([null, "a", "b"], set);
        Assert.True(set.TryGetUpperBound(null, out string? next));
        Assert.Equal("a", next);
        Assert.Same(set, set.Add(null));
        Assert.Equal(["a", "b"], set.Remove(null));
    }

    // ── Ordered queries ──────────────────────────────────────────────────────────

    [Fact]
    public void BoundQueries_ShouldAnswerAcrossLeafAndBranchBoundaries()
    {
        // Even elements 0, 2, …, so every odd probe falls between two elements — often between two leaves.
        var set = new PersistentSortedSet<int, DefaultComparer<int>>(Enumerable.Range(0, LargeCount).Select(i => 2 * i));
        int last = 2 * (LargeCount - 1);

        for (int probe = -1; probe <= last + 1; probe++)
        {
            int ceiling = probe < 0 ? 0 : probe + (probe & 1);
            int successor = probe < 0 ? 0 : probe + 2 - (probe & 1);
            int floor = probe - (probe & 1);
            int predecessor = probe - 2 + (probe & 1);

            Assert.Equal(ceiling <= last, set.TryGetLowerBound(probe, out int lower));
            if (ceiling <= last)
                Assert.Equal(ceiling, lower);

            Assert.Equal(successor <= last, set.TryGetUpperBound(probe, out int upper));
            if (successor <= last)
                Assert.Equal(successor, upper);

            Assert.Equal(floor >= 0, set.TryGetFloor(probe, out int floorResult));
            if (floor >= 0)
                Assert.Equal(Math.Min(floor, last), floorResult);

            Assert.Equal(predecessor >= 0, set.TryGetPredecessor(probe, out int before));
            if (predecessor >= 0)
                Assert.Equal(Math.Min(predecessor, last), before);
        }
    }

    [Fact]
    public void EnumerateRange_ShouldYieldTheHalfOpenRange_InOrder()
    {
        PersistentSortedSet<int, DefaultComparer<int>> set = BuildRange(LargeCount);

        Assert.Equal(Enumerable.Range(1000, 2000), set.EnumerateRange(1000, 3000));
        Assert.Equal(Enumerable.Range(0, LargeCount), set.EnumerateRange(-5, LargeCount + 5));
        Assert.Empty(set.EnumerateRange(42, 42));
        Assert.Empty(set.EnumerateRange(LargeCount, LargeCount + 10));
    }

    [Fact]
    public void EnumerateRange_ShouldThrow_WhenTheRangeIsInverted()
    {
        var ex = Assert.Throws<ArgumentException>(() => BuildRange(10).EnumerateRange(5, 4));

        Assert.Equal("toExclusive", ex.ParamName);
    }

    [Fact]
    public void Queries_ShouldFollowTheComparer_NotTheNaturalOrder()
    {
        var set = new PersistentSortedSet<int, PersistentSortedSetDifferentialTests.Descending>(Enumerable.Range(0, 100));

        Assert.Equal(99, set.Min);
        Assert.Equal(0, set.Max);
        Assert.Equal(Enumerable.Range(0, 100).Reverse(), set);
        Assert.True(set.TryGetLowerBound(50, out int lower));
        Assert.Equal(50, lower);
        Assert.True(set.TryGetUpperBound(50, out int upper));
        Assert.Equal(49, upper);
        Assert.Equal([60, 59, 58], set.EnumerateRange(60, 57));
    }

    // ── Persistent edits ─────────────────────────────────────────────────────────

    [Fact]
    public void Add_ShouldReturnANewSet_AndLeaveTheReceiverAlone()
    {
        PersistentSortedSet<int, DefaultComparer<int>> original = BuildRange(LargeCount);

        PersistentSortedSet<int, DefaultComparer<int>> grown = original.Add(-1).Add(LargeCount);

        Assert.Equal(LargeCount + 2, grown.Count);
        Assert.Equal(-1, grown.Min);
        Assert.Equal(LargeCount, grown.Max);
        Assert.Equal(LargeCount, original.Count);
        Assert.False(original.Contains(-1));
        Assert.Equal(LargeCount + 2, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(grown));
    }

    [Fact]
    public void Add_ShouldHandBackTheReceiver_AndKeepTheStoredInstance_WhenAnEqualElementIsPresent()
    {
        var set = new PersistentSortedSet<string, IgnoreCase>(["Apple"]);

        Assert.Same(set, set.Add("APPLE"));
        Assert.Equal(["Apple"], set);
    }

    [Fact]
    public void Remove_ShouldReturnANewSet_AndLeaveTheReceiverAlone()
    {
        PersistentSortedSet<int, DefaultComparer<int>> original = BuildRange(LargeCount);

        PersistentSortedSet<int, DefaultComparer<int>> trimmed = original.Remove(0).Remove(LargeCount / 2);

        Assert.Equal(LargeCount - 2, trimmed.Count);
        Assert.False(trimmed.Contains(LargeCount / 2));
        Assert.True(original.Contains(LargeCount / 2));
        Assert.Equal(LargeCount, original.Count);
    }

    [Fact]
    public void Remove_ShouldHandBackTheReceiver_WhenTheElementIsAbsent()
    {
        PersistentSortedSet<int, DefaultComparer<int>> set = BuildRange(LargeCount);

        Assert.Same(set, set.Remove(-1));
        Assert.Same(set, set.Remove(LargeCount));
    }

    [Fact]
    public void Remove_ShouldReturnEmpty_WhenTheLastElementGoes()
    {
        Assert.Same(EmptySet, EmptySet.Add(7).Remove(7));
        Assert.Same(EmptySet, EmptySet.Remove(7));
    }

    [Fact]
    public void ToBuilder_ShouldNotWriteThroughToTheSourceSet()
    {
        PersistentSortedSet<int, DefaultComparer<int>> original = BuildRange(LargeCount);

        PersistentSortedSet<int, DefaultComparer<int>>.Builder builder = original.ToBuilder();
        for (int i = 0; i < LargeCount; i += 2)
            builder.Remove(i);
        builder.Add(-10);

        Assert.Equal(Enumerable.Range(0, LargeCount), original);
        Assert.Equal(LargeCount / 2 + 1, builder.ToImmutable().Count);
    }

    // ── Set algebra: producing operations ────────────────────────────────────────

    [Fact]
    public void Union_ShouldAddTheOtherElements_AndHandBackTheReceiver_WhenNothingIsNew()
    {
        PersistentSortedSet<int, DefaultComparer<int>> set = BuildRange(10);

        Assert.Equal(Enumerable.Range(0, 15), set.Union([12, 10, 14, 11, 13, 13]));
        Assert.Same(set, set.Union([1, 2, 3]));
        Assert.Same(set, set.Union([]));
        Assert.Same(set, set.Union(set));
    }

    [Fact]
    public void Except_ShouldRemoveTheOtherElements_AndHandBackTheReceiver_WhenNoneArePresent()
    {
        PersistentSortedSet<int, DefaultComparer<int>> set = BuildRange(10);

        Assert.Equal([0, 2, 4, 6, 8], set.Except([1, 3, 5, 7, 9, 11]));
        Assert.Same(set, set.Except([20, 30]));
        Assert.Same(EmptySet, set.Except(set));
    }

    [Fact]
    public void Except_OfItself_ShouldHandBackAnEmptyReceiver_RatherThanTheSingleton()
    {
        var empty = new PersistentSortedSet<int, DefaultComparer<int>>([]);

        Assert.NotSame(EmptySet, empty);
        Assert.Same(empty, empty.Except(empty));
        Assert.Same(empty, empty.SymmetricExcept(empty));
    }

    [Fact]
    public void Intersect_ShouldKeepOnlySharedElements_AndHandBackTheReceiver_WhenAllAreShared()
    {
        PersistentSortedSet<int, DefaultComparer<int>> set = BuildRange(10);

        Assert.Equal([2, 4], set.Intersect([4, 2, 2, 40]));
        Assert.Same(EmptySet, set.Intersect([40, 50]));
        Assert.Same(EmptySet, set.Intersect([]));
        Assert.Same(set, set.Intersect(Enumerable.Range(-5, 30)));
        Assert.Same(set, set.Intersect(set));
        Assert.Same(EmptySet, EmptySet.Intersect([1, 2]));
    }

    [Fact]
    public void Intersect_ShouldKeepTheStoredInstances_NotTheOperandsEqualOnes()
    {
        var set = new PersistentSortedSet<string, IgnoreCase>(["Apple", "Banana", "Cherry"]);

        Assert.Equal(["Apple", "Cherry"], set.Intersect(["CHERRY", "apple", "durian"]));
    }

    [Fact]
    public void SymmetricExcept_ShouldToggleEachDistinctElementOnce()
    {
        PersistentSortedSet<int, DefaultComparer<int>> set = BuildRange(5);

        Assert.Equal([0, 1, 3, 5, 6], set.SymmetricExcept([2, 4, 5, 6, 6, 4]));
        Assert.Same(set, set.SymmetricExcept([]));
        Assert.Same(EmptySet, set.SymmetricExcept(set));
    }

    [Fact]
    public void SymmetricExcept_ShouldCollapseTheOperandUnderTheComparer()
    {
        // "a" and "A" are one element under IgnoreCase, so it toggles once — in, not in-then-out.
        var set = new PersistentSortedSet<string, IgnoreCase>(["b"]);

        Assert.Equal(["a", "b"], set.SymmetricExcept(["a", "A"]));
        Assert.Equal(["c"], set.SymmetricExcept(["B", "c", "C"]));
    }

    // ── Set algebra: queries ─────────────────────────────────────────────────────

    [Fact]
    public void Queries_ShouldAnswerTheSameInstanceWithoutEnumeratingIt()
    {
        PersistentSortedSet<int, DefaultComparer<int>> set = BuildRange(10);

        Assert.True(set.SetEquals(set));
        Assert.True(set.IsSubsetOf(set));
        Assert.False(set.IsProperSubsetOf(set));
        Assert.True(set.IsSupersetOf(set));
        Assert.False(set.IsProperSupersetOf(set));
        Assert.True(set.Overlaps(set));
        Assert.False(EmptySet.Overlaps(EmptySet));
    }

    [Fact]
    public void Queries_ShouldAnswerAgainstOtherSequences()
    {
        PersistentSortedSet<int, DefaultComparer<int>> set = BuildRange(5);

        Assert.True(set.SetEquals([4, 3, 2, 1, 0, 0]));
        Assert.False(set.SetEquals([0, 1, 2, 3]));
        Assert.True(set.IsSubsetOf([0, 1, 2, 3, 4, 5]));
        Assert.False(set.IsSubsetOf([0, 1, 2]));
        Assert.True(EmptySet.IsSubsetOf([]));
        Assert.True(set.IsProperSubsetOf([0, 1, 2, 3, 4, 5]));
        Assert.False(set.IsProperSubsetOf([0, 1, 2, 3, 4]));
        Assert.True(set.IsSupersetOf([1, 3, 3]));
        Assert.False(set.IsSupersetOf([1, 7]));
        Assert.True(set.IsProperSupersetOf([1, 3]));
        Assert.False(set.IsProperSupersetOf([0, 1, 2, 3, 4]));
        Assert.True(set.IsProperSupersetOf([]));
        Assert.True(set.Overlaps([9, 8, 4]));
        Assert.False(set.Overlaps([9, 8]));
        Assert.False(EmptySet.Overlaps([1]));
    }

    [Fact]
    public void Queries_ShouldUseTheComparersEquality()
    {
        // Under IgnoreCase, ["A", "a", "B"] is two elements — the same two this set holds. A HashSet keyed by
        // default string equality would count three and answer every one of these the other way.
        var set = new PersistentSortedSet<string, IgnoreCase>(["a", "b"]);
        string[] other = ["A", "a", "B"];

        Assert.True(set.SetEquals(other));
        Assert.True(set.IsSubsetOf(other));
        Assert.False(set.IsProperSubsetOf(other));
        Assert.True(set.IsSupersetOf(other));
        Assert.False(set.IsProperSupersetOf(other));
        Assert.True(set.Overlaps(["B"]));
    }

    [Fact]
    public void EverySetOperation_ShouldThrow_WhenOtherIsNull()
    {
        PersistentSortedSet<int, DefaultComparer<int>> set = BuildRange(3);
        IEnumerable<int> other = null!;

        Action[] calls =
        [
            () => set.Union(other),
            () => set.Except(other),
            () => set.Intersect(other),
            () => set.SymmetricExcept(other),
            () => set.SetEquals(other),
            () => set.IsSubsetOf(other),
            () => set.IsProperSubsetOf(other),
            () => set.IsSupersetOf(other),
            () => set.IsProperSupersetOf(other),
            () => set.Overlaps(other),
        ];

        foreach (Action call in calls)
            Assert.Equal("other", Assert.Throws<ArgumentNullException>(call).ParamName);
    }

    // ── Interfaces ───────────────────────────────────────────────────────────────

    [Fact]
    public void ReadOnlySetInterface_ShouldExposeTheSameAnswers()
    {
        IReadOnlySet<int> set = BuildRange(10);

        Assert.Equal(10, set.Count);
        Assert.True(set.Contains(9));
        Assert.True(set.IsSupersetOf([1, 2]));
        Assert.Equal(Enumerable.Range(0, 10), set);
        Assert.Equal(Enumerable.Range(0, 10), ((System.Collections.IEnumerable)set).Cast<int>());
    }
}
