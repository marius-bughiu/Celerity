using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural coverage for <see cref="CompressedGraph"/>: the constructor and its validation, the adjacency
/// surface (<see cref="CompressedGraph.Neighbors"/>, <see cref="CompressedGraph.Degree"/>,
/// <see cref="CompressedGraph.ContainsEdge"/>), the transpose, and the two traversals across their
/// allocation-free and convenience tiers.
///
/// <para>
/// The cases that decide whether the layout is right are the ones a naive fixture does not contain: an
/// isolated vertex (which repeats an offset, so every index-to-source conversion has to be an upper-bound
/// search rather than an equality one), a vertex declared past the last edge (which leaves the tail of the
/// offset array to be filled by the shift rather than by the scatter), duplicate edges (which the build
/// collapses, moving every later vertex's slice down), a self-loop, and a traversal over more than 64
/// vertices — the point at which the visited bitmap stops being one word.
/// </para>
///
/// <para>
/// The randomized reconciliation against a <c>Dictionary&lt;int, List&lt;int&gt;&gt;</c> adjacency map lives in
/// <see cref="CompressedGraphDifferentialTests"/>, and the <see cref="IReadOnlyList{T}"/> surface in
/// <see cref="CompressedGraphEnumerationTests"/>.
/// </para>
/// </summary>
public class CompressedGraphTests
{
    private static GraphEdge Edge(int source, int target) => new(source, target);

    // Five vertices, and deliberately not a tidy shape: vertex 2 is isolated (an offset repeated in the middle
    // of the array), vertex 4 is a sink reached from two places, and 0 fans out to both 1 and 3.
    //
    //   0 -> 1 -> 4
    //   0 -> 3 -> 4
    //   2 (isolated)
    private static CompressedGraph Fixture() => new(5,
    [
        Edge(0, 1),
        Edge(0, 3),
        Edge(1, 4),
        Edge(3, 4),
    ]);

    // ---- construction ------------------------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldBuildAnEmptyGraph_WhenGivenNoVerticesAndNoEdges()
    {
        var graph = new CompressedGraph(0, Array.Empty<GraphEdge>());

        Assert.Equal(0, graph.VertexCount);
        Assert.Equal(0, graph.EdgeCount);
        Assert.Empty(graph);
    }

    [Fact]
    public void Constructor_ShouldKeepEveryVertex_WhenNoEdgeReachesThem()
    {
        var graph = new CompressedGraph(4, Array.Empty<GraphEdge>());

        Assert.Equal(4, graph.VertexCount);
        Assert.Equal(0, graph.EdgeCount);
        for (int vertex = 0; vertex < 4; vertex++)
        {
            Assert.Equal(0, graph.Degree(vertex));
            Assert.True(graph.Neighbors(vertex).IsEmpty);
        }
    }

    [Fact]
    public void Constructor_ShouldSortEachVertexTargets_WhenTheEdgesArriveOutOfOrder()
    {
        var graph = new CompressedGraph(5, [Edge(0, 4), Edge(0, 1), Edge(0, 3), Edge(0, 2)]);

        Assert.Equal([1, 2, 3, 4], graph.Neighbors(0).ToArray());
    }

    [Fact]
    public void Constructor_ShouldCollapseDuplicateEdges_WhenTheSameEdgeIsSuppliedTwice()
    {
        var graph = new CompressedGraph(3, [Edge(0, 1), Edge(0, 1), Edge(0, 2), Edge(2, 1), Edge(2, 1)]);

        Assert.Equal(3, graph.EdgeCount);
        Assert.Equal([1, 2], graph.Neighbors(0).ToArray());
        Assert.Equal([1], graph.Neighbors(2).ToArray());
        Assert.Equal(2, graph.Degree(0));
    }

    [Fact]
    public void Constructor_ShouldKeepTheLaterSlicesAligned_WhenAnEarlierVertexLosesDuplicates()
    {
        // The compaction moves every later vertex's slice down by the number of duplicates dropped ahead of
        // it. A fixture whose duplicates all sit on the last vertex could not tell a correct shift from none.
        var graph = new CompressedGraph(4, [Edge(0, 1), Edge(0, 1), Edge(0, 1), Edge(1, 2), Edge(2, 3), Edge(3, 0)]);

        Assert.Equal(4, graph.EdgeCount);
        Assert.Equal([1], graph.Neighbors(0).ToArray());
        Assert.Equal([2], graph.Neighbors(1).ToArray());
        Assert.Equal([3], graph.Neighbors(2).ToArray());
        Assert.Equal([0], graph.Neighbors(3).ToArray());
    }

    [Fact]
    public void Constructor_ShouldPreserveASelfLoop_WhenAnEdgeStartsAndEndsAtOneVertex()
    {
        var graph = new CompressedGraph(2, [Edge(0, 0), Edge(0, 1)]);

        Assert.Equal([0, 1], graph.Neighbors(0).ToArray());
        Assert.True(graph.ContainsEdge(0, 0));
    }

    [Fact]
    public void Constructor_ShouldReadTheSequenceOnce_WhenTheSourceIsNotACollection()
    {
        static IEnumerable<GraphEdge> Streamed()
        {
            yield return new GraphEdge(0, 2);
            yield return new GraphEdge(2, 1);
        }

        var graph = new CompressedGraph(3, Streamed());

        Assert.Equal(2, graph.EdgeCount);
        Assert.Equal([2], graph.Neighbors(0).ToArray());
        Assert.Equal([1], graph.Neighbors(2).ToArray());
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTheEdgeSequenceIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new CompressedGraph(1, null!));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTheVertexCountIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CompressedGraph(-1, Array.Empty<GraphEdge>()));
    }

    [Theory]
    [InlineData(3, 0)]
    [InlineData(-1, 0)]
    [InlineData(0, 3)]
    [InlineData(0, -1)]
    public void Constructor_ShouldThrow_WhenAnEdgeLeavesTheVertexRange(int source, int target)
    {
        ArgumentException error = Assert.Throws<ArgumentException>(
            () => new CompressedGraph(3, [Edge(0, 1), Edge(source, target)]));

        Assert.Equal("edges", error.ParamName);
    }

    // ---- adjacency ---------------------------------------------------------------------------------

    [Fact]
    public void Neighbors_ShouldReturnTheTargets_WhenTheVertexHasEdges()
    {
        CompressedGraph graph = Fixture();

        Assert.Equal([1, 3], graph.Neighbors(0).ToArray());
        Assert.Equal([4], graph.Neighbors(1).ToArray());
        Assert.True(graph.Neighbors(2).IsEmpty);
        Assert.True(graph.Neighbors(4).IsEmpty);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void Neighbors_ShouldThrow_WhenTheVertexIsOutOfRange(int vertex)
    {
        CompressedGraph graph = Fixture();

        Assert.Throws<ArgumentOutOfRangeException>(() => graph.Neighbors(vertex).Length);
    }

    [Fact]
    public void Degree_ShouldCountDistinctTargets_WhenTheVertexHasEdges()
    {
        CompressedGraph graph = Fixture();

        Assert.Equal(2, graph.Degree(0));
        Assert.Equal(1, graph.Degree(1));
        Assert.Equal(0, graph.Degree(2));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void Degree_ShouldThrow_WhenTheVertexIsOutOfRange(int vertex)
    {
        CompressedGraph graph = Fixture();

        Assert.Throws<ArgumentOutOfRangeException>(() => graph.Degree(vertex));
    }

    [Fact]
    public void ContainsEdge_ShouldRespectDirection_WhenTheReverseEdgeIsAbsent()
    {
        CompressedGraph graph = Fixture();

        Assert.True(graph.ContainsEdge(0, 1));
        Assert.False(graph.ContainsEdge(1, 0));
        Assert.False(graph.ContainsEdge(0, 4));
        Assert.False(graph.ContainsEdge(2, 0));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(5, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 5)]
    public void ContainsEdge_ShouldThrow_WhenAnEndpointIsOutOfRange(int source, int target)
    {
        CompressedGraph graph = Fixture();

        Assert.Throws<ArgumentOutOfRangeException>(() => graph.ContainsEdge(source, target));
    }

    // ---- transpose ---------------------------------------------------------------------------------

    [Fact]
    public void Reverse_ShouldTurnOutEdgesIntoInEdges_WhenTheGraphIsDirected()
    {
        CompressedGraph reversed = Fixture().Reverse();

        Assert.Equal(5, reversed.VertexCount);
        Assert.Equal(4, reversed.EdgeCount);
        Assert.True(reversed.Neighbors(0).IsEmpty);
        Assert.Equal([0], reversed.Neighbors(1).ToArray());
        Assert.Equal([0], reversed.Neighbors(3).ToArray());
        Assert.Equal([1, 3], reversed.Neighbors(4).ToArray());
    }

    [Fact]
    public void Reverse_ShouldRoundTrip_WhenAppliedTwice()
    {
        CompressedGraph graph = Fixture();
        CompressedGraph roundTripped = graph.Reverse().Reverse();

        Assert.Equal(graph.ToArray(), roundTripped.ToArray());
    }

    [Fact]
    public void Reverse_ShouldKeepASelfLoop_WhenTheEdgeIsItsOwnReverse()
    {
        CompressedGraph reversed = new CompressedGraph(2, [Edge(0, 0), Edge(1, 0)]).Reverse();

        Assert.Equal([0, 1], reversed.Neighbors(0).ToArray());
    }

    // ---- breadth-first traversal -------------------------------------------------------------------

    [Fact]
    public void CopyBreadthFirstOrder_ShouldVisitByDistance_WhenTheGraphBranches()
    {
        CompressedGraph graph = Fixture();
        int[] order = new int[graph.VertexCount];

        int count = graph.CopyBreadthFirstOrder(0, order);

        Assert.Equal(4, count);
        Assert.Equal([0, 1, 3, 4], order[..count]);
    }

    [Fact]
    public void CopyBreadthFirstOrder_ShouldVisitOnlyTheSource_WhenItHasNoOutEdges()
    {
        CompressedGraph graph = Fixture();
        int[] order = new int[graph.VertexCount];

        Assert.Equal(1, graph.CopyBreadthFirstOrder(2, order));
        Assert.Equal(2, order[0]);
    }

    [Fact]
    public void CopyBreadthFirstOrder_ShouldVisitEachVertexOnce_WhenTheGraphHasACycle()
    {
        var graph = new CompressedGraph(3, [Edge(0, 1), Edge(1, 2), Edge(2, 0), Edge(0, 0)]);
        int[] order = new int[graph.VertexCount];

        Assert.Equal(3, graph.CopyBreadthFirstOrder(0, order));
        Assert.Equal([0, 1, 2], order);
    }

    [Fact]
    public void CopyBreadthFirstOrder_ShouldWriteNothing_WhenTheDestinationIsEmpty()
    {
        CompressedGraph graph = Fixture();

        Assert.Equal(0, graph.CopyBreadthFirstOrder(0, Span<int>.Empty));
    }

    [Fact]
    public void CopyBreadthFirstOrder_ShouldStopAtTheSource_WhenTheDestinationHoldsOneVertex()
    {
        CompressedGraph graph = Fixture();
        int[] order = new int[1];

        Assert.Equal(1, graph.CopyBreadthFirstOrder(0, order));
        Assert.Equal(0, order[0]);
    }

    [Fact]
    public void CopyBreadthFirstOrder_ShouldTruncateToThePrefixOfTheFullOrder_WhenTheDestinationIsShort()
    {
        CompressedGraph graph = Fixture();
        int[] order = new int[3];

        Assert.Equal(3, graph.CopyBreadthFirstOrder(0, order));
        Assert.Equal([0, 1, 3], order);
    }

    [Fact]
    public void CopyBreadthFirstOrder_ShouldTrackVisitsAcrossBitmapWords_WhenTheGraphExceeds64Vertices()
    {
        // One word of the visited bitmap covers 64 vertices, so a graph that fits in a single word cannot
        // tell a correct word index from an ignored one. The path 0 -> 1 -> ... -> 199 crosses three.
        const int VertexCount = 200;
        GraphEdge[] edges = new GraphEdge[VertexCount - 1];
        for (int vertex = 0; vertex < VertexCount - 1; vertex++)
            edges[vertex] = Edge(vertex, vertex + 1);

        var graph = new CompressedGraph(VertexCount, edges);
        int[] order = new int[VertexCount];

        Assert.Equal(VertexCount, graph.CopyBreadthFirstOrder(0, order));
        Assert.Equal(Enumerable.Range(0, VertexCount), order);
        Assert.Equal(VertexCount - 70, graph.CopyBreadthFirstOrder(70, order));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void CopyBreadthFirstOrder_ShouldThrow_WhenTheSourceIsOutOfRange(int source)
    {
        CompressedGraph graph = Fixture();
        int[] order = new int[5];

        Assert.Throws<ArgumentOutOfRangeException>(() => graph.CopyBreadthFirstOrder(source, order));
    }

    [Fact]
    public void GetBreadthFirstOrder_ShouldSizeTheResultToTheReachableSet_WhenPartOfTheGraphIsUnreachable()
    {
        CompressedGraph graph = Fixture();

        Assert.Equal([0, 1, 3, 4], graph.GetBreadthFirstOrder(0));
        Assert.Equal([2], graph.GetBreadthFirstOrder(2));
    }

    [Fact]
    public void GetBreadthFirstOrder_ShouldReturnEveryVertex_WhenTheWholeGraphIsReachable()
    {
        var graph = new CompressedGraph(3, [Edge(0, 1), Edge(1, 2)]);

        Assert.Equal([0, 1, 2], graph.GetBreadthFirstOrder(0));
    }

    [Fact]
    public void GetBreadthFirstOrder_ShouldThrow_WhenTheSourceIsOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Fixture().GetBreadthFirstOrder(9));
    }

    // ---- topological order -------------------------------------------------------------------------

    [Fact]
    public void TryCopyTopologicalOrder_ShouldPlaceEveryVertexBeforeItsTargets_WhenTheGraphIsAcyclic()
    {
        CompressedGraph graph = Fixture();
        int[] order = new int[graph.VertexCount];

        Assert.True(graph.TryCopyTopologicalOrder(order));
        AssertIsTopologicalOrder(graph, order);
    }

    [Fact]
    public void TryCopyTopologicalOrder_ShouldSucceed_WhenTheGraphHasNoEdges()
    {
        var graph = new CompressedGraph(3, Array.Empty<GraphEdge>());
        int[] order = new int[3];

        Assert.True(graph.TryCopyTopologicalOrder(order));
        Assert.Equal([0, 1, 2], order);
    }

    [Fact]
    public void TryCopyTopologicalOrder_ShouldSucceed_WhenTheGraphHasNoVertices()
    {
        var graph = new CompressedGraph(0, Array.Empty<GraphEdge>());

        Assert.True(graph.TryCopyTopologicalOrder(Span<int>.Empty));
    }

    [Fact]
    public void TryCopyTopologicalOrder_ShouldFail_WhenTheGraphHasACycle()
    {
        var graph = new CompressedGraph(3, [Edge(0, 1), Edge(1, 2), Edge(2, 0)]);
        int[] order = new int[3];

        Assert.False(graph.TryCopyTopologicalOrder(order));
    }

    [Fact]
    public void TryCopyTopologicalOrder_ShouldFail_WhenAVertexPointsAtItself()
    {
        var graph = new CompressedGraph(2, [Edge(0, 1), Edge(1, 1)]);
        int[] order = new int[2];

        Assert.False(graph.TryCopyTopologicalOrder(order));
    }

    [Fact]
    public void TryCopyTopologicalOrder_ShouldThrow_WhenTheDestinationIsShorterThanTheVertexCount()
    {
        CompressedGraph graph = Fixture();
        int[] order = new int[graph.VertexCount - 1];

        ArgumentException error = Assert.Throws<ArgumentException>(() => graph.TryCopyTopologicalOrder(order));
        Assert.Equal("destination", error.ParamName);
    }

    [Fact]
    public void TryGetTopologicalOrder_ShouldReturnTheOrder_WhenTheGraphIsAcyclic()
    {
        CompressedGraph graph = Fixture();

        Assert.True(graph.TryGetTopologicalOrder(out int[] order));
        Assert.Equal(graph.VertexCount, order.Length);
        AssertIsTopologicalOrder(graph, order);
    }

    [Fact]
    public void TryGetTopologicalOrder_ShouldReturnAnEmptyOrder_WhenTheGraphHasACycle()
    {
        var graph = new CompressedGraph(2, [Edge(0, 1), Edge(1, 0)]);

        Assert.False(graph.TryGetTopologicalOrder(out int[] order));
        Assert.Empty(order);
    }

    private static void AssertIsTopologicalOrder(CompressedGraph graph, int[] order)
    {
        Assert.Equal(graph.VertexCount, order.Length);
        Assert.Equal(Enumerable.Range(0, graph.VertexCount), order.OrderBy(vertex => vertex));

        int[] position = new int[graph.VertexCount];
        for (int index = 0; index < order.Length; index++)
            position[order[index]] = index;

        for (int source = 0; source < graph.VertexCount; source++)
        {
            foreach (int target in graph.Neighbors(source).ToArray())
                Assert.True(position[source] < position[target], $"{source} must precede {target}.");
        }
    }
}
