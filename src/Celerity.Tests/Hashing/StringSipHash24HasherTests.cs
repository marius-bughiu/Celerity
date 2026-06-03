using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class StringSipHash24HasherTests
{
    private readonly StringSipHash24Hasher _hasher = new StringSipHash24Hasher();

    // Canonical SipHash reference key (bytes 00..0f), as two little-endian halves.
    private const ulong K0 = 0x0706050403020100UL;
    private const ulong K1 = 0x0F0E0D0C0B0A0908UL;

    /// <summary>
    /// Independent, byte-oriented reference implementation of canonical SipHash-2-4
    /// over the BCL's little-endian UTF-16 encoding of the string, xor-folded to 32
    /// bits exactly as the hasher does. The hasher loops over <c>char</c>s and
    /// assembles 64-bit message words by hand; this reference goes through
    /// <see cref="Encoding.Unicode"/> and reads words with
    /// <see cref="BinaryPrimitives.ReadUInt64LittleEndian"/> against raw byte
    /// offsets, so agreement pins the word order, the tail packing, the length
    /// byte, the two-round compression and four-round finalization, and the final
    /// fold against drift without sharing the hasher's char-lane implementation.
    /// </summary>
    private static int ReferenceSipHash24Utf16(string s)
    {
        ulong h = SipHash24(Encoding.Unicode.GetBytes(s));
        return unchecked((int)(h ^ (h >> 32)));
    }

    private static void SipRound(ref ulong v0, ref ulong v1, ref ulong v2, ref ulong v3)
    {
        v0 += v1;
        v1 = BitOperations.RotateLeft(v1, 13);
        v1 ^= v0;
        v0 = BitOperations.RotateLeft(v0, 32);
        v2 += v3;
        v3 = BitOperations.RotateLeft(v3, 16);
        v3 ^= v2;
        v0 += v3;
        v3 = BitOperations.RotateLeft(v3, 21);
        v3 ^= v0;
        v2 += v1;
        v1 = BitOperations.RotateLeft(v1, 17);
        v1 ^= v2;
        v2 = BitOperations.RotateLeft(v2, 32);
    }

    private static ulong SipHash24(byte[] data)
    {
        ulong v0 = 0x736F6D6570736575UL ^ K0;
        ulong v1 = 0x646F72616E646F6DUL ^ K1;
        ulong v2 = 0x6C7967656E657261UL ^ K0;
        ulong v3 = 0x7465646279746573UL ^ K1;

        int len = data.Length;
        int end = len - (len & 7);          // largest multiple of 8 <= len
        for (int i = 0; i < end; i += 8)
        {
            ulong m = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(i));
            v3 ^= m;
            SipRound(ref v0, ref v1, ref v2, ref v3);
            SipRound(ref v0, ref v1, ref v2, ref v3);
            v0 ^= m;
        }

        ulong b = (ulong)len << 56;
        for (int k = 0; k < (len & 7); k++)
            b |= (ulong)data[end + k] << (8 * k);

        v3 ^= b;
        SipRound(ref v0, ref v1, ref v2, ref v3);
        SipRound(ref v0, ref v1, ref v2, ref v3);
        v0 ^= b;

        v2 ^= 0xFFUL;
        SipRound(ref v0, ref v1, ref v2, ref v3);
        SipRound(ref v0, ref v1, ref v2, ref v3);
        SipRound(ref v0, ref v1, ref v2, ref v3);
        SipRound(ref v0, ref v1, ref v2, ref v3);

        return v0 ^ v1 ^ v2 ^ v3;
    }

    // ── Spec anchor (official SipHash-2-4 test vectors, key 00..0f) ───────────────
    // These pin the test's own byte-oriented reference against the published
    // SipHash-2-4 vectors for the standard key over the canonical input
    // bytes[i] = i, independently of the hasher's char-loop. They cover the
    // length-0 path (init + zero final block + finalization), a single tail byte,
    // and a full 8-byte compression word plus a 7-byte tail.

    [Theory]
    [InlineData(0, 0x726FDB47DD0E0E31UL)]   // empty input
    [InlineData(1, 0x74F839C593DC67FDUL)]   // one tail byte
    [InlineData(15, 0xA129CA6149BE45E5UL)]  // one full word + 7-byte tail
    public void Reference_MatchesOfficialSipHash24Vectors(int len, ulong expected)
    {
        var input = new byte[len];
        for (int i = 0; i < len; i++)
            input[i] = (byte)i;
        Assert.Equal(expected, SipHash24(input));
    }

    [Fact]
    public void Hash_EmptyString_ReturnsLength0VectorFolded()
    {
        // SipHash-2-4 of zero input (the standard key) is the published vector
        // 0x726FDB47DD0E0E31; the empty string's UTF-16 stream is also zero bytes,
        // so the hasher (after the xor-fold) must reproduce that vector folded.
        const ulong v = 0x726FDB47DD0E0E31UL;
        int expected = unchecked((int)(v ^ (v >> 32)));
        Assert.Equal(expected, _hasher.Hash(""));
    }

    // ── Spec equivalence (canonical SipHash-2-4 over the UTF-16 byte stream) ──────

    [Theory]
    [InlineData("")]                     // 0 bytes → zero final block
    [InlineData("a")]                    // 2 bytes → tail only
    [InlineData("ab")]                   // 4 bytes → tail only
    [InlineData("abc")]                  // 6 bytes → tail only (3 chars, 6 tail bytes)
    [InlineData("abcd")]                 // 8 bytes → one full word, empty tail
    [InlineData("abcde")]                // 10 bytes → one word + 1-char tail
    [InlineData("abcdef")]               // 12 bytes → one word + 2-char tail
    [InlineData("abcdefg")]              // 14 bytes → one word + 3-char tail
    [InlineData("abcdefgh")]             // 16 bytes → two full words, empty tail
    [InlineData("abcdefghi")]            // 18 bytes → two words + 1-char tail
    [InlineData("0123456789012345")]     // 32 bytes → four full words
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
    public void Hash_MatchesCanonicalSipHash24OverUtf16ByteStream(string input)
    {
        Assert.Equal(ReferenceSipHash24Utf16(input), _hasher.Hash(input));
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
        int a = new StringSipHash24Hasher().Hash(value);
        int b = new StringSipHash24Hasher().Hash(value);
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
        // single-character strings; StringSipHash24Hasher consumes the full 16-bit
        // character and must keep them distinct.
        var lowByte = new StringFnV1AHasher();
        Assert.Equal(lowByte.Hash("A"), lowByte.Hash("Ł"));
        Assert.NotEqual(_hasher.Hash("A"), _hasher.Hash("Ł"));
    }

    [Fact]
    public void Hash_DistinguishesMultibyteStringsThatLowByteFnv1aCollides()
    {
        // Two strings whose characters pairwise share low bytes but differ in
        // upper bytes: the low-byte FNV-1a collides, the full-width SipHash does not.
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
    public void StringSipHash24Hasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<string, int, StringSipHash24Hasher>();

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
    public void StringSipHash24Hasher_KeepsUpperByteDistinctKeysSeparate()
    {
        // The dictionary must treat 'A' and 'Ł' as distinct keys — the full-width
        // fold on the probe path.
        var dict = new CelerityDictionary<string, int, StringSipHash24Hasher>();

        dict["A"] = 1;
        dict["Ł"] = 2;

        Assert.Equal(2, dict.Count);
        Assert.Equal(1, dict["A"]);
        Assert.Equal(2, dict["Ł"]);
    }

    [Fact]
    public void StringSipHash24Hasher_HandlesNullKeyOutOfBand()
    {
        // The dictionary routes the null/default key around the hasher, so a null
        // key must round-trip without the hasher's null guard firing.
        var dict = new CelerityDictionary<string, int, StringSipHash24Hasher>();

        dict[null!] = 42;
        dict["present"] = 1;

        Assert.Equal(2, dict.Count);
        Assert.True(dict.ContainsKey(null!));
        Assert.Equal(42, dict[null!]);
    }

    [Fact]
    public void StringSipHash24Hasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<string, StringSipHash24Hasher>();

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
