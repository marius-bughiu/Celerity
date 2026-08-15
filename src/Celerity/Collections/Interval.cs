namespace Celerity.Collections;

/// <summary>
/// A half-open interval <c>[Start, End)</c> and the value it carries — the element type of
/// <see cref="IntervalTree{TKey, TValue, TComparer}"/>.
/// </summary>
/// <typeparam name="TKey">The endpoint type, ordered by the tree's comparer.</typeparam>
/// <typeparam name="TValue">The payload carried alongside the range.</typeparam>
/// <remarks>
/// <para>
/// The interval is <b>half-open</b>: it covers every point from <see cref="Start"/> up to but not including
/// <see cref="End"/>, matching <see cref="SegmentTree{T, TMonoid}"/>'s range convention and the BCL's own
/// start/length slicing. Two intervals therefore overlap when each starts strictly before the other ends,
/// which is what lets adjacent ranges such as <c>[0, 10)</c> and <c>[10, 20)</c> tile a line without
/// reporting a conflict at the seam.
/// </para>
/// <para>
/// An interval whose endpoints are equal is empty: it covers no point, so no query ever reports it. It is
/// still legal to store, and it still appears in <see cref="IntervalTree{TKey, TValue, TComparer}"/>'s
/// <see cref="IReadOnlyList{T}"/> surface, so a caller's input is never silently discarded.
/// </para>
/// </remarks>
public readonly struct Interval<TKey, TValue>
{
    /// <summary>Initializes a new interval covering <c>[start, end)</c> and carrying <paramref name="value"/>.</summary>
    /// <param name="start">The inclusive lower endpoint.</param>
    /// <param name="end">The exclusive upper endpoint. Must not precede <paramref name="start"/>.</param>
    /// <param name="value">The payload to carry. May be <c>null</c> for a reference type.</param>
    /// <remarks>
    /// The endpoints are not validated here — ordering <typeparamref name="TKey"/> needs a comparer, and this
    /// type carries none. <see cref="IntervalTree{TKey, TValue, TComparer}"/> rejects an interval whose end
    /// precedes its start when it is built.
    /// </remarks>
    public Interval(TKey start, TKey end, TValue? value)
    {
        Start = start;
        End = end;
        Value = value;
    }

    /// <summary>Gets the inclusive lower endpoint.</summary>
    public TKey Start { get; }

    /// <summary>Gets the exclusive upper endpoint.</summary>
    public TKey End { get; }

    /// <summary>Gets the payload carried by this interval.</summary>
    public TValue? Value { get; }

    /// <summary>Deconstructs the interval into its endpoints and value.</summary>
    /// <param name="start">Receives the inclusive lower endpoint.</param>
    /// <param name="end">Receives the exclusive upper endpoint.</param>
    /// <param name="value">Receives the payload.</param>
    public void Deconstruct(out TKey start, out TKey end, out TValue? value)
    {
        start = Start;
        end = End;
        value = Value;
    }

    /// <summary>Returns a string of the form <c>[start, end) = value</c>.</summary>
    /// <returns>A readable rendering of the interval and its payload.</returns>
    public override string ToString() => $"[{Start}, {End}) = {Value}";
}
