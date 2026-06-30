using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class CelerityMultiSetBenchmark
{
    private int[] items = null!;

    private Dictionary<int, int> dictionary = null!;
    private CelerityMultiSet<int, Int32WangNaiveHasher> multiSet = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    // The element domain is ~1/4 of ItemCount so each distinct element is hit
    // several times — the realistic frequency-counting shape (heavy duplication).
    private int Domain => Math.Max(1, ItemCount / 4);

    [GlobalSetup]
    public void Setup()
    {
        items = new int[ItemCount];
        Random rand = new(42);
        for (int i = 0; i < ItemCount; i++)
            items[i] = rand.Next(1, Domain + 1);

        dictionary = BuildDictionary();
        multiSet = BuildMultiSet();
    }

    // ── Count (build a frequency histogram from the item stream) ──────────────
    // Baseline is the idiomatic BCL counting pattern over Dictionary<int,int>:
    // d[x] = d.GetValueOrDefault(x) + 1, which performs two hash probes per item
    // (one to read, one to write). CelerityMultiSet.Add does it in a single
    // probe-and-increment through the devirtualized struct hasher.

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Count")]
    public Dictionary<int, int> Dictionary_Count() => BuildDictionary();

    [Benchmark]
    [BenchmarkCategory("Count")]
    public CelerityMultiSet<int, Int32WangNaiveHasher> CelerityMultiSet_Count() => BuildMultiSet();

    // ── Lookup (fetch every item's multiplicity on the prebuilt instances) ────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public long Dictionary_Lookup()
    {
        long acc = 0;
        foreach (int item in items)
        {
            if (dictionary.TryGetValue(item, out int count))
                acc += count;
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public long CelerityMultiSet_Lookup()
    {
        long acc = 0;
        foreach (int item in items)
            acc += multiSet[item];
        return acc;
    }

    // ── Remove (drop every distinct element entirely) ─────────────────────────

    [IterationSetup(Target = nameof(Dictionary_Remove))]
    public void SetupForDictionaryRemove() => dictionary = BuildDictionary();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public void Dictionary_Remove()
    {
        foreach (int item in items)
            dictionary.Remove(item);
    }

    [IterationSetup(Target = nameof(CelerityMultiSet_Remove))]
    public void SetupForCelerityMultiSetRemove() => multiSet = BuildMultiSet();

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void CelerityMultiSet_Remove()
    {
        foreach (int item in items)
            multiSet.RemoveAll(item);
    }

    private Dictionary<int, int> BuildDictionary()
    {
        var map = new Dictionary<int, int>(Domain);
        foreach (int item in items)
            map[item] = map.GetValueOrDefault(item) + 1;
        return map;
    }

    private CelerityMultiSet<int, Int32WangNaiveHasher> BuildMultiSet()
    {
        var set = new CelerityMultiSet<int, Int32WangNaiveHasher>(Domain);
        foreach (int item in items)
            set.Add(item);
        return set;
    }
}
