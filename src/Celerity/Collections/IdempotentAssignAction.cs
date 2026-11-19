namespace Celerity.Collections;

/// <summary>
/// Range <b>assign</b> over any <b>idempotent</b> fold — minimum, maximum, bitwise and / or, gcd: setting every
/// element of a range to one value makes that value the range's aggregate, whatever the range's length. The update
/// is a <see cref="Nullable{T}"/> whose <c>null</c> is the update that changes nothing.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <remarks>
/// <para>
/// Lawful with exactly the folds that implement <see cref="IIdempotentMonoid{T}"/>, because that is what
/// <c>Combine(v, v) == v</c> guarantees: the fold of <c>n</c> copies of <c>v</c> is <c>v</c>. With
/// <see cref="SumMonoid{T}"/>, which is not idempotent, use <see cref="SumAssignAction{T}"/> instead. The
/// constraint cannot say this, because an action and a monoid are separate type arguments.
/// </para>
/// <para>
/// Assignments do not commute: a later one replaces an earlier one outright, which is what <see cref="Compose"/>
/// does.
/// </para>
/// </remarks>
public readonly struct IdempotentAssignAction<T> : IRangeAction<T, T?>
    where T : struct
{
    /// <summary>Gets the update that changes nothing, <c>null</c>.</summary>
    public T? Identity => null;

    /// <summary>Merges two pending assignments: the later one wins, unless it is <c>null</c>.</summary>
    /// <param name="earlier">The assignment applied first.</param>
    /// <param name="later">The assignment applied second.</param>
    /// <returns><paramref name="later"/> when it has a value; otherwise <paramref name="earlier"/>.</returns>
    public T? Compose(T? earlier, T? later) => later.HasValue ? later : earlier;

    /// <summary>Replaces a range's aggregate with the assigned value.</summary>
    /// <param name="update">The value every element is set to, or <c>null</c> for no change.</param>
    /// <param name="aggregate">The range's aggregate before the update.</param>
    /// <param name="count">The number of elements in the range. Not used: an idempotent fold of copies is the copy.</param>
    /// <returns>The assigned value, or <paramref name="aggregate"/> when <paramref name="update"/> is <c>null</c>.</returns>
    public T Apply(T? update, T aggregate, int count) => update ?? aggregate;
}
