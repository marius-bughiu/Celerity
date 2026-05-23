using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class Int32WangHasherTests
{
    private readonly Int32WangHasher _hasher = new Int32WangHasher();

    // ── Exact-value anchors ───────────────────────────────────────────────────

    [Theory]
    [InlineData(0,                 -895235421)]
    [InlineData(1,                  316017654)]
    [InlineData(-1,               -1118438376)]
    [InlineData(42,               2006371508)]
    [InlineData(int.MaxValue,     2015869290)]
    [InlineData(int.MinValue,     1699865937)]
    [InlineData(1234567890,       -193954774)]
    public void Hash_ReturnsExpected(int input, int expected)
    {
        Assert.Equal(expected, _hasher.Hash(input));
    }

    // ── Does not map zero to zero ─────────────────────────────────────────────

    [Fact]
    public void Hash_Zero_IsNotZero()
    {
        // Unlike the Murmur3 finalizer, the full Wang mixer has no 0 → 0 fixed
        // point. The dictionaries route the zero key out-of-band so this does
        // not affect the empty-slot sentinel, but it is a documented property.
        Assert.NotEqual(0, _hasher.Hash(0));
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void Hash_IsDeterministic_AcrossCalls()
    {
        int value = 1234567890;
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
        int a = new Int32WangHasher().Hash(value);
        int b = new Int32WangHasher().Hash(value);
        Assert.Equal(a, b);
    }

    // ── Avalanche ─────────────────────────────────────────────────────────────

    [Fact]
    public void Hash_HighBits_InfluenceResult()
    {
        // Flip a single bit in the top half of the int; the hash must change.
        // Guards against a regression where high bits are discarded.
        int low  = _hasher.Hash(1);
        int high = _hasher.Hash(1 | (1 << 24));
        Assert.NotEqual(low, high);
    }

    [Fact]
    public void Hash_ConsecutiveInputs_ProduceDistinctResults()
    {
        // The Wang 32-bit hash is invertible (bijective) on the full 32-bit
        // space, so it is collision-free on any contiguous range. A collision
        // here would indicate a broken mixer.
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
            0, 1, -1, int.MaxValue, int.MinValue, 1234567890, -987654321
        };

        foreach (int val in testValues)
        {
            var ex = Record.Exception(() => _hasher.Hash(val));
            Assert.Null(ex);
        }
    }

    // ── Integration: satisfies the hasher constraint on collections ──────────

    [Fact]
    public void Int32WangHasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<int, string, Int32WangHasher>();

        dict[0]   = "zero";    // default(int) — out-of-band slot
        dict[1]   = "one";
        dict[-1]  = "neg-one";
        dict[42]  = "forty-two";

        Assert.Equal(4, dict.Count);
        Assert.Equal("zero",      dict[0]);
        Assert.Equal("one",       dict[1]);
        Assert.Equal("neg-one",   dict[-1]);
        Assert.Equal("forty-two", dict[42]);
        Assert.True(dict.ContainsKey(0));
        Assert.False(dict.ContainsKey(999));
    }

    [Fact]
    public void Int32WangHasher_CanDriveIntDictionary()
    {
        var dict = new IntDictionary<string, Int32WangHasher>();

        dict[0]  = "zero";    // 0 collides with EMPTY_KEY — out-of-band slot
        dict[1]  = "one";
        dict[-1] = "neg-one";

        Assert.Equal(3, dict.Count);
        Assert.Equal("zero",    dict[0]);
        Assert.Equal("one",     dict[1]);
        Assert.Equal("neg-one", dict[-1]);
    }

    [Fact]
    public void Int32WangHasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<int, Int32WangHasher>();

        set.Add(0);    // default(int) — out-of-band slot
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

    [Fact]
    public void Int32WangHasher_CanDriveIntSet()
    {
        var set = new IntSet<Int32WangHasher>();

        set.Add(0);
        set.Add(1);
        set.Add(-1);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(0));
        Assert.True(set.Contains(1));
        Assert.True(set.Contains(-1));
        Assert.False(set.Contains(999));
    }
}
