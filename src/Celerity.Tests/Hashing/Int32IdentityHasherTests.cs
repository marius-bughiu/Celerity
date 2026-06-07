using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class Int32IdentityHasherTests
{
    private readonly Int32IdentityHasher _hasher = new Int32IdentityHasher();

    // ── Pass-through: the hash IS the key ─────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(42)]
    [InlineData(65536)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    [InlineData(1234567890)]
    [InlineData(-987654321)]
    public void Hash_ReturnsKeyUnchanged(int input)
    {
        // The defining property of the zero-work floor: identity.
        Assert.Equal(input, _hasher.Hash(input));
    }

    [Fact]
    public void Hash_MatchesIntGetHashCode()
    {
        // int.GetHashCode() is itself the identity function, so the identity
        // hasher must reproduce the framework hash exactly — that is the whole
        // point of labelling it the zero-work floor no mixing hasher can beat.
        int[] values = { 0, 1, -1, 42, int.MaxValue, int.MinValue, 1234567890 };
        foreach (int v in values)
        {
            Assert.Equal(v.GetHashCode(), _hasher.Hash(v));
        }
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void Hash_IsDeterministic_AcrossCalls()
    {
        int value = 1234567890;
        Assert.Equal(_hasher.Hash(value), _hasher.Hash(value));
    }

    [Fact]
    public void Hash_IsDeterministic_AcrossInstances()
    {
        // Hashers are stateless structs; two independent instances agree.
        int value = -987654321;
        Assert.Equal(new Int32IdentityHasher().Hash(value), new Int32IdentityHasher().Hash(value));
    }

    // ── Distribution: bijective, so collision-free on any contiguous range ────

    [Fact]
    public void Hash_ConsecutiveInputs_ProduceDistinctResults()
    {
        // Identity is a bijection on the full 32-bit space, so it is
        // collision-free on any contiguous range — exactly the uniform/trusted
        // key shape it is meant for.
        var seen = new HashSet<int>();
        for (int i = 0; i < 1000; i++)
        {
            Assert.True(seen.Add(_hasher.Hash(i)), $"Unexpected collision at input {i}.");
        }
    }

    [Fact]
    public void Hash_DoesNotThrow()
    {
        int[] testValues = { 0, 1, -1, int.MaxValue, int.MinValue, 1234567890, -987654321 };
        foreach (int val in testValues)
        {
            var ex = Record.Exception(() => _hasher.Hash(val));
            Assert.Null(ex);
        }
    }

    // ── Integration: satisfies the hasher constraint on collections ──────────

    [Fact]
    public void Int32IdentityHasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<int, string, Int32IdentityHasher>();

        dict[0]  = "zero";    // default(int) — out-of-band slot, never hashed
        dict[1]  = "one";
        dict[-1] = "neg-one";
        dict[42] = "forty-two";

        Assert.Equal(4, dict.Count);
        Assert.Equal("zero", dict[0]);
        Assert.Equal("one", dict[1]);
        Assert.Equal("neg-one", dict[-1]);
        Assert.Equal("forty-two", dict[42]);
        Assert.True(dict.ContainsKey(0));
        Assert.False(dict.ContainsKey(999));
    }

    [Fact]
    public void Int32IdentityHasher_CanDriveIntDictionary()
    {
        // The out-of-band zero-key slot is exercised end-to-end: Hash(0) == 0,
        // which is also EMPTY_KEY, but the dictionary stores key 0 out-of-band
        // so it never reaches the hasher in a way that aliases the sentinel.
        var dict = new IntDictionary<string, Int32IdentityHasher>();

        dict[0]  = "zero";
        dict[1]  = "one";
        dict[-1] = "neg-one";

        Assert.Equal(3, dict.Count);
        Assert.Equal("zero", dict[0]);
        Assert.Equal("one", dict[1]);
        Assert.Equal("neg-one", dict[-1]);
    }

    [Fact]
    public void Int32IdentityHasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<int, Int32IdentityHasher>();

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
    public void Int32IdentityHasher_CanDriveIntSet()
    {
        var set = new IntSet<Int32IdentityHasher>();

        set.Add(0);
        set.Add(1);
        set.Add(-1);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(0));
        Assert.True(set.Contains(1));
        Assert.True(set.Contains(-1));
        Assert.False(set.Contains(999));
    }

    [Fact]
    public void Int32IdentityHasher_DrivesDictionary_OnDenseSequentialKeys()
    {
        // The workload identity is designed for: dense sequential int keys are
        // collision-free under identity in an open-addressed power-of-two table,
        // so a few thousand inserts round-trip without a single mixing op.
        var dict = new CelerityDictionary<int, int, Int32IdentityHasher>();
        for (int i = 0; i < 5000; i++)
        {
            dict[i] = i * 2;
        }

        Assert.Equal(5000, dict.Count);
        for (int i = 0; i < 5000; i++)
        {
            Assert.Equal(i * 2, dict[i]);
        }
    }
}
