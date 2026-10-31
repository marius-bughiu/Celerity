using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
// Microsoft.CodeAnalysis 4.1.0 (a BenchmarkDotNet dependency) exposes a System.Collections.Generic.SegmentedList<T>,
// which the implicit using of that namespace makes ambiguous with Celerity's, so the type is aliased here.
using SegmentedIntList = Celerity.Collections.SegmentedList<int>;

// SegmentedList<int> vs the List<int> a caller builds by appending when the final size is not known up front.
// The baseline is deliberately NOT pre-sized: pre-sizing is the case where List<T> already avoids every cost
// this type exists to remove, and the pitch is the list whose count you cannot know in advance.
//
// Add is the headline: List<int> doubles one contiguous array, copying every element on each growth and, past
// about 21,000 ints, allocating each new array on the Large Object Heap, where it counts toward a gen-2
// collection. [MemoryDiagnoser] reports both the allocated bytes and the gen-2 collections per 1,000 builds,
// which are the two quantities the pre-registered criteria are written against. The 1,000,000 case is what
// makes the gen-2 column meaningful; 1,000 sits below the LOH threshold and is the small-list check.
//
// Index, Enumerate and SpanScan are the honest costs and ship as charted rows: an indexed read is two dependent
// loads instead of one, the struct enumerator is slower than List's, and SpanScan pairs a GetSegment walk with
// the CollectionsMarshal.AsSpan scan a caller would write over a List<int>. The read arms run against subjects
// built once in [GlobalSetup].
//
// The baseline arms are named List_* so the dashboard classifies them as the BCL reference, and each op name is
// shared by exactly one List_ arm and one SegmentedList_ arm.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SegmentedListBenchmark
{
    private const int ProbeCount = 10_000;

    private int[] items = null!;
    private int[] probes = null!;

    private List<int> listFull = null!;
    private SegmentedIntList segmentedFull = null!;

    [Params(1_000, 100_000, 1_000_000)]
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

        listFull = new List<int>(items);
        segmentedFull = new SegmentedIntList(items);
    }

    // ---- Add: build a list of ItemCount elements by appending, with no pre-size ------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public List<int> List_Add()
    {
        int[] source = items;
        var list = new List<int>();
        for (int i = 0; i < source.Length; i++)
            list.Add(source[i]);
        return list;
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public SegmentedIntList SegmentedList_Add()
    {
        int[] source = items;
        var list = new SegmentedIntList();
        for (int i = 0; i < source.Length; i++)
            list.Add(source[i]);
        return list;
    }

    // ---- Index: ProbeCount random reads, where the segmented layout pays a second dependent load --------

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
    public long SegmentedList_Index()
    {
        long sum = 0;
        foreach (int p in probes)
            sum += segmentedFull[p];
        return sum;
    }

    // ---- Enumerate: a front-to-back walk through each type's struct enumerator ---------------------------

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
    public long SegmentedList_Enumerate()
    {
        long sum = 0;
        foreach (int v in segmentedFull)
            sum += v;
        return sum;
    }

    // ---- SpanScan: the same walk over spans — CollectionsMarshal.AsSpan vs a GetSegment walk -------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SpanScan")]
    public long List_SpanScan()
    {
        long sum = 0;
        foreach (int v in CollectionsMarshal.AsSpan(listFull))
            sum += v;
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("SpanScan")]
    public long SegmentedList_SpanScan()
    {
        SegmentedIntList list = segmentedFull;
        long sum = 0;
        int segments = list.SegmentCount;
        for (int s = 0; s < segments; s++)
        {
            foreach (int v in list.GetSegment(s))
                sum += v;
        }
        return sum;
    }
}
