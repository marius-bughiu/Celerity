using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests that exercise <see cref="IntSet{THasher}"/>
/// under maximum hash collision pressure.
/// </summary>
public class IntSetCollisionTests
{
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 42;
    }

    [Fact]
    public void Insert_ShouldSucceed_UnderFullCollision()
    {
        var set = new IntSet<ConstantIntHasher>(16);
        for (int i = 1; i <= 10; i++)
            set.Add(i);

        Assert.Equal(10, set.Count);
        for (int i = 1; i <= 10; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void Remove_ShouldRehashCluster_UnderFullCollision()
    {
        var set = new IntSet<ConstantIntHasher>(16);
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
        var set = new IntSet<ConstantIntHasher>(8);

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
    public void Zero_ShouldWorkAlongside_CollisionChain()
    {
        var set = new IntSet<ConstantIntHasher>(16);
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
        var set = new IntSet<ConstantIntHasher>(
            capacity: 4, loadFactor: 0.5f);

        for (int i = 1; i <= 20; i++)
            set.Add(i);

        Assert.Equal(20, set.Count);
        for (int i = 1; i <= 20; i++)
            Assert.True(set.Contains(i));
    }
}
