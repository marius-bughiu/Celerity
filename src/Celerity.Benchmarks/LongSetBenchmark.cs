using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class LongSetBenchmark
{
    private long[] keys = null!;
    private HashSet<long> hashSet = null!;
    private LongSet longSet = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new long[ItemCount];
        hashSet = new HashSet<long>(ItemCount);
        longSet = new LongSet(ItemCount);

        Random rand = new(42);
        for (int i = 0; i < ItemCount; i++)
        {
            // Mirror LongDictionaryBenchmark's key shape: pack two ints so the
            // full 64-bit range is exercised.
            keys[i] = ((long)rand.Next(1, int.MaxValue) << 32) | (uint)rand.Next(1, int.MaxValue);
            hashSet.Add(keys[i]);
            longSet.Add(keys[i]);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public void HashSet_Add()
    {
        var set = new HashSet<long>();

        foreach (var key in keys)
        {
            set.Add(key);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public void LongSet_Add()
    {
        var set = new LongSet();

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
    public bool LongSet_Contains()
    {
        bool result = false;
        foreach (var key in keys)
        {
            result ^= longSet.Contains(key);
        }
        return result;
    }

    [IterationSetup(Target = nameof(HashSet_Remove))]
    public void SetupForHashSetRemove()
    {
        hashSet = new HashSet<long>(ItemCount);
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

    [IterationSetup(Target = nameof(LongSet_Remove))]
    public void SetupForLongSetRemove()
    {
        longSet = new LongSet(ItemCount);
        foreach (var key in keys)
        {
            longSet.Add(key);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void LongSet_Remove()
    {
        foreach (var key in keys)
        {
            longSet.Remove(key);
        }
    }
}
