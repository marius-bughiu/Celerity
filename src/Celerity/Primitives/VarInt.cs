using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Primitives;

/// <summary>
/// A span-based variable-length integer (varint) codec: LEB128 for unsigned 32-/64-bit values and
/// zig-zag + LEB128 for signed values, encoding directly over caller-owned <see cref="Span{T}"/> /
/// <see cref="ReadOnlySpan{T}"/> buffers with no streams and no allocation.
/// </summary>
/// <remarks>
/// <para>
/// A varint stores a small magnitude in fewer bytes than its fixed width: each byte carries 7 payload
/// bits in its low bits and a continuation flag (<c>0x80</c>) in its high bit, least-significant group
/// first (little-endian groups, LEB128). It is the wire format Protocol Buffers, the .NET metadata
/// tables, and most custom binary serializers use for length prefixes and field tags.
/// </para>
/// <para>
/// The BCL exposes this only as <c>BinaryWriter.Write7BitEncodedInt</c> / <c>BinaryReader.Read7BitEncodedInt</c>
/// — bound to a <see cref="System.IO.Stream"/> and allocating a writer/reader. There is no public span
/// overload (see runtime issue #24473). <see cref="VarInt"/> fills that gap for no-stream / no-allocation
/// hot paths: custom wire codecs, packet builders, append-only logs, and serializers that own their byte
/// buffer.
/// </para>
/// <para>
/// <strong>Signed values are zig-zag encoded.</strong> Two's-complement makes every negative number
/// occupy the full width (so a naive LEB128 of <c>-1</c> is always 10 bytes); zig-zag maps signed values
/// to unsigned so small magnitudes of either sign stay short: <c>0 → 0, -1 → 1, 1 → 2, -2 → 3, …</c>. The
/// <see cref="int"/> / <see cref="long"/> overloads apply it automatically; the <see cref="uint"/> /
/// <see cref="ulong"/> overloads are plain LEB128. Because the overloads are selected by argument type,
/// an untyped integer literal binds to the <em>signed</em> (zig-zag) overload — pass a <c>u</c>/<c>UL</c>
/// suffix (or an explicit cast) when you want plain unsigned LEB128.
/// </para>
/// <para>
/// All <c>TryWrite</c>/<c>TryRead</c> methods are bounds-safe: they return <see langword="false"/> (and
/// report <c>0</c> bytes) when the destination is too small or the source is truncated or malformed, and
/// never throw or read/write outside the supplied span.
/// </para>
/// </remarks>
public static class VarInt
{
    /// <summary>The maximum number of bytes a 32-bit value (<see cref="uint"/> / zig-zagged <see cref="int"/>) can occupy: <c>5</c>.</summary>
    /// <remarks>Size a stack/scratch buffer with this to guarantee any 32-bit value fits in a single <c>TryWrite</c> call.</remarks>
    public const int MaxVarIntLength32 = 5;

    /// <summary>The maximum number of bytes a 64-bit value (<see cref="ulong"/> / zig-zagged <see cref="long"/>) can occupy: <c>10</c>.</summary>
    /// <remarks>Size a stack/scratch buffer with this to guarantee any 64-bit value fits in a single <c>TryWrite</c> call.</remarks>
    public const int MaxVarIntLength64 = 10;

    // ── Zig-zag transforms ──────────────────────────────────────────────────────
    //
    // Map a signed value to an unsigned one that keeps small magnitudes of either sign short, by
    // interleaving non-negative and negative numbers: 0,-1,1,-2,2,… → 0,1,2,3,4,…. Encoding is
    // (n << 1) ^ (n >> (W-1)): the left shift makes room for the sign in bit 0, and the arithmetic
    // right shift broadcasts the sign across all bits (0 for non-negative, all-ones for negative) so
    // the XOR flips the magnitude bits of negatives. Decoding inverts it branchlessly.

    /// <summary>Zig-zag encodes a signed 32-bit value to the unsigned value LEB128 then stores compactly.</summary>
    /// <param name="value">The signed value.</param>
    /// <returns><c>(value &lt;&lt; 1) ^ (value &gt;&gt; 31)</c> — maps <c>0,-1,1,-2,…</c> to <c>0,1,2,3,…</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ZigZagEncode(int value) => (uint)((value << 1) ^ (value >> 31));

    /// <summary>Decodes a zig-zag-encoded unsigned 32-bit value back to its signed value.</summary>
    /// <param name="value">The zig-zag-encoded value.</param>
    /// <returns>The original signed value; the inverse of <see cref="ZigZagEncode(int)"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ZigZagDecode(uint value) => (int)(value >> 1) ^ -(int)(value & 1);

    /// <summary>Zig-zag encodes a signed 64-bit value to the unsigned value LEB128 then stores compactly.</summary>
    /// <param name="value">The signed value.</param>
    /// <returns><c>(value &lt;&lt; 1) ^ (value &gt;&gt; 63)</c> — maps <c>0,-1,1,-2,…</c> to <c>0,1,2,3,…</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ZigZagEncode(long value) => (ulong)((value << 1) ^ (value >> 63));

    /// <summary>Decodes a zig-zag-encoded unsigned 64-bit value back to its signed value.</summary>
    /// <param name="value">The zig-zag-encoded value.</param>
    /// <returns>The original signed value; the inverse of <see cref="ZigZagEncode(long)"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ZigZagDecode(ulong value) => (long)(value >> 1) ^ -(long)(value & 1);

    // ── Length helpers ──────────────────────────────────────────────────────────

    /// <summary>Returns the number of bytes the LEB128 encoding of <paramref name="value"/> occupies (1–5).</summary>
    /// <param name="value">The unsigned value.</param>
    /// <returns>The encoded length in bytes; <c>1</c> for <c>0</c>, up to <see cref="MaxVarIntLength32"/> for <see cref="uint.MaxValue"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int VarIntLength(uint value)
    {
        // Number of significant bits (>= 1, since `value | 1`), rounded up into 7-bit groups.
        int bits = 32 - BitOperations.LeadingZeroCount(value | 1);
        return (bits + 6) / 7;
    }

    /// <summary>Returns the number of bytes the LEB128 encoding of <paramref name="value"/> occupies (1–10).</summary>
    /// <param name="value">The unsigned value.</param>
    /// <returns>The encoded length in bytes; <c>1</c> for <c>0</c>, up to <see cref="MaxVarIntLength64"/> for <see cref="ulong.MaxValue"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int VarIntLength(ulong value)
    {
        int bits = 64 - BitOperations.LeadingZeroCount(value | 1);
        return (bits + 6) / 7;
    }

    /// <summary>Returns the number of bytes the zig-zag + LEB128 encoding of <paramref name="value"/> occupies (1–5).</summary>
    /// <param name="value">The signed value.</param>
    /// <returns>The encoded length in bytes; small magnitudes of either sign stay short via zig-zag.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int VarIntLength(int value) => VarIntLength(ZigZagEncode(value));

    /// <summary>Returns the number of bytes the zig-zag + LEB128 encoding of <paramref name="value"/> occupies (1–10).</summary>
    /// <param name="value">The signed value.</param>
    /// <returns>The encoded length in bytes; small magnitudes of either sign stay short via zig-zag.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int VarIntLength(long value) => VarIntLength(ZigZagEncode(value));

    // ── Unsigned LEB128 write ───────────────────────────────────────────────────

    /// <summary>
    /// Writes <paramref name="value"/> as an unsigned LEB128 varint into <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">The buffer to write into.</param>
    /// <param name="value">The unsigned value to encode.</param>
    /// <param name="bytesWritten">On success, the number of bytes written (1–5); <c>0</c> on failure.</param>
    /// <returns><see langword="true"/> if the value fit; <see langword="false"/> if <paramref name="destination"/> was too small (nothing is written).</returns>
    public static bool TryWriteVarInt(Span<byte> destination, uint value, out int bytesWritten)
    {
        int i = 0;
        while (value >= 0x80)
        {
            if (i >= destination.Length) { bytesWritten = 0; return false; }
            destination[i++] = (byte)(value | 0x80);
            value >>= 7;
        }
        if (i >= destination.Length) { bytesWritten = 0; return false; }
        destination[i++] = (byte)value;
        bytesWritten = i;
        return true;
    }

    /// <summary>
    /// Writes <paramref name="value"/> as an unsigned LEB128 varint into <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">The buffer to write into.</param>
    /// <param name="value">The unsigned value to encode.</param>
    /// <param name="bytesWritten">On success, the number of bytes written (1–10); <c>0</c> on failure.</param>
    /// <returns><see langword="true"/> if the value fit; <see langword="false"/> if <paramref name="destination"/> was too small (nothing is written).</returns>
    public static bool TryWriteVarInt(Span<byte> destination, ulong value, out int bytesWritten)
    {
        int i = 0;
        while (value >= 0x80)
        {
            if (i >= destination.Length) { bytesWritten = 0; return false; }
            destination[i++] = (byte)(value | 0x80);
            value >>= 7;
        }
        if (i >= destination.Length) { bytesWritten = 0; return false; }
        destination[i++] = (byte)value;
        bytesWritten = i;
        return true;
    }

    /// <summary>
    /// Writes <paramref name="value"/> as a zig-zag + LEB128 varint into <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">The buffer to write into.</param>
    /// <param name="value">The signed value to encode.</param>
    /// <param name="bytesWritten">On success, the number of bytes written (1–5); <c>0</c> on failure.</param>
    /// <returns><see langword="true"/> if the value fit; <see langword="false"/> if <paramref name="destination"/> was too small (nothing is written).</returns>
    public static bool TryWriteVarInt(Span<byte> destination, int value, out int bytesWritten)
        => TryWriteVarInt(destination, ZigZagEncode(value), out bytesWritten);

    /// <summary>
    /// Writes <paramref name="value"/> as a zig-zag + LEB128 varint into <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">The buffer to write into.</param>
    /// <param name="value">The signed value to encode.</param>
    /// <param name="bytesWritten">On success, the number of bytes written (1–10); <c>0</c> on failure.</param>
    /// <returns><see langword="true"/> if the value fit; <see langword="false"/> if <paramref name="destination"/> was too small (nothing is written).</returns>
    public static bool TryWriteVarInt(Span<byte> destination, long value, out int bytesWritten)
        => TryWriteVarInt(destination, ZigZagEncode(value), out bytesWritten);

    // ── Unsigned LEB128 read ────────────────────────────────────────────────────

    /// <summary>
    /// Reads an unsigned LEB128 varint from the start of <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The buffer to read from.</param>
    /// <param name="value">On success, the decoded value; <c>0</c> on failure.</param>
    /// <param name="bytesRead">On success, the number of bytes consumed (1–5); <c>0</c> on failure.</param>
    /// <returns>
    /// <see langword="true"/> if a complete, in-range varint was read; <see langword="false"/> if
    /// <paramref name="source"/> was truncated (continuation bit set with no further byte) or the encoding
    /// overflowed 32 bits (more than five bytes, or a fifth byte with bits set above bit 31).
    /// </returns>
    public static bool TryReadVarInt(ReadOnlySpan<byte> source, out uint value, out int bytesRead)
    {
        uint result = 0;
        int shift = 0;
        int limit = Math.Min(source.Length, MaxVarIntLength32);
        for (int i = 0; i < limit; i++)
        {
            byte b = source[i];
            // The fifth byte (shift == 28) holds only the top 4 bits of a 32-bit value; anything above
            // would overflow, so a larger byte marks a malformed encoding.
            if (shift == 28 && b > 0x0F) break;
            result |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                value = result;
                bytesRead = i + 1;
                return true;
            }
            shift += 7;
        }
        value = 0;
        bytesRead = 0;
        return false;
    }

    /// <summary>
    /// Reads an unsigned LEB128 varint from the start of <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The buffer to read from.</param>
    /// <param name="value">On success, the decoded value; <c>0</c> on failure.</param>
    /// <param name="bytesRead">On success, the number of bytes consumed (1–10); <c>0</c> on failure.</param>
    /// <returns>
    /// <see langword="true"/> if a complete, in-range varint was read; <see langword="false"/> if
    /// <paramref name="source"/> was truncated (continuation bit set with no further byte) or the encoding
    /// overflowed 64 bits (more than ten bytes, or a tenth byte with bits set above bit 63).
    /// </returns>
    public static bool TryReadVarInt(ReadOnlySpan<byte> source, out ulong value, out int bytesRead)
    {
        ulong result = 0;
        int shift = 0;
        int limit = Math.Min(source.Length, MaxVarIntLength64);
        for (int i = 0; i < limit; i++)
        {
            byte b = source[i];
            // The tenth byte (shift == 63) holds only the top bit of a 64-bit value; anything above
            // would overflow, so a larger byte marks a malformed encoding.
            if (shift == 63 && b > 0x01) break;
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                value = result;
                bytesRead = i + 1;
                return true;
            }
            shift += 7;
        }
        value = 0;
        bytesRead = 0;
        return false;
    }

    /// <summary>
    /// Reads a zig-zag + LEB128 varint from the start of <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The buffer to read from.</param>
    /// <param name="value">On success, the decoded signed value; <c>0</c> on failure.</param>
    /// <param name="bytesRead">On success, the number of bytes consumed (1–5); <c>0</c> on failure.</param>
    /// <returns><see langword="true"/> if a complete, in-range varint was read; <see langword="false"/> otherwise (see the unsigned overload).</returns>
    public static bool TryReadVarInt(ReadOnlySpan<byte> source, out int value, out int bytesRead)
    {
        if (TryReadVarInt(source, out uint encoded, out bytesRead))
        {
            value = ZigZagDecode(encoded);
            return true;
        }
        value = 0;
        return false;
    }

    /// <summary>
    /// Reads a zig-zag + LEB128 varint from the start of <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The buffer to read from.</param>
    /// <param name="value">On success, the decoded signed value; <c>0</c> on failure.</param>
    /// <param name="bytesRead">On success, the number of bytes consumed (1–10); <c>0</c> on failure.</param>
    /// <returns><see langword="true"/> if a complete, in-range varint was read; <see langword="false"/> otherwise (see the unsigned overload).</returns>
    public static bool TryReadVarInt(ReadOnlySpan<byte> source, out long value, out int bytesRead)
    {
        if (TryReadVarInt(source, out ulong encoded, out bytesRead))
        {
            value = ZigZagDecode(encoded);
            return true;
        }
        value = 0;
        return false;
    }
}
