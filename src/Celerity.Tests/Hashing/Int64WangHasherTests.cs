using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class Int64WangHasherTests
{
    private readonly Int64WangHasher _hasher = new Int64WangHasher();

    // ── Exact-value anchors ───────────────────────────────────────────────────

    [Theory]
    [InlineData(0L,                      -266614128)]
    [InlineData(1L,                     -1214973746)]
    [InlineData(-1L,                     1066321812)]
    [InlineData(42L,                      511405946)]
    [InlineData(long.MaxValue,          -2087131081)]
    [InlineData(long.MinValue,           2014176584)]
    [InlineData(1234567890123456789L,   -1485153766)]
    public void Hash_ReturnsExpected(long input, int expected)
    {
        Assert.Equal(expected, _hasher.Hash(input));
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void Hash_IsDeterministic_AcrossCalls()
    {
        long value = 1234567890123456789L;
        int result1 = _hasher.Hash(value);
        int result2 = _hasher.Hash(value);
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Hash_IsDeterministic_AcrossInstances()
    {
        // Hashers are structs with no state; two independently-constructed
        // instances must produce identical output for the same input.
        long value = -1234567890987654321L;
        int a = new Int64WangHasher().Hash(value);
        int b = new Int64WangHasher().Hash(value);
        Assert.Equal(a, b);
    }

    // ── Avalanche ─────────────────────────────────────────────────────────────

    [Fact]
    public void Hash_HighBits_InfluenceResult()
    {
        // Flip a single bit in the top half of the long; the hash must change.
        // Guards against a regression where high-half bits are discarded.
        int low  = _hasher.Hash(1L);
        int high = _hasher.Hash(1L | (1L << 48));
        Assert.NotEqual(low, high);
    }

    [Fact]
    public void Hash_ConsecutiveInputs_ProduceDistinctResults()
    {
        // The Wang 64-bit hash is invertible (bijective) on the full 64-bit
        // space, so its lower 32 bits form a near-perfect hash on small ranges.
        // A collision in this range would indicate a broken mixer.
        var seen = new HashSet<int>();
        for (long i = 0; i < 1000; i++)
        {
            Assert.True(seen.Add(_hasher.Hash(i)),
                $"Unexpected collision at input {i}.");
        }
    }

    // ── Does not throw ────────────────────────────────────────────────────────

    [Fact]
    public void Hash_DoesNotThrow()
    {
        long[] testValues =
        {
            0L, 1L, -1L, long.MaxValue, long.MinValue,
            1234567890123456789L, -1234567890987654321L
        };

        foreach (long val in testValues)
        {
            var ex = Record.Exception(() => _hasher.Hash(val));
            Assert.Null(ex);
        }
    }

    // ── Integration: satisfies the hasher constraint on collections ──────────

    [Fact]
    public void Int64WangHasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<long, string, Int64WangHasher>();

        dict[0L]   = "zero";    // default(long) — out-of-band slot
        dict[1L]   = "one";
        dict[-1L]  = "neg-one";
        dict[42L]  = "forty-two";

        Assert.Equal(4, dict.Count);
        Assert.Equal("zero",      dict[0L]);
        Assert.Equal("one",       dict[1L]);
        Assert.Equal("neg-one",   dict[-1L]);
        Assert.Equal("forty-two", dict[42L]);
        Assert.True(dict.ContainsKey(0L));
        Assert.False(dict.ContainsKey(999L));
    }

    [Fact]
    public void Int64WangHasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<long, Int64WangHasher>();

        set.Add(0L);    // default(long) — out-of-band slot
        set.Add(1L);
        set.Add(-1L);
        set.Add(42L);

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(0L));
        Assert.True(set.Contains(1L));
        Assert.True(set.Contains(-1L));
        Assert.True(set.Contains(42L));
        Assert.False(set.Contains(999L));
    }
}
