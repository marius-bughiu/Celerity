using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// EnumMap<TEnum, TValue> vs Dictionary<TEnum, TValue>. Like EnumSetBenchmark the workload is a
// bounded enum universe rather than a [Params] item-count sweep — the universe is the enum's set
// of members. BenchEnum (declared in EnumSetBenchmark.cs) has 40 contiguous members (one 64-bit
// word), the dense shape EnumMap is built for. Add / Lookup / Remove show the per-entry win (a
// shift + mask + direct array index vs a hash-and-probe); Enumerate is the contiguous-storage
// sweep win, where EnumMap walks a packed occupancy vector + value array while Dictionary chases
// its bucket / entry arrays.
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class EnumMapBenchmark
{
    private BenchEnum[] all = null!;

    private Dictionary<BenchEnum, int> dictionary = null!;
    private EnumMap<BenchEnum, int> enumMap = null!;

    [GlobalSetup]
    public void Setup()
    {
        all = Enum.GetValues<BenchEnum>();

        dictionary = new Dictionary<BenchEnum, int>();
        enumMap = new EnumMap<BenchEnum, int>();
        foreach (var e in all)
        {
            dictionary[e] = (int)e;
            enumMap[e] = (int)e;
        }
    }

    // ── Add (build fresh, add every member) ───────────────────────────────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public void Dictionary_Add()
    {
        var map = new Dictionary<BenchEnum, int>();
        foreach (var e in all)
            map[e] = (int)e;
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public void EnumMap_Add()
    {
        var map = new EnumMap<BenchEnum, int>();
        foreach (var e in all)
            map[e] = (int)e;
    }

    // ── Lookup (TryGetValue every member, on the prebuilt instances) ───────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public long Dictionary_Lookup()
    {
        long acc = 0;
        foreach (var e in all)
            if (dictionary.TryGetValue(e, out int v))
                acc += v;
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public long EnumMap_Lookup()
    {
        long acc = 0;
        foreach (var e in all)
            if (enumMap.TryGetValue(e, out int v))
                acc += v;
        return acc;
    }

    // ── Remove (every member; refilled each iteration) ────────────────────────

    [IterationSetup(Target = nameof(Dictionary_Remove))]
    public void SetupForDictionaryRemove()
    {
        dictionary = new Dictionary<BenchEnum, int>();
        foreach (var e in all)
            dictionary[e] = (int)e;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public void Dictionary_Remove()
    {
        foreach (var e in all)
            dictionary.Remove(e);
    }

    [IterationSetup(Target = nameof(EnumMap_Remove))]
    public void SetupForEnumMapRemove()
    {
        enumMap = new EnumMap<BenchEnum, int>();
        foreach (var e in all)
            enumMap[e] = (int)e;
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void EnumMap_Remove()
    {
        foreach (var e in all)
            enumMap.Remove(e);
    }

    // ── Enumerate (sum every value) — the contiguous-storage sweep win ─────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public long Dictionary_Enumerate()
    {
        long acc = 0;
        foreach (var kvp in dictionary)
            acc += kvp.Value;
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public long EnumMap_Enumerate()
    {
        long acc = 0;
        foreach (var kvp in enumMap)
            acc += kvp.Value;
        return acc;
    }
}
