using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Randomized reconciliation of <see cref="CompressedGraph"/> against the adjacency map it replaces — the
/// <c>Dictionary&lt;int, List&lt;int&gt;&gt;</c> a caller writes when the BCL offers no graph at all.
///
/// <para>
/// The compressed layout is what is on trial. Every question the type answers is a slice of one shared array,
/// so a build that misplaces a single offset — by one duplicate collapsed, one isolated vertex skipped, one
/// scatter cursor not shifted back — does not fail loudly: it hands a caller some <i>other</i> vertex's
/// neighbours, which is a plausible-looking answer of the right shape. A hand-written fixture is poorly suited
/// to catching that, because the shapes that expose it (duplicate edges ahead of a later vertex, an isolated
/// vertex in the middle, a graph declared wider than its edges reach) are exactly the ones an example is
/// written without.
/// </para>
///
/// <para>
/// Three layers, narrowest first. The CsCheck property generates the edge set from its own axes — vertex
/// count, edge count and the fraction of self-loops — so a disagreement shrinks to a minimal reproduction with
/// the seed printed. The seeded theory below it drives larger graphs at fixed shapes, including one wide
/// enough to cross several words of the traversal's visited bitmap. The exhaustive sweep at the end checks
/// <i>every</i> directed graph on three vertices — all 512 of them, self-loops included — which is the only
/// layer that can prove no case was merely missed by sampling.
/// </para>
/// </summary>
public class CompressedGraphDifferentialTests
{
    // The axes that decide which build paths a case can reach: how many vertices there are (a wide graph with
    // few edges leaves isolated vertices, which is what makes the offset repeats appear), how many edges (a
    // dense one against a narrow vertex set makes duplicates near-certain, which is what exercises the
    // compaction), and how many of those are self-loops.
    private static readonly Gen<(int VertexCount, int EdgeCount, int LoopPercent, uint Seed)> GenGraphs =
        Gen.Select(Gen.Int[1, 40], Gen.Int[0, 120], Gen.Int[0, 40], Gen.UInt);

    [Fact]
    public void EveryQuery_ShouldMatchAnAdjacencyMap_UnderGeneratedEdgeSets()
    {
        GenGraphs.Sample(spec =>
        {
            GraphEdge[] edges = BuildEdges(spec.VertexCount, spec.EdgeCount, spec.LoopPercent, spec.Seed);
            AssertAgreesWithOracle(spec.VertexCount, edges);
        }, iter: 500);
    }

    [Theory]
    [InlineData(64, 400, 1u)]
    [InlineData(65, 400, 2u)]
    [InlineData(200, 2000, 3u)]
    [InlineData(500, 500, 4u)]
    public void EveryQuery_ShouldMatchAnAdjacencyMap_OnLargerSeededGraphs(int vertexCount, int edgeCount, uint seed)
    {
        AssertAgreesWithOracle(vertexCount, BuildEdges(vertexCount, edgeCount, 5, seed));
    }

    [Fact]
    public void EveryQuery_ShouldMatchAnAdjacencyMap_AcrossEveryDirectedGraphOnThreeVertices()
    {
        const int VertexCount = 3;
        const int PossibleEdges = VertexCount * VertexCount;

        for (int mask = 0; mask < 1 << PossibleEdges; mask++)
        {
            var edges = new List<GraphEdge>();
            for (int bit = 0; bit < PossibleEdges; bit++)
            {
                if ((mask & (1 << bit)) != 0)
                    edges.Add(new GraphEdge(bit / VertexCount, bit % VertexCount));
            }

            AssertAgreesWithOracle(VertexCount, [.. edges]);
        }
    }

    private static GraphEdge[] BuildEdges(int vertexCount, int edgeCount, int loopPercent, uint seed)
    {
        var random = new Random((int)seed);
        var edges = new GraphEdge[edgeCount];
        for (int i = 0; i < edgeCount; i++)
        {
            int source = random.Next(vertexCount);
            int target = random.Next(100) < loopPercent ? source : random.Next(vertexCount);
            edges[i] = new GraphEdge(source, target);
        }

        return edges;
    }

    private static void AssertAgreesWithOracle(int vertexCount, GraphEdge[] edges)
    {
        var graph = new CompressedGraph(vertexCount, edges);
        List<int>[] oracle = Oracle(vertexCount, edges);

        Assert.Equal(vertexCount, graph.VertexCount);
        Assert.Equal(oracle.Sum(targets => targets.Count), graph.EdgeCount);

        for (int vertex = 0; vertex < vertexCount; vertex++)
        {
            Assert.Equal(oracle[vertex], graph.Neighbors(vertex).ToArray());
            Assert.Equal(oracle[vertex].Count, graph.Degree(vertex));

            for (int target = 0; target < vertexCount; target++)
                Assert.Equal(oracle[vertex].Contains(target), graph.ContainsEdge(vertex, target));
        }

        AssertEdgeListAgrees(graph, oracle);
        AssertTransposeAgrees(graph, vertexCount, oracle);
        AssertTraversalsAgree(graph, vertexCount, oracle);
    }

    private static void AssertEdgeListAgrees(CompressedGraph graph, List<int>[] oracle)
    {
        var expected = new List<GraphEdge>();
        for (int vertex = 0; vertex < oracle.Length; vertex++)
        {
            foreach (int target in oracle[vertex])
                expected.Add(new GraphEdge(vertex, target));
        }

        Assert.Equal(expected, graph.ToArray());

        // The indexer recovers the source by binary search rather than by the enumerator's running cursor, so
        // it is a genuinely different code path over the same data and has to agree with it.
        for (int index = 0; index < expected.Count; index++)
            Assert.Equal(expected[index], graph[index]);
    }

    private static void AssertTransposeAgrees(CompressedGraph graph, int vertexCount, List<int>[] oracle)
    {
        var expected = new List<int>[vertexCount];
        for (int vertex = 0; vertex < vertexCount; vertex++)
            expected[vertex] = [];

        for (int source = 0; source < vertexCount; source++)
        {
            foreach (int target in oracle[source])
                expected[target].Add(source);
        }

        CompressedGraph reversed = graph.Reverse();
        Assert.Equal(graph.EdgeCount, reversed.EdgeCount);
        for (int vertex = 0; vertex < vertexCount; vertex++)
            Assert.Equal(expected[vertex], reversed.Neighbors(vertex).ToArray());
    }

    private static void AssertTraversalsAgree(CompressedGraph graph, int vertexCount, List<int>[] oracle)
    {
        int[] buffer = new int[vertexCount];
        for (int source = 0; source < vertexCount; source++)
        {
            List<int> expected = ReferenceBreadthFirst(oracle, vertexCount, source);
            int count = graph.CopyBreadthFirstOrder(source, buffer);

            Assert.Equal(expected, buffer[..count]);
            Assert.Equal(expected, graph.GetBreadthFirstOrder(source));

            // A short destination must hand back the prefix of the same order, not a different traversal.
            if (expected.Count > 1)
            {
                int[] truncated = new int[expected.Count - 1];
                Assert.Equal(truncated.Length, graph.CopyBreadthFirstOrder(source, truncated));
                Assert.Equal(expected.Take(truncated.Length), truncated);
            }
        }

        bool acyclic = graph.TryGetTopologicalOrder(out int[] order);
        Assert.Equal(IsAcyclic(oracle, vertexCount), acyclic);
        if (!acyclic)
        {
            Assert.Empty(order);
            return;
        }

        Assert.Equal(Enumerable.Range(0, vertexCount), order.OrderBy(vertex => vertex));

        int[] position = new int[vertexCount];
        for (int index = 0; index < order.Length; index++)
            position[order[index]] = index;

        for (int source = 0; source < vertexCount; source++)
        {
            foreach (int target in oracle[source])
                Assert.True(position[source] < position[target], $"{source} must precede {target}.");
        }
    }

    // The adjacency map a caller writes by hand, with the same edge-set semantics the type documents: targets
    // distinct and ascending, so a breadth-first walk over it visits in the same order.
    private static List<int>[] Oracle(int vertexCount, GraphEdge[] edges)
    {
        var sorted = new SortedSet<int>[vertexCount];
        for (int vertex = 0; vertex < vertexCount; vertex++)
            sorted[vertex] = [];

        foreach (GraphEdge edge in edges)
            sorted[edge.Source].Add(edge.Target);

        var oracle = new List<int>[vertexCount];
        for (int vertex = 0; vertex < vertexCount; vertex++)
            oracle[vertex] = [.. sorted[vertex]];

        return oracle;
    }

    private static List<int> ReferenceBreadthFirst(List<int>[] oracle, int vertexCount, int source)
    {
        var visited = new bool[vertexCount];
        var queue = new Queue<int>();
        List<int> order = [source];

        visited[source] = true;
        queue.Enqueue(source);
        while (queue.Count > 0)
        {
            foreach (int target in oracle[queue.Dequeue()])
            {
                if (visited[target])
                    continue;

                visited[target] = true;
                order.Add(target);
                queue.Enqueue(target);
            }
        }

        return order;
    }

    // Depth-first three-colouring, which decides acyclicity by a different method than the type's
    // in-degree peeling — an agreeing bug would have to be present in both.
    private static bool IsAcyclic(List<int>[] oracle, int vertexCount)
    {
        var state = new byte[vertexCount];
        for (int vertex = 0; vertex < vertexCount; vertex++)
        {
            if (state[vertex] == 0 && HasCycleFrom(oracle, state, vertex))
                return false;
        }

        return true;
    }

    private static bool HasCycleFrom(List<int>[] oracle, byte[] state, int vertex)
    {
        state[vertex] = 1;
        foreach (int target in oracle[vertex])
        {
            if (state[target] == 1)
                return true;

            if (state[target] == 0 && HasCycleFrom(oracle, state, target))
                return true;
        }

        state[vertex] = 2;
        return false;
    }
}
