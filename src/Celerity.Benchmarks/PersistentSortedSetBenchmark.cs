using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// PersistentSortedSet<int> vs ImmutableSortedSet<int>, which is the BCL's only immutable sorted set. It is an AVL
// tree with one heap node per element — the element, two children, a height and a subtree count — so its
// branching factor is two and every membership test is a pointer chase per level over nodes scattered across
// the heap, with each comparison an interface call through the IComparer<T> it holds. PersistentSortedSet is
// the set half of PersistentSortedMap: a path-copying B+ tree of fan-out 32 whose elements sit in flat per-leaf
// arrays, so a level costs one binary search over a contiguous array and enumeration walks the leaves.
//
// The categories mirror PersistentHashSetBenchmark — the read the workload is dominated by, the two writes that
// path-copy, and the enumeration a snapshot consumer pays — plus LowerBound, the ordered query the type exists
// for. ImmutableSortedSet has no floor or ceiling method, so its arm is the idiom a caller writes: IndexOf (the
// complement of the next larger element's index on a miss) and then the indexer, two O(log n) descents.
//
// The baseline arms are named ImmutableSortedSet_* so the dashboard classifies them as the BCL reference.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class PersistentSortedSetBenchmark
{
    // Probe positions for Contains and LowerBound, drawn once so both structures answer the same sequence.
    // Capped so the probe count does not itself scale with ItemCount — what is measured is the cost of one
    // operation, not how many fit in an iteration.
    private const int ProbeCount = 10_000;

    private int[] elements = null!;
    private int[] probes = null!;
    private int[] lowerBoundProbes = null!;

    // The elements the Remove category takes out, in order: distinct and all present, so every step is a real
    // path copy in both arms rather than a no-op that hands back the receiver. Half the set at most, so the
    // 1,000-element run is not measuring a drain to empty.
    private int[] removals = null!;

    private PersistentSortedSet<int, DefaultComparer<int>> set = null!;
    private ImmutableSortedSet<int> immutable = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        // Even elements only, so a LowerBound or Contains probe can fall between two elements.
        elements = new int[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            elements[i] = 2 * i;

        // Fisher-Yates, so insertion order is not element order and neither structure gets a sorted build.
        for (int i = ItemCount - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (elements[i], elements[j]) = (elements[j], elements[i]);
        }

        // Uniform over [0, 2 * ItemCount], so about half the probes are hits and half land between elements —
        // and, for LowerBound, the top value lands past the largest element.
        probes = new int[ProbeCount];
        lowerBoundProbes = new int[ProbeCount];
        for (int i = 0; i < ProbeCount; i++)
        {
            probes[i] = rand.Next(2 * ItemCount);
            lowerBoundProbes[i] = rand.Next(2 * ItemCount + 1);
        }

        removals = elements[..Math.Min(ProbeCount, ItemCount / 2)];

        var builder = new PersistentSortedSet<int, DefaultComparer<int>>.Builder();
        ImmutableSortedSet<int>.Builder bclBuilder = ImmutableSortedSet.CreateBuilder<int>();
        foreach (int element in elements)
        {
            builder.Add(element);
            bclBuilder.Add(element);
        }

        set = builder.ToImmutable();
        immutable = bclBuilder.ToImmutable();
    }

    // ---- Contains: the axis the workload is dominated by -------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Contains")]
    public int ImmutableSortedSet_Contains()
    {
        int found = 0;
        foreach (int probe in probes)
        {
            if (immutable.Contains(probe))
                found++;
        }

        return found;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public int PersistentSortedSet_Contains()
    {
        int found = 0;
        foreach (int probe in probes)
        {
            if (set.Contains(probe))
                found++;
        }

        return found;
    }

    // ---- Insert: build the whole set from empty, one element at a time ----------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Insert")]
    public int ImmutableSortedSet_Insert()
    {
        ImmutableSortedSet<int> built = ImmutableSortedSet<int>.Empty;
        foreach (int element in elements)
            built = built.Add(element);

        return built.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Insert")]
    public int PersistentSortedSet_Insert()
    {
        PersistentSortedSet<int, DefaultComparer<int>> built = PersistentSortedSet<int, DefaultComparer<int>>.Empty;
        foreach (int element in elements)
            built = built.Add(element);

        return built.Count;
    }

    // ---- Remove: take elements out one at a time, keeping the result -----------------------------
    // Each step is applied to the previous result rather than to the original, so the path copies land on
    // storage the previous step created — the shape a caller produces when threading edits through a value.

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public int ImmutableSortedSet_Remove()
    {
        ImmutableSortedSet<int> trimmed = immutable;
        foreach (int element in removals)
            trimmed = trimmed.Remove(element);

        return trimmed.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public int PersistentSortedSet_Remove()
    {
        PersistentSortedSet<int, DefaultComparer<int>> trimmed = set;
        foreach (int element in removals)
            trimmed = trimmed.Remove(element);

        return trimmed.Count;
    }

    // ---- Enumerate: the whole set in order, which is what a snapshot consumer pays -----------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public long ImmutableSortedSet_Enumerate()
    {
        long sum = 0;
        foreach (int element in immutable)
            sum += element;

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public long PersistentSortedSet_Enumerate()
    {
        long sum = 0;
        foreach (int element in set)
            sum += element;

        return sum;
    }

    // ---- LowerBound: the first element >= a probe, the ordered query the type exists for -----------
    // ImmutableSortedSet has no ceiling method, so its arm is IndexOf (the complement of the next larger
    // element's index on a miss) and then the indexer: two O(log n) descents of an AVL tree. A probe past the
    // largest element has no bound in either arm.

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("LowerBound")]
    public long ImmutableSortedSet_LowerBound()
    {
        long sum = 0;
        int count = immutable.Count;
        foreach (int probe in lowerBoundProbes)
        {
            int index = immutable.IndexOf(probe);
            if (index < 0)
                index = ~index;
            if (index < count)
                sum += immutable[index];
        }

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("LowerBound")]
    public long PersistentSortedSet_LowerBound()
    {
        long sum = 0;
        foreach (int probe in lowerBoundProbes)
        {
            if (set.TryGetLowerBound(probe, out int bound))
                sum += bound;
        }

        return sum;
    }
}
