using Celerity.Sorting;

namespace Celerity.Tests.Sorting;

/// <summary>
/// Behavioural tests for <see cref="RadixSort"/> — the ordering contract for every supported key
/// type, in the keys-only, key+payload and argsort shapes, plus the edge shapes (empty, single,
/// all-equal, already sorted, reverse sorted, full-range) that decide which internal passes run.
/// </summary>
public class RadixSortTests
{
    // ---- uint ---------------------------------------------------------------------------------

    [Fact]
    public void Sort_ShouldOrderAscending_WhenKeysAreUInt32()
    {
        uint[] keys = [7, 0, uint.MaxValue, 3, 256, 1_000_000, 3];

        RadixSort.Sort(keys.AsSpan());

        Assert.Equal([0u, 3u, 3u, 7u, 256u, 1_000_000u, uint.MaxValue], keys);
    }

    [Fact]
    public void Sort_ShouldRunASinglePass_WhenEveryKeyFitsInTheLowByte()
    {
        // Only the low digit differs, so three of the four passes are skipped — an odd number of
        // executed passes, which is the case that has to copy the answer back out of the scratch.
        uint[] keys = [200, 1, 255, 0, 128];

        RadixSort.Sort(keys.AsSpan());

        Assert.Equal([0u, 1u, 128u, 200u, 255u], keys);
    }

    [Fact]
    public void Sort_ShouldLeaveTheSpanUnchanged_WhenEveryKeyIsIdentical()
    {
        uint[] keys = [42, 42, 42, 42];

        RadixSort.Sort(keys.AsSpan());

        Assert.Equal([42u, 42u, 42u, 42u], keys);
    }

    [Fact]
    public void Sort_ShouldDoNothing_WhenTheSpanHasFewerThanTwoKeys()
    {
        uint[] empty = [];
        uint[] single = [9];

        RadixSort.Sort(empty.AsSpan());
        RadixSort.Sort(single.AsSpan());

        Assert.Empty(empty);
        Assert.Equal([9u], single);
    }

    [Fact]
    public void Sort_ShouldOrderAscending_WhenGivenACallerSuppliedScratchBuffer()
    {
        uint[] keys = [5, 4, 3, 2, 1];
        uint[] scratch = new uint[8];

        RadixSort.SortWithScratch(keys.AsSpan(), scratch.AsSpan());

        Assert.Equal([1u, 2u, 3u, 4u, 5u], keys);
    }

    [Fact]
    public void Sort_ShouldMoveThePayload_WhenKeysAreUInt32()
    {
        uint[] keys = [3, 1, 2];
        string[] values = ["three", "one", "two"];

        RadixSort.Sort(keys.AsSpan(), values.AsSpan());

        Assert.Equal([1u, 2u, 3u], keys);
        Assert.Equal(["one", "two", "three"], values);
    }

    [Fact]
    public void Sort_ShouldMoveThePayload_WhenGivenCallerSuppliedScratchBuffers()
    {
        uint[] keys = [300, 1, 70_000];
        int[] values = [30, 1, 700];

        RadixSort.SortWithScratch(keys.AsSpan(), values.AsSpan(), new uint[3].AsSpan(), new int[3].AsSpan());

        Assert.Equal([1u, 300u, 70_000u], keys);
        Assert.Equal([1, 30, 700], values);
    }

    [Fact]
    public void ArgSort_ShouldRankWithoutReorderingTheKeys_WhenKeysAreUInt32()
    {
        uint[] keys = [30, 10, 20];
        int[] indices = new int[3];

        RadixSort.ArgSort(keys, indices);

        Assert.Equal([1, 2, 0], indices);
        Assert.Equal([30u, 10u, 20u], keys);
    }

    // ---- int ----------------------------------------------------------------------------------

    [Fact]
    public void Sort_ShouldPlaceNegativesFirst_WhenKeysAreInt32()
    {
        int[] keys = [3, -1, int.MinValue, 0, int.MaxValue, -1_000_000, 2];

        RadixSort.Sort(keys.AsSpan());

        Assert.Equal([int.MinValue, -1_000_000, -1, 0, 2, 3, int.MaxValue], keys);
    }

    [Fact]
    public void Sort_ShouldOrderAscending_WhenInt32KeysUseACallerSuppliedScratchBuffer()
    {
        int[] keys = [-5, 5, -10, 10];
        int[] scratch = new int[4];

        RadixSort.SortWithScratch(keys.AsSpan(), scratch.AsSpan());

        Assert.Equal([-10, -5, 5, 10], keys);
    }

    [Fact]
    public void Sort_ShouldMoveThePayload_WhenKeysAreInt32()
    {
        int[] keys = [1, -1, 0];
        char[] values = ['p', 'n', 'z'];

        RadixSort.Sort(keys.AsSpan(), values.AsSpan());

        Assert.Equal([-1, 0, 1], keys);
        Assert.Equal(['n', 'z', 'p'], values);
    }

    [Fact]
    public void Sort_ShouldMoveThePayload_WhenInt32KeysUseCallerSuppliedScratchBuffers()
    {
        int[] keys = [1, -1, 0];
        int[] values = [10, -10, 0];

        RadixSort.SortWithScratch(keys.AsSpan(), values.AsSpan(), new int[3].AsSpan(), new int[3].AsSpan());

        Assert.Equal([-1, 0, 1], keys);
        Assert.Equal([-10, 0, 10], values);
    }

    [Fact]
    public void ArgSort_ShouldRankWithoutReorderingTheKeys_WhenKeysAreInt32()
    {
        int[] keys = [5, -5, 0];
        int[] indices = new int[3];

        RadixSort.ArgSort(keys, indices);

        Assert.Equal([1, 2, 0], indices);
        Assert.Equal([5, -5, 0], keys);
    }

    [Fact]
    public void ArgSort_ShouldWriteTheOnlyIndex_WhenThereIsASingleKey()
    {
        int[] indices = new int[1];

        RadixSort.ArgSort(new int[] { 7 }, indices);

        Assert.Equal([0], indices);
    }

    // ---- ulong / long -------------------------------------------------------------------------

    [Fact]
    public void Sort_ShouldOrderAscending_WhenKeysAreUInt64()
    {
        ulong[] keys = [ulong.MaxValue, 0, 1UL << 40, 5, 1UL << 40];

        RadixSort.Sort(keys.AsSpan());

        Assert.Equal([0UL, 5UL, 1UL << 40, 1UL << 40, ulong.MaxValue], keys);
    }

    [Fact]
    public void Sort_ShouldOrderAscending_WhenUInt64KeysUseACallerSuppliedScratchBuffer()
    {
        ulong[] keys = [9, 8, 7];

        RadixSort.SortWithScratch(keys.AsSpan(), new ulong[3].AsSpan());

        Assert.Equal([7UL, 8UL, 9UL], keys);
    }

    [Fact]
    public void Sort_ShouldMoveThePayload_WhenKeysAreUInt64()
    {
        ulong[] keys = [2, 1];
        string[] values = ["b", "a"];

        RadixSort.Sort(keys.AsSpan(), values.AsSpan());

        Assert.Equal([1UL, 2UL], keys);
        Assert.Equal(["a", "b"], values);
    }

    [Fact]
    public void Sort_ShouldMoveThePayload_WhenUInt64KeysUseCallerSuppliedScratchBuffers()
    {
        ulong[] keys = [2, 1];
        int[] values = [20, 10];

        RadixSort.SortWithScratch(keys.AsSpan(), values.AsSpan(), new ulong[2].AsSpan(), new int[2].AsSpan());

        Assert.Equal([1UL, 2UL], keys);
        Assert.Equal([10, 20], values);
    }

    [Fact]
    public void ArgSort_ShouldRankWithoutReorderingTheKeys_WhenKeysAreUInt64()
    {
        ulong[] keys = [3, 1, 2];
        int[] indices = new int[3];

        RadixSort.ArgSort(keys, indices);

        Assert.Equal([1, 2, 0], indices);
    }

    [Fact]
    public void Sort_ShouldPlaceNegativesFirst_WhenKeysAreInt64()
    {
        long[] keys = [3, -1, long.MinValue, 0, long.MaxValue, -1_000_000_000_000, 2];

        RadixSort.Sort(keys.AsSpan());

        Assert.Equal([long.MinValue, -1_000_000_000_000, -1, 0, 2, 3, long.MaxValue], keys);
    }

    [Fact]
    public void Sort_ShouldOrderAscending_WhenInt64KeysUseACallerSuppliedScratchBuffer()
    {
        long[] keys = [-2, 2, -1];

        RadixSort.SortWithScratch(keys.AsSpan(), new long[3].AsSpan());

        Assert.Equal([-2L, -1L, 2L], keys);
    }

    [Fact]
    public void Sort_ShouldMoveThePayload_WhenKeysAreInt64()
    {
        long[] keys = [1, -1];
        string[] values = ["pos", "neg"];

        RadixSort.Sort(keys.AsSpan(), values.AsSpan());

        Assert.Equal([-1L, 1L], keys);
        Assert.Equal(["neg", "pos"], values);
    }

    [Fact]
    public void Sort_ShouldMoveThePayload_WhenInt64KeysUseCallerSuppliedScratchBuffers()
    {
        long[] keys = [1, -1];
        int[] values = [1, -1];

        RadixSort.SortWithScratch(keys.AsSpan(), values.AsSpan(), new long[2].AsSpan(), new int[2].AsSpan());

        Assert.Equal([-1L, 1L], keys);
        Assert.Equal([-1, 1], values);
    }

    [Fact]
    public void ArgSort_ShouldRankWithoutReorderingTheKeys_WhenKeysAreInt64()
    {
        long[] keys = [0, -7, 7];
        int[] indices = new int[3];

        RadixSort.ArgSort(keys, indices);

        Assert.Equal([1, 0, 2], indices);
    }

    // ---- float / double -----------------------------------------------------------------------

    [Fact]
    public void Sort_ShouldOrderAscending_WhenKeysAreSingle()
    {
        float[] keys = [1.5f, -2.5f, 0f, float.PositiveInfinity, float.NegativeInfinity, -0f];

        RadixSort.Sort(keys.AsSpan());

        Assert.Equal(
            [float.NegativeInfinity, -2.5f, -0f, 0f, 1.5f, float.PositiveInfinity],
            keys);

        // -0.0 sorts strictly before +0.0, which the bit order distinguishes and the BCL comparer
        // does not. Assert.Equal treats them as equal, so check the sign bit explicitly.
        Assert.True(float.IsNegative(keys[2]));
        Assert.False(float.IsNegative(keys[3]));
    }

    [Fact]
    public void Sort_ShouldOrderNaNsByTheirSignBit_WhenKeysAreSingle()
    {
        // float.NaN is 0xFFC00000 in .NET — the sign bit is already set — so the positive-signed
        // NaN is the one that has to be built by hand.
        float positiveNaN = BitConverter.Int32BitsToSingle(0x7FC0_0000);
        float[] keys = [1f, positiveNaN, float.NaN, -1f];

        RadixSort.Sort(keys.AsSpan());

        // Documented divergence from Array.Sort, which moves every NaN to the front.
        Assert.True(float.IsNaN(keys[0]) && float.IsNegative(keys[0]));
        Assert.Equal(-1f, keys[1]);
        Assert.Equal(1f, keys[2]);
        Assert.True(float.IsNaN(keys[3]) && !float.IsNegative(keys[3]));
    }

    [Fact]
    public void Sort_ShouldOrderAscending_WhenSingleKeysUseACallerSuppliedScratchBuffer()
    {
        float[] keys = [3f, 1f, 2f];

        RadixSort.SortWithScratch(keys.AsSpan(), new float[3].AsSpan());

        Assert.Equal([1f, 2f, 3f], keys);
    }

    [Fact]
    public void Sort_ShouldMoveThePayload_WhenKeysAreSingle()
    {
        float[] keys = [2.5f, -2.5f];
        string[] values = ["hi", "lo"];

        RadixSort.Sort(keys.AsSpan(), values.AsSpan());

        Assert.Equal([-2.5f, 2.5f], keys);
        Assert.Equal(["lo", "hi"], values);
    }

    [Fact]
    public void Sort_ShouldMoveThePayload_WhenSingleKeysUseCallerSuppliedScratchBuffers()
    {
        float[] keys = [2.5f, -2.5f];
        int[] values = [2, -2];

        RadixSort.SortWithScratch(keys.AsSpan(), values.AsSpan(), new float[2].AsSpan(), new int[2].AsSpan());

        Assert.Equal([-2.5f, 2.5f], keys);
        Assert.Equal([-2, 2], values);
    }

    [Fact]
    public void ArgSort_ShouldRankWithoutReorderingTheKeys_WhenKeysAreSingle()
    {
        float[] keys = [0.5f, -0.5f, 1.5f];
        int[] indices = new int[3];

        RadixSort.ArgSort(keys, indices);

        Assert.Equal([1, 0, 2], indices);
        Assert.Equal([0.5f, -0.5f, 1.5f], keys);
    }

    [Fact]
    public void Sort_ShouldOrderAscending_WhenKeysAreDouble()
    {
        double[] keys = [1.5, -2.5, 0d, double.PositiveInfinity, double.NegativeInfinity, -0d];

        RadixSort.Sort(keys.AsSpan());

        Assert.Equal(
            [double.NegativeInfinity, -2.5, -0d, 0d, 1.5, double.PositiveInfinity],
            keys);
        Assert.True(double.IsNegative(keys[2]));
        Assert.False(double.IsNegative(keys[3]));
    }

    [Fact]
    public void Sort_ShouldOrderNaNsByTheirSignBit_WhenKeysAreDouble()
    {
        // double.NaN is 0xFFF8000000000000 in .NET — the sign bit is already set.
        double positiveNaN = BitConverter.Int64BitsToDouble(0x7FF8_0000_0000_0000);
        double[] keys = [1d, positiveNaN, double.NaN, -1d];

        RadixSort.Sort(keys.AsSpan());

        Assert.True(double.IsNaN(keys[0]) && double.IsNegative(keys[0]));
        Assert.Equal(-1d, keys[1]);
        Assert.Equal(1d, keys[2]);
        Assert.True(double.IsNaN(keys[3]) && !double.IsNegative(keys[3]));
    }

    [Fact]
    public void Sort_ShouldOrderAscending_WhenDoubleKeysUseACallerSuppliedScratchBuffer()
    {
        double[] keys = [3d, 1d, 2d];

        RadixSort.SortWithScratch(keys.AsSpan(), new double[3].AsSpan());

        Assert.Equal([1d, 2d, 3d], keys);
    }

    [Fact]
    public void Sort_ShouldMoveThePayload_WhenKeysAreDouble()
    {
        double[] keys = [2.5, -2.5];
        string[] values = ["hi", "lo"];

        RadixSort.Sort(keys.AsSpan(), values.AsSpan());

        Assert.Equal([-2.5, 2.5], keys);
        Assert.Equal(["lo", "hi"], values);
    }

    [Fact]
    public void Sort_ShouldMoveThePayload_WhenDoubleKeysUseCallerSuppliedScratchBuffers()
    {
        double[] keys = [2.5, -2.5];
        int[] values = [2, -2];

        RadixSort.SortWithScratch(keys.AsSpan(), values.AsSpan(), new double[2].AsSpan(), new int[2].AsSpan());

        Assert.Equal([-2.5, 2.5], keys);
        Assert.Equal([-2, 2], values);
    }

    [Fact]
    public void ArgSort_ShouldRankWithoutReorderingTheKeys_WhenKeysAreDouble()
    {
        double[] keys = [0.5, -0.5, 1.5];
        int[] indices = new int[3];

        RadixSort.ArgSort(keys, indices);

        Assert.Equal([1, 0, 2], indices);
    }

    // ---- stability ----------------------------------------------------------------------------

    [Fact]
    public void Sort_ShouldKeepEqualKeysInInputOrder_WhenAPayloadIsCarried()
    {
        int[] keys = [1, 1, 1, 0, 0];
        string[] values = ["a", "b", "c", "d", "e"];

        RadixSort.Sort(keys.AsSpan(), values.AsSpan());

        Assert.Equal([0, 0, 1, 1, 1], keys);
        Assert.Equal(["d", "e", "a", "b", "c"], values);
    }

    [Fact]
    public void ArgSort_ShouldBreakTiesByInputPosition_WhenKeysRepeat()
    {
        int[] keys = [5, 5, 5];
        int[] indices = new int[3];

        RadixSort.ArgSort(keys, indices);

        Assert.Equal([0, 1, 2], indices);
    }

    // ---- empty / single, every shape ----------------------------------------------------------

    [Fact]
    public void Sort_ShouldDoNothing_WhenEveryShapeIsGivenAnEmptySpan()
    {
        RadixSort.Sort(Span<int>.Empty);
        RadixSort.SortWithScratch(Span<int>.Empty, Span<int>.Empty);
        RadixSort.Sort<int>(Span<int>.Empty, Span<int>.Empty);
        RadixSort.SortWithScratch(Span<int>.Empty, Span<int>.Empty, Span<int>.Empty, Span<int>.Empty);
        RadixSort.ArgSort(ReadOnlySpan<int>.Empty, Span<int>.Empty);

        RadixSort.Sort(Span<uint>.Empty);
        RadixSort.SortWithScratch(Span<uint>.Empty, Span<uint>.Empty);
        RadixSort.Sort<int>(Span<uint>.Empty, Span<int>.Empty);
        RadixSort.SortWithScratch(Span<uint>.Empty, Span<int>.Empty, Span<uint>.Empty, Span<int>.Empty);
        RadixSort.ArgSort(ReadOnlySpan<uint>.Empty, Span<int>.Empty);

        RadixSort.Sort(Span<long>.Empty);
        RadixSort.SortWithScratch(Span<long>.Empty, Span<long>.Empty);
        RadixSort.Sort<int>(Span<long>.Empty, Span<int>.Empty);
        RadixSort.SortWithScratch(Span<long>.Empty, Span<int>.Empty, Span<long>.Empty, Span<int>.Empty);
        RadixSort.ArgSort(ReadOnlySpan<long>.Empty, Span<int>.Empty);

        RadixSort.Sort(Span<ulong>.Empty);
        RadixSort.SortWithScratch(Span<ulong>.Empty, Span<ulong>.Empty);
        RadixSort.Sort<int>(Span<ulong>.Empty, Span<int>.Empty);
        RadixSort.SortWithScratch(Span<ulong>.Empty, Span<int>.Empty, Span<ulong>.Empty, Span<int>.Empty);
        RadixSort.ArgSort(ReadOnlySpan<ulong>.Empty, Span<int>.Empty);

        RadixSort.Sort(Span<float>.Empty);
        RadixSort.SortWithScratch(Span<float>.Empty, Span<float>.Empty);
        RadixSort.Sort<int>(Span<float>.Empty, Span<int>.Empty);
        RadixSort.SortWithScratch(Span<float>.Empty, Span<int>.Empty, Span<float>.Empty, Span<int>.Empty);
        RadixSort.ArgSort(ReadOnlySpan<float>.Empty, Span<int>.Empty);

        RadixSort.Sort(Span<double>.Empty);
        RadixSort.SortWithScratch(Span<double>.Empty, Span<double>.Empty);
        RadixSort.Sort<int>(Span<double>.Empty, Span<int>.Empty);
        RadixSort.SortWithScratch(Span<double>.Empty, Span<int>.Empty, Span<double>.Empty, Span<int>.Empty);
        RadixSort.ArgSort(ReadOnlySpan<double>.Empty, Span<int>.Empty);
    }
}
