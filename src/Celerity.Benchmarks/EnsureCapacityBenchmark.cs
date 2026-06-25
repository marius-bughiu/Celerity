using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

/// <summary>
/// Measures the payoff of pre-sizing a collection with <c>EnsureCapacity</c> before a bulk insert of a
/// known size (issue #231). An unsized open-addressed table starts small and rehashes its whole contents
/// every time it doubles — so building <c>n</c> entries pays <c>O(log n)</c> full re-hashes on top of the
/// <c>n</c> inserts. <c>EnsureCapacity(n)</c> grows the table once up front, so the fill pays exactly
/// <c>n</c> inserts and zero resizes.
/// </summary>
/// <remarks>
/// Each category pairs the unsized build (baseline) against the <c>EnsureCapacity</c>-pre-sized build for
/// the same type, so the ratio reads directly as "how much the single up-front grow saves over incremental
/// doubling". Both a BCL <see cref="Dictionary{TKey, TValue}"/> and Celerity's <see cref="IntDictionary{TValue}"/>
/// are shown so the win is visible against the type a developer is most likely replacing. This is an isolated
/// build microbenchmark, so it lives in the <strong>extended</strong> suite, not the per-PR core gate.
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class EnsureCapacityBenchmark
{
    private int[] keys = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        var rand = new Random(42);
        for (int i = 0; i < ItemCount; i++)
            keys[i] = rand.Next(1, int.MaxValue);
    }

    // ── BCL Dictionary<int,int> ─────────────────────────────────────────────────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Dictionary")]
    public int Dictionary_Insert_Unsized()
    {
        var map = new Dictionary<int, int>();
        foreach (int key in keys)
            map[key] = key;
        return map.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Dictionary")]
    public int Dictionary_Insert_EnsureCapacity()
    {
        var map = new Dictionary<int, int>();
        map.EnsureCapacity(ItemCount);
        foreach (int key in keys)
            map[key] = key;
        return map.Count;
    }

    // ── Celerity IntDictionary<int> ─────────────────────────────────────────────────

    [Benchmark]
    [BenchmarkCategory("IntDictionary")]
    public int IntDictionary_Insert_Unsized()
    {
        var map = new IntDictionary<int>();
        foreach (int key in keys)
            map[key] = key;
        return map.Count;
    }

    [Benchmark]
    [BenchmarkCategory("IntDictionary")]
    public int IntDictionary_Insert_EnsureCapacity()
    {
        var map = new IntDictionary<int>();
        map.EnsureCapacity(ItemCount);
        foreach (int key in keys)
            map[key] = key;
        return map.Count;
    }
}
