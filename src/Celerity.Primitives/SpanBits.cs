using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Primitives;

/// <summary>
/// Span-based bit-packing helpers that operate over <strong>caller-owned</strong> bit storage — a
/// <see cref="Span{T}"/> / <see cref="ReadOnlySpan{T}"/> of <see cref="ulong"/> words — with no allocation
/// and no heap object: get / set / clear / flip a single bit, count the set bits, and scan for the next
/// set bit.
/// </summary>
/// <remarks>
/// <para>
/// Each bit lives in word <c>index / 64</c> at bit position <c>index % 64</c>, least-significant bit first,
/// so a <c>Span&lt;ulong&gt;</c> of length <c>n</c> holds <c>64 · n</c> bits indexed <c>[0, 64·n)</c>. Use
/// <see cref="WordCount(int)"/> to size that span from a bit count (it rounds up to whole 64-bit words).
/// </para>
/// <para>
/// <strong>This is the non-owning counterpart to <c>BitSet</c></strong> (in the <c>Celerity.Collections</c>
/// package). <c>BitSet</c> is a growable, length-tracking collection that <em>owns</em> its
/// backing array; <see cref="SpanBits"/> owns nothing — it is a thin set of static operations over memory you
/// already have (a stack buffer, a slice of a larger array, a pooled / rented buffer, or memory mapped from
/// elsewhere). Reach for <see cref="SpanBits"/> when you are already managing the storage and only need the
/// bit arithmetic; reach for <c>BitSet</c> when you want a self-contained bit
/// vector with length, bulk boolean ops, and enumeration.
/// </para>
/// <para>
/// The single-bit operations index the span directly, so an <c>index</c> outside
/// <c>[0, 64·bits.Length)</c> throws <see cref="IndexOutOfRangeException"/> from the underlying span access —
/// these helpers do not silently mask or grow. <see cref="PopCount(ReadOnlySpan{ulong})"/> and
/// <see cref="NextSetBit(ReadOnlySpan{ulong}, int)"/> use the hardware <c>POPCNT</c> / <c>TZCNT</c> via
/// <see cref="BitOperations"/>.
/// </para>
/// </remarks>
public static class SpanBits
{
    /// <summary>
    /// Returns the number of 64-bit words needed to store <paramref name="bitCount"/> bits — i.e. the length
    /// to allocate for the backing <see cref="Span{T}"/> of <see cref="ulong"/>.
    /// </summary>
    /// <param name="bitCount">The number of bits to store. Must be non-negative.</param>
    /// <returns><c>ceil(bitCount / 64)</c>; <c>0</c> for a <paramref name="bitCount"/> of <c>0</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bitCount"/> is negative.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WordCount(int bitCount)
    {
        if (bitCount < 0)
            throw new ArgumentOutOfRangeException(nameof(bitCount), bitCount, "Bit count must be non-negative.");

        return (int)(((uint)bitCount + 63) >> 6);
    }

    /// <summary>Returns the value of the bit at <paramref name="index"/>.</summary>
    /// <param name="bits">The bit storage.</param>
    /// <param name="index">The zero-based bit index, in <c>[0, 64·bits.Length)</c>.</param>
    /// <returns><see langword="true"/> if the bit is set.</returns>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is outside the span.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Get(ReadOnlySpan<ulong> bits, int index)
        => (bits[index >> 6] & (1UL << (index & 63))) != 0;

    /// <summary>Sets the bit at <paramref name="index"/> to <c>1</c>.</summary>
    /// <param name="bits">The bit storage.</param>
    /// <param name="index">The zero-based bit index, in <c>[0, 64·bits.Length)</c>.</param>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is outside the span.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Set(Span<ulong> bits, int index)
        => bits[index >> 6] |= 1UL << (index & 63);

    /// <summary>Clears the bit at <paramref name="index"/> to <c>0</c>.</summary>
    /// <param name="bits">The bit storage.</param>
    /// <param name="index">The zero-based bit index, in <c>[0, 64·bits.Length)</c>.</param>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is outside the span.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clear(Span<ulong> bits, int index)
        => bits[index >> 6] &= ~(1UL << (index & 63));

    /// <summary>Toggles the bit at <paramref name="index"/> and returns its new value.</summary>
    /// <param name="bits">The bit storage.</param>
    /// <param name="index">The zero-based bit index, in <c>[0, 64·bits.Length)</c>.</param>
    /// <returns>The value of the bit after flipping.</returns>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is outside the span.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Flip(Span<ulong> bits, int index)
    {
        ulong mask = 1UL << (index & 63);
        ref ulong word = ref bits[index >> 6];
        word ^= mask;
        return (word & mask) != 0;
    }

    /// <summary>Returns the total number of set bits across every word of <paramref name="bits"/>.</summary>
    /// <param name="bits">The bit storage.</param>
    /// <returns>The population count (number of <c>1</c> bits), using the hardware <c>POPCNT</c> per word.</returns>
    public static int PopCount(ReadOnlySpan<ulong> bits)
    {
        int count = 0;
        for (int i = 0; i < bits.Length; i++)
            count += BitOperations.PopCount(bits[i]);
        return count;
    }

    /// <summary>
    /// Returns the index of the lowest set bit at or after <paramref name="fromIndex"/>, or <c>-1</c> if no
    /// bit at or after <paramref name="fromIndex"/> is set — a forward scan for the next occupied bit.
    /// </summary>
    /// <param name="bits">The bit storage.</param>
    /// <param name="fromIndex">The bit index to start scanning from (inclusive). Values <c>&lt; 0</c> are treated as <c>0</c>; values at or beyond the end yield <c>-1</c>.</param>
    /// <returns>The index of the next set bit in <c>[fromIndex, 64·bits.Length)</c>, or <c>-1</c> if there is none.</returns>
    /// <remarks>
    /// Iterate set bits by feeding the previous result <c>+ 1</c> back in:
    /// <code>for (int i = SpanBits.NextSetBit(bits, 0); i >= 0; i = SpanBits.NextSetBit(bits, i + 1)) { /* ... */ }</code>
    /// Each step skips whole empty words and uses <c>TZCNT</c> within a word, so the scan is
    /// proportional to the number of words, not the number of bits.
    /// </remarks>
    public static int NextSetBit(ReadOnlySpan<ulong> bits, int fromIndex)
    {
        if (fromIndex < 0)
            fromIndex = 0;

        int word = fromIndex >> 6;
        if (word >= bits.Length)
            return -1;

        // Mask off the bits below fromIndex in the starting word, then scan whole words afterwards.
        ulong current = bits[word] & (~0UL << (fromIndex & 63));
        while (true)
        {
            if (current != 0)
                return (word << 6) + BitOperations.TrailingZeroCount(current);

            if (++word >= bits.Length)
                return -1;

            current = bits[word];
        }
    }
}
