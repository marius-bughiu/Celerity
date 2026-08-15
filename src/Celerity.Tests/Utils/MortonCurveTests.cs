using System;
using Celerity.Primitives;

namespace Celerity.Tests.Utils;

/// <summary>
/// Correctness coverage for <see cref="MortonCurve"/> (issue #369): the interleave / de-interleave pair
/// itself, the ordering properties the type advertises, and the 21-bit domain guard on the 3-D form.
///
/// <para>
/// A bit-spread is exactly the kind of code that looks right and is wrong in one lane, so the fixtures
/// pin the code <em>bit pattern</em> against hand-computed values rather than only checking that a
/// round-trip closes — a spread and a compact with matching mistakes round-trip perfectly.
/// </para>
/// </summary>
public class MortonCurveTests
{
    // ── 2-D encode: the interleave, pinned bit-for-bit ───────────────────────────────────

    [Theory]
    [InlineData(0u, 0u, 0UL)]
    [InlineData(1u, 0u, 1UL)]                 // x on the even positions
    [InlineData(0u, 1u, 2UL)]                 // y on the odd ones
    [InlineData(1u, 1u, 3UL)]
    [InlineData(2u, 0u, 4UL)]
    [InlineData(3u, 5u, 39UL)]                // 0b100111: y2 x2 y1 x1 y0 x0 = 1 0 0 1 1 1
    [InlineData(uint.MaxValue, 0u, 0x5555_5555_5555_5555UL)]
    [InlineData(0u, uint.MaxValue, 0xAAAA_AAAA_AAAA_AAAAUL)]
    [InlineData(uint.MaxValue, uint.MaxValue, ulong.MaxValue)]
    public void Encode2D_ShouldInterleaveTheAxes_WhenGivenKnownCoordinates(uint x, uint y, ulong expected)
    {
        Assert.Equal(expected, MortonCurve.Encode2D(x, y));
    }

    [Theory]
    [InlineData(0UL, 0u, 0u)]
    [InlineData(39UL, 3u, 5u)]
    [InlineData(0x5555_5555_5555_5555UL, uint.MaxValue, 0u)]
    [InlineData(0xAAAA_AAAA_AAAA_AAAAUL, 0u, uint.MaxValue)]
    [InlineData(ulong.MaxValue, uint.MaxValue, uint.MaxValue)]
    public void Decode2D_ShouldSplitTheAxesApart_WhenGivenKnownCodes(ulong code, uint expectedX, uint expectedY)
    {
        Assert.Equal((expectedX, expectedY), MortonCurve.Decode2D(code));
    }

    [Fact]
    public void Decode2D_ShouldRecoverTheCoordinate_WhenSweepingASquareExhaustively()
    {
        for (uint y = 0; y < 256; y++)
        {
            for (uint x = 0; x < 256; x++)
            {
                Assert.Equal((x, y), MortonCurve.Decode2D(MortonCurve.Encode2D(x, y)));
            }
        }
    }

    [Fact]
    public void Encode2D_ShouldBeABijection_WhenEveryCodeIsDecodedAndReEncoded()
    {
        // The 2-D mapping fills the 64-bit code exactly, so a code drawn from anywhere in the range must
        // survive the trip in the other direction too — a property the 3-D form deliberately does not have.
        var rand = new Random(4242);
        for (int i = 0; i < 20_000; i++)
        {
            ulong code = ((ulong)(uint)rand.Next() << 32) | (uint)rand.Next();
            var (x, y) = MortonCurve.Decode2D(code);
            Assert.Equal(code, MortonCurve.Encode2D(x, y));
        }
    }

    // ── 3-D encode ───────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0u, 0u, 0u, 0UL)]
    [InlineData(1u, 0u, 0u, 1UL)]
    [InlineData(0u, 1u, 0u, 2UL)]
    [InlineData(0u, 0u, 1u, 4UL)]
    [InlineData(1u, 1u, 1u, 7UL)]
    [InlineData(MortonCurve.MaxCoordinate3D, 0u, 0u, 0x1249_2492_4924_9249UL)]
    [InlineData(MortonCurve.MaxCoordinate3D, MortonCurve.MaxCoordinate3D, MortonCurve.MaxCoordinate3D, 0x7FFF_FFFF_FFFF_FFFFUL)]
    public void Encode3D_ShouldInterleaveThreeAxes_WhenGivenKnownCoordinates(uint x, uint y, uint z, ulong expected)
    {
        Assert.Equal(expected, MortonCurve.Encode3D(x, y, z));
    }

    [Fact]
    public void Encode3D_ShouldLeaveTheTopBitClear_WhenGivenTheLargestCoordinate()
    {
        // Three 21-bit axes reach 63 bits, never 64 — the documented headroom the caller may rely on.
        ulong code = MortonCurve.Encode3D(MortonCurve.MaxCoordinate3D, MortonCurve.MaxCoordinate3D, MortonCurve.MaxCoordinate3D);
        Assert.True(code < 1UL << 63);
    }

    [Fact]
    public void Decode3D_ShouldRecoverTheCoordinate_WhenSweepingOneAxisExhaustively()
    {
        for (uint v = 0; v <= MortonCurve.MaxCoordinate3D; v++)
        {
            Assert.Equal((v, 0u, 0u), MortonCurve.Decode3D(MortonCurve.Encode3D(v, 0, 0)));
        }
    }

    [Fact]
    public void Decode3D_ShouldRecoverTheCoordinate_WhenSweepingACubeExhaustively()
    {
        for (uint z = 0; z < 32; z++)
        {
            for (uint y = 0; y < 32; y++)
            {
                for (uint x = 0; x < 32; x++)
                {
                    Assert.Equal((x, y, z), MortonCurve.Decode3D(MortonCurve.Encode3D(x, y, z)));
                }
            }
        }
    }

    [Fact]
    public void Decode3D_ShouldIgnoreTheUnusedTopBit_WhenItIsSet()
    {
        ulong code = MortonCurve.Encode3D(11, 22, 33);
        Assert.Equal(MortonCurve.Decode3D(code), MortonCurve.Decode3D(code | (1UL << 63)));
    }

    [Theory]
    [InlineData(MortonCurve.MaxCoordinate3D + 1, 0u, 0u, "x")]
    [InlineData(0u, MortonCurve.MaxCoordinate3D + 1, 0u, "y")]
    [InlineData(0u, 0u, MortonCurve.MaxCoordinate3D + 1, "z")]
    [InlineData(uint.MaxValue, 0u, 0u, "x")]
    public void Encode3D_ShouldThrow_WhenACoordinateExceedsTheDomain(uint x, uint y, uint z, string paramName)
    {
        // Masking off the high bits would round-trip a *different* point back to the caller, which is worse
        // than refusing the input outright.
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => MortonCurve.Encode3D(x, y, z));
        Assert.Equal(paramName, ex.ParamName);
    }

    // ── The ordering properties the docs promise ─────────────────────────────────────────

    [Fact]
    public void Encode2D_ShouldStayMonotoneInOneAxis_WhenTheOtherIsHeldFixed()
    {
        var rand = new Random(7);
        for (int i = 0; i < 5_000; i++)
        {
            uint fixedAxis = (uint)rand.Next();
            uint a = (uint)rand.Next();
            uint b = (uint)rand.Next();
            if (a == b)
            {
                continue;
            }

            Assert.Equal(a < b, MortonCurve.Encode2D(a, fixedAxis) < MortonCurve.Encode2D(b, fixedAxis));
            Assert.Equal(a < b, MortonCurve.Encode2D(fixedAxis, a) < MortonCurve.Encode2D(fixedAxis, b));
        }
    }

    [Fact]
    public void Encode2D_ShouldPlaceAnAlignedCellInOneContiguousCodeRange_WhenPointsShareTheirHighBits()
    {
        // The nesting property is what makes a sorted array of codes a spatial index: an aligned 2^k cell
        // must be an interval of codes, not a scattered set.
        const int CellBits = 4;              // 16 x 16 cell
        const uint CellOrigin = 48u << CellBits;

        ulong low = ulong.MaxValue, high = ulong.MinValue;
        for (uint dy = 0; dy < 1u << CellBits; dy++)
        {
            for (uint dx = 0; dx < 1u << CellBits; dx++)
            {
                ulong code = MortonCurve.Encode2D(CellOrigin + dx, CellOrigin + dy);
                low = Math.Min(low, code);
                high = Math.Max(high, code);
            }
        }

        // 256 distinct codes spanning exactly 256 values means the range contains nothing else.
        Assert.Equal((1UL << (2 * CellBits)) - 1, high - low);
    }

    [Fact]
    public void Encode2D_ShouldJumpAQuadrantWidth_WhenConsecutiveCodesCrossOne()
    {
        // The documented weakness, pinned so it is not mistaken for the Hilbert guarantee: code 1 and code 2
        // are a diagonal apart, not a step apart. This is the reason HilbertCurve exists.
        var (x1, y1) = MortonCurve.Decode2D(1);
        var (x2, y2) = MortonCurve.Decode2D(2);
        Assert.Equal(2, Math.Abs((long)x2 - x1) + Math.Abs((long)y2 - y1));
    }
}
