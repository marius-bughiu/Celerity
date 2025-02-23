using BenchmarkDotNet.Attributes;
using Celerity.Collections;

[MemoryDiagnoser(false)]
public class IntDictionaryBenchmark
{
    private int[] keys;
    private Dictionary<int, int> dictionary;
    private IntDictionary<int> intDictionary;

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
    public void IntDictionary_Insert()
    {
        var map = new IntDictionary<int>();

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
    public void IntDictionary_Lookup()
    {
        foreach (var key in keys)
        {
            _ = intDictionary[key];
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
    public void IntDictionary_Remove()
    {
        foreach (var key in keys)
        {
            intDictionary.Remove(key);
        }
    }
}
