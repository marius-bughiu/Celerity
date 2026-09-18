namespace Celerity.Ring.Tests;

/// <summary>
/// Regression suite for <see href="https://github.com/marius-bughiu/Celerity/issues/459">#459</see>: a node's
/// virtual-node positions (and <see cref="RendezvousHash{TNode, TKey, THasher}"/>'s weight sub-labels) must not
/// be correlated with another node's.
/// </summary>
/// <remarks>
/// The original placement fed the finalizer an arithmetic progression <c>h, h + φ, h + 2φ, …</c> of the node's
/// 32-bit hash. Two nodes whose hashes happened to sit <c>j·φ</c> apart then shared <c>V − |j|</c> of their
/// <c>V</c> positions outright, every tie went to the ordinally smaller id, and the other node was left with a
/// few percent of its fair share — deterministically, on every process. The pairs below are real node ids
/// taken from the issue whose hashes are exactly that close; each test first asserts the offset, so it keeps
/// meaning what it says if the node hasher ever changes.
/// </remarks>
public class VirtualNodePlacementTests
{
    private const uint GoldenRatio = 0x9E3779B9u;
    private const int KeyCount = 100_000;

    // Hashes 5·φ apart (issue table, row 1).
    private const string NearA = "node-136091";
    private const string NearB = "node-89892";

    // Hashes 8·φ apart (issue table, row 2).
    private const string NearC = "node-84541";
    private const string NearD = "node-177779";

    private static int StepOffset(string first, string second)
    {
        StringXxHash3Hasher hasher = default;
        uint a = (uint)hasher.Hash(first);
        uint b = (uint)hasher.Hash(second);
        for (int j = -200; j <= 200; j++)
        {
            if (unchecked(a + (uint)j * GoldenRatio) == b)
                return j;
        }

        return int.MaxValue;
    }

    private static Dictionary<string, int> Shares(Func<string, string> route)
    {
        var shares = new Dictionary<string, int>();
        for (int i = 0; i < KeyCount; i++)
        {
            string owner = route($"user:{i}");
            shares[owner] = shares.GetValueOrDefault(owner) + 1;
        }

        return shares;
    }

    [Theory]
    [InlineData(NearA, NearB, 5)]
    [InlineData(NearC, NearD, 8)]
    public void TestPairs_ShouldHaveHashesAFewGoldenRatioStepsApart(string first, string second, int expected)
    {
        Assert.Equal(expected, Math.Abs(StepOffset(first, second)));
    }

    [Theory]
    [InlineData(NearA, NearB)]
    [InlineData(NearC, NearD)]
    public void Ring_NodesWithNearbyHashes_ShouldEachOwnAFairShare(string first, string second)
    {
        var ring = new StringConsistentHashRing<string>();
        ring.Add(first, first);
        ring.Add(second, second);

        Dictionary<string, int> shares = Shares(ring.GetNode);

        // Fair share is 50%; with 160 independent virtual nodes each, the spread is a few percent. The
        // correlated placement left the tie-losing node with about 3%.
        Assert.InRange(shares.GetValueOrDefault(first), KeyCount * 35 / 100, KeyCount * 65 / 100);
        Assert.InRange(shares.GetValueOrDefault(second), KeyCount * 35 / 100, KeyCount * 65 / 100);
    }

    [Theory]
    [InlineData(NearA, NearB)]
    [InlineData(NearC, NearD)]
    public void Rendezvous_WeightedNodesWithNearbyHashes_ShouldEachOwnAFairShare(string first, string second)
    {
        // Weight sub-labels were drawn from the same progression, so two weight-40 nodes 5 steps apart shared
        // 35 of their 40 sub-labels and the tie-losing node kept about 13% of the keys.
        var pool = new StringRendezvousHash<string>();
        pool.Add(first, first, weight: 40);
        pool.Add(second, second, weight: 40);

        Dictionary<string, int> shares = Shares(pool.GetNode);

        Assert.InRange(shares.GetValueOrDefault(first), KeyCount * 45 / 100, KeyCount * 55 / 100);
        Assert.InRange(shares.GetValueOrDefault(second), KeyCount * 45 / 100, KeyCount * 55 / 100);
    }
}
