using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A high-quality hash provider for <see cref="ulong"/> keys using the
/// Murmur3 64-bit finalizer ("fmix64").
/// </summary>
/// <remarks>
/// This is the <see cref="ulong"/> counterpart to <see cref="Int64Murmur3Hasher"/>.
/// Every input bit affects every output bit, making it a good choice for
/// clustered or adversarial key distributions. The 64-bit result is truncated
/// to 32 bits by taking the lower half and reinterpreting it as a signed int.
/// <para>
/// The finalizer is a bijection on 64 bits, so the type also implements
/// <see cref="IHashProvider64{T}"/>: <see cref="Hash64"/> returns the full mix instead
/// of the low half, which is what the probabilistic sketches want (see
/// <see cref="IHashProvider64{T}"/> for why the extra 32 bits matter there and not in a
/// hash table).
/// </para>
/// </remarks>
public struct UInt64Hasher : IHashProvider<ulong>, IHashProvider64<ulong>
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
