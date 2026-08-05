using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Sorting;

// PartialSort against the three things a caller reaches for instead, one per category — because the
// honest comparison depends on which one you mean:
//
//   Select      — full Array.Sort. The O(n) quickselect against the O(n log n) sort that answers the
//                 same top-k question by ordering everything.
//   SortPrefix  — full Array.Sort again, but now the Celerity arm also orders the k it kept, so the
//                 row shows what the ordering costs on top of the selection.
//   TopK        — LINQ's OrderByDescending().Take(k). NOT a full sort: LINQ has applied its own
//                 partial-sort optimization since .NET 6, so the expected win here is allocation and
//                 boxing, not asymptotics. Naming it otherwise in the docs would be false.
//   TopKHeap    — the bounded min-heap a careful caller hand-rolls with PriorityQueue. This is the
//                 tightest baseline of the four and the one that keeps the package honest: it has
//                 the same O(n log k) shape, so what is left to measure is the allocation, the
//                 comparer dispatch, and the heap's constant factor.
//
// k is 1% of the span (floor 1), which is the regime top-k is actually used in. Both arms of every
// category copy or scan from a pristine source inside the measured method, so no [IterationSetup] is
// needed. ItemCount is the parameter name the gh-pages dashboard's benchmark-name parser requires.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class PartialSortBenchmark
{
    private int[] source = null!;
    private int[] work = null!;
    private int[] destination = null!;
    private int k;

    [Params(100, 1_000, 100_000, 1_000_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        source = new int[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            source[i] = rand.Next();
        }

        k = Math.Max(1, ItemCount / 100);
        work = new int[ItemCount];
        destination = new int[k];
    }

    // ---- Select: the k smallest, unordered ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Select")]
    public int[] Array_Select()
    {
        source.CopyTo(work, 0);
        Array.Sort(work);
        return work;
    }

    [Benchmark]
    [BenchmarkCategory("Select")]
    public int[] PartialSort_Select()
    {
        source.CopyTo(work, 0);
        PartialSort.Select(work.AsSpan(), k);
        return work;
    }

    // ---- SortPrefix: the k smallest, in order ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("SortPrefix")]
    public int[] Array_SortPrefix()
    {
        source.CopyTo(work, 0);
        Array.Sort(work);
        return work;
    }

    [Benchmark]
    [BenchmarkCategory("SortPrefix")]
    public int[] PartialSort_SortPrefix()
    {
        source.CopyTo(work, 0);
        PartialSort.Sort(work.AsSpan(), k);
        return work;
    }

    // ---- TopK: the k largest, without touching the source ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TopK")]
    public int[] Array_TopK() => source.OrderByDescending(x => x).Take(k).ToArray();

    [Benchmark]
    [BenchmarkCategory("TopK")]
    public int[] PartialSort_TopK()
    {
        PartialSort.TopK<int>(source, destination.AsSpan());
        return destination;
    }

    // ---- TopKHeap: the same answer against a hand-rolled bounded heap ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TopKHeap")]
    public int[] Array_TopKHeap()
    {
        // The idiomatic hand-rolled version: a min-heap of size k, evicting its root whenever a
        // larger element arrives. PriorityQueue is the BCL's only heap.
        var heap = new PriorityQueue<int, int>(k);
        for (int i = 0; i < source.Length; i++)
        {
            if (heap.Count < k)
            {
                heap.Enqueue(source[i], source[i]);
            }
            else if (source[i] > heap.Peek())
            {
                heap.EnqueueDequeue(source[i], source[i]);
            }
        }

        for (int i = k - 1; i >= 0; i--)
        {
            destination[i] = heap.Dequeue();
        }

        return destination;
    }

    [Benchmark]
    [BenchmarkCategory("TopKHeap")]
    public int[] PartialSort_TopKHeap()
    {
        PartialSort.TopK<int>(source, destination.AsSpan());
        return destination;
    }
}
