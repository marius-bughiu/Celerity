using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// SmallSet is built for the very-small (n <= ~16) case, where a flat-array linear
// scan beats a hash table. Its [Params] are therefore deliberately small — the win
// is at low n, and at large n the O(n) scan is meant to lose, so the 1000 / 100_000
// params used by the hash-table set benchmarks would be both catastrophically slow
// (O(n^2) inserts) and beside the point. We sweep 8 (a clear win) and 64 (into the
// region where the hash set catches up) so the dashboard shows both sides of the
// crossover honestly. Mirrors SmallDictionaryBenchmark.
//
// Keys are generated distinct (HashSet.Add doubles as the dedup oracle) because
// SmallSet.Add throws on a duplicate — the same guarantee the other set benchmarks
// make.
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SmallSetBenchmark
{
    private int[] keys = null!;
    private HashSet<int> hashSet = null!;
    private SmallSet<int> smallSet = null!;

    [Params(8, 64)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        hashSet = new HashSet<int>(ItemCount);
        smallSet = new SmallSet<int>(ItemCount);

        Random rand = new(42);
        int count = 0;
        while (count < ItemCount)
        {
            int key = rand.Next(1, int.MaxValue);
            if (!hashSet.Add(key))
            {
                continue;
            }

            keys[count] = key;
            smallSet.Add(key);
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
    public void SmallSet_Add()
    {
        var set = new SmallSet<int>();

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
    public bool SmallSet_Contains()
    {
        bool result = false;
        foreach (var key in keys)
        {
            result ^= smallSet.Contains(key);
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

    [IterationSetup(Target = nameof(SmallSet_Remove))]
    public void SetupForSmallSetRemove()
    {
        smallSet = new SmallSet<int>(ItemCount);
        foreach (var key in keys)
        {
            smallSet.Add(key);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void SmallSet_Remove()
    {
        foreach (var key in keys)
        {
            smallSet.Remove(key);
        }
    }
}
