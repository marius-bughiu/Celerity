using BenchmarkDotNet.Attributes;
using Celerity.Collections;
using Celerity.Hashing;

// TopKSketch<int, ...> vs a Dictionary<int, int> exact frequency table that is counted in full
// and then sorted to extract the top k. The headline is memory: the Space-Saving sketch keeps a
// fixed k monitors (here k = 100) regardless of how many distinct elements the stream contains,
// so the [MemoryDiagnoser] Allocated column shows it building in a small constant footprint while
// the Dictionary's memory grows with the distinct-key count. The trade-off is exactness: GetTopK
// returns the heavy hitters with a bounded one-sided error (never underestimates) instead of the
// Dictionary's exact, fully-ranked counts.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
public class TopKSketchBenchmark
{
    private const int K = 100;

    private int[] keys = null!;
    private Dictionary<int, int> dictionary = null!;
    private TopKSketch<int, Int32WangHasher> sketch = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        dictionary = new Dictionary<int, int>();
        sketch = new TopKSketch<int, Int32WangHasher>(K);

        // A high-cardinality stream (domain ~ ItemCount, so many distinct keys) with a handful of
        // planted heavy hitters — the shape a top-k sketch targets: the Dictionary must materialize
        // every distinct key, the sketch only k monitors.
        Random rand = new(42);
        int domain = Math.Max(1, ItemCount);
        for (int i = 0; i < ItemCount; i++)
        {
            keys[i] = (i % 10 == 0) ? rand.Next(0, 20) : rand.Next(0, domain);
            dictionary[keys[i]] = dictionary.GetValueOrDefault(keys[i]) + 1;
            sketch.Add(keys[i]);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public long Dictionary_Add()
    {
        var freq = new Dictionary<int, int>();
        foreach (int key in keys)
            freq[key] = freq.GetValueOrDefault(key) + 1;
        return freq.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public long TopKSketch_Add()
    {
        var topk = new TopKSketch<int, Int32WangHasher>(K);
        foreach (int key in keys)
            topk.Add(key);
        return topk.TotalCount;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TopK")]
    public int Dictionary_TopK()
    {
        // The exact top-k the sketch approximates: count-and-sort the whole distinct-key table.
        return dictionary
            .OrderByDescending(kv => kv.Value)
            .Take(K)
            .Count();
    }

    [Benchmark]
    [BenchmarkCategory("TopK")]
    public int TopKSketch_TopK() => sketch.GetTopK(K).Length;
}
