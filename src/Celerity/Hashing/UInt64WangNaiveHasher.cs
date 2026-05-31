using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A fast hash provider for <see cref="ulong"/> keys that folds the upper
/// 32 bits and the middle 16 bits of the key into the low half via XOR.
/// </summary>
/// <remarks>
/// This is the unsigned counterpart to <see cref="Int64WangNaiveHasher"/>: an
/// extremely cheap XOR-fold with modest avalanche but very low latency, the
/// cheapest hasher in the <see cref="ulong"/> family. The extra
/// <c>(int)(key &gt;&gt; 16)</c> fold over a naive <c>key.GetHashCode()</c>
/// keeps a chunk of the high-half entropy in the result, which materially
/// improves distribution on keys whose low 32 bits are sequential (e.g.
/// monotonically allocated IDs with upper bits carrying type / shard).
/// <para>
/// It fills the missing low-latency tier of the <see cref="ulong"/> family:
/// <see cref="UInt64Hasher"/> (the Murmur3 <c>fmix64</c> finalizer) and
/// <see cref="UInt64WangHasher"/> (Thomas Wang's <c>hash64shift</c>) both mix
/// every input bit, while this hasher trades that mixing for three shift /
/// XOR ops, matching the cheap default that <c>int</c>, <c>long</c>, and
/// <c>uint</c> already offer.
/// </para>
/// <para>
/// Because the formula operates on the raw 64-bit pattern, for any given
/// 64-bit value it returns exactly what <see cref="Int64WangNaiveHasher"/>
/// returns for the same bits.
/// </para>
/// <para>
/// Prefer it when the key distribution is already reasonably uniform and
/// latency matters more than collision resistance. For adversarial or
/// heavily clustered keys, switch to <see cref="UInt64WangHasher"/> (full
/// Thomas-Wang finalizer) or <see cref="UInt64Hasher"/> (Murmur3
/// <c>fmix64</c>).
/// </para>
/// </remarks>
public struct UInt64WangNaiveHasher : IHashProvider<ulong>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(ulong key) => (int)key ^ (int)(key >> 32) ^ (int)(key >> 16);
}
