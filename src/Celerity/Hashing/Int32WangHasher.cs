using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A hash provider for <see cref="int"/> keys using the Thomas Wang
/// 32-bit integer hash function.
/// </summary>
/// <remarks>
/// A non-cryptographic, invertible bit-mixer (Thomas Wang's <c>hash32shift</c>)
/// that provides strong avalanche for adversarially or heavily-clustered keys.
/// It sits between <see cref="Int32WangNaiveHasher"/> (the default for
/// <see cref="Celerity.Collections.IntDictionary{TValue}"/> and
/// <see cref="Celerity.Collections.IntSet"/>) and
/// <see cref="Int32Murmur3Hasher"/> on the cost-vs-avalanche curve, mirroring
/// the role that <see cref="Int64WangHasher"/> plays in the <see cref="long"/>
/// family. The mixer uses a single (shift-add-encoded) multiply and a chain of
/// XOR-shift / shift-add rounds, so it is cheaper than the two-multiply
/// <see cref="Int32Murmur3Hasher"/> finalizer while still giving every input
/// bit influence over the result. Prefer it over the default
/// <see cref="Int32WangNaiveHasher"/> when profiling shows the cheap XOR-fold is
/// producing measurable clustering; escalate to <see cref="Int32Murmur3Hasher"/>
/// when even better avalanche is needed.
/// <para>
/// Because the function is bijective on <see cref="uint"/>, the only source of
/// collisions is the structure of the keys themselves, not the mixer. Unlike the
/// Murmur3 finalizer, it does <em>not</em> map <c>0</c> to <c>0</c>.
/// </para>
/// </remarks>
public struct Int32WangHasher : IHashProvider<int>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(int key)
    {
        uint u = (uint)key;

        u = ~u + (u << 15);
        u ^= u >> 12;
        u += u << 2;
        u ^= u >> 4;
        u = (u + (u << 3)) + (u << 11); // u * 2057
        u ^= u >> 16;

        return (int)u;
    }
}
