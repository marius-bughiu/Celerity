using System.Reflection;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Core behavioural coverage for <see cref="BTreeList{T}"/>: the constructors (including the bottom-up bulk
/// build and how it evens out the tail at every level), the positional surface (<c>this[int]</c>,
/// <see cref="BTreeList{T}.Insert"/>, <see cref="BTreeList{T}.RemoveAt"/>, <see cref="BTreeList{T}.Add"/>),
/// the linear searches, <see cref="BTreeList{T}.Clear"/> / <see cref="BTreeList{T}.ToArray"/> /
/// <see cref="BTreeList{T}.CopyTo"/>, and the argument contracts.
///
/// <para>
/// Most of the file is about the tree rather than the list. Each rebalancing path — a split biased for an
/// append or a prepend or an even one, and an underflow repaired by a merge or a redistribution, from the
/// left sibling or the right, at the leaf level and again one level up, and the root collapsing — is driven
/// on purpose from a shape built to reach it, and the resulting node fills are asserted exactly. Every test
/// also checks the whole structure against the B+ tree invariants (fill floors, cached child offsets and their
/// <c>int.MaxValue</c> padding, the leaf chain, cleared slots past each node's count) through <see cref="AssertWellFormed{T}"/>, and the
/// contents against a <see cref="List{T}"/> oracle. The randomized counterpart is
/// <see cref="BTreeListDifferentialTests"/>; nothing here depends on a seed.
/// </para>
/// </summary>
public class BTreeListTests
{
    private const int LeafCapacity = 128;
    private const int BranchCapacity = 64;
    private const int MinLeaf = LeafCapacity / 4;
    private const int MinBranch = BranchCapacity / 4;
    private const int BulkLeafFill = 96;
    private const int BulkBranchFill = 48;

    // Two full bulk-built branches of full bulk-built leaves: the smallest bulk shape with three levels and
    // a root of exactly two children, so either child's underflow is repaired against the other.
    private const int TwoBulkBranches = BulkLeafFill * BulkBranchFill * 2;

    // The leaf removals that take one bulk-built 48-leaf branch from 48 children to 15: the first underflow
    // after 65 removals, then one more merge every 96, since each merge leaves a 127-element leaf.
    private const int RemovalsToUnderflowABulkBranch = (BulkLeafFill - MinLeaf + 1) + 32 * BulkLeafFill;

    // ---- constructors ---------------------------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldCreateAnEmptySingleLeafList_WhenGivenNothing()
    {
        var list = new BTreeList<int>();

        Assert.Equal(0, list.Count);
        Assert.Equal(1, list.Depth);
        Assert.Empty(list);
        AssertWellFormed(list, new List<int>());
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenCollectionIsNull()
    {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => new BTreeList<int>(null!));
        Assert.Equal("collection", ex.ParamName);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(31, 1)]
    [InlineData(96, 1)]          // exactly one bulk leaf
    [InlineData(97, 2)]          // a one-element tail leaf, evened with its neighbour
    [InlineData(127, 2)]         // a 31-element tail: below the floor by one
    [InlineData(128, 2)]         // a 32-element tail: exactly at the floor, left alone
    [InlineData(192, 2)]
    [InlineData(4_608, 2)]       // 48 full leaves under one full branch
    [InlineData(4_609, 3)]       // 49 leaves: a one-child tail branch, evened with its neighbour
    [InlineData(6_144, 3)]       // 64 leaves: a 16-child tail branch, exactly at the floor
    [InlineData(TwoBulkBranches, 3)]
    [InlineData(198_529, 3)]     // ~200k: a one-element tail leaf and a 5-child tail branch
    [InlineData(221_185, 4)]     // 49 branches one level up: tails balanced at every level, four deep
    public void Constructor_ShouldBuildAWellFormedTree_WhenGivenACollectionOfAnySize(int size, int depth)
    {
        List<int> oracle = Enumerable.Range(0, size).ToList();

        var list = new BTreeList<int>(oracle);

        Assert.Equal(size, list.Count);
        Assert.Equal(depth, list.Depth);
        AssertWellFormed(list, oracle);
    }

    [Fact]
    public void Constructor_ShouldFillLeavesThreeQuarters_AndEvenTheTailWithItsNeighbour()
    {
        // 97 elements: one full bulk leaf of 96 and a 1-element tail, which is evened to 48 / 49.
        var list = new BTreeList<int>(Enumerable.Range(0, 97));
        Assert.Equal(new[] { 48, 49 }, LevelCounts(list, 1));

        // 3 × 96 + 40: the 40-element tail is above the floor, so every leaf keeps the bulk fill.
        list = new BTreeList<int>(Enumerable.Range(0, 3 * BulkLeafFill + 40));
        Assert.Equal(new[] { 96, 96, 96, 40 }, LevelCounts(list, 1));
    }

    [Fact]
    public void Constructor_ShouldEvenTheTailBranchWithItsNeighbour_WhenItHasTooFewChildren()
    {
        // 49 leaves: one full 48-child branch and a 1-child tail, which is evened to 24 / 25 children.
        var list = new BTreeList<int>(Enumerable.Range(0, 48 * BulkLeafFill + 1));
        Assert.Equal(new[] { 24, 25 }, LevelCounts(list, 1));

        // 64 leaves: the 16-child tail is exactly at the floor and is left alone.
        list = new BTreeList<int>(Enumerable.Range(0, 64 * BulkLeafFill));
        Assert.Equal(new[] { 48, 16 }, LevelCounts(list, 1));
    }

    [Fact]
    public void Constructor_ShouldAcceptASourceThatIsNotACollection()
    {
        static IEnumerable<int> Stream(int n)
        {
            for (int i = 0; i < n; i++)
                yield return i * 3;
        }

        var list = new BTreeList<int>(Stream(500));

        AssertWellFormed(list, Stream(500).ToList());
    }

    [Fact]
    public void Constructor_ShouldPreserveEnumerationOrder_AndCopyTheSource()
    {
        var source = new List<string?> { "b", null, "a", "b" };
        var list = new BTreeList<string?>(source);

        source[0] = "changed";

        Assert.Equal(new[] { "b", null, "a", "b" }, list);
    }

    // ---- indexer --------------------------------------------------------------------------------------

    [Fact]
    public void Indexer_ShouldReadEveryPosition_AcrossLeavesAndBranches()
    {
        List<int> oracle = Enumerable.Range(0, 10_000).Select(i => i * 7).ToList();
        var list = new BTreeList<int>(oracle);

        for (int i = 0; i < oracle.Count; i++)
            Assert.Equal(oracle[i], list[i]);
    }

    [Fact]
    public void Indexer_Set_ShouldReplaceTheElementInPlace_WithoutChangingTheShape()
    {
        List<int> oracle = Enumerable.Range(0, 1_000).ToList();
        var list = new BTreeList<int>(oracle);
        int[] leavesBefore = LevelCounts(list, list.Depth - 1);

        foreach (int i in new[] { 0, 1, 95, 96, 500, 998, 999 })
        {
            list[i] = -i - 1;
            oracle[i] = -i - 1;
        }

        Assert.Equal(leavesBefore, LevelCounts(list, list.Depth - 1));
        AssertWellFormed(list, oracle);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Indexer_ShouldThrowArgumentOutOfRangeException_WhenIndexIsOutOfRange(int index)
    {
        var list = new BTreeList<int>(new[] { 1, 2, 3 });

        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => list[index]).ParamName);
        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => list[index] = 0).ParamName);
        Assert.Equal(new[] { 1, 2, 3 }, list);
    }

    [Fact]
    public void Indexer_ShouldThrowArgumentOutOfRangeException_WhenTheListIsEmpty()
    {
        var list = new BTreeList<int>();

        Assert.Throws<ArgumentOutOfRangeException>(() => list[0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => list[0] = 1);
    }

    // ---- insertion ------------------------------------------------------------------------------------

    [Fact]
    public void Insert_ShouldPlaceTheElementAtTheIndex_AtTheFrontMiddleAndEnd()
    {
        var list = new BTreeList<int>(new[] { 1, 2, 3 });

        list.Insert(0, 0);
        list.Insert(2, 15);
        list.Insert(list.Count, 4);   // index == Count appends

        Assert.Equal(new[] { 0, 1, 15, 2, 3, 4 }, list);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MinValue)]
    public void Insert_ShouldThrowArgumentOutOfRangeException_WhenIndexIsOutOfRange(int index)
    {
        var list = new BTreeList<int>(new[] { 1, 2, 3 });

        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => list.Insert(index, 9));

        Assert.Equal("index", ex.ParamName);
        Assert.Equal(new[] { 1, 2, 3 }, list);
    }

    [Fact]
    public void Insert_ShouldSendABoundaryPosition_ToTheEndOfTheLeftChild()
    {
        // Two bulk leaves of 96: position 96 is both "after the first leaf" and "before the second", and is
        // documented to go to the left one.
        List<int> oracle = Enumerable.Range(0, 2 * BulkLeafFill).ToList();
        var list = new BTreeList<int>(oracle);

        list.Insert(BulkLeafFill, -1);
        oracle.Insert(BulkLeafFill, -1);

        Assert.Equal(new[] { 97, 96 }, LevelCounts(list, 1));
        AssertWellFormed(list, oracle);
    }

    [Fact]
    public void Add_ShouldSplitAFullLeafThreeQuartersLeft_WhenAppending()
    {
        List<int> oracle = Enumerable.Range(0, LeafCapacity).ToList();
        var list = new BTreeList<int>();
        foreach (int x in oracle)
            list.Add(x);

        Assert.Equal(1, list.Depth);
        Assert.Equal(new[] { LeafCapacity }, LevelCounts(list, 0));

        list.Add(LeafCapacity);
        oracle.Add(LeafCapacity);

        // The append-biased split keeps 129 - 32 on the left and leaves the right at the floor.
        Assert.Equal(2, list.Depth);
        Assert.Equal(new[] { 97, 32 }, LevelCounts(list, 1));
        AssertWellFormed(list, oracle);
    }

    [Fact]
    public void Insert_ShouldSplitAFullLeafThreeQuartersRight_WhenPrepending()
    {
        List<int> oracle = Enumerable.Range(0, LeafCapacity).ToList();
        var list = new BTreeList<int>();
        foreach (int x in oracle)
            list.Add(x);

        list.Insert(0, -1);
        oracle.Insert(0, -1);

        Assert.Equal(new[] { 32, 97 }, LevelCounts(list, 1));
        AssertWellFormed(list, oracle);
    }

    [Theory]
    [InlineData(10)]    // the new element lands in the left half
    [InlineData(64)]    // exactly on the split point: the first element of the right half
    [InlineData(100)]   // the new element lands in the right half
    [InlineData(127)]   // the last slot that is not an append
    public void Insert_ShouldSplitAFullLeafEvenly_WhenInsertingInTheMiddle(int index)
    {
        List<int> oracle = Enumerable.Range(0, LeafCapacity).ToList();
        var list = new BTreeList<int>(oracle.Take(BulkLeafFill));
        foreach (int x in oracle.Skip(BulkLeafFill))
            list.Add(x);
        Assert.Equal(new[] { LeafCapacity }, LevelCounts(list, 0));

        list.Insert(index, -1);
        oracle.Insert(index, -1);

        Assert.Equal(new[] { 64, 65 }, LevelCounts(list, 1));
        AssertWellFormed(list, oracle);
    }

    [Fact]
    public void Add_ShouldBuildThreeQuarterLeaves_AndSplitTheFullRootBranchThreeQuartersLeft()
    {
        // Appending one at a time: every leaf split leaves the left at 97, so a root branch fills at 64
        // leaves and the 65th splits it 49 / 16 — the append bias, one level up.
        var oracle = new List<int>();
        var list = new BTreeList<int>();
        while (list.Depth < 3)
        {
            list.Add(oracle.Count);
            oracle.Add(oracle.Count);
        }

        Assert.Equal(64 * 97 + 32, list.Count);
        Assert.Equal(new[] { 2 }, LevelCounts(list, 0));
        Assert.Equal(new[] { 49, 16 }, LevelCounts(list, 1));
        int[] leaves = LevelCounts(list, 2);
        Assert.All(leaves.Take(leaves.Length - 1), c => Assert.Equal(97, c));
        Assert.Equal(32, leaves[^1]);
        AssertWellFormed(list, oracle);
    }

    [Fact]
    public void Insert_AtTheFront_ShouldBuildThreeQuarterLeaves_AndSplitTheFullRootBranchEvenly()
    {
        // Prepending one at a time mirrors the leaf bias (32 / 97), but the new leaf is always inserted at
        // child position 1 — after the leaf that split — so the branch split it eventually causes is the
        // even one, not a prepend-biased one.
        var oracle = new List<int>();
        var list = new BTreeList<int>();
        int next = 0;
        while (list.Depth < 3)
        {
            list.Insert(0, next);
            oracle.Insert(0, next);
            next++;
        }

        Assert.Equal(64 * 97 + 32, list.Count);
        Assert.Equal(new[] { 32, 33 }, LevelCounts(list, 1));
        int[] leaves = LevelCounts(list, 2);
        Assert.Equal(32, leaves[0]);
        Assert.All(leaves.Skip(1), c => Assert.Equal(97, c));
        AssertWellFormed(list, oracle);
    }

    [Fact]
    public void Insert_InTheMiddle_ShouldSplitAFullBranchEvenly()
    {
        // A root branch of 64 leaves at 97 (built by appending), then inserts in the middle of one leaf until
        // it splits: the new child lands mid-branch and the full root splits 32 / 33.
        var oracle = new List<int>();
        var list = new BTreeList<int>();
        for (int i = 0; i < 63 * 97 + LeafCapacity; i++)
        {
            list.Add(i);
            oracle.Add(i);
        }
        Assert.Equal(new[] { 64 }, LevelCounts(list, 0));

        int middle = 30 * 97 + 40;
        while (list.Depth < 3)
        {
            list.Insert(middle, -1);
            oracle.Insert(middle, -1);
        }

        Assert.Equal(new[] { 32, 33 }, LevelCounts(list, 1));
        AssertWellFormed(list, oracle);
    }

    [Fact]
    public void Insert_ShouldKeepTheTreeWellFormed_UnderSustainedInsertsAtOnePosition()
    {
        List<int> oracle = Enumerable.Range(0, TwoBulkBranches).ToList();
        var list = new BTreeList<int>(oracle);

        for (int i = 0; i < 20_000; i++)
        {
            list.Insert(5_000, i);
            oracle.Insert(5_000, i);
        }

        AssertWellFormed(list, oracle);
    }

    // ---- removal: leaf level --------------------------------------------------------------------------

    [Fact]
    public void RemoveAt_ShouldMergeTheFirstLeafWithItsRightSibling_AndCollapseTheRoot()
    {
        List<int> oracle = Enumerable.Range(0, 2 * BulkLeafFill).ToList();
        var list = new BTreeList<int>(oracle);

        RemoveRepeatedly(list, oracle, 64, () => 0);
        Assert.Equal(new[] { 32, 96 }, LevelCounts(list, 1)); // at the floor: no repair yet

        RemoveRepeatedly(list, oracle, 1, () => 0);

        // 31 + 96 fit in one leaf: merged, and the root is left with one child, so it collapses.
        Assert.Equal(1, list.Depth);
        Assert.Equal(new[] { 127 }, LevelCounts(list, 0));
        AssertWellFormed(list, oracle);
    }

    [Fact]
    public void RemoveAt_ShouldBorrowFromTheRightSibling_WhenTheFirstLeafUnderflowsAndTheyDoNotFit()
    {
        List<int> oracle = Enumerable.Range(0, 2 * BulkLeafFill).ToList();
        var list = new BTreeList<int>(oracle);
        InsertRepeatedly(list, oracle, 10, () => 150);
        Assert.Equal(new[] { 96, 106 }, LevelCounts(list, 1));

        RemoveRepeatedly(list, oracle, 65, () => 0);

        // 31 + 106 = 137 do not fit in one leaf, so they are split evenly instead.
        Assert.Equal(2, list.Depth);
        Assert.Equal(new[] { 68, 69 }, LevelCounts(list, 1));
        AssertWellFormed(list, oracle);
    }

    [Fact]
    public void RemoveAt_ShouldMergeTheLastLeafIntoItsLeftSibling_AndCollapseTheRoot()
    {
        List<int> oracle = Enumerable.Range(0, 2 * BulkLeafFill).ToList();
        var list = new BTreeList<int>(oracle);

        RemoveRepeatedly(list, oracle, 65, () => oracle.Count - 1);

        Assert.Equal(1, list.Depth);
        Assert.Equal(new[] { 127 }, LevelCounts(list, 0));
        AssertWellFormed(list, oracle);
    }

    [Fact]
    public void RemoveAt_ShouldBorrowFromTheLeftSibling_WhenALaterLeafUnderflowsAndTheyDoNotFit()
    {
        List<int> oracle = Enumerable.Range(0, 2 * BulkLeafFill).ToList();
        var list = new BTreeList<int>(oracle);
        InsertRepeatedly(list, oracle, 10, () => 10);
        Assert.Equal(new[] { 106, 96 }, LevelCounts(list, 1));

        RemoveRepeatedly(list, oracle, 65, () => oracle.Count - 1);

        Assert.Equal(new[] { 68, 69 }, LevelCounts(list, 1));
        AssertWellFormed(list, oracle);
    }

    [Fact]
    public void RemoveAt_ShouldMergeAMiddleLeafIntoItsLeftSibling_AndCloseTheGapInTheBranch()
    {
        List<int> oracle = Enumerable.Range(0, 3 * BulkLeafFill).ToList();
        var list = new BTreeList<int>(oracle);

        RemoveRepeatedly(list, oracle, 65, () => BulkLeafFill + 10);

        // The middle leaf merges left; the third leaf shifts down into its slot and the root stays.
        Assert.Equal(2, list.Depth);
        Assert.Equal(new[] { 127, 96 }, LevelCounts(list, 1));
        AssertWellFormed(list, oracle);
    }

    [Fact]
    public void RemoveAt_ShouldMergeTheFirstLeaf_AndCloseTheGapInTheBranch()
    {
        List<int> oracle = Enumerable.Range(0, 3 * BulkLeafFill).ToList();
        var list = new BTreeList<int>(oracle);

        RemoveRepeatedly(list, oracle, 65, () => 0);

        Assert.Equal(new[] { 127, 96 }, LevelCounts(list, 1));
        AssertWellFormed(list, oracle);
    }

    // ---- removal: branch level ------------------------------------------------------------------------

    [Fact]
    public void RemoveAt_ShouldMergeTheFirstBranchWithItsRightSibling_AndCollapseTheRoot()
    {
        List<int> oracle = Enumerable.Range(0, TwoBulkBranches).ToList();
        var list = new BTreeList<int>(oracle);
        Assert.Equal(new[] { 48, 48 }, LevelCounts(list, 1));

        RemoveRepeatedly(list, oracle, RemovalsToUnderflowABulkBranch - 1, () => 0);
        Assert.Equal(3, list.Depth);
        Assert.Equal(new[] { 16, 48 }, LevelCounts(list, 1));

        RemoveRepeatedly(list, oracle, 1, () => 0);

        // 15 + 48 children fit in one branch: merged, and the root collapses to it.
        Assert.Equal(2, list.Depth);
        Assert.Equal(new[] { 63 }, LevelCounts(list, 0));
        AssertWellFormed(list, oracle);
    }

    [Fact]
    public void RemoveAt_ShouldBorrowFromTheRightSiblingBranch_WhenTheFirstBranchUnderflowsAndTheyDoNotFit()
    {
        List<int> oracle = Enumerable.Range(0, TwoBulkBranches).ToList();
        var list = new BTreeList<int>(oracle);

        // Appending grows the second branch: 33 adds split its last leaf 97 / 32, then one more leaf per 97.
        for (int i = 0; i < 33 + 4 * 97; i++)
        {
            list.Add(-i);
            oracle.Add(-i);
        }
        Assert.Equal(new[] { 48, 53 }, LevelCounts(list, 1));

        RemoveRepeatedly(list, oracle, RemovalsToUnderflowABulkBranch, () => 0);

        // 15 + 53 = 68 children do not fit in one branch, so they are split evenly instead.
        Assert.Equal(3, list.Depth);
        Assert.Equal(new[] { 34, 34 }, LevelCounts(list, 1));
        AssertWellFormed(list, oracle);
    }

    [Fact]
    public void RemoveAt_ShouldMergeTheLastBranchIntoItsLeftSibling_AndCollapseTheRoot()
    {
        List<int> oracle = Enumerable.Range(0, TwoBulkBranches).ToList();
        var list = new BTreeList<int>(oracle);

        RemoveRepeatedly(list, oracle, RemovalsToUnderflowABulkBranch, () => oracle.Count - 1);

        Assert.Equal(2, list.Depth);
        Assert.Equal(new[] { 63 }, LevelCounts(list, 0));
        AssertWellFormed(list, oracle);
    }

    [Fact]
    public void RemoveAt_ShouldBorrowFromTheLeftSiblingBranch_WhenTheLastBranchUnderflowsAndTheyDoNotFit()
    {
        List<int> oracle = Enumerable.Range(0, TwoBulkBranches).ToList();
        var list = new BTreeList<int>(oracle);

        // Prepending grows the first branch: 33 inserts split its first leaf 32 / 97, then one more leaf per 97.
        InsertRepeatedly(list, oracle, 33 + 4 * 97, () => 0);
        Assert.Equal(new[] { 53, 48 }, LevelCounts(list, 1));

        RemoveRepeatedly(list, oracle, RemovalsToUnderflowABulkBranch, () => oracle.Count - 1);

        Assert.Equal(3, list.Depth);
        Assert.Equal(new[] { 34, 34 }, LevelCounts(list, 1));
        AssertWellFormed(list, oracle);
    }

    [Fact]
    public void RemoveAt_ShouldCollapseAFourLevelTree_OneLevelAtATime()
    {
        List<int> oracle = Enumerable.Range(0, 221_185).ToList();
        var list = new BTreeList<int>(oracle);
        Assert.Equal(4, list.Depth);

        // From the back, so the oracle's removals stay O(1) across the tens of thousands this takes.
        while (list.Depth == 4)
        {
            list.RemoveAt(list.Count - 1);
            oracle.RemoveAt(oracle.Count - 1);
        }

        Assert.Equal(3, list.Depth);
        AssertWellFormed(list, oracle);
    }

    [Fact]
    public void RemoveAt_ShouldShrinkTheDepthBackToOne_WhenDrainedOneElementAtATime()
    {
        var oracle = new List<int>();
        var list = new BTreeList<int>();
        for (int i = 0; i < 64 * 97 + 32; i++)
        {
            list.Add(i);
            oracle.Add(i);
        }
        Assert.Equal(3, list.Depth);

        // From the middle, so both siblings' sides are drained; the depth may only ever drop one level per
        // removal, and the tree must be well formed at every depth change.
        int depth = list.Depth;
        while (list.Count > 0)
        {
            int index = list.Count / 2;
            list.RemoveAt(index);
            oracle.RemoveAt(index);

            if (list.Depth != depth)
            {
                Assert.Equal(depth - 1, list.Depth);
                depth = list.Depth;
                AssertWellFormed(list, oracle);
            }
        }

        Assert.Equal(1, list.Depth);
        Assert.Equal(0, list.Count);
        AssertWellFormed(list, oracle);

        list.Add(42);
        Assert.Equal(new[] { 42 }, list);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public void RemoveAt_ShouldThrowArgumentOutOfRangeException_WhenIndexIsOutOfRange(int index)
    {
        var list = new BTreeList<int>(new[] { 1, 2, 3 });

        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAt(index));

        Assert.Equal("index", ex.ParamName);
        Assert.Equal(new[] { 1, 2, 3 }, list);
    }

    [Fact]
    public void RemoveAt_ShouldThrowArgumentOutOfRangeException_WhenTheListIsEmpty()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BTreeList<int>().RemoveAt(0));
    }

    // ---- search ---------------------------------------------------------------------------------------

    [Fact]
    public void Remove_ShouldRemoveTheFirstOccurrence_WhenFound()
    {
        var list = new BTreeList<int>(new[] { 1, 2, 3, 2 });

        Assert.True(list.Remove(2));

        Assert.Equal(new[] { 1, 3, 2 }, list);
    }

    [Fact]
    public void Remove_ShouldReturnFalseAndLeaveTheListUnchanged_WhenNotFound()
    {
        var list = new BTreeList<int>(new[] { 1, 2, 3 });

        Assert.False(list.Remove(4));
        Assert.False(new BTreeList<int>().Remove(4));

        Assert.Equal(new[] { 1, 2, 3 }, list);
    }

    [Fact]
    public void IndexOf_ShouldReturnTheFirstMatchingPosition_AcrossLeaves()
    {
        // The duplicate sits in a later leaf; the first match must win, and positions past the first leaf
        // must be offset by the leaves before them.
        List<int> oracle = Enumerable.Range(0, 1_000).ToList();
        oracle[900] = 5;
        var list = new BTreeList<int>(oracle);

        Assert.Equal(5, list.IndexOf(5));
        Assert.Equal(700, list.IndexOf(700));
        Assert.Equal(999, list.IndexOf(999));
        Assert.Equal(-1, list.IndexOf(1_000));
        Assert.Equal(-1, new BTreeList<int>().IndexOf(0));

        list.RemoveAt(5);
        Assert.Equal(899, list.IndexOf(5));
    }

    [Fact]
    public void IndexOf_ShouldIgnoreStaleSlotsPastTheLeafCount()
    {
        // A removal leaves its old slot at default(T); a search for default must not report it.
        var list = new BTreeList<int>(new[] { 1, 2, 3 });
        list.RemoveAt(2);

        Assert.Equal(-1, list.IndexOf(0));
        Assert.False(list.Contains(0));
    }

    [Fact]
    public void Contains_ShouldFindPresentElements_AndNotAbsentOnes()
    {
        var list = new BTreeList<int>(Enumerable.Range(0, 300));

        Assert.True(list.Contains(0));
        Assert.True(list.Contains(299));
        Assert.False(list.Contains(300));
        Assert.False(list.Contains(-1));
    }

    [Fact]
    public void Search_ShouldHandleNull_ForReferenceTypes()
    {
        var list = new BTreeList<string?>(new[] { "a", "b" });

        Assert.False(list.Contains(null));
        Assert.Equal(-1, list.IndexOf(null));
        Assert.False(list.Remove(null));

        list.Insert(1, null);

        Assert.True(list.Contains(null));
        Assert.Equal(1, list.IndexOf(null));
        Assert.True(list.Remove(null));
        Assert.Equal(new[] { "a", "b" }, list);
    }

    // ---- bulk operations ------------------------------------------------------------------------------

    [Fact]
    public void Clear_ShouldEmptyAMultiLevelList_AndLeaveItReusable()
    {
        var list = new BTreeList<int>(Enumerable.Range(0, TwoBulkBranches));
        Assert.Equal(3, list.Depth);

        list.Clear();

        Assert.Equal(0, list.Count);
        Assert.Equal(1, list.Depth);
        Assert.Empty(list);
        Assert.Throws<ArgumentOutOfRangeException>(() => list[0]);
        AssertWellFormed(list, new List<int>());

        list.Add(1);
        list.Insert(0, 0);
        Assert.Equal(new[] { 0, 1 }, list);
    }

    [Fact]
    public void Clear_ShouldBeANoOp_WhenTheListIsEmpty()
    {
        var list = new BTreeList<int>();

        list.Clear();

        Assert.Equal(0, list.Count);
        Assert.Equal(1, list.Depth);
        AssertWellFormed(list, new List<int>());
    }

    [Fact]
    public void ToArray_ShouldReturnTheSharedEmptyArray_WhenTheListIsEmpty()
    {
        Assert.Same(Array.Empty<int>(), new BTreeList<int>().ToArray());

        var drained = new BTreeList<int>(new[] { 1 });
        drained.RemoveAt(0);
        Assert.Same(Array.Empty<int>(), drained.ToArray());
    }

    [Fact]
    public void ToArray_ShouldCopyEveryLeafInOrder()
    {
        List<int> oracle = Enumerable.Range(0, 5_000).ToList();
        var list = new BTreeList<int>(oracle);

        Assert.Equal(oracle, list.ToArray());
    }

    [Fact]
    public void CopyTo_ShouldCopyEveryLeafInOrder_AtTheGivenOffset()
    {
        List<int> oracle = Enumerable.Range(1, 300).ToList();
        var list = new BTreeList<int>(oracle);
        var destination = new int[305];

        list.CopyTo(destination, 2);

        Assert.Equal(new[] { 0, 0 }, destination[..2]);
        Assert.Equal(oracle, destination[2..302]);
        Assert.Equal(new[] { 0, 0, 0 }, destination[302..]);
    }

    [Fact]
    public void CopyTo_ShouldCopyNothing_WhenTheListIsEmpty()
    {
        var destination = new[] { 7 };

        new BTreeList<int>().CopyTo(destination, 1);
        new BTreeList<int>().CopyTo(destination, 0);

        Assert.Equal(new[] { 7 }, destination);
    }

    [Fact]
    public void CopyTo_ShouldThrow_WhenTheDestinationIsTooSmall()
    {
        var list = new BTreeList<int>(new[] { 1, 2, 3 });

        Assert.Throws<ArgumentNullException>(() => list.CopyTo(null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.CopyTo(new int[3], -1));
        Assert.Throws<ArgumentException>(() => list.CopyTo(new int[3], 1));
    }

    // ---- interfaces -----------------------------------------------------------------------------------

    [Fact]
    public void ICollection_IsReadOnly_ShouldBeFalse()
    {
        ICollection<int> collection = new BTreeList<int>();

        Assert.False(collection.IsReadOnly);
    }

    [Fact]
    public void IList_ShouldExposeThePositionalSurface()
    {
        IList<int> list = new BTreeList<int>();
        list.Add(1);
        list.Add(3);
        list.Insert(1, 2);
        list[0] = 0;

        Assert.Equal(3, list.Count);
        Assert.Equal(0, list[0]);
        Assert.Equal(1, list.IndexOf(2));
        Assert.True(list.Contains(3));
        Assert.True(list.Remove(3));
        list.RemoveAt(0);
        Assert.Equal(new[] { 2 }, list);
    }

    [Fact]
    public void IReadOnlyList_ShouldIndexAndCount()
    {
        IReadOnlyList<int> list = new BTreeList<int>(new[] { 10, 20 });

        Assert.Equal(2, list.Count);
        Assert.Equal(10, list[0]);
        Assert.Equal(20, list[1]);
    }

    // ---- helpers --------------------------------------------------------------------------------------

    private static void RemoveRepeatedly(BTreeList<int> list, List<int> oracle, int times, Func<int> index)
    {
        for (int n = 0; n < times; n++)
        {
            int i = index();
            list.RemoveAt(i);
            oracle.RemoveAt(i);
        }
    }

    private static void InsertRepeatedly(BTreeList<int> list, List<int> oracle, int times, Func<int> index)
    {
        for (int n = 0; n < times; n++)
        {
            int i = index();
            list.Insert(i, -n - 1);
            oracle.Insert(i, -n - 1);
        }
    }

    /// <summary>
    /// The node fills on one level of the tree, left to right: element counts for leaves, child counts for
    /// branches. Level <c>0</c> is the root and level <c>Depth - 1</c> the leaves.
    /// </summary>
    private static int[] LevelCounts<T>(BTreeList<T> list, int level)
    {
        var nodes = new List<object> { Field(list, "_root")! };
        for (int l = 0; l < level; l++)
        {
            var next = new List<object>();
            foreach (object branch in nodes)
            {
                object[] children = (object[])NodeField(branch, "Children")!;
                next.AddRange(children.Take(NodeCount(branch)));
            }
            nodes = next;
        }
        return nodes.Select(NodeCount).ToArray();
    }

    /// <summary>
    /// Asserts the list holds exactly <paramref name="oracle"/> and that its tree satisfies every invariant
    /// the type relies on: each non-root node is between a quarter full and full, a branch root has at least
    /// two children, a branch's cached offsets start at 0 and are the running sum of its children's real
    /// element counts with every slot after the total padded with <see cref="int.MaxValue"/>, every slot past
    /// a node's count is cleared, and the leaf chain from the first leaf visits exactly the tree's leaves in
    /// order and ends in <c>null</c>.
    /// </summary>
    private static void AssertWellFormed<T>(BTreeList<T> list, List<T> oracle)
    {
        Assert.Equal(oracle.Count, list.Count);
        Assert.Equal(oracle, list.ToArray());
        Assert.Equal(oracle, list);

        int height = (int)Field(list, "_height")!;
        Assert.Equal(height + 1, list.Depth);

        var leaves = new List<object>();
        int size = CheckSubtree(Field(list, "_root")!, height, isRoot: true, leaves);
        Assert.Equal(oracle.Count, size);

        object? leaf = Field(list, "_first");
        foreach (object expected in leaves)
        {
            Assert.Same(expected, leaf);
            leaf = NodeField(leaf!, "Next");
        }
        Assert.Null(leaf);

        // Indexed reads agree with the oracle at every position near a leaf boundary and a sample between.
        int offset = 0;
        foreach (object l in leaves)
        {
            int count = NodeCount(l);
            foreach (int i in new[] { offset, offset + count / 2, offset + count - 1 })
            {
                if ((uint)i < (uint)oracle.Count)
                    Assert.Equal(oracle[i], list[i]);
            }
            offset += count;
        }
    }

    private static int CheckSubtree(object node, int height, bool isRoot, List<object> leaves)
    {
        int count = NodeCount(node);

        if (height == 0)
        {
            Assert.Equal("Leaf", node.GetType().Name.Split('`')[0]);
            Assert.InRange(count, isRoot ? 0 : MinLeaf, LeafCapacity);
            var items = (Array)NodeField(node, "Items")!;
            for (int i = count; i < items.Length; i++)
                Assert.Equal(DefaultOf(items), items.GetValue(i));
            leaves.Add(node);
            return count;
        }

        Assert.Equal("Branch", node.GetType().Name.Split('`')[0]);
        Assert.InRange(count, isRoot ? 2 : MinBranch, BranchCapacity);
        object[] children = (object[])NodeField(node, "Children")!;
        int[] offsets = (int[])NodeField(node, "Offsets")!;
        Assert.True(offsets.Length > BranchCapacity, "A branch's offsets must have room for the end of a full branch.");

        // Offsets[i] is where child i starts: 0 for the first, then a running sum of the child sizes, ending
        // in the subtree's element count at Offsets[Count].
        Assert.Equal(0, offsets[0]);
        int total = 0;
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(total, offsets[i]);
            total += CheckSubtree(children[i], height - 1, isRoot: false, leaves);
        }
        Assert.Equal(total, offsets[count]);

        // The vector search reads whole vectors past the end, and relies on every slot after Offsets[Count]
        // being int.MaxValue so none of them can match.
        for (int i = count + 1; i < offsets.Length; i++)
            Assert.Equal(int.MaxValue, offsets[i]);
        for (int i = count; i < BranchCapacity; i++)
            Assert.Null(children[i]);
        return total;
    }

    private static object? DefaultOf(Array items)
    {
        Type element = items.GetType().GetElementType()!;
        return element.IsValueType ? Activator.CreateInstance(element) : null;
    }

    private static object? Field<T>(BTreeList<T> list, string name)
    {
        FieldInfo? field = typeof(BTreeList<T>).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field.GetValue(list);
    }

    private static object? NodeField(object node, string name)
    {
        FieldInfo? field = node.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(field);
        return field.GetValue(node);
    }

    private static int NodeCount(object node) => (int)NodeField(node, "Count")!;
}
