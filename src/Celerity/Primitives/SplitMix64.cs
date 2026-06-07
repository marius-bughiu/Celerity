using System.Runtime.CompilerServices;

namespace Celerity.Primitives;

/// <summary>
/// SplitMix64 — a minimal, extremely fast 64-bit generator with a single <see cref="ulong"/> of state,
/// canonical as the <strong>seed expander</strong> for the larger-state generators (xoshiro / xoroshiro)
/// and usable on its own for non-critical randomness.
/// </summary>
/// <remarks>
/// <para>
/// Each step adds the fixed odd increment <c>0x9E3779B97F4A7C15</c> (the 64-bit golden ratio) to the state
/// and runs a strong avalanche finalizer (Vigna's variant of the MurmurHash3 mix). The function is a
/// bijection over the full 2^64 state, so the period is exactly 2^64 and every seed is valid — including
/// <c>0</c>, which is why it is the safe way to expand one user seed into the multi-word state of
/// <see cref="Xoshiro256StarStar"/> / <see cref="Xoroshiro128Plus"/> (whose all-zero state is degenerate).
/// </para>
/// <para>
/// SplitMix64 has only 64 bits of state, so it cannot be used where a longer period or higher-dimensional
/// equidistribution matters; for general-purpose work prefer <see cref="Xoshiro256StarStar"/>. It is a
/// mutable <see langword="struct"/>: copying it forks the stream.
/// </para>
/// </remarks>
public struct SplitMix64 : IRandomSource
{
    private ulong _state;

    /// <summary>
    /// Creates a generator seeded with <paramref name="seed"/>. Every <see cref="ulong"/> seed is valid,
    /// including <c>0</c>; the same seed always yields the same sequence.
    /// </summary>
    /// <param name="seed">The initial 64-bit state.</param>
    public SplitMix64(ulong seed)
    {
        _state = seed;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong NextUInt64()
    {
        ulong z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
