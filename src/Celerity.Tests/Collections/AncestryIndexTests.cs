using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural coverage of <see cref="AncestryIndex"/>: construction and its validation, every query on a
/// small hand-drawn forest, the forest-specific "no common ancestor" answers, and argument checking.
/// The randomized reconciliation against a parent-walking oracle lives in
/// <see cref="AncestryIndexDifferentialTests"/>.
/// </summary>
public class AncestryIndexTests
{
    //        0                6
    //      /   \              |
    //     1     2             7
    //    / \     \
    //   3   4     5
    private static readonly int[] Forest = [-1, 0, 0, 1, 1, 2, -1, 6];

    // ---- the documented example --------------------------------------------------------------------

    [Fact]
    public void DocumentationExample_ShouldProduceTheDocumentedAnswers()
    {
        // The example in the type's XML docs and in docs/api/collections.md, line for line.
        var tree = new AncestryIndex(new[] { -1, 0, 0, 1, 1, 2 });

        Assert.True(tree.IsAncestor(1, 4));
        Assert.Equal(0, tree.LowestCommonAncestor(3, 5));
        Assert.Equal(4, tree.Distance(3, 5));
        Assert.Equal(0, tree.GetAncestor(4, 2));
        Assert.Equal([1, 3, 4], tree.GetSubtree(1).ToArray());
    }

    [Fact]
    public void SubtreeAggregate_ShouldBeARangeQueryInPreorder()
    {
        // The documented use of GetPreorderIndex: lay a per-node value out in preorder and every subtree total
        // is a prefix-sum range. Checked against summing each subtree's own members.
        var index = new AncestryIndex(Forest);
        int[] cost = [10, 20, 30, 40, 50, 60, 70, 80];

        long[] prefix = new long[index.Count + 1];
        for (int position = 0; position < index.Count; position++)
            prefix[position + 1] = prefix[position] + cost[index.Preorder[position]];

        for (int v = 0; v < index.Count; v++)
        {
            int start = index.GetPreorderIndex(v);
            long expected = 0;
            foreach (int member in index.GetSubtree(v))
                expected += cost[member];

            Assert.Equal(expected, prefix[start + index.GetSubtreeSize(v)] - prefix[start]);
        }
    }

    // ---- construction ------------------------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldReportShape()
    {
        var index = new AncestryIndex(Forest);

        Assert.Equal(8, index.Count);
        Assert.Equal(2, index.RootCount);
        Assert.Equal(3, index.Height);
    }

    [Fact]
    public void Constructor_ShouldCopyTheSource()
    {
        int[] parents = (int[])Forest.Clone();
        var index = new AncestryIndex(parents);

        parents[3] = 2;

        Assert.Equal(1, index.GetParent(3));
        Assert.True(index.IsAncestor(1, 3));
    }

    [Fact]
    public void Constructor_Empty_ShouldBuildAnEmptyForest()
    {
        var index = new AncestryIndex(ReadOnlySpan<int>.Empty);

        Assert.Equal(0, index.Count);
        Assert.Equal(0, index.RootCount);
        Assert.Equal(0, index.Height);
        Assert.True(index.Preorder.IsEmpty);
        Assert.Throws<ArgumentOutOfRangeException>(() => index.GetParent(0));
    }

    [Fact]
    public void Constructor_SingleNode_ShouldBeItsOwnRootAndAncestor()
    {
        var index = new AncestryIndex([-1]);

        Assert.Equal(1, index.Height);
        Assert.Equal(0, index.GetRoot(0));
        Assert.Equal(0, index.LowestCommonAncestor(0, 0));
        Assert.Equal(0, index.Distance(0, 0));
        Assert.True(index.IsAncestor(0, 0));
        Assert.Equal(-1, index.GetAncestor(0, 1));
    }

    [Fact]
    public void Constructor_AllRoots_ShouldHaveHeightOne()
    {
        var index = new AncestryIndex([-1, -1, -1, -1]);

        Assert.Equal(4, index.RootCount);
        Assert.Equal(1, index.Height);
        Assert.Equal([0, 1, 2, 3], index.Preorder.ToArray());
        Assert.Equal(-1, index.LowestCommonAncestor(0, 3));
        Assert.False(index.IsAncestor(0, 3));
    }

    [Fact]
    public void Constructor_SelfParent_ShouldThrow()
    {
        var ex = Assert.Throws<ArgumentException>(() => new AncestryIndex([-1, 1]));

        Assert.Equal("parents", ex.ParamName);
        Assert.Contains("own parent", ex.Message);
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Constructor_ParentOutOfRange_ShouldThrow(int parent)
    {
        var ex = Assert.Throws<ArgumentException>(() => new AncestryIndex([-1, 0, parent]));

        Assert.Equal("parents", ex.ParamName);
        Assert.Contains("[0, 3)", ex.Message);
    }

    [Fact]
    public void Constructor_CycleBesideARoot_ShouldThrow()
    {
        // Node 0 is a proper root; 1 -> 2 -> 3 -> 1 never reaches it.
        var ex = Assert.Throws<ArgumentException>(() => new AncestryIndex([-1, 3, 1, 2]));

        Assert.Equal("parents", ex.ParamName);
        Assert.Contains("cycle", ex.Message);
    }

    [Fact]
    public void Constructor_CycleWithNoRoot_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new AncestryIndex([1, 0]));
    }

    // ---- per-node facts ----------------------------------------------------------------------------

    [Fact]
    public void PerNodeQueries_ShouldMatchTheDrawing()
    {
        var index = new AncestryIndex(Forest);

        Assert.Equal([-1, 0, 0, 1, 1, 2, -1, 6], Enumerable.Range(0, 8).Select(index.GetParent));
        Assert.Equal([0, 1, 1, 2, 2, 2, 0, 1], Enumerable.Range(0, 8).Select(index.GetDepth));
        Assert.Equal([6, 3, 2, 1, 1, 1, 2, 1], Enumerable.Range(0, 8).Select(index.GetSubtreeSize));
        Assert.Equal([0, 0, 0, 0, 0, 0, 6, 6], Enumerable.Range(0, 8).Select(index.GetRoot));
    }

    [Fact]
    public void Preorder_ShouldVisitChildrenInAscendingIdOrder()
    {
        var index = new AncestryIndex(Forest);

        Assert.Equal([0, 1, 3, 4, 2, 5, 6, 7], index.Preorder.ToArray());
        for (int v = 0; v < index.Count; v++)
            Assert.Equal(v, index.Preorder[index.GetPreorderIndex(v)]);
    }

    [Fact]
    public void Preorder_ChildIdsBelowTheParent_ShouldStillOrderByChildId()
    {
        // Node 3 is the root and its children 2, 0, 1 are listed out of order in the array.
        var index = new AncestryIndex([3, 3, 3, -1]);

        Assert.Equal([3, 0, 1, 2], index.Preorder.ToArray());
    }

    [Fact]
    public void GetSubtree_ShouldBeThePreorderSlice()
    {
        var index = new AncestryIndex(Forest);

        Assert.Equal([1, 3, 4], index.GetSubtree(1).ToArray());
        Assert.Equal([2, 5], index.GetSubtree(2).ToArray());
        Assert.Equal([5], index.GetSubtree(5).ToArray());
        Assert.Equal([6, 7], index.GetSubtree(6).ToArray());
        Assert.Equal([0, 1, 3, 4, 2, 5], index.GetSubtree(0).ToArray());
    }

    // ---- pair queries ------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 4, true)]
    [InlineData(1, 4, true)]
    [InlineData(4, 4, true)]
    [InlineData(4, 1, false)]
    [InlineData(2, 4, false)]
    [InlineData(3, 4, false)]
    [InlineData(0, 7, false)]
    [InlineData(6, 7, true)]
    public void IsAncestor_ShouldBeInclusive(int ancestor, int node, bool expected)
    {
        Assert.Equal(expected, new AncestryIndex(Forest).IsAncestor(ancestor, node));
    }

    [Theory]
    [InlineData(3, 4, 1)]
    [InlineData(3, 5, 0)]
    [InlineData(5, 3, 0)]
    [InlineData(1, 4, 1)]
    [InlineData(4, 1, 1)]
    [InlineData(5, 5, 5)]
    [InlineData(2, 0, 0)]
    [InlineData(3, 7, -1)]
    [InlineData(6, 0, -1)]
    [InlineData(7, 6, 6)]
    public void LowestCommonAncestor_ShouldMatchTheDrawing(int first, int second, int expected)
    {
        Assert.Equal(expected, new AncestryIndex(Forest).LowestCommonAncestor(first, second));
    }

    [Theory]
    [InlineData(3, 4, 2)]
    [InlineData(3, 5, 4)]
    [InlineData(0, 5, 2)]
    [InlineData(4, 4, 0)]
    [InlineData(3, 7, -1)]
    public void Distance_ShouldCountEdges(int first, int second, int expected)
    {
        Assert.Equal(expected, new AncestryIndex(Forest).Distance(first, second));
    }

    [Theory]
    [InlineData(4, 0, 4)]
    [InlineData(4, 1, 1)]
    [InlineData(4, 2, 0)]
    [InlineData(4, 3, -1)]
    [InlineData(4, int.MaxValue, -1)]
    [InlineData(5, 2, 0)]
    [InlineData(7, 1, 6)]
    [InlineData(0, 0, 0)]
    [InlineData(0, 1, -1)]
    public void GetAncestor_ShouldClimbTheRequestedLevels(int node, int levels, int expected)
    {
        Assert.Equal(expected, new AncestryIndex(Forest).GetAncestor(node, levels));
    }

    [Fact]
    public void LongPath_ShouldAnswerAcrossManyBlocks()
    {
        // A single path 0 <- 1 <- ... <- 999 is the deepest possible tree: every range-minimum query spans
        // whole blocks, and every level holds exactly one node.
        const int Length = 1000;
        int[] parents = new int[Length];
        for (int i = 0; i < Length; i++)
            parents[i] = i - 1;

        var index = new AncestryIndex(parents);

        Assert.Equal(Length, index.Height);
        Assert.Equal(400, index.LowestCommonAncestor(400, 999));
        Assert.Equal(599, index.Distance(400, 999));
        Assert.Equal(0, index.GetAncestor(999, 999));
        Assert.Equal(500, index.GetAncestor(999, 499));
        Assert.True(index.IsAncestor(3, 998));
        Assert.False(index.IsAncestor(998, 3));
        Assert.Equal(0, index.GetRoot(999));
    }

    [Fact]
    public void Caterpillar_ShouldFindTheSpineAncestorFromFarApartLeaves()
    {
        // A spine 0 <- 1 <- ... <- 299 with one leaf hanging off every spine node. In preorder the leaf of
        // spine node k sits after the whole remaining spine, so two leaves are far apart and their LCA query
        // crosses whole blocks in the middle.
        const int Spine = 300;
        int[] parents = new int[2 * Spine];
        for (int i = 0; i < Spine; i++)
        {
            parents[i] = i - 1;
            parents[Spine + i] = i;
        }

        var index = new AncestryIndex(parents);

        Assert.Equal(10, index.LowestCommonAncestor(Spine + 10, Spine + 250));
        Assert.Equal(10, index.LowestCommonAncestor(Spine + 250, Spine + 10));
        Assert.Equal(250 - 10 + 2, index.Distance(Spine + 10, Spine + 250));
    }

    // ---- cost reporting ----------------------------------------------------------------------------

    [Fact]
    public void MemoryUsageInBytes_ShouldBeAboutFortyBytesPerNode()
    {
        int[] parents = new int[10_000];
        var rng = new Random(7);
        parents[0] = -1;
        for (int i = 1; i < parents.Length; i++)
            parents[i] = rng.Next(i);

        long bytes = new AncestryIndex(parents).MemoryUsageInBytes;

        Assert.InRange(bytes, 40L * parents.Length, 42L * parents.Length);
    }

    [Fact]
    public void MemoryUsageInBytes_Empty_ShouldCountOnlyTheLevelSentinel()
    {
        Assert.Equal(sizeof(int), new AncestryIndex(ReadOnlySpan<int>.Empty).MemoryUsageInBytes);
    }

    // ---- argument checks ---------------------------------------------------------------------------

    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    [InlineData(int.MinValue)]
    public void SingleNodeQueries_NodeOutOfRange_ShouldThrow(int node)
    {
        var index = new AncestryIndex(Forest);

        AssertParam("node", () => index.GetParent(node));
        AssertParam("node", () => index.GetDepth(node));
        AssertParam("node", () => index.GetSubtreeSize(node));
        AssertParam("node", () => index.GetPreorderIndex(node));
        AssertParam("node", () => index.GetSubtree(node));
        AssertParam("node", () => index.GetRoot(node));
        AssertParam("node", () => index.GetAncestor(node, 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    public void PairQueries_NodeOutOfRange_ShouldNameTheArgument(int bad)
    {
        var index = new AncestryIndex(Forest);

        AssertParam("ancestor", () => index.IsAncestor(bad, 0));
        AssertParam("node", () => index.IsAncestor(0, bad));
        AssertParam("first", () => index.LowestCommonAncestor(bad, 0));
        AssertParam("second", () => index.LowestCommonAncestor(0, bad));
        AssertParam("first", () => index.Distance(bad, 0));
        AssertParam("second", () => index.Distance(0, bad));
    }

    [Fact]
    public void GetAncestor_NegativeLevels_ShouldThrow()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new AncestryIndex(Forest).GetAncestor(3, -1));

        Assert.Equal("levels", ex.ParamName);
    }

    private static void AssertParam(string expected, Action call)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(call);
        Assert.Equal(expected, ex.ParamName);
    }
}
