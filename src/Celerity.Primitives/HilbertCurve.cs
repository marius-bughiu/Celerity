using System;

namespace Celerity.Primitives;

/// <summary>
/// The Hilbert space-filling curve: maps a 2-D or 3-D integer coordinate to a single <see cref="ulong"/>
/// index along the curve, and back (issue #369).
/// </summary>
/// <remarks>
/// <para>
/// Hilbert is the sibling of <see cref="MortonCurve"/> and answers the same question — how do I turn a
/// coordinate into one number so that nearby points get nearby numbers — with strictly better locality,
/// for more work per conversion.
/// </para>
/// <para>
/// <strong>The property that distinguishes it, and the only reason to pay for it:</strong> consecutive
/// indices are <em>neighbouring cells</em>. Step the index by one and the coordinate moves by exactly one
/// unit along exactly one axis, at every scale, for every index below the last one —
/// <see cref="ulong.MaxValue"/> in 2-D and <c>2^63 - 1</c> in 3-D. The index space is finite, so the last
/// index has no successor: incrementing it wraps to <c>0</c> and lands back at the origin, which is a wrap
/// rather than a step along the curve. Morton cannot
/// promise that — its Z pattern jumps a whole quadrant every time it crosses one — which is why Morton
/// is the better locality-preserving <em>sort key</em> and Hilbert the better order to back a
/// <em>range query</em>: a contiguous run of Hilbert indices is a compact, connected region of the
/// plane rather than a set of scattered strips.
/// </para>
/// <para>
/// What both curves share: an aligned power-of-two cell is a contiguous range of indices, so a sorted
/// array of indices is a usable spatial index either way. What Hilbert gives up is the per-axis
/// monotonicity Morton has — the index does <em>not</em> increase with one coordinate while the other is
/// held fixed, because the curve reverses direction as it folds.
/// </para>
/// <para>
/// <strong>Orientation.</strong> The order-<em>b</em> curve starts at the origin and ends at
/// <c>(2^b - 1, 0)</c>, the conventional orientation.
/// </para>
/// <para>
/// <strong>Precision.</strong> 2-D covers the whole <see cref="uint"/> range on both axes — 32 bits per
/// axis, a bijection onto <see cref="ulong"/>. 3-D packs three axes into the same 64 bits, so it is
/// limited to 21 bits per axis (<see cref="MaxCoordinate3D"/>) and produces a 63-bit index; bit 63 is
/// always clear.
/// </para>
/// <para>
/// <strong>Sub-regions still work.</strong> The curve is self-similar, so coordinates confined to an
/// aligned <c>2^k</c>-sided sub-square still map into one contiguous, connected run of indices — you do
/// not need an order parameter to use this on a smaller universe. What you do <em>not</em> get is
/// agreement with the order-<em>k</em> curve computed independently: the sub-square is traversed in
/// whatever rotation the enclosing curve reaches it in.
/// </para>
/// <para>
/// Every method is static, allocation-free (the transform runs entirely in registers) and AOT-safe. A
/// conversion is a loop over the bit levels, so it costs meaningfully more
/// than <see cref="MortonCurve"/>'s straight-line bit spread; reach for Morton unless the adjacency
/// property is the point.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ulong index = HilbertCurve.Encode2D(x: 3, y: 5);
/// var (x, y) = HilbertCurve.Decode2D(index);   // (3, 5)
///
/// // Consecutive indices are neighbouring cells, at every scale — up to the final index, which has no
/// // successor to step to.
/// if (index &lt; ulong.MaxValue)
/// {
///     var (nx, ny) = HilbertCurve.Decode2D(index + 1);
/// }
/// </code>
/// </example>
public static class HilbertCurve
{
    /// <summary>
    /// The largest coordinate <see cref="Encode3D"/> accepts on each axis — <c>2^21 - 1</c>
    /// (<c>2,097,151</c>), the same 21-bit budget <see cref="MortonCurve.MaxCoordinate3D"/> imposes.
    /// </summary>
    public const uint MaxCoordinate3D = MortonCurve.MaxCoordinate3D;

    // Bits per axis. 2-D fills the 64-bit index exactly; 3-D leaves bit 63 unused.
    private const int Bits2D = 32;
    private const int Bits3D = 21;

    /// <summary>Maps a 2-D coordinate to its index along the Hilbert curve.</summary>
    /// <param name="x">The first-axis coordinate. The whole <see cref="uint"/> range is valid.</param>
    /// <param name="y">The second-axis coordinate. The whole <see cref="uint"/> range is valid.</param>
    /// <returns>The 64-bit Hilbert index. The mapping is a bijection, so no two coordinates collide.</returns>
    public static ulong Encode2D(uint x, uint y)
    {
        AxesToTranspose2D(ref x, ref y);

        // The index interleaves the transposed axes most-significant-bit first, which is exactly a
        // Morton interleave of them — the first axis supplies the odd bit positions, the second the even.
        return MortonCurve.Encode2D(y, x);
    }

    /// <summary>Maps a 2-D Hilbert index back to the coordinate that produced it.</summary>
    /// <param name="index">An index produced by <see cref="Encode2D"/>. Every <see cref="ulong"/> is a valid index.</param>
    /// <returns>The <c>(X, Y)</c> pair.</returns>
    public static (uint X, uint Y) Decode2D(ulong index)
    {
        var (a1, a0) = MortonCurve.Decode2D(index);
        TransposeToAxes2D(ref a0, ref a1);
        return (a0, a1);
    }

    /// <summary>Maps a 3-D coordinate to its index along the Hilbert curve.</summary>
    /// <param name="x">The first-axis coordinate, in <c>[0, <see cref="MaxCoordinate3D"/>]</c>.</param>
    /// <param name="y">The second-axis coordinate, in <c>[0, <see cref="MaxCoordinate3D"/>]</c>.</param>
    /// <param name="z">The third-axis coordinate, in <c>[0, <see cref="MaxCoordinate3D"/>]</c>.</param>
    /// <returns>The 63-bit Hilbert index; bit 63 is always clear.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Any coordinate exceeds <see cref="MaxCoordinate3D"/>.</exception>
    public static ulong Encode3D(uint x, uint y, uint z)
    {
        ThrowIfOutOfRange3D(x, nameof(x));
        ThrowIfOutOfRange3D(y, nameof(y));
        ThrowIfOutOfRange3D(z, nameof(z));

        AxesToTranspose3D(ref x, ref y, ref z);
        return MortonCurve.Encode3D(z, y, x);
    }

    /// <summary>Maps a 3-D Hilbert index back to the coordinate that produced it.</summary>
    /// <param name="index">
    /// An index produced by <see cref="Encode3D"/>. Bit 63 carries no axis and is ignored, so every
    /// <see cref="ulong"/> decodes.
    /// </param>
    /// <returns>The <c>(X, Y, Z)</c> triple.</returns>
    public static (uint X, uint Y, uint Z) Decode3D(ulong index)
    {
        var (a2, a1, a0) = MortonCurve.Decode3D(index);
        TransposeToAxes3D(ref a0, ref a1, ref a2);
        return (a0, a1, a2);
    }

    // Skilling's in-place transform (Programming the Hilbert curve, AIP Conf. Proc. 707, 2004): rewrites
    // the axes into the "transpose" form of the Hilbert index, whose bits interleave to give the index
    // itself. It is the inverse of TransposeToAxes2D, step for step and in the opposite order.
    //
    // Two departures from the published form, both measured rather than assumed. The axis loop is written
    // out per arity instead of running over a Span<uint>, which removes a bounds check from each of the
    // ~200 accesses a conversion makes. And every per-level decision goes through Branchless.Select: the
    // deciding bit is one bit of a coordinate, so the branch is a coin flip the predictor cannot learn,
    // and the mispredictions — not the arithmetic — were most of what a conversion cost.
    //
    // The first axis is Skilling's accumulator, so its own iteration of the axis loop degenerates:
    // exchanging a value with itself masks to zero, leaving only the reflection.
    private static void AxesToTranspose2D(ref uint a0, ref uint a1)
    {
        for (int level = Bits2D - 1; level >= 1; level--)
        {
            uint q = 1u << level;
            uint p = q - 1;

            a0 ^= Branchless.Select((a0 & q) != 0, p, 0u);

            uint t = (a0 ^ a1) & p;
            bool reflect = (a1 & q) != 0;
            a0 ^= Branchless.Select(reflect, p, t);
            a1 ^= Branchless.Select(reflect, 0u, t);
        }

        a1 ^= a0;                                    // Gray encode.

        uint carry = GrayCarry(a1, Bits2D);
        a0 ^= carry;
        a1 ^= carry;
    }

    // Skilling's inverse transform: rewrites the transposed index back into plain coordinates.
    private static void TransposeToAxes2D(ref uint a0, ref uint a1)
    {
        uint t = a1 >> 1;                            // Gray decode by H ^ (H / 2).
        a1 ^= a0;
        a0 ^= t;

        // Unwind each level's reflection and exchange, coarsest level first and last axis first.
        for (int level = 1; level < Bits2D; level++)
        {
            uint q = 1u << level;
            uint p = q - 1;

            uint s = (a0 ^ a1) & p;
            bool reflect = (a1 & q) != 0;
            a0 ^= Branchless.Select(reflect, p, s);
            a1 ^= Branchless.Select(reflect, 0u, s);

            a0 ^= Branchless.Select((a0 & q) != 0, p, 0u);
        }
    }

    private static void AxesToTranspose3D(ref uint a0, ref uint a1, ref uint a2)
    {
        for (int level = Bits3D - 1; level >= 1; level--)
        {
            uint q = 1u << level;
            uint p = q - 1;

            a0 ^= Branchless.Select((a0 & q) != 0, p, 0u);

            uint t1 = (a0 ^ a1) & p;
            bool reflect1 = (a1 & q) != 0;
            a0 ^= Branchless.Select(reflect1, p, t1);
            a1 ^= Branchless.Select(reflect1, 0u, t1);

            uint t2 = (a0 ^ a2) & p;
            bool reflect2 = (a2 & q) != 0;
            a0 ^= Branchless.Select(reflect2, p, t2);
            a2 ^= Branchless.Select(reflect2, 0u, t2);
        }

        a1 ^= a0;                                    // Gray encode, in ascending axis order.
        a2 ^= a1;

        uint carry = GrayCarry(a2, Bits3D);
        a0 ^= carry;
        a1 ^= carry;
        a2 ^= carry;
    }

    private static void TransposeToAxes3D(ref uint a0, ref uint a1, ref uint a2)
    {
        uint t = a2 >> 1;                            // Gray decode, in descending axis order.
        a2 ^= a1;
        a1 ^= a0;
        a0 ^= t;

        for (int level = 1; level < Bits3D; level++)
        {
            uint q = 1u << level;
            uint p = q - 1;

            uint s2 = (a0 ^ a2) & p;
            bool reflect2 = (a2 & q) != 0;
            a0 ^= Branchless.Select(reflect2, p, s2);
            a2 ^= Branchless.Select(reflect2, 0u, s2);

            uint s1 = (a0 ^ a1) & p;
            bool reflect1 = (a1 & q) != 0;
            a0 ^= Branchless.Select(reflect1, p, s1);
            a1 ^= Branchless.Select(reflect1, 0u, s1);

            a0 ^= Branchless.Select((a0 & q) != 0, p, 0u);
        }
    }

    // The reflection the Gray encoding leaves owing, accumulated from the last axis: one low-bit mask per
    // level that axis has set.
    private static uint GrayCarry(uint last, int bits)
    {
        uint carry = 0;
        for (int level = bits - 1; level >= 1; level--)
        {
            uint q = 1u << level;
            carry ^= Branchless.Select((last & q) != 0, q - 1, 0u);
        }

        return carry;
    }

    private static void ThrowIfOutOfRange3D(uint value, string paramName)
    {
        if (value > MaxCoordinate3D)
            throw new ArgumentOutOfRangeException(paramName, value, $"A 3-D coordinate must be in [0, {MaxCoordinate3D}].");
    }
}
