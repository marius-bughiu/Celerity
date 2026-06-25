using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Primitives;

/// <summary>
/// Provides fast utility methods for common math and system operations.
/// </summary>
public static class FastUtils
{
    /// <summary>
    /// The largest power-of-two capacity a Celerity open-addressed collection can reach:
    /// <c>2^30</c> (1,073,741,824). The next power of two (<c>2^31</c>) overflows a signed
    /// <see cref="int"/>, so this is the hard ceiling on the backing-array size.
    /// <see cref="NextPowerOfTwo"/> caps its result here, and the collections' resize paths
    /// refuse to grow past it via <see cref="DoubleCapacity"/> rather than overflowing.
    /// </summary>
    public const int MaxPowerOfTwoCapacity = 1 << 30;

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
        if (n >= MaxPowerOfTwoCapacity) return MaxPowerOfTwoCapacity; // Prevent overflow

        // Decrement n to ensure that exact powers of two aren't mistakenly doubled
        n--;

        n |= n >> 1;
        n |= n >> 2;
        n |= n >> 4;
        n |= n >> 8;
        n |= n >> 16;
        return n + 1;
    }

    /// <summary>
    /// Doubles a power-of-two collection capacity for a resize, throwing when the table has
    /// already reached <see cref="MaxPowerOfTwoCapacity"/>. The naive <c>capacity * 2</c> at
    /// that point computes <c>2^31</c>, which overflows a signed <see cref="int"/> to a
    /// negative value and corrupts the slot mask (<c>newSize - 1</c>); this method fails fast
    /// with a clear error instead. Mirrors the <c>size &lt;= MaxPowerOfTwoCapacity</c> guard in
    /// the frozen-collection build loops.
    /// </summary>
    /// <param name="currentCapacity">The current power-of-two capacity to double.</param>
    /// <returns><c>currentCapacity * 2</c>.</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="currentCapacity"/> is already at or above <see cref="MaxPowerOfTwoCapacity"/>,
    /// so the collection cannot grow any further without overflowing <see cref="int"/>.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DoubleCapacity(int currentCapacity)
    {
        if (currentCapacity >= MaxPowerOfTwoCapacity)
            throw new InvalidOperationException(
                $"Collection cannot grow beyond its maximum capacity of {MaxPowerOfTwoCapacity} slots.");

        return currentCapacity * 2;
    }

    /// <summary>
    /// Computes the smallest power-of-two hash-table size whose load-factor threshold admits
    /// <paramref name="entryCount"/> entries without triggering a resize — i.e. the smallest
    /// power of two <c>s</c> for which <c>(int)(s * loadFactor) &gt;= entryCount</c>, capped at
    /// <see cref="MaxPowerOfTwoCapacity"/>.
    /// </summary>
    /// <remarks>
    /// This is the sizing primitive behind <c>EnsureCapacity</c> / <c>TrimExcess</c> on the
    /// open-addressed collections: a table resizes once its live count reaches
    /// <c>(int)(size * loadFactor)</c>, so holding <paramref name="entryCount"/> entries without a
    /// rehash requires a table whose truncated threshold is at least that count. Because
    /// <see cref="NextPowerOfTwo"/> alone only guarantees <c>size &gt;= entryCount</c> (not
    /// <c>threshold &gt;= entryCount</c>), this doubles past the rounding shortfall until the
    /// threshold admits the count or the capacity ceiling is hit. For a strictly positive
    /// <paramref name="loadFactor"/> below 1 the returned size always strictly exceeds
    /// <paramref name="entryCount"/>, so the table is guaranteed at least one vacant slot.
    /// </remarks>
    /// <param name="entryCount">
    /// The number of entries the table must hold without resizing. Values <c>&lt;= 0</c> return the
    /// minimum table size of <c>1</c>.
    /// </param>
    /// <param name="loadFactor">
    /// The table's load factor, strictly between 0 and 1. Values outside that range are clamped
    /// into it so the helper never divides by a non-positive or &gt;= 1 factor.
    /// </param>
    /// <returns>
    /// The smallest power-of-two size satisfying the threshold, never exceeding
    /// <see cref="MaxPowerOfTwoCapacity"/> (which it returns when no power-of-two table can hold the
    /// requested count).
    /// </returns>
    public static int MinTableSizeFor(int entryCount, float loadFactor)
    {
        if (entryCount <= 0)
            return 1;
        if (loadFactor <= 0f)
            loadFactor = 0.0001f;
        else if (loadFactor >= 1f)
            loadFactor = 0.9999f;

        int size = NextPowerOfTwo(entryCount);
        while (size < MaxPowerOfTwoCapacity && (int)(size * loadFactor) < entryCount)
            size <<= 1;
        return size;
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

    // ── CountDigits / Log10 (base-10 digit count) ───────────────────────────────────────
    //
    // The number of decimal digits of an integer — what you need to size a buffer before
    // `TryFormat`, align a fixed-width numeric column, or pre-measure log / CSV / JSON output.
    // The BCL has a fast LZCNT-based counter (`System.Buffers.Text.FormattingHelpers.CountDigits`)
    // but it is `internal`; the only public base-10 log is `Math.Log10`, which is floating-point
    // (slow, and prone to off-by-one at exact powers of ten because of rounding). These methods
    // expose an exact, branch-lean integer digit count and its companion integer `Log10`.
    //
    // The 32-bit path uses Lemire's "computing the number of digits of an integer even faster"
    // (2021): `Log2(value)` (one LZCNT) indexes a 32-entry magic table whose value, added to
    // `value` and shifted right by 32, yields the digit count with no branches and no division.
    // The 64-bit path falls back to a short comparison ladder (at most a divide by 10^14 and a
    // 7-way compare), which beats a naive divide-by-10 loop while staying obviously correct.
    //
    // We deliberately do not try to beat `int.ToString` / `TryFormat` itself — those are already
    // optimized — only the digit-count primitive that the BCL keeps internal.

    // Lemire's magic table: indexed by Log2(value) in [0, 31]. For each i, the entry is
    // `(10^d << 32) - 10^d_lo` packed so that `(value + table[i]) >> 32` is the digit count
    // `d` of `value` for every `value` whose high set bit is at position `i`.
    private static readonly long[] DigitCountTable =
    {
        4294967296L,  8589934582L,  8589934582L,  8589934582L,  12884901788L, 12884901788L,
        12884901788L, 17179868184L, 17179868184L, 17179868184L, 21474826480L, 21474826480L,
        21474826480L, 21474826480L, 25769703776L, 25769703776L, 25769703776L, 30063771072L,
        30063771072L, 30063771072L, 34349738368L, 34349738368L, 34349738368L, 34349738368L,
        38554705664L, 38554705664L, 38554705664L, 41949672960L, 41949672960L, 41949672960L,
        42949672960L, 42949672960L,
    };

    /// <summary>
    /// Returns the number of decimal digits in <paramref name="value"/> — i.e.
    /// <c>value.ToString().Length</c> — using a single <c>Log2</c> (LZCNT) and a table lookup,
    /// with no branches and no division.
    /// </summary>
    /// <param name="value">The value to measure.</param>
    /// <returns>The decimal digit count, from <c>1</c> (for <c>0</c>) to <c>10</c> (for <see cref="uint.MaxValue"/>).</returns>
    /// <remarks>
    /// <c>0</c> counts as a single digit (<c>"0"</c> has length 1). Use this to size a buffer before
    /// <c>TryFormat</c> or to align fixed-width numeric columns.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountDigits(uint value)
    {
        // Algorithm: https://lemire.me/blog/2021/06/03/computing-the-number-of-digits-of-an-integer-even-faster.
        // Log2(0) is 0, so table[0] = 2^32 yields a digit count of 1 for value 0.
        long tableValue = DigitCountTable[BitOperations.Log2(value)];
        return (int)((value + tableValue) >> 32);
    }

    /// <summary>
    /// Returns the number of decimal digits in <paramref name="value"/> — i.e.
    /// <c>value.ToString().Length</c> — for 64-bit unsigned values.
    /// </summary>
    /// <param name="value">The value to measure.</param>
    /// <returns>The decimal digit count, from <c>1</c> (for <c>0</c>) to <c>20</c> (for <see cref="ulong.MaxValue"/>).</returns>
    /// <remarks>
    /// <c>0</c> counts as a single digit. The implementation reduces the value to its top decimal
    /// group with at most one division and finishes with a short comparison ladder, so it is exact
    /// across the whole range and far cheaper than a repeated divide-by-ten loop.
    /// </remarks>
    public static int CountDigits(ulong value)
    {
        int digits = 1;
        uint part;
        if (value >= 10_000_000UL)
        {
            if (value >= 100_000_000_000_000UL)
            {
                part = (uint)(value / 100_000_000_000_000UL);
                digits += 14;
            }
            else
            {
                part = (uint)(value / 10_000_000UL);
                digits += 7;
            }
        }
        else
        {
            part = (uint)value;
        }

        if (part >= 10)
        {
            if (part < 100) digits += 1;
            else if (part < 1_000) digits += 2;
            else if (part < 10_000) digits += 3;
            else if (part < 100_000) digits += 4;
            else if (part < 1_000_000) digits += 5;
            else digits += 6;
        }

        return digits;
    }

    /// <summary>
    /// Returns the number of decimal digits in the <strong>magnitude</strong> of <paramref name="value"/>,
    /// excluding any sign.
    /// </summary>
    /// <param name="value">The value whose magnitude is measured.</param>
    /// <returns>The decimal digit count of <c>|value|</c>, from <c>1</c> (for <c>0</c>) to <c>10</c>.</returns>
    /// <remarks>
    /// The sign is <strong>not</strong> counted (so <c>CountDigits(-5)</c> is <c>1</c>, not <c>2</c>). To size a
    /// buffer for the full signed text, add one for the minus sign when negative:
    /// <c>CountDigits(value) + (value &lt; 0 ? 1 : 0)</c>. <see cref="int.MinValue"/> is handled without overflow.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountDigits(int value)
    {
        // Magnitude via unsigned two's-complement negation: ~value + 1 never overflows (int.MinValue maps
        // to its correct 2^31 magnitude), unlike Math.Abs which throws / overflows at int.MinValue.
        uint magnitude = value < 0 ? (uint)(~value) + 1U : (uint)value;
        return CountDigits(magnitude);
    }

    /// <summary>
    /// Returns the number of decimal digits in the <strong>magnitude</strong> of <paramref name="value"/>,
    /// excluding any sign.
    /// </summary>
    /// <param name="value">The value whose magnitude is measured.</param>
    /// <returns>The decimal digit count of <c>|value|</c>, from <c>1</c> (for <c>0</c>) to <c>19</c>.</returns>
    /// <remarks>
    /// The sign is <strong>not</strong> counted. To size a buffer for the full signed text, add one for the
    /// minus sign when negative. <see cref="long.MinValue"/> is handled without overflow.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountDigits(long value)
    {
        ulong magnitude = value < 0 ? (ulong)(~value) + 1UL : (ulong)value;
        return CountDigits(magnitude);
    }

    /// <summary>
    /// Returns the integer base-10 logarithm of <paramref name="value"/> — i.e. <c>floor(log10(value))</c>
    /// — computed exactly via <see cref="CountDigits(uint)"/> with no floating-point.
    /// </summary>
    /// <param name="value">The value to take the log of.</param>
    /// <returns><c>floor(log10(value))</c> for <c>value &gt;= 1</c>; <c>0</c> for <c>value == 0</c>.</returns>
    /// <remarks>
    /// Defined as <c>CountDigits(value) - 1</c>, so it is exact at every power of ten (where the
    /// floating-point <see cref="Math.Log10(double)"/> can round to the wrong side). <c>log10(0)</c> is
    /// mathematically undefined; this method returns <c>0</c> there (treating <c>0</c> as a one-digit value).
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Log10(uint value) => CountDigits(value) - 1;

    /// <summary>
    /// Returns the integer base-10 logarithm of <paramref name="value"/> — i.e. <c>floor(log10(value))</c>
    /// — for 64-bit unsigned values, computed exactly with no floating-point.
    /// </summary>
    /// <param name="value">The value to take the log of.</param>
    /// <returns><c>floor(log10(value))</c> for <c>value &gt;= 1</c>; <c>0</c> for <c>value == 0</c>.</returns>
    /// <remarks>
    /// Defined as <c>CountDigits(value) - 1</c>; exact at every power of ten. <c>log10(0)</c> is
    /// mathematically undefined; this method returns <c>0</c> there.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Log10(ulong value) => CountDigits(value) - 1;

    // ── Alignment helpers (AlignUp / AlignDown / IsAligned) ──────────────────────────────
    //
    // Rounding a size or a (pointer-sized) address to a power-of-two boundary — what you need
    // when sub-allocating from a buffer, padding a struct stride to a SIMD width, computing the
    // start of the cache line / page a pointer sits in, or sizing a backing array in machine
    // words. The arithmetic is a one-liner (`(v + (a - 1)) & ~(a - 1)`), but it is fiddly to get
    // right at the boundary and easy to fumble the mask, so the BCL keeps an internal `Align`
    // helper rather than expose one. These methods expose it publicly for the common widths.
    //
    // The alignment must be a power of two — that is the precondition for the mask trick. It is
    // validated (`BitOperations.IsPow2`) and a non-power-of-two (including 0) throws. `AlignUp`
    // can overflow when `value` is within `alignment - 1` of the type's maximum; that wraps
    // (unsigned) or goes negative (signed), exactly as the underlying `+` would, and is a
    // documented call-site concern, not something these helpers guard.

    /// <summary>
    /// Rounds <paramref name="value"/> up to the nearest multiple of <paramref name="alignment"/>
    /// (a power of two), returning <paramref name="value"/> unchanged when it is already aligned.
    /// </summary>
    /// <param name="value">The non-negative value to align. Typically a byte size or an offset.</param>
    /// <param name="alignment">The alignment boundary. Must be a power of two (<c>1, 2, 4, 8, …</c>).</param>
    /// <returns>The smallest multiple of <paramref name="alignment"/> that is <c>&gt;= value</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="alignment"/> is not a power of two (including <c>0</c>).</exception>
    /// <remarks>
    /// Overflows (wrapping to a value below <paramref name="value"/>) when <paramref name="value"/> is within
    /// <c>alignment - 1</c> of <see cref="int.MaxValue"/> — guard at the call site if that is reachable.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AlignUp(int value, int alignment)
    {
        ThrowIfNotPowerOfTwo(alignment);
        return (value + (alignment - 1)) & ~(alignment - 1);
    }

    /// <summary>
    /// Rounds <paramref name="value"/> down to the nearest multiple of <paramref name="alignment"/>
    /// (a power of two), returning <paramref name="value"/> unchanged when it is already aligned.
    /// </summary>
    /// <param name="value">The non-negative value to align.</param>
    /// <param name="alignment">The alignment boundary. Must be a power of two.</param>
    /// <returns>The largest multiple of <paramref name="alignment"/> that is <c>&lt;= value</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="alignment"/> is not a power of two (including <c>0</c>).</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int AlignDown(int value, int alignment)
    {
        ThrowIfNotPowerOfTwo(alignment);
        return value & ~(alignment - 1);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="value"/> is an exact multiple of
    /// <paramref name="alignment"/> (a power of two).
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <param name="alignment">The alignment boundary. Must be a power of two.</param>
    /// <returns><see langword="true"/> if <c>value % alignment == 0</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="alignment"/> is not a power of two (including <c>0</c>).</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAligned(int value, int alignment)
    {
        ThrowIfNotPowerOfTwo(alignment);
        return (value & (alignment - 1)) == 0;
    }

    /// <inheritdoc cref="AlignUp(int, int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long AlignUp(long value, long alignment)
    {
        ThrowIfNotPowerOfTwo(alignment);
        return (value + (alignment - 1)) & ~(alignment - 1);
    }

    /// <inheritdoc cref="AlignDown(int, int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long AlignDown(long value, long alignment)
    {
        ThrowIfNotPowerOfTwo(alignment);
        return value & ~(alignment - 1);
    }

    /// <inheritdoc cref="IsAligned(int, int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAligned(long value, long alignment)
    {
        ThrowIfNotPowerOfTwo(alignment);
        return (value & (alignment - 1)) == 0;
    }

    /// <summary>
    /// Rounds a pointer-sized <paramref name="value"/> (e.g. an address or a native byte count) up to the
    /// nearest multiple of <paramref name="alignment"/> (a power of two).
    /// </summary>
    /// <param name="value">The pointer-sized value to align — an address or a native-sized length.</param>
    /// <param name="alignment">The alignment boundary. Must be a power of two.</param>
    /// <returns>The smallest multiple of <paramref name="alignment"/> that is <c>&gt;= value</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="alignment"/> is not a power of two (including <c>0</c>).</exception>
    /// <remarks>Use this overload to align raw pointers / addresses; it wraps to <c>0</c> on overflow near <see cref="nuint.MaxValue"/>.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint AlignUp(nuint value, nuint alignment)
    {
        ThrowIfNotPowerOfTwo(alignment);
        return (value + (alignment - 1)) & ~(alignment - 1);
    }

    /// <summary>
    /// Rounds a pointer-sized <paramref name="value"/> down to the nearest multiple of
    /// <paramref name="alignment"/> (a power of two) — e.g. the start of the cache line or page it sits in.
    /// </summary>
    /// <param name="value">The pointer-sized value to align.</param>
    /// <param name="alignment">The alignment boundary. Must be a power of two.</param>
    /// <returns>The largest multiple of <paramref name="alignment"/> that is <c>&lt;= value</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="alignment"/> is not a power of two (including <c>0</c>).</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static nuint AlignDown(nuint value, nuint alignment)
    {
        ThrowIfNotPowerOfTwo(alignment);
        return value & ~(alignment - 1);
    }

    /// <summary>
    /// Returns <see langword="true"/> when a pointer-sized <paramref name="value"/> is an exact multiple of
    /// <paramref name="alignment"/> (a power of two) — i.e. it lies on the boundary.
    /// </summary>
    /// <param name="value">The pointer-sized value to test.</param>
    /// <param name="alignment">The alignment boundary. Must be a power of two.</param>
    /// <returns><see langword="true"/> if <c>value</c> is a multiple of <paramref name="alignment"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="alignment"/> is not a power of two (including <c>0</c>).</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAligned(nuint value, nuint alignment)
    {
        ThrowIfNotPowerOfTwo(alignment);
        return (value & (alignment - 1)) == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ThrowIfNotPowerOfTwo(int alignment)
    {
        // IsPow2 is false for 0 and for negatives, so this also rejects non-positive alignments.
        if (!BitOperations.IsPow2(alignment))
            throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Alignment must be a power of two.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ThrowIfNotPowerOfTwo(long alignment)
    {
        if (!BitOperations.IsPow2(alignment))
            throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Alignment must be a power of two.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ThrowIfNotPowerOfTwo(nuint alignment)
    {
        if (!BitOperations.IsPow2(alignment))
            throw new ArgumentOutOfRangeException(nameof(alignment), "Alignment must be a power of two.");
    }
}
