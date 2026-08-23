using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural coverage for <see cref="RankedSet{T, TComparer}"/>: the ordered surface it shares with the
/// rest of the family, and the positional surface that is the reason it exists —
/// <see cref="RankedSet{T, TComparer}.this[int]"/>, <see cref="RankedSet{T, TComparer}.IndexOf"/>,
/// <see cref="RankedSet{T, TComparer}.CountLessThan"/> and
/// <see cref="RankedSet{T, TComparer}.RemoveAt"/>.
///
/// <para>
/// What decides whether this type is right is not whether an element is found — a set whose bucket index has
/// drifted still answers membership from the bucket it walks into. It is whether the <i>rank</i> agrees with
/// the enumeration after the structure has moved underneath it, so the tests below drive the three structural
/// events on purpose and re-check every rank afterwards: a bucket splitting when it fills, two thinned-out
/// neighbours merging, and a bucket emptying and being dropped. Each is reached by an explicit sequence with
/// the arithmetic written out, rather than left to a random walk to stumble into.
/// </para>
///
/// <para>
/// The randomized reconciliation against <see cref="SortedSet{T}"/> lives in
/// <see cref="RankedSetDifferentialTests"/>, and the enumerator and interface surfaces in
/// <see cref="RankedSetEnumerationTests"/>.
/// </para>
/// </summary>
public class RankedSetTests
{
    // The bucket capacity is 512 and a full bucket splits into two of 256, so an ascending fill of 1,200
    // elements lays out as 256 / 256 / 256 / 432 — four buckets, with room to thin any one of them out
    // without disturbing its neighbours. Every structural test below is written against that shape.
    private const int LayoutSize = 1200;

    private static RankedSet<int> Ascending(int count) => new(Enumerable.Range(0, count));

    private static int[] Items<TComparer>(RankedSet<int, TComparer> set)
        where TComparer : struct, IComparer<int> => [.. set];

    // Reconciles the whole positional surface against the sequence the set enumerates, which is the only
    // thing that catches a bucket index that has drifted out of step with the elements.
    private static void AssertRanksAgreeWithOrder(RankedSet<int> set)
    {
        int[] ordered = [.. set];

        Assert.Equal(ordered.Length, set.Count);

        for (int i = 0; i < ordered.Length; i++)
        {
            Assert.Equal(ordered[i], set[i]);
            Assert.Equal(i, set.IndexOf(ordered[i]));
            Assert.Equal(i, set.CountLessThan(ordered[i]));
            Assert.Equal(i + 1, set.CountLessThanOrEqual(ordered[i]));
        }
    }

    // ---- construction ------------------------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldProduceAnEmptySet_WhenNoSourceIsGiven()
    {
        var set = new RankedSet<int>();

        Assert.Equal(0, set.Count);
        Assert.False(set.Contains(0));
        Assert.Empty(set);
    }

    [Fact]
    public void Constructor_ShouldOrderAndDedupeTheSource_WhenItIsUnsorted()
    {
        var set = new RankedSet<int>([5, 1, 9, 1, 5, 3]);

        Assert.Equal(4, set.Count);
        Assert.Equal([1, 3, 5, 9], Items(set));
    }

    [Fact]
    public void Constructor_ShouldUseTheSuppliedComparer_WhenOneIsGiven()
    {
        var set = new RankedSet<int, Descending>([1, 2, 3], default);

        Assert.Equal([3, 2, 1], Items(set));
        Assert.Equal(0, set.IndexOf(3));
    }

    [Fact]
    public void Constructor_ShouldUseTheSuppliedComparer_WhenTheSetStartsEmpty()
    {
        var set = new RankedSet<int, Descending>(new Descending());
        set.Add(1);
        set.Add(2);

        Assert.Equal([2, 1], Items(set));
    }

    [Fact]
    public void Comparer_ShouldReturnTheComparerTheSetOrdersBy()
    {
        var set = new RankedSet<int, Descending>();

        Assert.IsType<Descending>(set.Comparer);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenTheSourceIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new RankedSet<int>(null!));
        Assert.Throws<ArgumentNullException>(() => new RankedSet<int, Descending>(null!, default));
    }

    [Fact]
    public void Constructor_ShouldOrderNullFirst_WhenTheElementsAreReferences()
    {
        var set = new RankedSet<string?>(["b", null, "a", null]);

        Assert.Equal(3, set.Count);
        Assert.Equal<string?[]>([null, "a", "b"], [.. set]);
        Assert.Equal(0, set.IndexOf(null));
    }

    // ---- add ---------------------------------------------------------------------------------------

    [Fact]
    public void Add_ShouldThrowArgumentException_WhenTheElementIsAlreadyPresent()
    {
        var set = new RankedSet<int>([1, 2, 3]);

        var thrown = Assert.Throws<ArgumentException>(() => set.Add(2));

        Assert.Equal("item", thrown.ParamName);
        Assert.Equal(3, set.Count);
    }

    [Fact]
    public void TryAdd_ShouldReportWhetherTheElementWasNew()
    {
        var set = new RankedSet<int>();

        Assert.True(set.TryAdd(7));
        Assert.False(set.TryAdd(7));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Add_ShouldKeepEveryRankConsistent_WhenElementsArriveAscending()
    {
        // Ascending inserts always land at the end of the last bucket, so this is the arm where the split
        // pushes the insertion point into the new right-hand half.
        var set = Ascending(LayoutSize);

        AssertRanksAgreeWithOrder(set);
        Assert.Equal(0, set.Min);
        Assert.Equal(LayoutSize - 1, set.Max);
    }

    [Fact]
    public void Add_ShouldKeepEveryRankConsistent_WhenElementsArriveDescending()
    {
        // The mirror arm: every insert is at offset 0, so a split must leave the insertion point on the left.
        var set = new RankedSet<int>();
        for (int i = LayoutSize - 1; i >= 0; i--)
            set.Add(i);

        AssertRanksAgreeWithOrder(set);
    }

    [Fact]
    public void Add_ShouldKeepEveryRankConsistent_WhenElementsArriveInterleaved()
    {
        // Fills the even values first and then threads the odd ones between them, so most inserts land in the
        // middle of an already-populated bucket rather than at either edge.
        var set = new RankedSet<int>();
        for (int i = 0; i < LayoutSize; i += 2)
            set.Add(i);
        for (int i = 1; i < LayoutSize; i += 2)
            set.Add(i);

        AssertRanksAgreeWithOrder(set);
    }

    [Fact]
    public void Add_ShouldGrowTheBucketIndex_WhenTheInitialSlotsAreExhausted()
    {
        // The bucket-slot array starts at four; this needs nine buckets, so it grows twice and the Fenwick
        // tree is rebuilt over the wider array both times.
        var set = Ascending(2600);

        AssertRanksAgreeWithOrder(set);
    }

    // ---- membership and the positional surface -----------------------------------------------------

    [Fact]
    public void Contains_ShouldAnswerCorrectly_WhetherTheElementIsInsideOutsideOrAbsent()
    {
        var set = new RankedSet<int>([10, 20, 30]);

        Assert.True(set.Contains(20));
        Assert.False(set.Contains(25));
        Assert.False(set.Contains(5));
        Assert.False(set.Contains(35));
    }

    [Fact]
    public void Indexer_ShouldReturnTheKthSmallest()
    {
        var set = new RankedSet<int>([50, 10, 40, 20, 30]);

        Assert.Equal(10, set[0]);
        Assert.Equal(30, set[2]);
        Assert.Equal(50, set[4]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Indexer_ShouldThrowArgumentOutOfRangeException_WhenTheRankIsOutsideTheSet(int index)
    {
        var set = new RankedSet<int>([1, 2, 3]);

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = set[index]);
    }

    [Fact]
    public void Indexer_ShouldThrowArgumentOutOfRangeException_WhenTheSetIsEmpty()
    {
        var set = new RankedSet<int>();

        Assert.Throws<ArgumentOutOfRangeException>(() => _ = set[0]);
    }

    [Fact]
    public void IndexOf_ShouldReturnMinusOne_WhenTheElementIsAbsent()
    {
        var set = new RankedSet<int>([10, 20, 30]);

        Assert.Equal(1, set.IndexOf(20));
        Assert.Equal(-1, set.IndexOf(25));
        Assert.Equal(-1, set.IndexOf(5));
        Assert.Equal(-1, set.IndexOf(35));
    }

    [Fact]
    public void CountLessThan_ShouldRankAnAbsentElement()
    {
        var set = new RankedSet<int>([10, 20, 30]);

        Assert.Equal(0, set.CountLessThan(5));
        Assert.Equal(1, set.CountLessThan(20));
        Assert.Equal(2, set.CountLessThan(25));
        Assert.Equal(3, set.CountLessThan(35));
    }

    [Fact]
    public void CountLessThanOrEqual_ShouldIncludeAnExactMatch()
    {
        var set = new RankedSet<int>([10, 20, 30]);

        Assert.Equal(0, set.CountLessThanOrEqual(5));
        Assert.Equal(2, set.CountLessThanOrEqual(20));
        Assert.Equal(2, set.CountLessThanOrEqual(25));
        Assert.Equal(3, set.CountLessThanOrEqual(35));
    }

    [Fact]
    public void PositionalQueries_ShouldReturnTheEmptyAnswers_WhenTheSetIsEmpty()
    {
        var set = new RankedSet<int>();

        Assert.Equal(-1, set.IndexOf(1));
        Assert.Equal(0, set.CountLessThan(1));
        Assert.Equal(0, set.CountLessThanOrEqual(1));
    }

    // ---- min, max and the bounds -------------------------------------------------------------------

    [Fact]
    public void MinAndMax_ShouldThrowInvalidOperationException_WhenTheSetIsEmpty()
    {
        var set = new RankedSet<int>();

        Assert.Throws<InvalidOperationException>(() => _ = set.Min);
        Assert.Throws<InvalidOperationException>(() => _ = set.Max);
        Assert.False(set.TryGetMin(out int min));
        Assert.False(set.TryGetMax(out int max));
        Assert.Equal(0, min);
        Assert.Equal(0, max);
    }

    [Fact]
    public void TryGetMinAndMax_ShouldReturnTheEnds_WhenTheSetSpansSeveralBuckets()
    {
        var set = Ascending(LayoutSize);

        Assert.True(set.TryGetMin(out int min));
        Assert.True(set.TryGetMax(out int max));
        Assert.Equal(0, min);
        Assert.Equal(LayoutSize - 1, max);
    }

    [Fact]
    public void TryGetLowerBound_ShouldReturnTheSmallestElementNotBelowTheProbe()
    {
        var set = new RankedSet<int>([10, 20, 30]);

        Assert.True(set.TryGetLowerBound(20, out int exact));
        Assert.Equal(20, exact);

        Assert.True(set.TryGetLowerBound(11, out int above));
        Assert.Equal(20, above);

        Assert.True(set.TryGetLowerBound(-1, out int first));
        Assert.Equal(10, first);

        Assert.False(set.TryGetLowerBound(31, out int none));
        Assert.Equal(0, none);
    }

    [Fact]
    public void TryGetUpperBound_ShouldReturnTheSmallestElementStrictlyAbove()
    {
        var set = new RankedSet<int>([10, 20, 30]);

        Assert.True(set.TryGetUpperBound(20, out int afterExact));
        Assert.Equal(30, afterExact);

        Assert.True(set.TryGetUpperBound(11, out int afterGap));
        Assert.Equal(20, afterGap);

        Assert.False(set.TryGetUpperBound(30, out int afterLast));
        Assert.Equal(0, afterLast);

        Assert.False(set.TryGetUpperBound(31, out int beyond));
        Assert.Equal(0, beyond);
    }

    [Fact]
    public void TryGetUpperBound_ShouldCrossIntoTheNextBucket_WhenTheProbeIsABucketMaximum()
    {
        // 255 is the last element of the first bucket in the ascending layout, so its upper bound is the
        // first element of the second — the case an in-bucket search alone gets wrong.
        var set = Ascending(LayoutSize);

        Assert.True(set.TryGetUpperBound(255, out int bound));
        Assert.Equal(256, bound);

        Assert.False(set.TryGetUpperBound(LayoutSize - 1, out _));
    }

    // ---- removal -----------------------------------------------------------------------------------

    [Fact]
    public void Remove_ShouldReportWhetherAnElementWasRemoved()
    {
        var set = new RankedSet<int>([10, 20, 30]);

        Assert.True(set.Remove(20));
        Assert.False(set.Remove(20));
        Assert.False(set.Remove(5));
        Assert.False(set.Remove(35));
        Assert.Equal([10, 30], Items(set));
    }

    [Fact]
    public void Remove_ShouldUpdateTheBucketMaximum_WhenTheLargestElementGoes()
    {
        var set = Ascending(LayoutSize);

        Assert.True(set.Remove(LayoutSize - 1));

        Assert.Equal(LayoutSize - 2, set.Max);
        Assert.False(set.TryGetUpperBound(LayoutSize - 2, out _));
        AssertRanksAgreeWithOrder(set);
    }

    [Fact]
    public void RemoveAt_ShouldRemoveByRank()
    {
        var set = new RankedSet<int>([10, 20, 30]);

        set.RemoveAt(1);

        Assert.Equal([10, 30], Items(set));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void RemoveAt_ShouldThrowArgumentOutOfRangeException_WhenTheRankIsOutsideTheSet(int index)
    {
        var set = new RankedSet<int>([1, 2, 3]);

        Assert.Throws<ArgumentOutOfRangeException>(() => set.RemoveAt(index));
        Assert.Equal(3, set.Count);
    }

    [Fact]
    public void RemoveAt_ShouldStayCorrect_WhenTheRankFallsInALaterBucket()
    {
        var set = Ascending(LayoutSize);

        // A rank well past the first bucket, so the removal is driven entirely by the Fenwick descent.
        set.RemoveAt(700);

        Assert.False(set.Contains(700));
        Assert.Equal(LayoutSize - 1, set.Count);
        AssertRanksAgreeWithOrder(set);
    }

    [Fact]
    public void Clear_ShouldEmptyTheSet_AndLeaveItReusable()
    {
        var set = Ascending(LayoutSize);

        set.Clear();

        Assert.Equal(0, set.Count);
        Assert.Empty(set);
        Assert.False(set.Contains(0));

        // Re-filling has to work against the bucket arrays the clear left behind rather than fresh ones.
        set.Add(42);
        Assert.Equal([42], Items(set));
        Assert.Equal(0, set.IndexOf(42));
    }

    // ---- the structural events ---------------------------------------------------------------------

    [Fact]
    public void Remove_ShouldMergeIntoTheRightNeighbour_WhenBothFallBelowTheThreshold()
    {
        // The ascending layout is 256 / 256 / 256 / 432. Thinning the second bucket to exactly 128 leaves it
        // at the merge threshold — not below it — so nothing has moved yet; the first bucket dropping to 127
        // is then the step where 127 + 128 fits in half a bucket and the two collapse into one.
        var set = Ascending(LayoutSize);
        var oracle = new SortedSet<int>(Enumerable.Range(0, LayoutSize));

        for (int i = 256; i < 384; i++)
        {
            Assert.True(set.Remove(i));
            oracle.Remove(i);
        }

        for (int i = 0; i < 129; i++)
        {
            Assert.True(set.Remove(i));
            oracle.Remove(i);
        }

        Assert.Equal(oracle, set);
        AssertRanksAgreeWithOrder(set);
    }

    [Fact]
    public void Remove_ShouldMergeIntoTheLeftNeighbour_WhenThereIsNoRightOne()
    {
        // Same layout, worked from the other end: the last bucket has no right neighbour, so once it falls
        // below the threshold the only merge available is backwards into the third.
        var set = Ascending(LayoutSize);
        var oracle = new SortedSet<int>(Enumerable.Range(0, LayoutSize));

        for (int i = 512; i < 640; i++)
        {
            Assert.True(set.Remove(i));
            oracle.Remove(i);
        }

        for (int i = 768; i < 1073; i++)
        {
            Assert.True(set.Remove(i));
            oracle.Remove(i);
        }

        Assert.Equal(oracle, set);
        AssertRanksAgreeWithOrder(set);
    }

    [Fact]
    public void Remove_ShouldDropTheBucket_WhenAMiddleOneEmptiesWithNeighboursTooLargeToMerge()
    {
        // Both neighbours stay at 256, so no merge is ever available and the second bucket thins all the way
        // to nothing — the path where a bucket is dropped rather than absorbed.
        var set = Ascending(LayoutSize);
        var oracle = new SortedSet<int>(Enumerable.Range(0, LayoutSize));

        for (int i = 256; i < 512; i++)
        {
            Assert.True(set.Remove(i));
            oracle.Remove(i);
        }

        Assert.Equal(oracle, set);
        AssertRanksAgreeWithOrder(set);
    }

    [Fact]
    public void Remove_ShouldEmptyTheSet_WhenTheLastBucketIsDropped()
    {
        var set = new RankedSet<int>([3, 1, 2]);

        Assert.True(set.Remove(1));
        Assert.True(set.Remove(2));
        Assert.True(set.Remove(3));

        Assert.Equal(0, set.Count);
        Assert.False(set.TryGetMin(out _));
        Assert.Empty(set);

        // The set has to come back to life through the same path a never-used one takes.
        set.Add(9);
        Assert.Equal([9], Items(set));
    }

    [Fact]
    public void RemoveAndAdd_ShouldStayConsistentWithASortedSet_UnderSustainedChurn()
    {
        // Deterministic churn over a domain a few buckets wide: enough splits, merges and drops to shake out
        // a bucket index that only drifts after the structure has moved a few hundred times.
        var set = new RankedSet<int>();
        var oracle = new SortedSet<int>();
        var rand = new Random(20260823);

        for (int step = 0; step < 60_000; step++)
        {
            int value = rand.Next(0, 2000);
            if (rand.Next(0, 100) < 55)
                Assert.Equal(oracle.Add(value), set.TryAdd(value));
            else
                Assert.Equal(oracle.Remove(value), set.Remove(value));

            Assert.Equal(oracle.Count, set.Count);
        }

        Assert.Equal(oracle, set);
        AssertRanksAgreeWithOrder(set);
    }

    // ---- ranges ------------------------------------------------------------------------------------

    [Fact]
    public void EnumerateRange_ShouldReturnTheHalfOpenRange()
    {
        var set = new RankedSet<int>([10, 20, 30, 40, 50]);

        Assert.Equal<int[]>([20, 30, 40], [.. set.EnumerateRange(20, 50)]);
        Assert.Equal<int[]>([20, 30], [.. set.EnumerateRange(15, 35)]);
        Assert.Empty(set.EnumerateRange(21, 22));
    }

    [Fact]
    public void EnumerateRange_ShouldReturnNothing_WhenTheRangeStartsPastTheLastElement()
    {
        var set = new RankedSet<int>([10, 20, 30]);

        Assert.Empty(set.EnumerateRange(31, 40));
    }

    [Fact]
    public void EnumerateRange_ShouldRunToTheEnd_WhenTheUpperBoundIsPastTheLastElement()
    {
        var set = new RankedSet<int>([10, 20, 30]);

        Assert.Equal<int[]>([20, 30], [.. set.EnumerateRange(20, 100)]);
    }

    [Fact]
    public void EnumerateRange_ShouldSpanBuckets_WhenTheRangeCrossesABoundary()
    {
        var set = Ascending(LayoutSize);

        Assert.Equal(Enumerable.Range(200, 400).ToArray(), set.EnumerateRange(200, 600).ToArray());
    }

    [Fact]
    public void EnumerateRange_ShouldThrowArgumentException_WhenTheBoundsAreInverted()
    {
        var set = new RankedSet<int>([10, 20]);

        var thrown = Assert.Throws<ArgumentException>(() => set.EnumerateRange(20, 10));

        Assert.Equal("toExclusive", thrown.ParamName);
    }

    /// <summary>Reverses <see cref="Comparer{T}.Default"/>, to prove the order really is the comparer's.</summary>
    private readonly struct Descending : IComparer<int>
    {
        public int Compare(int x, int y) => y.CompareTo(x);
    }
}
