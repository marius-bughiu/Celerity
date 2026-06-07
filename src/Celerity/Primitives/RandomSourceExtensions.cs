using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Celerity.Primitives;

/// <summary>
/// The shared, shape-specific surface (<c>NextUInt32</c>, <c>NextDouble</c>, <c>NextSingle</c>,
/// <c>NextBool</c>, bounded <c>NextInt</c> / <c>NextInt64</c>, <c>NextBytes</c>) built once over any
/// <see cref="IRandomSource"/>'s single <see cref="IRandomSource.NextUInt64"/> primitive.
/// </summary>
/// <remarks>
/// <para>
/// Every method is a <c>ref this</c> extension constrained to <c>where TRng : struct, IRandomSource</c>, so
/// at each call site the concrete generator is known, the <see cref="IRandomSource.NextUInt64"/> call is
/// devirtualized and inlined, and the generator's state is mutated in place with no boxing or allocation —
/// the same zero-cost pattern the struct hashers use. Because the receiver is taken by <c>ref</c>, these
/// can only be called on a mutable variable (a local or field), not on a temporary or readonly value.
/// </para>
/// <para>
/// The bounded-integer methods use Lemire's nearly-divisionless rejection algorithm (Lemire, 2019): a single
/// widening multiply yields an unbiased value in range, and the (rare) rejection branch only fires for the
/// few residues that would skew the distribution, so the common path has no division at all.
/// </para>
/// </remarks>
public static class RandomSourceExtensions
{
    /// <summary>Returns the next uniformly distributed 32-bit value, taken from the strong high bits of
    /// <see cref="IRandomSource.NextUInt64"/> (safe even for generators with weak low bits).</summary>
    /// <typeparam name="TRng">The concrete generator type.</typeparam>
    /// <param name="rng">The generator, advanced in place.</param>
    /// <returns>A pseudo-random <see cref="uint"/> in <c>[0, 2^32)</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint NextUInt32<TRng>(this ref TRng rng) where TRng : struct, IRandomSource
        => (uint)(rng.NextUInt64() >> 32);

    /// <summary>Returns the next double in <c>[0, 1)</c>, using the top 53 bits (the full mantissa) so every
    /// representable value is reachable with the correct probability.</summary>
    /// <typeparam name="TRng">The concrete generator type.</typeparam>
    /// <param name="rng">The generator, advanced in place.</param>
    /// <returns>A pseudo-random <see cref="double"/> in <c>[0.0, 1.0)</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double NextDouble<TRng>(this ref TRng rng) where TRng : struct, IRandomSource
        => (rng.NextUInt64() >> 11) * (1.0 / (1UL << 53));

    /// <summary>Returns the next float in <c>[0, 1)</c>, using the top 24 bits (the full mantissa).</summary>
    /// <typeparam name="TRng">The concrete generator type.</typeparam>
    /// <param name="rng">The generator, advanced in place.</param>
    /// <returns>A pseudo-random <see cref="float"/> in <c>[0.0f, 1.0f)</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float NextSingle<TRng>(this ref TRng rng) where TRng : struct, IRandomSource
        => (rng.NextUInt64() >> 40) * (1.0f / (1U << 24));

    /// <summary>Returns the next boolean from the single top bit of <see cref="IRandomSource.NextUInt64"/>.</summary>
    /// <typeparam name="TRng">The concrete generator type.</typeparam>
    /// <param name="rng">The generator, advanced in place.</param>
    /// <returns>A pseudo-random <see cref="bool"/>, each value equally likely.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NextBool<TRng>(this ref TRng rng) where TRng : struct, IRandomSource
        => (rng.NextUInt64() >> 63) != 0UL;

    /// <summary>Returns an unbiased integer in <c>[0, <paramref name="maxExclusive"/>)</c>.</summary>
    /// <typeparam name="TRng">The concrete generator type.</typeparam>
    /// <param name="rng">The generator, advanced in place.</param>
    /// <param name="maxExclusive">The exclusive upper bound. Must be positive.</param>
    /// <returns>A pseudo-random <see cref="int"/> in <c>[0, maxExclusive)</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxExclusive"/> is not positive.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NextInt<TRng>(this ref TRng rng, int maxExclusive) where TRng : struct, IRandomSource
    {
        if (maxExclusive <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be positive.");

        return (int)NextBoundedUInt32(ref rng, (uint)maxExclusive);
    }

    /// <summary>Returns an unbiased integer in <c>[<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>)</c>.</summary>
    /// <typeparam name="TRng">The concrete generator type.</typeparam>
    /// <param name="rng">The generator, advanced in place.</param>
    /// <param name="minInclusive">The inclusive lower bound.</param>
    /// <param name="maxExclusive">The exclusive upper bound. Must be greater than or equal to <paramref name="minInclusive"/>.</param>
    /// <returns>A pseudo-random <see cref="int"/> in <c>[minInclusive, maxExclusive)</c>; <paramref name="minInclusive"/>
    /// when the two bounds are equal.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxExclusive"/> is less than <paramref name="minInclusive"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NextInt<TRng>(this ref TRng rng, int minInclusive, int maxExclusive)
        where TRng : struct, IRandomSource
    {
        if (maxExclusive < minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive),
                "maxExclusive must be greater than or equal to minInclusive.");

        uint range = (uint)((long)maxExclusive - minInclusive);
        if (range == 0)
            return minInclusive;

        return minInclusive + (int)NextBoundedUInt32(ref rng, range);
    }

    /// <summary>Returns an unbiased 64-bit integer in <c>[<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>)</c>.</summary>
    /// <typeparam name="TRng">The concrete generator type.</typeparam>
    /// <param name="rng">The generator, advanced in place.</param>
    /// <param name="minInclusive">The inclusive lower bound.</param>
    /// <param name="maxExclusive">The exclusive upper bound. Must be greater than or equal to <paramref name="minInclusive"/>.</param>
    /// <returns>A pseudo-random <see cref="long"/> in <c>[minInclusive, maxExclusive)</c>; <paramref name="minInclusive"/>
    /// when the two bounds are equal.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxExclusive"/> is less than <paramref name="minInclusive"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long NextInt64<TRng>(this ref TRng rng, long minInclusive, long maxExclusive)
        where TRng : struct, IRandomSource
    {
        if (maxExclusive < minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive),
                "maxExclusive must be greater than or equal to minInclusive.");

        // For min = long.MinValue, max = long.MaxValue the true difference is 2^64 - 1, which overflows a
        // signed long; the unchecked subtraction reinterpreted as ulong recovers the correct magnitude.
        ulong range = unchecked((ulong)(maxExclusive - minInclusive));
        if (range == 0)
            return minInclusive;

        return unchecked(minInclusive + (long)NextBoundedUInt64(ref rng, range));
    }

    /// <summary>Fills <paramref name="buffer"/> with pseudo-random bytes, eight at a time.</summary>
    /// <typeparam name="TRng">The concrete generator type.</typeparam>
    /// <param name="rng">The generator, advanced in place.</param>
    /// <param name="buffer">The destination span; every byte is overwritten.</param>
    public static void NextBytes<TRng>(this ref TRng rng, Span<byte> buffer) where TRng : struct, IRandomSource
    {
        int i = 0;
        for (; i + sizeof(ulong) <= buffer.Length; i += sizeof(ulong))
            BinaryPrimitives.WriteUInt64LittleEndian(buffer.Slice(i), rng.NextUInt64());

        if (i < buffer.Length)
        {
            ulong tail = rng.NextUInt64();
            for (; i < buffer.Length; i++)
            {
                buffer[i] = (byte)tail;
                tail >>= 8;
            }
        }
    }

    // ── Lemire nearly-divisionless bounded range (range assumed non-zero) ────────────────
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint NextBoundedUInt32<TRng>(ref TRng rng, uint range) where TRng : struct, IRandomSource
    {
        uint x = rng.NextUInt32();
        ulong m = (ulong)x * range;
        uint low = (uint)m;
        if (low < range)
        {
            // threshold = (2^32 - range) % range — the count of low residues that would bias the result.
            uint threshold = (0u - range) % range;
            while (low < threshold)
            {
                x = rng.NextUInt32();
                m = (ulong)x * range;
                low = (uint)m;
            }
        }

        return (uint)(m >> 32);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong NextBoundedUInt64<TRng>(ref TRng rng, ulong range) where TRng : struct, IRandomSource
    {
        ulong x = rng.NextUInt64();
        UInt128 m = (UInt128)x * range;
        ulong low = (ulong)m;
        if (low < range)
        {
            // threshold = (2^64 - range) % range.
            ulong threshold = (0ul - range) % range;
            while (low < threshold)
            {
                x = rng.NextUInt64();
                m = (UInt128)x * range;
                low = (ulong)m;
            }
        }

        return (ulong)(m >> 64);
    }
}
