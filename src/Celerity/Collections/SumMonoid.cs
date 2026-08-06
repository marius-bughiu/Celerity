using System.Numerics;

namespace Celerity.Collections;

/// <summary>
/// The additive monoid: <c>Combine</c> is <c>+</c> and the identity is <see cref="INumberBase{TSelf}.Zero"/>.
/// </summary>
/// <typeparam name="T">The numeric element type.</typeparam>
/// <remarks>
/// A <see cref="SegmentTree{T, TMonoid}"/> over this monoid answers range <b>sums</b>, which
/// <see cref="FenwickTree{T}"/> also does — in half the memory and with a shorter constant, because addition
/// has an inverse and a Fenwick tree exploits that. Prefer <see cref="FenwickTree{T}"/> for sums; this monoid
/// exists so the segment tree can be differentially tested against it, and for the case where a single tree
/// has to be switchable between a sum fold and a non-invertible one.
/// </remarks>
public readonly struct SumMonoid<T> : IMonoid<T>
    where T : struct, INumberBase<T>
{
    /// <summary>Gets the additive identity, <c>zero</c>.</summary>
    public T Identity => T.Zero;

    /// <summary>Adds two values.</summary>
    /// <param name="left">The first addend.</param>
    /// <param name="right">The second addend.</param>
    /// <returns>The sum of the two values.</returns>
    public T Combine(T left, T right) => left + right;
}
