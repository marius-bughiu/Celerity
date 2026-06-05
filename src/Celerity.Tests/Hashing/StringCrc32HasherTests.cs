using System.Text;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class StringCrc32HasherTests
{
    private readonly StringCrc32Hasher _hasher = new StringCrc32Hasher();

    /// <summary>
    /// Independent reference: the standard CRC-32 (ISO-HDLC / IEEE 802.3 / zlib) of an
    /// arbitrary byte array, computed with the <em>bit-by-bit</em> reflected algorithm
    /// (no lookup table). The hasher uses the 256-entry table-driven form over a
    /// <c>char</c> loop; this reference uses the table-free bitwise form over a raw
    /// byte array, so agreement pins the polynomial, the reflected bit order, the
    /// <c>0xFFFFFFFF</c> pre-set and final XOR, and the byte order against drift
    /// without sharing the hasher's implementation. The reference is itself anchored
    /// to the universal CRC-32 check value in
    /// <see cref="Reference_MatchesCanonicalCheckVector"/>.
    /// </summary>
    private static uint Crc32OverBytes(byte[] data)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1u) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static int ReferenceCrc32Utf16(string s)
        => unchecked((int)Crc32OverBytes(Encoding.Unicode.GetBytes(s)));

    // ── External canonical anchor ─────────────────────────────────────────────
    // The universal CRC-32 (ISO-HDLC) check value: the CRC of the nine ASCII bytes
    // "123456789" is 0xCBF43926. Pinning the test's own reference against this
    // published constant proves the reference implements *the* standard CRC-32, so
    // the spec-equivalence theory below is anchored to ground truth, not to a
    // hand-computed value of the hasher itself.

    [Fact]
    public void Reference_MatchesCanonicalCheckVector()
    {
        byte[] check = Encoding.ASCII.GetBytes("123456789");
        Assert.Equal(0xCBF43926u, Crc32OverBytes(check));
    }

    // ── Exact-value anchor ────────────────────────────────────────────────────
    // The empty string folds no characters: the accumulator stays at the initial
    // 0xFFFFFFFF and the final XOR with 0xFFFFFFFF yields 0, so it must hash to 0 —
    // mirroring the 0 → 0 fixed point of StringMurmur2Hasher's / StringMurmur3Hasher's
    // empty string.

    [Fact]
    public void Hash_EmptyString_ReturnsZero()
    {
        Assert.Equal(0, _hasher.Hash(""));
    }

    // ── Spec equivalence (CRC-32 over the UTF-16 byte stream) ──────────────────

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("abc")]
    [InlineData("abcd")]
    [InlineData("hello")]
    [InlineData("hello world")]
    [InlineData("HELLO")]
    [InlineData("Ł")]                  // U+0141 — non-ASCII, high byte set
    [InlineData("Łatin")]              // mixed ASCII / non-ASCII
    [InlineData("日本語")]            // CJK, all high bytes set
    [InlineData("a\0b")]              // embedded NUL char (folds two zero bytes)
    [InlineData("😀")]                 // surrogate pair (two UTF-16 code units)
    public void Hash_MatchesCrc32OverUtf16ByteStream(string input)
    {
        Assert.Equal(ReferenceCrc32Utf16(input), _hasher.Hash(input));
    }

    /// <summary>
    /// CRC-32 is a checksum, not a designed mixer, so for typical inputs it must
    /// produce different results from the other cheap-tier classics. Pin that it is a
    /// genuinely distinct algorithm rather than an accidental alias of its neighbours.
    /// </summary>
    [Fact]
    public void Hash_DiffersFromOtherClassicHashers_ForTypicalInputs()
    {
        var elf = new StringElfHasher();
        var djb2 = new StringDjb2Hasher();
        Assert.NotEqual(elf.Hash("hello"), _hasher.Hash("hello"));
        Assert.NotEqual(djb2.Hash("hello"), _hasher.Hash("hello"));
        Assert.NotEqual(elf.Hash("Celerity"), _hasher.Hash("Celerity"));
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
        int a = new StringCrc32Hasher().Hash(value);
        int b = new StringCrc32Hasher().Hash(value);
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
        // CRC-32 of a strict prefix differs from the longer string (a different
        // polynomial remainder), so a prefix must not collide with the longer key.
        Assert.NotEqual(_hasher.Hash("abc"), _hasher.Hash("abcd"));
    }

    // ── Full-character distinction (folds the full UTF-16 character, so it does
    //    not share the low-byte StringFnV1AHasher's upper-byte collisions) ──────

    [Fact]
    public void Hash_DistinguishesCharactersDifferingOnlyInUpperByte()
    {
        // 'A' (U+0041) and 'Ł' (U+0141) share the same low byte (0x41).
        // The low-byte StringFnV1AHasher folds only that byte and so collides
        // these two single-character strings; StringCrc32Hasher folds the full
        // 16-bit character and must keep them distinct.
        var lowByte = new StringFnV1AHasher();
        Assert.Equal(lowByte.Hash("A"), lowByte.Hash("Ł"));
        Assert.NotEqual(_hasher.Hash("A"), _hasher.Hash("Ł"));
    }

    [Fact]
    public void Hash_DistinguishesMultibyteStringsThatLowByteFnvCollides()
    {
        // Two strings whose characters pairwise share low bytes but differ in
        // upper bytes: the low-byte FNV collides, the full-width CRC-32 does not.
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
        // 1000 distinct short strings should hash without collision under CRC-32's
        // 32-bit spread; the hash is deterministic, so a pass here is a pass
        // everywhere.
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
    public void StringCrc32Hasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<string, int, StringCrc32Hasher>();

        dict[""] = 0;            // empty string is a regular key (hashes to 0)
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
    public void StringCrc32Hasher_KeepsUpperByteDistinctKeysSeparate()
    {
        // The dictionary must treat 'A' and 'Ł' as distinct keys — the full-width
        // fold on the probe path.
        var dict = new CelerityDictionary<string, int, StringCrc32Hasher>();

        dict["A"] = 1;
        dict["Ł"] = 2;

        Assert.Equal(2, dict.Count);
        Assert.Equal(1, dict["A"]);
        Assert.Equal(2, dict["Ł"]);
    }

    [Fact]
    public void StringCrc32Hasher_HandlesNullKeyOutOfBand()
    {
        // The dictionary routes the null/default key around the hasher, so a null
        // key must round-trip without the hasher's null guard firing.
        var dict = new CelerityDictionary<string, int, StringCrc32Hasher>();

        dict[null!] = 42;
        dict["present"] = 1;

        Assert.Equal(2, dict.Count);
        Assert.True(dict.ContainsKey(null!));
        Assert.Equal(42, dict[null!]);
    }

    [Fact]
    public void StringCrc32Hasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<string, StringCrc32Hasher>();

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
