using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

/// <summary>
/// A throughput-oriented, strong-distribution hash provider for <see cref="string"/>
/// keys using the XXH3 algorithm (64-bit output, default secret, seed <c>0</c>) over
/// the string's native little-endian UTF-16 byte stream, xor-folded down to a signed
/// 32-bit result.
/// </summary>
/// <remarks>
/// XXH3 is the third-generation member of the xxHash family — the successor to
/// <see cref="StringXxHash32Hasher"/> (XXH32) and <see cref="StringXxHash64Hasher"/>
/// (XXH64). It mixes the input against a fixed 192-byte <em>secret</em> and is
/// <em>length-classed</em>: short inputs take dedicated branches (1–3, 4–8, 9–16,
/// 17–128, and 129–240 bytes) that mix the relevant bytes against slices of the
/// secret through a 128-bit multiply-fold, and only inputs longer than 240 bytes
/// enter the eight-lane accumulator loop that consumes 64-byte stripes against the
/// secret and periodically scrambles the lanes. This structure makes XXH3 both very
/// fast on the short-to-mid keys typical of identifiers and dictionary keys — where a
/// single stripe loop spends most of its time on tail handling — and extremely fast
/// in bulk on long keys. The 64-bit result is reduced to 32 bits by xor-folding the
/// high half into the low half (<c>h ^ (h &gt;&gt; 32)</c>), keeping the extra
/// high-half entropy in the returned value, exactly as
/// <see cref="StringXxHash64Hasher"/>, <see cref="StringMetroHash64Hasher"/>, and
/// <see cref="StringCityHash64Hasher"/> do.
/// <para>
/// It sits at the strong-distribution top of the <see cref="string"/> escalation
/// ladder alongside <see cref="StringMurmur3Hasher"/>,
/// <see cref="StringXxHash32Hasher"/>, <see cref="StringXxHash64Hasher"/>,
/// <see cref="StringMetroHash64Hasher"/>, and <see cref="StringCityHash64Hasher"/>:
/// <see cref="StringFnV1AHasher"/> (cheapest, low-byte only) →
/// <see cref="StringFnV1AFullHasher"/> (cheap, full Unicode width, 32-bit state) →
/// <see cref="StringFnV1A64Hasher"/> (full Unicode width, 64-bit state) →
/// <see cref="StringMurmur3Hasher"/> / <see cref="StringXxHash32Hasher"/> /
/// <see cref="StringXxHash64Hasher"/> / <see cref="StringMetroHash64Hasher"/> /
/// <see cref="StringCityHash64Hasher"/> / <see cref="StringXxHash3Hasher"/>
/// (strong avalanche, maximum throughput). Like the other full-width string hashers
/// it consumes the <em>full</em> 16-bit value of every character — treating the
/// string as its native little-endian UTF-16 byte stream — so it distinguishes
/// characters that differ only in their upper byte (for example <c>'A'</c>
/// (<c>U+0041</c>) and <c>'Ł'</c> (<c>U+0141</c>), which
/// <see cref="StringFnV1AHasher"/> collides on). <see cref="StringXxHash3Hasher"/>,
/// <see cref="StringXxHash64Hasher"/>, <see cref="StringMetroHash64Hasher"/>, and
/// <see cref="StringCityHash64Hasher"/> are peers at the throughput-oriented top of
/// the ladder — profile on your own key shape to pick between them; XXH3 typically
/// leads on both short keys (its dedicated length classes) and long keys (its
/// eight-lane accumulator), while <see cref="StringMurmur3Hasher"/> stays the simplest
/// choice for very short keys. All are good answers when FNV-1a's weaker avalanche
/// pushes clustered or adversarial keys into long probe chains.
/// </para>
/// <para>
/// <c>StringXxHash3Hasher.Hash(s)</c> equals canonical XXH3 64-bit (default secret,
/// seed <c>0</c>) over <c>Encoding.Unicode.GetBytes(s)</c>, xor-folded to a signed
/// 32-bit integer. The empty string maps to the well-known canonical empty-input
/// vector <c>0x2D06800538D394C2</c> folded to 32 bits (its UTF-16 byte stream is zero
/// bytes). The dictionaries store the out-of-band <c>null</c>-key entry without ever
/// calling the hasher, so this does not collide with the empty-slot sentinel.
/// </para>
/// </remarks>
public struct StringXxHash3Hasher : IHashProvider<string>
{
    private const ulong Prime64_1 = 0x9E3779B185EBCA87UL;
    private const ulong Prime64_2 = 0xC2B2AE3D27D4EB4FUL;
    private const ulong Prime64_3 = 0x165667B19E3779F9UL;
    private const ulong Prime64_4 = 0x85EBCA77C2B2AE63UL;
    private const ulong Prime64_5 = 0x27D4EB2F165667C5UL;
    private const uint Prime32_1 = 0x9E3779B1u;
    private const uint Prime32_2 = 0x85EBCA77u;
    private const uint Prime32_3 = 0xC2B2AE3Du;

    // XXH3 mixing primes (distinct from the XXH32/XXH64 primes above).
    private const ulong PrimeMx1 = 0x165667919E3779F9UL;
    private const ulong PrimeMx2 = 0x9FB21C651E98DF25UL;

    // Long-path constants.
    private const int StripeChars = 32;          // 64 bytes per stripe
    private const int LaneChars = 4;             // 8 bytes per lane, eight lanes per stripe
    private const int NbStripesPerBlock = 16;    // (192 - 64) / 8
    private const int BlockChars = StripeChars * NbStripesPerBlock; // 512 chars (1024 bytes)

    /// <summary>
    /// The canonical XXH3 default secret (<c>XXH3_kSecret</c>), 192 bytes. Returned as
    /// a <see cref="ReadOnlySpan{T}"/> over embedded constant data (no allocation).
    /// </summary>
    private static ReadOnlySpan<byte> Secret => new byte[]
    {
        0xb8, 0xfe, 0x6c, 0x39, 0x23, 0xa4, 0x4b, 0xbe, 0x7c, 0x01, 0x81, 0x2c, 0xf7, 0x21, 0xad, 0x1c,
        0xde, 0xd4, 0x6d, 0xe9, 0x83, 0x90, 0x97, 0xdb, 0x72, 0x40, 0xa4, 0xa4, 0xb7, 0xb3, 0x67, 0x1f,
        0xcb, 0x79, 0xe6, 0x4e, 0xcc, 0xc0, 0xe5, 0x78, 0x82, 0x5a, 0xd0, 0x7d, 0xcc, 0xff, 0x72, 0x21,
        0xb8, 0x08, 0x46, 0x74, 0xf7, 0x43, 0x24, 0x8e, 0xe0, 0x35, 0x90, 0xe6, 0x81, 0x3a, 0x26, 0x4c,
        0x3c, 0x28, 0x52, 0xbb, 0x91, 0xc3, 0x00, 0xcb, 0x88, 0xd0, 0x65, 0x8b, 0x1b, 0x53, 0x2e, 0xa3,
        0x71, 0x64, 0x48, 0x97, 0xa2, 0x0d, 0xf9, 0x4e, 0x38, 0x19, 0xef, 0x46, 0xa9, 0xde, 0xac, 0xd8,
        0xa8, 0xfa, 0x76, 0x3f, 0xe3, 0x9c, 0x34, 0x3f, 0xf9, 0xdc, 0xbb, 0xc7, 0xc7, 0x0b, 0x4f, 0x1d,
        0x8a, 0x51, 0xe0, 0x4b, 0xcd, 0xb4, 0x59, 0x31, 0xc8, 0x9f, 0x7e, 0xc9, 0xd9, 0x78, 0x73, 0x64,
        0xea, 0xc5, 0xac, 0x83, 0x34, 0xd3, 0xeb, 0xc3, 0xc5, 0x81, 0xa0, 0xff, 0xfa, 0x13, 0x63, 0xeb,
        0x17, 0x0d, 0xdd, 0x51, 0xb7, 0xf0, 0xda, 0x49, 0xd3, 0x16, 0x55, 0x26, 0x29, 0xd4, 0x68, 0x9e,
        0x2b, 0x16, 0xbe, 0x58, 0x7d, 0x47, 0xa1, 0xfc, 0x8f, 0xf8, 0xb8, 0xd1, 0x7a, 0xd0, 0x31, 0xce,
        0x45, 0xcb, 0x3a, 0x8f, 0x95, 0x16, 0x04, 0x28, 0xaf, 0xd7, 0xfb, 0xca, 0xbb, 0x4b, 0x40, 0x7e,
    };

    /// <summary>
    /// Computes the XXH3 64-bit hash (default secret, seed <c>0</c>) of the specified
    /// string over its native little-endian UTF-16 byte stream, xor-folded to a signed
    /// 32-bit result.
    /// </summary>
    /// <param name="key">The string to hash. Must not be <c>null</c>.</param>
    /// <returns>The signed 32-bit, xor-folded XXH3 hash of <paramref name="key"/>.</returns>
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

        // n = count of UTF-16 code units (chars); the conceptual byte length is 2 * n,
        // which is always even — so every XXH3 read in the 4-to-240-byte and long
        // paths lands on a char boundary. The single odd-aligned read in XXH3 (the
        // 1-to-3-byte path) is only reachable for the byte length 2, i.e. n == 1,
        // which is handled explicitly below.
        int n = key.Length;
        ulong h;

        if (n == 0)
        {
            h = Xxh64Avalanche(Sec64(56) ^ Sec64(64));
        }
        else if (n == 1)
        {
            // XXH3_len_1to3 over the 2-byte UTF-16 stream of a single char: c1 is the
            // low byte, c2 == c3 the high byte, and the byte length is 2.
            char c0 = key[0];
            uint c1 = (byte)c0;
            uint c2 = (byte)(c0 >> 8);
            uint combined = (c1 << 16) | (c2 << 24) | c2 | (2u << 8);
            ulong bitflip = Sec32(0) ^ Sec32(4);
            h = Xxh64Avalanche(combined ^ bitflip);
        }
        else if (n <= 4)
        {
            h = Len4to8(key, n);
        }
        else if (n <= 8)
        {
            h = Len9to16(key, n);
        }
        else if (n <= 64)
        {
            h = Len17to128(key, n);
        }
        else if (n <= 120)
        {
            h = Len129to240(key, n);
        }
        else
        {
            h = HashLong(key, n);
        }

        // Xor-fold the 64-bit state down to 32 bits, keeping the high-half entropy.
        return unchecked((int)(h ^ (h >> 32)));
    }

    // ── Short-length code paths (seed 0) ──────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Len4to8(string s, int n)
    {
        ulong byteLength = (ulong)(2 * n);
        uint input1 = Key32(s, 0);          // bytes [0, 4)
        uint input2 = Key32(s, n - 2);      // bytes [2n-4, 2n)
        ulong bitflip = Sec64(8) ^ Sec64(16);
        ulong input64 = input2 + ((ulong)input1 << 32);
        return Rrmxmx(input64 ^ bitflip, byteLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Len9to16(string s, int n)
    {
        ulong byteLength = (ulong)(2 * n);
        ulong bitflip1 = Sec64(24) ^ Sec64(32);
        ulong bitflip2 = Sec64(40) ^ Sec64(48);
        ulong inputLo = Key64(s, 0) ^ bitflip1;             // bytes [0, 8)
        ulong inputHi = Key64(s, n - 4) ^ bitflip2;         // bytes [2n-8, 2n)
        ulong acc = byteLength
                  + BinaryPrimitives.ReverseEndianness(inputLo)
                  + inputHi
                  + Mul128Fold64(inputLo, inputHi);
        return Xxh3Avalanche(acc);
    }

    private static ulong Len17to128(string s, int n)
    {
        int len = 2 * n; // byte length, in (16, 128]
        ulong acc = (ulong)len * Prime64_1;

        if (len > 32)
        {
            if (len > 64)
            {
                if (len > 96)
                {
                    acc += Mix16B(s, 24, 96);            // input + 48
                    acc += Mix16B(s, n - 32, 112);       // input + len - 64
                }

                acc += Mix16B(s, 16, 64);                // input + 32
                acc += Mix16B(s, n - 24, 80);            // input + len - 48
            }

            acc += Mix16B(s, 8, 32);                     // input + 16
            acc += Mix16B(s, n - 16, 48);                // input + len - 32
        }

        acc += Mix16B(s, 0, 0);                          // input + 0
        acc += Mix16B(s, n - 8, 16);                     // input + len - 16
        return Xxh3Avalanche(acc);
    }

    private static ulong Len129to240(string s, int n)
    {
        int len = 2 * n; // byte length, in (128, 240]
        ulong acc = (ulong)len * Prime64_1;

        // First eight 16-byte chunks against secret[0..128).
        for (int i = 0; i < 8; i++)
        {
            acc += Mix16B(s, 8 * i, 16 * i);
        }

        int nbRounds = len / 16;
        ulong accEnd = Mix16B(s, n - 8, 119);            // input + len - 16, secret + 119
        acc = Xxh3Avalanche(acc);

        // Remaining 16-byte chunks against secret[3..], offset by XXH3_MIDSIZE_STARTOFFSET.
        for (int i = 8; i < nbRounds; i++)
        {
            accEnd += Mix16B(s, 8 * i, (16 * (i - 8)) + 3);
        }

        return Xxh3Avalanche(acc + accEnd);
    }

    // ── Long code path (> 240 bytes), default secret / seed 0 ─────────────────

    private static ulong HashLong(string s, int n)
    {
        long byteLength = 2L * n;

        Span<ulong> acc = stackalloc ulong[8];
        acc[0] = Prime32_3;
        acc[1] = Prime64_1;
        acc[2] = Prime64_2;
        acc[3] = Prime64_3;
        acc[4] = Prime64_4;
        acc[5] = Prime32_2;
        acc[6] = Prime64_5;
        acc[7] = Prime32_1;

        const long blockBytes = 64L * NbStripesPerBlock; // 1024
        int nbBlocks = (int)((byteLength - 1) / blockBytes);

        for (int b = 0; b < nbBlocks; b++)
        {
            Accumulate(acc, s, b * BlockChars, NbStripesPerBlock);
            Scramble(acc);
        }

        // Last partial block: the stripes that remain before the dedicated last stripe.
        int nbStripes = (int)(((byteLength - 1) - ((long)nbBlocks * blockBytes)) / 64);
        Accumulate(acc, s, nbBlocks * BlockChars, nbStripes);

        // The final stripe always re-reads the last 64 bytes (last 32 chars), against
        // secret + secretLen - 64 - 7 = 121.
        AccumulateStripe(acc, s, n - StripeChars, 121);

        return MergeAccs(acc, 11, (ulong)byteLength * Prime64_1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Accumulate(Span<ulong> acc, string s, int startChar, int nbStripes)
    {
        for (int i = 0; i < nbStripes; i++)
        {
            // Each stripe consumes 8 bytes of secret (XXH_SECRET_CONSUME_RATE).
            AccumulateStripe(acc, s, startChar + (i * StripeChars), i * 8);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AccumulateStripe(Span<ulong> acc, string s, int stripeChar, int secretOffset)
    {
        for (int lane = 0; lane < 8; lane++)
        {
            ulong dataVal = Key64(s, stripeChar + (lane * LaneChars));
            ulong dataKey = dataVal ^ Sec64(secretOffset + (lane * 8));
            acc[lane ^ 1] += dataVal;                                       // swap adjacent lanes
            acc[lane] += (ulong)(uint)dataKey * (ulong)(uint)(dataKey >> 32); // 32x32 -> 64 mult
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Scramble(Span<ulong> acc)
    {
        for (int lane = 0; lane < 8; lane++)
        {
            ulong key64 = Sec64(128 + (lane * 8)); // secret + secretLen - 64
            ulong a = acc[lane];
            a ^= a >> 47;
            a ^= key64;
            a *= Prime32_1;
            acc[lane] = a;
        }
    }

    private static ulong MergeAccs(Span<ulong> acc, int secretOffset, ulong start)
    {
        ulong result = start;
        for (int i = 0; i < 4; i++)
        {
            result += Mul128Fold64(
                acc[2 * i] ^ Sec64(secretOffset + (16 * i)),
                acc[(2 * i) + 1] ^ Sec64(secretOffset + (16 * i) + 8));
        }

        return Xxh3Avalanche(result);
    }

    // ── Mixing primitives ─────────────────────────────────────────────────────

    /// <summary>
    /// XXH3_mix16B: fold a 16-byte input block (two 64-bit lanes at char index
    /// <paramref name="ci"/>) against the secret at byte offset <paramref name="so"/>
    /// through a 128-bit multiply. Seed is <c>0</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Mix16B(string s, int ci, int so)
    {
        ulong inputLo = Key64(s, ci);
        ulong inputHi = Key64(s, ci + 4);
        return Mul128Fold64(inputLo ^ Sec64(so), inputHi ^ Sec64(so + 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Mul128Fold64(ulong a, ulong b)
    {
        ulong high = Math.BigMul(a, b, out ulong low);
        return low ^ high;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Xxh3Avalanche(ulong h)
    {
        h ^= h >> 37;
        h *= PrimeMx1;
        h ^= h >> 32;
        return h;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Xxh64Avalanche(ulong h)
    {
        h ^= h >> 33;
        h *= Prime64_2;
        h ^= h >> 29;
        h *= Prime64_3;
        h ^= h >> 32;
        return h;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Rrmxmx(ulong h, ulong length)
    {
        h ^= BitOperations.RotateLeft(h, 49) ^ BitOperations.RotateLeft(h, 24);
        h *= PrimeMx2;
        h ^= (h >> 35) + length;
        h *= PrimeMx2;
        h ^= h >> 28;
        return h;
    }

    // ── Stream readers ────────────────────────────────────────────────────────

    /// <summary>
    /// Reads one 64-bit little-endian word (eight bytes / four UTF-16 code units)
    /// starting at char index <paramref name="ci"/> — exactly the 8 bytes a
    /// byte-oriented XXH3 would read over the native little-endian UTF-16 stream.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Key64(string s, int ci) =>
        (ulong)s[ci]
        | ((ulong)s[ci + 1] << 16)
        | ((ulong)s[ci + 2] << 32)
        | ((ulong)s[ci + 3] << 48);

    /// <summary>
    /// Reads one 32-bit little-endian word (four bytes / two UTF-16 code units)
    /// starting at char index <paramref name="ci"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Key32(string s, int ci) =>
        (uint)s[ci] | ((uint)s[ci + 1] << 16);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Sec64(int byteOffset) =>
        BinaryPrimitives.ReadUInt64LittleEndian(Secret.Slice(byteOffset));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Sec32(int byteOffset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(Secret.Slice(byteOffset));
}
