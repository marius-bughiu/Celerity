using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

public struct Int64Murmur3Hasher : IHashProvider<long>
{
    private const long C1 = unchecked((long)0xff51afd7ed558ccdUL);
    private const long C2 = unchecked((long)0xc4ceb9fe1a85ec53UL);

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
