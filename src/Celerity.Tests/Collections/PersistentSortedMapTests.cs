using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// The public surface of <see cref="PersistentSortedMap{TKey, TValue, TComparer}"/>: construction, the read
/// paths, the ordered queries, the persistent operations and every documented guard.
///
/// <para>
/// The randomized reconciliation against a <see cref="SortedDictionary{TKey, TValue}"/> oracle lives in
/// <see cref="PersistentSortedMapDifferentialTests"/>. What is pinned here is what a caller can read from the
/// documentation: that an edit which changes something returns a <i>new</i> map, that a write which changes
/// nothing hands back the receiver itself, that either way the receiver is left alone, that the bound queries
/// answer across leaf and subtree boundaries, that a <c>null</c> key is an ordinary key under a comparer that
/// orders it, and that the exceptions are the ones the XML docs promise.
/// </para>
/// </summary>
public class PersistentSortedMapTests
{
    // Enough keys that the tree is three levels deep (a two-level tree tops out at 32 * 32 = 1,024 entries),
    // so the reads and writes under test cross branch boundaries as well as leaf boundaries.
    private const int LargeCount = 5_000;

    private readonly struct Descending : IComparer<int>
    {
        public int Compare(int x, int y) => y.CompareTo(x);
    }

    private static PersistentSortedMap<int, string, DefaultComparer<int>> EmptyMap =>
        PersistentSortedMap<int, string, DefaultComparer<int>>.Empty;

    // ── Empty ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Empty_HasNoEntries()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = EmptyMap;

        Assert.Equal(0, map.Count);
        Assert.True(map.IsEmpty);
        Assert.False(map.ContainsKey(1));
        Assert.False(map.TryGetValue(1, out string? value));
        Assert.Null(value);
        Assert.Empty(map);
    }

    [Fact]
    public void Empty_IsTheSameInstanceEveryTime()
    {
        Assert.Same(EmptyMap, PersistentSortedMap<int, string, DefaultComparer<int>>.Empty);
    }

    [Fact]
    public void Count_AndIsEmpty_ShouldTrackEdits()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = EmptyMap.Add(1, "one");

        Assert.Equal(1, map.Count);
        Assert.False(map.IsEmpty);
        Assert.Equal(2, map.Add(2, "two").Count);
        Assert.Equal(1, map.SetItem(1, "uno").Count);
    }

    [Fact]
    public void Indexer_ShouldReturnTheStoredValue_AtEveryDepth()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = BuildRange(LargeCount);

        for (int key = 0; key < LargeCount; key += 7)
            Assert.Equal(key.ToString(), map[key]);
    }

    [Fact]
    public void Indexer_ShouldThrow_WhenKeyIsAbsent()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = EmptyMap.Add(1, "one");

        Assert.Throws<KeyNotFoundException>(() => map[2]);
        Assert.Throws<KeyNotFoundException>(() => BuildRange(LargeCount)[LargeCount]);
    }

    // ── Construction ─────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ShouldSortAnUnsortedSource()
    {
        int[] keys = [50, 3, 99, -4, 17, 0, 64];

        var map = new PersistentSortedMap<int, string, DefaultComparer<int>>(
            keys.Select(key => new KeyValuePair<int, string>(key, key.ToString())));

        Assert.Equal(keys.Length, map.Count);
        Assert.Equal(keys.Order(), map.Select(entry => entry.Key));
        Assert.Equal("17", map[17]);
    }

    [Fact]
    public void Constructor_ShouldBuildALargeTree_FromAShuffledSource()
    {
        int[] keys = Shuffled(LargeCount, seed: 11);

        var map = new PersistentSortedMap<int, string, DefaultComparer<int>>(
            keys.Select(key => new KeyValuePair<int, string>(key, key.ToString())));

        Assert.Equal(LargeCount, map.Count);
        Assert.Equal(LargeCount, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(map));
        Assert.Equal(Enumerable.Range(0, LargeCount), map.Select(entry => entry.Key));
    }

    [Fact]
    public void Constructor_ShouldAcceptAnEmptySource()
    {
        var map = new PersistentSortedMap<int, string, DefaultComparer<int>>([]);

        Assert.True(map.IsEmpty);
        Assert.Empty(map);
        Assert.Equal(1, map.Add(1, "one").Count);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTheSourceIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new PersistentSortedMap<int, string, DefaultComparer<int>>(null!));

        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_OnADuplicateKey()
    {
        KeyValuePair<int, string>[] source =
        [
            new(1, "one"),
            new(2, "two"),
            new(1, "uno"),
        ];

        Assert.Throws<ArgumentException>(() => new PersistentSortedMap<int, string, DefaultComparer<int>>(source));
    }

    // ── Add / SetItem ────────────────────────────────────────────────────────────

    [Fact]
    public void Add_ShouldReturnANewMap_AndLeaveTheReceiverAlone()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> before = EmptyMap.Add(1, "one");
        PersistentSortedMap<int, string, DefaultComparer<int>> after = before.Add(2, "two");

        Assert.Equal(1, before.Count);
        Assert.False(before.ContainsKey(2));
        Assert.Equal(2, after.Count);
        Assert.Equal("one", after[1]);
        Assert.Equal("two", after[2]);
    }

    [Fact]
    public void Add_ShouldThrow_OnADuplicateKey_AndLeaveTheReceiverUnchanged()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = BuildRange(LargeCount);

        var ex = Assert.Throws<ArgumentException>(() => map.Add(1_234, "changed"));

        Assert.Equal("key", ex.ParamName);
        Assert.Equal("1234", map[1_234]);
        Assert.Equal(LargeCount, map.Count);
    }

    [Fact]
    public void Add_ShouldThrow_OnADuplicateKey_EvenWhenTheValueMatches()
    {
        // Deliberately stricter than ImmutableSortedDictionary<,>.Add, which tolerates a duplicate whose value
        // is equal. This type matches Dictionary<,>.Add instead, and the docs say so.
        PersistentSortedMap<int, string, DefaultComparer<int>> map = EmptyMap.Add(1, "one");

        Assert.Throws<ArgumentException>(() => map.Add(1, "one"));
    }

    [Fact]
    public void SetItem_ShouldOverwrite_WithoutTouchingTheReceiver()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> before = EmptyMap.Add(1, "one");
        PersistentSortedMap<int, string, DefaultComparer<int>> after = before.SetItem(1, "uno");

        Assert.NotSame(before, after);
        Assert.Equal("one", before[1]);
        Assert.Equal("uno", after[1]);
        Assert.Equal(1, after.Count);
    }

    [Fact]
    public void SetItem_ShouldReturnTheReceiver_WhenTheValueIsAlreadyEqual()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> small = EmptyMap.Add(1, "one");
        PersistentSortedMap<int, string, DefaultComparer<int>> large = BuildRange(LargeCount);

        Assert.Same(small, small.SetItem(1, "one"));

        // The no-op has to propagate up through every branch on the path, not just the leaf.
        Assert.Same(large, large.SetItem(3_210, "3210"));
    }

    [Fact]
    public void SetItem_ShouldInsert_WhenTheKeyIsAbsent()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = EmptyMap.SetItem(7, "seven");

        Assert.Equal(1, map.Count);
        Assert.Equal("seven", map[7]);
    }

    [Fact]
    public void SetItem_ShouldWriteThroughEveryTreeDepth()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = BuildRange(LargeCount);

        PersistentSortedMap<int, string, DefaultComparer<int>> updated = map.SetItem(4_321, "changed");

        Assert.Equal("changed", updated[4_321]);
        Assert.Equal("4321", map[4_321]);
        Assert.Equal(LargeCount, updated.Count);
        Assert.Equal(LargeCount, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(updated));
    }

    [Fact]
    public void Add_ShouldGrowTheTree_AndKeepEveryEarlierVersion()
    {
        // Inserting in an order that splits leaves in the middle of the tree as well as at its edges, so the
        // persistent split and the branch insert run on interior paths, not just the rightmost one.
        int[] keys = Shuffled(LargeCount, seed: 3);
        PersistentSortedMap<int, string, DefaultComparer<int>> map = EmptyMap;
        var snapshots = new List<(PersistentSortedMap<int, string, DefaultComparer<int>> Map, int Present)>();

        for (int i = 0; i < keys.Length; i++)
        {
            map = map.Add(keys[i], keys[i].ToString());
            if (i % 499 == 0)
                snapshots.Add((map, i + 1));
        }

        Assert.Equal(LargeCount, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(map));
        foreach ((PersistentSortedMap<int, string, DefaultComparer<int>> snapshot, int present) in snapshots)
        {
            Assert.Equal(present, snapshot.Count);
            Assert.Equal(keys.Take(present).Order(), snapshot.Select(entry => entry.Key));
            Assert.Equal(present, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(snapshot));
        }
    }

    // ── Bulk writes ──────────────────────────────────────────────────────────────

    [Fact]
    public void SetItems_ShouldApplyEveryEntry_AndLetLaterEntriesWin()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = EmptyMap.Add(1, "one");

        PersistentSortedMap<int, string, DefaultComparer<int>> updated = map.SetItems(
        [
            new KeyValuePair<int, string>(1, "uno"),
            new KeyValuePair<int, string>(2, "two"),
            new KeyValuePair<int, string>(2, "dos"),
        ]);

        Assert.Equal(2, updated.Count);
        Assert.Equal("uno", updated[1]);
        Assert.Equal("dos", updated[2]);
        Assert.Equal("one", map[1]);
        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void SetItems_ShouldReturnTheReceiver_WhenNothingChanges()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = BuildRange(LargeCount);

        Assert.Same(map, map.SetItems([new KeyValuePair<int, string>(1, "1"), new KeyValuePair<int, string>(4_000, "4000")]));
        Assert.Same(map, map.SetItems([]));
    }

    [Fact]
    public void SetItems_ShouldThrow_WhenTheSourceIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => EmptyMap.SetItems(null!));

        Assert.Equal("items", ex.ParamName);
    }

    [Fact]
    public void RemoveRange_ShouldDropEveryPresentKey_AndIgnoreTheRest()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = BuildRange(LargeCount);

        PersistentSortedMap<int, string, DefaultComparer<int>> trimmed = map.RemoveRange([3, 9, 4_999, 100_000]);

        Assert.Equal(map.Count - 3, trimmed.Count);
        Assert.False(trimmed.ContainsKey(3));
        Assert.False(trimmed.ContainsKey(9));
        Assert.False(trimmed.ContainsKey(4_999));
        Assert.True(map.ContainsKey(3));
        Assert.Equal(LargeCount, map.Count);
    }

    [Fact]
    public void RemoveRange_ShouldReturnTheReceiver_WhenNoKeyIsPresent()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = BuildRange(LargeCount);

        Assert.Same(map, map.RemoveRange([-1, LargeCount, 9_001]));
        Assert.Same(map, map.RemoveRange([]));
    }

    [Fact]
    public void RemoveRange_ShouldDrainToTheEmptySingleton()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = BuildRange(100);

        Assert.Same(EmptyMap, map.RemoveRange(Enumerable.Range(0, 100)));
    }

    [Fact]
    public void RemoveRange_ShouldThrow_WhenTheSourceIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => EmptyMap.RemoveRange(null!));

        Assert.Equal("keys", ex.ParamName);
    }

    // ── Remove ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Remove_ShouldReturnTheReceiver_WhenTheKeyIsAbsent()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> small = EmptyMap.Add(1, "one");
        PersistentSortedMap<int, string, DefaultComparer<int>> large = BuildRange(LargeCount);

        Assert.Same(EmptyMap, EmptyMap.Remove(1));
        Assert.Same(small, small.Remove(2));
        Assert.Same(large, large.Remove(LargeCount));
        Assert.Same(large, large.Remove(-1));
    }

    [Fact]
    public void Remove_ShouldReturnTheEmptySingleton_WhenTheLastEntryGoes()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = EmptyMap.Add(1, "one");

        Assert.Same(EmptyMap, map.Remove(1));
        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void Remove_ShouldLeaveTheReceiverAlone()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = BuildRange(LargeCount);

        PersistentSortedMap<int, string, DefaultComparer<int>> trimmed = map.Remove(2_500);

        Assert.Equal(LargeCount - 1, trimmed.Count);
        Assert.False(trimmed.ContainsKey(2_500));
        Assert.True(map.ContainsKey(2_500));
        Assert.Equal(LargeCount, map.Count);
    }

    [Fact]
    public void Remove_ShouldRebalanceFromTheLeftAndTheRightNeighbour()
    {
        // Draining from the front underflows child 0 of every branch on the leftmost path, which has only a
        // right neighbour; draining from the back underflows the last child, which pairs with its left one.
        // Between them every borrow and every merge — for leaves and for branches — runs at both ends.
        PersistentSortedMap<int, string, DefaultComparer<int>> full = BuildRange(LargeCount);

        PersistentSortedMap<int, string, DefaultComparer<int>> front = full;
        PersistentSortedMap<int, string, DefaultComparer<int>> back = full;
        for (int i = 0; i < LargeCount - 1; i++)
        {
            front = front.Remove(i);
            back = back.Remove(LargeCount - 1 - i);

            if (i % 250 == 0)
            {
                Assert.Equal(front.Count, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(front));
                Assert.Equal(back.Count, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(back));
                Assert.Equal(i + 1, front.Min.Key);
                Assert.Equal(LargeCount - 2 - i, back.Max.Key);
            }
        }

        Assert.Equal(LargeCount - 1, front.Min.Key);
        Assert.Equal(0, back.Max.Key);
        Assert.Equal(LargeCount, full.Count);
        Assert.Equal(Enumerable.Range(0, LargeCount), full.Select(entry => entry.Key));
    }

    [Fact]
    public void Remove_InRandomOrder_ShouldKeepTheTreeValid()
    {
        // Removals scattered through the middle of a three-level tree underflow interior nodes whose neighbours
        // are sometimes fat enough to lend (redistribute) and sometimes not (merge).
        PersistentSortedMap<int, string, DefaultComparer<int>> map = BuildRange(LargeCount);
        var present = new SortedSet<int>(Enumerable.Range(0, LargeCount));

        foreach (int key in Shuffled(LargeCount, seed: 5))
        {
            map = map.Remove(key);
            present.Remove(key);

            if (present.Count % 397 == 0)
            {
                Assert.Equal(present.Count, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(map));
                Assert.Equal(present, map.Select(entry => entry.Key));
            }
        }

        Assert.Same(EmptyMap, map);
    }

    // ── Membership ───────────────────────────────────────────────────────────────

    [Fact]
    public void ContainsKey_ShouldAnswerForPresentAndAbsentKeys()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = BuildRange(LargeCount);

        Assert.True(map.ContainsKey(0));
        Assert.True(map.ContainsKey(LargeCount - 1));
        Assert.True(map.ContainsKey(2_048));
        Assert.False(map.ContainsKey(-1));
        Assert.False(map.ContainsKey(LargeCount));
    }

    [Fact]
    public void ContainsValue_ShouldScanEveryEntry()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = BuildRange(LargeCount).SetItem(LargeCount, null!);

        Assert.True(map.ContainsValue("0"));
        Assert.True(map.ContainsValue("4999"));
        Assert.True(map.ContainsValue(null));
        Assert.False(map.ContainsValue("missing"));
        Assert.False(EmptyMap.ContainsValue("0"));
    }

    // ── Min / Max ────────────────────────────────────────────────────────────────

    [Fact]
    public void MinAndMax_ShouldReturnTheEndsOfTheOrder()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = BuildRange(LargeCount);

        Assert.Equal(new KeyValuePair<int, string?>(0, "0"), map.Min);
        Assert.Equal(new KeyValuePair<int, string?>(LargeCount - 1, "4999"), map.Max);

        PersistentSortedMap<int, string, DefaultComparer<int>> single = EmptyMap.Add(5, "five");
        Assert.Equal(5, single.Min.Key);
        Assert.Equal(5, single.Max.Key);
    }

    [Fact]
    public void MinAndMax_ShouldThrow_OnAnEmptyMap()
    {
        Assert.Throws<InvalidOperationException>(() => EmptyMap.Min);
        Assert.Throws<InvalidOperationException>(() => EmptyMap.Max);
    }

    [Fact]
    public void TryGetMinAndMax_ShouldReportEmptiness()
    {
        Assert.False(EmptyMap.TryGetMin(out KeyValuePair<int, string?> min));
        Assert.Equal(default, min);
        Assert.False(EmptyMap.TryGetMax(out KeyValuePair<int, string?> max));
        Assert.Equal(default, max);

        PersistentSortedMap<int, string, DefaultComparer<int>> map = BuildRange(LargeCount);
        Assert.True(map.TryGetMin(out min));
        Assert.Equal(0, min.Key);
        Assert.True(map.TryGetMax(out max));
        Assert.Equal(LargeCount - 1, max.Key);
    }

    // ── Lower / upper bound ──────────────────────────────────────────────────────

    [Fact]
    public void LowerAndUpperBound_ShouldAnswerExactHits_Gaps_AndBothEnds()
    {
        // Even keys only, so every odd probe falls strictly between two keys.
        PersistentSortedMap<int, string, DefaultComparer<int>> map = BuildEven(10);

        Assert.True(map.TryGetLowerBound(6, out KeyValuePair<int, string?> entry));
        Assert.Equal(new KeyValuePair<int, string?>(6, "6"), entry);
        Assert.True(map.TryGetUpperBound(6, out entry));
        Assert.Equal(8, entry.Key);

        Assert.True(map.TryGetLowerBound(7, out entry));
        Assert.Equal(8, entry.Key);
        Assert.True(map.TryGetUpperBound(7, out entry));
        Assert.Equal(8, entry.Key);

        Assert.True(map.TryGetLowerBound(-5, out entry));
        Assert.Equal(0, entry.Key);
        Assert.True(map.TryGetUpperBound(-5, out entry));
        Assert.Equal(0, entry.Key);

        Assert.True(map.TryGetLowerBound(18, out entry));
        Assert.Equal(18, entry.Key);
        Assert.False(map.TryGetUpperBound(18, out entry));
        Assert.Equal(default, entry);
        Assert.False(map.TryGetLowerBound(19, out entry));
        Assert.Equal(default, entry);
    }

    [Fact]
    public void LowerAndUpperBound_ShouldReportNothing_OnAnEmptyMap()
    {
        Assert.False(EmptyMap.TryGetLowerBound(0, out KeyValuePair<int, string?> entry));
        Assert.Equal(default, entry);
        Assert.False(EmptyMap.TryGetUpperBound(0, out entry));
        Assert.Equal(default, entry);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(1_500)]
    [InlineData(LargeCount)]
    public void LowerAndUpperBound_ShouldCrossIntoTheNextLeafAndSubtree(int count)
    {
        // A probe just past the last key of a leaf finds nothing in that leaf; the answer is the first entry
        // of the nearest subtree to the right of the descent — a sibling leaf, or, at a leaf that ends its
        // parent, the leftmost leaf of an uncle subtree one or more levels up. Probing every key and every gap
        // in multi-level trees hits each of those.
        PersistentSortedMap<int, string, DefaultComparer<int>> map = BuildEven(count);

        for (int probe = -1; probe <= 2 * count; probe++)
        {
            int lower = probe <= 0 ? 0 : (probe + 1) & ~1;
            int upper = probe < 0 ? 0 : (probe + 2) & ~1;

            Assert.Equal(lower < 2 * count, map.TryGetLowerBound(probe, out KeyValuePair<int, string?> entry));
            if (lower < 2 * count)
                Assert.Equal(new KeyValuePair<int, string?>(lower, lower.ToString()), entry);

            Assert.Equal(upper < 2 * count, map.TryGetUpperBound(probe, out entry));
            if (upper < 2 * count)
                Assert.Equal(upper, entry.Key);
        }
    }

    // ── Floor / predecessor ──────────────────────────────────────────────────────

    [Fact]
    public void FloorAndPredecessor_ShouldAnswerExactHits_Gaps_AndBothEnds()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = BuildEven(10);

        Assert.True(map.TryGetFloor(6, out KeyValuePair<int, string?> entry));
        Assert.Equal(new KeyValuePair<int, string?>(6, "6"), entry);
        Assert.True(map.TryGetPredecessor(6, out entry));
        Assert.Equal(4, entry.Key);

        Assert.True(map.TryGetFloor(7, out entry));
        Assert.Equal(6, entry.Key);
        Assert.True(map.TryGetPredecessor(7, out entry));
        Assert.Equal(6, entry.Key);

        Assert.True(map.TryGetFloor(100, out entry));
        Assert.Equal(18, entry.Key);
        Assert.True(map.TryGetPredecessor(100, out entry));
        Assert.Equal(18, entry.Key);

        Assert.True(map.TryGetFloor(0, out entry));
        Assert.Equal(0, entry.Key);
        Assert.False(map.TryGetPredecessor(0, out entry));
        Assert.Equal(default, entry);
        Assert.False(map.TryGetFloor(-1, out entry));
        Assert.Equal(default, entry);
    }

    [Fact]
    public void FloorAndPredecessor_ShouldReportNothing_OnAnEmptyMap()
    {
        Assert.False(EmptyMap.TryGetFloor(0, out KeyValuePair<int, string?> entry));
        Assert.Equal(default, entry);
        Assert.False(EmptyMap.TryGetPredecessor(0, out entry));
        Assert.Equal(default, entry);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(1_500)]
    [InlineData(LargeCount)]
    public void FloorAndPredecessor_ShouldCrossIntoThePreviousLeafAndSubtree(int count)
    {
        // The mirror of the lower-bound case: a probe equal to a leaf's first key (or in the gap before it)
        // finds nothing at or below it in that leaf, and the answer is the last entry of the nearest subtree to
        // the left of the descent — a sibling leaf, or the rightmost leaf of a subtree one or more levels up.
        PersistentSortedMap<int, string, DefaultComparer<int>> map = BuildEven(count);

        for (int probe = -1; probe <= 2 * count; probe++)
        {
            int last = 2 * (count - 1);
            int floor = Math.Min(probe & ~1, last);
            int predecessor = Math.Min((probe - 1) & ~1, last);

            Assert.Equal(floor >= 0, map.TryGetFloor(probe, out KeyValuePair<int, string?> entry));
            if (floor >= 0)
                Assert.Equal(new KeyValuePair<int, string?>(floor, floor.ToString()), entry);

            Assert.Equal(predecessor >= 0, map.TryGetPredecessor(probe, out entry));
            if (predecessor >= 0)
                Assert.Equal(predecessor, entry.Key);
        }
    }

    // ── EnumerateRange ───────────────────────────────────────────────────────────

    [Fact]
    public void EnumerateRange_ShouldThrow_WhenTheBoundsAreInverted()
    {
        var ex = Assert.Throws<ArgumentException>(() => BuildRange(10).EnumerateRange(5, 4));

        Assert.Equal("toExclusive", ex.ParamName);
    }

    [Fact]
    public void EnumerateRange_ShouldBeEmpty_WhenTheBoundsAreEqual()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = BuildRange(100);

        Assert.Empty(map.EnumerateRange(50, 50));
        Assert.Empty(EmptyMap.EnumerateRange(0, 10));
    }

    [Fact]
    public void EnumerateRange_ShouldBeHalfOpen()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = BuildRange(100);

        Assert.Equal([10, 11, 12], map.EnumerateRange(10, 13).Select(entry => entry.Key));
        Assert.Equal([98, 99], map.EnumerateRange(98, 1_000).Select(entry => entry.Key));
        Assert.Equal([0, 1], map.EnumerateRange(-10, 2).Select(entry => entry.Key));
        Assert.Empty(map.EnumerateRange(100, 200));
        Assert.Empty(map.EnumerateRange(-20, -10));
    }

    [Fact]
    public void EnumerateRange_ShouldSpanManyLeavesAndSubtrees()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = BuildEven(LargeCount);

        // Odd bounds land between keys at both ends, so the seek and the stop both work off a gap.
        int[] expected = Enumerable.Range(0, LargeCount).Select(i => 2 * i).Where(k => k >= 501 && k < 8_999).ToArray();
        Assert.Equal(expected, map.EnumerateRange(501, 8_999).Select(entry => entry.Key));
        Assert.Equal(Enumerable.Range(0, LargeCount).Select(i => 2 * i), map.EnumerateRange(int.MinValue, int.MaxValue).Select(entry => entry.Key));
    }

    // ── Null keys ────────────────────────────────────────────────────────────────

    [Fact]
    public void NullKey_ShouldBeALegalKey_ThatOrdersFirst()
    {
        // Comparer<string>.Default orders null before every string, so unlike the hashed family this map
        // needs no out-of-band slot for it: null is just the smallest key.
        PersistentSortedMap<string, string, DefaultComparer<string>> map =
            PersistentSortedMap<string, string, DefaultComparer<string>>.Empty
                .Add("b", "bee")
                .Add(null!, "nothing")
                .Add("a", "ay");

        Assert.Equal(3, map.Count);
        Assert.True(map.ContainsKey(null!));
        Assert.Equal("nothing", map[null!]);
        Assert.Null(map.Min.Key);
        Assert.Equal([null, "a", "b"], map.Select(entry => entry.Key));

        Assert.True(map.TryGetLowerBound(null!, out KeyValuePair<string, string?> entry));
        Assert.Null(entry.Key);
        Assert.True(map.TryGetUpperBound(null!, out entry));
        Assert.Equal("a", entry.Key);
        Assert.Equal([null, "a"], map.EnumerateRange(null!, "b").Select(e => e.Key));

        Assert.Throws<ArgumentException>(() => map.Add(null!, "again"));
        Assert.Equal("none", map.SetItem(null!, "none")[null!]);

        PersistentSortedMap<string, string, DefaultComparer<string>> without = map.Remove(null!);
        Assert.False(without.ContainsKey(null!));
        Assert.Equal("a", without.Min.Key);
        Assert.True(map.ContainsKey(null!));
    }

    // ── Custom comparer ──────────────────────────────────────────────────────────

    [Fact]
    public void ADescendingComparer_ShouldReverseEveryOrderedQuery()
    {
        PersistentSortedMap<int, string, Descending> map = PersistentSortedMap<int, string, Descending>.Empty;
        for (int i = 0; i < 200; i++)
            map = map.Add(2 * i, i.ToString());

        Assert.Equal(398, map.Min.Key);
        Assert.Equal(0, map.Max.Key);
        Assert.Equal(Enumerable.Range(0, 200).Select(i => 2 * i).Reverse(), map.Select(entry => entry.Key));

        // "At or after" now means "at or below".
        Assert.True(map.TryGetLowerBound(51, out KeyValuePair<int, string?> entry));
        Assert.Equal(50, entry.Key);
        Assert.True(map.TryGetUpperBound(50, out entry));
        Assert.Equal(48, entry.Key);
        Assert.False(map.TryGetUpperBound(0, out _));

        Assert.Equal([60, 58, 56], map.EnumerateRange(61, 55).Select(e => e.Key));
        Assert.Throws<ArgumentException>(() => map.EnumerateRange(55, 61));
    }

    // ── Persistence ──────────────────────────────────────────────────────────────

    [Fact]
    public void OldVersions_ShouldBeUnchanged_AfterManyEditsOnADerivedVersion()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> original = BuildRange(LargeCount);
        PersistentSortedMap<int, string, DefaultComparer<int>> derived = original;
        var rng = new Random(17);

        for (int step = 0; step < 6_000; step++)
        {
            int key = rng.Next(LargeCount + 500);
            derived = rng.Next(3) == 0 ? derived.Remove(key) : derived.SetItem(key, "x" + step);
        }

        Assert.Equal(LargeCount, original.Count);
        Assert.Equal(LargeCount, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(original));
        Assert.Equal(derived.Count, PersistentSortedMapShape.AssertValid<int, DefaultComparer<int>>(derived));

        int key2 = 0;
        foreach (KeyValuePair<int, string?> entry in original)
        {
            Assert.Equal(key2, entry.Key);
            Assert.Equal(key2.ToString(), entry.Value);
            key2++;
        }
    }

    // Keys 0..count-1 in ascending order, valued with their decimal text.
    private static PersistentSortedMap<int, string, DefaultComparer<int>> BuildRange(int count)
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = EmptyMap;
        for (int key = 0; key < count; key++)
            map = map.Add(key, key.ToString());

        return map;
    }

    // Keys 0, 2, ..., 2 * (count - 1), so every odd number is a gap.
    private static PersistentSortedMap<int, string, DefaultComparer<int>> BuildEven(int count)
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = EmptyMap;
        for (int i = 0; i < count; i++)
            map = map.Add(2 * i, (2 * i).ToString());

        return map;
    }

    private static int[] Shuffled(int count, int seed)
    {
        int[] keys = Enumerable.Range(0, count).ToArray();
        new Random(seed).Shuffle(keys);
        return keys;
    }
}
