using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Primitives;

/// <summary>
/// xoshiro256** — the recommended general-purpose 64-bit generator (Blackman &amp; Vigna, 2018): 256 bits of
/// state, a period of 2^256 − 1, and output passing the full BigCrush / PractRand batteries. It is the same
/// algorithm the .NET runtime uses internally for the <em>unseeded</em> <see cref="System.Random.Shared"/>,
/// exposed here as an allocation-free, inlinable, deterministically seedable <see langword="struct"/>.
/// </summary>
/// <remarks>
/// <para>
/// The <c>**</c> (&quot;starstar&quot;) scrambler applies a strong non-linear output transform
/// (<c>rotl(s1 * 5, 7) * 9</c>), so unlike the <c>+</c> variant every output bit — including the low ones —
/// is well distributed; this is the right default for arbitrary bounded integers and bit extraction. For
/// generating doubles where only the top bits are consumed, <see cref="Xoroshiro128Plus"/> is marginally
/// faster.
/// </para>
/// <para>
/// The constructor seeds all four state words through <see cref="SplitMix64"/>, so any single
/// <see cref="ulong"/> seed (including <c>0</c>) produces a valid, well-distributed state — the all-zero
/// state, which would lock the generator at zero, is never reachable from a SplitMix64 expansion. It is a
/// mutable <see langword="struct"/>: copying it forks the stream.
/// </para>
/// </remarks>
public struct Xoshiro256StarStar : IRandomSource
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    /// <summary>
    /// Creates a generator whose 256-bit state is expanded from <paramref name="seed"/> via
    /// <see cref="SplitMix64"/>. Every seed is valid (including <c>0</c>); the same seed always yields the
    /// same sequence.
    /// </summary>
    /// <param name="seed">The 64-bit seed expanded into the full state.</param>
    public Xoshiro256StarStar(ulong seed)
    {
        var sm = new SplitMix64(seed);
        _s0 = sm.NextUInt64();
        _s1 = sm.NextUInt64();
        _s2 = sm.NextUInt64();
        _s3 = sm.NextUInt64();
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong NextUInt64()
    {
        // Output scrambler: rotl(s1 * 5, 7) * 9.
        ulong result = BitOperations.RotateLeft(_s1 * 5UL, 7) * 9UL;

        ulong t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = BitOperations.RotateLeft(_s3, 45);

        return result;
    }
}
