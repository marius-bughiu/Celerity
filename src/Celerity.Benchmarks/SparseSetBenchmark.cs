using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// SparseSet vs HashSet<int> over a bounded universe. The Add / Contains / Remove
// categories mirror IntSetBenchmark; the headline is the ClearRefill category, where
// SparseSet's O(1) Clear (resets the count/version, leaving the backing arrays untouched)
// beats HashSet's O(capacity) table-zeroing on the clear-and-rebuild workload it is built for
// (per-frame / per-query "visited" sets). The universe is 4× the item count, so the
// set is ~25% dense — a realistic sparse-occupancy shape.
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SparseSetBenchmark
{
    private int[] keys = null!;
    private int universe;
    private HashSet<int> hashSet = null!;
    private SparseSet sparseSet = null!;

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

        hashSet = new HashSet<int>(ItemCount);
        sparseSet = new SparseSet(universe);
        foreach (int key in keys)
        {
            hashSet.Add(key);
            sparseSet.Add(key);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public void HashSet_Add()
    {
        // Pre-sized to keep the per-add comparison fair: SparseSet's universe is
        // mandatory, so `new SparseSet(universe)` inherently pre-allocates its sparse
        // array (no sparse resize on Add). The baseline is given the matching capacity
        // hint (ItemCount, as the Setup / Remove paths already do) so this measures
        // per-add cost rather than HashSet's incremental-rehash overhead.
        var set = new HashSet<int>(ItemCount);
        foreach (var key in keys)
            set.Add(key);
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public void SparseSet_Add()
    {
        var set = new SparseSet(universe);
        foreach (var key in keys)
            set.Add(key);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Contains")]
    public bool HashSet_Contains()
    {
        bool result = false;
        foreach (var key in keys)
            result ^= hashSet.Contains(key);
        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public bool SparseSet_Contains()
    {
        bool result = false;
        foreach (var key in keys)
            result ^= sparseSet.Contains(key);
        return result;
    }

    // The headline: clear a full set and refill it. HashSet.Clear zeroes the whole
    // entry table (O(capacity)); SparseSet.Clear resets a single field (O(1)).
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ClearRefill")]
    public void HashSet_ClearRefill()
    {
        hashSet.Clear();
        foreach (var key in keys)
            hashSet.Add(key);
    }

    [Benchmark]
    [BenchmarkCategory("ClearRefill")]
    public void SparseSet_ClearRefill()
    {
        sparseSet.Clear();
        foreach (var key in keys)
            sparseSet.Add(key);
    }

    [IterationSetup(Target = nameof(HashSet_Remove))]
    public void SetupForHashSetRemove()
    {
        hashSet = new HashSet<int>(ItemCount);
        foreach (var key in keys)
            hashSet.Add(key);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public void HashSet_Remove()
    {
        foreach (var key in keys)
            hashSet.Remove(key);
    }

    [IterationSetup(Target = nameof(SparseSet_Remove))]
    public void SetupForSparseSetRemove()
    {
        sparseSet = new SparseSet(universe);
        foreach (var key in keys)
            sparseSet.Add(key);
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void SparseSet_Remove()
    {
        foreach (var key in keys)
            sparseSet.Remove(key);
    }
}
