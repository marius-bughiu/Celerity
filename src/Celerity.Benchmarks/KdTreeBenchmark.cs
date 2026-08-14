using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// KdTree<int> vs the linear scan it replaces. There is no BCL counterpart of any kind — .NET ships no k-d
// tree, no quadtree, no R-tree and no spatial index anywhere — so the honest baseline is what a caller writes
// instead: an array of points and a loop that measures all of them. The baseline arms are named Array_* so the
// dashboard classifies them as the reference series, the same convention FenwickTree uses against a raw long[].
//
// Two baselines, because there are two things a caller might write, and quoting only the weaker one would
// inflate every ratio here. The plain one measures every point. The better one — the Sorted* arms — keeps the
// points ordered by x, binary-searches to the query's x and works outward, stopping in each direction once the
// horizontal gap alone exceeds the best distance found so far. That is a real and effective optimization: on
// uniformly scattered points it settles the nearest-neighbour query after touching on the order of sqrt(n)
// points rather than n, and it is close to what a competent developer actually writes. It is, in effect, a
// one-dimensional version of the tree, which is exactly why measuring against it is the honest comparison —
// the second dimension is the whole of what the tree adds.
//
// Six categories cover the shapes the type is for. NearestQuery is the defining query against the naive scan,
// and NearestSorted is the same query against that better hand-roll — the number to judge the type by.
// RadiusQuery and RadiusSlab are the same pairing for "everything within r", where the sorted baseline can
// restrict itself to the [x - r, x + r] slab. KNearest asks for the ten closest, and its baseline is already
// the smart one: a bounded max-heap scan, O(n log k), rather than sorting all n. Build is the price of the
// index, which a caller only recovers by querying it repeatedly.
//
// The rectangle query is not charted separately: it shares one traversal with the radius query and differs
// only in the membership predicate, so it measures the same pruning and would add a category that says the
// same thing.
//
// The ratio these arms report is a function of selectivity, not just of size. Pruning works by discarding
// subtrees that cannot hold a result, so as a query's answer grows toward the whole tree its cost converges on
// the scan's. The radius here covers roughly a thousandth of the domain, which puts about a hundred matches on
// a query at 100,000 points; a radius ten times wider would narrow the gap considerably, and the docs say so
// next to the number.
//
// What the two baselines actually measured, at 100,000 points (in-process on a dev machine, so the ratios are
// the point and not the absolute times):
//
//     NearestQuery   46.82 ms -> 168 us   278x        NearestSorted   317 us -> 168 us   1.9x
//     RadiusQuery    46.83 ms -> 864 us    54x        RadiusSlab     2.90 ms -> 857 us   3.4x
//     KNearest       81.07 ms -> 920 us    88x        Build           508 us -> 14.6 ms  29x slower
//
// At 1,000 points both sorted arms are a shade faster than the tree (77 us against 86 us, 53 us against 55 us):
// the second dimension does not pay for its indirection until tens of thousands of points. That is the sort of
// crossover this repo states rather than rounds away, so it is in the README and the API reference too.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class KdTreeBenchmark
{
    private const int QueryCount = 1000;
    private const double Domain = 10_000;
    private const double QueryRadius = 180;
    private const int NeighbourCount = 10;

    private SpatialPoint<int>[] points = null!;
    private KdTree<int> tree = null!;

    // The better hand-roll's own structure: the same points ordered by x, split into parallel arrays so the
    // scan reads coordinates without touching the payload — the same courtesy the tree's own layout extends.
    private double[] sortedX = null!;
    private double[] sortedY = null!;

    private double[] queryX = null!;
    private double[] queryY = null!;
    private SpatialPoint<int>[] buffer = null!;
    private SpatialPoint<int>[] neighbours = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        points = new SpatialPoint<int>[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            points[i] = new SpatialPoint<int>(rand.NextDouble() * Domain, rand.NextDouble() * Domain, i);

        tree = new KdTree<int>(points);

        var byX = (SpatialPoint<int>[])points.Clone();
        Array.Sort(byX, (a, b) => a.X.CompareTo(b.X));
        sortedX = new double[ItemCount];
        sortedY = new double[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            sortedX[i] = byX[i].X;
            sortedY[i] = byX[i].Y;
        }

        queryX = new double[QueryCount];
        queryY = new double[QueryCount];
        for (int i = 0; i < QueryCount; i++)
        {
            queryX[i] = rand.NextDouble() * Domain;
            queryY[i] = rand.NextDouble() * Domain;
        }

        // Sized for the whole point set so neither side is measuring an allocation the other avoids.
        buffer = new SpatialPoint<int>[ItemCount];
        neighbours = new SpatialPoint<int>[NeighbourCount];
    }

    // ---- NearestQuery: the defining query, against the scan that measures every point ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("NearestQuery")]
    public int Array_NearestQuery()
    {
        int found = 0;
        for (int i = 0; i < QueryCount; i++)
        {
            double qx = queryX[i];
            double qy = queryY[i];
            double best = double.PositiveInfinity;
            int bestIndex = -1;

            for (int j = 0; j < points.Length; j++)
            {
                double dx = points[j].X - qx;
                double dy = points[j].Y - qy;
                double distance = (dx * dx) + (dy * dy);
                if (distance < best)
                {
                    best = distance;
                    bestIndex = j;
                }
            }

            if (bestIndex >= 0)
                found++;
        }

        return found;
    }

    [Benchmark]
    [BenchmarkCategory("NearestQuery")]
    public int KdTree_NearestQuery()
    {
        int found = 0;
        for (int i = 0; i < QueryCount; i++)
        {
            if (tree.TryFindNearest(queryX[i], queryY[i], out _))
                found++;
        }

        return found;
    }

    // ---- NearestSorted: the same query against the better hand-roll, which works outward from the query's x ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("NearestSorted")]
    public int Array_NearestSorted()
    {
        int found = 0;
        for (int i = 0; i < QueryCount; i++)
        {
            if (NearestByXScan(queryX[i], queryY[i]) >= 0)
                found++;
        }

        return found;
    }

    [Benchmark]
    [BenchmarkCategory("NearestSorted")]
    public int KdTree_NearestSorted()
    {
        int found = 0;
        for (int i = 0; i < QueryCount; i++)
        {
            if (tree.TryFindNearest(queryX[i], queryY[i], out _))
                found++;
        }

        return found;
    }

    // ---- RadiusQuery: everything within r, against the scan that measures every point ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RadiusQuery")]
    public int Array_RadiusQuery()
    {
        double radiusSquared = QueryRadius * QueryRadius;
        int matches = 0;

        for (int i = 0; i < QueryCount; i++)
        {
            double qx = queryX[i];
            double qy = queryY[i];
            for (int j = 0; j < points.Length; j++)
            {
                double dx = points[j].X - qx;
                double dy = points[j].Y - qy;
                if ((dx * dx) + (dy * dy) <= radiusSquared)
                    matches++;
            }
        }

        return matches;
    }

    [Benchmark]
    [BenchmarkCategory("RadiusQuery")]
    public int KdTree_RadiusQuery()
    {
        int matches = 0;
        for (int i = 0; i < QueryCount; i++)
            matches += tree.CountWithin(queryX[i], queryY[i], QueryRadius);

        return matches;
    }

    // ---- RadiusSlab: the same query against the better hand-roll, which scans only the [x - r, x + r] slab ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RadiusSlab")]
    public int Array_RadiusSlab()
    {
        double radiusSquared = QueryRadius * QueryRadius;
        int matches = 0;

        for (int i = 0; i < QueryCount; i++)
        {
            double qx = queryX[i];
            double qy = queryY[i];

            // The one bound sorting by x does buy: everything outside the slab is out of range on x alone.
            // What it cannot do is bound y, so the slab still has to be measured point by point.
            for (int j = LowerBound(qx - QueryRadius); j < sortedX.Length && sortedX[j] <= qx + QueryRadius; j++)
            {
                double dx = sortedX[j] - qx;
                double dy = sortedY[j] - qy;
                if ((dx * dx) + (dy * dy) <= radiusSquared)
                    matches++;
            }
        }

        return matches;
    }

    [Benchmark]
    [BenchmarkCategory("RadiusSlab")]
    public int KdTree_RadiusSlab()
    {
        int matches = 0;
        for (int i = 0; i < QueryCount; i++)
            matches += tree.CountWithin(queryX[i], queryY[i], QueryRadius);

        return matches;
    }

    // ---- KNearest: the ten closest, against a bounded max-heap scan — already the smart hand-roll ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("KNearest")]
    public int Array_KNearest()
    {
        int written = 0;
        for (int i = 0; i < QueryCount; i++)
            written = KNearestByHeapScan(queryX[i], queryY[i]);

        return written;
    }

    [Benchmark]
    [BenchmarkCategory("KNearest")]
    public int KdTree_KNearest()
    {
        int written = 0;
        for (int i = 0; i < QueryCount; i++)
            written = tree.CopyNearest(queryX[i], queryY[i], neighbours);

        return written;
    }

    // ---- Build: the index the queries above amortize, against merely holding the points ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public SpatialPoint<int>[] Array_Build() => (SpatialPoint<int>[])points.Clone();

    [Benchmark]
    [BenchmarkCategory("Build")]
    public KdTree<int> KdTree_Build() => new(points);

    // The better hand-roll for the nearest query: seek to the query's x, then work outward in both directions,
    // abandoning a direction once the horizontal gap alone is worse than the best full distance so far. It pays
    // the same O(n log n) ordering the tree's own build pays, which is why the Build arms understate the tree's
    // cost against this alternative rather than against the plain array.
    private int NearestByXScan(double qx, double qy)
    {
        int right = LowerBound(qx);
        int left = right - 1;
        double best = double.PositiveInfinity;
        int bestIndex = -1;

        while (left >= 0 || right < sortedX.Length)
        {
            bool advanced = false;

            if (right < sortedX.Length)
            {
                double dx = sortedX[right] - qx;
                if (dx * dx < best)
                {
                    double dy = sortedY[right] - qy;
                    double distance = (dx * dx) + (dy * dy);
                    if (distance < best)
                    {
                        best = distance;
                        bestIndex = right;
                    }

                    right++;
                    advanced = true;
                }
                else
                {
                    // Ordered by x, so no point further right can be closer either.
                    right = sortedX.Length;
                }
            }

            if (left >= 0)
            {
                double dx = qx - sortedX[left];
                if (dx * dx < best)
                {
                    double dy = sortedY[left] - qy;
                    double distance = (dx * dx) + (dy * dy);
                    if (distance < best)
                    {
                        best = distance;
                        bestIndex = left;
                    }

                    left--;
                    advanced = true;
                }
                else
                {
                    left = -1;
                }
            }

            if (!advanced)
                break;
        }

        return bestIndex;
    }

    // The k-nearest baseline: one pass keeping a k-element max-heap, which is what a caller writes instead of
    // sorting all n. The heap lives in the same buffer the Celerity arm writes into, so neither side is
    // measuring an allocation the other avoids.
    private int KNearestByHeapScan(double qx, double qy)
    {
        int count = 0;

        for (int i = 0; i < points.Length; i++)
        {
            double dx = points[i].X - qx;
            double dy = points[i].Y - qy;
            double distance = (dx * dx) + (dy * dy);

            if (count < NeighbourCount)
            {
                buffer[count] = points[i];
                SiftUp(count, qx, qy);
                count++;
            }
            else if (distance < Distance(buffer[0], qx, qy))
            {
                buffer[0] = points[i];
                SiftDown(0, count, qx, qy);
            }
        }

        // The heap alone answers "which ten", not "which ten, nearest first" — and CopyNearest promises the
        // second. Leaving this out would have the baseline doing strictly less work than the arm it is compared
        // against, so the same in-place heapsort runs here. It is ten elements against a scan of n, so it
        // changes the ratio barely at all; that is not the point. The arms have to answer the same question.
        for (int end = count - 1; end > 0; end--)
        {
            (buffer[0], buffer[end]) = (buffer[end], buffer[0]);
            SiftDown(0, end, qx, qy);
        }

        return count;
    }

    private void SiftUp(int child, double qx, double qy)
    {
        while (child > 0)
        {
            int parent = (child - 1) >> 1;
            if (Distance(buffer[child], qx, qy) <= Distance(buffer[parent], qx, qy))
                return;

            (buffer[child], buffer[parent]) = (buffer[parent], buffer[child]);
            child = parent;
        }
    }

    private void SiftDown(int parent, int size, double qx, double qy)
    {
        while (true)
        {
            int left = (parent << 1) + 1;
            if (left >= size)
                return;

            int worst = left;
            int right = left + 1;
            if (right < size && Distance(buffer[right], qx, qy) > Distance(buffer[left], qx, qy))
                worst = right;

            if (Distance(buffer[worst], qx, qy) <= Distance(buffer[parent], qx, qy))
                return;

            (buffer[worst], buffer[parent]) = (buffer[parent], buffer[worst]);
            parent = worst;
        }
    }

    private static double Distance(SpatialPoint<int> point, double qx, double qy)
    {
        double dx = point.X - qx;
        double dy = point.Y - qy;
        return (dx * dx) + (dy * dy);
    }

    // First index whose x is at or above the value, or the array length when none is.
    private int LowerBound(double value)
    {
        int lo = 0;
        int hi = sortedX.Length;

        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (sortedX[mid] < value)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }
}
