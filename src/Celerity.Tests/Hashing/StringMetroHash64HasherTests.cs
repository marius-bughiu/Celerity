using System.Buffers.Binary;
using System.Text;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class StringMetroHash64HasherTests
{
    private readonly StringMetroHash64Hasher _hasher = new StringMetroHash64Hasher();

    // metrohash64_1 mixing constants (J. Andrew Rogers' public-domain reference).
    private const ulong K0 = 0xC83A91E1UL;
    private const ulong K1 = 0x8648DBDBUL;
    private const ulong K2 = 0x7BDEC03BUL;
    private const ulong K3 = 0x2F5870A5UL;

    /// <summary>
    /// Independent, byte-oriented reference implementation of canonical MetroHash64
    /// (<c>metrohash64_1</c>, seed <c>0</c>) over the BCL's little-endian UTF-16
    /// encoding of the string, xor-folded to 32 bits exactly as the hasher does.
    /// The hasher loops over <c>char</c>s and assembles lanes by hand; this
    /// reference goes through <see cref="Encoding.Unicode"/> and a byte loop with
    /// <see cref="BinaryPrimitives.ReadUInt64LittleEndian"/> /
    /// <see cref="BinaryPrimitives.ReadUInt32LittleEndian"/> /
    /// <see cref="BinaryPrimitives.ReadUInt16LittleEndian"/> (and even includes the
    /// canonical single-byte tail step the hasher can elide, since the UTF-16 stream
    /// is always even-length), so agreement pins the byte order, the stripe / lane /
    /// block boundaries, the tail handling, the post-loop mixing, the finalization,
    /// and the final fold against drift without sharing the hasher's implementation.
    /// </summary>
    private static int ReferenceMetroHash64Utf16(string s)
    {
        ulong h = MetroHash64(Encoding.Unicode.GetBytes(s));
        return unchecked((int)(h ^ (h >> 32)));
    }

    private static ulong MetroHash64(byte[] data)
    {
        int len = data.Length;
        int ptr = 0;
        int end = len;

        ulong hash = ((0UL + K2) * K0) + (ulong)len;

        if (len >= 32)
        {
            ulong v0 = hash, v1 = hash, v2 = hash, v3 = hash;
            do
            {
                v0 += BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(ptr)) * K0; ptr += 8; v0 = RotR(v0, 29) + v2;
                v1 += BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(ptr)) * K1; ptr += 8; v1 = RotR(v1, 29) + v3;
                v2 += BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(ptr)) * K2; ptr += 8; v2 = RotR(v2, 29) + v0;
                v3 += BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(ptr)) * K3; ptr += 8; v3 = RotR(v3, 29) + v1;
            }
            while (ptr <= end - 32);

            v2 ^= RotR(((v0 + v3) * K0) + v1, 33) * K1;
            v3 ^= RotR(((v1 + v2) * K1) + v0, 33) * K0;
            v0 ^= RotR(((v0 + v2) * K0) + v3, 33) * K1;
            v1 ^= RotR(((v1 + v3) * K1) + v2, 33) * K0;
            hash += v0 ^ v1;
        }

        if (end - ptr >= 16)
        {
            ulong v0 = hash + (BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(ptr)) * K0); ptr += 8; v0 = RotR(v0, 33) * K1;
            ulong v1 = hash + (BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(ptr)) * K1); ptr += 8; v1 = RotR(v1, 33) * K2;
            v0 ^= RotR(v0 * K0, 35) + v1;
            v1 ^= RotR(v1 * K3, 35) + v0;
            hash += v1;
        }

        if (end - ptr >= 8)
        {
            hash += BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(ptr)) * K3; ptr += 8;
            hash ^= RotR(hash, 33) * K1;
        }

        if (end - ptr >= 4)
        {
            hash += (ulong)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(ptr)) * K3; ptr += 4;
            hash ^= RotR(hash, 15) * K1;
        }

        if (end - ptr >= 2)
        {
            hash += (ulong)BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(ptr)) * K3; ptr += 2;
            hash ^= RotR(hash, 13) * K1;
        }

        if (end - ptr >= 1)
        {
            hash += (ulong)data[ptr] * K3;
            hash ^= RotR(hash, 25) * K1;
        }

        hash ^= RotR(hash, 33);
        hash *= K0;
        hash ^= RotR(hash, 33);
        return hash;
    }

    private static ulong RotR(ulong value, int count) =>
        (value >> count) | (value << (64 - count));

    /// <summary>The metrohash64_1 finalization, used to pin the empty-input value.</summary>
    private static ulong Finalize(ulong h)
    {
        h ^= RotR(h, 33);
        h *= K0;
        h ^= RotR(h, 33);
        return h;
    }

    // ── Spec anchor (length-0 result) ─────────────────────────────────────────
    // metrohash64_1 of an empty input with seed 0 reduces to finalize(k2 * k0):
    // no stripes or lanes are consumed and the byte length is 0. The empty
    // string's UTF-16 byte stream is also zero bytes, so the hasher (after the
    // xor-fold) must reproduce that value folded to 32 bits. Expressed
    // symbolically from the constants so it pins the constants, the finalization,
    // and the fold direction without a hand-computed magic number.

    [Fact]
    public void Hash_EmptyString_ReturnsCanonicalMetroHash64OfEmptyInputFolded()
    {
        ulong empty = Finalize(K2 * K0);
        int expected = unchecked((int)(empty ^ (empty >> 32)));
        Assert.Equal(expected, _hasher.Hash(""));
    }

    [Fact]
    public void Reference_EmptyInput_MatchesSpecValue()
    {
        // Pins the reference helper itself against the spec-derived length-0 value,
        // so the spec-equivalence theory below is anchored independently of the
        // hasher's char-loop.
        Assert.Equal(Finalize(K2 * K0), MetroHash64(Array.Empty<byte>()));
    }

    // ── Spec equivalence (canonical metrohash64_1 over the UTF-16 byte stream) ──

    [Theory]
    [InlineData("")]
    [InlineData("a")]                    // 2 bytes → the 2-byte tail read
    [InlineData("ab")]                   // 4 bytes → one 4-byte block
    [InlineData("abc")]                  // 6 bytes → 4-byte block + trailing char
    [InlineData("abcd")]                 // 8 bytes → exactly one 8-byte lane
    [InlineData("abcde")]                // 8-byte lane + a trailing char
    [InlineData("abcdef")]               // 8-byte lane + a 4-byte block
    [InlineData("abcdefgh")]             // 16 bytes → exercises the 16-byte tail block
    [InlineData("abcdefghij")]           // 16-byte block + a 4-byte block
    [InlineData("hello")]
    [InlineData("hello world")]
    [InlineData("0123456789012345")]     // exactly 32 bytes → one full stripe, no tail
    [InlineData("the quick brown fox jumps")] // one stripe + tail
    [InlineData("the quick brown fox jumps over the lazy dog")] // multiple stripes + tail
    [InlineData("HELLO")]
    [InlineData("Ł")]                  // U+0141 — non-ASCII, high byte set
    [InlineData("Łatin")]              // mixed ASCII / non-ASCII
    [InlineData("日本語")]            // CJK, all high bytes set
    [InlineData("a\0b")]              // embedded NUL char (folds two zero bytes)
    [InlineData("😀")]                 // surrogate pair (two UTF-16 code units)
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // 36 chars → multiple stripes + lanes
    public void Hash_MatchesCanonicalMetroHash64OverUtf16ByteStream(string input)
    {
        Assert.Equal(ReferenceMetroHash64Utf16(input), _hasher.Hash(input));
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
        // must produce identical output for the same input.
        const string value = "another value";
        int a = new StringMetroHash64Hasher().Hash(value);
        int b = new StringMetroHash64Hasher().Hash(value);
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
        // The byte length is mixed into the hash, so a strict prefix must not
        // collide with the longer string here.
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
        // single-character strings; StringMetroHash64Hasher consumes the full 16-bit
        // character and must keep them distinct.
        var lowByte = new StringFnV1AHasher();
        Assert.Equal(lowByte.Hash("A"), lowByte.Hash("Ł"));
        Assert.NotEqual(_hasher.Hash("A"), _hasher.Hash("Ł"));
    }

    [Fact]
    public void Hash_DistinguishesMultibyteStringsThatLowByteFnv1aCollides()
    {
        // Two strings whose characters pairwise share low bytes but differ in
        // upper bytes: the low-byte FNV-1a collides, the full-width MetroHash does not.
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
    public void StringMetroHash64Hasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<string, int, StringMetroHash64Hasher>();

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
    public void StringMetroHash64Hasher_KeepsUpperByteDistinctKeysSeparate()
    {
        // The dictionary must treat 'A' and 'Ł' as distinct keys — the full-width
        // fold on the probe path.
        var dict = new CelerityDictionary<string, int, StringMetroHash64Hasher>();

        dict["A"] = 1;
        dict["Ł"] = 2;

        Assert.Equal(2, dict.Count);
        Assert.Equal(1, dict["A"]);
        Assert.Equal(2, dict["Ł"]);
    }

    [Fact]
    public void StringMetroHash64Hasher_HandlesNullKeyOutOfBand()
    {
        // The dictionary routes the null/default key around the hasher, so a null
        // key must round-trip without the hasher's null guard firing.
        var dict = new CelerityDictionary<string, int, StringMetroHash64Hasher>();

        dict[null!] = 42;
        dict["present"] = 1;

        Assert.Equal(2, dict.Count);
        Assert.True(dict.ContainsKey(null!));
        Assert.Equal(42, dict[null!]);
    }

    [Fact]
    public void StringMetroHash64Hasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<string, StringMetroHash64Hasher>();

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
