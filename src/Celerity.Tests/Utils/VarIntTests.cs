using System;
using System.Collections.Generic;
using Celerity.Primitives;
using Xunit;

namespace Celerity.Tests.Utils;

/// <summary>
/// Correctness coverage for the span-based <see cref="VarInt"/> codec (issue #193): LEB128 round-trip
/// for <see cref="uint"/> / <see cref="ulong"/>, zig-zag + LEB128 for <see cref="int"/> / <see cref="long"/>,
/// the <c>VarIntLength</c> size helpers, the zig-zag transforms, and the bounds-safe short-buffer /
/// truncated / overflowed / max-length edge cases.
/// </summary>
public class VarIntTests
{
    // ── Representative values per width ──────────────────────────────────────────

    private static IEnumerable<uint> SampleU32(Random rng)
    {
        yield return 0;
        yield return 1;
        yield return 0x7F;          // 1-byte boundary
        yield return 0x80;          // 2-byte boundary
        yield return 0x3FFF;        // 2-byte boundary
        yield return 0x4000;        // 3-byte boundary
        yield return 0x1F_FFFF;     // 3-byte boundary
        yield return 0x20_0000;     // 4-byte boundary
        yield return 0x0FFF_FFFF;   // 4-byte boundary
        yield return 0x1000_0000;   // 5-byte boundary
        yield return uint.MaxValue;
        for (int i = 0; i < 5000; i++)
            yield return (uint)rng.NextInt64(0, 1L << 32);
    }

    private static IEnumerable<ulong> SampleU64(Random rng)
    {
        yield return 0;
        yield return 1;
        yield return 0x7F;
        yield return 0x80;
        yield return 0x3FFF;
        yield return 0x4000;
        yield return uint.MaxValue;
        yield return 1UL << 35;
        yield return (1UL << 56) - 1;   // 8-byte boundary
        yield return 1UL << 56;          // 9-byte boundary
        yield return (1UL << 63) - 1;
        yield return 1UL << 63;          // 10-byte boundary
        yield return ulong.MaxValue;
        for (int i = 0; i < 5000; i++)
            yield return NextULong(rng);
    }

    private static IEnumerable<int> SampleI32(Random rng)
    {
        yield return 0;
        yield return -1;
        yield return 1;
        yield return -2;
        yield return 2;
        yield return int.MaxValue;
        yield return int.MinValue;
        yield return -64;
        yield return 63;
        for (int i = 0; i < 5000; i++)
            yield return unchecked((int)NextULong(rng));
    }

    private static IEnumerable<long> SampleI64(Random rng)
    {
        yield return 0;
        yield return -1;
        yield return 1;
        yield return long.MaxValue;
        yield return long.MinValue;
        yield return -1_000_000_000_000L;
        yield return 1_000_000_000_000L;
        for (int i = 0; i < 5000; i++)
            yield return unchecked((long)NextULong(rng));
    }

    // ── Round-trip ───────────────────────────────────────────────────────────────

    [Fact]
    public void UInt32_RoundTrips_AndLengthMatchesBytesWritten()
    {
        var rng = new Random(20260608);
        Span<byte> buffer = stackalloc byte[VarInt.MaxVarIntLength32];
        foreach (uint value in SampleU32(rng))
        {
            int expectedLen = VarInt.VarIntLength(value);
            Assert.True(VarInt.TryWriteVarInt(buffer, value, out int written), $"write {value}");
            Assert.Equal(expectedLen, written);
            Assert.True(VarInt.TryReadVarInt(buffer, out uint read, out int consumed), $"read {value}");
            Assert.Equal(value, read);
            Assert.Equal(written, consumed);
        }
    }

    [Fact]
    public void UInt64_RoundTrips_AndLengthMatchesBytesWritten()
    {
        var rng = new Random(20260609);
        Span<byte> buffer = stackalloc byte[VarInt.MaxVarIntLength64];
        foreach (ulong value in SampleU64(rng))
        {
            int expectedLen = VarInt.VarIntLength(value);
            Assert.True(VarInt.TryWriteVarInt(buffer, value, out int written), $"write {value}");
            Assert.Equal(expectedLen, written);
            Assert.True(VarInt.TryReadVarInt(buffer, out ulong read, out int consumed), $"read {value}");
            Assert.Equal(value, read);
            Assert.Equal(written, consumed);
        }
    }

    [Fact]
    public void Int32_ZigZag_RoundTrips()
    {
        var rng = new Random(20260610);
        Span<byte> buffer = stackalloc byte[VarInt.MaxVarIntLength32];
        foreach (int value in SampleI32(rng))
        {
            int expectedLen = VarInt.VarIntLength(value);
            Assert.True(VarInt.TryWriteVarInt(buffer, value, out int written), $"write {value}");
            Assert.Equal(expectedLen, written);
            Assert.True(VarInt.TryReadVarInt(buffer, out int read, out int consumed), $"read {value}");
            Assert.Equal(value, read);
            Assert.Equal(written, consumed);
        }
    }

    [Fact]
    public void Int64_ZigZag_RoundTrips()
    {
        var rng = new Random(20260611);
        Span<byte> buffer = stackalloc byte[VarInt.MaxVarIntLength64];
        foreach (long value in SampleI64(rng))
        {
            int expectedLen = VarInt.VarIntLength(value);
            Assert.True(VarInt.TryWriteVarInt(buffer, value, out int written), $"write {value}");
            Assert.Equal(expectedLen, written);
            Assert.True(VarInt.TryReadVarInt(buffer, out long read, out int consumed), $"read {value}");
            Assert.Equal(value, read);
            Assert.Equal(written, consumed);
        }
    }

    [Fact]
    public void Exhaustive_LowRange_UInt32_RoundTrips()
    {
        // A dense sweep over the first three length classes catches any off-by-one a random sample skips.
        Span<byte> buffer = stackalloc byte[VarInt.MaxVarIntLength32];
        for (uint value = 0; value < 2_000_000; value++)
        {
            Assert.True(VarInt.TryWriteVarInt(buffer, value, out int written));
            Assert.True(VarInt.TryReadVarInt(buffer, out uint read, out int consumed));
            Assert.Equal(value, read);
            Assert.Equal(written, consumed);
        }
    }

    [Fact]
    public void Exhaustive_LowRange_Int32_ZigZag_RoundTrips()
    {
        Span<byte> buffer = stackalloc byte[VarInt.MaxVarIntLength32];
        for (int value = -1_000_000; value < 1_000_000; value++)
        {
            Assert.True(VarInt.TryWriteVarInt(buffer, value, out int written));
            Assert.True(VarInt.TryReadVarInt(buffer, out int read, out int consumed));
            Assert.Equal(value, read);
            Assert.Equal(written, consumed);
        }
    }

    // ── Encoded layout (LEB128 byte values are stable) ───────────────────────────

    [Fact]
    public void EncodedBytes_MatchLeb128Layout()
    {
        Span<byte> buffer = stackalloc byte[VarInt.MaxVarIntLength64];

        Assert.True(VarInt.TryWriteVarInt(buffer, 0u, out int n));
        Assert.Equal(1, n);
        Assert.Equal(0x00, buffer[0]);

        Assert.True(VarInt.TryWriteVarInt(buffer, 127u, out n));
        Assert.Equal(1, n);
        Assert.Equal(0x7F, buffer[0]);

        Assert.True(VarInt.TryWriteVarInt(buffer, 128u, out n));
        Assert.Equal(2, n);
        Assert.Equal(0x80, buffer[0]);
        Assert.Equal(0x01, buffer[1]);

        Assert.True(VarInt.TryWriteVarInt(buffer, 300u, out n));
        Assert.Equal(2, n);
        Assert.Equal(0xAC, buffer[0]);  // 300 = 0b1_0010_1100 → low 7 = 0x2C, cont. set → 0xAC
        Assert.Equal(0x02, buffer[1]);  // high group = 0x02
    }

    // ── Length helper ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0u, 1)]
    [InlineData(1u, 1)]
    [InlineData(0x7Fu, 1)]
    [InlineData(0x80u, 2)]
    [InlineData(0x3FFFu, 2)]
    [InlineData(0x4000u, 3)]
    [InlineData(0x1FFFFFu, 3)]
    [InlineData(0x200000u, 4)]
    [InlineData(0x0FFFFFFFu, 4)]
    [InlineData(0x10000000u, 5)]
    [InlineData(uint.MaxValue, 5)]
    public void VarIntLength_UInt32_IsCorrect(uint value, int expected)
        => Assert.Equal(expected, VarInt.VarIntLength(value));

    [Theory]
    [InlineData(0UL, 1)]
    [InlineData((1UL << 56) - 1, 8)]
    [InlineData(1UL << 56, 9)]
    [InlineData(1UL << 63, 10)]
    [InlineData(ulong.MaxValue, 10)]
    public void VarIntLength_UInt64_IsCorrect(ulong value, int expected)
        => Assert.Equal(expected, VarInt.VarIntLength(value));

    [Fact]
    public void VarIntLength_MatchesActualWrittenBytes_OverRandomSweep()
    {
        var rng = new Random(20260612);
        Span<byte> buffer = stackalloc byte[VarInt.MaxVarIntLength64];
        for (int i = 0; i < 20_000; i++)
        {
            ulong u = NextULong(rng);
            VarInt.TryWriteVarInt(buffer, u, out int written);
            Assert.Equal(written, VarInt.VarIntLength(u));

            long s = unchecked((long)u);
            VarInt.TryWriteVarInt(buffer, s, out int writtenSigned);
            Assert.Equal(writtenSigned, VarInt.VarIntLength(s));
        }
    }

    // ── Zig-zag transform ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0u)]
    [InlineData(-1, 1u)]
    [InlineData(1, 2u)]
    [InlineData(-2, 3u)]
    [InlineData(2, 4u)]
    [InlineData(int.MaxValue, 0xFFFFFFFEu)]
    [InlineData(int.MinValue, 0xFFFFFFFFu)]
    public void ZigZag32_EncodesAndDecodes(int value, uint encoded)
    {
        Assert.Equal(encoded, VarInt.ZigZagEncode(value));
        Assert.Equal(value, VarInt.ZigZagDecode(encoded));
    }

    [Theory]
    [InlineData(0L, 0UL)]
    [InlineData(-1L, 1UL)]
    [InlineData(1L, 2UL)]
    [InlineData(long.MaxValue, 0xFFFFFFFFFFFFFFFEUL)]
    [InlineData(long.MinValue, 0xFFFFFFFFFFFFFFFFUL)]
    public void ZigZag64_EncodesAndDecodes(long value, ulong encoded)
    {
        Assert.Equal(encoded, VarInt.ZigZagEncode(value));
        Assert.Equal(value, VarInt.ZigZagDecode(encoded));
    }

    // ── Bounds safety: short destination buffers ─────────────────────────────────

    [Fact]
    public void TryWrite_ShortBuffer_ReturnsFalse_AndWritesNothingMeaningful()
    {
        // 300u needs 2 bytes; a 1-byte buffer must fail.
        var one = new byte[1];
        Assert.False(VarInt.TryWriteVarInt(one, 300u, out int written));
        Assert.Equal(0, written);

        // ulong.MaxValue needs 10 bytes; 9 must fail.
        var nine = new byte[9];
        Assert.False(VarInt.TryWriteVarInt(nine, ulong.MaxValue, out written));
        Assert.Equal(0, written);

        // Empty destination always fails, even for 0.
        Assert.False(VarInt.TryWriteVarInt(Span<byte>.Empty, 0u, out written));
        Assert.Equal(0, written);
    }

    [Fact]
    public void TryWrite_ExactlyFittingBuffer_Succeeds()
    {
        var two = new byte[2];
        Assert.True(VarInt.TryWriteVarInt(two, 300u, out int written));
        Assert.Equal(2, written);

        var ten = new byte[VarInt.MaxVarIntLength64];
        Assert.True(VarInt.TryWriteVarInt(ten, ulong.MaxValue, out written));
        Assert.Equal(10, written);
    }

    // ── Bounds safety: truncated / malformed source ──────────────────────────────

    [Fact]
    public void TryRead_Truncated_ReturnsFalse()
    {
        // Continuation bit set on the only byte: no terminating byte follows.
        var truncated = new byte[] { 0x80 };
        Assert.False(VarInt.TryReadVarInt(truncated, out uint v, out int read));
        Assert.Equal(0u, v);
        Assert.Equal(0, read);

        Assert.False(VarInt.TryReadVarInt(Span<byte>.Empty, out v, out read));
        Assert.Equal(0, read);
    }

    [Fact]
    public void TryRead_Overflow_UInt32_ReturnsFalse()
    {
        // Six continuation bytes — exceeds the 5-byte 32-bit ceiling.
        var sixBytes = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F };
        Assert.False(VarInt.TryReadVarInt(sixBytes, out uint v, out int read));
        Assert.Equal(0, read);

        // Fifth byte carries bits above bit 31 (> 0x0F): overflows a uint.
        var fifthTooBig = new byte[] { 0x80, 0x80, 0x80, 0x80, 0x10 };
        Assert.False(VarInt.TryReadVarInt(fifthTooBig, out v, out read));
        Assert.Equal(0, read);
    }

    [Fact]
    public void TryRead_Overflow_UInt64_ReturnsFalse()
    {
        // Eleven bytes — exceeds the 10-byte 64-bit ceiling.
        var elevenBytes = new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x01 };
        Assert.False(VarInt.TryReadVarInt(elevenBytes, out ulong v, out int read));
        Assert.Equal(0, read);

        // Tenth byte carries bits above bit 63 (> 0x01): overflows a ulong.
        var tenthTooBig = new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x02 };
        Assert.False(VarInt.TryReadVarInt(tenthTooBig, out v, out read));
        Assert.Equal(0, read);
    }

    [Fact]
    public void TryRead_MaxLength_UInt64_Succeeds()
    {
        // ulong.MaxValue is exactly 10 bytes: nine 0xFF continuation bytes + a final 0x01.
        var maxEncoded = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 };
        Assert.True(VarInt.TryReadVarInt(maxEncoded, out ulong v, out int read));
        Assert.Equal(ulong.MaxValue, v);
        Assert.Equal(10, read);
    }

    // ── Multiple values back-to-back (the real codec workload) ───────────────────

    [Fact]
    public void Sequential_WriteThenRead_AdvancesByBytesConsumed()
    {
        var rng = new Random(20260613);
        var values = new ulong[256];
        for (int i = 0; i < values.Length; i++) values[i] = NextULong(rng);

        var buffer = new byte[values.Length * VarInt.MaxVarIntLength64];

        // Write all values into one contiguous buffer, advancing the cursor.
        int offset = 0;
        foreach (ulong value in values)
        {
            Assert.True(VarInt.TryWriteVarInt(buffer.AsSpan(offset), value, out int written));
            offset += written;
        }
        int totalWritten = offset;

        // Read them all back, advancing by each varint's length.
        offset = 0;
        foreach (ulong expected in values)
        {
            Assert.True(VarInt.TryReadVarInt(buffer.AsSpan(offset), out ulong read, out int consumed));
            Assert.Equal(expected, read);
            offset += consumed;
        }
        Assert.Equal(totalWritten, offset);
    }

    private static ulong NextULong(Random rng)
    {
        Span<byte> b = stackalloc byte[8];
        rng.NextBytes(b);
        return BitConverter.ToUInt64(b);
    }
}
