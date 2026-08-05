using Celerity.Sorting;

namespace Celerity.Tests.Sorting;

/// <summary>
/// The argument contract shared by the caller-supplied-buffer overloads: a buffer that is too short
/// is rejected before anything is written, and a scratch buffer that aliases the span it serves is
/// rejected outright — that one would silently corrupt the result rather than fail, since the sort
/// writes both.
/// </summary>
public class SortingArgumentValidationTests
{
    [Fact]
    public void Sort_ShouldThrow_WhenTheScratchBufferIsShorterThanTheKeys()
    {
        int[] keys = [3, 2, 1];
        int[] scratch = new int[2];

        var ex = Assert.Throws<ArgumentException>(() => RadixSort.SortWithScratch(keys.AsSpan(), scratch.AsSpan()));
        Assert.Equal("scratch", ex.ParamName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenTheScratchBufferAliasesTheKeys()
    {
        int[] keys = [3, 2, 1, 0];

        var ex = Assert.Throws<ArgumentException>(
            () => RadixSort.SortWithScratch(keys.AsSpan(0, 2), keys.AsSpan(1, 2)));
        Assert.Equal("scratch", ex.ParamName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenThePayloadIsShorterThanTheKeys()
    {
        int[] keys = [3, 2, 1];
        string[] values = new string[2];

        var ex = Assert.Throws<ArgumentException>(() => RadixSort.Sort(keys.AsSpan(), values.AsSpan()));
        Assert.Equal("values", ex.ParamName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenTheKeyScratchBufferIsTooShort()
    {
        int[] keys = [3, 2, 1];
        int[] values = [1, 2, 3];

        var ex = Assert.Throws<ArgumentException>(
            () => RadixSort.SortWithScratch(keys.AsSpan(), values.AsSpan(), new int[2].AsSpan(), new int[3].AsSpan()));
        Assert.Equal("keyScratch", ex.ParamName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenTheValueScratchBufferIsTooShort()
    {
        int[] keys = [3, 2, 1];
        int[] values = [1, 2, 3];

        var ex = Assert.Throws<ArgumentException>(
            () => RadixSort.SortWithScratch(keys.AsSpan(), values.AsSpan(), new int[3].AsSpan(), new int[2].AsSpan()));
        Assert.Equal("valueScratch", ex.ParamName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenTheKeyScratchBufferAliasesTheKeys()
    {
        int[] keys = [3, 2, 1, 0];
        int[] values = [1, 2];

        var ex = Assert.Throws<ArgumentException>(
            () => RadixSort.SortWithScratch(keys.AsSpan(0, 2), values.AsSpan(), keys.AsSpan(1, 2), new int[2].AsSpan()));
        Assert.Equal("keyScratch", ex.ParamName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenTheValueScratchBufferAliasesThePayload()
    {
        int[] keys = [3, 2];
        int[] values = [1, 2, 3, 4];

        var ex = Assert.Throws<ArgumentException>(
            () => RadixSort.SortWithScratch(keys.AsSpan(), values.AsSpan(0, 2), new int[2].AsSpan(), values.AsSpan(1, 2)));
        Assert.Equal("valueScratch", ex.ParamName);
    }

    [Fact]
    public void ArgSort_ShouldThrow_WhenTheIndexBufferIsShorterThanTheKeys()
    {
        int[] keys = [3, 2, 1];

        var ex = Assert.Throws<ArgumentException>(() => RadixSort.ArgSort(keys, new int[2].AsSpan()));
        Assert.Equal("indices", ex.ParamName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenTheCounterBufferIsShorterThanTheUInt16Range()
    {
        ushort[] keys = [3, 2, 1];

        var ex = Assert.Throws<ArgumentException>(() => CountingSort.SortWithScratch(keys.AsSpan(), new int[8].AsSpan()));
        Assert.Equal("counts", ex.ParamName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenTheCounterBufferIsShorterThanTheDeclaredRange()
    {
        int[] keys = [3, 2, 1];

        var ex = Assert.Throws<ArgumentException>(
            () => CountingSort.SortWithScratch(keys.AsSpan(), 0, 10, new int[4].AsSpan()));
        Assert.Equal("counts", ex.ParamName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenACountingSortPayloadIsShorterThanTheKeys()
    {
        byte[] keys = [3, 2, 1];

        var ex = Assert.Throws<ArgumentException>(
            () => CountingSort.Sort<int>(keys.AsSpan(), new int[2].AsSpan()));
        Assert.Equal("values", ex.ParamName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenACountingSortValueScratchBufferIsTooShort()
    {
        byte[] keys = [3, 2, 1];
        int[] values = [1, 2, 3];

        var ex = Assert.Throws<ArgumentException>(
            () => CountingSort.SortWithScratch(keys.AsSpan(), values.AsSpan(), new int[2].AsSpan()));
        Assert.Equal("valueScratch", ex.ParamName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenACountingSortValueScratchBufferAliasesThePayload()
    {
        byte[] keys = [3, 2];
        int[] values = [1, 2, 3, 4];

        var ex = Assert.Throws<ArgumentException>(
            () => CountingSort.SortWithScratch(keys.AsSpan(), values.AsSpan(0, 2), values.AsSpan(1, 2)));
        Assert.Equal("valueScratch", ex.ParamName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenAUInt16CountingSortBufferIsTooShort()
    {
        ushort[] keys = [3, 2, 1];
        int[] values = [1, 2, 3];

        var tooShortScratch = Assert.Throws<ArgumentException>(
            () => CountingSort.SortWithScratch(
                keys.AsSpan(),
                values.AsSpan(),
                new int[2].AsSpan(),
                new int[CountingSort.UInt16Range].AsSpan()));
        Assert.Equal("valueScratch", tooShortScratch.ParamName);

        var tooShortCounts = Assert.Throws<ArgumentException>(
            () => CountingSort.SortWithScratch(keys.AsSpan(), values.AsSpan(), new int[3].AsSpan(), new int[3].AsSpan()));
        Assert.Equal("counts", tooShortCounts.ParamName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenARangeCountingSortBufferIsTooShort()
    {
        int[] keys = [3, 2, 1];
        string[] values = new string[3];

        var tooShortScratch = Assert.Throws<ArgumentException>(
            () => CountingSort.SortWithScratch(keys.AsSpan(), values.AsSpan(), 0, 5, new string[2].AsSpan(), new int[6].AsSpan()));
        Assert.Equal("valueScratch", tooShortScratch.ParamName);

        var tooShortCounts = Assert.Throws<ArgumentException>(
            () => CountingSort.SortWithScratch(keys.AsSpan(), values.AsSpan(), 0, 5, new string[3].AsSpan(), new int[3].AsSpan()));
        Assert.Equal("counts", tooShortCounts.ParamName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenARangeCountingSortPayloadIsShorterThanTheKeys()
    {
        int[] keys = [3, 2, 1];
        string[] values = new string[2];

        var ex = Assert.Throws<ArgumentException>(
            () => CountingSort.Sort(keys.AsSpan(), values.AsSpan(), 0, 5));
        Assert.Equal("values", ex.ParamName);
    }
}
