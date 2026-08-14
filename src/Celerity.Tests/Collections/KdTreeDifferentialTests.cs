using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="KdTree{TValue}"/> against the brute-force scan a caller
/// writes by hand: measure every point, keep the ones that qualify. CsCheck generates random point sets and
/// random queries, so any disagreement shrinks to a minimal reproduction with the seed printed.
///
/// <para>
/// The pruning is what is on trial. Every query the tree answers is a claim that some subtree could not have
/// held a result, and the failure mode of a wrong claim is a <i>missing</i> match rather than an exception —
/// exactly the kind of bug a hand-written example suite steps around, because the examples are chosen from the
/// same intuition that wrote the prune. Reconciling against an oracle that prunes nothing is what closes that.
/// </para>
///
/// <para>
/// Coordinates are drawn from a small integer grid rather than from the whole <see cref="double"/> range. That
/// is deliberate: it makes ties, duplicate points and exactly-on-the-boundary queries common instead of
/// vanishingly rare, and those are the cases where an inclusive bound or a strict prune is decided. It also
/// keeps every squared distance exactly representable, so the oracle can be compared with <c>==</c> and a
/// disagreement is never a rounding artefact.
/// </para>
/// </summary>
public class KdTreeDifferentialTests
{
    private static readonly Gen<(int Count, int Extent, uint Seed)> GenScenario =
        Gen.Select(Gen.Int[0, 200], Gen.Int[1, 40], Gen.UInt);

    [Fact]
    public void RadiusQueries_ShouldMatchTheBruteForceScan()
    {
        GenScenario.Sample(spec =>
        {
            SpatialPoint<int>[] points = Build(spec.Count, spec.Extent, spec.Seed);
            var tree = new KdTree<int>(points);
            var rand = new Random((int)spec.Seed);

            for (int q = 0; q < 20; q++)
            {
                double x = rand.Next(-spec.Extent, spec.Extent + 1);
                double y = rand.Next(-spec.Extent, spec.Extent + 1);
                double radius = rand.Next(0, spec.Extent + 1);

                int[] expected = points
                    .Where(p => Squared(p, x, y) <= radius * radius)
                    .Select(p => p.Value)
                    .OrderBy(v => v)
                    .ToArray();

                Assert.Equal(expected.Length, tree.CountWithin(x, y, radius));
                Assert.Equal(expected.Length > 0, tree.ContainsWithin(x, y, radius));
                Assert.Equal(expected, tree.GetWithin(x, y, radius).Select(p => p.Value).OrderBy(v => v));
            }
        }, iter: 200);
    }

    [Fact]
    public void RectangleQueries_ShouldMatchTheBruteForceScan()
    {
        GenScenario.Sample(spec =>
        {
            SpatialPoint<int>[] points = Build(spec.Count, spec.Extent, spec.Seed);
            var tree = new KdTree<int>(points);
            var rand = new Random((int)spec.Seed);

            for (int q = 0; q < 20; q++)
            {
                double minX = rand.Next(-spec.Extent, spec.Extent + 1);
                double minY = rand.Next(-spec.Extent, spec.Extent + 1);
                double maxX = minX + rand.Next(0, spec.Extent + 1);
                double maxY = minY + rand.Next(0, spec.Extent + 1);

                int[] expected = points
                    .Where(p => p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY)
                    .Select(p => p.Value)
                    .OrderBy(v => v)
                    .ToArray();

                Assert.Equal(expected.Length, tree.CountInRectangle(minX, minY, maxX, maxY));
                Assert.Equal(expected.Length > 0, tree.ContainsInRectangle(minX, minY, maxX, maxY));
                Assert.Equal(expected, tree.GetInRectangle(minX, minY, maxX, maxY).Select(p => p.Value).OrderBy(v => v));
            }
        }, iter: 200);
    }

    [Fact]
    public void NearestQueries_ShouldMatchTheBruteForceMinimum()
    {
        GenScenario.Sample(spec =>
        {
            SpatialPoint<int>[] points = Build(spec.Count, spec.Extent, spec.Seed);
            var tree = new KdTree<int>(points);
            var rand = new Random((int)spec.Seed);

            for (int q = 0; q < 20; q++)
            {
                double x = rand.Next(-spec.Extent, spec.Extent + 1);
                double y = rand.Next(-spec.Extent, spec.Extent + 1);

                bool found = tree.TryFindNearest(x, y, out SpatialPoint<int> nearest);
                Assert.Equal(points.Length > 0, found);

                if (!found)
                    continue;

                // Ties are unspecified, so the contract is the distance, not the identity of the winner.
                double best = points.Min(p => Squared(p, x, y));
                Assert.Equal(best, Squared(nearest, x, y));
                Assert.Contains(points, p => p.Value == nearest.Value);
            }
        }, iter: 200);
    }

    [Fact]
    public void BoundedNearestQueries_ShouldMatchTheBruteForceMinimumInsideTheBound()
    {
        GenScenario.Sample(spec =>
        {
            SpatialPoint<int>[] points = Build(spec.Count, spec.Extent, spec.Seed);
            var tree = new KdTree<int>(points);
            var rand = new Random((int)spec.Seed);

            for (int q = 0; q < 20; q++)
            {
                double x = rand.Next(-spec.Extent, spec.Extent + 1);
                double y = rand.Next(-spec.Extent, spec.Extent + 1);
                double bound = rand.Next(0, spec.Extent + 1);

                double[] inside = points
                    .Select(p => Squared(p, x, y))
                    .Where(d => d <= bound * bound)
                    .ToArray();

                bool found = tree.TryFindNearest(x, y, bound, out SpatialPoint<int> nearest);

                Assert.Equal(inside.Length > 0, found);
                if (found)
                    Assert.Equal(inside.Min(), Squared(nearest, x, y));
            }
        }, iter: 200);
    }

    [Fact]
    public void KNearestQueries_ShouldMatchTheBruteForceOrdering()
    {
        GenScenario.Sample(spec =>
        {
            SpatialPoint<int>[] points = Build(spec.Count, spec.Extent, spec.Seed);
            var tree = new KdTree<int>(points);
            var rand = new Random((int)spec.Seed);

            for (int q = 0; q < 10; q++)
            {
                double x = rand.Next(-spec.Extent, spec.Extent + 1);
                double y = rand.Next(-spec.Extent, spec.Extent + 1);
                int k = rand.Next(0, 12);

                double[] expected = points
                    .Select(p => Squared(p, x, y))
                    .OrderBy(d => d)
                    .Take(k)
                    .ToArray();

                SpatialPoint<int>[] actual = tree.GetNearest(x, y, k);

                // Which of a set of tied points is returned is unspecified, so the comparable invariant is the
                // sequence of distances — which pins both the selection and the ascending order it promises.
                Assert.Equal(expected, actual.Select(p => Squared(p, x, y)).ToArray());
            }
        }, iter: 200);
    }

    [Fact]
    public void CopyNearest_ShouldAgreeWithGetNearest_WhenTheBufferIsOffsetAndUndersized()
    {
        GenScenario.Sample(spec =>
        {
            SpatialPoint<int>[] points = Build(spec.Count, spec.Extent, spec.Seed);
            var tree = new KdTree<int>(points);
            var rand = new Random((int)spec.Seed);

            for (int q = 0; q < 10; q++)
            {
                double x = rand.Next(-spec.Extent, spec.Extent + 1);
                double y = rand.Next(-spec.Extent, spec.Extent + 1);
                int offset = rand.Next(0, 4);
                int room = rand.Next(0, 10);

                var buffer = new SpatialPoint<int>[offset + room];
                int written = tree.CopyNearest(x, y, buffer, offset);

                Assert.Equal(Math.Min(room, points.Length), written);
                Assert.Equal(
                    tree.GetNearest(x, y, room).Select(p => Squared(p, x, y)).ToArray(),
                    buffer.Skip(offset).Take(written).Select(p => Squared(p, x, y)).ToArray());
            }
        }, iter: 200);
    }

    [Fact]
    public void Enumeration_ShouldBeAPermutationOfTheInput()
    {
        GenScenario.Sample(spec =>
        {
            SpatialPoint<int>[] points = Build(spec.Count, spec.Extent, spec.Seed);
            var tree = new KdTree<int>(points);

            Assert.Equal(points.Length, tree.Count);
            Assert.Equal(
                points.Select(p => (p.X, p.Y, p.Value)).OrderBy(t => t.Value).ToArray(),
                tree.Select(p => (p.X, p.Y, p.Value)).OrderBy(t => t.Value).ToArray());
        }, iter: 200);
    }

    // A small integer grid, so duplicate coordinates and exactly-on-the-boundary queries are common rather
    // than rare. The value is the point's index, which makes it a stable identity across the oracle and the
    // tree even when several points share a position.
    private static SpatialPoint<int>[] Build(int count, int extent, uint seed)
    {
        var rand = new Random((int)seed);
        var points = new SpatialPoint<int>[count];
        for (int i = 0; i < count; i++)
            points[i] = new SpatialPoint<int>(rand.Next(-extent, extent + 1), rand.Next(-extent, extent + 1), i);

        return points;
    }

    private static double Squared(SpatialPoint<int> point, double x, double y)
    {
        double dx = point.X - x;
        double dy = point.Y - y;
        return (dx * dx) + (dy * dy);
    }
}
