using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using BenchmarkDotNet.Attributes;
using Celerity.Ring;

/// <summary>
/// <see cref="StringConsistentHashRing{TNode}"/> <c>GetNode</c> throughput against the ring a developer
/// hand-rolls before reaching for a library: a sorted <c>uint[]</c> continuum keyed by MD5 (the classic
/// libketama recipe). Both sides do the same O(log v) binary-search ceiling lookup, so the comparison
/// isolates the per-lookup hash — Celerity hashes the key through an inlined, allocation-free
/// <c>StringXxHash3Hasher</c>, while the MD5 baseline hashes a freshly-allocated UTF-8 byte array on
/// every call. The <c>Allocated</c> column is the headline: the Celerity ring routes with zero
/// per-lookup allocation. (Its real reason to exist — a node assignment that is byte-identical across
/// OS / architecture / runtime — a throughput benchmark can't show.) Isolated microbenchmark, so it
/// lives in the extended suite, not the per-PR core gate.
/// </summary>
[MemoryDiagnoser]
public class RingBenchmark
{
    private const int Nodes = 50;
    private const int VirtualNodesPerNode = 160;
    private const int Lookups = 4096;

    private StringConsistentHashRing<string> ring = null!;
    private uint[] baselineHashes = null!;
    private string[] baselineNodes = null!;
    private string[] keys = null!;

    [GlobalSetup]
    public void Setup()
    {
        var nodeIds = new string[Nodes];
        for (int i = 0; i < Nodes; i++)
            nodeIds[i] = $"cache-node-{i:D2}.internal";

        ring = new StringConsistentHashRing<string>(VirtualNodesPerNode);
        foreach (var id in nodeIds)
            ring.Add(id, id);

        // Baseline ring: MD5-hashed virtual nodes on a flat sorted continuum (the textbook hand-roll).
        var entries = new List<(uint Hash, string Node)>(Nodes * VirtualNodesPerNode);
        foreach (var id in nodeIds)
            for (int v = 0; v < VirtualNodesPerNode; v++)
                entries.Add((Md5ToUint($"{id}-{v}"), id));
        entries.Sort((a, b) => a.Hash.CompareTo(b.Hash));

        baselineHashes = new uint[entries.Count];
        baselineNodes = new string[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            baselineHashes[i] = entries[i].Hash;
            baselineNodes[i] = entries[i].Node;
        }

        keys = new string[Lookups];
        var rand = new Random(42);
        for (int i = 0; i < Lookups; i++)
            keys[i] = $"session:{rand.Next():x8}:{rand.Next():x8}";
    }

    [Benchmark(Baseline = true)]
    public int Md5Ring_GetNode()
    {
        int acc = 0;
        foreach (var key in keys)
            acc += BaselineGetNode(key).Length;
        return acc;
    }

    [Benchmark]
    public int CelerityRing_GetNode()
    {
        int acc = 0;
        foreach (var key in keys)
            acc += ring.GetNode(key).Length;
        return acc;
    }

    private string BaselineGetNode(string key)
    {
        uint h = Md5ToUint(key);
        int idx = Array.BinarySearch(baselineHashes, h);
        if (idx < 0)
            idx = ~idx;
        if (idx >= baselineHashes.Length)
            idx = 0; // wrap around the ring
        return baselineNodes[idx];
    }

    private static uint Md5ToUint(string s)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(s); // per-call allocation, as the naive hand-roll writes it
        Span<byte> digest = stackalloc byte[16];
        MD5.HashData(bytes, digest);
        return BinaryPrimitives.ReadUInt32BigEndian(digest);
    }
}
