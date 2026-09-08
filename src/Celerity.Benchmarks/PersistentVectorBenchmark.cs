using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// PersistentVector<int> vs ImmutableList<int>, which is the BCL's only immutable sequence that can be
// appended to in better than O(n). It is an AVL tree with one heap node per element, so both the append and
// the indexed read cost a pointer chase per level over nodes scattered across the heap; PersistentVector is a
// 32-way trie of 32-element leaf arrays with a tail buffer, so an append usually writes only the tail and a
// read walks at most three levels at this scale.
//
// ImmutableArray<int> is the other half of the story and is charted too, but only on the QuadraticAppend
// category and only up to AppendCap elements. Its Add copies the whole array, so an uncapped 100,000-element
// build is 5e9 element copies — minutes per invocation — which is why that arm is capped rather than swept
// with the rest. Both arms of that category append the same capped count, so the ratio is honest at each
// point even though the work does not scale with ItemCount past the cap.
//
// The baseline arms are named ImmutableList_* so the dashboard classifies them as the BCL reference.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class PersistentVectorBenchmark
{
    // Probe positions for the Index and Update categories, drawn once so both structures answer the same
    // sequence of indices. Capped so the probe count does not itself scale with ItemCount — what is being
    // measured is the cost of one lookup, not how many fit in an iteration.
    private const int ProbeCount = 10_000;

    // Ceiling on the QuadraticAppend arms. ImmutableArray<T>.Add is O(n) per call, so this bounds that arm at
    // ~5e7 element copies instead of the ~5e9 an uncapped 100,000-element sweep would cost.
    private const int AppendCap = 10_000;

    private int[] items = null!;
    private int[] probes = null!;

    private PersistentVector<int> vector = null!;
    private ImmutableList<int> list = null!;

    // The capped prefix the QuadraticAppend arms build, so neither arm pays for slicing inside the timed region.
    private int[] cappedItems = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        items = new int[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            items[i] = rand.Next();

        probes = new int[ProbeCount];
        for (int i = 0; i < ProbeCount; i++)
            probes[i] = rand.Next(ItemCount);

        cappedItems = new int[Math.Min(ItemCount, AppendCap)];
        Array.Copy(items, cappedItems, cappedItems.Length);

        vector = new PersistentVector<int>(items);
        list = ImmutableList.CreateRange(items);
    }

    // ---- Index: random indexed reads, the axis ImmutableList structurally loses -------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Index")]
    public long ImmutableList_Index()
    {
        long sum = 0;
        foreach (int probe in probes)
            sum += list[probe];

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Index")]
    public long PersistentVector_Index()
    {
        long sum = 0;
        foreach (int probe in probes)
            sum += vector[probe];

        return sum;
    }

    // ---- Append: build the whole sequence from empty, one element at a time -----------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Append")]
    public int ImmutableList_Append()
    {
        ImmutableList<int> built = ImmutableList<int>.Empty;
        foreach (int item in items)
            built = built.Add(item);

        return built.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Append")]
    public int PersistentVector_Append()
    {
        PersistentVector<int> built = PersistentVector<int>.Empty;
        foreach (int item in items)
            built = built.Add(item);

        return built.Count;
    }

    // ---- Enumerate: the whole sequence in order ---------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public long ImmutableList_Enumerate()
    {
        long sum = 0;
        foreach (int value in list)
            sum += value;

        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public long PersistentVector_Enumerate()
    {
        long sum = 0;
        foreach (int value in vector)
            sum += value;

        return sum;
    }

    // ---- Update: replace one element at a random index, keeping the result ------------------------
    // Each step is applied to the previous result rather than to the original, so the path copies land on
    // storage the previous step created — the shape a caller actually produces when threading a sequence of
    // edits through an immutable value.

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Update")]
    public int ImmutableList_Update()
    {
        ImmutableList<int> updated = list;
        foreach (int probe in probes)
            updated = updated.SetItem(probe, probe);

        return updated.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Update")]
    public int PersistentVector_Update()
    {
        PersistentVector<int> updated = vector;
        foreach (int probe in probes)
            updated = updated.SetItem(probe, probe);

        return updated.Count;
    }

    // ---- QuadraticAppend: the same build against ImmutableArray, capped ---------------------------
    // ImmutableArray<T> is the second Add baseline #431 registered. It is charted here rather than on the
    // Append category above because its Add is O(n) per call: capping both arms at AppendCap keeps the arm
    // tractable while still measuring the ratio the criterion is about.

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("QuadraticAppend")]
    public int ImmutableArray_QuadraticAppend()
    {
        ImmutableArray<int> built = ImmutableArray<int>.Empty;
        foreach (int item in cappedItems)
            built = built.Add(item);

        return built.Length;
    }

    [Benchmark]
    [BenchmarkCategory("QuadraticAppend")]
    public int PersistentVector_QuadraticAppend()
    {
        PersistentVector<int> built = PersistentVector<int>.Empty;
        foreach (int item in cappedItems)
            built = built.Add(item);

        return built.Count;
    }
}
