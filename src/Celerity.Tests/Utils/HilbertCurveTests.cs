using System;
using Celerity.Primitives;

namespace Celerity.Tests.Utils;

/// <summary>
/// Correctness coverage for <see cref="HilbertCurve"/> (issue #369).
///
/// <para>
/// Round-tripping is the weakest thing that can be checked here: an encode and a decode built from the
/// same misunderstanding close perfectly while tracing a curve that is not Hilbert's. So the fixtures
/// pin two independent things — the <em>shape</em>, against the textbook order-2 traversal, and the
/// <em>adjacency</em> property, which is the whole reason to pay for this curve instead of
/// <see cref="MortonCurve"/>: stepping the index by one must move the point exactly one cell along
/// exactly one axis, everywhere on the curve.
/// </para>
/// </summary>
public class HilbertCurveTests
{
    // ── The curve's shape, against the textbook traversal ────────────────────────────────

    [Fact]
    public void Decode2D_ShouldTraceTheTextbookOrder2Curve_WhenWalkingTheFirstSixteenIndices()
    {
        // The classic 4x4 Hilbert traversal: up the left column, across the top, back down the right.
        (uint X, uint Y)[] expected =
        [
            (0, 0), (1, 0), (1, 1), (0, 1),
            (0, 2), (0, 3), (1, 3), (1, 2),
            (2, 2), (2, 3), (3, 3), (3, 2),
            (3, 1), (2, 1), (2, 0), (3, 0),
        ];

        for (ulong index = 0; index < (ulong)expected.Length; index++)
        {
            Assert.Equal(expected[index], HilbertCurve.Decode2D(index));
        }
    }

    [Fact]
    public void Encode2D_ShouldStartAtTheOriginAndEndOnTheFirstAxis_WhenTakenOverTheWholeDomain()
    {
        // The conventional orientation, and the one the docs promise: index 0 is (0, 0) and the last index
        // is (2^32 - 1, 0), so the curve enters and leaves along the same edge.
        Assert.Equal(0UL, HilbertCurve.Encode2D(0, 0));
        Assert.Equal((uint.MaxValue, 0u), HilbertCurve.Decode2D(ulong.MaxValue));
    }

    // ── Adjacency: the property that distinguishes Hilbert from Morton ───────────────────

    [Fact]
    public void Decode2D_ShouldMoveOneCellPerIndex_WhenWalkingAContiguousRun()
    {
        AssertAdjacent2D(0, 100_000);
    }

    [Theory]
    [InlineData(0x0000_0000_FFFF_FF00UL)]
    [InlineData(0x1234_5678_9ABC_DEF0UL)]
    [InlineData(0x8000_0000_0000_0000UL)]
    [InlineData(0xFFFF_FFFF_FFFF_0000UL)]
    public void Decode2D_ShouldMoveOneCellPerIndex_WhenWalkingARunFarFromTheOrigin(ulong start)
    {
        // Self-similarity means a bug at one scale need not show up at another, so the runs are sampled
        // from across the index space rather than only from the front of the curve.
        AssertAdjacent2D(start, 2_000);
    }

    [Fact]
    public void Decode3D_ShouldMoveOneCellPerIndex_WhenWalkingAContiguousRun()
    {
        AssertAdjacent3D(0, 100_000);
    }

    [Theory]
    [InlineData(0x0000_0000_FFFF_FF00UL)]
    [InlineData(0x1234_5678_9ABC_DEF0UL)]
    [InlineData(0x7FFF_FFFF_FFFF_0000UL)]
    public void Decode3D_ShouldMoveOneCellPerIndex_WhenWalkingARunFarFromTheOrigin(ulong start)
    {
        AssertAdjacent3D(start, 2_000);
    }

    // ── Round-trip ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Decode2D_ShouldRecoverTheCoordinate_WhenSweepingASquareExhaustively()
    {
        for (uint y = 0; y < 128; y++)
        {
            for (uint x = 0; x < 128; x++)
            {
                Assert.Equal((x, y), HilbertCurve.Decode2D(HilbertCurve.Encode2D(x, y)));
            }
        }
    }

    [Fact]
    public void Encode2D_ShouldBeABijection_WhenEveryIndexIsDecodedAndReEncoded()
    {
        var rand = new Random(31337);
        for (int i = 0; i < 20_000; i++)
        {
            ulong index = ((ulong)(uint)rand.Next() << 32) | (uint)rand.Next();
            var (x, y) = HilbertCurve.Decode2D(index);
            Assert.Equal(index, HilbertCurve.Encode2D(x, y));
        }
    }

    [Fact]
    public void Decode3D_ShouldRecoverTheCoordinate_WhenSweepingACubeExhaustively()
    {
        for (uint z = 0; z < 24; z++)
        {
            for (uint y = 0; y < 24; y++)
            {
                for (uint x = 0; x < 24; x++)
                {
                    Assert.Equal((x, y, z), HilbertCurve.Decode3D(HilbertCurve.Encode3D(x, y, z)));
                }
            }
        }
    }

    [Fact]
    public void Encode3D_ShouldRecoverTheCoordinate_WhenGivenTheExtremesOfTheDomain()
    {
        const uint Max = HilbertCurve.MaxCoordinate3D;
        (uint X, uint Y, uint Z)[] corners =
        [
            (0, 0, 0), (Max, 0, 0), (0, Max, 0), (0, 0, Max),
            (Max, Max, 0), (Max, 0, Max), (0, Max, Max), (Max, Max, Max),
        ];

        foreach (var corner in corners)
        {
            ulong index = HilbertCurve.Encode3D(corner.X, corner.Y, corner.Z);
            Assert.True(index < 1UL << 63);
            Assert.Equal(corner, HilbertCurve.Decode3D(index));
        }
    }

    [Fact]
    public void Decode3D_ShouldIgnoreTheUnusedTopBit_WhenItIsSet()
    {
        ulong index = HilbertCurve.Encode3D(101, 202, 303);
        Assert.Equal(HilbertCurve.Decode3D(index), HilbertCurve.Decode3D(index | (1UL << 63)));
    }

    [Theory]
    [InlineData(HilbertCurve.MaxCoordinate3D + 1, 0u, 0u, "x")]
    [InlineData(0u, HilbertCurve.MaxCoordinate3D + 1, 0u, "y")]
    [InlineData(0u, 0u, HilbertCurve.MaxCoordinate3D + 1, "z")]
    [InlineData(uint.MaxValue, uint.MaxValue, uint.MaxValue, "x")]
    public void Encode3D_ShouldThrow_WhenACoordinateExceedsTheDomain(uint x, uint y, uint z, string paramName)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => HilbertCurve.Encode3D(x, y, z));
        Assert.Equal(paramName, ex.ParamName);
    }

    // ── Locality: what Hilbert keeps, and what it gives up ───────────────────────────────

    [Fact]
    public void Encode2D_ShouldPlaceAnAlignedCellInOneContiguousIndexRange_WhenPointsShareTheirHighBits()
    {
        const int CellBits = 4;              // 16 x 16 cell
        const uint CellOrigin = 48u << CellBits;

        ulong low = ulong.MaxValue, high = ulong.MinValue;
        for (uint dy = 0; dy < 1u << CellBits; dy++)
        {
            for (uint dx = 0; dx < 1u << CellBits; dx++)
            {
                ulong index = HilbertCurve.Encode2D(CellOrigin + dx, CellOrigin + dy);
                low = Math.Min(low, index);
                high = Math.Max(high, index);
            }
        }

        Assert.Equal((1UL << (2 * CellBits)) - 1, high - low);
    }

    [Fact]
    public void Encode2D_ShouldNotBeMonotoneInOneAxis_WhenTheOtherIsHeldFixed()
    {
        // The price of adjacency, pinned rather than left implicit: the curve folds back on itself, so the
        // index does not rise with a coordinate the way a Morton code does. Callers who need that ordering
        // want MortonCurve instead. Along y = 1 the traversal above runs right-to-left, so x = 1 comes first.
        Assert.True(HilbertCurve.Encode2D(1, 1) < HilbertCurve.Encode2D(0, 1));
        Assert.True(MortonCurve.Encode2D(0, 1) < MortonCurve.Encode2D(1, 1));
    }

    private static void AssertAdjacent2D(ulong start, int count)
    {
        var (x, y) = HilbertCurve.Decode2D(start);
        for (int i = 0; i < count; i++)
        {
            var (nx, ny) = HilbertCurve.Decode2D(start + (ulong)i + 1);
            long step = Math.Abs((long)nx - x) + Math.Abs((long)ny - y);
            Assert.True(step == 1, $"index {start + (ulong)i} -> {start + (ulong)i + 1} moved ({x},{y}) to ({nx},{ny})");
            (x, y) = (nx, ny);
        }
    }

    private static void AssertAdjacent3D(ulong start, int count)
    {
        var (x, y, z) = HilbertCurve.Decode3D(start);
        for (int i = 0; i < count; i++)
        {
            var (nx, ny, nz) = HilbertCurve.Decode3D(start + (ulong)i + 1);
            long step = Math.Abs((long)nx - x) + Math.Abs((long)ny - y) + Math.Abs((long)nz - z);
            Assert.True(step == 1, $"index {start + (ulong)i} -> {start + (ulong)i + 1} moved ({x},{y},{z}) to ({nx},{ny},{nz})");
            (x, y, z) = (nx, ny, nz);
        }
    }
}
