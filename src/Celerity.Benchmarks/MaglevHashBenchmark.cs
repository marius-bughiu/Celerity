using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Hashing;
using Celerity.Ring;

/// <summary>
/// <see cref="StringMaglevHash{TNode}"/> against the consistent-hash ring a developer hand-rolls: a sorted
/// <c>uint[]</c> continuum of 160 virtual nodes per node, searched with <see cref="Array.BinarySearch{T}(T[], T)"/>.
/// Both sides hash the key with the same inlined <see cref="StringXxHash3Hasher"/>, so the comparison isolates the
/// structure: Maglev's modulo and one table load against a binary search over <c>160·N</c> positions.
/// </summary>
/// <remarks>
/// <para>
/// <c>ItemCount</c> is the node count, so the ring's search deepens as it grows while Maglev's lookup stays one
/// load. The Maglev table follows the paper's sizing rule (at least 100 slots per node): 65,537 slots up to 100
/// nodes and 100,003 at 1,000.
/// </para>
/// <para>
/// <c>Churn</c> is the cost Maglev pays for that: one node leaves and rejoins. The ring is the careful hand-roll —
/// each node's 160 positions are hashed and sorted once, a removal filters them out and a join merges them back in,
/// two linear passes over fresh arrays — while Maglev refills its whole table twice whatever the node count. It
/// loses there, and is charted so the trade is visible, not hidden.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class MaglevHashBenchmark
{
    private const int VirtualNodesPerNode = 160;
    private const int Lookups = 4096;

    [Params(10, 100, 1000)]
    public int ItemCount { get; set; }

    private StringMaglevHash<string> maglev = null!;
    private string[] nodeIds = null!;
    private string[] keys = null!;
    private uint[][] nodePositions = null!;
    private uint[] ringPositions = null!;
    private string[] ringOwners = null!;
    private StringXxHash3Hasher hasher;

    [GlobalSetup]
    public void Setup()
    {
        nodeIds = new string[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            nodeIds[i] = $"backend-{i:D4}.internal";

        maglev = new StringMaglevHash<string>(ItemCount <= 100 ? 65_537 : 100_003);
        foreach (string id in nodeIds)
            maglev.Add(id, id);

        // Each node's virtual-node positions are hashed and sorted once, as a careful hand-roll caches them.
        nodePositions = new uint[ItemCount][];
        for (int i = 0; i < ItemCount; i++)
        {
            var positions = new uint[VirtualNodesPerNode];
            for (int v = 0; v < VirtualNodesPerNode; v++)
                positions[v] = (uint)hasher.Hash($"{nodeIds[i]}-{v}");
            Array.Sort(positions);
            nodePositions[i] = positions;
        }

        ringPositions = new uint[ItemCount * VirtualNodesPerNode];
        ringOwners = new string[ringPositions.Length];
        for (int i = 0; i < ItemCount; i++)
        {
            nodePositions[i].CopyTo(ringPositions, i * VirtualNodesPerNode);
            ringOwners.AsSpan(i * VirtualNodesPerNode, VirtualNodesPerNode).Fill(nodeIds[i]);
        }

        Array.Sort(ringPositions, ringOwners);

        keys = new string[Lookups];
        var rand = new Random(42);
        for (int i = 0; i < Lookups; i++)
            keys[i] = $"session:{rand.Next():x8}:{rand.Next():x8}";
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("GetNode")]
    public int Array_GetNode()
    {
        int acc = 0;
        foreach (string key in keys)
            acc += RingGetNode(key).Length;
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("GetNode")]
    public int MaglevHash_GetNode()
    {
        int acc = 0;
        foreach (string key in keys)
            acc += maglev.GetNode(key).Length;
        return acc;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Churn")]
    public int Array_Churn()
    {
        // The last node leaves (filter its positions out), then rejoins (merge them back in). Fresh arrays each
        // time, as a lock-free reader needs an immutable snapshot — which is what Maglev publishes too.
        string last = nodeIds[^1];
        int kept = ringPositions.Length - VirtualNodesPerNode;
        var keptPositions = new uint[kept];
        var keptOwners = new string[kept];
        int j = 0;
        for (int i = 0; i < ringPositions.Length; i++)
        {
            if (!ReferenceEquals(ringOwners[i], last))
            {
                keptPositions[j] = ringPositions[i];
                keptOwners[j++] = ringOwners[i];
            }
        }

        uint[] joining = nodePositions[^1];
        var positions = new uint[ringPositions.Length];
        var owners = new string[positions.Length];
        int a = 0, b = 0;
        for (int k = 0; k < positions.Length; k++)
        {
            if (b == joining.Length || (a < kept && keptPositions[a] <= joining[b]))
            {
                positions[k] = keptPositions[a];
                owners[k] = keptOwners[a++];
            }
            else
            {
                positions[k] = joining[b++];
                owners[k] = last;
            }
        }

        ringPositions = positions;
        ringOwners = owners;
        return positions.Length;
    }

    [Benchmark]
    [BenchmarkCategory("Churn")]
    public int MaglevHash_Churn()
    {
        string last = nodeIds[^1];
        maglev.Remove(last);
        maglev.Add(last, last);
        return maglev.NodeCount;
    }

    private string RingGetNode(string key)
    {
        int index = Array.BinarySearch(ringPositions, (uint)hasher.Hash(key));
        if (index < 0)
            index = ~index;
        if (index == ringPositions.Length)
            index = 0; // wrap around the ring
        return ringOwners[index];
    }
}
