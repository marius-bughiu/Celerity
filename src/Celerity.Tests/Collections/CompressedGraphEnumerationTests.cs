using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Coverage for <see cref="CompressedGraph"/>'s <see cref="IReadOnlyList{T}"/> surface: the struct enumerator
/// and both boxed interface paths, the indexer and its bounds, and the deliberately explicit
/// <see cref="IReadOnlyCollection{T}.Count"/>.
///
/// <para>
/// The enumerator advances its source cursor forward rather than recovering the source per edge, which is what
/// keeps a full walk linear — so the cases that matter are the ones where that cursor has to skip: a vertex
/// with no out-edges between two that have them, and a run of them at either end of the graph.
/// </para>
/// </summary>
public class CompressedGraphEnumerationTests
{
    private static GraphEdge Edge(int source, int target) => new(source, target);

    // Isolated vertices at the front (0), in the middle (2, 3) and at the back (6), so the enumerator's source
    // cursor has to skip one, several, and a trailing run it must never reach.
    private static CompressedGraph Fixture() => new(7,
    [
        Edge(1, 4),
        Edge(1, 0),
        Edge(4, 5),
        Edge(5, 1),
        Edge(5, 6),
    ]);

    [Fact]
    public void GetEnumerator_ShouldYieldEveryEdgeInSourceMajorOrder_WhenVerticesAreIsolated()
    {
        var edges = new List<GraphEdge>();
        foreach (GraphEdge edge in Fixture())
            edges.Add(edge);

        Assert.Equal(
            [Edge(1, 0), Edge(1, 4), Edge(4, 5), Edge(5, 1), Edge(5, 6)],
            edges);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenTheGraphHasNoEdges()
    {
        var graph = new CompressedGraph(3, Array.Empty<GraphEdge>());

        CompressedGraph.Enumerator enumerator = graph.GetEnumerator();

        Assert.False(enumerator.MoveNext());
        Assert.Equal(default, enumerator.Current);
    }

    [Fact]
    public void MoveNext_ShouldClearCurrent_WhenTheWalkRunsOut()
    {
        CompressedGraph.Enumerator enumerator = Fixture().GetEnumerator();

        while (enumerator.MoveNext())
        {
        }

        Assert.Equal(default, enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Reset_ShouldRestartTheWalk_WhenTheEnumeratorIsPartiallyConsumed()
    {
        CompressedGraph.Enumerator enumerator = Fixture().GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        Assert.Equal(Edge(1, 4), enumerator.Current);

        enumerator.Reset();

        Assert.Equal(default, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(Edge(1, 0), enumerator.Current);

        enumerator.Dispose();
    }

    [Fact]
    public void GenericEnumerator_ShouldYieldTheSameEdges_WhenTakenThroughTheInterface()
    {
        IEnumerable<GraphEdge> graph = Fixture();

        using IEnumerator<GraphEdge> enumerator = graph.GetEnumerator();

        var edges = new List<GraphEdge>();
        while (enumerator.MoveNext())
            edges.Add(enumerator.Current);

        Assert.Equal(Fixture().ToArray(), edges);
    }

    [Fact]
    public void NonGenericEnumerator_ShouldYieldTheSameEdges_WhenTakenThroughTheInterface()
    {
        IEnumerable graph = Fixture();

        IEnumerator enumerator = graph.GetEnumerator();

        var edges = new List<GraphEdge>();
        while (enumerator.MoveNext())
            edges.Add((GraphEdge)enumerator.Current!);

        Assert.Equal(Fixture().ToArray(), edges);
    }

    [Fact]
    public void Count_ShouldReportTheEdgeCount_WhenReadThroughTheReadOnlyCollectionInterface()
    {
        IReadOnlyCollection<GraphEdge> graph = Fixture();

        Assert.Equal(5, graph.Count);
        Assert.Equal(((CompressedGraph)graph).EdgeCount, graph.Count);
    }

    [Fact]
    public void Indexer_ShouldRecoverTheSource_WhenIsolatedVerticesRepeatAnOffset()
    {
        CompressedGraph graph = Fixture();

        Assert.Equal(Edge(1, 0), graph[0]);
        Assert.Equal(Edge(1, 4), graph[1]);
        Assert.Equal(Edge(4, 5), graph[2]);
        Assert.Equal(Edge(5, 1), graph[3]);
        Assert.Equal(Edge(5, 6), graph[4]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void Indexer_ShouldThrow_WhenTheIndexIsOutsideTheEdgeRange(int index)
    {
        CompressedGraph graph = Fixture();

        Assert.Throws<ArgumentOutOfRangeException>(() => graph[index]);
    }

    [Fact]
    public void GraphEdge_ShouldCompareByBothEndpointsAndDirection()
    {
        GraphEdge edge = Edge(2, 7);

        Assert.True(edge.Equals(Edge(2, 7)));
        Assert.True(edge == Edge(2, 7));
        Assert.False(edge != Edge(2, 7));
        Assert.False(edge.Equals(Edge(2, 8)));
        Assert.False(edge.Equals(Edge(3, 7)));
        Assert.True(edge != Edge(7, 2));
        Assert.True(edge.Equals((object)Edge(2, 7)));
        Assert.False(edge.Equals("2 -> 7"));
        Assert.Equal(Edge(2, 7).GetHashCode(), edge.GetHashCode());
        Assert.Equal("2 -> 7", edge.ToString());
    }

    [Fact]
    public void GraphEdge_ShouldDeconstructIntoItsEndpoints()
    {
        (int source, int target) = Edge(3, 9);

        Assert.Equal(3, source);
        Assert.Equal(9, target);
    }
}
