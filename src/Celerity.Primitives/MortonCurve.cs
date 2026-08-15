using System;
using System.Runtime.CompilerServices;

namespace Celerity.Primitives;

/// <summary>
/// The Z-order (Morton) space-filling curve: interleaves the bits of a 2-D or 3-D integer coordinate
/// into a single <see cref="ulong"/> key, and de-interleaves that key back into the coordinate
/// (issue #369).
/// </summary>
/// <remarks>
/// <para>
/// A space-filling curve is what lets a <strong>one-dimensional</strong> structure answer a spatially
/// local question. Sort points by their Morton code and a plain sorted array, a
/// <c>BTreeSet&lt;T&gt;</c> or a <see cref="SortedSpan"/> becomes a cache-coherent spatial container;
/// the same order is the standard packing order for bulk-loading a bounding-volume index, and the
/// standard way to build a tile / cell identifier that survives a sort or a hash-partition.
/// </para>
/// <para>
/// <strong>The BCL has no bit-interleave.</strong> <see cref="System.Numerics.BitOperations"/> ships
/// popcount, leading/trailing-zero counts and rotates, but nothing that scatters a value's bits across
/// a mask — so a caller who wants a Morton code has to write the magic-number sequence themselves.
/// </para>
/// <para><strong>What the ordering does and does not promise.</strong></para>
/// <list type="bullet">
/// <item><description>
/// <strong>Cells nest.</strong> Two points sharing the top <c>k</c> bits of every coordinate share the
/// top <c>2k</c> (2-D) or <c>3k</c> (3-D) bits of their code, so an aligned power-of-two cell is a
/// <em>contiguous range</em> of codes. That is what makes a sorted array of codes a usable index.
/// </description></item>
/// <item><description>
/// <strong>Each axis stays monotone.</strong> With the other coordinates held fixed, the code increases
/// with the coordinate.
/// </description></item>
/// <item><description>
/// <strong>Consecutive codes are <em>not</em> always adjacent cells.</strong> The Z pattern jumps the
/// width of a quadrant every time it crosses one — the well-known "Z jump". Where that matters (a range
/// query backed by the order, rather than merely a locality-friendly sort key), use
/// <see cref="HilbertCurve"/>, whose consecutive codes are always neighbouring cells. Morton is the
/// cheaper of the two and the right default when the consumer only needs <em>a</em> locality-preserving
/// order.
/// </description></item>
/// </list>
/// <para>
/// <strong>Precision.</strong> 2-D encoding is lossless over the whole <see cref="uint"/> range: 32 bits
/// per axis fill the 64-bit code exactly, and <see cref="Encode2D"/> is a bijection onto
/// <see cref="ulong"/>. 3-D packs three axes into the same 64 bits, so it is limited to 21 bits per axis
/// (<see cref="MaxCoordinate3D"/>) and produces a 63-bit code; bit 63 is always clear.
/// </para>
/// <para>
/// Every method is static, allocation-free and AOT-safe, and the spread / compact arithmetic itself is
/// branch-free — <see cref="Encode3D"/> adds the three range checks its domain guard needs, and nothing
/// else branches. The implementation is the portable
/// magic-number bit-spread rather than the x86 <c>BMI2</c> <c>PDEP</c> / <c>PEXT</c> pair: see the
/// <see href="https://github.com/marius-bughiu/Celerity/blob/main/docs/api/utilities.md">API reference</see>
/// for the measurement and the reasoning behind that choice.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// ulong code = MortonCurve.Encode2D(x: 3, y: 5);
/// var (x, y) = MortonCurve.Decode2D(code);   // (3, 5)
///
/// // Sorting by the code groups spatially-near points together in memory.
/// Array.Sort(codes, points);
/// </code>
/// </example>
public static class MortonCurve
{
    /// <summary>
    /// The largest coordinate <see cref="Encode3D"/> accepts on each axis — <c>2^21 - 1</c>
    /// (<c>2,097,151</c>). Three 21-bit axes are what fits in one 64-bit code.
    /// </summary>
    public const uint MaxCoordinate3D = (1u << 21) - 1;

    // Every other bit / every third bit: the positions one axis occupies in a 2-D and a 3-D code.
    private const ulong EveryOtherBit = 0x5555_5555_5555_5555UL;
    private const ulong EveryThirdBit = 0x1249_2492_4924_9249UL;

    /// <summary>
    /// Interleaves <paramref name="x"/> and <paramref name="y"/> into a single Z-order code, with
    /// <paramref name="x"/> on the even bit positions and <paramref name="y"/> on the odd ones.
    /// </summary>
    /// <param name="x">The first-axis coordinate. The whole <see cref="uint"/> range is valid.</param>
    /// <param name="y">The second-axis coordinate. The whole <see cref="uint"/> range is valid.</param>
    /// <returns>The 64-bit Morton code. The mapping is a bijection, so no two coordinates collide.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Encode2D(uint x, uint y) => Spread2(x) | (Spread2(y) << 1);

    /// <summary>De-interleaves a 2-D Morton code back into the coordinate that produced it.</summary>
    /// <param name="code">A code produced by <see cref="Encode2D"/>. Every <see cref="ulong"/> is a valid code.</param>
    /// <returns>The <c>(X, Y)</c> pair, such that <c>Encode2D(Decode2D(code)) == code</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (uint X, uint Y) Decode2D(ulong code) => (Compact2(code), Compact2(code >> 1));

    /// <summary>
    /// Interleaves <paramref name="x"/>, <paramref name="y"/> and <paramref name="z"/> into a single
    /// Z-order code, one axis per bit position modulo three.
    /// </summary>
    /// <param name="x">The first-axis coordinate, in <c>[0, <see cref="MaxCoordinate3D"/>]</c>.</param>
    /// <param name="y">The second-axis coordinate, in <c>[0, <see cref="MaxCoordinate3D"/>]</c>.</param>
    /// <param name="z">The third-axis coordinate, in <c>[0, <see cref="MaxCoordinate3D"/>]</c>.</param>
    /// <returns>The 63-bit Morton code; bit 63 is always clear.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Any coordinate exceeds <see cref="MaxCoordinate3D"/>. Silently keeping the low 21 bits would break
    /// the round-trip contract on exactly the input a caller got wrong, so it is rejected instead.
    /// </exception>
    public static ulong Encode3D(uint x, uint y, uint z)
    {
        ThrowIfOutOfRange3D(x, nameof(x));
        ThrowIfOutOfRange3D(y, nameof(y));
        ThrowIfOutOfRange3D(z, nameof(z));

        return Spread3(x) | (Spread3(y) << 1) | (Spread3(z) << 2);
    }

    /// <summary>De-interleaves a 3-D Morton code back into the coordinate that produced it.</summary>
    /// <param name="code">
    /// A code produced by <see cref="Encode3D"/>. Bit 63 carries no axis and is ignored, so every
    /// <see cref="ulong"/> decodes.
    /// </param>
    /// <returns>The <c>(X, Y, Z)</c> triple, such that <c>Encode3D(Decode3D(code)) == code</c> for any code below 2^63.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (uint X, uint Y, uint Z) Decode3D(ulong code)
        => (Compact3(code), Compact3(code >> 1), Compact3(code >> 2));

    // Scatters 32 bits across the even bit positions of a 64-bit word: b31..b0 -> b31 0 b30 0 ... b0.
    // Each step doubles the gap between the halves it has already separated.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Spread2(uint value)
    {
        ulong v = value;
        v = (v | (v << 16)) & 0x0000_FFFF_0000_FFFFUL;
        v = (v | (v << 8)) & 0x00FF_00FF_00FF_00FFUL;
        v = (v | (v << 4)) & 0x0F0F_0F0F_0F0F_0F0FUL;
        v = (v | (v << 2)) & 0x3333_3333_3333_3333UL;
        v = (v | (v << 1)) & EveryOtherBit;
        return v;
    }

    // The inverse of Spread2: gathers the even bit positions back into the low 32 bits.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Compact2(ulong code)
    {
        ulong v = code & EveryOtherBit;
        v = (v | (v >> 1)) & 0x3333_3333_3333_3333UL;
        v = (v | (v >> 2)) & 0x0F0F_0F0F_0F0F_0F0FUL;
        v = (v | (v >> 4)) & 0x00FF_00FF_00FF_00FFUL;
        v = (v | (v >> 8)) & 0x0000_FFFF_0000_FFFFUL;
        v = (v | (v >> 16)) & 0x0000_0000_FFFF_FFFFUL;
        return (uint)v;
    }

    // Scatters 21 bits across every third bit position. The first step lifts the top five bits clear of
    // the low sixteen in one move, which is what keeps the sequence at five steps rather than six.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Spread3(uint value)
    {
        ulong v = value & MaxCoordinate3D;
        v = (v | (v << 32)) & 0x001F_0000_0000_FFFFUL;
        v = (v | (v << 16)) & 0x001F_0000_FF00_00FFUL;
        v = (v | (v << 8)) & 0x100F_00F0_0F00_F00FUL;
        v = (v | (v << 4)) & 0x10C3_0C30_C30C_30C3UL;
        v = (v | (v << 2)) & EveryThirdBit;
        return v;
    }

    // The inverse of Spread3.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Compact3(ulong code)
    {
        ulong v = code & EveryThirdBit;
        v = (v | (v >> 2)) & 0x10C3_0C30_C30C_30C3UL;
        v = (v | (v >> 4)) & 0x100F_00F0_0F00_F00FUL;
        v = (v | (v >> 8)) & 0x001F_0000_FF00_00FFUL;
        v = (v | (v >> 16)) & 0x001F_0000_0000_FFFFUL;
        v = (v | (v >> 32)) & MaxCoordinate3D;
        return (uint)v;
    }

    private static void ThrowIfOutOfRange3D(uint value, string paramName)
    {
        if (value > MaxCoordinate3D)
            throw new ArgumentOutOfRangeException(paramName, value, $"A 3-D coordinate must be in [0, {MaxCoordinate3D}].");
    }
}
