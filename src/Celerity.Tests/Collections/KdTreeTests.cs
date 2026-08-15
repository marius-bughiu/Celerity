using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Dedicated coverage for <see cref="KdTree{TValue}"/>: construction and validation, the three query families
/// (nearest, radius, rectangle) across their allocation-free and convenience tiers, and the degenerate inputs
/// that a spatial index has to answer for — an empty tree, duplicate coordinates, collinear points, and a
/// <see cref="double.NaN"/> query.
/// </summary>
public class KdTreeTests
{
    // A 3x3 lattice at the integer coordinates 0, 10 and 20, which makes every expected distance checkable by
    // hand. The value carries the point's own name so a returned entry identifies itself.
    private static SpatialPoint<string>[] Lattice() =>
    [
        new(0, 0, "a"), new(10, 0, "b"), new(20, 0, "c"),
        new(0, 10, "d"), new(10, 10, "e"), new(20, 10, "f"),
        new(0, 20, "g"), new(10, 20, "h"), new(20, 20, "i"),
    ];

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenPointsAreNull()
    {
        Assert.Throws<ArgumentNullException>(() => new KdTree<string>(null!));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenAnXCoordinateIsNaN()
    {
        var points = new SpatialPoint<int>[] { new(1, 1, 1), new(double.NaN, 2, 2) };
        var exception = Assert.Throws<ArgumentException>(() => new KdTree<int>(points));
        Assert.Equal("points", exception.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenAYCoordinateIsNaN()
    {
        var points = new SpatialPoint<int>[] { new(1, 1, 1), new(2, double.NaN, 2) };
        Assert.Throws<ArgumentException>(() => new KdTree<int>(points));
    }

    [Fact]
    public void Constructor_ShouldBuildFromAnUncountedSequence_WhenTheSourceIsNotACollection()
    {
        // The ICollection<T> fast path sizes and copies once; this exercises the List<T> route beside it.
        IEnumerable<SpatialPoint<string>> uncounted = Lattice().Where(p => p.X <= 10);
        var tree = new KdTree<string>(uncounted);

        Assert.Equal(6, tree.Count);
        Assert.True(tree.TryFindNearest(10, 10, out SpatialPoint<string> nearest));
        Assert.Equal("e", nearest.Value);
    }

    [Theory]
    [InlineData(double.PositiveInfinity, 0)]
    [InlineData(double.NegativeInfinity, 0)]
    [InlineData(0, double.PositiveInfinity)]
    public void Constructor_ShouldThrowArgumentException_WhenACoordinateIsInfinite(double x, double y)
    {
        // An infinite coordinate measures NaN against itself (Infinity - Infinity), so a stored infinite point
        // could not be found even by a query for its own coordinates. Rejecting beats storing that.
        var points = new SpatialPoint<int>[] { new(1, 1, 1), new(x, y, 2) };

        Assert.Throws<ArgumentException>(() => new KdTree<int>(points));
    }

    [Theory]
    [InlineData(1e154, 0)]
    [InlineData(-1e154, 0)]
    [InlineData(0, 1e200)]
    public void Constructor_ShouldThrowArgumentException_WhenACoordinateIsTooLargeToSquare(double x, double y)
    {
        // Past ~1e153 a squared separation overflows to Infinity, at which point two far-apart points compare
        // equal and a radius that also overflows reports them as matches. The bound is what keeps every
        // comparison in this type meaningful, so it is enforced rather than documented and hoped for.
        var points = new SpatialPoint<int>[] { new(1, 1, 1), new(x, y, 2) };

        Assert.Throws<ArgumentException>(() => new KdTree<int>(points));
    }

    [Fact]
    public void Constructor_ShouldAcceptCoordinatesAtTheMagnitudeLimit_WhereSquaringStillHolds()
    {
        var tree = new KdTree<string>([new(-1e153, -1e153, "min"), new(1e153, 1e153, "max"), new(0, 0, "origin")]);

        Assert.Equal(3, tree.Count);
        Assert.True(tree.TryFindNearest(1e153, 1e153, out SpatialPoint<string> nearest));
        Assert.Equal("max", nearest.Value);

        // The whole point of the bound: the far corner is genuinely outside a large radius and is reported so,
        // rather than both distances collapsing to Infinity and comparing equal.
        Assert.Equal(1, tree.CountWithin(1e153, 1e153, 1e152));
        Assert.Equal(3, tree.CountWithin(0, 0, 1e154));
    }

    [Fact]
    public void Count_ShouldCountDuplicateCoordinatesSeparately_WhenPointsCoincide()
    {
        var tree = new KdTree<string>([new(5, 5, "a"), new(5, 5, "b"), new(5, 5, "c")]);

        Assert.Equal(3, tree.Count);
        Assert.Equal(3, tree.CountWithin(5, 5, 0));
    }

    [Fact]
    public void Indexer_ShouldThrowArgumentOutOfRangeException_WhenIndexIsOutsideTheTree()
    {
        var tree = new KdTree<string>(Lattice());

        Assert.Throws<ArgumentOutOfRangeException>(() => tree[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => tree[tree.Count]);
    }

    [Fact]
    public void Indexer_ShouldExposeEveryStoredPoint_WhenReadAcrossTheWholeRange()
    {
        SpatialPoint<string>[] expected = Lattice();
        var tree = new KdTree<string>(expected);

        // The layout order is deliberately unspecified, so the contract is the multiset, not the sequence.
        var seen = new List<(double, double, string?)>();
        for (int i = 0; i < tree.Count; i++)
            seen.Add((tree[i].X, tree[i].Y, tree[i].Value));

        Assert.Equal(
            expected.Select(p => (p.X, p.Y, p.Value)).OrderBy(t => t.Item1).ThenBy(t => t.Item2).ToArray(),
            seen.OrderBy(t => t.Item1).ThenBy(t => t.Item2).ToArray());
    }

    // ---- nearest ------------------------------------------------------------------------------------

    [Fact]
    public void TryFindNearest_ShouldReturnFalse_WhenTheTreeIsEmpty()
    {
        var tree = new KdTree<string>([]);

        Assert.False(tree.TryFindNearest(0, 0, out SpatialPoint<string> nearest));
        Assert.Equal(default, nearest.Value);
        Assert.Equal(0, tree.Count);
    }

    [Theory]
    [InlineData(1, 1, "a")]
    [InlineData(9, 1, "b")]
    [InlineData(19, 21, "i")]
    [InlineData(11, 9, "e")]
    [InlineData(-100, -100, "a")]
    public void TryFindNearest_ShouldReturnTheClosestPoint_WhenTheTreeIsPopulated(double x, double y, string expected)
    {
        var tree = new KdTree<string>(Lattice());

        Assert.True(tree.TryFindNearest(x, y, out SpatialPoint<string> nearest));
        Assert.Equal(expected, nearest.Value);
    }

    [Fact]
    public void TryFindNearest_ShouldReturnFalse_WhenAQueryCoordinateIsNaN()
    {
        var tree = new KdTree<string>(Lattice());

        Assert.False(tree.TryFindNearest(double.NaN, 0, out _));
        Assert.False(tree.TryFindNearest(0, double.NaN, out _));
    }

    [Fact]
    public void TryFindNearest_ShouldRespectTheDistanceBound_WhenOneIsGiven()
    {
        var tree = new KdTree<string>(Lattice());

        // (5, 5) is 7.07 from each of the four surrounding lattice points.
        Assert.False(tree.TryFindNearest(5, 5, 7, out _));
        Assert.True(tree.TryFindNearest(5, 5, 7.1, out SpatialPoint<string> nearest));
        Assert.Contains(nearest.Value, new[] { "a", "b", "d", "e" });
    }

    [Fact]
    public void TryFindNearest_ShouldIncludeAPointExactlyOnTheBound_BecauseTheBoundIsInclusive()
    {
        var tree = new KdTree<string>(Lattice());

        // Exactly 10 away, and the far side of the very first split, so this also pins that a strict prune
        // would discard the subtree holding the answer.
        Assert.True(tree.TryFindNearest(0, 10, 0, out SpatialPoint<string> exact));
        Assert.Equal("d", exact.Value);
        Assert.True(tree.TryFindNearest(-10, 0, 10, out SpatialPoint<string> onBound));
        Assert.Equal("a", onBound.Value);
    }

    [Fact]
    public void TryFindNearest_ShouldThrowArgumentOutOfRangeException_WhenTheBoundIsNegativeOrNaN()
    {
        var tree = new KdTree<string>(Lattice());

        Assert.Throws<ArgumentOutOfRangeException>(() => tree.TryFindNearest(0, 0, -1, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.TryFindNearest(0, 0, double.NaN, out _));
    }

    [Fact]
    public void GetNearest_ShouldReturnPointsInAscendingDistanceOrder_WhenSeveralAreRequested()
    {
        var tree = new KdTree<string>(Lattice());

        SpatialPoint<string>[] nearest = tree.GetNearest(0, 0, 3);

        Assert.Equal(3, nearest.Length);
        Assert.Equal("a", nearest[0].Value);
        Assert.Equal(new[] { "b", "d" }, nearest.Skip(1).Select(p => p.Value!).OrderBy(v => v).ToArray());
        AssertAscendingDistance(nearest, 0, 0);
    }

    [Fact]
    public void GetNearest_ShouldReturnEveryPoint_WhenMoreAreRequestedThanAreStored()
    {
        var tree = new KdTree<string>(Lattice());

        SpatialPoint<string>[] nearest = tree.GetNearest(10, 10, 100);

        Assert.Equal(9, nearest.Length);
        Assert.Equal("e", nearest[0].Value);
        AssertAscendingDistance(nearest, 10, 10);
    }

    [Fact]
    public void GetNearest_ShouldReturnEmpty_WhenNoneAreRequestedOrTheTreeIsEmpty()
    {
        var tree = new KdTree<string>(Lattice());

        Assert.Empty(tree.GetNearest(0, 0, 0));
        Assert.Empty(new KdTree<string>([]).GetNearest(0, 0, 5));
    }

    [Fact]
    public void GetNearest_ShouldReturnEmpty_WhenAQueryCoordinateIsNaN()
    {
        var tree = new KdTree<string>(Lattice());

        // The exactly-sized buffer comes back short, which is the one path where the result is trimmed.
        Assert.Empty(tree.GetNearest(double.NaN, 0, 4));
    }

    [Fact]
    public void GetNearest_ShouldThrowArgumentOutOfRangeException_WhenCountIsNegative()
    {
        var tree = new KdTree<string>(Lattice());

        Assert.Throws<ArgumentOutOfRangeException>(() => tree.GetNearest(0, 0, -1));
    }

    [Fact]
    public void CopyNearest_ShouldFillTheBufferWithTheClosestPoints_WhenRoomIsLimited()
    {
        var tree = new KdTree<string>(Lattice());
        var buffer = new SpatialPoint<string>[4];

        int written = tree.CopyNearest(20, 20, buffer, 1);

        Assert.Equal(3, written);
        Assert.Equal("i", buffer[1].Value);
        Assert.Equal(new[] { "f", "h" }, buffer.Skip(2).Take(2).Select(p => p.Value!).OrderBy(v => v).ToArray());
        Assert.Null(buffer[0].Value);
    }

    [Fact]
    public void CopyNearest_ShouldReturnZero_WhenThereIsNoRoomOrTheQueryIsNaN()
    {
        var tree = new KdTree<string>(Lattice());
        var buffer = new SpatialPoint<string>[2];

        Assert.Equal(0, tree.CopyNearest(0, 0, buffer, 2));
        Assert.Equal(0, tree.CopyNearest(0, 0, []));
        Assert.Equal(0, tree.CopyNearest(double.NaN, double.NaN, buffer));
    }

    [Fact]
    public void CopyNearest_ShouldThrow_WhenTheDestinationIsNullOrTheIndexIsOutOfRange()
    {
        var tree = new KdTree<string>(Lattice());

        Assert.Throws<ArgumentNullException>(() => tree.CopyNearest(0, 0, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.CopyNearest(0, 0, new SpatialPoint<string>[2], 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.CopyNearest(0, 0, new SpatialPoint<string>[2], -1));
    }

    [Fact]
    public void CopyNearest_ShouldOrderByDistance_WhenTheHeapHasToEvictRepeatedly()
    {
        // A line of 64 points queried from one end: every offer after the buffer fills evicts the current
        // worst, so this drives the max-heap's replace-and-sift-down path many times over.
        var points = new SpatialPoint<int>[64];
        for (int i = 0; i < points.Length; i++)
            points[i] = new SpatialPoint<int>(i, 0, i);

        var tree = new KdTree<int>(points);
        var buffer = new SpatialPoint<int>[5];

        int written = tree.CopyNearest(0, 0, buffer);

        Assert.Equal(5, written);
        Assert.Equal([0, 1, 2, 3, 4], buffer.Select(p => p.Value).ToArray());
    }

    // ---- radius -------------------------------------------------------------------------------------

    [Fact]
    public void ContainsWithin_ShouldReportWhetherAnyPointIsInRange_WhenQueried()
    {
        var tree = new KdTree<string>(Lattice());

        Assert.True(tree.ContainsWithin(11, 11, 2));
        Assert.False(tree.ContainsWithin(5, 5, 7));
        Assert.False(new KdTree<string>([]).ContainsWithin(0, 0, 1000));
    }

    [Fact]
    public void CountWithin_ShouldCountOnlyPointsInsideTheCircle_NotItsBoundingBox()
    {
        var tree = new KdTree<string>(Lattice());

        // The box of half-width 10 around (10, 10) holds all nine; the inscribed circle holds five, because
        // the four corners are 14.14 away. This is the assertion that pins the exact test against the prune.
        Assert.Equal(9, tree.CountInRectangle(0, 0, 20, 20));
        Assert.Equal(5, tree.CountWithin(10, 10, 10));
    }

    [Fact]
    public void CountWithin_ShouldIncludePointsExactlyOnTheRadius_BecauseTheRadiusIsInclusive()
    {
        var tree = new KdTree<string>(Lattice());

        Assert.Equal(1, tree.CountWithin(0, 0, 0));
        Assert.Equal(3, tree.CountWithin(0, 0, 10));
    }

    [Fact]
    public void CountWithin_ShouldThrowArgumentOutOfRangeException_WhenTheRadiusIsNegativeOrNaN()
    {
        var tree = new KdTree<string>(Lattice());

        Assert.Throws<ArgumentOutOfRangeException>(() => tree.CountWithin(0, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.CountWithin(0, 0, double.NaN));
    }

    [Fact]
    public void CountWithin_ShouldReturnZero_WhenAQueryCoordinateIsNaN()
    {
        var tree = new KdTree<string>(Lattice());

        Assert.Equal(0, tree.CountWithin(double.NaN, 0, 1000));
        Assert.Equal(0, tree.CountWithin(0, double.NaN, 1000));
    }

    [Fact]
    public void GetWithin_ShouldReturnEveryMatch_WhenTheCircleCoversPartOfTheTree()
    {
        var tree = new KdTree<string>(Lattice());

        SpatialPoint<string>[] matches = tree.GetWithin(0, 0, 10);

        Assert.Equal(new[] { "a", "b", "d" }, matches.Select(p => p.Value!).OrderBy(v => v).ToArray());
        Assert.Empty(tree.GetWithin(100, 100, 1));
    }

    [Fact]
    public void CopyWithin_ShouldTruncate_WhenTheBufferFillsBeforeTheMatchesRunOut()
    {
        var tree = new KdTree<string>(Lattice());
        var buffer = new SpatialPoint<string>[2];

        int written = tree.CopyWithin(10, 10, 100, buffer);

        Assert.Equal(2, written);
        Assert.All(buffer, p => Assert.NotNull(p.Value));
    }

    [Fact]
    public void CopyWithin_ShouldReturnZero_WhenTheBufferHasNoRoomLeft()
    {
        var tree = new KdTree<string>(Lattice());
        var buffer = new SpatialPoint<string>[1];

        Assert.Equal(0, tree.CopyWithin(0, 0, 100, buffer, 1));
    }

    [Fact]
    public void CopyWithin_ShouldThrow_WhenTheDestinationOrTheRadiusIsInvalid()
    {
        var tree = new KdTree<string>(Lattice());

        Assert.Throws<ArgumentNullException>(() => tree.CopyWithin(0, 0, 1, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.CopyWithin(0, 0, 1, new SpatialPoint<string>[2], 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.CopyWithin(0, 0, -1, new SpatialPoint<string>[2]));
    }

    // ---- rectangle ----------------------------------------------------------------------------------

    [Fact]
    public void ContainsInRectangle_ShouldReportWhetherAnyPointIsInsideTheBox_WhenQueried()
    {
        var tree = new KdTree<string>(Lattice());

        Assert.True(tree.ContainsInRectangle(9, 9, 11, 11));
        Assert.False(tree.ContainsInRectangle(11, 11, 19, 19));
    }

    [Fact]
    public void CountInRectangle_ShouldIncludePointsOnTheEdges_BecauseTheBoxIsClosed()
    {
        var tree = new KdTree<string>(Lattice());

        Assert.Equal(4, tree.CountInRectangle(0, 0, 10, 10));
        Assert.Equal(1, tree.CountInRectangle(10, 10, 10, 10));
        Assert.Equal(3, tree.CountInRectangle(0, 0, 20, 0));
    }

    [Fact]
    public void CountInRectangle_ShouldThrowArgumentException_WhenAnEdgeIsInverted()
    {
        var tree = new KdTree<string>(Lattice());

        var x = Assert.Throws<ArgumentException>(() => tree.CountInRectangle(10, 0, 0, 10));
        Assert.Equal("maxX", x.ParamName);
        var y = Assert.Throws<ArgumentException>(() => tree.CountInRectangle(0, 10, 10, 0));
        Assert.Equal("maxY", y.ParamName);
    }

    [Fact]
    public void GetInRectangle_ShouldReturnEveryMatch_WhenTheBoxCoversPartOfTheTree()
    {
        var tree = new KdTree<string>(Lattice());

        SpatialPoint<string>[] matches = tree.GetInRectangle(-5, -5, 15, 5);

        Assert.Equal(new[] { "a", "b" }, matches.Select(p => p.Value!).OrderBy(v => v).ToArray());
        Assert.Empty(tree.GetInRectangle(100, 100, 200, 200));
    }

    [Fact]
    public void CopyInRectangle_ShouldTruncate_WhenTheBufferFillsBeforeTheMatchesRunOut()
    {
        var tree = new KdTree<string>(Lattice());
        var buffer = new SpatialPoint<string>[2];

        Assert.Equal(2, tree.CopyInRectangle(-1, -1, 21, 21, buffer));
        Assert.Equal(0, tree.CopyInRectangle(-1, -1, 21, 21, buffer, 2));
    }

    [Fact]
    public void CopyInRectangle_ShouldThrow_WhenTheDestinationOrTheBoxIsInvalid()
    {
        var tree = new KdTree<string>(Lattice());

        Assert.Throws<ArgumentNullException>(() => tree.CopyInRectangle(0, 0, 1, 1, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.CopyInRectangle(0, 0, 1, 1, new SpatialPoint<string>[2], 3));
        Assert.Throws<ArgumentException>(() => tree.CopyInRectangle(0, 0, -1, 1, new SpatialPoint<string>[2]));
    }

    // ---- degenerate shapes --------------------------------------------------------------------------

    [Fact]
    public void Queries_ShouldStayCorrect_WhenEveryPointHasIdenticalCoordinates()
    {
        // The quickselect's duplicate handling is what is on trial: an all-equal key run must not drive the
        // partition into a one-element step, and the tree must still answer for all of them.
        var points = new SpatialPoint<int>[100];
        for (int i = 0; i < points.Length; i++)
            points[i] = new SpatialPoint<int>(7, 7, i);

        var tree = new KdTree<int>(points);

        Assert.Equal(100, tree.Count);
        Assert.Equal(100, tree.CountWithin(7, 7, 0));
        Assert.Equal(100, tree.CountInRectangle(7, 7, 7, 7));
        Assert.True(tree.TryFindNearest(7, 7, out SpatialPoint<int> nearest));
        Assert.Equal(7, nearest.X);
        Assert.Equal(100, tree.GetNearest(7, 7, 100).Length);
    }

    [Fact]
    public void Queries_ShouldStayCorrect_WhenEveryPointIsCollinear()
    {
        // Collinear points give one axis zero variance, so every split on that axis is degenerate — the shape
        // that a k-d tree is classically warned about.
        var points = new SpatialPoint<int>[50];
        for (int i = 0; i < points.Length; i++)
            points[i] = new SpatialPoint<int>(0, i, i);

        var tree = new KdTree<int>(points);

        Assert.True(tree.TryFindNearest(0, 25.4, out SpatialPoint<int> nearest));
        Assert.Equal(25, nearest.Value);
        Assert.Equal(5, tree.CountWithin(0, 10, 2));
        Assert.Equal([8, 9, 10, 11, 12], tree.GetWithin(0, 10, 2).Select(p => p.Value).OrderBy(v => v).ToArray());
    }

    [Fact]
    public void Constructor_ShouldStayFastAndCorrect_WhenTheInputIsAnOrganPipe()
    {
        // The shape that defeats a middle-element pivot: ascending values interleaved with descending ones, so
        // an extreme lands at the midpoint of every subrange and each quickselect pass peels off one element.
        // Simulated at 4,096 points, that is 2,048 passes against a depth budget of 24 — a single selection
        // gone quadratic. It is also not an exotic input: it is any path that goes out and comes back.
        //
        // This exercises the introselect fallback. The assertion is correctness rather than a timing bound,
        // which no test should assert; the guard against the quadratic blow-up is that a quadratic build of
        // this size would not finish inside the suite's patience.
        const int count = 4096;
        var points = new SpatialPoint<int>[count];
        for (int i = 0; i < count; i++)
        {
            double x = i % 2 == 0 ? i : count - i;
            points[i] = new SpatialPoint<int>(x, x, i);
        }

        var tree = new KdTree<int>(points);

        Assert.Equal(count, tree.Count);

        // The layout must still be a correct k-d tree, so reconcile a sample of queries against brute force.
        foreach (int probe in new[] { 0, 1, 37, 512, 2049, 4095 })
        {
            double qx = probe;
            double qy = probe;

            double best = points.Min(p => ((p.X - qx) * (p.X - qx)) + ((p.Y - qy) * (p.Y - qy)));
            Assert.True(tree.TryFindNearest(qx, qy, out SpatialPoint<int> nearest));
            Assert.Equal(best, ((nearest.X - qx) * (nearest.X - qx)) + ((nearest.Y - qy) * (nearest.Y - qy)));

            int expected = points.Count(p => Math.Abs(p.X - qx) <= 50 && Math.Abs(p.Y - qy) <= 50);
            Assert.Equal(expected, tree.CountInRectangle(qx - 50, qy - 50, qx + 50, qy + 50));
        }
    }

    [Fact]
    public void Queries_ShouldStayCorrect_WhenTheTreeHoldsASinglePoint()
    {
        var tree = new KdTree<string>([new(3, 4, "only")]);

        Assert.True(tree.TryFindNearest(0, 0, out SpatialPoint<string> nearest));
        Assert.Equal("only", nearest.Value);
        Assert.Equal(1, tree.CountWithin(0, 0, 5));
        Assert.Equal(0, tree.CountWithin(0, 0, 4.9));
        Assert.True(tree.ContainsInRectangle(3, 4, 3, 4));
        Assert.Single(tree.GetNearest(0, 0, 3));
    }

    // ---- the element type ---------------------------------------------------------------------------

    [Fact]
    public void Deconstruct_ShouldYieldTheCoordinatesAndValue_WhenAPointIsDestructured()
    {
        var tree = new KdTree<string>(Lattice());

        Assert.True(tree.TryFindNearest(21, 19, out SpatialPoint<string> nearest));
        (double x, double y, string? value) = nearest;

        Assert.Equal(20, x);
        Assert.Equal(20, y);
        Assert.Equal("i", value);
    }

    [Fact]
    public void ToString_ShouldRenderTheCoordinatesAndValue_WhenAPointIsFormatted()
    {
        var point = new SpatialPoint<string>(1.5, -2, "here");

        Assert.Equal($"({1.5}, {-2.0}) = here", point.ToString());
    }

    private static void AssertAscendingDistance<TValue>(SpatialPoint<TValue>[] points, double x, double y)
    {
        for (int i = 1; i < points.Length; i++)
        {
            double previous = Distance(points[i - 1], x, y);
            double current = Distance(points[i], x, y);
            Assert.True(previous <= current, $"Result {i} at distance {current} follows one at {previous}.");
        }
    }

    private static double Distance<TValue>(SpatialPoint<TValue> point, double x, double y)
    {
        double dx = point.X - x;
        double dy = point.Y - y;
        return (dx * dx) + (dy * dy);
    }
}
