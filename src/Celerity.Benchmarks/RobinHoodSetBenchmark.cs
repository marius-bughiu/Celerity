using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RobinHoodSetBenchmark
{
    private int[] keys = null!;
    private int[] missingKeys = null!;
    private HashSet<int> hashSet = null!;
    private RobinHoodSet<int, Int32WangNaiveHasher> robinHoodSet = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        missingKeys = new int[ItemCount];
        hashSet = new HashSet<int>(ItemCount);
        robinHoodSet = new RobinHoodSet<int, Int32WangNaiveHasher>(ItemCount);

        // Keys must be distinct: RobinHoodSet.Add throws on a duplicate (TryAdd is
        // the non-throwing variant), and over this halved range a random collision
        // is near-certain at 100k by the birthday bound — which would abort the run.
        // hashSet.Add returns false on a repeat, so it doubles as the dedup oracle:
        // every key that reaches robinHoodSet.Add is guaranteed unique.
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
            robinHoodSet.Add(key);
            // A disjoint key space for the negative-lookup arm — where the Robin
            // Hood PSL invariant lets a miss stop early instead of scanning the
            // whole probe run.
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
    public void RobinHoodSet_Add()
    {
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();

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
    public bool RobinHoodSet_Contains()
    {
        bool result = false;
        foreach (var key in keys)
        {
            result ^= robinHoodSet.Contains(key);
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
    public bool RobinHoodSet_ContainsMissing()
    {
        bool result = false;
        foreach (var key in missingKeys)
        {
            result ^= robinHoodSet.Contains(key);
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

    [IterationSetup(Target = nameof(RobinHoodSet_Remove))]
    public void SetupForRobinHoodSetRemove()
    {
        robinHoodSet = new RobinHoodSet<int, Int32WangNaiveHasher>(ItemCount);
        foreach (var key in keys)
        {
            robinHoodSet.Add(key);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void RobinHoodSet_Remove()
    {
        foreach (var key in keys)
        {
            robinHoodSet.Remove(key);
        }
    }
}
