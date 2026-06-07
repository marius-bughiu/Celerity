using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests that exercise <see cref="HashCachingDictionary{TKey, TValue, THasher}"/>
/// under maximum hash collision pressure and with reference-type keys whose
/// default is <c>null</c>. HashCachingDictionary shares CelerityDictionary's
/// linear-probing + backward-shift machinery, layering a cached-fingerprint side
/// array on top, so it must satisfy the same correctness contract — including the
/// fingerprint short-circuit never letting a colliding key be missed (issue #65).
/// </summary>
public class HashCachingDictionaryCollisionTests
{
    /// <summary>
    /// A test-only hasher that returns a constant value for every key,
    /// forcing every insert into a single linear-probing chain — and, because
    /// every key then shares the same cached fingerprint, the worst case for the
    /// fingerprint filter (every probe falls through to the full key compare).
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
    /// A test-only hasher that returns the key itself. Combined with a
    /// power-of-two table size, <c>key &amp; mask</c> places each key at a
    /// predictable slot, which lets a test build a wrapped cluster whose
    /// entries have different natural slots — the shape needed to exercise
    /// every branch of the backward-shift cyclic comparison.
    /// </summary>
    private struct IdentityIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => key;
    }

    // ---------------------------------------------------------------
    //  Int-key collision tests (same shape as IntDictionary tests)
    // ---------------------------------------------------------------

    [Fact]
    public void Insert_ShouldSucceed_UnderFullCollision()
    {
        var map = new HashCachingDictionary<int, string, ConstantIntHasher>(16);
        for (int i = 1; i <= 10; i++)
            map[i] = $"v{i}";

        Assert.Equal(10, map.Count);
        for (int i = 1; i <= 10; i++)
            Assert.Equal($"v{i}", map[i]);
    }

    [Fact]
    public void Overwrite_ShouldSucceed_UnderFullCollision()
    {
        var map = new HashCachingDictionary<int, int, ConstantIntHasher>(16);
        for (int i = 1; i <= 5; i++)
            map[i] = i;
        for (int i = 1; i <= 5; i++)
            map[i] = i * 100;

        Assert.Equal(5, map.Count);
        for (int i = 1; i <= 5; i++)
            Assert.Equal(i * 100, map[i]);
    }

    [Fact]
    public void Remove_ShouldRehashCluster_UnderFullCollision()
    {
        var map = new HashCachingDictionary<int, int, ConstantIntHasher>(16);
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
        var map = new HashCachingDictionary<int, int, ConstantIntHasher>(8);

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
        var map = new HashCachingDictionary<int, string, ConstantIntHasher>(16);
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
        var map = new HashCachingDictionary<int, int, ConstantIntHasher>(
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
        // Bulk-insert past the load-factor threshold to force multiple Resize
        // calls under a single forced-collision chain — each resize re-homes
        // entries straight from their cached fingerprints without rehashing —
        // then remove every other key to drive BackwardShiftRemove through long
        // clusters, then reinsert and verify.
        var map = new HashCachingDictionary<int, int, ConstantIntHasher>(
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
    //  Remove-then-reinsert with the standard hasher (parity with
    //  IntDictionaryTests.Remove_Then_Reinsert_ManyKeys)
    // ---------------------------------------------------------------

    [Fact]
    public void RemoveThenReinsert_ManyKeys_ShouldNotLoseEntries()
    {
        var map = new HashCachingDictionary<int, int, Int32WangNaiveHasher>(8);
        for (int i = 1; i <= 100; i++)
            map[i] = i;

        for (int i = 1; i <= 100; i += 2)
            Assert.True(map.Remove(i));

        Assert.Equal(50, map.Count);

        for (int i = 1; i <= 100; i += 2)
            map[i] = -i;

        Assert.Equal(100, map.Count);
        for (int i = 1; i <= 100; i++)
        {
            int expected = i % 2 == 0 ? i : -i;
            Assert.Equal(expected, map[i]);
        }
    }

    // ---------------------------------------------------------------
    //  String keys — exercises default(string) == null path
    // ---------------------------------------------------------------

    [Fact]
    public void StringKey_Null_ShouldRoundTrip()
    {
        var map = new HashCachingDictionary<string, int, ConstantStringHasher>();
        map[null!] = 99;

        Assert.True(map.ContainsKey(null!));
        Assert.Equal(99, map[null!]);
        Assert.Single(map);
    }

    [Fact]
    public void StringKey_Null_ShouldCoexistWithNonNullKeys()
    {
        var map = new HashCachingDictionary<string, int, ConstantStringHasher>(16);
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
        var map = new HashCachingDictionary<string, string, ConstantStringHasher>(16);
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
        var map = new HashCachingDictionary<string, string, ConstantStringHasher>();
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
        var map = new HashCachingDictionary<string, int, ConstantStringHasher>();
        map[null!] = 1;
        map["x"] = 2;

        map.Clear();

        Assert.Empty(map);
        Assert.False(map.ContainsKey(null!));
        Assert.False(map.ContainsKey("x"));

        // Reusable after clear.
        map[null!] = 10;
        Assert.Single(map);
        Assert.Equal(10, map[null!]);
    }

    [Fact]
    public void Remove_WrapAroundCluster_KeepsBypassEntriesPut_AndShiftsTheRest()
    {
        // Regression for the backward-shift deletion (BackwardShiftRemove). With
        // table size 8 (mask = 7) and the identity hasher, keys 6, 7, 8, 14 build
        // a cluster that crosses the array boundary:
        //   slot 6 -> 6  (natural 6)
        //   slot 7 -> 7  (natural 7)
        //   slot 0 -> 8  (natural 0; collided through 6, 7)
        //   slot 1 -> 14 (natural 6; displaced through 6, 7, 0)
        // Removing key 6 must SKIP slots 7 and 0 (bypass cases for the
        // `i <= j` and `i > j` branches respectively) and SHIFT slot 1 into
        // the gap. The cached fingerprint supplies each candidate's natural
        // slot, so the shift logic never rehashes.
        var map = new HashCachingDictionary<int, int, IdentityIntHasher>(capacity: 8, loadFactor: 0.9f);
        map[6] = 60;
        map[7] = 70;
        map[8] = 80;
        map[14] = 140;

        Assert.True(map.Remove(6));

        Assert.Equal(3, map.Count);
        Assert.False(map.ContainsKey(6));
        Assert.Equal(70, map[7]);
        Assert.Equal(80, map[8]);
        Assert.Equal(140, map[14]);
    }

    [Fact]
    public void Remove_WrapAroundCluster_WithHomeAboveGap_ExercisesWrappedKeepBranch()
    {
        // Complements the test above. Table size 8 (mask = 7), identity hasher.
        // Keys 6, 7, 15, 23 land as:
        //   slot 6 -> 6  (natural 6) — removed, becomes the gap at i = 6
        //   slot 7 -> 7  (natural 7)
        //   slot 0 -> 15 (natural 7; wrapped past 7)
        //   slot 1 -> 23 (natural 7; wrapped past 7, 0)
        // Removing key 6 scans the wrapped slots 0 and 1, whose natural slot (7)
        // is GREATER than the gap (6), so the `i > j` bypass takes its `i < k`
        // == true path — the branch the all-homes-below-the-gap cluster misses.
        var map = new HashCachingDictionary<int, int, IdentityIntHasher>(capacity: 8, loadFactor: 0.9f);
        map[6] = 60;
        map[7] = 70;
        map[15] = 150;
        map[23] = 230;

        Assert.True(map.Remove(6));

        Assert.Equal(3, map.Count);
        Assert.False(map.ContainsKey(6));
        Assert.Equal(70, map[7]);
        Assert.Equal(150, map[15]);
        Assert.Equal(230, map[23]);
    }

    // ---------------------------------------------------------------
    //  Fingerprint-collision stress — keys whose hashes share low bits but
    //  differ in the high bits the fingerprint keeps, so the filter must
    //  still discriminate them and never miss a key.
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
        // Small key universe forces collisions, long clusters, and dense
        // shift/reinsert reuse far more than a sparse key space would.
        var map = new HashCachingDictionary<int, int, IdentityIntHasher>(capacity: 16, loadFactor: 0.75f);

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
