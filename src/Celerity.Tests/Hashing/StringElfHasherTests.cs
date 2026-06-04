using System.Text;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class StringElfHasherTests
{
    private readonly StringElfHasher _hasher = new StringElfHasher();

    /// <summary>
    /// Independent reference: the canonical System V ABI <c>elf_hash</c> over the
    /// BCL's little-endian UTF-16 encoding of the string. The hasher loops over
    /// <c>char</c>s and extracts the low/high byte by hand; this reference goes
    /// through <see cref="Encoding.Unicode"/> and a flat byte loop, so agreement
    /// pins the byte order, the seed, the per-byte step, and the top-nibble
    /// feedback/clear against drift without sharing the hasher's char-loop.
    /// </summary>
    private static int ReferenceElfUtf16(string s)
    {
        uint hash = 0u;
        foreach (byte b in Encoding.Unicode.GetBytes(s))
        {
            hash = (hash << 4) + b;
            uint high = hash & 0xF0000000u;
            if (high != 0u)
            {
                hash ^= high >> 24;
            }

            hash &= ~high;
        }

        return unchecked((int)hash);
    }

    // ── Exact-value anchor ────────────────────────────────────────────────────
    // The empty string folds no bytes, so the accumulator stays at the ELF seed
    // constant 0.

    [Fact]
    public void Hash_EmptyString_ReturnsSeed()
    {
        Assert.Equal(0, _hasher.Hash(""));
    }

    // ── Spec equivalence (ELF hash over the UTF-16 byte stream) ────────────────

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
    public void Hash_MatchesElfOverUtf16ByteStream(string input)
    {
        Assert.Equal(ReferenceElfUtf16(input), _hasher.Hash(input));
    }

    /// <summary>
    /// Independent value anchor for a single ASCII character, computed here by
    /// hand-running the ELF step over the two UTF-16 bytes of <c>'a'</c>
    /// (<c>0x61</c>, <c>0x00</c>) starting from the seed. Neither byte ever drives
    /// the accumulator into its top nibble, so the high-nibble feedback never fires
    /// and the result is simply <c>((0 &lt;&lt; 4) + 0x61) &lt;&lt; 4) + 0x00 == 0x610</c>.
    /// This shares no code with either the hasher or the
    /// <see cref="Encoding.Unicode"/>-based reference, pinning the exact arithmetic
    /// to the algorithm definition.
    /// </summary>
    [Fact]
    public void Hash_SingleAsciiChar_MatchesHandComputedElf()
    {
        // step 1 (low byte 0x61): hash = (0 << 4) + 0x61      = 0x61
        // step 2 (high byte 0x00): hash = (0x61 << 4) + 0x00  = 0x610
        Assert.Equal(0x610, _hasher.Hash("a"));
    }

    /// <summary>
    /// Pins the defining behaviour of the ELF hash: the top nibble is always
    /// cleared, so every result is non-negative and no larger than
    /// <c>0x0FFFFFFF</c>. A naïve shift-and-add without the fold/clear would let
    /// the accumulator occupy all 32 bits and go negative for long inputs.
    /// </summary>
    [Fact]
    public void Hash_NeverSetsTopNibble()
    {
        foreach (string key in new[] { "", "a", "abc", "a long-ish key that climbs high", "日本語テスト", new string('z', 64) })
        {
            int h = _hasher.Hash(key);
            Assert.True(h >= 0, $"Result for \"{key}\" was negative ({h:X8}).");
            Assert.Equal(0u, (uint)h & 0xF0000000u);
        }
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
        int a = new StringElfHasher().Hash(value);
        int b = new StringElfHasher().Hash(value);
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
        // single-character strings; StringElfHasher folds the full 16-bit
        // character and must keep them distinct.
        var lowByte = new StringFnV1AHasher();
        Assert.Equal(lowByte.Hash("A"), lowByte.Hash("Ł"));
        Assert.NotEqual(_hasher.Hash("A"), _hasher.Hash("Ł"));
    }

    [Fact]
    public void Hash_DistinguishesMultibyteStringsThatLowByteFnv1aCollides()
    {
        // Two strings whose characters pairwise share low bytes but differ in
        // upper bytes: the low-byte FNV-1a collides, the full-width ELF hash does not.
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
    public void StringElfHasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<string, int, StringElfHasher>();

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
    public void StringElfHasher_KeepsUpperByteDistinctKeysSeparate()
    {
        // The dictionary must treat 'A' and 'Ł' as distinct keys — the whole
        // point of the full-width fold on the probe path.
        var dict = new CelerityDictionary<string, int, StringElfHasher>();

        dict["A"] = 1;
        dict["Ł"] = 2;

        Assert.Equal(2, dict.Count);
        Assert.Equal(1, dict["A"]);
        Assert.Equal(2, dict["Ł"]);
    }

    [Fact]
    public void StringElfHasher_HandlesNullKeyOutOfBand()
    {
        // The dictionary routes the null/default key around the hasher, so a null
        // key must round-trip without the hasher's null guard firing.
        var dict = new CelerityDictionary<string, int, StringElfHasher>();

        dict[null!] = 42;
        dict["present"] = 1;

        Assert.Equal(2, dict.Count);
        Assert.True(dict.ContainsKey(null!));
        Assert.Equal(42, dict[null!]);
    }

    [Fact]
    public void StringElfHasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<string, StringElfHasher>();

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
