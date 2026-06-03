using System.Buffers.Binary;
using System.Text;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class StringXxHash32HasherTests
{
    private readonly StringXxHash32Hasher _hasher = new StringXxHash32Hasher();

    private const uint Prime1 = 2654435761u;
    private const uint Prime2 = 2246822519u;
    private const uint Prime3 = 3266489917u;
    private const uint Prime4 = 668265263u;
    private const uint Prime5 = 374761393u;

    /// <summary>
    /// Independent, byte-oriented reference implementation of canonical xxHash32
    /// (seed <c>0</c>) over the BCL's little-endian UTF-16 encoding of the string.
    /// The hasher loops over <c>char</c>s and assembles lanes by hand; this
    /// reference goes through <see cref="Encoding.Unicode"/> and a byte loop with
    /// <see cref="BinaryPrimitives.ReadUInt32LittleEndian"/>, so agreement pins the
    /// byte order, the stripe / lane boundaries, the tail handling, and the
    /// finalizer against drift without sharing the hasher's implementation.
    /// </summary>
    private static int ReferenceXxHash32Utf16(string s) =>
        unchecked((int)XxHash32(Encoding.Unicode.GetBytes(s)));

    private static uint XxHash32(byte[] data)
    {
        int len = data.Length;
        int i = 0;
        uint h32;

        if (len >= 16)
        {
            uint v1 = unchecked(Prime1 + Prime2);
            uint v2 = Prime2;
            uint v3 = 0u;
            uint v4 = unchecked(0u - Prime1);

            int limit = len - 16;
            do
            {
                v1 = Round(v1, BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(i))); i += 4;
                v2 = Round(v2, BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(i))); i += 4;
                v3 = Round(v3, BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(i))); i += 4;
                v4 = Round(v4, BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(i))); i += 4;
            }
            while (i <= limit);

            h32 = RotL(v1, 1) + RotL(v2, 7) + RotL(v3, 12) + RotL(v4, 18);
        }
        else
        {
            h32 = Prime5;
        }

        h32 += (uint)len;

        while (i + 4 <= len)
        {
            h32 += BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(i)) * Prime3;
            h32 = RotL(h32, 17) * Prime4;
            i += 4;
        }

        while (i < len)
        {
            h32 += data[i] * Prime5;
            h32 = RotL(h32, 11) * Prime1;
            i++;
        }

        h32 ^= h32 >> 15;
        h32 *= Prime2;
        h32 ^= h32 >> 13;
        h32 *= Prime3;
        h32 ^= h32 >> 16;
        return h32;
    }

    private static uint Round(uint acc, uint lane) =>
        RotL(acc + (lane * Prime2), 13) * Prime1;

    private static uint RotL(uint value, int count) =>
        (value << count) | (value >> (32 - count));

    // ── External canonical anchor ─────────────────────────────────────────────
    // XXH32 of zero bytes with seed 0 is the well-known published vector
    // 0x02CC5D05. The empty string's UTF-16 byte stream is also zero bytes, so the
    // hasher must reproduce that constant — an anchor independent of both the
    // hasher and the byte-oriented reference below.

    [Fact]
    public void Hash_EmptyString_ReturnsCanonicalXxHash32OfEmptyInput()
    {
        Assert.Equal(unchecked((int)0x02CC5D05u), _hasher.Hash(""));
    }

    [Fact]
    public void Reference_EmptyInput_MatchesCanonicalVector()
    {
        // Pins the reference helper itself against the published constant, so the
        // spec-equivalence theory below is anchored to an external source of truth.
        Assert.Equal(0x02CC5D05u, XxHash32(Array.Empty<byte>()));
    }

    // ── Spec equivalence (canonical XXH32 over the UTF-16 byte stream) ─────────

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("abc")]
    [InlineData("abcd")]                 // exactly one 4-byte lane after the prefix
    [InlineData("hello")]
    [InlineData("hello world")]
    [InlineData("the quick brown fox")]  // long enough to exercise the 16-byte stripe loop
    [InlineData("HELLO")]
    [InlineData("Ł")]                  // U+0141 — non-ASCII, high byte set
    [InlineData("Łatin")]              // mixed ASCII / non-ASCII
    [InlineData("日本語")]            // CJK, all high bytes set
    [InlineData("a\0b")]              // embedded NUL char (folds two zero bytes)
    [InlineData("😀")]                 // surrogate pair (two UTF-16 code units)
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaa")] // 24 chars → multiple full stripes + lanes
    public void Hash_MatchesCanonicalXxHash32OverUtf16ByteStream(string input)
    {
        Assert.Equal(ReferenceXxHash32Utf16(input), _hasher.Hash(input));
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
        int a = new StringXxHash32Hasher().Hash(value);
        int b = new StringXxHash32Hasher().Hash(value);
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
        // single-character strings; StringXxHash32Hasher consumes the full 16-bit
        // character and must keep them distinct.
        var lowByte = new StringFnV1AHasher();
        Assert.Equal(lowByte.Hash("A"), lowByte.Hash("Ł"));
        Assert.NotEqual(_hasher.Hash("A"), _hasher.Hash("Ł"));
    }

    [Fact]
    public void Hash_DistinguishesMultibyteStringsThatLowByteFnv1aCollides()
    {
        // Two strings whose characters pairwise share low bytes but differ in
        // upper bytes: the low-byte FNV-1a collides, the full-width XXH32 does not.
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
    public void StringXxHash32Hasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<string, int, StringXxHash32Hasher>();

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
    public void StringXxHash32Hasher_KeepsUpperByteDistinctKeysSeparate()
    {
        // The dictionary must treat 'A' and 'Ł' as distinct keys — the full-width
        // fold on the probe path.
        var dict = new CelerityDictionary<string, int, StringXxHash32Hasher>();

        dict["A"] = 1;
        dict["Ł"] = 2;

        Assert.Equal(2, dict.Count);
        Assert.Equal(1, dict["A"]);
        Assert.Equal(2, dict["Ł"]);
    }

    [Fact]
    public void StringXxHash32Hasher_HandlesNullKeyOutOfBand()
    {
        // The dictionary routes the null/default key around the hasher, so a null
        // key must round-trip without the hasher's null guard firing.
        var dict = new CelerityDictionary<string, int, StringXxHash32Hasher>();

        dict[null!] = 42;
        dict["present"] = 1;

        Assert.Equal(2, dict.Count);
        Assert.True(dict.ContainsKey(null!));
        Assert.Equal(42, dict[null!]);
    }

    [Fact]
    public void StringXxHash32Hasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<string, StringXxHash32Hasher>();

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
