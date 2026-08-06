using System.Numerics;

namespace Celerity.Collections;

/// <summary>
/// The bitwise-or monoid: <c>Combine</c> is <c>|</c> and the identity is <see cref="INumberBase{TSelf}.Zero"/>.
/// </summary>
/// <typeparam name="T">The integral element type.</typeparam>
/// <remarks>
/// The mirror of <see cref="BitwiseAndMonoid{T}"/> — "which flags does <i>any</i> entry in this window set?".
/// Non-invertible for the same reason, so <see cref="FenwickTree{T}"/> cannot answer it.
/// </remarks>
public readonly struct BitwiseOrMonoid<T> : IMonoid<T>
    where T : struct, INumberBase<T>, IBitwiseOperators<T, T, T>
{
    /// <summary>Gets the identity, all bits clear — oring with it leaves every bit as it was.</summary>
    public T Identity => T.Zero;

    /// <summary>Ors two values together.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>The bitwise or of the two values.</returns>
    public T Combine(T left, T right) => left | right;
}
