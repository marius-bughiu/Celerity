namespace Celerity.Primitives;

/// <summary>
/// A value-type, allocation-free, seed-deterministic source of pseudo-random 64-bit words.
/// </summary>
/// <remarks>
/// <para>
/// This is the single primitive every Celerity struct PRNG implements: a mutable <see langword="struct"/>
/// advances its internal state and returns the next 64-bit output from <see cref="NextUInt64"/>. The richer,
/// shape-specific surface (<c>NextDouble</c>, <c>NextSingle</c>, <c>NextInt</c>, <c>NextBytes</c>, …) is
/// provided once by <see cref="RandomSourceExtensions"/> as <c>ref this</c> extension methods constrained to
/// <c>where TRng : struct, IRandomSource</c>, so the JIT devirtualizes and inlines the
/// <see cref="NextUInt64"/> call per concrete generator — exactly the zero-cost-abstraction pattern the
/// <c>Celerity.Hashing.IHashProvider&lt;T&gt;</c> hashers use (Celerity.Primitives is the lowest layer and
/// does not reference Celerity.Hashing, so this is a prose reference rather than a doc link).
/// </para>
/// <para>
/// Implementations are deliberately <strong>not thread-safe</strong> (a generator is a small mutable struct;
/// share one per thread) and are seeded deterministically — the same seed always produces the same sequence,
/// which is the property fuzzers, Monte-Carlo runs, shuffles, and procedural generation rely on. Unlike
/// <see cref="System.Random"/>, the seeded path is the <em>fast</em> path: there is no virtual dispatch, no
/// heap allocation, and no fallback to a legacy algorithm.
/// </para>
/// </remarks>
public interface IRandomSource
{
    /// <summary>
    /// Advances the generator state and returns the next uniformly distributed 64-bit value.
    /// </summary>
    /// <returns>The next pseudo-random <see cref="ulong"/> in <c>[0, 2^64)</c>.</returns>
    ulong NextUInt64();
}
