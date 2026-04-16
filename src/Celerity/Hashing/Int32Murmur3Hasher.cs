using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A high-quality hash provider for <see cref="int"/> keys using the
/// Murmur3 32-bit finalizer ("fmix32").
/// </summary>
/// <remarks>
/// This is the same finalizer used in MurmurHash3 for 32-bit keys. Every input
/// bit affects every output bit, giving excellent avalanche properties at the
/// cost of a few more instructions than <see cref="Int32WangNaiveHasher"/>.
/// Prefer this hasher when keys may be clustered or adversarial; prefer
/// <see cref="Int32WangNaiveHasher"/> when keys are already well distributed
/// and latency dominates.
/// </remarks>
public struct Int32Murmur3Hasher : IHashProvider<int>
{
    private const uint C1 = 0x85ebca6bU;
    private const uint C2 = 0xc2b2ae35U;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(int key)
    {
        // Reinterpret as unsigned so the shifts are logical, not arithmetic.
        uint k = unchecked((uint)key);

        // XOR with its shifted self.
        k ^= k >> 16;

        // Multiply by a large odd constant.
        k *= C1;

        // XOR again with its shifted self.
        k ^= k >> 13;

        // Multiply by another large odd constant.
        k *= C2;

        // Final XOR.
        k ^= k >> 16;

        // Reinterpret the 32-bit result as a signed integer.
        return unchecked((int)k);
    }
}
