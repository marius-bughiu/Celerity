using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class CelerityMultiMapBenchmark
{
    private int[] keys = null!;

    private Dictionary<int, List<int>> dictionary = null!;
    private CelerityMultiMap<int, int, Int32WangNaiveHasher> multiMap = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    // Two values per key, the canonical one-to-many shape.
    private const int VALUES_PER_KEY = 2;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        Random rand = new(42);
        for (int i = 0; i < ItemCount; i++)
            keys[i] = rand.Next(1, int.MaxValue);

        dictionary = BuildDictionary();
        multiMap = BuildMultiMap();
    }

    // ── Insert (group VALUES_PER_KEY values under each key) ───────────────────
    // Baseline is the idiomatic BCL multi-map, Dictionary<TKey, List<TValue>>:
    // the fair like-for-like for a one-to-many map (a plain Dictionary<,> can
    // only hold a single value per key, so it is not a multi-map baseline).

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Insert")]
    public Dictionary<int, List<int>> Dictionary_Insert() => BuildDictionary();

    [Benchmark]
    [BenchmarkCategory("Insert")]
    public CelerityMultiMap<int, int, Int32WangNaiveHasher> CelerityMultiMap_Insert() => BuildMultiMap();

    // ── Lookup (fetch every key's value group on the prebuilt instances) ──────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public int Dictionary_Lookup()
    {
        int acc = 0;
        foreach (int key in keys)
        {
            if (dictionary.TryGetValue(key, out List<int>? group))
                acc += group.Count;
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int CelerityMultiMap_Lookup()
    {
        int acc = 0;
        foreach (int key in keys)
            acc += multiMap[key].Count;
        return acc;
    }

    // ── Remove (drop every key and all of its values) ─────────────────────────

    [IterationSetup(Target = nameof(Dictionary_Remove))]
    public void SetupForDictionaryRemove() => dictionary = BuildDictionary();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public void Dictionary_Remove()
    {
        foreach (int key in keys)
            dictionary.Remove(key);
    }

    [IterationSetup(Target = nameof(CelerityMultiMap_Remove))]
    public void SetupForCelerityMultiMapRemove() => multiMap = BuildMultiMap();

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void CelerityMultiMap_Remove()
    {
        foreach (int key in keys)
            multiMap.RemoveAll(key);
    }

    private Dictionary<int, List<int>> BuildDictionary()
    {
        var map = new Dictionary<int, List<int>>(ItemCount);
        foreach (int key in keys)
        {
            if (!map.TryGetValue(key, out List<int>? group))
            {
                group = new List<int>(VALUES_PER_KEY);
                map[key] = group;
            }
            for (int v = 0; v < VALUES_PER_KEY; v++)
                group.Add(key + v);
        }
        return map;
    }

    private CelerityMultiMap<int, int, Int32WangNaiveHasher> BuildMultiMap()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>(ItemCount);
        foreach (int key in keys)
        {
            for (int v = 0; v < VALUES_PER_KEY; v++)
                map.Add(key, key + v);
        }
        return map;
    }
}
