using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// Two controls for RTreeBenchmark, on the two questions its headline numbers most need qualifying on.
//
// FIRST, THE SHAPE. RTreeBenchmark deliberately measures extents spanning three orders of magnitude, because
// that is where an R-tree is supposed to earn its keep. The obligation that comes with choosing the flattering
// shape is to measure the other one too and say where the type loses — so this class runs the same comparison
// against uniform extents, and adds the structure the type is supposed to lose to there: a bucketed uniform
// grid. That third arm is not decoration. "For uniform boxes reach for a grid instead" is a recommendation
// about which type to use, and this repository's rule is that such a claim needs its own arm rather than an
// inference chained from two other comparisons.
//
// THE RECEIVED WISDOM THIS CLASS WAS WRITTEN TO CONFIRM DID NOT SURVIVE THE MEASUREMENT, and the arms are left
// in place saying so. The expectation was that uniform extents would be where the R-tree gives way — a cell
// size exists that fits them all, so the grid should win, and the one-dimensional hand-roll should close much
// of its gap too because its slab then holds far fewer boxes that cannot match. What the numbers say is that
// the R-tree's own advantage *grows* on the uniform shape rather than shrinking, in both comparisons.
//
// The reason is that the grid's query cost is dominated by the cells a query covers, not by the boxes in them.
// Cells sized to the data (twice the mean extent, the standard heuristic) are much smaller than the query box
// here, so a uniform-shape query walks about fifty cells and pays a stamp-array write per candidate to undo
// the replication — while the R-tree, whose node boxes get *tighter* as the extents get more alike, settles
// the same query in a short descent. The honest qualification is that a grid's cell size is a tuning knob and
// this one is sized by the data rather than by the query; a grid tuned to a known query size would close some
// of that gap. What is not supported is the flat claim that uniform extents belong to the grid — at least not
// on query-cost grounds. The reason to reach for a grid remains that it is mutable and this type is not.
//
// SECOND, THE PACKING. STR is a sort into tiles per level; ordering the boxes along a space-filling curve and
// cutting the result into runs is the standard alternative, and the issue that asked for this type asked for
// the two to be measured rather than for either to be assumed better. Since Celerity.Primitives now ships
// HilbertCurve, that measurement is available rather than hypothetical — and it comes out for the shipped
// choice: sort-tile is 1.05x ahead of the Hilbert order on varying extents and 1.23x ahead on uniform ones.
//
// The packing arms run on a COMMON HARNESS — one PackedTree below, built and queried by identical code, with
// only the initial permutation differing. That is the whole reason it exists: comparing the shipped RTree
// against a separately written Hilbert index would measure two implementations rather than two orderings.
// PackedTree is not the shipped type and its absolute times should not be read as this library's; the shipped
// numbers are RTreeBenchmark's. What is readable here is the ratio between its own two arms.
//
// Measured (100,000 boxes, 1,000 queries, in-process on a dev machine, so read the ratios and not the absolute
// times):
//
//     Varying    SortedScan 16.78 ms    RTree 1.69 ms    9.9x     Grid 2.19 ms    tree 1.29x ahead of the grid
//     Uniform    SortedScan  7.31 ms    RTree 0.62 ms   11.9x     Grid 1.25 ms    tree 2.03x ahead of the grid
//
//     Varying    Packed_SortTile 1.68 ms    Packed_Hilbert 1.76 ms    sort-tile 1.05x
//     Uniform    Packed_SortTile 0.62 ms    Packed_Hilbert 0.77 ms    sort-tile 1.23x
//
//   dotnet run -c Release -- --filter '*RTreeShape*'
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RTreeShapeBenchmark
{
    private const int ItemCount = 100_000;
    private const int QueryCount = 1000;
    private const double Domain = 10_000;
    private const double QuerySide = 220;

    // The varying shape: three orders of magnitude, log-uniform, so small boxes dominate by count and large
    // ones by area. The uniform shape holds every box at the geometric mean of that range, so the two shapes
    // cover comparable total area and the comparison is about the spread rather than about the size.
    private const double MinExtent = 0.5;
    private const double MaxExtent = 500;
    private const double UniformExtent = 15.8;

    private SpatialBox<int>[] boxes = null!;
    private RTree<int> tree = null!;
    private BucketGrid grid = null!;
    private PackedTree strPacked = null!;
    private PackedTree hilbertPacked = null!;

    private double[] sortedMinX = null!;
    private double[] sortedMinY = null!;
    private double[] sortedMaxX = null!;
    private double[] sortedMaxY = null!;
    private double widest;

    private double[] queryX = null!;
    private double[] queryY = null!;

    /// <summary>The distribution of the indexed boxes' extents.</summary>
    public enum Shape
    {
        /// <summary>
        /// Extents spanning three orders of magnitude — a few huge boxes among many small ones, the shape real
        /// map and scene data takes and the one this type is supposed to exist for. This is what
        /// RTreeBenchmark measures. Measured at 9.9x the sorted hand-roll and 1.29x the bucketed grid.
        /// </summary>
        Varying,

        /// <summary>
        /// Every box the same size, at the geometric mean of the varying range so the two shapes cover
        /// comparable total area. Included as the shape where this type was expected to give way, and measured
        /// as the shape where its margin is <i>widest</i> instead — 11.9x the sorted hand-roll and 2.03x the
        /// bucketed grid. See the note on the class.
        /// </summary>
        Uniform,
    }

    [Params(Shape.Varying, Shape.Uniform)]
    public Shape Extents;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        boxes = new SpatialBox<int>[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            double width = Extent(rand);
            double height = Extent(rand);
            double minX = rand.NextDouble() * Domain;
            double minY = rand.NextDouble() * Domain;
            boxes[i] = new SpatialBox<int>(minX, minY, minX + width, minY + height, i);
        }

        tree = new RTree<int>(boxes);
        grid = new BucketGrid(boxes, Domain + MaxExtent);
        strPacked = PackedTree.SortTiled(boxes);
        hilbertPacked = PackedTree.HilbertOrdered(boxes, Domain + MaxExtent);

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

        // Four structures are being compared here, two of them written for this file. A ratio between arms that
        // answer different questions is worse than no measurement, so they are reconciled against each other
        // once at setup rather than trusted — the grid's stamp-based deduplication in particular is exactly the
        // kind of thing that silently under-counts.
        Check(SortedScan(), RTree(), "sorted scan");
        Check(Grid(), RTree(), "bucket grid");
        Check(Packed_SortTile(), RTree(), "sort-tile packing");
        Check(Packed_Hilbert(), RTree(), "Hilbert packing");
    }

    private static void Check(int actual, int expected, string arm)
    {
        if (actual != expected)
            throw new InvalidOperationException($"RTreeShapeBenchmark: the {arm} arm counted {actual} against {expected}.");
    }

    // ---- Shape: the shipped type against the better hand-roll, under both extent distributions ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Shape")]
    public int SortedScan()
    {
        int matches = 0;
        for (int i = 0; i < QueryCount; i++)
        {
            double minX = queryX[i];
            double minY = queryY[i];
            double maxX = minX + QuerySide;
            double maxY = minY + QuerySide;

            for (int j = LowerBound(minX - widest); j < sortedMinX.Length && sortedMinX[j] <= maxX; j++)
            {
                if (sortedMaxX[j] >= minX && sortedMinY[j] <= maxY && sortedMaxY[j] >= minY)
                    matches++;
            }
        }

        return matches;
    }

    [Benchmark]
    [BenchmarkCategory("Shape")]
    public int RTree()
    {
        int matches = 0;
        for (int i = 0; i < QueryCount; i++)
            matches += tree.CountOverlapping(queryX[i], queryY[i], queryX[i] + QuerySide, queryY[i] + QuerySide);

        return matches;
    }

    [Benchmark]
    [BenchmarkCategory("Shape")]
    public int Grid()
    {
        int matches = 0;
        for (int i = 0; i < QueryCount; i++)
            matches += grid.CountOverlapping(queryX[i], queryY[i], queryX[i] + QuerySide, queryY[i] + QuerySide);

        return matches;
    }

    // ---- Packing: sort-tile against a Hilbert order, on one harness so only the ordering differs ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Packing")]
    public int Packed_SortTile()
    {
        int matches = 0;
        for (int i = 0; i < QueryCount; i++)
            matches += strPacked.CountOverlapping(queryX[i], queryY[i], queryX[i] + QuerySide, queryY[i] + QuerySide);

        return matches;
    }

    [Benchmark]
    [BenchmarkCategory("Packing")]
    public int Packed_Hilbert()
    {
        int matches = 0;
        for (int i = 0; i < QueryCount; i++)
            matches += hilbertPacked.CountOverlapping(queryX[i], queryY[i], queryX[i] + QuerySide, queryY[i] + QuerySide);

        return matches;
    }

    // The deviate is drawn on both shapes even though the uniform one ignores it, so the two draw the same
    // numbers in the same order from the same seed and the box *positions* are identical between them. Without
    // that the RNG streams diverge and the comparison quietly varies position as well as extent — which is the
    // shape of the sampling bias that made an earlier KdTreeShapeBenchmark flatter its own type.
    private double Extent(Random rand)
    {
        double deviate = rand.NextDouble();
        return Extents == Shape.Uniform ? UniformExtent : MinExtent * Math.Pow(MaxExtent / MinExtent, deviate);
    }

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

    // A bucketed uniform-cell grid, the structure the R-tree is supposed to lose to on uniform extents. It is
    // here because that claim is a recommendation about which type to reach for, and this repository's rule is
    // that such a claim needs its own arm rather than an inference chained from two other comparisons.
    //
    // Built the way a competent hand-roll would be: cells sized at twice the mean extent, entries in CSR
    // layout (a count pass, a prefix sum, a fill) so a populated grid carries no per-cell list object, and each
    // box filed into every cell it overlaps. A query walks the cells its own box covers, and a stamp array
    // suppresses the duplicates that replication creates — which is the grid's structural cost, paid on the
    // build for a small box and on every query for a large one.
    private sealed class BucketGrid
    {
        private readonly SpatialBox<int>[] _boxes;
        private readonly int[] _cellStart;
        private readonly int[] _entries;
        private readonly int[] _stamp;
        private readonly double _cellSize;
        private readonly int _side;
        private int _query;

        internal BucketGrid(SpatialBox<int>[] boxes, double domain)
        {
            _boxes = boxes;

            double mean = 0;
            foreach (SpatialBox<int> box in boxes)
                mean += ((box.MaxX - box.MinX) + (box.MaxY - box.MinY)) * 0.5;

            _cellSize = Math.Max(2 * mean / boxes.Length, domain / 1024);
            _side = (int)(domain / _cellSize) + 1;
            _cellStart = new int[(_side * _side) + 1];
            _stamp = new int[boxes.Length];

            // Count pass: how many cells each box lands in.
            for (int i = 0; i < boxes.Length; i++)
            {
                Cells(boxes[i], out int loX, out int hiX, out int loY, out int hiY);
                for (int y = loY; y <= hiY; y++)
                {
                    for (int x = loX; x <= hiX; x++)
                        _cellStart[(y * _side) + x]++;
                }
            }

            // Prefix sum, turning the counts into starts with the total in the trailing slot.
            int running = 0;
            for (int cell = 0; cell <= _side * _side; cell++)
            {
                int here = _cellStart[cell];
                _cellStart[cell] = running;
                running += here;
            }

            // Fill pass, using a cursor per cell so the starts survive it.
            _entries = new int[running];
            var cursor = new int[_side * _side];
            for (int i = 0; i < boxes.Length; i++)
            {
                Cells(boxes[i], out int loX, out int hiX, out int loY, out int hiY);
                for (int y = loY; y <= hiY; y++)
                {
                    for (int x = loX; x <= hiX; x++)
                    {
                        int cell = (y * _side) + x;
                        _entries[_cellStart[cell] + cursor[cell]++] = i;
                    }
                }
            }
        }

        internal int CountOverlapping(double minX, double minY, double maxX, double maxY)
        {
            int stamp = ++_query;
            int matches = 0;

            Span(minX, maxX, out int loX, out int hiX);
            Span(minY, maxY, out int loY, out int hiY);

            for (int y = loY; y <= hiY; y++)
            {
                for (int x = loX; x <= hiX; x++)
                {
                    int cell = (y * _side) + x;
                    for (int slot = _cellStart[cell]; slot < _cellStart[cell + 1]; slot++)
                    {
                        int entry = _entries[slot];
                        if (_stamp[entry] == stamp)
                            continue;

                        _stamp[entry] = stamp;
                        SpatialBox<int> box = _boxes[entry];
                        if (box.MinX <= maxX && box.MaxX >= minX && box.MinY <= maxY && box.MaxY >= minY)
                            matches++;
                    }
                }
            }

            return matches;
        }

        private void Cells(SpatialBox<int> box, out int loX, out int hiX, out int loY, out int hiY)
        {
            Span(box.MinX, box.MaxX, out loX, out hiX);
            Span(box.MinY, box.MaxY, out loY, out hiY);
        }

        private void Span(double lo, double hi, out int first, out int last)
        {
            first = Math.Clamp((int)(lo / _cellSize), 0, _side - 1);
            last = Math.Clamp((int)(hi / _cellSize), 0, _side - 1);
        }
    }

    // A minimal static bounding-volume hierarchy over a caller-supplied entry order: fixed fanout, one flat
    // array of node boxes, leaf i owning the run of 16 entries at i * 16 — the same implicit shape the shipped
    // RTree uses. It exists only so the two packing orders can be compared with everything else held identical.
    // Deliberately not a general-purpose type: no validation, no payloads, count-only queries.
    private sealed class PackedTree
    {
        private const int NodeCapacity = 16;

        private readonly double[] _boxes;
        private readonly double[] _nodes;
        private readonly int[] _levelStart;
        private readonly int _count;

        private PackedTree(SpatialBox<int>[] ordered)
        {
            _count = ordered.Length;
            _boxes = new double[_count * 4];
            for (int i = 0; i < _count; i++)
            {
                int at = i * 4;
                _boxes[at] = ordered[i].MinX;
                _boxes[at + 1] = ordered[i].MinY;
                _boxes[at + 2] = ordered[i].MaxX;
                _boxes[at + 3] = ordered[i].MaxY;
            }

            var starts = new List<int> { 0 };
            int total = 0;
            for (int level = (_count + NodeCapacity - 1) / NodeCapacity; ; level = (level + NodeCapacity - 1) / NodeCapacity)
            {
                total += level;
                starts.Add(total);
                if (level == 1)
                    break;
            }

            _levelStart = [.. starts];
            _nodes = new double[total * 4];

            for (int leaf = 0; leaf < _levelStart[1]; leaf++)
                Cover(_boxes, leaf * NodeCapacity, Math.Min(_count, (leaf * NodeCapacity) + NodeCapacity), leaf);

            for (int level = 1; level < _levelStart.Length - 1; level++)
            {
                int below = _levelStart[level - 1];
                int belowCount = _levelStart[level] - below;
                for (int node = 0; node < _levelStart[level + 1] - _levelStart[level]; node++)
                {
                    int start = node * NodeCapacity;
                    Cover(_nodes, below + start, below + Math.Min(belowCount, start + NodeCapacity), _levelStart[level] + node);
                }
            }
        }

        // Sort-tile: the same recursive tiling the shipped type does, restated here so both packing arms are
        // built by this class rather than one of them arriving from elsewhere.
        internal static PackedTree SortTiled(SpatialBox<int>[] source)
        {
            var ordered = (SpatialBox<int>[])source.Clone();
            long capacity = NodeCapacity;
            while (capacity * NodeCapacity < ordered.Length)
                capacity *= NodeCapacity;

            Tile(ordered, new double[ordered.Length], 0, ordered.Length, capacity);
            return new PackedTree(ordered);
        }

        // Hilbert order: one sort of the whole array on the Hilbert index of each box's centre, quantized onto
        // a 2^16 grid over the domain. No per-level work at all, which is the packing's own selling point.
        internal static PackedTree HilbertOrdered(SpatialBox<int>[] source, double domain)
        {
            var ordered = (SpatialBox<int>[])source.Clone();
            var keys = new ulong[ordered.Length];
            const double Scale = 65535;

            for (int i = 0; i < ordered.Length; i++)
            {
                SpatialBox<int> box = ordered[i];
                var x = (uint)Math.Clamp((box.MinX + box.MaxX) * 0.5 / domain * Scale, 0, Scale);
                var y = (uint)Math.Clamp((box.MinY + box.MaxY) * 0.5 / domain * Scale, 0, Scale);
                keys[i] = HilbertCurve.Encode2D(x, y);
            }

            Array.Sort(keys, ordered);
            return new PackedTree(ordered);
        }

        internal int CountOverlapping(double minX, double minY, double maxX, double maxY)
            => Walk(_levelStart.Length - 2, 0, minX, minY, maxX, maxY);

        private static void Tile(SpatialBox<int>[] items, double[] keys, int lo, int hi, long capacity)
        {
            int children = (int)((hi - lo + capacity - 1) / capacity);
            if (children > 1)
            {
                int slices = (int)Math.Ceiling(Math.Sqrt(children));
                long sliceEntries = (long)((children + slices - 1) / slices) * capacity;

                SortRange(items, keys, lo, hi, byX: true);
                for (long slice = lo; slice < hi; slice += sliceEntries)
                    SortRange(items, keys, (int)slice, (int)Math.Min(hi, slice + sliceEntries), byX: false);
            }

            if (capacity <= NodeCapacity)
                return;

            for (long child = lo; child < hi; child += capacity)
                Tile(items, keys, (int)child, (int)Math.Min(hi, child + capacity), capacity / NodeCapacity);
        }

        private static void SortRange(SpatialBox<int>[] items, double[] keys, int lo, int hi, bool byX)
        {
            for (int i = lo; i < hi; i++)
            {
                SpatialBox<int> box = items[i];
                keys[i] = byX ? (box.MinX + box.MaxX) * 0.5 : (box.MinY + box.MaxY) * 0.5;
            }

            Array.Sort(keys, items, lo, hi - lo);
        }

        private void Cover(double[] source, int start, int end, int target)
        {
            double minX = double.PositiveInfinity;
            double minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double maxY = double.NegativeInfinity;

            for (int i = start; i < end; i++)
            {
                int at = i * 4;
                minX = Math.Min(minX, source[at]);
                minY = Math.Min(minY, source[at + 1]);
                maxX = Math.Max(maxX, source[at + 2]);
                maxY = Math.Max(maxY, source[at + 3]);
            }

            int to = target * 4;
            _nodes[to] = minX;
            _nodes[to + 1] = minY;
            _nodes[to + 2] = maxX;
            _nodes[to + 3] = maxY;
        }

        private int Walk(int level, int node, double minX, double minY, double maxX, double maxY)
        {
            int at = (_levelStart[level] + node) * 4;
            if (!(_nodes[at] <= maxX && _nodes[at + 2] >= minX && _nodes[at + 1] <= maxY && _nodes[at + 3] >= minY))
                return 0;

            int start = node * NodeCapacity;
            int matches = 0;

            if (level == 0)
            {
                int end = Math.Min(_count, start + NodeCapacity);
                for (int entry = start; entry < end; entry++)
                {
                    int box = entry * 4;
                    if (_boxes[box] <= maxX && _boxes[box + 2] >= minX && _boxes[box + 1] <= maxY && _boxes[box + 3] >= minY)
                        matches++;
                }

                return matches;
            }

            int lastChild = Math.Min(_levelStart[level] - _levelStart[level - 1], start + NodeCapacity);
            for (int child = start; child < lastChild; child++)
                matches += Walk(level - 1, child, minX, minY, maxX, maxY);

            return matches;
        }
    }
}
