using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// BTreeList<int> vs the two BCL lists a caller would reach for when elements are inserted and removed in the
// middle. List<int> is the charted baseline: it indexes in O(1) and shifts every later element on each
// Insert / RemoveAt, so a middle edit is O(n). ImmutableList<int>.Builder is the BCL's only list that is
// O(log n) on both an edit and a read, but it is an AVL tree with a heap node per element; it is measured in
// the *Avl categories and deliberately NOT charted, because the dashboard renders one `vs` label per
// collection and the card reads "vs List<int>". Those categories still run in the CI suite and appear in the
// joined report, which is what makes the figures quoted in the API reference reproducible.
//
// The edit categories rebuild the subject in an [IterationSetup] and then apply EditCount edits at positions
// drawn once in [GlobalSetup], so every arm edits the same positions in the same order. EditCount is fixed so
// the measured quantity is the cost of an edit at a given list size, not how many edits fit in an iteration.
// The Index and Enumerate arms are where BTreeList loses to List<int>, and they ship as charted rows rather
// than being left out.
//
// The baseline arms are named List_* / ImmutableList_* so the dashboard classifies them as the BCL reference.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class BTreeListBenchmark
{
    private const int EditCount = 1_000;
    private const int ProbeCount = 10_000;

    private int[] items = null!;
    private int[] insertPositions = null!;
    private int[] removePositions = null!;
    private int[] probes = null!;

    // Edit subjects, rebuilt per iteration by the [IterationSetup]s below.
    private List<int> list = null!;
    private BTreeList<int> tree = null!;
    private ImmutableList<int>.Builder builder = null!;

    // Full, warm subjects for the non-destructive read categories, built once in [GlobalSetup].
    private List<int> listFull = null!;
    private BTreeList<int> treeFull = null!;
    private ImmutableList<int>.Builder builderFull = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        items = new int[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            items[i] = rand.Next();

        // Each position is valid for the list as it stands at that edit: the inserts grow it by one each, the
        // removals shrink it by one each (and at ItemCount == EditCount they drain it exactly).
        insertPositions = new int[EditCount];
        for (int i = 0; i < EditCount; i++)
            insertPositions[i] = rand.Next(ItemCount + i + 1);

        removePositions = new int[EditCount];
        for (int i = 0; i < EditCount; i++)
            removePositions[i] = rand.Next(ItemCount - i);

        probes = new int[ProbeCount];
        for (int i = 0; i < ProbeCount; i++)
            probes[i] = rand.Next(ItemCount);

        listFull = new List<int>(items);
        treeFull = new BTreeList<int>(items);
        builderFull = ImmutableList.CreateRange(items).ToBuilder();
    }

    // ---- Insert: EditCount inserts at random positions, the edit List<T> pays O(n) for ---------------

    [IterationSetup(Targets = new[] { nameof(List_Insert), nameof(List_RemoveAt) })]
    public void ResetList() => list = new List<int>(items);

    [IterationSetup(Targets = new[] { nameof(BTreeList_Insert), nameof(BTreeList_RemoveAt), nameof(BTreeList_InsertAvl) })]
    public void ResetTree() => tree = new BTreeList<int>(items);

    [IterationSetup(Target = nameof(ImmutableList_InsertAvl))]
    public void ResetBuilder() => builder = ImmutableList.CreateRange(items).ToBuilder();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Insert")]
    public void List_Insert()
    {
        int[] positions = insertPositions;
        for (int i = 0; i < positions.Length; i++)
            list.Insert(positions[i], i);
    }

    [Benchmark]
    [BenchmarkCategory("Insert")]
    public void BTreeList_Insert()
    {
        int[] positions = insertPositions;
        for (int i = 0; i < positions.Length; i++)
            tree.Insert(positions[i], i);
    }

    // ---- RemoveAt: EditCount removals at random positions ---------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RemoveAt")]
    public void List_RemoveAt()
    {
        int[] positions = removePositions;
        for (int i = 0; i < positions.Length; i++)
            list.RemoveAt(positions[i]);
    }

    [Benchmark]
    [BenchmarkCategory("RemoveAt")]
    public void BTreeList_RemoveAt()
    {
        int[] positions = removePositions;
        for (int i = 0; i < positions.Length; i++)
            tree.RemoveAt(positions[i]);
    }

    // ---- Index: random reads, where a descent loses to one array load ---------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Index")]
    public long List_Index()
    {
        long sum = 0;
        foreach (int p in probes)
            sum += listFull[p];
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Index")]
    public long BTreeList_Index()
    {
        long sum = 0;
        foreach (int p in probes)
            sum += treeFull[p];
        return sum;
    }

    // ---- Enumerate: a front-to-back walk ---------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public long List_Enumerate()
    {
        long sum = 0;
        foreach (int v in listFull)
            sum += v;
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public long BTreeList_Enumerate()
    {
        long sum = 0;
        foreach (int v in treeFull)
            sum += v;
        return sum;
    }

    // ---- InsertAvl / IndexAvl: the same work against ImmutableList<int>.Builder (not charted) ----------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("InsertAvl")]
    public void ImmutableList_InsertAvl()
    {
        int[] positions = insertPositions;
        for (int i = 0; i < positions.Length; i++)
            builder.Insert(positions[i], i);
    }

    [Benchmark]
    [BenchmarkCategory("InsertAvl")]
    public void BTreeList_InsertAvl()
    {
        int[] positions = insertPositions;
        for (int i = 0; i < positions.Length; i++)
            tree.Insert(positions[i], i);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("IndexAvl")]
    public long ImmutableList_IndexAvl()
    {
        long sum = 0;
        foreach (int p in probes)
            sum += builderFull[p];
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("IndexAvl")]
    public long BTreeList_IndexAvl()
    {
        long sum = 0;
        foreach (int p in probes)
            sum += treeFull[p];
        return sum;
    }
}
