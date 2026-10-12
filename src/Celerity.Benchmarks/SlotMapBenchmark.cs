using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// SlotMap<TValue> vs the idiomatic BCL hand-roll for stable handles: a Dictionary<int, TValue>
// keyed by an incrementing int counter. Both sides mint their own keys, so the comparison is the
// one a caller actually faces when they need an object table addressed by handle.
//
// Lookups and removals visit the handles in a shuffled order, because handle tables are looked
// up from wherever the handles were stored, not in insertion order. Churn is the steady-state
// workload the type is for — remove a value and add a replacement, keeping the size constant —
// and needs no per-iteration reset because it leaves the table the size it found it. Add /
// Lookup / Enumerate / Remove mirror SparseMapBenchmark.
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SlotMapBenchmark
{
    private int[] order = null!;
    private int[] ids = null!;
    private SlotKey[] slotKeys = null!;
    private int nextId;
    private Dictionary<int, int> dictionary = null!;
    private SlotMap<int> slotMap = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        // A fixed random permutation of [0, ItemCount): the order handles are visited in.
        order = new int[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            order[i] = i;
        new Random(42).Shuffle(order);

        Fill();
    }

    private void Fill()
    {
        ids = new int[ItemCount];
        slotKeys = new SlotKey[ItemCount];
        dictionary = new Dictionary<int, int>(ItemCount);
        slotMap = new SlotMap<int>(ItemCount);
        nextId = 0;

        for (int i = 0; i < ItemCount; i++)
        {
            int id = nextId++;
            dictionary.Add(id, i);
            ids[i] = id;
            slotKeys[i] = slotMap.Add(i);
        }
    }

    // Both arms are pre-sized and pay for it inside the measured region.
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public void Dictionary_Add()
    {
        var map = new Dictionary<int, int>(ItemCount);
        int next = 0;
        for (int i = 0; i < ItemCount; i++)
            map.Add(next++, i);
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public void SlotMap_Add()
    {
        var map = new SlotMap<int>(ItemCount);
        for (int i = 0; i < ItemCount; i++)
            map.Add(i);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public long Dictionary_Lookup()
    {
        long total = 0;
        foreach (int i in order)
        {
            if (dictionary.TryGetValue(ids[i], out int value))
                total += value;
        }

        return total;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public long SlotMap_Lookup()
    {
        long total = 0;
        foreach (int i in order)
        {
            if (slotMap.TryGetValue(slotKeys[i], out int value))
                total += value;
        }

        return total;
    }

    // Enumeration: Dictionary walks its entry table; SlotMap walks its slot array. Both skip the
    // entries a removal freed; this table has none.
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
    public long SlotMap_Enumerate()
    {
        long total = 0;
        foreach (KeyValuePair<SlotKey, int> entry in slotMap)
            total += entry.Value;

        return total;
    }

    // Steady-state handle turnover: retire a value and add its replacement, in shuffled order, so
    // the table stays at ItemCount throughout. The slot map reuses the vacated slot under a new
    // version; the dictionary mints a fresh id and reuses its freed entry.
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Churn")]
    public void Dictionary_Churn()
    {
        foreach (int i in order)
        {
            dictionary.Remove(ids[i]);
            int id = nextId++;
            dictionary.Add(id, i);
            ids[i] = id;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Churn")]
    public void SlotMap_Churn()
    {
        foreach (int i in order)
        {
            slotMap.Remove(slotKeys[i]);
            slotKeys[i] = slotMap.Add(i);
        }
    }

    [IterationSetup(Target = nameof(Dictionary_Remove))]
    public void SetupForDictionaryRemove() => Fill();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public void Dictionary_Remove()
    {
        foreach (int i in order)
            dictionary.Remove(ids[i]);
    }

    [IterationSetup(Target = nameof(SlotMap_Remove))]
    public void SetupForSlotMapRemove() => Fill();

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void SlotMap_Remove()
    {
        foreach (int i in order)
            slotMap.Remove(slotKeys[i]);
    }
}
