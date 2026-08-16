using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// RTree<int> vs the linear scan it replaces. There is no BCL counterpart of any kind — .NET ships no R-tree,
// no bounding-volume hierarchy and no spatial index anywhere, and System.Drawing ships a Rectangle with an
// IntersectsWith on it and no index over a collection of them — so the honest baseline is what a caller writes
// instead: an array of boxes and a loop that tests all of them. The baseline arms are named Array_* so the
// dashboard classifies them as the reference series, the same convention KdTree and FenwickTree use.
//
// Two baselines, because there are two things a caller might write, and quoting only the weaker one would
// inflate every ratio here. The plain one tests every box. The better one — the *Sorted arms — keeps the boxes
// ordered by minX, binary-searches to the query's minX minus the widest stored box, and scans forward while
// minX stays at or below the query's maxX. That is a real and effective optimization: it is, in effect, a
// one-dimensional R-tree, which is exactly why measuring against it is the honest comparison — the second
// dimension and the extent hierarchy are the whole of what this type adds. It is also the baseline the issue
// named *before* implementation, at a deliberately modest 3x bar, because KdTree's analogous one-dimensional
// hand-roll came in at a surprising 2.5x and that argument should not be had after the fact.
//
// THE SHAPE IS DELIBERATE. The boxes here have extents drawn log-uniformly across three orders of magnitude —
// a few huge boxes among many small ones, which is what real map and scene data looks like and the shape an
// R-tree is supposed to earn its keep on, since no single grid cell size suits both ends. RTreeShapeBenchmark
// measures the uniform shape as well rather than leaving it to be discovered, and adds a bucketed grid as its
// own arm; the received wisdom that uniform extents belong to the grid did not survive that measurement, and
// the numbers there say so.
//
// The ratio these arms report is a function of selectivity, not just of size. Pruning works by discarding
// subtrees whose bounding box misses the query, so as a query's answer grows toward the whole tree its cost
// converges on the scan's. A query ten times wider would narrow the gap considerably.
//
// THE TWO FAMILIES DO NOT SIT AT THE SAME SELECTIVITY, and that is inherent rather than an oversight. QuerySide
// is tuned so the overlap query lands on the ~0.1% the issue's criterion names — measured at 83.5 matches per
// query, 0.0835%, at 100,000 boxes. A point query has no such knob: its answer size is fixed by the extents
// alone, and on this distribution it comes to 5.04 matches per query, 0.0050% — twenty times more selective.
// Bringing it to 0.1% would need a mean extent near 316 units in a 10,000-unit world, which is not "a few huge
// boxes among many small ones" but boxes that blanket the map, and it would drag the overlap arm far above
// 0.1% in the process. So the point ratio is measured on a more selective query than the overlap one and is
// flattered accordingly; the two columns are not like-for-like, and every surface quoting them says so.
// CheckSelectivity below fails the run rather than letting a later change to the data shape move either figure
// in silence — the boxes are seeded, so both counts are exact on any machine.
//
// What the two baselines actually measured, at 100,000 boxes. These are CI's same-runner figures rather than
// a development machine's, which matters: on KdTree the two disagreed by enough to change a documented claim,
// and the hosted runner is the number this repo quotes.
//
//     OverlapQuery  433.74 ms -> 3.08 ms  141x      OverlapSorted  29.77 ms -> 3.09 ms   9.6x   (0.0835%)
//     PointQuery    401.29 ms -> 1.67 ms  240x      PointSorted    17.78 ms -> 1.67 ms  10.6x   (0.0050%)
//     Build          436.05 us -> 62.60 ms  144x slower
//
// Both bars the issue set before implementation are cleared, and the one that mattered — 3x over the sorted
// hand-roll, set modestly because KdTree's analogous baseline came in at 2.5x — is cleared with room to spare.
//
// AT 1,000 BOXES THE POINT QUERY LOSES OUTRIGHT, which the development machine did not show and which is the
// reason this repo quotes CI. Against the sorted hand-roll the overlap query is 1.48x there, but the point
// query is 0.91x — the scan is ahead. The index has not paid for its indirection at that size, and a slab
// scan over a thousand boxes is a handful of cache lines. Stated rather than rounded away, in the README and
// the API reference as well as here. The build multiple is also far worse on CI (144x against the dev
// machine's 27x), because the hosted runner's array copy is quicker and its sorts are slower.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RTreeBenchmark
{
    private const int QueryCount = 1000;
    private const double Domain = 10_000;

    // Three orders of magnitude of extent, drawn log-uniformly so the small boxes dominate by count and the
    // large ones by area — the hierarchy an R-tree exists to index and a uniform grid cannot size a cell for.
    private const double MinExtent = 0.5;
    private const double MaxExtent = 500;

    // Chosen so a query selects roughly a thousandth of the boxes at 100,000: about a hundred matches, which
    // is a viewport-sized question rather than one whose answer is most of the tree.
    private const double QuerySide = 220;

    private SpatialBox<int>[] boxes = null!;
    private RTree<int> tree = null!;

    // The better hand-roll's own structure: the same boxes ordered by minX, split into parallel arrays so the
    // scan reads edges without touching the payload — the same courtesy the tree's own layout extends.
    private double[] sortedMinX = null!;
    private double[] sortedMinY = null!;
    private double[] sortedMaxX = null!;
    private double[] sortedMaxY = null!;
    private double widest;

    private double[] queryX = null!;
    private double[] queryY = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        boxes = new SpatialBox<int>[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            double width = LogUniform(rand);
            double height = LogUniform(rand);
            double minX = rand.NextDouble() * Domain;
            double minY = rand.NextDouble() * Domain;
            boxes[i] = new SpatialBox<int>(minX, minY, minX + width, minY + height, i);
        }

        tree = new RTree<int>(boxes);

        var byMinX = (SpatialBox<int>[])boxes.Clone();
        Array.Sort(byMinX, (a, b) => a.MinX.CompareTo(b.MinX));
        sortedMinX = new double[ItemCount];
        sortedMinY = new double[ItemCount];
        sortedMaxX = new double[ItemCount];
        sortedMaxY = new double[ItemCount];
        widest = 0;
        for (int i = 0; i < ItemCount; i++)
        {
            sortedMinX[i] = byMinX[i].MinX;
            sortedMinY[i] = byMinX[i].MinY;
            sortedMaxX[i] = byMinX[i].MaxX;
            sortedMaxY[i] = byMinX[i].MaxY;
            widest = Math.Max(widest, byMinX[i].MaxX - byMinX[i].MinX);
        }

        queryX = new double[QueryCount];
        queryY = new double[QueryCount];
        for (int i = 0; i < QueryCount; i++)
        {
            queryX[i] = rand.NextDouble() * Domain;
            queryY[i] = rand.NextDouble() * Domain;
        }

        // Every ratio this class reports is a claim about a query of a stated selectivity, so the selectivity
        // is checked rather than asserted in a comment. The bands are wide enough to survive a harmless tweak
        // and tight enough to catch a change that silently makes a query answer with much more of the tree —
        // which would move the ratios without moving anything a reader could see.
        CheckSelectivity(RTree_OverlapQuery(), 0.0005, 0.002, "overlap");
        CheckSelectivity(RTree_PointQuery(), 0.00002, 0.0002, "point");
    }

    private void CheckSelectivity(int matches, double low, double high, string family)
    {
        double fraction = (double)matches / QueryCount / ItemCount;
        if (fraction < low || fraction > high)
        {
            throw new InvalidOperationException(
                $"RTreeBenchmark: the {family} query selects {fraction:P4} of {ItemCount} boxes, outside [{low:P4}, {high:P4}].");
        }
    }

    // ---- OverlapQuery: the defining query, against the scan that tests every box ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("OverlapQuery")]
    public int Array_OverlapQuery()
    {
        int matches = 0;
        for (int i = 0; i < QueryCount; i++)
        {
            double minX = queryX[i];
            double minY = queryY[i];
            double maxX = minX + QuerySide;
            double maxY = minY + QuerySide;

            for (int j = 0; j < boxes.Length; j++)
            {
                SpatialBox<int> box = boxes[j];
                if (box.MinX <= maxX && box.MaxX >= minX && box.MinY <= maxY && box.MaxY >= minY)
                    matches++;
            }
        }

        return matches;
    }

    [Benchmark]
    [BenchmarkCategory("OverlapQuery")]
    public int RTree_OverlapQuery()
    {
        int matches = 0;
        for (int i = 0; i < QueryCount; i++)
            matches += tree.CountOverlapping(queryX[i], queryY[i], queryX[i] + QuerySide, queryY[i] + QuerySide);

        return matches;
    }

    // ---- OverlapSorted: the same query against the better hand-roll, a one-dimensional R-tree ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("OverlapSorted")]
    public int Array_OverlapSorted()
    {
        int matches = 0;
        for (int i = 0; i < QueryCount; i++)
            matches += SortedScan(queryX[i], queryY[i], queryX[i] + QuerySide, queryY[i] + QuerySide);

        return matches;
    }

    [Benchmark]
    [BenchmarkCategory("OverlapSorted")]
    public int RTree_OverlapSorted()
    {
        int matches = 0;
        for (int i = 0; i < QueryCount; i++)
            matches += tree.CountOverlapping(queryX[i], queryY[i], queryX[i] + QuerySide, queryY[i] + QuerySide);

        return matches;
    }

    // ---- PointQuery: which boxes cover this coordinate, against the scan that tests every box ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PointQuery")]
    public int Array_PointQuery()
    {
        int matches = 0;
        for (int i = 0; i < QueryCount; i++)
        {
            double x = queryX[i];
            double y = queryY[i];

            for (int j = 0; j < boxes.Length; j++)
            {
                SpatialBox<int> box = boxes[j];
                if (box.MinX <= x && box.MaxX >= x && box.MinY <= y && box.MaxY >= y)
                    matches++;
            }
        }

        return matches;
    }

    [Benchmark]
    [BenchmarkCategory("PointQuery")]
    public int RTree_PointQuery()
    {
        int matches = 0;
        for (int i = 0; i < QueryCount; i++)
            matches += tree.CountAtPoint(queryX[i], queryY[i]);

        return matches;
    }

    // ---- PointSorted: the same point query against the better hand-roll ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PointSorted")]
    public int Array_PointSorted()
    {
        int matches = 0;
        for (int i = 0; i < QueryCount; i++)
            matches += SortedScan(queryX[i], queryY[i], queryX[i], queryY[i]);

        return matches;
    }

    [Benchmark]
    [BenchmarkCategory("PointSorted")]
    public int RTree_PointSorted()
    {
        int matches = 0;
        for (int i = 0; i < QueryCount; i++)
            matches += tree.CountAtPoint(queryX[i], queryY[i]);

        return matches;
    }

    // ---- Build: the index the queries above amortize, against merely holding the boxes ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public SpatialBox<int>[] Array_Build() => (SpatialBox<int>[])boxes.Clone();

    [Benchmark]
    [BenchmarkCategory("Build")]
    public RTree<int> RTree_Build() => new(boxes);

    // The better hand-roll: seek to the first box whose minX could still reach the query — that is the query's
    // own minX less the widest stored box, since nothing narrower starting further left can extend into it —
    // then scan forward until a box starts past the query's right edge, at which point no later one can meet it
    // either. That single bound is everything sorting on one axis buys; what it cannot do is bound y or exploit
    // the extent hierarchy, so every box in the slab still has to be tested one at a time.
    private int SortedScan(double minX, double minY, double maxX, double maxY)
    {
        int matches = 0;
        for (int i = LowerBound(minX - widest); i < sortedMinX.Length && sortedMinX[i] <= maxX; i++)
        {
            if (sortedMaxX[i] >= minX && sortedMinY[i] <= maxY && sortedMaxY[i] >= minY)
                matches++;
        }

        return matches;
    }

    // First index whose minX is at or above the value, or the array length when none is.
    private int LowerBound(double value)
    {
        int lo = 0;
        int hi = sortedMinX.Length;

        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (sortedMinX[mid] < value)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }

    // Log-uniform rather than uniform, so each order of magnitude of extent gets the same share of the boxes.
    // A uniform draw over the same range would put almost every box near the top of it, which is the uniform
    // shape wearing a wide range's clothes.
    private static double LogUniform(Random rand)
        => MinExtent * Math.Pow(MaxExtent / MinExtent, rand.NextDouble());
}
