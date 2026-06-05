using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A hash provider for <see cref="string"/> keys using Austin Appleby's original
/// <em>MurmurHash2</em> (32-bit) algorithm over the full UTF-16 representation of
/// the string — both bytes of every character.
/// </summary>
/// <remarks>
/// This is the MurmurHash2 predecessor of <see cref="StringMurmur3Hasher"/>: the
/// two share the same family and the same multiply constant
/// (<c>m = 0x5bd1e995</c>), but MurmurHash2 uses a single mixing pass per 4-byte
/// block (<c>k *= m; k ^= k &gt;&gt; 24; k *= m; h *= m; h ^= k</c>) and a lighter
/// two-shift finalization (<c>h ^= h &gt;&gt; 13; h *= m; h ^= h &gt;&gt; 15</c>),
/// where MurmurHash3 adds a rotate-based block mix and the stronger
/// <c>fmix32</c> finalizer. The MurmurHash3 finalizer gives every input bit
/// influence over every output bit and avoids the few known MurmurHash2 weaknesses
/// (most notably that XOR-equal differences in adjacent blocks can cancel), so
/// <see cref="StringMurmur3Hasher"/> is the generally preferred member of the
/// family and the recommended default. <see cref="StringMurmur2Hasher"/> is
/// provided for users who specifically need MurmurHash2 — for example to match an
/// external system (Hadoop, Cassandra, Elasticsearch, and many client libraries
/// have historically hashed with MurmurHash2) — or who want its slightly cheaper
/// single-multiply-per-block mix on trusted, already-uniform keys.
/// <para>
/// Like the other full-width string hashers it consumes the <em>full</em> 16-bit
/// value of every character — treating the string as its native little-endian
/// UTF-16 byte stream — so it distinguishes characters that differ only in their
/// upper byte, for example <c>'A'</c> (<c>U+0041</c>) and <c>'Ł'</c>
/// (<c>U+0141</c>), which <see cref="StringFnV1AHasher"/> collides on. Two
/// consecutive characters form one little-endian 32-bit block (the first supplies
/// the low 16 bits, the second the high 16 bits); a trailing odd character is
/// folded as a 2-byte tail.
/// </para>
/// <para>
/// It sits at the strong-distribution top of the <see cref="string"/> escalation
/// ladder as a same-family peer of <see cref="StringMurmur3Hasher"/>:
/// <see cref="StringFnV1AHasher"/> (cheapest, low-byte only) →
/// <see cref="StringFnV1AFullHasher"/> / <see cref="StringFnV1A64Hasher"/> (cheap
/// FNV-1a, full Unicode width) → <see cref="StringJenkinsOaatHasher"/> (cheap,
/// full Unicode width, multiply-free, stronger per-bit avalanche) →
/// <see cref="StringMurmur2Hasher"/> / <see cref="StringMurmur3Hasher"/> /
/// <see cref="StringXxHash32Hasher"/> and the throughput-oriented strong family.
/// Prefer <see cref="StringMurmur3Hasher"/> for general use; reach for
/// <see cref="StringMurmur2Hasher"/> when you need MurmurHash2 compatibility
/// specifically.
/// </para>
/// <para>
/// <c>StringMurmur2Hasher.Hash(s)</c> equals canonical MurmurHash2 (seed <c>0</c>)
/// over <c>Encoding.Unicode.GetBytes(s)</c>. The empty string folds no characters
/// and hashes to <c>0</c> (seed <c>0</c> XOR a zero byte length, left unchanged by
/// the finalization mix), exactly as <see cref="StringMurmur3Hasher"/> maps the
/// empty string. The dictionaries store the out-of-band <c>null</c>-key entry
/// without ever calling the hasher, so this does not collide with the empty-slot
/// sentinel.
/// </para>
/// </remarks>
public struct StringMurmur2Hasher : IHashProvider<string>
{
    private const uint M = 0x5bd1e995u;
    private const int R = 24;

    /// <summary>
    /// Computes the MurmurHash2 (32-bit, seed <c>0</c>) hash of the specified
    /// string over the full little-endian UTF-16 byte stream (both bytes of every
    /// character).
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit MurmurHash2 hash of <paramref name="key"/>.</returns>
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

        int length = key.Length;

        // MurmurHash2 seeds the accumulator with seed ^ byteLength; seed is 0 here,
        // and the byte length is twice the UTF-16 code-unit count.
        uint h = (uint)(length * 2);

        // Consume two chars (one 32-bit little-endian block) per iteration: the
        // first char supplies the low 16 bits, the second the high 16 bits — exactly
        // the 4-byte block a byte-oriented MurmurHash2 would read over the native
        // little-endian UTF-16 representation of the string.
        int i = 0;
        for (; i + 1 < length; i += 2)
        {
            uint k = (uint)key[i] | ((uint)key[i + 1] << 16);

            k *= M;
            k ^= k >> R;
            k *= M;

            h *= M;
            h ^= k;
        }

        // Tail: a single trailing char (2 bytes) when the length is odd. The two
        // tail bytes are the low and high byte of that char; XORing both in is
        // exactly XORing the 16-bit char value, followed by the tail multiply.
        if ((length & 1) != 0)
        {
            h ^= (uint)key[length - 1];
            h *= M;
        }

        // MurmurHash2 finalization mix.
        h ^= h >> 13;
        h *= M;
        h ^= h >> 15;

        return unchecked((int)h);
    }
}
