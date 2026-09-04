using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// WaveletTree vs the code a caller writes by hand over the same int[]. There is no BCL counterpart and no
// counterpart elsewhere in this library either — SegmentTree folds a window down to one value and so cannot
// answer the k-th smallest, and RankedSet ranks a whole set with no positional window and no duplicates — so
// the honest baselines are a sort of the window for a quantile and a counting loop for everything else. They
// are marked Baseline in every category and named Array_* so the dashboard classifies them as the reference
// arm.
//
// The window is 1% of the sequence, which is where the shapes diverge: the baseline's cost is O(window) (or
// O(window log window) for the quantile) while every indexed answer is a fixed ten-level descent. At
// ItemCount = 1024 that window is ten elements and the descent is the more expensive of the two — that arm is
// kept precisely because it is where the constant loses, and the docs say so. Build is the price of the index,
// pure overhead a caller only recovers by querying many times; the space price (IndexSizeInBytes, ~1.25 bits
// per element per level) is what a timing benchmark cannot show, and the docs carry that number.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class WaveletTreeBenchmark
{
    private const int QueryCount = 100;

    // 1024 distinct values is exactly ten levels at both sizes, so the sweep varies the sequence length
    // without also varying the descent depth. Sampling alone would not guarantee that: drawing ItemCount
    // values with replacement leaves some codes unused, so the setup seeds every code once and shuffles, and
    // the alphabet is exactly 1024 by construction at both sizes rather than by luck.
    private const int Alphabet = 1024;

    private int[] values = null!;
    private WaveletTree tree = null!;
    private int[] windowStarts = null!;
    private int[] ordinals = null!;
    private int[] bandLows = null!;
    private int[] rankPositions = null!;
    private int[] rankValues = null!;
    private int[] scratch = null!;
    private int windowLength;

    [Params(1024, 1_000_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        values = new int[ItemCount];
        for (int i = 0; i < Alphabet; i++)
            values[i] = i;
        for (int i = Alphabet; i < values.Length; i++)
            values[i] = rand.Next(Alphabet);

        // Fisher-Yates, so the seeded prefix does not leave the first 1024 positions sorted — which would
        // hand both the sort baseline and the descent an unrepresentative shortcut.
        for (int i = values.Length - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }

        tree = new WaveletTree(values);
        windowLength = Math.Max(1, ItemCount / 100);
        scratch = new int[windowLength];

        windowStarts = new int[QueryCount];
        ordinals = new int[QueryCount];
        bandLows = new int[QueryCount];
        rankPositions = new int[QueryCount];
        rankValues = new int[QueryCount];
        for (int i = 0; i < QueryCount; i++)
        {
            windowStarts[i] = rand.Next(ItemCount - windowLength + 1);
            ordinals[i] = rand.Next(windowLength);
            bandLows[i] = rand.Next(Alphabet - (Alphabet / 10));
            rankPositions[i] = rand.Next(ItemCount + 1);
            rankValues[i] = rand.Next(Alphabet);
        }
    }

    // ---- RangeCount: how many values in the window fall in a band a tenth of the alphabet wide ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RangeCount")]
    public int Array_RangeCount()
    {
        int sink = 0;
        for (int q = 0; q < QueryCount; q++)
        {
            int start = windowStarts[q];
            int lo = bandLows[q];
            int hi = lo + (Alphabet / 10);
            int end = start + windowLength;
            for (int i = start; i < end; i++)
            {
                int value = values[i];
                if (value >= lo && value <= hi)
                    sink++;
            }
        }

        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("RangeCount")]
    public int WaveletTree_RangeCount()
    {
        int sink = 0;
        for (int q = 0; q < QueryCount; q++)
        {
            int lo = bandLows[q];
            sink += tree.RangeCount(windowStarts[q], windowLength, lo, lo + (Alphabet / 10));
        }

        return sink;
    }

    // ---- Quantile: the k-th smallest in the window, which the baseline can only reach by sorting a copy ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Quantile")]
    public int Array_Quantile()
    {
        int sink = 0;
        for (int q = 0; q < QueryCount; q++)
        {
            // The scratch buffer is preallocated so the baseline is charged for the copy and the sort but not
            // for an allocation per query, which is the fairest form of the hand-roll.
            Array.Copy(values, windowStarts[q], scratch, 0, windowLength);
            Array.Sort(scratch);
            sink += scratch[ordinals[q]];
        }

        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("Quantile")]
    public int WaveletTree_Quantile()
    {
        int sink = 0;
        for (int q = 0; q < QueryCount; q++)
            sink += tree.Quantile(windowStarts[q], windowLength, ordinals[q]);

        return sink;
    }

    // ---- Rank: occurrences of one value below a position, against the counting loop ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Rank")]
    public int Array_Rank()
    {
        int sink = 0;
        for (int q = 0; q < QueryCount; q++)
        {
            int end = rankPositions[q];
            int value = rankValues[q];
            for (int i = 0; i < end; i++)
            {
                if (values[i] == value)
                    sink++;
            }
        }

        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("Rank")]
    public int WaveletTree_Rank()
    {
        int sink = 0;
        for (int q = 0; q < QueryCount; q++)
            sink += tree.Rank(rankPositions[q], rankValues[q]);

        return sink;
    }

    // ---- Build: the index construction the queries above amortize, against holding the raw values alone ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public int[] Array_Build() => (int[])values.Clone();

    [Benchmark]
    [BenchmarkCategory("Build")]
    public WaveletTree WaveletTree_Build() => new(values);
}
