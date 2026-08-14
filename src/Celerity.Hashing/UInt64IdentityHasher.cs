using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A pass-through hash provider for <see cref="ulong"/> keys that returns the key's low 32 bits unchanged.
/// </summary>
/// <remarks>
/// <para>
/// This is the <strong>zero-work floor</strong> of the <c>ulong</c> hasher family and the unsigned
/// counterpart to <see cref="Int64IdentityHasher"/>: it performs no mixing
/// (<c>Hash(key) =&gt; (int)key</c>), truncating the 64-bit key to its low 32 bits, so it is
/// strictly cheaper than even the XOR-fold <see cref="UInt64WangNaiveHasher"/>. It exists as an
/// explicit opt-out from mixing — the F14 / ahash / FxHash position that, when keys are already
/// uniform and trusted, any avalanche step is pure overhead.
/// </para>
/// <para>
/// Because it keeps only the low 32 bits, two keys that differ only in their upper 32 bits
/// <strong>collide</strong> — unlike <see cref="UInt64WangNaiveHasher"/>, which folds the high half
/// back in. That makes it the right call only when the discriminating entropy lives in the low 32
/// bits (dense sequential <c>ulong</c> IDs), and the wrong call when the upper bits carry the
/// distinguishing information (type / shard tags, timestamps in the high word). For the same
/// reason it deliberately does not implement <see cref="IHashProvider64{T}"/>: a truncating
/// pass-through has no 64-bit code to publish, and claiming otherwise would tell a sketch it had
/// escaped an entropy floor it is still standing on.
/// </para>
/// <para>
/// A cast at the call site is <em>not</em> a substitute for this type. The collections and sketches
/// take the hasher as a type parameter and invoke it internally
/// (<c>where THasher : struct, IHashProvider&lt;TKey&gt;</c>), so a <c>ulong</c>-keyed collection
/// needs an <see cref="IHashProvider{T}"/> over <c>ulong</c> — there is no call site to insert a
/// cast into.
/// </para>
/// <para>
/// <strong>Decision rule:</strong> uniform / trusted keys whose low 32 bits are well distributed →
/// <em>skip</em> mixing with this hasher; clustered, high-half-distinct, or adversarial keys →
/// <em>mix</em> with <see cref="UInt64WangHasher"/> (full Thomas-Wang finalizer) or
/// <see cref="UInt64Murmur3Hasher"/>. As with <see cref="UInt32IdentityHasher"/>, Celerity's
/// open-addressed, power-of-two-masked tables are <em>more</em> sensitive to a weak integer hash
/// than the prime-bucketed BCL <see cref="System.Collections.Generic.Dictionary{TKey, TValue}"/>,
/// and a fixed identity hash is <strong>not</strong> a HashDoS defence.
/// </para>
/// </remarks>
public struct UInt64IdentityHasher : IHashProvider<ulong>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(ulong key) => (int)key;
}
