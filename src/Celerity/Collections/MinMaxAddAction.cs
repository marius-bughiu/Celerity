using System.Numerics;

namespace Celerity.Collections;

/// <summary>
/// Range <b>add</b> over range <b>minimum</b> or <b>maximum</b>: adding the same value to every element of a range
/// moves its minimum and its maximum by exactly that value, whatever the range's length. Pair it with
/// <see cref="MinMonoid{T}"/> or <see cref="MaxMonoid{T}"/>.
/// </summary>
/// <typeparam name="T">The ordered numeric element type.</typeparam>
/// <remarks>
/// <para>
/// This is the headline lazy-segment-tree workload: "book one seat on every slot of <c>[a, b)</c>" as a range add,
/// "is there a free seat anywhere in <c>[c, e)</c>?" as a range maximum. Neither <see cref="FenwickTree{T}"/> nor
/// <see cref="SegmentTree{T, TMonoid}"/> can do both in <c>O(log n)</c>.
/// </para>
/// <para>
/// <b>Domain.</b> Adding a constant preserves order only while no element overflows. For a fixed-width integer
/// <typeparamref name="T"/> the laws therefore hold over values whose updated results stay in range; an element
/// that wraps compares as if it had not moved past the boundary, and the aggregate is unspecified. For a
/// floating-point <typeparamref name="T"/> the domain is the finite values, as it is for the paired monoids.
/// </para>
/// </remarks>
public readonly struct MinMaxAddAction<T> : IRangeAction<T, T>
    where T : struct, INumber<T>
{
    /// <summary>Gets the update that changes nothing, <c>zero</c>.</summary>
    public T Identity => T.Zero;

    /// <summary>Merges two pending additions into one.</summary>
    /// <param name="earlier">The addition applied first.</param>
    /// <param name="later">The addition applied second.</param>
    /// <returns>The sum of the two additions.</returns>
    public T Compose(T earlier, T later) => earlier + later;

    /// <summary>Moves a range's minimum or maximum by <paramref name="update"/>.</summary>
    /// <param name="update">The value added to every element.</param>
    /// <param name="aggregate">The range's minimum or maximum before the update.</param>
    /// <param name="count">The number of elements in the range. Not used: the extreme moves by the update alone.</param>
    /// <returns><c>aggregate + update</c>.</returns>
    public T Apply(T update, T aggregate, int count) => aggregate + update;
}
