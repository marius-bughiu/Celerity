using System;
using Celerity.Primitives;

namespace Celerity.Tests.Utils;

/// <summary>
/// Pins the two <see cref="VarInt"/> failure arms that the main <c>VarIntTests</c> suite only reaches from the
/// outside: a <c>TryWriteVarInt</c> destination that runs out <em>mid-encode</em>, and the failure arm of the
/// zig-zag <c>TryReadVarInt</c> wrappers.
/// </summary>
/// <remarks>
/// <para>
/// Both <c>TryWriteVarInt</c> overloads size the encoding with <see cref="VarInt.VarIntLength(uint)"/> before
/// writing, so a short destination fails before a single byte is stored. The existing suite only uses buffers
/// one byte short of a complete encoding; these tests also supply destinations that would run out while
/// continuation bytes are still pending (an empty span, or a buffer shorter than the value's continuation
/// prefix). The contract they pin is the one the type documents: return <see langword="false"/>, report
/// <c>0</c> bytes written, and write nothing — the destination is left exactly as the caller passed it, so a
/// caller that retries into a larger buffer never sees a stale partial prefix.
/// </para>
/// <para>
/// Symmetrically, the signed <c>TryReadVarInt</c> overloads are thin zig-zag wrappers over the unsigned
/// decoders. Their success arm is covered by the round-trip tests, but their failure arm has to clear the
/// caller's <c>value</c> itself (the inner decoder only owns <c>bytesRead</c>). These tests feed truncated,
/// empty, and overflowing encodings through the <em>signed</em> entry points and assert the wrapper leaves
/// <c>value</c> at <c>0</c> rather than at whatever the caller happened to have there — the property a decoder
/// loop relies on when it ignores the return value's sign and only checks the boolean.
/// </para>
/// </remarks>
public class VarIntBoundaryCoverageTests
{
    // ── TryWriteVarInt: destination too short to hold the continuation prefix ────────────────────────────

    [Fact]
    public void TryWriteVarInt_ShouldReturnFalseAndReportZeroBytes_WhenTheUInt32DestinationIsEmpty()
    {
        // 300u needs two bytes, so the very first continuation byte has nowhere to go.
        Assert.False(VarInt.TryWriteVarInt(Span<byte>.Empty, 300u, out int written));
        Assert.Equal(0, written);
    }

    [Fact]
    public void TryWriteVarInt_ShouldReturnFalseAndReportZeroBytes_WhenTheUInt32DestinationRunsOutMidEncode()
    {
        // 0x4000u encodes as three bytes (0x80 0x80 0x01), so a one-byte destination cannot even hold the
        // continuation prefix.
        var destination = new byte[1];
        Assert.False(VarInt.TryWriteVarInt(destination, 0x4000u, out int written));
        Assert.Equal(0, written);

        // A two-byte buffer is still one short (the terminating byte has no room).
        var twoBytes = new byte[2];
        Assert.False(VarInt.TryWriteVarInt(twoBytes, 0x4000u, out written));
        Assert.Equal(0, written);

        // Three bytes is the exact fit, so the same value now succeeds — proving the failures above were
        // capacity, not a malformed value.
        var threeBytes = new byte[3];
        Assert.True(VarInt.TryWriteVarInt(threeBytes, 0x4000u, out written));
        Assert.Equal(3, written);
        Assert.Equal(new byte[] { 0x80, 0x80, 0x01 }, threeBytes);
    }

    [Fact]
    public void TryWriteVarInt_ShouldReturnFalseAndReportZeroBytes_WhenTheUInt64DestinationIsEmpty()
    {
        Assert.False(VarInt.TryWriteVarInt(Span<byte>.Empty, 0x4000UL, out int written));
        Assert.Equal(0, written);
    }

    [Fact]
    public void TryWriteVarInt_ShouldReturnFalseAndReportZeroBytes_WhenTheUInt64DestinationRunsOutMidEncode()
    {
        // 0x1_0000_0000UL needs five bytes; a two-byte destination is exhausted while the third
        // continuation byte is still pending.
        var destination = new byte[2];
        Assert.False(VarInt.TryWriteVarInt(destination, 0x1_0000_0000UL, out int written));
        Assert.Equal(0, written);

        // The exact-fit buffer succeeds, confirming five bytes was the real requirement.
        var fiveBytes = new byte[5];
        Assert.True(VarInt.TryWriteVarInt(fiveBytes, 0x1_0000_0000UL, out written));
        Assert.Equal(5, written);
    }

    [Fact]
    public void TryWriteVarInt_ShouldReturnFalseAndReportZeroBytes_WhenTheZigZagDestinationRunsOutMidEncode()
    {
        // The signed overloads delegate to the unsigned ones after zig-zagging, so they inherit the same
        // length check. -100_000 zig-zags to 199_999, which needs three bytes.
        Assert.Equal(3, VarInt.VarIntLength(-100_000));
        var oneByte = new byte[1];
        Assert.False(VarInt.TryWriteVarInt(oneByte, -100_000, out int written));
        Assert.Equal(0, written);

        // long.MinValue zig-zags to ulong.MaxValue — the full ten bytes.
        Assert.Equal(VarInt.MaxVarIntLength64, VarInt.VarIntLength(long.MinValue));
        var twoBytes = new byte[2];
        Assert.False(VarInt.TryWriteVarInt(twoBytes, long.MinValue, out written));
        Assert.Equal(0, written);
    }

    [Fact]
    public void TryWriteVarInt_ShouldLeaveTheDestinationUntouched_WhenItIsTooShort()
    {
        // Regression: the write loop used to store each byte before it knew the whole encoding fit, so a
        // failed write left a partial prefix behind despite the documented "nothing is written".
        byte[] uint32 = { 0xEE, 0xEE, 0xEE };
        Assert.False(VarInt.TryWriteVarInt(uint32, uint.MaxValue, out int written));
        Assert.Equal(0, written);
        Assert.Equal(new byte[] { 0xEE, 0xEE, 0xEE }, uint32);

        byte[] uint64 = { 0xEE, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE };
        Assert.False(VarInt.TryWriteVarInt(uint64, ulong.MaxValue, out written));
        Assert.Equal(0, written);
        Assert.All(uint64, b => Assert.Equal(0xEE, b));

        byte[] int32 = { 0xEE, 0xEE };
        Assert.False(VarInt.TryWriteVarInt(int32, int.MinValue, out written));
        Assert.Equal(0, written);
        Assert.Equal(new byte[] { 0xEE, 0xEE }, int32);

        byte[] int64 = { 0xEE, 0xEE, 0xEE, 0xEE };
        Assert.False(VarInt.TryWriteVarInt(int64, long.MinValue, out written));
        Assert.Equal(0, written);
        Assert.All(int64, b => Assert.Equal(0xEE, b));
    }

    // ── TryReadVarInt: the zig-zag wrappers' failure arm clears `value` ──────────────────────────────────

    [Fact]
    public void TryReadVarInt_ShouldClearTheSignedValue_WhenTheInt32SourceIsTruncated()
    {
        // A lone continuation byte promises a follow-up that never arrives.
        int value = -12345;
        Assert.False(VarInt.TryReadVarInt(new byte[] { 0x80 }, out value, out int bytesRead));
        Assert.Equal(0, value);
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public void TryReadVarInt_ShouldClearTheSignedValue_WhenTheInt32SourceIsEmpty()
    {
        int value = int.MinValue;
        Assert.False(VarInt.TryReadVarInt(ReadOnlySpan<byte>.Empty, out value, out int bytesRead));
        Assert.Equal(0, value);
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public void TryReadVarInt_ShouldClearTheSignedValue_WhenTheInt32EncodingOverflows()
    {
        // Six continuation-terminated bytes exceed the five-byte 32-bit ceiling.
        int value = 7;
        Assert.False(VarInt.TryReadVarInt(
            new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F }, out value, out int bytesRead));
        Assert.Equal(0, value);
        Assert.Equal(0, bytesRead);

        // A fifth byte carrying bits above bit 31 is malformed for a 32-bit value.
        value = 7;
        Assert.False(VarInt.TryReadVarInt(
            new byte[] { 0x80, 0x80, 0x80, 0x80, 0x10 }, out value, out bytesRead));
        Assert.Equal(0, value);
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public void TryReadVarInt_ShouldClearTheSignedValue_WhenTheInt64SourceIsTruncated()
    {
        long value = -98765L;
        Assert.False(VarInt.TryReadVarInt(new byte[] { 0xFF, 0xFF, 0x80 }, out value, out int bytesRead));
        Assert.Equal(0L, value);
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public void TryReadVarInt_ShouldClearTheSignedValue_WhenTheInt64SourceIsEmpty()
    {
        long value = long.MinValue;
        Assert.False(VarInt.TryReadVarInt(ReadOnlySpan<byte>.Empty, out value, out int bytesRead));
        Assert.Equal(0L, value);
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public void TryReadVarInt_ShouldClearTheSignedValue_WhenTheInt64EncodingOverflows()
    {
        // Eleven bytes exceed the ten-byte 64-bit ceiling.
        long value = 9L;
        Assert.False(VarInt.TryReadVarInt(
            new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x01 },
            out value, out int bytesRead));
        Assert.Equal(0L, value);
        Assert.Equal(0, bytesRead);

        // A tenth byte carrying bits above bit 63 is malformed for a 64-bit value.
        value = 9L;
        Assert.False(VarInt.TryReadVarInt(
            new byte[] { 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x02 },
            out value, out bytesRead));
        Assert.Equal(0L, value);
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public void TryReadVarInt_ShouldDecodeTheSignedValue_WhenTheSourceIsWellFormed()
    {
        // The success arm of the same wrappers, so the failure assertions above are not vacuously true for
        // an always-false method.
        Span<byte> buffer = stackalloc byte[VarInt.MaxVarIntLength64];

        Assert.True(VarInt.TryWriteVarInt(buffer, -100_000, out int written));
        Assert.True(VarInt.TryReadVarInt(buffer, out int decoded32, out int bytesRead));
        Assert.Equal(-100_000, decoded32);
        Assert.Equal(written, bytesRead);

        Assert.True(VarInt.TryWriteVarInt(buffer, long.MinValue, out written));
        Assert.True(VarInt.TryReadVarInt(buffer, out long decoded64, out bytesRead));
        Assert.Equal(long.MinValue, decoded64);
        Assert.Equal(written, bytesRead);
    }
}
