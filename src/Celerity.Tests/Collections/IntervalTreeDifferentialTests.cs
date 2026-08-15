using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Randomized reconciliation of <see cref="IntervalTree{TKey, TValue, TComparer}"/> against the linear scan it
/// replaces — the <c>List&lt;T&gt;</c> filter a caller writes when the BCL offers nothing.
///
/// <para>
/// The pruning is what is on trial. Every query skips whole subtrees on two bounds: one on the maximum end
/// stored in a subtree, one on the sorted starts. A wrong comparison in either direction is invisible on a
/// hand-written fixture — pruning too little only costs time, and pruning too much drops matches that a small
/// example is unlikely to contain. Reconciling every query shape against an exhaustive scan over the same
/// intervals is what makes a dropped match fail loudly.
/// </para>
///
/// <para>
/// The generated shapes are deliberately mixed: long spanning intervals (which defeat any bound based on
/// starts alone), tight clusters (which pile many matches on one point), empty intervals, and exact duplicates.
/// </para>
///
/// <para>
/// Three layers, narrowest first. The CsCheck property generates the interval set from its own axes — count,
/// domain width and the fraction of long spans — so a disagreement shrinks to a minimal reproduction with the
/// seed printed. The seeded theory below it drives longer runs at a fixed shape, and the exhaustive sweep at
/// the end checks <i>every</i> point and <i>every</i> window of a small domain, which is the only layer that
/// can prove no case was merely missed by sampling.
/// </para>
/// </summary>
public class IntervalTreeDifferentialTests
{
    // The generation axes that decide which pruning paths a case can reach: how many intervals there are, how
    // wide the value domain is (a narrow domain against many intervals piles matches on every point, a wide one
    // makes them sparse), and what fraction of them are long spans — the shape no bound over the sorted starts
    // can constrain, and therefore the one that decides whether pruning is even exercised.
    private static readonly Gen<(int Count, int Domain, int SpanPercent, uint Seed)> GenIntervals =
        Gen.Select(Gen.Int[0, 160], Gen.Int[2, 200], Gen.Int[0, 100], Gen.UInt);

    [Fact]
    public void Queries_ShouldMatchALinearScan_UnderGeneratedIntervalSets()
    {
        GenIntervals.Sample(spec =>
        {
            var rand = new Random((int)spec.Seed);
            Interval<int, int>[] intervals = Build(rand, spec.Count, spec.Domain, spec.SpanPercent);
            var tree = new IntervalTree<int, int>(intervals);

            Assert.Equal(intervals.Length, tree.Count);
            AssertStartOrdered(tree);

            // The query domain runs past both ends of the interval domain, so the fully-pruned cases — every
            // subtree ending before the query, every subtree starting after it — are reached as well.
            for (int query = 0; query < 25; query++)
            {
                int point = rand.Next(-3, spec.Domain + 3);
                AssertPointQuery(tree, intervals, point);

                int start = rand.Next(-3, spec.Domain + 3);
                int end = start + rand.Next(0, spec.Domain);
                AssertWindowQuery(tree, intervals, start, end);
            }
        }, iter: 300);
    }

    private static Interval<int, int>[] Build(Random rand, int count, int domain, int spanPercent)
    {
        var intervals = new Interval<int, int>[count];
        for (int i = 0; i < count; i++)
        {
            int start = rand.Next(0, domain);
            int length = rand.Next(0, 100) < spanPercent
                ? rand.Next(0, domain)   // a long span, which start ordering cannot bound
                : rand.Next(0, 5);       // a tight one, and 0 draws the empty interval that must never match

            intervals[i] = new Interval<int, int>(start, start + length, i);
        }

        return intervals;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(2026)]
    public void Queries_ShouldMatchALinearScan_OverRandomIntervals(int seed)
    {
        var rand = new Random(seed);

        for (int trial = 0; trial < 40; trial++)
        {
            Interval<int, int>[] intervals = RandomIntervals(rand, rand.Next(0, 200));
            var tree = new IntervalTree<int, int>(intervals);

            Assert.Equal(intervals.Length, tree.Count);
            AssertStartOrdered(tree);

            for (int query = 0; query < 60; query++)
            {
                int point = rand.Next(-20, 140);
                AssertPointQuery(tree, intervals, point);

                int start = rand.Next(-20, 140);
                int end = start + rand.Next(0, 60);
                AssertWindowQuery(tree, intervals, start, end);
            }
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(11)]
    public void Queries_ShouldMatchALinearScan_OverEveryPointOfASmallDomain(int seed)
    {
        var rand = new Random(seed);

        // Small domains and small counts put every structural shape in reach — a single interval, an even
        // count (whose implicit tree has an empty side at some level), a tree of depth one — and the domain is
        // narrow enough to sweep exhaustively rather than sample.
        for (int count = 0; count <= 12; count++)
        {
            Interval<int, int>[] intervals = RandomIntervals(rand, count, domain: 12);
            var tree = new IntervalTree<int, int>(intervals);

            for (int point = -2; point <= 14; point++)
            {
                AssertPointQuery(tree, intervals, point);

                for (int end = point; end <= 14; end++)
                    AssertWindowQuery(tree, intervals, point, end);
            }
        }
    }

    private static Interval<int, int>[] RandomIntervals(Random rand, int count, int domain = 100)
    {
        var intervals = new Interval<int, int>[count];
        for (int i = 0; i < count; i++)
        {
            int start = rand.Next(0, domain);
            int length = rand.Next(0, 10) switch
            {
                0 => 0,                                 // an empty interval, which must never match
                < 4 => rand.Next(0, domain),            // a long span, which start ordering cannot bound
                _ => rand.Next(0, 5),                   // a tight one, which clusters matches on a point
            };

            intervals[i] = new Interval<int, int>(start, start + length, i);
        }

        return intervals;
    }

    private static void AssertStartOrdered(IntervalTree<int, int> tree)
    {
        for (int i = 1; i < tree.Count; i++)
            Assert.True(tree[i - 1].Start <= tree[i].Start, "Entries are not in ascending start order.");
    }

    private static void AssertPointQuery(IntervalTree<int, int> tree, Interval<int, int>[] oracle, int point)
    {
        var expected = new List<int>();
        foreach (var interval in oracle)
        {
            if (interval.Start <= point && point < interval.End)
                expected.Add(interval.Value);
        }

        Interval<int, int>[] actual = tree.GetContaining(point);

        AssertSameMatches(expected, actual, $"point {point}");
        Assert.Equal(expected.Count, tree.CountContaining(point));
        Assert.Equal(expected.Count > 0, tree.ContainsPoint(point));

        // The allocation-free tier must agree with the convenience one, including when the buffer is short.
        var destination = new Interval<int, int>[expected.Count + 1];
        Assert.Equal(expected.Count, tree.CopyContaining(point, destination));
        AssertSameMatches(expected, destination[..expected.Count], $"copied point {point}");

        if (expected.Count > 1)
        {
            var truncated = new Interval<int, int>[expected.Count - 1];
            Assert.Equal(truncated.Length, tree.CopyContaining(point, truncated));
        }
    }

    private static void AssertWindowQuery(IntervalTree<int, int> tree, Interval<int, int>[] oracle, int start, int end)
    {
        var expected = new List<int>();
        foreach (var interval in oracle)
        {
            // Two half-open ranges share a point exactly when each starts before the other ends, and neither
            // is empty.
            if (interval.Start < interval.End && start < end && interval.Start < end && start < interval.End)
                expected.Add(interval.Value);
        }

        Interval<int, int>[] actual = tree.GetOverlapping(start, end);

        AssertSameMatches(expected, actual, $"window [{start}, {end})");
        Assert.Equal(expected.Count, tree.CountOverlapping(start, end));
        Assert.Equal(expected.Count > 0, tree.Overlaps(start, end));

        var destination = new Interval<int, int>[expected.Count + 1];
        Assert.Equal(expected.Count, tree.CopyOverlapping(start, end, destination));
        AssertSameMatches(expected, destination[..expected.Count], $"copied window [{start}, {end})");
    }

    private static void AssertSameMatches(List<int> expected, Interval<int, int>[] actual, string what)
    {
        var expectedSorted = new List<int>(expected);
        expectedSorted.Sort();

        var actualSorted = new List<int>();
        foreach (var interval in actual)
            actualSorted.Add(interval.Value);
        actualSorted.Sort();

        Assert.Equal(expectedSorted, actualSorted);

        for (int i = 1; i < actual.Length; i++)
            Assert.True(actual[i - 1].Start <= actual[i].Start, $"Matches for {what} are not in start order.");
    }
}
