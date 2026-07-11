namespace Celerity.Ring.Tests;

public class RendezvousHashTests
{
    private static StringRendezvousHash<string> PoolOf(params string[] nodeIds)
    {
        var pool = new StringRendezvousHash<string>();
        foreach (string id in nodeIds)
            pool.Add(id, id);
        return pool;
    }

    [Fact]
    public void EmptyPool_GetNode_Throws()
    {
        var pool = new StringRendezvousHash<string>();
        Assert.Throws<InvalidOperationException>(() => pool.GetNode("x"));
    }

    [Fact]
    public void EmptyPool_TryGetNode_ReturnsFalse()
    {
        var pool = new StringRendezvousHash<string>();
        Assert.False(pool.TryGetNode("x", out string? node));
        Assert.Null(node);
    }

    [Fact]
    public void SingleNode_RoutesEveryKeyToIt()
    {
        var pool = PoolOf("only");
        for (int i = 0; i < 500; i++)
            Assert.Equal("only", pool.GetNode($"key-{i}"));
    }

    [Fact]
    public void Add_DuplicateNodeId_Throws()
    {
        var pool = PoolOf("a");
        Assert.Throws<ArgumentException>(() => pool.Add("a", "a"));
    }

    [Fact]
    public void Distribution_AcrossEqualNodes_IsRoughlyBalanced()
    {
        string[] nodeIds = Enumerable.Range(0, 8).Select(i => $"node-{i}").ToArray();
        var pool = PoolOf(nodeIds);

        var counts = new Dictionary<string, int>();
        const int keys = 80_000;
        for (int i = 0; i < keys; i++)
        {
            string owner = pool.GetNode($"key-{i}");
            counts[owner] = counts.GetValueOrDefault(owner) + 1;
        }

        // HRW scores are per-(node,key), so equal nodes split traffic evenly. Assert within ±20% of 1/8.
        double ideal = keys / 8.0;
        Assert.Equal(8, counts.Count);
        foreach (int count in counts.Values)
        {
            Assert.True(count > ideal * 0.8, $"a node owned only {count} keys (ideal {ideal})");
            Assert.True(count < ideal * 1.2, $"a node owned {count} keys (ideal {ideal})");
        }
    }

    [Fact]
    public void Weight_AttractsProportionallyMoreKeys()
    {
        var pool = new StringRendezvousHash<string>();
        pool.Add("light", "light", weight: 1);
        pool.Add("heavy", "heavy", weight: 3);

        int heavy = 0;
        const int keys = 80_000;
        for (int i = 0; i < keys; i++)
        {
            if (pool.GetNode($"key-{i}") == "heavy")
                heavy++;
        }

        double heavyFraction = heavy / (double)keys;
        // ~3/4 of keys to the weight-3 node. Loose bounds around 75%.
        Assert.True(heavyFraction > 0.66, $"heavy owned {heavyFraction:P1}, expected ~75%");
        Assert.True(heavyFraction < 0.84, $"heavy owned {heavyFraction:P1}, expected ~75%");
    }

    [Fact]
    public void RemovingANode_RemapsOnlyItsOwnKeys()
    {
        string[] nodeIds = Enumerable.Range(0, 6).Select(i => $"n{i}").ToArray();
        var pool = PoolOf(nodeIds);

        const int keys = 40_000;
        var before = new string[keys];
        for (int i = 0; i < keys; i++)
            before[i] = pool.GetNode($"key-{i}");

        pool.Remove("n2");

        for (int i = 0; i < keys; i++)
        {
            string after = pool.GetNode($"key-{i}");
            Assert.NotEqual("n2", after);
            // HRW's defining property: a key whose owner was NOT removed keeps that owner exactly.
            if (before[i] != "n2")
                Assert.Equal(before[i], after);
        }
    }

    [Fact]
    public void GetReplicas_ReturnsRankedDistinctNodes()
    {
        var pool = PoolOf("a", "b", "c", "d", "e");

        IReadOnlyList<string> replicas = pool.GetReplicas("some-key", 3);

        Assert.Equal(3, replicas.Count);
        Assert.Equal(replicas.Count, replicas.Distinct().Count());
        Assert.Equal(pool.GetNode("some-key"), replicas[0]);
    }

    [Fact]
    public void GetReplicas_IsPrefixConsistentAsCountGrows()
    {
        var pool = PoolOf("a", "b", "c", "d", "e");

        IReadOnlyList<string> two = pool.GetReplicas("k", 2);
        IReadOnlyList<string> four = pool.GetReplicas("k", 4);

        // The ranking is total, so a larger replica set extends the smaller one.
        Assert.Equal(two, four.Take(2).ToList());
    }
}
