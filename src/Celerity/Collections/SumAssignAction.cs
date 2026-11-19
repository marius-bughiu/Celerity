using System.Numerics;

namespace Celerity.Collections;

/// <summary>
/// Range <b>assign</b> over range <b>sums</b>: the update sets every element of the range to one value, so the
/// range's sum becomes <c>value × count</c>. The update is a <see cref="Nullable{T}"/> whose <c>null</c> is the
/// update that changes nothing. Pair it with <see cref="SumMonoid{T}"/>.
/// </summary>
/// <typeparam name="T">The numeric element type.</typeparam>
/// <remarks>
/// Assignments do not commute: a later one replaces an earlier one outright, which is what
/// <see cref="Compose"/> does. As with <see cref="SumAddAction{T}"/>, the count is converted with
/// <see cref="INumberBase{TSelf}.CreateTruncating{TOther}(TOther)"/>, which gives the exact wrapping sum for a
/// fixed-width integer; <see cref="Half"/>, which cannot hold a count above 65,504, is scaled in
/// <see cref="float"/> instead.
/// </remarks>
public readonly struct SumAssignAction<T> : IRangeAction<T, T?>
    where T : struct, INumberBase<T>
{
    /// <summary>Gets the update that changes nothing, <c>null</c>.</summary>
    public T? Identity => null;

    /// <summary>Merges two pending assignments: the later one wins, unless it is <c>null</c>.</summary>
    /// <param name="earlier">The assignment applied first.</param>
    /// <param name="later">The assignment applied second.</param>
    /// <returns><paramref name="later"/> when it has a value; otherwise <paramref name="earlier"/>.</returns>
    public T? Compose(T? earlier, T? later) => later.HasValue ? later : earlier;

    /// <summary>Replaces a range's sum with that of <paramref name="count"/> copies of the assigned value.</summary>
    /// <param name="update">The value every element is set to, or <c>null</c> for no change.</param>
    /// <param name="aggregate">The range's sum before the update.</param>
    /// <param name="count">The number of elements in the range.</param>
    /// <returns><c>update × count</c>, or <paramref name="aggregate"/> when <paramref name="update"/> is <c>null</c>.</returns>
    public T Apply(T? update, T aggregate, int count) =>
        update.HasValue ? RangeCountScale.Multiply(update.GetValueOrDefault(), count) : aggregate;
}
