namespace Celerity.Collections;

/// <summary>
/// A directed edge from one vertex id to another — the element type of <see cref="CompressedGraph"/>.
/// </summary>
/// <remarks>
/// <para>
/// Both endpoints are dense vertex ids in <c>[0, VertexCount)</c>. The type carries no payload: a graph built
/// on dense ids lets a caller keep per-edge data in a parallel array of their own, indexed the same way, which
/// is cheaper than threading a type parameter through the adjacency structure and is the reason
/// <see cref="CompressedGraph"/> is not generic.
/// </para>
/// <para>
/// The endpoints are not validated here — legality depends on the vertex count, which this type does not know.
/// <see cref="CompressedGraph"/> rejects an out-of-range endpoint when it is built.
/// </para>
/// </remarks>
public readonly struct GraphEdge : IEquatable<GraphEdge>
{
    /// <summary>Initializes a new directed edge from <paramref name="source"/> to <paramref name="target"/>.</summary>
    /// <param name="source">The vertex the edge leaves.</param>
    /// <param name="target">The vertex the edge enters.</param>
    public GraphEdge(int source, int target)
    {
        Source = source;
        Target = target;
    }

    /// <summary>Gets the vertex the edge leaves.</summary>
    public int Source { get; }

    /// <summary>Gets the vertex the edge enters.</summary>
    public int Target { get; }

    /// <summary>Determines whether two edges have the same endpoints in the same direction.</summary>
    /// <param name="left">The first edge.</param>
    /// <param name="right">The second edge.</param>
    /// <returns><c>true</c> if the endpoints match; otherwise <c>false</c>.</returns>
    public static bool operator ==(GraphEdge left, GraphEdge right) => left.Equals(right);

    /// <summary>Determines whether two edges differ in either endpoint or in direction.</summary>
    /// <param name="left">The first edge.</param>
    /// <param name="right">The second edge.</param>
    /// <returns><c>true</c> if the edges differ; otherwise <c>false</c>.</returns>
    public static bool operator !=(GraphEdge left, GraphEdge right) => !left.Equals(right);

    /// <summary>Deconstructs the edge into its endpoints.</summary>
    /// <param name="source">Receives the vertex the edge leaves.</param>
    /// <param name="target">Receives the vertex the edge enters.</param>
    public void Deconstruct(out int source, out int target)
    {
        source = Source;
        target = Target;
    }

    /// <summary>Determines whether this edge has the same endpoints, in the same direction, as another.</summary>
    /// <param name="other">The edge to compare against.</param>
    /// <returns><c>true</c> if the endpoints match; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// The edge is directed, so <c>(1, 2)</c> and <c>(2, 1)</c> are different edges. An undirected graph is
    /// built by supplying both.
    /// </remarks>
    public bool Equals(GraphEdge other) => Source == other.Source && Target == other.Target;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is GraphEdge other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Source, Target);

    /// <summary>Returns a string of the form <c>source -&gt; target</c>.</summary>
    /// <returns>A readable rendering of the edge.</returns>
    public override string ToString() => $"{Source} -> {Target}";
}
