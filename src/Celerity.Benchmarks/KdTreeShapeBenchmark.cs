using BenchmarkDotNet.Attributes;
using Celerity.Collections;

// A control for the one number in KdTreeBenchmark that most needs qualifying: how KdTree compares against the
// *better* hand-roll — points ordered by x, scanned outward from the query and abandoned in each direction once
// the horizontal gap alone exceeds the best distance so far.
//
// That baseline is, in effect, a one-dimensional spatial index: it finds the nearest neighbour by widening an
// x-slab around the query until the horizontal gap alone rules out anything closer. Since it is a real
// structure rather than a strawman, the gap between it and the tree is a small constant factor, and which way
// that factor moves with the data's shape is not obvious enough to assert — hence this class.
//
// Real spatial data is not uniform — cities, users, sensors, sprites and trace points cluster — so this class
// exists to change the distribution under a fixed comparison and see what the ratio does. It rides the extended
// suite, not the CI one: it answers "when does this type earn its keep", a question asked once while reading
// the docs, not on every pull request.
//
// THE HYPOTHESIS IT WAS WRITTEN TO CONFIRM WAS WRONG, and the measurement is left in place saying so. The
// expectation was that clustering would punish the one-dimensional scan — a dense cluster puts many points
// inside the query's x-slab, every one of which has to be measured because nothing bounds their y, while the
// tree alternates axes and prunes the same cluster on y as readily as on x. Measured (100,000 points, 1,000
// queries, in-process on a dev machine, so read the ratios and not the absolute times):
//
//     Uniform      SortedScan 274.7 us    KdTree 154.8 us    1.77x
//     Clustered    SortedScan 195.2 us    KdTree 174.7 us    1.12x
//
// Clustering *narrows* the gap rather than widening it, and both arms get faster. The reason is that the
// queries are drawn from the same distribution as the points, so inside a cluster the nearest neighbour is very
// close indeed — and a very small best distance is exactly what makes the scan's abandon-on-the-x-gap test fire
// after a handful of points. The scan's weakness is not density as such; it is a query whose nearest neighbour
// is *far*, which is what forces it to widen its slab. Uniform points at this density are the shape that
// produces the tree's best relative showing here, and the README quotes that shape rather than this one.
//
//   dotnet run -c Release -- --filter '*KdTreeShape*'
[MemoryDiagnoser]
public class KdTreeShapeBenchmark
{
    private const int ItemCount = 100_000;
    private const int QueryCount = 1000;
    private const double Domain = 10_000;

    // 200 clusters over the same domain, each a tight Gaussian blob. The blobs are far narrower than the mean
    // gap between them, which is what makes an x-slab through one of them dense.
    private const int ClusterCount = 200;
    private const double ClusterSpread = Domain / 400;

    private SpatialPoint<int>[] points = null!;
    private KdTree<int> tree = null!;
    private double[] sortedX = null!;
    private double[] sortedY = null!;
    private double[] queryX = null!;
    private double[] queryY = null!;

    /// <summary>The spatial distribution of the indexed points.</summary>
    public enum Shape
    {
        /// <summary>Points scattered uniformly. Measured as the tree's <i>best</i> relative showing, at 1.77x.</summary>
        Uniform,

        /// <summary>
        /// Points gathered into tight clusters, the shape real spatial data actually takes. Measured as the
        /// shape most favourable to the one-dimensional baseline, at 1.12x — the opposite of the expectation
        /// this class was written to confirm; see the note on the class.
        /// </summary>
        Clustered,
    }

    /// <summary>Gets or sets the distribution the indexed points are drawn from.</summary>
    [Params(Shape.Uniform, Shape.Clustered)]
    public Shape Distribution { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        points = new SpatialPoint<int>[ItemCount];
        if (Distribution == Shape.Uniform)
        {
            for (int i = 0; i < ItemCount; i++)
                points[i] = new SpatialPoint<int>(rand.NextDouble() * Domain, rand.NextDouble() * Domain, i);
        }
        else
        {
            var centreX = new double[ClusterCount];
            var centreY = new double[ClusterCount];
            for (int c = 0; c < ClusterCount; c++)
            {
                centreX[c] = rand.NextDouble() * Domain;
                centreY[c] = rand.NextDouble() * Domain;
            }

            for (int i = 0; i < ItemCount; i++)
            {
                int c = rand.Next(ClusterCount);
                points[i] = new SpatialPoint<int>(
                    centreX[c] + (Gaussian(rand) * ClusterSpread),
                    centreY[c] + (Gaussian(rand) * ClusterSpread),
                    i);
            }
        }

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

        // Queries are drawn from the same distribution as the points, so a clustered run is asking about a
        // crowded neighbourhood rather than about empty space between the blobs.
        queryX = new double[QueryCount];
        queryY = new double[QueryCount];
        for (int i = 0; i < QueryCount; i++)
        {
            int source = rand.Next(ItemCount);
            queryX[i] = points[source].X + (Gaussian(rand) * ClusterSpread);
            queryY[i] = points[source].Y + (Gaussian(rand) * ClusterSpread);
        }
    }

    [Benchmark(Baseline = true)]
    public int SortedScan_Nearest()
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
    public int KdTree_Nearest()
    {
        int found = 0;
        for (int i = 0; i < QueryCount; i++)
        {
            if (tree.TryFindNearest(queryX[i], queryY[i], out _))
                found++;
        }

        return found;
    }

    // Box-Muller, which is enough of a normal deviate for a benchmark's point cloud.
    private static double Gaussian(Random rand)
    {
        double u1 = 1.0 - rand.NextDouble();
        double u2 = rand.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    // The same hand-roll KdTreeBenchmark measures, kept in step with it deliberately: this class exists to
    // change the data under a fixed comparison, so the comparison itself must not move.
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
