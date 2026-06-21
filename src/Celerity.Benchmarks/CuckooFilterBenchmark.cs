using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

// CuckooFilter<int, ...> vs HashSet<int>. Like BloomFilter the headline is memory — it stores only short
// fingerprints (no keys, no buckets) — but the cuckoo filter additionally supports Remove, so it carries a
// Remove category a Bloom filter cannot. Lookups touch at most two buckets (≈ two cache lines), keeping both
// positive and negative Contains competitive. The trade-off is exactness: a Contains may be a
// bounded-probability false positive.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class CuckooFilterBenchmark
{
    private int[] keys = null!;
    private int[] absentKeys = null!;
    private HashSet<int> hashSet = null!;
    private CuckooFilter<int, Int32Murmur3Hasher> cuckooFilter = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        absentKeys = new int[ItemCount];
        hashSet = new HashSet<int>(ItemCount);
        cuckooFilter = new CuckooFilter<int, Int32Murmur3Hasher>(ItemCount);

        Random rand = new(42);
        for (int i = 0; i < ItemCount; i++)
        {
            keys[i] = rand.Next(1, int.MaxValue / 2);
            absentKeys[i] = rand.Next(int.MaxValue / 2, int.MaxValue);
            hashSet.Add(keys[i]);
            cuckooFilter.Add(keys[i]);
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
    public void CuckooFilter_Add()
    {
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(ItemCount);
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
    public bool CuckooFilter_Contains()
    {
        bool result = false;
        foreach (var key in keys)
            result ^= cuckooFilter.Contains(key);
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
    public bool CuckooFilter_ContainsMissing()
    {
        bool result = false;
        foreach (var key in absentKeys)
            result ^= cuckooFilter.Contains(key);
        return result;
    }

    // Remove is the cuckoo filter's differentiator; rebuild each iteration so the work is constant. Both the
    // baseline and the candidate add the full key set and then remove it, so the category isolates removal cost.
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public void HashSet_Remove()
    {
        var set = new HashSet<int>(keys);
        foreach (var key in keys)
            set.Remove(key);
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void CuckooFilter_Remove()
    {
        var filter = new CuckooFilter<int, Int32Murmur3Hasher>(ItemCount);
        foreach (var key in keys)
            filter.Add(key);
        foreach (var key in keys)
            filter.Remove(key);
    }
}
