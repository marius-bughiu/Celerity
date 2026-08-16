using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Dedicated coverage for <see cref="RTree{TValue}"/>: construction and its rejections, both query families
/// across both tiers, the closed-edge rule, and the buffer contract the allocation-free tier carries.
///
/// <para>
/// The box counts here are chosen rather than round. Sixteen is the node fanout, so a tree of that size is a
/// single leaf and exercises none of the descent; 17 is the smallest two-level tree, 257 the smallest
/// three-level one, and 4,097 the smallest four-level one. Those three are what put the recursive tiling and
/// the multi-level walk under test deterministically rather than leaving them to the property suite.
/// </para>
/// </summary>
public class RTreeTests
{
    // A grid of unit boxes, one per cell, so a query's expected matches can be counted by hand from its edges.
    private static SpatialBox<int>[] Grid(int side)
    {
        var boxes = new SpatialBox<int>[side * side];
        for (int i = 0; i < boxes.Length; i++)
        {
            double x = i % side;
            double y = i / side;
            boxes[i] = new SpatialBox<int>(x * 10, y * 10, (x * 10) + 4, (y * 10) + 4, i);
        }

        return boxes;
    }

    private static SpatialBox<int>[] Ladder(int count)
    {
        var boxes = new SpatialBox<int>[count];
        for (int i = 0; i < count; i++)
            boxes[i] = new SpatialBox<int>(i, i % 37, i + 0.5, (i % 37) + 0.5, i);

        return boxes;
    }

    // ---- construction ------------------------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenTheSourceIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new RTree<int>(null!));
    }

    [Theory]
    [InlineData(double.NaN, 0, 1, 1)]
    [InlineData(0, double.NaN, 1, 1)]
    [InlineData(0, 0, double.NaN, 1)]
    [InlineData(0, 0, 1, double.NaN)]
    [InlineData(double.PositiveInfinity, 0, double.PositiveInfinity, 1)]
    [InlineData(double.NegativeInfinity, 0, 1, 1)]
    [InlineData(0, double.NegativeInfinity, 1, 1)]
    [InlineData(0, 0, 1, double.PositiveInfinity)]
    public void Constructor_ShouldThrowArgumentException_WhenACoordinateIsNotFinite(double minX, double minY, double maxX, double maxY)
    {
        SpatialBox<int>[] boxes = [new SpatialBox<int>(minX, minY, maxX, maxY, 0)];

        ArgumentException error = Assert.Throws<ArgumentException>(() => new RTree<int>(boxes));
        Assert.Equal("boxes", error.ParamName);
    }

    [Theory]
    [InlineData(0, 0, -1, 1)]
    [InlineData(0, 0, 1, -1)]
    public void Constructor_ShouldThrowArgumentException_WhenAnUpperEdgePrecedesItsLowerEdge(double minX, double minY, double maxX, double maxY)
    {
        SpatialBox<int>[] boxes = [new SpatialBox<int>(minX, minY, maxX, maxY, 0)];

        ArgumentException error = Assert.Throws<ArgumentException>(() => new RTree<int>(boxes));
        Assert.Equal("boxes", error.ParamName);
    }

    [Fact]
    public void Constructor_ShouldReadTheSequenceOnce_WhenTheSourceIsNotACollection()
    {
        // The counted path copies through ICollection<T>.CopyTo; a bare IEnumerable<T> has to go through the
        // List<T> fallback, and both have to land on the same tree.
        IEnumerable<SpatialBox<int>> lazy = Ladder(50).Where(_ => true);

        var tree = new RTree<int>(lazy);

        Assert.Equal(50, tree.Count);
        Assert.Equal(Enumerable.Range(0, 50), tree.Select(b => b.Value).OrderBy(v => v));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(257)]
    [InlineData(4097)]
    public void Constructor_ShouldIndexEveryBox_AtEachTreeHeight(int count)
    {
        SpatialBox<int>[] boxes = Ladder(count);

        var tree = new RTree<int>(boxes);

        Assert.Equal(count, tree.Count);
        Assert.Equal(Enumerable.Range(0, count), tree.Select(b => b.Value).OrderBy(v => v));

        // Every box has to be findable by a query for its own extent, at every height.
        for (int i = 0; i < count; i++)
        {
            SpatialBox<int> box = boxes[i];
            Assert.Contains(i, tree.GetOverlapping(box.MinX, box.MinY, box.MaxX, box.MaxY).Select(b => b.Value));
        }
    }

    // ---- shape -------------------------------------------------------------------------------------

    [Fact]
    public void Count_ShouldCountDuplicateExtentsSeparately_WhenBoxesCoincide()
    {
        SpatialBox<int>[] boxes = [.. Enumerable.Range(0, 40).Select(i => new SpatialBox<int>(2, 3, 5, 7, i))];

        var tree = new RTree<int>(boxes);

        Assert.Equal(40, tree.Count);
        Assert.Equal(40, tree.CountOverlapping(2, 3, 5, 7));
        Assert.Equal(40, tree.CountAtPoint(3, 4));
    }

    [Fact]
    public void Indexer_ShouldReturnEveryStoredBox_WhenWalkedInLayoutOrder()
    {
        var tree = new RTree<int>(Ladder(30));

        var seen = new List<int>();
        for (int i = 0; i < tree.Count; i++)
            seen.Add(tree[i].Value);

        Assert.Equal(Enumerable.Range(0, 30), seen.OrderBy(v => v));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(30)]
    [InlineData(int.MinValue)]
    public void Indexer_ShouldThrowArgumentOutOfRangeException_WhenTheIndexIsOutsideTheTree(int index)
    {
        var tree = new RTree<int>(Ladder(30));

        Assert.Throws<ArgumentOutOfRangeException>(() => tree[index]);
    }

    [Fact]
    public void TryGetBounds_ShouldReportFalse_WhenTheTreeIsEmpty()
    {
        var tree = new RTree<int>([]);

        Assert.False(tree.TryGetBounds(out double minX, out double minY, out double maxX, out double maxY));
        Assert.Equal(0, minX);
        Assert.Equal(0, minY);
        Assert.Equal(0, maxX);
        Assert.Equal(0, maxY);
    }

    [Fact]
    public void TryGetBounds_ShouldReportTheRootExtent_WhenTheTreeHoldsBoxes()
    {
        SpatialBox<int>[] boxes =
        [
            new SpatialBox<int>(-4, 2, -1, 3, 0),
            new SpatialBox<int>(0, -8, 100, 9, 1),
            new SpatialBox<int>(5, 5, 6, 6, 2),
        ];

        var tree = new RTree<int>(boxes);

        Assert.True(tree.TryGetBounds(out double minX, out double minY, out double maxX, out double maxY));
        Assert.Equal(-4, minX);
        Assert.Equal(-8, minY);
        Assert.Equal(100, maxX);
        Assert.Equal(9, maxY);
    }

    // ---- overlap queries ---------------------------------------------------------------------------

    [Fact]
    public void CountOverlapping_ShouldCountOnlyTheBoxesMeetingTheQuery_WhenTheTreeIsMultiLevel()
    {
        // A 20x20 grid of 4-wide boxes on a 10-unit pitch: 400 boxes, three levels deep. The query spans
        // cells 1..3 on both axes, so exactly nine boxes qualify and their neighbours must be pruned away.
        var tree = new RTree<int>(Grid(20));

        Assert.Equal(9, tree.CountOverlapping(10, 10, 34, 34));
        Assert.True(tree.ContainsOverlapping(10, 10, 34, 34));
        Assert.Equal(9, tree.GetOverlapping(10, 10, 34, 34).Length);
    }

    [Fact]
    public void CountOverlapping_ShouldReportZero_WhenTheQueryFallsInTheGapsBetweenBoxes()
    {
        // The boxes end at x = 4 mod 10, so the strip from 5 to 9 is empty on every row.
        var tree = new RTree<int>(Grid(20));

        Assert.Equal(0, tree.CountOverlapping(5, 5, 9, 9));
        Assert.False(tree.ContainsOverlapping(5, 5, 9, 9));
        Assert.Empty(tree.GetOverlapping(5, 5, 9, 9));
    }

    [Fact]
    public void CountOverlapping_ShouldReportZero_WhenTheQueryIsOutsideTheRootExtent()
    {
        // The root's own bounding box rejects this before a single child is looked at.
        var tree = new RTree<int>(Grid(20));

        Assert.Equal(0, tree.CountOverlapping(10_000, 10_000, 20_000, 20_000));
        Assert.False(tree.ContainsOverlapping(10_000, 10_000, 20_000, 20_000));
    }

    [Fact]
    public void CountOverlapping_ShouldCountBoxesTouchingOnlyAtAnEdgeOrCorner_BecauseEdgesAreClosed()
    {
        SpatialBox<int>[] boxes =
        [
            new SpatialBox<int>(0, 0, 1, 1, 0),
            new SpatialBox<int>(1, 1, 2, 2, 1),
            new SpatialBox<int>(2, 2, 3, 3, 2),
        ];
        var tree = new RTree<int>(boxes);

        // The query's corner sits exactly on box 1's corner, and its edge exactly on box 0's.
        Assert.Equal(2, tree.CountOverlapping(1, 0, 1, 1));
        Assert.Equal(1, tree.CountOverlapping(3, 3, 4, 4));
    }

    [Fact]
    public void CountOverlapping_ShouldReportZero_WhenTheTreeIsEmpty()
    {
        var tree = new RTree<int>([]);

        Assert.Equal(0, tree.CountOverlapping(-1, -1, 1, 1));
        Assert.False(tree.ContainsOverlapping(-1, -1, 1, 1));
        Assert.Empty(tree.GetOverlapping(-1, -1, 1, 1));
        Assert.Equal(0, tree.CopyOverlapping(-1, -1, 1, 1, new SpatialBox<int>[4]));
    }

    [Fact]
    public void CountOverlapping_ShouldReportZero_WhenAQueryEdgeIsNaN()
    {
        // NaN fails every comparison, so the query prunes the root and reports nothing rather than throwing.
        var tree = new RTree<int>(Grid(20));

        Assert.Equal(0, tree.CountOverlapping(double.NaN, 0, double.NaN, 40));
        Assert.Equal(0, tree.CountOverlapping(0, double.NaN, 40, double.NaN));
        Assert.False(tree.ContainsOverlapping(double.NaN, double.NaN, double.NaN, double.NaN));
    }

    [Theory]
    [InlineData(0, 0, -1, 1, "maxX")]
    [InlineData(0, 0, 1, -1, "maxY")]
    public void OverlapQueries_ShouldThrowArgumentException_WhenAQueryEdgePrecedesItsLowerEdge(
        double minX, double minY, double maxX, double maxY, string paramName)
    {
        var tree = new RTree<int>(Ladder(30));

        Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => tree.CountOverlapping(minX, minY, maxX, maxY)).ParamName);
        Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => tree.ContainsOverlapping(minX, minY, maxX, maxY)).ParamName);
        Assert.Equal(paramName, Assert.Throws<ArgumentException>(() => tree.GetOverlapping(minX, minY, maxX, maxY)).ParamName);
        Assert.Equal(paramName, Assert.Throws<ArgumentException>(
            () => tree.CopyOverlapping(minX, minY, maxX, maxY, new SpatialBox<int>[4])).ParamName);
    }

    // ---- point queries -----------------------------------------------------------------------------

    [Fact]
    public void CountAtPoint_ShouldCountEveryBoxCoveringThePoint_WhenExtentsNest()
    {
        SpatialBox<int>[] boxes =
        [
            new SpatialBox<int>(-100, -100, 100, 100, 0),
            new SpatialBox<int>(-10, -10, 10, 10, 1),
            new SpatialBox<int>(-1, -1, 1, 1, 2),
            new SpatialBox<int>(50, 50, 60, 60, 3),
        ];
        var tree = new RTree<int>(boxes);

        // The nested case an R-tree exists for: the centre is inside three boxes whose sizes differ by orders
        // of magnitude, which no single grid cell size indexes well.
        Assert.Equal(3, tree.CountAtPoint(0, 0));
        Assert.Equal([0, 1, 2], tree.GetAtPoint(0, 0).Select(b => b.Value).OrderBy(v => v));
        Assert.Equal(2, tree.CountAtPoint(55, 55));
        Assert.True(tree.ContainsAtPoint(55, 55));
    }

    [Fact]
    public void CountAtPoint_ShouldCountABoxWhosePointLiesOnItsEdge_BecauseEdgesAreClosed()
    {
        SpatialBox<int>[] boxes = [new SpatialBox<int>(0, 0, 4, 4, 0)];
        var tree = new RTree<int>(boxes);

        Assert.Equal(1, tree.CountAtPoint(0, 0));
        Assert.Equal(1, tree.CountAtPoint(4, 4));
        Assert.Equal(1, tree.CountAtPoint(0, 4));
        Assert.Equal(0, tree.CountAtPoint(4.000001, 4));
    }

    [Fact]
    public void CountAtPoint_ShouldReportZero_WhenTheTreeIsEmptyOrThePointIsNaN()
    {
        var empty = new RTree<int>([]);
        var tree = new RTree<int>(Grid(20));

        Assert.Equal(0, empty.CountAtPoint(0, 0));
        Assert.False(empty.ContainsAtPoint(0, 0));
        Assert.Empty(empty.GetAtPoint(0, 0));
        Assert.Equal(0, empty.CopyAtPoint(0, 0, new SpatialBox<int>[4]));

        Assert.Equal(0, tree.CountAtPoint(double.NaN, 0));
        Assert.Equal(0, tree.CountAtPoint(0, double.NaN));
        Assert.False(tree.ContainsAtPoint(double.NaN, double.NaN));
    }

    [Fact]
    public void ContainsAtPoint_ShouldStopAtTheFirstMatch_WhenManyBoxesCoverThePoint()
    {
        SpatialBox<int>[] boxes = [.. Enumerable.Range(0, 500).Select(i => new SpatialBox<int>(-i - 1, -i - 1, i + 1, i + 1, i))];
        var tree = new RTree<int>(boxes);

        Assert.True(tree.ContainsAtPoint(0, 0));
        Assert.Equal(500, tree.CountAtPoint(0, 0));
    }

    // ---- the allocation-free tier ------------------------------------------------------------------

    [Fact]
    public void CopyOverlapping_ShouldWriteFromTheGivenIndex_WhenTheBufferIsOffset()
    {
        var tree = new RTree<int>(Grid(20));
        var buffer = new SpatialBox<int>[12];

        int written = tree.CopyOverlapping(10, 10, 34, 34, buffer, 3);

        Assert.Equal(9, written);
        Assert.Equal(0, buffer[0].Value);
        Assert.Equal(0, buffer[1].Value);
        Assert.Equal(0, buffer[2].Value);
        Assert.Equal(9, buffer.Skip(3).Take(9).Select(b => b.Value).Distinct().Count());
    }

    [Fact]
    public void CopyOverlapping_ShouldTruncate_WhenTheBufferIsSmallerThanTheMatchCount()
    {
        var tree = new RTree<int>(Grid(20));
        var buffer = new SpatialBox<int>[4];

        int written = tree.CopyOverlapping(10, 10, 34, 34, buffer);

        Assert.Equal(4, written);
        Assert.Equal(4, buffer.Select(b => b.Value).Distinct().Count());
    }

    [Fact]
    public void CopyOverlapping_ShouldReportZero_WhenTheBufferHasNoRoomLeft()
    {
        var tree = new RTree<int>(Grid(20));
        var buffer = new SpatialBox<int>[4];

        Assert.Equal(0, tree.CopyOverlapping(10, 10, 34, 34, buffer, 4));
        Assert.Equal(0, tree.CopyAtPoint(10, 10, buffer, 4));
        Assert.Equal(0, tree.CopyOverlapping(10, 10, 34, 34, [], 0));
    }

    [Fact]
    public void CopyAtPoint_ShouldWriteEveryCoveringBox_WhenTheBufferIsLargeEnough()
    {
        SpatialBox<int>[] boxes =
        [
            new SpatialBox<int>(-100, -100, 100, 100, 0),
            new SpatialBox<int>(-10, -10, 10, 10, 1),
            new SpatialBox<int>(-1, -1, 1, 1, 2),
        ];
        var tree = new RTree<int>(boxes);
        var buffer = new SpatialBox<int>[8];

        int written = tree.CopyAtPoint(0.5, 0.5, buffer, 1);

        Assert.Equal(3, written);
        Assert.Equal([0, 1, 2], buffer.Skip(1).Take(3).Select(b => b.Value).OrderBy(v => v));
    }

    [Fact]
    public void CopyMembers_ShouldThrowArgumentNullException_WhenTheDestinationIsNull()
    {
        var tree = new RTree<int>(Ladder(30));

        Assert.Throws<ArgumentNullException>(() => tree.CopyOverlapping(0, 0, 1, 1, null!));
        Assert.Throws<ArgumentNullException>(() => tree.CopyAtPoint(0, 0, null!));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void CopyMembers_ShouldThrowArgumentOutOfRangeException_WhenTheIndexIsOutsideTheBuffer(int index)
    {
        var tree = new RTree<int>(Ladder(30));
        var buffer = new SpatialBox<int>[4];

        Assert.Throws<ArgumentOutOfRangeException>(() => tree.CopyOverlapping(0, 0, 1, 1, buffer, index));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.CopyAtPoint(0, 0, buffer, index));
    }

    // ---- the coordinate domain ---------------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldIndexBoxesNearTheTopOfTheDoubleRange_BecauseThereIsNoMagnitudeBound()
    {
        // Unlike KdTree, which bounds stored coordinates at 1e153 because it squares separations, this type
        // only ever compares — so it documents the whole finite range as usable, and this is what holds it to
        // that. The magnitudes are chosen so that min + max overflows: two coordinates near 1e308 sum past
        // double.MaxValue, which is the case the packing's centre calculation is written to survive.
        const double Base = 1.0e308;
        const double Step = 1.0e305;

        // Past the fanout, so the packing sorts on those centres rather than dropping everything in one leaf.
        var boxes = new SpatialBox<int>[100];
        for (int i = 0; i < boxes.Length; i++)
            boxes[i] = new SpatialBox<int>(Base + (i * Step), Base + (i * Step), Base + ((i + 1) * Step), Base + ((i + 1) * Step), i);

        var tree = new RTree<int>(boxes);

        Assert.Equal(100, tree.Count);
        Assert.Equal(Enumerable.Range(0, 100), tree.Select(b => b.Value).OrderBy(v => v));

        // Every box is still findable by a query for its own extent, and the extents survive the round trip.
        for (int i = 0; i < boxes.Length; i++)
        {
            SpatialBox<int> box = boxes[i];
            Assert.Contains(i, tree.GetOverlapping(box.MinX, box.MinY, box.MaxX, box.MaxY).Select(b => b.Value));
        }

        // A selective query at this magnitude: boxes 10 and 11 share the corner at Base + 11 * Step.
        Assert.Equal([10, 11], tree.GetAtPoint(Base + (11 * Step), Base + (11 * Step)).Select(b => b.Value).OrderBy(v => v));

        Assert.True(tree.TryGetBounds(out double minX, out double minY, out double maxX, out double maxY));
        Assert.Equal(Base, minX);
        Assert.Equal(Base, minY);
        Assert.Equal(Base + (100 * Step), maxX);
        Assert.Equal(Base + (100 * Step), maxY);
    }

    [Fact]
    public void Constructor_ShouldIndexBoxesSpanningTheWholeFiniteRange_WhenEdgesSitAtOppositeExtremes()
    {
        // The mirror case: edges at opposite ends of the range, where the difference rather than the sum is
        // what overflows. A box this wide overlaps every query inside it, which is the observable claim.
        SpatialBox<int>[] boxes =
        [
            new SpatialBox<int>(double.MinValue, double.MinValue, double.MaxValue, double.MaxValue, 0),
            new SpatialBox<int>(-1, -1, 1, 1, 1),
        ];

        var tree = new RTree<int>(boxes);

        Assert.Equal([0, 1], tree.GetAtPoint(0, 0).Select(b => b.Value).OrderBy(v => v));
        Assert.Equal([0], tree.GetAtPoint(1e300, -1e300).Select(b => b.Value));
        Assert.Equal(2, tree.CountOverlapping(-1, -1, 1, 1));
    }

    // ---- payloads ----------------------------------------------------------------------------------

    [Fact]
    public void Queries_ShouldCarryTheirPayloads_WhenTheValueIsAReferenceType()
    {
        SpatialBox<string>[] boxes =
        [
            new SpatialBox<string>(0, 0, 1, 1, "first"),
            new SpatialBox<string>(2, 2, 3, 3, null),
        ];
        var tree = new RTree<string>(boxes);

        Assert.Equal("first", tree.GetAtPoint(0.5, 0.5)[0].Value);
        Assert.Null(tree.GetAtPoint(2.5, 2.5)[0].Value);
    }
}
