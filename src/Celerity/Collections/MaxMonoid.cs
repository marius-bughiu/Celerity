using System.Numerics;

namespace Celerity.Collections;

/// <summary>
/// The maximum monoid: <c>Combine</c> keeps the larger of two values and the identity is
/// <see cref="IMinMaxValue{TSelf}.MinValue"/>.
/// </summary>
/// <typeparam name="T">The ordered numeric element type.</typeparam>
/// <remarks>
/// <para>
/// Like <see cref="MinMonoid{T}"/>, the domain is every value of an integral <typeparamref name="T"/> but only
/// the <b>finite</b> values of a floating-point one.
/// </para>
/// <para>
/// The mirror of <see cref="MinMonoid{T}"/>, and non-invertible for the same reason, so
/// <see cref="FenwickTree{T}"/> cannot answer it either. The same floating-point caveat applies with the signs
/// reversed: the identity is <c>T.MinValue</c>, the smallest <i>finite</i> value, so a stored <c>-∞</c>
/// aggregates to <c>T.MinValue</c>; and <c>NaN</c> loses every <c>&gt;</c> comparison, so it is discarded from
/// the left operand and kept from the right.
/// </remarks>
public readonly struct MaxMonoid<T> : IMonoid<T>
    where T : struct, INumber<T>, IMinMaxValue<T>
{
    /// <summary>Gets the identity, <c>T.MinValue</c> — no stored value can fall below it.</summary>
    public T Identity => T.MinValue;

    /// <summary>Returns the larger of two values.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><paramref name="left"/> when it is strictly larger; otherwise <paramref name="right"/>.</returns>
    public T Combine(T left, T right) => left > right ? left : right;
}
