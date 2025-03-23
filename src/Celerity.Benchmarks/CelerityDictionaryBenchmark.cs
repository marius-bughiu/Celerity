using BenchmarkDotNet.Attributes;
using Celerity.Collections;
using Celerity.Hashing;

[MemoryDiagnoser(false)]
public class CelerityDictionaryBenchmark
{
    private int[] keys;
    private Dictionary<int, int> dictionary;
    private CelerityDictionary<int, int, Int32WangNaiveHasher> celerityDictionary;

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

    [Benchmark]
    public void Dictionary_Insert()
    {
        var map = new Dictionary<int, int>();

        foreach (var key in keys)
        {
            map[key] = key;
        }
    }

    [Benchmark]
    public void CelerityDictionary_Insert()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();

        foreach (var key in keys)
        {
            map[key] = key;
        }
    }

    [Benchmark]
    public void Dictionary_Lookup()
    {
        foreach (var key in keys)
        {
            _ = dictionary[key];
        }
    }

    [Benchmark]
    public void CelerityDictionary_Lookup()
    {
        foreach (var key in keys)
        {
            _ = celerityDictionary[key];
        }
    }

    [Benchmark]
    public void Dictionary_Remove()
    {
        foreach (var key in keys)
        {
            dictionary.Remove(key);
        }
    }

    [Benchmark]
    public void CelerityDictionary_Remove()
    {
        foreach (var key in keys)
        {
            celerityDictionary.Remove(key);
        }
    }
}
