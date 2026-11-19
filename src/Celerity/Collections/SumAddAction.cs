using System.Numerics;

namespace Celerity.Collections;

/// <summary>
/// Range <b>add</b> over range <b>sums</b>: the update is a value added to every element of the range, so a
/// range's sum moves by <c>update × count</c>. Pair it with <see cref="SumMonoid{T}"/>.
/// </summary>
/// <typeparam name="T">The numeric element type.</typeparam>
/// <remarks>
/// <para>
/// The count is converted with <see cref="INumberBase{TSelf}.CreateTruncating{TOther}(TOther)"/>, which for a
/// fixed-width integer is exactly right rather than an approximation: <see cref="SumMonoid{T}"/> wraps on
/// overflow, and adding <c>d</c> to <c>n</c> elements moves a wrapping sum by <c>d × n</c> modulo the type's
/// width — the same residue the truncated count gives. For <see cref="float"/> and <see cref="double"/> the one
/// multiplication rounds differently from <c>n</c> separate additions, as any reassociation of a floating-point
/// sum does.
/// </para>
/// <para>
/// For range sums under range add with <i>no</i> other fold, two <see cref="FenwickTree{T}"/> instances over a
/// difference sequence also do the job, in less memory and with a shorter constant. Prefer that pair when range add
/// and range sum are the whole workload.
/// </para>
/// </remarks>
public readonly struct SumAddAction<T> : IRangeAction<T, T>
    where T : struct, INumberBase<T>
{
    /// <summary>Gets the update that changes nothing, <c>zero</c>.</summary>
    public T Identity => T.Zero;

    /// <summary>Merges two pending additions into one.</summary>
    /// <param name="earlier">The addition applied first.</param>
    /// <param name="later">The addition applied second.</param>
    /// <returns>The sum of the two additions.</returns>
    public T Compose(T earlier, T later) => earlier + later;

    /// <summary>Moves a range's sum by <paramref name="update"/> once per element.</summary>
    /// <param name="update">The value added to every element.</param>
    /// <param name="aggregate">The range's sum before the update.</param>
    /// <param name="count">The number of elements in the range.</param>
    /// <returns><c>aggregate + update × count</c>.</returns>
    public T Apply(T update, T aggregate, int count) => aggregate + update * T.CreateTruncating(count);
}
