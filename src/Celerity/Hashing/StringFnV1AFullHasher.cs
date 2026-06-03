using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A hash provider for <see cref="string"/> keys using the FNV-1a 32-bit
/// algorithm over the <em>full</em> UTF-16 representation of the string — both
/// bytes of every character.
/// </summary>
/// <remarks>
/// This is the full-character-width counterpart to <see cref="StringFnV1AHasher"/>,
/// and directly answers the note in that hasher's remarks that "for keys with
/// significant non-ASCII content, a full UTF-8 or UTF-16 hash is preferable."
/// Where <see cref="StringFnV1AHasher"/> folds only the low byte of each
/// character (<c>c &amp; 0xFF</c>) and therefore cannot distinguish characters
/// that differ only in their upper byte — for example <c>'A'</c> (<c>U+0041</c>)
/// and <c>'Ł'</c> (<c>U+0141</c>), which it collides on — this hasher folds both
/// bytes of every 16-bit code unit, low byte then high byte, which is exactly the
/// FNV-1a of the string's native little-endian UTF-16 byte stream. It therefore
/// keeps those characters distinct while still costing only a pair of XOR /
/// multiply steps per character.
/// <para>
/// It sits in the <see cref="string"/> escalation ladder:
/// <see cref="StringFnV1AHasher"/> (cheapest, but low-byte only) →
/// <see cref="StringFnV1AFullHasher"/> (cheap, full Unicode width, 32-bit state) →
/// <see cref="StringFnV1A64Hasher"/> (full Unicode width, 64-bit state) →
/// <see cref="StringMurmur3Hasher"/> (the <c>fmix32</c> finalizer gives every
/// input bit influence over every output bit, holding distribution up on
/// clustered or adversarial keys). Prefer it over <see cref="StringFnV1AHasher"/>
/// whenever keys contain non-ASCII characters that the low-byte fold would
/// collide; step up to <see cref="StringFnV1A64Hasher"/> when keys are long or
/// numerous enough that the 32-bit accumulator starts clustering; escalate to
/// <see cref="StringMurmur3Hasher"/> when FNV-1a's weaker avalanche pushes
/// clustered or adversarial keys into long probe chains.
/// </para>
/// <para>
/// The empty string hashes to the FNV-1a offset basis (<c>2166136261</c>,
/// <c>-2128831035</c> as a signed <see cref="int"/>) — no characters are folded,
/// exactly as <see cref="StringFnV1AHasher"/> maps the empty string. The
/// dictionaries store the out-of-band <c>null</c>-key entry without ever calling
/// the hasher, so this does not collide with the empty-slot sentinel.
/// </para>
/// </remarks>
public struct StringFnV1AFullHasher : IHashProvider<string>
{
    /// <summary>
    /// Computes the FNV-1a 32-bit hash of the specified string over the full
    /// little-endian UTF-16 byte stream (both bytes of every character).
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit FNV-1a hash of <paramref name="key"/>.</returns>
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

        // The FNV-1a 32-bit parameters.
        const uint fnvPrime = 16777619u;
        const uint offsetBasis = 2166136261u;

        uint hash = offsetBasis;
        foreach (char c in key)
        {
            // Fold the low byte then the high byte of each 16-bit code unit —
            // i.e. FNV-1a over the character's little-endian UTF-16 bytes. The
            // extra high-byte step is what keeps characters that share a low byte
            // but differ in their upper byte distinct, unlike StringFnV1AHasher.
            hash ^= (byte)(c & 0xFF);
            hash *= fnvPrime;
            hash ^= (byte)(c >> 8);
            hash *= fnvPrime;
        }

        return unchecked((int)hash);
    }
}
