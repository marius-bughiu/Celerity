using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

// BloomFilter<int, ...> vs HashSet<int>. The headline is memory: a Bloom filter
// stores only a bit array (no keys, no buckets), so the [MemoryDiagnoser] Allocated
// column shows it building in a fraction of the space of a HashSet at the same
// element count, while staying competitive on Add throughput (no resize, no bucket
// chains) and on negative lookups (no key comparison — a missing bit ends the probe).
// The trade-off is exactness: a Contains may be a bounded-probability false positive.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class BloomFilterBenchmark
{
    private int[] keys = null!;
    private int[] absentKeys = null!;
    private HashSet<int> hashSet = null!;
    private BloomFilter<int, Int32WangNaiveHasher> bloomFilter = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        absentKeys = new int[ItemCount];
        hashSet = new HashSet<int>(ItemCount);
        bloomFilter = new BloomFilter<int, Int32WangNaiveHasher>(ItemCount);

        Random rand = new(42);
        for (int i = 0; i < ItemCount; i++)
        {
            keys[i] = rand.Next(1, int.MaxValue / 2);
            absentKeys[i] = rand.Next(int.MaxValue / 2, int.MaxValue);
            hashSet.Add(keys[i]);
            bloomFilter.Add(keys[i]);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public void HashSet_Add()
    {
        var set = new HashSet<int>();
        foreach (var key in keys)
            set.Add(key);
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public void BloomFilter_Add()
    {
        var filter = new BloomFilter<int, Int32WangNaiveHasher>(ItemCount);
        foreach (var key in keys)
            filter.Add(key);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Contains")]
    public bool HashSet_Contains()
    {
        bool result = false;
        foreach (var key in keys)
            result ^= hashSet.Contains(key);
        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public bool BloomFilter_Contains()
    {
        bool result = false;
        foreach (var key in keys)
            result ^= bloomFilter.Contains(key);
        return result;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ContainsMissing")]
    public bool HashSet_ContainsMissing()
    {
        bool result = false;
        foreach (var key in absentKeys)
            result ^= hashSet.Contains(key);
        return result;
    }

    [Benchmark]
    [BenchmarkCategory("ContainsMissing")]
    public bool BloomFilter_ContainsMissing()
    {
        bool result = false;
        foreach (var key in absentKeys)
            result ^= bloomFilter.Contains(key);
        return result;
    }
}
