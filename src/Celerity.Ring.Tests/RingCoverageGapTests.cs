namespace Celerity.Ring.Tests;

/// <summary>
/// Pins the corners of <see cref="ConsistentHashRing{TNode, TKey, THasher}"/> and
/// <see cref="RendezvousHash{TNode, TKey, THasher}"/> that the behavioural suites in
/// <c>ConsistentHashRingTests</c>, <c>RendezvousHashTests</c>, and <c>DeterminismTests</c> leave untested:
/// the <em>successful</em> half of the <c>TryGetNode</c> pair, the degenerate replica requests
/// (<c>count == 0</c>, an empty ring, a negative count), the clockwise walk's wrap off the end of the ring
/// array back to slot 0, the pool's argument guards and membership accessors, and the snapshot rebuild that
/// runs when the <em>last</em> node leaves.
/// </summary>
/// <remarks>
/// <para>
/// These are not line-coverage busywork: each one is a contract a sharded fleet actually leans on. The
/// wrap-around case is the whole point of a <em>ring</em> — a key that hashes past the last virtual node must
/// fall through to the first, not off the end — and it only shows up when the replica walk is asked for the
/// full node set from a start slot that is not slot&#160;0. The "last node removed" case matters because the
/// rebuild takes an early-return path that republishes the empty snapshot; if it left the previous topology
/// published, a drained ring would keep routing keys to nodes that are gone. And the empty-result paths must
/// return an empty list rather than throw, because callers use replica counts as a configured constant.
/// </para>
/// <para>
/// Where the topology makes the answer unique — a single-node ring, or a two-node pool after one removal —
/// these tests assert the exact node identity rather than mere membership, matching the package's selling
/// point that assignment is a pure, reproducible function of the node set.
/// </para>
/// </remarks>
public class RingCoverageGapTests
{
    private static StringConsistentHashRing<string> RingOf(params string[] nodeIds)
    {
        var ring = new StringConsistentHashRing<string>();
        foreach (string id in nodeIds)
            ring.Add(id, id);
        return ring;
    }

    private static StringRendezvousHash<string> PoolOf(params string[] nodeIds)
    {
        var pool = new StringRendezvousHash<string>();
        foreach (string id in nodeIds)
            pool.Add(id, id);
        return pool;
    }

    // Every rotation of `order`, joined, so a walk result can be checked for "is the ring order, started
    // somewhere".
    private static HashSet<string> RotationsOf(IReadOnlyList<string> order)
    {
        var rotations = new HashSet<string>(StringComparer.Ordinal);
        for (int start = 0; start < order.Count; start++)
        {
            var rotated = new string[order.Count];
            for (int step = 0; step < order.Count; step++)
                rotated[step] = order[(start + step) % order.Count];
            rotations.Add(string.Join(",", rotated));
        }

        return rotations;
    }

    // ---------------------------------------------------------------------------------------------------
    // ConsistentHashRing
    // ---------------------------------------------------------------------------------------------------

    /// <summary>The populated-ring success path of <c>TryGetNode</c>: true, a real owner, and the same answer as
    /// <c>GetNode</c> on every repeat.</summary>
    [Fact]
    public void TryGetNode_ShouldReturnTrueAndTheOwningNode_WhenTheRingHasNodes()
    {
        // A one-node ring has exactly one possible answer, so the identity is pinned exactly.
        var solo = RingOf("only");
        Assert.True(solo.TryGetNode("any-key", out string soloNode));
        Assert.Equal("only", soloNode);

        string[] nodeIds = { "alpha", "beta", "gamma" };
        var ring = RingOf(nodeIds);
        var twin = RingOf(nodeIds.Reverse().ToArray());

        for (int i = 0; i < 500; i++)
        {
            string key = $"key-{i}";

            Assert.True(ring.TryGetNode(key, out string owner));
            Assert.Contains(owner, nodeIds);

            // Same answer as the throwing overload, stable across repeat calls, and identical on an
            // independently built ring holding the same node set.
            Assert.Equal(ring.GetNode(key), owner);
            Assert.True(ring.TryGetNode(key, out string again));
            Assert.Equal(owner, again);
            Assert.True(twin.TryGetNode(key, out string twinOwner));
            Assert.Equal(owner, twinOwner);
        }
    }

    /// <summary>A replica request that resolves to zero nodes yields an empty list rather than throwing.</summary>
    [Fact]
    public void GetReplicas_ShouldReturnEmpty_WhenTheRingReplicaRequestResolvesToZero()
    {
        var ring = RingOf("a", "b", "c");

        // count == 0 on a populated ring.
        Assert.Empty(ring.GetReplicas("some-key", 0));

        // An empty ring clamps any count down to zero and takes the same path.
        var empty = new StringConsistentHashRing<string>();
        Assert.Empty(empty.GetReplicas("some-key", 0));
        Assert.Empty(empty.GetReplicas("some-key", 5));

        // The zero-count request is not destructive: routing still works afterwards.
        Assert.Contains(ring.GetNode("some-key"), new[] { "a", "b", "c" });
    }

    /// <summary>The clockwise replica walk wraps off the end of the ring array back to slot 0.</summary>
    [Fact]
    public void GetReplicas_ShouldWrapPastTheLastRingPosition_WhenAskedForEveryNode()
    {
        // With one virtual node per node the ring array is exactly one slot per node, so asking for the full
        // node set forces the walk to visit every slot: the result is the fixed ring order rotated to start at
        // the key's owner. Any key whose owner is not slot 0 therefore runs off the end of the array and can
        // only complete by wrapping around to the front.
        string[] nodeIds = { "n0", "n1", "n2", "n3", "n4", "n5", "n6", "n7" };
        var ring = new StringConsistentHashRing<string>(virtualNodesPerNode: 1);
        foreach (string id in nodeIds)
            ring.Add(id, id);

        Assert.Equal(nodeIds.Length, ring.VirtualNodeCount);

        HashSet<string> ringOrderRotations = RotationsOf(ring.GetReplicas("key-0", nodeIds.Length));
        var observed = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < 500; i++)
        {
            string key = $"key-{i}";
            IReadOnlyList<string> replicas = ring.GetReplicas(key, nodeIds.Length);

            // Exactly the distinct node set, primary first...
            Assert.Equal(nodeIds.Length, replicas.Count);
            Assert.Equal(nodeIds, replicas.OrderBy(x => x, StringComparer.Ordinal).ToArray());
            Assert.Equal(ring.GetNode(key), replicas[0]);

            // ...and in ring order: the walk is always a rotation of the one fixed clockwise sequence.
            string joined = string.Join(",", replicas);
            Assert.Contains(joined, ringOrderRotations);
            observed.Add(joined);
        }

        // More than one distinct rotation means at least one of these keys started at a slot other than 0, and
        // a full 8-slot walk from a non-zero start is only expressible by wrapping around the end of the ring.
        Assert.InRange(observed.Count, 2, nodeIds.Length);
    }

    /// <summary>Removing the last node republishes an empty ring: no owner, no virtual nodes, and no stale
    /// routing to the departed nodes.</summary>
    [Fact]
    public void Remove_ShouldLeaveTheRingEmptyAndUnroutable_WhenTheLastNodeIsRemoved()
    {
        var ring = RingOf("a", "b", "c");
        Assert.Equal(3, ring.NodeCount);

        Assert.True(ring.Remove("a"));
        Assert.True(ring.Remove("b"));
        Assert.True(ring.Remove("c"));

        Assert.Equal(0, ring.NodeCount);
        Assert.Equal(0, ring.VirtualNodeCount);
        Assert.False(ring.Contains("a"));

        // "No owner" is signalled by a false return from TryGetNode and a throw from GetNode — the drained ring
        // must not keep handing out the nodes that just left.
        Assert.False(ring.TryGetNode("some-key", out string? node));
        Assert.Null(node);
        Assert.Throws<InvalidOperationException>(() => ring.GetNode("some-key"));
        Assert.Empty(ring.GetReplicas("some-key", 3));

        // The ring is emptied, not wedged: refilling it routes again, and to the only node there is.
        ring.Add("fresh", "fresh");
        Assert.Equal(1, ring.NodeCount);
        Assert.Equal("fresh", ring.GetNode("some-key"));
        Assert.Equal(new[] { "fresh" }, ring.GetReplicas("some-key", 3));
    }

    // ---------------------------------------------------------------------------------------------------
    // RendezvousHash
    // ---------------------------------------------------------------------------------------------------

    /// <summary><c>NodeCount</c> reports physical members only — it follows adds and removes and is unaffected
    /// by node weight or by a removal that matched nothing.</summary>
    [Fact]
    public void NodeCount_ShouldTrackAddsAndRemoves_WhenNodesJoinAndLeaveThePool()
    {
        var pool = new StringRendezvousHash<string>();
        Assert.Equal(0, pool.NodeCount);

        pool.Add("a", "a");
        Assert.Equal(1, pool.NodeCount);

        // Weight buys sub-labels, not membership: a weight-5 node is still one node.
        pool.Add("b", "b", weight: 5);
        Assert.Equal(2, pool.NodeCount);

        Assert.False(pool.Remove("never-added"));
        Assert.Equal(2, pool.NodeCount);

        Assert.True(pool.Remove("a"));
        Assert.Equal(1, pool.NodeCount);

        Assert.True(pool.Remove("b"));
        Assert.Equal(0, pool.NodeCount);
    }

    /// <summary>A weight below 1 is rejected before the node joins the pool.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Add_ShouldThrowArgumentOutOfRange_WhenWeightIsBelowOne(int weight)
    {
        var pool = PoolOf("existing");

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => pool.Add("rejected", "rejected", weight));
        Assert.Equal("weight", ex.ParamName);
        Assert.Equal(weight, Assert.IsType<int>(ex.ActualValue));

        // The rejected node never joined, and the pool still routes to the node that did.
        Assert.False(pool.Contains("rejected"));
        Assert.Equal(1, pool.NodeCount);
        Assert.Equal("existing", pool.GetNode("some-key"));
    }

    /// <summary>Removing an identity the pool never held reports false and changes nothing.</summary>
    [Fact]
    public void Remove_ShouldReturnFalseAndLeaveRoutingIntact_WhenTheNodeWasNeverAdded()
    {
        var pool = PoolOf("a", "b");
        string ownerBefore = pool.GetNode("some-key");

        Assert.False(pool.Remove("never-added"));
        Assert.False(pool.Remove("A")); // identities are ordinal, so case matters.
        Assert.Equal(2, pool.NodeCount);
        Assert.Equal(ownerBefore, pool.GetNode("some-key"));

        // A real removal reports true, and removing the same identity a second time reports false.
        Assert.True(pool.Remove("a"));
        Assert.False(pool.Remove("a"));
        Assert.Equal(1, pool.NodeCount);

        // Only "b" is left, so every key routes there — an exact identity, not just "something".
        Assert.Equal("b", pool.GetNode("some-key"));
    }

    /// <summary><c>Contains</c> reports membership by ordinal identity and rejects a null identity.</summary>
    [Fact]
    public void Contains_ShouldReportMembership_WhenTheNodeIsPresentOrAbsent()
    {
        var pool = PoolOf("a", "b");

        Assert.True(pool.Contains("a"));
        Assert.True(pool.Contains("b"));
        Assert.False(pool.Contains("c"));
        Assert.False(pool.Contains("A"));
        Assert.False(pool.Contains(string.Empty));

        Assert.True(pool.Remove("a"));
        Assert.False(pool.Contains("a"));
        Assert.True(pool.Contains("b"));

        var ex = Assert.Throws<ArgumentNullException>(() => pool.Contains(null!));
        Assert.Equal("nodeId", ex.ParamName);
    }

    /// <summary>The populated-pool success path of <c>TryGetNode</c>: true, a real owner, and the same answer as
    /// <c>GetNode</c> on every repeat.</summary>
    [Fact]
    public void TryGetNode_ShouldReturnTrueAndTheOwningNode_WhenThePoolHasNodes()
    {
        // A one-node pool has exactly one possible answer.
        var solo = PoolOf("only");
        Assert.True(solo.TryGetNode("any-key", out string soloNode));
        Assert.Equal("only", soloNode);

        string[] nodeIds = { "alpha", "beta", "gamma", "delta" };
        var pool = PoolOf(nodeIds);
        var twin = PoolOf(nodeIds.Reverse().ToArray());

        for (int i = 0; i < 500; i++)
        {
            string key = $"tenant:{i}";

            Assert.True(pool.TryGetNode(key, out string owner));
            Assert.Contains(owner, nodeIds);

            // Agrees with GetNode and with the top of the ranked preference list, is stable on repeat, and an
            // independently built pool over the same node set picks the same owner.
            Assert.Equal(pool.GetNode(key), owner);
            Assert.Equal(pool.GetReplicas(key, 1)[0], owner);
            Assert.True(pool.TryGetNode(key, out string again));
            Assert.Equal(owner, again);
            Assert.True(twin.TryGetNode(key, out string twinOwner));
            Assert.Equal(owner, twinOwner);
        }
    }

    /// <summary>A negative replica count is rejected outright.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void GetReplicas_ShouldThrowArgumentOutOfRange_WhenCountIsNegative(int count)
    {
        var pool = PoolOf("a", "b");

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => pool.GetReplicas("some-key", count));
        Assert.Equal("count", ex.ParamName);
        Assert.Equal(count, Assert.IsType<int>(ex.ActualValue));

        // The rejected call left the pool routable.
        Assert.Equal(2, pool.NodeCount);
        Assert.Contains(pool.GetNode("some-key"), new[] { "a", "b" });
    }

    /// <summary>A replica request that resolves to zero nodes yields an empty list rather than throwing.</summary>
    [Fact]
    public void GetReplicas_ShouldReturnEmpty_WhenThePoolReplicaRequestResolvesToZero()
    {
        var pool = PoolOf("a", "b", "c");

        // count == 0 on a populated pool.
        Assert.Empty(pool.GetReplicas("some-key", 0));

        // An empty pool clamps any count down to zero and takes the same path.
        var empty = new StringRendezvousHash<string>();
        Assert.Empty(empty.GetReplicas("some-key", 0));
        Assert.Empty(empty.GetReplicas("some-key", 4));

        // Still routable afterwards.
        Assert.Equal(3, pool.GetReplicas("some-key", 3).Count);
    }

    /// <summary>Removing the last node republishes an empty pool: no owner, and no stale routing to the departed
    /// nodes.</summary>
    [Fact]
    public void Remove_ShouldLeaveThePoolEmptyAndUnroutable_WhenTheLastNodeIsRemoved()
    {
        var pool = PoolOf("a", "b", "c");
        Assert.Equal(3, pool.NodeCount);

        Assert.True(pool.Remove("a"));
        Assert.True(pool.Remove("b"));
        Assert.True(pool.Remove("c"));

        Assert.Equal(0, pool.NodeCount);
        Assert.False(pool.Contains("a"));

        Assert.False(pool.TryGetNode("some-key", out string? node));
        Assert.Null(node);
        Assert.Throws<InvalidOperationException>(() => pool.GetNode("some-key"));
        Assert.Empty(pool.GetReplicas("some-key", 3));

        // Drained, not wedged: refilling routes again, and to the only node there is.
        pool.Add("fresh", "fresh");
        Assert.Equal(1, pool.NodeCount);
        Assert.Equal("fresh", pool.GetNode("some-key"));
        Assert.Equal(new[] { "fresh" }, pool.GetReplicas("some-key", 3));
    }
}
