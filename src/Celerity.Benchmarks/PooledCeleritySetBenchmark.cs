using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

// MemoryDiagnoser is enabled with GC columns here (unlike the other set
// benchmarks, which suppress them) because the whole point of the pooled set is
// allocation: the Add row builds a fresh set each iteration and disposes it, so
// the rented buffers return to ArrayPool<T>.Shared and the next iteration rents
// them back — the Allocated column should read dramatically lower than the
// non-pooled HashSet<int> / CeleritySet as buffers are reused rather than
// allocated. The set counterpart of PooledCelerityDictionaryBenchmark.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class PooledCeleritySetBenchmark
{
    private int[] keys = null!;
    private HashSet<int> hashSet = null!;
    private PooledCeleritySet<int, Int32WangNaiveHasher> pooledSet = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        hashSet = new HashSet<int>(ItemCount);
        pooledSet = new PooledCeleritySet<int, Int32WangNaiveHasher>(ItemCount);

        Random rand = new(42);
        for (int i = 0; i < ItemCount; i++)
        {
            keys[i] = rand.Next(1, int.MaxValue);
            hashSet.Add(keys[i]);
            pooledSet.Add(keys[i]);
        }
    }

    [GlobalCleanup]
    public void Cleanup() => pooledSet.Dispose();

    // ── Add (Insert): the allocation showcase ────────────────────────────────
    // Each iteration builds a fresh set from empty. The pooled variant disposes
    // at the end of the iteration so the next iteration rents the same buffers
    // back from the pool — that is the GC win the Allocated column reports.

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
    public void PooledCeleritySet_Add()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();

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
    public bool PooledCeleritySet_Contains()
    {
        bool result = false;
        foreach (var key in keys)
        {
            result ^= pooledSet.Contains(key);
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

    [IterationSetup(Target = nameof(PooledCeleritySet_Remove))]
    public void SetupForPooledCeleritySetRemove()
    {
        pooledSet = new PooledCeleritySet<int, Int32WangNaiveHasher>(ItemCount);
        foreach (var key in keys)
        {
            pooledSet.Add(key);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void PooledCeleritySet_Remove()
    {
        foreach (var key in keys)
        {
            pooledSet.Remove(key);
        }
    }
}
