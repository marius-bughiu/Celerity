using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="RTree{TValue}"/> against the brute-force scan a caller
/// writes by hand: test every box, keep the ones that qualify. CsCheck generates random box sets and random
/// queries, so any disagreement shrinks to a minimal reproduction with the seed printed.
///
/// <para>
/// The pruning is what is on trial. Every query the tree answers is a claim that some subtree's bounding box
/// could not have held a match, and the failure mode of a wrong claim is a <i>missing</i> match rather than an
/// exception — exactly the kind of bug a hand-written example suite steps around, because the examples are
/// chosen from the same intuition that wrote the prune. Reconciling against an oracle that prunes nothing is
/// what closes that. It is a sharper test here than for <see cref="KdTree{TValue}"/>: an R-tree's bounding
/// boxes are computed by the build rather than implied by a split plane, so a packing bug and a pruning bug
/// both surface as the same lost match.
/// </para>
///
/// <para>
/// Coordinates come off a small integer grid rather than the whole <see cref="double"/> range. That is
/// deliberate: it makes duplicate extents, zero-area boxes and exactly-on-the-edge queries common instead of
/// vanishingly rare, and those are the cases where the closed-edge rule is decided. Box counts run past 256 so
/// that the generated trees are three levels deep and the recursive tiling is exercised rather than only the
/// single-node case.
/// </para>
/// </summary>
public class RTreeDifferentialTests
{
    private static readonly Gen<(int Count, int Extent, int Span, uint Seed)> GenScenario =
        Gen.Select(Gen.Int[0, 400], Gen.Int[1, 40], Gen.Int[0, 12], Gen.UInt);

    [Fact]
    public void OverlapQueries_ShouldMatchTheBruteForceScan()
    {
        GenScenario.Sample(spec =>
        {
            SpatialBox<int>[] boxes = Build(spec.Count, spec.Extent, spec.Span, spec.Seed);
            var tree = new RTree<int>(boxes);
            var rand = Queries(spec.Seed);

            for (int q = 0; q < 20; q++)
            {
                (double minX, double minY, double maxX, double maxY) = Query(rand, spec.Extent, spec.Span);

                int[] expected = boxes
                    .Where(b => Overlaps(b, minX, minY, maxX, maxY))
                    .Select(b => b.Value)
                    .OrderBy(v => v)
                    .ToArray();

                Assert.Equal(expected.Length, tree.CountOverlapping(minX, minY, maxX, maxY));
                Assert.Equal(expected.Length > 0, tree.ContainsOverlapping(minX, minY, maxX, maxY));
                Assert.Equal(expected, tree.GetOverlapping(minX, minY, maxX, maxY).Select(b => b.Value).OrderBy(v => v));
            }
        }, iter: 200);
    }

    [Fact]
    public void PointQueries_ShouldMatchTheBruteForceScan()
    {
        GenScenario.Sample(spec =>
        {
            SpatialBox<int>[] boxes = Build(spec.Count, spec.Extent, spec.Span, spec.Seed);
            var tree = new RTree<int>(boxes);
            var rand = Queries(spec.Seed);

            for (int q = 0; q < 20; q++)
            {
                double x = rand.Next(-spec.Extent, spec.Extent + 1);
                double y = rand.Next(-spec.Extent, spec.Extent + 1);

                int[] expected = boxes
                    .Where(b => Overlaps(b, x, y, x, y))
                    .Select(b => b.Value)
                    .OrderBy(v => v)
                    .ToArray();

                Assert.Equal(expected.Length, tree.CountAtPoint(x, y));
                Assert.Equal(expected.Length > 0, tree.ContainsAtPoint(x, y));
                Assert.Equal(expected, tree.GetAtPoint(x, y).Select(b => b.Value).OrderBy(v => v));
            }
        }, iter: 200);
    }

    [Fact]
    public void CopyOverlapping_ShouldWriteTheSameMatches_WhenGivenRoomForThemAll()
    {
        GenScenario.Sample(spec =>
        {
            SpatialBox<int>[] boxes = Build(spec.Count, spec.Extent, spec.Span, spec.Seed);
            var tree = new RTree<int>(boxes);
            var rand = Queries(spec.Seed);
            var buffer = new SpatialBox<int>[Math.Max(1, spec.Count)];

            for (int q = 0; q < 20; q++)
            {
                (double minX, double minY, double maxX, double maxY) = Query(rand, spec.Extent, spec.Span);

                int[] expected = boxes
                    .Where(b => Overlaps(b, minX, minY, maxX, maxY))
                    .Select(b => b.Value)
                    .OrderBy(v => v)
                    .ToArray();

                int written = tree.CopyOverlapping(minX, minY, maxX, maxY, buffer);

                Assert.Equal(expected.Length, written);
                Assert.Equal(expected, buffer.Take(written).Select(b => b.Value).OrderBy(v => v));
            }
        }, iter: 100);
    }

    [Fact]
    public void Enumeration_ShouldBeAPermutationOfTheInput()
    {
        GenScenario.Sample(spec =>
        {
            SpatialBox<int>[] boxes = Build(spec.Count, spec.Extent, spec.Span, spec.Seed);
            var tree = new RTree<int>(boxes);

            Assert.Equal(spec.Count, tree.Count);
            Assert.Equal(Enumerable.Range(0, spec.Count), tree.Select(b => b.Value).OrderBy(v => v));

            // Packing permutes the entries, so every extent has to survive the move attached to its own value.
            foreach (SpatialBox<int> entry in tree)
            {
                SpatialBox<int> original = boxes[entry.Value];
                Assert.Equal(original.MinX, entry.MinX);
                Assert.Equal(original.MinY, entry.MinY);
                Assert.Equal(original.MaxX, entry.MaxX);
                Assert.Equal(original.MaxY, entry.MaxY);
            }
        }, iter: 100);
    }

    [Fact]
    public void TryGetBounds_ShouldReportTheExtentOfEveryStoredBox()
    {
        GenScenario.Sample(spec =>
        {
            SpatialBox<int>[] boxes = Build(spec.Count, spec.Extent, spec.Span, spec.Seed);
            var tree = new RTree<int>(boxes);

            bool any = tree.TryGetBounds(out double minX, out double minY, out double maxX, out double maxY);

            Assert.Equal(spec.Count > 0, any);
            if (!any)
                return;

            // The root's own box, which is the pruning's outermost claim: a query outside it matches nothing.
            Assert.Equal(boxes.Min(b => b.MinX), minX);
            Assert.Equal(boxes.Min(b => b.MinY), minY);
            Assert.Equal(boxes.Max(b => b.MaxX), maxX);
            Assert.Equal(boxes.Max(b => b.MaxY), maxY);
        }, iter: 100);
    }

    private static SpatialBox<int>[] Build(int count, int extent, int span, uint seed)
    {
        var rand = new Random((int)seed);
        var boxes = new SpatialBox<int>[count];

        for (int i = 0; i < count; i++)
        {
            double minX = rand.Next(-extent, extent + 1);
            double minY = rand.Next(-extent, extent + 1);

            // A span of zero gives a degenerate box, which is a point filed among extents — the case that
            // decides whether the closed-edge rule holds all the way down.
            boxes[i] = new SpatialBox<int>(minX, minY, minX + rand.Next(0, span + 1), minY + rand.Next(0, span + 1), i);
        }

        return boxes;
    }

    // A second stream from the same seed, so a shrunk counterexample replays its queries as well as its boxes.
    private static Random Queries(uint seed) => new(unchecked((int)(seed ^ 0x9E3779B9)));

    private static (double MinX, double MinY, double MaxX, double MaxY) Query(Random rand, int extent, int span)
    {
        double minX = rand.Next(-extent, extent + 1);
        double minY = rand.Next(-extent, extent + 1);
        return (minX, minY, minX + rand.Next(0, span + 1), minY + rand.Next(0, span + 1));
    }

    private static bool Overlaps(SpatialBox<int> box, double minX, double minY, double maxX, double maxY)
        => box.MinX <= maxX && box.MaxX >= minX && box.MinY <= maxY && box.MaxY >= minY;
}
