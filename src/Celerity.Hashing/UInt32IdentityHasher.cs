using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A pass-through hash provider for <see cref="uint"/> keys that returns the key's bits unchanged.
/// </summary>
/// <remarks>
/// <para>
/// This is the <strong>zero-work floor</strong> of the <c>uint</c> hasher family and the unsigned
/// counterpart to <see cref="Int32IdentityHasher"/>: it performs no mixing at all
/// (<c>Hash(key) =&gt; (int)key</c>), so it is strictly cheaper than even the XOR-fold
/// <see cref="UInt32WangNaiveHasher"/>. The cast is a reinterpretation, not a truncation — a
/// <c>uint</c> and an <c>int</c> are both 32 bits wide, so every key keeps all of its entropy and
/// distinct keys always produce distinct codes. It exists as an explicit opt-out from mixing — the
/// F14 / ahash / FxHash position that, when keys are already uniform and trusted, any avalanche
/// step is pure overhead. (<c>uint.GetHashCode()</c> is itself this reinterpretation, so this
/// hasher reproduces the framework's own <c>uint</c> hash exactly, with the JIT able to inline it
/// through the <c>where THasher : struct, IHashProvider&lt;uint&gt;</c> constraint.)
/// </para>
/// <para>
/// A cast at the call site is <em>not</em> a substitute for this type. The collections and sketches
/// take the hasher as a type parameter and invoke it internally
/// (<c>where THasher : struct, IHashProvider&lt;TKey&gt;</c>), so a <c>uint</c>-keyed collection
/// needs an <see cref="IHashProvider{T}"/> over <c>uint</c> — there is no call site to insert a cast
/// into.
/// </para>
/// <para>
/// <strong>Decision rule:</strong> uniform / trusted keys (sequential IDs, dense indices) →
/// <em>skip</em> mixing with this hasher; clustered or adversarial keys → <em>mix</em> with
/// <see cref="UInt32WangHasher"/> (full Thomas-Wang finalizer) or
/// <see cref="UInt32Murmur3Hasher"/>. Note that Celerity's open-addressed, power-of-two-masked
/// tables are <em>more</em> sensitive to a weak integer hash than the prime-bucketed BCL
/// <see cref="System.Collections.Generic.Dictionary{TKey, TValue}"/>: because the table masks off
/// the low bits, keys whose low bits collide (e.g. multiples of the capacity) cluster into long
/// probe chains. Identity is the right call only when the low bits of the keys are themselves well
/// distributed.
/// </para>
/// <para>
/// A fixed identity (or any fixed-seed) hasher is <strong>not</strong> a HashDoS defence: an
/// attacker who can choose keys can force collisions. For untrusted integer keys, prefer a strong
/// mixer; keyed PRF hashers are only provided for <see cref="string"/> keys.
/// </para>
/// </remarks>
public struct UInt32IdentityHasher : IHashProvider<uint>
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(uint key) => (int)key;
}
