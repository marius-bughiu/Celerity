using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class UInt32Murmur3HasherTests
{
    private readonly UInt32Murmur3Hasher _hasher = new UInt32Murmur3Hasher();

    // ── Exact-value anchors ───────────────────────────────────────────────────
    //
    // fmix32 is computed on the uint directly, so for any given 32-bit pattern
    // these results are identical to Int32Murmur3Hasher's (which runs the same
    // finalizer on (uint)key). The anchors below pin the round order / shift
    // amounts / multiply constants against drift.

    [Theory]
    [InlineData(0u,            0)]            // fmix32 maps 0 → 0 (identity fixed-point)
    [InlineData(1u,            1364076727)]
    [InlineData(42u,           142593372)]
    [InlineData(16u,           1428509628)]
    [InlineData(65536u,        245581154)]
    [InlineData(0x7FFFFFFFu,   -104067416)]   // int.MaxValue bit pattern
    [InlineData(0x80000000u,   1832674720)]   // int.MinValue bit pattern
    [InlineData(uint.MaxValue, -2114883783)]  // all-ones / int -1 bit pattern
    public void Hash_ReturnsExpected(uint input, int expected)
    {
        Assert.Equal(expected, _hasher.Hash(input));
    }

    // ── Maps zero to zero ─────────────────────────────────────────────────────

    [Fact]
    public void Hash_Zero_IsZero()
    {
        // fmix32 has a fixed point at 0, mirroring Int32Murmur3Hasher. The
        // dictionaries route the zero key out-of-band so this does not collide
        // with the empty-slot sentinel, but it is a documented property.
        Assert.Equal(0, _hasher.Hash(0u));
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void Hash_IsDeterministic_AcrossCalls()
    {
        uint value = 1234567890u;
        int result1 = _hasher.Hash(value);
        int result2 = _hasher.Hash(value);
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Hash_IsDeterministic_AcrossInstances()
    {
        // Hashers are structs with no state; two independently-constructed
        // instances must produce identical output for the same input.
        uint value = 0xDEADBEEFu;
        int a = new UInt32Murmur3Hasher().Hash(value);
        int b = new UInt32Murmur3Hasher().Hash(value);
        Assert.Equal(a, b);
    }

    // ── Avalanche ─────────────────────────────────────────────────────────────

    [Fact]
    public void Hash_HighBits_InfluenceResult()
    {
        // Flip a single bit in the upper half; the hash must change.
        // Guards against a regression where fmix32 stops mixing high bits.
        int low  = _hasher.Hash(1u);
        int high = _hasher.Hash(1u | (1u << 24));
        Assert.NotEqual(low, high);
    }

    [Fact]
    public void Hash_ConsecutiveInputs_ProduceDistinctResults()
    {
        // fmix32 is a bijection on uint32; no two distinct 32-bit inputs can
        // produce the same 32-bit output. Consecutive small integers should all
        // hash to distinct values.
        var seen = new HashSet<int>();
        for (uint i = 0; i < 1000; i++)
        {
            Assert.True(seen.Add(_hasher.Hash(i)),
                $"Unexpected collision at input {i}.");
        }
    }

    // ── Does not throw ────────────────────────────────────────────────────────

    [Fact]
    public void Hash_DoesNotThrow()
    {
        uint[] testValues =
        {
            0u, 1u, uint.MaxValue, 0x7FFFFFFFu, 0x80000000u, 123456789u, 987654321u
        };

        foreach (uint val in testValues)
        {
            var ex = Record.Exception(() => _hasher.Hash(val));
            Assert.Null(ex);
        }
    }

    // ── Integration: satisfies the hasher constraint on collections ──────────

    [Fact]
    public void UInt32Murmur3Hasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<uint, string, UInt32Murmur3Hasher>();

        dict[0u]            = "zero";        // default(uint) — out-of-band slot
        dict[1u]            = "one";
        dict[42u]           = "forty-two";
        dict[3000000000u]   = "big";         // beyond int.MaxValue

        Assert.Equal(4, dict.Count);
        Assert.Equal("zero",      dict[0u]);
        Assert.Equal("one",       dict[1u]);
        Assert.Equal("forty-two", dict[42u]);
        Assert.Equal("big",       dict[3000000000u]);
        Assert.True(dict.ContainsKey(0u));
        Assert.False(dict.ContainsKey(999u));
    }

    [Fact]
    public void UInt32Murmur3Hasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<uint, UInt32Murmur3Hasher>();

        set.Add(0u);             // default(uint) — out-of-band slot
        set.Add(1u);
        set.Add(42u);
        set.Add(uint.MaxValue);

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(0u));
        Assert.True(set.Contains(1u));
        Assert.True(set.Contains(42u));
        Assert.True(set.Contains(uint.MaxValue));
        Assert.False(set.Contains(999u));
    }
}
