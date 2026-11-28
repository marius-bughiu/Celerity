using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

// BinaryFuseFilter<int, ...> vs HashSet<int>, with XorFilter as a cross arm. The binary fuse filter is the
// xor filter's build-once successor: the same 8-bit fingerprint and three-read query, but each element's
// three slots sit in consecutive power-of-two segments, so the store needs fewer spare slots at large sizes
// and construction can sort the keys by segment before peeling. Like XorFilterBenchmark there is no Add
// category (construction is the Build category) and no Remove.
//
// The xor arms are named Xor_Cross* so their op names differ from the fuse arms': the dashboard keeps one
// Celerity series per (collection, op), and two arms sharing an op would overwrite each other. The shared
// categories keep all three arms in one BenchmarkDotNet group against the HashSet baseline.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class BinaryFuseFilterBenchmark
{
    private int[] keys = null!;
    private int[] absentKeys = null!;
    private HashSet<int> hashSet = null!;
    private BinaryFuseFilter<int, Int32WangNaiveHasher> fuseFilter = null!;
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

        fuseFilter = new BinaryFuseFilter<int, Int32WangNaiveHasher>(keys);
        xorFilter = new XorFilter<int, Int32WangNaiveHasher>(keys);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public HashSet<int> HashSet_Build() => new(keys);

    [Benchmark]
    [BenchmarkCategory("Build")]
    public BinaryFuseFilter<int, Int32WangNaiveHasher> BinaryFuseFilter_Build() => new(keys);

    [Benchmark]
    [BenchmarkCategory("Build")]
    public XorFilter<int, Int32WangNaiveHasher> Xor_CrossBuild() => new(keys);

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
    public bool BinaryFuseFilter_Contains()
    {
        bool result = false;
        foreach (var key in keys)
            result ^= fuseFilter.Contains(key);
        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public bool Xor_CrossContains()
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
    public bool BinaryFuseFilter_ContainsMissing()
    {
        bool result = false;
        foreach (var key in absentKeys)
            result ^= fuseFilter.Contains(key);
        return result;
    }

    [Benchmark]
    [BenchmarkCategory("ContainsMissing")]
    public bool Xor_CrossContainsMissing()
    {
        bool result = false;
        foreach (var key in absentKeys)
            result ^= xorFilter.Contains(key);
        return result;
    }
}
