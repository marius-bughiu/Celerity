using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

// XorFilter<int, ...> vs HashSet<int>. The xor filter is the build-once, immutable member of the membership
// family: the whole element set is supplied at construction and the filter is then read-only. In exchange it
// is the most space-efficient (the [MemoryDiagnoser] Allocated column shows ~9.84 bits/element — smaller than
// a Bloom filter at the same ~0.4% rate) and the fastest to query (exactly three probes + two XORs, no probe
// loop, no data-dependent branch), so both positive and negative lookups are cheap and branch-predictable.
// There is no Add category — construction is the Build category — and no Remove. The trade-off is exactness:
// a Contains may be a bounded-probability false positive.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class XorFilterBenchmark
{
    private int[] keys = null!;
    private int[] absentKeys = null!;
    private HashSet<int> hashSet = null!;
    private XorFilter<int, Int32WangNaiveHasher> xorFilter = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        absentKeys = new int[ItemCount];
        hashSet = new HashSet<int>(ItemCount);

        Random rand = new(42);
        for (int i = 0; i < ItemCount; i++)
        {
            keys[i] = rand.Next(1, int.MaxValue / 2);
            absentKeys[i] = rand.Next(int.MaxValue / 2, int.MaxValue);
            hashSet.Add(keys[i]);
        }

        xorFilter = new XorFilter<int, Int32WangNaiveHasher>(keys);
    }

    // Build is the xor filter's whole write path — there is no incremental Add. Both the baseline and the
    // candidate consume the full key set once, so the category isolates the peeling construction cost against
    // building a HashSet.
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public HashSet<int> HashSet_Build() => new(keys);

    [Benchmark]
    [BenchmarkCategory("Build")]
    public XorFilter<int, Int32WangNaiveHasher> XorFilter_Build() => new(keys);

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
    public bool XorFilter_Contains()
    {
        bool result = false;
        foreach (var key in keys)
            result ^= xorFilter.Contains(key);
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
    public bool XorFilter_ContainsMissing()
    {
        bool result = false;
        foreach (var key in absentKeys)
            result ^= xorFilter.Contains(key);
        return result;
    }
}
