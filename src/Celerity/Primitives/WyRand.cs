using System.Runtime.CompilerServices;

namespace Celerity.Primitives;

/// <summary>
/// wyrand — the raw-throughput pick: a single <see cref="ulong"/> of state advanced by an add and folded
/// through one 64×64→128-bit multiply (the mixing core of Wang Yi's wyhash). It is typically the fastest
/// generator in the suite and passes PractRand to large sizes, making it the choice when you need a flood
/// of decent-quality numbers (procedural generation, sampling, jitter) and a 2^64 period is enough.
/// </summary>
/// <remarks>
/// <para>
/// Each step adds the fixed constant <c>0xA0761D6478BD642F</c> to the state, multiplies the result by an
/// xor-perturbed copy of itself, and returns the high and low halves of the 128-bit product xored together.
/// Because the state advance is a pure 64-bit add it has a full period of 2^64; every seed is valid,
/// including <c>0</c>.
/// </para>
/// <para>
/// With only 64 bits of state it does not offer the equidistribution guarantees of
/// <see cref="Xoshiro256StarStar"/>; for the general-purpose default prefer the latter. It is a mutable
/// <see langword="struct"/>: copying it forks the stream.
/// </para>
/// </remarks>
public struct WyRand : IRandomSource
{
    private ulong _state;

    /// <summary>
    /// Creates a generator seeded with <paramref name="seed"/>. Every seed is valid (including <c>0</c>);
    /// the same seed always yields the same sequence.
    /// </summary>
    /// <param name="seed">The initial 64-bit state.</param>
    public WyRand(ulong seed)
    {
        _state = seed;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong NextUInt64()
    {
        _state += 0xA0761D6478BD642FUL;
        UInt128 t = (UInt128)_state * (_state ^ 0xE7037ED1A0B428DBUL);
        return (ulong)(t >> 64) ^ (ulong)t;
    }
}
