using System;
using System.Runtime.CompilerServices;

namespace Celerity;

/// <summary>
/// Provides fast utility methods for common math and system operations.
/// </summary>
public static class FastUtils
{
    /// <summary>
    /// Returns the smallest power of two greater than or equal to the specified number.
    /// If <paramref name="n"/> is 0 or negative, 1 is returned.
    /// If <paramref name="n"/> is very large, the method caps the result to avoid overflow.
    /// </summary>
    /// <param name="n">The integer to find the next power of two for.</param>
    /// <returns>The smallest power of two greater than or equal to <paramref name="n"/>.</returns>
    public static int NextPowerOfTwo(int n)
    {
        if (n <= 0) return 1;
        if (n >= (1 << 30)) return 1 << 30; // Prevent overflow

        // Decrement n to ensure that exact powers of two aren't mistakenly doubled
        n--;

        n |= n >> 1;
        n |= n >> 2;
        n |= n >> 4;
        n |= n >> 8;
        n |= n >> 16;
        return n + 1;
    }

    // ── FastMod / FastDiv (Lemire reciprocal modulo & division) ─────────────────────────
    //
    // Daniel Lemire's "Faster Remainder by Direct Computation" (Lemire, Kaser, Kurz, 2019):
    // for a divisor `d` fixed at run time, precompute a reciprocal once and replace each
    // `value % d` / `value / d` with a widening multiply and a shift. On a 2019-era x64 a
    // hardware `DIV` is ~20–40 cycles and does not pipeline; the multiply-based form is a
    // couple of `mul`/`mulx` and runs 2–4× faster when the same divisor is reused millions
    // of times (hash buckets, ring buffers, sharding, rate limiting, time-wheel timers).
    //
    // The BCL has exactly this (`System.Collections.HashHelpers.GetFastModMultiplier` /
    // `FastMod`) but it is `internal`. These methods expose the same technique publicly,
    // generalized to a `FastDiv` companion and a 64-bit (`ulong`) variant.
    //
    // For a 32-bit divisor the reciprocal is c = ceil(2^64 / d), held in a `ulong`; for a
    // 64-bit divisor it is c = ceil(2^128 / d), held in a `UInt128`. The remainder is the
    // high word of `((c * value mod 2^W) * d)` and the quotient is the high word of
    // `(c * value)`, where W is 64 or 128. The quotient (`FastDiv`) is exact for all
    // `value` only when the shift width is at least the operand width plus ceil(log2 d);
    // with W = 2 × operand width that holds for every `d >= 2` (see the per-method remarks).

    /// <summary>
    /// Precomputes the Lemire reciprocal multiplier for a 32-bit <paramref name="divisor"/>, for reuse
    /// with <see cref="FastMod(uint, uint, ulong)"/> and <see cref="FastDiv(uint, ulong)"/>.
    /// </summary>
    /// <param name="divisor">The divisor the multiplier is computed for. Must be non-zero.</param>
    /// <returns>The 64-bit reciprocal multiplier <c>ceil(2^64 / divisor)</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="divisor"/> is <c>0</c>.</exception>
    /// <remarks>
    /// Compute this once per divisor and pass the result to every <see cref="FastMod(uint, uint, ulong)"/>
    /// / <see cref="FastDiv(uint, ulong)"/> call — recomputing it per operation throws away the win.
    /// For <c>divisor == 1</c> the multiplier overflows to <c>0</c>: that is still correct for
    /// <see cref="FastMod(uint, uint, ulong)"/> (every value mod 1 is 0) but <strong>not</strong> for
    /// <see cref="FastDiv(uint, ulong)"/>, which requires <c>divisor &gt;= 2</c>.
    /// </remarks>
    public static ulong GetFastModMultiplier(uint divisor)
    {
        if (divisor == 0)
            throw new ArgumentOutOfRangeException(nameof(divisor), "Divisor must be non-zero.");

        return ulong.MaxValue / divisor + 1;
    }

    /// <summary>
    /// Computes <c>value % divisor</c> using a precomputed Lemire reciprocal multiplier, faster than the
    /// hardware <c>%</c> operator when the same divisor is reused many times.
    /// </summary>
    /// <param name="value">The dividend.</param>
    /// <param name="divisor">The divisor — must be the same value passed to <see cref="GetFastModMultiplier(uint)"/>.</param>
    /// <param name="multiplier">The reciprocal from <see cref="GetFastModMultiplier(uint)"/> for <paramref name="divisor"/>.</param>
    /// <returns>The remainder <c>value % divisor</c>, identical to the built-in operator for every 32-bit input.</returns>
    /// <remarks>
    /// Correct for every <c>value</c> in <c>[0, 2^32)</c> and every <c>divisor</c> in <c>[1, 2^32)</c>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint FastMod(uint value, uint divisor, ulong multiplier)
    {
        // lowbits = (c * value) mod 2^64; the remainder is the high 64 bits of lowbits * divisor.
        ulong lowbits = multiplier * value;
        ulong high = Math.BigMul(lowbits, divisor, out _);
        return (uint)high;
    }

    /// <summary>
    /// Computes <c>value / divisor</c> (integer quotient) using a precomputed Lemire reciprocal multiplier,
    /// faster than the hardware <c>/</c> operator when the same divisor is reused many times.
    /// </summary>
    /// <param name="value">The dividend.</param>
    /// <param name="multiplier">The reciprocal from <see cref="GetFastModMultiplier(uint)"/> for the divisor.</param>
    /// <returns>The quotient <c>value / divisor</c>, identical to the built-in operator for every 32-bit input.</returns>
    /// <remarks>
    /// The quotient is the high 64 bits of <c>multiplier * value</c>. Correct for every <c>value</c> in
    /// <c>[0, 2^32)</c> provided the multiplier was produced for a <c>divisor &gt;= 2</c>; a multiplier
    /// for <c>divisor == 1</c> (which overflows to <c>0</c>) yields <c>0</c> rather than <paramref name="value"/>,
    /// so guard or special-case <c>divisor == 1</c> at the call site.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint FastDiv(uint value, ulong multiplier)
    {
        // quotient = (c * value) >> 64 = high 64 bits of the 128-bit product.
        return (uint)Math.BigMul(multiplier, value, out _);
    }

    /// <summary>
    /// Precomputes the Lemire reciprocal multiplier for a 64-bit <paramref name="divisor"/>, for reuse
    /// with <see cref="FastMod(ulong, ulong, UInt128)"/> and <see cref="FastDiv(ulong, UInt128)"/>.
    /// </summary>
    /// <param name="divisor">The divisor the multiplier is computed for. Must be non-zero.</param>
    /// <returns>The 128-bit reciprocal multiplier <c>ceil(2^128 / divisor)</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="divisor"/> is <c>0</c>.</exception>
    /// <remarks>
    /// Compute this once per divisor and pass the result to every <see cref="FastMod(ulong, ulong, UInt128)"/>
    /// / <see cref="FastDiv(ulong, UInt128)"/> call. For <c>divisor == 1</c> the multiplier overflows to
    /// <c>0</c>: still correct for <see cref="FastMod(ulong, ulong, UInt128)"/> but <strong>not</strong> for
    /// <see cref="FastDiv(ulong, UInt128)"/>, which requires <c>divisor &gt;= 2</c>.
    /// </remarks>
    public static UInt128 GetFastModMultiplier(ulong divisor)
    {
        if (divisor == 0)
            throw new ArgumentOutOfRangeException(nameof(divisor), "Divisor must be non-zero.");

        return UInt128.MaxValue / divisor + 1;
    }

    /// <summary>
    /// Computes <c>value % divisor</c> for 64-bit operands using a precomputed Lemire reciprocal multiplier,
    /// faster than the hardware <c>%</c> operator when the same divisor is reused many times.
    /// </summary>
    /// <param name="value">The dividend.</param>
    /// <param name="divisor">The divisor — must be the same value passed to <see cref="GetFastModMultiplier(ulong)"/>.</param>
    /// <param name="multiplier">The reciprocal from <see cref="GetFastModMultiplier(ulong)"/> for <paramref name="divisor"/>.</param>
    /// <returns>The remainder <c>value % divisor</c>, identical to the built-in operator for every 64-bit input.</returns>
    /// <remarks>
    /// Correct for every <c>value</c> in <c>[0, 2^64)</c> and every <c>divisor</c> in <c>[1, 2^64)</c>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong FastMod(ulong value, ulong divisor, UInt128 multiplier)
    {
        // lowbits = (c * value) mod 2^128; the remainder is the high 64 bits of lowbits * divisor.
        UInt128 lowbits = multiplier * value;
        return MultiplyHigh(lowbits, divisor);
    }

    /// <summary>
    /// Computes <c>value / divisor</c> (integer quotient) for 64-bit operands using a precomputed Lemire
    /// reciprocal multiplier, faster than the hardware <c>/</c> operator when the same divisor is reused many times.
    /// </summary>
    /// <param name="value">The dividend.</param>
    /// <param name="multiplier">The reciprocal from <see cref="GetFastModMultiplier(ulong)"/> for the divisor.</param>
    /// <returns>The quotient <c>value / divisor</c>, identical to the built-in operator for every 64-bit input.</returns>
    /// <remarks>
    /// The quotient is the high 64 bits of <c>multiplier * value</c>. Correct for every <c>value</c> in
    /// <c>[0, 2^64)</c> provided the multiplier was produced for a <c>divisor &gt;= 2</c>; a multiplier for
    /// <c>divisor == 1</c> (which overflows to <c>0</c>) yields <c>0</c> rather than <paramref name="value"/>,
    /// so guard or special-case <c>divisor == 1</c> at the call site.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong FastDiv(ulong value, UInt128 multiplier)
    {
        // quotient = (c * value) >> 128 = high 64 bits of the 192-bit product.
        return MultiplyHigh(multiplier, value);
    }

    /// <summary>
    /// Returns the high 64 bits of the (up to 192-bit) product of a 128-bit and a 64-bit value, i.e.
    /// <c>(a * b) &gt;&gt; 128</c>, without ever materializing the full product in a 128-bit container.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong MultiplyHigh(UInt128 a, ulong b)
    {
        ulong aLow = (ulong)a;
        ulong aHigh = (ulong)(a >> 64);

        // a * b = aLow*b + (aHigh*b << 64). Both partial products are 128-bit; align them at bit 64
        // (so the sum represents (a*b) >> 64) and shift a further 64 bits to obtain (a*b) >> 128.
        UInt128 low = (UInt128)aLow * b;          // contributes bits [0, 128)
        UInt128 high = (UInt128)aHigh * b;         // contributes bits [64, 192)
        UInt128 mid = (low >> 64) + high;          // value of (a*b) >> 64, bits [64, 192)
        return (ulong)(mid >> 64);                 // (a*b) >> 128
    }
}
