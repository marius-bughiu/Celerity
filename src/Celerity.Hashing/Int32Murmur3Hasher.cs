using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A high-quality hash provider for <see cref="int"/> keys using the
/// Murmur3 32-bit finalizer ("fmix32").
/// </summary>
/// <remarks>
/// This is the same finalizer used in MurmurHash3 for 32-bit output. It has
/// excellent avalanche properties — every input bit affects every output bit —
/// making it a good choice for clustered or adversarial key distributions.
/// Prefer this over <see cref="Int32WangNaiveHasher"/> when key distribution
/// is non-uniform or collision resistance matters more than raw throughput.
/// </remarks>
public struct Int32Murmur3Hasher : IHashProvider<int>
{
    private const uint C1 = 0x85ebca6bu;
    private const uint C2 = 0xc2b2ae35u;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(int key)
    {
        uint h = (uint)key;

        // XOR with its right-shifted self.
        h ^= h >> 16;

        // Multiply by a large odd constant.
        h *= C1;

        // XOR again with its right-shifted self.
        h ^= h >> 13;

        // Multiply by another large odd constant.
        h *= C2;

        // Final XOR.
        h ^= h >> 16;

        return (int)h;
    }
}
