using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

public class BitPackedArrayEnumerationTests
{
    [Fact]
    public void GetEnumerator_ShouldYieldEveryElementInOrder()
    {
        ulong[] values = { 3, 0, 7, 1, 5 };
        var packed = new BitPackedArray(values, 3);

        var observed = new List<ulong>();
        foreach (ulong value in packed)
            observed.Add(value);

        Assert.Equal(values, observed);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenTheArrayIsEmpty()
    {
        var packed = new BitPackedArray(0, 8);

        foreach (ulong _ in packed)
            Assert.Fail("an empty array should yield no elements");
    }

    [Fact]
    public void GenericEnumerator_ShouldYieldEveryElement()
    {
        IEnumerable<ulong> enumerable = new BitPackedArray(new ulong[] { 9, 8, 7 }, 4);

        Assert.Equal(new ulong[] { 9, 8, 7 }, enumerable.ToList());
    }

    [Fact]
    public void NonGenericEnumerator_ShouldYieldEveryElement()
    {
        IEnumerable enumerable = new BitPackedArray(new ulong[] { 1, 2 }, 2);

        var observed = new List<object?>();
        foreach (object? value in enumerable)
            observed.Add(value);

        Assert.Equal(new object?[] { 1UL, 2UL }, observed);
    }

    [Fact]
    public void Current_ShouldBeZero_BeforeTheFirstMoveNextAndAfterTheLast()
    {
        var packed = new BitPackedArray(new ulong[] { 6 }, 3);
        BitPackedArray.Enumerator enumerator = packed.GetEnumerator();

        Assert.Equal(0UL, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(6UL, enumerator.Current);
        Assert.Equal(6UL, ((IEnumerator)enumerator).Current);
        Assert.False(enumerator.MoveNext());
        Assert.Equal(0UL, enumerator.Current);
    }

    [Fact]
    public void MoveNext_ShouldKeepReturningFalse_AfterTheEnd()
    {
        var packed = new BitPackedArray(new ulong[] { 1, 2 }, 2);
        BitPackedArray.Enumerator enumerator = packed.GetEnumerator();

        while (enumerator.MoveNext()) { }

        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Reset_ShouldReplayTheSequence()
    {
        var packed = new BitPackedArray(new ulong[] { 4, 5 }, 3);
        BitPackedArray.Enumerator enumerator = packed.GetEnumerator();
        while (enumerator.MoveNext()) { }

        enumerator.Reset();

        Assert.Equal(0UL, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(4UL, enumerator.Current);
        enumerator.Dispose();
    }

    [Fact]
    public void MoveNext_ShouldThrow_WhenAnElementIsSetMidEnumeration()
    {
        var packed = new BitPackedArray(4, 4);
        BitPackedArray.Enumerator enumerator = packed.GetEnumerator();
        enumerator.MoveNext();

        packed[2] = 3;

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_EvenWhenTheWriteStoresTheValueAlreadyThere()
    {
        // The BitSet rule: a write is a write, whether or not it changes the stored bits.
        var packed = new BitPackedArray(4, 4);
        BitPackedArray.Enumerator enumerator = packed.GetEnumerator();

        packed[2] = 0;

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_WhenTheArrayIsModifiedAfterTheEnd()
    {
        var packed = new BitPackedArray(new ulong[] { 1 }, 1);
        BitPackedArray.Enumerator enumerator = packed.GetEnumerator();
        while (enumerator.MoveNext()) { }

        packed.Fill(1);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Enumerators_ShouldBeInvalidated_ByEveryBulkWrite()
    {
        var packed = new BitPackedArray(4, 4);

        var beforeSetRange = packed.GetEnumerator();
        packed.SetRange(1, new ulong[] { 2 });
        Assert.Throws<InvalidOperationException>(() => beforeSetRange.MoveNext());

        var beforeFill = packed.GetEnumerator();
        packed.Fill(0);
        Assert.Throws<InvalidOperationException>(() => beforeFill.MoveNext());

        var beforeClear = packed.GetEnumerator();
        packed.Clear();
        Assert.Throws<InvalidOperationException>(() => beforeClear.MoveNext());
    }

    [Fact]
    public void Enumerators_ShouldSurvive_AnEmptySetRangeAndEveryRejectedWrite()
    {
        var packed = new BitPackedArray(new ulong[] { 1, 2, 3 }, 2);
        BitPackedArray.Enumerator enumerator = packed.GetEnumerator();

        packed.SetRange(1, ReadOnlySpan<ulong>.Empty);
        Assert.Throws<ArgumentOutOfRangeException>(() => packed[0] = 4);
        Assert.Throws<ArgumentException>(() => packed.SetRange(0, new ulong[] { 4 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => packed.Fill(4));
        packed.GetRange(0, new ulong[3]);

        var observed = new List<ulong>();
        while (enumerator.MoveNext())
            observed.Add(enumerator.Current);
        Assert.Equal(new ulong[] { 1, 2, 3 }, observed);
    }

    [Fact]
    public void Reset_ShouldThrow_WhenTheArrayWasModified()
    {
        var packed = new BitPackedArray(2, 2);
        BitPackedArray.Enumerator enumerator = packed.GetEnumerator();

        packed[0] = 1;

        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }
}
