using System;
using Celerity.Primitives;
using CsCheck;

namespace Celerity.Tests.Utils;

/// <summary>
/// Randomized reconciliation of <see cref="MortonCurve"/> and <see cref="HilbertCurve"/> against
/// independent oracles (issue #369).
///
/// <para>
/// The shipped implementations are the fast ones — a five-step magic-number bit spread for Morton, and
/// Skilling's transpose transform for Hilbert. Neither is readable enough to check by eye, and both are
/// the kind of code that is wrong in exactly one lane or at exactly one level. So each is driven against
/// a deliberately slow, obviously-correct alternative over generated input: Morton against a bit-by-bit
/// interleave, and Hilbert's 2-D form against the Lam–Shapiro rotation algorithm, which arrives at the
/// same curve by a completely different route (a quadrant walk with reflections, rather than a Gray-code
/// transform). A bug shared by both would have to be a bug in the definition of the curve.
/// </para>
///
/// <para>
/// The 3-D Hilbert form has no comparably simple second algorithm, so it is held to the curve's defining
/// properties instead: the mapping is injective over a generated sample, it stays inside the declared
/// domain, and consecutive indices are neighbouring cells.
/// </para>
/// </summary>
public class SpaceFillingCurveDifferentialTests
{
    private static readonly Gen<(uint X, uint Y)> GenPoint2D = Gen.Select(Gen.UInt, Gen.UInt);

    private static readonly Gen<(uint X, uint Y, uint Z)> GenPoint3D =
        Gen.Select(Gen.UInt[0, MortonCurve.MaxCoordinate3D], Gen.UInt[0, MortonCurve.MaxCoordinate3D], Gen.UInt[0, MortonCurve.MaxCoordinate3D]);

    // ── Morton against a bit-by-bit interleave ───────────────────────────────────────────

    [Fact]
    public void MortonEncode2D_ShouldMatchABitByBitInterleave_OverGeneratedPoints()
    {
        GenPoint2D.Sample(point =>
        {
            Assert.Equal(NaiveInterleave2D(point.X, point.Y), MortonCurve.Encode2D(point.X, point.Y));
        }, iter: 10_000);
    }

    [Fact]
    public void MortonDecode2D_ShouldInvertTheEncode_OverGeneratedCodes()
    {
        Gen.ULong.Sample(code =>
        {
            var (x, y) = MortonCurve.Decode2D(code);
            Assert.Equal(code, NaiveInterleave2D(x, y));
        }, iter: 10_000);
    }

    [Fact]
    public void MortonEncode3D_ShouldMatchABitByBitInterleave_OverGeneratedPoints()
    {
        GenPoint3D.Sample(point =>
        {
            Assert.Equal(NaiveInterleave3D(point.X, point.Y, point.Z), MortonCurve.Encode3D(point.X, point.Y, point.Z));
        }, iter: 10_000);
    }

    [Fact]
    public void MortonDecode3D_ShouldInvertTheEncode_OverGeneratedPoints()
    {
        GenPoint3D.Sample(point =>
        {
            Assert.Equal(point, MortonCurve.Decode3D(MortonCurve.Encode3D(point.X, point.Y, point.Z)));
        }, iter: 10_000);
    }

    // ── Hilbert 2-D against the Lam–Shapiro rotation algorithm ───────────────────────────

    [Fact]
    public void HilbertEncode2D_ShouldMatchTheRotationAlgorithm_OverGeneratedPoints()
    {
        GenPoint2D.Sample(point =>
        {
            Assert.Equal(NaiveHilbertEncode2D(point.X, point.Y), HilbertCurve.Encode2D(point.X, point.Y));
        }, iter: 10_000);
    }

    [Fact]
    public void HilbertDecode2D_ShouldMatchTheRotationAlgorithm_OverGeneratedIndices()
    {
        Gen.ULong.Sample(index =>
        {
            Assert.Equal(NaiveHilbertDecode2D(index), HilbertCurve.Decode2D(index));
        }, iter: 10_000);
    }

    // ── Hilbert 3-D held to the curve's defining properties ──────────────────────────────

    [Fact]
    public void HilbertEncode3D_ShouldRoundTripInsideTheDomain_OverGeneratedPoints()
    {
        GenPoint3D.Sample(point =>
        {
            ulong index = HilbertCurve.Encode3D(point.X, point.Y, point.Z);
            Assert.True(index < 1UL << 63);
            Assert.Equal(point, HilbertCurve.Decode3D(index));
        }, iter: 10_000);
    }

    [Fact]
    public void HilbertDecode3D_ShouldMoveOneCellPerIndex_OverGeneratedRuns()
    {
        // Adjacency is scale-invariant, so a run drawn anywhere in the index space is as strong a check as a
        // run from the front of the curve — and far more likely to catch a level-specific mistake.
        Gen.ULong[0, (1UL << 63) - 64].Sample(start =>
        {
            var (x, y, z) = HilbertCurve.Decode3D(start);
            for (ulong i = 1; i <= 32; i++)
            {
                var (nx, ny, nz) = HilbertCurve.Decode3D(start + i);
                long step = Math.Abs((long)nx - x) + Math.Abs((long)ny - y) + Math.Abs((long)nz - z);
                Assert.Equal(1L, step);
                (x, y, z) = (nx, ny, nz);
            }
        }, iter: 2_000);
    }

    [Fact]
    public void HilbertDecode2D_ShouldMoveOneCellPerIndex_OverGeneratedRuns()
    {
        Gen.ULong[0, ulong.MaxValue - 64].Sample(start =>
        {
            var (x, y) = HilbertCurve.Decode2D(start);
            for (ulong i = 1; i <= 32; i++)
            {
                var (nx, ny) = HilbertCurve.Decode2D(start + i);
                long step = Math.Abs((long)nx - x) + Math.Abs((long)ny - y);
                Assert.Equal(1L, step);
                (x, y) = (nx, ny);
            }
        }, iter: 2_000);
    }

    // ── Oracles ──────────────────────────────────────────────────────────────────────────

    // One bit at a time, no magic numbers: the definition of a Z-order interleave written out.
    private static ulong NaiveInterleave2D(uint x, uint y)
    {
        ulong code = 0;
        for (int bit = 0; bit < 32; bit++)
        {
            code |= (ulong)((x >> bit) & 1) << (2 * bit);
            code |= (ulong)((y >> bit) & 1) << ((2 * bit) + 1);
        }

        return code;
    }

    private static ulong NaiveInterleave3D(uint x, uint y, uint z)
    {
        ulong code = 0;
        for (int bit = 0; bit < 21; bit++)
        {
            code |= (ulong)((x >> bit) & 1) << (3 * bit);
            code |= (ulong)((y >> bit) & 1) << ((3 * bit) + 1);
            code |= (ulong)((z >> bit) & 1) << ((3 * bit) + 2);
        }

        return code;
    }

    // The Lam–Shapiro quadrant walk: at each scale, work out which quadrant the point is in, add that
    // quadrant's share of the index, then rotate the frame so the next scale down is the canonical case.
    // Structurally unrelated to Skilling's transform, which is what makes it a useful oracle.
    private static ulong NaiveHilbertEncode2D(uint x, uint y)
    {
        ulong index = 0;
        for (int bit = 31; bit >= 0; bit--)
        {
            uint side = 1u << bit;
            uint rx = (x & side) != 0 ? 1u : 0u;
            uint ry = (y & side) != 0 ? 1u : 0u;
            index += (ulong)side * side * ((3 * rx) ^ ry);
            Rotate(uint.MaxValue, ref x, ref y, rx, ry);
        }

        return index;
    }

    private static (uint X, uint Y) NaiveHilbertDecode2D(ulong index)
    {
        uint x = 0, y = 0;
        ulong remaining = index;
        for (int bit = 0; bit < 32; bit++)
        {
            uint side = 1u << bit;
            uint rx = (uint)(remaining >> 1) & 1;
            uint ry = (uint)(remaining ^ rx) & 1;
            Rotate(side - 1, ref x, ref y, rx, ry);
            x += side * rx;
            y += side * ry;
            remaining >>= 2;
        }

        return (x, y);
    }

    // Reflect and transpose the frame. `last` is the largest coordinate of the frame being reflected, so a
    // reflection is `last - value` — for the encode direction that frame is the whole domain.
    private static void Rotate(uint last, ref uint x, ref uint y, uint rx, uint ry)
    {
        if (ry != 0)
        {
            return;
        }

        if (rx == 1)
        {
            x = last - x;
            y = last - y;
        }

        (x, y) = (y, x);
    }
}
