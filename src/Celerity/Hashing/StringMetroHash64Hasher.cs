using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A high-quality, throughput-oriented hash provider for <see cref="string"/>
/// keys using the MetroHash64 algorithm (J. Andrew Rogers' <c>metrohash64_1</c>
/// variant) over the string's native little-endian UTF-16 byte stream, with a
/// seed of <c>0</c>, xor-folded down to a signed 32-bit result.
/// </summary>
/// <remarks>
/// MetroHash is a family of statistically robust hash functions designed for
/// maximum throughput on modern 64-bit CPUs. Like <see cref="StringXxHash64Hasher"/>
/// it processes the input in 32-byte stripes (sixteen chars) across four
/// independent <em>64-bit</em> accumulators, but its lattice of multiply / rotate /
/// add steps gives the out-of-order core four independent multiply chains to keep
/// in flight, so on mid-length keys it is competitive with — and often beats — the
/// xxHash family. The 64-bit result is reduced to 32 bits by xor-folding the high
/// half into the low half (<c>h ^ (h &gt;&gt; 32)</c>), keeping the extra high-half
/// entropy in the returned value, exactly as <see cref="StringXxHash64Hasher"/> and
/// <see cref="StringFnV1A64Hasher"/> do.
/// <para>
/// It sits at the strong-distribution top of the <see cref="string"/> escalation
/// ladder alongside <see cref="StringMurmur3Hasher"/>,
/// <see cref="StringXxHash32Hasher"/>, and <see cref="StringXxHash64Hasher"/>:
/// <see cref="StringFnV1AHasher"/> (cheapest, low-byte only) →
/// <see cref="StringFnV1AFullHasher"/> (cheap, full Unicode width, 32-bit state) →
/// <see cref="StringFnV1A64Hasher"/> (full Unicode width, 64-bit state) →
/// <see cref="StringMurmur3Hasher"/> / <see cref="StringXxHash32Hasher"/> /
/// <see cref="StringXxHash64Hasher"/> / <see cref="StringMetroHash64Hasher"/>
/// (strong avalanche). Like the other full-width string hashers it consumes the
/// <em>full</em> 16-bit value of every character — treating the string as its
/// native little-endian UTF-16 byte stream — so it distinguishes characters that
/// differ only in their upper byte (for example <c>'A'</c> (<c>U+0041</c>) and
/// <c>'Ł'</c> (<c>U+0141</c>), which <see cref="StringFnV1AHasher"/> collides on).
/// <see cref="StringMetroHash64Hasher"/>, <see cref="StringXxHash64Hasher"/>, and
/// <see cref="StringXxHash32Hasher"/> are peers at the throughput-oriented top of
/// the ladder — profile on your own key shape to pick between them; prefer
/// <see cref="StringMurmur3Hasher"/> for very short keys, where its simpler
/// single-accumulator loop has less fixed overhead. All are good answers when
/// FNV-1a's weaker avalanche pushes clustered or adversarial keys into long probe
/// chains.
/// </para>
/// <para>
/// <c>StringMetroHash64Hasher.Hash(s)</c> equals canonical MetroHash64
/// (<c>metrohash64_1</c>, seed <c>0</c>) over <c>Encoding.Unicode.GetBytes(s)</c>,
/// xor-folded to a signed 32-bit integer. The empty string maps to the algorithm's
/// length-<c>0</c> result — <c>finalize(k2 * k0)</c> — folded to 32 bits (its byte
/// length is <c>0</c> and no stripes or lanes are consumed). The dictionaries store
/// the out-of-band <c>null</c>-key entry without ever calling the hasher, so this
/// does not collide with the empty-slot sentinel.
/// </para>
/// </remarks>
public struct StringMetroHash64Hasher : IHashProvider<string>
{
    // metrohash64_1 mixing constants (J. Andrew Rogers, public-domain reference).
    private const ulong K0 = 0xC83A91E1UL;
    private const ulong K1 = 0x8648DBDBUL;
    private const ulong K2 = 0x7BDEC03BUL;
    private const ulong K3 = 0x2F5870A5UL;

    /// <summary>
    /// Computes the MetroHash64 (<c>metrohash64_1</c>, seed <c>0</c>) hash of the
    /// specified string over its native little-endian UTF-16 byte stream, xor-folded
    /// to a signed 32-bit result.
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit, xor-folded MetroHash64 hash of <paramref name="key"/>.</returns>
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

        int length = key.Length;                // count of UTF-16 code units (chars)
        ulong byteLength = (ulong)length * 2UL; // MetroHash mixes in the byte length

        // Seed 0: hash starts at ((0 + k2) * k0) + len.
        ulong hash = (K2 * K0) + byteLength;
        int i = 0;

        // Bulk path: process 32-byte stripes (sixteen chars) across four
        // accumulators while at least a full stripe of chars remains.
        if (length >= 16)
        {
            ulong v0 = hash, v1 = hash, v2 = hash, v3 = hash;

            do
            {
                v0 += Lane(key, i) * K0; i += 4; v0 = BitOperations.RotateRight(v0, 29) + v2;
                v1 += Lane(key, i) * K1; i += 4; v1 = BitOperations.RotateRight(v1, 29) + v3;
                v2 += Lane(key, i) * K2; i += 4; v2 = BitOperations.RotateRight(v2, 29) + v0;
                v3 += Lane(key, i) * K3; i += 4; v3 = BitOperations.RotateRight(v3, 29) + v1;
            }
            while (i <= length - 16);

            v2 ^= BitOperations.RotateRight(((v0 + v3) * K0) + v1, 33) * K1;
            v3 ^= BitOperations.RotateRight(((v1 + v2) * K1) + v0, 33) * K0;
            v0 ^= BitOperations.RotateRight(((v0 + v2) * K0) + v3, 33) * K1;
            v1 ^= BitOperations.RotateRight(((v1 + v3) * K1) + v2, 33) * K0;
            hash += v0 ^ v1;
        }

        // Tail: a 16-byte block (eight chars) across two accumulators.
        if (length - i >= 8)
        {
            ulong v0 = hash + (Lane(key, i) * K0); i += 4; v0 = BitOperations.RotateRight(v0, 33) * K1;
            ulong v1 = hash + (Lane(key, i) * K1); i += 4; v1 = BitOperations.RotateRight(v1, 33) * K2;
            v0 ^= BitOperations.RotateRight(v0 * K0, 35) + v1;
            v1 ^= BitOperations.RotateRight(v1 * K3, 35) + v0;
            hash += v1;
        }

        // Tail: a single 8-byte lane (four chars).
        if (length - i >= 4)
        {
            hash += Lane(key, i) * K3; i += 4;
            hash ^= BitOperations.RotateRight(hash, 33) * K1;
        }

        // Tail: a 4-byte block (two chars).
        if (length - i >= 2)
        {
            hash += (ulong)Block(key, i) * K3; i += 2;
            hash ^= BitOperations.RotateRight(hash, 15) * K1;
        }

        // Tail: a single trailing 2-byte char (the canonical 16-bit tail read).
        // The UTF-16 byte stream is always even-length, so MetroHash's final
        // single-byte tail step never applies here.
        if (length - i >= 1)
        {
            hash += (ulong)key[i] * K3;
            hash ^= BitOperations.RotateRight(hash, 13) * K1;
        }

        // Finalization.
        hash ^= BitOperations.RotateRight(hash, 33);
        hash *= K0;
        hash ^= BitOperations.RotateRight(hash, 33);

        // Xor-fold the 64-bit state down to 32 bits, keeping the high-half entropy.
        return unchecked((int)(hash ^ (hash >> 32)));
    }

    /// <summary>
    /// Reads one 64-bit little-endian lane (eight bytes / four UTF-16 code units)
    /// starting at char index <paramref name="i"/>: each successive char supplies
    /// the next 16 bits — exactly the 8-byte block a byte-oriented MetroHash would
    /// read over the native little-endian UTF-16 stream.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Lane(string key, int i) =>
        (ulong)key[i]
        | ((ulong)key[i + 1] << 16)
        | ((ulong)key[i + 2] << 32)
        | ((ulong)key[i + 3] << 48);

    /// <summary>
    /// Reads one 32-bit little-endian lane (four bytes / two UTF-16 code units)
    /// starting at char index <paramref name="i"/>: the low char supplies the low
    /// 16 bits, the next char the high 16 bits.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Block(string key, int i) =>
        (uint)key[i] | ((uint)key[i + 1] << 16);
}
