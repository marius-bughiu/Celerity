using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class StringMurmur3HasherTests
{
    private readonly StringMurmur3Hasher _hasher = new StringMurmur3Hasher();

    // ── Exact-value anchors ───────────────────────────────────────────────────
    // The empty string is the fixed point of fmix32 over a zero accumulator
    // (no blocks, no tail, length 0), so it must hash to 0 — mirroring the
    // 0 → 0 fixed point of the integer Murmur3 hashers. The remaining anchors
    // pin the algorithm against accidental drift (block read order, tail
    // handling, finalizer constants); they are MurmurHash3 x86_32 over the
    // little-endian UTF-16 byte stream of each key.

    [Theory]
    [InlineData("", 0)]
    [InlineData("a", 1867108634)]
    [InlineData("ab", 374890698)]
    [InlineData("abc", 1118836419)]
    [InlineData("hello", -675079799)]
    [InlineData("hello world", 1689409188)]
    public void Hash_ReturnsExpected(string input, int expected)
    {
        Assert.Equal(expected, _hasher.Hash(input));
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
        int a = new StringMurmur3Hasher().Hash(value);
        int b = new StringMurmur3Hasher().Hash(value);
        Assert.Equal(a, b);
    }

    // ── Case sensitivity ──────────────────────────────────────────────────────

    [Fact]
    public void Hash_DifferentCaseStrings_ProduceDifferentValues()
    {
        Assert.NotEqual(_hasher.Hash("hello"), _hasher.Hash("HELLO"));
    }

    // ── Avalanche ─────────────────────────────────────────────────────────────

    [Fact]
    public void Hash_SingleCharacterChange_ChangesResult()
    {
        // A one-character edit must alter the hash; guards against a regression
        // where the finalizer or block mixing stops propagating low-order
        // differences.
        Assert.NotEqual(_hasher.Hash("hello"), _hasher.Hash("hellp"));
    }

    [Fact]
    public void Hash_AppendingCharacter_ChangesResult()
    {
        // The length is folded into the finalizer, so a strict prefix must not
        // collide with the longer string by construction of the algorithm.
        Assert.NotEqual(_hasher.Hash("abc"), _hasher.Hash("abcd"));
    }

    // ── Full-character distinction (the headline improvement over FNV-1a) ──────

    [Fact]
    public void Hash_DistinguishesCharactersDifferingOnlyInUpperByte()
    {
        // 'A' (U+0041) and 'Ł' (U+0141) share the same low byte (0x41).
        // StringFnV1AHasher hashes only that low byte, so it collides on these
        // two single-character strings; StringMurmur3Hasher consumes the full
        // 16-bit character and must keep them distinct. This is the core reason
        // the hasher exists, so pin both halves of the contrast.
        var fnv = new StringFnV1AHasher();
        Assert.Equal(fnv.Hash("A"), fnv.Hash("Ł"));
        Assert.NotEqual(_hasher.Hash("A"), _hasher.Hash("Ł"));
    }

    [Fact]
    public void Hash_DistinguishesMultibyteStringsThatFNV1ACollides()
    {
        // Two strings whose characters pairwise share low bytes but differ in
        // upper bytes: FNV-1a (low-byte only) collides, Murmur3 does not.
        var fnv = new StringFnV1AHasher();
        const string ascii = "AB";          // U+0041 U+0042
        const string wide = "Łł"; // U+0141 U+0142 — same low bytes
        Assert.Equal(fnv.Hash(ascii), fnv.Hash(wide));
        Assert.NotEqual(_hasher.Hash(ascii), _hasher.Hash(wide));
    }

    // ── Distinctness sweep ────────────────────────────────────────────────────

    [Fact]
    public void Hash_ManyDistinctStrings_ProduceDistinctResults()
    {
        // 1000 distinct short strings should hash without collision under a
        // good 32-bit mixer. The hash is deterministic, so a pass here is a
        // pass everywhere.
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
    public void StringMurmur3Hasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<string, int, StringMurmur3Hasher>();

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
    public void StringMurmur3Hasher_HandlesNullKeyOutOfBand()
    {
        // The dictionary routes the null/default key around the hasher, so a
        // null key must round-trip without the hasher's null guard firing.
        var dict = new CelerityDictionary<string, int, StringMurmur3Hasher>();

        dict[null!] = 42;
        dict["present"] = 1;

        Assert.Equal(2, dict.Count);
        Assert.True(dict.ContainsKey(null!));
        Assert.Equal(42, dict[null!]);
    }

    [Fact]
    public void StringMurmur3Hasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<string, StringMurmur3Hasher>();

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
