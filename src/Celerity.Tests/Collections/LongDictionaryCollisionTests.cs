using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests that exercise <see cref="LongDictionary{TValue, THasher}"/> under
/// maximum hash collision pressure. Every key hashes to the same bucket,
/// so all probing, removal, and rehash logic runs the longest possible
/// chain. Mirrors <see cref="IntDictionaryCollisionTests"/> for 64-bit keys.
/// </summary>
public class LongDictionaryCollisionTests
{
    /// <summary>
    /// A test-only hasher that returns a constant value for every key,
    /// forcing every insert into a single linear-probing chain.
    /// </summary>
    private struct ConstantLongHasher : IHashProvider<long>
    {
        public int Hash(long key) => 42;
    }

    [Fact]
    public void Insert_ShouldSucceed_UnderFullCollision()
    {
        var map = new LongDictionary<string, ConstantLongHasher>(16);
        for (long i = 1; i <= 10; i++)
            map[i] = $"v{i}";

        Assert.Equal(10, map.Count);
        for (long i = 1; i <= 10; i++)
            Assert.Equal($"v{i}", map[i]);
    }

    [Fact]
    public void Overwrite_ShouldSucceed_UnderFullCollision()
    {
        var map = new LongDictionary<long, ConstantLongHasher>(16);
        for (long i = 1; i <= 5; i++)
            map[i] = i;
        for (long i = 1; i <= 5; i++)
            map[i] = i * 100;

        Assert.Equal(5, map.Count);
        for (long i = 1; i <= 5; i++)
            Assert.Equal(i * 100, map[i]);
    }

    [Fact]
    public void ContainsKey_ShouldFindAll_UnderFullCollision()
    {
        var map = new LongDictionary<long, ConstantLongHasher>(16);
        for (long i = 1; i <= 8; i++)
            map[i] = i;

        for (long i = 1; i <= 8; i++)
            Assert.True(map.ContainsKey(i));

        Assert.False(map.ContainsKey(99L));
    }

    [Fact]
    public void TryGetValue_ShouldWork_UnderFullCollision()
    {
        var map = new LongDictionary<string, ConstantLongHasher>(16);
        map[1L] = "one";
        map[2L] = "two";
        map[3L] = "three";

        Assert.True(map.TryGetValue(2L, out var v));
        Assert.Equal("two", v);

        Assert.False(map.TryGetValue(99L, out var missing));
        Assert.Null(missing);
    }

    [Fact]
    public void Remove_ShouldRehashCluster_UnderFullCollision()
    {
        // Insert keys 1..6 into a single chain, then remove one from the
        // middle. The rehash-after-remove must re-seat all successors so
        // they remain reachable.
        var map = new LongDictionary<long, ConstantLongHasher>(16);
        for (long i = 1; i <= 6; i++)
            map[i] = i * 10;

        Assert.True(map.Remove(3L));
        Assert.Equal(5, map.Count);
        Assert.False(map.ContainsKey(3L));

        // Every other key must still be reachable.
        Assert.Equal(10L, map[1L]);
        Assert.Equal(20L, map[2L]);
        Assert.Equal(40L, map[4L]);
        Assert.Equal(50L, map[5L]);
        Assert.Equal(60L, map[6L]);
    }

    [Fact]
    public void Remove_AllFromChain_ShouldLeaveEmptyMap()
    {
        var map = new LongDictionary<long, ConstantLongHasher>(16);
        for (long i = 1; i <= 5; i++)
            map[i] = i;

        for (long i = 1; i <= 5; i++)
            Assert.True(map.Remove(i));

        Assert.Equal(0, map.Count);
        for (long i = 1; i <= 5; i++)
            Assert.False(map.ContainsKey(i));
    }

    [Fact]
    public void RemoveThenReinsert_ShouldWork_UnderFullCollision()
    {
        var map = new LongDictionary<long, ConstantLongHasher>(8);

        // Fill the chain.
        for (long i = 1; i <= 10; i++)
            map[i] = i;

        // Remove every other key.
        for (long i = 1; i <= 10; i += 2)
            Assert.True(map.Remove(i));

        Assert.Equal(5, map.Count);

        // Reinsert with different values.
        for (long i = 1; i <= 10; i += 2)
            map[i] = -i;

        Assert.Equal(10, map.Count);
        for (long i = 1; i <= 10; i++)
        {
            long expected = i % 2 == 0 ? i : -i;
            Assert.Equal(expected, map[i]);
        }
    }

    [Fact]
    public void ZeroKey_ShouldWorkAlongside_CollisionChain()
    {
        // The zero key (0L) is stored out-of-band. Make sure it coexists
        // with a fully-colliding chain of non-zero keys.
        var map = new LongDictionary<string, ConstantLongHasher>(16);
        map[0L] = "zero";
        for (long i = 1; i <= 5; i++)
            map[i] = $"v{i}";

        Assert.Equal(6, map.Count);
        Assert.Equal("zero", map[0L]);
        for (long i = 1; i <= 5; i++)
            Assert.Equal($"v{i}", map[i]);

        // Remove zero key — chain should be unaffected.
        Assert.True(map.Remove(0L));
        Assert.Equal(5, map.Count);
        Assert.False(map.ContainsKey(0L));
        for (long i = 1; i <= 5; i++)
            Assert.True(map.ContainsKey(i));
    }

    [Fact]
    public void Resize_ShouldPreserveAll_UnderFullCollision()
    {
        // Use a tiny capacity with a low load factor so we trigger
        // multiple resizes while all keys collide.
        var map = new LongDictionary<long, ConstantLongHasher>(capacity: 4, loadFactor: 0.5f);
        for (long i = 1; i <= 20; i++)
            map[i] = i * 10;

        Assert.Equal(20, map.Count);
        for (long i = 1; i <= 20; i++)
            Assert.Equal(i * 10, map[i]);
    }

    [Fact]
    public void ResizeThenRemoveSweep_ShouldPreserveAllRemainingKeys_UnderFullCollision()
    {
        // Regression for the tightened Resize / RehashAfterRemove rewrite
        // (issue #82): both paths now reinsert directly into the table without
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
        var map = new LongDictionary<long, ConstantLongHasher>(capacity: 4, loadFactor: 0.5f);

        for (long i = 1; i <= 40; i++)
            map[i] = i * 7;

        Assert.Equal(40, map.Count);

        for (long i = 1; i <= 40; i += 2)
            Assert.True(map.Remove(i, out long removed) && removed == i * 7);

        Assert.Equal(20, map.Count);
        for (long i = 2; i <= 40; i += 2)
            Assert.Equal(i * 7, map[i]);
        for (long i = 1; i <= 40; i += 2)
            Assert.False(map.ContainsKey(i));

        for (long i = 1; i <= 40; i += 2)
            map[i] = i * 7;

        Assert.Equal(40, map.Count);
        for (long i = 1; i <= 40; i++)
            Assert.Equal(i * 7, map[i]);
    }

    [Fact]
    public void ExtremeKeys_ShouldWork_UnderFullCollision()
    {
        // Long-specific: extreme values (including negative) live on the
        // same collision chain as small positive keys. The probe path must
        // not truncate or misinterpret the upper 32 bits.
        var map = new LongDictionary<string, ConstantLongHasher>(16);
        map[long.MaxValue] = "max";
        map[long.MinValue] = "min";
        map[(long)int.MaxValue + 1L] = "above-int-max";
        map[(long)int.MinValue - 1L] = "below-int-min";
        map[-1L] = "neg-one";

        Assert.Equal(5, map.Count);
        Assert.Equal("max", map[long.MaxValue]);
        Assert.Equal("min", map[long.MinValue]);
        Assert.Equal("above-int-max", map[(long)int.MaxValue + 1L]);
        Assert.Equal("below-int-min", map[(long)int.MinValue - 1L]);
        Assert.Equal("neg-one", map[-1L]);

        // Two keys sharing the lower 32 bits but differing in the upper 32
        // bits must remain distinct even when forced into the same chain.
        Assert.False(map.ContainsKey(int.MaxValue));
        Assert.False(map.ContainsKey(int.MinValue));
    }
}
