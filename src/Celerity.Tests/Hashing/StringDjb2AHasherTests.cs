using System.Text;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class StringDjb2AHasherTests
{
    private readonly StringDjb2AHasher _hasher = new StringDjb2AHasher();

    /// <summary>
    /// Independent reference: the djb2a (XOR-folding) variant over the BCL's
    /// little-endian UTF-16 encoding of the string. The hasher loops over
    /// <c>char</c>s and extracts the low/high byte by hand, expressing the
    /// multiply as a shift-and-add; this reference goes through
    /// <see cref="Encoding.Unicode"/>, a flat byte loop, and the literal
    /// <c>* 33</c> with an <c>^</c> fold, so agreement pins the byte order, the
    /// seed, and the per-byte step against drift without sharing the hasher's
    /// implementation.
    /// </summary>
    private static int ReferenceDjb2AUtf16(string s)
    {
        uint hash = 5381u;
        foreach (byte b in Encoding.Unicode.GetBytes(s))
        {
            hash = (hash * 33u) ^ b;
        }

        return unchecked((int)hash);
    }

    /// <summary>
    /// Independent reference for the additive djb2 (not djb2a) — used to prove the
    /// XOR variant is a genuinely distinct algorithm, not an accidental alias.
    /// </summary>
    private static int ReferenceDjb2Utf16(string s)
    {
        uint hash = 5381u;
        foreach (byte b in Encoding.Unicode.GetBytes(s))
        {
            hash = hash * 33u + b;
        }

        return unchecked((int)hash);
    }

    // ── Exact-value anchor ────────────────────────────────────────────────────
    // The empty string folds no bytes, so the accumulator stays at the djb2 seed
    // constant 5381 (djb2a shares djb2's seed).

    [Fact]
    public void Hash_EmptyString_ReturnsSeed()
    {
        Assert.Equal(5381, _hasher.Hash(""));
    }

    // ── Spec equivalence (djb2a over the UTF-16 byte stream) ───────────────────

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
    [InlineData("a\0b")]              // embedded NUL char (folds two zero bytes)
    [InlineData("😀")]                 // surrogate pair (two UTF-16 code units)
    public void Hash_MatchesDjb2AOverUtf16ByteStream(string input)
    {
        Assert.Equal(ReferenceDjb2AUtf16(input), _hasher.Hash(input));
    }

    /// <summary>
    /// Independent value anchor for a single ASCII character, computed here by
    /// hand-running the djb2a step over the two UTF-16 bytes of <c>'a'</c>
    /// (<c>0x61</c>, <c>0x00</c>) starting from the seed. This shares no code with
    /// either the hasher or the <see cref="Encoding.Unicode"/>-based reference,
    /// pinning the exact arithmetic to the algorithm definition: the result is
    /// <c>((5381 * 33) ^ 0x61) * 33 ^ 0x00</c>.
    /// </summary>
    [Fact]
    public void Hash_SingleAsciiChar_MatchesHandComputedDjb2A()
    {
        uint expected = (((5381u * 33u) ^ 0x61u) * 33u) ^ 0x00u;
        Assert.Equal(unchecked((int)expected), _hasher.Hash("a"));
    }

    /// <summary>
    /// djb2 (additive) and djb2a (XOR) share the seed and the <c>* 33</c> step but
    /// differ in the final combine, so for typical inputs they must produce
    /// different results — proving djb2a is not silently aliasing the additive
    /// variant. (The empty string is excluded: it folds no bytes, so both return
    /// the shared seed.)
    /// </summary>
    [Theory]
    [InlineData("a")]
    [InlineData("hello")]
    [InlineData("hello world")]
    [InlineData("Łatin")]
    public void Hash_DiffersFromAdditiveDjb2(string input)
    {
        Assert.NotEqual(ReferenceDjb2Utf16(input), _hasher.Hash(input));
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
        int a = new StringDjb2AHasher().Hash(value);
        int b = new StringDjb2AHasher().Hash(value);
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
        // single-character strings; StringDjb2AHasher folds the full 16-bit
        // character and must keep them distinct.
        var lowByte = new StringFnV1AHasher();
        Assert.Equal(lowByte.Hash("A"), lowByte.Hash("Ł"));
        Assert.NotEqual(_hasher.Hash("A"), _hasher.Hash("Ł"));
    }

    [Fact]
    public void Hash_DistinguishesMultibyteStringsThatLowByteFnv1aCollides()
    {
        // Two strings whose characters pairwise share low bytes but differ in
        // upper bytes: the low-byte FNV-1a collides, the full-width djb2a does not.
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
    public void StringDjb2AHasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<string, int, StringDjb2AHasher>();

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
    public void StringDjb2AHasher_KeepsUpperByteDistinctKeysSeparate()
    {
        // The dictionary must treat 'A' and 'Ł' as distinct keys — the whole
        // point of the full-width fold on the probe path.
        var dict = new CelerityDictionary<string, int, StringDjb2AHasher>();

        dict["A"] = 1;
        dict["Ł"] = 2;

        Assert.Equal(2, dict.Count);
        Assert.Equal(1, dict["A"]);
        Assert.Equal(2, dict["Ł"]);
    }

    [Fact]
    public void StringDjb2AHasher_HandlesNullKeyOutOfBand()
    {
        // The dictionary routes the null/default key around the hasher, so a null
        // key must round-trip without the hasher's null guard firing.
        var dict = new CelerityDictionary<string, int, StringDjb2AHasher>();

        dict[null!] = 42;
        dict["present"] = 1;

        Assert.Equal(2, dict.Count);
        Assert.True(dict.ContainsKey(null!));
        Assert.Equal(42, dict[null!]);
    }

    [Fact]
    public void StringDjb2AHasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<string, StringDjb2AHasher>();

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
