using BenchmarkDotNet.Attributes;
using Celerity.Ring;

/// <summary>
/// <see cref="StringMaglevHash{TNode}"/> against the package's own <see cref="StringConsistentHashRing{TNode}"/> —
/// the comparison issue #602's kill criterion was set against (at least 3x on <c>GetNode</c> at 100 and 1,000
/// nodes). Both route the same keys with the same <c>StringXxHash3Hasher</c>; the ring keeps its default 160
/// virtual nodes per node and Maglev follows the sizing rule <c>MaglevHashBenchmark</c> uses. The charted sweep
/// against a hand-rolled ring is <c>MaglevHashBenchmark</c>; this one rides the extended suite, because a second
/// Celerity arm per op would collide with Maglev's on the dashboard.
/// </summary>
[MemoryDiagnoser]
public class MaglevVsRingBenchmark
{
    private const int Lookups = 4096;

    [Params(100, 1000)]
    public int ItemCount { get; set; }

    private StringConsistentHashRing<string> ring = null!;
    private StringMaglevHash<string> maglev = null!;
    private string[] keys = null!;

    [GlobalSetup]
    public void Setup()
    {
        ring = new StringConsistentHashRing<string>();
        maglev = new StringMaglevHash<string>(ItemCount <= 100 ? 65_537 : 100_003);
        for (int i = 0; i < ItemCount; i++)
        {
            string id = $"backend-{i:D4}.internal";
            ring.Add(id, id);
            maglev.Add(id, id);
        }

        keys = new string[Lookups];
        var rand = new Random(42);
        for (int i = 0; i < Lookups; i++)
            keys[i] = $"session:{rand.Next():x8}:{rand.Next():x8}";
    }

    [Benchmark(Baseline = true)]
    public int ConsistentHashRing_GetNode()
    {
        int acc = 0;
        foreach (string key in keys)
            acc += ring.GetNode(key).Length;
        return acc;
    }

    [Benchmark]
    public int MaglevHash_GetNode()
    {
        int acc = 0;
        foreach (string key in keys)
            acc += maglev.GetNode(key).Length;
        return acc;
    }
}
