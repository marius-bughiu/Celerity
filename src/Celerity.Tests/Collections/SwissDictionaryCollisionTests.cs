using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests that exercise <see cref="SwissDictionary{TKey, TValue, THasher}"/>
/// under maximum hash-collision pressure, with reference-type (null-default)
/// keys, and against a <see cref="Dictionary{TKey, TValue}"/> oracle. The parts
/// unique to Swiss-table probing — SIMD group scanning that overflows a single
/// group, tombstone deletion that must not terminate a later lookup, and the
/// tombstone-reclaiming rehash — get the heaviest coverage here.
/// </summary>
public class SwissDictionaryCollisionTests
{
    /// <summary>
    /// A test-only hasher that returns a constant value for every key, forcing
    /// every insert into the same home group so the probe overflows into
    /// subsequent groups (the multi-group probe path).
    /// </summary>
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 42;
    }

    /// <summary>
    /// A test-only hasher for <see cref="string"/> keys that returns a constant
    /// value, forcing full collision into one home group.
    /// </summary>
    private struct ConstantStringHasher : IHashProvider<string>
    {
        public int Hash(string key) => 7;
    }

    /// <summary>
    /// A test-only hasher that returns the key itself, so the home group is a
    /// predictable function of the key — used to build clusters that straddle
    /// group boundaries, the shape that drives the group-overflow probe.
    /// </summary>
    private struct IdentityIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => key;
    }

    // ---------------------------------------------------------------
    //  Full-collision tests — every key shares a home group
    // ---------------------------------------------------------------

    [Fact]
    public void Insert_ShouldSucceed_UnderFullCollision()
    {
        var map = new SwissDictionary<int, string, ConstantIntHasher>(16);
        for (int i = 1; i <= 40; i++)
            map[i] = $"v{i}";

        Assert.Equal(40, map.Count);
        for (int i = 1; i <= 40; i++)
            Assert.Equal($"v{i}", map[i]);
    }

    [Fact]
    public void Overwrite_ShouldSucceed_UnderFullCollision()
    {
        var map = new SwissDictionary<int, int, ConstantIntHasher>(16);
        for (int i = 1; i <= 20; i++)
            map[i] = i;
        for (int i = 1; i <= 20; i++)
            map[i] = i * 100;

        Assert.Equal(20, map.Count);
        for (int i = 1; i <= 20; i++)
            Assert.Equal(i * 100, map[i]);
    }

    [Fact]
    public void Remove_ShouldLeaveOthersReachable_UnderFullCollision()
    {
        var map = new SwissDictionary<int, int, ConstantIntHasher>(64);
        for (int i = 1; i <= 30; i++)
            map[i] = i * 10;

        Assert.True(map.Remove(3));
        Assert.Equal(29, map.Count);
        Assert.False(map.ContainsKey(3));

        for (int i = 1; i <= 30; i++)
        {
            if (i == 3) continue;
            Assert.True(map.ContainsKey(i));
            Assert.Equal(i * 10, map[i]);
        }
    }

    [Fact]
    public void RemoveThenReinsert_ShouldWork_UnderFullCollision()
    {
        var map = new SwissDictionary<int, int, ConstantIntHasher>(16);

        for (int i = 1; i <= 30; i++)
            map[i] = i;

        for (int i = 1; i <= 30; i += 2)
            Assert.True(map.Remove(i));

        Assert.Equal(15, map.Count);

        // Reinserting must reuse the tombstones the removals left behind.
        for (int i = 1; i <= 30; i += 2)
            map[i] = -i;

        Assert.Equal(30, map.Count);
        for (int i = 1; i <= 30; i++)
        {
            int expected = i % 2 == 0 ? i : -i;
            Assert.Equal(expected, map[i]);
        }
    }

    [Fact]
    public void DefaultKey_ShouldWorkAlongside_CollisionChain()
    {
        var map = new SwissDictionary<int, string, ConstantIntHasher>(16);
        map[0] = "zero";
        for (int i = 1; i <= 20; i++)
            map[i] = $"v{i}";

        Assert.Equal(21, map.Count);
        Assert.Equal("zero", map[0]);
        for (int i = 1; i <= 20; i++)
            Assert.Equal($"v{i}", map[i]);

        Assert.True(map.Remove(0));
        Assert.Equal(20, map.Count);
        Assert.False(map.ContainsKey(0));
        for (int i = 1; i <= 20; i++)
            Assert.True(map.ContainsKey(i));
    }

    [Fact]
    public void Resize_ShouldPreserveAll_UnderFullCollision()
    {
        var map = new SwissDictionary<int, int, ConstantIntHasher>(
            capacity: 16, loadFactor: 0.5f);

        for (int i = 1; i <= 60; i++)
            map[i] = i * 10;

        Assert.Equal(60, map.Count);
        for (int i = 1; i <= 60; i++)
            Assert.Equal(i * 10, map[i]);
    }

    [Fact]
    public void ResizeThenRemoveSweep_ShouldPreserveAllRemainingKeys_UnderFullCollision()
    {
        var map = new SwissDictionary<int, int, ConstantIntHasher>(
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

    // ---------------------------------------------------------------
    //  Tombstone-specific behaviour
    // ---------------------------------------------------------------

    [Fact]
    public void Lookup_ShouldFindKey_PastATombstone_UnderFullCollision()
    {
        // Fill one home group so it overflows, then delete an early entry: its
        // slot becomes a tombstone (the group has no empty), and a later resident
        // must still be reachable by probing *past* the tombstone.
        var map = new SwissDictionary<int, int, ConstantIntHasher>(16);
        for (int i = 1; i <= 20; i++)
            map[i] = i;

        Assert.True(map.Remove(2));   // leaves a tombstone in the full home group
        Assert.True(map.Remove(5));

        for (int i = 1; i <= 20; i++)
        {
            if (i == 2 || i == 5)
            {
                Assert.False(map.ContainsKey(i));
                continue;
            }
            Assert.True(map.ContainsKey(i));
            Assert.Equal(i, map[i]);
        }
    }

    [Fact]
    public void ChurnOfInsertRemove_ShouldNotGrowUnbounded_NorCorrupt()
    {
        // Many insert/remove cycles over a tiny key set stress the
        // tombstone-reclaiming rehash; correctness must hold throughout.
        var map = new SwissDictionary<int, int, ConstantIntHasher>(16);
        for (int round = 0; round < 500; round++)
        {
            for (int i = 1; i <= 10; i++)
                map[i] = round * 100 + i;
            for (int i = 1; i <= 10; i++)
                Assert.True(map.Remove(i));
            Assert.Empty(map);
        }

        for (int i = 1; i <= 10; i++)
            map[i] = i;
        Assert.Equal(10, map.Count);
        for (int i = 1; i <= 10; i++)
            Assert.Equal(i, map[i]);
    }

    [Fact]
    public void TombstoneHeavyResize_ShouldRehashInPlace_NotDouble()
    {
        // Drives the same-size (tombstone-reclaiming) rehash branch of Resize:
        // a resize triggered while the *live* count is below half the threshold
        // must rehash at the current capacity rather than doubling, so churn that
        // accumulates tombstones cannot grow the table without bound.
        //
        // With IdentityIntHasher and capacity 32 there are two SIMD groups
        // (group = (key >> 7) & 1, threshold 24). Keys 128..143 all land in
        // group 1; keys 1..9 all land in group 0.
        var map = new SwissDictionary<int, int, IdentityIntHasher>(capacity: 32, loadFactor: 0.75f);

        // Fill group 1 completely, then delete it all: because the group is full,
        // every erase leaves a DELETED tombstone (16 of them), not an empty slot.
        for (int k = 128; k <= 143; k++)
            map[k] = k;
        for (int k = 128; k <= 143; k++)
            Assert.True(map.Remove(k));
        Assert.Equal(0, map.Count);

        // Now fill group 0 (no tombstones on its probe path, so these consume
        // empty slots and drain the growth budget). The insert that finds the
        // budget exhausted triggers a resize while only ~8 entries are live —
        // below threshold/2 — so the in-place rehash path runs and reclaims the
        // 16 group-1 tombstones instead of doubling to capacity 64.
        for (int k = 1; k <= 9; k++)
            map[k] = k * 10;

        Assert.Equal(9, map.Count);
        for (int k = 1; k <= 9; k++)
            Assert.Equal(k * 10, map[k]);
        for (int k = 128; k <= 143; k++)
            Assert.False(map.ContainsKey(k));

        // The reclaimed table still works: refill it past the original threshold.
        for (int k = 200; k <= 240; k++)
            map[k] = k;
        Assert.Equal(50, map.Count);
        for (int k = 1; k <= 9; k++)
            Assert.Equal(k * 10, map[k]);
        for (int k = 200; k <= 240; k++)
            Assert.Equal(k, map[k]);
    }

    // ---------------------------------------------------------------
    //  Group-straddling clusters (identity hasher)
    // ---------------------------------------------------------------

    [Fact]
    public void Insert_ShouldOverflowGroup_AndKeepEveryKeyReachable()
    {
        // Identity hasher: keys 0..40 land in home groups by their high bits, but
        // a dense run forces several groups to fill and the probe to overflow.
        var map = new SwissDictionary<int, int, IdentityIntHasher>(capacity: 16, loadFactor: 0.9f);
        int[] keys = { 16, 32, 48, 1, 17, 33, 49, 2, 18, 34 };
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
    public void NegativeLookup_ShouldMiss_OnClusteredKeys()
    {
        var map = new SwissDictionary<int, int, IdentityIntHasher>(capacity: 16, loadFactor: 0.9f);
        foreach (int k in new[] { 16, 32, 48, 1 })
            map[k] = k;

        Assert.False(map.ContainsKey(64)); // never inserted
        Assert.False(map.ContainsKey(80));
        Assert.False(map.ContainsKey(7));
    }

    // ---------------------------------------------------------------
    //  String keys — exercises default(string) == null path
    // ---------------------------------------------------------------

    [Fact]
    public void StringKey_Null_ShouldRoundTrip()
    {
        var map = new SwissDictionary<string, int, ConstantStringHasher>();
        map[null!] = 99;

        Assert.True(map.ContainsKey(null!));
        Assert.Equal(99, map[null!]);
        Assert.Single(map);
    }

    [Fact]
    public void StringKey_Null_ShouldCoexistWithNonNullKeys()
    {
        var map = new SwissDictionary<string, int, ConstantStringHasher>(16);
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
        var map = new SwissDictionary<string, string, ConstantStringHasher>(16);
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
        var map = new SwissDictionary<string, string, ConstantStringHasher>();
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
        var map = new SwissDictionary<string, int, ConstantStringHasher>();
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
    //  correctness check on the group-probe / tombstone machinery.
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
        // Small key universe forces collisions, group overflows, and dense
        // tombstone reuse far more than a sparse key space would.
        var map = new SwissDictionary<int, int, IdentityIntHasher>(capacity: 16, loadFactor: 0.75f);

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
