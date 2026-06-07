using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A pass-through hash provider for <see cref="int"/> keys that returns the key unchanged.
/// </summary>
/// <remarks>
/// <para>
/// This is the <strong>zero-work floor</strong> of the <c>int</c> hasher family: it performs no
/// mixing at all (<c>Hash(key) =&gt; key</c>), so it is strictly cheaper than even the XOR-fold
/// <see cref="Int32WangNaiveHasher"/>. It exists as an explicit opt-out from mixing — the
/// F14 / ahash / FxHash position that, when keys are already uniform and trusted, any avalanche
/// step is pure overhead. (<c>int.GetHashCode()</c> is itself the identity function, so this
/// hasher reproduces the framework's own <c>int</c> hash exactly, with the JIT able to inline it
/// through the <c>where THasher : struct, IHashProvider&lt;int&gt;</c> constraint.)
/// </para>
/// <para>
/// <strong>Decision rule:</strong> uniform / trusted keys (sequential IDs, dense indices) →
/// <em>skip</em> mixing with this hasher; clustered or adversarial keys → <em>mix</em> with
/// <see cref="Int32WangHasher"/> (full Thomas-Wang finalizer) or <see cref="Int32Murmur3Hasher"/>.
/// Note that Celerity's open-addressed, power-of-two-masked tables are <em>more</em> sensitive to a
/// weak integer hash than the prime-bucketed BCL <see cref="System.Collections.Generic.Dictionary{TKey, TValue}"/>:
/// because the table masks off the low bits, keys whose low bits collide (e.g. multiples of the
/// capacity) cluster into long probe chains. Identity is the right call only when the low bits of
/// the keys are themselves well distributed.
/// </para>
/// <para>
/// A fixed identity (or any fixed-seed) hasher is <strong>not</strong> a HashDoS defence: an
/// attacker who can choose keys can force collisions. For untrusted integer keys, prefer a strong
/// mixer; keyed PRF hashers are only provided for <see cref="string"/> keys.
/// </para>
/// </remarks>
public struct Int32IdentityHasher : IHashProvider<int>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(int key) => key;
}
