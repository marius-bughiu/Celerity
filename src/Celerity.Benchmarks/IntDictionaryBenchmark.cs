using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class IntDictionaryBenchmark
{
    private int[] keys = null!;
    private Dictionary<int, int> dictionary = null!;
    private IntDictionary<int> intDictionary = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        dictionary = new Dictionary<int, int>(ItemCount);
        intDictionary = new IntDictionary<int>(ItemCount);

        Random rand = new(42);
        for (int i = 0; i < ItemCount; i++)
        {
            keys[i] = rand.Next(1, int.MaxValue);
            dictionary[keys[i]] = keys[i];
            intDictionary[keys[i]] = keys[i];
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
    public void IntDictionary_Insert()
    {
        var map = new IntDictionary<int>();

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
    public void IntDictionary_Lookup()
    {
        foreach (var key in keys)
        {
            _ = intDictionary[key];
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

    [IterationSetup(Target = nameof(IntDictionary_Remove))]
    public void SetupForIntDictionaryRemove()
    {
        intDictionary = new IntDictionary<int>(ItemCount);
        foreach (var key in keys)
        {
            intDictionary[key] = key;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void IntDictionary_Remove()
    {
        foreach (var key in keys)
        {
            intDictionary.Remove(key);
        }
    }
}
