using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// SparseTable<long, MinMonoid<long>> vs the plain-array baseline and vs SegmentTree<long, MinMonoid<long>>, on
// the same fold and the same shape SegmentTreeBenchmark measures — minus the mutation. The BCL ships no
// range-aggregate structure at all, so the outside baseline is still a raw long[] and a hand-written loop
// folding the slice: O(n) per query. Range minimum is the cheapest fold that loop can carry, which makes it
// the baseline's best case and the fair one to measure against.
//
// The third arm is the point of the class. Both Celerity types answer the same question, and the documented
// reason to reach for this one is that the sequence never changes after build — so the comparison that decides
// between them is SparseTable's O(1) two-load query against SegmentTree's O(log n) descent, and the price it
// is paid for. Both BenchmarkDotNet categories therefore carry all three arms:
//
//   RangeMin — a batch of half-open range-minimum queries against a pre-built structure. This is where the
//              table is supposed to win, and by a constant factor over SegmentTree rather than an asymptotic
//              one over the array.
//   Build    — the same structures constructed from the same seed. This is what the O(1) query costs:
//              O(n log n) time into levels * n cells, against SegmentTree's O(n) into 2n. A caller who queries
//              a handful of times should read these two together rather than only the first.
//
// Measuring all three in one class rather than reading SegmentTree's own card off the dashboard is deliberate:
// SegmentTreeBenchmark draws its range bounds from a different point in its own Random(42) stream, so a
// cross-class ratio would be comparing two different query sets. Here every arm queries the same rangeStart /
// rangeEnd arrays built once in Setup.
//
// ---- Why the SegmentTree arms are named Cross* -------------------------------------------------------------
//
// The dashboard's data model is two series per (collection, op, itemCount) bucket: web/dev/bench/index.html
// parses the *method* name, classifies the type before the underscore as BCL or Celerity, and stores one value
// under each. Two Celerity arms sharing an op would land in the same slot and one would silently overwrite the
// other — the card would then plot whichever ran last while labelling it SparseTable.
//
// So the two charted pairs keep the plain op names (Array_* against SparseTable_*, which is what the
// SparseTable cards render) and the SegmentTree arms take an op of their own. Their bucket holds a single
// value and no BCL arm, so no card is drawn for it and nothing is overwritten; the numbers are read off the
// BenchmarkDotNet report, where the BenchmarkCategory keeps all three arms in one group against the same
// Array baseline. Charting them instead would mean teaching every consumer of that index to key by type name,
// which is a change to shared dashboard code that 63 collections depend on.
//
// The baseline arms are named Array_* so the dashboard classifies them as the reference series. The query
// count is capped the same way SegmentTreeBenchmark caps its operation count: the Array_RangeMin arm is
// quadratic in ItemCount, and at 100,000 elements that cap is what keeps the class inside a benchmark shard's
// budget. The ratio the card reports is unaffected by how many queries it is averaged over.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SparseTableBenchmark
{
    private long[] initial = null!;   // the immutable sequence both structures index
    private int[] rangeStart = null!; // half-open range bounds, shared by every arm
    private int[] rangeEnd = null!;

    private SparseTable<long, MinMonoid<long>> sparseFull = null!;
    private SegmentTree<long, MinMonoid<long>> segmentFull = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        initial = new long[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            initial[i] = rand.Next(-1_000_000, 1_000_000);

        int queries = Math.Min(ItemCount, 2_000);
        rangeStart = new int[queries];
        rangeEnd = new int[queries];
        for (int i = 0; i < queries; i++)
        {
            int a = rand.Next(ItemCount + 1);
            int b = rand.Next(ItemCount + 1);
            if (a > b)
                (a, b) = (b, a);
            rangeStart[i] = a;
            rangeEnd[i] = b;
        }

        sparseFull = new SparseTable<long, MinMonoid<long>>(initial);
        segmentFull = new SegmentTree<long, MinMonoid<long>>(initial);
    }

    // ---- RangeMin: a batch of half-open range-minimum queries against the pre-built structure ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RangeMin")]
    public long Array_RangeMin()
    {
        long sink = 0;
        for (int i = 0; i < rangeStart.Length; i++)
        {
            long min = long.MaxValue;
            int end = rangeEnd[i];
            for (int j = rangeStart[i]; j < end; j++)
            {
                if (initial[j] < min)
                    min = initial[j];
            }

            sink += min;
        }

        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("RangeMin")]
    public long SegmentTree_CrossRangeMin()
    {
        long sink = 0;
        for (int i = 0; i < rangeStart.Length; i++)
            sink += segmentFull.Query(rangeStart[i], rangeEnd[i]);

        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("RangeMin")]
    public long SparseTable_RangeMin()
    {
        long sink = 0;
        for (int i = 0; i < rangeStart.Length; i++)
            sink += sparseFull.Query(rangeStart[i], rangeEnd[i]);

        return sink;
    }

    // ---- Build: what the O(1) query costs up front ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public long Array_Build()
    {
        // The "no structure at all" arm: the array a caller keeps anyway, copied once. It answers no range in
        // better than O(n), which is what the RangeMin category charges it for.
        long[] values = (long[])initial.Clone();

        return values.Length;
    }

    [Benchmark]
    [BenchmarkCategory("Build")]
    public long SegmentTree_CrossBuild()
    {
        var tree = new SegmentTree<long, MinMonoid<long>>(initial);

        return tree.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Build")]
    public long SparseTable_Build()
    {
        var table = new SparseTable<long, MinMonoid<long>>(initial);

        return table.Count;
    }
}
