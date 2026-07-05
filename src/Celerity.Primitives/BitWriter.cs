using System;
using System.Runtime.CompilerServices;

namespace Celerity.Primitives;

/// <summary>
/// A sequential, bounds-safe writer of arbitrary-width bit fields into a caller-owned
/// <see cref="Span{T}"/> of bytes, with no stream and no allocation. Pairs with
/// <see cref="BitReader"/> for the read side.
/// </summary>
/// <remarks>
/// <para>
/// A bit writer packs values whose widths are <em>not</em> whole bytes — a 3-bit flag group, a 12-bit
/// sample, a 20-bit offset — end to end into a byte buffer, so a record of odd-width fields occupies
/// exactly <c>ceil(total_bits / 8)</c> bytes rather than one byte per field. It is the sequential,
/// sub-byte counterpart to <see cref="VarInt"/> (byte-granular variable-length integers) and to
/// <see cref="SpanBits"/> (random-access get/set of individual bits over a <c>Span&lt;ulong&gt;</c>):
/// where <see cref="SpanBits"/> addresses one bit at a fixed index, <see cref="BitWriter"/> appends
/// whole multi-bit fields at a moving cursor.
/// </para>
/// <para>
/// The BCL has no span-based bit writer: <see cref="System.Collections.BitArray"/> is a heap object
/// that sets one bit at a time and cannot append a multi-bit field, and
/// <see cref="System.Buffers.Binary.BinaryPrimitives"/> is byte-granular. <see cref="BitWriter"/> fills
/// that gap for bit-packed formats — custom wire protocols, compression bitstreams, packed columnar
/// encodings, and fixed-width-field records — with a single multi-bit append per field and no
/// per-field allocation.
/// </para>
/// <para>
/// <strong>Bit order is LSB-first (little-endian bits).</strong> Bit position <c>p</c> is byte
/// <c>p / 8</c>, bit <c>p % 8</c> counting from the least-significant bit of the byte; a field's
/// least-significant bit occupies the current position and higher bits follow toward higher positions,
/// spilling into the next byte as needed. This is the same convention DEFLATE and most bit-packed
/// codecs use, and it round-trips exactly with <see cref="BitReader"/> reading the fields back in the
/// same order and widths.
/// </para>
/// <para>
/// Every <c>TryWrite</c> is bounds-safe: it returns <see langword="false"/> and leaves the cursor and
/// buffer <strong>unchanged</strong> when the field would not fit in the remaining space, so a partial
/// field is never written. Each field overwrites exactly the bits it occupies (it clears them first),
/// so the buffer need not be pre-zeroed and a field can be rewritten. The type is a mutable
/// <c>ref struct</c> cursor — construct it over the destination and write fields in sequence; use it as
/// a local, and pass it by <c>ref</c> if a helper method needs to advance the same cursor.
/// </para>
/// </remarks>
public ref struct BitWriter
{
    private readonly Span<byte> _destination;
    private int _bitPosition;

    /// <summary>
    /// Initializes a new <see cref="BitWriter"/> positioned at bit <c>0</c> of
    /// <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">
    /// The byte buffer to pack bits into. Only the bits actually written are modified (each field
    /// overwrites its own bits), so the buffer need not be zeroed first.
    /// </param>
    public BitWriter(Span<byte> destination)
    {
        _destination = destination;
        _bitPosition = 0;
    }

    /// <summary>
    /// Gets the number of bits written so far — the current cursor position.
    /// </summary>
    public readonly int BitsWritten => _bitPosition;

    /// <summary>
    /// Gets the number of whole bytes the written bits occupy, <c>ceil(BitsWritten / 8)</c> — the length
    /// of the meaningful prefix of the destination.
    /// </summary>
    public readonly int BytesWritten => (int)(((uint)_bitPosition + 7) >> 3);

    /// <summary>
    /// Gets the total capacity of the destination in bits (<c>destination.Length * 8</c>).
    /// </summary>
    public readonly long CapacityInBits => (long)_destination.Length * 8;

    /// <summary>
    /// Gets the number of bits still available before the destination is full
    /// (<c>CapacityInBits - BitsWritten</c>).
    /// </summary>
    public readonly long BitsRemaining => CapacityInBits - _bitPosition;

    /// <summary>
    /// Returns the number of bytes needed to hold <paramref name="bitCount"/> bits — i.e.
    /// <c>ceil(bitCount / 8)</c> — for sizing a destination buffer before writing.
    /// </summary>
    /// <param name="bitCount">The number of bits to store. Must be non-negative.</param>
    /// <returns><c>ceil(bitCount / 8)</c>; <c>0</c> for a <paramref name="bitCount"/> of <c>0</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bitCount"/> is negative.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ByteCount(int bitCount)
    {
        if (bitCount < 0)
            throw new ArgumentOutOfRangeException(nameof(bitCount), bitCount, "Bit count must be non-negative.");

        return (int)(((uint)bitCount + 7) >> 3);
    }

    /// <summary>
    /// Writes a single bit (<c>1</c> if <paramref name="value"/> is <see langword="true"/>, else <c>0</c>)
    /// at the current position and advances the cursor by one.
    /// </summary>
    /// <param name="value">The bit to write.</param>
    /// <returns><see langword="true"/> if the bit fit; <see langword="false"/> if the destination is full
    /// (nothing is written and the cursor is unchanged).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryWriteBit(bool value) => TryWriteBits(value ? 1UL : 0UL, 1);

    /// <summary>
    /// Writes the low <paramref name="bitCount"/> bits of <paramref name="value"/> at the current position
    /// (least-significant bit first) and advances the cursor by <paramref name="bitCount"/>.
    /// </summary>
    /// <param name="value">
    /// The value to write. Only its low <paramref name="bitCount"/> bits are stored; any higher bits are
    /// ignored, so an out-of-range value never corrupts a following field.
    /// </param>
    /// <param name="bitCount">The number of bits to write, in <c>[0, 64]</c>. A <c>0</c> count is a no-op.</param>
    /// <returns>
    /// <see langword="true"/> if the field fit; <see langword="false"/> if fewer than
    /// <paramref name="bitCount"/> bits remain (nothing is written and the cursor is unchanged).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bitCount"/> is negative or greater than 64.</exception>
    public bool TryWriteBits(ulong value, int bitCount)
    {
        if ((uint)bitCount > 64u)
            throw new ArgumentOutOfRangeException(nameof(bitCount), bitCount, "Bit count must be between 0 and 64 inclusive.");
        if (bitCount == 0)
            return true;

        long endBit = (long)_bitPosition + bitCount;
        // Not enough room, or the resulting position would overflow a 32-bit cursor: refuse without mutating.
        if (endBit > CapacityInBits || endBit > int.MaxValue)
            return false;

        int byteIndex = _bitPosition >> 3;
        int bitOffset = _bitPosition & 7;
        int remaining = bitCount;
        int shift = 0;

        while (remaining > 0)
        {
            int take = Math.Min(8 - bitOffset, remaining);           // bits placed into the current byte
            int regionMask = ((1 << take) - 1) << bitOffset;         // the bits of this byte the field occupies
            int chunk = (int)((value >> shift) & (ulong)((1 << take) - 1));

            // Clear the region then deposit the chunk, so a dirty buffer (or a rewrite) stays correct.
            _destination[byteIndex] = (byte)((_destination[byteIndex] & ~regionMask) | (chunk << bitOffset));

            shift += take;
            remaining -= take;
            byteIndex++;
            bitOffset = 0;
        }

        _bitPosition = (int)endBit;
        return true;
    }
}
