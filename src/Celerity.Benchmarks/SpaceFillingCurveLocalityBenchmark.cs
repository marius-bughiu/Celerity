using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Primitives;

/// <summary>
/// The payoff benchmark for <see cref="MortonCurve"/> / <see cref="HilbertCurve"/> (issue #369): does
/// sorting a point set by its curve index actually earn the primitive its place, on a cache-bound sweep?
/// </summary>
/// <remarks>
/// <para>
/// The issue's second kill criterion is that a bit trick nobody builds on does not belong in the library,
/// so this measures the one thing a space-filling curve is <em>for</em> — laying spatially-near points
/// near each other in memory — rather than the conversion itself (that is
/// <see cref="SpaceFillingCurveBenchmark"/>).
/// </para>
/// <para>
/// <strong>Every arm runs the identical algorithm over the identical indirection.</strong> Points are
/// bucketed into a uniform cell grid; the workload is a batch of <em>randomly located</em> neighbourhood
/// queries, each summing the weights of the points in one cell and its eight neighbours — the broadphase
/// / hit-test shape. The four arms differ in exactly one thing: the order the point records are
/// <em>stored</em> in. Query sequence, load count, branch count and instruction mix are identical; only
/// the addresses move.
/// </para>
/// <para>
/// The queries are random, and that is load-bearing. An earlier version of this benchmark swept one
/// aligned block of cells and measured <em>no difference at all</em> between the four layouts: the block
/// touched a small enough slice of the point set that it stayed resident in L2 whatever order it was
/// stored in, so there were no misses left for a better layout to save. Randomly located queries over the
/// whole grid remove that reuse, and what is left to measure is the only thing a curve can help with —
/// how many distinct cache lines one query's neighbourhood is spread across.
/// </para>
/// <para>
/// <strong>The baseline is the smarter hand-roll, not a strawman.</strong> A caller who wants locality
/// and has no curve reaches for a row-major cell id, so that is its own arm. It gets horizontal locality
/// for free and loses on the vertical neighbours, which sit a whole grid row away — precisely the axis a
/// space-filling curve exists to bound. Unsorted insertion order is the baseline underneath it.
/// </para>
/// <para>
/// <strong>The shape where this does nothing is a parameter, not a footnote.</strong> At the small
/// <see cref="PointCount"/> the whole point set fits in cache and every arm should measure the same; the
/// win is a memory-hierarchy effect and only exists once the working set escapes it.
/// </para>
/// <para>
/// Allocates hundreds of megabytes in setup, so it rides the <strong>extended</strong> suite.
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SpaceFillingCurveLocalityBenchmark
{
    // 16 bytes per point, so the large parameter puts 32 MB behind each layout: far past any core's private
    // L2, and past the 30 MB last-level cache of the machine the quoted figures came from. Last-level cache
    // sizes vary a lot across the hardware an extended-suite run may land on, so the small parameter is the
    // control rather than a second data point — 1.6 MB of points plus its index arrays, which stayed
    // resident on the measured machine and showed no separation between the four layouts.
    private struct Point
    {
        public float X;
        public float Y;
        public float Weight;
        public int Tag;
    }

    /// <summary>Points in the set. The small value is the in-cache control where the ordering cannot pay.</summary>
    [Params(100_000, 2_000_000)]
    public int PointCount;

    // Queries per measured operation. Fixed, so the work does not scale with the point count — what
    // scales is how far apart the records one query touches are.
    private const int QueryCount = 20_000;

    private int cellSide;
    private Point[] unordered = null!;
    private Point[] rowMajorSorted = null!;
    private Point[] mortonSorted = null!;
    private Point[] hilbertSorted = null!;

    // cellStart[c]..cellStart[c+1] indexes into the layout's own point array for cell c. One per layout,
    // because the whole difference between the arms is which slots a cell's points occupy.
    private int[] cellStartUnordered = null!;
    private int[] cellStartRowMajor = null!;
    private int[] cellStartMorton = null!;
    private int[] cellStartHilbert = null!;
    private int[] pointsUnordered = null!;
    private int[] pointsRowMajor = null!;
    private int[] pointsMorton = null!;
    private int[] pointsHilbert = null!;

    // The query cells, drawn once and shared by every arm so they all answer exactly the same questions.
    private int[] queryCells = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Roughly four points per cell at every size, so the sweep's work per cell does not drift between
        // the two parameters and the only thing that changes is the spread of the records it touches.
        cellSide = FastUtils.NextPowerOfTwo(Math.Max(2, (int)Math.Sqrt(PointCount / 4.0)));

        var rng = new Random(20260815);
        var cells = new int[PointCount];
        unordered = new Point[PointCount];

        for (int i = 0; i < PointCount; i++)
        {
            uint cx = (uint)rng.Next(cellSide);
            uint cy = (uint)rng.Next(cellSide);
            unordered[i] = new Point
            {
                X = cx + (float)rng.NextDouble(),
                Y = cy + (float)rng.NextDouble(),
                Weight = (float)rng.NextDouble(),
                Tag = i,
            };
            cells[i] = (int)(cy * cellSide + cx);
        }

        // Four orderings of the same points, each keyed by a different cell ranking.
        (rowMajorSorted, cellStartRowMajor, pointsRowMajor) = Reorder(cells, c => (ulong)c);
        (mortonSorted, cellStartMorton, pointsMorton) = Reorder(cells, c => MortonCurve.Encode2D((uint)(c % cellSide), (uint)(c / cellSide)));
        (hilbertSorted, cellStartHilbert, pointsHilbert) = Reorder(cells, c => HilbertCurve.Encode2D((uint)(c % cellSide), (uint)(c / cellSide)));

        // The unsorted baseline keeps the points where they landed; only the bucket index is built.
        (cellStartUnordered, pointsUnordered) = Bucket(cells, Enumerable.Range(0, PointCount).ToArray());

        // Query cells avoid the grid border so the 3x3 neighbourhood needs no clamping in the hot loop.
        queryCells = new int[QueryCount];
        for (int q = 0; q < QueryCount; q++)
        {
            int cx = rng.Next(1, cellSide - 1);
            int cy = rng.Next(1, cellSide - 1);
            queryCells[q] = (cy * cellSide) + cx;
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("NeighbourhoodQuery")]
    public float Query_Unordered() => Query(unordered, cellStartUnordered, pointsUnordered);

    [Benchmark]
    [BenchmarkCategory("NeighbourhoodQuery")]
    public float Query_RowMajorSorted() => Query(rowMajorSorted, cellStartRowMajor, pointsRowMajor);

    [Benchmark]
    [BenchmarkCategory("NeighbourhoodQuery")]
    public float Query_MortonSorted() => Query(mortonSorted, cellStartMorton, pointsMorton);

    [Benchmark]
    [BenchmarkCategory("NeighbourhoodQuery")]
    public float Query_HilbertSorted() => Query(hilbertSorted, cellStartHilbert, pointsHilbert);

    // For each query cell, sum the weights of its own points and its eight neighbours'. Identical in
    // every arm — the layouts differ only in where those points live.
    private float Query(Point[] points, int[] cellStart, int[] cellPoints)
    {
        int side = cellSide;
        int[] queries = queryCells;
        float total = 0;

        for (int q = 0; q < queries.Length; q++)
        {
            int centre = queries[q];
            for (int row = -1; row <= 1; row++)
            {
                int first = centre + (row * side) - 1;
                int end = cellStart[first + 3];
                for (int i = cellStart[first]; i < end; i++)
                {
                    total += points[cellPoints[i]].Weight;
                }
            }
        }

        return total;
    }

    // Produce a layout in which the points are stored in ascending order of `rank(cell)`, plus the bucket
    // index into that layout.
    private (Point[] Points, int[] CellStart, int[] CellPoints) Reorder(int[] cells, Func<int, ulong> rank)
    {
        int[] order = Enumerable.Range(0, PointCount).ToArray();
        ulong[] keys = new ulong[PointCount];
        for (int i = 0; i < PointCount; i++)
        {
            keys[i] = rank(cells[i]);
        }

        Array.Sort(keys, order);

        var reordered = new Point[PointCount];
        var reorderedCells = new int[PointCount];
        for (int slot = 0; slot < PointCount; slot++)
        {
            reordered[slot] = unordered[order[slot]];
            reorderedCells[slot] = cells[order[slot]];
        }

        var (cellStart, cellPoints) = Bucket(reorderedCells, Enumerable.Range(0, PointCount).ToArray());
        return (reordered, cellStart, cellPoints);
    }

    // Counting-sort the slot indices into per-cell runs.
    private (int[] CellStart, int[] CellPoints) Bucket(int[] cellOfSlot, int[] slots)
    {
        int cellCount = cellSide * cellSide;
        var cellStart = new int[cellCount + 1];
        foreach (int cell in cellOfSlot)
        {
            cellStart[cell + 1]++;
        }

        for (int c = 0; c < cellCount; c++)
        {
            cellStart[c + 1] += cellStart[c];
        }

        var cursor = (int[])cellStart.Clone();
        var cellPoints = new int[slots.Length];
        foreach (int slot in slots)
        {
            cellPoints[cursor[cellOfSlot[slot]]++] = slot;
        }

        return (cellStart, cellPoints);
    }
}
