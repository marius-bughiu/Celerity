using System;
using Celerity.Primitives;

namespace Celerity.Tests.Utils;

/// <summary>
/// Correctness coverage for the <see cref="SimdReductions"/> fused/specialized SIMD reductions
/// (single-pass <c>MinMax</c> for <c>int</c>/<c>long</c>/<c>uint</c>/<c>ulong</c> and overflow-checked
/// <c>CheckedSum</c> for <c>int</c>, issue #197), reconciled against scalar oracles and exercising the
/// vector-boundary lengths (sub-vector spans, exact multiples, and ragged tails) where SIMD off-by-ones hide.
/// </summary>
public class SimdReductionsTests
{
    // ── MinMax: small, exact cases ───────────────────────────────────────────────────────

    [Fact]
    public void MinMax_Int_SingleElement_IsBothExtrema()
    {
        var (min, max) = SimdReductions.MinMax(new[] { 42 });
        Assert.Equal(42, min);
        Assert.Equal(42, max);
    }

    [Fact]
    public void MinMax_Int_FindsExtremaIncludingNegatives()
    {
        var (min, max) = SimdReductions.MinMax(new[] { 3, -7, 0, 9, -1, 5 });
        Assert.Equal(-7, min);
        Assert.Equal(9, max);
    }

    [Fact]
    public void MinMax_Int_ExtremaAtVectorBoundaries()
    {
        // Place int.MinValue / int.MaxValue at the very ends so the horizontal lane reduction must
        // surface a value from the first and the last lane.
        var data = new int[40];
        for (int i = 0; i < data.Length; i++)
            data[i] = i;
        data[0] = int.MinValue;
        data[^1] = int.MaxValue;

        var (min, max) = SimdReductions.MinMax(data);
        Assert.Equal(int.MinValue, min);
        Assert.Equal(int.MaxValue, max);
    }

    [Fact]
    public void MinMax_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => SimdReductions.MinMax(ReadOnlySpan<int>.Empty));
        Assert.Throws<ArgumentException>(() => SimdReductions.MinMax(ReadOnlySpan<long>.Empty));
        Assert.Throws<ArgumentException>(() => SimdReductions.MinMax(ReadOnlySpan<uint>.Empty));
        Assert.Throws<ArgumentException>(() => SimdReductions.MinMax(ReadOnlySpan<ulong>.Empty));
    }

    [Fact]
    public void MinMax_Long_FindsExtrema()
    {
        var (min, max) = SimdReductions.MinMax(new[] { 5L, long.MinValue, 100L, long.MaxValue, -3L });
        Assert.Equal(long.MinValue, min);
        Assert.Equal(long.MaxValue, max);
    }

    [Fact]
    public void MinMax_UInt_ExtremaAtVectorBoundaries()
    {
        // Longer than any SIMD vector width, with the extrema at the very ends so the horizontal lane
        // reduction (not just the scalar path) must surface a first- and last-lane value.
        var data = new uint[40];
        for (int i = 0; i < data.Length; i++)
            data[i] = (uint)(i + 10);
        data[0] = uint.MaxValue;
        data[^1] = 0u;

        var (min, max) = SimdReductions.MinMax(data);
        Assert.Equal(0u, min);
        Assert.Equal(uint.MaxValue, max);
    }

    [Fact]
    public void MinMax_ULong_ExtremaAtVectorBoundaries()
    {
        var data = new ulong[40];
        for (int i = 0; i < data.Length; i++)
            data[i] = (ulong)(i + 10);
        data[0] = ulong.MaxValue;
        data[^1] = 0ul;

        var (min, max) = SimdReductions.MinMax(data);
        Assert.Equal(0ul, min);
        Assert.Equal(ulong.MaxValue, max);
    }

    // ── MinMax: randomized reconciliation against a scalar oracle across every length ──────

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(64)]
    [InlineData(257)]
    [InlineData(1000)]
    public void MinMax_Int_MatchesScalarOracle(int length)
    {
        var rng = new Random(0x5197 + length);
        var data = new int[length];
        for (int i = 0; i < length; i++)
            data[i] = rng.Next(int.MinValue, int.MaxValue);

        int expectedMin = data[0], expectedMax = data[0];
        foreach (int v in data)
        {
            if (v < expectedMin) expectedMin = v;
            if (v > expectedMax) expectedMax = v;
        }

        var (min, max) = SimdReductions.MinMax(data);
        Assert.Equal(expectedMin, min);
        Assert.Equal(expectedMax, max);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(31)]
    [InlineData(512)]
    public void MinMax_Long_MatchesScalarOracle(int length)
    {
        var rng = new Random(0x6197 + length);
        var data = new long[length];
        for (int i = 0; i < length; i++)
            // Full-range, including negatives: a non-negative high word would never set the sign bit and
            // would leave the signed Vector<long> comparison path untested.
            data[i] = ((long)rng.Next(int.MinValue, int.MaxValue) << 32) | (uint)rng.Next();

        long expectedMin = data[0], expectedMax = data[0];
        foreach (long v in data)
        {
            if (v < expectedMin) expectedMin = v;
            if (v > expectedMax) expectedMax = v;
        }

        var (min, max) = SimdReductions.MinMax(data);
        Assert.Equal(expectedMin, min);
        Assert.Equal(expectedMax, max);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(31)]
    [InlineData(512)]
    public void MinMax_UInt_MatchesScalarOracle(int length)
    {
        var rng = new Random(0x8197 + length);
        var data = new uint[length];
        for (int i = 0; i < length; i++)
            data[i] = (uint)rng.NextInt64(0, uint.MaxValue + 1L);

        uint expectedMin = data[0], expectedMax = data[0];
        foreach (uint v in data)
        {
            if (v < expectedMin) expectedMin = v;
            if (v > expectedMax) expectedMax = v;
        }

        var (min, max) = SimdReductions.MinMax(data);
        Assert.Equal(expectedMin, min);
        Assert.Equal(expectedMax, max);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(31)]
    [InlineData(512)]
    public void MinMax_ULong_MatchesScalarOracle(int length)
    {
        var rng = new Random(0x9197 + length);
        var data = new ulong[length];
        for (int i = 0; i < length; i++)
            // Full unsigned range: casting a full-range (possibly negative) int to uint spans all 32 bits,
            // so the high word's top bit is set ~half the time rather than never.
            data[i] = ((ulong)(uint)rng.Next(int.MinValue, int.MaxValue) << 32) | (uint)rng.Next(int.MinValue, int.MaxValue);

        ulong expectedMin = data[0], expectedMax = data[0];
        foreach (ulong v in data)
        {
            if (v < expectedMin) expectedMin = v;
            if (v > expectedMax) expectedMax = v;
        }

        var (min, max) = SimdReductions.MinMax(data);
        Assert.Equal(expectedMin, min);
        Assert.Equal(expectedMax, max);
    }

    // ── CheckedSum ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CheckedSum_Empty_IsZero()
    {
        Assert.Equal(0, SimdReductions.CheckedSum(ReadOnlySpan<int>.Empty));
    }

    [Fact]
    public void CheckedSum_SmallValues_MatchesArithmetic()
    {
        Assert.Equal(15, SimdReductions.CheckedSum(new[] { 1, 2, 3, 4, 5 }));
        Assert.Equal(0, SimdReductions.CheckedSum(new[] { -5, 5, -3, 3 }));
        Assert.Equal(-6, SimdReductions.CheckedSum(new[] { -1, -2, -3 }));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(33)]
    [InlineData(1000)]
    public void CheckedSum_MatchesLongOracle(int length)
    {
        var rng = new Random(0x7197 + length);
        var data = new int[length];
        long oracle = 0;
        for (int i = 0; i < length; i++)
        {
            // Keep magnitudes small enough that the true sum stays inside int range for this arm.
            data[i] = rng.Next(-1000, 1000);
            oracle += data[i];
        }

        Assert.Equal((int)oracle, SimdReductions.CheckedSum(data));
    }

    [Fact]
    public void CheckedSum_PositiveOverflow_Throws()
    {
        // Many large positives whose true sum exceeds int.MaxValue.
        var data = new int[100];
        Array.Fill(data, int.MaxValue / 10); // ~10 * (2^31/10) ≈ 2^31, but 100 of them overflows hugely
        Assert.Throws<OverflowException>(() => SimdReductions.CheckedSum(data));
    }

    [Fact]
    public void CheckedSum_NegativeOverflow_Throws()
    {
        var data = new int[100];
        Array.Fill(data, int.MinValue / 10);
        Assert.Throws<OverflowException>(() => SimdReductions.CheckedSum(data));
    }

    [Fact]
    public void CheckedSum_ExactlyIntMaxValue_DoesNotThrow()
    {
        // A sum that lands exactly on the int.MaxValue boundary must be returned, not rejected.
        var data = new[] { int.MaxValue - 10, 10 };
        Assert.Equal(int.MaxValue, SimdReductions.CheckedSum(data));
    }

    [Fact]
    public void CheckedSum_OverflowOnlyAtFinalNarrowing_StillThrows()
    {
        // Each element fits in int and partial sums stay positive, but the total just clears int.MaxValue —
        // exercises the path where SIMD long accumulation succeeds and only the checked cast catches it.
        var data = new int[3];
        data[0] = int.MaxValue;
        data[1] = int.MaxValue;
        data[2] = -int.MaxValue + 5; // total = int.MaxValue + 5 > int.MaxValue
        Assert.Throws<OverflowException>(() => SimdReductions.CheckedSum(data));
    }
}
