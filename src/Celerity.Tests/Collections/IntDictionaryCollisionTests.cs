using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests that exercise <see cref="IntDictionary{TValue, THasher}"/> under
/// maximum hash collision pressure. Every key hashes to the same bucket,
/// so all probing, removal, and rehash logic runs the longest possible
/// chain. Covers gaps identified in issue #7.
/// </summary>
public class IntDictionaryCollisionTests
{
    /// <summary>
    /// A test-only hasher that returns a constant value for every key,
    /// forcing every insert into a single linear-probing chain.
    /// </summary>
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 42;
    }

    [Fact]
    public void Insert_ShouldSucceed_UnderFullCollision()
    {
        var map = new IntDictionary<string, ConstantIntHasher>(16);
        for (int i = 1; i <= 10; i++)
            map[i] = $"v{i}";

        Assert.Equal(10, map.Count);
        for (int i = 1; i <= 10; i++)
            Assert.Equal($"v{i}", map[i]);
    }

    [Fact]
    public void Overwrite_ShouldSucceed_UnderFullCollision()
    {
        var map = new IntDictionary<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 5; i++)
            map[i] = i;
        for (int i = 1; i <= 5; i++)
            map[i] = i * 100;

        Assert.Equal(5, map.Count);
        for (int i = 1; i <= 5; i++)
            Assert.Equal(i * 100, map[i]);
    }

    [Fact]
    public void ContainsKey_ShouldFindAll_UnderFullCollision()
    {
        var map = new IntDictionary<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 8; i++)
            map[i] = i;

        for (int i = 1; i <= 8; i++)
            Assert.True(map.ContainsKey(i));

        Assert.False(map.ContainsKey(99));
    }

    [Fact]
    public void TryGetValue_ShouldWork_UnderFullCollision()
    {
        var map = new IntDictionary<string, ConstantIntHasher>(16);
        map[1] = "one";
        map[2] = "two";
        map[3] = "three";

        Assert.True(map.TryGetValue(2, out var v));
        Assert.Equal("two", v);

        Assert.False(map.TryGetValue(99, out var missing));
        Assert.Null(missing);
    }

    [Fact]
    public void Remove_ShouldRehashCluster_UnderFullCollision()
    {
        // Insert keys 1..6 into a single chain, then remove one from the
        // middle. The rehash-after-remove must re-seat all successors so
        // they remain reachable.
        var map = new IntDictionary<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 6; i++)
            map[i] = i * 10;

        Assert.True(map.Remove(3));
        Assert.Equal(5, map.Count);
        Assert.False(map.ContainsKey(3));

        // Every other key must still be reachable.
        Assert.Equal(10, map[1]);
        Assert.Equal(20, map[2]);
        Assert.Equal(40, map[4]);
        Assert.Equal(50, map[5]);
        Assert.Equal(60, map[6]);
    }

    [Fact]
    public void Remove_AllFromChain_ShouldLeaveEmptyMap()
    {
        var map = new IntDictionary<int, ConstantIntHasher>(16);
        for (int i = 1; i <= 5; i++)
            map[i] = i;

        for (int i = 1; i <= 5; i++)
            Assert.True(map.Remove(i));

        Assert.Equal(0, map.Count);
        for (int i = 1; i <= 5; i++)
            Assert.False(map.ContainsKey(i));
    }

    [Fact]
    public void RemoveThenReinsert_ShouldWork_UnderFullCollision()
    {
        var map = new IntDictionary<int, ConstantIntHasher>(8);

        // Fill the chain.
        for (int i = 1; i <= 10; i++)
            map[i] = i;

        // Remove every other key.
        for (int i = 1; i <= 10; i += 2)
            Assert.True(map.Remove(i));

        Assert.Equal(5, map.Count);

        // Reinsert with different values.
        for (int i = 1; i <= 10; i += 2)
            map[i] = -i;

        Assert.Equal(10, map.Count);
        for (int i = 1; i <= 10; i++)
        {
            int expected = i % 2 == 0 ? i : -i;
            Assert.Equal(expected, map[i]);
        }
    }

    [Fact]
    public void ZeroKey_ShouldWorkAlongside_CollisionChain()
    {
        // The zero key is stored out-of-band. Make sure it coexists with
        // a fully-colliding chain of non-zero keys.
        var map = new IntDictionary<string, ConstantIntHasher>(16);
        map[0] = "zero";
        for (int i = 1; i <= 5; i++)
            map[i] = $"v{i}";

        Assert.Equal(6, map.Count);
        Assert.Equal("zero", map[0]);
        for (int i = 1; i <= 5; i++)
            Assert.Equal($"v{i}", map[i]);

        // Remove zero key — chain should be unaffected.
        Assert.True(map.Remove(0));
        Assert.Equal(5, map.Count);
        Assert.False(map.ContainsKey(0));
        for (int i = 1; i <= 5; i++)
            Assert.True(map.ContainsKey(i));
    }

    [Fact]
    public void Resize_ShouldPreserveAll_UnderFullCollision()
    {
        // Use a tiny capacity with a low load factor so we trigger
        // multiple resizes while all keys collide.
        var map = new IntDictionary<int, ConstantIntHasher>(capacity: 4, loadFactor: 0.5f);
        for (int i = 1; i <= 20; i++)
            map[i] = i * 10;

        Assert.Equal(20, map.Count);
        for (int i = 1; i <= 20; i++)
            Assert.Equal(i * 10, map[i]);
    }

    [Fact]
    public void ResizeThenRemoveSweep_ShouldPreserveAllRemainingKeys_UnderFullCollision()
    {
        // Regression for the tightened Resize / RehashAfterRemove rewrite
        // (issue #83): both paths now reinsert directly into the table without
        // going through the public indexer setter, so they skip the equality
        // check in the probe walk and don't touch _count / _version per entry.
        // This shape exercises both paths under maximum collision pressure:
        //   1. Bulk-insert past the load-factor threshold so Resize fires
        //      multiple times rebuilding a single linear chain.
        //   2. Remove every other key from the middle of the chain, which
        //      forces RehashAfterRemove to walk and reinsert each survivor
        //      whose natural position is behind the freshly emptied slot.
        //   3. Re-insert the removed half, then verify every original key
        //      maps to its expected value and the count is restored.
        var map = new IntDictionary<int, ConstantIntHasher>(capacity: 4, loadFactor: 0.5f);

        for (int i = 1; i <= 40; i++)
            map[i] = i * 7;

        Assert.Equal(40, map.Count);

        for (int i = 1; i <= 40; i += 2)
            Assert.True(map.Remove(i, out int removed) && removed == i * 7);

        Assert.Equal(20, map.Count);
        for (int i = 2; i <= 40; i += 2)
            Assert.Equal(i * 7, map[i]);
        for (int i = 1; i <= 40; i += 2)
            Assert.False(map.ContainsKey(i));

        for (int i = 1; i <= 40; i += 2)
            map[i] = i * 7;

        Assert.Equal(40, map.Count);
        for (int i = 1; i <= 40; i++)
            Assert.Equal(i * 7, map[i]);
    }
}
