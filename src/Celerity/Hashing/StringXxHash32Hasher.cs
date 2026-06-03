using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A high-quality, throughput-oriented hash provider for <see cref="string"/>
/// keys using the xxHash32 (XXH32) algorithm over the string's native
/// little-endian UTF-16 byte stream, with a seed of <c>0</c>.
/// </summary>
/// <remarks>
/// xxHash is a non-cryptographic hash designed for speed on variable-length
/// inputs: it processes the input in 16-byte stripes across four independent
/// accumulators (so the work parallelises within a single core's pipeline),
/// then runs an avalanche finalizer that gives every input bit influence over
/// every output bit. That makes it the strong-distribution sibling of
/// <see cref="StringMurmur3Hasher"/> at the top of the <see cref="string"/>
/// escalation ladder, but tuned for throughput on longer keys: where
/// MurmurHash3 mixes one 32-bit block at a time through a single accumulator,
/// XXH32 keeps four accumulators in flight and only folds them together once
/// the bulk of the input is consumed.
/// <para>
/// It sits alongside <see cref="StringMurmur3Hasher"/> on the
/// <see cref="string"/> escalation ladder:
/// <see cref="StringFnV1AHasher"/> (cheapest, low-byte only) →
/// <see cref="StringFnV1AFullHasher"/> (cheap, full Unicode width, 32-bit state) →
/// <see cref="StringFnV1A64Hasher"/> (full Unicode width, 64-bit state) →
/// <see cref="StringMurmur3Hasher"/> / <see cref="StringXxHash32Hasher"/>
/// (strong avalanche). Like the other full-width string hashers it consumes the
/// <em>full</em> 16-bit value of every character — treating the string as its
/// native little-endian UTF-16 byte stream — so it distinguishes characters that
/// differ only in their upper byte (for example <c>'A'</c> (<c>U+0041</c>) and
/// <c>'Ł'</c> (<c>U+0141</c>), which <see cref="StringFnV1AHasher"/> collides on).
/// Prefer it over <see cref="StringMurmur3Hasher"/> when keys are long enough
/// that XXH32's four-accumulator stripe loop wins on throughput; prefer
/// <see cref="StringMurmur3Hasher"/> for very short keys where the simpler
/// single-accumulator loop has less fixed overhead. Both are good answers when
/// FNV-1a's weaker avalanche pushes clustered or adversarial keys into long
/// probe chains.
/// </para>
/// <para>
/// <c>StringXxHash32Hasher.Hash(s)</c> equals canonical XXH32 (seed <c>0</c>)
/// over <c>Encoding.Unicode.GetBytes(s)</c>, reinterpreted as a signed 32-bit
/// integer. The empty string hashes to the avalanche of <c>PRIME32_5</c> (its
/// byte length is <c>0</c> and no stripes or lanes are consumed). The
/// dictionaries store the out-of-band <c>null</c>-key entry without ever calling
/// the hasher, so this does not collide with the empty-slot sentinel.
/// </para>
/// </remarks>
public struct StringXxHash32Hasher : IHashProvider<string>
{
    private const uint Prime1 = 2654435761u;
    private const uint Prime2 = 2246822519u;
    private const uint Prime3 = 3266489917u;
    private const uint Prime4 = 668265263u;
    private const uint Prime5 = 374761393u;

    /// <summary>
    /// Computes the xxHash32 hash (seed <c>0</c>) of the specified string over its
    /// native little-endian UTF-16 byte stream.
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit xxHash32 hash of <paramref name="key"/>.</returns>
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

        int length = key.Length;            // count of UTF-16 code units (chars)
        uint byteLength = (uint)length * 2u; // XXH32 mixes in the byte length

        uint h32;
        int i = 0;

        // Bulk path: process 16-byte stripes (eight chars) across four
        // accumulators while at least a full stripe of chars remains.
        if (length >= 8)
        {
            uint v1 = unchecked(Prime1 + Prime2);
            uint v2 = Prime2;
            uint v3 = 0u;
            uint v4 = unchecked(0u - Prime1);

            do
            {
                v1 = Round(v1, Block(key, i)); i += 2;
                v2 = Round(v2, Block(key, i)); i += 2;
                v3 = Round(v3, Block(key, i)); i += 2;
                v4 = Round(v4, Block(key, i)); i += 2;
            }
            while (i <= length - 8);

            h32 = BitOperations.RotateLeft(v1, 1)
                + BitOperations.RotateLeft(v2, 7)
                + BitOperations.RotateLeft(v3, 12)
                + BitOperations.RotateLeft(v4, 18);
        }
        else
        {
            h32 = Prime5;
        }

        h32 += byteLength;

        // Remaining whole 32-bit lanes (two chars / four bytes each).
        while (i + 2 <= length)
        {
            uint lane = Block(key, i);
            h32 += lane * Prime3;
            h32 = BitOperations.RotateLeft(h32, 17) * Prime4;
            i += 2;
        }

        // A single trailing char contributes two bytes (the UTF-16 byte stream is
        // always even-length), each consumed by the per-byte tail step.
        if (i < length)
        {
            char c = key[i];

            h32 += (uint)(byte)(c & 0xFF) * Prime5;
            h32 = BitOperations.RotateLeft(h32, 11) * Prime1;

            h32 += (uint)(byte)(c >> 8) * Prime5;
            h32 = BitOperations.RotateLeft(h32, 11) * Prime1;
        }

        // Final avalanche (fmix-style) so every input bit reaches every output bit.
        h32 ^= h32 >> 15;
        h32 *= Prime2;
        h32 ^= h32 >> 13;
        h32 *= Prime3;
        h32 ^= h32 >> 16;

        return unchecked((int)h32);
    }

    /// <summary>
    /// Reads one 32-bit little-endian lane (four bytes / two UTF-16 code units)
    /// starting at char index <paramref name="i"/>: the low char supplies the low
    /// 16 bits, the next char the high 16 bits — exactly the 4-byte block a
    /// byte-oriented XXH32 would read over the native little-endian UTF-16 stream.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Block(string key, int i) =>
        (uint)key[i] | ((uint)key[i + 1] << 16);

    /// <summary>The XXH32 accumulator round: <c>rotl(acc + lane * PRIME32_2, 13) * PRIME32_1</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Round(uint acc, uint lane) =>
        BitOperations.RotateLeft(acc + (lane * Prime2), 13) * Prime1;
}
