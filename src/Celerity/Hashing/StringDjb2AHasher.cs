using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A hash provider for <see cref="string"/> keys using the <em>djb2a</em> variant
/// of Daniel J. Bernstein's classic djb2 hash — the XOR-folding sibling that
/// replaces djb2's trailing add with an XOR (<c>hash = hash * 33 ^ b</c>) — over
/// the full little-endian UTF-16 byte stream of the string (both bytes of every
/// character).
/// </summary>
/// <remarks>
/// djb2a seeds the accumulator with the same magic constant <c>5381</c> as
/// <see cref="StringDjb2Hasher"/> and folds each input byte with the single step
/// <c>hash = hash * 33 ^ b</c>. As in djb2 the multiply by <c>33</c> is a
/// power-of-two-plus-one, so the JIT lowers it to a shift-and-add
/// (<c>(hash &lt;&lt; 5) + hash</c>) — no real multiply, no table, no finalizer.
/// The <em>only</em> difference from <see cref="StringDjb2Hasher"/> is the final
/// combine: djb2 <em>adds</em> the byte (<c>* 33 + b</c>), djb2a <em>XORs</em> it
/// (<c>* 33 ^ b</c>). This is the exact same relationship FNV-1 and FNV-1a have to
/// each other (add/XOR ordering aside), which is why this type mirrors the
/// <see cref="StringFnV1Hasher"/> / <see cref="StringFnV1AHasher"/> naming.
/// <para>
/// Replacing the add with an XOR avoids the low-bit carry bias that addition
/// introduces, so djb2a tends to diffuse the freshly-folded byte across the
/// accumulator a touch more evenly than djb2 — but it is still a single
/// combine per byte with no shift/xor avalanche step, so a changed input byte
/// propagates less thoroughly than under Bob Jenkins' one-at-a-time hash. It
/// keeps djb2's strengths: famous, fast, and a good fit for the short ASCII
/// identifiers that dominate many real key sets.
/// </para>
/// <para>
/// Like the other full-width string hashers it folds both bytes of every 16-bit
/// code unit (low byte then high byte) — exactly djb2a over the string's native
/// little-endian UTF-16 byte stream — so it distinguishes characters that differ
/// only in their upper byte, for example <c>'A'</c> (<c>U+0041</c>) and
/// <c>'Ł'</c> (<c>U+0141</c>), which <see cref="StringFnV1AHasher"/> collides on.
/// </para>
/// <para>
/// It sits at the cheapest, classic end of the <see cref="string"/> escalation
/// ladder, a peer to <see cref="StringDjb2Hasher"/>, <see cref="StringSdbmHasher"/>,
/// <see cref="StringElfHasher"/>, the FNV-1a variants, and
/// <see cref="StringJenkinsOaatHasher"/>:
/// <see cref="StringDjb2Hasher"/> / <see cref="StringDjb2AHasher"/> /
/// <see cref="StringSdbmHasher"/> / <see cref="StringElfHasher"/> (cheapest,
/// multiply-free classics, weaker avalanche) / <see cref="StringFnV1AHasher"/>
/// (low-byte only) → <see cref="StringFnV1AFullHasher"/> /
/// <see cref="StringFnV1A64Hasher"/> (cheap FNV-1a, full Unicode width) →
/// <see cref="StringJenkinsOaatHasher"/> (cheap, full Unicode width, multiply-free,
/// stronger per-bit avalanche than djb2/djb2a or FNV-1a) →
/// <see cref="StringMurmur3Hasher"/> / <see cref="StringXxHash32Hasher"/> /
/// <see cref="StringXxHash64Hasher"/> / <see cref="StringMetroHash64Hasher"/> /
/// <see cref="StringCityHash64Hasher"/> (strong avalanche, maximum throughput).
/// Prefer it over <see cref="StringDjb2Hasher"/> when you want the same minimal,
/// familiar djb2 cost but slightly cleaner low-bit diffusion from the XOR fold;
/// step up to <see cref="StringJenkinsOaatHasher"/> (still cheap, but with proper
/// shift/xor diffusion) or the FNV-1a variants when djb2a's weak avalanche starts
/// clustering keys, and escalate to the throughput-oriented strong family for
/// clustered or adversarial keys.
/// </para>
/// <para>
/// The empty string hashes to the seed constant <c>5381</c> — no bytes are folded
/// — exactly as <see cref="StringDjb2Hasher"/> and FNV-1a map the empty string to
/// their seeds. The dictionaries store the out-of-band <c>null</c>-key entry
/// without ever calling the hasher, so this does not collide with the empty-slot
/// sentinel.
/// </para>
/// </remarks>
public struct StringDjb2AHasher : IHashProvider<string>
{
    /// <summary>
    /// Computes the djb2a (XOR-folding) hash of the specified string over the
    /// full little-endian UTF-16 byte stream (both bytes of every character).
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit djb2a hash of <paramref name="key"/>.</returns>
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

        uint hash = 5381u;
        foreach (char c in key)
        {
            // Fold the low byte then the high byte of each 16-bit code unit —
            // i.e. djb2a over the character's little-endian UTF-16 bytes. The
            // extra high-byte step keeps characters that share a low byte but
            // differ in their upper byte distinct, unlike StringFnV1AHasher.
            // hash * 33 lowers to (hash << 5) + hash, so no real multiply runs;
            // the byte is XORed in (the djb2a variant) rather than added (djb2).
            hash = ((hash << 5) + hash) ^ (byte)(c & 0xFF);
            hash = ((hash << 5) + hash) ^ (byte)(c >> 8);
        }

        return unchecked((int)hash);
    }
}
