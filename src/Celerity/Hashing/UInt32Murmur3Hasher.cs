using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A high-quality hash provider for <see cref="uint"/> keys using the
/// Murmur3 32-bit finalizer ("fmix32").
/// </summary>
/// <remarks>
/// This is the <see cref="uint"/> counterpart to <see cref="Int32Murmur3Hasher"/>
/// and the strong-avalanche escalation option for <see cref="UInt32Hasher"/>
/// (the cheap XOR-fold default). Every input bit affects every output bit,
/// making it a good choice for clustered or adversarial key distributions.
/// Prefer it over <see cref="UInt32Hasher"/> when the cheap XOR-fold produces
/// measurable clustering and collision resistance matters more than raw
/// throughput.
/// </remarks>
public struct UInt32Murmur3Hasher : IHashProvider<uint>
{
    private const uint C1 = 0x85ebca6bu;
    private const uint C2 = 0xc2b2ae35u;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(uint key)
    {
        uint h = key;

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
