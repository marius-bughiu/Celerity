using System.Reflection;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural coverage for <see cref="RankedMultiSet{T, TComparer}"/>. Most of it is about what makes the
/// type different from <see cref="RankedSet{T, TComparer}"/>. Duplicates are kept, a run of equal elements
/// can span several buckets, and among equal elements the order is insertion order.
///
/// <para>
/// The structural events (a bucket splitting, two neighbours merging, a bucket being dropped) are reached by
/// explicit sequences, and every rank is re-checked afterwards. Several of those sequences are built from runs
/// of one repeated value. That is the case a set never has: the bucket maxima are equal across a boundary, so
/// any search for "a match" instead of "the first match" lands in the wrong bucket.
/// </para>
///
/// <para>
/// The randomized reconciliation against a stable sorted list lives in
/// <see cref="RankedMultiSetDifferentialTests"/>, and the enumerators and interfaces in
/// <see cref="RankedMultiSetEnumerationTests"/>.
/// </para>
/// </summary>
public class RankedMultiSetTests
{
    // A bucket holds 512 and splits into two of 256, so an ascending fill of 1,200 lays out as
    // 256 / 256 / 256 / 432, the same shape RankedSetTests works against.
    private const int LayoutSize = 1200;

    private static RankedMultiSet<int> Ascending(int count) => new(Enumerable.Range(0, count));

    private static int[] Items<TComparer>(RankedMultiSet<int, TComparer> set)
        where TComparer : struct, IComparer<int> => [.. set];

    // Reconciles the positional surface against the enumeration order, including the first/last occurrence
    // answers that only a multiset has.
    private static void AssertRanksAgreeWithOrder(RankedMultiSet<int> set)
    {
        int[] ordered = [.. set];

        Assert.Equal(ordered.Length, set.Count);

        for (int i = 0; i < ordered.Length; i++)
        {
            int value = ordered[i];
            int first = Array.IndexOf(ordered, value);
            int last = Array.LastIndexOf(ordered, value);

            Assert.Equal(value, set[i]);
            Assert.Equal(first, set.IndexOf(value));
            Assert.Equal(last, set.LastIndexOf(value));
            Assert.Equal(first, set.CountLessThan(value));
            Assert.Equal(last + 1, set.CountLessThanOrEqual(value));
            Assert.Equal(last - first + 1, set.CountOf(value));
        }
    }

    // ---- construction ------------------------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldProduceAnEmptyMultiSet_WhenNoSourceIsGiven()
    {
        var set = new RankedMultiSet<int>();

        Assert.Equal(0, set.Count);
        Assert.Empty(set);
    }

    [Fact]
    public void Constructor_ShouldOrderTheSource_AndKeepEveryDuplicate()
    {
        var set = new RankedMultiSet<int>([5, 1, 5, 3, 1, 5]);

        Assert.Equal([1, 1, 3, 5, 5, 5], Items(set));
        Assert.Equal(6, set.Count);
    }

    [Fact]
    public void Constructor_ShouldUseTheSuppliedComparer_WhenOneIsGiven()
    {
        var set = new RankedMultiSet<int, Descending>([1, 3, 2, 3], default(Descending));

        Assert.Equal([3, 3, 2, 1], Items(set));
    }

    [Fact]
    public void Constructor_ShouldUseTheSuppliedComparer_WhenTheMultiSetStartsEmpty()
    {
        var set = new RankedMultiSet<int, Descending>(default(Descending));
        set.Add(1);
        set.Add(2);
        set.Add(2);

        Assert.Equal([2, 2, 1], Items(set));
    }

    [Fact]
    public void ComparerParameterizedSourceConstructor_ShouldUseTheDefaultComparer()
    {
        var set = new RankedMultiSet<int, DefaultComparer<int>>([2, 1, 2]);

        Assert.Equal([1, 2, 2], Items(set));
    }

    [Fact]
    public void Comparer_ShouldReturnTheComparerTheMultiSetOrdersBy()
    {
        var set = new RankedMultiSet<int, Descending>(default(Descending));

        Assert.IsType<Descending>(set.Comparer);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenTheSourceIsNull()
    {
        Assert.Throws<ArgumentNullException>("source", () => new RankedMultiSet<int>(null!));
        Assert.Throws<ArgumentNullException>(
            "source", () => new RankedMultiSet<int, Descending>(null!, default(Descending)));
    }

    [Fact]
    public void Constructor_ShouldOrderNullFirst_AndKeepRepeatedNulls()
    {
        var set = new RankedMultiSet<string?>(["b", null, "a", null]);

        Assert.Equal(new string?[] { null, null, "a", "b" }, set.ToArray());
        Assert.Equal(2, set.CountOf(null));
        Assert.Equal(1, set.LastIndexOf(null));
    }

    // ---- duplicates and stability ------------------------------------------------------------------

    [Fact]
    public void Add_ShouldKeepEqualElementsInInsertionOrder()
    {
        // The comparer looks only at the key, so the ids tell the copies apart. They must come back in the
        // order they were added, and Remove must take the earliest of them.
        var set = new RankedMultiSet<(int Key, int Id), ByKey>(default(ByKey));
        set.Add((2, 0));
        set.Add((1, 1));
        set.Add((2, 2));
        set.Add((1, 3));
        set.Add((2, 4));

        Assert.Equal([(1, 1), (1, 3), (2, 0), (2, 2), (2, 4)], set.ToArray());

        Assert.True(set.Remove((2, -1)));
        Assert.Equal([(1, 1), (1, 3), (2, 2), (2, 4)], set.ToArray());
        Assert.Equal((2, 2), set[2]);
        Assert.Equal((1, 1), set.Min);
        Assert.Equal((2, 4), set.Max);
    }

    [Fact]
    public void Constructor_ShouldKeepEqualElementsInSourceOrder()
    {
        (int Key, int Id)[] source = [(3, 0), (1, 1), (3, 2), (1, 3)];

        var set = new RankedMultiSet<(int Key, int Id), ByKey>(source, default(ByKey));

        Assert.Equal([(1, 1), (1, 3), (3, 0), (3, 2)], set.ToArray());
    }

    [Fact]
    public void Stability_ShouldSurviveSplitsMergesAndTrimExcess()
    {
        // 3,000 copies of one key in 6 buckets, then thinned, merged and rebuilt. The ids must stay ascending
        // throughout, because a split, merge or rebuild that reorders a run would break first-in first-out.
        var set = new RankedMultiSet<(int Key, int Id), ByKey>(default(ByKey));
        for (int i = 0; i < 3000; i++)
            set.Add((7, i));

        AssertIdsAscending(set, 3000);

        for (int i = 0; i < 2500; i++)
            Assert.True(set.Remove((7, -1)));

        AssertIdsAscending(set, 500);
        Assert.Equal((7, 2500), set.Min);

        set.TrimExcess();
        AssertIdsAscending(set, 500);
        Assert.Equal((7, 2500), set[0]);
        Assert.Equal((7, 2999), set[499]);

        static void AssertIdsAscending(RankedMultiSet<(int Key, int Id), ByKey> set, int count)
        {
            var items = set.ToArray();
            Assert.Equal(count, items.Length);
            for (int i = 1; i < items.Length; i++)
                Assert.True(items[i - 1].Id < items[i].Id);
        }
    }

    [Fact]
    public void Stability_ShouldHold_WhenACrossBucketRunIsRemovedAndRefilled()
    {
        // Runs of 2 interleaved with a 1,500-copy run of 5 that spans several buckets. RemoveAll cuts the run
        // out bucket by bucket, then fresh copies go back in; every key's ids must still be ascending.
        var set = new RankedMultiSet<(int Key, int Id), ByKey>(default(ByKey));
        int id = 0;
        for (int i = 0; i < 1500; i++)
        {
            set.Add((5, id++));
            if (i % 3 == 0)
                set.Add((2, id++));
            if (i % 5 == 0)
                set.Add((8, id++));
        }

        Assert.Equal(1500, set.RemoveAll((5, -1)));
        for (int i = 0; i < 800; i++)
            set.Add((5, id++));

        var items = set.ToArray();
        Assert.Equal(500 + 300 + 800, items.Length);
        for (int i = 1; i < items.Length; i++)
        {
            Assert.True(items[i - 1].Key <= items[i].Key);
            if (items[i - 1].Key == items[i].Key)
                Assert.True(items[i - 1].Id < items[i].Id);
        }

        Assert.Equal(500, set.IndexOf((5, -1)));
        Assert.Equal(800, set.CountOf((5, -1)));
    }

    [Fact]
    public void Add_ShouldPlaceALowerValueAtTheFront_WhenEveryBucketHoldsTheSameLargerValue()
    {
        var set = new RankedMultiSet<int>(Enumerable.Repeat(7, LayoutSize));

        set.Add(3);
        set.Add(9);

        Assert.Equal(3, set[0]);
        Assert.Equal(9, set[LayoutSize + 1]);
        Assert.Equal(LayoutSize, set.CountOf(7));
        Assert.Equal(1, set.IndexOf(7));
        Assert.Equal(LayoutSize, set.LastIndexOf(7));
        AssertRanksAgreeWithOrder(set);
    }

    [Fact]
    public void QueriesOnARunThatSpansBuckets_ShouldFindItsFirstAndLastCopies()
    {
        // 0..299, then 1,000 copies of 300, then 301..599. The run of 300s spans at least three buckets, and
        // every one of those buckets has 300 as its maximum, so only a lower-bound search finds the first copy.
        int[] source = [.. Enumerable.Range(0, 300), .. Enumerable.Repeat(300, 1000), .. Enumerable.Range(301, 299)];
        var set = new RankedMultiSet<int>(source);

        Assert.Equal(300, set.IndexOf(300));
        Assert.Equal(1299, set.LastIndexOf(300));
        Assert.Equal(1000, set.CountOf(300));
        Assert.Equal(300, set.CountLessThan(300));
        Assert.Equal(1300, set.CountLessThanOrEqual(300));
        Assert.True(set.TryGetUpperBound(300, out int above));
        Assert.Equal(301, above);
        Assert.True(set.TryGetLowerBound(300, out int atOrAbove));
        Assert.Equal(300, atOrAbove);
        Assert.Equal(1000, set.EnumerateRange(300, 301).Count());
        AssertRanksAgreeWithOrder(set);
    }

    [Fact]
    public void CountOf_ShouldReturnZero_WhenTheValueIsAbsent()
    {
        var set = new RankedMultiSet<int>([1, 1, 3]);

        Assert.Equal(0, set.CountOf(2));
        Assert.Equal(0, set.CountOf(0));
        Assert.Equal(0, set.CountOf(4));
        Assert.Equal(2, set.CountOf(1));
    }

    [Fact]
    public void IndexOfAndLastIndexOf_ShouldReturnMinusOne_WhenTheValueIsAbsent()
    {
        var set = new RankedMultiSet<int>([1, 1, 3]);

        Assert.Equal(-1, set.IndexOf(2));
        Assert.Equal(-1, set.IndexOf(4));
        Assert.Equal(-1, set.IndexOf(0));
        Assert.Equal(-1, set.LastIndexOf(2));
        Assert.Equal(-1, set.LastIndexOf(4));
        Assert.Equal(-1, set.LastIndexOf(0));
        Assert.Equal(0, set.IndexOf(1));
        Assert.Equal(1, set.LastIndexOf(1));
        Assert.Equal(2, set.LastIndexOf(3));
    }

    // ---- the ordered and positional surface ---------------------------------------------------------

    [Fact]
    public void Add_ShouldKeepEveryRankConsistent_WhenElementsArriveInterleaved()
    {
        var set = new RankedMultiSet<int>();
        for (int i = 0; i < 3000; i++)
            set.Add((i * 7919) % 400);

        AssertRanksAgreeWithOrder(set);
    }

    [Fact]
    public void Add_ShouldGrowTheBucketIndex_WhenTheInitialSlotsAreExhausted()
    {
        // The slot array starts at four; this needs nine buckets, so it grows twice.
        var set = Ascending(2600);

        AssertRanksAgreeWithOrder(set);
    }

    [Fact]
    public void Add_ShouldGrowTheBucketsRatherThanSplitThem_WhenTheMultiSetOutgrowsTheStartingCapacity()
    {
        // Past 512 x 512 = 262,144 elements a full bucket is widened instead of split. Values repeat ten
        // times and arrive shuffled, so the widening happens across many buckets.
        const int Count = 300_000;
        var values = new int[Count];
        for (int i = 0; i < Count; i++)
            values[i] = i / 10;

        var rand = new Random(20261105);
        for (int i = Count - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }

        var set = new RankedMultiSet<int>(values);

        Assert.Equal(Count, set.Count);
        for (int i = 0; i < Count; i += 997)
            Assert.Equal(i / 10, set[i]);

        for (int v = 0; v < Count / 10; v += 101)
        {
            Assert.Equal(10, set.CountOf(v));
            Assert.Equal(v * 10, set.IndexOf(v));
        }

        Assert.Equal(10, set.RemoveAll(5));
        Assert.Equal(Count - 10, set.Count);
        Assert.Equal(6, set[50]);
    }

    [Fact]
    public void Contains_ShouldAnswerCorrectly_WhetherTheElementIsInsideOutsideOrAbsent()
    {
        var set = new RankedMultiSet<int>([2, 4, 4, 6]);

        Assert.True(set.Contains(4));
        Assert.False(set.Contains(3));
        Assert.False(set.Contains(1));
        Assert.False(set.Contains(7));
        Assert.False(new RankedMultiSet<int>().Contains(0));
    }

    [Fact]
    public void Indexer_ShouldReturnTheKthSmallest_CountingDuplicates()
    {
        var set = new RankedMultiSet<int>([4, 2, 4, 9]);

        Assert.Equal(2, set[0]);
        Assert.Equal(4, set[1]);
        Assert.Equal(4, set[2]);
        Assert.Equal(9, set[3]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(int.MinValue)]
    public void Indexer_ShouldThrowArgumentOutOfRangeException_WhenTheRankIsOutside(int index)
    {
        var set = new RankedMultiSet<int>([1, 1, 2]);

        Assert.Throws<ArgumentOutOfRangeException>("index", () => set[index]);
    }

    [Fact]
    public void CountLessThan_ShouldRankAnAbsentValue()
    {
        var set = new RankedMultiSet<int>([10, 20, 20, 30]);

        Assert.Equal(0, set.CountLessThan(5));
        Assert.Equal(1, set.CountLessThan(15));
        Assert.Equal(1, set.CountLessThan(20));
        Assert.Equal(3, set.CountLessThanOrEqual(20));
        Assert.Equal(4, set.CountLessThan(35));
        Assert.Equal(4, set.CountLessThanOrEqual(35));
    }

    [Fact]
    public void PositionalQueries_ShouldReturnTheEmptyAnswers_WhenTheMultiSetIsEmpty()
    {
        var set = new RankedMultiSet<int>();

        Assert.Equal(0, set.CountLessThan(1));
        Assert.Equal(0, set.CountLessThanOrEqual(1));
        Assert.Equal(0, set.CountOf(1));
        Assert.Equal(-1, set.IndexOf(1));
        Assert.Equal(-1, set.LastIndexOf(1));
        Assert.False(set.TryGetLowerBound(1, out _));
        Assert.False(set.TryGetUpperBound(1, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => set[0]);
    }

    [Fact]
    public void MinAndMax_ShouldThrowInvalidOperationException_WhenTheMultiSetIsEmpty()
    {
        var set = new RankedMultiSet<int>();

        Assert.Throws<InvalidOperationException>(() => set.Min);
        Assert.Throws<InvalidOperationException>(() => set.Max);
        Assert.False(set.TryGetMin(out int min));
        Assert.False(set.TryGetMax(out int max));
        Assert.Equal(0, min);
        Assert.Equal(0, max);
    }

    [Fact]
    public void TryGetMinAndMax_ShouldReturnTheEnds_WhenTheMultiSetSpansSeveralBuckets()
    {
        var set = Ascending(LayoutSize);

        Assert.True(set.TryGetMin(out int min));
        Assert.True(set.TryGetMax(out int max));
        Assert.Equal(0, min);
        Assert.Equal(LayoutSize - 1, max);
    }

    [Fact]
    public void Bounds_ShouldReturnTheFirstElementNotBelow_AndStrictlyAbove()
    {
        var set = new RankedMultiSet<int>([10, 20, 20, 30]);

        Assert.True(set.TryGetLowerBound(20, out int lower));
        Assert.Equal(20, lower);
        Assert.True(set.TryGetLowerBound(21, out lower));
        Assert.Equal(30, lower);
        Assert.False(set.TryGetLowerBound(31, out lower));
        Assert.Equal(0, lower);

        Assert.True(set.TryGetUpperBound(20, out int upper));
        Assert.Equal(30, upper);
        Assert.True(set.TryGetUpperBound(5, out upper));
        Assert.Equal(10, upper);
        Assert.False(set.TryGetUpperBound(30, out upper));
        Assert.Equal(0, upper);
    }

    [Fact]
    public void TryGetUpperBound_ShouldCrossIntoTheNextBucket_WhenTheProbeIsABucketMaximum()
    {
        var set = Ascending(LayoutSize);

        // 255 is the first bucket's maximum.
        Assert.True(set.TryGetUpperBound(255, out int upper));
        Assert.Equal(256, upper);
    }

    // ---- removal -----------------------------------------------------------------------------------

    [Fact]
    public void Remove_ShouldRemoveOneCopy_AndReportWhetherOneWasFound()
    {
        var set = new RankedMultiSet<int>([1, 2, 2, 3]);

        Assert.True(set.Remove(2));
        Assert.Equal([1, 2, 3], Items(set));
        Assert.False(set.Remove(5));
        Assert.False(set.Remove(0));
        Assert.False(set.Remove(4));
        Assert.True(set.Remove(2));
        Assert.False(set.Remove(2));
        Assert.Equal([1, 3], Items(set));
    }

    [Fact]
    public void Remove_ShouldUpdateTheBucketMaximum_WhenTheLargestElementGoes()
    {
        var set = new RankedMultiSet<int>([1, 2, 3, 3]);

        Assert.True(set.Remove(3));
        Assert.Equal(3, set.Max);
        Assert.True(set.Remove(3));
        Assert.Equal(2, set.Max);
        Assert.False(set.Contains(3));
        Assert.Equal(2, set.CountLessThanOrEqual(3));
    }

    [Fact]
    public void RemoveAll_ShouldRemoveEveryCopy_AndReturnHowManyWent()
    {
        var set = new RankedMultiSet<int>([1, 2, 2, 2, 3]);

        Assert.Equal(3, set.RemoveAll(2));
        Assert.Equal([1, 3], Items(set));
        Assert.Equal(0, set.RemoveAll(2));
        Assert.Equal(0, set.RemoveAll(9));
        Assert.Equal(0, new RankedMultiSet<int>().RemoveAll(1));
    }

    [Fact]
    public void RemoveAll_ShouldRemoveARunThatSpansSeveralBuckets()
    {
        // 0..299, 1,500 copies of 300, 301..599: the run fills several whole buckets and parts of two more,
        // so it is removed in several passes, dropping whole buckets and trimming the ends of two others.
        int[] source = [.. Enumerable.Range(0, 300), .. Enumerable.Repeat(300, 1500), .. Enumerable.Range(301, 299)];
        var set = new RankedMultiSet<int>(source);

        Assert.Equal(1500, set.RemoveAll(300));

        Assert.Equal([.. Enumerable.Range(0, 300), .. Enumerable.Range(301, 299)], Items(set));
        Assert.False(set.Contains(300));
        AssertRanksAgreeWithOrder(set);
    }

    [Fact]
    public void RemoveAll_ShouldEmptyTheMultiSet_WhenEveryElementIsTheSameValue()
    {
        var set = new RankedMultiSet<int>(Enumerable.Repeat(4, 2000));

        Assert.Equal(2000, set.RemoveAll(4));
        Assert.Equal(0, set.Count);
        Assert.Empty(set);

        set.Add(4);
        Assert.Equal([4], Items(set));
    }

    [Fact]
    public void RemoveAll_ShouldInvalidateEnumerators_OnlyWhenItRemovesSomething()
    {
        var set = new RankedMultiSet<int>([1, 2, 2]);

        var untouched = set.GetEnumerator();
        Assert.Equal(0, set.RemoveAll(5));
        Assert.True(untouched.MoveNext());

        Assert.Equal(2, set.RemoveAll(2));
        Assert.Throws<InvalidOperationException>(() => untouched.MoveNext());
    }

    [Fact]
    public void RemoveAt_ShouldRemoveByRank()
    {
        var set = new RankedMultiSet<int>([5, 1, 5, 3]);

        set.RemoveAt(2);

        Assert.Equal([1, 3, 5], Items(set));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void RemoveAt_ShouldThrowArgumentOutOfRangeException_WhenTheRankIsOutside(int index)
    {
        var set = new RankedMultiSet<int>([1, 2, 2, 3]);

        Assert.Throws<ArgumentOutOfRangeException>("index", () => set.RemoveAt(index));
    }

    [Fact]
    public void RemoveAt_ShouldStayCorrect_WhenTheRankFallsInALaterBucket()
    {
        var set = Ascending(LayoutSize);

        set.RemoveAt(1000);

        Assert.Equal(1001, set[1000]);
        AssertRanksAgreeWithOrder(set);
    }

    // ---- the structural events ---------------------------------------------------------------------

    [Fact]
    public void Remove_ShouldMergeIntoTheRightNeighbour_WhenBothFallBelowTheThreshold()
    {
        var set = Ascending(LayoutSize);
        var oracle = Enumerable.Range(0, LayoutSize).ToList();

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

        Assert.Equal(oracle, Items(set));
        AssertRanksAgreeWithOrder(set);
    }

    [Fact]
    public void Remove_ShouldMergeIntoTheLeftNeighbour_WhenThereIsNoRightOne()
    {
        var set = Ascending(LayoutSize);
        var oracle = Enumerable.Range(0, LayoutSize).ToList();

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

        Assert.Equal(oracle, Items(set));
        AssertRanksAgreeWithOrder(set);
    }

    [Fact]
    public void Remove_ShouldDropTheBucket_WhenAMiddleOneEmptiesWithNeighboursTooLargeToMerge()
    {
        var set = Ascending(LayoutSize);
        var oracle = Enumerable.Range(0, LayoutSize).ToList();

        for (int i = 256; i < 512; i++)
        {
            Assert.True(set.Remove(i));
            oracle.Remove(i);
        }

        Assert.Equal(oracle, Items(set));
        AssertRanksAgreeWithOrder(set);
    }

    [Fact]
    public void Remove_ShouldEmptyTheMultiSet_WhenTheLastBucketIsDropped()
    {
        var set = new RankedMultiSet<int>([3, 1, 3]);

        Assert.True(set.Remove(1));
        Assert.True(set.Remove(3));
        Assert.True(set.Remove(3));

        Assert.Equal(0, set.Count);
        Assert.False(set.TryGetMin(out _));
        Assert.Empty(set);

        set.Add(9);
        Assert.Equal([9], Items(set));
    }

    [Fact]
    public void RemoveAndAdd_ShouldStayConsistentWithASortedList_UnderSustainedChurn()
    {
        var set = new RankedMultiSet<int>();
        var oracle = new List<int>();
        var rand = new Random(594);

        for (int step = 0; step < 20_000; step++)
        {
            int value = rand.Next(300);
            if (rand.Next(10) < 6)
            {
                set.Add(value);
                int at = oracle.BinarySearch(value);
                oracle.Insert(at < 0 ? ~at : at, value);
            }
            else
            {
                Assert.Equal(oracle.Remove(value), set.Remove(value));
            }
        }

        Assert.Equal(oracle, Items(set));
        AssertRanksAgreeWithOrder(set);
    }

    // ---- Clear and TrimExcess ----------------------------------------------------------------------

    [Fact]
    public void Clear_ShouldEmptyTheMultiSet_AndLeaveItReusable()
    {
        var set = Ascending(LayoutSize);
        var live = set.GetEnumerator();

        set.Clear();

        Assert.Equal(0, set.Count);
        Assert.Empty(set);
        Assert.Throws<InvalidOperationException>(() => live.MoveNext());
        Assert.Equal(0, SlotCount(set));
        Assert.Equal(1, TreeLength(set));

        set.Add(2);
        set.Add(2);
        Assert.Equal([2, 2], Items(set));
    }

    [Fact]
    public void Clear_ShouldReleaseTheSlotArrays_WhenRemovalHadAlreadyEmptiedTheMultiSet()
    {
        var set = new RankedMultiSet<int>(Enumerable.Repeat(1, 3000));
        Assert.Equal(3000, set.RemoveAll(1));
        Assert.NotEqual(0, SlotCount(set));

        var overEmpty = set.GetEnumerator();
        set.Clear();

        Assert.Equal(0, SlotCount(set));
        Assert.False(overEmpty.MoveNext());

        // A never-used multiset has nothing to release.
        var neverUsed = new RankedMultiSet<int>();
        neverUsed.Clear();
        Assert.Equal(0, SlotCount(neverUsed));
    }

    [Fact]
    public void Add_ShouldReuseTheRetainedSlots_WhenRemovalEmptiedTheMultiSet()
    {
        var set = new RankedMultiSet<int>([1, 2]);
        Assert.Equal(1, set.RemoveAll(1));
        Assert.Equal(1, set.RemoveAll(2));
        int slots = SlotCount(set);

        set.Add(3);

        Assert.Equal(slots, SlotCount(set));
        Assert.Equal([3], Items(set));
    }

    [Fact]
    public void TrimExcess_ShouldRebuildAtTheCurrentSize_AndPreserveEveryRank()
    {
        var set = new RankedMultiSet<int>(Enumerable.Range(0, 300_000).Select(i => i / 3));

        for (int v = 0; v < 100_000; v++)
        {
            if (v % 100 != 0)
                Assert.Equal(3, set.RemoveAll(v));
        }

        int[] before = [.. set];
        Assert.Equal(3000, before.Length);
        AssertRanksAgreeWithOrder(set);

        set.TrimExcess();

        Assert.Equal(before, Items(set));
        AssertRanksAgreeWithOrder(set);

        set.Add(100);
        Assert.Equal(4, set.CountOf(100));
        Assert.Equal(3, set.IndexOf(100));
        AssertRanksAgreeWithOrder(set);
    }

    [Fact]
    public void TrimExcess_ShouldBeANoOp_WhenTheMultiSetIsEmpty()
    {
        var neverUsed = new RankedMultiSet<int>();
        neverUsed.TrimExcess();
        Assert.Empty(neverUsed);

        var emptied = new RankedMultiSet<int>([1, 1]);
        Assert.Equal(2, emptied.RemoveAll(1));
        var overEmpty = emptied.GetEnumerator();

        emptied.TrimExcess();

        Assert.Equal(0, SlotCount(emptied));
        Assert.False(overEmpty.MoveNext());
        emptied.Add(7);
        Assert.Equal([7], Items(emptied));
    }

    [Fact]
    public void TrimExcess_ShouldInvalidateActiveEnumerators_WhenItRebuilds()
    {
        var set = new RankedMultiSet<int>([1, 2, 2]);
        var live = set.GetEnumerator();
        Assert.True(live.MoveNext());

        set.TrimExcess();

        Assert.Throws<InvalidOperationException>(() => live.MoveNext());
    }

    // ---- EnumerateRange ----------------------------------------------------------------------------

    [Fact]
    public void EnumerateRange_ShouldReturnTheHalfOpenRange_WithEveryDuplicate()
    {
        var set = new RankedMultiSet<int>([1, 2, 2, 3, 3, 4]);

        Assert.Equal([2, 2, 3, 3], set.EnumerateRange(2, 4).ToArray());
        Assert.Empty(set.EnumerateRange(2, 2));
    }

    [Fact]
    public void EnumerateRange_ShouldReturnNothing_WhenTheRangeStartsPastTheLastElement()
    {
        var set = new RankedMultiSet<int>([1, 2]);

        Assert.Empty(set.EnumerateRange(5, 9));
    }

    [Fact]
    public void EnumerateRange_ShouldRunToTheEnd_WhenTheUpperBoundIsPastTheLastElement()
    {
        var set = Ascending(LayoutSize);

        Assert.Equal(Enumerable.Range(1000, 200), set.EnumerateRange(1000, 5000));
    }

    [Fact]
    public void EnumerateRange_ShouldSpanBuckets_WhenTheRangeCrossesABoundary()
    {
        var set = Ascending(LayoutSize);

        Assert.Equal(Enumerable.Range(250, 20), set.EnumerateRange(250, 270));
    }

    [Fact]
    public void EnumerateRange_ShouldThrowArgumentException_WhenTheBoundsAreInverted()
    {
        var set = new RankedMultiSet<int>([1]);

        Assert.Throws<ArgumentException>("toExclusive", () => set.EnumerateRange(5, 4));
    }

    // ---- helpers -----------------------------------------------------------------------------------

    private static int SlotCount<T>(RankedMultiSet<T> set) => Field<T, T[][]>(set, "_buckets").Length;

    private static int TreeLength<T>(RankedMultiSet<T> set) => Field<T, int[]>(set, "_tree").Length;

    private static TField Field<T, TField>(RankedMultiSet<T> set, string name) =>
        (TField)typeof(RankedMultiSet<T, DefaultComparer<T>>)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(set)!;

    internal readonly struct Descending : IComparer<int>
    {
        public int Compare(int x, int y) => y.CompareTo(x);
    }

    // Orders by the key alone, so two elements with the same key and different ids are equal to the
    // multiset and only their insertion order separates them.
    internal readonly struct ByKey : IComparer<(int Key, int Id)>
    {
        public int Compare((int Key, int Id) x, (int Key, int Id) y) => x.Key.CompareTo(y.Key);
    }
}
