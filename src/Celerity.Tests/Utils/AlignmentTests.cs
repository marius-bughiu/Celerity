using System;

namespace Celerity.Tests.Utils;

/// <summary>
/// Correctness coverage for the <see cref="FastUtils"/> alignment helpers — <c>AlignUp</c> /
/// <c>AlignDown</c> / <c>IsAligned</c> for <see cref="int"/>, <see cref="long"/>, and pointer-sized
/// (<see cref="nuint"/>) values (issue #196) — reconciled against a modulo-based reference oracle.
/// </summary>
public class AlignmentTests
{
    // ── int overloads ───────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 8, 0)]
    [InlineData(1, 8, 8)]
    [InlineData(7, 8, 8)]
    [InlineData(8, 8, 8)]   // already aligned → unchanged
    [InlineData(9, 8, 16)]
    [InlineData(13, 1, 13)] // alignment 1 → everything aligned
    [InlineData(1000, 64, 1024)]
    [InlineData(1024, 64, 1024)]
    public void AlignUp_Int_MatchesExpected(int value, int alignment, int expected)
    {
        Assert.Equal(expected, FastUtils.AlignUp(value, alignment));
    }

    [Theory]
    [InlineData(0, 8, 0)]
    [InlineData(1, 8, 0)]
    [InlineData(7, 8, 0)]
    [InlineData(8, 8, 8)]
    [InlineData(9, 8, 8)]
    [InlineData(13, 1, 13)]
    [InlineData(1000, 64, 960)]
    [InlineData(1024, 64, 1024)]
    public void AlignDown_Int_MatchesExpected(int value, int alignment, int expected)
    {
        Assert.Equal(expected, FastUtils.AlignDown(value, alignment));
    }

    [Fact]
    public void AlignUpDown_Int_ReconcileWithModuloReference()
    {
        var rng = new Random(20260610);
        int[] alignments = { 1, 2, 4, 8, 16, 32, 64, 256, 4096, 1 << 20 };

        for (int iter = 0; iter < 200_000; iter++)
        {
            int alignment = alignments[rng.Next(alignments.Length)];
            // Keep value well clear of int.MaxValue so AlignUp cannot overflow.
            int value = rng.Next(0, int.MaxValue - (1 << 20));

            int up = FastUtils.AlignUp(value, alignment);
            int down = FastUtils.AlignDown(value, alignment);

            // Reference: round to multiples of alignment.
            int refDown = value - (value % alignment);
            int refUp = refDown == value ? value : refDown + alignment;

            Assert.Equal(refUp, up);
            Assert.Equal(refDown, down);
            Assert.Equal(value % alignment == 0, FastUtils.IsAligned(value, alignment));

            // Structural invariants.
            Assert.True(down <= value && value <= up);
            Assert.Equal(0, up % alignment);
            Assert.Equal(0, down % alignment);
            Assert.True(FastUtils.IsAligned(up, alignment));
            Assert.True(FastUtils.IsAligned(down, alignment));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(-8)]
    [InlineData(100)]
    public void NonPowerOfTwoAlignment_Int_Throws(int alignment)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FastUtils.AlignUp(0, alignment));
        Assert.Throws<ArgumentOutOfRangeException>(() => FastUtils.AlignDown(0, alignment));
        Assert.Throws<ArgumentOutOfRangeException>(() => FastUtils.IsAligned(0, alignment));
    }

    // ── long overloads ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0L, 8L, 0L)]
    [InlineData(1L, 8L, 8L)]
    [InlineData(8L, 8L, 8L)]
    [InlineData(9L, 8L, 16L)]
    [InlineData(5_000_000_001L, 4096L, 5_000_003_584L)]
    public void AlignUp_Long_MatchesExpected(long value, long alignment, long expected)
    {
        Assert.Equal(expected, FastUtils.AlignUp(value, alignment));
    }

    [Fact]
    public void AlignUpDown_Long_ReconcileWithModuloReference()
    {
        var rng = new Random(99887766);
        long[] alignments = { 1, 2, 8, 64, 4096, 1L << 30, 1L << 40 };

        for (int iter = 0; iter < 200_000; iter++)
        {
            long alignment = alignments[rng.Next(alignments.Length)];
            long value = (long)(rng.NextDouble() * (1L << 52)); // non-negative, far from overflow

            long up = FastUtils.AlignUp(value, alignment);
            long down = FastUtils.AlignDown(value, alignment);

            long refDown = value - (value % alignment);
            long refUp = refDown == value ? value : refDown + alignment;

            Assert.Equal(refUp, up);
            Assert.Equal(refDown, down);
            Assert.Equal(value % alignment == 0, FastUtils.IsAligned(value, alignment));
        }
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(3L)]
    [InlineData(48L)]
    [InlineData(-16L)]
    public void NonPowerOfTwoAlignment_Long_Throws(long alignment)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FastUtils.AlignUp(0L, alignment));
        Assert.Throws<ArgumentOutOfRangeException>(() => FastUtils.AlignDown(0L, alignment));
        Assert.Throws<ArgumentOutOfRangeException>(() => FastUtils.IsAligned(0L, alignment));
    }

    // ── nuint (pointer-sized) overloads ─────────────────────────────────────────────────

    [Fact]
    public void AlignUpDown_NUInt_ReconcileWithModuloReference()
    {
        var rng = new Random(13572468);
        nuint[] alignments = { 1, 2, 8, 16, 64, 4096 };

        for (int iter = 0; iter < 200_000; iter++)
        {
            nuint alignment = alignments[rng.Next(alignments.Length)];
            nuint value = (nuint)(uint)rng.Next(0, 1 << 30);

            nuint up = FastUtils.AlignUp(value, alignment);
            nuint down = FastUtils.AlignDown(value, alignment);

            nuint refDown = value - (value % alignment);
            nuint refUp = refDown == value ? value : refDown + alignment;

            Assert.Equal(refUp, up);
            Assert.Equal(refDown, down);
            Assert.Equal(value % alignment == 0, FastUtils.IsAligned(value, alignment));
            Assert.True(down <= value && value <= up);
        }
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(3u)]
    [InlineData(100u)]
    public void NonPowerOfTwoAlignment_NUInt_Throws(uint alignment)
    {
        nuint a = alignment;
        Assert.Throws<ArgumentOutOfRangeException>(() => FastUtils.AlignUp((nuint)0, a));
        Assert.Throws<ArgumentOutOfRangeException>(() => FastUtils.AlignDown((nuint)0, a));
        Assert.Throws<ArgumentOutOfRangeException>(() => FastUtils.IsAligned((nuint)0, a));
    }
}
