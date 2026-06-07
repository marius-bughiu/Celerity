using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

// MemoryDiagnoser is enabled with GC columns here (unlike the other collection
// benchmarks, which suppress them) because the whole point of the pooled
// dictionary is allocation: the Build+Dispose row should show a dramatically
// lower Allocated figure than the non-pooled Dictionary / CelerityDictionary as
// rented buffers are reused across iterations rather than allocated each time.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class PooledCelerityDictionaryBenchmark
{
    private int[] keys = null!;
    private Dictionary<int, int> dictionary = null!;
    private PooledCelerityDictionary<int, int, Int32WangNaiveHasher> pooledDictionary = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        dictionary = new Dictionary<int, int>(ItemCount);
        pooledDictionary = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>(ItemCount);

        Random rand = new(42);
        for (int i = 0; i < ItemCount; i++)
        {
            keys[i] = rand.Next(1, int.MaxValue);
            dictionary[keys[i]] = keys[i];
            pooledDictionary[keys[i]] = keys[i];
        }
    }

    [GlobalCleanup]
    public void Cleanup() => pooledDictionary.Dispose();

    // ── Build (Insert): the allocation showcase ──────────────────────────────
    // Each iteration builds a fresh map from empty. The pooled variant disposes
    // at the end of the iteration so the next iteration rents the same buffers
    // back from the pool — that is the GC win the Allocated column reports.

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Insert")]
    public void Dictionary_Insert()
    {
        var map = new Dictionary<int, int>();

        foreach (var key in keys)
        {
            map[key] = key;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Insert")]
    public void PooledCelerityDictionary_Insert()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();

        foreach (var key in keys)
        {
            map[key] = key;
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public void Dictionary_Lookup()
    {
        foreach (var key in keys)
        {
            _ = dictionary[key];
        }
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public void PooledCelerityDictionary_Lookup()
    {
        foreach (var key in keys)
        {
            _ = pooledDictionary[key];
        }
    }

    [IterationSetup(Target = nameof(Dictionary_Remove))]
    public void SetupForDictionaryRemove()
    {
        dictionary = new Dictionary<int, int>(ItemCount);
        foreach (var key in keys)
        {
            dictionary[key] = key;
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public void Dictionary_Remove()
    {
        foreach (var key in keys)
        {
            dictionary.Remove(key);
        }
    }

    [IterationSetup(Target = nameof(PooledCelerityDictionary_Remove))]
    public void SetupForPooledCelerityDictionaryRemove()
    {
        pooledDictionary = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>(ItemCount);
        foreach (var key in keys)
        {
            pooledDictionary[key] = key;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void PooledCelerityDictionary_Remove()
    {
        foreach (var key in keys)
        {
            pooledDictionary.Remove(key);
        }
    }
}
