using System.Text;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class StringAdler32HasherTests
{
    private readonly StringAdler32Hasher _hasher = new StringAdler32Hasher();

    /// <summary>
    /// Independent reference: the standard Adler-32 (RFC 1950 / zlib) of an arbitrary
    /// byte array, computed straight from the definition — two running 16-bit sums
    /// modulo 65521, <c>a</c> seeded to 1 and <c>b</c> to 0, result <c>(b &lt;&lt; 16) | a</c>.
    /// The hasher runs the same sums over a <c>char</c> loop folding both UTF-16 bytes;
    /// this reference runs over a raw byte array, so agreement pins the modulus, the
    /// seeds, the running-sum order, the byte order, and the <c>(b &lt;&lt; 16) | a</c>
    /// packing against drift without sharing the hasher's implementation. The reference
    /// is itself anchored to the universal Adler-32 check value in
    /// <see cref="Reference_MatchesCanonicalCheckVector"/>.
    /// </summary>
    private static uint Adler32OverBytes(byte[] data)
    {
        const uint mod = 65521u;
        uint a = 1u;
        uint b = 0u;
        foreach (byte x in data)
        {
            a = (a + x) % mod;
            b = (b + a) % mod;
        }

        return (b << 16) | a;
    }

    private static int ReferenceAdler32Utf16(string s)
        => unchecked((int)Adler32OverBytes(Encoding.Unicode.GetBytes(s)));

    // ── External canonical anchor ─────────────────────────────────────────────
    // The universal Adler-32 check value: the Adler-32 of the nine ASCII bytes of
    // "Wikipedia" is 0x11E60398 (the worked example from the Adler-32 specification).
    // Pinning the test's own reference against this published constant proves the
    // reference implements *the* standard Adler-32, so the spec-equivalence theory
    // below is anchored to ground truth, not to a hand-computed value of the hasher
    // itself.

    [Fact]
    public void Reference_MatchesCanonicalCheckVector()
    {
        byte[] check = Encoding.ASCII.GetBytes("Wikipedia");
        Assert.Equal(0x11E60398u, Adler32OverBytes(check));
    }

    // ── Exact-value anchor ────────────────────────────────────────────────────
    // The empty string folds no characters: the sums stay at their seeds (a = 1,
    // b = 0), so the packed result is (0 << 16) | 1 == 1.

    [Fact]
    public void Hash_EmptyString_ReturnsOne()
    {
        Assert.Equal(1, _hasher.Hash(""));
    }

    // ── Spec equivalence (Adler-32 over the UTF-16 byte stream) ────────────────

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
    public void Hash_MatchesAdler32OverUtf16ByteStream(string input)
    {
        Assert.Equal(ReferenceAdler32Utf16(input), _hasher.Hash(input));
    }

    /// <summary>
    /// Adler-32 is a checksum, not a designed mixer, so for typical inputs it must
    /// produce different results from the other cheap-tier classics and from its
    /// fellow checksum CRC-32. Pin that it is a genuinely distinct algorithm rather
    /// than an accidental alias of its neighbours.
    /// </summary>
    [Fact]
    public void Hash_DiffersFromOtherClassicHashers_ForTypicalInputs()
    {
        var crc32 = new StringCrc32Hasher();
        var elf = new StringElfHasher();
        var djb2 = new StringDjb2Hasher();
        Assert.NotEqual(crc32.Hash("hello"), _hasher.Hash("hello"));
        Assert.NotEqual(elf.Hash("hello"), _hasher.Hash("hello"));
        Assert.NotEqual(djb2.Hash("hello"), _hasher.Hash("hello"));
        Assert.NotEqual(crc32.Hash("Celerity"), _hasher.Hash("Celerity"));
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
        int a = new StringAdler32Hasher().Hash(value);
        int b = new StringAdler32Hasher().Hash(value);
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
        // Adler-32 of a strict prefix differs from the longer string (more terms
        // accumulate into both running sums), so a prefix must not collide with the
        // longer key.
        Assert.NotEqual(_hasher.Hash("abc"), _hasher.Hash("abcd"));
    }

    // ── Full-character distinction (folds the full UTF-16 character, so it does
    //    not share the low-byte StringFnV1AHasher's upper-byte collisions) ──────

    [Fact]
    public void Hash_DistinguishesCharactersDifferingOnlyInUpperByte()
    {
        // 'A' (U+0041) and 'Ł' (U+0141) share the same low byte (0x41).
        // The low-byte StringFnV1AHasher folds only that byte and so collides
        // these two single-character strings; StringAdler32Hasher folds the full
        // 16-bit character and must keep them distinct.
        var lowByte = new StringFnV1AHasher();
        Assert.Equal(lowByte.Hash("A"), lowByte.Hash("Ł"));
        Assert.NotEqual(_hasher.Hash("A"), _hasher.Hash("Ł"));
    }

    [Fact]
    public void Hash_DistinguishesMultibyteStringsThatLowByteFnvCollides()
    {
        // Two strings whose characters pairwise share low bytes but differ in
        // upper bytes: the low-byte FNV collides, the full-width Adler-32 does not.
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
        // Adler-32 is a checksum, not a designed hash: its low 16 bits are just the
        // running byte-sum, so *unrelated* short keys cluster badly (its documented
        // weakness — `key-0` … `key-999` collide en masse, unlike under CRC-32). The
        // sweep therefore uses a corpus with strictly distinct byte-sums — each key
        // appends `i` copies of one character to a fixed prefix, so its byte-sum is
        // `P + 97 * i`, all distinct modulo the prime 65521 for i < 65521 (97 is
        // invertible there). Distinct `a` ⇒ distinct packed result, so this confirms
        // the hasher separates keys it *can* separate without collision, while the
        // remarks on the type document that arbitrary short keys do not get this
        // guarantee. The hash is deterministic, so a pass here is a pass everywhere.
        var seen = new HashSet<int>();
        for (int i = 0; i < 1000; i++)
        {
            string key = "celerity-adler-probe-" + new string('a', i);
            Assert.True(seen.Add(_hasher.Hash(key)),
                $"Unexpected collision at distinct-byte-sum input of length {key.Length}.");
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
    public void StringAdler32Hasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<string, int, StringAdler32Hasher>();

        dict[""] = 0;            // empty string is a regular key (hashes to 1)
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
    public void StringAdler32Hasher_KeepsUpperByteDistinctKeysSeparate()
    {
        // The dictionary must treat 'A' and 'Ł' as distinct keys — the full-width
        // fold on the probe path.
        var dict = new CelerityDictionary<string, int, StringAdler32Hasher>();

        dict["A"] = 1;
        dict["Ł"] = 2;

        Assert.Equal(2, dict.Count);
        Assert.Equal(1, dict["A"]);
        Assert.Equal(2, dict["Ł"]);
    }

    [Fact]
    public void StringAdler32Hasher_HandlesNullKeyOutOfBand()
    {
        // The dictionary routes the null/default key around the hasher, so a null
        // key must round-trip without the hasher's null guard firing.
        var dict = new CelerityDictionary<string, int, StringAdler32Hasher>();

        dict[null!] = 42;
        dict["present"] = 1;

        Assert.Equal(2, dict.Count);
        Assert.True(dict.ContainsKey(null!));
        Assert.Equal(42, dict[null!]);
    }

    [Fact]
    public void StringAdler32Hasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<string, StringAdler32Hasher>();

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
