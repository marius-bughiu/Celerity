namespace Celerity.Collections;

/// <summary>
/// An axis-aligned rectangle in the plane and the value it carries — the element type of
/// <see cref="RTree{TValue}"/>.
/// </summary>
/// <typeparam name="TValue">The payload carried alongside the extent.</typeparam>
/// <remarks>
/// <para>
/// This is the extent counterpart of <see cref="SpatialPoint{TValue}"/>: a thing that <i>occupies</i> an area
/// rather than one that sits at a coordinate. The distinction is the whole reason
/// <see cref="RTree{TValue}"/> exists next to <see cref="KdTree{TValue}"/> — a box can overlap a query while
/// its centre sits far outside it, so indexing centres as points answers a different question.
/// </para>
/// <para>
/// The edges are closed: a box covers <c>[MinX, MaxX] &#215; [MinY, MaxY]</c>, so two boxes that share only an
/// edge or a corner do overlap, and a point exactly on an edge is inside. A box may be degenerate — equal
/// edges make it a point, and equal edges on one axis make it a segment — which is deliberate, since that is
/// how a point is filed alongside extents in the same index.
/// </para>
/// <para>
/// The coordinates are <see cref="double"/> rather than a generic numeric type, matching
/// <see cref="SpatialPoint{TValue}"/> so that the two spatial element types agree at the boundary. Unlike the
/// point, nothing here is ever squared — every query is a comparison — so <see cref="RTree{TValue}"/> imposes
/// no magnitude bound and only requires that the coordinates be finite and that neither upper edge precede
/// its lower one.
/// </para>
/// </remarks>
public readonly struct SpatialBox<TValue>
{
    /// <summary>
    /// Initializes a new box covering <c>[minX, maxX] &#215; [minY, maxY]</c> carrying
    /// <paramref name="value"/>.
    /// </summary>
    /// <param name="minX">The inclusive left edge.</param>
    /// <param name="minY">The inclusive bottom edge.</param>
    /// <param name="maxX">The inclusive right edge.</param>
    /// <param name="maxY">The inclusive top edge.</param>
    /// <param name="value">The payload to carry. May be <c>null</c> for a reference type.</param>
    /// <remarks>
    /// The edges are not validated here. <see cref="RTree{TValue}"/> rejects a coordinate that is not finite,
    /// and an upper edge that precedes its lower one, when it is built — the point at which one first has to
    /// be ordered and bounded.
    /// </remarks>
    public SpatialBox(double minX, double minY, double maxX, double maxY, TValue? value)
    {
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
        Value = value;
    }

    /// <summary>Gets the inclusive left edge.</summary>
    public double MinX { get; }

    /// <summary>Gets the inclusive bottom edge.</summary>
    public double MinY { get; }

    /// <summary>Gets the inclusive right edge.</summary>
    public double MaxX { get; }

    /// <summary>Gets the inclusive top edge.</summary>
    public double MaxY { get; }

    /// <summary>Gets the payload carried by this box.</summary>
    public TValue? Value { get; }

    /// <summary>Deconstructs the box into its edges and value.</summary>
    /// <param name="minX">Receives the inclusive left edge.</param>
    /// <param name="minY">Receives the inclusive bottom edge.</param>
    /// <param name="maxX">Receives the inclusive right edge.</param>
    /// <param name="maxY">Receives the inclusive top edge.</param>
    /// <param name="value">Receives the payload.</param>
    public void Deconstruct(out double minX, out double minY, out double maxX, out double maxY, out TValue? value)
    {
        minX = MinX;
        minY = MinY;
        maxX = MaxX;
        maxY = MaxY;
        value = Value;
    }

    /// <summary>Returns a string of the form <c>[minX, maxX] x [minY, maxY] = value</c>.</summary>
    /// <returns>A readable rendering of the box and its payload.</returns>
    public override string ToString() => $"[{MinX}, {MaxX}] x [{MinY}, {MaxY}] = {Value}";
}
