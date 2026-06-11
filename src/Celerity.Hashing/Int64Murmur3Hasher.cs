using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A high-quality hash provider for <see cref="long"/> keys using the
/// Murmur3 64-bit finalizer ("fmix64").
/// </summary>
/// <remarks>
/// This is the same finalizer used in MurmurHash3 for 64-bit keys. It has
/// excellent avalanche properties — every input bit affects every output bit —
/// making it a good choice for clustered or adversarial key distributions.
/// The result is truncated to 32 bits by taking the lower half.
/// </remarks>
public struct Int64Murmur3Hasher : IHashProvider<long>
{
    private const long C1 = unchecked((long)0xff51afd7ed558ccdUL);
    private const long C2 = unchecked((long)0xc4ceb9fe1a85ec53UL);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(long key)
    {
        // XOR with its shifted self.
        key ^= (long)((ulong)key >> 33);

        // Multiply by a large odd constant.
        key *= C1;

        // XOR again with its shifted self.
        key ^= (long)((ulong)key >> 33);

        // Multiply by another large odd constant.
        key *= C2;

        // Final XOR.
        key ^= (long)((ulong)key >> 33);

        // Take the lower 32 bits as the final hash value.
        return (int)key;
    }
}
