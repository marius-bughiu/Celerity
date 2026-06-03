using System.Buffers.Binary;
using System.Text;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class StringCityHash64HasherTests
{
    private readonly StringCityHash64Hasher _hasher = new StringCityHash64Hasher();

    // CityHash v1.1 mixing constants (Geoff Pike & Jyrki Alakuijala, public domain).
    private const ulong K0 = 0xC3A5C85C97CB3127UL;
    private const ulong K1 = 0xB492B66FBE98F273UL;
    private const ulong K2 = 0x9AE16A3B2F90404FUL;
    private const ulong KMul = 0x9DDFEA08EB382D69UL;

    /// <summary>
    /// Independent, byte-oriented reference implementation of canonical CityHash64
    /// (v1.1) over the BCL's little-endian UTF-16 encoding of the string, xor-folded
    /// to 32 bits exactly as the hasher does. The hasher loops over <c>char</c>s and
    /// assembles 64-bit words by hand; this reference goes through
    /// <see cref="Encoding.Unicode"/> and reads words with
    /// <see cref="BinaryPrimitives.ReadUInt64LittleEndian"/> /
    /// <see cref="BinaryPrimitives.ReadUInt32LittleEndian"/> against raw byte
    /// offsets, so agreement pins the word order, the four length-class dispatch
    /// boundaries, the end-relative offsets, the 64-byte main loop, the
    /// finalization, and the final fold against drift without sharing the hasher's
    /// char-lane implementation.
    /// </summary>
    private static int ReferenceCityHash64Utf16(string s)
    {
        ulong h = CityHash64(Encoding.Unicode.GetBytes(s));
        return unchecked((int)(h ^ (h >> 32)));
    }

    private static ulong Fetch64(byte[] d, int i) => BinaryPrimitives.ReadUInt64LittleEndian(d.AsSpan(i));

    private static ulong Fetch32(byte[] d, int i) => BinaryPrimitives.ReadUInt32LittleEndian(d.AsSpan(i));

    private static ulong Rotate(ulong val, int shift) =>
        shift == 0 ? val : (val >> shift) | (val << (64 - shift));

    private static ulong ShiftMix(ulong val) => val ^ (val >> 47);

    private static ulong HashLen16(ulong u, ulong v, ulong mul)
    {
        ulong a = (u ^ v) * mul;
        a ^= a >> 47;
        ulong b = (v ^ a) * mul;
        b ^= b >> 47;
        b *= mul;
        return b;
    }

    private static ulong HashLen16(ulong u, ulong v) => HashLen16(u, v, KMul);

    private static ulong HashLen0to16(byte[] d, int off, int len)
    {
        if (len >= 8)
        {
            ulong mul = K2 + (ulong)len * 2UL;
            ulong a = Fetch64(d, off) + K2;
            ulong b = Fetch64(d, off + len - 8);
            ulong c = Rotate(b, 37) * mul + a;
            ulong dd = (Rotate(a, 25) + b) * mul;
            return HashLen16(c, dd, mul);
        }
        if (len >= 4)
        {
            ulong mul = K2 + (ulong)len * 2UL;
            ulong a = Fetch32(d, off);
            return HashLen16((ulong)len + (a << 3), Fetch32(d, off + len - 4), mul);
        }
        if (len > 0)
        {
            uint a = d[off];
            uint b = d[off + (len >> 1)];
            uint c = d[off + len - 1];
            uint y = a + (b << 8);
            uint z = (uint)len + (c << 2);
            return ShiftMix((ulong)y * K2 ^ (ulong)z * K0) * K2;
        }
        return K2;
    }

    private static ulong HashLen17to32(byte[] d, int off, int len)
    {
        ulong mul = K2 + (ulong)len * 2UL;
        ulong a = Fetch64(d, off) * K1;
        ulong b = Fetch64(d, off + 8);
        ulong c = Fetch64(d, off + len - 8) * mul;
        ulong dd = Fetch64(d, off + len - 16) * K2;
        return HashLen16(Rotate(a + b, 43) + Rotate(c, 30) + dd,
                         a + Rotate(b + K2, 18) + c, mul);
    }

    private static (ulong, ulong) WeakHashLen32WithSeeds(
        ulong w, ulong x, ulong y, ulong z, ulong a, ulong b)
    {
        a += w;
        b = Rotate(b + a + z, 21);
        ulong c = a;
        a += x;
        a += y;
        b += Rotate(a, 44);
        return (a + z, b + c);
    }

    private static (ulong, ulong) WeakHashLen32WithSeeds(byte[] d, int off, ulong a, ulong b) =>
        WeakHashLen32WithSeeds(Fetch64(d, off), Fetch64(d, off + 8),
                               Fetch64(d, off + 16), Fetch64(d, off + 24), a, b);

    private static ulong HashLen33to64(byte[] d, int off, int len)
    {
        ulong mul = K2 + (ulong)len * 2UL;
        ulong a = Fetch64(d, off) * K2;
        ulong b = Fetch64(d, off + 8);
        ulong c = Fetch64(d, off + len - 24);
        ulong dd = Fetch64(d, off + len - 32);
        ulong e = Fetch64(d, off + 16) * K2;
        ulong f = Fetch64(d, off + 24) * 9UL;
        ulong g = Fetch64(d, off + len - 8);
        ulong h = Fetch64(d, off + len - 16) * mul;

        ulong u = Rotate(a + g, 43) + (Rotate(b, 30) + c) * 9UL;
        ulong v = ((a + g) ^ dd) + f + 1UL;
        ulong w = BinaryPrimitives.ReverseEndianness((u + v) * mul) + h;
        ulong x = Rotate(e + f, 42) + c;
        ulong y = (BinaryPrimitives.ReverseEndianness((v + w) * mul) + g) * mul;
        ulong z = e + f + c;
        a = BinaryPrimitives.ReverseEndianness((x + z) * mul + y) + b;
        b = ShiftMix((z + a) * mul + dd + h) * mul;
        return b + x;
    }

    private static ulong CityHash64(byte[] d)
    {
        int len = d.Length;
        if (len <= 32)
            return len <= 16 ? HashLen0to16(d, 0, len) : HashLen17to32(d, 0, len);
        if (len <= 64)
            return HashLen33to64(d, 0, len);

        int s = 0;
        ulong x = Fetch64(d, s + len - 40);
        ulong y = Fetch64(d, s + len - 16) + Fetch64(d, s + len - 56);
        ulong z = HashLen16(Fetch64(d, s + len - 48) + (ulong)len, Fetch64(d, s + len - 24));
        (ulong vFirst, ulong vSecond) = WeakHashLen32WithSeeds(d, s + len - 64, (ulong)len, z);
        (ulong wFirst, ulong wSecond) = WeakHashLen32WithSeeds(d, s + len - 32, y + K1, x);
        x = x * K1 + Fetch64(d, s);

        int remaining = (len - 1) & ~63;
        do
        {
            x = Rotate(x + y + vFirst + Fetch64(d, s + 8), 37) * K1;
            y = Rotate(y + vSecond + Fetch64(d, s + 48), 42) * K1;
            x ^= wSecond;
            y += vFirst + Fetch64(d, s + 40);
            z = Rotate(z + wFirst, 33) * K1;
            (vFirst, vSecond) = WeakHashLen32WithSeeds(d, s, vSecond * K1, x + wFirst);
            (wFirst, wSecond) = WeakHashLen32WithSeeds(d, s + 32, z + wSecond, y + Fetch64(d, s + 16));
            (z, x) = (x, z);
            s += 64;
            remaining -= 64;
        }
        while (remaining != 0);

        return HashLen16(HashLen16(vFirst, wFirst) + ShiftMix(y) * K1 + z,
                         HashLen16(vSecond, wSecond) + x);
    }

    // ── Spec anchor (length-0 result) ─────────────────────────────────────────
    // CityHash64 of an empty input returns the constant k2: no length class beyond
    // "len == 0" applies. The empty string's UTF-16 byte stream is also zero bytes,
    // so the hasher (after the xor-fold) must reproduce k2 folded to 32 bits.

    [Fact]
    public void Hash_EmptyString_ReturnsK2Folded()
    {
        int expected = unchecked((int)(K2 ^ (K2 >> 32)));
        Assert.Equal(expected, _hasher.Hash(""));
    }

    [Fact]
    public void Reference_EmptyInput_ReturnsK2()
    {
        // Pins the reference helper itself against the spec-defined length-0 value,
        // so the spec-equivalence theory below is anchored independently of the
        // hasher's char-loop.
        Assert.Equal(K2, CityHash64(Array.Empty<byte>()));
    }

    // ── Spec equivalence (canonical CityHash64 over the UTF-16 byte stream) ──────

    [Theory]
    [InlineData("")]                     // 0 bytes → k2
    [InlineData("a")]                    // 2 bytes → single-char tail path
    [InlineData("ab")]                   // 4 bytes → 4-byte path
    [InlineData("abc")]                  // 6 bytes → 4-byte path (overlapping reads)
    [InlineData("abcd")]                 // 8 bytes → 8-byte path
    [InlineData("abcde")]                // 10 bytes
    [InlineData("abcdefgh")]             // 16 bytes → top of HashLen0to16
    [InlineData("abcdefghi")]            // 18 bytes → HashLen17to32
    [InlineData("0123456789012345")]     // 32 bytes → top of HashLen17to32
    [InlineData("01234567890123456")]    // 34 bytes → HashLen33to64
    [InlineData("the quick brown fox jumps over t")]   // 64 bytes → top of HashLen33to64
    [InlineData("the quick brown fox jumps over th")]  // 66 bytes → main loop, one chunk
    [InlineData("hello")]
    [InlineData("hello world")]
    [InlineData("the quick brown fox jumps over the lazy dog")]
    [InlineData("the quick brown fox jumps over the lazy dog, and then jumps back again twice")] // > 128 bytes → multiple chunks
    [InlineData("HELLO")]
    [InlineData("Ł")]                  // U+0141 — non-ASCII, high byte set
    [InlineData("Łatin")]              // mixed ASCII / non-ASCII
    [InlineData("日本語")]            // CJK, all high bytes set
    [InlineData("a\0b")]              // embedded NUL char (folds two zero bytes)
    [InlineData("😀")]                 // surrogate pair (two UTF-16 code units)
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // 98 bytes → main loop + tail end
    public void Hash_MatchesCanonicalCityHash64OverUtf16ByteStream(string input)
    {
        Assert.Equal(ReferenceCityHash64Utf16(input), _hasher.Hash(input));
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
        int a = new StringCityHash64Hasher().Hash(value);
        int b = new StringCityHash64Hasher().Hash(value);
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
        // single-character strings; StringCityHash64Hasher consumes the full 16-bit
        // character and must keep them distinct.
        var lowByte = new StringFnV1AHasher();
        Assert.Equal(lowByte.Hash("A"), lowByte.Hash("Ł"));
        Assert.NotEqual(_hasher.Hash("A"), _hasher.Hash("Ł"));
    }

    [Fact]
    public void Hash_DistinguishesMultibyteStringsThatLowByteFnv1aCollides()
    {
        // Two strings whose characters pairwise share low bytes but differ in
        // upper bytes: the low-byte FNV-1a collides, the full-width CityHash does not.
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
    public void StringCityHash64Hasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<string, int, StringCityHash64Hasher>();

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
    public void StringCityHash64Hasher_KeepsUpperByteDistinctKeysSeparate()
    {
        // The dictionary must treat 'A' and 'Ł' as distinct keys — the full-width
        // fold on the probe path.
        var dict = new CelerityDictionary<string, int, StringCityHash64Hasher>();

        dict["A"] = 1;
        dict["Ł"] = 2;

        Assert.Equal(2, dict.Count);
        Assert.Equal(1, dict["A"]);
        Assert.Equal(2, dict["Ł"]);
    }

    [Fact]
    public void StringCityHash64Hasher_HandlesNullKeyOutOfBand()
    {
        // The dictionary routes the null/default key around the hasher, so a null
        // key must round-trip without the hasher's null guard firing.
        var dict = new CelerityDictionary<string, int, StringCityHash64Hasher>();

        dict[null!] = 42;
        dict["present"] = 1;

        Assert.Equal(2, dict.Count);
        Assert.True(dict.ContainsKey(null!));
        Assert.Equal(42, dict[null!]);
    }

    [Fact]
    public void StringCityHash64Hasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<string, StringCityHash64Hasher>();

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
