using System;
using Celerity.Primitives;

namespace Celerity.Tests.Utils;

/// <summary>
/// Correctness coverage for the <see cref="SpanBits"/> non-owning span bit-packing helpers
/// (<c>WordCount</c> / <c>Get</c> / <c>Set</c> / <c>Clear</c> / <c>Flip</c> / <c>PopCount</c> /
/// <c>NextSetBit</c>, issue #196), reconciled against a plain <c>bool[]</c> model and exercising the
/// 64-bit word boundaries where off-by-one bugs hide.
/// </summary>
public class SpanBitsTests
{
    // ── WordCount ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(63, 1)]
    [InlineData(64, 1)]
    [InlineData(65, 2)]
    [InlineData(128, 2)]
    [InlineData(129, 3)]
    public void WordCount_RoundsUpToWholeWords(int bitCount, int expected)
    {
        Assert.Equal(expected, SpanBits.WordCount(bitCount));
    }

    [Fact]
    public void WordCount_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SpanBits.WordCount(-1));
    }

    // ── Get / Set / Clear / Flip ─────────────────────────────────────────────────────────

    [Fact]
    public void SetGetClear_SingleBit_RoundTrips()
    {
        Span<ulong> bits = stackalloc ulong[2]; // 128 bits

        foreach (int index in new[] { 0, 1, 31, 32, 63, 64, 65, 127 })
        {
            Assert.False(SpanBits.Get(bits, index));
            SpanBits.Set(bits, index);
            Assert.True(SpanBits.Get(bits, index));
            SpanBits.Clear(bits, index);
            Assert.False(SpanBits.Get(bits, index));
        }
    }

    [Fact]
    public void Set_DoesNotDisturbNeighbouringBits()
    {
        Span<ulong> bits = stackalloc ulong[2];
        SpanBits.Set(bits, 64);

        Assert.True(SpanBits.Get(bits, 64));
        Assert.False(SpanBits.Get(bits, 63));
        Assert.False(SpanBits.Get(bits, 65));
        Assert.Equal(1, SpanBits.PopCount(bits));
    }

    [Fact]
    public void Flip_TogglesAndReturnsNewValue()
    {
        Span<ulong> bits = stackalloc ulong[1];

        Assert.True(SpanBits.Flip(bits, 10));   // 0 → 1
        Assert.True(SpanBits.Get(bits, 10));
        Assert.False(SpanBits.Flip(bits, 10));  // 1 → 0
        Assert.False(SpanBits.Get(bits, 10));
    }

    // ── PopCount ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PopCount_EmptyAndFull()
    {
        Span<ulong> bits = stackalloc ulong[3]; // 192 bits
        Assert.Equal(0, SpanBits.PopCount(bits));

        bits.Fill(ulong.MaxValue);
        Assert.Equal(192, SpanBits.PopCount(bits));
    }

    // ── NextSetBit ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void NextSetBit_EmptyStorage_ReturnsMinusOne()
    {
        Span<ulong> bits = stackalloc ulong[2];
        Assert.Equal(-1, SpanBits.NextSetBit(bits, 0));
    }

    [Fact]
    public void NextSetBit_FindsBitsAcrossWordBoundaries()
    {
        Span<ulong> bits = stackalloc ulong[3]; // 192 bits
        foreach (int i in new[] { 5, 63, 64, 130 })
            SpanBits.Set(bits, i);

        Assert.Equal(5, SpanBits.NextSetBit(bits, 0));
        Assert.Equal(5, SpanBits.NextSetBit(bits, 5));   // inclusive of fromIndex
        Assert.Equal(63, SpanBits.NextSetBit(bits, 6));
        Assert.Equal(64, SpanBits.NextSetBit(bits, 64)); // first bit of the second word
        Assert.Equal(130, SpanBits.NextSetBit(bits, 65));
        Assert.Equal(-1, SpanBits.NextSetBit(bits, 131));
    }

    [Fact]
    public void NextSetBit_NegativeFromIndex_TreatedAsZero()
    {
        Span<ulong> bits = stackalloc ulong[1];
        SpanBits.Set(bits, 3);
        Assert.Equal(3, SpanBits.NextSetBit(bits, -100));
    }

    [Fact]
    public void NextSetBit_FromIndexBeyondEnd_ReturnsMinusOne()
    {
        Span<ulong> bits = stackalloc ulong[1];
        SpanBits.Set(bits, 10);
        Assert.Equal(-1, SpanBits.NextSetBit(bits, 64));
        Assert.Equal(-1, SpanBits.NextSetBit(bits, 1000));
    }

    // ── Randomized reconciliation against a bool[] model ─────────────────────────────────

    [Fact]
    public void Randomized_MatchesBoolArrayModel()
    {
        var rng = new Random(20260610);
        const int wordCount = 8;
        const int bitCount = wordCount * 64; // 512

        var bits = new ulong[wordCount];
        var model = new bool[bitCount];

        for (int op = 0; op < 200_000; op++)
        {
            int index = rng.Next(bitCount);
            switch (rng.Next(3))
            {
                case 0:
                    SpanBits.Set(bits, index);
                    model[index] = true;
                    break;
                case 1:
                    SpanBits.Clear(bits, index);
                    model[index] = false;
                    break;
                default:
                    bool expected = !model[index];
                    Assert.Equal(expected, SpanBits.Flip(bits, index));
                    model[index] = expected;
                    break;
            }

            Assert.Equal(model[index], SpanBits.Get(bits, index));
        }

        // Final reconciliation of the aggregate queries against the model.
        int expectedPop = 0;
        foreach (bool b in model)
            if (b) expectedPop++;
        Assert.Equal(expectedPop, SpanBits.PopCount(bits));

        // Walk every set bit via NextSetBit and confirm it visits exactly the model's set indices.
        int visited = 0;
        for (int i = SpanBits.NextSetBit(bits, 0); i >= 0; i = SpanBits.NextSetBit(bits, i + 1))
        {
            Assert.True(model[i]);
            visited++;
        }
        Assert.Equal(expectedPop, visited);
    }

    // ── Out-of-range indexing throws via the span access ─────────────────────────────────

    [Fact]
    public void OutOfRangeIndex_Throws()
    {
        Assert.Throws<IndexOutOfRangeException>(() =>
        {
            var bits = new ulong[1];
            SpanBits.Set(bits, 64); // valid indices are [0, 64)
        });

        Assert.Throws<IndexOutOfRangeException>(() =>
        {
            var bits = new ulong[1];
            return SpanBits.Get(bits, 64);
        });
    }
}
