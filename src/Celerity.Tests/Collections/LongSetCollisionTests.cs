using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests that exercise <see cref="LongSet{THasher}"/>
/// under maximum hash collision pressure.
/// </summary>
public class LongSetCollisionTests
{
    private struct ConstantLongHasher : IHashProvider<long>
    {
        public int Hash(long key) => 42;
    }

    [Fact]
    public void Insert_ShouldSucceed_UnderFullCollision()
    {
        var set = new LongSet<ConstantLongHasher>(16);
        for (long i = 1; i <= 10; i++)
            set.Add(i);

        Assert.Equal(10, set.Count);
        for (long i = 1; i <= 10; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void Remove_ShouldRehashCluster_UnderFullCollision()
    {
        var set = new LongSet<ConstantLongHasher>(16);
        for (long i = 1; i <= 6; i++)
            set.Add(i);

        Assert.True(set.Remove(3L));
        Assert.Equal(5, set.Count);
        Assert.False(set.Contains(3L));

        Assert.True(set.Contains(1L));
        Assert.True(set.Contains(2L));
        Assert.True(set.Contains(4L));
        Assert.True(set.Contains(5L));
        Assert.True(set.Contains(6L));
    }

    [Fact]
    public void RemoveThenReinsert_ShouldWork_UnderFullCollision()
    {
        var set = new LongSet<ConstantLongHasher>(8);

        for (long i = 1; i <= 10; i++)
            set.Add(i);

        for (long i = 1; i <= 10; i += 2)
            Assert.True(set.Remove(i));

        Assert.Equal(5, set.Count);

        for (long i = 1; i <= 10; i += 2)
            set.Add(i);

        Assert.Equal(10, set.Count);
        for (long i = 1; i <= 10; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void Zero_ShouldWorkAlongside_CollisionChain()
    {
        var set = new LongSet<ConstantLongHasher>(16);
        set.Add(0L);
        for (long i = 1; i <= 5; i++)
            set.Add(i);

        Assert.Equal(6, set.Count);
        Assert.True(set.Contains(0L));
        for (long i = 1; i <= 5; i++)
            Assert.True(set.Contains(i));

        Assert.True(set.Remove(0L));
        Assert.Equal(5, set.Count);
        Assert.False(set.Contains(0L));
        for (long i = 1; i <= 5; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void Resize_ShouldPreserveAll_UnderFullCollision()
    {
        var set = new LongSet<ConstantLongHasher>(
            capacity: 4, loadFactor: 0.5f);

        for (long i = 1; i <= 20; i++)
            set.Add(i);

        Assert.Equal(20, set.Count);
        for (long i = 1; i <= 20; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void ResizeThenRemoveSweep_ShouldPreserveAllRemainingItems_UnderFullCollision()
    {
        // Regression mirror of IntSet's tightened Resize / RehashAfterRemove
        // path (issue #83): both reinsert directly into the slots array without
        // going through a general insert helper, so they skip the equality
        // check in the probe walk and don't touch _count / _version per entry.
        // Bulk-add past the threshold to force multiple Resize calls, remove
        // every other item to drive RehashAfterRemove through long clusters,
        // then re-add and verify.
        var set = new LongSet<ConstantLongHasher>(
            capacity: 4, loadFactor: 0.5f);

        for (long i = 1; i <= 40; i++)
            set.Add(i);

        Assert.Equal(40, set.Count);

        for (long i = 1; i <= 40; i += 2)
            Assert.True(set.Remove(i));

        Assert.Equal(20, set.Count);
        for (long i = 2; i <= 40; i += 2)
            Assert.True(set.Contains(i));
        for (long i = 1; i <= 40; i += 2)
            Assert.False(set.Contains(i));

        for (long i = 1; i <= 40; i += 2)
            set.Add(i);

        Assert.Equal(40, set.Count);
        for (long i = 1; i <= 40; i++)
            Assert.True(set.Contains(i));
    }

    // Long-specific: keys sharing the same lower 32 bits but differing in the
    // upper 32 bits must remain distinct, even when their hashes collide.
    [Fact]
    public void UpperBitsOnlyDifference_ShouldKeepValuesDistinct_UnderFullCollision()
    {
        var set = new LongSet<ConstantLongHasher>(16);
        long a = 0x0000_0001_0000_0001L;
        long b = 0x0000_0002_0000_0001L;
        long c = 0x0000_0003_0000_0001L;

        set.Add(a);
        set.Add(b);
        set.Add(c);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(a));
        Assert.True(set.Contains(b));
        Assert.True(set.Contains(c));

        Assert.True(set.Remove(b));
        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(a));
        Assert.False(set.Contains(b));
        Assert.True(set.Contains(c));
    }
}
