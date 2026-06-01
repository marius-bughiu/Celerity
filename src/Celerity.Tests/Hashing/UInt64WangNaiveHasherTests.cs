using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class UInt64WangNaiveHasherTests
{
    private readonly UInt64WangNaiveHasher _hasher = new UInt64WangNaiveHasher();

    // The naive hasher folds three 32-bit windows of the key into one int.
    // The formula is short enough to act as the spec, so the theory cases
    // compute the expected value inline rather than hard-coding anchors.
    private static int Expected(ulong key) =>
        (int)key ^ (int)(key >> 32) ^ (int)(key >> 16);

    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(42UL)]
    [InlineData(ulong.MaxValue)]
    [InlineData(0x7FFFFFFFFFFFFFFFUL)]   // long.MaxValue bit pattern
    [InlineData(0x8000000000000000UL)]   // long.MinValue bit pattern
    [InlineData(1234567890123456789UL)]
    [InlineData(0x0000FFFFFFFFFFFFUL)]
    [InlineData(0xDEADBEEFCAFEBABEUL)]
    public void Hash_MatchesFormula(ulong input)
    {
        Assert.Equal(Expected(input), _hasher.Hash(input));
    }

    // ── Bit-pattern equivalence with Int64WangNaiveHasher ─────────────────────

    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(42UL)]
    [InlineData(1234567890123456789UL)]
    [InlineData(0x7FFFFFFFFFFFFFFFUL)]
    [InlineData(0x8000000000000000UL)]
    [InlineData(0xDEADBEEFCAFEBABEUL)]
    [InlineData(ulong.MaxValue)]
    public void Hash_MatchesInt64WangNaiveHasher_ForSameBitPattern(ulong input)
    {
        // The XOR-fold runs over the raw bits in both hashers, so the ulong and
        // long variants must agree bit-for-bit on the same 64-bit pattern.
        var longHasher = new Int64WangNaiveHasher();
        Assert.Equal(longHasher.Hash(unchecked((long)input)), _hasher.Hash(input));
    }

    // ── Zero maps to zero ─────────────────────────────────────────────────────

    [Fact]
    public void Hash_Zero_IsZero()
    {
        // The XOR-fold has a 0 → 0 fixed point. The dictionaries route the zero
        // key out-of-band so this does not affect the empty-slot sentinel, but
        // it is a documented property.
        Assert.Equal(0, _hasher.Hash(0UL));
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void Hash_IsDeterministic_AcrossCalls()
    {
        ulong value = 1234567890123456789UL;
        int result1 = _hasher.Hash(value);
        int result2 = _hasher.Hash(value);
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Hash_IsDeterministic_AcrossInstances()
    {
        // Hashers are structs with no state; two independently-constructed
        // instances must produce identical output for the same input.
        ulong value = 0xDEADBEEFCAFEBABEUL;
        int a = new UInt64WangNaiveHasher().Hash(value);
        int b = new UInt64WangNaiveHasher().Hash(value);
        Assert.Equal(a, b);
    }

    // ── Avalanche / window coverage ───────────────────────────────────────────

    [Fact]
    public void Hash_HighBits_InfluenceResult()
    {
        // A regression guard against any future change that accidentally
        // discards the upper 32 bits. Flip a single bit in the top half and
        // assert the hash moves with it.
        int low = _hasher.Hash(1UL);
        int high = _hasher.Hash(1UL | (1UL << 48));
        Assert.NotEqual(low, high);
    }

    [Fact]
    public void Hash_MidBits_InfluenceResult()
    {
        // The naive hasher folds the >>16 window in to keep bits 16-47 alive.
        // A version that only XORed (int)key with (int)(key >> 32) would lose
        // the entire byte at position [2..6]; this test pins that down.
        int baseline = _hasher.Hash(0UL);
        int withMidBit = _hasher.Hash(1UL << 20);
        Assert.NotEqual(baseline, withMidBit);
    }

    [Fact]
    public void Hash_ConsecutiveSmallInputs_AreDistinct()
    {
        // For sequential keys 0..999 the low 32 bits dominate, so the result
        // is just (int)key in that range — no collisions expected.
        var seen = new HashSet<int>();
        for (ulong i = 0; i < 1000; i++)
        {
            Assert.True(seen.Add(_hasher.Hash(i)),
                $"Unexpected collision at input {i}.");
        }
    }

    [Fact]
    public void Hash_BitStridedInputs_AreDistinct()
    {
        // Keys constructed by setting a single high-half bit at a time. The
        // naive hasher must spread these across distinct buckets — a hasher
        // that ignored the upper half would map every one of these to 0.
        var seen = new HashSet<int>();
        for (int bit = 32; bit < 64; bit++)
        {
            ulong key = 1UL << bit;
            Assert.True(seen.Add(_hasher.Hash(key)),
                $"Collision at bit {bit} (key = 0x{key:X16}).");
        }
    }

    [Fact]
    public void Hash_DoesNotThrow()
    {
        ulong[] testValues =
        {
            0UL, 1UL, ulong.MaxValue,
            0x7FFFFFFFFFFFFFFFUL, 0x8000000000000000UL,
            1234567890123456789UL, 0xDEADBEEFCAFEBABEUL
        };

        foreach (ulong val in testValues)
        {
            var ex = Record.Exception(() => _hasher.Hash(val));
            Assert.Null(ex);
        }
    }

    // ── Integration: satisfies the hasher constraint on collections ──────────

    [Fact]
    public void UInt64WangNaiveHasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<ulong, string, UInt64WangNaiveHasher>();

        dict[0UL]            = "zero";        // default(ulong) — out-of-band slot
        dict[1UL]            = "one";
        dict[42UL]           = "forty-two";
        dict[ulong.MaxValue] = "max";         // beyond long.MaxValue

        Assert.Equal(4, dict.Count);
        Assert.Equal("zero",      dict[0UL]);
        Assert.Equal("one",       dict[1UL]);
        Assert.Equal("forty-two", dict[42UL]);
        Assert.Equal("max",       dict[ulong.MaxValue]);
        Assert.True(dict.ContainsKey(0UL));
        Assert.False(dict.ContainsKey(999UL));
    }

    [Fact]
    public void UInt64WangNaiveHasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<ulong, UInt64WangNaiveHasher>();

        set.Add(0UL);                // default(ulong) — out-of-band slot
        set.Add(1UL);
        set.Add(42UL);
        set.Add(ulong.MaxValue);

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(0UL));
        Assert.True(set.Contains(1UL));
        Assert.True(set.Contains(42UL));
        Assert.True(set.Contains(ulong.MaxValue));
        Assert.False(set.Contains(999UL));
    }

    [Fact]
    public void UInt64WangNaiveHasher_KeepsUpperHalfDistinct_UnderCollidingLowerHalf()
    {
        // Two keys sharing the same lower 32 bits but differing in the upper 32
        // must remain distinct entries — guards against any accidental
        // int-truncation that would drop the high half on the probe path.
        var dict = new CelerityDictionary<ulong, string, UInt64WangNaiveHasher>();

        ulong a = 0x0000000100000001UL;
        ulong b = 0x0000000200000001UL;   // same low 32 bits as a, different high

        dict[a] = "a";
        dict[b] = "b";

        Assert.Equal(2, dict.Count);
        Assert.Equal("a", dict[a]);
        Assert.Equal("b", dict[b]);
    }
}
