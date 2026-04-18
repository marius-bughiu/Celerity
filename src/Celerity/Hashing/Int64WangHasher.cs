using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A fast hash provider for <see cref="long"/> keys using the Thomas Wang
/// 64-bit integer hash function.
/// </summary>
/// <remarks>
/// This is the <see cref="long"/> counterpart to <see cref="Int32WangNaiveHasher"/>:
/// a non-cryptographic, invertible bit-mixer that is faster than a full Murmur3
/// finalizer. It provides better avalanche than a simple XOR-fold while remaining
/// cheaper than <see cref="Int64Murmur3Hasher"/>. Prefer it when throughput
/// matters more than collision resistance on adversarial inputs.
/// <para>
/// Unlike the Murmur3 finalizer, this function does <em>not</em> map
/// <c>0</c> to <c>0</c>. If your keys are heavily concentrated around zero,
/// measure both hashers before committing to one.
/// </para>
/// </remarks>
public struct Int64WangHasher : IHashProvider<long>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(long key)
    {
        ulong u = (ulong)key;

        u = (~u) + (u << 21);
        u ^= u >> 24;
        u = (u + (u << 3)) + (u << 8);
        u ^= u >> 14;
        u = (u + (u << 2)) + (u << 4);
        u ^= u >> 28;
        u += u << 31;

        return (int)u;
    }
}
