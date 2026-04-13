using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class DefaultHasherTests
{
    // ── Core contract ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Hash_ShouldMatchEqualityComparerDefault_ForInt(int key)
    {
        var hasher = new DefaultHasher<int>();
        Assert.Equal(EqualityComparer<int>.Default.GetHashCode(key), hasher.Hash(key));
    }

    [Fact]
    public void Hash_ShouldMatchEqualityComparerDefault_ForString()
    {
        var hasher = new DefaultHasher<string>();
        const string key = "hello world";
        Assert.Equal(EqualityComparer<string>.Default.GetHashCode(key), hasher.Hash(key));
    }

    [Fact]
    public void Hash_ShouldMatchEqualityComparerDefault_ForGuid()
    {
        var hasher = new DefaultHasher<Guid>();
        Guid key = Guid.NewGuid();
        Assert.Equal(EqualityComparer<Guid>.Default.GetHashCode(key), hasher.Hash(key));
    }

    [Fact]
    public void Hash_ShouldMatchEqualityComparerDefault_ForGuidEmpty()
    {
        // Guid.Empty is default(Guid); the set/dictionary stores it out-of-band,
        // but the hasher itself must still produce the correct value.
        var hasher = new DefaultHasher<Guid>();
        Assert.Equal(EqualityComparer<Guid>.Default.GetHashCode(Guid.Empty), hasher.Hash(Guid.Empty));
    }

    // ── Determinism ────────────────────────────────────────────────────────────

    [Fact]
    public void Hash_ShouldBeConsistent_WhenCalledMultipleTimes()
    {
        var hasher = new DefaultHasher<string>();
        const string key = "consistency";
        int h1 = hasher.Hash(key);
        int h2 = hasher.Hash(key);
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void Hash_ShouldBeConsistent_AcrossInstances()
    {
        // Each DefaultHasher<T> is a new struct value — they must agree.
        var h1 = new DefaultHasher<int>().Hash(99);
        var h2 = new DefaultHasher<int>().Hash(99);
        Assert.Equal(h1, h2);
    }

    // ── Integration: usable as a CeleritySet hasher ────────────────────────────

    [Fact]
    public void DefaultHasher_CanDriveIntSet_ViaIntSet()
    {
        // DefaultHasher<Guid> as a stand-in for an arbitrary type on CeleritySet.
        // (IntSet uses Int32WangNaiveHasher by default; here we use DefaultHasher
        //  through the generic IntSet<THasher> overload to verify the constraint.)
        var set = new IntSet<DefaultHasher<int>>(capacity: 16);

        set.Add(1);
        set.Add(2);
        set.Add(0);   // zero key goes out-of-band
        set.Add(-1);

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(0));
        Assert.True(set.Contains(1));
        Assert.True(set.Contains(2));
        Assert.True(set.Contains(-1));
        Assert.False(set.Contains(42));
    }

    [Fact]
    public void DefaultHasher_CanDriveCeleritySet_ForGuidKeys()
    {
        // Guid has no specialized hasher in Celerity yet; DefaultHasher fills the gap.
        var set = new CeleritySet<Guid, DefaultHasher<Guid>>();

        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.Empty;   // default(Guid) — stored out-of-band

        set.Add(a);
        set.Add(b);
        set.Add(c);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(a));
        Assert.True(set.Contains(b));
        Assert.True(set.Contains(c));
        Assert.False(set.Contains(Guid.NewGuid()));
    }

    [Fact]
    public void DefaultHasher_CanDriveCeleritySet_ForStringKeys()
    {
        var set = new CeleritySet<string, DefaultHasher<string>>();

        set.Add("alpha");
        set.Add("beta");
        set.Add("gamma");

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains("alpha"));
        Assert.False(set.Contains("delta"));
    }

    [Fact]
    public void DefaultHasher_CanDriveCelerityDictionary_ForGuidKeys()
    {
        // Demonstrates that DefaultHasher<T> satisfies the dictionary's hasher constraint.
        var dict = new CelerityDictionary<Guid, string, DefaultHasher<Guid>>();

        var key = Guid.NewGuid();
        dict[key] = "value";

        Assert.Equal("value", dict[key]);
        Assert.True(dict.ContainsKey(key));
    }

    // ── Duplicate / remove round-trip ──────────────────────────────────────────

    [Fact]
    public void DefaultHasher_SetDuplicateHandling_WorksCorrectly()
    {
        var set = new CeleritySet<string, DefaultHasher<string>>();

        Assert.True(set.TryAdd("x"));
        Assert.False(set.TryAdd("x"));   // duplicate
        Assert.Equal(1, set.Count);

        Assert.True(set.Remove("x"));
        Assert.False(set.Contains("x"));
        Assert.Equal(0, set.Count);

        Assert.True(set.TryAdd("x"));    // re-insert after remove
        Assert.Equal(1, set.Count);
    }
}
