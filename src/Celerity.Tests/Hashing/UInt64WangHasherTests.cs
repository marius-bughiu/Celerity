using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class UInt64WangHasherTests
{
    private readonly UInt64WangHasher _hasher = new UInt64WangHasher();

    // ── Exact-value anchors ───────────────────────────────────────────────────
    //
    // The mixer is computed on the ulong directly, so for any given 64-bit
    // pattern these results are identical to Int64WangHasher's (which runs the
    // same hash64shift over the (ulong)key reinterpretation). The anchors below
    // match the corresponding Int64WangHasherTests bit patterns exactly,
    // pinning the round order / shift amounts / final `u += u << 31` step
    // against drift.

    [Theory]
    [InlineData(0UL,                              -266614128)]
    [InlineData(1UL,                             -1214973746)]
    [InlineData(ulong.MaxValue,                   1066321812)]   // long -1 bit pattern
    [InlineData(42UL,                              511405946)]
    [InlineData(0x7FFFFFFFFFFFFFFFUL,            -2087131081)]   // long.MaxValue bit pattern
    [InlineData(0x8000000000000000UL,             2014176584)]   // long.MinValue bit pattern
    [InlineData(1234567890123456789UL,           -1485153766)]
    public void Hash_ReturnsExpected(ulong input, int expected)
    {
        Assert.Equal(expected, _hasher.Hash(input));
    }

    // ── Bit-pattern equivalence with Int64WangHasher ──────────────────────────

    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(42UL)]
    [InlineData(1234567890123456789UL)]
    [InlineData(0x7FFFFFFFFFFFFFFFUL)]
    [InlineData(0x8000000000000000UL)]
    [InlineData(0xDEADBEEFCAFEBABEUL)]
    [InlineData(ulong.MaxValue)]
    public void Hash_MatchesInt64WangHasher_ForSameBitPattern(ulong input)
    {
        // hash64shift runs over the ulong reinterpretation in both hashers, so
        // the ulong and long variants must agree bit-for-bit on the same
        // 64-bit pattern.
        var longHasher = new Int64WangHasher();
        Assert.Equal(longHasher.Hash(unchecked((long)input)), _hasher.Hash(input));
    }

    // ── Does not map zero to zero ─────────────────────────────────────────────

    [Fact]
    public void Hash_Zero_IsNotZero()
    {
        // Unlike the Murmur3 finalizer, the full Wang mixer has no 0 → 0 fixed
        // point. The dictionaries route the zero key out-of-band so this does
        // not affect the empty-slot sentinel, but it is a documented property.
        Assert.NotEqual(0, _hasher.Hash(0UL));
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
        int a = new UInt64WangHasher().Hash(value);
        int b = new UInt64WangHasher().Hash(value);
        Assert.Equal(a, b);
    }

    // ── Avalanche ─────────────────────────────────────────────────────────────

    [Fact]
    public void Hash_HighBits_InfluenceResult()
    {
        // Flip a single bit in the top half of the ulong; the hash must change.
        // Guards against a regression where high-half bits are discarded before
        // truncation to 32 bits.
        int low  = _hasher.Hash(1UL);
        int high = _hasher.Hash(1UL | (1UL << 48));
        Assert.NotEqual(low, high);
    }

    [Fact]
    public void Hash_ConsecutiveInputs_ProduceDistinctResults()
    {
        // The Wang 64-bit hash is invertible (bijective) on the full 64-bit
        // space, so its lower 32 bits form a near-perfect hash on small
        // contiguous ranges. A collision here would indicate a broken mixer.
        var seen = new HashSet<int>();
        for (ulong i = 0; i < 1000; i++)
        {
            Assert.True(seen.Add(_hasher.Hash(i)),
                $"Unexpected collision at input {i}.");
        }
    }

    // ── Does not throw ────────────────────────────────────────────────────────

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
    public void UInt64WangHasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<ulong, string, UInt64WangHasher>();

        dict[0UL]              = "zero";        // default(ulong) — out-of-band slot
        dict[1UL]              = "one";
        dict[42UL]             = "forty-two";
        dict[ulong.MaxValue]   = "max";         // beyond long.MaxValue

        Assert.Equal(4, dict.Count);
        Assert.Equal("zero",      dict[0UL]);
        Assert.Equal("one",       dict[1UL]);
        Assert.Equal("forty-two", dict[42UL]);
        Assert.Equal("max",       dict[ulong.MaxValue]);
        Assert.True(dict.ContainsKey(0UL));
        Assert.False(dict.ContainsKey(999UL));
    }

    [Fact]
    public void UInt64WangHasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<ulong, UInt64WangHasher>();

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
}
