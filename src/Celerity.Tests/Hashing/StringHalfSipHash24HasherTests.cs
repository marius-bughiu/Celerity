using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class StringHalfSipHash24HasherTests
{
    private readonly StringHalfSipHash24Hasher _hasher = new StringHalfSipHash24Hasher();

    // Canonical HalfSipHash reference key (bytes 00..07), as two little-endian halves.
    private const uint K0 = 0x03020100U;
    private const uint K1 = 0x07060504U;

    /// <summary>
    /// Independent, byte-oriented reference implementation of canonical
    /// HalfSipHash-2-4 (4-byte output) over the BCL's little-endian UTF-16 encoding
    /// of the string. The hasher loops over <c>char</c>s and assembles 32-bit
    /// message words by hand; this reference goes through <see cref="Encoding.Unicode"/>
    /// and reads words with <see cref="BinaryPrimitives.ReadUInt32LittleEndian"/>
    /// against raw byte offsets, handling a fully general 0–3 byte tail (the UTF-16
    /// stream only ever yields a 0- or 2-byte tail, but the reference pins the
    /// general tail packing anyway), so agreement pins the word order, the tail
    /// packing, the length byte, the two-round compression and four-round
    /// finalization, and the native 32-bit output against drift without sharing the
    /// hasher's char-lane implementation.
    /// </summary>
    private static int ReferenceHalfSipHash24Utf16(string s) =>
        unchecked((int)HalfSipHash24(Encoding.Unicode.GetBytes(s)));

    private static void SipRound(ref uint v0, ref uint v1, ref uint v2, ref uint v3)
    {
        v0 += v1;
        v1 = BitOperations.RotateLeft(v1, 5);
        v1 ^= v0;
        v0 = BitOperations.RotateLeft(v0, 16);
        v2 += v3;
        v3 = BitOperations.RotateLeft(v3, 8);
        v3 ^= v2;
        v0 += v3;
        v3 = BitOperations.RotateLeft(v3, 7);
        v3 ^= v0;
        v2 += v1;
        v1 = BitOperations.RotateLeft(v1, 13);
        v1 ^= v2;
        v2 = BitOperations.RotateLeft(v2, 16);
    }

    private static uint HalfSipHash24(byte[] data)
    {
        uint v0 = 0U ^ K0;
        uint v1 = 0U ^ K1;
        uint v2 = 0x6C796765U ^ K0;
        uint v3 = 0x74656462U ^ K1;

        int len = data.Length;
        int end = len - (len & 3);          // largest multiple of 4 <= len
        for (int i = 0; i < end; i += 4)
        {
            uint m = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(i));
            v3 ^= m;
            SipRound(ref v0, ref v1, ref v2, ref v3);
            SipRound(ref v0, ref v1, ref v2, ref v3);
            v0 ^= m;
        }

        uint b = (uint)len << 24;
        for (int k = 0; k < (len & 3); k++)
            b |= (uint)data[end + k] << (8 * k);

        v3 ^= b;
        SipRound(ref v0, ref v1, ref v2, ref v3);
        SipRound(ref v0, ref v1, ref v2, ref v3);
        v0 ^= b;

        v2 ^= 0xFFU;
        SipRound(ref v0, ref v1, ref v2, ref v3);
        SipRound(ref v0, ref v1, ref v2, ref v3);
        SipRound(ref v0, ref v1, ref v2, ref v3);
        SipRound(ref v0, ref v1, ref v2, ref v3);

        return v1 ^ v3;
    }

    // ── Spec anchor (official HalfSipHash-2-4 vectors_hsip32, key 00..0f) ─────────
    // These pin the test's own byte-oriented reference against the published
    // HalfSipHash-2-4 32-bit-output vectors for the standard key over the canonical
    // input bytes[i] = i, independently of the hasher's char-loop. They cover the
    // length-0 path (init + zero final block + finalization), each 1/2/3-byte
    // sub-word tail, exactly one full 4-byte compression word (len 4), one word plus
    // a 3-byte tail (len 7), two full words (len 8), and a multi-word input with a
    // 3-byte tail (len 15). The expected values are the little-endian uint32 of the
    // published 4-byte output vectors.

    [Theory]
    [InlineData(0, 0x5B9F35A9U)]    // empty input          (a9 35 9f 5b)
    [InlineData(1, 0xB85A4727U)]    // one tail byte         (27 47 5a b8)
    [InlineData(2, 0x03A662FAU)]    // two tail bytes        (fa 62 a6 03)
    [InlineData(3, 0x04E7FE8AU)]    // three tail bytes      (8a fe e7 04)
    [InlineData(4, 0x89466E2AU)]    // one full word         (2a 6e 46 89)
    [InlineData(7, 0xC563CF8BU)]    // one word + 3-byte tail(8b cf 63 c5)
    [InlineData(8, 0x8F84B8D0U)]    // two full words        (d0 b8 84 8f)
    [InlineData(15, 0x972BFE74U)]   // three words + 3-byte tail (74 fe 2b 97)
    public void Reference_MatchesOfficialHalfSipHash24Vectors(int len, uint expected)
    {
        var input = new byte[len];
        for (int i = 0; i < len; i++)
            input[i] = (byte)i;
        Assert.Equal(expected, HalfSipHash24(input));
    }

    [Fact]
    public void Hash_EmptyString_ReturnsLength0Vector()
    {
        // HalfSipHash-2-4 of zero input (the standard key) is the published vector
        // 0x5B9F35A9; the empty string's UTF-16 stream is also zero bytes, and the
        // output is natively 32 bits (no fold), so the hasher must reproduce it.
        Assert.Equal(unchecked((int)0x5B9F35A9U), _hasher.Hash(""));
    }

    // ── Spec equivalence (canonical HalfSipHash-2-4 over the UTF-16 byte stream) ──

    [Theory]
    [InlineData("")]                     // 0 bytes → zero final block
    [InlineData("a")]                    // 2 bytes → 1-char tail, no full word
    [InlineData("ab")]                   // 4 bytes → one full word, empty tail
    [InlineData("abc")]                  // 6 bytes → one word + 1-char tail
    [InlineData("abcd")]                 // 8 bytes → two full words, empty tail
    [InlineData("abcde")]                // 10 bytes → two words + 1-char tail
    [InlineData("abcdef")]               // 12 bytes → three full words
    [InlineData("abcdefg")]              // 14 bytes → three words + 1-char tail
    [InlineData("abcdefgh")]             // 16 bytes → four full words
    [InlineData("abcdefghi")]            // 18 bytes → four words + 1-char tail
    [InlineData("0123456789012345")]     // 32 bytes → eight full words
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
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // 48 chars → many words
    public void Hash_MatchesCanonicalHalfSipHash24OverUtf16ByteStream(string input)
    {
        Assert.Equal(ReferenceHalfSipHash24Utf16(input), _hasher.Hash(input));
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
        int a = new StringHalfSipHash24Hasher().Hash(value);
        int b = new StringHalfSipHash24Hasher().Hash(value);
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
        // The input length is mixed into the final block, so a strict prefix must
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
        // single-character strings; StringHalfSipHash24Hasher consumes the full
        // 16-bit character and must keep them distinct.
        var lowByte = new StringFnV1AHasher();
        Assert.Equal(lowByte.Hash("A"), lowByte.Hash("Ł"));
        Assert.NotEqual(_hasher.Hash("A"), _hasher.Hash("Ł"));
    }

    [Fact]
    public void Hash_DistinguishesMultibyteStringsThatLowByteFnv1aCollides()
    {
        // Two strings whose characters pairwise share low bytes but differ in
        // upper bytes: the low-byte FNV-1a collides, the full-width HalfSipHash does not.
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
    public void StringHalfSipHash24Hasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<string, int, StringHalfSipHash24Hasher>();

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
    public void StringHalfSipHash24Hasher_KeepsUpperByteDistinctKeysSeparate()
    {
        // The dictionary must treat 'A' and 'Ł' as distinct keys — the full-width
        // fold on the probe path.
        var dict = new CelerityDictionary<string, int, StringHalfSipHash24Hasher>();

        dict["A"] = 1;
        dict["Ł"] = 2;

        Assert.Equal(2, dict.Count);
        Assert.Equal(1, dict["A"]);
        Assert.Equal(2, dict["Ł"]);
    }

    [Fact]
    public void StringHalfSipHash24Hasher_HandlesNullKeyOutOfBand()
    {
        // The dictionary routes the null/default key around the hasher, so a null
        // key must round-trip without the hasher's null guard firing.
        var dict = new CelerityDictionary<string, int, StringHalfSipHash24Hasher>();

        dict[null!] = 42;
        dict["present"] = 1;

        Assert.Equal(2, dict.Count);
        Assert.True(dict.ContainsKey(null!));
        Assert.Equal(42, dict[null!]);
    }

    [Fact]
    public void StringHalfSipHash24Hasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<string, StringHalfSipHash24Hasher>();

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
