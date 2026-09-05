using System.Numerics;

namespace Celerity.Collections;

/// <summary>
/// The bitwise-and monoid: <c>Combine</c> is <c>&amp;</c> and the identity is the all-ones pattern
/// (<c>~T.Zero</c>).
/// </summary>
/// <typeparam name="T">The integral element type.</typeparam>
/// <remarks>
/// The range fold behind "which capabilities does <i>every</i> entry in this window still hold?" — permission
/// masks, feature flags, and any other intersection of bit sets kept in a mutable sequence. Non-invertible
/// (clearing a bit cannot be undone from the aggregate alone), so <see cref="FenwickTree{T}"/> cannot answer
/// it.
/// <para>
/// <b>Idempotent</b> — <c>a &amp; a</c> is <c>a</c> — so it implements <see cref="IIdempotentMonoid{T}"/> and
/// can fold a <see cref="SparseTable{T, TMonoid}"/> when the window sequence is immutable after build.
/// </para>
/// </remarks>
public readonly struct BitwiseAndMonoid<T> : IIdempotentMonoid<T>
    where T : struct, INumberBase<T>, IBitwiseOperators<T, T, T>
{
    /// <summary>Gets the identity, the all-ones pattern — anding with it leaves every bit as it was.</summary>
    public T Identity => ~T.Zero;

    /// <summary>Ands two values together.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>The bitwise and of the two values.</returns>
    public T Combine(T left, T right) => left & right;
}
