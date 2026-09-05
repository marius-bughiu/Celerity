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
/// <para>
/// This is the one shipped fold that is <b>not</b> idempotent — <c>a + a</c> is not <c>a</c> — so it
/// deliberately does not implement <see cref="IIdempotentMonoid{T}"/> and cannot fold a
/// <see cref="SparseTable{T, TMonoid}"/>. That table covers a range with two <i>overlapping</i> windows, which
/// would count the overlap twice; the constraint turns what would be a silently inflated sum into a compile
/// error. For prefix and range sums over an immutable sequence, a precomputed prefix array answers in
/// <c>O(1)</c> and is the right tool.
/// </para>
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
