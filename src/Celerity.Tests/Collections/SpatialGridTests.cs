using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Core coverage for <see cref="SpatialGrid{TValue}"/>: the constructor's world/cell validation, the
/// handle-addressed mutations that are the whole reason the type exists, and the three query families.
///
/// <para>
/// The cases worth naming here are the ones a uniform grid gets wrong if the cell arithmetic is off by one:
/// a point exactly on a cell boundary, a point outside the declared world (clamped rather than rejected, which
/// only stays correct because a query's own cell range clamps the same way), and a query whose radius spans
/// many cells or none.
/// </para>
/// </summary>
public class SpatialGridTests
{
    // A 10x10 grid of unit cells over [0, 10] x [0, 10], which is small enough to reason about cell by cell.
    private static SpatialGrid<int> Grid(int capacity = 0) => new(0, 0, 10, 10, 1, capacity);

    // ---- construction -------------------------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldExposeTheWorldAndItsCellCounts_WhenGivenAWholeNumberOfCells()
    {
        var grid = new SpatialGrid<int>(-5, -2.5, 5, 7.5, 2.5);

        Assert.Equal(0, grid.Count);
        Assert.Equal(-5, grid.MinX);
        Assert.Equal(-2.5, grid.MinY);
        Assert.Equal(5, grid.MaxX);
        Assert.Equal(7.5, grid.MaxY);
        Assert.Equal(2.5, grid.CellSize);
        Assert.Equal(4, grid.Columns);
        Assert.Equal(4, grid.Rows);
    }

    [Fact]
    public void Constructor_ShouldRoundTheCellCountUp_WhenTheWorldIsNotAWholeNumberOfCells()
    {
        var grid = new SpatialGrid<int>(0, 0, 10.5, 3.1, 4);

        Assert.Equal(3, grid.Columns);
        Assert.Equal(1, grid.Rows);
    }

    [Fact]
    public void Constructor_ShouldStillGiveOneCell_WhenTheWorldIsDegenerate()
    {
        var grid = new SpatialGrid<int>(3, 3, 3, 3, 1);

        Assert.Equal(1, grid.Columns);
        Assert.Equal(1, grid.Rows);

        grid.Add(3, 3, 7);
        Assert.Equal(1, grid.CountWithin(3, 3, 0));
    }

    [Theory]
    [InlineData(double.NaN, 0, 1, 1, "minX")]
    [InlineData(0, double.NaN, 1, 1, "minY")]
    [InlineData(0, 0, double.NaN, 1, "maxX")]
    [InlineData(0, 0, 1, double.NaN, "maxY")]
    [InlineData(double.NegativeInfinity, 0, 1, 1, "minX")]
    [InlineData(0, 0, 1e200, 1, "maxX")]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenAnEdgeIsOutsideTheStorableDomain(
        double minX, double minY, double maxX, double maxY, string paramName)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialGrid<int>(minX, minY, maxX, maxY, 1));
        Assert.Equal(paramName, ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenTheRightEdgePrecedesTheLeft()
    {
        var ex = Assert.Throws<ArgumentException>(() => new SpatialGrid<int>(0, 0, -1, 10, 1));
        Assert.Equal("maxX", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenTheTopEdgePrecedesTheBottom()
    {
        var ex = Assert.Throws<ArgumentException>(() => new SpatialGrid<int>(0, 0, 10, -1, 1));
        Assert.Equal("maxY", ex.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenTheCellSizeIsNotPositiveAndFinite(double cellSize)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialGrid<int>(0, 0, 10, 10, cellSize));
        Assert.Equal("cellSize", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenTheCapacityIsNegative()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialGrid<int>(0, 0, 10, 10, 1, -1));
        Assert.Equal("capacity", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenOneAxisAloneNeedsTooManyCells()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialGrid<int>(0, 0, 1e10, 10, 1));
        Assert.Equal("cellSize", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenTheTwoAxesTogetherNeedTooManyCells()
    {
        // Each axis on its own is a legal cell count; it is the product that no array can hold, which is the
        // case an int multiply would wrap and turn into a negative length.
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialGrid<int>(0, 0, 100_000, 100_000, 1));
        Assert.Equal("cellSize", ex.ParamName);
    }

    // ---- add, move, remove --------------------------------------------------------------------------

    [Fact]
    public void Add_ShouldReturnAHandleThatReadsBackThePoint_WhenTheEntryIsLive()
    {
        SpatialGrid<int> grid = Grid();

        SpatialGridHandle handle = grid.Add(1.5, 2.5, 42);

        Assert.Equal(1, grid.Count);
        Assert.True(grid.TryGetPoint(handle, out SpatialPoint<int> point));
        Assert.Equal(1.5, point.X);
        Assert.Equal(2.5, point.Y);
        Assert.Equal(42, point.Value);
    }

    [Fact]
    public void Add_ShouldKeepDuplicateCoordinatesDistinct_WhenTwoPointsCoincide()
    {
        SpatialGrid<int> grid = Grid();

        SpatialGridHandle first = grid.Add(4, 4, 1);
        SpatialGridHandle second = grid.Add(4, 4, 2);

        Assert.NotEqual(first, second);
        Assert.Equal(2, grid.Count);
        Assert.Equal(2, grid.CountWithin(4, 4, 0));
    }

    [Theory]
    [InlineData(double.NaN, 0, "x")]
    [InlineData(0, double.NaN, "y")]
    [InlineData(double.PositiveInfinity, 0, "x")]
    [InlineData(0, 1e200, "y")]
    public void Add_ShouldThrowArgumentOutOfRange_WhenACoordinateIsOutsideTheStorableDomain(double x, double y, string paramName)
    {
        SpatialGrid<int> grid = Grid();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => grid.Add(x, y, 1));
        Assert.Equal(paramName, ex.ParamName);
    }

    [Fact]
    public void Add_ShouldClampIntoTheEdgeCell_WhenThePointIsOutsideTheDeclaredWorld()
    {
        SpatialGrid<int> grid = Grid();

        grid.Add(-500, -500, 1);
        grid.Add(500, 500, 2);

        // Clamping stores the point but never fakes its position: the coordinates are its own, so a query
        // that does not reach the point does not match it, however close their cells are.
        Assert.Equal(0, grid.CountWithin(0, 0, 5));
        Assert.Equal(1, grid.CountWithin(-500, -500, 1));
        Assert.Equal(2, grid.Count);
    }

    [Fact]
    public void Add_ShouldGrowTheEntryStorage_WhenMoreEntriesArriveThanTheInitialCapacity()
    {
        SpatialGrid<int> grid = Grid();

        for (int i = 0; i < 100; i++)
            grid.Add(i % 10, i / 10, i);

        Assert.Equal(100, grid.Count);
        Assert.Equal(100, grid.CountInRectangle(0, 0, 10, 10));
    }

    [Fact]
    public void Add_ShouldNotGrow_WhenTheCapacityWasReservedUpFront()
    {
        SpatialGrid<int> grid = Grid(capacity: 8);

        for (int i = 0; i < 8; i++)
            grid.Add(i, i, i);

        Assert.Equal(8, grid.Count);
    }

    [Fact]
    public void Move_ShouldRelocateTheEntry_WhenItCrossesACellBoundary()
    {
        SpatialGrid<int> grid = Grid();
        SpatialGridHandle handle = grid.Add(0.5, 0.5, 7);

        grid.Move(handle, 8.5, 9.5);

        Assert.Equal(0, grid.CountWithin(0.5, 0.5, 0.25));
        Assert.Equal(1, grid.CountWithin(8.5, 9.5, 0.25));
        Assert.True(grid.TryGetPoint(handle, out SpatialPoint<int> point));
        Assert.Equal(8.5, point.X);
        Assert.Equal(9.5, point.Y);
    }

    [Fact]
    public void Move_ShouldUpdateTheCoordinates_WhenTheEntryStaysInTheSameCell()
    {
        SpatialGrid<int> grid = Grid();
        SpatialGridHandle handle = grid.Add(3.1, 3.1, 7);

        grid.Move(handle, 3.9, 3.9);

        Assert.True(grid.TryGetPoint(handle, out SpatialPoint<int> point));
        Assert.Equal(3.9, point.X);
        Assert.Equal(3.9, point.Y);
        Assert.Equal(1, grid.CountWithin(3.9, 3.9, 0));
        Assert.Equal(0, grid.CountWithin(3.1, 3.1, 0));
    }

    [Fact]
    public void Move_ShouldLeaveTheOtherEntriesOfTheCellIntact_WhenOneOfThemLeaves()
    {
        SpatialGrid<int> grid = Grid();
        grid.Add(1.1, 1.1, 1);
        SpatialGridHandle middle = grid.Add(1.2, 1.2, 2);
        grid.Add(1.3, 1.3, 3);

        grid.Move(middle, 7.5, 7.5);

        Assert.Equal(2, grid.CountWithin(1.2, 1.2, 0.5));
        Assert.Equal(1, grid.CountWithin(7.5, 7.5, 0.5));
    }

    [Theory]
    [InlineData(double.NaN, 0, "x")]
    [InlineData(0, double.NaN, "y")]
    public void Move_ShouldThrowArgumentOutOfRange_WhenACoordinateIsOutsideTheStorableDomain(double x, double y, string paramName)
    {
        SpatialGrid<int> grid = Grid();
        SpatialGridHandle handle = grid.Add(1, 1, 1);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => grid.Move(handle, x, y));
        Assert.Equal(paramName, ex.ParamName);
    }

    [Fact]
    public void Remove_ShouldRetireTheHandle_WhenTheEntryIsGone()
    {
        SpatialGrid<int> grid = Grid();
        SpatialGridHandle handle = grid.Add(2, 2, 5);

        grid.Remove(handle);

        Assert.Equal(0, grid.Count);
        Assert.False(grid.TryGetPoint(handle, out _));
        Assert.Throws<ArgumentException>(() => grid.Remove(handle));
        Assert.Throws<ArgumentException>(() => grid.Move(handle, 3, 3));
    }

    [Fact]
    public void Remove_ShouldUnlinkFromAnyPositionInItsCell_WhenTheCellHoldsSeveralEntries()
    {
        SpatialGrid<int> grid = Grid();
        SpatialGridHandle first = grid.Add(5.1, 5.1, 1);
        SpatialGridHandle middle = grid.Add(5.2, 5.2, 2);
        SpatialGridHandle last = grid.Add(5.3, 5.3, 3);

        grid.Remove(middle);
        Assert.Equal(2, grid.CountWithin(5.2, 5.2, 1));

        grid.Remove(last);
        Assert.Equal(1, grid.CountWithin(5.2, 5.2, 1));

        grid.Remove(first);
        Assert.Equal(0, grid.CountWithin(5.2, 5.2, 1));
        Assert.Equal(0, grid.Count);
    }

    [Fact]
    public void Add_ShouldReuseAVacatedSlotWithAFreshHandle_WhenAnEntryWasRemoved()
    {
        SpatialGrid<int> grid = Grid();
        SpatialGridHandle stale = grid.Add(1, 1, 1);
        grid.Remove(stale);

        SpatialGridHandle fresh = grid.Add(2, 2, 2);

        Assert.NotEqual(stale, fresh);
        Assert.False(grid.TryGetPoint(stale, out _));
        Assert.True(grid.TryGetPoint(fresh, out SpatialPoint<int> point));
        Assert.Equal(2, point.Value);
    }

    [Fact]
    public void TryGetPoint_ShouldReturnFalse_WhenTheHandleIsTheDefault()
    {
        SpatialGrid<int> grid = Grid();
        grid.Add(1, 1, 1);

        Assert.False(grid.TryGetPoint(default, out SpatialPoint<int> point));
        Assert.Equal(0, point.Value);
    }

    [Fact]
    public void TryGetPoint_ShouldReturnFalse_WhenTheHandleIsBeyondEveryAllocatedSlot()
    {
        SpatialGrid<int> grid = Grid();
        SpatialGridHandle handle = grid.Add(1, 1, 1);

        var other = new SpatialGrid<int>(0, 0, 10, 10, 1);
        other.Add(1, 1, 1);
        other.Add(2, 2, 2);
        SpatialGridHandle beyond = other.Add(3, 3, 3);

        Assert.True(grid.TryGetPoint(handle, out _));
        Assert.False(grid.TryGetPoint(beyond, out _));
    }

    // ---- clear --------------------------------------------------------------------------------------

    [Fact]
    public void Clear_ShouldEmptyTheGridAndRetireEveryHandle_WhenItHeldEntries()
    {
        SpatialGrid<int> grid = Grid();
        SpatialGridHandle first = grid.Add(1, 1, 1);
        SpatialGridHandle second = grid.Add(9, 9, 2);

        grid.Clear();

        Assert.Equal(0, grid.Count);
        Assert.False(grid.TryGetPoint(first, out _));
        Assert.False(grid.TryGetPoint(second, out _));
        Assert.Equal(0, grid.CountInRectangle(0, 0, 10, 10));
    }

    [Fact]
    public void Clear_ShouldReuseTheStorageWithHandlesThatDoNotCollide_WhenTheGridIsRefilled()
    {
        SpatialGrid<int> grid = Grid();
        SpatialGridHandle before = grid.Add(1, 1, 1);
        grid.Clear();

        SpatialGridHandle after = grid.Add(1, 1, 99);

        Assert.NotEqual(before, after);
        Assert.False(grid.TryGetPoint(before, out _));
        Assert.True(grid.TryGetPoint(after, out SpatialPoint<int> point));
        Assert.Equal(99, point.Value);
    }

    [Fact]
    public void Clear_ShouldReleaseThePayloads_WhenTheValueTypeIsAReferenceType()
    {
        var grid = new SpatialGrid<string>(0, 0, 10, 10, 1);
        SpatialGridHandle handle = grid.Add(1, 1, "kept");

        grid.Clear();

        Assert.Equal(0, grid.Count);
        Assert.False(grid.TryGetPoint(handle, out _));
    }

    [Fact]
    public void Remove_ShouldReleaseThePayload_WhenTheValueTypeIsAReferenceType()
    {
        var grid = new SpatialGrid<string>(0, 0, 10, 10, 1);
        SpatialGridHandle handle = grid.Add(1, 1, "kept");

        grid.Remove(handle);

        Assert.Equal(0, grid.Count);
        Assert.False(grid.TryGetPoint(handle, out _));
    }

    // ---- radius queries -----------------------------------------------------------------------------

    [Fact]
    public void CountWithin_ShouldIncludeAPointExactlyOnTheRadius_BecauseTheBoundIsInclusive()
    {
        SpatialGrid<int> grid = Grid();
        grid.Add(3, 0, 1);

        Assert.Equal(1, grid.CountWithin(0, 0, 3));
        Assert.Equal(0, grid.CountWithin(0, 0, 2.99));
    }

    [Fact]
    public void CountWithin_ShouldSpanEveryCellTheCircleTouches_WhenTheRadiusIsWiderThanACell()
    {
        SpatialGrid<int> grid = Grid();
        for (int x = 0; x < 10; x++)
            for (int y = 0; y < 10; y++)
                grid.Add(x + 0.5, y + 0.5, (x * 10) + y);

        Assert.Equal(100, grid.CountWithin(5, 5, 100));
        Assert.Equal(4, grid.CountWithin(5, 5, 0.75));
    }

    [Fact]
    public void ContainsWithin_ShouldStopAtTheFirstMatch_WhenAnyPointQualifies()
    {
        SpatialGrid<int> grid = Grid();
        grid.Add(1, 1, 1);
        grid.Add(1.1, 1.1, 2);

        Assert.True(grid.ContainsWithin(1, 1, 1));
        Assert.False(grid.ContainsWithin(8, 8, 1));
    }

    [Fact]
    public void RadiusQueries_ShouldReportNothing_WhenTheGridIsEmpty()
    {
        SpatialGrid<int> grid = Grid();

        Assert.False(grid.ContainsWithin(1, 1, 5));
        Assert.Equal(0, grid.CountWithin(1, 1, 5));
        Assert.Empty(grid.GetWithin(1, 1, 5));
    }

    [Theory]
    [InlineData(double.NaN, 1)]
    [InlineData(1, double.NaN)]
    public void RadiusQueries_ShouldReportNothing_WhenAQueryCoordinateIsNaN(double x, double y)
    {
        SpatialGrid<int> grid = Grid();
        grid.Add(1, 1, 1);

        Assert.False(grid.ContainsWithin(x, y, 5));
        Assert.Equal(0, grid.CountWithin(x, y, 5));
        Assert.Empty(grid.GetWithin(x, y, 5));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void RadiusQueries_ShouldThrowArgumentOutOfRange_WhenTheRadiusIsNotUsable(double radius)
    {
        SpatialGrid<int> grid = Grid();

        Assert.Throws<ArgumentOutOfRangeException>(() => grid.ContainsWithin(1, 1, radius));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.CountWithin(1, 1, radius));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.GetWithin(1, 1, radius));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.CopyWithin(1, 1, radius, new SpatialPoint<int>[4]));
    }

    [Fact]
    public void GetWithin_ShouldReturnEveryMatch_WhenSeveralPointsQualify()
    {
        SpatialGrid<int> grid = Grid();
        grid.Add(1, 1, 1);
        grid.Add(1.5, 1.5, 2);
        grid.Add(9, 9, 3);

        int[] values = grid.GetWithin(1, 1, 1).Select(p => p.Value).OrderBy(v => v).ToArray();

        Assert.Equal([1, 2], values);
    }

    [Fact]
    public void CopyWithin_ShouldStopAtTheEndOfTheBuffer_WhenThereAreMoreMatchesThanRoom()
    {
        SpatialGrid<int> grid = Grid();
        for (int i = 0; i < 5; i++)
            grid.Add(2 + (i * 0.01), 2, i);

        var buffer = new SpatialPoint<int>[2];
        Assert.Equal(2, grid.CopyWithin(2, 2, 1, buffer));
    }

    [Fact]
    public void CopyWithin_ShouldWriteFromTheGivenOffset_WhenOneIsSupplied()
    {
        SpatialGrid<int> grid = Grid();
        grid.Add(2, 2, 11);

        var buffer = new SpatialPoint<int>[3];
        Assert.Equal(1, grid.CopyWithin(2, 2, 1, buffer, 2));
        Assert.Equal(11, buffer[2].Value);
        Assert.Equal(0, buffer[0].Value);
    }

    [Fact]
    public void CopyWithin_ShouldWriteNothing_WhenTheBufferHasNoRoomLeft()
    {
        SpatialGrid<int> grid = Grid();
        grid.Add(2, 2, 11);

        var buffer = new SpatialPoint<int>[2];
        Assert.Equal(0, grid.CopyWithin(2, 2, 1, buffer, 2));
        Assert.Equal(0, grid.CopyInRectangle(0, 0, 10, 10, buffer, 2));
    }

    [Fact]
    public void CopyWithin_ShouldValidateItsDestination_WhenItIsNullOrTheOffsetIsOutOfRange()
    {
        SpatialGrid<int> grid = Grid();

        Assert.Throws<ArgumentNullException>(() => grid.CopyWithin(1, 1, 1, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.CopyWithin(1, 1, 1, new SpatialPoint<int>[2], 3));
        Assert.Throws<ArgumentNullException>(() => grid.CopyInRectangle(0, 0, 1, 1, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.CopyInRectangle(0, 0, 1, 1, new SpatialPoint<int>[2], 3));
    }

    // ---- rectangle queries --------------------------------------------------------------------------

    [Fact]
    public void RectangleQueries_ShouldTreatTheBoxAsClosed_WhenAPointSitsOnAnEdge()
    {
        SpatialGrid<int> grid = Grid();
        grid.Add(2, 3, 1);

        Assert.True(grid.ContainsInRectangle(2, 3, 2, 3));
        Assert.Equal(1, grid.CountInRectangle(0, 0, 2, 3));
        Assert.Equal(0, grid.CountInRectangle(0, 0, 1.99, 3));
    }

    [Fact]
    public void GetInRectangle_ShouldReturnEveryMatch_WhenTheBoxSpansSeveralCells()
    {
        SpatialGrid<int> grid = Grid();
        for (int i = 0; i < 10; i++)
            grid.Add(i + 0.5, 0.5, i);

        int[] values = grid.GetInRectangle(2, 0, 5, 1).Select(p => p.Value).OrderBy(v => v).ToArray();

        Assert.Equal([2, 3, 4], values);
        Assert.Empty(grid.GetInRectangle(0, 5, 10, 6));
    }

    [Theory]
    [InlineData(double.NaN, 0, 1, 1)]
    [InlineData(0, double.NaN, 1, 1)]
    [InlineData(0, 0, double.NaN, 1)]
    [InlineData(0, 0, 1, double.NaN)]
    public void RectangleQueries_ShouldReportNothing_WhenAnEdgeIsNaN(double minX, double minY, double maxX, double maxY)
    {
        SpatialGrid<int> grid = Grid();
        grid.Add(0.5, 0.5, 1);

        Assert.False(grid.ContainsInRectangle(minX, minY, maxX, maxY));
        Assert.Equal(0, grid.CountInRectangle(minX, minY, maxX, maxY));
        Assert.Empty(grid.GetInRectangle(minX, minY, maxX, maxY));
    }

    [Fact]
    public void RectangleQueries_ShouldThrowArgumentException_WhenAnUpperEdgePrecedesItsLower()
    {
        SpatialGrid<int> grid = Grid();

        Assert.Equal("maxX", Assert.Throws<ArgumentException>(() => grid.CountInRectangle(5, 0, 1, 10)).ParamName);
        Assert.Equal("maxY", Assert.Throws<ArgumentException>(() => grid.CountInRectangle(0, 5, 10, 1)).ParamName);
        Assert.Throws<ArgumentException>(() => grid.ContainsInRectangle(5, 0, 1, 10));
        Assert.Throws<ArgumentException>(() => grid.GetInRectangle(5, 0, 1, 10));
        Assert.Throws<ArgumentException>(() => grid.CopyInRectangle(5, 0, 1, 10, new SpatialPoint<int>[2]));
    }

    [Fact]
    public void CopyInRectangle_ShouldStopAtTheEndOfTheBuffer_WhenThereAreMoreMatchesThanRoom()
    {
        SpatialGrid<int> grid = Grid();
        for (int i = 0; i < 6; i++)
            grid.Add(i + 0.5, 0.5, i);

        var buffer = new SpatialPoint<int>[3];
        Assert.Equal(3, grid.CopyInRectangle(0, 0, 10, 10, buffer));
    }

    // ---- nearest ------------------------------------------------------------------------------------

    [Fact]
    public void TryFindNearest_ShouldReturnTheClosestPoint_WhenItIsSeveralRingsAway()
    {
        SpatialGrid<int> grid = Grid();
        grid.Add(9.5, 9.5, 1);
        grid.Add(0.5, 0.5, 2);

        Assert.True(grid.TryFindNearest(0.6, 0.6, out SpatialPoint<int> nearest));
        Assert.Equal(2, nearest.Value);

        Assert.True(grid.TryFindNearest(6, 6, out SpatialPoint<int> far));
        Assert.Equal(1, far.Value);
    }

    [Fact]
    public void TryFindNearest_ShouldKeepExpanding_WhenAFurtherRingHoldsANearerPoint()
    {
        // The trap a ring search falls into. The query sits in the far corner of its own cell, so the point
        // sharing that cell is 1.386 away while a point two rings out is only 1.132 — finding *a* point is
        // not finding the *nearest* one, and the search may only stop once a ring's own distance floor rules
        // the rest out.
        SpatialGrid<int> grid = Grid();
        grid.Add(3.01, 5.01, 1);
        grid.Add(5.01, 5.50, 2);

        Assert.True(grid.TryFindNearest(3.99, 5.99, out SpatialPoint<int> nearest));
        Assert.Equal(2, nearest.Value);
    }

    [Fact]
    public void TryFindNearest_ShouldReturnFalse_WhenTheGridIsEmpty()
    {
        SpatialGrid<int> grid = Grid();

        Assert.False(grid.TryFindNearest(1, 1, out SpatialPoint<int> nearest));
        Assert.Equal(0, nearest.Value);
    }

    [Theory]
    [InlineData(double.NaN, 1)]
    [InlineData(1, double.NaN)]
    public void TryFindNearest_ShouldReturnFalse_WhenAQueryCoordinateIsNaN(double x, double y)
    {
        SpatialGrid<int> grid = Grid();
        grid.Add(1, 1, 1);

        Assert.False(grid.TryFindNearest(x, y, out _));
    }

    [Fact]
    public void TryFindNearest_ShouldRejectEverythingBeyondTheBound_WhenAMaxDistanceIsGiven()
    {
        SpatialGrid<int> grid = Grid();
        grid.Add(9.5, 9.5, 1);

        Assert.False(grid.TryFindNearest(0.5, 0.5, 2, out _));
        Assert.True(grid.TryFindNearest(9.4, 9.4, 1, out SpatialPoint<int> nearest));
        Assert.Equal(1, nearest.Value);
    }

    [Fact]
    public void TryFindNearest_ShouldStillMatchAPointExactlyOnTheBound_BecauseTheBoundIsInclusive()
    {
        SpatialGrid<int> grid = Grid();
        grid.Add(3, 0, 1);

        Assert.True(grid.TryFindNearest(0, 0, 3, out SpatialPoint<int> nearest));
        Assert.Equal(1, nearest.Value);
    }

    [Fact]
    public void TryFindNearest_ShouldFindACoincidentPoint_WhenTheBoundIsZero()
    {
        SpatialGrid<int> grid = Grid();
        grid.Add(4, 4, 1);

        Assert.True(grid.TryFindNearest(4, 4, 0, out SpatialPoint<int> nearest));
        Assert.Equal(1, nearest.Value);
        Assert.False(grid.TryFindNearest(4.001, 4, 0, out _));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void TryFindNearest_ShouldThrowArgumentOutOfRange_WhenTheBoundIsNotUsable(double maxDistance)
    {
        SpatialGrid<int> grid = Grid();

        Assert.Throws<ArgumentOutOfRangeException>(() => grid.TryFindNearest(1, 1, maxDistance, out _));
    }

    [Fact]
    public void TryFindNearest_ShouldSearchTheWholeWorld_WhenTheQuerySitsOutsideItAndTheGridIsSparse()
    {
        SpatialGrid<int> grid = Grid();
        grid.Add(0.5, 0.5, 1);

        Assert.True(grid.TryFindNearest(-100, -100, out SpatialPoint<int> nearest));
        Assert.Equal(1, nearest.Value);

        Assert.True(grid.TryFindNearest(100, 100, out SpatialPoint<int> other));
        Assert.Equal(1, other.Value);
    }

    [Fact]
    public void TryFindNearest_ShouldWorkOnASingleRowGrid_WhenTheWorldIsOneCellTall()
    {
        // A one-row world makes the ring's bottom row and its column pair degenerate, which is where an
        // off-by-one in the annulus walk would show up as a missed point rather than an exception.
        var grid = new SpatialGrid<int>(0, 0, 10, 0.5, 1);
        grid.Add(9.5, 0.25, 1);

        Assert.Equal(1, grid.Rows);
        Assert.True(grid.TryFindNearest(0.25, 0.25, out SpatialPoint<int> nearest));
        Assert.Equal(1, nearest.Value);
    }

    // ---- handles ------------------------------------------------------------------------------------

    [Fact]
    public void SpatialGridHandle_ShouldCompareByBothSlotAndVersion_WhenEqualityIsAsked()
    {
        SpatialGrid<int> grid = Grid();
        SpatialGridHandle first = grid.Add(1, 1, 1);
        SpatialGridHandle second = grid.Add(2, 2, 2);
        grid.Remove(first);
        SpatialGridHandle reused = grid.Add(3, 3, 3);

        SpatialGridHandle copy = first;
        Assert.True(copy == first);
        Assert.True(first != second);
        Assert.True(first != reused);
        Assert.False(first.Equals(reused));
        Assert.True(first.Equals((object)first));
        Assert.False(first.Equals("not a handle"));
        Assert.Equal(first.GetHashCode(), first.GetHashCode());
        Assert.NotEqual(default, first);
    }

    [Fact]
    public void SpatialGridHandle_ShouldRenderItsSlotAndVersion_WhenConvertedToAString()
    {
        SpatialGrid<int> grid = Grid();
        SpatialGridHandle handle = grid.Add(1, 1, 1);

        Assert.Equal("#0.1", handle.ToString());
    }
}
