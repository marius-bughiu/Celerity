using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

/// <summary>
/// <see cref="HierarchicalBitSet"/> against the BCL's ordered set, <see cref="SortedSet{T}"/>, over a universe
/// eight times the item count, so the set is 12.5% dense. The headline groups are the ordered queries:
/// <c>LowerBound</c> (smallest element ≥ a probe) and <c>Floor</c> (largest ≤ a probe). The BCL has no
/// successor method, so its arm asks the way a caller has to, through <see cref="SortedSet{T}.GetViewBetween"/>.
/// The <c>LowerBound</c> group also carries the library's own <see cref="BTreeSet{T}"/>, the type a caller
/// would otherwise pick for an ordered integer set. Its method has an op name of its own
/// (<c>CrossLowerBound</c>) so the dashboard, which charts two series per op, never draws it over the
/// Celerity series.
/// </summary>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class HierarchicalBitSetBenchmark
{
    // Queries per invocation. A fixed count rather than ItemCount keeps the slow arm's invocation short at
    // 100,000, and the probes are spread over the whole universe either way.
    private const int ProbeCount = 1000;

    private int[] keys = null!;
    private int[] probes = null!;
    private int universe;
    private SortedSet<int> sortedSet = null!;
    private BTreeSet<int> bTree = null!;
    private HierarchicalBitSet bitSet = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        universe = ItemCount * 8;

        // Distinct keys drawn from [0, universe) in random order, so Add never hits a duplicate and the
        // red-black tree is not built from a sorted run.
        var distinct = new HashSet<int>(ItemCount);
        Random rand = new(42);
        while (distinct.Count < ItemCount)
            distinct.Add(rand.Next(0, universe));
        keys = distinct.ToArray();

        sortedSet = new SortedSet<int>(keys);
        bTree = new BTreeSet<int>(keys);
        bitSet = new HierarchicalBitSet(universe, keys);

        // Probes anywhere in [min, max], so every query has an answer on both sides of it and no arm takes
        // an early "nothing there" exit.
        int min = sortedSet.Min;
        int max = sortedSet.Max;
        probes = new int[ProbeCount];
        for (int i = 0; i < probes.Length; i++)
            probes[i] = rand.Next(min, max + 1);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public void SortedSet_Add()
    {
        var set = new SortedSet<int>();
        foreach (int key in keys)
            set.Add(key);
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public void HierarchicalBitSet_Add()
    {
        // The universe is mandatory, so the constructor allocates the whole bit vector up front; that
        // allocation is part of what this arm pays.
        var set = new HierarchicalBitSet(universe);
        foreach (int key in keys)
            set.Add(key);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Contains")]
    public bool SortedSet_Contains()
    {
        bool result = false;
        foreach (int key in keys)
            result ^= sortedSet.Contains(key);
        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public bool HierarchicalBitSet_Contains()
    {
        bool result = false;
        foreach (int key in keys)
            result ^= bitSet.Contains(key);
        return result;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("LowerBound")]
    public int SortedSet_LowerBound()
    {
        // SortedSet<T> has no successor method. A view from the probe upwards and its Min is the shortest
        // BCL route; every probe is at or below Max, so the view is never empty.
        int result = 0;
        foreach (int probe in probes)
            result += sortedSet.GetViewBetween(probe, int.MaxValue).Min;
        return result;
    }

    [Benchmark]
    [BenchmarkCategory("LowerBound")]
    public int HierarchicalBitSet_LowerBound()
    {
        int result = 0;
        foreach (int probe in probes)
        {
            bitSet.TryGetLowerBound(probe, out int bound);
            result += bound;
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("LowerBound")]
    public int BTreeSet_CrossLowerBound()
    {
        int result = 0;
        foreach (int probe in probes)
        {
            bTree.TryGetLowerBound(probe, out int bound);
            result += bound;
        }

        return result;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Floor")]
    public int SortedSet_Floor()
    {
        // The mirror image: a view from the bottom up to the probe, and its Max. Every probe is at or above
        // Min, so the view is never empty.
        int result = 0;
        foreach (int probe in probes)
            result += sortedSet.GetViewBetween(int.MinValue, probe).Max;
        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Floor")]
    public int HierarchicalBitSet_Floor()
    {
        int result = 0;
        foreach (int probe in probes)
        {
            bitSet.TryGetFloor(probe, out int bound);
            result += bound;
        }

        return result;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public int SortedSet_Enumerate()
    {
        int result = 0;
        foreach (int item in sortedSet)
            result += item;
        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public int HierarchicalBitSet_Enumerate()
    {
        int result = 0;
        foreach (int item in bitSet)
            result += item;
        return result;
    }

    [IterationSetup(Target = nameof(SortedSet_Remove))]
    public void SetupForSortedSetRemove() => sortedSet = new SortedSet<int>(keys);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public void SortedSet_Remove()
    {
        foreach (int key in keys)
            sortedSet.Remove(key);
    }

    [IterationSetup(Target = nameof(HierarchicalBitSet_Remove))]
    public void SetupForHierarchicalBitSetRemove() => bitSet = new HierarchicalBitSet(universe, keys);

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void HierarchicalBitSet_Remove()
    {
        foreach (int key in keys)
            bitSet.Remove(key);
    }
}
