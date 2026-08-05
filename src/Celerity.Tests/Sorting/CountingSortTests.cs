using Celerity.Sorting;

namespace Celerity.Tests.Sorting;

/// <summary>
/// Behavioural tests for <see cref="CountingSort"/> — the ordering contract for the three key
/// shapes (<see cref="byte"/>, <see cref="ushort"/>, and <see cref="int"/> over a declared range),
/// their key+payload forms, the stability the payload forms promise, and the range validation that
/// separates a usable declared range from an unusable one.
/// </summary>
public class CountingSortTests
{
    // ---- byte keys ----------------------------------------------------------------------------

    [Fact]
    public void Sort_ShouldOrderAscending_WhenKeysAreBytes()
    {
        byte[] keys = [5, 255, 0, 5, 1];

        CountingSort.Sort(keys.AsSpan());

        Assert.Equal<byte>([0, 1, 5, 5, 255], keys);
    }

    [Fact]
    public void Sort_ShouldDoNothing_WhenTheByteSpanIsEmpty()
    {
        byte[] keys = [];

        CountingSort.Sort(keys.AsSpan());

        Assert.Empty(keys);
    }

    [Fact]
    public void Sort_ShouldMoveThePayload_WhenKeysAreBytes()
    {
        byte[] keys = [2, 1, 2, 0];
        string[] values = ["a", "b", "c", "d"];

        CountingSort.Sort(keys.AsSpan(), values.AsSpan());

        Assert.Equal<byte>([0, 1, 2, 2], keys);
        Assert.Equal(["d", "b", "a", "c"], values);
    }

    [Fact]
    public void Sort_ShouldMoveThePayload_WhenByteKeysUseACallerSuppliedScratchBuffer()
    {
        byte[] keys = [3, 1, 2];
        int[] values = [30, 10, 20];

        CountingSort.SortWithScratch(keys.AsSpan(), values.AsSpan(), new int[3].AsSpan());

        Assert.Equal<byte>([1, 2, 3], keys);
        Assert.Equal([10, 20, 30], values);
    }

    // ---- ushort keys --------------------------------------------------------------------------

    [Fact]
    public void Sort_ShouldOrderAscending_WhenKeysAreUInt16()
    {
        ushort[] keys = [40_000, 0, 65_535, 7];

        CountingSort.Sort(keys.AsSpan());

        Assert.Equal<ushort>([0, 7, 40_000, 65_535], keys);
    }

    [Fact]
    public void Sort_ShouldOrderAscending_WhenUInt16KeysUseACallerSuppliedCounterBuffer()
    {
        ushort[] keys = [9, 3, 6];
        int[] counts = new int[CountingSort.UInt16Range];

        CountingSort.SortWithScratch(keys.AsSpan(), counts.AsSpan());

        Assert.Equal<ushort>([3, 6, 9], keys);
    }

    [Fact]
    public void Sort_ShouldMoveThePayload_WhenKeysAreUInt16()
    {
        ushort[] keys = [2, 1, 2];
        string[] values = ["a", "b", "c"];

        CountingSort.Sort(keys.AsSpan(), values.AsSpan());

        Assert.Equal<ushort>([1, 2, 2], keys);
        Assert.Equal(["b", "a", "c"], values);
    }

    [Fact]
    public void Sort_ShouldMoveThePayload_WhenUInt16KeysUseCallerSuppliedBuffers()
    {
        ushort[] keys = [2, 1, 2];
        int[] values = [20, 10, 21];

        CountingSort.SortWithScratch(
            keys.AsSpan(),
            values.AsSpan(),
            new int[3].AsSpan(),
            new int[CountingSort.UInt16Range].AsSpan());

        Assert.Equal<ushort>([1, 2, 2], keys);
        Assert.Equal([10, 20, 21], values);
    }

    // ---- int keys over a declared range -------------------------------------------------------

    [Fact]
    public void Sort_ShouldOrderAscending_WhenKeysAreIntegersOverADeclaredRange()
    {
        int[] keys = [3, -2, 0, 3, -5];

        CountingSort.Sort(keys.AsSpan(), -5, 3);

        Assert.Equal([-5, -2, 0, 3, 3], keys);
    }

    [Fact]
    public void Sort_ShouldOrderAscending_WhenTheRangeIsASingleValue()
    {
        int[] keys = [4, 4, 4];

        CountingSort.Sort(keys.AsSpan(), 4, 4);

        Assert.Equal([4, 4, 4], keys);
    }

    [Fact]
    public void Sort_ShouldOrderAscending_WhenTheRangeUsesACallerSuppliedCounterBuffer()
    {
        int[] keys = [10, 8, 9];
        int[] counts = new int[CountingSort.RequiredCounts(8, 10)];

        CountingSort.SortWithScratch(keys.AsSpan(), 8, 10, counts.AsSpan());

        Assert.Equal([8, 9, 10], keys);
    }

    [Fact]
    public void Sort_ShouldMoveThePayload_WhenKeysAreIntegersOverADeclaredRange()
    {
        int[] keys = [1, -1, 1, 0];
        string[] values = ["a", "b", "c", "d"];

        CountingSort.Sort(keys.AsSpan(), values.AsSpan(), -1, 1);

        Assert.Equal([-1, 0, 1, 1], keys);
        Assert.Equal(["b", "d", "a", "c"], values);
    }

    [Fact]
    public void Sort_ShouldMoveThePayload_WhenTheRangeUsesCallerSuppliedBuffers()
    {
        int[] keys = [1, -1, 0];
        int[] values = [10, -10, 0];

        CountingSort.SortWithScratch(
            keys.AsSpan(),
            values.AsSpan(),
            -1,
            1,
            new int[3].AsSpan(),
            new int[CountingSort.RequiredCounts(-1, 1)].AsSpan());

        Assert.Equal([-1, 0, 1], keys);
        Assert.Equal([-10, 0, 10], values);
    }

    [Fact]
    public void Sort_ShouldDoNothing_WhenAByteOrUInt16SpanHasFewerThanTwoKeys()
    {
        // The short-span guard is what stops a one-element ushort sort from clearing a
        // 65,536-counter array to do nothing.
        byte[] singleByte = [9];
        ushort[] singleShort = [9];
        ushort[] emptyShorts = [];
        int[] payload = [1];

        CountingSort.Sort(singleByte.AsSpan());
        CountingSort.Sort<int>(singleByte.AsSpan(), payload.AsSpan());
        CountingSort.SortWithScratch(singleByte.AsSpan(), payload.AsSpan(), new int[1].AsSpan());

        CountingSort.Sort(emptyShorts.AsSpan());
        CountingSort.Sort(singleShort.AsSpan());
        CountingSort.SortWithScratch(singleShort.AsSpan(), new int[CountingSort.UInt16Range].AsSpan());
        CountingSort.Sort<int>(singleShort.AsSpan(), payload.AsSpan());
        CountingSort.SortWithScratch(
            singleShort.AsSpan(),
            payload.AsSpan(),
            new int[1].AsSpan(),
            new int[CountingSort.UInt16Range].AsSpan());

        Assert.Equal<byte>([9], singleByte);
        Assert.Equal<ushort>([9], singleShort);
        Assert.Empty(emptyShorts);
        Assert.Equal([1], payload);
    }

    [Fact]
    public void RequiredCounts_ShouldReturnTheInclusiveWidth_WhenTheRangeIsValid()
    {
        Assert.Equal(1, CountingSort.RequiredCounts(0, 0));
        Assert.Equal(11, CountingSort.RequiredCounts(-5, 5));
        Assert.Equal(CountingSort.ByteRange, CountingSort.RequiredCounts(0, 255));
    }

    // ---- validation ---------------------------------------------------------------------------

    [Fact]
    public void Sort_ShouldThrow_WhenTheRangeEndIsBelowItsStart()
    {
        int[] keys = [1];

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => CountingSort.Sort(keys.AsSpan(), 5, 4));
        Assert.Equal("max", ex.ParamName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenTheRangeNeedsMoreCountersThanAnArrayCanHold()
    {
        int[] keys = [1];

        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => CountingSort.Sort(keys.AsSpan(), int.MinValue, int.MaxValue));
        Assert.Equal("max", ex.ParamName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenAKeyFallsBelowTheDeclaredRange()
    {
        int[] keys = [0, -1];

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => CountingSort.Sort(keys.AsSpan(), 0, 10));
        Assert.Equal("keys", ex.ParamName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenAKeyRisesAboveTheDeclaredRange()
    {
        int[] keys = [0, 11];
        int[] values = [0, 1];

        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => CountingSort.Sort(keys.AsSpan(), values.AsSpan(), 0, 10));
        Assert.Equal("keys", ex.ParamName);
    }

    [Fact]
    public void RequiredCounts_ShouldThrow_WhenTheRangeIsInverted()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CountingSort.RequiredCounts(1, 0));
    }
}
