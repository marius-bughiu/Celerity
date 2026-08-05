using System;
using System.Linq;
using Celerity.Primitives;

namespace Celerity.Tests.Utils;

/// <summary>
/// Correctness coverage for <see cref="SortedSpan"/>: the two-cursor linear merge, the galloping path
/// taken when one side is at least 32x the other, the duplicate-collapsing set semantics, and the
/// destination-too-short contract.
///
/// <para>
/// Every input here is sorted ascending, which is the type's stated precondition — unsorted input is
/// documented as producing a wrong answer, and Debug builds assert on it, so there is deliberately no
/// test that feeds one in.
/// </para>
/// </summary>
public class SortedSpanTests
{
    // A long run, so `LongRun.Length >= 32 * short.Length` selects the galloping path and the
    // exponential probe has room to double several times before it brackets the answer.
    private static readonly int[] LongRun = Enumerable.Range(0, 1000).ToArray();

    // The same length, but with gaps, so a galloping probe can land on a real element that is not
    // the value it was looking for — the case a contiguous run cannot produce.
    private static readonly int[] LongEvens = Enumerable.Range(0, 1000).Select(x => x * 2).ToArray();

    // ── Intersect ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Intersect_ShouldReturnZero_WhenEitherSideIsEmpty()
    {
        Span<int> destination = stackalloc int[4];

        Assert.Equal(0, SortedSpan.Intersect<int>([], [1, 2, 3], destination));
        Assert.Equal(0, SortedSpan.Intersect<int>([1, 2, 3], [], destination));
        Assert.Equal(0, SortedSpan.Intersect<int>([], [], destination));
    }

    [Fact]
    public void Intersect_ShouldReturnZero_WhenTheSpansAreDisjoint()
    {
        Span<int> destination = stackalloc int[4];

        Assert.Equal(0, SortedSpan.Intersect<int>([1, 3, 5], [2, 4, 6], destination));
        Assert.Equal(0, SortedSpan.Intersect<int>([1, 2, 3], [7, 8, 9], destination));
        Assert.Equal(0, SortedSpan.Intersect<int>([7, 8, 9], [1, 2, 3], destination));
    }

    [Fact]
    public void Intersect_ShouldReturnEverything_WhenTheSpansAreIdentical()
    {
        Span<int> destination = stackalloc int[4];
        int written = SortedSpan.Intersect<int>([1, 2, 3, 4], [1, 2, 3, 4], destination);

        Assert.Equal(new[] { 1, 2, 3, 4 }, destination[..written].ToArray());
    }

    [Fact]
    public void Intersect_ShouldReturnTheCommonValues_WhenTheSpansOverlapPartially()
    {
        Span<int> destination = stackalloc int[8];
        int written = SortedSpan.Intersect<int>([1, 3, 5, 7, 9], [3, 4, 5, 6, 9, 11], destination);

        Assert.Equal(new[] { 3, 5, 9 }, destination[..written].ToArray());

        // The right side runs out on a match while the left still has values — the other way out
        // of the merge loop.
        written = SortedSpan.Intersect<int>([1, 2, 3], [2], destination);
        Assert.Equal(new[] { 2 }, destination[..written].ToArray());
    }

    [Fact]
    public void Intersect_ShouldMatchTheSingleValue_WhenBothSpansHaveOneElement()
    {
        Span<int> destination = stackalloc int[1];

        Assert.Equal(1, SortedSpan.Intersect<int>([5], [5], destination));
        Assert.Equal(5, destination[0]);
        Assert.Equal(0, SortedSpan.Intersect<int>([5], [6], destination));
    }

    [Fact]
    public void Intersect_ShouldCollapseThem_WhenAnInputRepeatsAValue()
    {
        Span<int> destination = stackalloc int[8];
        int written = SortedSpan.Intersect<int>([1, 2, 2, 2, 3], [2, 2, 3, 3, 4], destination);

        Assert.Equal(new[] { 2, 3 }, destination[..written].ToArray());
    }

    [Fact]
    public void Intersect_ShouldSucceed_WhenTheDestinationFitsExactly()
    {
        Span<int> destination = stackalloc int[2];
        int written = SortedSpan.Intersect<int>([1, 2, 3], [2, 3, 4], destination);

        Assert.Equal(new[] { 2, 3 }, destination[..written].ToArray());
    }

    [Fact]
    public void Intersect_ShouldThrow_WhenTheDestinationIsTooShort()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            Span<int> destination = stackalloc int[1];
            SortedSpan.Intersect<int>([1, 2, 3], [1, 2, 3], destination);
        });

        Assert.Equal("destination", exception.ParamName);
    }

    [Fact]
    public void Intersect_ShouldStillBeCorrect_WhenTheRightSideIsLongEnoughToGallop()
    {
        Span<int> destination = stackalloc int[8];
        int written = SortedSpan.Intersect<int>([5, 500, 999, 1500], LongRun, destination);

        Assert.Equal(new[] { 5, 500, 999 }, destination[..written].ToArray());
    }

    [Fact]
    public void Intersect_ShouldStillBeCorrect_WhenTheLeftSideIsLongEnoughToGallop()
    {
        Span<int> destination = stackalloc int[8];
        int written = SortedSpan.Intersect<int>(LongRun, [0, 500, 500, 2000], destination);

        Assert.Equal(new[] { 0, 500 }, destination[..written].ToArray());
    }

    [Fact]
    public void Intersect_ShouldStopEarly_WhenTheGallopingProbeRunsOffTheLongSide()
    {
        Span<int> destination = stackalloc int[8];

        // Every probe value sits beyond the last element of the long run, so the first lower-bound
        // search lands on the end and the remaining short-side values are never probed.
        Assert.Equal(0, SortedSpan.Intersect<int>([5000, 6000], LongRun, destination));
    }

    [Fact]
    public void Intersect_ShouldThrow_WhenTheGallopingPathOverrunsTheDestination()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            Span<int> destination = stackalloc int[1];
            SortedSpan.Intersect<int>([1, 2, 3], LongRun, destination);
        });

        Assert.Equal("destination", exception.ParamName);
    }

    // ── Union ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Union_ShouldReturnTheOtherSide_WhenOneSideIsEmpty()
    {
        Span<int> destination = stackalloc int[8];

        int written = SortedSpan.Union<int>([], [1, 2, 2, 3], destination);
        Assert.Equal(new[] { 1, 2, 3 }, destination[..written].ToArray());

        written = SortedSpan.Union<int>([1, 1, 4], [], destination);
        Assert.Equal(new[] { 1, 4 }, destination[..written].ToArray());
    }

    [Fact]
    public void Union_ShouldReturnZero_WhenBothSidesAreEmpty()
    {
        Span<int> destination = stackalloc int[4];

        Assert.Equal(0, SortedSpan.Union<int>([], [], destination));
    }

    [Fact]
    public void Union_ShouldInterleaveBothSides_WhenTheSpansAreDisjoint()
    {
        Span<int> destination = stackalloc int[8];
        int written = SortedSpan.Union<int>([1, 3, 5], [2, 4, 6], destination);

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, destination[..written].ToArray());
    }

    [Fact]
    public void Union_ShouldDrainTheLeftSide_WhenTheRightSideIsExhaustedFirst()
    {
        Span<int> destination = stackalloc int[8];
        int written = SortedSpan.Union<int>([1, 2, 7, 8, 8, 9], [1, 2], destination);

        Assert.Equal(new[] { 1, 2, 7, 8, 9 }, destination[..written].ToArray());
    }

    [Fact]
    public void Union_ShouldDrainTheRightSide_WhenTheLeftSideIsExhaustedFirst()
    {
        Span<int> destination = stackalloc int[8];
        int written = SortedSpan.Union<int>([1, 2], [1, 2, 7, 8, 8, 9], destination);

        Assert.Equal(new[] { 1, 2, 7, 8, 9 }, destination[..written].ToArray());

        // ... and the same exit reached by consuming the right side outright rather than by matching.
        written = SortedSpan.Union<int>([3], [1, 2], destination);
        Assert.Equal(new[] { 1, 2, 3 }, destination[..written].ToArray());
    }

    [Fact]
    public void Union_ShouldDrainNothing_WhenBothSidesEndOnTheSameValue()
    {
        Span<int> destination = stackalloc int[8];
        int written = SortedSpan.Union<int>([1, 5], [3, 5], destination);

        Assert.Equal(new[] { 1, 3, 5 }, destination[..written].ToArray());
    }

    [Fact]
    public void Union_ShouldCollapseThem_WhenAnInputRepeatsAValue()
    {
        Span<int> destination = stackalloc int[8];
        int written = SortedSpan.Union<int>([1, 1, 2, 4, 4], [2, 2, 3], destination);

        Assert.Equal(new[] { 1, 2, 3, 4 }, destination[..written].ToArray());
    }

    [Fact]
    public void Union_ShouldThrow_WhenTheDestinationIsTooShort()
    {
        var mergeOverrun = Assert.Throws<ArgumentException>(() =>
        {
            Span<int> destination = stackalloc int[2];
            SortedSpan.Union<int>([1, 3], [2, 4], destination);
        });
        Assert.Equal("destination", mergeOverrun.ParamName);

        // The tail drain has its own write path, so it gets its own overrun case.
        var drainOverrun = Assert.Throws<ArgumentException>(() =>
        {
            Span<int> destination = stackalloc int[2];
            SortedSpan.Union<int>([1, 2, 3, 4], [1], destination);
        });
        Assert.Equal("destination", drainOverrun.ParamName);

        var emptySideOverrun = Assert.Throws<ArgumentException>(() =>
        {
            Span<int> destination = stackalloc int[1];
            SortedSpan.Union<int>([], [1, 2, 3], destination);
        });
        Assert.Equal("destination", emptySideOverrun.ParamName);
    }

    // ── Except ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Except_ShouldReturnZero_WhenTheLeftSideIsEmpty()
    {
        Span<int> destination = stackalloc int[4];

        Assert.Equal(0, SortedSpan.Except<int>([], [1, 2, 3], destination));
    }

    [Fact]
    public void Except_ShouldReturnTheLeftSide_WhenTheRightSideIsEmpty()
    {
        Span<int> destination = stackalloc int[4];
        int written = SortedSpan.Except<int>([1, 2, 2, 3], [], destination);

        Assert.Equal(new[] { 1, 2, 3 }, destination[..written].ToArray());
    }

    [Fact]
    public void Except_ShouldReturnTheLeftSide_WhenTheSpansAreDisjoint()
    {
        Span<int> destination = stackalloc int[8];
        int written = SortedSpan.Except<int>([1, 3, 5], [2, 4, 6], destination);

        Assert.Equal(new[] { 1, 3, 5 }, destination[..written].ToArray());
    }

    [Fact]
    public void Except_ShouldReturnZero_WhenTheSpansAreIdentical()
    {
        Span<int> destination = stackalloc int[8];

        Assert.Equal(0, SortedSpan.Except<int>([1, 2, 3], [1, 2, 3], destination));
    }

    [Fact]
    public void Except_ShouldRemoveOnlyTheCommonValues_WhenTheSpansOverlapPartially()
    {
        Span<int> destination = stackalloc int[8];
        int written = SortedSpan.Except<int>([1, 2, 3, 4, 5], [2, 4, 9], destination);

        Assert.Equal(new[] { 1, 3, 5 }, destination[..written].ToArray());
    }

    [Fact]
    public void Except_ShouldDrainTheRemainder_WhenTheRightSideIsExhaustedFirst()
    {
        Span<int> destination = stackalloc int[8];

        // The right side runs out on a match, then on a skip-ahead — two different exits from the merge.
        int written = SortedSpan.Except<int>([1, 2, 3, 3, 4], [2], destination);
        Assert.Equal(new[] { 1, 3, 4 }, destination[..written].ToArray());

        written = SortedSpan.Except<int>([1, 5, 6], [0], destination);
        Assert.Equal(new[] { 1, 5, 6 }, destination[..written].ToArray());

        // A right-side value below everything on the left is skipped without ending the merge.
        written = SortedSpan.Except<int>([5, 6], [0, 5], destination);
        Assert.Equal(new[] { 6 }, destination[..written].ToArray());
    }

    [Fact]
    public void Except_ShouldCollapseThem_WhenTheLeftSideRepeatsAValue()
    {
        Span<int> destination = stackalloc int[8];
        int written = SortedSpan.Except<int>([1, 1, 2, 2, 5], [2, 2], destination);

        Assert.Equal(new[] { 1, 5 }, destination[..written].ToArray());
    }

    [Fact]
    public void Except_ShouldThrow_WhenTheDestinationIsTooShort()
    {
        var mergeOverrun = Assert.Throws<ArgumentException>(() =>
        {
            Span<int> destination = stackalloc int[1];
            SortedSpan.Except<int>([1, 2, 3], [9], destination);
        });
        Assert.Equal("destination", mergeOverrun.ParamName);

        var emptyRightOverrun = Assert.Throws<ArgumentException>(() =>
        {
            Span<int> destination = stackalloc int[1];
            SortedSpan.Except<int>([1, 2, 3], [], destination);
        });
        Assert.Equal("destination", emptyRightOverrun.ParamName);
    }

    [Fact]
    public void Except_ShouldStillBeCorrect_WhenTheRightSideIsLongEnoughToGallop()
    {
        Span<int> destination = stackalloc int[8];
        int written = SortedSpan.Except<int>([5, 5, 500, 1500, 1600], LongRun, destination);

        Assert.Equal(new[] { 1500, 1600 }, destination[..written].ToArray());
    }

    [Fact]
    public void Except_ShouldThrow_WhenTheGallopingPathOverrunsTheDestination()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
        {
            Span<int> destination = stackalloc int[1];
            SortedSpan.Except<int>([1500, 1600, 1700], LongRun, destination);
        });

        Assert.Equal("destination", exception.ParamName);
    }

    // ── IntersectCount ───────────────────────────────────────────────────────────────────

    [Fact]
    public void IntersectCount_ShouldReturnZero_WhenEitherSideIsEmpty()
    {
        Assert.Equal(0, SortedSpan.IntersectCount<int>([], [1, 2]));
        Assert.Equal(0, SortedSpan.IntersectCount<int>([1, 2], []));
    }

    [Fact]
    public void IntersectCount_ShouldCountDistinctCommonValues_WhenInputsRepeat()
    {
        Assert.Equal(2, SortedSpan.IntersectCount<int>([1, 2, 2, 3, 5], [2, 2, 4, 5]));
        Assert.Equal(0, SortedSpan.IntersectCount<int>([1, 3, 5], [2, 4, 6]));
        Assert.Equal(0, SortedSpan.IntersectCount<int>([7, 8], [1, 2]));
    }

    [Fact]
    public void IntersectCount_ShouldStillBeCorrect_WhenEitherSideIsLongEnoughToGallop()
    {
        Assert.Equal(3, SortedSpan.IntersectCount([5, 5, 500, 999, 1500], (ReadOnlySpan<int>)LongRun));
        Assert.Equal(2, SortedSpan.IntersectCount((ReadOnlySpan<int>)LongRun, [0, 999, 1000]));
        Assert.Equal(0, SortedSpan.IntersectCount([5000], (ReadOnlySpan<int>)LongRun));
    }

    // ── Overlaps ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Overlaps_ShouldReturnFalse_WhenEitherSideIsEmpty()
    {
        Assert.False(SortedSpan.Overlaps<int>([], [1, 2]));
        Assert.False(SortedSpan.Overlaps<int>([1, 2], []));
    }

    [Fact]
    public void Overlaps_ShouldReportSharedMembership_WhenTheSpansAreMerged()
    {
        Assert.True(SortedSpan.Overlaps<int>([1, 2, 3], [3, 4, 5]));
        Assert.False(SortedSpan.Overlaps<int>([1, 3, 5], [2, 4, 6]));
        Assert.False(SortedSpan.Overlaps<int>([1, 2], [8, 9]));
        Assert.False(SortedSpan.Overlaps<int>([8, 9], [1, 2]));
    }

    [Fact]
    public void Overlaps_ShouldReportSharedMembership_WhenTheSpansGallop()
    {
        Assert.True(SortedSpan.Overlaps([999, 1500], (ReadOnlySpan<int>)LongRun));
        Assert.True(SortedSpan.Overlaps((ReadOnlySpan<int>)LongRun, [0, 5000]));
        Assert.False(SortedSpan.Overlaps([5000, 6000], (ReadOnlySpan<int>)LongRun));
    }

    // ── Galloping onto a value that is present in range but absent from the long side ────

    [Fact]
    public void Intersect_ShouldSkipIt_WhenAGallopingProbeLandsOnADifferentValue()
    {
        Span<int> destination = stackalloc int[8];
        int written = SortedSpan.Intersect([3, 500, 777], (ReadOnlySpan<int>)LongEvens, destination);

        Assert.Equal(new[] { 500 }, destination[..written].ToArray());
    }

    [Fact]
    public void Except_ShouldKeepIt_WhenAGallopingProbeLandsOnADifferentValue()
    {
        Span<int> destination = stackalloc int[8];
        int written = SortedSpan.Except([3, 500, 777], (ReadOnlySpan<int>)LongEvens, destination);

        Assert.Equal(new[] { 3, 777 }, destination[..written].ToArray());
    }

    [Fact]
    public void IntersectCountAndOverlaps_ShouldSkipIt_WhenAGallopingProbeLandsOnADifferentValue()
    {
        Assert.Equal(0, SortedSpan.IntersectCount([3, 777], (ReadOnlySpan<int>)LongEvens));
        Assert.Equal(1, SortedSpan.IntersectCount([3, 500, 777], (ReadOnlySpan<int>)LongEvens));
        Assert.False(SortedSpan.Overlaps([3, 777], (ReadOnlySpan<int>)LongEvens));
        Assert.True(SortedSpan.Overlaps([3, 500, 777], (ReadOnlySpan<int>)LongEvens));
    }

    // ── Element types other than int ──────────────────────────────────────────────────────

    [Fact]
    public void Intersect_ShouldWork_WhenTheElementTypeIsAnotherPrimitive()
    {
        Span<long> longs = stackalloc long[4];
        int written = SortedSpan.Intersect<long>([1L, 4L, 9L], [4L, 9L, 16L], longs);
        Assert.Equal(new[] { 4L, 9L }, longs[..written].ToArray());

        Span<uint> uints = stackalloc uint[4];
        written = SortedSpan.Union<uint>([1u, 3u], [2u], uints);
        Assert.Equal(new[] { 1u, 2u, 3u }, uints[..written].ToArray());

        Span<ulong> ulongs = stackalloc ulong[4];
        written = SortedSpan.Except<ulong>([1ul, 2ul, 3ul], [2ul], ulongs);
        Assert.Equal(new[] { 1ul, 3ul }, ulongs[..written].ToArray());
    }

    // ── Cross-checks against the HashSet oracle ──────────────────────────────────────────

    [Theory]
    [InlineData(1, 40, 40)]
    [InlineData(2, 1, 400)]
    [InlineData(3, 400, 1)]
    [InlineData(4, 3, 900)]
    [InlineData(5, 250, 250)]
    public void EveryOperation_ShouldMatchTheHashSetOracle_AcrossLengthRatios(int seed, int leftCount, int rightCount)
    {
        var rand = new Random(seed);
        int[] left = SortedSample(rand, leftCount);
        int[] right = SortedSample(rand, rightCount);

        var leftSet = new HashSet<int>(left);
        var rightSet = new HashSet<int>(right);

        var expectedIntersect = leftSet.Intersect(rightSet).OrderBy(x => x).ToArray();
        var expectedUnion = leftSet.Union(rightSet).OrderBy(x => x).ToArray();
        var expectedExcept = leftSet.Except(rightSet).OrderBy(x => x).ToArray();

        var buffer = new int[left.Length + right.Length];

        Assert.Equal(expectedIntersect, buffer.AsSpan(0, SortedSpan.Intersect<int>(left, right, buffer)).ToArray());
        Assert.Equal(expectedUnion, buffer.AsSpan(0, SortedSpan.Union<int>(left, right, buffer)).ToArray());
        Assert.Equal(expectedExcept, buffer.AsSpan(0, SortedSpan.Except<int>(left, right, buffer)).ToArray());
        Assert.Equal(expectedIntersect.Length, SortedSpan.IntersectCount<int>(left, right));
        Assert.Equal(expectedIntersect.Length > 0, SortedSpan.Overlaps<int>(left, right));
    }

    // A sorted sample with duplicates: the values are drawn from a domain narrow enough that the
    // same one comes up repeatedly, which is what exercises the duplicate-collapsing paths.
    private static int[] SortedSample(Random rand, int count)
    {
        var values = new int[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = rand.Next(0, Math.Max(4, count));
        }

        Array.Sort(values);
        return values;
    }
}
