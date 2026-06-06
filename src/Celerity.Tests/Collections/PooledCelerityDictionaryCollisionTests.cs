using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Mirror of <see cref="CelerityDictionaryCollisionTests"/> for
/// <see cref="PooledCelerityDictionary{TKey, TValue, THasher}"/>: maximum hash
/// collision pressure, reference-type (null) keys, and the backward-shift
/// deletion cyclic-comparison branches — all over rented backing arrays.
/// </summary>
public class PooledCelerityDictionaryCollisionTests
{
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 42;
    }

    private struct ConstantStringHasher : IHashProvider<string>
    {
        public int Hash(string key) => 7;
    }

    private struct IdentityIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => key;
    }

    [Fact]
    public void Insert_ShouldSucceed_UnderFullCollision()
    {
        using var map = new PooledCelerityDictionary<int, string, ConstantIntHasher>(16);
        for (int i = 1; i <= 10; i++)
            map[i] = $"v{i}";

        Assert.Equal(10, map.Count);
        for (int i = 1; i <= 10; i++)
            Assert.Equal($"v{i}", map[i]);
    }

    [Fact]
    public void Overwrite_ShouldSucceed_UnderFullCollision()
    {
        using var map = new PooledCelerityDictionary<int, int, ConstantIntHasher>(16);
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
        using var map = new PooledCelerityDictionary<int, int, ConstantIntHasher>(16);
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
        using var map = new PooledCelerityDictionary<int, int, ConstantIntHasher>(8);

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
        using var map = new PooledCelerityDictionary<int, string, ConstantIntHasher>(16);
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
        using var map = new PooledCelerityDictionary<int, int, ConstantIntHasher>(
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
        using var map = new PooledCelerityDictionary<int, int, ConstantIntHasher>(
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

    [Fact]
    public void RemoveThenReinsert_ManyKeys_ShouldNotLoseEntries()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>(8);
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

    [Fact]
    public void StringKey_Null_ShouldRoundTrip()
    {
        using var map = new PooledCelerityDictionary<string, int, ConstantStringHasher>();
        map[null!] = 99;

        Assert.True(map.ContainsKey(null!));
        Assert.Equal(99, map[null!]);
        Assert.Single(map);
    }

    [Fact]
    public void StringKey_Null_ShouldCoexistWithNonNullKeys()
    {
        using var map = new PooledCelerityDictionary<string, int, ConstantStringHasher>(16);
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
        using var map = new PooledCelerityDictionary<string, string, ConstantStringHasher>(16);
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
        using var map = new PooledCelerityDictionary<string, string, ConstantStringHasher>();
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
        using var map = new PooledCelerityDictionary<string, int, ConstantStringHasher>();
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
    public void Remove_WrapAroundCluster_KeepsBypassEntriesPut_AndShiftsTheRest()
    {
        using var map = new PooledCelerityDictionary<int, int, IdentityIntHasher>(capacity: 8, loadFactor: 0.9f);
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
        using var map = new PooledCelerityDictionary<int, int, IdentityIntHasher>(capacity: 8, loadFactor: 0.9f);
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

    [Fact]
    public void RandomizedOps_ShouldMatchBclDictionary()
    {
        // Differential test against a BCL Dictionary oracle over a deliberately
        // tiny key universe (forcing dense collisions, resizes, and backward-shift
        // removes) — stresses the rent/return machinery under churn.
        foreach (int seed in new[] { 1, 7, 13, 99 })
        {
            var rand = new Random(seed);
            using var map = new PooledCelerityDictionary<int, int, ConstantIntHasher>(4, 0.5f);
            var oracle = new Dictionary<int, int>();

            for (int op = 0; op < 5000; op++)
            {
                int key = rand.Next(0, 40);
                switch (rand.Next(3))
                {
                    case 0:
                        int val = rand.Next();
                        map[key] = val;
                        oracle[key] = val;
                        break;
                    case 1:
                        Assert.Equal(oracle.Remove(key), map.Remove(key));
                        break;
                    default:
                        bool expected = oracle.TryGetValue(key, out int ev);
                        bool actual = map.TryGetValue(key, out int av);
                        Assert.Equal(expected, actual);
                        if (expected)
                            Assert.Equal(ev, av);
                        break;
                }

                Assert.Equal(oracle.Count, map.Count);
            }
        }
    }
}
