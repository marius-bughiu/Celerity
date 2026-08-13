using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// IntervalTree<int, int> vs the linear scan it replaces. There is no BCL counterpart of any kind — .NET ships
// no interval tree, no interval map, and no range-overlap query anywhere in System.Collections — so the honest
// baseline is what a caller writes instead: a List<T> of ranges and a loop that filters it. Sorting that list
// by start would not help, because an interval beginning far to the left can still cover the query point, so
// the scan cannot stop early; the baseline arms are therefore an unsorted scan, named List_* so the dashboard
// classifies them as the reference series.
//
// Three query categories cover the shapes the type is for. PointQuery is the stabbing query ("which ranges
// cover time t"), and it is the headline: the tree's cost is O(log n + k) against the baseline's O(n).
// WindowQuery is the overlap query over a half-open window, where a wider window raises k and narrows the gap
// — worth reporting next to the point query rather than instead of it. AnyOverlap is the conflict check
// ("is this slot already booked"), the one shape where both sides can exit early, so it is where the win is
// smallest and the comparison most honest. Build is the price of the index, which a caller only recovers by
// querying it repeatedly.
//
// The generated intervals are deliberately mixed: mostly short ranges with a minority of long spans, which is
// what real calendars, IP tables and trace-span sets look like — and the long spans are exactly what defeats
// the sorted-by-start shortcut a reader might otherwise expect the baseline to take.
//
// The ratio this class reports is a function of that shape, and the density is the term that matters: the tree
// does O(log n + k) work where k is the number of matches, so as the intervals pile deeper over each point the
// baseline's O(n) and the tree's O(k) converge. The mix here is a minority of spans covering ~1% of the domain
// each, which puts roughly 70 matches on a point at 100,000 intervals, and the point query measures 154x. A
// shape ten times denser (spans covering a quarter of the domain, ~1,250 matches per point) was measured too
// and the same query fell to 8.2x. The honest reading is that this type is for selective interval sets, which
// is what the docs say next to the number.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class IntervalTreeBenchmark
{
    private const int QueryCount = 1000;
    private const int Domain = 10_000_000;

    private Interval<int, int>[] intervals = null!;
    private List<Interval<int, int>> list = null!;
    private IntervalTree<int, int> tree = null!;
    private int[] points = null!;
    private int[] windowStart = null!;
    private Interval<int, int>[] buffer = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        intervals = new Interval<int, int>[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            int start = rand.Next(Domain);
            int length = rand.Next(0, 10) == 0
                ? rand.Next(Domain / 100)    // a long span, which no bound over the starts can constrain
                : rand.Next(1, 5_000);       // the common short range

            intervals[i] = new Interval<int, int>(start, start + length, i);
        }

        list = new List<Interval<int, int>>(intervals);
        tree = new IntervalTree<int, int>(intervals);

        points = new int[QueryCount];
        windowStart = new int[QueryCount];
        for (int i = 0; i < QueryCount; i++)
        {
            points[i] = rand.Next(Domain);
            windowStart[i] = rand.Next(Domain);
        }

        // Sized for the whole result set so neither side is measuring an allocation the other avoids.
        buffer = new Interval<int, int>[ItemCount];
    }

    // ---- PointQuery: which intervals cover this point — the stabbing query, O(log n + k) vs O(n) ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PointQuery")]
    public int List_PointQuery()
    {
        int matches = 0;
        for (int i = 0; i < points.Length; i++)
        {
            int point = points[i];
            for (int j = 0; j < list.Count; j++)
            {
                if (list[j].Start <= point && point < list[j].End)
                    matches++;
            }
        }

        return matches;
    }

    [Benchmark]
    [BenchmarkCategory("PointQuery")]
    public int IntervalTree_PointQuery()
    {
        int matches = 0;
        for (int i = 0; i < points.Length; i++)
            matches += tree.CountContaining(points[i]);

        return matches;
    }

    // ---- WindowQuery: which intervals overlap this window, materialized into a caller-owned buffer ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("WindowQuery")]
    public int List_WindowQuery()
    {
        int written = 0;
        for (int i = 0; i < windowStart.Length; i++)
        {
            int start = windowStart[i];
            int end = start + 50_000;
            written = 0;
            for (int j = 0; j < list.Count; j++)
            {
                if (list[j].Start < end && start < list[j].End)
                    buffer[written++] = list[j];
            }
        }

        return written;
    }

    [Benchmark]
    [BenchmarkCategory("WindowQuery")]
    public int IntervalTree_WindowQuery()
    {
        int written = 0;
        for (int i = 0; i < windowStart.Length; i++)
        {
            int start = windowStart[i];
            written = tree.CopyOverlapping(start, start + 50_000, buffer);
        }

        return written;
    }

    // ---- AnyOverlap: the conflict check, where both sides can stop at the first match ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("AnyOverlap")]
    public int List_AnyOverlap()
    {
        int hits = 0;
        for (int i = 0; i < windowStart.Length; i++)
        {
            int start = windowStart[i];
            int end = start + 1_000;
            for (int j = 0; j < list.Count; j++)
            {
                if (list[j].Start < end && start < list[j].End)
                {
                    hits++;
                    break;
                }
            }
        }

        return hits;
    }

    [Benchmark]
    [BenchmarkCategory("AnyOverlap")]
    public int IntervalTree_AnyOverlap()
    {
        int hits = 0;
        for (int i = 0; i < windowStart.Length; i++)
        {
            if (tree.Overlaps(windowStart[i], windowStart[i] + 1_000))
                hits++;
        }

        return hits;
    }

    // ---- Build: the index the queries above amortize, against merely holding the intervals ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public List<Interval<int, int>> List_Build() => new(intervals);

    [Benchmark]
    [BenchmarkCategory("Build")]
    public IntervalTree<int, int> IntervalTree_Build() => new(intervals);
}
