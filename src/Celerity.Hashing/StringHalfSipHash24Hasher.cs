using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A keyed, hash-flooding-resistant hash provider for <see cref="string"/> keys
/// using the HalfSipHash-2-4 pseudorandom function over the string's native
/// little-endian UTF-16 byte stream, producing a native 32-bit result.
/// </summary>
/// <remarks>
/// HalfSipHash (Jean-Philippe Aumasson &amp; Daniel J. Bernstein) is the 32-bit-word
/// sibling of <see cref="StringSipHash24Hasher">SipHash</see>: the same keyed
/// add-rotate-xor (ARX) construction and the same <em>hash-flooding</em> defence —
/// an adversary who controls the keys cannot construct a flood of values that
/// collide into the same bucket without recovering the secret key — but built on
/// four <em>32-bit</em> state words (<c>v0..v3</c>) advanced over 4-byte (two-char)
/// message words instead of SipHash's 64-bit words over 8-byte words. That narrower
/// state makes each round cheaper and the per-call fixed overhead lower, so on the
/// short keys typical of identifiers — and on 32-bit-leaning targets — it is the
/// faster keyed option, at the cost of the wider cryptographic margin the full
/// 64-bit SipHash carries on long inputs. The <c>2-4</c> suffix is the round count:
/// two HalfSipRounds of compression per 4-byte (two-char) message word and four
/// HalfSipRounds of finalization, matching <see cref="StringSipHash24Hasher"/>.
/// <para>
/// Unlike every other <see cref="string"/> hasher here, HalfSipHash's primitive
/// output is <em>already</em> 32 bits wide (<c>v1 ^ v3</c> after finalization), so
/// this hasher returns it directly — there is no 64-bit-to-32-bit xor-fold to lose
/// entropy through. This places <see cref="StringHalfSipHash24Hasher"/> on the
/// keyed tier of the <see cref="string"/> escalation ladder alongside its 64-bit
/// relatives, as the cheaper keyed choice for shorter keys:
/// <see cref="StringFnV1AHasher"/> (cheapest, low-byte only) →
/// <see cref="StringFnV1AFullHasher"/> (cheap, full Unicode width, 32-bit state) →
/// <see cref="StringFnV1A64Hasher"/> (full Unicode width, 64-bit state) →
/// <see cref="StringMurmur3Hasher"/> / <see cref="StringXxHash32Hasher"/> /
/// <see cref="StringXxHash64Hasher"/> / <see cref="StringMetroHash64Hasher"/> /
/// <see cref="StringCityHash64Hasher"/> (strong avalanche, maximum throughput) →
/// <see cref="StringHalfSipHash24Hasher"/> (keyed, 32-bit, cheapest on short keys) /
/// <see cref="StringSipHash13Hasher"/> / <see cref="StringSipHash24Hasher"/> /
/// <see cref="StringHighwayHash64Hasher"/> (keyed hash-flooding resistance). Reach
/// for a keyed hasher when the keys originate from an untrusted source (request
/// paths, header names, user-supplied identifiers) and an adversary could otherwise
/// deliberately drive worst-case collisions; prefer the throughput family on
/// trusted keys. Among the keyed options, prefer
/// <see cref="StringHalfSipHash24Hasher"/> for short keys / 32-bit targets and the
/// 64-bit <see cref="StringSipHash24Hasher"/> when the wider margin matters on long
/// inputs. Like the other full-width string hashers it consumes the <em>full</em>
/// 16-bit value of every character — treating the string as its native
/// little-endian UTF-16 byte stream — so it distinguishes characters that differ
/// only in their upper byte (for example <c>'A'</c> (<c>U+0041</c>) and
/// <c>'Ł'</c> (<c>U+0141</c>), which <see cref="StringFnV1AHasher"/> collides on).
/// </para>
/// <para>
/// Because Celerity hashers are zero-state structs that collections construct via
/// <c>new THasher()</c>, this hasher carries a <em>fixed</em> built-in key (the
/// canonical HalfSipHash reference key, bytes <c>00..07</c>) rather than a
/// per-process secret. That is enough to give every Celerity collection HalfSipHash's
/// strong, well-distributed avalanche and to defeat collision sets crafted against a
/// weaker published algorithm, but a determined attacker who knows the table uses
/// this exact fixed key could still precompute collisions against it. Callers who
/// need full secret-keyed hash-flooding resistance (a key the attacker cannot know)
/// should seed a per-process key out of band; this type's value is a strong,
/// standards-based mixing function with a clear upgrade path, not a turnkey secret —
/// exactly mirroring the <see cref="StringSipHash24Hasher"/> caveat.
/// </para>
/// <para>
/// <c>StringHalfSipHash24Hasher.Hash(s)</c> equals canonical HalfSipHash-2-4 (4-byte
/// output, with the fixed key above) over <c>Encoding.Unicode.GetBytes(s)</c>,
/// reinterpreted as a signed 32-bit integer. The empty string maps to
/// HalfSipHash-2-4's length-<c>0</c> output for that key (the published reference
/// vector <c>0x5B9F35A9</c>) since its UTF-16 byte stream is zero bytes. The
/// dictionaries store the out-of-band <c>null</c>-key entry without ever calling the
/// hasher, so this does not collide with the empty-slot sentinel.
/// </para>
/// </remarks>
public struct StringHalfSipHash24Hasher : IHashProvider<string>
{
    // Canonical HalfSipHash reference key (test-vector key, bytes 00..07), read as
    // two little-endian 32-bit halves. Fixed because collections build the hasher
    // via new THasher() and so cannot supply a per-process secret.
    private const uint K0 = 0x03020100U;
    private const uint K1 = 0x07060504U;

    // HalfSipHash initialization constants: the high 32 bits of SipHash's v2/v3
    // "somepseudorandomlygeneratedbytes" words (v0/v1 start at zero).
    private const uint Init2 = 0x6C796765U;
    private const uint Init3 = 0x74656462U;

    /// <summary>
    /// Computes the HalfSipHash-2-4 hash of the specified string over its native
    /// little-endian UTF-16 byte stream (using this type's fixed built-in key),
    /// returning the native 32-bit result as a signed integer.
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit HalfSipHash-2-4 hash of <paramref name="key"/>.</returns>
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

        int charLen = key.Length;               // count of UTF-16 code units (chars)
        uint byteLen = (uint)charLen * 2U;      // HalfSipHash operates on the byte length

        uint v0 = K0;                           // Init0 is 0, so v0 = 0 ^ K0
        uint v1 = K1;                           // Init1 is 0, so v1 = 0 ^ K1
        uint v2 = Init2 ^ K0;
        uint v3 = Init3 ^ K1;

        // Each 32-bit message word is four bytes — two UTF-16 code units. The byte
        // stream is always even-length, so the tail is 0 or 1 char (0 or 2 bytes).
        int fullWords = charLen >> 1;           // charLen / 2 == byteLen / 4
        int p = 0;                              // char index of the current word
        for (int w = 0; w < fullWords; w++)
        {
            uint m = Word(key, p);
            v3 ^= m;
            SipRound(ref v0, ref v1, ref v2, ref v3);
            SipRound(ref v0, ref v1, ref v2, ref v3);
            v0 ^= m;
            p += 2;
        }

        // Final block: the top byte holds the total input length (mod 256); the low
        // bytes hold the leftover tail. The UTF-16 stream is even-length, so the
        // tail is either empty or exactly one char contributing its 16 bits.
        uint b = byteLen << 24;
        if ((charLen & 1) != 0)
            b |= key[charLen - 1];

        v3 ^= b;
        SipRound(ref v0, ref v1, ref v2, ref v3);
        SipRound(ref v0, ref v1, ref v2, ref v3);
        v0 ^= b;

        // Finalization: four HalfSipRounds after folding 0xFF into v2.
        v2 ^= 0xFFU;
        SipRound(ref v0, ref v1, ref v2, ref v3);
        SipRound(ref v0, ref v1, ref v2, ref v3);
        SipRound(ref v0, ref v1, ref v2, ref v3);
        SipRound(ref v0, ref v1, ref v2, ref v3);

        // HalfSipHash's 4-byte output is already 32 bits wide — no fold required.
        return unchecked((int)(v1 ^ v3));
    }

    /// <summary>
    /// Reads one 32-bit little-endian message word (four bytes / two UTF-16 code
    /// units) starting at char index <paramref name="i"/>: each successive char
    /// supplies the next 16 bits — exactly the 4-byte word a byte-oriented
    /// HalfSipHash would read over the native little-endian UTF-16 stream.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Word(string key, int i) =>
        key[i] | ((uint)key[i + 1] << 16);

    /// <summary>
    /// The HalfSipHash round function: one fixed lattice of 32-bit add / rotate / xor
    /// steps over the four state words. HalfSipHash-2-4 runs it twice per message
    /// word (compression) and four times at the end (finalization).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SipRound(ref uint v0, ref uint v1, ref uint v2, ref uint v3)
    {
        v0 += v1;
        v1 = BitOperations.RotateLeft(v1, 5);
        v1 ^= v0;
        v0 = BitOperations.RotateLeft(v0, 16);
        v2 += v3;
        v3 = BitOperations.RotateLeft(v3, 8);
        v3 ^= v2;
        v0 += v3;
        v3 = BitOperations.RotateLeft(v3, 7);
        v3 ^= v0;
        v2 += v1;
        v1 = BitOperations.RotateLeft(v1, 13);
        v1 ^= v2;
        v2 = BitOperations.RotateLeft(v2, 16);
    }
}
