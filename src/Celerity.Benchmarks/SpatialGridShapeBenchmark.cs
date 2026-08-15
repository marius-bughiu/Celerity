using BenchmarkDotNet.Attributes;
using Celerity.Collections;

// A control for the one number in SpatialGridBenchmark that most needs qualifying: how far ahead of the
// Dictionary-of-Lists hand-roll SpatialGrid actually is. That ratio is not a property of the type — it is a
// property of **how many points a query has to measure**, and this class exists to say so with a measurement
// rather than to let the core benchmark's single shape read as a general claim.
//
// The mechanism is simple once stated. Both structures walk the same cells and run the same distance test on
// the same candidates; that work is identical. Everything the grid saves is *per cell* — an array index
// instead of a tuple hash and a bucket probe, an intrusive link instead of a separately allocated List<T>. So
// the margin is the ratio of per-cell overhead to per-candidate work, and it collapses as the cells fill up.
//
// Measured at 100,000 entities, one frame = move 10% then one radius query per moved entity, in-process on a
// development machine — read the ratios, not the absolute times, and treat even the ratios as provisional
// until CI's same-runner A/B replaces them:
//
//     Tight       ~1 point per cell, ~2 matches       Dictionary  6.58 ms   SpatialGrid  1.12 ms   5.9x
//     Wide        ~10 points per cell, ~25 matches    Dictionary  6.65 ms   SpatialGrid  5.92 ms   1.12x
//     Clustered   200 blobs, hundreds per cell        Dictionary 12.37 ms   SpatialGrid 21.38 ms   0.58x
//
// The wide shape is not a pathological one — cell size still equals the query radius, which is the tuning rule
// the type documents. It is simply a query that answers with twenty-five points instead of two, and at that
// point both structures are memory-bound on the candidates' coordinates and neither layout can pull ahead.
// That is the honest ceiling on this type's time advantage.
//
// THE THIRD SHAPE DID NOT DO WHAT THIS CLASS WAS WRITTEN TO SHOW, and the measurement is left in place saying
// so. The expectation was that clustering would hurt both structures equally — the baseline is the same grid,
// after all, so a cell holding hundreds of points is a scan of hundreds either way. It does not: the grid ends
// up 1.75x *slower*. The cause is the one place the layouts genuinely differ inside a cell. The baseline walks
// a contiguous List<int> and then issues two independent loads per candidate, which the processor overlaps;
// the grid walks an intrusive linked list, so each step is a load that has to complete before the address of
// the next one is known — a serial dependency chain of cache misses, and a cell holding five hundred entries
// is five hundred links long. That cost is invisible when cells hold one or two entries, which is why the
// tuned shapes above show none of it.
//
// So the type's documented failure mode is worse than "it degrades to a scan": on clustered data it degrades
// to a scan the hand-roll does faster, and the docs say that rather than rounding it to a caveat. Clustered
// points belong in KdTree — and note that this is the exact mirror of KdTreeShapeBenchmark, where clustering
// is what makes the *tree* lose to its own hand-roll. The two types prefer opposite distributions, which is
// the measurement behind the claim that they are complements rather than rivals.
//
// It rides the extended suite rather than the CI one: it answers "when does this type earn its keep", a
// question asked once while reading the docs, not on every pull request.
//
//   dotnet run -c Release -- --filter '*SpatialGridShape*'
[MemoryDiagnoser]
public class SpatialGridShapeBenchmark
{
    private const int ItemCount = 100_000;
    private const double Domain = 10_000;
    private const int PoolSize = 65_536;
    private const int MoveCount = ItemCount / 10;

    // 200 tight Gaussian blobs over the same domain, the same clustered shape KdTreeShapeBenchmark uses, so
    // the two controls are looking at one distribution from opposite sides.
    private const int ClusterCount = 200;
    private const double ClusterSpread = Domain / 400;

    private double[] poolX = null!;
    private double[] poolY = null!;
    private int cursor;

    private SpatialGrid<int> grid = null!;
    private SpatialGridHandle[] handles = null!;

    private Dictionary<(int, int), List<int>> buckets = null!;
    private double[] bucketX = null!;
    private double[] bucketY = null!;

    private double cellSize;
    private double queryRadius;
    private double[] clusterX = null!;
    private double[] clusterY = null!;

    /// <summary>How much of the population one query has to measure.</summary>
    public enum Density
    {
        /// <summary>
        /// About one point per cell and two matches per query — the broadphase shape, where the per-cell
        /// overhead the grid removes is most of the work. Measured at 5.9x, and the shape the CI benchmark
        /// uses.
        /// </summary>
        Tight,

        /// <summary>
        /// About ten points per cell and twenty-five matches per query. Still a tuned grid (cell size equals
        /// the query radius); the candidates simply dominate. Measured at 1.12x — the honest ceiling.
        /// </summary>
        Wide,

        /// <summary>
        /// The type's own documented failure mode: the same points gathered into 200 tight blobs, so most of
        /// the population lands in a handful of cells. Measured at 0.58x — the grid <i>loses</i>, because a
        /// long cell list is a serial chain of dependent loads where the baseline's contiguous list is not.
        /// </summary>
        Clustered,
    }

    [Params(Density.Tight, Density.Wide, Density.Clustered)]
    public Density Shape;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        (cellSize, queryRadius) = Shape == Density.Wide ? (100.0, 90.0) : (30.0, 25.0);

        // One centre per cluster, drawn once — not one per axis, which would cross into ClusterCount^2
        // centres and produce a distribution barely distinguishable from uniform.
        clusterX = new double[ClusterCount];
        clusterY = new double[ClusterCount];
        for (int i = 0; i < ClusterCount; i++)
        {
            clusterX[i] = rand.NextDouble() * Domain;
            clusterY[i] = rand.NextDouble() * Domain;
        }

        var pointX = new double[ItemCount];
        var pointY = new double[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            (pointX[i], pointY[i]) = NextPoint(rand);

        poolX = new double[PoolSize];
        poolY = new double[PoolSize];
        for (int i = 0; i < PoolSize; i++)
            (poolX[i], poolY[i]) = NextPoint(rand);

        grid = new SpatialGrid<int>(0, 0, Domain, Domain, cellSize, ItemCount);
        handles = new SpatialGridHandle[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            handles[i] = grid.Add(pointX[i], pointY[i], i);

        bucketX = pointX;
        bucketY = pointY;
        buckets = new Dictionary<(int, int), List<int>>(ItemCount);
        for (int i = 0; i < ItemCount; i++)
            BucketFor(bucketX[i], bucketY[i]).Add(i);
    }

    [Benchmark(Baseline = true)]
    public int Dictionary_Frame()
    {
        int start = NextCursor();

        for (int i = 0; i < MoveCount; i++)
        {
            int at = (start + i) & (PoolSize - 1);
            BucketMove((start + i) % ItemCount, poolX[at], poolY[at]);
        }

        int matches = 0;
        for (int i = 0; i < MoveCount; i++)
        {
            int at = (start + i) & (PoolSize - 1);
            matches += BucketCountWithin(poolX[at], poolY[at]);
        }

        return matches;
    }

    [Benchmark]
    public int SpatialGrid_Frame()
    {
        int start = NextCursor();

        for (int i = 0; i < MoveCount; i++)
        {
            int at = (start + i) & (PoolSize - 1);
            grid.Move(handles[(start + i) % ItemCount], poolX[at], poolY[at]);
        }

        int matches = 0;
        for (int i = 0; i < MoveCount; i++)
        {
            int at = (start + i) & (PoolSize - 1);
            matches += grid.CountWithin(poolX[at], poolY[at], queryRadius);
        }

        return matches;
    }

    // Queries are drawn from the same generator as the points rather than derived from a stored one — the
    // sampling mistake KdTreeShapeBenchmark's own header records, which biased that comparison's decisive
    // quantity by widening the query cloud relative to the data.
    private (double X, double Y) NextPoint(Random rand)
    {
        if (Shape != Density.Clustered)
            return (rand.NextDouble() * Domain, rand.NextDouble() * Domain);

        int cluster = rand.Next(ClusterCount);
        return (Clamp(clusterX[cluster] + Gaussian(rand)), Clamp(clusterY[cluster] + Gaussian(rand)));
    }

    private static double Gaussian(Random rand)
    {
        double u1 = 1.0 - rand.NextDouble();
        double u2 = rand.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2) * ClusterSpread;
    }

    private static double Clamp(double value) => Math.Clamp(value, 0, Domain);

    private int NextCursor()
    {
        cursor = (cursor + MoveCount) & (PoolSize - 1);
        return cursor;
    }

    private (int, int) Key(double x, double y) => ((int)(x / cellSize), (int)(y / cellSize));

    private List<int> BucketFor(double x, double y)
    {
        (int, int) key = Key(x, y);
        if (!buckets.TryGetValue(key, out List<int>? bucket))
        {
            bucket = new List<int>();
            buckets[key] = bucket;
        }

        return bucket;
    }

    private void BucketMove(int entity, double x, double y)
    {
        (int, int) from = Key(bucketX[entity], bucketY[entity]);
        (int, int) to = Key(x, y);

        bucketX[entity] = x;
        bucketY[entity] = y;

        if (from == to)
            return;

        if (buckets.TryGetValue(from, out List<int>? bucket))
            bucket.Remove(entity);

        BucketFor(x, y).Add(entity);
    }

    private int BucketCountWithin(double x, double y)
    {
        double radiusSquared = queryRadius * queryRadius;
        int minColumn = (int)((x - queryRadius) / cellSize);
        int maxColumn = (int)((x + queryRadius) / cellSize);
        int minRow = (int)((y - queryRadius) / cellSize);
        int maxRow = (int)((y + queryRadius) / cellSize);

        int matches = 0;
        for (int column = minColumn; column <= maxColumn; column++)
        {
            for (int row = minRow; row <= maxRow; row++)
            {
                if (!buckets.TryGetValue((column, row), out List<int>? bucket))
                    continue;

                foreach (int entity in bucket)
                {
                    double dx = bucketX[entity] - x;
                    double dy = bucketY[entity] - y;
                    if ((dx * dx) + (dy * dy) <= radiusSquared)
                        matches++;
                }
            }
        }

        return matches;
    }
}
