using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class LongDictionaryBenchmark
{
    private long[] keys = null!;
    private Dictionary<long, int> dictionary = null!;
    private LongDictionary<int> longDictionary = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new long[ItemCount];
        dictionary = new Dictionary<long, int>(ItemCount);
        longDictionary = new LongDictionary<int>(ItemCount);

        Random rand = new(42);
        for (int i = 0; i < ItemCount; i++)
        {
            keys[i] = ((long)rand.Next(1, int.MaxValue) << 32) | (uint)rand.Next(1, int.MaxValue);
            dictionary[keys[i]] = i;
            longDictionary[keys[i]] = i;
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Insert")]
    public void Dictionary_Insert()
    {
        var map = new Dictionary<long, int>();

        for (int i = 0; i < keys.Length; i++)
        {
            map[keys[i]] = i;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Insert")]
    public void LongDictionary_Insert()
    {
        var map = new LongDictionary<int>();

        for (int i = 0; i < keys.Length; i++)
        {
            map[keys[i]] = i;
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
    public void LongDictionary_Lookup()
    {
        foreach (var key in keys)
        {
            _ = longDictionary[key];
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

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void LongDictionary_Remove()
    {
        foreach (var key in keys)
        {
            longDictionary.Remove(key);
        }
    }
}
