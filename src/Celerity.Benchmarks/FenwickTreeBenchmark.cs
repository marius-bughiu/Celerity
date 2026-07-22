using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// FenwickTree<long> vs the plain-array baseline a developer reaches for without a Binary Indexed Tree. The
// BCL ships no prefix-sum structure, so the honest baseline is a raw long[]: point updates are O(1), but
// every prefix / range sum re-adds the slice (O(n)). That is the losing side of the tradeoff the Fenwick
// tree exists to erase — it keeps BOTH the point update and the prefix/range query at O(log n).
//
// Two categories cover the documented BCL-beating shape. Mixed interleaves point updates with prefix-sum
// queries (the headline workload: running aggregates, rank counters, cumulative-frequency tables) where the
// array is O(n) per query; RangeSum runs a batch of half-open range-sum queries against a pre-built
// structure. The baseline arms are named Array_* so the dashboard classifies them as the BCL reference.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class FenwickTreeBenchmark
{
    private long[] initial = null!;    // initial logical values seeding both structures
    private int[] updateIndex = null!; // point-update positions for the mixed stream
    private long[] updateDelta = null!;
    private int[] queryEnd = null!;    // prefix-sum query positions for the mixed stream
    private int[] rangeStart = null!;  // half-open range-sum query bounds for the RangeSum category
    private int[] rangeEnd = null!;

    private FenwickTree<long> fenwickFull = null!;
    private long[] arrayFull = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        initial = new long[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            initial[i] = rand.Next(-100, 100);

        int ops = Math.Min(ItemCount, 10_000);
        updateIndex = new int[ops];
        updateDelta = new long[ops];
        queryEnd = new int[ops];
        rangeStart = new int[ops];
        rangeEnd = new int[ops];
        for (int i = 0; i < ops; i++)
        {
            updateIndex[i] = rand.Next(ItemCount);
            updateDelta[i] = rand.Next(-100, 100);
            queryEnd[i] = rand.Next(ItemCount + 1);

            int a = rand.Next(ItemCount + 1);
            int b = rand.Next(ItemCount + 1);
            if (a > b)
                (a, b) = (b, a);
            rangeStart[i] = a;
            rangeEnd[i] = b;
        }

        fenwickFull = new FenwickTree<long>(initial);
        arrayFull = (long[])initial.Clone();
    }

    // ---- Mixed: interleave point updates with prefix-sum queries (the headline O(log n) vs O(n) split) ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Mixed")]
    public long Array_Mixed()
    {
        long[] values = (long[])initial.Clone();
        long sink = 0;
        for (int i = 0; i < updateIndex.Length; i++)
        {
            values[updateIndex[i]] += updateDelta[i];

            // Prefix sum by re-adding the slice — O(n) per query.
            long sum = 0;
            int end = queryEnd[i];
            for (int j = 0; j < end; j++)
                sum += values[j];
            sink += sum;
        }

        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("Mixed")]
    public long FenwickTree_Mixed()
    {
        var tree = new FenwickTree<long>(initial);
        long sink = 0;
        for (int i = 0; i < updateIndex.Length; i++)
        {
            tree.Add(updateIndex[i], updateDelta[i]);
            sink += tree.PrefixSum(queryEnd[i]);
        }

        return sink;
    }

    // ---- RangeSum: a batch of half-open range-sum queries against the pre-built structure ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RangeSum")]
    public long Array_RangeSum()
    {
        long sink = 0;
        for (int i = 0; i < rangeStart.Length; i++)
        {
            long sum = 0;
            int end = rangeEnd[i];
            for (int j = rangeStart[i]; j < end; j++)
                sum += arrayFull[j];
            sink += sum;
        }

        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("RangeSum")]
    public long FenwickTree_RangeSum()
    {
        long sink = 0;
        for (int i = 0; i < rangeStart.Length; i++)
            sink += fenwickFull.RangeSum(rangeStart[i], rangeEnd[i]);

        return sink;
    }
}
