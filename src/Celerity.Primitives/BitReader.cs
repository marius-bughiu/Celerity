using System;
using System.Runtime.CompilerServices;

namespace Celerity.Primitives;

/// <summary>
/// A sequential, bounds-safe reader of arbitrary-width bit fields from a caller-owned
/// <see cref="ReadOnlySpan{T}"/> of bytes, with no stream and no allocation. The read counterpart of
/// <see cref="BitWriter"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BitReader"/> decodes fields written by <see cref="BitWriter"/>: it reads a field of a
/// requested bit width from a moving cursor and reassembles the value, least-significant bit first. Read
/// the fields back in the <strong>same order and widths</strong> they were written, and each value is
/// recovered exactly.
/// </para>
/// <para>
/// <strong>Bit order is LSB-first (little-endian bits)</strong>, matching <see cref="BitWriter"/>: bit
/// position <c>p</c> is byte <c>p / 8</c>, bit <c>p % 8</c> from the least-significant end; a field's
/// least-significant bit is at the current position and higher bits follow toward higher positions.
/// </para>
/// <para>
/// Every <c>TryRead</c> is bounds-safe: it returns <see langword="false"/> with a <c>0</c> value and
/// leaves the cursor <strong>unchanged</strong> when fewer than the requested number of bits remain, so
/// a partial field is never consumed. The type is a mutable <c>ref struct</c> cursor — construct it over
/// the source and read fields in sequence; use it as a local, and pass it by <c>ref</c> if a helper
/// method needs to advance the same cursor.
/// </para>
/// </remarks>
public ref struct BitReader
{
    private readonly ReadOnlySpan<byte> _source;
    private int _bitPosition;

    /// <summary>
    /// Initializes a new <see cref="BitReader"/> positioned at bit <c>0</c> of <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The byte buffer to read bit fields from.</param>
    public BitReader(ReadOnlySpan<byte> source)
    {
        _source = source;
        _bitPosition = 0;
    }

    /// <summary>
    /// Gets the number of bits read so far — the current cursor position.
    /// </summary>
    public readonly int BitsRead => _bitPosition;

    /// <summary>
    /// Gets the total number of bits in the source (<c>source.Length * 8</c>).
    /// </summary>
    public readonly long CapacityInBits => (long)_source.Length * 8;

    /// <summary>
    /// Gets the number of bits still available to read (<c>CapacityInBits - BitsRead</c>).
    /// </summary>
    public readonly long BitsRemaining => CapacityInBits - _bitPosition;

    /// <summary>
    /// Reads a single bit at the current position and advances the cursor by one.
    /// </summary>
    /// <param name="value">On success, the bit read (<see langword="true"/> for <c>1</c>); otherwise <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if a bit was available; <see langword="false"/> if the source is
    /// exhausted (the cursor is unchanged).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadBit(out bool value)
    {
        if (TryReadBits(1, out ulong bit))
        {
            value = bit != 0;
            return true;
        }

        value = false;
        return false;
    }

    /// <summary>
    /// Reads a <paramref name="bitCount"/>-bit field at the current position (least-significant bit first)
    /// and advances the cursor by <paramref name="bitCount"/>.
    /// </summary>
    /// <param name="bitCount">The number of bits to read, in <c>[0, 64]</c>. A <c>0</c> count yields <c>0</c>.</param>
    /// <param name="value">
    /// On success, the decoded value in its low <paramref name="bitCount"/> bits (higher bits are <c>0</c>);
    /// otherwise <c>0</c>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="bitCount"/> bits were available; <see langword="false"/> if
    /// the source has fewer bits left (the cursor is unchanged).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bitCount"/> is negative or greater than 64.</exception>
    public bool TryReadBits(int bitCount, out ulong value)
    {
        if ((uint)bitCount > 64u)
            throw new ArgumentOutOfRangeException(nameof(bitCount), bitCount, "Bit count must be between 0 and 64 inclusive.");
        if (bitCount == 0)
        {
            value = 0;
            return true;
        }

        long endBit = (long)_bitPosition + bitCount;
        // Not enough bits left, or the resulting position would overflow a 32-bit cursor: refuse without consuming.
        if (endBit > CapacityInBits || endBit > int.MaxValue)
        {
            value = 0;
            return false;
        }

        int byteIndex = _bitPosition >> 3;
        int bitOffset = _bitPosition & 7;
        int remaining = bitCount;
        int shift = 0;
        ulong result = 0;

        while (remaining > 0)
        {
            int take = Math.Min(8 - bitOffset, remaining);           // bits taken from the current byte
            int chunk = (_source[byteIndex] >> bitOffset) & ((1 << take) - 1);
            result |= (ulong)chunk << shift;

            shift += take;
            remaining -= take;
            byteIndex++;
            bitOffset = 0;
        }

        value = result;
        _bitPosition = (int)endBit;
        return true;
    }
}
