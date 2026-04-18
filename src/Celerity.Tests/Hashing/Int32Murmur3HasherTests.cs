using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class Int32Murmur3HasherTests
{
    private readonly Int32Murmur3Hasher _hasher = new Int32Murmur3Hasher();

    // ── Exact-value anchors ───────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0)]                      // fmix32 maps 0 → 0 (identity fixed-point)
    [InlineData(1, 1364076727)]
    [InlineData(-1, -2114883783)]
    [InlineData(42, 142593372)]
    [InlineData(16, 1428509628)]
    [InlineData(65536, 245581154)]
    [InlineData(int.MaxValue, -104067416)]
    [InlineData(int.MinValue, 1832674720)]
    public void Hash_ReturnsExpected(int input, int expected)
    {
        Assert.Equal(expected, _hasher.Hash(input));
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void Hash_IsDeterministic_AcrossCalls()
    {
        int value = 12345;
        int result1 = _hasher.Hash(value);
        int result2 = _hasher.Hash(value);
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Hash_IsDeterministic_AcrossInstances()
    {
        // Hashers are structs with no state; two independently-constructed
        // instances must produce identical output for the same input.
        int value = -987654321;
        int a = new Int32Murmur3Hasher().Hash(value);
        int b = new Int32Murmur3Hasher().Hash(value);
        Assert.Equal(a, b);
    }

    // ── Avalanche ─────────────────────────────────────────────────────────────

    [Fact]
    public void Hash_HighBits_InfluenceResult()
    {
        // Flip a single bit in the upper half; the hash must change.
        // Guards against a regression where fmix32 stops mixing high bits.
        int low  = _hasher.Hash(1);
        int high = _hasher.Hash(1 | (1 << 24));
        Assert.NotEqual(low, high);
    }

    [Fact]
    public void Hash_ConsecutiveInputs_ProduceDistinctResults()
    {
        // fmix32 is a bijection on uint32; no two distinct 32-bit inputs can
        // produce the same 32-bit output. Consecutive small integers should all
        // hash to distinct values.
        var seen = new HashSet<int>();
        for (int i = 0; i < 1000; i++)
        {
            Assert.True(seen.Add(_hasher.Hash(i)),
                $"Unexpected collision at input {i}.");
        }
    }

    // ── Does not throw ────────────────────────────────────────────────────────

    [Fact]
    public void Hash_DoesNotThrow()
    {
        int[] testValues =
        {
            0, 1, -1, int.MaxValue, int.MinValue, 123456789, -987654321
        };

        foreach (int val in testValues)
        {
            var ex = Record.Exception(() => _hasher.Hash(val));
            Assert.Null(ex);
        }
    }

    // ── Integration: satisfies the hasher constraint on collections ──────────

    [Fact]
    public void Int32Murmur3Hasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<int, string, Int32Murmur3Hasher>();

        dict[0]   = "zero";   // default(int) — out-of-band slot
        dict[1]   = "one";
        dict[-1]  = "neg-one";
        dict[42]  = "forty-two";

        Assert.Equal(4, dict.Count);
        Assert.Equal("zero",     dict[0]);
        Assert.Equal("one",      dict[1]);
        Assert.Equal("neg-one",  dict[-1]);
        Assert.Equal("forty-two",dict[42]);
        Assert.True(dict.ContainsKey(0));
        Assert.False(dict.ContainsKey(999));
    }

    [Fact]
    public void Int32Murmur3Hasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<int, Int32Murmur3Hasher>();

        set.Add(0);   // default(int) — out-of-band slot
        set.Add(1);
        set.Add(-1);
        set.Add(42);

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(0));
        Assert.True(set.Contains(1));
        Assert.True(set.Contains(-1));
        Assert.True(set.Contains(42));
        Assert.False(set.Contains(999));
    }
}
