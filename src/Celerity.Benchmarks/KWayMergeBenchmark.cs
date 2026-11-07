using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Sorting;

// KWayMerge against what a caller writes without it. There is no BCL multiway merge, so each
// category pairs the loser tree with the closest thing .NET offers for the same job:
//
//   Merge         — 16 sorted int runs, against the PriorityQueue hand-roll: enqueue each run's
//                   head with its key as the priority, dequeue the minimum, enqueue that run's next.
//                   Int keys are the case where the comparisons are cheapest, so this is where the
//                   loser tree's smaller comparison count matters least.
//   MergeWide     — the same at 256 runs, past the 128-run stack limit, where the rented-bookkeeping
//                   path runs and the tree is eight levels deep.
//   MergeStrings  — 16 runs of ordinal-sorted strings, against the same PriorityQueue hand-roll
//                   with StringComparer.Ordinal. The documented win: when a comparison is expensive,
//                   doing at most ⌈log₂ k⌉ of them per element instead of a heap's sift is the whole cost.
//   ConcatSort    — the same 16 string runs against the other BCL answer: copy them into one array
//                   and Array.Sort it, which throws the runs' order away and pays O(n log n).
//   Lazy          — the streaming form over 16 IEnumerable<int> runs, against the PriorityQueue
//                   hand-roll written as an iterator over the same enumerators, so both sides are a
//                   lazy IEnumerable<int> the caller drives. Both pay interface calls per element,
//                   which dominate at int keys — expect near parity here, not a headline.
//
// Not measured as a card, and stated in the docs instead: for primitive keys that are all in
// memory, RadixSort over the concatenation is several times faster than any merge, because it
// does no comparisons at all.
//
// Every arm reads a pristine source and writes a preallocated destination, so no [IterationSetup]
// is needed. ItemCount is the parameter name the gh-pages dashboard's benchmark-name parser
// requires; it is the total element count across all runs.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class KWayMergeBenchmark
{
    private const int Runs = 16;
    private const int WideRuns = 256;

    private int[] ints = null!;
    private int[] intLengths = null!;
    private int[] wideInts = null!;
    private int[] wideLengths = null!;
    private string[] strings = null!;
    private string[] stringWork = null!;
    private int[] stringLengths = null!;
    private IEnumerable<int>[] intSequences = null!;
    private int[] intDestination = null!;
    private string[] stringDestination = null!;

    [Params(1_000, 100_000, 1_000_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        (ints, intLengths) = SortedRuns(Runs, () => rand.Next(), Comparer<int>.Default);
        (wideInts, wideLengths) = SortedRuns(WideRuns, () => rand.Next(), Comparer<int>.Default);

        // Keys that share a long prefix, as identifiers and paths do, so an ordinal comparison
        // costs more than the first character.
        (strings, stringLengths) = SortedRuns(
            Runs,
            () => "tenant-07/user/" + rand.Next(1_000_000).ToString("D7") + "/" + rand.Next(1_000),
            StringComparer.Ordinal);

        intSequences = new IEnumerable<int>[Runs];
        int offset = 0;
        for (int r = 0; r < Runs; r++)
        {
            intSequences[r] = new ArraySegment<int>(ints, offset, intLengths[r]);
            offset += intLengths[r];
        }

        intDestination = new int[ItemCount];
        stringDestination = new string[ItemCount];
        stringWork = new string[ItemCount];
    }

    private (T[] Source, int[] Lengths) SortedRuns<T>(int runs, Func<T> next, IComparer<T> comparer)
    {
        var source = new T[ItemCount];
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = next();
        }

        var lengths = new int[runs];
        int offset = 0;
        for (int r = 0; r < runs; r++)
        {
            lengths[r] = ItemCount / runs + (r < ItemCount % runs ? 1 : 0);
            Array.Sort(source, offset, lengths[r], comparer);
            offset += lengths[r];
        }

        return (source, lengths);
    }

    // The idiomatic hand-rolled k-way merge: a heap of run indices keyed by each run's head.
    private static void HeapMerge<T>(T[] source, int[] lengths, T[] destination, IComparer<T>? comparer)
    {
        int k = lengths.Length;
        var heap = new PriorityQueue<int, T>(k, comparer);
        var position = new int[k];
        var end = new int[k];
        int offset = 0;
        for (int r = 0; r < k; r++)
        {
            position[r] = offset;
            offset += lengths[r];
            end[r] = offset;
            if (position[r] < end[r])
            {
                heap.Enqueue(r, source[position[r]]);
            }
        }

        int o = 0;
        while (heap.TryDequeue(out int run, out _))
        {
            destination[o++] = source[position[run]++];
            if (position[run] < end[run])
            {
                heap.Enqueue(run, source[position[run]]);
            }
        }
    }

    // ---- Merge: 16 int runs ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Merge")]
    public int[] PriorityQueue_Merge()
    {
        HeapMerge(ints, intLengths, intDestination, null);
        return intDestination;
    }

    [Benchmark]
    [BenchmarkCategory("Merge")]
    public int[] KWayMerge_Merge()
    {
        KWayMerge.Merge<int>(ints, intLengths, intDestination);
        return intDestination;
    }

    // ---- MergeWide: 256 int runs ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("MergeWide")]
    public int[] PriorityQueue_MergeWide()
    {
        HeapMerge(wideInts, wideLengths, intDestination, null);
        return intDestination;
    }

    [Benchmark]
    [BenchmarkCategory("MergeWide")]
    public int[] KWayMerge_MergeWide()
    {
        KWayMerge.Merge<int>(wideInts, wideLengths, intDestination);
        return intDestination;
    }

    // ---- MergeStrings: 16 ordinal string runs ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("MergeStrings")]
    public string[] PriorityQueue_MergeStrings()
    {
        HeapMerge(strings, stringLengths, stringDestination, StringComparer.Ordinal);
        return stringDestination;
    }

    [Benchmark]
    [BenchmarkCategory("MergeStrings")]
    public string[] KWayMerge_MergeStrings()
    {
        KWayMerge.Merge(strings, stringLengths, stringDestination.AsSpan(), default(OrdinalComparer));
        return stringDestination;
    }

    // ---- ConcatSort: the same string runs against concatenate-and-sort ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ConcatSort")]
    public string[] Array_ConcatSort()
    {
        strings.CopyTo(stringWork, 0);
        Array.Sort(stringWork, StringComparer.Ordinal);
        return stringWork;
    }

    [Benchmark]
    [BenchmarkCategory("ConcatSort")]
    public string[] KWayMerge_ConcatSort()
    {
        KWayMerge.Merge(strings, stringLengths, stringDestination.AsSpan(), default(OrdinalComparer));
        return stringDestination;
    }

    // ---- Lazy: 16 int sequences, streamed ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lazy")]
    public long PriorityQueue_Lazy()
    {
        long sum = 0;
        foreach (int value in HeapMergeLazy(intSequences))
        {
            sum += value;
        }

        return sum;
    }

    // The like-for-like hand-roll for the lazy form: the same heap merge, written as an iterator so it
    // too yields one element at a time through an IEnumerator<int> the caller drives.
    private static IEnumerable<int> HeapMergeLazy(IEnumerable<int>[] sources)
    {
        var enumerators = new IEnumerator<int>[sources.Length];
        var heap = new PriorityQueue<int, int>(sources.Length);
        try
        {
            for (int r = 0; r < sources.Length; r++)
            {
                enumerators[r] = sources[r].GetEnumerator();
                if (enumerators[r].MoveNext())
                {
                    heap.Enqueue(r, enumerators[r].Current);
                }
            }

            while (heap.TryDequeue(out int run, out int value))
            {
                yield return value;
                if (enumerators[run].MoveNext())
                {
                    heap.Enqueue(run, enumerators[run].Current);
                }
            }
        }
        finally
        {
            foreach (IEnumerator<int>? enumerator in enumerators)
            {
                enumerator?.Dispose();
            }
        }
    }

    [Benchmark]
    [BenchmarkCategory("Lazy")]
    public long KWayMerge_Lazy()
    {
        long sum = 0;
        foreach (int value in KWayMerge.Merge(intSequences))
        {
            sum += value;
        }

        return sum;
    }

    private readonly struct OrdinalComparer : IComparer<string>
    {
        public int Compare(string? x, string? y) => string.CompareOrdinal(x, y);
    }
}
