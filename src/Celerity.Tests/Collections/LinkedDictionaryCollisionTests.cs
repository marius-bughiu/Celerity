using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests that exercise <see cref="LinkedDictionary{TKey, TValue, THasher}"/> under maximum
/// hash-collision pressure, with reference-type (null-default) keys, and against a
/// <see cref="Dictionary{TKey, TValue}"/> oracle. Mirrors <c>SwissDictionaryCollisionTests</c>; the
/// Swiss-specific group / tombstone cases are replaced by the separate-chaining ones that matter here:
/// unlinking an entry from the head, middle and tail of a bucket chain (by key and from either end
/// of the order), free-list reuse under churn, and growth that relinks every chain.
/// </summary>
/// <remarks>
/// A new entry is pushed onto the <i>front</i> of its bucket's chain, so under a constant hasher the
/// newest entry heads the chain and the oldest sits at its end. <c>TryRemoveFirst</c> on a full
/// collision therefore removes the entry at the far end of the chain, and <c>TryRemoveLast</c> the
/// one at its head.
/// </remarks>
public class LinkedDictionaryCollisionTests
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
    /// A test-only hasher for <see cref="string"/> keys that returns a constant value, forcing full
    /// collision into one chain with equal stored hashes, so every step falls through to Equals.
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

    private static int[] KeysOf<TValue, THasher>(LinkedDictionary<int, TValue, THasher> map)
        where THasher : struct, IHashProvider<int>
    {
        var keys = new List<int>();
        foreach (var kvp in map)
            keys.Add(kvp.Key);
        return keys.ToArray();
    }

    // ---------------------------------------------------------------
    //  Full-collision tests — every key shares one chain
    // ---------------------------------------------------------------

    [Fact]
    public void Insert_ShouldSucceed_UnderFullCollision()
    {
        var map = new LinkedDictionary<int, string, ConstantIntHasher>(16);
        for (int i = 1; i <= 40; i++)
            map[i] = $"v{i}";

        Assert.Equal(40, map.Count);
        for (int i = 1; i <= 40; i++)
            Assert.Equal($"v{i}", map[i]);
        Assert.Equal(Enumerable.Range(1, 40), KeysOf(map));
        Assert.False(map.ContainsKey(41));
    }

    [Fact]
    public void Overwrite_ShouldSucceed_UnderFullCollision()
    {
        var map = new LinkedDictionary<int, int, ConstantIntHasher>(16);
        for (int i = 1; i <= 20; i++)
            map[i] = i;
        for (int i = 1; i <= 20; i++)
            map[i] = i * 100;

        Assert.Equal(20, map.Count);
        for (int i = 1; i <= 20; i++)
            Assert.Equal(i * 100, map[i]);
        Assert.Equal(Enumerable.Range(1, 20), KeysOf(map));
    }

    [Fact]
    public void Remove_ShouldLeaveOthersReachable_UnderFullCollision()
    {
        var map = new LinkedDictionary<int, int, ConstantIntHasher>(64);
        for (int i = 1; i <= 30; i++)
            map[i] = i * 10;

        Assert.True(map.Remove(3)); // mid-chain
        Assert.Equal(29, map.Count);
        Assert.False(map.ContainsKey(3));
        Assert.False(map.Remove(3));

        for (int i = 1; i <= 30; i++)
        {
            if (i == 3) continue;
            Assert.True(map.ContainsKey(i));
            Assert.Equal(i * 10, map[i]);
        }
    }

    [Fact]
    public void Remove_ShouldUnlinkTheChainHeadMiddleAndTail_UnderFullCollision()
    {
        var map = new LinkedDictionary<int, int, ConstantIntHasher>(16);
        for (int i = 1; i <= 6; i++)
            map[i] = i;
        // Chain (head → tail): 6, 5, 4, 3, 2, 1.

        Assert.True(map.Remove(6, out int head)); // chain head: chainPrev == Nil
        Assert.Equal(6, head);
        Assert.True(map.Remove(1, out int tail)); // chain tail: chainPrev != Nil
        Assert.Equal(1, tail);
        Assert.True(map.Remove(3, out int middle));
        Assert.Equal(3, middle);

        Assert.Equal(new[] { 2, 4, 5 }, KeysOf(map));
        foreach (int k in new[] { 2, 4, 5 })
            Assert.Equal(k, map[k]);
        foreach (int k in new[] { 1, 3, 6 })
            Assert.False(map.ContainsKey(k));
    }

    [Fact]
    public void RemoveThenReinsert_ShouldWork_UnderFullCollision()
    {
        var map = new LinkedDictionary<int, int, ConstantIntHasher>(16);

        for (int i = 1; i <= 30; i++)
            map[i] = i;

        for (int i = 1; i <= 30; i += 2)
            Assert.True(map.Remove(i));

        Assert.Equal(15, map.Count);
        int capacity = map.EnsureCapacity(0);

        // Reinserting must reuse the slots the removals put on the free list.
        for (int i = 1; i <= 30; i += 2)
            map[i] = -i;

        Assert.Equal(30, map.Count);
        Assert.Equal(capacity, map.EnsureCapacity(0));
        for (int i = 1; i <= 30; i++)
        {
            int expected = i % 2 == 0 ? i : -i;
            Assert.Equal(expected, map[i]);
        }

        // The re-added odd keys go to the end, after every surviving even key.
        var expectedOrder = Enumerable.Range(1, 30).Where(i => i % 2 == 0)
            .Concat(Enumerable.Range(1, 30).Where(i => i % 2 == 1));
        Assert.Equal(expectedOrder, KeysOf(map));
    }

    [Fact]
    public void DefaultKey_ShouldWorkAlongside_CollisionChain()
    {
        var map = new LinkedDictionary<int, string, ConstantIntHasher>(16);
        map[0] = "zero";
        for (int i = 1; i <= 20; i++)
            map[i] = $"v{i}";

        Assert.Equal(21, map.Count);
        Assert.Equal("zero", map[0]);
        for (int i = 1; i <= 20; i++)
            Assert.Equal($"v{i}", map[i]);
        Assert.Equal(Enumerable.Range(0, 21), KeysOf(map));

        Assert.True(map.Remove(0));
        Assert.Equal(20, map.Count);
        Assert.False(map.ContainsKey(0));
        for (int i = 1; i <= 20; i++)
            Assert.True(map.ContainsKey(i));
    }

    [Fact]
    public void Resize_ShouldPreserveAll_UnderFullCollision()
    {
        var map = new LinkedDictionary<int, int, ConstantIntHasher>(
            capacity: 16, loadFactor: 0.5f);

        for (int i = 1; i <= 60; i++)
            map[i] = i * 10;

        Assert.Equal(60, map.Count);
        for (int i = 1; i <= 60; i++)
            Assert.Equal(i * 10, map[i]);
        Assert.Equal(Enumerable.Range(1, 60), KeysOf(map));
    }

    [Fact]
    public void ResizeThenRemoveSweep_ShouldPreserveAllRemainingKeys_UnderFullCollision()
    {
        var map = new LinkedDictionary<int, int, ConstantIntHasher>(
            capacity: 16, loadFactor: 0.5f);

        for (int i = 1; i <= 80; i++)
            map[i] = i * 7;

        Assert.Equal(80, map.Count);

        for (int i = 1; i <= 80; i += 2)
            Assert.True(map.Remove(i, out int removed) && removed == i * 7);

        Assert.Equal(40, map.Count);
        for (int i = 2; i <= 80; i += 2)
            Assert.Equal(i * 7, map[i]);
        for (int i = 1; i <= 80; i += 2)
            Assert.False(map.ContainsKey(i));

        for (int i = 1; i <= 80; i += 2)
            map[i] = i * 7;

        Assert.Equal(80, map.Count);
        for (int i = 1; i <= 80; i++)
            Assert.Equal(i * 7, map[i]);
    }

    [Fact]
    public void ChurnOfInsertRemove_ShouldNotGrowUnbounded_NorCorrupt()
    {
        // Many insert/remove cycles over a tiny key set: every removal frees a slot for the next
        // insert, so the table must never grow past its first size.
        var map = new LinkedDictionary<int, int, ConstantIntHasher>(16);
        int capacity = map.EnsureCapacity(0);
        for (int round = 0; round < 500; round++)
        {
            for (int i = 1; i <= 10; i++)
                map[i] = round * 100 + i;
            for (int i = 1; i <= 10; i++)
                Assert.True(map.Remove(i));
            Assert.Empty(map);
        }

        Assert.Equal(capacity, map.EnsureCapacity(0));
        for (int i = 1; i <= 10; i++)
            map[i] = i;
        Assert.Equal(10, map.Count);
        for (int i = 1; i <= 10; i++)
            Assert.Equal(i, map[i]);
    }

    // ---------------------------------------------------------------
    //  Ordered removal from either end of a collision chain
    // ---------------------------------------------------------------

    [Fact]
    public void TryRemoveFirst_ShouldUnlinkAnEntryAtTheFarEndOfItsChain()
    {
        // The oldest entry sits at the end of the chain, so TryRemoveFirst must walk the chain
        // to find its predecessor (the non-head arm of the chain unlink).
        var map = new LinkedDictionary<int, int, ConstantIntHasher>(16);
        for (int i = 1; i <= 8; i++)
            map[i] = i * 10;

        for (int expected = 1; expected <= 8; expected++)
        {
            Assert.True(map.TryRemoveFirst(out int key, out int value));
            Assert.Equal(expected, key);
            Assert.Equal(expected * 10, value);
            for (int k = expected + 1; k <= 8; k++)
                Assert.Equal(k * 10, map[k]);
        }

        Assert.Empty(map);
        Assert.False(map.TryRemoveFirst(out _, out _));
    }

    [Fact]
    public void TryRemoveLast_ShouldUnlinkTheHeadOfItsChain()
    {
        var map = new LinkedDictionary<int, int, ConstantIntHasher>(16);
        for (int i = 1; i <= 8; i++)
            map[i] = i * 10;

        for (int expected = 8; expected >= 1; expected--)
        {
            Assert.True(map.TryRemoveLast(out int key, out int value));
            Assert.Equal(expected, key);
            Assert.Equal(expected * 10, value);
            for (int k = 1; k < expected; k++)
                Assert.Equal(k * 10, map[k]);
        }

        Assert.Empty(map);
        Assert.False(map.TryRemoveLast(out _, out _));
    }

    [Fact]
    public void TryRemoveFirst_ShouldWalkPastAnUnrelatedChainMember_AfterReordering()
    {
        // MoveToFront changes the order list, not the chain: after it, the order head (5) is the
        // chain head, and the order tail (4) is mid-chain.
        var map = new LinkedDictionary<int, int, ConstantIntHasher>(16);
        for (int i = 1; i <= 5; i++)
            map[i] = i;
        Assert.True(map.MoveToFront(5));
        Assert.True(map.MoveToEnd(1));
        // Order: 5, 2, 3, 4, 1. Chain: 5, 4, 3, 2, 1.

        Assert.True(map.TryRemoveFirst(out int first, out _));
        Assert.Equal(5, first);
        Assert.True(map.TryRemoveLast(out int last, out _));
        Assert.Equal(1, last);
        Assert.True(map.MoveToEnd(2));
        Assert.True(map.TryRemoveLast(out last, out _)); // 2 sits at the end of the chain
        Assert.Equal(2, last);

        Assert.Equal(new[] { 3, 4 }, KeysOf(map));
        Assert.Equal(3, map[3]);
        Assert.Equal(4, map[4]);
        Assert.False(map.ContainsKey(1));
        Assert.False(map.ContainsKey(2));
        Assert.False(map.ContainsKey(5));
    }

    [Fact]
    public void TryRemoveFirst_ShouldFindTheEntry_InASharedBucketWithDifferentHashes()
    {
        // IdentityIntHasher at 4 buckets: keys 1, 5, 9 share bucket 1 with distinct full hashes.
        var map = new LinkedDictionary<int, int, IdentityIntHasher>(capacity: 4, loadFactor: 0.75f);
        map[1] = 1;
        map[5] = 5;
        map[9] = 9; // chain: 9, 5, 1

        Assert.True(map.TryRemoveFirst(out int key, out _));
        Assert.Equal(1, key);
        Assert.Equal(new[] { 5, 9 }, KeysOf(map));
        Assert.Equal(5, map[5]);
        Assert.Equal(9, map[9]);
    }

    // ---------------------------------------------------------------
    //  Shared-bucket clusters (identity hasher)
    // ---------------------------------------------------------------

    [Fact]
    public void Insert_ShouldChainInOneBucket_AndKeepEveryKeyReachable()
    {
        var map = new LinkedDictionary<int, int, IdentityIntHasher>(capacity: 16, loadFactor: 0.9f);
        int[] keys = { 16, 32, 48, 1, 17, 33, 49, 2, 18, 34 };
        foreach (int k in keys)
            map[k] = k * 11;

        Assert.Equal(keys.Length, map.Count);
        foreach (int k in keys)
        {
            Assert.True(map.ContainsKey(k));
            Assert.Equal(k * 11, map[k]);
        }
        Assert.Equal(keys, KeysOf(map));
    }

    [Fact]
    public void NegativeLookup_ShouldMiss_OnClusteredKeys()
    {
        var map = new LinkedDictionary<int, int, IdentityIntHasher>(capacity: 16, loadFactor: 0.9f);
        foreach (int k in new[] { 16, 32, 48, 1 })
            map[k] = k;

        Assert.False(map.ContainsKey(64)); // never inserted, same bucket as 16/32/48
        Assert.False(map.ContainsKey(80));
        Assert.False(map.ContainsKey(7));  // empty bucket
    }

    // ---------------------------------------------------------------
    //  String keys — default(string) == null and the stored-hash test
    // ---------------------------------------------------------------

    [Fact]
    public void StringKey_Null_ShouldRoundTrip()
    {
        var map = new LinkedDictionary<string, int, ConstantStringHasher>();
        map[null!] = 99;

        Assert.True(map.ContainsKey(null!));
        Assert.Equal(99, map[null!]);
        Assert.Single(map);
    }

    [Fact]
    public void StringKey_Null_ShouldCoexistWithNonNullKeys()
    {
        var map = new LinkedDictionary<string, int, ConstantStringHasher>(16);
        map[null!] = 0;
        map["alpha"] = 1;
        map["beta"] = 2;
        map["gamma"] = 3;

        Assert.Equal(4, map.Count);
        Assert.Equal(0, map[null!]);
        Assert.Equal(1, map["alpha"]);
        Assert.Equal(2, map["beta"]);
        Assert.Equal(3, map["gamma"]);
        Assert.Equal(new[] { null, "alpha", "beta", "gamma" }, map.Keys.ToArray());
    }

    [Fact]
    public void StringKey_Null_Remove_ShouldWork()
    {
        var map = new LinkedDictionary<string, string, ConstantStringHasher>(16);
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
        var map = new LinkedDictionary<string, string, ConstantStringHasher>();
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
        var map = new LinkedDictionary<string, int, ConstantStringHasher>();
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

    [Fact]
    public void StringKey_SharedBucket_ShouldSkipBothHashMismatchesAndKeyMismatches()
    {
        // CacheCollidingStringHasher puts every key in bucket 0 with one of two full hashes by
        // length parity, so a reference-key lookup meets both a stored-hash mismatch (skipped
        // without Equals) and a stored-hash match whose key differs.
        var map = new LinkedDictionary<string, int, CacheCollidingStringHasher>(16);
        string[] keys = { "a", "bb", "ccc", "dddd", "e", "ff" };
        for (int i = 0; i < keys.Length; i++)
            map[keys[i]] = i;

        for (int i = 0; i < keys.Length; i++)
            Assert.Equal(i, map[keys[i]]);
        Assert.False(map.ContainsKey("g"));   // odd length: walks past even-hash and odd-hash entries
        Assert.False(map.ContainsKey("gg"));  // even length

        Assert.True(map.Remove("a"));          // the far end of the chain
        Assert.True(map.Remove("dddd"));       // mid-chain
        Assert.False(map.Remove("zz"));
        Assert.Equal(new[] { "bb", "ccc", "e", "ff" }, map.Keys.ToArray());
    }

    [Fact]
    public void StringKey_NullAndHashZeroKeysShareABucket_WithoutConfusion()
    {
        // The null key is stored under hash 0. With a constant hasher of 0, every other key has
        // the same stored hash, so only Equals tells them apart.
        var map = new LinkedDictionary<string, int, ZeroStringHasher>(16);
        map["x"] = 1;
        map[null!] = 2;
        map["y"] = 3;

        Assert.Equal(1, map["x"]);
        Assert.Equal(2, map[null!]);
        Assert.Equal(3, map["y"]);
        Assert.True(map.Remove(null!));
        Assert.False(map.ContainsKey(null!));
        Assert.Equal(1, map["x"]);
        Assert.Equal(3, map["y"]);
    }

    private struct ZeroStringHasher : IHashProvider<string>
    {
        public int Hash(string key) => 0;
    }

    // ---------------------------------------------------------------
    //  Seeded randomized check against Dictionary<int,int>. The CsCheck property
    //  (shrinking, full order model) lives in LinkedDictionaryDifferentialTests.
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
        // A small key universe on an identity hasher keeps chains long and the free list busy.
        var map = new LinkedDictionary<int, int, IdentityIntHasher>(capacity: 16, loadFactor: 0.75f);

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
