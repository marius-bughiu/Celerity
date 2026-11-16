using System.Reflection;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural coverage for <see cref="RankedDictionary{TKey, TValue, TComparer}"/>: the ordered map surface it
/// shares with <see cref="BTreeDictionary{TKey, TValue, TComparer}"/>, and the positional surface that is the
/// reason it exists — <see cref="RankedDictionary{TKey, TValue, TComparer}.IndexOfKey"/>,
/// <see cref="RankedDictionary{TKey, TValue, TComparer}.GetKeyAtIndex"/>,
/// <see cref="RankedDictionary{TKey, TValue, TComparer}.GetValueAtIndex"/>,
/// <see cref="RankedDictionary{TKey, TValue, TComparer}.SetValueAtIndex"/> and
/// <see cref="RankedDictionary{TKey, TValue, TComparer}.RemoveAt"/>.
///
/// <para>
/// As with <see cref="RankedSetTests"/>, the thing that decides whether this type is right is whether the
/// <i>rank</i> — and here also the value beside each key — still agrees with the enumeration after the
/// buckets have moved underneath it. So the structural tests drive a split, a merge in each direction and a
/// bucket drop on purpose and reconcile every rank and every value afterwards. The value array is the part
/// <see cref="RankedSet{T, TComparer}"/> does not have: a memmove, split or merge that moved the keys and
/// not the values would leave every rank right and every value wrong, which is why each reconciliation checks
/// the value at each rank, not only the key.
/// </para>
///
/// <para>
/// The randomized reconciliation against <see cref="SortedDictionary{TKey, TValue}"/> lives in
/// <see cref="RankedDictionaryDifferentialTests"/>, and the enumerator, view and interface surfaces in
/// <see cref="RankedDictionaryEnumerationTests"/>.
/// </para>
/// </summary>
public class RankedDictionaryTests
{
    // Same shape as RankedSetTests: a bucket holds 512 and splits into two of 256, so an ascending fill of
    // 1,200 keys lays out as 256 / 256 / 256 / 432.
    private const int LayoutSize = 1200;

    // Each key's value is derived from it, so a value that drifted to the wrong slot is caught by a rank check.
    private static int ValueOf(int key) => (key * 7) + 1;

    private static RankedDictionary<int, int> Ascending(int count)
    {
        var map = new RankedDictionary<int, int>();
        for (int i = 0; i < count; i++)
            map.Add(i, ValueOf(i));

        return map;
    }

    private static SortedDictionary<int, int> AscendingOracle(int count)
    {
        var oracle = new SortedDictionary<int, int>();
        for (int i = 0; i < count; i++)
            oracle.Add(i, ValueOf(i));

        return oracle;
    }

    // Reconciles the whole positional surface — key and value at every rank — against the enumeration order.
    private static void AssertRanksAgreeWithOrder(RankedDictionary<int, int> map)
    {
        KeyValuePair<int, int>[] ordered = [.. map];

        Assert.Equal(ordered.Length, map.Count);

        for (int i = 0; i < ordered.Length; i++)
        {
            (int key, int value) = ordered[i];
            Assert.Equal(key, map.GetKeyAtIndex(i));
            Assert.Equal(value, map.GetValueAtIndex(i));
            Assert.Equal(ordered[i], map.GetEntryAtIndex(i));
            Assert.Equal(i, map.IndexOfKey(key));
            Assert.Equal(i, map.CountLessThan(key));
            Assert.Equal(i + 1, map.CountLessThanOrEqual(key));
            Assert.Equal(value, map[key]);
        }
    }

    private static void AssertMatches(SortedDictionary<int, int> oracle, RankedDictionary<int, int> map)
    {
        Assert.Equal(oracle, map);
        AssertRanksAgreeWithOrder(map);
    }

    // ---- construction ------------------------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldProduceAnEmptyDictionary_WhenNoSourceIsGiven()
    {
        var map = new RankedDictionary<int, string>();

        Assert.Equal(0, map.Count);
        Assert.Empty(map);
        Assert.False(map.TryGetMin(out _));
        Assert.False(map.TryGetMax(out _));
    }

    [Fact]
    public void Constructor_ShouldOrderTheSource_WhenItIsUnsorted()
    {
        var map = new RankedDictionary<int, string>(
        [
            new(5, "five"), new(1, "one"), new(3, "three"),
        ]);

        Assert.Equal([1, 3, 5], map.Keys.ToArray());
        Assert.Equal(new string?[] { "one", "three", "five" }, map.Values.ToArray());
    }

    [Fact]
    public void Constructor_ShouldUseTheSuppliedComparer_WhenOneIsGiven()
    {
        var map = new RankedDictionary<int, string, DescendingInt>(
            [new(1, "a"), new(3, "c"), new(2, "b")], default);

        Assert.Equal([3, 2, 1], map.Keys.ToArray());
        Assert.Equal(0, map.IndexOfKey(3));
        Assert.Equal("a", map.GetValueAtIndex(2));
    }

    [Fact]
    public void Constructor_ShouldUseTheSuppliedComparer_WhenTheDictionaryStartsEmpty()
    {
        var map = new RankedDictionary<string, int, OrdinalIgnoreCase>(new OrdinalIgnoreCase());
        map["b"] = 2;
        map["A"] = 1;

        Assert.True(map.ContainsKey("a"));
        Assert.Equal(0, map.IndexOfKey("a"));

        // Same key under the comparer, so this is an overwrite, not a second entry.
        map["B"] = 20;
        Assert.Equal(2, map.Count);
        Assert.Equal(20, map["b"]);
    }

    [Fact]
    public void Comparer_ShouldReturnTheComparerTheDictionaryOrdersBy()
    {
        var map = new RankedDictionary<int, int, DescendingInt>(new DescendingInt());

        Assert.IsType<DescendingInt>(map.Comparer);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenTheSourceIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RankedDictionary<int, int, DefaultComparer<int>>(null!, default));
    }

    [Fact]
    public void Constructor_ShouldOrderANullKeyFirst_WhenTheKeysAreReferences()
    {
        // DefaultComparer orders null before every non-null key, so it is an ordinary key here — unlike
        // SortedDictionary, which rejects it.
        var map = new RankedDictionary<string?, int>();
        map["b"] = 2;
        map[null] = 0;
        map["a"] = 1;

        Assert.Null(map.GetKeyAtIndex(0));
        Assert.Equal(0, map.IndexOfKey(null));
        Assert.Equal(0, map[null]);
        Assert.True(map.Remove(null));
        Assert.Equal("a", map.Min.Key);
    }

    // ---- add, overwrite and lookup -----------------------------------------------------------------

    [Fact]
    public void Add_ShouldThrowArgumentException_WhenTheKeyIsAlreadyPresent()
    {
        var map = new RankedDictionary<int, string> { [1] = "one" };

        var ex = Assert.Throws<ArgumentException>(() => map.Add(1, "uno"));

        Assert.Equal("key", ex.ParamName);
        Assert.Equal("one", map[1]);
    }

    [Fact]
    public void TryAdd_ShouldReportWhetherTheKeyWasNew_AndNeverOverwrite()
    {
        var map = new RankedDictionary<int, string>();

        Assert.True(map.TryAdd(1, "one"));
        Assert.False(map.TryAdd(1, "uno"));
        Assert.Equal("one", map[1]);
    }

    [Fact]
    public void IndexerSetter_ShouldInsertWhenAbsent_AndOverwriteWhenPresent()
    {
        var map = new RankedDictionary<int, string>();

        map[2] = "two";
        map[1] = "one";
        map[2] = "TWO";

        Assert.Equal(2, map.Count);
        Assert.Equal("TWO", map[2]);
        Assert.Equal(1, map.IndexOfKey(2));
    }

    [Fact]
    public void IndexerGetter_ShouldThrowKeyNotFoundException_WhenTheKeyIsAbsent()
    {
        var map = Ascending(10);

        Assert.Throws<KeyNotFoundException>(() => map[-1]);
        Assert.Throws<KeyNotFoundException>(() => map[100]);
    }

    [Fact]
    public void TryGetValue_ShouldAnswerCorrectly_WhetherTheKeyIsInsideOutsideOrAbsent()
    {
        var map = new RankedDictionary<int, int>();
        Assert.False(map.TryGetValue(0, out int none));
        Assert.Equal(0, none);

        for (int i = 0; i < LayoutSize; i += 2)
            map.Add(i, ValueOf(i));

        Assert.True(map.TryGetValue(600, out int hit));
        Assert.Equal(ValueOf(600), hit);
        Assert.False(map.TryGetValue(601, out _));        // inside the range, absent
        Assert.False(map.TryGetValue(-1, out _));         // before the first key
        Assert.False(map.TryGetValue(LayoutSize, out _)); // past the last key
        Assert.True(map.ContainsKey(0));
        Assert.False(map.ContainsKey(1));
    }

    [Fact]
    public void Add_ShouldKeepEveryRankAndValueConsistent_WhenKeysArriveDescending()
    {
        var map = new RankedDictionary<int, int>();
        for (int i = LayoutSize - 1; i >= 0; i--)
            map.Add(i, ValueOf(i));

        AssertMatches(AscendingOracle(LayoutSize), map);
    }

    [Fact]
    public void Add_ShouldKeepEveryRankAndValueConsistent_WhenKeysArriveInterleaved()
    {
        // Odd keys first, then evens: every even insert lands between two existing keys, across every bucket,
        // and each split cuts a bucket whose values must travel with its keys.
        var map = new RankedDictionary<int, int>();
        for (int i = 1; i < LayoutSize; i += 2)
            map.Add(i, ValueOf(i));
        for (int i = 0; i < LayoutSize; i += 2)
            map.Add(i, ValueOf(i));

        AssertMatches(AscendingOracle(LayoutSize), map);
    }

    [Fact]
    public void Add_ShouldGrowTheBucketIndex_WhenTheInitialSlotsAreExhausted()
    {
        // The slot array starts at four; this needs nine buckets, so it grows twice.
        var map = Ascending(2600);

        AssertMatches(AscendingOracle(2600), map);
    }

    [Fact]
    public void Add_ShouldGrowTheBucketsRatherThanSplitThem_WhenTheMapOutgrowsTheStartingCapacity()
    {
        // Past 512 x 512 = 262,144 entries a full bucket is widened in place instead of split. Shuffled keys
        // spread the widening across many buckets; the value array has to widen with the key array.
        const int Count = 300_000;
        var keys = new int[Count];
        for (int i = 0; i < Count; i++)
            keys[i] = i;

        var rand = new Random(20261116);
        for (int i = Count - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (keys[i], keys[j]) = (keys[j], keys[i]);
        }

        var map = new RankedDictionary<int, int>();
        foreach (int key in keys)
            map.Add(key, ValueOf(key));

        Assert.Equal(Count, map.Count);
        Assert.Equal(0, map.Min.Key);
        Assert.Equal(Count - 1, map.Max.Key);

        for (int i = 0; i < Count; i += 997)
        {
            Assert.Equal(i, map.GetKeyAtIndex(i));
            Assert.Equal(ValueOf(i), map.GetValueAtIndex(i));
            Assert.Equal(i, map.IndexOfKey(i));
        }

        for (int i = 0; i < Count; i += 3)
            Assert.True(map.Remove(i));

        Assert.Equal(Count - ((Count + 2) / 3), map.Count);
        Assert.Equal(new KeyValuePair<int, int>(1, ValueOf(1)), map.GetEntryAtIndex(0));
        Assert.Equal(new KeyValuePair<int, int>(4, ValueOf(4)), map.GetEntryAtIndex(2));
    }

    // ---- the positional surface --------------------------------------------------------------------

    [Fact]
    public void IndexOfKey_ShouldReturnMinusOne_WhenTheKeyIsAbsent()
    {
        var map = new RankedDictionary<int, int>();
        Assert.Equal(-1, map.IndexOfKey(5));

        map[10] = 1;
        map[20] = 2;

        Assert.Equal(-1, map.IndexOfKey(15)); // inside the range
        Assert.Equal(-1, map.IndexOfKey(25)); // past the last key
        Assert.Equal(1, map.IndexOfKey(20));
    }

    [Fact]
    public void CountLessThan_ShouldRankAnAbsentKey()
    {
        var map = Ascending(LayoutSize);

        Assert.Equal(0, map.CountLessThan(-5));
        Assert.Equal(LayoutSize, map.CountLessThan(LayoutSize + 5));
        Assert.Equal(LayoutSize, map.CountLessThanOrEqual(LayoutSize + 5));

        Assert.True(map.Remove(700));
        Assert.Equal(700, map.CountLessThan(700));
        Assert.Equal(700, map.CountLessThanOrEqual(700));
        Assert.Equal(701, map.CountLessThanOrEqual(701));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void PositionalAccessors_ShouldThrowArgumentOutOfRangeException_WhenTheRankIsOutsideTheMap(int index)
    {
        var map = Ascending(3);

        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => map.GetKeyAtIndex(index)).ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => map.GetValueAtIndex(index));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.GetEntryAtIndex(index));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.SetValueAtIndex(index, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.RemoveAt(index));
        Assert.Equal(3, map.Count);
    }

    [Fact]
    public void PositionalAccessors_ShouldThrowArgumentOutOfRangeException_WhenTheMapIsEmpty()
    {
        var map = new RankedDictionary<int, int>();

        Assert.Throws<ArgumentOutOfRangeException>(() => map.GetKeyAtIndex(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.RemoveAt(0));
    }

    [Fact]
    public void SetValueAtIndex_ShouldReplaceTheValue_AndLeaveTheKeyAndRank()
    {
        var map = Ascending(LayoutSize);

        map.SetValueAtIndex(900, -1);

        Assert.Equal(-1, map[900]);
        Assert.Equal(900, map.GetKeyAtIndex(900));
        Assert.Equal(LayoutSize, map.Count);
    }

    [Fact]
    public void RemoveAt_ShouldRemoveByRank_InEveryBucket()
    {
        var map = Ascending(LayoutSize);
        var oracle = AscendingOracle(LayoutSize);

        // Ranks landing in the first, a middle and the last bucket; each RemoveAt is checked by what it took.
        foreach (int target in new[] { 0, 300, 700, 1100, -1 }) // -1: the last rank, read after the earlier removals
        {
            int rank = target < 0 ? map.Count - 1 : target;
            int key = map.GetKeyAtIndex(rank);
            map.RemoveAt(rank);
            Assert.True(oracle.Remove(key));
        }

        AssertMatches(oracle, map);
    }

    // ---- min, max and bounds -----------------------------------------------------------------------

    [Fact]
    public void MinAndMax_ShouldThrowInvalidOperationException_WhenTheDictionaryIsEmpty()
    {
        var map = new RankedDictionary<int, int>();

        Assert.Throws<InvalidOperationException>(() => map.Min);
        Assert.Throws<InvalidOperationException>(() => map.Max);
    }

    [Fact]
    public void MinAndMax_ShouldReturnTheEndEntries_WhenTheMapSpansSeveralBuckets()
    {
        var map = Ascending(LayoutSize);

        Assert.Equal(new KeyValuePair<int, int>(0, ValueOf(0)), map.Min);
        Assert.Equal(new KeyValuePair<int, int>(LayoutSize - 1, ValueOf(LayoutSize - 1)), map.Max);
    }

    [Fact]
    public void TryGetLowerBound_ShouldReturnTheFirstEntryNotBelowTheProbe()
    {
        var map = new RankedDictionary<int, string> { [10] = "ten", [20] = "twenty" };

        Assert.True(map.TryGetLowerBound(10, out var exact));
        Assert.Equal(new KeyValuePair<int, string?>(10, "ten"), exact);
        Assert.True(map.TryGetLowerBound(11, out var next));
        Assert.Equal(20, next.Key);
        Assert.True(map.TryGetLowerBound(-100, out var first));
        Assert.Equal(10, first.Key);
        Assert.False(map.TryGetLowerBound(21, out var none));
        Assert.Equal(default, none);
    }

    [Fact]
    public void TryGetUpperBound_ShouldReturnTheFirstEntryStrictlyAbove()
    {
        var map = new RankedDictionary<int, string> { [10] = "ten", [20] = "twenty" };

        Assert.True(map.TryGetUpperBound(10, out var above));
        Assert.Equal(new KeyValuePair<int, string?>(20, "twenty"), above);
        Assert.True(map.TryGetUpperBound(5, out var first));
        Assert.Equal(10, first.Key);
        Assert.False(map.TryGetUpperBound(20, out _)); // exact hit on the last key
        Assert.False(map.TryGetUpperBound(25, out _)); // past every key
    }

    [Fact]
    public void TryGetUpperBound_ShouldCrossIntoTheNextBucket_WhenTheProbeIsABucketMaximum()
    {
        // The ascending layout's first bucket ends at 255, so its upper bound is the next bucket's first key.
        var map = Ascending(LayoutSize);

        Assert.True(map.TryGetUpperBound(255, out var bound));
        Assert.Equal(new KeyValuePair<int, int>(256, ValueOf(256)), bound);
    }

    // ---- removal and the structural events --------------------------------------------------------

    [Fact]
    public void Remove_ShouldReportWhetherAnEntryWasRemoved_AndReturnItsValue()
    {
        var map = Ascending(LayoutSize);

        Assert.True(map.Remove(500, out int removed));
        Assert.Equal(ValueOf(500), removed);
        Assert.False(map.Remove(500, out int missing));
        Assert.Equal(0, missing);
        Assert.False(map.Remove(-1));
        Assert.False(map.Remove(LayoutSize));
        Assert.False(new RankedDictionary<int, int>().Remove(0));
    }

    [Fact]
    public void Remove_ShouldUpdateTheBucketMaximum_WhenTheLargestKeyGoes()
    {
        var map = Ascending(LayoutSize);

        Assert.True(map.Remove(255)); // the first bucket's maximum

        Assert.True(map.TryGetUpperBound(254, out var bound));
        Assert.Equal(256, bound.Key);
        Assert.False(map.ContainsKey(255));
        AssertRanksAgreeWithOrder(map);
    }

    [Fact]
    public void Remove_ShouldMergeIntoTheRightNeighbour_WhenBothFallBelowTheThreshold()
    {
        // Thin the second bucket to 128 (at the threshold, so nothing moves), then the first to 127: 127 + 128
        // fits in half a bucket and the two collapse into one, values and all.
        var map = Ascending(LayoutSize);
        var oracle = AscendingOracle(LayoutSize);

        for (int i = 256; i < 384; i++)
        {
            Assert.True(map.Remove(i));
            oracle.Remove(i);
        }

        for (int i = 0; i < 129; i++)
        {
            Assert.True(map.Remove(i));
            oracle.Remove(i);
        }

        AssertMatches(oracle, map);
    }

    [Fact]
    public void Remove_ShouldMergeIntoTheLeftNeighbour_WhenThereIsNoRightOne()
    {
        var map = Ascending(LayoutSize);
        var oracle = AscendingOracle(LayoutSize);

        for (int i = 512; i < 640; i++)
        {
            Assert.True(map.Remove(i));
            oracle.Remove(i);
        }

        for (int i = 768; i < 1073; i++)
        {
            Assert.True(map.Remove(i));
            oracle.Remove(i);
        }

        AssertMatches(oracle, map);
    }

    [Fact]
    public void Remove_ShouldDropTheBucket_WhenAMiddleOneEmptiesWithNeighboursTooLargeToMerge()
    {
        var map = Ascending(LayoutSize);
        var oracle = AscendingOracle(LayoutSize);

        for (int i = 256; i < 512; i++)
        {
            Assert.True(map.Remove(i));
            oracle.Remove(i);
        }

        AssertMatches(oracle, map);
    }

    [Fact]
    public void Remove_ShouldEmptyTheDictionary_AndLetItComeBack()
    {
        var map = Ascending(3);

        Assert.True(map.Remove(1));
        Assert.True(map.Remove(0));
        Assert.True(map.Remove(2));

        Assert.Equal(0, map.Count);
        Assert.False(map.TryGetMin(out _));
        Assert.Empty(map);

        // Revived through the first-bucket path with the slot arrays still allocated.
        map[9] = 90;
        Assert.Equal(90, map.GetValueAtIndex(0));
    }

    [Fact]
    public void RemoveAndAdd_ShouldStayConsistentWithASortedDictionary_UnderSustainedChurn()
    {
        var map = new RankedDictionary<int, int>();
        var oracle = new SortedDictionary<int, int>();
        var rand = new Random(20261116);

        for (int step = 0; step < 60_000; step++)
        {
            int key = rand.Next(0, 2000);
            int roll = rand.Next(0, 100);
            if (roll < 45)
            {
                Assert.Equal(oracle.TryAdd(key, step), map.TryAdd(key, step));
            }
            else if (roll < 55)
            {
                oracle[key] = -step;
                map[key] = -step;
            }
            else
            {
                Assert.Equal(oracle.Remove(key, out int expected), map.Remove(key, out int actual));
                Assert.Equal(expected, actual);
            }

            Assert.Equal(oracle.Count, map.Count);
        }

        AssertMatches(oracle, map);
    }

    // ---- clear and trim ----------------------------------------------------------------------------

    [Fact]
    public void Clear_ShouldEmptyTheDictionary_AndLeaveItReusable()
    {
        var map = Ascending(LayoutSize);

        map.Clear();

        Assert.Equal(0, map.Count);
        Assert.Empty(map);
        Assert.Equal(0, SlotCount(map));

        for (int i = 0; i < 2000; i++)
            map.Add(i, ValueOf(i));

        AssertMatches(AscendingOracle(2000), map);
    }

    [Fact]
    public void Clear_ShouldReleaseTheSlotArrays_WhenRemovalHadAlreadyEmptiedTheDictionary()
    {
        var map = Ascending(5000);
        for (int i = 0; i < 5000; i++)
            Assert.True(map.Remove(i));

        Assert.NotEqual(0, SlotCount(map));
        var live = map.GetEnumerator();

        map.Clear();

        Assert.Equal(0, SlotCount(map));
        Assert.Equal(1, Field<int[]>(map, "_tree").Length);
        Assert.False(live.MoveNext()); // releasing an empty map's arrays is not observable
    }

    [Fact]
    public void TrimExcess_ShouldRebuildAtTheCurrentSize_AndPreserveEveryRankAndValue()
    {
        var map = Ascending(300_000);
        var oracle = new SortedDictionary<int, int>();

        for (int i = 0; i < 300_000; i++)
        {
            if (i % 100 != 0)
                Assert.True(map.Remove(i));
            else
                oracle.Add(i, ValueOf(i));
        }

        AssertMatches(oracle, map);

        map.TrimExcess();

        AssertMatches(oracle, map);

        // 3,000 entries pack 384 to a 512 bucket, so eight buckets — more than the four initial slots.
        Assert.Equal(8, Field<int>(map, "_bucketCount"));
        Assert.True(map.TryAdd(50, 5));
        Assert.Equal(1, map.IndexOfKey(50));
        Assert.True(map.Remove(0));
        Assert.Equal(0, map.IndexOfKey(50));
        AssertRanksAgreeWithOrder(map);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(383)]
    [InlineData(384)]  // exactly one full packed bucket: no partial tail
    [InlineData(385)]
    [InlineData(768)]  // exactly two
    [InlineData(1200)]
    public void TrimExcess_ShouldPackAnyCount_WithOrWithoutAPartialLastBucket(int count)
    {
        var map = Ascending(count);

        map.TrimExcess();

        AssertMatches(AscendingOracle(count), map);
        Assert.Equal(((count - 1) / 384) + 1, Field<int>(map, "_bucketCount"));
    }

    [Fact]
    public void TrimExcess_ShouldBeANoOp_WhenTheDictionaryIsEmpty_AndReleaseLeftoverSlots()
    {
        var neverUsed = new RankedDictionary<int, int>();
        neverUsed.TrimExcess();
        Assert.Empty(neverUsed);

        var emptied = Ascending(3);
        Assert.True(emptied.Remove(0));
        Assert.True(emptied.Remove(1));
        Assert.True(emptied.Remove(2));
        Assert.NotEqual(0, SlotCount(emptied));

        emptied.TrimExcess();

        Assert.Equal(0, SlotCount(emptied));
        emptied.Add(7, 70);
        Assert.Equal(70, emptied[7]);
    }

    [Fact]
    public void TrimExcess_ShouldInvalidateActiveEnumerators_WhenItRebuilds()
    {
        var map = Ascending(3);
        var live = map.GetEnumerator();
        Assert.True(live.MoveNext());

        map.TrimExcess();

        Assert.Throws<InvalidOperationException>(() => live.MoveNext());
    }

    // ---- ContainsValue and the range scan ----------------------------------------------------------

    [Fact]
    public void ContainsValue_ShouldScanEveryBucket_WithDefaultEquality()
    {
        var map = Ascending(LayoutSize);

        Assert.True(map.ContainsValue(ValueOf(0)));
        Assert.True(map.ContainsValue(ValueOf(LayoutSize - 1)));
        Assert.False(map.ContainsValue(-5));
    }

    [Fact]
    public void EnumerateRange_ShouldReturnTheHalfOpenRange_AcrossBuckets()
    {
        var map = Ascending(LayoutSize);

        KeyValuePair<int, int>[] range = [.. map.EnumerateRange(250, 260)];

        Assert.Equal(Enumerable.Range(250, 10), range.Select(e => e.Key));
        Assert.All(range, e => Assert.Equal(ValueOf(e.Key), e.Value));
    }

    [Fact]
    public void EnumerateRange_ShouldReturnNothingOrRunToTheEnd_AtTheEdges()
    {
        var map = Ascending(LayoutSize);

        Assert.Empty(map.EnumerateRange(LayoutSize, LayoutSize + 10));
        Assert.Equal(Enumerable.Range(1190, 10), map.EnumerateRange(1190, 5000).Select(e => e.Key));
        Assert.Empty(map.EnumerateRange(5, 5));
        Assert.Empty(new RankedDictionary<int, int>().EnumerateRange(0, 10));
    }

    [Fact]
    public void EnumerateRange_ShouldThrowArgumentException_WhenTheBoundsAreInverted()
    {
        var map = Ascending(3);

        var ex = Assert.Throws<ArgumentException>(() => map.EnumerateRange(2, 1));
        Assert.Equal("toExclusive", ex.ParamName);
    }

    [Fact]
    public void Example_FromTheXmlDocs_ShouldProduceTheDocumentedAnswers()
    {
        var board = new RankedDictionary<int, string>();
        board[120] = "ada";
        board[340] = "brian";
        board[90] = "cleo";
        board[500] = "dmitri";

        Assert.Equal("dmitri", board.GetValueAtIndex(board.Count - 1));
        Assert.Equal(2, board.IndexOfKey(340));
        Assert.Equal(2, board.CountLessThan(300));

        board.RemoveAt(0);
        Assert.Equal(120, board.GetKeyAtIndex(0));
    }

    // ---- helpers ------------------------------------------------------------------------------------

    private static int SlotCount<TValue>(RankedDictionary<int, TValue> map) => Field<int[][]>(map, "_keys").Length;

    // The alias derives from the comparer-parameterized type, which is where the storage lives.
    private static TField Field<TField>(object map, string name)
    {
        for (Type? type = map.GetType(); type is not null; type = type.BaseType)
        {
            FieldInfo? field = type.GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            if (field is not null)
                return (TField)field.GetValue(map)!;
        }

        throw new InvalidOperationException($"RankedDictionary has no field named '{name}'.");
    }

    private struct DescendingInt : IComparer<int>
    {
        public readonly int Compare(int x, int y) => y.CompareTo(x);
    }

    private struct OrdinalIgnoreCase : IComparer<string>
    {
        public readonly int Compare(string? x, string? y) => StringComparer.OrdinalIgnoreCase.Compare(x, y);
    }
}
