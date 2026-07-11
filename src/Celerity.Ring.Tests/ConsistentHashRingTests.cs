namespace Celerity.Ring.Tests;

public class ConsistentHashRingTests
{
    private static StringConsistentHashRing<string> RingOf(params string[] nodeIds)
    {
        var ring = new StringConsistentHashRing<string>();
        foreach (string id in nodeIds)
            ring.Add(id, id);
        return ring;
    }

    [Fact]
    public void EmptyRing_GetNode_Throws()
    {
        var ring = new StringConsistentHashRing<string>();
        Assert.Throws<InvalidOperationException>(() => ring.GetNode("anything"));
    }

    [Fact]
    public void EmptyRing_TryGetNode_ReturnsFalse()
    {
        var ring = new StringConsistentHashRing<string>();
        Assert.False(ring.TryGetNode("anything", out string? node));
        Assert.Null(node);
    }

    [Fact]
    public void SingleNode_RoutesEveryKeyToIt()
    {
        var ring = RingOf("only");
        for (int i = 0; i < 1000; i++)
            Assert.Equal("only", ring.GetNode($"key-{i}"));
    }

    [Fact]
    public void Add_DuplicateNodeId_Throws()
    {
        var ring = RingOf("a");
        Assert.Throws<ArgumentException>(() => ring.Add("a", "a"));
    }

    [Fact]
    public void Add_NullNodeId_Throws()
    {
        var ring = new StringConsistentHashRing<string>();
        Assert.Throws<ArgumentNullException>(() => ring.Add(null!, "x"));
    }

    [Fact]
    public void Add_NonPositiveWeight_Throws()
    {
        var ring = new StringConsistentHashRing<string>();
        Assert.Throws<ArgumentOutOfRangeException>(() => ring.Add("a", "a", 0));
    }

    [Fact]
    public void Constructor_NonPositiveVirtualNodes_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringConsistentHashRing<string>(0));
    }

    [Fact]
    public void Contains_TracksMembership()
    {
        var ring = RingOf("a", "b");
        Assert.True(ring.Contains("a"));
        Assert.True(ring.Contains("b"));
        Assert.False(ring.Contains("c"));

        Assert.True(ring.Remove("a"));
        Assert.False(ring.Contains("a"));
        Assert.False(ring.Remove("a"));
    }

    [Fact]
    public void Counts_ReflectNodesAndVirtualNodes()
    {
        var ring = new StringConsistentHashRing<string>(virtualNodesPerNode: 50);
        ring.Add("a", "a");
        ring.Add("b", "b", weight: 2);

        Assert.Equal(2, ring.NodeCount);
        Assert.Equal(50, ring.VirtualNodesPerNode);
        // a: 50 vnodes, b (weight 2): 100 vnodes.
        Assert.Equal(150, ring.VirtualNodeCount);
    }

    [Fact]
    public void GetReplicas_ReturnsDistinctNodesPrimaryFirst()
    {
        var ring = RingOf("a", "b", "c", "d");

        IReadOnlyList<string> replicas = ring.GetReplicas("some-key", 3);

        Assert.Equal(3, replicas.Count);
        Assert.Equal(replicas.Count, replicas.Distinct().Count());
        Assert.Equal(ring.GetNode("some-key"), replicas[0]);
    }

    [Fact]
    public void GetReplicas_CountExceedingNodes_ReturnsAllDistinctNodes()
    {
        var ring = RingOf("a", "b", "c");
        IReadOnlyList<string> replicas = ring.GetReplicas("k", 10);
        Assert.Equal(3, replicas.Count);
        Assert.Equal(new[] { "a", "b", "c" }, replicas.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void GetReplicas_NegativeCount_Throws()
    {
        var ring = RingOf("a");
        Assert.Throws<ArgumentOutOfRangeException>(() => ring.GetReplicas("k", -1));
    }

    [Fact]
    public void Distribution_AcrossEqualNodes_IsRoughlyBalanced()
    {
        string[] nodeIds = Enumerable.Range(0, 10).Select(i => $"node-{i}").ToArray();
        var ring = RingOf(nodeIds);

        var counts = new Dictionary<string, int>();
        const int keys = 100_000;
        for (int i = 0; i < keys; i++)
        {
            string owner = ring.GetNode($"key-{i}");
            counts[owner] = counts.GetValueOrDefault(owner) + 1;
        }

        // Every node owns a share; with 160 vnodes/node the spread is modest. Assert each node lands within
        // ±40% of the 10% ideal — loose enough to never flake, tight enough to catch a broken distribution.
        double ideal = keys / 10.0;
        Assert.Equal(10, counts.Count);
        foreach (int count in counts.Values)
        {
            Assert.True(count > ideal * 0.6, $"a node owned only {count} keys (ideal {ideal})");
            Assert.True(count < ideal * 1.4, $"a node owned {count} keys (ideal {ideal})");
        }
    }

    [Fact]
    public void Weight_AttractsProportionallyMoreKeys()
    {
        var ring = new StringConsistentHashRing<string>();
        ring.Add("light", "light", weight: 1);
        ring.Add("heavy", "heavy", weight: 4);

        int heavy = 0;
        const int keys = 100_000;
        for (int i = 0; i < keys; i++)
        {
            if (ring.GetNode($"key-{i}") == "heavy")
                heavy++;
        }

        // Heavy has 4x the virtual nodes, so it should attract roughly 80% of keys. Loose bounds.
        double heavyFraction = heavy / (double)keys;
        Assert.True(heavyFraction > 0.7, $"heavy owned {heavyFraction:P1}, expected ~80%");
        Assert.True(heavyFraction < 0.9, $"heavy owned {heavyFraction:P1}, expected ~80%");
    }

    [Fact]
    public void RemovingANode_RemapsOnlyItsShareOfKeys()
    {
        string[] nodeIds = Enumerable.Range(0, 8).Select(i => $"n{i}").ToArray();
        var ring = RingOf(nodeIds);

        const int keys = 50_000;
        var before = new string[keys];
        for (int i = 0; i < keys; i++)
            before[i] = ring.GetNode($"key-{i}");

        ring.Remove("n3");

        int moved = 0;
        for (int i = 0; i < keys; i++)
        {
            string after = ring.GetNode($"key-{i}");
            if (after != before[i])
                moved++;
            // No key should ever move to the removed node, and keys not owned by n3 should mostly stay put.
            Assert.NotEqual("n3", after);
        }

        // Consistent hashing guarantees only ~1/8 of keys (those owned by n3) move. Allow generous headroom.
        double movedFraction = moved / (double)keys;
        Assert.True(movedFraction < 0.20, $"{movedFraction:P1} of keys moved; consistent hashing expects ~12.5%");
    }

    [Fact]
    public void Add_ThatOverflowsVirtualNodeGuard_ThrowsAndLeavesRingUncorrupted()
    {
        var ring = RingOf("stable");

        // A weight this large blows past the total virtual-node cap; the Add must fail atomically.
        Assert.Throws<InvalidOperationException>(() => ring.Add("oversized", "oversized", weight: 100_000_000));

        // The failed Add left no trace: the bad node is neither registered nor routable, and the ring still works.
        Assert.False(ring.Contains("oversized"));
        Assert.Equal(1, ring.NodeCount);
        Assert.Equal("stable", ring.GetNode("any-key"));

        // And the ring is not wedged — a normal Add still succeeds afterwards.
        ring.Add("second", "second");
        Assert.Equal(2, ring.NodeCount);
    }

    [Fact]
    public void Works_WithIntegerKeysAndCustomHasher()
    {
        var ring = new ConsistentHashRing<string, long, Int64WangNaiveHasher>();
        ring.Add("shard-a", "shard-a");
        ring.Add("shard-b", "shard-b");
        ring.Add("shard-c", "shard-c");

        // Every key routes to a real node and does so stably.
        for (long k = 0; k < 1000; k++)
        {
            string first = ring.GetNode(k);
            Assert.Contains(first, new[] { "shard-a", "shard-b", "shard-c" });
            Assert.Equal(first, ring.GetNode(k));
        }
    }
}
