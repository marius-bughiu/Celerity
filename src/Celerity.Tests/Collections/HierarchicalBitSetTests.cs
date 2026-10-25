using Celerity.Collections;

namespace Celerity.Tests.Collections;

// Issue #531: HierarchicalBitSet, an ordered integer set over a bounded universe — a bit vector with a
// 64-ary tree of summary words above it, so the successor / predecessor queries read a few words.
//
// These tests mirror SparseSetTests for the bounded-universe ISet<int> surface (out-of-range values throw
// on Add and read as absent on Contains / Remove), and BTreeSetTests for the ordered surface. What is
// specific to this type is the summary tree: a summary bit must be set exactly when its child word is
// non-zero, so the cases below cross word and level boundaries on purpose — the universes 64, 4096 and
// 262,144 are the points where a level is added. The randomized oracle comparison, including the
// set algebra, lives in HierarchicalBitSetDifferentialTests: like SparseSet, this type cannot join the
// int-parameterized SetAlgebraDifferentialTests, whose values span negatives.
public class HierarchicalBitSetTests
{
    [Fact]
    public void Add_ShouldStoreElement()
    {
        var set = new HierarchicalBitSet(100);
        set.Add(42);

        Assert.True(set.Contains(42));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Add_ShouldThrow_OnDuplicate()
    {
        var set = new HierarchicalBitSet(100);
        set.Add(7);

        var ex = Assert.Throws<ArgumentException>(() => set.Add(7));
        Assert.Equal("item", ex.ParamName);
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void TryAdd_ShouldReturnTrueThenFalse()
    {
        var set = new HierarchicalBitSet(100);

        Assert.True(set.TryAdd(3));
        Assert.False(set.TryAdd(3));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Contains_ShouldReturnFalse_ForAbsentElement()
    {
        var set = new HierarchicalBitSet(100);
        set.Add(1);

        Assert.False(set.Contains(2));
        Assert.False(set.Contains(65));
    }

    [Fact]
    public void ZeroElement_ShouldBeHandled_AsOrdinaryElement()
    {
        var set = new HierarchicalBitSet(10);

        Assert.False(set.Contains(0));
        set.Add(0);
        Assert.True(set.Contains(0));
        Assert.True(set.TryGetMin(out int min));
        Assert.Equal(0, min);
        Assert.True(set.Remove(0));
        Assert.False(set.Contains(0));
    }

    [Fact]
    public void Add_ShouldStoreTopOfUniverse()
    {
        var set = new HierarchicalBitSet(130);
        set.Add(129);

        Assert.True(set.Contains(129));
        Assert.True(set.TryGetMax(out int max));
        Assert.Equal(129, max);
    }

    // ── Out-of-range handling ─────────────────────────────────────────────────

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Add_ShouldThrow_WhenOutOfRange(int value)
    {
        var set = new HierarchicalBitSet(100);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => set.Add(value));
        Assert.Equal("item", ex.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void TryAdd_ShouldThrow_WhenOutOfRange(int value)
    {
        var set = new HierarchicalBitSet(100);

        Assert.Throws<ArgumentOutOfRangeException>(() => set.TryAdd(value));
        Assert.Equal(0, set.Count);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void Contains_ShouldReturnFalse_WhenOutOfRange(int value)
    {
        var set = new HierarchicalBitSet(100);
        Assert.False(set.Contains(value));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public void Remove_ShouldReturnFalse_WhenOutOfRange(int value)
    {
        var set = new HierarchicalBitSet(100);
        Assert.False(set.Remove(value));
    }

    // ── Remove ────────────────────────────────────────────────────────────────

    [Fact]
    public void Remove_ShouldDeleteElementAndMakeItUnreachable()
    {
        var set = new HierarchicalBitSet(100);
        set.Add(10);
        set.Add(20);

        Assert.True(set.Remove(10));
        Assert.False(set.Contains(10));
        Assert.True(set.Contains(20));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenElementDoesNotExist()
    {
        var set = new HierarchicalBitSet(100);
        set.Add(10);

        Assert.False(set.Remove(11));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Remove_ShouldKeepSummary_WhenWordStillHasOtherElements()
    {
        // 3 and 5 share leaf word 0; removing one must leave the word reachable through the summary.
        var set = new HierarchicalBitSet(10_000);
        set.Add(3);
        set.Add(5);
        set.Remove(3);

        Assert.True(set.TryGetLowerBound(0, out int bound));
        Assert.Equal(5, bound);
    }

    [Fact]
    public void Remove_ShouldClearSummary_WhenWordEmpties()
    {
        // 100 is alone in leaf word 1; once it goes, a successor query from 0 must skip to word 156.
        var set = new HierarchicalBitSet(10_000);
        set.Add(100);
        set.Add(10_000 - 1);
        set.Remove(100);

        Assert.True(set.TryGetLowerBound(0, out int bound));
        Assert.Equal(9_999, bound);
        Assert.False(set.TryGetFloor(9_998, out _));
    }

    [Fact]
    public void Remove_Then_Reinsert_ManyElements_ShouldNotLoseEntries()
    {
        var set = new HierarchicalBitSet(5000);
        for (int i = 0; i < 5000; i++)
            set.Add(i);

        for (int i = 0; i < 5000; i += 2)
            Assert.True(set.Remove(i));

        for (int i = 0; i < 5000; i += 2)
            Assert.True(set.TryAdd(i));

        Assert.Equal(5000, set.Count);
        Assert.Equal(Enumerable.Range(0, 5000), set);
    }

    // ── Level boundaries ──────────────────────────────────────────────────────

    // One past each of these adds a level: 64 (one word), 4096 (two levels), 262,144 (three levels).
    [Theory]
    [InlineData(1)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(4096)]
    [InlineData(4097)]
    [InlineData(262_144)]
    [InlineData(262_145)]
    public void EveryLevelShape_ShouldAnswerOrderedQueries_AtBothEnds(int universe)
    {
        var set = new HierarchicalBitSet(universe);
        int top = universe - 1;

        set.Add(top);
        Assert.True(set.TryGetLowerBound(0, out int lower));
        Assert.Equal(top, lower);
        Assert.True(set.TryGetMin(out int min));
        Assert.Equal(top, min);

        set.TryAdd(0);
        Assert.True(set.TryGetFloor(int.MaxValue, out int floor));
        Assert.Equal(top, floor);
        Assert.True(set.TryGetMax(out int max));
        Assert.Equal(top, max);

        if (top > 0)
        {
            Assert.True(set.TryGetPredecessor(top, out int predecessor));
            Assert.Equal(0, predecessor);
            Assert.True(set.TryGetUpperBound(0, out int upper));
            Assert.Equal(top, upper);
        }

        set.Remove(0);
        set.Remove(top);
        Assert.Equal(0, set.Count);
        Assert.False(set.TryGetMin(out _));
        Assert.False(set.TryGetLowerBound(0, out _));
        Assert.False(set.TryGetFloor(top, out _));
    }

    [Fact]
    public void FullUniverse_ShouldStoreEveryValue_InAscendingOrder()
    {
        const int Universe = 4100;
        var set = new HierarchicalBitSet(Universe);
        for (int i = Universe - 1; i >= 0; i--)
            set.Add(i);

        Assert.Equal(Universe, set.Count);
        Assert.Equal(Enumerable.Range(0, Universe), set);

        for (int i = 0; i < Universe; i++)
            Assert.True(set.Remove(i));

        Assert.Empty(set);
        Assert.False(set.TryGetMax(out _));
    }

    [Fact]
    public void LargestUniverse_ShouldReachTheTopValue_ThroughSixLevels()
    {
        // int.MaxValue bits is the deepest tree an int universe builds (64^5 < 2^31 <= 64^6). The top value
        // sits in the last leaf word, whose successor word would start at 2^31.
        const int Top = int.MaxValue - 1;
        var set = new HierarchicalBitSet(int.MaxValue);
        set.Add(Top);
        set.Add(0);

        Assert.True(set.TryGetLowerBound(1, out int lower));
        Assert.Equal(Top, lower);
        Assert.True(set.TryGetFloor(int.MaxValue, out int floor));
        Assert.Equal(Top, floor);
        Assert.True(set.TryGetPredecessor(Top, out int predecessor));
        Assert.Equal(0, predecessor);
        Assert.False(set.TryGetUpperBound(Top, out _));
        Assert.True(set.TryGetMax(out int max));
        Assert.Equal(Top, max);
        Assert.Equal(new[] { 0, Top }, set);
        Assert.Equal(new[] { Top }, set.EnumerateRange(1, int.MaxValue));
    }

    // ── Ordered queries ───────────────────────────────────────────────────────

    [Fact]
    public void OrderedQueries_ShouldReturnFalseAndZero_WhenEmpty()
    {
        var set = new HierarchicalBitSet(1000);

        Assert.False(set.TryGetMin(out int min));
        Assert.Equal(0, min);
        Assert.False(set.TryGetMax(out int max));
        Assert.Equal(0, max);
        Assert.False(set.TryGetLowerBound(5, out int lower));
        Assert.Equal(0, lower);
        Assert.False(set.TryGetUpperBound(5, out int upper));
        Assert.Equal(0, upper);
        Assert.False(set.TryGetFloor(5, out int floor));
        Assert.Equal(0, floor);
        Assert.False(set.TryGetPredecessor(5, out int predecessor));
        Assert.Equal(0, predecessor);
    }

    [Fact]
    public void LowerBound_ShouldReturnExactMatch_OrNextLarger()
    {
        var set = new HierarchicalBitSet(10_000, new[] { 5, 70, 5000 });

        Assert.True(set.TryGetLowerBound(5, out int exact));
        Assert.Equal(5, exact);
        Assert.True(set.TryGetLowerBound(6, out int next));
        Assert.Equal(70, next);
        Assert.True(set.TryGetLowerBound(71, out int far));
        Assert.Equal(5000, far);
        Assert.False(set.TryGetLowerBound(5001, out _));
    }

    [Fact]
    public void LowerBound_ShouldReturnFalse_PastTheLastWordOfAMultipleOf64Level()
    {
        // 8,192 = 128 level-0 words = 2 level-1 words, so level 1 is a whole number of words. The climb out of
        // the last level-0 word lands one past the end of level 1, which must report "no successor" rather
        // than read into the next level's words. Only a universe that is a multiple of 8,192 reaches that exit,
        // and the randomized suite hit one only by chance, which made the coverage gate flaky (#558).
        var set = new HierarchicalBitSet(8192);
        set.Add(5);

        Assert.False(set.TryGetLowerBound(8150, out int bound));
        Assert.Equal(0, bound);
        Assert.False(set.TryGetUpperBound(8150, out _));
    }

    [Fact]
    public void UpperBound_ShouldSkipExactMatch()
    {
        var set = new HierarchicalBitSet(10_000, new[] { 5, 70, 5000 });

        Assert.True(set.TryGetUpperBound(5, out int bound));
        Assert.Equal(70, bound);
        Assert.False(set.TryGetUpperBound(5000, out _));
    }

    [Fact]
    public void Floor_ShouldReturnExactMatch_OrNextSmaller()
    {
        var set = new HierarchicalBitSet(10_000, new[] { 5, 70, 5000 });

        Assert.True(set.TryGetFloor(70, out int exact));
        Assert.Equal(70, exact);
        Assert.True(set.TryGetFloor(4999, out int previous));
        Assert.Equal(70, previous);
        Assert.True(set.TryGetFloor(69, out int far));
        Assert.Equal(5, far);
        Assert.False(set.TryGetFloor(4, out _));
    }

    [Fact]
    public void Predecessor_ShouldSkipExactMatch()
    {
        var set = new HierarchicalBitSet(10_000, new[] { 5, 70, 5000 });

        Assert.True(set.TryGetPredecessor(70, out int bound));
        Assert.Equal(5, bound);
        Assert.False(set.TryGetPredecessor(5, out _));
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    public void LowerBound_ShouldReturnMin_ForNegativeProbe(int probe)
    {
        var set = new HierarchicalBitSet(100, new[] { 0, 50 });

        Assert.True(set.TryGetLowerBound(probe, out int bound));
        Assert.Equal(0, bound);
        Assert.True(set.TryGetUpperBound(probe, out int upper));
        Assert.Equal(0, upper);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void Floor_ShouldReturnMax_ForProbeAtOrAboveUniverse(int probe)
    {
        var set = new HierarchicalBitSet(100, new[] { 0, 99 });

        Assert.True(set.TryGetFloor(probe, out int bound));
        Assert.Equal(99, bound);
        Assert.True(set.TryGetPredecessor(probe, out int predecessor));
        Assert.Equal(99, predecessor);
    }

    [Fact]
    public void Bounds_ShouldNotWrap_AtTheEndsOfInt()
    {
        var set = new HierarchicalBitSet(100, new[] { 0, 99 });

        // item + 1 and item - 1 would wrap here; both must answer "nothing beyond".
        Assert.False(set.TryGetUpperBound(int.MaxValue, out _));
        Assert.False(set.TryGetPredecessor(int.MinValue, out _));
        Assert.False(set.TryGetLowerBound(100, out _));
        Assert.False(set.TryGetFloor(-1, out _));
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_ShouldRemoveAllElements_AndLeaveSetReusable()
    {
        var set = new HierarchicalBitSet(10_000);
        for (int i = 0; i < 10_000; i += 7)
            set.Add(i);

        set.Clear();

        Assert.Equal(0, set.Count);
        Assert.Empty(set);
        Assert.False(set.Contains(7));
        Assert.False(set.TryGetLowerBound(0, out _));

        set.Add(9_000);
        Assert.True(set.TryGetLowerBound(0, out int bound));
        Assert.Equal(9_000, bound);
    }

    [Fact]
    public void Clear_ShouldBeNoOp_WhenAlreadyEmpty()
    {
        var set = new HierarchicalBitSet(10);
        set.Clear();
        Assert.Equal(0, set.Count);
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ShouldExposeUniverse()
    {
        var set = new HierarchicalBitSet(500);
        Assert.Equal(500, set.Universe);
        Assert.Empty(set);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenUniverseIsNegative()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new HierarchicalBitSet(-1));
        Assert.Equal("universe", ex.ParamName);
    }

    [Fact]
    public void ZeroUniverse_ShouldStoreNothing_AndRejectEveryValue()
    {
        var set = new HierarchicalBitSet(0);

        Assert.Equal(0, set.Universe);
        Assert.Throws<ArgumentOutOfRangeException>(() => set.Add(0));
        Assert.False(set.Contains(0));
        Assert.False(set.Remove(0));
        Assert.False(set.TryGetMin(out _));
        Assert.False(set.TryGetMax(out _));
        Assert.False(set.TryGetLowerBound(-1, out _));
        Assert.False(set.TryGetFloor(0, out _));
        Assert.False(set.TryGetFloor(int.MaxValue, out _));
        Assert.Empty(set);
        Assert.Empty(set.EnumerateRange(0, 10));
    }

    // ── IEnumerable<int> constructor ──────────────────────────────────────────

    [Fact]
    public void SourceConstructor_ShouldCopyAllElements()
    {
        var source = new[] { 5, 1, 4, 2, 3 };

        var set = new HierarchicalBitSet(100, source);

        Assert.Equal(5, set.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, set);
    }

    [Fact]
    public void SourceConstructor_ShouldSilentlyDedupe()
    {
        var set = new HierarchicalBitSet(100, new[] { 1, 2, 1, 3, 2, 0, 0 });

        Assert.Equal(4, set.Count);
        Assert.Equal(new[] { 0, 1, 2, 3 }, set);
    }

    [Fact]
    public void SourceConstructor_ShouldThrow_WhenSourceIsNull()
    {
        IEnumerable<int>? source = null;
        var ex = Assert.Throws<ArgumentNullException>(() => new HierarchicalBitSet(100, source!));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void SourceConstructor_NullSource_ShouldBeatNegativeUniverse()
    {
        IEnumerable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => new HierarchicalBitSet(-5, source!));
    }

    [Fact]
    public void SourceConstructor_ShouldThrow_WhenSourceHasOutOfRangeValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HierarchicalBitSet(100, new[] { 1, 2, 500 }));
    }

    [Fact]
    public void SourceConstructor_ShouldAcceptLazyNonCollectionSource()
    {
        IEnumerable<int> source = Enumerable.Range(0, 20).Where(x => x % 2 == 0);

        var set = new HierarchicalBitSet(100, source);

        Assert.Equal(10, set.Count);
        Assert.True(set.Contains(18));
        Assert.False(set.Contains(1));
    }

    // ── ICollection<T> / ISet<T> conformance ──────────────────────────────────

    [Fact]
    public void ISetAdd_ReturnsBool_WithoutThrowingOnDuplicate()
    {
        ISet<int> set = new HierarchicalBitSet(100);
        Assert.True(set.Add(1));
        Assert.False(set.Add(1));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void ICollectionAdd_DoesNotThrowOnDuplicate()
    {
        ICollection<int> set = new HierarchicalBitSet(100);
        set.Add(5);
        set.Add(5);
        Assert.Single(set);
    }

    [Fact]
    public void CopyTo_ShouldCopyEveryElement_InAscendingOrder()
    {
        var set = new HierarchicalBitSet(100, new[] { 5, 3, 1, 4, 2 });

        var array = new int[7];
        set.CopyTo(array, 1);

        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 0 }, array);
    }
}
