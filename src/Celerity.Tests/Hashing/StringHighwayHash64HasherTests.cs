using System.Buffers.Binary;
using System.Text;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class StringHighwayHash64HasherTests
{
    private readonly StringHighwayHash64Hasher _hasher = new StringHighwayHash64Hasher();

    // Canonical HighwayHash reference test key (bytes 00..1f), four little-endian
    // 64-bit words — the same fixed key the hasher bakes in.
    private static readonly ulong[] TestKey =
    {
        0x0706050403020100UL,
        0x0F0E0D0C0B0A0908UL,
        0x1716151413121110UL,
        0x1F1E1D1C1B1A1918UL,
    };

    /// <summary>
    /// Independent, byte-oriented reference implementation of canonical HighwayHash
    /// (64-bit output). It mirrors the public-domain C reference, reading 64-bit
    /// words straight out of a raw <see cref="byte"/> array with
    /// <see cref="BinaryPrimitives.ReadUInt64LittleEndian"/>, so it shares no code
    /// with the hasher's <c>char</c>-lane loop. Pinned against the published
    /// HighwayHash64 test vectors below; the hasher is then asserted equal to this
    /// reference over <see cref="Encoding.Unicode"/> bytes, which transitively pins
    /// the hasher to the spec without the two implementations sharing a line.
    /// </summary>
    private static ulong ReferenceHighway64(byte[] data, ulong[] key)
    {
        ulong[] v0 = new ulong[4];
        ulong[] v1 = new ulong[4];
        ulong[] mul0 = new ulong[4];
        ulong[] mul1 = new ulong[4];
        Reset(key, v0, v1, mul0, mul1);

        int size = data.Length;
        int i = 0;
        for (; i + 32 <= size; i += 32)
            UpdatePacket(data, i, v0, v1, mul0, mul1);

        int mod = size & 31;
        if (mod != 0)
            UpdateRemainder(data, i, mod, v0, v1, mul0, mul1);

        for (int r = 0; r < 4; r++)
            PermuteAndUpdate(v0, v1, mul0, mul1);

        return v0[0] + v1[0] + mul0[0] + mul1[0];
    }

    private static ulong Rot32(ulong x) => (x >> 32) | (x << 32);

    private static void Reset(ulong[] key, ulong[] v0, ulong[] v1, ulong[] mul0, ulong[] mul1)
    {
        mul0[0] = 0xDBE6D5D5FE4CCE2FUL;
        mul0[1] = 0xA4093822299F31D0UL;
        mul0[2] = 0x13198A2E03707344UL;
        mul0[3] = 0x243F6A8885A308D3UL;
        mul1[0] = 0x3BD39E10CB0EF593UL;
        mul1[1] = 0xC0ACF169B5F18A8CUL;
        mul1[2] = 0xBE5466CF34E90C6CUL;
        mul1[3] = 0x452821E638D01377UL;
        for (int i = 0; i < 4; i++)
        {
            v0[i] = mul0[i] ^ key[i];
            v1[i] = mul1[i] ^ Rot32(key[i]);
        }
    }

    private static void ZipperMergeAndAdd(ulong v1, ulong v0, ulong[] add, int add1, int add0)
    {
        add[add0] += (((v0 & 0xFF000000UL) | (v1 & 0xFF00000000UL)) >> 24)
                   | (((v0 & 0xFF0000000000UL) | (v1 & 0xFF000000000000UL)) >> 16)
                   | (v0 & 0xFF0000UL) | ((v0 & 0xFF00UL) << 32)
                   | ((v1 & 0xFF00000000000000UL) >> 8) | (v0 << 56);
        add[add1] += (((v1 & 0xFF000000UL) | (v0 & 0xFF00000000UL)) >> 24)
                   | (v1 & 0xFF0000UL) | ((v1 & 0xFF0000000000UL) >> 16)
                   | ((v1 & 0xFF00UL) << 24) | ((v0 & 0xFF000000000000UL) >> 8)
                   | ((v1 & 0xFFUL) << 48) | (v0 & 0xFF00000000000000UL);
    }

    private static void Update(ulong[] lanes, ulong[] v0, ulong[] v1, ulong[] mul0, ulong[] mul1)
    {
        for (int i = 0; i < 4; i++)
        {
            v1[i] += mul0[i] + lanes[i];
            mul0[i] ^= (v1[i] & 0xFFFFFFFFUL) * (v0[i] >> 32);
            v0[i] += mul1[i];
            mul1[i] ^= (v0[i] & 0xFFFFFFFFUL) * (v1[i] >> 32);
        }
        ZipperMergeAndAdd(v1[1], v1[0], v0, 1, 0);
        ZipperMergeAndAdd(v1[3], v1[2], v0, 3, 2);
        ZipperMergeAndAdd(v0[1], v0[0], v1, 1, 0);
        ZipperMergeAndAdd(v0[3], v0[2], v1, 3, 2);
    }

    private static void UpdatePacket(byte[] data, int off, ulong[] v0, ulong[] v1, ulong[] mul0, ulong[] mul1)
    {
        ulong[] lanes =
        {
            BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(off + 0)),
            BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(off + 8)),
            BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(off + 16)),
            BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(off + 24)),
        };
        Update(lanes, v0, v1, mul0, mul1);
    }

    private static void Rotate32By(uint count, ulong[] v1)
    {
        for (int i = 0; i < 4; i++)
        {
            uint half0 = (uint)v1[i];
            uint half1 = (uint)(v1[i] >> 32);
            uint r0 = (half0 << (int)count) | (half0 >> (int)(32 - count));
            uint r1 = (half1 << (int)count) | (half1 >> (int)(32 - count));
            v1[i] = r0 | ((ulong)r1 << 32);
        }
    }

    private static void UpdateRemainder(byte[] bytes, int off, int sizeMod32,
        ulong[] v0, ulong[] v1, ulong[] mul0, ulong[] mul1)
    {
        int sizeMod4 = sizeMod32 & 3;
        int rem = sizeMod32 & ~3;
        for (int i = 0; i < 4; i++)
            v0[i] += ((ulong)sizeMod32 << 32) + (ulong)sizeMod32;
        Rotate32By((uint)sizeMod32, v1);

        byte[] packet = new byte[32];
        for (int i = 0; i < rem; i++)
            packet[i] = bytes[off + i];

        if ((sizeMod32 & 16) != 0)
        {
            for (int i = 0; i < 4; i++)
                packet[28 + i] = bytes[off + rem + i + sizeMod4 - 4];
        }
        else if (sizeMod4 != 0)
        {
            packet[16] = bytes[off + rem];
            packet[17] = bytes[off + rem + (sizeMod4 >> 1)];
            packet[18] = bytes[off + rem + sizeMod4 - 1];
        }

        UpdatePacket(packet, 0, v0, v1, mul0, mul1);
    }

    private static void PermuteAndUpdate(ulong[] v0, ulong[] v1, ulong[] mul0, ulong[] mul1)
    {
        ulong[] permuted = { Rot32(v0[2]), Rot32(v0[3]), Rot32(v0[0]), Rot32(v0[1]) };
        Update(permuted, v0, v1, mul0, mul1);
    }

    private static int Fold(ulong h) => unchecked((int)(h ^ (h >> 32)));

    // ── Spec anchor: published HighwayHash64 test vectors ─────────────────────────
    // The official reference vectors for the test key (bytes 00..1f) over the
    // canonical input bytes[i] = i, for input lengths 0..64. Pinning the test's own
    // byte-oriented reference against every one of these — independently of the
    // hasher — covers the length-0 path, the partial-packet remainder under each
    // alignment (the size_mod32 & 16 branch and the short size_mod4 branch), the
    // exact 32-byte packet boundary, and multi-packet inputs.

    private static readonly ulong[] Expected64 =
    {
        0x907A56DE22C26E53, 0x7EAB43AAC7CDDD78, 0xB8D0569AB0B53D62,
        0x5C6BEFAB8A463D80, 0xF205A46893007EDA, 0x2B8A1668E4A94541,
        0xBD4CCC325BEFCA6F, 0x4D02AE1738F59482, 0xE1205108E55F3171,
        0x32D2644EC77A1584, 0xF6E10ACDB103A90B, 0xC3BBF4615B415C15,
        0x243CC2040063FA9C, 0xA89A58CE65E641FF, 0x24B031A348455A23,
        0x40793F86A449F33B, 0xCFAB3489F97EB832, 0x19FE67D2C8C5C0E2,
        0x04DD90A69C565CC2, 0x75D9518E2371C504, 0x38AD9B1141D3DD16,
        0x0264432CCD8A70E0, 0xA9DB5A6288683390, 0xD7B05492003F028C,
        0x205F615AEA59E51E, 0xEEE0C89621052884, 0x1BFC1A93A7284F4F,
        0x512175B5B70DA91D, 0xF71F8976A0A2C639, 0xAE093FEF1F84E3E7,
        0x22CA92B01161860F, 0x9FC7007CCF035A68, 0xA0C964D9ECD580FC,
        0x2C90F73CA03181FC, 0x185CF84E5691EB9E, 0x4FC1F5EF2752AA9B,
        0xF5B7391A5E0A33EB, 0xB9B84B83B4E96C9C, 0x5E42FE712A5CD9B4,
        0xA150F2F90C3F97DC, 0x7FA522D75E2D637D, 0x181AD0CC0DFFD32B,
        0x3889ED981E854028, 0xFB4297E8C586EE2D, 0x6D064A45BB28059C,
        0x90563609B3EC860C, 0x7AA4FCE94097C666, 0x1326BAC06B911E08,
        0xB926168D2B154F34, 0x9919848945B1948D, 0xA2A98FC534825EBE,
        0xE9809095213EF0B6, 0x582E5483707BC0E9, 0x086E9414A88A6AF5,
        0xEE86B98D20F6743D, 0xF89B7FF609B1C0A7, 0x4C7D9CC19E22C3E8,
        0x9A97005024562A6F, 0x5DD41CF423E6EBEF, 0xDF13609C0468E227,
        0x6E0DA4F64188155A, 0xB755BA4B50D7D4A1, 0x887A3484647479BD,
        0xAB8EEBE9BF2139A0, 0x75542C5D4CD2A6FF,
    };

    [Fact]
    public void Reference_MatchesOfficialHighwayHash64Vectors()
    {
        for (int len = 0; len < Expected64.Length; len++)
        {
            var input = new byte[len];
            for (int i = 0; i < len; i++)
                input[i] = (byte)i;
            Assert.Equal(Expected64[len], ReferenceHighway64(input, TestKey));
        }
    }

    [Fact]
    public void Hash_EmptyString_ReturnsLength0VectorFolded()
    {
        // HighwayHash64 of zero input (the test key) is the published vector
        // 0x907A56DE22C26E53; the empty string's UTF-16 stream is also zero bytes,
        // so the hasher (after the xor-fold) must reproduce that vector folded.
        Assert.Equal(Fold(Expected64[0]), _hasher.Hash(""));
    }

    // ── Spec equivalence (canonical HighwayHash64 over the UTF-16 byte stream) ─────

    [Theory]
    [InlineData("")]                     // 0 bytes → no packet, no remainder
    [InlineData("a")]                    // 2 bytes → remainder, size_mod4 == 2
    [InlineData("ab")]                   // 4 bytes → remainder, rem-only
    [InlineData("abc")]                  // 6 bytes → remainder, rem + size_mod4
    [InlineData("abcd")]                 // 8 bytes → remainder, size_mod32 & 16 == 0
    [InlineData("abcde")]                // 10 bytes
    [InlineData("abcdef")]               // 12 bytes
    [InlineData("abcdefg")]              // 14 bytes (size_mod32 & 16 == 0, max short)
    [InlineData("abcdefgh")]             // 16 bytes → size_mod32 & 16 branch
    [InlineData("abcdefghi")]            // 18 bytes → size_mod32 & 16 branch + tail
    [InlineData("abcdefghijklmno")]      // 30 bytes → largest remainder
    [InlineData("abcdefghijklmnop")]     // 32 bytes → exactly one full packet, no remainder
    [InlineData("abcdefghijklmnopq")]    // 34 bytes → one packet + remainder
    [InlineData("0123456789012345678901234567890123456789012345")] // 47 chars / 94 bytes → two packets + remainder
    [InlineData("hello")]
    [InlineData("hello world")]
    [InlineData("Consistent Hashing")]
    [InlineData("the quick brown fox jumps over the lazy dog")]
    [InlineData("the quick brown fox jumps over the lazy dog, and then jumps back again twice")]
    [InlineData("HELLO")]
    [InlineData("Ł")]                  // U+0141 — non-ASCII, high byte set
    [InlineData("Łatin")]              // mixed ASCII / non-ASCII
    [InlineData("日本語")]            // CJK, all high bytes set
    [InlineData("a\0b")]              // embedded NUL char (folds two zero bytes)
    [InlineData("😀")]                 // surrogate pair (two UTF-16 code units)
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // 62 chars → many packets
    public void Hash_MatchesCanonicalHighwayHash64OverUtf16ByteStream(string input)
    {
        int expected = Fold(ReferenceHighway64(Encoding.Unicode.GetBytes(input), TestKey));
        Assert.Equal(expected, _hasher.Hash(input));
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
        // Hashers are stateless structs; two independently-constructed instances
        // (same fixed key) must produce identical output for the same input.
        const string value = "another value";
        int a = new StringHighwayHash64Hasher().Hash(value);
        int b = new StringHighwayHash64Hasher().Hash(value);
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
        // The input length feeds into the remainder mixing, so a strict prefix must
        // not collide with the longer string here.
        Assert.NotEqual(_hasher.Hash("abc"), _hasher.Hash("abcd"));
    }

    // ── Full-character distinction (shared with the other full-width string
    //    hashers — the full 16-bit fold keeps upper-byte-distinct characters
    //    apart, unlike the low-byte StringFnV1AHasher) ───────────────────────────

    [Fact]
    public void Hash_DistinguishesCharactersDifferingOnlyInUpperByte()
    {
        // 'A' (U+0041) and 'Ł' (U+0141) share the same low byte (0x41).
        // StringFnV1AHasher folds only that low byte, so it collides on these two
        // single-character strings; StringHighwayHash64Hasher consumes the full
        // 16-bit character and must keep them distinct.
        var lowByte = new StringFnV1AHasher();
        Assert.Equal(lowByte.Hash("A"), lowByte.Hash("Ł"));
        Assert.NotEqual(_hasher.Hash("A"), _hasher.Hash("Ł"));
    }

    [Fact]
    public void Hash_DistinguishesMultibyteStringsThatLowByteFnv1aCollides()
    {
        // Two strings whose characters pairwise share low bytes but differ in
        // upper bytes: the low-byte FNV-1a collides, the full-width HighwayHash does not.
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
        // 1000 distinct short strings should hash without collision; the hash is
        // deterministic, so a pass here is a pass everywhere.
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
    public void StringHighwayHash64Hasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<string, int, StringHighwayHash64Hasher>();

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
    public void StringHighwayHash64Hasher_KeepsUpperByteDistinctKeysSeparate()
    {
        // The dictionary must treat 'A' and 'Ł' as distinct keys — the full-width
        // fold on the probe path.
        var dict = new CelerityDictionary<string, int, StringHighwayHash64Hasher>();

        dict["A"] = 1;
        dict["Ł"] = 2;

        Assert.Equal(2, dict.Count);
        Assert.Equal(1, dict["A"]);
        Assert.Equal(2, dict["Ł"]);
    }

    [Fact]
    public void StringHighwayHash64Hasher_HandlesNullKeyOutOfBand()
    {
        // The dictionary routes the null/default key around the hasher, so a null
        // key must round-trip without the hasher's null guard firing.
        var dict = new CelerityDictionary<string, int, StringHighwayHash64Hasher>();

        dict[null!] = 42;
        dict["present"] = 1;

        Assert.Equal(2, dict.Count);
        Assert.True(dict.ContainsKey(null!));
        Assert.Equal(42, dict[null!]);
    }

    [Fact]
    public void StringHighwayHash64Hasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<string, StringHighwayHash64Hasher>();

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
