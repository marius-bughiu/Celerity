using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests that exercise <see cref="BiDictionary{TKey, TValue, TKeyHasher, TValueHasher}"/> under maximum
/// hash-collision pressure on the key side, the value side, or both, with reference-type (null-default)
/// keys and values, and against a pair of <see cref="Dictionary{TKey, TValue}"/> oracles. Mirrors
/// <c>LinkedDictionaryCollisionTests</c>; the ordered-end cases are replaced by the ones that matter for
/// two chain families over one dense array: unlinking an entry from the head, middle and tail of a key
/// chain and of a value chain, the swap-last move when the moved entry shares a chain with the removed
/// one, and a rebind that moves an entry from one value chain to another.
/// </summary>
/// <remarks>
/// A new entry is pushed onto the <i>front</i> of its bucket's chain, so under a constant hasher the
/// newest entry (the last slot) heads both chains and the oldest (slot 0) sits at their end. Removing
/// any entry but the last therefore moves the chain head into the hole, and re-aims the bucket itself.
/// </remarks>
public class BiDictionaryCollisionTests
{
    /// <summary>
    /// A test-only hasher that returns a constant value for every key, forcing every insert into
    /// the same bucket chain.
    /// </summary>
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 42;
    }

    /// <summary>
    /// A test-only hasher for <see cref="string"/> keys or values that returns a constant value, forcing
    /// full collision into one chain, so every step falls through to Equals.
    /// </summary>
    private struct ConstantStringHasher : IHashProvider<string>
    {
        public int Hash(string key) => 7;
    }

    /// <summary>
    /// A test-only hasher that returns the key itself, so the bucket is a predictable function of
    /// the key.
    /// </summary>
    private struct IdentityIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => key;
    }

    private struct ZeroStringHasher : IHashProvider<string>
    {
        public int Hash(string key) => 0;
    }

    private static int[] KeysOf<TValue, TKeyHasher, TValueHasher>(BiDictionary<int, TValue, TKeyHasher, TValueHasher> map)
        where TKeyHasher : struct, IHashProvider<int>
        where TValueHasher : struct, IHashProvider<TValue>
    {
        var keys = new List<int>();
        foreach (var kvp in map)
            keys.Add(kvp.Key);
        return keys.ToArray();
    }

    // Asserts that every pair is reachable from both sides and that Count agrees.
    private static void AssertPairs<TKeyHasher, TValueHasher>(
        BiDictionary<int, int, TKeyHasher, TValueHasher> map, params (int Key, int Value)[] pairs)
        where TKeyHasher : struct, IHashProvider<int>
        where TValueHasher : struct, IHashProvider<int>
    {
        Assert.Equal(pairs.Length, map.Count);
        foreach (var (key, value) in pairs)
        {
            Assert.Equal(value, map[key]);
            Assert.Equal(key, map.GetKey(value));
        }
    }

    // ---------------------------------------------------------------
    //  Full-collision tests — every key and every value shares one chain
    // ---------------------------------------------------------------

    [Fact]
    public void Insert_ShouldSucceed_UnderFullCollision()
    {
        var map = new BiDictionary<int, string, ConstantIntHasher, ConstantStringHasher>(16);
        for (int i = 1; i <= 40; i++)
            map[i] = $"v{i}";

        Assert.Equal(40, map.Count);
        for (int i = 1; i <= 40; i++)
        {
            Assert.Equal($"v{i}", map[i]);
            Assert.Equal(i, map.GetKey($"v{i}"));
        }
        Assert.Equal(Enumerable.Range(1, 40), KeysOf(map));
        Assert.False(map.ContainsKey(41));
        Assert.False(map.ContainsValue("v41"));
    }

    [Fact]
    public void ValueConflicts_ShouldBeDetected_UnderFullCollision()
    {
        var map = new BiDictionary<int, int, ConstantIntHasher, ConstantIntHasher>(16);
        for (int i = 1; i <= 10; i++)
            map[i] = i * 10;

        for (int i = 1; i <= 10; i++)
        {
            Assert.False(map.TryAdd(100 + i, i * 10));
            Assert.Throws<ArgumentException>(() => map[100 + i] = i * 10);
        }
        Assert.Equal(10, map.Count);
    }

    [Fact]
    public void Overwrite_ShouldRebindWithinOneValueChain_UnderFullCollision()
    {
        var map = new BiDictionary<int, int, ConstantIntHasher, ConstantIntHasher>(16);
        for (int i = 1; i <= 20; i++)
            map[i] = i;

        // Rebinding each key unlinks its old value from the head, middle or tail of the one chain.
        for (int i = 1; i <= 20; i++)
            map[i] = i * 100;

        Assert.Equal(20, map.Count);
        for (int i = 1; i <= 20; i++)
        {
            Assert.Equal(i * 100, map[i]);
            Assert.Equal(i, map.GetKey(i * 100));
            Assert.False(map.ContainsValue(i)); // the old value was unlinked
        }
        Assert.Equal(Enumerable.Range(1, 20), KeysOf(map));
    }

    [Fact]
    public void Remove_ShouldUnlinkTheChainHeadMiddleAndTail_UnderFullCollision()
    {
        var map = new BiDictionary<int, int, ConstantIntHasher, ConstantIntHasher>(16);
        for (int i = 1; i <= 6; i++)
            map[i] = i * 10;
        // Both chains (head → tail): 6, 5, 4, 3, 2, 1. Slots: 1, 2, 3, 4, 5, 6.

        Assert.True(map.Remove(6, out int head)); // chain head, and the last slot: nothing moves
        Assert.Equal(60, head);
        AssertPairs(map, (1, 10), (2, 20), (3, 30), (4, 40), (5, 50));

        Assert.True(map.Remove(1, out int tail)); // chain tail, slot 0: 5 (the chain head) moves in
        Assert.Equal(10, tail);
        Assert.Equal(new[] { 5, 2, 3, 4 }, KeysOf(map));
        AssertPairs(map, (2, 20), (3, 30), (4, 40), (5, 50));

        Assert.True(map.Remove(3, out int middle)); // mid-chain: 4 moves in
        Assert.Equal(30, middle);
        Assert.Equal(new[] { 5, 2, 4 }, KeysOf(map));
        AssertPairs(map, (2, 20), (4, 40), (5, 50));

        foreach (int k in new[] { 1, 3, 6 })
        {
            Assert.False(map.ContainsKey(k));
            Assert.False(map.ContainsValue(k * 10));
        }
    }

    [Fact]
    public void RemoveValue_ShouldUnlinkTheChainHeadMiddleAndTail_UnderFullCollision()
    {
        var map = new BiDictionary<int, int, ConstantIntHasher, ConstantIntHasher>(16);
        for (int i = 1; i <= 6; i++)
            map[i] = i * 10;

        Assert.True(map.RemoveValue(60, out int head));
        Assert.Equal(6, head);
        Assert.True(map.RemoveValue(10, out int tail));
        Assert.Equal(1, tail);
        Assert.True(map.RemoveValue(30, out int middle));
        Assert.Equal(3, middle);

        Assert.Equal(new[] { 5, 2, 4 }, KeysOf(map));
        AssertPairs(map, (2, 20), (4, 40), (5, 50));
        Assert.False(map.RemoveValue(30));
    }

    [Fact]
    public void SwapLast_ShouldReaimTheMovedEntrysLinks_WhenItPointsAtTheRemovedEntry()
    {
        // Two entries in one chain on each side: slot 1 heads both chains and links to slot 0.
        // Removing slot 0 first rewrites slot 1's own links (they pointed at slot 0), then moves
        // slot 1 into slot 0 and re-aims both buckets.
        var map = new BiDictionary<int, int, ConstantIntHasher, ConstantIntHasher>(16);
        map[1] = 10;
        map[2] = 20;

        Assert.True(map.Remove(1));

        AssertPairs(map, (2, 20));
        Assert.False(map.ContainsKey(1));
        Assert.False(map.ContainsValue(10));

        map[3] = 30;
        AssertPairs(map, (2, 20), (3, 30));
    }

    [Fact]
    public void SwapLast_ShouldReaimAPredecessorLink_WhenTheMovedEntryIsMidChain()
    {
        // Slots 0..3 hold keys 1..4; both chains read 4, 3, 2, 1. Removing key 1 moves key 4 (the chain
        // head) into slot 0, so the last slot now holds key 3, which sits behind key 4 in both chains.
        // Removing key 2 must therefore re-aim key 4's links, not a bucket, at the hole.
        var map = new BiDictionary<int, int, ConstantIntHasher, ConstantIntHasher>(16);
        for (int i = 1; i <= 4; i++)
            map[i] = i * 10;

        Assert.True(map.Remove(1));
        Assert.Equal(new[] { 4, 2, 3 }, KeysOf(map));
        Assert.True(map.Remove(2));
        Assert.Equal(new[] { 4, 3 }, KeysOf(map));
        AssertPairs(map, (4, 40), (3, 30));

        map[5] = 50;
        Assert.True(map.RemoveValue(40)); // slot 0 again: key 5 (the head) moves in
        Assert.Equal(new[] { 5, 3 }, KeysOf(map));
        AssertPairs(map, (5, 50), (3, 30));
    }

    [Fact]
    public void SwapLast_ShouldReaimTheBuckets_WhenTheMovedEntryLivesInOtherChains()
    {
        // Identity hashers at 8 buckets. Key buckets: 1 → {9, 1}, 5 → {13, 5}. Value buckets:
        // 2 → {10, 2}, 6 → {6}, 3 → {3}. The removed entry and the moved one share no chain.
        var map = new BiDictionary<int, int, IdentityIntHasher, IdentityIntHasher>(capacity: 8, loadFactor: 0.75f);
        map[1] = 2;   // slot 0
        map[5] = 6;   // slot 1
        map[9] = 10;  // slot 2
        map[13] = 3;  // slot 3

        Assert.True(map.Remove(5)); // slot 1: key 13 moves in; it heads its key chain and its value chain
        Assert.Equal(new[] { 1, 13, 9 }, KeysOf(map));
        AssertPairs(map, (1, 2), (13, 3), (9, 10));

        map[21] = 14; // slot 3: key bucket 5 → {21, 13}; value bucket 6 → {14}
        Assert.True(map.Remove(1)); // slot 0: key 21 moves in
        Assert.Equal(new[] { 21, 13, 9 }, KeysOf(map));
        AssertPairs(map, (21, 14), (13, 3), (9, 10));

        Assert.True(map.Remove(21)); // slot 0 again: key 9 moves in
        Assert.Equal(new[] { 9, 13 }, KeysOf(map));
        AssertPairs(map, (9, 10), (13, 3));
    }

    [Fact]
    public void SwapLast_ShouldKeepEveryPairReachable_UnderRepeatedFrontRemovals()
    {
        var map = new BiDictionary<int, int, ConstantIntHasher, ConstantIntHasher>(16);
        for (int i = 1; i <= 8; i++)
            map[i] = -i;

        var live = new HashSet<int>(Enumerable.Range(1, 8));
        while (map.Count > 0)
        {
            int first = KeysOf(map)[0];
            Assert.True(map.Remove(first));
            live.Remove(first);
            foreach (int k in live)
            {
                Assert.Equal(-k, map[k]);
                Assert.Equal(k, map.GetKey(-k));
            }
            Assert.Equal(live.Count, map.Count);
        }
    }

    [Fact]
    public void KeyCollisionsOnly_ShouldStillFindValuesDirectly()
    {
        var map = new BiDictionary<int, int, ConstantIntHasher, IdentityIntHasher>(16);
        for (int i = 1; i <= 10; i++)
            map[i] = i * 3;

        Assert.True(map.Remove(4));
        Assert.True(map.RemoveValue(27)); // key 9
        Assert.True(map.ForceSet(1, 30)); // evicts key 10

        Assert.Equal(new[] { 1, 2, 3, 8, 5, 6, 7 }, KeysOf(map));
        AssertPairs(map, (1, 30), (2, 6), (3, 9), (5, 15), (6, 18), (7, 21), (8, 24));
    }

    [Fact]
    public void ValueCollisionsOnly_ShouldStillFindKeysDirectly()
    {
        var map = new BiDictionary<int, int, IdentityIntHasher, ConstantIntHasher>(16);
        for (int i = 1; i <= 10; i++)
            map[i] = i * 3;

        Assert.True(map.Remove(4));
        Assert.True(map.RemoveValue(27)); // key 9
        Assert.True(map.ForceSet(1, 30)); // evicts key 10

        Assert.Equal(new[] { 1, 2, 3, 8, 5, 6, 7 }, KeysOf(map));
        AssertPairs(map, (1, 30), (2, 6), (3, 9), (5, 15), (6, 18), (7, 21), (8, 24));
    }

    [Fact]
    public void Rebind_ShouldMoveTheEntryAcrossValueChains()
    {
        // Identity hashers at 8 buckets: values 1, 9, 17 share value bucket 1; 2, 10, 18, 26 share bucket 2.
        var map = new BiDictionary<int, int, IdentityIntHasher, IdentityIntHasher>(capacity: 8, loadFactor: 0.9f);
        map[100] = 1;
        map[101] = 9;
        map[102] = 17;
        map[103] = 2;
        // 8 buckets: value bucket 1 → {17, 9, 1}; value bucket 2 → {2}.

        map[101] = 10;  // mid-chain unlink from bucket 1, push onto bucket 2
        map[100] = 18;  // chain tail unlink, push onto bucket 2
        map[102] = 26;  // the only entry left in bucket 1

        AssertPairs(map, (100, 18), (101, 10), (102, 26), (103, 2));
        foreach (int stale in new[] { 1, 9, 17 })
            Assert.False(map.ContainsValue(stale));

        // And back again, which unlinks from the middle of the now-long bucket-2 chain.
        map[101] = 9;
        AssertPairs(map, (100, 18), (101, 9), (102, 26), (103, 2));
        Assert.False(map.ContainsValue(10));
    }

    [Fact]
    public void ForceSet_ShouldEvictFromTheMiddleOfAChain_UnderFullCollision()
    {
        var map = new BiDictionary<int, int, ConstantIntHasher, ConstantIntHasher>(16);
        for (int i = 1; i <= 5; i++)
            map[i] = i * 10;

        Assert.True(map.ForceSet(2, 40)); // key 2 (slot 1) evicts key 4 (slot 3); 5 moves into slot 3
        Assert.Equal(new[] { 1, 2, 3, 5 }, KeysOf(map));
        AssertPairs(map, (1, 10), (2, 40), (3, 30), (5, 50));

        Assert.True(map.ForceSet(5, 10)); // key 5 (the last slot) evicts key 1 (slot 0) and moves into it
        Assert.Equal(new[] { 5, 2, 3 }, KeysOf(map));
        AssertPairs(map, (5, 10), (2, 40), (3, 30));

        Assert.True(map.ForceSet(7, 30)); // new key evicts key 3
        Assert.Equal(new[] { 5, 2, 7 }, KeysOf(map));
        AssertPairs(map, (5, 10), (2, 40), (7, 30));
    }

    [Fact]
    public void RemoveThenReinsert_ShouldWork_UnderFullCollision()
    {
        var map = new BiDictionary<int, int, ConstantIntHasher, ConstantIntHasher>(16);

        for (int i = 1; i <= 30; i++)
            map[i] = i;

        for (int i = 1; i <= 30; i += 2)
            Assert.True(map.Remove(i));

        Assert.Equal(15, map.Count);
        int capacity = map.EnsureCapacity(0);

        for (int i = 1; i <= 30; i += 2)
            map[i] = -i;

        Assert.Equal(30, map.Count);
        Assert.Equal(capacity, map.EnsureCapacity(0));
        for (int i = 1; i <= 30; i++)
        {
            int expected = i % 2 == 0 ? i : -i;
            Assert.Equal(expected, map[i]);
            Assert.Equal(i, map.GetKey(expected));
        }
    }

    [Fact]
    public void DefaultKeyAndValue_ShouldWorkAlongside_CollisionChain()
    {
        var map = new BiDictionary<int, int, ConstantIntHasher, ConstantIntHasher>(16);
        map[0] = 100;
        map[100] = 0;
        for (int i = 1; i <= 20; i++)
            map[i] = -i;

        Assert.Equal(22, map.Count);
        Assert.Equal(100, map[0]);
        Assert.Equal(0, map[100]);
        Assert.Equal(100, map.GetKey(0));

        Assert.True(map.Remove(0));
        Assert.True(map.RemoveValue(0));
        Assert.Equal(20, map.Count);
        Assert.False(map.ContainsKey(0));
        Assert.False(map.ContainsValue(0));
        for (int i = 1; i <= 20; i++)
            Assert.Equal(i, map.GetKey(-i));
    }

    [Fact]
    public void Resize_ShouldPreserveAll_UnderFullCollision()
    {
        var map = new BiDictionary<int, int, ConstantIntHasher, ConstantIntHasher>(
            capacity: 16, loadFactor: 0.5f);

        for (int i = 1; i <= 60; i++)
            map[i] = i * 10;

        Assert.Equal(60, map.Count);
        for (int i = 1; i <= 60; i++)
        {
            Assert.Equal(i * 10, map[i]);
            Assert.Equal(i, map.GetKey(i * 10));
        }
        Assert.Equal(Enumerable.Range(1, 60), KeysOf(map));
    }

    [Fact]
    public void ResizeThenRemoveSweep_ShouldPreserveAllRemainingPairs_UnderFullCollision()
    {
        var map = new BiDictionary<int, int, ConstantIntHasher, ConstantIntHasher>(
            capacity: 16, loadFactor: 0.5f);

        for (int i = 1; i <= 80; i++)
            map[i] = i * 7;

        for (int i = 1; i <= 80; i += 2)
            Assert.True(map.Remove(i, out int removed) && removed == i * 7);

        Assert.Equal(40, map.Count);
        for (int i = 2; i <= 80; i += 2)
            Assert.Equal(i, map.GetKey(i * 7));
        for (int i = 1; i <= 80; i += 2)
        {
            Assert.False(map.ContainsKey(i));
            Assert.False(map.ContainsValue(i * 7));
        }

        map.TrimExcess();
        for (int i = 1; i <= 80; i += 2)
            map[i] = i * 7;

        Assert.Equal(80, map.Count);
        for (int i = 1; i <= 80; i++)
            Assert.Equal(i * 7, map[i]);
    }

    [Fact]
    public void ChurnOfInsertRemove_ShouldNotGrowUnbounded_NorCorrupt()
    {
        var map = new BiDictionary<int, int, ConstantIntHasher, ConstantIntHasher>(16);
        int capacity = map.EnsureCapacity(0);
        for (int round = 0; round < 500; round++)
        {
            for (int i = 1; i <= 10; i++)
                map[i] = round * 100 + i;
            for (int i = 1; i <= 10; i++)
            {
                if ((round & 1) == 0)
                    Assert.True(map.Remove(i));
                else
                    Assert.True(map.RemoveValue(round * 100 + i));
            }
            Assert.Empty(map);
        }

        Assert.Equal(capacity, map.EnsureCapacity(0));
        for (int i = 1; i <= 10; i++)
            map[i] = i;
        AssertPairs(map, Enumerable.Range(1, 10).Select(i => (i, i)).ToArray());
    }

    // ---------------------------------------------------------------
    //  Shared-bucket clusters (identity hasher)
    // ---------------------------------------------------------------

    [Fact]
    public void Insert_ShouldChainInOneBucket_AndKeepEveryPairReachable()
    {
        var map = new BiDictionary<int, int, IdentityIntHasher, IdentityIntHasher>(capacity: 16, loadFactor: 0.9f);
        int[] keys = { 16, 32, 48, 1, 17, 33, 49, 2, 18, 34 };
        foreach (int k in keys)
            map[k] = k * 16;

        Assert.Equal(keys.Length, map.Count);
        foreach (int k in keys)
        {
            Assert.True(map.ContainsKey(k));
            Assert.Equal(k * 16, map[k]);
            Assert.Equal(k, map.GetKey(k * 16)); // every value lands in bucket 0
        }
        Assert.Equal(keys, KeysOf(map));
    }

    [Fact]
    public void NegativeLookup_ShouldMiss_OnClusteredKeysAndValues()
    {
        var map = new BiDictionary<int, int, IdentityIntHasher, IdentityIntHasher>(capacity: 16, loadFactor: 0.9f);
        foreach (int k in new[] { 16, 32, 48, 1 })
            map[k] = k + 16;

        Assert.False(map.ContainsKey(64)); // never inserted, same bucket as 16/32/48
        Assert.False(map.ContainsKey(7));  // empty bucket
        Assert.False(map.ContainsValue(80)); // same value bucket as 32/48/64
        Assert.False(map.ContainsValue(7));
        Assert.False(map.TryGetKey(96, out _));
    }

    // ---------------------------------------------------------------
    //  String keys and values — default(string) == null
    // ---------------------------------------------------------------

    [Fact]
    public void StringNullKeyAndValue_ShouldCoexistWithNonNullEntries()
    {
        var map = new BiDictionary<string, string, ConstantStringHasher, ConstantStringHasher>(16);
        map[null!] = "n";
        map["alpha"] = null!;
        map["beta"] = "b";
        map["gamma"] = "g";

        Assert.Equal(4, map.Count);
        Assert.Equal("n", map[null!]);
        Assert.Null(map["alpha"]);
        Assert.Null(map.GetKey("n"));
        Assert.Equal("alpha", map.GetKey(null!));
        Assert.Equal(new[] { null, "alpha", "beta", "gamma" }, map.Keys.ToArray());

        Assert.True(map.Remove(null!)); // slot 0: gamma moves in
        Assert.True(map.RemoveValue(null!));
        Assert.Equal(new[] { "gamma", "beta" }, map.Keys.ToArray());
        Assert.Equal("g", map["gamma"]);
        Assert.Equal("beta", map.GetKey("b"));
    }

    [Fact]
    public void StringKey_Clear_ShouldResetNullKeyAndValue()
    {
        var map = new BiDictionary<string, string, ConstantStringHasher, ConstantStringHasher>();
        map[null!] = "x";
        map["x"] = null!;

        map.Clear();

        Assert.Empty(map);
        Assert.False(map.ContainsKey(null!));
        Assert.False(map.ContainsValue(null!));

        map[null!] = null!;
        Assert.Single(map);
        Assert.Null(map.GetKey(null!));
    }

    [Fact]
    public void StringNullAndHashZeroEntriesShareABucket_WithoutConfusion()
    {
        // Null is stored under hash 0. With a constant hasher of 0, every other key and value lands in
        // the same bucket, so only Equals tells them apart.
        var map = new BiDictionary<string, string, ZeroStringHasher, ZeroStringHasher>(16);
        map["x"] = "X";
        map[null!] = null!;
        map["y"] = "Y";

        Assert.Equal("X", map["x"]);
        Assert.Null(map[null!]);
        Assert.Null(map.GetKey(null!));
        Assert.Equal("y", map.GetKey("Y"));
        Assert.True(map.Remove(null!)); // slot 1: y moves in
        Assert.False(map.ContainsKey(null!));
        Assert.False(map.ContainsValue(null!));
        Assert.Equal("x", map.GetKey("X"));
        Assert.Equal("y", map.GetKey("Y"));
    }

    // ---------------------------------------------------------------
    //  Seeded randomized check against a pair of Dictionary<int,int>. The CsCheck property
    //  (shrinking, full order model) lives in BiDictionaryDifferentialTests.
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(1234)]
    [InlineData(98765)]
    public void RandomizedOps_ShouldMatchADictionaryPair(int seed)
    {
        var rng = new Random(seed);
        var forward = new Dictionary<int, int>();
        var inverse = new Dictionary<int, int>();
        // A small universe on identity hashers keeps chains long and value conflicts frequent.
        var map = new BiDictionary<int, int, IdentityIntHasher, IdentityIntHasher>(capacity: 16, loadFactor: 0.75f);

        for (int step = 0; step < 5000; step++)
        {
            int key = rng.Next(0, 64);
            int value = rng.Next(0, 64);
            switch (rng.Next(0, 4))
            {
                case 0: // force set
                {
                    bool evict = inverse.TryGetValue(value, out int owner) && owner != key;
                    if (evict)
                        forward.Remove(owner);
                    if (forward.TryGetValue(key, out int old))
                        inverse.Remove(old);
                    forward[key] = value;
                    inverse[value] = key;
                    Assert.Equal(evict, map.ForceSet(key, value));
                    break;
                }
                case 1: // remove by key
                {
                    bool removed = forward.Remove(key, out int oVal);
                    if (removed)
                        inverse.Remove(oVal);
                    Assert.Equal(removed, map.Remove(key, out int mVal));
                    Assert.Equal(oVal, mVal);
                    break;
                }
                case 2: // remove by value
                {
                    bool removed = inverse.Remove(value, out int oKey);
                    if (removed)
                        forward.Remove(oKey);
                    Assert.Equal(removed, map.RemoveValue(value, out int mKey));
                    Assert.Equal(oKey, mKey);
                    break;
                }
                case 3: // lookups
                {
                    bool present = forward.TryGetValue(key, out int expected);
                    Assert.Equal(present, map.TryGetValue(key, out int actual));
                    Assert.Equal(expected, actual);
                    present = inverse.TryGetValue(value, out int expectedKey);
                    Assert.Equal(present, map.TryGetKey(value, out int actualKey));
                    Assert.Equal(expectedKey, actualKey);
                    break;
                }
            }

            Assert.Equal(forward.Count, map.Count);
        }

        foreach (var kvp in forward)
            Assert.Equal(kvp.Value, map[kvp.Key]);
        foreach (var kvp in inverse)
            Assert.Equal(kvp.Value, map.GetKey(kvp.Key));
        foreach (var kvp in map)
            Assert.Equal(forward[kvp.Key], kvp.Value);
    }
}
