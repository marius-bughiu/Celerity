namespace Celerity.Collections;

/// <summary>
/// A point in the plane and the value it carries — the element type of <see cref="KdTree{TValue}"/>.
/// </summary>
/// <typeparam name="TValue">The payload carried alongside the coordinates.</typeparam>
/// <remarks>
/// <para>
/// The coordinates are <see cref="double"/> rather than a generic numeric type. <c>INumber&lt;T&gt;</c> is
/// available on the <c>net8.0</c> floor and would admit <see cref="int"/> or <see cref="float"/> coordinates,
/// but every spatial query compares <i>squared</i> distances, and a squared distance silently overflows an
/// integer type at coordinates a map or a game world reaches easily. Widening to <see cref="double"/> at the
/// boundary is the cost of not having that failure mode.
/// </para>
/// <para>
/// A coordinate of <see cref="double.NaN"/> has no position, so it cannot be ordered, indexed, or measured
/// against. <see cref="KdTree{TValue}"/> rejects such a point when it is built rather than storing one that no
/// query could answer for.
/// </para>
/// </remarks>
public readonly struct SpatialPoint<TValue>
{
    /// <summary>Initializes a new point at <c>(x, y)</c> carrying <paramref name="value"/>.</summary>
    /// <param name="x">The horizontal coordinate.</param>
    /// <param name="y">The vertical coordinate.</param>
    /// <param name="value">The payload to carry. May be <c>null</c> for a reference type.</param>
    /// <remarks>
    /// The coordinates are not validated here. <see cref="KdTree{TValue}"/> rejects a <see cref="double.NaN"/>
    /// coordinate when it is built, which is the point at which one first has to be ordered.
    /// </remarks>
    public SpatialPoint(double x, double y, TValue? value)
    {
        X = x;
        Y = y;
        Value = value;
    }

    /// <summary>Gets the horizontal coordinate.</summary>
    public double X { get; }

    /// <summary>Gets the vertical coordinate.</summary>
    public double Y { get; }

    /// <summary>Gets the payload carried by this point.</summary>
    public TValue? Value { get; }

    /// <summary>Deconstructs the point into its coordinates and value.</summary>
    /// <param name="x">Receives the horizontal coordinate.</param>
    /// <param name="y">Receives the vertical coordinate.</param>
    /// <param name="value">Receives the payload.</param>
    public void Deconstruct(out double x, out double y, out TValue? value)
    {
        x = X;
        y = Y;
        value = Value;
    }

    /// <summary>Returns a string of the form <c>(x, y) = value</c>.</summary>
    /// <returns>A readable rendering of the point and its payload.</returns>
    public override string ToString() => $"({X}, {Y}) = {Value}";
}
