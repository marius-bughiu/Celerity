using System.Text;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class StringMurmur2HasherTests
{
    private readonly StringMurmur2Hasher _hasher = new StringMurmur2Hasher();

    private const uint M = 0x5bd1e995u;
    private const int R = 24;

    /// <summary>
    /// Independent reference: canonical MurmurHash2 (32-bit, seed <c>0</c>) over the
    /// BCL's little-endian UTF-16 encoding of the string. The hasher loops over
    /// <c>char</c> pairs and builds each block by hand; this reference goes through
    /// <see cref="Encoding.Unicode"/> and a byte-oriented block / tail loop with the
    /// literal <c>switch</c> fall-through tail, so agreement pins the byte order, the
    /// 4-byte block read, the per-block mix, the tail handling, and the finalizer
    /// against drift without sharing the hasher's char-loop implementation.
    /// </summary>
    private static int ReferenceMurmur2Utf16(string s)
    {
        byte[] data = Encoding.Unicode.GetBytes(s);
        int len = data.Length;

        uint h = (uint)len; // seed 0 ^ len

        int index = 0;
        while (len >= 4)
        {
            uint k = (uint)data[index]
                   | ((uint)data[index + 1] << 8)
                   | ((uint)data[index + 2] << 16)
                   | ((uint)data[index + 3] << 24);

            k *= M;
            k ^= k >> R;
            k *= M;

            h *= M;
            h ^= k;

            index += 4;
            len -= 4;
        }

        // Canonical fall-through tail (1..3 trailing bytes). A UTF-16 byte stream is
        // always an even length, so only the len == 2 and len == 0 arms are ever
        // exercised here — but the full ladder is written out to pin the algorithm.
        switch (len)
        {
            case 3:
                h ^= (uint)data[index + 2] << 16;
                goto case 2;
            case 2:
                h ^= (uint)data[index + 1] << 8;
                goto case 1;
            case 1:
                h ^= data[index];
                h *= M;
                break;
        }

        h ^= h >> 13;
        h *= M;
        h ^= h >> 15;

        return unchecked((int)h);
    }

    // ── Exact-value anchor ────────────────────────────────────────────────────
    // The empty string folds no characters: h = seed ^ 0 = 0, and the finalization
    // mix leaves 0 unchanged, so it must hash to 0 — mirroring the 0 → 0 fixed
    // point of StringMurmur3Hasher's empty string.

    [Fact]
    public void Hash_EmptyString_ReturnsZero()
    {
        Assert.Equal(0, _hasher.Hash(""));
    }

    // ── Spec equivalence (MurmurHash2 over the UTF-16 byte stream) ─────────────

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
    public void Hash_MatchesMurmur2OverUtf16ByteStream(string input)
    {
        Assert.Equal(ReferenceMurmur2Utf16(input), _hasher.Hash(input));
    }

    /// <summary>
    /// Independent algebraic pin for a single ASCII character, derived directly from
    /// the MurmurHash2 definition. One ASCII char is a 2-byte tail (no full block):
    /// <c>h = 2</c> (seed 0 XOR byte length 2); the tail XORs the char value and
    /// multiplies by <c>m</c>; then the finalization mix runs. This shares no code
    /// with either the hasher or the <see cref="Encoding.Unicode"/>-based reference.
    /// </summary>
    [Theory]
    [InlineData('a')]
    [InlineData('Z')]
    [InlineData('7')]
    public void Hash_SingleAsciiChar_MatchesMurmur2Definition(char c)
    {
        uint h = 2u;          // seed 0 ^ byteLength 2
        h ^= c;               // 2-byte tail: high<<8 | low == the char value
        h *= M;
        h ^= h >> 13;
        h *= M;
        h ^= h >> 15;
        int expected = unchecked((int)h);

        Assert.Equal(expected, _hasher.Hash(c.ToString()));
    }

    /// <summary>
    /// MurmurHash2 and MurmurHash3 share a family and a multiply constant but differ
    /// in their block mix and finalizer, so for typical inputs they must produce
    /// different results. Pin that the two are genuinely distinct algorithms (not an
    /// accidental alias).
    /// </summary>
    [Fact]
    public void Hash_DiffersFromMurmur3_ForTypicalInputs()
    {
        var murmur3 = new StringMurmur3Hasher();
        Assert.NotEqual(murmur3.Hash("hello"), _hasher.Hash("hello"));
        Assert.NotEqual(murmur3.Hash("Celerity"), _hasher.Hash("Celerity"));
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
        int a = new StringMurmur2Hasher().Hash(value);
        int b = new StringMurmur2Hasher().Hash(value);
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
        // The byte length is folded into the seed, so a strict prefix must not
        // collide with the longer string by construction of the algorithm.
        Assert.NotEqual(_hasher.Hash("abc"), _hasher.Hash("abcd"));
    }

    // ── Full-character distinction (folds the full UTF-16 character, so it does
    //    not share the low-byte StringFnV1AHasher's upper-byte collisions) ──────

    [Fact]
    public void Hash_DistinguishesCharactersDifferingOnlyInUpperByte()
    {
        // 'A' (U+0041) and 'Ł' (U+0141) share the same low byte (0x41).
        // The low-byte StringFnV1AHasher folds only that byte and so collides
        // these two single-character strings; StringMurmur2Hasher folds the full
        // 16-bit character and must keep them distinct.
        var lowByte = new StringFnV1AHasher();
        Assert.Equal(lowByte.Hash("A"), lowByte.Hash("Ł"));
        Assert.NotEqual(_hasher.Hash("A"), _hasher.Hash("Ł"));
    }

    [Fact]
    public void Hash_DistinguishesMultibyteStringsThatLowByteFnvCollides()
    {
        // Two strings whose characters pairwise share low bytes but differ in
        // upper bytes: the low-byte FNV collides, the full-width Murmur2 does not.
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
        // 1000 distinct short strings should hash without collision under a good
        // 32-bit mixer; the hash is deterministic, so a pass here is a pass
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
    public void StringMurmur2Hasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<string, int, StringMurmur2Hasher>();

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
    public void StringMurmur2Hasher_KeepsUpperByteDistinctKeysSeparate()
    {
        // The dictionary must treat 'A' and 'Ł' as distinct keys — the full-width
        // fold on the probe path.
        var dict = new CelerityDictionary<string, int, StringMurmur2Hasher>();

        dict["A"] = 1;
        dict["Ł"] = 2;

        Assert.Equal(2, dict.Count);
        Assert.Equal(1, dict["A"]);
        Assert.Equal(2, dict["Ł"]);
    }

    [Fact]
    public void StringMurmur2Hasher_HandlesNullKeyOutOfBand()
    {
        // The dictionary routes the null/default key around the hasher, so a null
        // key must round-trip without the hasher's null guard firing.
        var dict = new CelerityDictionary<string, int, StringMurmur2Hasher>();

        dict[null!] = 42;
        dict["present"] = 1;

        Assert.Equal(2, dict.Count);
        Assert.True(dict.ContainsKey(null!));
        Assert.Equal(42, dict[null!]);
    }

    [Fact]
    public void StringMurmur2Hasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<string, StringMurmur2Hasher>();

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
