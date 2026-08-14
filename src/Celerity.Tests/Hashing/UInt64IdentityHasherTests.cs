using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class UInt64IdentityHasherTests
{
    private readonly UInt64IdentityHasher _hasher = new UInt64IdentityHasher();

    // The identity hasher returns the key's low 32 bits unchanged.
    private static int Expected(ulong key) => unchecked((int)key);

    // ── Pass-through of the low 32 bits ───────────────────────────────────────

    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(42UL)]
    [InlineData(ulong.MaxValue)]
    [InlineData(0x8000_0000_0000_0000UL)]
    [InlineData(0x0000_FFFF_FFFF_FFFFUL)]
    [InlineData(0x1234_5678_9ABC_DEF0UL)]
    [InlineData(1234567890123456789UL)]
    public void Hash_ShouldReturnTheLow32Bits_WhenGivenAnyKey(ulong input)
    {
        Assert.Equal(Expected(input), _hasher.Hash(input));
    }

    [Fact]
    public void Hash_ShouldEqualTheKey_WhenItFitsInAnInt()
    {
        // For keys that fit in an int the hash equals the key — the zero-work
        // floor for ulong keys whose discriminating entropy lives in the low 32
        // bits (dense sequential IDs).
        for (ulong i = 0; i <= 1000; i++)
        {
            Assert.Equal((int)i, _hasher.Hash(i));
        }
    }

    // ── Documented collision: upper-32-bit-only differences truncate away ─────

    [Fact]
    public void Hash_ShouldCollide_WhenTwoKeysDifferOnlyInTheUpper32Bits()
    {
        // The zero-work floor keeps only the low half, so two keys that differ
        // ONLY in the upper 32 bits collide. This is the documented tradeoff
        // (and the reason UInt64WangNaiveHasher exists — it folds the high half
        // back in). Pinning it down guards against an accidental "fix" that
        // would change the contract.
        ulong low  = 1UL;
        ulong high = 1UL | (1UL << 48);
        Assert.Equal(_hasher.Hash(low), _hasher.Hash(high));
    }

    [Fact]
    public void Hash_ShouldProduceDistinctCodes_WhenKeysDifferInTheLow32Bits()
    {
        // Conversely, anything that differs in the low 32 bits is distinguished
        // exactly — the shape identity is meant for (dense sequential IDs).
        var seen = new HashSet<int>();
        for (ulong i = 0; i < 1000; i++)
        {
            Assert.True(seen.Add(_hasher.Hash(i)), $"Unexpected collision at input {i}.");
        }
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void Hash_ShouldReturnTheSameCode_WhenCalledTwiceWithTheSameKey()
    {
        ulong value = 1234567890123456789UL;
        Assert.Equal(_hasher.Hash(value), _hasher.Hash(value));
    }

    [Fact]
    public void Hash_ShouldAgreeAcrossInstances_WhenTheHasherIsDefaultConstructed()
    {
        ulong value = 0x8000_0000_0000_0001UL;
        Assert.Equal(new UInt64IdentityHasher().Hash(value), new UInt64IdentityHasher().Hash(value));
    }

    [Fact]
    public void Hash_ShouldNotThrow_ForAnyKeyInTheValueRange()
    {
        ulong[] testValues =
        {
            0UL, 1UL, ulong.MaxValue, 0x8000_0000_0000_0000UL,
            1234567890123456789UL, 0x0000_FFFF_FFFF_FFFFUL
        };

        foreach (ulong val in testValues)
        {
            var ex = Record.Exception(() => _hasher.Hash(val));
            Assert.Null(ex);
        }
    }

    // ── Integration: satisfies the hasher constraint on collections ──────────
    //
    // This is the gap the type exists to close: the collections take the hasher
    // as a type parameter and invoke it internally, so a ulong-keyed collection
    // needs an IHashProvider<ulong>. A cast to Int64IdentityHasher at the call
    // site is not an option, because there is no call site.

    [Fact]
    public void Hash_ShouldSatisfyTheHasherConstraint_WhenDrivingCelerityDictionary()
    {
        var dict = new CelerityDictionary<ulong, string, UInt64IdentityHasher>();

        dict[0UL]  = "zero";    // default(ulong) — out-of-band slot, never hashed
        dict[1UL]  = "one";
        dict[ulong.MaxValue] = "max";
        dict[42UL] = "forty-two";

        Assert.Equal(4, dict.Count);
        Assert.Equal("zero", dict[0UL]);
        Assert.Equal("one", dict[1UL]);
        Assert.Equal("max", dict[ulong.MaxValue]);
        Assert.Equal("forty-two", dict[42UL]);
        Assert.True(dict.ContainsKey(0UL));
        Assert.False(dict.ContainsKey(999UL));
    }

    [Fact]
    public void Hash_ShouldSatisfyTheHasherConstraint_WhenDrivingCeleritySet()
    {
        var set = new CeleritySet<ulong, UInt64IdentityHasher>();

        set.Add(0UL);    // default(ulong) — out-of-band slot
        set.Add(1UL);
        set.Add(ulong.MaxValue);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(0UL));
        Assert.True(set.Contains(1UL));
        Assert.True(set.Contains(ulong.MaxValue));
        Assert.False(set.Contains(999UL));
    }

    [Fact]
    public void Hash_ShouldSatisfyTheHasherConstraint_WhenDrivingABloomFilter()
    {
        // The sketches carry the same constraint, so they had the same gap.
        var filter = new BloomFilter<ulong, UInt64IdentityHasher>(expectedItems: 1000);
        for (ulong i = 0; i < 500; i++)
        {
            filter.Add(i);
        }

        for (ulong i = 0; i < 500; i++)
        {
            Assert.True(filter.Contains(i));
        }
    }

    [Fact]
    public void Hash_ShouldRoundTripEveryEntry_WhenKeysAreDenseAndSequential()
    {
        // The workload identity is designed for: dense sequential ulong keys are
        // collision-free under identity (low 32 bits distinct) in an
        // open-addressed power-of-two table.
        var dict = new CelerityDictionary<ulong, ulong, UInt64IdentityHasher>();
        for (ulong i = 0; i < 5000; i++)
        {
            dict[i] = i * 2;
        }

        Assert.Equal(5000, dict.Count);
        for (ulong i = 0; i < 5000; i++)
        {
            Assert.Equal(i * 2, dict[i]);
        }
    }
}
