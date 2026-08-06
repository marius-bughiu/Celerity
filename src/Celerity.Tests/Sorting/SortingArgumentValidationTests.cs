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

    // ---- aliasing between the key-side and payload-side buffers -------------------------------
    //
    // The kernel ping-pongs between (keys, values) and (keyScratch, valueScratch), writing a key
    // and a payload element to the same index of whichever pair is the destination. If any two of
    // those four spans share storage the second write lands on top of the first, so the result is
    // silently wrong rather than failing — which is what makes these argument checks worth having.
    // Only a same-typed pair can alias through safe code, so `int` keys with an `int` payload is
    // the shape every case below uses.

    [Fact]
    public void Sort_ShouldThrow_WhenThePayloadSharesStorageWithTheKeys()
    {
        int[] buffer = [4, 3, 2, 1];

        var ex = Assert.Throws<ArgumentException>(
            () => RadixSort.Sort<int>(buffer.AsSpan(), buffer.AsSpan()));
        Assert.Equal("values", ex.ParamName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenThePayloadPartiallyOverlapsTheKeys()
    {
        int[] buffer = [4, 3, 2, 1];

        var ex = Assert.Throws<ArgumentException>(
            () => RadixSort.Sort<int>(buffer.AsSpan(0, 3), buffer.AsSpan(1, 3)));
        Assert.Equal("values", ex.ParamName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenAScratchBufferSharesStorageAcrossTheKeyValueDivide()
    {
        int[] keys = [3, 2];
        int[] values = [1, 2];
        int[] keyScratch = new int[2];
        int[] valueScratch = new int[2];

        var keysAliasValueScratch = Assert.Throws<ArgumentException>(
            () => RadixSort.SortWithScratch(keys.AsSpan(), values.AsSpan(), keyScratch.AsSpan(), keys.AsSpan()));
        Assert.Equal("valueScratch", keysAliasValueScratch.ParamName);

        var keyScratchAliasesValues = Assert.Throws<ArgumentException>(
            () => RadixSort.SortWithScratch(keys.AsSpan(), values.AsSpan(), values.AsSpan(), valueScratch.AsSpan()));
        Assert.Equal("values", keyScratchAliasesValues.ParamName);

        var keyScratchAliasesValueScratch = Assert.Throws<ArgumentException>(
            () => RadixSort.SortWithScratch(keys.AsSpan(), values.AsSpan(), keyScratch.AsSpan(), keyScratch.AsSpan()));
        Assert.Equal("valueScratch", keyScratchAliasesValueScratch.ParamName);
    }

    [Fact]
    public void Sort_ShouldNotThrow_WhenThePayloadIsADisjointSliceOfTheSameArray()
    {
        // The guard has to reject overlap, not co-residence: two halves of one buffer are fine.
        int[] buffer = [3, 1, 2, 0, 0, 0];

        RadixSort.Sort<int>(buffer.AsSpan(0, 3), buffer.AsSpan(3, 3));

        Assert.Equal([1, 2, 3], buffer.Take(3));
    }

    [Fact]
    public void TopK_ShouldThrow_WhenTheDestinationSharesStorageWithTheSource()
    {
        int[] buffer = [5, 1, 4, 2, 3];

        var whole = Assert.Throws<ArgumentException>(
            () => PartialSort.TopK<int>(buffer, buffer.AsSpan()));
        Assert.Equal("destination", whole.ParamName);

        var partial = Assert.Throws<ArgumentException>(
            () => PartialSort.TopK<int>(buffer.AsSpan(0, 4), buffer.AsSpan(3, 2)));
        Assert.Equal("destination", partial.ParamName);
    }

    [Fact]
    public void TopK_ShouldSucceed_WhenTheDestinationOnlyNeighboursTheSource()
    {
        // Co-residence in one buffer is fine; only genuine overlap is rejected.
        int[] buffer = [5, 1, 4, 2, 0, 0];

        Assert.Equal(2, PartialSort.TopK<int>(buffer.AsSpan(0, 4), buffer.AsSpan(4, 2)));
        Assert.Equal([5, 4], buffer.AsSpan(4, 2).ToArray());
    }

    [Fact]
    public void ArgSort_ShouldThrow_WhenTheIndexBufferSharesStorageWithTheKeys()
    {
        int[] buffer = [3, 1, 2];

        var ex = Assert.Throws<ArgumentException>(() => RadixSort.ArgSort(buffer, buffer.AsSpan()));
        Assert.Equal("indices", ex.ParamName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenACountingSortBufferSharesStorageWithTheKeys()
    {
        int[] buffer = [1, 0, 1];
        int[] counts = new int[CountingSort.RequiredCounts(0, 1)];

        var payloadAliasesKeys = Assert.Throws<ArgumentException>(
            () => CountingSort.Sort<int>(buffer.AsSpan(), buffer.AsSpan(), 0, 1));
        Assert.Equal("values", payloadAliasesKeys.ParamName);

        var countsAliasKeys = Assert.Throws<ArgumentException>(
            () => CountingSort.SortWithScratch(counts.AsSpan(), 0, 1, counts.AsSpan()));
        Assert.Equal("counts", countsAliasKeys.ParamName);

        int[] keys = [1, 0, 1];
        int[] values = [1, 2, 3];
        var scratchAliasesKeys = Assert.Throws<ArgumentException>(
            () => CountingSort.SortWithScratch(keys.AsSpan(), values.AsSpan(), 0, 1, keys.AsSpan(), counts.AsSpan()));
        Assert.Equal("valueScratch", scratchAliasesKeys.ParamName);
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
