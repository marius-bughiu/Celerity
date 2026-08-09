using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A high-quality hash provider for <see cref="ulong"/> keys using the
/// Murmur3 64-bit finalizer ("fmix64").
/// </summary>
/// <remarks>
/// This is the <see cref="ulong"/> counterpart to <see cref="Int64Murmur3Hasher"/>
/// and the strongest tier of the <see cref="ulong"/> escalation ladder. Every input
/// bit affects every output bit, making it a good choice for clustered or adversarial
/// key distributions. The 64-bit result is truncated to 32 bits by taking the lower
/// half and reinterpreting it as a signed int. For any given 64-bit pattern it returns
/// exactly what <see cref="Int64Murmur3Hasher"/> returns for the same bits. Drop to
/// <see cref="UInt64WangHasher"/> (the full Thomas-Wang <c>hash64shift</c> finalizer)
/// or <see cref="UInt64WangNaiveHasher"/> (the XOR-fold) when the two 64-bit multiplies
/// cost more than they buy on already-uniform keys.
/// <para>
/// The finalizer is a bijection on 64 bits, so the type also implements
/// <see cref="IHashProvider64{T}"/>: <see cref="Hash64"/> returns the full mix instead
/// of the low half, which is what the probabilistic sketches want (see
/// <see cref="IHashProvider64{T}"/> for why the extra 32 bits matter there and not in a
/// hash table).
/// </para>
/// <para>
/// This type replaces <c>UInt64Hasher</c>, whose bare name said nothing about which
/// tier of the ladder it occupied — and named the strongest tier for <see cref="ulong"/>
/// while the identically-shaped <c>UInt32Hasher</c> named the cheapest one for
/// <see cref="uint"/>. The old name remains as an obsolete alias and will be removed
/// in a future major version.
/// </para>
/// </remarks>
public struct UInt64Murmur3Hasher : IHashProvider<ulong>, IHashProvider64<ulong>
{
    private const ulong C1 = 0xff51afd7ed558ccdUL;
    private const ulong C2 = 0xc4ceb9fe1a85ec53UL;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(ulong key) => (int)Hash64(key);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Hash64(ulong key)
    {
        // XOR with its shifted self.
        key ^= key >> 33;

        // Multiply by a large odd constant.
        key *= C1;

        // XOR again with its shifted self.
        key ^= key >> 33;

        // Multiply by another large odd constant.
        key *= C2;

        // Final XOR.
        key ^= key >> 33;

        return key;
    }
}
