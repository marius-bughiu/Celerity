using System;
using System.Collections.Generic;
using Celerity.Primitives;
using Xunit;

namespace Celerity.Tests.Utils;

/// <summary>
/// Correctness coverage for the sequential sub-byte bit I/O pair <see cref="BitWriter"/> /
/// <see cref="BitReader"/>: LSB-first round-trips across byte boundaries, the <c>ByteCount</c> size
/// helper, high-bit masking, single-bit helpers, the bounds-safe short-buffer / truncated failure paths,
/// the <c>bitCount</c> argument validation, and a randomized reconciliation against a <c>bool[]</c> bit
/// model.
/// </summary>
public class BitBufferTests
{
    // ── ByteCount ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(7, 1)]
    [InlineData(8, 1)]
    [InlineData(9, 2)]
    [InlineData(64, 8)]
    [InlineData(65, 9)]
    public void ByteCount_RoundsUpToWholeBytes(int bitCount, int expected)
    {
        Assert.Equal(expected, BitWriter.ByteCount(bitCount));
    }

    [Fact]
    public void ByteCount_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BitWriter.ByteCount(-1));
    }

    // ── Round-trip of a mixed-width field sequence ───────────────────────────────────────

    [Fact]
    public void WriteRead_MixedWidthFields_RoundTrip()
    {
        // A record of odd-width fields: 3 + 12 + 1 + 20 + 28 = 64 bits → exactly 8 bytes.
        (ulong Value, int Bits)[] fields =
        {
            (5, 3),
            (3000, 12),
            (1, 1),
            (0xABCDE, 20),
            (0x0FFFFFFF, 28),
        };

        var buffer = new byte[8];
        var writer = new BitWriter(buffer);
        foreach (var (value, bits) in fields)
            Assert.True(writer.TryWriteBits(value, bits));

        Assert.Equal(64, writer.BitsWritten);
        Assert.Equal(8, writer.BytesWritten);

        var reader = new BitReader(buffer);
        foreach (var (value, bits) in fields)
        {
            Assert.True(reader.TryReadBits(bits, out ulong read));
            Assert.Equal(value, read);
        }

        Assert.Equal(64, reader.BitsRead);
        Assert.Equal(0, reader.BitsRemaining);
    }

    // ── LSB-first bit order is observable in the packed bytes ─────────────────────────────

    [Fact]
    public void WriteBits_IsLsbFirst_InPackedBytes()
    {
        // Write 0b101 (=5) as a 3-bit field, then 0b11 (=3) as a 2-bit field at the same byte.
        // LSB-first: byte 0 = ...0_11_101 = 0b0001_1101 = 0x1D.
        var buffer = new byte[1];
        var writer = new BitWriter(buffer);
        Assert.True(writer.TryWriteBits(5, 3));
        Assert.True(writer.TryWriteBits(3, 2));

        Assert.Equal(0x1D, buffer[0]);
        Assert.Equal(5, writer.BitsWritten);
    }

    // ── High bits above bitCount are ignored, not leaked into the next field ──────────────

    [Fact]
    public void WriteBits_IgnoresHighBitsAboveWidth()
    {
        var buffer = new byte[2];
        var writer = new BitWriter(buffer);
        // 0xFF written as a 3-bit field must store only the low 3 bits (0b111 = 7).
        Assert.True(writer.TryWriteBits(0xFF, 3));
        // A following 5-bit field must be uncorrupted by the discarded high bits.
        Assert.True(writer.TryWriteBits(0b10101, 5));

        var reader = new BitReader(buffer);
        Assert.True(reader.TryReadBits(3, out ulong a));
        Assert.True(reader.TryReadBits(5, out ulong b));
        Assert.Equal(7u, a);
        Assert.Equal(0b10101u, b);
    }

    // ── Full-width fields (64, 32, 1) round-trip at every start offset ────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(63)]
    [InlineData(64)]
    public void WriteRead_FullWidthValue_RoundTripsAtEveryStartOffset(int width)
    {
        ulong value = width == 64 ? ulong.MaxValue : (1UL << width) - 1; // all-ones of the width

        // Try every sub-byte alignment for the field's start.
        for (int pad = 0; pad < 8; pad++)
        {
            var buffer = new byte[BitWriter.ByteCount(pad + width)];
            var writer = new BitWriter(buffer);
            if (pad > 0)
                Assert.True(writer.TryWriteBits(0, pad)); // shift the field off the byte boundary
            Assert.True(writer.TryWriteBits(value, width));

            var reader = new BitReader(buffer);
            if (pad > 0)
                Assert.True(reader.TryReadBits(pad, out _));
            Assert.True(reader.TryReadBits(width, out ulong read));
            Assert.Equal(value, read);
        }
    }

    // ── Single-bit helpers ───────────────────────────────────────────────────────────────

    [Fact]
    public void WriteReadBit_RoundTrips()
    {
        bool[] pattern = { true, false, false, true, true, true, false, true, false, true };
        var buffer = new byte[BitWriter.ByteCount(pattern.Length)];

        var writer = new BitWriter(buffer);
        foreach (bool b in pattern)
            Assert.True(writer.TryWriteBit(b));

        var reader = new BitReader(buffer);
        foreach (bool b in pattern)
        {
            Assert.True(reader.TryReadBit(out bool read));
            Assert.Equal(b, read);
        }
    }

    // ── Zero-width field is a no-op success ──────────────────────────────────────────────

    [Fact]
    public void WriteReadBits_ZeroWidth_IsNoOpSuccess()
    {
        var buffer = new byte[1];
        var writer = new BitWriter(buffer);
        Assert.True(writer.TryWriteBits(12345, 0));
        Assert.Equal(0, writer.BitsWritten);

        var reader = new BitReader(buffer);
        Assert.True(reader.TryReadBits(0, out ulong value));
        Assert.Equal(0u, value);
        Assert.Equal(0, reader.BitsRead);
    }

    // ── Bounds safety: overfull write / exhausted read fail without mutation ──────────────

    [Fact]
    public void TryWriteBits_InsufficientSpace_FailsWithoutMutation()
    {
        var buffer = new byte[1]; // 8 bits
        var writer = new BitWriter(buffer);
        Assert.True(writer.TryWriteBits(0b111, 3)); // 3 bits used, 5 remain

        // A 6-bit field does not fit in the remaining 5 bits.
        Assert.False(writer.TryWriteBits(0b111111, 6));
        Assert.Equal(3, writer.BitsWritten);       // cursor unchanged
        Assert.Equal(0b111, buffer[0] & 0b111);    // earlier bits intact

        // The remaining 5 bits are still writable.
        Assert.True(writer.TryWriteBits(0b10101, 5));
        Assert.Equal(8, writer.BitsWritten);
    }

    [Fact]
    public void TryReadBits_TruncatedSource_FailsWithoutMutation()
    {
        var buffer = new byte[1]; // 8 bits
        var reader = new BitReader(buffer);
        Assert.True(reader.TryReadBits(6, out _)); // 6 read, 2 remain

        Assert.False(reader.TryReadBits(3, out ulong value)); // only 2 left
        Assert.Equal(0u, value);
        Assert.Equal(6, reader.BitsRead);          // cursor unchanged

        Assert.True(reader.TryReadBits(2, out _));  // the remaining 2 are still readable
        Assert.Equal(8, reader.BitsRead);
    }

    [Fact]
    public void TryWriteBit_FullBuffer_Fails()
    {
        var buffer = new byte[1];
        var writer = new BitWriter(buffer);
        for (int i = 0; i < 8; i++)
            Assert.True(writer.TryWriteBit(true));
        Assert.False(writer.TryWriteBit(true));
        Assert.Equal(8, writer.BitsWritten);
    }

    // ── bitCount argument validation ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(-1)]
    [InlineData(65)]
    [InlineData(100)]
    public void TryWriteBits_InvalidBitCount_Throws(int bitCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var writer = new BitWriter(new byte[16]);
            writer.TryWriteBits(0, bitCount);
        });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(65)]
    [InlineData(100)]
    public void TryReadBits_InvalidBitCount_Throws(int bitCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var reader = new BitReader(new byte[16]);
            reader.TryReadBits(bitCount, out _);
        });
    }

    // ── Rewriting a field over a dirty buffer stays correct (clear-then-set) ──────────────

    [Fact]
    public void TryWriteBits_OverwritesOverDirtyBuffer()
    {
        var buffer = new byte[2];
        buffer.AsSpan().Fill(0xFF); // dirty: every bit set

        var writer = new BitWriter(buffer);
        Assert.True(writer.TryWriteBits(0b010, 3));   // straddles nothing
        Assert.True(writer.TryWriteBits(0b1100, 4));

        var reader = new BitReader(buffer);
        Assert.True(reader.TryReadBits(3, out ulong a));
        Assert.True(reader.TryReadBits(4, out ulong b));
        Assert.Equal(0b010u, a); // the 0-bits were actually cleared despite the dirty buffer
        Assert.Equal(0b1100u, b);
    }

    // ── Randomized reconciliation against a bool[] bit model ─────────────────────────────

    [Fact]
    public void Randomized_MatchesBoolArrayModel()
    {
        var rng = new Random(20260705);

        for (int trial = 0; trial < 3000; trial++)
        {
            // Build a random field plan whose total width fits a fixed buffer.
            var plan = new List<(ulong Value, int Bits)>();
            var model = new List<bool>();
            int totalBits = 0;
            const int capacityBits = 40 * 8; // 40 bytes

            while (true)
            {
                int bits = rng.Next(0, 65);
                if (totalBits + bits > capacityBits)
                    break;

                ulong value = NextUInt64(rng);
                plan.Add((value, bits));
                totalBits += bits;

                // Model: append the low `bits` bits of value, least-significant first.
                for (int i = 0; i < bits; i++)
                    model.Add(((value >> i) & 1UL) != 0);

                if (plan.Count > 20)
                    break;
            }

            var buffer = new byte[40];
            var writer = new BitWriter(buffer);
            foreach (var (value, bits) in plan)
                Assert.True(writer.TryWriteBits(value, bits));
            Assert.Equal(totalBits, writer.BitsWritten);

            // The packed buffer must match the model bit-for-bit (LSB-first).
            for (int i = 0; i < model.Count; i++)
            {
                bool actualBit = ((buffer[i >> 3] >> (i & 7)) & 1) != 0;
                Assert.Equal(model[i], actualBit);
            }

            // And reading the fields back recovers each value exactly (masked to its width).
            var reader = new BitReader(buffer);
            foreach (var (value, bits) in plan)
            {
                Assert.True(reader.TryReadBits(bits, out ulong read));
                ulong expected = bits == 0 ? 0UL : bits == 64 ? value : value & ((1UL << bits) - 1);
                Assert.Equal(expected, read);
            }
            Assert.Equal(totalBits, reader.BitsRead);
        }
    }

    private static ulong NextUInt64(Random rng)
    {
        Span<byte> b = stackalloc byte[8];
        rng.NextBytes(b);
        return BitConverter.ToUInt64(b);
    }
}
