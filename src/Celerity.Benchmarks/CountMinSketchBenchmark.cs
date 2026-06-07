using BenchmarkDotNet.Attributes;
using Celerity.Collections;
using Celerity.Hashing;

// CountMinSketch<int, ...> vs a Dictionary<int, int> exact frequency table. The headline
// is memory: a Count-Min sketch stores a fixed depth × width grid of counters
// (a few KB, sized from the error parameters) regardless of how many distinct elements it
// counts, so the [MemoryDiagnoser] Allocated column shows it building in a small constant
// footprint while a Dictionary frequency table's memory grows with the number of distinct
// keys. The trade-off is exactness: EstimateCount returns an approximate frequency with a
// bounded one-sided error (never underestimates) instead of the Dictionary's exact count.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
public class CountMinSketchBenchmark
{
    private int[] keys = null!;
    private Dictionary<int, int> dictionary = null!;
    private CountMinSketch<int, Int32WangNaiveHasher> sketch = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        dictionary = new Dictionary<int, int>();
        sketch = new CountMinSketch<int, Int32WangNaiveHasher>();

        // A skewed stream over a smaller key domain so frequencies vary (the heavy-hitter
        // shape a Count-Min sketch targets).
        Random rand = new(42);
        int domain = Math.Max(1, ItemCount / 8);
        for (int i = 0; i < ItemCount; i++)
        {
            keys[i] = rand.Next(0, domain);
            dictionary[keys[i]] = dictionary.GetValueOrDefault(keys[i]) + 1;
            sketch.Add(keys[i]);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public int Dictionary_Add()
    {
        var freq = new Dictionary<int, int>();
        foreach (var key in keys)
            freq[key] = freq.GetValueOrDefault(key) + 1;
        return freq.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public long CountMinSketch_Add()
    {
        var cms = new CountMinSketch<int, Int32WangNaiveHasher>();
        foreach (var key in keys)
            cms.Add(key);
        return cms.TotalCount;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Estimate")]
    public long Dictionary_Estimate()
    {
        long sum = 0;
        foreach (var key in keys)
            sum += dictionary[key];
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Estimate")]
    public long CountMinSketch_Estimate()
    {
        long sum = 0;
        foreach (var key in keys)
            sum += sketch.EstimateCount(key);
        return sum;
    }
}
