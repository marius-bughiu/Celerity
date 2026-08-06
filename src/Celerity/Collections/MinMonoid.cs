using System.Numerics;

namespace Celerity.Collections;

/// <summary>
/// The minimum monoid: <c>Combine</c> keeps the smaller of two values and the identity is
/// <see cref="IMinMaxValue{TSelf}.MaxValue"/>.
/// </summary>
/// <typeparam name="T">The ordered numeric element type.</typeparam>
/// <remarks>
/// <para>
/// This is one of the folds <see cref="FenwickTree{T}"/> structurally cannot answer: a Fenwick range query is
/// the difference of two prefix folds, so it needs an inverse, and minimum has none. Range minimum over a
/// sequence that keeps changing is the headline <see cref="SegmentTree{T, TMonoid}"/> workload.
/// </para>
/// <para>
/// <b>Floating-point caveat.</b> The identity is <c>T.MaxValue</c> — the largest <i>finite</i> value — because
/// <see cref="IMinMaxValue{TSelf}"/> is what the constraint can ask for. For <see cref="float"/> /
/// <see cref="double"/> that means a stored <c>+∞</c> aggregates to <c>T.MaxValue</c> rather than to <c>+∞</c>.
/// <c>NaN</c> loses every <c>&lt;</c> comparison, so <c>Combine(NaN, x)</c> is <c>x</c> while
/// <c>Combine(x, NaN)</c> is <c>NaN</c> — the aggregate of a range containing a <c>NaN</c> therefore depends on
/// where it sits. Both are the ordinary consequences of ordering IEEE values by <c>&lt;</c>; if you need
/// IEEE-exact minimum semantics, pass a custom <see cref="IMonoid{T}"/> that calls <c>T.Min</c>.
/// </para>
/// </remarks>
public readonly struct MinMonoid<T> : IMonoid<T>
    where T : struct, INumber<T>, IMinMaxValue<T>
{
    /// <summary>Gets the identity, <c>T.MaxValue</c> — no stored value can exceed it.</summary>
    public T Identity => T.MaxValue;

    /// <summary>Returns the smaller of two values.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><paramref name="left"/> when it is strictly smaller; otherwise <paramref name="right"/>.</returns>
    public T Combine(T left, T right) => left < right ? left : right;
}
