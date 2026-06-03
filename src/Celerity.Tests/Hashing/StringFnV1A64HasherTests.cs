using System.Text;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class StringFnV1A64HasherTests
{
    private readonly StringFnV1A64Hasher _hasher = new StringFnV1A64Hasher();

    private const ulong FnvPrime = 1099511628211UL;
    private const ulong OffsetBasis = 14695981039346656037UL;

    /// <summary>
    /// Independent reference: plain FNV-1a 64-bit over the BCL's little-endian
    /// UTF-16 encoding of the string, xor-folded to 32 bits. The hasher loops over
    /// <c>char</c>s and extracts the low/high byte by hand; this reference goes
    /// through <see cref="Encoding.Unicode"/> and a byte loop, so agreement pins
    /// the byte order, the per-character fold, and the final fold against drift
    /// without sharing the hasher's implementation.
    /// </summary>
    private static int ReferenceFnv1a64Utf16(string s)
    {
        ulong hash = OffsetBasis;
        foreach (byte b in Encoding.Unicode.GetBytes(s))
        {
            hash ^= b;
            hash *= FnvPrime;
        }

        return unchecked((int)(hash ^ (hash >> 32)));
    }

    // ── Exact-value anchor ────────────────────────────────────────────────────
    // The empty string folds no characters, so it must hash to the 64-bit offset
    // basis xor-folded down to 32 bits. Expressed symbolically so it pins the fold
    // direction without a hand-computed magic number.

    [Fact]
    public void Hash_EmptyString_ReturnsFoldedOffsetBasis()
    {
        int expected = unchecked((int)(OffsetBasis ^ (OffsetBasis >> 32)));
        Assert.Equal(expected, _hasher.Hash(""));
    }

    // ── Spec equivalence (FNV-1a 64-bit over the UTF-16 byte stream) ───────────

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
    public void Hash_MatchesFnv1a64OverUtf16ByteStream(string input)
    {
        Assert.Equal(ReferenceFnv1a64Utf16(input), _hasher.Hash(input));
    }

    /// <summary>
    /// Independent algebraic pin for the 64-bit accumulator <em>before</em> the
    /// fold. Appending a <c>0x00</c> byte to an FNV-1a accumulator is equivalent to
    /// multiplying by the prime (<c>(h ^ 0) * prime</c>), so for a one-character
    /// ASCII string the pre-fold 64-bit state is
    /// <c>((offsetBasis ^ lowByte) * prime) * prime</c>. We then apply the same
    /// fold and compare — derived from the FNV-1a definition, independent of both
    /// the hasher and the <see cref="Encoding.Unicode"/>-based reference.
    /// </summary>
    [Theory]
    [InlineData('a')]
    [InlineData('Z')]
    [InlineData('7')]
    public void Hash_SingleAsciiChar_MatchesAlgebraicDerivation(char c)
    {
        ulong state = OffsetBasis;
        state ^= (byte)(c & 0xFF);
        state *= FnvPrime;
        state ^= (byte)(c >> 8); // 0 for ASCII
        state *= FnvPrime;
        int expected = unchecked((int)(state ^ (state >> 32)));

        Assert.Equal(expected, _hasher.Hash(c.ToString()));
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
        int a = new StringFnV1A64Hasher().Hash(value);
        int b = new StringFnV1A64Hasher().Hash(value);
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
        // FNV-1a never leaves the accumulator unchanged by a fold step, so a
        // strict prefix must not collide with the longer string here.
        Assert.NotEqual(_hasher.Hash("abc"), _hasher.Hash("abcd"));
    }

    // ── Full-character distinction (shared with StringFnV1AFullHasher — the
    //    full-width fold keeps upper-byte-distinct characters apart, unlike the
    //    low-byte StringFnV1AHasher) ─────────────────────────────────────────────

    [Fact]
    public void Hash_DistinguishesCharactersDifferingOnlyInUpperByte()
    {
        // 'A' (U+0041) and 'Ł' (U+0141) share the same low byte (0x41).
        // StringFnV1AHasher folds only that low byte, so it collides on these two
        // single-character strings; StringFnV1A64Hasher folds the full 16-bit
        // character and must keep them distinct.
        var lowByte = new StringFnV1AHasher();
        Assert.Equal(lowByte.Hash("A"), lowByte.Hash("Ł"));
        Assert.NotEqual(_hasher.Hash("A"), _hasher.Hash("Ł"));
    }

    [Fact]
    public void Hash_DistinguishesMultibyteStringsThatLowByteFnv1aCollides()
    {
        // Two strings whose characters pairwise share low bytes but differ in
        // upper bytes: the low-byte FNV-1a collides, the full-width one does not.
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
    public void StringFnV1A64Hasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<string, int, StringFnV1A64Hasher>();

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
    public void StringFnV1A64Hasher_KeepsUpperByteDistinctKeysSeparate()
    {
        // The dictionary must treat 'A' and 'Ł' as distinct keys — the full-width
        // fold on the probe path.
        var dict = new CelerityDictionary<string, int, StringFnV1A64Hasher>();

        dict["A"] = 1;
        dict["Ł"] = 2;

        Assert.Equal(2, dict.Count);
        Assert.Equal(1, dict["A"]);
        Assert.Equal(2, dict["Ł"]);
    }

    [Fact]
    public void StringFnV1A64Hasher_HandlesNullKeyOutOfBand()
    {
        // The dictionary routes the null/default key around the hasher, so a null
        // key must round-trip without the hasher's null guard firing.
        var dict = new CelerityDictionary<string, int, StringFnV1A64Hasher>();

        dict[null!] = 42;
        dict["present"] = 1;

        Assert.Equal(2, dict.Count);
        Assert.True(dict.ContainsKey(null!));
        Assert.Equal(42, dict[null!]);
    }

    [Fact]
    public void StringFnV1A64Hasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<string, StringFnV1A64Hasher>();

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
