using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SwissSetBenchmark
{
    private int[] keys = null!;
    private int[] missingKeys = null!;
    private HashSet<int> hashSet = null!;
    private SwissSet<int, Int32WangNaiveHasher> swissSet = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        missingKeys = new int[ItemCount];
        hashSet = new HashSet<int>(ItemCount);
        swissSet = new SwissSet<int, Int32WangNaiveHasher>(ItemCount);

        Random rand = new(42);
        for (int i = 0; i < ItemCount; i++)
        {
            keys[i] = rand.Next(1, int.MaxValue / 2);
            hashSet.Add(keys[i]);
            swissSet.Add(keys[i]);
            // A disjoint key space for the negative-lookup arm — SwissSet's
            // headline win, where the SIMD group scan short-circuits a miss.
            missingKeys[i] = rand.Next(int.MaxValue / 2, int.MaxValue);
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
    public void SwissSet_Add()
    {
        var set = new SwissSet<int, Int32WangNaiveHasher>();

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
    public bool SwissSet_Contains()
    {
        bool result = false;
        foreach (var key in keys)
        {
            result ^= swissSet.Contains(key);
        }
        return result;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ContainsMissing")]
    public bool HashSet_ContainsMissing()
    {
        bool result = false;
        foreach (var key in missingKeys)
        {
            result ^= hashSet.Contains(key);
        }
        return result;
    }

    [Benchmark]
    [BenchmarkCategory("ContainsMissing")]
    public bool SwissSet_ContainsMissing()
    {
        bool result = false;
        foreach (var key in missingKeys)
        {
            result ^= swissSet.Contains(key);
        }
        return result;
    }

    [IterationSetup(Target = nameof(HashSet_Remove))]
    public void SetupForHashSetRemove()
    {
        hashSet = new HashSet<int>(ItemCount);
        foreach (var key in keys)
        {
            hashSet.Add(key);
        }
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

    [IterationSetup(Target = nameof(SwissSet_Remove))]
    public void SetupForSwissSetRemove()
    {
        swissSet = new SwissSet<int, Int32WangNaiveHasher>(ItemCount);
        foreach (var key in keys)
        {
            swissSet.Add(key);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void SwissSet_Remove()
    {
        foreach (var key in keys)
        {
            swissSet.Remove(key);
        }
    }
}
