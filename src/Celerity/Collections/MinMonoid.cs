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
/// <b>Domain.</b> For an integral <typeparamref name="T"/> the monoid laws hold over every value. For a
/// floating-point one the domain is the <b>finite</b> values only: the identity law fails at <c>±∞</c> and
/// <c>NaN</c>, as described next, and <see cref="IMonoid{T}"/> permits a declared domain for exactly this case.
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
/// <para>
/// <b>Idempotent</b>, so it implements <see cref="IIdempotentMonoid{T}"/> and can also fold a
/// <see cref="SparseTable{T, TMonoid}"/>: <c>Combine(a, a)</c> is <c>a</c>, so the two overlapping windows a
/// sparse table combines may double-count the overlap without changing the answer. Range minimum over a
/// sequence that is <i>immutable</i> after build is that type's headline workload, as this is the segment
/// tree's.
/// </para>
/// </remarks>
public readonly struct MinMonoid<T> : IIdempotentMonoid<T>
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
