using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A keyed, hash-flooding-resistant hash provider for <see cref="string"/> keys
/// using the SipHash-1-3 pseudorandom function over the string's native
/// little-endian UTF-16 byte stream, xor-folded down to a signed 32-bit result.
/// </summary>
/// <remarks>
/// SipHash (Jean-Philippe Aumasson &amp; Daniel J. Bernstein, 2012) is a keyed
/// add-rotate-xor (ARX) pseudorandom function designed specifically to resist
/// <em>hash flooding</em>: an adversary who controls the keys cannot construct a
/// flood of values that collide into the same bucket and degrade a hash table to
/// O(n) probe chains, because doing so would require recovering the secret key —
/// a problem SipHash is built to make infeasible. The two-digit suffix is the
/// round count: SipHash-<c>c</c>-<c>d</c> runs <c>c</c> SipRounds of compression
/// per 8-byte (four-char) message word and <c>d</c> SipRounds of finalization.
/// This type is the <c>1-3</c> variant — <strong>one</strong> compression round
/// and <strong>three</strong> finalization rounds — the reduced-round sibling of
/// <see cref="StringSipHash24Hasher"/> (the conservative <c>2-4</c> variant). It is
/// the exact construction Rust's standard-library <c>HashMap</c> uses by default,
/// chosen there as the sweet spot between throughput and flooding resistance:
/// halving the compression work makes it materially faster than SipHash-2-4 on
/// every word while keeping the keyed, well-distributed avalanche that defeats
/// adversarial collision sets. Each SipRound is a fixed lattice of 64-bit
/// add / rotate / xor steps over four words of state (<c>v0..v3</c>), so — unlike
/// a cryptographic hash built on a compression function — it carries no large
/// table and stays branch-light.
/// <para>
/// This places <see cref="StringSipHash13Hasher"/> at the strong-avalanche top of
/// the <see cref="string"/> escalation ladder, alongside
/// <see cref="StringSipHash24Hasher"/> and <see cref="StringHighwayHash64Hasher"/>
/// as the <em>keyed</em> options, but it is a different <em>kind</em> of answer
/// from the throughput-oriented family there:
/// <see cref="StringFnV1AHasher"/> (cheapest, low-byte only) →
/// <see cref="StringFnV1AFullHasher"/> (cheap, full Unicode width, 32-bit state) →
/// <see cref="StringFnV1A64Hasher"/> (full Unicode width, 64-bit state) →
/// <see cref="StringMurmur3Hasher"/> / <see cref="StringXxHash32Hasher"/> /
/// <see cref="StringXxHash64Hasher"/> / <see cref="StringMetroHash64Hasher"/> /
/// <see cref="StringCityHash64Hasher"/> (strong avalanche, maximum throughput,
/// unkeyed) → <see cref="StringSipHash13Hasher"/> / <see cref="StringSipHash24Hasher"/> /
/// <see cref="StringHighwayHash64Hasher"/> (strong avalanche, <em>keyed</em>
/// hash-flooding resistance). The unkeyed throughput peers are faster on trusted
/// keys and should be preferred there; reach for a keyed option when the keys
/// originate from an untrusted source (request paths, header names, user-supplied
/// identifiers) and an adversary could otherwise deliberately drive worst-case
/// collisions. Between the SipHash variants, prefer <see cref="StringSipHash13Hasher"/>
/// (Rust's choice) when you want the keyed defense at the lowest cost, and
/// <see cref="StringSipHash24Hasher"/> when you want the extra cryptographic margin
/// of the conservative round counts. Like the other full-width string hashers it
/// consumes the <em>full</em> 16-bit value of every character — treating the string
/// as its native little-endian UTF-16 byte stream — so it distinguishes characters
/// that differ only in their upper byte (for example <c>'A'</c> (<c>U+0041</c>) and
/// <c>'Ł'</c> (<c>U+0141</c>), which <see cref="StringFnV1AHasher"/> collides on).
/// </para>
/// <para>
/// Because Celerity hashers are zero-state structs that collections construct via
/// <c>new THasher()</c>, this hasher carries a <em>fixed</em> built-in key (the
/// canonical SipHash reference key, bytes <c>00..0f</c>) rather than a per-process
/// secret. That is enough to give every Celerity collection SipHash's strong,
/// well-distributed avalanche and to defeat collision sets crafted against a
/// weaker published algorithm, but a determined attacker who knows the table uses
/// this exact fixed key could still precompute collisions against it. Callers who
/// need full secret-keyed hash-flooding resistance (a key the attacker cannot
/// know) should seed a per-process key out of band; this type's value is a strong,
/// standards-based mixing function with a clear upgrade path, not a turnkey secret.
/// </para>
/// <para>
/// <c>StringSipHash13Hasher.Hash(s)</c> equals canonical SipHash-1-3 (with the
/// fixed key above) over <c>Encoding.Unicode.GetBytes(s)</c>, xor-folded to a
/// signed 32-bit integer (<c>h ^ (h &gt;&gt; 32)</c>, keeping the high-half
/// entropy in the result). The empty string maps to SipHash-1-3's length-<c>0</c>
/// output for that key (the reference vector <c>0xABAC0158050FC4DC</c>) folded to
/// 32 bits (its UTF-16 byte stream is zero bytes). The dictionaries store the
/// out-of-band <c>null</c>-key entry without ever calling the hasher, so this does
/// not collide with the empty-slot sentinel.
/// </para>
/// </remarks>
public struct StringSipHash13Hasher : IHashProvider<string>
{
    // Canonical SipHash reference key (RFC-draft test-vector key, bytes 00..0f),
    // read as two little-endian 64-bit halves. Fixed because collections build the
    // hasher via new THasher() and so cannot supply a per-process secret.
    private const ulong K0 = 0x0706050403020100UL;
    private const ulong K1 = 0x0F0E0D0C0B0A0908UL;

    // SipHash initialization constants ("somepseudorandomlygeneratedbytes").
    private const ulong Init0 = 0x736F6D6570736575UL;
    private const ulong Init1 = 0x646F72616E646F6DUL;
    private const ulong Init2 = 0x6C7967656E657261UL;
    private const ulong Init3 = 0x7465646279746573UL;

    /// <summary>
    /// Computes the SipHash-1-3 hash of the specified string over its native
    /// little-endian UTF-16 byte stream (using this type's fixed built-in key),
    /// xor-folded to a signed 32-bit result.
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit, xor-folded SipHash-1-3 hash of <paramref name="key"/>.</returns>
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
        ulong byteLen = (ulong)charLen * 2UL;   // SipHash operates on the byte length

        ulong v0 = Init0 ^ K0;
        ulong v1 = Init1 ^ K1;
        ulong v2 = Init2 ^ K0;
        ulong v3 = Init3 ^ K1;

        // Each 64-bit message word is eight bytes — four UTF-16 code units. The
        // byte stream is always even-length, so the tail is 0, 1, 2, or 3 chars.
        // SipHash-1-3 runs a single compression SipRound per message word.
        int fullWords = charLen >> 2;           // charLen / 4 == byteLen / 8
        int p = 0;                              // char index of the current word
        for (int w = 0; w < fullWords; w++)
        {
            ulong m = Word(key, p);
            v3 ^= m;
            SipRound(ref v0, ref v1, ref v2, ref v3);
            v0 ^= m;
            p += 4;
        }

        // Final block: the top byte holds the total input length (mod 256); the
        // low bytes hold the leftover tail, each char contributing its 16 bits at
        // the matching little-endian offset (2 bytes per char).
        ulong b = byteLen << 56;
        int tailChars = charLen & 3;
        for (int j = 0; j < tailChars; j++)
            b |= (ulong)key[p + j] << (16 * j);

        v3 ^= b;
        SipRound(ref v0, ref v1, ref v2, ref v3);
        v0 ^= b;

        // Finalization: three SipRounds after folding 0xFF into v2.
        v2 ^= 0xFFUL;
        SipRound(ref v0, ref v1, ref v2, ref v3);
        SipRound(ref v0, ref v1, ref v2, ref v3);
        SipRound(ref v0, ref v1, ref v2, ref v3);

        ulong h = v0 ^ v1 ^ v2 ^ v3;

        // Xor-fold the 64-bit state down to 32 bits, keeping the high-half entropy.
        return unchecked((int)(h ^ (h >> 32)));
    }

    /// <summary>
    /// Reads one 64-bit little-endian message word (eight bytes / four UTF-16 code
    /// units) starting at char index <paramref name="i"/>: each successive char
    /// supplies the next 16 bits — exactly the 8-byte word a byte-oriented SipHash
    /// would read over the native little-endian UTF-16 stream.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Word(string key, int i) =>
        (ulong)key[i]
        | ((ulong)key[i + 1] << 16)
        | ((ulong)key[i + 2] << 32)
        | ((ulong)key[i + 3] << 48);

    /// <summary>
    /// The SipHash round function: one fixed lattice of 64-bit add / rotate / xor
    /// steps over the four state words. SipHash-1-3 runs it once per message word
    /// (compression) and three times at the end (finalization).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SipRound(ref ulong v0, ref ulong v1, ref ulong v2, ref ulong v3)
    {
        v0 += v1;
        v1 = BitOperations.RotateLeft(v1, 13);
        v1 ^= v0;
        v0 = BitOperations.RotateLeft(v0, 32);
        v2 += v3;
        v3 = BitOperations.RotateLeft(v3, 16);
        v3 ^= v2;
        v0 += v3;
        v3 = BitOperations.RotateLeft(v3, 21);
        v3 ^= v0;
        v2 += v1;
        v1 = BitOperations.RotateLeft(v1, 17);
        v1 ^= v2;
        v2 = BitOperations.RotateLeft(v2, 32);
    }
}
