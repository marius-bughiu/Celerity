using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// GapBuffer<int> vs the List<int> it replaces for a sequence edited around a moving cursor. List<int> is the
// charted baseline: every Insert / RemoveAt that is not at the end shifts every later element, so an edit
// near the middle is O(n) however close it is to the previous one, while the gap buffer pays only the
// distance the cursor moved.
//
// CursorEdit applies EditCount edits, alternating Insert and RemoveAt, at a cursor that starts at the middle
// and drifts by a step in [-MaxDrift, MaxDrift] after each edit — the shape of typing and deleting behind a
// caret. The positions are drawn once in [GlobalSetup], so every arm edits the same positions in the same
// order, and the subjects are rebuilt in an [IterationSetup] because the arm mutates them. Both the List and
// the GapBuffer are pre-sized to the same capacity (ItemCount + 1, the most the alternation ever holds), so
// neither arm pays a growth inside the measurement; the gap starts at the end, where the constructor leaves
// it, and the first edit pays to move it to the cursor.
//
// BTreeList_CrossCursorEdit is the honest other half of the trade: the O(log n)-anywhere list on the same
// edits. It carries an op name of its own so it lands in a dashboard bucket by itself — two Celerity arms
// sharing an op would overwrite each other in the index the cards are built from — while the shared
// [BenchmarkCategory] keeps the BenchmarkDotNet report grouping all three against one baseline.
//
// RandomRead and Enumerate run against a buffer whose gap sits in the MIDDLE (GapPosition == ItemCount / 2),
// so half the reads land on each side of it and the branch-free position mapping is actually exercised; a
// gap at the end would make every read look like an array load. Append builds the list by Add from empty,
// with neither side pre-sized.
//
// The baseline arms are named List_* so the dashboard classifies them as the BCL reference.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class GapBufferBenchmark
{
    private const int EditCount = 1_000;
    private const int ReadCount = 1_000;
    private const int MaxDrift = 8;

    private int[] items = null!;
    private int[] editPositions = null!;
    private int[] probes = null!;

    // Edit subjects, rebuilt per iteration by the [IterationSetup]s below.
    private List<int> list = null!;
    private GapBuffer<int> buffer = null!;
    private BTreeList<int> tree = null!;

    // Full, warm subjects for the non-destructive read categories, built once in [GlobalSetup].
    private List<int> listFull = null!;
    private GapBuffer<int> bufferFull = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        items = new int[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            items[i] = rand.Next();

        // Even edits insert into a list of ItemCount elements (valid positions [0, ItemCount]) and odd edits
        // remove from one of ItemCount + 1 (valid positions [0, ItemCount]), so one clamp serves both.
        editPositions = new int[EditCount];
        int cursor = ItemCount / 2;
        for (int i = 0; i < EditCount; i++)
        {
            editPositions[i] = cursor;
            cursor = Math.Clamp(cursor + rand.Next(-MaxDrift, MaxDrift + 1), 0, ItemCount);
        }

        probes = new int[ReadCount];
        for (int i = 0; i < ReadCount; i++)
            probes[i] = rand.Next(ItemCount);

        listFull = new List<int>(items);

        // Opening and closing an edit at the middle leaves the gap there.
        int middle = ItemCount / 2;
        bufferFull = new GapBuffer<int>(items);
        bufferFull.Insert(middle, 0);
        bufferFull.RemoveAt(middle);
        if (bufferFull.GapPosition != middle || bufferFull.Count != ItemCount)
            throw new InvalidOperationException($"Read subject's gap is at {bufferFull.GapPosition}, not {middle}.");
    }

    // ---- CursorEdit: alternating Insert / RemoveAt at a drifting cursor --------------------------------

    [IterationSetup(Target = nameof(List_CursorEdit))]
    public void ResetList()
    {
        list = new List<int>(ItemCount + 1);
        list.AddRange(items);
    }

    [IterationSetup(Target = nameof(GapBuffer_CursorEdit))]
    public void ResetBuffer()
    {
        buffer = new GapBuffer<int>(ItemCount + 1);
        buffer.InsertRange(0, items);
    }

    [IterationSetup(Target = nameof(BTreeList_CrossCursorEdit))]
    public void ResetTree() => tree = new BTreeList<int>(items);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CursorEdit")]
    public void List_CursorEdit()
    {
        int[] positions = editPositions;
        for (int i = 0; i < positions.Length; i += 2)
        {
            list.Insert(positions[i], i);
            list.RemoveAt(positions[i + 1]);
        }
    }

    [Benchmark]
    [BenchmarkCategory("CursorEdit")]
    public void GapBuffer_CursorEdit()
    {
        int[] positions = editPositions;
        for (int i = 0; i < positions.Length; i += 2)
        {
            buffer.Insert(positions[i], i);
            buffer.RemoveAt(positions[i + 1]);
        }
    }

    [Benchmark]
    [BenchmarkCategory("CursorEdit")]
    public void BTreeList_CrossCursorEdit()
    {
        int[] positions = editPositions;
        for (int i = 0; i < positions.Length; i += 2)
        {
            tree.Insert(positions[i], i);
            tree.RemoveAt(positions[i + 1]);
        }
    }

    // ---- RandomRead: indexer reads at random positions, gap in the middle ------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RandomRead")]
    public long List_RandomRead()
    {
        long sum = 0;
        foreach (int p in probes)
            sum += listFull[p];
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("RandomRead")]
    public long GapBuffer_RandomRead()
    {
        long sum = 0;
        foreach (int p in probes)
            sum += bufferFull[p];
        return sum;
    }

    // ---- Enumerate: a front-to-back walk across the gap ------------------------------------------------

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
    public long GapBuffer_Enumerate()
    {
        long sum = 0;
        foreach (int v in bufferFull)
            sum += v;
        return sum;
    }

    // ---- Append: build ItemCount elements by Add from empty, neither side pre-sized ---------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Append")]
    public List<int> List_Append()
    {
        var l = new List<int>();
        int[] source = items;
        for (int i = 0; i < source.Length; i++)
            l.Add(source[i]);
        return l;
    }

    [Benchmark]
    [BenchmarkCategory("Append")]
    public GapBuffer<int> GapBuffer_Append()
    {
        var b = new GapBuffer<int>();
        int[] source = items;
        for (int i = 0; i < source.Length; i++)
            b.Add(source[i]);
        return b;
    }
}
