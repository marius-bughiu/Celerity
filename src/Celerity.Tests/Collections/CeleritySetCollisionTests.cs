using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests that exercise <see cref="CeleritySet{T, THasher}"/>
/// under maximum hash collision pressure and with reference-type elements
/// whose default is <c>null</c>.
/// </summary>
public class CeleritySetCollisionTests
{
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 42;
    }

    private struct ConstantStringHasher : IHashProvider<string>
    {
        public int Hash(string key) => 7;
    }

    // ---------------------------------------------------------------
    //  Int-element collision tests
    // ---------------------------------------------------------------

    [Fact]
    public void Insert_ShouldSucceed_UnderFullCollision()
    {
        var set = new CeleritySet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 10; i++)
            set.Add(i);

        Assert.Equal(10, set.Count);
        for (int i = 1; i <= 10; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void Duplicate_ShouldBeRejected_UnderFullCollision()
    {
        var set = new CeleritySet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 5; i++)
            set.Add(i);
        for (int i = 1; i <= 5; i++)
            Assert.False(set.TryAdd(i));

        Assert.Equal(5, set.Count);
    }

    [Fact]
    public void Remove_ShouldRehashCluster_UnderFullCollision()
    {
        var set = new CeleritySet<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 6; i++)
            set.Add(i);

        Assert.True(set.Remove(3));
        Assert.Equal(5, set.Count);
        Assert.False(set.Contains(3));

        Assert.True(set.Contains(1));
        Assert.True(set.Contains(2));
        Assert.True(set.Contains(4));
        Assert.True(set.Contains(5));
        Assert.True(set.Contains(6));
    }

    [Fact]
    public void RemoveThenReinsert_ShouldWork_UnderFullCollision()
    {
        var set = new CeleritySet<int, ConstantIntHasher>(8);

        for (int i = 1; i <= 10; i++)
            set.Add(i);

        for (int i = 1; i <= 10; i += 2)
            Assert.True(set.Remove(i));

        Assert.Equal(5, set.Count);

        for (int i = 1; i <= 10; i += 2)
            set.Add(i);

        Assert.Equal(10, set.Count);
        for (int i = 1; i <= 10; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void DefaultKey_ShouldWorkAlongside_CollisionChain()
    {
        var set = new CeleritySet<int, ConstantIntHasher>(16);
        set.Add(0);
        for (int i = 1; i <= 5; i++)
            set.Add(i);

        Assert.Equal(6, set.Count);
        Assert.True(set.Contains(0));
        for (int i = 1; i <= 5; i++)
            Assert.True(set.Contains(i));

        Assert.True(set.Remove(0));
        Assert.Equal(5, set.Count);
        Assert.False(set.Contains(0));
        for (int i = 1; i <= 5; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void Resize_ShouldPreserveAll_UnderFullCollision()
    {
        var set = new CeleritySet<int, ConstantIntHasher>(
            capacity: 4, loadFactor: 0.5f);

        for (int i = 1; i <= 20; i++)
            set.Add(i);

        Assert.Equal(20, set.Count);
        for (int i = 1; i <= 20; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void ResizeThenRemoveSweep_ShouldPreserveAllRemainingItems_UnderFullCollision()
    {
        // Regression for the tightened Resize / RehashAfterRemove rewrite
        // (issue #83): both paths now reinsert directly into the slots array
        // without going through InsertNonDefault, so they skip the equality
        // check in the probe walk and don't touch _count / _version per entry.
        // Mirrors the dictionary cases under maximum collision pressure:
        // bulk-add past the threshold to force multiple Resize calls, remove
        // every other item to drive RehashAfterRemove through long clusters,
        // then re-add and verify.
        var set = new CeleritySet<int, ConstantIntHasher>(
            capacity: 4, loadFactor: 0.5f);

        for (int i = 1; i <= 40; i++)
            set.Add(i);

        Assert.Equal(40, set.Count);

        for (int i = 1; i <= 40; i += 2)
            Assert.True(set.Remove(i));

        Assert.Equal(20, set.Count);
        for (int i = 2; i <= 40; i += 2)
            Assert.True(set.Contains(i));
        for (int i = 1; i <= 40; i += 2)
            Assert.False(set.Contains(i));

        for (int i = 1; i <= 40; i += 2)
            set.Add(i);

        Assert.Equal(40, set.Count);
        for (int i = 1; i <= 40; i++)
            Assert.True(set.Contains(i));
    }

    // ---------------------------------------------------------------
    //  String elements — exercises default(string) == null path
    // ---------------------------------------------------------------

    [Fact]
    public void StringNull_ShouldRoundTrip()
    {
        var set = new CeleritySet<string, ConstantStringHasher>();
        set.Add(null!);

        Assert.True(set.Contains(null!));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void StringNull_ShouldCoexistWithNonNullElements()
    {
        var set = new CeleritySet<string, ConstantStringHasher>(16);
        set.Add(null!);
        set.Add("alpha");
        set.Add("beta");
        set.Add("gamma");

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(null!));
        Assert.True(set.Contains("alpha"));
        Assert.True(set.Contains("beta"));
        Assert.True(set.Contains("gamma"));
    }

    [Fact]
    public void StringNull_Remove_ShouldWork()
    {
        var set = new CeleritySet<string, ConstantStringHasher>(16);
        set.Add(null!);
        set.Add("a");

        Assert.True(set.Remove(null!));
        Assert.False(set.Contains(null!));
        Assert.Equal(1, set.Count);
        Assert.True(set.Contains("a"));
    }

    [Fact]
    public void StringNull_Clear_ShouldReset()
    {
        var set = new CeleritySet<string, ConstantStringHasher>();
        set.Add(null!);
        set.Add("x");

        set.Clear();

        Assert.Equal(0, set.Count);
        Assert.False(set.Contains(null!));
        Assert.False(set.Contains("x"));

        // Reusable after clear.
        set.Add(null!);
        Assert.Equal(1, set.Count);
        Assert.True(set.Contains(null!));
    }
}
