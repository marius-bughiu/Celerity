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
// Measured at 100,000 entities, one frame = move 10% then one radius query per moved entity. These are CI's
// figures from a single run: run 43575145503 of benchmarks-extended.yml, the suite's first on .NET 10, a manual
// dispatch on 2026-11-14 (ubuntu-latest, x64, an AMD EPYC 9V74) at 611f2184, whose measured code matches main.
// Being off main, it did not publish to the dev/bench-extended dashboard that the weekly runs feed. Every run
// measures all three arms on one runner, so a row compares like with like. One run carries its runner's noise:
// on .NET 8, the twelve weekly runs that reached this class, six of which the timeout cut off (one partway
// through it), spread the same ratios up to 20% either side of their midpoints. The Tight row is the shape
// SpatialGridBenchmark charts:
//
//                                                 Dictionary    SpatialGrid   KdTree/frame      vs dict      vs tree
//     Tight      ~1 pt/cell,  ~2 matches             3.96 ms        1.69 ms       22.84 ms         2.3x        13.5x
//     Wide      ~10 pts/cell, ~25 matches           10.85 ms       12.58 ms       26.90 ms        0.86x         2.1x
//     Clustered  200 blobs, hundreds per cell       26.48 ms       44.47 ms       34.49 ms        0.60x        0.78x
//
// The Tight row reads well under SpatialGridBenchmark's 5.0x because that figure was measured on .NET 8. On
// matched runners (an EPYC 9V74, the same Ubuntu release, AVX-512 on), SpatialGridBenchmark measured this shape on
// both runtimes a day apart: .NET 10 runs the Dictionary arm 1.8x faster and the grid only 1.06x faster. The
// twelve .NET 8 runs above agree with this one on every comparison but Wide against the Dictionary, which sat
// near parity: 0.91-1.03x, the grid narrowly ahead in three of them.
//
// A development machine first read these at 6.5x, 1.12x and 0.52x against the Dictionary and put the clustered
// rebuild level with the grid (1.01x). Two of those flip in this run rather than just shrinking: the hand-roll is
// ahead at the wide shape, and the rebuild beats the grid on clustered points, as it did in every .NET 8 run that
// measured it. No CI run came near the development machine's 1.12x at the wide shape. The docs quote CI.
//
// The wide shape is not a pathological one — a 100-unit cell against a 90-unit radius, still sized just above
// it, which is the tuning rule the type documents. It is simply a query that answers with twenty-five points
// instead of two, and at that point both structures are memory-bound on the candidates' coordinates and the
// grid's per-cell saving no longer pays: the hand-roll finishes ahead here, and on .NET 8 the ratio ranged from
// 0.91x to 1.03x, which fits the dependent-load cost described below starting to show at ten entries a cell. The
// time advantage belongs to sparse cells, and the docs put that next to the headline.
//
// THE THIRD SHAPE DID NOT DO WHAT THIS CLASS WAS WRITTEN TO SHOW, and the measurement is left in place saying
// so. The expectation was that clustering would hurt both structures equally — the baseline is the same grid,
// after all, so a cell holding hundreds of points is a scan of hundreds either way. It does not: the grid takes
// 1.7x the hand-roll's time. The cause is the one place the layouts genuinely differ inside a cell. The
// baseline walks a contiguous List<int> and then issues two independent loads per candidate, which the
// processor overlaps; the grid walks an intrusive linked list, so each step is a load that has to complete
// before the address of the next one is known — a serial dependency chain of cache misses, and a cell holding
// five hundred entries is five hundred links long. That cost is invisible when cells hold one or two entries,
// which is why the tight shape shows none of it.
//
// So the type's documented failure mode is worse than "it degrades to a scan": on clustered data it degrades
// to a scan the hand-roll does faster, and the docs say that rather than rounding it to a caveat.
//
// THE KdTree ARM EXISTS BECAUSE THE DOCS WERE ASSERTING A WAY OUT THAT NOBODY HAD MEASURED. Every surface used
// to send clustered points to KdTree, reasoning that pruning adapts to density where a fixed cell size cannot —
// but that inference chained two unrelated comparisons (this class against a Dictionary, KdTreeShapeBenchmark
// against an x-sorted scan) and nobody had measured the one a caller actually faces. Measured, it is not a way
// out: on clustered moving points a per-frame rebuild takes 22% less time than the grid, but still 1.3x
// the hand-roll's. For heavily clustered points that move, the bucketed Dictionary of contiguous lists wins
// and neither Celerity type catches it. That is now what the docs say.
//
// It rides the extended suite rather than the core one: it answers "when does this type earn its keep", a
// question asked once while reading the docs, not on every merge.
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

    private SpatialPoint<int>[] kdPoints = null!;

    private double cellSize;
    private double queryRadius;
    private double[] clusterX = null!;
    private double[] clusterY = null!;

    /// <summary>How much of the population one query has to measure.</summary>
    public enum Density
    {
        /// <summary>
        /// About one point per cell and two matches per query — the broadphase shape, where the per-cell
        /// overhead the grid removes is most of the work. Measured at 2.3x the hand-roll and 13.5x a per-frame
        /// tree rebuild in one CI run on .NET 10; this is the shape SpatialGridBenchmark charts.
        /// </summary>
        Tight,

        /// <summary>
        /// About ten points per cell and twenty-five matches per query. Still a tuned grid — a 100-unit cell
        /// against a 90-unit radius; the candidates simply dominate. Measured in the same run at 0.86x the
        /// hand-roll — the per-cell saving is gone and the hand-roll edges ahead — and still 2.1x a per-frame
        /// tree rebuild.
        /// </summary>
        Wide,

        /// <summary>
        /// The type's own documented failure mode: the same points gathered into 200 tight blobs, so most of
        /// the population lands in a handful of cells. Measured in the same run at 0.60x — the grid
        /// <i>loses</i>, because a long cell list is a serial chain of dependent loads where the baseline's
        /// contiguous list is not — and a per-frame tree rebuild, though it takes 22% less time than the grid,
        /// is no way out: the hand-roll beats it too.
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

        kdPoints = new SpatialPoint<int>[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            kdPoints[i] = new SpatialPoint<int>(pointX[i], pointY[i], i);
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

    // The third arm, added because the docs were asserting something no benchmark had measured. Every surface
    // said "clustered points belong in KdTree", reasoning from KdTreeShapeBenchmark — but that class measures
    // the tree against its *own* hand-roll, not against this type. So the complementarity claim was inferred
    // from two unrelated comparisons. This arm makes the actual comparison a caller faces for moving points:
    // rebuild a tree every frame, or move a grid.
    [Benchmark]
    public int KdTree_Frame()
    {
        int start = NextCursor();

        for (int i = 0; i < MoveCount; i++)
        {
            int entity = (start + i) % ItemCount;
            int at = (start + i) & (PoolSize - 1);
            kdPoints[entity] = new SpatialPoint<int>(poolX[at], poolY[at], entity);
        }

        var tree = new KdTree<int>(kdPoints);

        int matches = 0;
        for (int i = 0; i < MoveCount; i++)
        {
            int at = (start + i) & (PoolSize - 1);
            matches += tree.CountWithin(poolX[at], poolY[at], queryRadius);
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
