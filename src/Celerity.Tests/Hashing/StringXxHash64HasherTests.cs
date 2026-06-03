using System.Buffers.Binary;
using System.Text;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class StringXxHash64HasherTests
{
    private readonly StringXxHash64Hasher _hasher = new StringXxHash64Hasher();

    private const ulong Prime1 = 11400714785074694791UL;
    private const ulong Prime2 = 14029467366897019727UL;
    private const ulong Prime3 = 1609587929392839161UL;
    private const ulong Prime4 = 9650029242287828579UL;
    private const ulong Prime5 = 2870177450012600261UL;

    // The well-known canonical XXH64 vector for an empty input with seed 0.
    private const ulong CanonicalEmptyXxHash64 = 0xEF46DB3751D8E999UL;

    /// <summary>
    /// Independent, byte-oriented reference implementation of canonical xxHash64
    /// (seed <c>0</c>) over the BCL's little-endian UTF-16 encoding of the string,
    /// xor-folded to 32 bits exactly as the hasher does. The hasher loops over
    /// <c>char</c>s and assembles lanes by hand; this reference goes through
    /// <see cref="Encoding.Unicode"/> and a byte loop with
    /// <see cref="BinaryPrimitives.ReadUInt64LittleEndian"/> /
    /// <see cref="BinaryPrimitives.ReadUInt32LittleEndian"/>, so agreement pins the
    /// byte order, the stripe / lane boundaries, the tail handling, the merge
    /// rounds, the avalanche, and the final fold against drift without sharing the
    /// hasher's implementation.
    /// </summary>
    private static int ReferenceXxHash64Utf16(string s)
    {
        ulong h = XxHash64(Encoding.Unicode.GetBytes(s));
        return unchecked((int)(h ^ (h >> 32)));
    }

    private static ulong XxHash64(byte[] data)
    {
        int len = data.Length;
        int i = 0;
        ulong h64;

        if (len >= 32)
        {
            ulong v1 = unchecked(Prime1 + Prime2);
            ulong v2 = Prime2;
            ulong v3 = 0UL;
            ulong v4 = unchecked(0UL - Prime1);

            int limit = len - 32;
            do
            {
                v1 = Round(v1, BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(i))); i += 8;
                v2 = Round(v2, BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(i))); i += 8;
                v3 = Round(v3, BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(i))); i += 8;
                v4 = Round(v4, BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(i))); i += 8;
            }
            while (i <= limit);

            h64 = RotL(v1, 1) + RotL(v2, 7) + RotL(v3, 12) + RotL(v4, 18);
            h64 = MergeRound(h64, v1);
            h64 = MergeRound(h64, v2);
            h64 = MergeRound(h64, v3);
            h64 = MergeRound(h64, v4);
        }
        else
        {
            h64 = Prime5;
        }

        h64 += (ulong)len;

        while (i + 8 <= len)
        {
            ulong k1 = Round(0UL, BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(i)));
            h64 ^= k1;
            h64 = RotL(h64, 27) * Prime1 + Prime4;
            i += 8;
        }

        if (i + 4 <= len)
        {
            h64 ^= (ulong)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(i)) * Prime1;
            h64 = RotL(h64, 23) * Prime2 + Prime3;
            i += 4;
        }

        while (i < len)
        {
            h64 ^= data[i] * Prime5;
            h64 = RotL(h64, 11) * Prime1;
            i++;
        }

        h64 ^= h64 >> 33;
        h64 *= Prime2;
        h64 ^= h64 >> 29;
        h64 *= Prime3;
        h64 ^= h64 >> 32;
        return h64;
    }

    private static ulong Round(ulong acc, ulong lane) =>
        RotL(acc + (lane * Prime2), 31) * Prime1;

    private static ulong MergeRound(ulong acc, ulong val)
    {
        val = Round(0UL, val);
        acc ^= val;
        return acc * Prime1 + Prime4;
    }

    private static ulong RotL(ulong value, int count) =>
        (value << count) | (value >> (64 - count));

    // ── External canonical anchor ─────────────────────────────────────────────
    // XXH64 of zero bytes with seed 0 is the well-known published vector
    // 0xEF46DB3751D8E999. The empty string's UTF-16 byte stream is also zero bytes,
    // so the hasher (after the xor-fold) must reproduce that constant folded to 32
    // bits — an anchor independent of both the hasher and the byte-oriented
    // reference below. Expressed symbolically so it pins the fold direction without
    // a hand-computed constant.

    [Fact]
    public void Hash_EmptyString_ReturnsCanonicalXxHash64OfEmptyInputFolded()
    {
        int expected = unchecked((int)(CanonicalEmptyXxHash64 ^ (CanonicalEmptyXxHash64 >> 32)));
        Assert.Equal(expected, _hasher.Hash(""));
    }

    [Fact]
    public void Reference_EmptyInput_MatchesCanonicalVector()
    {
        // Pins the reference helper itself against the published constant, so the
        // spec-equivalence theory below is anchored to an external source of truth.
        Assert.Equal(CanonicalEmptyXxHash64, XxHash64(Array.Empty<byte>()));
    }

    // ── Spec equivalence (canonical XXH64 over the UTF-16 byte stream, folded) ──

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("abc")]
    [InlineData("abcd")]                 // exactly one 8-byte lane after the prefix
    [InlineData("abcde")]                // one 8-byte lane + a trailing char
    [InlineData("abcdef")]               // one 8-byte lane + a 4-byte lane
    [InlineData("hello")]
    [InlineData("hello world")]
    [InlineData("the quick brown fox jumps")] // long enough to exercise the 32-byte stripe loop
    [InlineData("HELLO")]
    [InlineData("Ł")]                  // U+0141 — non-ASCII, high byte set
    [InlineData("Łatin")]              // mixed ASCII / non-ASCII
    [InlineData("日本語")]            // CJK, all high bytes set
    [InlineData("a\0b")]              // embedded NUL char (folds two zero bytes)
    [InlineData("😀")]                 // surrogate pair (two UTF-16 code units)
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // 36 chars → multiple full stripes + lanes
    public void Hash_MatchesCanonicalXxHash64OverUtf16ByteStream(string input)
    {
        Assert.Equal(ReferenceXxHash64Utf16(input), _hasher.Hash(input));
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
        int a = new StringXxHash64Hasher().Hash(value);
        int b = new StringXxHash64Hasher().Hash(value);
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
        // single-character strings; StringXxHash64Hasher consumes the full 16-bit
        // character and must keep them distinct.
        var lowByte = new StringFnV1AHasher();
        Assert.Equal(lowByte.Hash("A"), lowByte.Hash("Ł"));
        Assert.NotEqual(_hasher.Hash("A"), _hasher.Hash("Ł"));
    }

    [Fact]
    public void Hash_DistinguishesMultibyteStringsThatLowByteFnv1aCollides()
    {
        // Two strings whose characters pairwise share low bytes but differ in
        // upper bytes: the low-byte FNV-1a collides, the full-width XXH64 does not.
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
    public void StringXxHash64Hasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<string, int, StringXxHash64Hasher>();

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
    public void StringXxHash64Hasher_KeepsUpperByteDistinctKeysSeparate()
    {
        // The dictionary must treat 'A' and 'Ł' as distinct keys — the full-width
        // fold on the probe path.
        var dict = new CelerityDictionary<string, int, StringXxHash64Hasher>();

        dict["A"] = 1;
        dict["Ł"] = 2;

        Assert.Equal(2, dict.Count);
        Assert.Equal(1, dict["A"]);
        Assert.Equal(2, dict["Ł"]);
    }

    [Fact]
    public void StringXxHash64Hasher_HandlesNullKeyOutOfBand()
    {
        // The dictionary routes the null/default key around the hasher, so a null
        // key must round-trip without the hasher's null guard firing.
        var dict = new CelerityDictionary<string, int, StringXxHash64Hasher>();

        dict[null!] = 42;
        dict["present"] = 1;

        Assert.Equal(2, dict.Count);
        Assert.True(dict.ContainsKey(null!));
        Assert.Equal(42, dict[null!]);
    }

    [Fact]
    public void StringXxHash64Hasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<string, StringXxHash64Hasher>();

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
