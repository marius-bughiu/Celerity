using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A hash provider for <see cref="string"/> keys using Bob Jenkins'
/// <em>one-at-a-time</em> (OAAT) hash over the full little-endian UTF-16 byte
/// stream of the string — both bytes of every character.
/// </summary>
/// <remarks>
/// The one-at-a-time hash mixes each input byte with a per-byte
/// add / shift / xor lattice (<c>hash += b; hash += hash &lt;&lt; 10;
/// hash ^= hash &gt;&gt; 6</c>) and finishes with a three-step avalanche
/// (<c>hash += hash &lt;&lt; 3; hash ^= hash &gt;&gt; 11;
/// hash += hash &lt;&lt; 15</c>). Unlike FNV-1a — which folds each byte with a
/// single xor and one multiply and therefore has comparatively weak per-bit
/// avalanche — the shift / xor steps give every input bit influence over the
/// whole 32-bit result, yet the function uses <em>no multiplies at all</em>,
/// only adds, shifts, and xors, so it stays in the same cheap cost class. It is
/// the classic "better distributed than FNV-1a, cheaper than the block hashes"
/// middle option.
/// <para>
/// Like the other full-width string hashers it folds both bytes of every 16-bit
/// code unit (low byte then high byte) — exactly the one-at-a-time hash of the
/// string's native little-endian UTF-16 byte stream — so it distinguishes
/// characters that differ only in their upper byte, for example <c>'A'</c>
/// (<c>U+0041</c>) and <c>'Ł'</c> (<c>U+0141</c>), which
/// <see cref="StringFnV1AHasher"/> collides on.
/// </para>
/// <para>
/// It sits in the cheap tier of the <see cref="string"/> escalation ladder,
/// between the FNV-1a variants and the strong block hashes:
/// <see cref="StringFnV1AHasher"/> (cheapest, low-byte only) →
/// <see cref="StringFnV1AFullHasher"/> / <see cref="StringFnV1A64Hasher"/>
/// (cheap FNV-1a, full Unicode width) →
/// <see cref="StringJenkinsOaatHasher"/> (cheap, full Unicode width, multiply-free,
/// stronger per-bit avalanche than FNV-1a) →
/// <see cref="StringMurmur3Hasher"/> / <see cref="StringXxHash32Hasher"/> /
/// <see cref="StringXxHash64Hasher"/> / <see cref="StringMetroHash64Hasher"/> /
/// <see cref="StringCityHash64Hasher"/> (strong avalanche, maximum throughput).
/// Prefer it over the FNV-1a hashers when FNV-1a's single-multiply mixing
/// clusters your keys but you do not want to pay for a block hash; escalate to
/// the throughput-oriented strong family when the keys are clustered or
/// adversarial enough to push long probe chains regardless.
/// </para>
/// <para>
/// The empty string hashes to <c>0</c> — the accumulator starts at zero and the
/// three finalization steps leave zero unchanged. The dictionaries store the
/// out-of-band <c>null</c>-key entry without ever calling the hasher, so this
/// does not collide with the empty-slot sentinel.
/// </para>
/// </remarks>
public struct StringJenkinsOaatHasher : IHashProvider<string>
{
    /// <summary>
    /// Computes Bob Jenkins' one-at-a-time hash of the specified string over the
    /// full little-endian UTF-16 byte stream (both bytes of every character).
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit one-at-a-time hash of <paramref name="key"/>.</returns>
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
            // Mix the low byte then the high byte of each 16-bit code unit —
            // i.e. the one-at-a-time hash over the character's little-endian
            // UTF-16 bytes. The extra high-byte step keeps characters that share
            // a low byte but differ in their upper byte distinct, unlike
            // StringFnV1AHasher.
            hash += (byte)(c & 0xFF);
            hash += hash << 10;
            hash ^= hash >> 6;

            hash += (byte)(c >> 8);
            hash += hash << 10;
            hash ^= hash >> 6;
        }

        // Final avalanche.
        hash += hash << 3;
        hash ^= hash >> 11;
        hash += hash << 15;

        return unchecked((int)hash);
    }
}
