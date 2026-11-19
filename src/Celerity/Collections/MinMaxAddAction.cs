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
/// "what is the peak occupancy in <c>[c, e)</c>?" as a range maximum. Neither <see cref="FenwickTree{T}"/> nor
/// <see cref="SegmentTree{T, TMonoid}"/> can do both in <c>O(log n)</c>.
/// </para>
/// <para>
/// <b>Domain.</b> Adding a constant preserves order only while no element overflows. For a fixed-width integer
/// <typeparamref name="T"/> the laws therefore hold over values whose updated results stay in range; an element
/// that wraps compares as if it had not moved past the boundary, and the aggregate is unspecified.
/// </para>
/// <para>
/// For a floating-point <typeparamref name="T"/> the laws need every sum involved to be <b>exact</b>, not merely
/// finite. A node's pending update is the sum of the updates parked on it, applied in one step, and floating-point
/// addition is not associative: under <see cref="MaxMonoid{T}"/> over <c>[1.0, 2.0]</c>, adding <c>1e20</c> and then
/// <c>-1e20</c> to the whole range makes the root <c>0</c> (each element rounds to <c>1e20</c> and back to
/// <c>0</c>), while the pending update composes to exactly <c>0</c>, so a later read of element 0 sees <c>1</c>. Keep
/// floating-point values and updates exact in the mantissa — for <see cref="double"/>, integers below <c>2^53</c> —
/// or use an integer <typeparamref name="T"/>.
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
