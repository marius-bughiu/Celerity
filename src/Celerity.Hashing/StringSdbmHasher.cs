using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A hash provider for <see cref="string"/> keys using the classic <em>sdbm</em>
/// hash over the full little-endian UTF-16 byte stream of the string — both bytes
/// of every character.
/// </summary>
/// <remarks>
/// sdbm — the hash from Ozan Yigit's public-domain re-implementation of the
/// <c>sdbm</c> database library, also used in gawk and Berkeley DB — seeds the
/// accumulator at <c>0</c> and folds each input byte with the single step
/// <c>hash = b + (hash &lt;&lt; 6) + (hash &lt;&lt; 16) - hash</c>. That lattice is exactly
/// <c>hash = hash * 65599 + b</c> (because <c>64 + 65536 - 1 = 65599</c>), so the
/// multiply by the prime <c>65599</c> lowers to two shifts and a subtract — no real
/// multiply, no table, and no finalizer. It is a peer of <see cref="StringDjb2Hasher"/>
/// at the cheapest, classic end of the ladder: where djb2 mixes with one shift and
/// two adds (<c>* 33</c>), sdbm mixes with two shifts, an add, and a subtract
/// (<c>* 65599</c>). The larger multiplier spreads each folded byte across more of
/// the accumulator than djb2's <c>* 33</c>, which tends to give sdbm slightly better
/// distribution on short keys, though both lack a true avalanche step.
/// <para>
/// Its tradeoff is the same weak avalanche as djb2 — sdbm has no shift/xor
/// diffusion or finalizer, so a single changed input byte propagates less
/// thoroughly than under <see cref="StringJenkinsOaatHasher"/> — but it is famous,
/// fast, and distributes well on the short ASCII identifiers that dominate many
/// real key sets.
/// </para>
/// <para>
/// Like the other full-width string hashers it folds both bytes of every 16-bit
/// code unit (low byte then high byte) — exactly sdbm over the string's native
/// little-endian UTF-16 byte stream — so it distinguishes characters that differ
/// only in their upper byte, for example <c>'A'</c> (<c>U+0041</c>) and
/// <c>'Ł'</c> (<c>U+0141</c>), which <see cref="StringFnV1AHasher"/> collides on.
/// </para>
/// <para>
/// It sits at the cheapest, classic end of the <see cref="string"/> escalation
/// ladder, a peer to <see cref="StringDjb2Hasher"/>, the FNV-1a variants, and
/// <see cref="StringJenkinsOaatHasher"/>:
/// <see cref="StringDjb2Hasher"/> / <see cref="StringSdbmHasher"/> (cheapest,
/// shift-and-add classics, weaker avalanche) / <see cref="StringFnV1AHasher"/>
/// (low-byte only) → <see cref="StringFnV1AFullHasher"/> /
/// <see cref="StringFnV1A64Hasher"/> (cheap FNV-1a, full Unicode width) →
/// <see cref="StringJenkinsOaatHasher"/> (cheap, full Unicode width, multiply-free,
/// stronger per-bit avalanche than the classics or FNV-1a) →
/// <see cref="StringMurmur3Hasher"/> / <see cref="StringXxHash32Hasher"/> /
/// <see cref="StringXxHash64Hasher"/> / <see cref="StringMetroHash64Hasher"/> /
/// <see cref="StringCityHash64Hasher"/> (strong avalanche, maximum throughput).
/// Prefer it when you want a simple, familiar cheap hash and your keys are short
/// ASCII identifiers; step up to <see cref="StringJenkinsOaatHasher"/> (still
/// cheap, but with proper shift/xor diffusion) or the FNV-1a variants when sdbm's
/// weak avalanche starts clustering keys, and escalate to the throughput-oriented
/// strong family for clustered or adversarial keys.
/// </para>
/// <para>
/// The empty string hashes to the seed constant <c>0</c> — no bytes are folded.
/// The dictionaries store the out-of-band <c>null</c>-key entry without ever
/// calling the hasher, so this does not collide with the empty-slot sentinel.
/// </para>
/// </remarks>
public struct StringSdbmHasher : IHashProvider<string>
{
    /// <summary>
    /// Computes the sdbm hash of the specified string over the full little-endian
    /// UTF-16 byte stream (both bytes of every character).
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit sdbm hash of <paramref name="key"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="key"/> is <c>null</c>. Celerity dictionaries store the
    /// out-of-band <c>null</c>-key entry without calling the hasher, so this
    /// check only surfaces when the hasher is used directly or plugged into a
    /// consumer that does not handle <c>null</c> keys out-of-band.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        uint hash = 0u;
        foreach (char c in key)
        {
            // Fold the low byte then the high byte of each 16-bit code unit —
            // i.e. sdbm over the character's little-endian UTF-16 bytes. The
            // extra high-byte step keeps characters that share a low byte but
            // differ in their upper byte distinct, unlike StringFnV1AHasher.
            // b + (hash << 6) + (hash << 16) - hash is exactly hash * 65599 + b,
            // so no real multiply runs.
            hash = (byte)(c & 0xFF) + (hash << 6) + (hash << 16) - hash;
            hash = (byte)(c >> 8) + (hash << 6) + (hash << 16) - hash;
        }

        return unchecked((int)hash);
    }
}
