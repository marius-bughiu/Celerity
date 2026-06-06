using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests that exercise <see cref="RobinHoodDictionary{TKey, TValue, THasher}"/>
/// under maximum hash-collision pressure, with reference-type (null-default)
/// keys, and against a <see cref="Dictionary{TKey, TValue}"/> oracle. The
/// displacement ("rob from the rich") and backward-shift-with-distance-decrement
/// deletion paths are the parts unique to Robin Hood probing, so they get the
/// heaviest coverage here.
/// </summary>
public class RobinHoodDictionaryCollisionTests
{
    /// <summary>
    /// A test-only hasher that returns a constant value for every key,
    /// forcing every insert into a single linear-probing chain.
    /// </summary>
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 42;
    }

    /// <summary>
    /// A test-only hasher for <see cref="string"/> keys that returns a
    /// constant value, forcing full collision.
    /// </summary>
    private struct ConstantStringHasher : IHashProvider<string>
    {
        public int Hash(string key) => 7;
    }

    /// <summary>
    /// A test-only hasher that returns the key itself, so <c>key &amp; mask</c>
    /// places each key at a predictable slot — used to build clusters whose
    /// entries have different ideal slots, the shape that drives the Robin Hood
    /// displacement and shift-back branches.
    /// </summary>
    private struct IdentityIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => key;
    }

    // ---------------------------------------------------------------
    //  Full-collision tests
    // ---------------------------------------------------------------

    [Fact]
    public void Insert_ShouldSucceed_UnderFullCollision()
    {
        var map = new RobinHoodDictionary<int, string, ConstantIntHasher>(16);
        for (int i = 1; i <= 10; i++)
            map[i] = $"v{i}";

        Assert.Equal(10, map.Count);
        for (int i = 1; i <= 10; i++)
            Assert.Equal($"v{i}", map[i]);
    }

    [Fact]
    public void Overwrite_ShouldSucceed_UnderFullCollision()
    {
        var map = new RobinHoodDictionary<int, int, ConstantIntHasher>(16);
        for (int i = 1; i <= 5; i++)
            map[i] = i;
        for (int i = 1; i <= 5; i++)
            map[i] = i * 100;

        Assert.Equal(5, map.Count);
        for (int i = 1; i <= 5; i++)
            Assert.Equal(i * 100, map[i]);
    }

    [Fact]
    public void Remove_ShouldShiftCluster_UnderFullCollision()
    {
        var map = new RobinHoodDictionary<int, int, ConstantIntHasher>(16);
        for (int i = 1; i <= 6; i++)
            map[i] = i * 10;

        Assert.True(map.Remove(3));
        Assert.Equal(5, map.Count);
        Assert.False(map.ContainsKey(3));

        Assert.Equal(10, map[1]);
        Assert.Equal(20, map[2]);
        Assert.Equal(40, map[4]);
        Assert.Equal(50, map[5]);
        Assert.Equal(60, map[6]);
    }

    [Fact]
    public void RemoveThenReinsert_ShouldWork_UnderFullCollision()
    {
        var map = new RobinHoodDictionary<int, int, ConstantIntHasher>(8);

        for (int i = 1; i <= 10; i++)
            map[i] = i;

        for (int i = 1; i <= 10; i += 2)
            Assert.True(map.Remove(i));

        Assert.Equal(5, map.Count);

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
    public void DefaultKey_ShouldWorkAlongside_CollisionChain()
    {
        var map = new RobinHoodDictionary<int, string, ConstantIntHasher>(16);
        map[0] = "zero";
        for (int i = 1; i <= 5; i++)
            map[i] = $"v{i}";

        Assert.Equal(6, map.Count);
        Assert.Equal("zero", map[0]);
        for (int i = 1; i <= 5; i++)
            Assert.Equal($"v{i}", map[i]);

        Assert.True(map.Remove(0));
        Assert.Equal(5, map.Count);
        Assert.False(map.ContainsKey(0));
        for (int i = 1; i <= 5; i++)
            Assert.True(map.ContainsKey(i));
    }

    [Fact]
    public void Resize_ShouldPreserveAll_UnderFullCollision()
    {
        var map = new RobinHoodDictionary<int, int, ConstantIntHasher>(
            capacity: 4, loadFactor: 0.5f);

        for (int i = 1; i <= 20; i++)
            map[i] = i * 10;

        Assert.Equal(20, map.Count);
        for (int i = 1; i <= 20; i++)
            Assert.Equal(i * 10, map[i]);
    }

    [Fact]
    public void ResizeThenRemoveSweep_ShouldPreserveAllRemainingKeys_UnderFullCollision()
    {
        var map = new RobinHoodDictionary<int, int, ConstantIntHasher>(
            capacity: 4, loadFactor: 0.5f);

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

    // ---------------------------------------------------------------
    //  Displacement-specific tests (the Robin Hood "rob" path)
    // ---------------------------------------------------------------

    [Fact]
    public void Insert_ShouldDisplaceRicherResident_AndKeepEveryKeyReachable()
    {
        // Identity hasher + size 8 (mask 7). Key 8 has ideal slot 0; key 16 has
        // ideal slot 0 too; key 1 has ideal slot 1. Inserting 8, 16 fills slots
        // 0,1 (distances 0,1). Inserting 1 (ideal slot 1, distance 0) meets key
        // 16 at slot 1 with distance 1 > 0, so 1 must NOT displace it; it walks
        // to slot 2. Then inserting 9 (ideal 1) walks past 16's chain. Whichever
        // way the swaps fall, every key stays reachable with the right value.
        var map = new RobinHoodDictionary<int, int, IdentityIntHasher>(capacity: 8, loadFactor: 0.9f);
        int[] keys = { 8, 16, 1, 9, 24, 17 };
        foreach (int k in keys)
            map[k] = k * 11;

        Assert.Equal(keys.Length, map.Count);
        foreach (int k in keys)
        {
            Assert.True(map.ContainsKey(k));
            Assert.Equal(k * 11, map[k]);
        }
    }

    [Fact]
    public void NegativeLookup_ShouldTerminate_OnClusteredKeys()
    {
        // A clustered identity-hashed table where the absent key's ideal slot is
        // occupied by a longer-distance resident: the Robin Hood early-exit must
        // stop the probe rather than scanning the whole cluster — but the test
        // only asserts correctness (ContainsKey false), which is what matters.
        var map = new RobinHoodDictionary<int, int, IdentityIntHasher>(capacity: 8, loadFactor: 0.9f);
        foreach (int k in new[] { 8, 16, 24, 1 })
            map[k] = k;

        Assert.False(map.ContainsKey(32)); // ideal slot 0, never inserted
        Assert.False(map.ContainsKey(40)); // ideal slot 0
        Assert.False(map.ContainsKey(7));  // ideal slot 7, empty
    }

    // ---------------------------------------------------------------
    //  String keys — exercises default(string) == null path
    // ---------------------------------------------------------------

    [Fact]
    public void StringKey_Null_ShouldRoundTrip()
    {
        var map = new RobinHoodDictionary<string, int, ConstantStringHasher>();
        map[null!] = 99;

        Assert.True(map.ContainsKey(null!));
        Assert.Equal(99, map[null!]);
        Assert.Single(map);
    }

    [Fact]
    public void StringKey_Null_ShouldCoexistWithNonNullKeys()
    {
        var map = new RobinHoodDictionary<string, int, ConstantStringHasher>(16);
        map[null!] = 0;
        map["alpha"] = 1;
        map["beta"] = 2;
        map["gamma"] = 3;

        Assert.Equal(4, map.Count);
        Assert.Equal(0, map[null!]);
        Assert.Equal(1, map["alpha"]);
        Assert.Equal(2, map["beta"]);
        Assert.Equal(3, map["gamma"]);
    }

    [Fact]
    public void StringKey_Null_Remove_ShouldWork()
    {
        var map = new RobinHoodDictionary<string, string, ConstantStringHasher>(16);
        map[null!] = "null-val";
        map["a"] = "a-val";

        Assert.True(map.Remove(null!));
        Assert.False(map.ContainsKey(null!));
        Assert.Single(map);
        Assert.Equal("a-val", map["a"]);
    }

    [Fact]
    public void StringKey_TryGetValue_ShouldWork_ForNullKey()
    {
        var map = new RobinHoodDictionary<string, string, ConstantStringHasher>();
        map[null!] = "found";

        Assert.True(map.TryGetValue(null!, out var v));
        Assert.Equal("found", v);

        map.Remove(null!);
        Assert.False(map.TryGetValue(null!, out var missing));
        Assert.Null(missing);
    }

    [Fact]
    public void StringKey_Clear_ShouldResetNullKey()
    {
        var map = new RobinHoodDictionary<string, int, ConstantStringHasher>();
        map[null!] = 1;
        map["x"] = 2;

        map.Clear();

        Assert.Empty(map);
        Assert.False(map.ContainsKey(null!));
        Assert.False(map.ContainsKey("x"));

        map[null!] = 10;
        Assert.Single(map);
        Assert.Equal(10, map[null!]);
    }

    // ---------------------------------------------------------------
    //  Differential fuzz against Dictionary<int,int> — the strongest
    //  correctness check on the displacement / shift-back machinery.
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(1234)]
    [InlineData(98765)]
    public void RandomizedOps_ShouldMatchBclDictionary(int seed)
    {
        var rng = new Random(seed);
        var oracle = new Dictionary<int, int>();
        // Small key universe forces collisions and long probe chains, exercising
        // displacement and backward-shift far more than a sparse key space would.
        var map = new RobinHoodDictionary<int, int, IdentityIntHasher>(capacity: 8, loadFactor: 0.75f);

        for (int step = 0; step < 5000; step++)
        {
            int key = rng.Next(0, 64);
            int op = rng.Next(0, 3);
            switch (op)
            {
                case 0: // insert / overwrite
                    int v = rng.Next();
                    map[key] = v;
                    oracle[key] = v;
                    break;
                case 1: // remove
                    bool oRemoved = oracle.Remove(key, out int oVal);
                    bool mRemoved = map.Remove(key, out int mVal);
                    Assert.Equal(oRemoved, mRemoved);
                    if (oRemoved)
                        Assert.Equal(oVal, mVal);
                    break;
                case 2: // lookup
                    bool present = oracle.TryGetValue(key, out int expected);
                    Assert.Equal(present, map.TryGetValue(key, out int actual));
                    if (present)
                        Assert.Equal(expected, actual);
                    break;
            }

            Assert.Equal(oracle.Count, map.Count);
        }

        // Final full reconciliation.
        Assert.Equal(oracle.Count, map.Count);
        foreach (var kvp in oracle)
        {
            Assert.True(map.TryGetValue(kvp.Key, out int got));
            Assert.Equal(kvp.Value, got);
        }
        foreach (var kvp in map)
            Assert.Equal(oracle[kvp.Key], kvp.Value);
    }
}
