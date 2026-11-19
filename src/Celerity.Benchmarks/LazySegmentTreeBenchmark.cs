using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// LazySegmentTree vs the plain-array baseline, on interleaved range updates and range queries — the shape neither
// FenwickTree nor SegmentTree serves. The BCL ships no range-aggregate structure, so the honest baseline is a raw
// long[]: a range update is a loop over the slice and a range query is another. That is also the strongest
// non-lazy baseline in the library. SegmentTree would turn a k-element range update into k point updates at
// O(log n) each — strictly more work than the array's k stores — so it is not measured as a separate arm.
//
// Two categories, one per pair of shipped action and fold:
// - AddMax: range add + range maximum (the reservation workload: book a seat over a time range, ask the peak
//   occupancy of another range). MinMaxAddAction over MaxMonoid.
// - AssignSum: range assign + range sum (span painting with span totals). SumAssignAction over SumMonoid. The
//   array arm uses Span<T>.Fill, the BCL's vectorized fill, for the update half.
// Each operation is one update followed by one query over an independently drawn range, so both halves are
// measured. The baseline arms are named Array_* so the dashboard classifies them as the reference series.
//
// The operation count is capped at 2,000, matching SegmentTreeBenchmark: the array arms are quadratic in
// ItemCount, and at 100,000 elements the cap is what keeps the class inside a benchmark shard's budget. The
// ratio the card reports does not depend on how many operations it is averaged over. Every arm rebuilds its
// structure from the same seed values, so each invocation replays the same script from the same start.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class LazySegmentTreeBenchmark
{
    private long[] initial = null!;   // initial logical values seeding every structure
    private int[] updateStart = null!; // half-open update ranges
    private int[] updateEnd = null!;
    private long[] updateValue = null!;
    private int[] queryStart = null!;  // half-open query ranges, drawn independently of the updates
    private int[] queryEnd = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        initial = new long[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            initial[i] = rand.Next(0, 1_000);

        int ops = Math.Min(ItemCount, 2_000);
        updateStart = new int[ops];
        updateEnd = new int[ops];
        updateValue = new long[ops];
        queryStart = new int[ops];
        queryEnd = new int[ops];
        for (int i = 0; i < ops; i++)
        {
            (updateStart[i], updateEnd[i]) = DrawRange(rand);
            updateValue[i] = rand.Next(-50, 50);
            (queryStart[i], queryEnd[i]) = DrawRange(rand);
        }
    }

    private (int Start, int End) DrawRange(Random rand)
    {
        int a = rand.Next(ItemCount + 1);
        int b = rand.Next(ItemCount + 1);
        return a <= b ? (a, b) : (b, a);
    }

    // ---- AddMax: range add + range maximum ----------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("AddMax")]
    public long Array_AddMax()
    {
        long[] values = (long[])initial.Clone();
        long sink = 0;
        for (int i = 0; i < updateStart.Length; i++)
        {
            long delta = updateValue[i];
            int end = updateEnd[i];
            for (int j = updateStart[i]; j < end; j++)
                values[j] += delta;

            long max = long.MinValue;
            end = queryEnd[i];
            for (int j = queryStart[i]; j < end; j++)
            {
                if (values[j] > max)
                    max = values[j];
            }

            sink += max;
        }

        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("AddMax")]
    public long LazySegmentTree_AddMax()
    {
        var tree = new LazySegmentTree<long, long, MaxMonoid<long>, MinMaxAddAction<long>>(initial);
        long sink = 0;
        for (int i = 0; i < updateStart.Length; i++)
        {
            tree.Apply(updateStart[i], updateEnd[i], updateValue[i]);
            sink += tree.Query(queryStart[i], queryEnd[i]);
        }

        return sink;
    }

    // ---- AssignSum: range assign + range sum --------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("AssignSum")]
    public long Array_AssignSum()
    {
        long[] values = (long[])initial.Clone();
        long sink = 0;
        for (int i = 0; i < updateStart.Length; i++)
        {
            values.AsSpan(updateStart[i], updateEnd[i] - updateStart[i]).Fill(updateValue[i]);

            long sum = 0;
            int end = queryEnd[i];
            for (int j = queryStart[i]; j < end; j++)
                sum += values[j];

            sink += sum;
        }

        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("AssignSum")]
    public long LazySegmentTree_AssignSum()
    {
        var tree = new LazySegmentTree<long, long?, SumMonoid<long>, SumAssignAction<long>>(initial);
        long sink = 0;
        for (int i = 0; i < updateStart.Length; i++)
        {
            tree.Apply(updateStart[i], updateEnd[i], updateValue[i]);
            sink += tree.Query(queryStart[i], queryEnd[i]);
        }

        return sink;
    }
}
