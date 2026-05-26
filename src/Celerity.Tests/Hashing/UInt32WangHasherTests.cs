using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class UInt32WangHasherTests
{
    private readonly UInt32WangHasher _hasher = new UInt32WangHasher();

    // ── Exact-value anchors ───────────────────────────────────────────────────
    //
    // The mixer is computed on the uint directly, so for any given 32-bit pattern
    // these results are identical to Int32WangHasher's (which runs the same
    // hash32shift on (uint)key). The anchors below match the corresponding
    // Int32WangHasherTests bit patterns exactly, pinning the round order / shift
    // amounts / 2057 multiply against drift.

    [Theory]
    [InlineData(0u,            -895235421)]
    [InlineData(1u,             316017654)]
    [InlineData(42u,           2006371508)]
    [InlineData(1234567890u,   -193954774)]
    [InlineData(0x7FFFFFFFu,   2015869290)]   // int.MaxValue bit pattern
    [InlineData(0x80000000u,   1699865937)]   // int.MinValue bit pattern
    [InlineData(uint.MaxValue, -1118438376)]  // all-ones / int -1 bit pattern
    public void Hash_ReturnsExpected(uint input, int expected)
    {
        Assert.Equal(expected, _hasher.Hash(input));
    }

    // ── Bit-pattern equivalence with Int32WangHasher ──────────────────────────

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(42u)]
    [InlineData(1234567890u)]
    [InlineData(0x7FFFFFFFu)]
    [InlineData(0x80000000u)]
    [InlineData(3000000000u)]
    [InlineData(uint.MaxValue)]
    public void Hash_MatchesInt32WangHasher_ForSameBitPattern(uint input)
    {
        // hash32shift runs over the uint reinterpretation in both hashers, so the
        // uint and int variants must agree bit-for-bit on the same 32-bit pattern.
        var intHasher = new Int32WangHasher();
        Assert.Equal(intHasher.Hash(unchecked((int)input)), _hasher.Hash(input));
    }

    // ── Does not map zero to zero ─────────────────────────────────────────────

    [Fact]
    public void Hash_Zero_IsNotZero()
    {
        // Unlike the Murmur3 finalizer, the full Wang mixer has no 0 → 0 fixed
        // point. The dictionaries route the zero key out-of-band so this does
        // not affect the empty-slot sentinel, but it is a documented property.
        Assert.NotEqual(0, _hasher.Hash(0u));
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
        int a = new UInt32WangHasher().Hash(value);
        int b = new UInt32WangHasher().Hash(value);
        Assert.Equal(a, b);
    }

    // ── Avalanche ─────────────────────────────────────────────────────────────

    [Fact]
    public void Hash_HighBits_InfluenceResult()
    {
        // Flip a single bit in the top half of the value; the hash must change.
        // Guards against a regression where high bits are discarded.
        int low  = _hasher.Hash(1u);
        int high = _hasher.Hash(1u | (1u << 24));
        Assert.NotEqual(low, high);
    }

    [Fact]
    public void Hash_ConsecutiveInputs_ProduceDistinctResults()
    {
        // The Wang 32-bit hash is invertible (bijective) on the full 32-bit
        // space, so it is collision-free on any contiguous range. A collision
        // here would indicate a broken mixer.
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
    public void UInt32WangHasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<uint, string, UInt32WangHasher>();

        dict[0u]          = "zero";        // default(uint) — out-of-band slot
        dict[1u]          = "one";
        dict[42u]         = "forty-two";
        dict[3000000000u] = "big";         // beyond int.MaxValue

        Assert.Equal(4, dict.Count);
        Assert.Equal("zero",      dict[0u]);
        Assert.Equal("one",       dict[1u]);
        Assert.Equal("forty-two", dict[42u]);
        Assert.Equal("big",       dict[3000000000u]);
        Assert.True(dict.ContainsKey(0u));
        Assert.False(dict.ContainsKey(999u));
    }

    [Fact]
    public void UInt32WangHasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<uint, UInt32WangHasher>();

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
