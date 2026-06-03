using System.Text;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class StringJenkinsOaatHasherTests
{
    private readonly StringJenkinsOaatHasher _hasher = new StringJenkinsOaatHasher();

    /// <summary>
    /// Independent reference: Bob Jenkins' one-at-a-time hash over the BCL's
    /// little-endian UTF-16 encoding of the string. The hasher loops over
    /// <c>char</c>s and extracts the low/high byte by hand; this reference goes
    /// through <see cref="Encoding.Unicode"/> and a flat byte loop, so agreement
    /// pins the byte order and the per-byte mix against drift without sharing the
    /// hasher's implementation.
    /// </summary>
    private static int ReferenceOaatUtf16(string s)
    {
        uint hash = 0u;
        foreach (byte b in Encoding.Unicode.GetBytes(s))
        {
            hash += b;
            hash += hash << 10;
            hash ^= hash >> 6;
        }

        hash += hash << 3;
        hash ^= hash >> 11;
        hash += hash << 15;

        return unchecked((int)hash);
    }

    // ── Exact-value anchor ────────────────────────────────────────────────────
    // The empty string mixes no bytes, so the accumulator stays at its initial
    // zero through the (zero-preserving) finalization steps and the hash is 0.

    [Fact]
    public void Hash_EmptyString_ReturnsZero()
    {
        Assert.Equal(0, _hasher.Hash(""));
    }

    // ── Spec equivalence (one-at-a-time over the UTF-16 byte stream) ───────────

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("abc")]
    [InlineData("hello")]
    [InlineData("hello world")]
    [InlineData("HELLO")]
    [InlineData("Ł")]                  // U+0141 — non-ASCII, high byte set
    [InlineData("Łatin")]              // mixed ASCII / non-ASCII
    [InlineData("日本語")]            // CJK, all high bytes set
    [InlineData("a\0b")]              // embedded NUL char (mixes two zero bytes)
    [InlineData("😀")]                 // surrogate pair (two UTF-16 code units)
    public void Hash_MatchesOaatOverUtf16ByteStream(string input)
    {
        Assert.Equal(ReferenceOaatUtf16(input), _hasher.Hash(input));
    }

    /// <summary>
    /// Independent value anchor for a single ASCII character, computed here by
    /// hand-running the one-at-a-time steps over the two UTF-16 bytes of
    /// <c>'a'</c> (<c>0x61</c>, <c>0x00</c>). This shares no code with either the
    /// hasher or the <see cref="Encoding.Unicode"/>-based reference, pinning the
    /// exact arithmetic to the algorithm definition.
    /// </summary>
    [Fact]
    public void Hash_SingleAsciiChar_MatchesHandComputedOaat()
    {
        uint hash = 0u;
        foreach (byte b in new byte[] { 0x61, 0x00 }) // 'a' little-endian UTF-16
        {
            hash += b;
            hash += hash << 10;
            hash ^= hash >> 6;
        }
        hash += hash << 3;
        hash ^= hash >> 11;
        hash += hash << 15;

        Assert.Equal(unchecked((int)hash), _hasher.Hash("a"));
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
        int a = new StringJenkinsOaatHasher().Hash(value);
        int b = new StringJenkinsOaatHasher().Hash(value);
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
        // StringFnV1AHasher folds only that low byte, so it collides on these two
        // single-character strings; StringJenkinsOaatHasher mixes the full 16-bit
        // character and must keep them distinct.
        var lowByte = new StringFnV1AHasher();
        Assert.Equal(lowByte.Hash("A"), lowByte.Hash("Ł"));
        Assert.NotEqual(_hasher.Hash("A"), _hasher.Hash("Ł"));
    }

    [Fact]
    public void Hash_DistinguishesMultibyteStringsThatLowByteFnv1aCollides()
    {
        // Two strings whose characters pairwise share low bytes but differ in
        // upper bytes: the low-byte FNV-1a collides, the full-width OAAT does not.
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
    public void StringJenkinsOaatHasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<string, int, StringJenkinsOaatHasher>();

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
    public void StringJenkinsOaatHasher_KeepsUpperByteDistinctKeysSeparate()
    {
        // The dictionary must treat 'A' and 'Ł' as distinct keys — the whole
        // point of the full-width fold on the probe path.
        var dict = new CelerityDictionary<string, int, StringJenkinsOaatHasher>();

        dict["A"] = 1;
        dict["Ł"] = 2;

        Assert.Equal(2, dict.Count);
        Assert.Equal(1, dict["A"]);
        Assert.Equal(2, dict["Ł"]);
    }

    [Fact]
    public void StringJenkinsOaatHasher_HandlesNullKeyOutOfBand()
    {
        // The dictionary routes the null/default key around the hasher, so a null
        // key must round-trip without the hasher's null guard firing.
        var dict = new CelerityDictionary<string, int, StringJenkinsOaatHasher>();

        dict[null!] = 42;
        dict["present"] = 1;

        Assert.Equal(2, dict.Count);
        Assert.True(dict.ContainsKey(null!));
        Assert.Equal(42, dict[null!]);
    }

    [Fact]
    public void StringJenkinsOaatHasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<string, StringJenkinsOaatHasher>();

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
