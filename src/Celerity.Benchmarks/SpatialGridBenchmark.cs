using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// SpatialGrid<int> against the two things a caller actually has for points that move. There is no BCL
// counterpart — .NET ships no spatial index of any kind — so both baselines are hand-rolls, and both were
// named in the issue before a line of this was written rather than argued about afterwards, which is the
// lesson KdTree (#367) taught this repo the hard way.
//
// Baseline one, and the one that decides whether this type is real: Dictionary<(int, int), List<int>>, the
// bucketed grid a competent developer writes. It is genuinely reasonable — the same cell idea, the same query
// shape — and what it costs is a tuple hash and a bucket probe per cell touched, a List<int> object per
// occupied cell, and a pointer chase into it. Note what it does *not* cost, because the measurement says so
// rather than the pitch: in steady state neither side allocates, since a cell that empties keeps its list.
// Its arms are named Dictionary_* so the dashboard classifies them as the reference series.
//
// Baseline two: rebuilding a KdTree every frame, which is what this library offered before this type existed.
// That arm is measured but deliberately NOT charted — the dashboard decides which series is the baseline from
// the type name in the arm's name, and `KdTree` is one of ours, so charting it would put two Celerity series
// in one bucket and silently drop one. The number belongs in the docs, not on a card.
//
// A "frame" is the unit throughout: move 10% of the population, then run one radius query per moved entity.
// That is the loop this type exists for, and measuring the move and the query separately would flatter both
// sides in different directions. Movement targets come from a large precomputed pool advanced by a wrapping
// cursor, so every invocation does statistically identical work without an [IterationSetup] reset — the
// population stays uniformly scattered no matter how many frames have run, which is what makes the cost
// stationary rather than drifting.
//
// Four categories:
//   Frame     — the defining workload, against the Dictionary-of-Lists hand-roll.
//   Query     — the same radius queries with no movement, which isolates how much of the gap is the query
//               rather than the churn.
//   Rebuild   — the same frame against rebuilding a KdTree, uncharted for the reason above.
//   Churn     — remove and re-add a tenth of the population per frame, the shape where the baseline pays a
//               List<T> lookup and an O(cell) List.Remove for every entry that leaves.
//
// The cell size equals the query radius, which is the tuning rule the type documents; both sides use the same
// one, so neither is measuring a different grid. That leaves about one point per cell at 100,000 entities and
// about two matches per query, which is the broadphase shape — a query that answers with a handful of
// neighbours rather than with a slice of the population.
//
// THAT PARAMETER IS THE WHOLE STORY AND IT IS NOT HIDDEN HERE. Both structures walk the same cells and run the
// same distance test on the same candidates; everything this type saves is *per cell* — an array index instead
// of a tuple hash and a bucket probe, an intrusive link instead of a separately allocated List<T> — so the
// margin is the ratio of per-cell overhead to per-candidate work and it collapses as the cells fill up. At ten
// points per cell it is 1.14x rather than 5.5x, and on clustered data the grid *loses*. SpatialGridShapeBenchmark
// in the extended suite carries both of those measurements, and the README and API reference quote them next
// to the headline rather than underneath it.
//
// Measured at 100,000 entities on a development machine — read the ratios, not the absolute times; CI's
// same-runner A/B is what the docs quote, and on KdTree the two disagreed enough to flip a documented claim:
//
//     Frame    6.11 ms -> 1.10 ms   5.5x        Query      4.72 ms -> 0.98 ms   4.8x
//     Churn     826 us ->  109 us   7.6x        Rebuild   17.72 ms -> 1.08 ms  16.4x
//
// At 1,000 entities the margins are wider, not narrower (10.2x on the frame), because the cells are emptier
// still and the per-cell overhead is then nearly all of the baseline's work.
//
// The Rebuild number is worth reading against the issue that asked for this type, which predicted ">= 50x" over
// rebuilding a KdTree per frame and called it the easy bar. It is 16.4x, and the reason the prediction missed
// is that a frame is not only the rebuild: both arms also run 10,000 radius queries, and that shared work sits
// in the denominator of the ratio however cheap the index makes it. Comparing the builds alone would give a
// larger number and a less useful one, since nobody rebuilds without then querying.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SpatialGridBenchmark
{
    private const double Domain = 10_000;
    private const double CellSize = 30;
    private const double QueryRadius = 25;
    private const int PoolSize = 65_536;

    private double[] pointX = null!;
    private double[] pointY = null!;

    // The movement pool: fresh uniformly scattered coordinates, walked with a wrapping cursor so no frame
    // repeats the previous one's targets and no frame is cheaper than another.
    private double[] poolX = null!;
    private double[] poolY = null!;
    private int cursor;

    private SpatialGrid<int> grid = null!;
    private SpatialGridHandle[] handles = null!;

    private Dictionary<(int, int), List<int>> buckets = null!;
    private double[] bucketX = null!;
    private double[] bucketY = null!;

    private SpatialPoint<int>[] kdPoints = null!;

    private int moveCount;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        pointX = new double[ItemCount];
        pointY = new double[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            pointX[i] = rand.NextDouble() * Domain;
            pointY[i] = rand.NextDouble() * Domain;
        }

        poolX = new double[PoolSize];
        poolY = new double[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            poolX[i] = rand.NextDouble() * Domain;
            poolY[i] = rand.NextDouble() * Domain;
        }

        moveCount = Math.Max(1, ItemCount / 10);

        grid = new SpatialGrid<int>(0, 0, Domain, Domain, CellSize, ItemCount);
        handles = new SpatialGridHandle[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            handles[i] = grid.Add(pointX[i], pointY[i], i);

        bucketX = (double[])pointX.Clone();
        bucketY = (double[])pointY.Clone();
        buckets = new Dictionary<(int, int), List<int>>(ItemCount);
        for (int i = 0; i < ItemCount; i++)
            BucketFor(bucketX[i], bucketY[i]).Add(i);

        kdPoints = new SpatialPoint<int>[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            kdPoints[i] = new SpatialPoint<int>(pointX[i], pointY[i], i);
    }

    // ---- Frame: move a tenth of the population, then query around each moved entity ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Frame")]
    public int Dictionary_Frame()
    {
        int start = NextCursor();
        int matches = 0;

        for (int i = 0; i < moveCount; i++)
        {
            int entity = (start + i) % ItemCount;
            double x = poolX[(start + i) & (PoolSize - 1)];
            double y = poolY[(start + i) & (PoolSize - 1)];
            BucketMove(entity, x, y);
            matches += BucketCountWithin(x, y, QueryRadius);
        }

        return matches;
    }

    [Benchmark]
    [BenchmarkCategory("Frame")]
    public int SpatialGrid_Frame()
    {
        int start = NextCursor();
        int matches = 0;

        for (int i = 0; i < moveCount; i++)
        {
            int entity = (start + i) % ItemCount;
            double x = poolX[(start + i) & (PoolSize - 1)];
            double y = poolY[(start + i) & (PoolSize - 1)];
            grid.Move(handles[entity], x, y);
            matches += grid.CountWithin(x, y, QueryRadius);
        }

        return matches;
    }

    // ---- Query: the same radius queries with nothing moving, which is the baseline at its strongest ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Query")]
    public int Dictionary_Query()
    {
        int start = NextCursor();
        int matches = 0;

        for (int i = 0; i < moveCount; i++)
        {
            int at = (start + i) & (PoolSize - 1);
            matches += BucketCountWithin(poolX[at], poolY[at], QueryRadius);
        }

        return matches;
    }

    [Benchmark]
    [BenchmarkCategory("Query")]
    public int SpatialGrid_Query()
    {
        int start = NextCursor();
        int matches = 0;

        for (int i = 0; i < moveCount; i++)
        {
            int at = (start + i) & (PoolSize - 1);
            matches += grid.CountWithin(poolX[at], poolY[at], QueryRadius);
        }

        return matches;
    }

    // ---- Rebuild: the same frame against rebuilding a KdTree, which is what the library offered before ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Rebuild")]
    public int KdTree_Rebuild()
    {
        int start = NextCursor();

        for (int i = 0; i < moveCount; i++)
        {
            int entity = (start + i) % ItemCount;
            int at = (start + i) & (PoolSize - 1);
            kdPoints[entity] = new SpatialPoint<int>(poolX[at], poolY[at], entity);
        }

        var tree = new KdTree<int>(kdPoints);

        int matches = 0;
        for (int i = 0; i < moveCount; i++)
        {
            int at = (start + i) & (PoolSize - 1);
            matches += tree.CountWithin(poolX[at], poolY[at], QueryRadius);
        }

        return matches;
    }

    [Benchmark]
    [BenchmarkCategory("Rebuild")]
    public int SpatialGrid_Rebuild()
    {
        int start = NextCursor();
        int matches = 0;

        for (int i = 0; i < moveCount; i++)
        {
            int entity = (start + i) % ItemCount;
            int at = (start + i) & (PoolSize - 1);
            grid.Move(handles[entity], poolX[at], poolY[at]);
            matches += grid.CountWithin(poolX[at], poolY[at], QueryRadius);
        }

        return matches;
    }

    // ---- Churn: a tenth of the population leaves and an equal number arrives ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Churn")]
    public int Dictionary_Churn()
    {
        int start = NextCursor();

        for (int i = 0; i < moveCount; i++)
        {
            int entity = (start + i) % ItemCount;
            BucketRemove(entity);

            int at = (start + i) & (PoolSize - 1);
            bucketX[entity] = poolX[at];
            bucketY[entity] = poolY[at];
            BucketFor(bucketX[entity], bucketY[entity]).Add(entity);
        }

        return buckets.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Churn")]
    public int SpatialGrid_Churn()
    {
        int start = NextCursor();

        for (int i = 0; i < moveCount; i++)
        {
            int entity = (start + i) % ItemCount;
            grid.Remove(handles[entity]);

            int at = (start + i) & (PoolSize - 1);
            handles[entity] = grid.Add(poolX[at], poolY[at], entity);
        }

        return grid.Count;
    }

    // ---- the hand-rolled bucketed grid ----

    private int NextCursor()
    {
        cursor = (cursor + moveCount) & (PoolSize - 1);
        return cursor;
    }

    private static (int, int) Key(double x, double y) => ((int)(x / CellSize), (int)(y / CellSize));

    private List<int> BucketFor(double x, double y)
    {
        (int, int) key = Key(x, y);
        if (!buckets.TryGetValue(key, out List<int>? bucket))
        {
            // The allocation this type exists to avoid: one list per occupied cell, created again every time
            // a cell that emptied out refills.
            bucket = new List<int>();
            buckets[key] = bucket;
        }

        return bucket;
    }

    private void BucketRemove(int entity)
    {
        (int, int) key = Key(bucketX[entity], bucketY[entity]);
        if (buckets.TryGetValue(key, out List<int>? bucket))
            bucket.Remove(entity);
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

    private int BucketCountWithin(double x, double y, double radius)
    {
        double radiusSquared = radius * radius;
        int minColumn = (int)((x - radius) / CellSize);
        int maxColumn = (int)((x + radius) / CellSize);
        int minRow = (int)((y - radius) / CellSize);
        int maxRow = (int)((y + radius) / CellSize);

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
