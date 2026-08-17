using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="SpatialGrid{TValue}"/> against the two things a caller
/// writes instead: the brute-force scan that measures every point, and the bucketed
/// <c>Dictionary&lt;(int, int), List&lt;T&gt;&gt;</c> that is the idiomatic hand-rolled grid. CsCheck generates
/// the point sets, the queries and — for the sequence test — the operations themselves, so a disagreement
/// shrinks to a minimal reproduction with the seed printed.
///
/// <para>
/// Two different failure modes are on trial. The queries can lose a match by visiting the wrong set of cells,
/// which shows up as a <i>missing</i> result rather than an exception and is exactly what an example suite
/// written from the same intuition as the cell arithmetic will step around. The mutations can corrupt a cell's
/// intrusive list — a <see cref="SpatialGrid{TValue}.Move"/> that unlinks from the wrong cell, a
/// <see cref="SpatialGrid{TValue}.Remove"/> that drops a neighbour with it — which is where a mutable
/// structure's bugs actually live and which only a long randomized operation sequence reaches.
/// </para>
///
/// <para>
/// Coordinates are drawn from a small integer grid rather than from the whole <see cref="double"/> range, the
/// same choice <see cref="KdTreeDifferentialTests"/> makes and for the same reasons: it makes ties, duplicate
/// points and exactly-on-a-cell-boundary placements common instead of vanishingly rare, and it keeps every
/// squared distance exactly representable, so a disagreement is never a rounding artefact.
/// </para>
/// </summary>
public class SpatialGridDifferentialTests
{
    private const double Extent = 24;

    private static readonly Gen<(int Count, int CellSize, uint Seed)> GenScenario =
        Gen.Select(Gen.Int[0, 150], Gen.Int[1, 8], Gen.UInt);

    [Fact]
    public void RadiusQueries_ShouldMatchTheBruteForceScan()
    {
        GenScenario.Sample(spec =>
        {
            (SpatialGrid<int> grid, List<Point> oracle) = Build(spec.Count, spec.CellSize, spec.Seed);
            var rand = new Random((int)spec.Seed);

            for (int q = 0; q < 20; q++)
            {
                double x = rand.Next(-4, (int)Extent + 5);
                double y = rand.Next(-4, (int)Extent + 5);
                double radius = rand.Next(0, 12);

                int[] expected = oracle
                    .Where(p => Squared(p, x, y) <= radius * radius)
                    .Select(p => p.Value)
                    .OrderBy(v => v)
                    .ToArray();

                Assert.Equal(expected.Length, grid.CountWithin(x, y, radius));
                Assert.Equal(expected.Length > 0, grid.ContainsWithin(x, y, radius));
                Assert.Equal(expected, grid.GetWithin(x, y, radius).Select(p => p.Value).OrderBy(v => v));

                var buffer = new SpatialPoint<int>[expected.Length];
                Assert.Equal(expected.Length, grid.CopyWithin(x, y, radius, buffer));
                Assert.Equal(expected, buffer.Select(p => p.Value).OrderBy(v => v));
            }
        }, iter: 200);
    }

    [Fact]
    public void RectangleQueries_ShouldMatchTheBruteForceScan()
    {
        GenScenario.Sample(spec =>
        {
            (SpatialGrid<int> grid, List<Point> oracle) = Build(spec.Count, spec.CellSize, spec.Seed);
            var rand = new Random((int)spec.Seed);

            for (int q = 0; q < 20; q++)
            {
                double minX = rand.Next(-4, (int)Extent + 5);
                double minY = rand.Next(-4, (int)Extent + 5);
                double maxX = minX + rand.Next(0, 12);
                double maxY = minY + rand.Next(0, 12);

                int[] expected = oracle
                    .Where(p => p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY)
                    .Select(p => p.Value)
                    .OrderBy(v => v)
                    .ToArray();

                Assert.Equal(expected.Length, grid.CountInRectangle(minX, minY, maxX, maxY));
                Assert.Equal(expected.Length > 0, grid.ContainsInRectangle(minX, minY, maxX, maxY));
                Assert.Equal(expected, grid.GetInRectangle(minX, minY, maxX, maxY).Select(p => p.Value).OrderBy(v => v));
            }
        }, iter: 200);
    }

    [Fact]
    public void NearestQuery_ShouldMatchTheBruteForceScan()
    {
        GenScenario.Sample(spec =>
        {
            (SpatialGrid<int> grid, List<Point> oracle) = Build(spec.Count, spec.CellSize, spec.Seed);
            var rand = new Random((int)spec.Seed);

            for (int q = 0; q < 20; q++)
            {
                double x = rand.Next(-4, (int)Extent + 5);
                double y = rand.Next(-4, (int)Extent + 5);
                double bound = rand.Next(0, 40);

                // Ties are unspecified, so the distance is what has to agree — not which of two equally close
                // points was picked.
                double expectedBest = oracle.Count == 0 ? double.PositiveInfinity : oracle.Min(p => Squared(p, x, y));

                Assert.Equal(oracle.Count > 0, grid.TryFindNearest(x, y, out SpatialPoint<int> nearest));
                if (oracle.Count > 0)
                    Assert.Equal(expectedBest, Squared(nearest, x, y));

                bool expectedInBound = expectedBest <= bound * bound;
                Assert.Equal(expectedInBound, grid.TryFindNearest(x, y, bound, out SpatialPoint<int> bounded));
                if (expectedInBound)
                    Assert.Equal(expectedBest, Squared(bounded, x, y));
            }
        }, iter: 200);
    }

    [Fact]
    public void RandomizedOperationSequence_ShouldMatchTheDictionaryOfListsOracle()
    {
        Gen.Select(Gen.Int[1, 8], Gen.Int[20, 300], Gen.UInt).Sample(spec =>
        {
            (int cellSize, int operations, uint seed) = spec;

            var grid = new SpatialGrid<int>(0, 0, Extent, Extent, cellSize);
            var oracle = new BucketedGrid(cellSize);
            var live = new List<SpatialGridHandle>();
            var rand = new Random((int)seed);
            int nextValue = 0;

            for (int op = 0; op < operations; op++)
            {
                int roll = rand.Next(100);

                if (roll < 45 || live.Count == 0)
                {
                    double x = rand.Next(-2, (int)Extent + 3);
                    double y = rand.Next(-2, (int)Extent + 3);
                    live.Add(grid.Add(x, y, nextValue));
                    oracle.Add(nextValue, x, y);
                    nextValue++;
                }
                else if (roll < 85)
                {
                    // Move-heavy on purpose: this is the operation the type exists for and the one whose
                    // bugs — an unlink from the cell the entry used to be in, a relink that loses its
                    // neighbours — corrupt a cell list silently.
                    SpatialGridHandle handle = live[rand.Next(live.Count)];
                    double x = rand.Next(-2, (int)Extent + 3);
                    double y = rand.Next(-2, (int)Extent + 3);
                    Assert.True(grid.TryGetPoint(handle, out SpatialPoint<int> before));
                    grid.Move(handle, x, y);
                    oracle.Move(before.Value, x, y);
                }
                else if (roll < 97)
                {
                    int index = rand.Next(live.Count);
                    SpatialGridHandle handle = live[index];
                    Assert.True(grid.TryGetPoint(handle, out SpatialPoint<int> point));
                    grid.Remove(handle);
                    oracle.Remove(point.Value);
                    live.RemoveAt(index);

                    // The retired handle must stay retired, whatever later reuses its slot.
                    Assert.False(grid.TryGetPoint(handle, out _));
                }
                else
                {
                    grid.Clear();
                    oracle.Clear();
                    foreach (SpatialGridHandle stale in live)
                        Assert.False(grid.TryGetPoint(stale, out _));

                    live.Clear();
                }

                Assert.Equal(oracle.Count, grid.Count);

                double qx = rand.Next(-2, (int)Extent + 3);
                double qy = rand.Next(-2, (int)Extent + 3);
                double radius = rand.Next(0, 10);

                Assert.Equal(
                    oracle.Within(qx, qy, radius),
                    grid.GetWithin(qx, qy, radius).Select(p => p.Value).OrderBy(v => v).ToArray());
            }

            Assert.Equal(
                oracle.All(),
                grid.Select(p => p.Value).OrderBy(v => v).ToArray());
        }, iter: 100);
    }

    // ---- helpers ------------------------------------------------------------------------------------

    private readonly record struct Point(double X, double Y, int Value);

    private static (SpatialGrid<int>, List<Point>) Build(int count, int cellSize, uint seed)
    {
        var rand = new Random(unchecked((int)(seed * 2654435761)));
        var grid = new SpatialGrid<int>(0, 0, Extent, Extent, cellSize);
        var oracle = new List<Point>(count);

        for (int i = 0; i < count; i++)
        {
            // A slice outside the declared world on purpose: those points clamp into an edge cell, and a
            // query has to keep finding them at their real coordinates rather than at the cell's.
            double x = rand.Next(-3, (int)Extent + 4);
            double y = rand.Next(-3, (int)Extent + 4);
            grid.Add(x, y, i);
            oracle.Add(new Point(x, y, i));
        }

        return (grid, oracle);
    }

    private static double Squared(Point point, double x, double y)
    {
        double dx = point.X - x;
        double dy = point.Y - y;
        return (dx * dx) + (dy * dy);
    }

    private static double Squared(SpatialPoint<int> point, double x, double y)
    {
        double dx = point.X - x;
        double dy = point.Y - y;
        return (dx * dx) + (dy * dy);
    }

    /// <summary>
    /// The hand-rolled bucketed grid the type is measured against, used here as a behavioural oracle: a
    /// dictionary of cell key to the list of entries in that cell, walked cell by cell for a radius query.
    /// </summary>
    private sealed class BucketedGrid(int cellSize)
    {
        private readonly Dictionary<(int, int), List<int>> _cells = [];
        private readonly Dictionary<int, (double X, double Y)> _positions = [];

        internal int Count => _positions.Count;

        internal void Add(int value, double x, double y)
        {
            _positions[value] = (x, y);
            Cell(x, y).Add(value);
        }

        internal void Move(int value, double x, double y)
        {
            Remove(value);
            Add(value, x, y);
        }

        internal void Remove(int value)
        {
            (double X, double Y) position = _positions[value];
            Cell(position.X, position.Y).Remove(value);
            _positions.Remove(value);
        }

        internal void Clear()
        {
            _cells.Clear();
            _positions.Clear();
        }

        internal int[] All() => _positions.Keys.OrderBy(v => v).ToArray();

        internal int[] Within(double x, double y, double radius)
        {
            var matches = new List<int>();

            int minColumn = Key(x - radius);
            int maxColumn = Key(x + radius);
            int minRow = Key(y - radius);
            int maxRow = Key(y + radius);

            for (int column = minColumn; column <= maxColumn; column++)
            {
                for (int row = minRow; row <= maxRow; row++)
                {
                    if (!_cells.TryGetValue((column, row), out List<int>? bucket))
                        continue;

                    foreach (int value in bucket)
                    {
                        (double X, double Y) position = _positions[value];
                        double dx = position.X - x;
                        double dy = position.Y - y;
                        if ((dx * dx) + (dy * dy) <= radius * radius)
                            matches.Add(value);
                    }
                }
            }

            matches.Sort();
            return [.. matches];
        }

        private List<int> Cell(double x, double y)
        {
            (int, int) key = (Key(x), Key(y));
            if (!_cells.TryGetValue(key, out List<int>? bucket))
            {
                bucket = [];
                _cells[key] = bucket;
            }

            return bucket;
        }

        private int Key(double value) => (int)Math.Floor(value / cellSize);
    }
}
