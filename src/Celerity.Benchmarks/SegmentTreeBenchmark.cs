using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// SegmentTree<long, MinMonoid<long>> vs the plain-array baseline, on the fold FenwickTree structurally cannot
// answer. A Fenwick range query is the difference of two prefix folds, so it needs an inverse; minimum has
// none. The BCL ships no range-aggregate structure at all — not even a Span<T>.Min to lean on — so the honest
// baseline is a raw long[] and a hand-written loop folding the slice: O(n) per query, while precomputing the
// answers instead would make every point update O(n). Range minimum is the cheapest fold that loop can carry,
// which makes it the baseline's best case and the fair one to measure against.
//
// Two categories cover the documented BCL-beating shape. Mixed interleaves point updates with range-minimum
// queries (the headline workload: sliding-window minima over a mutating history, "cheapest offer in this
// band" over a live book) where the array is O(n) per query; RangeMin runs a batch of half-open queries
// against a pre-built structure. The baseline arms are named Array_* so the dashboard classifies them as the
// reference series.
//
// The operation count is capped well below the FenwickTree benchmark's 10,000. Both baseline arms are
// quadratic in ItemCount, and at 100,000 elements that cap is what keeps the class inside a benchmark shard's
// budget — the ratio the card reports is unaffected by how many operations it is averaged over.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SegmentTreeBenchmark
{
    private long[] initial = null!;    // initial logical values seeding both structures
    private int[] updateIndex = null!; // point-update positions for the mixed stream
    private long[] updateValue = null!;
    private int[] rangeStart = null!;  // half-open range bounds, shared by both categories
    private int[] rangeEnd = null!;

    private SegmentTree<long, MinMonoid<long>> segmentFull = null!;
    private long[] arrayFull = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        initial = new long[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            initial[i] = rand.Next(-1_000_000, 1_000_000);

        int ops = Math.Min(ItemCount, 2_000);
        updateIndex = new int[ops];
        updateValue = new long[ops];
        rangeStart = new int[ops];
        rangeEnd = new int[ops];
        for (int i = 0; i < ops; i++)
        {
            updateIndex[i] = rand.Next(ItemCount);
            updateValue[i] = rand.Next(-1_000_000, 1_000_000);

            int a = rand.Next(ItemCount + 1);
            int b = rand.Next(ItemCount + 1);
            if (a > b)
                (a, b) = (b, a);
            rangeStart[i] = a;
            rangeEnd[i] = b;
        }

        segmentFull = new SegmentTree<long, MinMonoid<long>>(initial);
        arrayFull = (long[])initial.Clone();
    }

    // ---- Mixed: interleave point updates with range-minimum queries (the O(log n) vs O(n) split) ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Mixed")]
    public long Array_Mixed()
    {
        long[] values = (long[])initial.Clone();
        long sink = 0;
        for (int i = 0; i < updateIndex.Length; i++)
        {
            values[updateIndex[i]] = updateValue[i];

            // Range minimum by scanning the slice — O(n) per query, and there is no precomputation that
            // survives the update on the line above.
            long min = long.MaxValue;
            int end = rangeEnd[i];
            for (int j = rangeStart[i]; j < end; j++)
            {
                if (values[j] < min)
                    min = values[j];
            }

            sink += min;
        }

        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("Mixed")]
    public long SegmentTree_Mixed()
    {
        var tree = new SegmentTree<long, MinMonoid<long>>(initial);
        long sink = 0;
        for (int i = 0; i < updateIndex.Length; i++)
        {
            tree[updateIndex[i]] = updateValue[i];
            sink += tree.Query(rangeStart[i], rangeEnd[i]);
        }

        return sink;
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
                if (arrayFull[j] < min)
                    min = arrayFull[j];
            }

            sink += min;
        }

        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("RangeMin")]
    public long SegmentTree_RangeMin()
    {
        long sink = 0;
        for (int i = 0; i < rangeStart.Length; i++)
            sink += segmentFull.Query(rangeStart[i], rangeEnd[i]);

        return sink;
    }
}
