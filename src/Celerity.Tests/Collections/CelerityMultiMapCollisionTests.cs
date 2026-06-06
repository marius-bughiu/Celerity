using System.Collections.Generic;
using System.Linq;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Exercises <see cref="CelerityMultiMap{TKey, TValue, THasher}"/> under maximum
/// hash-collision pressure (every key probes the same chain) and with the
/// backward-shift deletion that runs when a key's last value is removed. Mirrors
/// <c>CelerityDictionaryCollisionTests</c> for the grouping semantics: a single
/// linear-probing cluster must keep every key's value group distinct, and
/// collapsing one key must not orphan the others sharing its chain.
/// </summary>
public class CelerityMultiMapCollisionTests
{
    /// <summary>A test-only hasher that returns a constant for every int key.</summary>
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 42;
    }

    /// <summary>A test-only hasher that returns a constant for every string key.</summary>
    private struct ConstantStringHasher : IHashProvider<string>
    {
        public int Hash(string key) => 7;
    }

    /// <summary>A test-only hasher returning the key itself (predictable slots).</summary>
    private struct IdentityIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => key;
    }

    [Fact]
    public void Add_ShouldKeepGroupsDistinct_UnderFullCollision()
    {
        var map = new CelerityMultiMap<int, string, ConstantIntHasher>(16);
        for (int i = 1; i <= 10; i++)
        {
            map.Add(i, $"v{i}a");
            map.Add(i, $"v{i}b");
        }

        Assert.Equal(10, map.Count);
        Assert.Equal(20, map.ValueCount);
        for (int i = 1; i <= 10; i++)
            Assert.Equal(new[] { $"v{i}a", $"v{i}b" }, map[i].ToArray());
    }

    [Fact]
    public void RemoveAll_FromMiddleOfCluster_ShouldNotOrphanOtherKeys()
    {
        var map = new CelerityMultiMap<int, string, ConstantIntHasher>(16);
        for (int i = 1; i <= 8; i++)
            map.Add(i, $"v{i}");

        // Collapse a key in the middle of the probe cluster.
        Assert.True(map.RemoveAll(4));

        Assert.Equal(7, map.Count);
        Assert.False(map.ContainsKey(4));
        for (int i = 1; i <= 8; i++)
        {
            if (i == 4) continue;
            Assert.True(map.ContainsKey(i));
            Assert.Equal(new[] { $"v{i}" }, map[i].ToArray());
        }
    }

    [Fact]
    public void Remove_LastValue_ShouldBackwardShift_UnderFullCollision()
    {
        var map = new CelerityMultiMap<int, string, ConstantIntHasher>(16);
        for (int i = 1; i <= 6; i++)
            map.Add(i, $"v{i}");

        // Remove the only value of key 3 — triggers backward-shift deletion.
        Assert.True(map.Remove(3, "v3"));
        Assert.False(map.ContainsKey(3));
        Assert.Equal(5, map.Count);

        // Re-add 3; it must find a slot and stay distinct from the others.
        map.Add(3, "v3-new");
        Assert.Equal(new[] { "v3-new" }, map[3].ToArray());
        Assert.Equal(6, map.Count);
        for (int i = 1; i <= 6; i++)
            Assert.True(map.ContainsKey(i));
    }

    [Fact]
    public void StringKeys_ShouldGroupCorrectly_UnderFullCollision()
    {
        var map = new CelerityMultiMap<string, int, ConstantStringHasher>(16);
        string[] keys = { "alpha", "beta", "gamma", "delta", "epsilon" };
        foreach (string k in keys)
        {
            map.Add(k, 1);
            map.Add(k, 2);
        }

        Assert.Equal(keys.Length, map.Count);
        foreach (string k in keys)
            Assert.Equal(new[] { 1, 2 }, map[k].ToArray());

        // Remove all from one wrapped-cluster key, others must survive.
        Assert.True(map.RemoveAll("gamma"));
        Assert.False(map.ContainsKey("gamma"));
        foreach (string k in keys.Where(k => k != "gamma"))
            Assert.Equal(new[] { 1, 2 }, map[k].ToArray());
    }

    [Fact]
    public void WrappedCluster_RemoveAll_ShouldPreserveAllSurvivors()
    {
        // IdentityIntHasher + a known table size builds a cluster that wraps the
        // end of the array, exercising the wrapped branch of backward-shift.
        var map = new CelerityMultiMap<int, int, IdentityIntHasher>(8); // size 8, mask 7
        int[] keys = { 6, 7, 14, 15, 22 }; // all land near the high end and wrap
        foreach (int k in keys)
        {
            map.Add(k, k);
            map.Add(k, k * 10);
        }

        Assert.True(map.RemoveAll(7));
        Assert.False(map.ContainsKey(7));
        foreach (int k in keys)
        {
            if (k == 7) continue;
            Assert.True(map.ContainsKey(k));
            Assert.Equal(new[] { k, k * 10 }, map[k].ToArray());
        }
    }

    [Fact]
    public void NullKeyGroup_ShouldCoexistWithCollidingStringKeys()
    {
        var map = new CelerityMultiMap<string, int, ConstantStringHasher>(16);
        map.Add(null!, 100);
        map.Add(null!, 200);
        for (int i = 1; i <= 5; i++)
            map.Add($"k{i}", i);

        Assert.Equal(6, map.Count);
        Assert.Equal(new[] { 100, 200 }, map[null!].ToArray());

        Assert.True(map.RemoveAll(null!));
        Assert.False(map.ContainsKey(null!));
        Assert.Equal(5, map.Count);
        for (int i = 1; i <= 5; i++)
            Assert.Equal(new[] { i }, map[$"k{i}"].ToArray());
    }
}
