namespace Celerity.Ring.Tests;

/// <summary>
/// Pins the replica reads of <see cref="ConsistentHashRing{TNode, TKey, THasher}"/> and
/// <see cref="RendezvousHash{TNode, TKey, THasher}"/>: that the span-destination overload writes exactly what the
/// list overload returns, that both still produce the replica sets the original implementation produced, and that
/// the span form does not allocate.
/// </summary>
/// <remarks>
/// <para>
/// The oracle here is <c>GetNode</c>, whose code the replica paths do not share. Both routing schemes define
/// the <c>i</c>-th replica as the node that would own the key once the first <c>i - 1</c> were gone: on the ring,
/// removing a node deletes its virtual nodes, so the key falls through to the next distinct owner clockwise;
/// under HRW a score depends only on the (node, key) pair, so the next-highest scorer takes over. Removing each
/// replica in turn from an identical twin and asking <c>GetNode</c> must therefore walk the replica list exactly,
/// ties included, because removal preserves the ordinal order that breaks them.
/// </para>
/// <para>
/// The golden sets were captured from the implementation before the span overload existed, so a change to
/// either selection algorithm that reorders a replica set fails here rather than silently re-homing data in a
/// fleet that computes replicas on two different versions.
/// </para>
/// </remarks>
public class ReplicaSpanTests
{
    private const string Sentinel = "untouched";

    // Ten nodes n0..n9, with n3 at weight 3 so weighted placement and weighted scoring are both exercised.
    private static StringConsistentHashRing<string> WeightedRing()
    {
        var ring = new StringConsistentHashRing<string>();
        for (int i = 0; i < 10; i++)
            ring.Add($"n{i}", $"n{i}", i == 3 ? 3 : 1);
        return ring;
    }

    private static StringRendezvousHash<string> WeightedPool(int nodeCount = 10)
    {
        var pool = new StringRendezvousHash<string>();
        for (int i = 0; i < nodeCount; i++)
            pool.Add($"n{i}", $"n{i}", i == 3 ? 3 : 1);
        return pool;
    }

    private static string[] SpanReplicas(Func<string[], int> getReplicas, int length)
    {
        var buffer = new string[length];
        int written = getReplicas(buffer);
        return buffer.Take(written).ToArray();
    }

    // ---------------------------------------------------------------------------------------------------
    // Golden replica sets
    // ---------------------------------------------------------------------------------------------------

    /// <summary>The ring's replica sets are unchanged from the original implementation, through both overloads.</summary>
    [Theory]
    [InlineData("user:42", "n3", "n5", "n8", "n9")]
    [InlineData("tenant:7", "n9", "n3", "n2", "n4")]
    [InlineData("session:0", "n3", "n5", "n2", "n1")]
    [InlineData("a", "n9", "n3", "n7", "n8")]
    [InlineData("", "n3", "n2", "n5", "n0")]
    [InlineData("user:42", "n3", "n5", "n8", "n9", "n6", "n4", "n7", "n1", "n0", "n2")]
    public void Ring_ShouldReturnTheGoldenReplicaSet_ThroughBothOverloads(string key, params string[] expected)
    {
        var ring = WeightedRing();

        Assert.Equal(expected, ring.GetReplicas(key, expected.Length));
        Assert.Equal(expected, SpanReplicas(buffer => ring.GetReplicas(key, buffer), expected.Length));
    }

    /// <summary>The pool's ranked preference lists are unchanged from the original full-sort implementation.</summary>
    [Theory]
    [InlineData("user:42", "n9", "n0", "n7", "n4")]
    [InlineData("tenant:7", "n5", "n4", "n3", "n1")]
    [InlineData("session:0", "n8", "n9", "n4", "n1")]
    [InlineData("a", "n1", "n8", "n3", "n2")]
    [InlineData("", "n6", "n4", "n2", "n7")]
    [InlineData("user:42", "n9", "n0", "n7", "n4", "n5", "n2", "n3", "n8", "n1", "n6")]
    public void Pool_ShouldReturnTheGoldenReplicaSet_ThroughBothOverloads(string key, params string[] expected)
    {
        var pool = WeightedPool();

        Assert.Equal(expected, pool.GetReplicas(key, expected.Length));
        Assert.Equal(expected, SpanReplicas(buffer => pool.GetReplicas(key, buffer), expected.Length));
    }

    // ---------------------------------------------------------------------------------------------------
    // Span overload agrees with the list overload
    // ---------------------------------------------------------------------------------------------------

    /// <summary>For every count from zero past the node count, the span form writes the list form's nodes.</summary>
    [Fact]
    public void Ring_SpanOverload_ShouldWriteTheListOverloadsNodes_ForEveryCount()
    {
        var ring = WeightedRing();
        for (int k = 0; k < 200; k++)
        {
            string key = $"key-{k}";
            for (int count = 0; count <= ring.NodeCount + 2; count++)
                Assert.Equal(ring.GetReplicas(key, count), SpanReplicas(buffer => ring.GetReplicas(key, buffer), count));
        }
    }

    /// <summary>For every count from zero past the node count, the span form writes the list form's nodes.</summary>
    [Fact]
    public void Pool_SpanOverload_ShouldWriteTheListOverloadsNodes_ForEveryCount()
    {
        var pool = WeightedPool();
        for (int k = 0; k < 200; k++)
        {
            string key = $"key-{k}";
            for (int count = 0; count <= pool.NodeCount + 2; count++)
                Assert.Equal(pool.GetReplicas(key, count), SpanReplicas(buffer => pool.GetReplicas(key, buffer), count));
        }
    }

    // ---------------------------------------------------------------------------------------------------
    // The removal oracle
    // ---------------------------------------------------------------------------------------------------

    /// <summary>The ring's i-th replica is the key's owner once the first i - 1 replicas have left.</summary>
    [Fact]
    public void Ring_Replicas_ShouldBeTheSuccessiveOwners_AsEachReplicaIsRemoved()
    {
        var ring = WeightedRing();
        for (int k = 0; k < 50; k++)
        {
            string key = $"key-{k}";
            IReadOnlyList<string> replicas = ring.GetReplicas(key, ring.NodeCount);

            var twin = WeightedRing();
            foreach (string replica in replicas)
            {
                Assert.Equal(replica, twin.GetNode(key));
                Assert.True(twin.Remove(replica));
            }
        }
    }

    /// <summary>The pool's i-th replica is the key's owner once the first i - 1 replicas have left — including
    /// past 32 replicas, where the ranking moves its candidates off the stack.</summary>
    [Theory]
    [InlineData(10)]
    [InlineData(40)]
    public void Pool_Replicas_ShouldBeTheSuccessiveOwners_AsEachReplicaIsRemoved(int nodeCount)
    {
        var pool = WeightedPool(nodeCount);
        for (int k = 0; k < 30; k++)
        {
            string key = $"key-{k}";
            string[] replicas = SpanReplicas(buffer => pool.GetReplicas(key, buffer), nodeCount);
            Assert.Equal(nodeCount, replicas.Length);

            var twin = WeightedPool(nodeCount);
            foreach (string replica in replicas)
            {
                Assert.Equal(replica, twin.GetNode(key));
                Assert.True(twin.Remove(replica));
            }
        }
    }

    /// <summary>Past 1,024 physical nodes the ring rents its seen-set instead of stack-allocating it, with the
    /// same answer.</summary>
    [Fact]
    public void Ring_Replicas_ShouldMatchTheRemovalOracle_WhenTheRingOutgrowsTheStackSeenSet()
    {
        const int NodeCount = 1025;
        var ring = new StringConsistentHashRing<string>(virtualNodesPerNode: 1);
        for (int i = 0; i < NodeCount; i++)
            ring.Add($"node-{i}", $"node-{i}");

        for (int k = 0; k < 100; k++)
        {
            string key = $"key-{k}";
            string[] replicas = SpanReplicas(buffer => ring.GetReplicas(key, buffer), 5);
            Assert.Equal(ring.GetReplicas(key, 5), replicas);
            Assert.Equal(5, replicas.Distinct().Count());
            Assert.Equal(ring.GetNode(key), replicas[0]);
        }

        string[] walked = SpanReplicas(buffer => ring.GetReplicas("key-0", buffer), 5);
        foreach (string replica in walked)
        {
            Assert.Equal(replica, ring.GetNode("key-0"));
            Assert.True(ring.Remove(replica));
        }
    }

    // ---------------------------------------------------------------------------------------------------
    // Degenerate destinations
    // ---------------------------------------------------------------------------------------------------

    /// <summary>An empty buffer asks for nothing, an empty ring writes nothing, and a buffer longer than the node
    /// count is filled to the node count with its tail left as it was.</summary>
    [Fact]
    public void Ring_SpanOverload_ShouldWriteOnlyWhatExists_WhenTheBufferOrRingIsShort()
    {
        var ring = WeightedRing();
        Assert.Equal(0, ring.GetReplicas("some-key", Span<string>.Empty));

        var empty = new StringConsistentHashRing<string>();
        string[] untouched = { Sentinel, Sentinel };
        Assert.Equal(0, empty.GetReplicas("some-key", untouched));
        Assert.All(untouched, node => Assert.Equal(Sentinel, node));

        string[] oversized = Enumerable.Repeat(Sentinel, ring.NodeCount + 3).ToArray();
        Assert.Equal(ring.NodeCount, ring.GetReplicas("some-key", oversized));
        Assert.Equal(ring.GetReplicas("some-key", ring.NodeCount), oversized.Take(ring.NodeCount));
        Assert.All(oversized.Skip(ring.NodeCount), node => Assert.Equal(Sentinel, node));
    }

    /// <summary>An empty buffer asks for nothing, an empty pool writes nothing, and a buffer longer than the node
    /// count is filled to the node count with its tail left as it was.</summary>
    [Fact]
    public void Pool_SpanOverload_ShouldWriteOnlyWhatExists_WhenTheBufferOrPoolIsShort()
    {
        var pool = WeightedPool();
        Assert.Equal(0, pool.GetReplicas("some-key", Span<string>.Empty));

        var empty = new StringRendezvousHash<string>();
        string[] untouched = { Sentinel, Sentinel };
        Assert.Equal(0, empty.GetReplicas("some-key", untouched));
        Assert.All(untouched, node => Assert.Equal(Sentinel, node));

        string[] oversized = Enumerable.Repeat(Sentinel, pool.NodeCount + 3).ToArray();
        Assert.Equal(pool.NodeCount, pool.GetReplicas("some-key", oversized));
        Assert.Equal(pool.GetReplicas("some-key", pool.NodeCount), oversized.Take(pool.NodeCount));
        Assert.All(oversized.Skip(pool.NodeCount), node => Assert.Equal(Sentinel, node));
    }

    // ---------------------------------------------------------------------------------------------------
    // Allocation
    // ---------------------------------------------------------------------------------------------------

    /// <summary>The ring's span overload allocates nothing — the per-request replica read it exists for.</summary>
    [Fact]
    public void Ring_SpanOverload_ShouldNotAllocate()
    {
        var ring = new StringConsistentHashRing<string>();
        for (int i = 0; i < 50; i++)
            ring.Add($"node-{i}", $"node-{i}");
        string[] keys = Enumerable.Range(0, 1000).Select(i => $"key-{i}").ToArray();
        var buffer = new string[3];

        Assert.Equal(3, ring.GetReplicas(keys[0], buffer)); // warm up

        long before = GC.GetAllocatedBytesForCurrentThread();
        int written = 0;
        foreach (string key in keys)
            written += ring.GetReplicas(key, buffer);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(3 * keys.Length, written);
        Assert.Equal(0, allocated);
    }

    /// <summary>The pool's span overload allocates nothing, on the single-replica fast path and up to the 32
    /// candidates the ranking keeps on the stack.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(32)]
    public void Pool_SpanOverload_ShouldNotAllocate(int replicaCount)
    {
        var pool = WeightedPool(50);
        string[] keys = Enumerable.Range(0, 1000).Select(i => $"key-{i}").ToArray();
        var buffer = new string[replicaCount];

        Assert.Equal(replicaCount, pool.GetReplicas(keys[0], buffer)); // warm up

        long before = GC.GetAllocatedBytesForCurrentThread();
        int written = 0;
        foreach (string key in keys)
            written += pool.GetReplicas(key, buffer);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(replicaCount * keys.Length, written);
        Assert.Equal(0, allocated);
    }
}
