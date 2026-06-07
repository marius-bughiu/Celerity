using BenchmarkDotNet.Attributes;
using Celerity.Collections;
using Celerity.Hashing;

// HyperLogLog<int, ...> vs HashSet<int>. The headline is memory: HyperLogLog stores a
// fixed array of 2^precision one-byte registers (16 KB at the default precision 14)
// regardless of how many distinct elements it counts, so the [MemoryDiagnoser]
// Allocated column shows it building in a small constant footprint while a HashSet's
// memory grows with the cardinality. The trade-off is exactness: EstimateCardinality
// returns an approximate distinct count with a bounded relative error (~0.8% at the
// default precision) instead of HashSet.Count's exact value.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
public class HyperLogLogBenchmark
{
    private int[] keys = null!;
    private HashSet<int> hashSet = null!;
    private HyperLogLog<int, Int32WangNaiveHasher> hll = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        hashSet = new HashSet<int>(ItemCount);
        hll = new HyperLogLog<int, Int32WangNaiveHasher>();

        Random rand = new(42);
        for (int i = 0; i < ItemCount; i++)
        {
            keys[i] = rand.Next(1, int.MaxValue / 2);
            hashSet.Add(keys[i]);
            hll.Add(keys[i]);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public int HashSet_Add()
    {
        var set = new HashSet<int>();
        foreach (var key in keys)
            set.Add(key);
        return set.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public long HyperLogLog_Add()
    {
        var estimator = new HyperLogLog<int, Int32WangNaiveHasher>();
        foreach (var key in keys)
            estimator.Add(key);
        return estimator.EstimateCardinality();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Estimate")]
    public int HashSet_Estimate()
    {
        return hashSet.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Estimate")]
    public long HyperLogLog_Estimate()
    {
        return hll.EstimateCardinality();
    }
}
