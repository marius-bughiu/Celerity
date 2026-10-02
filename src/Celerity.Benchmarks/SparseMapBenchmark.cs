using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// SparseMap<TValue> vs Dictionary<int, TValue> over a bounded universe — the dictionary
// half of SparseSetBenchmark, and the same shape: the universe is 4x the item count, so
// the map is ~25% dense.
//
// The headline is the ClearRefill category, where SparseMap's Clear resets the count and
// leaves the backing arrays untouched while Dictionary zeroes its whole entry table. The
// ClearRefillRefs category is the same workload with a reference-holding TValue, which is
// the case where SparseMap has to clear its dense value prefix — it is here so the weaker
// half of the split Clear contract is published alongside the strong one rather than
// hidden behind an int-valued measurement. Add / Lookup / Enumerate / Remove mirror
// EnumMapBenchmark and IntDictionaryBenchmark.
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SparseMapBenchmark
{
    private int[] keys = null!;
    private string[] values = null!;
    private int universe;
    private Dictionary<int, int> dictionary = null!;
    private SparseMap<int> sparseMap = null!;
    private Dictionary<int, string> refDictionary = null!;
    private SparseMap<string> refSparseMap = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        universe = ItemCount * 4;

        // Distinct keys drawn from [0, universe) so Add never hits a duplicate.
        var distinct = new HashSet<int>(ItemCount);
        Random rand = new(42);
        while (distinct.Count < ItemCount)
            distinct.Add(rand.Next(0, universe));
        keys = distinct.ToArray();

        values = new string[keys.Length];
        for (int i = 0; i < keys.Length; i++)
            values[i] = keys[i].ToString();

        dictionary = new Dictionary<int, int>(ItemCount);
        sparseMap = new SparseMap<int>(universe);
        refDictionary = new Dictionary<int, string>(ItemCount);
        refSparseMap = new SparseMap<string>(universe);
        for (int i = 0; i < keys.Length; i++)
        {
            dictionary[keys[i]] = keys[i];
            sparseMap[keys[i]] = keys[i];
            refDictionary[keys[i]] = values[i];
            refSparseMap[keys[i]] = values[i];
        }
    }

    // Both arms are pre-sized, and both pay for it inside the measured region. The universe
    // constructor sizes only the *sparse* index array; the dense key and value arrays still start
    // empty, so without the EnsureCapacity below this arm would pay every geometric resize while
    // the baseline paid none — an asymmetry that would be charged to SparseMap in both the time
    // and the allocation column.
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public void Dictionary_Add()
    {
        var map = new Dictionary<int, int>(ItemCount);
        foreach (int key in keys)
            map.Add(key, key);
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public void SparseMap_Add()
    {
        var map = new SparseMap<int>(universe);
        map.EnsureCapacity(ItemCount);
        foreach (int key in keys)
            map.Add(key, key);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public long Dictionary_Lookup()
    {
        long total = 0;
        foreach (int key in keys)
        {
            if (dictionary.TryGetValue(key, out int value))
                total += value;
        }

        return total;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public long SparseMap_Lookup()
    {
        long total = 0;
        foreach (int key in keys)
        {
            if (sparseMap.TryGetValue(key, out int value))
                total += value;
        }

        return total;
    }

    // The headline: clear a full map and refill it. Dictionary.Clear zeroes the whole entry
    // table (O(capacity)); SparseMap.Clear resets a single field for a TValue that holds no
    // references.
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ClearRefill")]
    public void Dictionary_ClearRefill()
    {
        dictionary.Clear();
        foreach (int key in keys)
            dictionary[key] = key;
    }

    [Benchmark]
    [BenchmarkCategory("ClearRefill")]
    public void SparseMap_ClearRefill()
    {
        sparseMap.Clear();
        foreach (int key in keys)
            sparseMap[key] = key;
    }

    // The same workload with a reference-holding TValue, where SparseMap.Clear must clear its
    // dense value prefix so nothing is retained — O(Count) rather than O(1), still against
    // Dictionary's O(capacity).
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ClearRefillRefs")]
    public void Dictionary_ClearRefillRefs()
    {
        refDictionary.Clear();
        for (int i = 0; i < keys.Length; i++)
            refDictionary[keys[i]] = values[i];
    }

    [Benchmark]
    [BenchmarkCategory("ClearRefillRefs")]
    public void SparseMap_ClearRefillRefs()
    {
        refSparseMap.Clear();
        for (int i = 0; i < keys.Length; i++)
            refSparseMap[keys[i]] = values[i];
    }

    // Enumeration: Dictionary walks a possibly-sparse entry table; SparseMap walks its dense
    // prefix, which holds exactly the present entries.
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public long Dictionary_Enumerate()
    {
        long total = 0;
        foreach (KeyValuePair<int, int> entry in dictionary)
            total += entry.Value;

        return total;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public long SparseMap_Enumerate()
    {
        long total = 0;
        foreach (KeyValuePair<int, int> entry in sparseMap)
            total += entry.Value;

        return total;
    }

    [IterationSetup(Target = nameof(Dictionary_Remove))]
    public void SetupForDictionaryRemove()
    {
        dictionary = new Dictionary<int, int>(ItemCount);
        foreach (int key in keys)
            dictionary[key] = key;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public void Dictionary_Remove()
    {
        foreach (int key in keys)
            dictionary.Remove(key);
    }

    [IterationSetup(Target = nameof(SparseMap_Remove))]
    public void SetupForSparseMapRemove()
    {
        sparseMap = new SparseMap<int>(universe);
        foreach (int key in keys)
            sparseMap[key] = key;
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void SparseMap_Remove()
    {
        foreach (int key in keys)
            sparseMap.Remove(key);
    }
}
