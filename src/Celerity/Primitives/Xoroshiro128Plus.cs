using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Primitives;

/// <summary>
/// xoroshiro128+ — a fast 128-bit-state generator (Blackman &amp; Vigna) tuned for producing
/// <strong>floating-point</strong> values: a period of 2^128 − 1 and the lowest per-call cost of the suite,
/// at the documented cost that its <em>lowest</em> output bits have weak linear complexity.
/// </summary>
/// <remarks>
/// <para>
/// The <c>+</c> scrambler is a single add (<c>s0 + s1</c>), which is why it is the quickest generator here
/// and ideal for <see cref="RandomSourceExtensions.NextDouble{TRng}(ref TRng)"/> /
/// <see cref="RandomSourceExtensions.NextSingle{TRng}(ref TRng)"/>, which consume only the high bits. The
/// low bits (in particular the lowest) fail linear-complexity tests, so do <strong>not</strong> use the
/// raw low bits directly — for arbitrary bounded integers or bit masks where every bit must be strong,
/// prefer <see cref="Xoshiro256StarStar"/>. (The bounded-integer extensions in this library already read
/// the high bits, so they are safe on this generator.)
/// </para>
/// <para>
/// The constructor seeds both state words through <see cref="SplitMix64"/>, so any single
/// <see cref="ulong"/> seed (including <c>0</c>) produces a valid state — the degenerate all-zero state is
/// never reachable. It is a mutable <see langword="struct"/>: copying it forks the stream.
/// </para>
/// </remarks>
public struct Xoroshiro128Plus : IRandomSource
{
    private ulong _s0;
    private ulong _s1;

    /// <summary>
    /// Creates a generator whose 128-bit state is expanded from <paramref name="seed"/> via
    /// <see cref="SplitMix64"/>. Every seed is valid (including <c>0</c>); the same seed always yields the
    /// same sequence.
    /// </summary>
    /// <param name="seed">The 64-bit seed expanded into the full state.</param>
    public Xoroshiro128Plus(ulong seed)
    {
        var sm = new SplitMix64(seed);
        _s0 = sm.NextUInt64();
        _s1 = sm.NextUInt64();
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong NextUInt64()
    {
        ulong s0 = _s0;
        ulong s1 = _s1;
        ulong result = s0 + s1;

        s1 ^= s0;
        _s0 = BitOperations.RotateLeft(s0, 24) ^ s1 ^ (s1 << 16);
        _s1 = BitOperations.RotateLeft(s1, 37);

        return result;
    }
}
