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

        // Keys must be distinct: SwissSet.Add throws on a duplicate (TryAdd is the
        // non-throwing variant), and over this halved range a random collision is
        // near-certain at 100k by the birthday bound — which would abort the run.
        // hashSet.Add returns false on a repeat, so it doubles as the dedup oracle:
        // every key that reaches swissSet.Add is guaranteed unique.
        Random rand = new(42);
        int count = 0;
        while (count < ItemCount)
        {
            int key = rand.Next(1, int.MaxValue / 2);
            if (!hashSet.Add(key))
            {
                continue;
            }

            keys[count] = key;
            swissSet.Add(key);
            // A disjoint key space for the negative-lookup arm — SwissSet's
            // headline win, where the SIMD group scan short-circuits a miss.
            missingKeys[count] = rand.Next(int.MaxValue / 2, int.MaxValue);
            count++;
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
