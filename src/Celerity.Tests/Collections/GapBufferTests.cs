using System.Reflection;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Unit coverage for <see cref="GapBuffer{T}"/>: construction, the positional API against its argument
/// contract, where each edit leaves the gap (the cost model <see cref="GapBuffer{T}.GapPosition"/> exposes),
/// growth and trimming, the zero-copy span view, and the GC contract that a slot inside the gap holds no
/// reference.
/// </summary>
public class GapBufferTests
{
    private static readonly FieldInfo ItemsField =
        typeof(GapBuffer<string>).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!;

    // Built from an array, so the backing array is exactly `count` long and the gap is empty, at the end.
    private static GapBuffer<int> Filled(int count) => new(Enumerable.Range(0, count).ToArray());

    // ---- construction -----------------------------------------------------------------------------

    [Fact]
    public void DefaultConstructor_ShouldAllocateNothing()
    {
        var buffer = new GapBuffer<int>();

        Assert.Empty(buffer);
        Assert.Equal(0, buffer.Capacity);
        Assert.Equal(0, buffer.GapPosition);
    }

    [Fact]
    public void CapacityConstructor_ShouldPreSizeTheGap()
    {
        var buffer = new GapBuffer<int>(10);

        Assert.Empty(buffer);
        Assert.Equal(10, buffer.Capacity);

        for (int i = 0; i < 10; i++)
            buffer.Add(i);
        Assert.Equal(10, buffer.Capacity);
        Assert.Equal(Enumerable.Range(0, 10), buffer);
    }

    [Fact]
    public void CapacityConstructor_WithZero_ShouldAllocateNothing()
    {
        var buffer = new GapBuffer<int>(0);

        Assert.Equal(0, buffer.Capacity);
        buffer.Add(1);
        Assert.Equal(new[] { 1 }, buffer);
    }

    [Fact]
    public void CapacityConstructor_ShouldThrow_WhenNegative()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new GapBuffer<int>(-1));
        Assert.Equal("capacity", ex.ParamName);
    }

    [Fact]
    public void CollectionConstructor_ShouldThrow_WhenNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new GapBuffer<int>((IEnumerable<int>)null!));
        Assert.Equal("collection", ex.ParamName);
    }

    [Fact]
    public void CollectionConstructor_FromAnICollection_ShouldSizeExactly_WithTheGapAtTheEnd()
    {
        var buffer = new GapBuffer<int>(new[] { 3, 1, 2 });

        Assert.Equal(new[] { 3, 1, 2 }, buffer);
        Assert.Equal(3, buffer.Capacity);
        Assert.Equal(3, buffer.GapPosition);
    }

    [Fact]
    public void CollectionConstructor_FromAnEmptyICollection_ShouldAllocateNothing()
    {
        var buffer = new GapBuffer<int>(new List<int>());

        Assert.Empty(buffer);
        Assert.Equal(0, buffer.Capacity);
    }

    [Fact]
    public void CollectionConstructor_FromALazySequence_ShouldKeepEnumerationOrder()
    {
        var buffer = new GapBuffer<int>(Enumerable.Range(0, 100).Where(i => i % 2 == 0));

        Assert.Equal(Enumerable.Range(0, 100).Where(i => i % 2 == 0), buffer);
        Assert.Equal(50, buffer.GapPosition);

        // The build is not a mutation an enumerator taken afterwards could have observed.
        GapBuffer<int>.Enumerator e = buffer.GetEnumerator();
        Assert.True(e.MoveNext());
    }

    // ---- indexer ----------------------------------------------------------------------------------

    [Fact]
    public void Indexer_ShouldReadAndWrite_OnBothSidesOfTheGap()
    {
        GapBuffer<int> buffer = Filled(10);
        buffer.Insert(5, 100); // gap now sits after position 5
        Assert.Equal(6, buffer.GapPosition);

        Assert.Equal(0, buffer[0]);
        Assert.Equal(4, buffer[4]);
        Assert.Equal(100, buffer[5]);
        Assert.Equal(5, buffer[6]);
        Assert.Equal(9, buffer[10]);

        buffer[5] = -5;  // last before the gap
        buffer[6] = -6;  // first after it
        buffer[10] = -10;
        Assert.Equal(new[] { 0, 1, 2, 3, 4, -5, -6, 6, 7, 8, -10 }, buffer);
        Assert.Equal(6, buffer.GapPosition);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Indexer_ShouldThrow_WhenOutOfRange(int index)
    {
        GapBuffer<int> buffer = Filled(3);

        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => buffer[index]).ParamName);
        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => buffer[index] = 0).ParamName);
    }

    [Fact]
    public void Indexer_ShouldThrow_OnAnEmptyBufferWithCapacity()
    {
        var buffer = new GapBuffer<int>(8);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[0]);
    }

    // ---- insertion --------------------------------------------------------------------------------

    [Fact]
    public void Insert_AtTheGap_ShouldAdvanceTheGapByOne()
    {
        GapBuffer<int> buffer = Filled(10);
        buffer.Insert(4, -1);
        Assert.Equal(5, buffer.GapPosition);

        buffer.Insert(5, -2);
        buffer.Insert(6, -3);

        Assert.Equal(7, buffer.GapPosition);
        Assert.Equal(new[] { 0, 1, 2, 3, -1, -2, -3, 4, 5, 6, 7, 8, 9 }, buffer);
    }

    [Fact]
    public void Insert_BehindAndAheadOfTheGap_ShouldMoveItThere()
    {
        var buffer = new GapBuffer<int>(32);
        for (int i = 0; i < 10; i++)
            buffer.Add(i);

        buffer.Insert(2, -1);
        Assert.Equal(3, buffer.GapPosition);

        buffer.Insert(9, -2);
        Assert.Equal(10, buffer.GapPosition);

        buffer.Insert(0, -3);
        Assert.Equal(1, buffer.GapPosition);

        buffer.Insert(buffer.Count, -4);
        Assert.Equal(buffer.Count, buffer.GapPosition);

        Assert.Equal(new[] { -3, 0, 1, -1, 2, 3, 4, 5, 6, 7, -2, 8, 9, -4 }, buffer);
        Assert.Equal(32, buffer.Capacity);
    }

    [Fact]
    public void Insert_WhenFull_ShouldDoubleAndReopenTheGapAtTheInsertPosition()
    {
        GapBuffer<int> buffer = Filled(8); // full: gap is empty
        Assert.Equal(8, buffer.Capacity);

        buffer.Insert(3, -1);

        Assert.Equal(16, buffer.Capacity);
        Assert.Equal(4, buffer.GapPosition);
        Assert.Equal(new[] { 0, 1, 2, -1, 3, 4, 5, 6, 7 }, buffer);
    }

    [Fact]
    public void Add_FromEmpty_ShouldGrowFromTheDefaultCapacity()
    {
        var buffer = new GapBuffer<int>();
        buffer.Add(1);
        Assert.Equal(4, buffer.Capacity);

        for (int i = 2; i <= 5; i++)
            buffer.Add(i);

        Assert.Equal(8, buffer.Capacity);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, buffer);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void Insert_ShouldThrow_WhenOutOfRange(int index)
    {
        GapBuffer<int> buffer = Filled(3);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Insert(index, 0));
        Assert.Equal("index", ex.ParamName);
        Assert.Equal(new[] { 0, 1, 2 }, buffer);
    }

    [Fact]
    public void InsertRange_ShouldPlaceTheRunInOrder_AndLeaveTheGapAfterIt()
    {
        var buffer = new GapBuffer<int>(20);
        for (int i = 0; i < 5; i++)
            buffer.Add(i);

        buffer.InsertRange(2, new[] { 10, 11, 12 });

        Assert.Equal(new[] { 0, 1, 10, 11, 12, 2, 3, 4 }, buffer);
        Assert.Equal(5, buffer.GapPosition);
        Assert.Equal(20, buffer.Capacity);
    }

    [Fact]
    public void InsertRange_LargerThanTheGap_ShouldGrowToFitInOneStep()
    {
        GapBuffer<int> buffer = Filled(4);

        buffer.InsertRange(1, Enumerable.Range(100, 50).ToArray());

        Assert.Equal(54, buffer.Capacity); // doubling to 8 is not enough, so the request wins
        Assert.Equal(51, buffer.GapPosition);
        Assert.Equal(new[] { 0 }.Concat(Enumerable.Range(100, 50)).Concat(new[] { 1, 2, 3 }), buffer);
    }

    [Fact]
    public void InsertRange_Empty_ShouldBeANoOp_ThatKeepsEnumeratorsValid()
    {
        GapBuffer<int> buffer = Filled(5);
        GapBuffer<int>.Enumerator e = buffer.GetEnumerator();

        buffer.InsertRange(1, ReadOnlySpan<int>.Empty);

        Assert.Equal(5, buffer.GapPosition);
        Assert.True(e.MoveNext());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void InsertRange_ShouldThrow_WhenOutOfRange_EvenForAnEmptyRun(int index)
    {
        GapBuffer<int> buffer = Filled(3);

        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(
            () => buffer.InsertRange(index, ReadOnlySpan<int>.Empty)).ParamName);
        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(
            () => buffer.InsertRange(index, new[] { 1 })).ParamName);
    }

    [Fact]
    public void InsertRange_OfItsOwnSpan_ShouldCopyTheSourceBeforeMovingTheGap()
    {
        // The gap sits at the front and has room, so nothing grows: moving it to the end shifts every element
        // left over the span's own slots and clears the ones left behind, so without the overlap copy the
        // inserted run would read moved and cleared slots instead of the elements it was taken from.
        var buffer = new GapBuffer<string>(32);
        buffer.InsertRange(0, new[] { "a", "b", "c", "d" });
        buffer.Insert(0, "x");
        buffer.RemoveAt(0); // gap at 0: [ | a, b, c, d]

        buffer.GetSpans(out ReadOnlySpan<string> before, out ReadOnlySpan<string> after);
        Assert.True(before.IsEmpty);
        Assert.Equal(new[] { "a", "b", "c", "d" }, after.ToArray());

        buffer.InsertRange(buffer.Count, after);
        Assert.Equal(new[] { "a", "b", "c", "d", "a", "b", "c", "d" }, buffer);
        Assert.Equal(32, buffer.Capacity);

        // And the other half: a slice of the run before the gap, inserted at the front.
        buffer.GetSpans(out before, out _);
        buffer.InsertRange(0, before.Slice(1, 2));
        Assert.Equal(new[] { "b", "c", "a", "b", "c", "d", "a", "b", "c", "d" }, buffer);
    }

    // ---- removal ----------------------------------------------------------------------------------

    [Fact]
    public void RemoveAt_EitherSideOfTheGap_ShouldMoveNothing()
    {
        GapBuffer<int> buffer = Filled(10);
        buffer.Insert(5, -1); // [0..4, -1 | 5..9], gap at 6

        buffer.RemoveAt(5);   // backspace: the element just before the gap
        Assert.Equal(5, buffer.GapPosition);

        buffer.RemoveAt(5);   // delete: the element just after it
        Assert.Equal(5, buffer.GapPosition);

        Assert.Equal(new[] { 0, 1, 2, 3, 4, 6, 7, 8, 9 }, buffer);
    }

    [Fact]
    public void RemoveAt_AwayFromTheGap_ShouldMoveItToTheRemovedPosition()
    {
        GapBuffer<int> buffer = Filled(10); // gap at 10

        buffer.RemoveAt(2);
        Assert.Equal(2, buffer.GapPosition);

        buffer.RemoveAt(6);
        Assert.Equal(6, buffer.GapPosition);

        Assert.Equal(new[] { 0, 1, 3, 4, 5, 6, 8, 9 }, buffer);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void RemoveAt_ShouldThrow_WhenOutOfRange(int index)
    {
        GapBuffer<int> buffer = Filled(3);

        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => buffer.RemoveAt(index)).ParamName);
    }

    [Fact]
    public void RemoveRange_StraddlingTheGap_ShouldAbsorbBothSidesWithoutMovingIt()
    {
        GapBuffer<int> buffer = Filled(10);
        buffer.Insert(5, -1);
        buffer.RemoveAt(5); // gap at 5, elements intact

        buffer.RemoveRange(3, 4); // removes 3, 4 (before the gap) and 5, 6 (after it)

        Assert.Equal(3, buffer.GapPosition);
        Assert.Equal(new[] { 0, 1, 2, 7, 8, 9 }, buffer);
    }

    [Fact]
    public void RemoveRange_EntirelyBeforeOrAfterTheGap_ShouldMoveItToTheNearerEnd()
    {
        GapBuffer<int> buffer = Filled(20); // gap at 20

        buffer.RemoveRange(2, 3);          // before the gap: it moves to 5, then absorbs 2..4
        Assert.Equal(2, buffer.GapPosition);
        Assert.Equal(17, buffer.Count);

        buffer.RemoveRange(10, 2);         // after the gap: it moves to 10, then absorbs 10..11
        Assert.Equal(10, buffer.GapPosition);

        Assert.Equal(new[] { 0, 1, 5, 6, 7, 8, 9, 10, 11, 12, 15, 16, 17, 18, 19 }, buffer);
    }

    [Fact]
    public void RemoveRange_Everything_ShouldEmptyTheBuffer()
    {
        GapBuffer<int> buffer = Filled(10);
        buffer.RemoveRange(0, 10);

        Assert.Empty(buffer);
        Assert.Equal(0, buffer.GapPosition);
        Assert.Equal(10, buffer.Capacity);
    }

    [Fact]
    public void RemoveRange_OfZero_ShouldBeANoOp_ThatKeepsEnumeratorsValid()
    {
        GapBuffer<int> buffer = Filled(5);
        GapBuffer<int>.Enumerator e = buffer.GetEnumerator();

        buffer.RemoveRange(2, 0);
        buffer.RemoveRange(5, 0);

        Assert.Equal(5, buffer.GapPosition);
        Assert.True(e.MoveNext());
    }

    [Fact]
    public void RemoveRange_ShouldValidateItsArguments_LikeList()
    {
        GapBuffer<int> buffer = Filled(5);

        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => buffer.RemoveRange(-1, 1)).ParamName);
        Assert.Equal("count", Assert.Throws<ArgumentOutOfRangeException>(() => buffer.RemoveRange(0, -1)).ParamName);
        Assert.Throws<ArgumentException>(() => buffer.RemoveRange(4, 2));
        Assert.Throws<ArgumentException>(() => buffer.RemoveRange(6, 0));
        Assert.Throws<ArgumentException>(() => buffer.RemoveRange(1, int.MaxValue));
        Assert.Equal(Enumerable.Range(0, 5), buffer);
    }

    [Fact]
    public void Remove_ShouldRemoveTheFirstMatch_OnEitherSideOfTheGap()
    {
        var buffer = new GapBuffer<string>(new[] { "a", "b", "a", "c", "b" });
        buffer.Insert(2, "x"); // [a, b, x | a, c, b]

        Assert.True(buffer.Remove("a"));
        Assert.Equal(new[] { "b", "x", "a", "c", "b" }, buffer);

        Assert.True(buffer.Remove("c")); // after the gap
        Assert.Equal(new[] { "b", "x", "a", "b" }, buffer);

        Assert.False(buffer.Remove("zzz"));
        Assert.False(buffer.Remove(null!));
    }

    [Fact]
    public void IndexOfAndContains_ShouldSearchBothHalves_WithoutMovingTheGap()
    {
        GapBuffer<int> buffer = Filled(10);
        buffer.Insert(5, 3); // a duplicate 3, before the gap at 6

        Assert.Equal(3, buffer.IndexOf(3));
        Assert.Equal(6, buffer.IndexOf(5));   // first element after the gap
        Assert.Equal(10, buffer.IndexOf(9));
        Assert.Equal(-1, buffer.IndexOf(42));
        Assert.True(buffer.Contains(9));
        Assert.False(buffer.Contains(-1));
        Assert.Equal(6, buffer.GapPosition);
    }

    [Fact]
    public void IndexOf_OnAnEmptyBuffer_ShouldReturnMinusOne()
    {
        Assert.Equal(-1, new GapBuffer<int>().IndexOf(0));
        Assert.Equal(-1, new GapBuffer<int>(4).IndexOf(0)); // the gap's default slots are not elements
    }

    [Fact]
    public void Clear_ShouldKeepTheArray_AndMakeAllOfItTheGap()
    {
        GapBuffer<string> buffer = new(new[] { "a", "b", "c" });
        buffer.Insert(1, "x");
        int capacity = buffer.Capacity;

        buffer.Clear();

        Assert.Empty(buffer);
        Assert.Equal(capacity, buffer.Capacity);
        Assert.Equal(0, buffer.GapPosition);
        AssertGapHoldsNoReferences(buffer);

        buffer.Add("z");
        Assert.Equal(new[] { "z" }, buffer);
    }

    [Fact]
    public void Clear_OfValueTypes_ShouldEmptyTheBuffer()
    {
        GapBuffer<int> buffer = Filled(10);
        buffer.Insert(3, -1);

        buffer.Clear();

        Assert.Empty(buffer);
        Assert.Equal(0, buffer.GapPosition);
        buffer.Insert(0, 5);
        Assert.Equal(new[] { 5 }, buffer);
    }

    // ---- capacity ---------------------------------------------------------------------------------

    [Fact]
    public void EnsureCapacity_ShouldGrow_AndKeepTheGapPosition()
    {
        GapBuffer<int> buffer = Filled(10);
        buffer.Insert(4, -1);
        int gap = buffer.GapPosition;

        Assert.Equal(100, buffer.EnsureCapacity(100));

        Assert.Equal(100, buffer.Capacity);
        Assert.Equal(gap, buffer.GapPosition);
        Assert.Equal(new[] { 0, 1, 2, 3, -1, 4, 5, 6, 7, 8, 9 }, buffer);
    }

    [Fact]
    public void EnsureCapacity_ShouldDouble_WhenThatExceedsTheRequest()
    {
        GapBuffer<int> buffer = Filled(10);

        Assert.Equal(20, buffer.EnsureCapacity(11));
    }

    [Fact]
    public void EnsureCapacity_ShouldDoNothing_WhenAlreadyLargeEnough()
    {
        GapBuffer<int> buffer = Filled(10);
        GapBuffer<int>.Enumerator e = buffer.GetEnumerator();

        Assert.Equal(10, buffer.EnsureCapacity(10));
        Assert.Equal(10, buffer.EnsureCapacity(0));
        Assert.True(e.MoveNext());
    }

    [Fact]
    public void EnsureCapacity_ShouldThrow_WhenNegative()
    {
        Assert.Equal("capacity", Assert.Throws<ArgumentOutOfRangeException>(
            () => new GapBuffer<int>().EnsureCapacity(-1)).ParamName);
    }

    [Fact]
    public void TrimExcess_ShouldShrinkToCount_AndKeepTheGapPosition()
    {
        var buffer = new GapBuffer<int>(64);
        for (int i = 0; i < 10; i++)
            buffer.Add(i);
        buffer.Insert(3, -1);

        buffer.TrimExcess();

        Assert.Equal(11, buffer.Capacity);
        Assert.Equal(4, buffer.GapPosition);
        Assert.Equal(new[] { 0, 1, 2, -1, 3, 4, 5, 6, 7, 8, 9 }, buffer);

        // The gap is now empty, so the next insert grows the array again.
        buffer.Insert(4, -2);
        Assert.Equal(22, buffer.Capacity);
        Assert.Equal(new[] { 0, 1, 2, -1, -2, 3, 4, 5, 6, 7, 8, 9 }, buffer);
    }

    [Fact]
    public void TrimExcess_OnAnEmptyBuffer_ShouldReleaseTheArray()
    {
        var buffer = new GapBuffer<int>(16);

        buffer.TrimExcess();

        Assert.Equal(0, buffer.Capacity);
        buffer.Add(1);
        Assert.Equal(new[] { 1 }, buffer);
    }

    [Fact]
    public void TrimExcess_ShouldDoNothing_WhenTheArrayIsFull()
    {
        GapBuffer<int> buffer = Filled(5);
        GapBuffer<int>.Enumerator e = buffer.GetEnumerator();

        buffer.TrimExcess();

        Assert.Equal(5, buffer.Capacity);
        Assert.True(e.MoveNext());
    }

    // ---- views and copies -------------------------------------------------------------------------

    [Fact]
    public void GetSpans_ShouldSplitAtTheGap()
    {
        GapBuffer<int> buffer = Filled(6);
        buffer.RemoveAt(2); // gap at 2

        buffer.GetSpans(out ReadOnlySpan<int> before, out ReadOnlySpan<int> after);

        Assert.Equal(new[] { 0, 1 }, before.ToArray());
        Assert.Equal(new[] { 3, 4, 5 }, after.ToArray());
    }

    [Fact]
    public void GetSpans_ShouldReturnEmptySpans_ForAnEmptyBuffer()
    {
        new GapBuffer<int>().GetSpans(out ReadOnlySpan<int> before, out ReadOnlySpan<int> after);

        Assert.True(before.IsEmpty);
        Assert.True(after.IsEmpty);
    }

    [Fact]
    public void ToArray_ShouldCopyAcrossTheGap()
    {
        GapBuffer<int> buffer = Filled(6);
        buffer.Insert(3, -1);

        Assert.Equal(new[] { 0, 1, 2, -1, 3, 4, 5 }, buffer.ToArray());
        Assert.Same(Array.Empty<int>(), new GapBuffer<int>(8).ToArray());
    }

    [Fact]
    public void CopyTo_ShouldCopyAcrossTheGap_AtTheOffset()
    {
        GapBuffer<int> buffer = Filled(4);
        buffer.Insert(1, -1);
        var array = new int[7];

        buffer.CopyTo(array, 2);

        Assert.Equal(new[] { 0, 0, 0, -1, 1, 2, 3 }, array);
    }

    [Fact]
    public void IListMembers_ShouldBehaveLikeList()
    {
        IList<int> list = new GapBuffer<int>();
        Assert.False(list.IsReadOnly);

        list.Add(1);
        list.Insert(0, 0);
        Assert.Equal(new[] { 0, 1 }, list);

        IReadOnlyList<int> readOnly = (GapBuffer<int>)list;
        Assert.Equal(2, readOnly.Count);
        Assert.Equal(1, readOnly[1]);
    }

    // ---- GC contract ------------------------------------------------------------------------------

    [Fact]
    public void GapSlots_ShouldHoldNoReferences_AfterEveryKindOfEdit()
    {
        var buffer = new GapBuffer<string>(8);
        for (int i = 0; i < 20; i++)
            buffer.Add(i.ToString());
        AssertGapHoldsNoReferences(buffer);

        buffer.Insert(3, "x");            // gap moves far left, further than its length
        AssertGapHoldsNoReferences(buffer);

        buffer.EnsureCapacity(100);
        buffer.Insert(5, "y");            // short move right inside a long gap
        AssertGapHoldsNoReferences(buffer);

        buffer.Insert(18, "z");           // long move right
        AssertGapHoldsNoReferences(buffer);

        buffer.RemoveAt(0);
        buffer.RemoveAt(buffer.Count - 1);
        buffer.RemoveRange(4, 6);
        AssertGapHoldsNoReferences(buffer);

        Assert.True(buffer.Remove("10"));
        AssertGapHoldsNoReferences(buffer);
    }

    private static void AssertGapHoldsNoReferences(GapBuffer<string> buffer)
    {
        var items = (string?[])ItemsField.GetValue(buffer)!;
        int gapLength = buffer.Capacity - buffer.Count;
        for (int i = buffer.GapPosition; i < buffer.GapPosition + gapLength; i++)
            Assert.Null(items[i]);
    }
}
