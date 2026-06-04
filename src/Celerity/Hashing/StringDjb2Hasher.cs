using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A hash provider for <see cref="string"/> keys using Daniel J. Bernstein's
/// classic <em>djb2</em> hash over the full little-endian UTF-16 byte stream of
/// the string — both bytes of every character.
/// </summary>
/// <remarks>
/// djb2 seeds the accumulator with the magic constant <c>5381</c> and folds each
/// input byte with the single step <c>hash = hash * 33 + b</c>. The multiply by
/// <c>33</c> is a power-of-two-plus-one, so the JIT lowers it to a shift-and-add
/// (<c>(hash &lt;&lt; 5) + hash</c>) — no real multiply, no table, no finalizer.
/// That makes it the simplest and cheapest of the classic string hashes: even
/// FNV-1a pays a multiply by its 32-bit prime per byte, whereas djb2 mixes with
/// one shift, one add, and one more add. Its tradeoff is weaker avalanche — djb2
/// has no shift/xor diffusion step, so a single changed input byte propagates
/// less thoroughly than under Bob Jenkins' one-at-a-time hash — but it is famous,
/// fast, and distributes well on the short ASCII identifiers that dominate many
/// real key sets.
/// <para>
/// Like the other full-width string hashers it folds both bytes of every 16-bit
/// code unit (low byte then high byte) — exactly djb2 over the string's native
/// little-endian UTF-16 byte stream — so it distinguishes characters that differ
/// only in their upper byte, for example <c>'A'</c> (<c>U+0041</c>) and
/// <c>'Ł'</c> (<c>U+0141</c>), which <see cref="StringFnV1AHasher"/> collides on.
/// </para>
/// <para>
/// It sits at the cheapest, classic end of the <see cref="string"/> escalation
/// ladder, a peer to the FNV-1a variants and <see cref="StringJenkinsOaatHasher"/>:
/// <see cref="StringDjb2Hasher"/> (cheapest, shift-and-add, classic, weaker
/// avalanche) / <see cref="StringFnV1AHasher"/> (low-byte only) →
/// <see cref="StringFnV1AFullHasher"/> / <see cref="StringFnV1A64Hasher"/>
/// (cheap FNV-1a, full Unicode width) →
/// <see cref="StringJenkinsOaatHasher"/> (cheap, full Unicode width, multiply-free,
/// stronger per-bit avalanche than djb2 or FNV-1a) →
/// <see cref="StringMurmur3Hasher"/> / <see cref="StringXxHash32Hasher"/> /
/// <see cref="StringXxHash64Hasher"/> / <see cref="StringMetroHash64Hasher"/> /
/// <see cref="StringCityHash64Hasher"/> (strong avalanche, maximum throughput).
/// Prefer it when you want the simplest, most familiar cheap hash and your keys
/// are short ASCII identifiers; step up to <see cref="StringJenkinsOaatHasher"/>
/// (still cheap, but with proper shift/xor diffusion) or the FNV-1a variants when
/// djb2's weak avalanche starts clustering keys, and escalate to the
/// throughput-oriented strong family for clustered or adversarial keys.
/// </para>
/// <para>
/// The empty string hashes to the seed constant <c>5381</c> — no bytes are folded
/// — exactly as FNV-1a maps the empty string to its offset basis. The
/// dictionaries store the out-of-band <c>null</c>-key entry without ever calling
/// the hasher, so this does not collide with the empty-slot sentinel.
/// </para>
/// </remarks>
public struct StringDjb2Hasher : IHashProvider<string>
{
    /// <summary>
    /// Computes Daniel J. Bernstein's djb2 hash of the specified string over the
    /// full little-endian UTF-16 byte stream (both bytes of every character).
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit djb2 hash of <paramref name="key"/>.</returns>
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
            // i.e. djb2 over the character's little-endian UTF-16 bytes. The
            // extra high-byte step keeps characters that share a low byte but
            // differ in their upper byte distinct, unlike StringFnV1AHasher.
            // hash * 33 lowers to (hash << 5) + hash, so no real multiply runs.
            hash = (hash << 5) + hash + (byte)(c & 0xFF);
            hash = (hash << 5) + hash + (byte)(c >> 8);
        }

        return unchecked((int)hash);
    }
}
