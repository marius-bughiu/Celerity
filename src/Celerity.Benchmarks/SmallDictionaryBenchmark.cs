using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// SmallDictionary is built for the very-small (n <= ~16) case, where a flat-array
// linear scan beats a hash table. Its [Params] are therefore deliberately small —
// the win is at low n, and at large n the O(n) scan is meant to lose, so the
// 1000 / 100_000 params used by the hash-table benchmarks would be both
// catastrophically slow (O(n^2) inserts) and beside the point. We sweep 8 (a
// clear win) and 64 (into the region where the hash table catches up) so the
// dashboard shows both sides of the crossover honestly.
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SmallDictionaryBenchmark
{
    private int[] keys = null!;
    private Dictionary<int, int> dictionary = null!;
    private SmallDictionary<int, int> smallDictionary = null!;

    [Params(8, 64)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        dictionary = new Dictionary<int, int>(ItemCount);
        smallDictionary = new SmallDictionary<int, int>(ItemCount);

        Random rand = new(42);
        for (int i = 0; i < ItemCount; i++)
        {
            keys[i] = rand.Next(1, int.MaxValue);
            dictionary[keys[i]] = keys[i];
            smallDictionary[keys[i]] = keys[i];
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
    public void SmallDictionary_Insert()
    {
        var map = new SmallDictionary<int, int>();

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
    public void SmallDictionary_Lookup()
    {
        foreach (var key in keys)
        {
            _ = smallDictionary[key];
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

    [IterationSetup(Target = nameof(SmallDictionary_Remove))]
    public void SetupForSmallDictionaryRemove()
    {
        smallDictionary = new SmallDictionary<int, int>(ItemCount);
        foreach (var key in keys)
        {
            smallDictionary[key] = key;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void SmallDictionary_Remove()
    {
        foreach (var key in keys)
        {
            smallDictionary.Remove(key);
        }
    }
}
