using System.Linq;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural coverage for <see cref="IntervalTree{TKey, TValue, TComparer}"/>: the constructors and
/// their validation, the point (<c>Contains</c>) and window (<c>Overlaps</c>) query shapes across the
/// allocation-free and convenience tiers, and the boundary cases half-open intervals turn on — an interval
/// starting exactly at the query point, two ranges meeting at a seam, an empty stored interval, and an empty
/// query window. The randomized reconciliation against a linear scan lives in
/// <see cref="IntervalTreeDifferentialTests"/>, and the <see cref="IReadOnlyList{T}"/> surface in
/// <see cref="IntervalTreeEnumerationTests"/>.
/// </summary>
public class IntervalTreeTests
{
    // Orders strings without regard to case, so the suite exercises a TComparer that is neither
    // DefaultComparer<T> nor an ordering over a value type.
    private readonly struct IgnoreCaseOrder : IComparer<string>
    {
        public int Compare(string? x, string? y) => string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
    }

    // A stateful struct comparer, matching BTreeSetTests' DirectionalComparer: it proves the type parameter is
    // not assumed to be default-constructed, which a field-free comparer passed as `default` cannot show.
    private readonly struct DirectionalComparer : IComparer<int>
    {
        private readonly int _sign;

        public DirectionalComparer(bool ascending) => _sign = ascending ? 1 : -1;

        public int Compare(int x, int y) => _sign * x.CompareTo(y);
    }

    private static Interval<int, string> Interval(int start, int end, string value) => new(start, end, value);

    // A three-interval fixture whose members deliberately nest, abut and sit apart:
    //   [0, 100) spans everything, [10, 20) and [30, 40) are disjoint islands inside it.
    private static IntervalTree<int, string> Fixture() => new(
    [
        Interval(0, 100, "span"),
        Interval(10, 20, "first"),
        Interval(30, 40, "second"),
    ]);

    private static string[] Values(Interval<int, string>[] matches) =>
        Array.ConvertAll(matches, match => match.Value!);

    // ---- construction ------------------------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldBuildAnEmptyTree_WhenGivenNoIntervals()
    {
        var tree = new IntervalTree<int, string>(Array.Empty<Interval<int, string>>());

        Assert.Equal(0, tree.Count);
        Assert.False(tree.ContainsPoint(5));
        Assert.False(tree.Overlaps(0, 100));
        Assert.Equal(0, tree.CountContaining(5));
        Assert.Equal(0, tree.CountOverlapping(0, 100));
        Assert.Empty(tree.GetContaining(5));
        Assert.Empty(tree.GetOverlapping(0, 100));
    }

    [Fact]
    public void Constructor_ShouldOrderEntriesByStart_WhenTheSourceIsUnordered()
    {
        var tree = new IntervalTree<int, string>(
        [
            Interval(30, 40, "third"),
            Interval(0, 100, "first"),
            Interval(10, 20, "second"),
        ]);

        Assert.Equal(3, tree.Count);
        Assert.Equal(new[] { 0, 10, 30 }, new[] { tree[0].Start, tree[1].Start, tree[2].Start });
    }

    [Fact]
    public void Constructor_ShouldOrderByEnd_WhenTwoIntervalsShareAStart()
    {
        var tree = new IntervalTree<int, string>(
        [
            Interval(5, 30, "wide"),
            Interval(5, 10, "narrow"),
        ]);

        Assert.Equal(10, tree[0].End);
        Assert.Equal(30, tree[1].End);
    }

    [Fact]
    public void Constructor_ShouldReadTheSequenceOnce_WhenGivenALazyEnumerable()
    {
        int enumerations = 0;

        IEnumerable<Interval<int, string>> Source()
        {
            enumerations++;
            yield return Interval(0, 10, "a");
            yield return Interval(5, 15, "b");
        }

        var tree = new IntervalTree<int, string>(Source());

        Assert.Equal(1, enumerations);
        Assert.Equal(2, tree.Count);
    }

    [Fact]
    public void Constructor_ShouldSizeExactly_WhenTheSourceIsCounted()
    {
        // An ICollection<T> source is copied straight into a right-sized array rather than going through a
        // List<T> first. Both shapes have to produce the same tree, which is what this pins.
        var list = new List<Interval<int, string>> { Interval(10, 20, "a"), Interval(0, 5, "b") };

        var fromCounted = new IntervalTree<int, string>(list);
        var fromUncounted = new IntervalTree<int, string>(list.Where(_ => true));

        Assert.Equal(2, fromCounted.Count);
        Assert.Equal(2, fromUncounted.Count);
        Assert.Equal(fromCounted[0].Value, fromUncounted[0].Value);
        Assert.True(fromCounted.ContainsPoint(15) && fromUncounted.ContainsPoint(15));
    }

    [Fact]
    public void Constructor_ShouldNotAliasTheSource_WhenGivenAnArray()
    {
        var source = new[] { Interval(0, 10, "a") };

        var tree = new IntervalTree<int, string>(source);
        source[0] = Interval(50, 60, "mutated");

        Assert.Equal(0, tree[0].Start);
        Assert.True(tree.ContainsPoint(5));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTheSequenceIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new IntervalTree<int, string>(null!));
        Assert.Throws<ArgumentNullException>(
            () => new IntervalTree<int, string, DefaultComparer<int>>(null!, default));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenAnIntervalEndsBeforeItStarts()
    {
        var inverted = new[] { Interval(0, 10, "fine"), Interval(30, 20, "inverted") };

        Assert.Throws<ArgumentException>(() => new IntervalTree<int, string>(inverted));
        Assert.Throws<ArgumentException>(() => new IntervalTree<int, string>((IEnumerable<Interval<int, string>>)inverted));
    }

    [Fact]
    public void Constructor_ShouldUseACustomComparer_WhenTheKeyIsNotOrderedNaturally()
    {
        var intervals = new[]
        {
            new Interval<string, int>("a", "d", 1),
            new Interval<string, int>("m", "q", 2),
        };

        var tree = new IntervalTree<string, int, IgnoreCaseOrder>(intervals, default);

        // "B" falls inside [a, d) only when the comparison ignores case; ordinally it precedes "a".
        Assert.True(tree.ContainsPoint("B"));

        // ["m", "q") starts exactly at the window's end under a case-insensitive order, so it does not overlap.
        Assert.Equal(1, tree.CountOverlapping("C", "M"));
        Assert.Equal(2, tree.CountOverlapping("C", "N"));
    }

    [Fact]
    public void Constructor_ShouldUseSuppliedComparer_WhenComparerIsStateful()
    {
        // Descending order, carried in a field: the tree is built and queried entirely through the instance
        // handed to the constructor, so this fails outright if the comparer argument were discarded in favour
        // of default(TComparer) — under which "descending" intervals would be rejected as inverted.
        var intervals = new[]
        {
            new Interval<int, string>(100, 50, "high"),   // [100, 50) descending: 100 precedes 50
            new Interval<int, string>(40, 10, "low"),
        };

        var tree = new IntervalTree<int, string, DirectionalComparer>(intervals, new DirectionalComparer(ascending: false));

        // Descending start order puts the interval starting at 100 first.
        Assert.Equal(100, tree[0].Start);
        Assert.Equal(40, tree[1].Start);

        Assert.True(tree.ContainsPoint(70));
        Assert.False(tree.ContainsPoint(45));
        Assert.Equal("low", tree.GetContaining(20)[0].Value);

        // The window runs in the comparer's direction too: [90, 30) covers both intervals' live ranges.
        Assert.Equal(2, tree.CountOverlapping(90, 30));
    }

    [Fact]
    public void Constructor_ShouldUseTheDefaultComparer_WhenBuiltThroughTheAlias()
    {
        var tree = new IntervalTree<int, string>([Interval(0, 10, "a")]);

        Assert.True(tree.ContainsPoint(0));
        Assert.False(tree.ContainsPoint(10));
    }

    // ---- point queries -----------------------------------------------------------------------------

    [Fact]
    public void ContainsPoint_ShouldFindAnIntervalInEveryPositionOfTheTree()
    {
        var tree = Fixture();

        Assert.True(tree.ContainsPoint(5));    // only the spanning interval, reached down the left subtree
        Assert.True(tree.ContainsPoint(15));   // the root node
        Assert.True(tree.ContainsPoint(35));   // the right subtree
    }

    [Fact]
    public void ContainsPoint_ShouldReturnFalse_WhenThePointIsOutsideEveryInterval()
    {
        var tree = Fixture();

        Assert.False(tree.ContainsPoint(-1));    // before every start
        Assert.False(tree.ContainsPoint(100));   // exactly at the widest end, which is exclusive
        Assert.False(tree.ContainsPoint(500));   // past every end, so the root prunes immediately
    }

    [Fact]
    public void ContainsPoint_ShouldTreatTheStartAsInclusiveAndTheEndAsExclusive()
    {
        var tree = new IntervalTree<int, string>([Interval(10, 20, "only")]);

        Assert.True(tree.ContainsPoint(10));
        Assert.True(tree.ContainsPoint(19));
        Assert.False(tree.ContainsPoint(20));
        Assert.False(tree.ContainsPoint(9));
    }

    [Fact]
    public void CountContaining_ShouldCountEveryCoveringInterval()
    {
        var tree = Fixture();

        Assert.Equal(2, tree.CountContaining(15));
        Assert.Equal(1, tree.CountContaining(5));
        Assert.Equal(0, tree.CountContaining(200));
    }

    [Fact]
    public void GetContaining_ShouldReturnTheCoveringIntervalsInStartOrder()
    {
        var tree = Fixture();

        Assert.Equal(new[] { "span", "first" }, Values(tree.GetContaining(15)));
        Assert.Empty(tree.GetContaining(1000));
    }

    [Fact]
    public void CountContaining_ShouldStillFindDeeplyNestedIntervals_WhenManyIntervalsShareAPoint()
    {
        // 64 nested intervals all covering 100, which forces the walk through every level of the tree.
        var nested = new Interval<int, string>[64];
        for (int i = 0; i < nested.Length; i++)
            nested[i] = Interval(i, 200 - i, $"level{i}");

        var tree = new IntervalTree<int, string>(nested);

        Assert.Equal(64, tree.CountContaining(100));
        Assert.Equal(64, tree.GetContaining(100).Length);
    }

    // ---- window queries ----------------------------------------------------------------------------

    [Fact]
    public void Overlaps_ShouldReturnTrue_WhenAnyIntervalSharesAPointWithTheWindow()
    {
        var tree = Fixture();

        Assert.True(tree.Overlaps(15, 16));
        Assert.True(tree.Overlaps(-50, 1));
        Assert.True(tree.Overlaps(38, 90));
    }

    [Fact]
    public void Overlaps_ShouldReturnFalse_WhenTheWindowOnlyAbutsAnInterval()
    {
        var tree = new IntervalTree<int, string>([Interval(10, 20, "only")]);

        Assert.False(tree.Overlaps(0, 10));    // ends exactly where the interval starts
        Assert.False(tree.Overlaps(20, 30));   // starts exactly where the interval ends
        Assert.True(tree.Overlaps(0, 11));
        Assert.True(tree.Overlaps(19, 30));
    }

    [Fact]
    public void Overlaps_ShouldReturnFalse_WhenTheWindowIsEmpty()
    {
        var tree = Fixture();

        Assert.False(tree.Overlaps(15, 15));
        Assert.Equal(0, tree.CountOverlapping(15, 15));
        Assert.Empty(tree.GetOverlapping(15, 15));
        Assert.Equal(0, tree.CopyOverlapping(15, 15, new Interval<int, string>[4]));
    }

    [Fact]
    public void Overlaps_ShouldThrow_WhenTheWindowEndsBeforeItStarts()
    {
        var tree = Fixture();

        Assert.Throws<ArgumentException>(() => tree.Overlaps(20, 10));
        Assert.Throws<ArgumentException>(() => tree.CountOverlapping(20, 10));
        Assert.Throws<ArgumentException>(() => tree.GetOverlapping(20, 10));
        Assert.Throws<ArgumentException>(() => tree.CopyOverlapping(20, 10, new Interval<int, string>[4]));
    }

    [Fact]
    public void CountOverlapping_ShouldCountEveryIntervalSharingAPointWithTheWindow()
    {
        var tree = Fixture();

        Assert.Equal(3, tree.CountOverlapping(0, 100));
        Assert.Equal(3, tree.CountOverlapping(15, 35));
        Assert.Equal(2, tree.CountOverlapping(15, 25));
        Assert.Equal(1, tree.CountOverlapping(50, 60));
        Assert.Equal(0, tree.CountOverlapping(200, 300));
    }

    [Fact]
    public void GetOverlapping_ShouldReturnTheMatchesInStartOrder()
    {
        var tree = Fixture();

        Assert.Equal(new[] { "span", "first", "second" }, Values(tree.GetOverlapping(0, 100)));
        Assert.Empty(tree.GetOverlapping(500, 600));
    }

    [Fact]
    public void GetOverlapping_ShouldNotReportAnEmptyStoredInterval_BecauseItCoversNoPoint()
    {
        var tree = new IntervalTree<int, string>(
        [
            Interval(10, 10, "empty"),
            Interval(0, 5, "real"),
        ]);

        // The empty interval is stored and enumerable, but sits strictly inside the window and still
        // matches nothing — it covers no point, so it shares none.
        Assert.Equal(2, tree.Count);
        Assert.Equal(new[] { "real" }, Values(tree.GetOverlapping(0, 50)));
        Assert.False(tree.ContainsPoint(10));
        Assert.Equal(0, tree.CountOverlapping(9, 11));
    }

    [Fact]
    public void Overlaps_ShouldReturnTrue_WhenOnlyTheLastIntervalMatches()
    {
        // Drives the walk all the way into the right subtree before the first match is found, and the
        // early-exit unwind back out of it.
        var tree = new IntervalTree<int, string>(
        [
            Interval(0, 1, "a"),
            Interval(2, 3, "b"),
            Interval(4, 5, "c"),
            Interval(6, 7, "d"),
            Interval(8, 9, "e"),
        ]);

        Assert.True(tree.Overlaps(8, 20));
        Assert.True(tree.ContainsPoint(8));
        Assert.False(tree.Overlaps(9, 20));
    }

    // ---- copy tier ---------------------------------------------------------------------------------

    [Fact]
    public void CopyOverlapping_ShouldFillTheDestinationFromTheGivenOffset()
    {
        var tree = Fixture();
        var destination = new Interval<int, string>[5];

        int written = tree.CopyOverlapping(0, 100, destination, 1);

        Assert.Equal(3, written);
        Assert.Null(destination[0].Value);
        Assert.Equal(new[] { "span", "first", "second" }, Values(destination[1..4]));
    }

    [Fact]
    public void CopyContaining_ShouldFillTheDestination()
    {
        var tree = Fixture();
        var destination = new Interval<int, string>[4];

        int written = tree.CopyContaining(15, destination);

        Assert.Equal(2, written);
        Assert.Equal(new[] { "span", "first" }, Values(destination[..2]));
    }

    [Fact]
    public void CopyOverlapping_ShouldStopWriting_WhenTheDestinationFillsUp()
    {
        var tree = Fixture();
        var destination = new Interval<int, string>[2];

        int written = tree.CopyOverlapping(0, 100, destination);

        Assert.Equal(2, written);
        Assert.Equal(new[] { "span", "first" }, Values(destination));
    }

    [Fact]
    public void CopyContaining_ShouldWriteNothing_WhenTheDestinationHasNoRoom()
    {
        var tree = Fixture();

        Assert.Equal(0, tree.CopyContaining(15, Array.Empty<Interval<int, string>>()));
        Assert.Equal(0, tree.CopyContaining(15, new Interval<int, string>[2], 2));
        Assert.Equal(0, tree.CopyOverlapping(0, 100, new Interval<int, string>[2], 2));
    }

    [Fact]
    public void CopyOverlapping_ShouldThrow_WhenTheDestinationIsNullOrTheOffsetIsOutOfRange()
    {
        var tree = Fixture();

        Assert.Throws<ArgumentNullException>(() => tree.CopyOverlapping(0, 10, null!));
        Assert.Throws<ArgumentNullException>(() => tree.CopyContaining(0, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.CopyOverlapping(0, 10, new Interval<int, string>[2], -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.CopyOverlapping(0, 10, new Interval<int, string>[2], 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => tree.CopyContaining(0, new Interval<int, string>[2], 3));
    }

    // ---- degenerate shapes -------------------------------------------------------------------------

    [Fact]
    public void Queries_ShouldWork_WhenTheTreeHoldsASingleInterval()
    {
        var tree = new IntervalTree<int, string>([Interval(10, 20, "only")]);

        Assert.Equal(1, tree.Count);
        Assert.True(tree.ContainsPoint(10));
        Assert.False(tree.ContainsPoint(20));
        Assert.Equal(1, tree.CountOverlapping(0, 50));
        Assert.Equal(0, tree.CountOverlapping(20, 50));
    }

    [Fact]
    public void Queries_ShouldReportEveryDuplicate_WhenIntervalsRepeat()
    {
        var tree = new IntervalTree<int, string>(
        [
            Interval(5, 10, "a"),
            Interval(5, 10, "b"),
            Interval(5, 10, "c"),
        ]);

        Assert.Equal(3, tree.CountContaining(7));
        Assert.Equal(3, tree.CountOverlapping(0, 100));
    }

    [Fact]
    public void Queries_ShouldWork_WhenEveryIntervalIsEmpty()
    {
        var tree = new IntervalTree<int, string>(
        [
            Interval(5, 5, "a"),
            Interval(9, 9, "b"),
        ]);

        Assert.Equal(2, tree.Count);
        Assert.False(tree.ContainsPoint(5));
        Assert.Equal(0, tree.CountOverlapping(0, 100));
    }

    [Fact]
    public void Queries_ShouldWork_WhenTheEndpointsAreDateTimes()
    {
        // The workload the type is for: overlapping bookings over a calendar.
        var day = new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc);
        var tree = new IntervalTree<DateTime, string>(
        [
            new Interval<DateTime, string>(day.AddHours(9), day.AddHours(10), "standup"),
            new Interval<DateTime, string>(day.AddHours(13), day.AddHours(14), "review"),
        ]);

        Assert.True(tree.Overlaps(day.AddHours(9).AddMinutes(30), day.AddHours(11)));
        Assert.False(tree.Overlaps(day.AddHours(10), day.AddHours(13)));
        Assert.Equal("review", tree.GetContaining(day.AddHours(13))[0].Value);
    }
}
