using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class CelerityDictionaryBenchmark
{
    private int[] keys = null!;
    private Dictionary<int, int> dictionary = null!;
    private CelerityDictionary<int, int, Int32WangNaiveHasher> celerityDictionary = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        dictionary = new Dictionary<int, int>(ItemCount);
        celerityDictionary = new CelerityDictionary<int, int, Int32WangNaiveHasher>(ItemCount);

        Random rand = new(42);
        for (int i = 0; i < ItemCount; i++)
        {
            keys[i] = rand.Next(1, int.MaxValue);
            dictionary[keys[i]] = keys[i];
            celerityDictionary[keys[i]] = keys[i];
        }
    }

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
    public void CelerityDictionary_Insert()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();

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
    public void CelerityDictionary_Lookup()
    {
        foreach (var key in keys)
        {
            _ = celerityDictionary[key];
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

    [IterationSetup(Target = nameof(CelerityDictionary_Remove))]
    public void SetupForCelerityDictionaryRemove()
    {
        celerityDictionary = new CelerityDictionary<int, int, Int32WangNaiveHasher>(ItemCount);
        foreach (var key in keys)
        {
            celerityDictionary[key] = key;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void CelerityDictionary_Remove()
    {
        foreach (var key in keys)
        {
            celerityDictionary.Remove(key);
        }
    }
}
