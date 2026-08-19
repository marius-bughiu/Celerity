using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Runs the <see cref="CompressedGraph"/> usage examples published in <c>docs/api/collections.md</c>, the
/// README and the type's own XML documentation, and asserts the outputs those pages print in their comments.
///
/// <para>
/// A published example is documentation a reader will copy, so an incorrect one is a defect rather than a
/// typo — and the values here are exactly the kind worked out by hand: which of several admissible
/// topological orders Kahn's algorithm actually emits depends on the order vertices are enqueued, which is a
/// property of the implementation rather than of the graph. The assertions below are what stop the pages and
/// the code drifting apart.
/// </para>
/// </summary>
public class CompressedGraphDocumentationExampleTests
{
    // Verbatim from docs/api/collections.md and README.md.
    private static CompressedGraph Builds() => new(5,
    [
        new GraphEdge(0, 1),
        new GraphEdge(0, 3),
        new GraphEdge(1, 4),
        new GraphEdge(3, 4),
    ]);

    [Fact]
    public void AdjacencyExample_ShouldPrintWhatTheDocumentedCommentsClaim()
    {
        CompressedGraph builds = Builds();

        Assert.Equal([1, 3], builds.Neighbors(0).ToArray());
        Assert.Equal(2, builds.Degree(0));
        Assert.False(builds.ContainsEdge(0, 4));
    }

    [Fact]
    public void TopologicalExample_ShouldPrintWhatTheDocumentedCommentsClaim()
    {
        Assert.True(Builds().TryGetTopologicalOrder(out int[] order));

        Assert.Equal("0 2 1 3 4", string.Join(" ", order));
    }

    [Fact]
    public void ReverseExample_ShouldPrintWhatTheDocumentedCommentsClaim()
    {
        CompressedGraph dependents = Builds().Reverse();

        Assert.Equal("1 3", string.Join(" ", dependents.Neighbors(4).ToArray()));
    }

    [Fact]
    public void BreadthFirstExample_ShouldPrintWhatTheDocumentedCommentsClaim()
    {
        CompressedGraph builds = Builds();
        int[] reached = new int[builds.VertexCount];

        int count = builds.CopyBreadthFirstOrder(0, reached);

        Assert.Equal("0 1 3 4", string.Join(" ", reached[..count]));
    }

    // Verbatim from the <example> block on CompressedGraph itself, which uses a different graph.
    [Fact]
    public void XmlDocumentationExample_ShouldPrintWhatItsCommentsClaim()
    {
        var builds = new CompressedGraph(4,
        [
            new GraphEdge(0, 1),
            new GraphEdge(0, 2),
            new GraphEdge(1, 3),
            new GraphEdge(2, 3),
        ]);

        Assert.Equal([1, 2], builds.Neighbors(0).ToArray());
        Assert.True(builds.TryGetTopologicalOrder(out int[] order));
        Assert.Equal("0 1 2 3", string.Join(" ", order));
    }
}
