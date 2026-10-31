using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Core behavioural coverage for <see cref="SegmentedList{T}"/>: the constructors, the segment geometry
/// (<see cref="SegmentedList{T}.SegmentLength"/> per element size, <see cref="SegmentedList{T}.SegmentCount"/>,
/// <see cref="SegmentedList{T}.GetSegment"/>), the growth policy (the first segment doubling toward a full one,
/// whole segments after it, segments retained by <c>Clear</c> and removal and reused by the next
/// <c>Add</c>), <see cref="SegmentedList{T}.EnsureCapacity"/> / <see cref="SegmentedList{T}.TrimExcess"/> on
/// every path, each <see cref="SegmentedList{T}.AddRange"/> source shape, and the positional edits that shift
/// elements across segment boundaries.
///
/// <para>
/// An <see cref="int"/> list has 16,384-element segments, so crossing a boundary with it costs tens of
/// thousands of elements. <see cref="SegmentedBig"/> is a 16 KB struct whose segment holds exactly four, so
/// the multi-segment shift, spine growth and enumeration paths are driven with a handful of elements; and
/// <see cref="SegmentedHuge"/> is wider than half the 64 KB budget, so its segment holds one. The randomized
/// counterpart is <see cref="SegmentedListDifferentialTests"/>; nothing here depends on a seed.
/// </para>
/// </summary>
public class SegmentedListTests
{
    private const int IntSegment = 16_384;

    // ---- geometry -------------------------------------------------------------------------------------

    [Fact]
    public void SegmentLength_ShouldBeTheLargestPowerOfTwoWhoseArrayFitsIn64KB()
    {
        Assert.Equal(65_536, new SegmentedList<byte>().SegmentLength);
        Assert.Equal(IntSegment, new SegmentedList<int>().SegmentLength);
        Assert.Equal(8_192, new SegmentedList<long>().SegmentLength);
        Assert.Equal(65_536 / IntPtr.Size, new SegmentedList<string>().SegmentLength);
        Assert.Equal(4, new SegmentedList<SegmentedBig>().SegmentLength);

        // Wider than half the budget: one element per segment. (No element can be wider than the whole
        // budget: the runtime refuses to create an array of a value type of 64 KB or more.)
        Assert.True(Unsafe.SizeOf<SegmentedHuge>() > 32_768);
        Assert.Equal(1, new SegmentedList<SegmentedHuge>().SegmentLength);
    }

    [Fact]
    public void SegmentCount_ShouldCountOnlySegmentsHoldingElements()
    {
        var list = new SegmentedList<SegmentedBig>();
        Assert.Equal(0, list.SegmentCount);

        int[] expected = { 1, 1, 1, 1, 2, 2, 2, 2, 3 };
        for (int i = 0; i < expected.Length; i++)
        {
            list.Add(SegmentedBig.Of(i));
            Assert.Equal(expected[i], list.SegmentCount);
        }

        // Retained segments are not counted.
        list.RemoveAt(8);
        list.RemoveAt(7);
        Assert.Equal(2, list.SegmentCount);
        Assert.Equal(12, list.Capacity);
    }

    [Fact]
    public void GetSegment_ShouldExposeEachSegmentInOrder_WithAShortLastOne()
    {
        var list = new SegmentedList<SegmentedBig>();
        for (int i = 0; i < 10; i++)
            list.Add(SegmentedBig.Of(i));

        Assert.Equal(3, list.SegmentCount);
        Assert.Equal(new[] { 0, 1, 2, 3 }, Values(list.GetSegment(0)));
        Assert.Equal(new[] { 4, 5, 6, 7 }, Values(list.GetSegment(1)));
        Assert.Equal(new[] { 8, 9 }, Values(list.GetSegment(2)));
    }

    [Fact]
    public void GetSegment_ShouldCoverTheWholeList_WhenWalkedFromZeroToSegmentCount()
    {
        var list = new SegmentedList<int>(Enumerable.Range(0, 2 * IntSegment + 5));

        var walked = new List<int>();
        for (int s = 0; s < list.SegmentCount; s++)
            walked.AddRange(list.GetSegment(s).ToArray());

        Assert.Equal(3, list.SegmentCount);
        Assert.Equal(5, list.GetSegment(2).Length);
        Assert.Equal(Enumerable.Range(0, 2 * IntSegment + 5), walked);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void GetSegment_ShouldThrowArgumentOutOfRangeException_WhenIndexIsOutOfRange(int segmentIndex)
    {
        var list = new SegmentedList<SegmentedBig>();
        for (int i = 0; i < 5; i++)
            list.Add(SegmentedBig.Of(i));

        ArgumentOutOfRangeException ex =
            Assert.Throws<ArgumentOutOfRangeException>(() => list.GetSegment(segmentIndex));
        Assert.Equal("segmentIndex", ex.ParamName);
    }

    [Fact]
    public void GetSegment_ShouldThrow_WhenTheListIsEmpty_EvenWithRetainedSegments()
    {
        var list = new SegmentedList<int>(new[] { 1, 2, 3 });
        list.Clear();

        Assert.Equal(0, list.SegmentCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => list.GetSegment(0));
    }

    // ---- constructors ---------------------------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldCreateAnEmptyListWithNoCapacity_WhenGivenNothing()
    {
        var list = new SegmentedList<int>();

        Assert.Equal(0, list.Count);
        Assert.Equal(0, list.Capacity);
        Assert.Equal(0, list.SegmentCount);
        Assert.Empty(list);
        Assert.Empty(list.ToArray());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 4)]           // the default first allocation
    [InlineData(10, 10)]
    [InlineData(IntSegment, IntSegment)]
    [InlineData(IntSegment + 1, 2 * IntSegment)]
    [InlineData(5 * IntSegment - 3, 5 * IntSegment)]
    public void Constructor_WithCapacity_ShouldReserveAtLeastThatMany(int capacity, int expected)
    {
        var list = new SegmentedList<int>(capacity);

        Assert.Equal(0, list.Count);
        Assert.Equal(expected, list.Capacity);
    }

    [Fact]
    public void Constructor_WithCapacity_ShouldThrow_WhenCapacityIsNegative()
    {
        ArgumentOutOfRangeException ex =
            Assert.Throws<ArgumentOutOfRangeException>(() => new SegmentedList<int>(-1));
        Assert.Equal("capacity", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithCapacity_ShouldNotLeaveTheEnumeratorVersionAdvanced()
    {
        // The reservation is construction, not a mutation an enumerator could observe; the version is reset
        // so a fresh list reads the same however it was made.
        var list = new SegmentedList<int>(100);
        Assert.Equal(0, Version(list));

        var built = new SegmentedList<int>(Enumerable.Range(0, 100));
        Assert.Equal(0, Version(built));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenCollectionIsNull()
    {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => new SegmentedList<int>(null!));
        Assert.Equal("collection", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldCopyTheSource_InEnumerationOrder()
    {
        int[] source = { 5, 3, 9, 1 };
        var list = new SegmentedList<int>(source);
        source[0] = 99;

        Assert.Equal(new[] { 5, 3, 9, 1 }, list);
        Assert.Equal(4, list.Capacity);
    }

    // ---- growth ---------------------------------------------------------------------------------------

    [Fact]
    public void Add_ShouldGrowTheFirstSegmentByDoubling_UpToAFullSegment()
    {
        var list = new SegmentedList<int>();
        var capacities = new List<int>();
        for (int i = 0; i < IntSegment; i++)
        {
            list.Add(i);
            if (capacities.Count == 0 || capacities[^1] != list.Capacity)
                capacities.Add(list.Capacity);
        }

        Assert.Equal(new[] { 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384 }, capacities);
        Assert.Equal(1, list.SegmentCount);
    }

    [Fact]
    public void Add_ShouldAddWholeSegments_PastTheFirst_WithoutMovingTheFullOnes()
    {
        var list = new SegmentedList<int>(Enumerable.Range(0, IntSegment));
        ref readonly int firstBefore = ref MemoryMarshal.GetReference(list.GetSegment(0));

        list.Add(IntSegment);
        Assert.Equal(2 * IntSegment, list.Capacity);
        Assert.Equal(2, list.SegmentCount);

        for (int i = IntSegment + 1; i < 3 * IntSegment + 1; i++)
            list.Add(i);
        Assert.Equal(4 * IntSegment, list.Capacity);

        // The first segment was never reallocated, so a ref into it still points at the list's storage.
        Assert.True(Unsafe.AreSame(ref Unsafe.AsRef(in firstBefore), ref MemoryMarshal.GetReference(list.GetSegment(0))));
        Assert.Equal(Enumerable.Range(0, 3 * IntSegment + 1), list);
    }

    [Fact]
    public void Add_ShouldGrowTheSpine_PastItsInitialFourSegments()
    {
        var list = new SegmentedList<SegmentedBig>();
        for (int i = 0; i < 40; i++)
            list.Add(SegmentedBig.Of(i));

        Assert.Equal(10, list.SegmentCount);
        Assert.Equal(40, list.Capacity);
        Assert.Equal(Enumerable.Range(0, 40), Values(list));
    }

    [Fact]
    public void Add_ShouldStoreOneElementPerSegment_WhenTheElementIsWiderThanTheBudget()
    {
        var list = new SegmentedList<SegmentedHuge>();
        for (int i = 0; i < 6; i++)
            list.Add(SegmentedHuge.Of(i));

        Assert.Equal(6, list.SegmentCount);
        Assert.Equal(6, list.Capacity);
        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(1, list.GetSegment(i).Length);
            Assert.Equal(i, list[i].Value);
        }

        list.Insert(0, SegmentedHuge.Of(-1));
        list.Insert(3, SegmentedHuge.Of(-3));
        list.RemoveAt(5);
        Assert.Equal(new[] { -1, 0, 1, -3, 2, 4, 5 }, list.Select(h => h.Value));
    }

    [Fact]
    public void Add_ShouldReuseTheRetainedSegments_AfterClear()
    {
        var list = new SegmentedList<int>(Enumerable.Range(0, 3 * IntSegment));
        ref readonly int secondBefore = ref MemoryMarshal.GetReference(list.GetSegment(1));

        list.Clear();
        Assert.Equal(3 * IntSegment, list.Capacity);

        for (int i = 0; i < 2 * IntSegment + 1; i++)
            list.Add(-i);

        Assert.Equal(3 * IntSegment, list.Capacity);
        Assert.True(Unsafe.AreSame(ref Unsafe.AsRef(in secondBefore), ref MemoryMarshal.GetReference(list.GetSegment(1))));
        Assert.Equal(Enumerable.Range(0, 2 * IntSegment + 1).Select(i => -i), list);
    }

    [Fact]
    public void Add_ShouldReuseASegmentRetainedByRemoval()
    {
        var list = new SegmentedList<SegmentedBig>();
        for (int i = 0; i < 9; i++)
            list.Add(SegmentedBig.Of(i));
        Assert.Equal(12, list.Capacity);

        for (int i = 0; i < 5; i++)
            list.RemoveAt(list.Count - 1);
        Assert.Equal(4, list.Count);
        Assert.Equal(12, list.Capacity);

        for (int i = 0; i < 8; i++)
            list.Add(SegmentedBig.Of(100 + i));

        Assert.Equal(12, list.Capacity);
        Assert.Equal(new[] { 0, 1, 2, 3, 100, 101, 102, 103, 104, 105, 106, 107 }, Values(list));
    }

    [Fact]
    public void Add_ShouldStartAgainFromASmallFirstSegment_AfterTrimExcessReleasedEverything()
    {
        var list = new SegmentedList<int>(new[] { 1, 2, 3 });
        list.Clear();
        list.TrimExcess();
        Assert.Equal(0, list.Capacity);

        list.Add(7);
        Assert.Equal(4, list.Capacity);
        Assert.Equal(new[] { 7 }, list);
    }

    // ---- EnsureCapacity -------------------------------------------------------------------------------

    [Fact]
    public void EnsureCapacity_ShouldGrowTheFirstSegment_AtLeastByDoubling_WhenItStillFits()
    {
        var list = new SegmentedList<int>();
        Assert.Equal(10, list.EnsureCapacity(10));

        // 11 is less than double 10, so it doubles; 100 is more, so it gets exactly what it asked for.
        Assert.Equal(20, list.EnsureCapacity(11));
        Assert.Equal(100, list.EnsureCapacity(100));

        // A small request on an empty list still gets the default first allocation.
        Assert.Equal(4, new SegmentedList<int>().EnsureCapacity(1));
    }

    [Fact]
    public void EnsureCapacity_ShouldKeepTheElements_WhenTheFirstSegmentGrows()
    {
        var list = new SegmentedList<int>(new[] { 1, 2, 3 });

        Assert.Equal(1_000, list.EnsureCapacity(1_000));

        Assert.Equal(new[] { 1, 2, 3 }, list);
        list.Add(4);
        Assert.Equal(new[] { 1, 2, 3, 4 }, list);
    }

    [Fact]
    public void EnsureCapacity_ShouldAllocateWholeSegments_WhenNothingIsAllocatedYet()
    {
        var list = new SegmentedList<int>();

        Assert.Equal(2 * IntSegment, list.EnsureCapacity(IntSegment + 1));
        Assert.Equal(IntSegment, ((Array)Segments(list)[0]!).Length);

        for (int i = 0; i < 2 * IntSegment; i++)
            list.Add(i);
        Assert.Equal(2 * IntSegment, list.Capacity);
        Assert.Equal(Enumerable.Range(0, 2 * IntSegment), list);
    }

    [Fact]
    public void EnsureCapacity_ShouldWidenAPartialFirstSegment_BeforeAddingMoreSegments()
    {
        var list = new SegmentedList<int>(new[] { 10, 20, 30 });
        Assert.Equal(4, list.Capacity);

        Assert.Equal(3 * IntSegment, list.EnsureCapacity(2 * IntSegment + 1));

        Assert.Equal(IntSegment, ((Array)Segments(list)[0]!).Length);
        Assert.Equal(new[] { 10, 20, 30 }, list);
        for (int i = 0; i < 3 * IntSegment - 3; i++)
            list.Add(i);
        Assert.Equal(3 * IntSegment, list.Capacity);
        Assert.Equal(3 * IntSegment, list.Count);
        Assert.Equal(10, list[0]);
        Assert.Equal(3 * IntSegment - 4, list[^1]);
    }

    [Fact]
    public void EnsureCapacity_ShouldAppendSegments_WhenTheFirstIsAlreadyFull()
    {
        var list = new SegmentedList<SegmentedBig>();
        list.Add(SegmentedBig.Of(1));
        Assert.Equal(4, list.Capacity);

        Assert.Equal(24, list.EnsureCapacity(21));

        Assert.Equal(1, list.SegmentCount);
        Assert.Equal(1, list[0].Value);
    }

    [Fact]
    public void EnsureCapacity_ShouldReturnTheCurrentCapacity_AndNotInvalidate_WhenAlreadyLargeEnough()
    {
        var list = new SegmentedList<int>(new[] { 1, 2, 3 });
        SegmentedList<int>.Enumerator e = list.GetEnumerator();

        Assert.Equal(4, list.EnsureCapacity(0));
        Assert.Equal(4, list.EnsureCapacity(4));

        Assert.True(e.MoveNext());
        Assert.Equal(1, e.Current);
    }

    [Fact]
    public void EnsureCapacity_ShouldInvalidateTheEnumerator_WhenItAllocates()
    {
        var list = new SegmentedList<int>(new[] { 1, 2, 3 });
        SegmentedList<int>.Enumerator e = list.GetEnumerator();

        list.EnsureCapacity(5);

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void EnsureCapacity_ShouldThrow_WhenCapacityIsNegative()
    {
        var list = new SegmentedList<int>();
        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => list.EnsureCapacity(-1));
        Assert.Equal("capacity", ex.ParamName);
    }

    [Fact]
    public void EnsureCapacity_ShouldThrow_WhenCapacityExceedsTheElementCeiling()
    {
        // The ceiling is one segment short of 2^31 slots, so int.MaxValue is past it for any element that
        // packs more than one per segment.
        var list = new SegmentedList<int>();
        ArgumentOutOfRangeException ex =
            Assert.Throws<ArgumentOutOfRangeException>(() => list.EnsureCapacity(int.MaxValue));
        Assert.Equal("capacity", ex.ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SegmentedList<int>(int.MaxValue - IntSegment + 2));
        Assert.Equal(0, list.Capacity);
    }

    // ---- TrimExcess -----------------------------------------------------------------------------------

    [Fact]
    public void TrimExcess_ShouldDoNothing_WhenNothingIsAllocated()
    {
        var list = new SegmentedList<int>();
        SegmentedList<int>.Enumerator e = list.GetEnumerator();

        list.TrimExcess();

        Assert.Equal(0, list.Capacity);
        Assert.False(e.MoveNext());
    }

    [Fact]
    public void TrimExcess_ShouldDoNothing_WhenTheOnlySegmentIsExactlyFull()
    {
        var list = new SegmentedList<int>(new[] { 1, 2, 3, 4 });
        SegmentedList<int>.Enumerator e = list.GetEnumerator();

        list.TrimExcess();

        Assert.Equal(4, list.Capacity);
        Assert.True(e.MoveNext());
    }

    [Fact]
    public void TrimExcess_ShouldShrinkASingleSegment_ToExactlyTheCount()
    {
        // Built by Add, so the first segment doubled past the count (a sized source would reserve exactly).
        var list = new SegmentedList<int>();
        for (int i = 1; i <= 5; i++)
            list.Add(i);
        Assert.Equal(8, list.Capacity);
        SegmentedList<int>.Enumerator e = list.GetEnumerator();

        list.TrimExcess();

        Assert.Equal(5, list.Capacity);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, list);
        Assert.Throws<InvalidOperationException>(() => e.MoveNext());

        list.Add(6);
        Assert.Equal(10, list.Capacity);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, list);
    }

    [Fact]
    public void TrimExcess_ShouldReleaseEverything_WhenTheListIsEmpty()
    {
        var list = new SegmentedList<int>(Enumerable.Range(0, IntSegment + 1));
        list.Clear();
        Assert.Equal(2 * IntSegment, list.Capacity);
        SegmentedList<int>.Enumerator e = list.GetEnumerator();

        list.TrimExcess();

        Assert.Equal(0, list.Capacity);
        Assert.Empty(Segments(list));
        Assert.Throws<InvalidOperationException>(() => e.MoveNext());

        // A second trim has nothing to release.
        SegmentedList<int>.Enumerator again = list.GetEnumerator();
        list.TrimExcess();
        Assert.False(again.MoveNext());
    }

    [Fact]
    public void TrimExcess_ShouldReleaseTheLaterSegments_AndShrinkTheFirst_WhenOneSegmentIsInUse()
    {
        var list = new SegmentedList<int>(Enumerable.Range(0, 3 * IntSegment));
        while (list.Count > 10)
            list.RemoveAt(list.Count - 1);
        Assert.Equal(3 * IntSegment, list.Capacity);

        list.TrimExcess();

        Assert.Equal(10, list.Capacity);
        Assert.Equal(Enumerable.Range(0, 10), list);
        list.Add(10);
        Assert.Equal(Enumerable.Range(0, 11), list);
    }

    [Fact]
    public void TrimExcess_ShouldKeepTheFirstSegment_WhenItIsExactlyFullAndLaterOnesAreReleased()
    {
        // Regression: with Count == SegmentLength and spare segments allocated, TrimExcess used to replace the
        // full first segment with an identical copy of the same length.
        var list = new SegmentedList<int>(Enumerable.Range(0, 2 * IntSegment));
        while (list.Count > IntSegment)
            list.RemoveAt(list.Count - 1);
        ref readonly int firstBefore = ref MemoryMarshal.GetReference(list.GetSegment(0));

        list.TrimExcess();

        Assert.Equal(IntSegment, list.Capacity);
        Assert.True(Unsafe.AreSame(ref Unsafe.AsRef(in firstBefore), ref MemoryMarshal.GetReference(list.GetSegment(0))));
        Assert.Equal(Enumerable.Range(0, IntSegment), list);
    }

    [Fact]
    public void TrimExcess_ShouldReleaseOnlyTheUnusedSegments_WhenSeveralAreInUse()
    {
        var list = new SegmentedList<SegmentedBig>();
        for (int i = 0; i < 20; i++)
            list.Add(SegmentedBig.Of(i));
        for (int i = 0; i < 13; i++)
            list.RemoveAt(list.Count - 1);
        Assert.Equal(7, list.Count);
        Assert.Equal(20, list.Capacity);
        SegmentedList<SegmentedBig>.Enumerator e = list.GetEnumerator();

        list.TrimExcess();

        Assert.Equal(8, list.Capacity);
        Assert.Equal(2, list.SegmentCount);
        Assert.Throws<InvalidOperationException>(() => e.MoveNext());

        // The trimmed spine slots are cleared, so the dropped segments are collectable.
        object?[] spine = Segments(list);
        Assert.All(spine.Skip(2), Assert.Null);

        for (int i = 0; i < 5; i++)
            list.Add(SegmentedBig.Of(50 + i));
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 50, 51, 52, 53, 54 }, Values(list));
    }

    [Fact]
    public void TrimExcess_ShouldDoNothing_WhenEverySegmentIsInUse()
    {
        var list = new SegmentedList<SegmentedBig>();
        for (int i = 0; i < 9; i++)
            list.Add(SegmentedBig.Of(i));
        SegmentedList<SegmentedBig>.Enumerator e = list.GetEnumerator();

        list.TrimExcess();

        Assert.Equal(12, list.Capacity);
        Assert.True(e.MoveNext());
    }

    // ---- indexer --------------------------------------------------------------------------------------

    [Fact]
    public void Indexer_ShouldReadAndWriteEveryPosition_AcrossSegments()
    {
        var list = new SegmentedList<int>(Enumerable.Range(0, 2 * IntSegment + 7));
        for (int i = 0; i < list.Count; i += 1_001)
            Assert.Equal(i, list[i]);

        list[IntSegment - 1] = -1;
        list[IntSegment] = -2;
        list[list.Count - 1] = -3;

        Assert.Equal(-1, list[IntSegment - 1]);
        Assert.Equal(-2, list[IntSegment]);
        Assert.Equal(-3, list[^1]);
        Assert.Equal(IntSegment + 1, list[IntSegment + 1]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(int.MinValue)]
    public void Indexer_ShouldThrowArgumentOutOfRangeException_WhenIndexIsOutOfRange(int index)
    {
        // Capacity 4, count 3: the slot at 3 exists but holds no element.
        var list = new SegmentedList<int>(new[] { 1, 2, 3 });

        ArgumentOutOfRangeException get = Assert.Throws<ArgumentOutOfRangeException>(() => list[index]);
        Assert.Equal("index", get.ParamName);
        ArgumentOutOfRangeException set = Assert.Throws<ArgumentOutOfRangeException>(() => list[index] = 0);
        Assert.Equal("index", set.ParamName);
    }

    // ---- Insert ---------------------------------------------------------------------------------------

    [Fact]
    public void Insert_ShouldPlaceTheElementAtTheIndex_AtTheFrontMiddleAndEnd()
    {
        var list = new SegmentedList<int>(new[] { 1, 2, 3 });

        list.Insert(0, 0);
        list.Insert(2, 15);
        list.Insert(list.Count, 4);

        Assert.Equal(new[] { 0, 1, 15, 2, 3, 4 }, list);
    }

    [Fact]
    public void Insert_ShouldGrowTheFirstSegment_WhenItIsFull()
    {
        var list = new SegmentedList<int>(new[] { 1, 2, 3, 4 });
        Assert.Equal(4, list.Capacity);

        list.Insert(1, 9);

        Assert.Equal(8, list.Capacity);
        Assert.Equal(new[] { 1, 9, 2, 3, 4 }, list);
    }

    [Fact]
    public void Insert_AtCount_ShouldAppend_EvenOnAnEmptyList()
    {
        var list = new SegmentedList<int>();
        list.Insert(0, 5);
        list.Insert(1, 6);

        Assert.Equal(new[] { 5, 6 }, list);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]  // the last slot of the first segment
    [InlineData(4)]  // the first slot of the second
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(13)] // the last element, which moves into a new segment
    public void Insert_ShouldShiftEveryLaterElement_AcrossSegmentBoundaries(int index)
    {
        var list = new SegmentedList<SegmentedBig>();
        var oracle = new List<int>();
        for (int i = 0; i < 14; i++)
        {
            list.Add(SegmentedBig.Of(i));
            oracle.Add(i);
        }

        list.Insert(index, SegmentedBig.Of(-1));
        oracle.Insert(index, -1);

        Assert.Equal(oracle, Values(list));
        Assert.Equal(4, list.SegmentCount);
    }

    [Fact]
    public void Insert_ShouldShiftAcrossSegments_WhenTheLastSegmentIsExactlyFull()
    {
        // The appended copy of the last element opens a fresh segment, so every segment shifts.
        var list = new SegmentedList<SegmentedBig>();
        var oracle = new List<int>();
        for (int i = 0; i < 12; i++)
        {
            list.Add(SegmentedBig.Of(i));
            oracle.Add(i);
        }

        list.Insert(0, SegmentedBig.Of(-1));
        oracle.Insert(0, -1);

        Assert.Equal(oracle, Values(list));
        Assert.Equal(4, list.SegmentCount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void Insert_ShouldThrowArgumentOutOfRangeException_WhenIndexIsOutOfRange(int index)
    {
        var list = new SegmentedList<int>(new[] { 1, 2, 3 });

        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => list.Insert(index, 0));
        Assert.Equal("index", ex.ParamName);
        Assert.Equal(new[] { 1, 2, 3 }, list);
    }

    // ---- RemoveAt / Remove ----------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(9)]
    [InlineData(12)] // the only element of the last segment
    public void RemoveAt_ShouldShiftEveryLaterElement_AcrossSegmentBoundaries(int index)
    {
        var list = new SegmentedList<SegmentedBig>();
        var oracle = new List<int>();
        for (int i = 0; i < 13; i++)
        {
            list.Add(SegmentedBig.Of(i));
            oracle.Add(i);
        }

        list.RemoveAt(index);
        oracle.RemoveAt(index);

        Assert.Equal(oracle, Values(list));
        Assert.Equal(3, list.SegmentCount);
        list.Add(SegmentedBig.Of(99));
        oracle.Add(99);
        Assert.Equal(oracle, Values(list));
    }

    [Fact]
    public void RemoveAt_ShouldClearTheVacatedSlot_SoAReferenceIsNotRetained()
    {
        var list = new SegmentedList<string>(new[] { "a", "b", "c" });

        list.RemoveAt(0);

        Assert.Equal(new[] { "b", "c" }, list);
        Assert.Null(((string?[])Segments(list)[0]!)[2]);
    }

    [Fact]
    public void RemoveAt_ShouldDrainTheList_AndLeaveItReusable()
    {
        var list = new SegmentedList<SegmentedBig>();
        for (int i = 0; i < 9; i++)
            list.Add(SegmentedBig.Of(i));

        while (list.Count > 0)
            list.RemoveAt(0);

        Assert.Empty(list);
        Assert.Equal(0, list.SegmentCount);
        list.Add(SegmentedBig.Of(1));
        Assert.Equal(new[] { 1 }, Values(list));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void RemoveAt_ShouldThrowArgumentOutOfRangeException_WhenIndexIsOutOfRange(int index)
    {
        var list = new SegmentedList<int>(new[] { 1, 2, 3 });

        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => list.RemoveAt(index));
        Assert.Equal("index", ex.ParamName);
    }

    [Fact]
    public void Remove_ShouldRemoveTheFirstOccurrence_WhenFound()
    {
        var list = new SegmentedList<int>(new[] { 1, 2, 3, 2 });

        Assert.True(list.Remove(2));
        Assert.Equal(new[] { 1, 3, 2 }, list);
    }

    [Fact]
    public void Remove_ShouldReturnFalseAndLeaveTheListUnchanged_WhenNotFound()
    {
        var list = new SegmentedList<int>(new[] { 1, 2, 3 });

        Assert.False(list.Remove(4));
        Assert.Equal(new[] { 1, 2, 3 }, list);
    }

    // ---- search ---------------------------------------------------------------------------------------

    [Fact]
    public void IndexOf_ShouldReturnTheFirstMatch_InAnySegment()
    {
        var list = new SegmentedList<SegmentedBig>();
        for (int i = 0; i < 10; i++)
            list.Add(SegmentedBig.Of(i % 7));

        Assert.Equal(0, list.IndexOf(SegmentedBig.Of(0)));
        Assert.Equal(6, list.IndexOf(SegmentedBig.Of(6)));
        Assert.Equal(-1, list.IndexOf(SegmentedBig.Of(7)));
        Assert.True(list.Contains(SegmentedBig.Of(5)));
        Assert.False(list.Contains(SegmentedBig.Of(70)));
    }

    [Fact]
    public void IndexOf_ShouldIgnoreRetainedSlotsPastTheCount()
    {
        var list = new SegmentedList<int>(new[] { 1, 2, 3 });
        list.RemoveAt(2);
        list.Clear();
        list.Add(5);

        // The stale 2 is still physically in the retained segment, but past the count.
        Assert.Equal(-1, list.IndexOf(2));
        Assert.Equal(-1, list.IndexOf(0));
        Assert.Equal(0, list.IndexOf(5));
        Assert.Equal(-1, new SegmentedList<int>().IndexOf(0));
    }

    [Fact]
    public void Search_ShouldHandleNull_ForReferenceTypes()
    {
        var list = new SegmentedList<string?>(new[] { "a", null, "b" });

        Assert.Equal(1, list.IndexOf(null));
        Assert.True(list.Contains(null));
        Assert.True(list.Remove(null));
        Assert.False(list.Contains(null));
    }

    // ---- AddRange -------------------------------------------------------------------------------------

    [Fact]
    public void AddRange_ShouldThrowArgumentNullException_WhenCollectionIsNull()
    {
        var list = new SegmentedList<int>();
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => list.AddRange(null!));
        Assert.Equal("collection", ex.ParamName);
    }

    [Fact]
    public void AddRange_ShouldAppendAnArray_AcrossSegmentBoundaries()
    {
        var list = new SegmentedList<int>(new[] { -2, -1 });

        list.AddRange(Enumerable.Range(0, 2 * IntSegment).ToArray());

        Assert.Equal(2 * IntSegment + 2, list.Count);
        Assert.Equal(3 * IntSegment, list.Capacity);
        Assert.Equal(new[] { -2, -1 }.Concat(Enumerable.Range(0, 2 * IntSegment)), list);
    }

    [Fact]
    public void AddRange_ShouldAppendAList()
    {
        var list = new SegmentedList<SegmentedBig>();
        list.Add(SegmentedBig.Of(-1));

        list.AddRange(Enumerable.Range(0, 9).Select(SegmentedBig.Of).ToList());

        Assert.Equal(new[] { -1, 0, 1, 2, 3, 4, 5, 6, 7, 8 }, Values(list));
    }

    [Fact]
    public void AddRange_ShouldAppendAnotherSegmentedList_SegmentBySegment()
    {
        var source = new SegmentedList<SegmentedBig>();
        for (int i = 0; i < 10; i++)
            source.Add(SegmentedBig.Of(i));
        var list = new SegmentedList<SegmentedBig>();
        list.Add(SegmentedBig.Of(-1));

        list.AddRange(source);

        Assert.Equal(new[] { -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, Values(list));
        Assert.Equal(Enumerable.Range(0, 10), Values(source));
    }

    [Fact]
    public void AddRange_ShouldAppendOneCopy_WhenTheListIsAddedToItself()
    {
        var list = new SegmentedList<SegmentedBig>();
        for (int i = 0; i < 6; i++)
            list.Add(SegmentedBig.Of(i));

        list.AddRange(list);

        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 0, 1, 2, 3, 4, 5 }, Values(list));

        // A small int list whose first segment has to grow, and be re-read, mid-append.
        var ints = new SegmentedList<int>(new[] { 1, 2, 3 });
        ints.AddRange(ints);
        Assert.Equal(new[] { 1, 2, 3, 1, 2, 3 }, ints);
    }

    [Fact]
    public void AddRange_ShouldReserveTheCount_ForAnyOtherCollection()
    {
        var list = new SegmentedList<int>(new[] { 0 });

        list.AddRange(new LinkedList<int>(Enumerable.Range(1, 100)));

        Assert.Equal(101, list.Capacity);
        Assert.Equal(Enumerable.Range(0, 101), list);

        list.AddRange(new Collection<int> { 101, 102 });
        Assert.Equal(Enumerable.Range(0, 103), list);
    }

    [Fact]
    public void AddRange_ShouldAppendOneCopy_WhenTheSourceIsACollectionWrappingTheList()
    {
        // Regression: a ReadOnlyCollection<T> over the list used to be enumerated while Add mutated the list
        // underneath it, so it threw after appending one element. List<T> snapshots through CopyTo instead.
        var list = new SegmentedList<int>(new[] { 1, 2, 3 });

        list.AddRange(new ReadOnlyCollection<int>(list));

        Assert.Equal(new[] { 1, 2, 3, 1, 2, 3 }, list);
    }

    [Fact]
    public void AddRange_ShouldAppendALazySequence_OneElementAtATime()
    {
        static IEnumerable<int> Stream(int n)
        {
            for (int i = 0; i < n; i++)
                yield return i * 3;
        }

        var list = new SegmentedList<int>();
        list.AddRange(Stream(IntSegment + 10));

        Assert.Equal(IntSegment + 10, list.Count);
        Assert.Equal(Stream(IntSegment + 10), list);
    }

    [Fact]
    public void AddRange_ShouldNotInvalidateTheEnumerator_WhenTheSourceIsEmpty()
    {
        var list = new SegmentedList<int>(new[] { 1, 2 });
        SegmentedList<int>.Enumerator e = list.GetEnumerator();

        list.AddRange(Array.Empty<int>());
        list.AddRange(new List<int>());
        list.AddRange(new SegmentedList<int>());
        list.AddRange(new HashSet<int>());
        list.AddRange(Enumerable.Empty<int>().Where(_ => true));

        Assert.True(e.MoveNext());
        Assert.Equal(new[] { 1, 2 }, list);
        Assert.Equal(4, list.Capacity);
    }

    [Fact]
    public void AddRange_ShouldInvalidateTheEnumerator_WhenItAppends()
    {
        var list = new SegmentedList<int>(new[] { 1, 2 });
        SegmentedList<int>.Enumerator e = list.GetEnumerator();

        list.AddRange(new[] { 3 });

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    // ---- Clear / ToArray / CopyTo ---------------------------------------------------------------------

    [Fact]
    public void Clear_ShouldEmptyAMultiSegmentList_KeepTheCapacity_AndLeaveItReusable()
    {
        var list = new SegmentedList<int>(Enumerable.Range(0, IntSegment + 3));

        list.Clear();

        Assert.Equal(0, list.Count);
        Assert.Equal(0, list.SegmentCount);
        Assert.Equal(2 * IntSegment, list.Capacity);
        Assert.Empty(list);

        list.Add(42);
        Assert.Equal(new[] { 42 }, list);
    }

    [Fact]
    public void Clear_ShouldReleaseReferences_ForAReferenceElementType()
    {
        var list = new SegmentedList<string>();
        for (int i = 0; i < 4 * IntSegment + 1; i += 1)
            list.Add(i.ToString());
        // 8,192 references per segment: the list spans nine segments and ends one into the ninth.
        Assert.Equal(9, list.SegmentCount);

        list.Clear();

        object?[] spine = Segments(list);
        for (int s = 0; s < 9; s++)
            Assert.All((string?[])spine[s]!, Assert.Null);
    }

    [Fact]
    public void Clear_ShouldLeaveTheSlots_ForAnElementTypeWithoutReferences()
    {
        // Nothing to release, so the clear skips the wipe; the stale values are past the count and unreadable.
        var list = new SegmentedList<int>(new[] { 7, 8, 9 });

        list.Clear();

        Assert.Equal(new[] { 7, 8, 9, 0 }, (int[])Segments(list)[0]!);
        Assert.Throws<ArgumentOutOfRangeException>(() => list[0]);
    }

    [Fact]
    public void Clear_ShouldBeANoOp_WhenTheListIsEmpty()
    {
        var list = new SegmentedList<int>();
        SegmentedList<int>.Enumerator e = list.GetEnumerator();

        list.Clear();

        Assert.False(e.MoveNext());
        Assert.Equal(0, list.Capacity);
    }

    [Fact]
    public void ToArray_ShouldReturnTheSharedEmptyArray_WhenTheListIsEmpty()
    {
        Assert.Same(Array.Empty<int>(), new SegmentedList<int>().ToArray());
        var cleared = new SegmentedList<int>(new[] { 1 });
        cleared.Clear();
        Assert.Same(Array.Empty<int>(), cleared.ToArray());
    }

    [Fact]
    public void ToArray_ShouldCopyEverySegmentInOrder()
    {
        var list = new SegmentedList<SegmentedBig>();
        for (int i = 0; i < 11; i++)
            list.Add(SegmentedBig.Of(i));

        Assert.Equal(Enumerable.Range(0, 11), list.ToArray().Select(b => b.Value));
    }

    [Fact]
    public void CopyTo_ShouldCopyEverySegmentInOrder_AtTheGivenOffset()
    {
        var list = new SegmentedList<int>(Enumerable.Range(0, 2 * IntSegment + 1));
        var target = new int[2 * IntSegment + 4];

        list.CopyTo(target, 2);

        Assert.Equal(0, target[0]);
        Assert.Equal(0, target[1]);
        Assert.Equal(Enumerable.Range(0, 2 * IntSegment + 1), target.Skip(2).Take(2 * IntSegment + 1));
        Assert.Equal(0, target[^1]);
    }

    [Fact]
    public void CopyTo_ShouldCopyNothing_WhenTheListIsEmpty()
    {
        var target = new[] { 9, 9 };

        new SegmentedList<int>().CopyTo(target, 2);

        Assert.Equal(new[] { 9, 9 }, target);
    }

    [Fact]
    public void CopyTo_ShouldThrow_WhenTheDestinationIsTooSmall()
    {
        var list = new SegmentedList<int>(new[] { 1, 2, 3 });

        Assert.Throws<ArgumentException>(() => list.CopyTo(new int[3], 1));
        Assert.Throws<ArgumentNullException>(() => list.CopyTo(null!, 0));
    }

    // ---- interfaces -----------------------------------------------------------------------------------

    [Fact]
    public void ICollection_IsReadOnly_ShouldBeFalse()
    {
        ICollection<int> collection = new SegmentedList<int>();
        Assert.False(collection.IsReadOnly);
    }

    [Fact]
    public void IList_ShouldExposeThePositionalSurface()
    {
        IList<int> list = new SegmentedList<int>();
        list.Add(1);
        list.Add(3);
        list.Insert(1, 2);
        list[0] = 0;

        Assert.Equal(new[] { 0, 2, 3 }, list);
        Assert.Equal(1, list.IndexOf(2));
        Assert.True(list.Contains(3));
        Assert.True(list.Remove(3));
        list.RemoveAt(0);
        Assert.Equal(new[] { 2 }, list);

        var target = new int[1];
        list.CopyTo(target, 0);
        Assert.Equal(new[] { 2 }, target);

        list.Clear();
        Assert.Equal(0, list.Count);
    }

    [Fact]
    public void IReadOnlyList_ShouldIndexAndCount()
    {
        IReadOnlyList<int> list = new SegmentedList<int>(new[] { 4, 5, 6 });

        Assert.Equal(3, list.Count);
        Assert.Equal(5, list[1]);
    }

    // ---- helpers --------------------------------------------------------------------------------------

    private static int[] Values(ReadOnlySpan<SegmentedBig> span)
    {
        var values = new int[span.Length];
        for (int i = 0; i < span.Length; i++)
            values[i] = span[i].Value;
        return values;
    }

    private static int[] Values(SegmentedList<SegmentedBig> list) => list.Select(b => b.Value).ToArray();

    private static object?[] Segments<T>(SegmentedList<T> list) =>
        (object?[])Field(list, "_segments")!;

    private static int Version<T>(SegmentedList<T> list) => (int)Field(list, "_version")!;

    private static object? Field<T>(SegmentedList<T> list, string name) =>
        typeof(SegmentedList<T>).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(list);
}

/// <summary>
/// A 16 KB element: <see cref="SegmentedList{T}"/> packs four of them per segment, so a dozen elements cross
/// several segment boundaries. Equality compares the first and last slots, which both carry the value.
/// </summary>
[InlineArray(2048)]
internal struct SegmentedBig : IEquatable<SegmentedBig>
{
    private long _element;

    public readonly int Value => (int)this[0];

    public static SegmentedBig Of(int value)
    {
        SegmentedBig big = default;
        big[0] = value;
        big[2047] = value;
        return big;
    }

    public readonly bool Equals(SegmentedBig other) => this[0] == other[0] && this[2047] == other[2047];

    public override readonly bool Equals(object? obj) => obj is SegmentedBig other && Equals(other);

    public override readonly int GetHashCode() => this[0].GetHashCode();
}

/// <summary>
/// A 40,000-byte element, wider than half the 64 KB segment budget, so <see cref="SegmentedList{T}"/> stores one
/// per segment. It cannot be wider than the whole budget: the runtime refuses arrays of a value type of 64 KB or
/// more.
/// </summary>
[InlineArray(5000)]
internal struct SegmentedHuge
{
    private long _element;

    public readonly int Value => (int)this[0];

    public static SegmentedHuge Of(int value)
    {
        SegmentedHuge huge = default;
        huge[0] = value;
        return huge;
    }
}
