using System.Buffers.Binary;
using System.Text;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class StringXxHash3HasherTests
{
    private readonly StringXxHash3Hasher _hasher = new StringXxHash3Hasher();

    // ── Independent, byte-oriented XXH3-64 reference ──────────────────────────
    //
    // The hasher loops over `char`s and reads 64/32-bit words by combining UTF-16
    // code units; this reference reads from the real `Encoding.Unicode` byte array
    // with `BinaryPrimitives` at raw byte offsets and dispatches on the byte length.
    // It shares the XXH3 *spec* with the hasher but none of its reading / indexing
    // code, so agreement pins the length-class dispatch, the secret offsets, the
    // 128-bit folds, the stripe / lane boundaries, the scramble, and the merge — the
    // places implementation bugs actually live — without sharing the char-loop.

    private const ulong Prime64_1 = 0x9E3779B185EBCA87UL;
    private const ulong Prime64_2 = 0xC2B2AE3D27D4EB4FUL;
    private const ulong Prime64_3 = 0x165667B19E3779F9UL;
    private const ulong Prime64_4 = 0x85EBCA77C2B2AE63UL;
    private const ulong Prime64_5 = 0x27D4EB2F165667C5UL;
    private const uint Prime32_1 = 0x9E3779B1u;
    private const uint Prime32_2 = 0x85EBCA77u;
    private const uint Prime32_3 = 0xC2B2AE3Du;
    private const ulong PrimeMx1 = 0x165667919E3779F9UL;
    private const ulong PrimeMx2 = 0x9FB21C651E98DF25UL;

    private static readonly byte[] Secret =
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

    private static ulong S64(int off) => BinaryPrimitives.ReadUInt64LittleEndian(Secret.AsSpan(off));

    private static uint S32(int off) => BinaryPrimitives.ReadUInt32LittleEndian(Secret.AsSpan(off));

    private static ulong R64(ReadOnlySpan<byte> b, int off) => BinaryPrimitives.ReadUInt64LittleEndian(b.Slice(off));

    private static uint R32(ReadOnlySpan<byte> b, int off) => BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(off));

    private static ulong Mul128Fold64(ulong a, ulong b)
    {
        ulong high = Math.BigMul(a, b, out ulong low);
        return low ^ high;
    }

    private static ulong Xxh3Avalanche(ulong h)
    {
        h ^= h >> 37;
        h *= PrimeMx1;
        h ^= h >> 32;
        return h;
    }

    private static ulong Xxh64Avalanche(ulong h)
    {
        h ^= h >> 33;
        h *= Prime64_2;
        h ^= h >> 29;
        h *= Prime64_3;
        h ^= h >> 32;
        return h;
    }

    private static ulong Rrmxmx(ulong h, ulong len)
    {
        h ^= ulong.RotateLeft(h, 49) ^ ulong.RotateLeft(h, 24);
        h *= PrimeMx2;
        h ^= (h >> 35) + len;
        h *= PrimeMx2;
        h ^= h >> 28;
        return h;
    }

    private static ulong RefMix16B(ReadOnlySpan<byte> b, int inOff, int secOff) =>
        Mul128Fold64(R64(b, inOff) ^ S64(secOff), R64(b, inOff + 8) ^ S64(secOff + 8));

    /// <summary>Canonical XXH3 64-bit (default secret, seed 0) over a raw byte buffer.</summary>
    private static ulong Reference64(ReadOnlySpan<byte> b)
    {
        int len = b.Length;

        if (len <= 16)
        {
            if (len > 8)
            {
                ulong bitflip1 = S64(24) ^ S64(32);
                ulong bitflip2 = S64(40) ^ S64(48);
                ulong inputLo = R64(b, 0) ^ bitflip1;
                ulong inputHi = R64(b, len - 8) ^ bitflip2;
                ulong acc = (ulong)len
                          + BinaryPrimitives.ReverseEndianness(inputLo)
                          + inputHi
                          + Mul128Fold64(inputLo, inputHi);
                return Xxh3Avalanche(acc);
            }

            if (len >= 4)
            {
                uint input1 = R32(b, 0);
                uint input2 = R32(b, len - 4);
                ulong bitflip = S64(8) ^ S64(16);
                ulong input64 = input2 + ((ulong)input1 << 32);
                return Rrmxmx(input64 ^ bitflip, (ulong)len);
            }

            if (len > 0)
            {
                byte c1 = b[0];
                byte c2 = b[len >> 1];
                byte c3 = b[len - 1];
                uint combined = ((uint)c1 << 16) | ((uint)c2 << 24) | c3 | ((uint)len << 8);
                ulong bitflip = S32(0) ^ S32(4);
                return Xxh64Avalanche(combined ^ bitflip);
            }

            return Xxh64Avalanche(S64(56) ^ S64(64));
        }

        if (len <= 128)
        {
            ulong acc = (ulong)len * Prime64_1;
            if (len > 32)
            {
                if (len > 64)
                {
                    if (len > 96)
                    {
                        acc += RefMix16B(b, 48, 96);
                        acc += RefMix16B(b, len - 64, 112);
                    }

                    acc += RefMix16B(b, 32, 64);
                    acc += RefMix16B(b, len - 48, 80);
                }

                acc += RefMix16B(b, 16, 32);
                acc += RefMix16B(b, len - 32, 48);
            }

            acc += RefMix16B(b, 0, 0);
            acc += RefMix16B(b, len - 16, 16);
            return Xxh3Avalanche(acc);
        }

        if (len <= 240)
        {
            ulong acc = (ulong)len * Prime64_1;
            int nbRounds = len / 16;
            for (int i = 0; i < 8; i++)
            {
                acc += RefMix16B(b, 16 * i, 16 * i);
            }

            ulong accEnd = RefMix16B(b, len - 16, 119);
            acc = Xxh3Avalanche(acc);
            for (int i = 8; i < nbRounds; i++)
            {
                accEnd += RefMix16B(b, 16 * i, (16 * (i - 8)) + 3);
            }

            return Xxh3Avalanche(acc + accEnd);
        }

        // Long path.
        Span<ulong> accs = stackalloc ulong[8]
        {
            Prime32_3, Prime64_1, Prime64_2, Prime64_3,
            Prime64_4, Prime32_2, Prime64_5, Prime32_1,
        };

        const int blockBytes = 1024;
        int nbBlocks = (len - 1) / blockBytes;
        for (int blk = 0; blk < nbBlocks; blk++)
        {
            RefAccumulate(accs, b, blk * blockBytes, 16);
            RefScramble(accs);
        }

        int nbStripes = ((len - 1) - (nbBlocks * blockBytes)) / 64;
        RefAccumulate(accs, b, nbBlocks * blockBytes, nbStripes);
        RefAccumulateStripe(accs, b, len - 64, 121);

        ulong result = (ulong)len * Prime64_1;
        for (int i = 0; i < 4; i++)
        {
            result += Mul128Fold64(accs[2 * i] ^ S64(11 + (16 * i)), accs[(2 * i) + 1] ^ S64(11 + (16 * i) + 8));
        }

        return Xxh3Avalanche(result);
    }

    private static void RefAccumulate(Span<ulong> acc, ReadOnlySpan<byte> b, int startByte, int nbStripes)
    {
        for (int i = 0; i < nbStripes; i++)
        {
            RefAccumulateStripe(acc, b, startByte + (i * 64), i * 8);
        }
    }

    private static void RefAccumulateStripe(Span<ulong> acc, ReadOnlySpan<byte> b, int stripeByte, int secOff)
    {
        for (int lane = 0; lane < 8; lane++)
        {
            ulong dataVal = R64(b, stripeByte + (lane * 8));
            ulong dataKey = dataVal ^ S64(secOff + (lane * 8));
            acc[lane ^ 1] += dataVal;
            acc[lane] += (ulong)(uint)dataKey * (ulong)(uint)(dataKey >> 32);
        }
    }

    private static void RefScramble(Span<ulong> acc)
    {
        for (int lane = 0; lane < 8; lane++)
        {
            ulong key64 = S64(128 + (lane * 8));
            ulong a = acc[lane];
            a ^= a >> 47;
            a ^= key64;
            a *= Prime32_1;
            acc[lane] = a;
        }
    }

    private static int Fold(ulong h) => unchecked((int)(h ^ (h >> 32)));

    // ── External anchor: the published XXH3-64 empty-input vector ──────────────

    [Fact]
    public void Reference_EmptyInput_MatchesPublishedVector()
    {
        // XXH3_64bits("", seed 0) == 0x2D06800538D394C2 (canonical published vector).
        // Pins the reference's empty path, the secret bytes at offsets 56/64, and the
        // XXH64 avalanche to the spec independently of the hasher.
        Assert.Equal(0x2D06800538D394C2UL, Reference64(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void Hash_EmptyString_FoldsPublishedEmptyVector()
    {
        Assert.Equal(Fold(0x2D06800538D394C2UL), _hasher.Hash(""));
    }

    // ── Spec equivalence over the UTF-16 byte stream ──────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("a")]                  // 1 char  → 2-byte 1to3 path
    [InlineData("ab")]                 // 2 chars → 4-byte 4to8 path
    [InlineData("abc")]                // 3 chars → 6-byte 4to8 path
    [InlineData("abcd")]               // 4 chars → 8-byte 4to8 path
    [InlineData("hello")]              // 5 chars → 10-byte 9to16 path
    [InlineData("hello12")]            // 7 chars → 14-byte 9to16 path
    [InlineData("hello123")]           // 8 chars → 16-byte 9to16 path
    [InlineData("hello, world!")]      // 13 chars → 26-byte 17to128 path
    [InlineData("Ł")]                  // U+0141 — non-ASCII, high byte set
    [InlineData("Łatin")]              // mixed ASCII / non-ASCII
    [InlineData("日本語テスト")]      // CJK, all high bytes set
    [InlineData("a\0b")]              // embedded NUL char (zero bytes mid-stream)
    [InlineData("😀")]                 // surrogate pair (two UTF-16 code units)
    [InlineData("😀😀😀😀😀")]         // five surrogate pairs (10 chars, 20 bytes)
    public void Hash_MatchesXxh3OverUtf16ByteStream(string input)
    {
        ulong expected = Reference64(Encoding.Unicode.GetBytes(input));
        Assert.Equal(Fold(expected), _hasher.Hash(input));
    }

    // ── Length sweep: every code-path boundary ────────────────────────────────

    public static IEnumerable<object[]> LengthBoundaries()
    {
        // Char lengths spanning each XXH3 byte-length class and its edges:
        //   byteLen = 2 * charLen.
        //   1to3: charLen 1 · 4to8: 2-4 · 9to16: 5-8 · 17to128: 9-64 ·
        //   129to240: 65-120 · long: 121+ (512+ chars exercise multiple 1024-byte blocks).
        int[] lengths =
        {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 15, 16, 17, 24, 31, 32, 33, 48, 63, 64,
            65, 72, 96, 119, 120, 121, 128, 160, 200, 240, 256, 384, 511, 512, 513,
            600, 1000, 1023, 1024, 1025, 1100, 2049,
        };

        foreach (int len in lengths)
        {
            yield return new object[] { len };
        }
    }

    [Theory]
    [MemberData(nameof(LengthBoundaries))]
    public void Hash_MatchesReference_AcrossLengthBoundaries(int charLength)
    {
        string input = MakeString(charLength);
        ulong expected = Reference64(Encoding.Unicode.GetBytes(input));
        Assert.Equal(Fold(expected), _hasher.Hash(input));
    }

    /// <summary>
    /// Builds a deterministic string of the requested char length with a mix of
    /// ASCII and high-byte-set characters, so the byte stream exercises both halves
    /// of every code unit on every code path.
    /// </summary>
    private static string MakeString(int charLength)
    {
        var sb = new StringBuilder(charLength);
        for (int i = 0; i < charLength; i++)
        {
            // Cycle through ASCII letters and a couple of non-ASCII code points.
            char c = (i % 7) switch
            {
                0 => (char)('a' + (i % 26)),
                3 => (char)(0x0100 + (i % 64)), // Latin Extended-A, high byte set
                5 => (char)(0x4E00 + (i % 128)), // CJK, high byte set
                _ => (char)('A' + (i % 26)),
            };
            sb.Append(c);
        }

        return sb.ToString();
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void Hash_IsDeterministic_AcrossCalls()
    {
        const string value = "Consistent Hashing";
        Assert.Equal(_hasher.Hash(value), _hasher.Hash(value));
    }

    [Fact]
    public void Hash_IsDeterministic_AcrossInstances()
    {
        const string value = "another value";
        int a = new StringXxHash3Hasher().Hash(value);
        int b = new StringXxHash3Hasher().Hash(value);
        Assert.Equal(a, b);
    }

    // ── Case sensitivity & avalanche ──────────────────────────────────────────

    [Fact]
    public void Hash_DifferentCaseStrings_ProduceDifferentValues()
    {
        Assert.NotEqual(_hasher.Hash("hello"), _hasher.Hash("HELLO"));
    }

    [Fact]
    public void Hash_SingleCharacterChange_ChangesResult()
    {
        Assert.NotEqual(_hasher.Hash("hello"), _hasher.Hash("hellp"));
    }

    [Fact]
    public void Hash_AppendingCharacter_ChangesResult()
    {
        Assert.NotEqual(_hasher.Hash("abc"), _hasher.Hash("abcd"));
    }

    // ── Full-character distinction (shared with the other full-width hashers) ──

    [Fact]
    public void Hash_DistinguishesCharactersDifferingOnlyInUpperByte()
    {
        // 'A' (U+0041) and 'Ł' (U+0141) share the same low byte (0x41).
        var lowByte = new StringFnV1AHasher();
        Assert.Equal(lowByte.Hash("A"), lowByte.Hash("Ł"));
        Assert.NotEqual(_hasher.Hash("A"), _hasher.Hash("Ł"));
    }

    [Fact]
    public void Hash_DistinguishesMultibyteStringsThatLowByteFnv1aCollides()
    {
        var lowByte = new StringFnV1AHasher();
        const string ascii = "AB";          // U+0041 U+0042
        const string wide = "Łł"; // U+0141 U+0142 — same low bytes
        Assert.Equal(lowByte.Hash(ascii), lowByte.Hash(wide));
        Assert.NotEqual(_hasher.Hash(ascii), _hasher.Hash(wide));
    }

    // ── Distinctness sweep ────────────────────────────────────────────────────

    [Fact]
    public void Hash_ManyDistinctStrings_ProduceDistinctResults()
    {
        var seen = new HashSet<int>();
        for (int i = 0; i < 1000; i++)
        {
            string key = "key-" + i.ToString();
            Assert.True(seen.Add(_hasher.Hash(key)),
                $"Unexpected collision at input \"{key}\".");
        }
    }

    // ── Null handling ─────────────────────────────────────────────────────────

    [Fact]
    public void Hash_NullString_ThrowsArgumentNullException()
    {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => _hasher.Hash(null!));
        Assert.Equal("key", ex.ParamName);
    }

    // ── Integration: satisfies the hasher constraint on collections ──────────

    [Fact]
    public void StringXxHash3Hasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<string, int, StringXxHash3Hasher>();

        dict[""] = 0;            // empty string is a regular key
        dict["alice"] = 1;
        dict["bob"] = 2;
        dict["Łatin"] = 3;  // non-ASCII key

        Assert.Equal(4, dict.Count);
        Assert.Equal(0, dict[""]);
        Assert.Equal(1, dict["alice"]);
        Assert.Equal(2, dict["bob"]);
        Assert.Equal(3, dict["Łatin"]);
        Assert.True(dict.ContainsKey("alice"));
        Assert.False(dict.ContainsKey("carol"));
    }

    [Fact]
    public void StringXxHash3Hasher_KeepsUpperByteDistinctKeysSeparate()
    {
        var dict = new CelerityDictionary<string, int, StringXxHash3Hasher>();

        dict["A"] = 1;
        dict["Ł"] = 2;

        Assert.Equal(2, dict.Count);
        Assert.Equal(1, dict["A"]);
        Assert.Equal(2, dict["Ł"]);
    }

    [Fact]
    public void StringXxHash3Hasher_HandlesNullKeyOutOfBand()
    {
        var dict = new CelerityDictionary<string, int, StringXxHash3Hasher>();

        dict[null!] = 42;
        dict["present"] = 1;

        Assert.Equal(2, dict.Count);
        Assert.True(dict.ContainsKey(null!));
        Assert.Equal(42, dict[null!]);
    }

    [Fact]
    public void StringXxHash3Hasher_HandlesLongKeys()
    {
        // A key long enough to drive the > 240-byte accumulator loop must still
        // round-trip cleanly through the probe path.
        var dict = new CelerityDictionary<string, int, StringXxHash3Hasher>();
        string longKey = MakeString(600);
        string otherLongKey = MakeString(601);

        dict[longKey] = 1;
        dict[otherLongKey] = 2;

        Assert.Equal(2, dict.Count);
        Assert.Equal(1, dict[longKey]);
        Assert.Equal(2, dict[otherLongKey]);
    }

    [Fact]
    public void StringXxHash3Hasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<string, StringXxHash3Hasher>();

        set.Add("");        // empty string — regular element
        set.Add("alice");
        set.Add("Łatin");

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(""));
        Assert.True(set.Contains("alice"));
        Assert.True(set.Contains("Łatin"));
        Assert.False(set.Contains("bob"));
    }
}
