using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class HashCachingSetBenchmark
{
    private int[] keys = null!;
    private int[] missingKeys = null!;
    private HashSet<int> hashSet = null!;
    private HashCachingSet<int, Int32WangNaiveHasher> hashCachingSet = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        missingKeys = new int[ItemCount];
        hashSet = new HashSet<int>(ItemCount);
        hashCachingSet = new HashCachingSet<int, Int32WangNaiveHasher>(ItemCount);

        // Keys must be distinct: HashCachingSet.Add throws on a duplicate (TryAdd is
        // the non-throwing variant), and over this halved range a random collision
        // is near-certain at 100k by the birthday bound — which would abort the run.
        // hashSet.Add returns false on a repeat, so it doubles as the dedup oracle:
        // every key that reaches hashCachingSet.Add is guaranteed unique.
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
            hashCachingSet.Add(key);
            // A disjoint key space for the negative-lookup arm — where the cached
            // fingerprint filter lets a miss reject a slot on a single integer
            // compare instead of dereferencing every candidate element.
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
    public void HashCachingSet_Add()
    {
        var set = new HashCachingSet<int, Int32WangNaiveHasher>();

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
    public bool HashCachingSet_Contains()
    {
        bool result = false;
        foreach (var key in keys)
        {
            result ^= hashCachingSet.Contains(key);
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
    public bool HashCachingSet_ContainsMissing()
    {
        bool result = false;
        foreach (var key in missingKeys)
        {
            result ^= hashCachingSet.Contains(key);
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

    [IterationSetup(Target = nameof(HashCachingSet_Remove))]
    public void SetupForHashCachingSetRemove()
    {
        hashCachingSet = new HashCachingSet<int, Int32WangNaiveHasher>(ItemCount);
        foreach (var key in keys)
        {
            hashCachingSet.Add(key);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void HashCachingSet_Remove()
    {
        foreach (var key in keys)
        {
            hashCachingSet.Remove(key);
        }
    }
}
