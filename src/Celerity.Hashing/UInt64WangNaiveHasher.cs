using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A fast hash provider for <see cref="ulong"/> keys that folds the upper
/// 32 bits and the middle 16 bits of the key into the low half via XOR.
/// </summary>
/// <remarks>
/// This is the <see cref="ulong"/> counterpart to <see cref="Int64WangNaiveHasher"/>:
/// an extremely cheap XOR-fold with modest avalanche but very low latency. It
/// fills the cheap-default tier of the <see cref="ulong"/> escalation ladder,
/// which previously jumped straight from <see cref="UInt64WangHasher"/> (the
/// full Thomas-Wang <c>hash64shift</c> finalizer) to <see cref="UInt64Hasher"/>
/// (the Murmur3 <c>fmix64</c> finalizer) with no cheap XOR-fold option — even
/// though <c>int</c>, <c>long</c>, and <c>uint</c> all ship one. The extra
/// <c>(int)(key &gt;&gt; 16)</c> fold over a naive <c>key.GetHashCode()</c>
/// keeps a chunk of the high-half entropy in the result, which materially
/// improves distribution on keys whose low 32 bits are sequential (e.g.
/// monotonically allocated IDs with upper bits carrying type / shard).
/// <para>
/// Because the fold runs over the raw bits, for any given 64-bit pattern it
/// returns exactly what <see cref="Int64WangNaiveHasher"/> returns for the
/// same bits.
/// </para>
/// <para>
/// Prefer it when the key distribution is already reasonably uniform and
/// latency matters more than collision resistance. For adversarial or
/// heavily clustered keys, escalate to <see cref="UInt64WangHasher"/> (full
/// Thomas-Wang finalizer) or <see cref="UInt64Hasher"/> (Murmur3 <c>fmix64</c>).
/// </para>
/// <para>
/// The XOR-fold maps <c>0</c> to <c>0</c>. The dictionaries store the
/// out-of-band zero-key entry without calling the hasher, so this does not
/// collide with the empty-slot sentinel.
/// </para>
/// </remarks>
public struct UInt64WangNaiveHasher : IHashProvider<ulong>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(ulong key) => (int)key ^ (int)(key >> 32) ^ (int)(key >> 16);
}
