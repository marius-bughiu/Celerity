using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Primitives;

/// <summary>
/// PCG32 (PCG-XSH-RR, O'Neill 2014) — the statistical-reputation pick: a 64-bit LCG whose output is
/// permuted by an xorshift-then-random-rotation step, giving 32-bit outputs that pass the standard test
/// batteries from a small, simple core. It additionally supports <strong>independent streams</strong>: two
/// instances with the same seed but different sequence selectors produce uncorrelated sequences.
/// </summary>
/// <remarks>
/// <para>
/// The native output of PCG32 is 32 bits; <see cref="NextUInt64"/> concatenates two successive 32-bit
/// outputs (advancing the LCG twice) to satisfy <see cref="IRandomSource"/>, so the derived
/// <see cref="RandomSourceExtensions"/> surface works uniformly across the suite. If you only need 32-bit
/// values, call <see cref="NextUInt32"/> directly to avoid the extra advance. PCG is chosen for output
/// quality and the stream feature rather than raw speed — for maximum throughput use <see cref="WyRand"/>,
/// and for the general-purpose 64-bit default use <see cref="Xoshiro256StarStar"/>.
/// </para>
/// <para>
/// It is a mutable <see langword="struct"/>: copying it forks the stream.
/// </para>
/// </remarks>
public struct Pcg32 : IRandomSource
{
    private const ulong Multiplier = 6364136223846793005UL;

    private ulong _state;
    private readonly ulong _increment;

    /// <summary>
    /// Creates a generator seeded with <paramref name="seed"/> on the default stream. Every seed is valid
    /// (including <c>0</c>); the same seed always yields the same sequence.
    /// </summary>
    /// <param name="seed">The 64-bit seed.</param>
    public Pcg32(ulong seed)
        : this(seed, 0xDA3E39CB94B95BDBUL)
    {
    }

    /// <summary>
    /// Creates a generator seeded with <paramref name="seed"/> on the independent stream selected by
    /// <paramref name="sequence"/>. Two generators sharing a seed but differing in <paramref name="sequence"/>
    /// produce uncorrelated sequences, which is the standard way to give each worker / shard its own
    /// reproducible stream.
    /// </summary>
    /// <param name="seed">The 64-bit seed.</param>
    /// <param name="sequence">The stream selector; any value distinguishes one stream from another.</param>
    public Pcg32(ulong seed, ulong sequence)
    {
        // Standard PCG seeding: the increment must be odd, so it is (sequence << 1) | 1.
        _increment = (sequence << 1) | 1UL;
        _state = 0UL;
        Step();
        _state += seed;
        Step();
    }

    /// <summary>
    /// Advances the generator state and returns the next uniformly distributed 32-bit value — the native
    /// output width of PCG32.
    /// </summary>
    /// <returns>The next pseudo-random <see cref="uint"/> in <c>[0, 2^32)</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint NextUInt32()
    {
        ulong oldState = _state;
        Step();

        // XSH-RR output permutation: xorshift-high then a data-dependent rotate.
        uint xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
        int rot = (int)(oldState >> 59);
        return BitOperations.RotateRight(xorShifted, rot);
    }

    /// <inheritdoc />
    /// <remarks>Concatenates two successive 32-bit outputs (high word first), advancing the LCG twice.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong NextUInt64()
    {
        ulong hi = NextUInt32();
        ulong lo = NextUInt32();
        return (hi << 32) | lo;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Step()
    {
        _state = _state * Multiplier + _increment;
    }
}
