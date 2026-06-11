using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A hash provider for <see cref="string"/> keys using the standard
/// <em>Adler-32</em> checksum (RFC 1950 — the same variant used by zlib's DEFLATE
/// wrapper and the zlib chunk of PNG) over the full UTF-16 representation of the
/// string — both bytes of every character.
/// </summary>
/// <remarks>
/// Adler-32 is the other widely deployed checksum from the zlib family, alongside
/// <see cref="StringCrc32Hasher"/>. It carries two running 16-bit sums modulo
/// <c>65521</c> (the largest prime below 2^16): <c>a</c> accumulates the bytes
/// (seeded to <c>1</c>) and <c>b</c> accumulates the running value of <c>a</c>
/// after each byte (seeded to <c>0</c>); the 32-bit result is <c>(b &lt;&lt; 16) | a</c>.
/// It is computed here with the straightforward per-byte modulo form — two adds and
/// two modulo reductions per byte, with <em>no table</em> (unlike the table-driven
/// <see cref="StringCrc32Hasher"/>) and no finalizer — so the per-call path is
/// allocation-free.
/// <para>
/// As a checksum Adler-32 is <em>even weaker</em> than CRC-32 as a hash function:
/// for short inputs both sums stay numerically small, so the high bits of the 32-bit
/// result are barely populated and many short keys land in a narrow range — it
/// clusters sooner than <see cref="StringCrc32Hasher"/>, and far sooner than
/// <see cref="StringJenkinsOaatHasher"/> or the designed mixers. Its value here is
/// purely <em>compatibility</em>: Adler-32 is one of the most widely deployed
/// checksums (zlib, PNG, the rsync rolling-checksum family), and external systems
/// that shard, route, or bucket by it can be reproduced exactly with this hasher —
/// the same role <see cref="StringCrc32Hasher"/>, <see cref="StringMurmur2Hasher"/>,
/// and the FNV-1 hashers fill for their respective external systems. Do not reach
/// for it as a general-purpose hash; prefer <see cref="StringFnV1AFullHasher"/> or
/// <see cref="StringJenkinsOaatHasher"/> for that.
/// </para>
/// <para>
/// Like the other full-width string hashers it consumes the <em>full</em> 16-bit
/// value of every character — treating the string as its native little-endian UTF-16
/// byte stream (low byte then high byte of each character) — so it distinguishes
/// characters that differ only in their upper byte, for example <c>'A'</c>
/// (<c>U+0041</c>) and <c>'Ł'</c> (<c>U+0141</c>), which
/// <see cref="StringFnV1AHasher"/> collides on.
/// </para>
/// <para>
/// It sits at the cheapest, classic end of the <see cref="string"/> escalation
/// ladder, a checksum peer of <see cref="StringCrc32Hasher"/>:
/// <see cref="StringDjb2Hasher"/> / <see cref="StringDjb2AHasher"/> /
/// <see cref="StringSdbmHasher"/> / <see cref="StringElfHasher"/> /
/// <see cref="StringCrc32Hasher"/> / <see cref="StringAdler32Hasher"/> (cheapest
/// classics and checksums, weaker avalanche) / <see cref="StringFnV1AHasher"/>
/// (low-byte only) → <see cref="StringFnV1AFullHasher"/> /
/// <see cref="StringFnV1A64Hasher"/> (cheap FNV-1a, full Unicode width) →
/// <see cref="StringJenkinsOaatHasher"/> (cheap, full Unicode width, multiply-free,
/// stronger per-bit avalanche) → <see cref="StringMurmur3Hasher"/> /
/// <see cref="StringXxHash32Hasher"/> and the throughput-oriented strong family.
/// Reach for <see cref="StringAdler32Hasher"/> when you specifically need Adler-32
/// compatibility; for general use prefer <see cref="StringFnV1AFullHasher"/> or
/// <see cref="StringJenkinsOaatHasher"/>, and escalate to the strong family for
/// clustered or adversarial keys.
/// </para>
/// <para>
/// <c>StringAdler32Hasher.Hash(s)</c> equals the standard Adler-32 (RFC 1950 / zlib)
/// over <c>Encoding.Unicode.GetBytes(s)</c>. The empty string folds no characters,
/// so the sums stay at their seeds and it hashes to <c>1</c> (<c>a = 1</c>,
/// <c>b = 0</c>). The dictionaries store the out-of-band <c>null</c>-key entry
/// without ever calling the hasher, so this does not collide with the empty-slot
/// sentinel.
/// </para>
/// </remarks>
public struct StringAdler32Hasher : IHashProvider<string>
{
    /// <summary>
    /// The Adler-32 modulus: <c>65521</c>, the largest prime below 2^16.
    /// </summary>
    private const uint ModAdler = 65521u;

    /// <summary>
    /// Computes the standard Adler-32 (RFC 1950 / zlib) of the specified string over
    /// the full little-endian UTF-16 byte stream (both bytes of every character).
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit Adler-32 of <paramref name="key"/>.</returns>
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

        uint a = 1u;
        uint b = 0u;

        // Fold both bytes of every character in native little-endian UTF-16 order
        // (low byte then high byte) — exactly the byte stream a byte-oriented
        // Adler-32 would consume over Encoding.Unicode.GetBytes(key).
        int length = key.Length;
        for (int i = 0; i < length; i++)
        {
            char ch = key[i];

            a = (a + (byte)ch) % ModAdler;
            b = (b + a) % ModAdler;

            a = (a + (byte)(ch >> 8)) % ModAdler;
            b = (b + a) % ModAdler;
        }

        return unchecked((int)((b << 16) | a));
    }
}
