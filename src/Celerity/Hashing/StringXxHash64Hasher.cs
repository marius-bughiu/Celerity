using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A high-quality, throughput-oriented hash provider for <see cref="string"/>
/// keys using the xxHash64 (XXH64) algorithm over the string's native
/// little-endian UTF-16 byte stream, with a seed of <c>0</c>, xor-folded down to
/// a signed 32-bit result.
/// </summary>
/// <remarks>
/// This is the wide-accumulator counterpart to <see cref="StringXxHash32Hasher"/>:
/// it folds the same little-endian UTF-16 byte stream, but processes the input in
/// 32-byte stripes (sixteen chars) across four <em>64-bit</em> accumulators and
/// finishes with the XXH64 avalanche. Carrying twice as many bits through the
/// accumulation means intermediate values collide far less often, so for longer
/// keys and larger key sets the distribution holds up even better than XXH32
/// before the final fold. Each multiply is 64-bit, which on 64-bit platforms is
/// the same throughput as a 32-bit multiply, so the wider state is effectively
/// free there — and the wider stripe lets the loop consume input twice as fast.
/// The 64-bit result is reduced to 32 bits by xor-folding the high half into the
/// low half (<c>h ^ (h &gt;&gt; 32)</c>), keeping the extra high-half entropy in
/// the returned value, exactly as <see cref="StringFnV1A64Hasher"/> does.
/// <para>
/// It sits at the strong-distribution top of the <see cref="string"/> escalation
/// ladder alongside <see cref="StringMurmur3Hasher"/>,
/// <see cref="StringXxHash32Hasher"/>, and <see cref="StringMetroHash64Hasher"/>:
/// <see cref="StringFnV1AHasher"/> (cheapest, low-byte only) →
/// <see cref="StringFnV1AFullHasher"/> (cheap, full Unicode width, 32-bit state) →
/// <see cref="StringFnV1A64Hasher"/> (full Unicode width, 64-bit state) →
/// <see cref="StringMurmur3Hasher"/> / <see cref="StringXxHash32Hasher"/> /
/// <see cref="StringXxHash64Hasher"/> / <see cref="StringMetroHash64Hasher"/>
/// (strong avalanche). <see cref="StringMetroHash64Hasher"/> is a same-class
/// 64-bit, four-accumulator peer worth profiling against on longer keys. Like the other
/// full-width string hashers it consumes the <em>full</em> 16-bit value of every
/// character — treating the string as its native little-endian UTF-16 byte
/// stream — so it distinguishes characters that differ only in their upper byte
/// (for example <c>'A'</c> (<c>U+0041</c>) and <c>'Ł'</c> (<c>U+0141</c>), which
/// <see cref="StringFnV1AHasher"/> collides on). Prefer it over
/// <see cref="StringXxHash32Hasher"/> when keys are long enough that the wider
/// 64-bit accumulator and 32-byte stripe pull ahead on a 64-bit platform; prefer
/// <see cref="StringXxHash32Hasher"/> or <see cref="StringMurmur3Hasher"/> for
/// shorter keys, where the narrower-state loops have less fixed overhead. All
/// three are good answers when FNV-1a's weaker avalanche pushes clustered or
/// adversarial keys into long probe chains.
/// </para>
/// <para>
/// <c>StringXxHash64Hasher.Hash(s)</c> equals canonical XXH64 (seed <c>0</c>)
/// over <c>Encoding.Unicode.GetBytes(s)</c>, xor-folded to a signed 32-bit
/// integer. The empty string maps to the well-known canonical empty-input vector
/// <c>0xEF46DB3751D8E999</c> folded to 32 bits (its byte length is <c>0</c> and no
/// stripes or lanes are consumed). The dictionaries store the out-of-band
/// <c>null</c>-key entry without ever calling the hasher, so this does not collide
/// with the empty-slot sentinel.
/// </para>
/// </remarks>
public struct StringXxHash64Hasher : IHashProvider<string>
{
    private const ulong Prime1 = 11400714785074694791UL;
    private const ulong Prime2 = 14029467366897019727UL;
    private const ulong Prime3 = 1609587929392839161UL;
    private const ulong Prime4 = 9650029242287828579UL;
    private const ulong Prime5 = 2870177450012600261UL;

    /// <summary>
    /// Computes the xxHash64 hash (seed <c>0</c>) of the specified string over its
    /// native little-endian UTF-16 byte stream, xor-folded to a signed 32-bit
    /// result.
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit, xor-folded xxHash64 hash of <paramref name="key"/>.</returns>
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

        int length = key.Length;             // count of UTF-16 code units (chars)
        ulong byteLength = (ulong)length * 2UL; // XXH64 mixes in the byte length

        ulong h64;
        int i = 0;

        // Bulk path: process 32-byte stripes (sixteen chars) across four
        // accumulators while at least a full stripe of chars remains.
        if (length >= 16)
        {
            ulong v1 = unchecked(Prime1 + Prime2);
            ulong v2 = Prime2;
            ulong v3 = 0UL;
            ulong v4 = unchecked(0UL - Prime1);

            do
            {
                v1 = Round(v1, Lane(key, i)); i += 4;
                v2 = Round(v2, Lane(key, i)); i += 4;
                v3 = Round(v3, Lane(key, i)); i += 4;
                v4 = Round(v4, Lane(key, i)); i += 4;
            }
            while (i <= length - 16);

            h64 = BitOperations.RotateLeft(v1, 1)
                + BitOperations.RotateLeft(v2, 7)
                + BitOperations.RotateLeft(v3, 12)
                + BitOperations.RotateLeft(v4, 18);

            h64 = MergeRound(h64, v1);
            h64 = MergeRound(h64, v2);
            h64 = MergeRound(h64, v3);
            h64 = MergeRound(h64, v4);
        }
        else
        {
            h64 = Prime5;
        }

        h64 += byteLength;

        // Remaining whole 64-bit lanes (four chars / eight bytes each).
        while (i + 4 <= length)
        {
            ulong k1 = Lane(key, i);
            k1 *= Prime2;
            k1 = BitOperations.RotateLeft(k1, 31);
            k1 *= Prime1;
            h64 ^= k1;
            h64 = BitOperations.RotateLeft(h64, 27) * Prime1 + Prime4;
            i += 4;
        }

        // A remaining 32-bit lane (two chars / four bytes).
        if (i + 2 <= length)
        {
            h64 ^= (ulong)Block(key, i) * Prime1;
            h64 = BitOperations.RotateLeft(h64, 23) * Prime2 + Prime3;
            i += 2;
        }

        // A single trailing char contributes two bytes (the UTF-16 byte stream is
        // always even-length), each consumed by the per-byte tail step.
        if (i < length)
        {
            char c = key[i];

            h64 ^= (ulong)(byte)(c & 0xFF) * Prime5;
            h64 = BitOperations.RotateLeft(h64, 11) * Prime1;

            h64 ^= (ulong)(byte)(c >> 8) * Prime5;
            h64 = BitOperations.RotateLeft(h64, 11) * Prime1;
        }

        // Final avalanche so every input bit reaches every output bit.
        h64 ^= h64 >> 33;
        h64 *= Prime2;
        h64 ^= h64 >> 29;
        h64 *= Prime3;
        h64 ^= h64 >> 32;

        // Xor-fold the 64-bit state down to 32 bits, keeping the high-half entropy.
        return unchecked((int)(h64 ^ (h64 >> 32)));
    }

    /// <summary>
    /// Reads one 64-bit little-endian lane (eight bytes / four UTF-16 code units)
    /// starting at char index <paramref name="i"/>: each successive char supplies
    /// the next 16 bits — exactly the 8-byte block a byte-oriented XXH64 would read
    /// over the native little-endian UTF-16 stream.
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

    /// <summary>The XXH64 accumulator round: <c>rotl(acc + lane * PRIME64_2, 31) * PRIME64_1</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Round(ulong acc, ulong lane) =>
        BitOperations.RotateLeft(acc + (lane * Prime2), 31) * Prime1;

    /// <summary>
    /// The XXH64 merge round that folds a finished accumulator into the running
    /// hash: <c>h = (h ^ round(0, acc)) * PRIME64_1 + PRIME64_4</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong MergeRound(ulong acc, ulong val)
    {
        val *= Prime2;
        val = BitOperations.RotateLeft(val, 31);
        val *= Prime1;
        acc ^= val;
        return acc * Prime1 + Prime4;
    }
}
