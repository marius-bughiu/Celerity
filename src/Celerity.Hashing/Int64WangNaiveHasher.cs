using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A fast hash provider for <see cref="long"/> keys that folds the upper
/// 32 bits and the middle 16 bits of the key into the low half via XOR.
/// </summary>
/// <remarks>
/// This is the 64-bit counterpart to <see cref="Int32WangNaiveHasher"/>: an
/// extremely cheap XOR-fold with modest avalanche but very low latency. The
/// extra <c>(int)(key &gt;&gt;&gt; 16)</c> fold over a naive <c>key.GetHashCode()</c>
/// keeps a chunk of the high-half entropy in the result, which materially
/// improves distribution on keys whose low 32 bits are sequential (e.g.
/// monotonically allocated IDs with upper bits carrying type / shard).
/// <para>
/// Prefer it when the key distribution is already reasonably uniform and
/// latency matters more than collision resistance. For adversarial or
/// heavily clustered keys, switch to <see cref="Int64WangHasher"/> (full
/// Thomas-Wang finalizer) or <see cref="Int64Murmur3Hasher"/>.
/// </para>
/// </remarks>
public struct Int64WangNaiveHasher : IHashProvider<long>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(long key) => (int)key ^ (int)((ulong)key >> 32) ^ (int)((ulong)key >> 16);
}
