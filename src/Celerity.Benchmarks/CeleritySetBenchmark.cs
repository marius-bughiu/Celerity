using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class CeleritySetBenchmark
{
    private int[] keys = null!;
    private HashSet<int> hashSet = null!;
    private CeleritySet<int, Int32WangNaiveHasher> celeritySet = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        hashSet = new HashSet<int>(ItemCount);
        celeritySet = new CeleritySet<int, Int32WangNaiveHasher>(ItemCount);

        Random rand = new(42);
        for (int i = 0; i < ItemCount; i++)
        {
            keys[i] = rand.Next(1, int.MaxValue);
            hashSet.Add(keys[i]);
            celeritySet.Add(keys[i]);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public void HashSet_Add()
    {
        var set = new HashSet<int>();

        foreach (var key in keys)
        {
            set.Add(key);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public void CeleritySet_Add()
    {
        var set = new CeleritySet<int, Int32WangNaiveHasher>();

        foreach (var key in keys)
        {
            set.Add(key);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Contains")]
    public bool HashSet_Contains()
    {
        bool result = false;
        foreach (var key in keys)
        {
            result ^= hashSet.Contains(key);
        }
        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public bool CeleritySet_Contains()
    {
        bool result = false;
        foreach (var key in keys)
        {
            result ^= celeritySet.Contains(key);
        }
        return result;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public void HashSet_Remove()
    {
        foreach (var key in keys)
        {
            hashSet.Remove(key);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void CeleritySet_Remove()
    {
        foreach (var key in keys)
        {
            celeritySet.Remove(key);
        }
    }
}
