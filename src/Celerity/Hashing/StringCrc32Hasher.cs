using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A hash provider for <see cref="string"/> keys using the standard
/// <em>CRC-32</em> (ISO-HDLC / IEEE 802.3 — the same variant used by zlib, gzip,
/// PNG, and Ethernet) over the full UTF-16 representation of the string — both
/// bytes of every character.
/// </summary>
/// <remarks>
/// CRC-32 is a cyclic redundancy check, not a designed hash function: it is the
/// remainder of the message — interpreted as a polynomial over GF(2) — divided by
/// the reflected generator polynomial <c>0xEDB88320</c>, with the accumulator
/// pre-set to <c>0xFFFFFFFF</c> and the final value bit-inverted (XOR
/// <c>0xFFFFFFFF</c>). It is implemented here with the classic 256-entry byte
/// lookup table (one table lookup, one XOR, and one shift per byte), so it is the
/// first <em>table-driven</em> member of the <see cref="string"/> hasher family —
/// the cheap classics <see cref="StringDjb2Hasher"/>, <see cref="StringSdbmHasher"/>,
/// and <see cref="StringElfHasher"/> are all table-free. The table is a single
/// <c>static readonly</c> array built once at type initialization, so the hot path
/// stays allocation-free.
/// <para>
/// Because CRC-32 is a <em>linear</em> function over GF(2), it has weaker avalanche
/// than the designed mixers: flipping one input bit flips a fixed, input-independent
/// set of output bits, and the low bits in particular mix poorly, so for adversarial
/// or heavily clustered keys it clusters sooner than <see cref="StringJenkinsOaatHasher"/>
/// or the strong block hashes. Its value here is <em>compatibility</em>: CRC-32 is
/// one of the most widely deployed checksums, and many external systems shard, route,
/// or bucket by it — so <see cref="StringCrc32Hasher"/> is the in-box answer when you
/// need to reproduce a CRC-32-based key distribution exactly (for example to match an
/// existing partitioning scheme), the same role <see cref="StringMurmur2Hasher"/> and
/// the FNV-1 hashers fill for their external systems. On its own merits it still
/// distributes the short, structured identifiers typical of real key sets reasonably
/// well, at a per-byte cost comparable to the cheap classics.
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
/// ladder, a peer of the table-free classics but distinguished by being
/// table-driven and a checksum rather than a designed mixer:
/// <see cref="StringDjb2Hasher"/> / <see cref="StringDjb2AHasher"/> /
/// <see cref="StringSdbmHasher"/> / <see cref="StringElfHasher"/> /
/// <see cref="StringCrc32Hasher"/> (cheapest classics, weaker avalanche) /
/// <see cref="StringFnV1AHasher"/> (low-byte only) →
/// <see cref="StringFnV1AFullHasher"/> / <see cref="StringFnV1A64Hasher"/> (cheap
/// FNV-1a, full Unicode width) → <see cref="StringJenkinsOaatHasher"/> (cheap, full
/// Unicode width, multiply-free, stronger per-bit avalanche) →
/// <see cref="StringMurmur3Hasher"/> / <see cref="StringXxHash32Hasher"/> and the
/// throughput-oriented strong family. Reach for <see cref="StringCrc32Hasher"/> when
/// you specifically need CRC-32 compatibility; for general use prefer
/// <see cref="StringFnV1AFullHasher"/> or <see cref="StringJenkinsOaatHasher"/>, and
/// escalate to the strong family for clustered or adversarial keys.
/// </para>
/// <para>
/// <c>StringCrc32Hasher.Hash(s)</c> equals the standard CRC-32 (zlib / IEEE 802.3)
/// over <c>Encoding.Unicode.GetBytes(s)</c>. The empty string folds no characters
/// and hashes to <c>0</c> (the initial <c>0xFFFFFFFF</c> XOR the final
/// <c>0xFFFFFFFF</c> leaves zero). The dictionaries store the out-of-band
/// <c>null</c>-key entry without ever calling the hasher, so this does not collide
/// with the empty-slot sentinel.
/// </para>
/// </remarks>
public struct StringCrc32Hasher : IHashProvider<string>
{
    /// <summary>The reflected CRC-32 (ISO-HDLC / IEEE 802.3) generator polynomial.</summary>
    private const uint Polynomial = 0xEDB88320u;

    /// <summary>
    /// The 256-entry byte lookup table, built once at type initialization. A
    /// <c>static readonly</c> array keeps the per-call <see cref="Hash(string)"/>
    /// path allocation-free.
    /// </summary>
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int bit = 0; bit < 8; bit++)
            {
                c = (c & 1u) != 0 ? Polynomial ^ (c >> 1) : c >> 1;
            }

            table[i] = c;
        }

        return table;
    }

    /// <summary>
    /// Computes the standard CRC-32 (ISO-HDLC / IEEE 802.3 / zlib) of the specified
    /// string over the full little-endian UTF-16 byte stream (both bytes of every
    /// character).
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit CRC-32 of <paramref name="key"/>.</returns>
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

        uint[] table = Table;
        uint crc = 0xFFFFFFFFu;

        // Fold both bytes of every character in native little-endian UTF-16 order
        // (low byte then high byte) — exactly the byte stream a byte-oriented CRC-32
        // would consume over Encoding.Unicode.GetBytes(key).
        int length = key.Length;
        for (int i = 0; i < length; i++)
        {
            char ch = key[i];
            crc = table[(crc ^ (byte)ch) & 0xFFu] ^ (crc >> 8);
            crc = table[(crc ^ (byte)(ch >> 8)) & 0xFFu] ^ (crc >> 8);
        }

        return unchecked((int)(crc ^ 0xFFFFFFFFu));
    }
}
