using Celerity.Collections;

namespace Celerity.Tests.Collections;

public class BitPackedArrayTests
{
    // ---- Construction ----

    [Fact]
    public void Constructor_ShouldCreateAnAllZeroArray()
    {
        var packed = new BitPackedArray(100, 7);

        Assert.Equal(100, packed.Count);
        Assert.Equal(7, packed.BitsPerValue);
        Assert.Equal(127UL, packed.MaxValue);
        for (int i = 0; i < packed.Count; i++)
            Assert.Equal(0UL, packed[i]);
    }

    [Fact]
    public void Constructor_ShouldAcceptAZeroLength()
    {
        var packed = new BitPackedArray(0, 13);

        Assert.Equal(0, packed.Count);
        Assert.Empty(packed);
    }

    [Theory]
    [InlineData(1, 1UL)]
    [InlineData(20, (1UL << 20) - 1)]
    [InlineData(63, (1UL << 63) - 1)]
    [InlineData(64, ulong.MaxValue)]
    public void MaxValue_ShouldBeAllOnesAtTheWidth(int bits, ulong expected)
    {
        Assert.Equal(expected, new BitPackedArray(1, bits).MaxValue);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLengthIsNegative()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new BitPackedArray(-1, 8));
        Assert.Equal("length", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_ShouldThrow_WhenBitsPerValueIsOutOfRange(int bits)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new BitPackedArray(4, bits));
        Assert.Equal("bitsPerValue", ex.ParamName);

        var fromValues = Assert.Throws<ArgumentOutOfRangeException>(() => new BitPackedArray(new ulong[] { 1 }, bits));
        Assert.Equal("bitsPerValue", fromValues.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenTheWordsWouldNotFitInOneArray()
    {
        // At 64 bits a value every element is a whole word, so int.MaxValue elements plus the padding word pass
        // Array.MaxLength. The check runs before the allocation, so this costs no memory.
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new BitPackedArray(int.MaxValue, 64));
        Assert.Equal("length", ex.ParamName);
    }

    [Fact]
    public void FromValues_ShouldThrow_WhenValuesIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new BitPackedArray(null!, 8));
        Assert.Equal("values", ex.ParamName);
    }

    [Fact]
    public void FromValues_ShouldPackAnArray()
    {
        ulong[] values = { 0, 1, 2, 3, 4, 5, 6, 7 };
        var packed = new BitPackedArray(values, 3);

        Assert.Equal(values, packed.ToArray());
    }

    [Fact]
    public void FromValues_ShouldPackAList()
    {
        var values = new List<ulong> { 9, 0, 1023, 512 };
        var packed = new BitPackedArray(values, 10);

        Assert.Equal(values, packed.ToArray());
    }

    [Fact]
    public void FromValues_ShouldPackAnyOtherCountedCollection()
    {
        // A SortedSet is an ICollection<ulong> that is neither an array nor a List, so it takes the copy path.
        var values = new SortedSet<ulong> { 40, 10, 30, 20 };
        var packed = new BitPackedArray(values, 6);

        Assert.Equal(new ulong[] { 10, 20, 30, 40 }, packed.ToArray());
    }

    [Fact]
    public void FromValues_ShouldPackAnUncountedSequence()
    {
        var packed = new BitPackedArray(Sequence(), 5);

        Assert.Equal(new ulong[] { 31, 0, 17 }, packed.ToArray());

        static IEnumerable<ulong> Sequence()
        {
            yield return 31;
            yield return 0;
            yield return 17;
        }
    }

    [Fact]
    public void FromValues_ShouldPackAnEmptySource()
    {
        var packed = new BitPackedArray(Array.Empty<ulong>(), 9);

        Assert.Equal(0, packed.Count);
        Assert.Equal(8, packed.MemoryUsageInBytes);
    }

    [Theory]
    [InlineData(3, 8UL, 2)]
    [InlineData(20, 1UL << 20, 0)]
    [InlineData(63, 1UL << 63, 4)]
    public void FromValues_ShouldThrowNamingTheFirstTooWideValue(int bits, ulong tooWide, int position)
    {
        var values = new ulong[5];
        values[position] = tooWide;
        if (position + 1 < values.Length)
            values[position + 1] = tooWide;

        var ex = Assert.Throws<ArgumentException>(() => new BitPackedArray(values, bits));
        Assert.Equal("values", ex.ParamName);
        Assert.Contains($"element {position} ", ex.Message);
    }

    [Fact]
    public void FromValues_ShouldAcceptEveryValueAt64Bits()
    {
        ulong[] values = { ulong.MaxValue, 0, 1UL << 63, 12345 };
        var packed = new BitPackedArray(values, 64);

        Assert.Equal(values, packed.ToArray());
    }

    // ---- Reads and writes ----

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(20)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(63)]
    [InlineData(64)]
    public void Set_ShouldStoreEachElementWithoutDisturbingItsNeighbours(int bits)
    {
        // 200 elements cover every straddle offset the width has. All-ones next to zero is where a write that
        // clears or sets too wide a mask shows.
        var packed = new BitPackedArray(200, bits);
        ulong max = packed.MaxValue;
        for (int i = 0; i < packed.Count; i += 2)
            packed[i] = max;

        for (int i = 0; i < packed.Count; i++)
            Assert.Equal(i % 2 == 0 ? max : 0UL, packed[i]);

        for (int i = 0; i < packed.Count; i += 2)
            packed.Set(i, 0);
        for (int i = 1; i < packed.Count; i += 2)
            packed.Set(i, max);

        for (int i = 0; i < packed.Count; i++)
            Assert.Equal(i % 2 == 1 ? max : 0UL, packed.Get(i));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Get_ShouldThrow_WhenIndexIsOutOfRange(int index)
    {
        var packed = new BitPackedArray(10, 4);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => packed[index]);
        Assert.Equal("index", ex.ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => packed.Get(index));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public void Set_ShouldThrow_WhenIndexIsOutOfRange(int index)
    {
        var packed = new BitPackedArray(10, 4);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => packed[index] = 1);
        Assert.Equal("index", ex.ParamName);
    }

    [Fact]
    public void Set_ShouldRejectAValueWiderThanTheWidth_AndLeaveTheElementUnchanged()
    {
        var packed = new BitPackedArray(4, 4);
        packed[1] = 9;

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => packed[1] = 16);
        Assert.Equal("value", ex.ParamName);
        Assert.Equal(9UL, packed[1]);
        Assert.Equal(new ulong[] { 0, 9, 0, 0 }, packed.ToArray());
    }

    // ---- Bulk reads and writes ----

    [Fact]
    public void GetRange_ShouldCopyAWindow()
    {
        var packed = new BitPackedArray(Enumerable.Range(0, 50).Select(i => (ulong)(i * 3)), 9);
        var buffer = new ulong[10];

        packed.GetRange(17, buffer);

        Assert.Equal(Enumerable.Range(17, 10).Select(i => (ulong)(i * 3)), buffer);
    }

    [Fact]
    public void GetRange_ShouldAcceptAnEmptyWindowAtTheEnd()
    {
        var packed = new BitPackedArray(5, 3);

        packed.GetRange(5, Span<ulong>.Empty);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void GetRange_ShouldThrow_WhenIndexIsOutsideTheArray(int index)
    {
        var packed = new BitPackedArray(5, 3);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => packed.GetRange(index, Span<ulong>.Empty));
        Assert.Equal("index", ex.ParamName);
    }

    [Fact]
    public void GetRange_ShouldThrow_WhenTheBufferRunsPastTheEnd()
    {
        var packed = new BitPackedArray(5, 3);

        var ex = Assert.Throws<ArgumentException>(() => packed.GetRange(3, new ulong[3]));
        Assert.Equal("destination", ex.ParamName);
    }

    [Fact]
    public void SetRange_ShouldWriteAWindowAndLeaveTheRestAlone()
    {
        var packed = new BitPackedArray(8, 11);
        packed.Fill(2047);

        packed.SetRange(2, new ulong[] { 1, 2, 3 });

        Assert.Equal(new ulong[] { 2047, 2047, 1, 2, 3, 2047, 2047, 2047 }, packed.ToArray());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(9)]
    public void SetRange_ShouldThrow_WhenIndexIsOutsideTheArray(int index)
    {
        var packed = new BitPackedArray(8, 11);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => packed.SetRange(index, ReadOnlySpan<ulong>.Empty));
        Assert.Equal("index", ex.ParamName);
    }

    [Fact]
    public void SetRange_ShouldThrow_WhenTheValuesRunPastTheEnd()
    {
        var packed = new BitPackedArray(8, 11);

        var ex = Assert.Throws<ArgumentException>(() => packed.SetRange(6, new ulong[3]));
        Assert.Equal("values", ex.ParamName);
    }

    [Fact]
    public void SetRange_ShouldWriteNothing_WhenAnyValueIsTooWide()
    {
        var packed = new BitPackedArray(6, 4);

        var ex = Assert.Throws<ArgumentException>(() => packed.SetRange(1, new ulong[] { 1, 2, 16, 3 }));
        Assert.Equal("values", ex.ParamName);
        Assert.Contains("element 3 ", ex.Message);
        Assert.Equal(new ulong[6], packed.ToArray());
    }

    // ---- Fill and Clear ----

    [Theory]
    [InlineData(7)]
    [InlineData(64)]
    public void Fill_ShouldSetEveryElement(int bits)
    {
        var packed = new BitPackedArray(130, bits);

        packed.Fill(packed.MaxValue);
        Assert.All(packed, value => Assert.Equal(packed.MaxValue, value));

        packed.Fill(0);
        Assert.All(packed, value => Assert.Equal(0UL, value));
    }

    [Fact]
    public void Fill_ShouldRejectAValueWiderThanTheWidth()
    {
        var packed = new BitPackedArray(3, 2);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => packed.Fill(4));
        Assert.Equal("value", ex.ParamName);
    }

    [Fact]
    public void Clear_ShouldZeroEveryElement()
    {
        var packed = new BitPackedArray(new ulong[] { 5, 6, 7 }, 3);

        packed.Clear();

        Assert.Equal(new ulong[3], packed.ToArray());
    }

    // ---- CopyTo ----

    [Fact]
    public void CopyTo_ShouldCopyEveryElementAtTheOffset()
    {
        var packed = new BitPackedArray(new ulong[] { 1, 2, 3 }, 2);
        var array = new ulong[5];

        packed.CopyTo(array, 1);

        Assert.Equal(new ulong[] { 0, 1, 2, 3, 0 }, array);
    }

    // ---- Footprint and width selection ----

    [Theory]
    [InlineData(0, 20, 8)]
    [InlineData(1, 20, 16)]
    [InlineData(16, 20, 48)]          // 320 bits: exactly five words, plus the padding word
    [InlineData(1_000_000, 20, 2_500_008)]
    [InlineData(1_000_000, 64, 8_000_008)]
    public void MemoryUsageInBytes_ShouldBeTheRoundedUpWordsPlusThePaddingWord(int length, int bits, long expected)
    {
        Assert.Equal(expected, new BitPackedArray(length, bits).MemoryUsageInBytes);
    }

    [Fact]
    public void MemoryUsageInBytes_ShouldClearTheFootprintCriterionAgainstAnIntArray()
    {
        // The pre-registered bar (#547): at a million 20-bit values, at least 1.55x smaller than the int[].
        var packed = new BitPackedArray(1_000_000, 20);

        Assert.True(1_000_000L * sizeof(int) / (double)packed.MemoryUsageInBytes >= 1.55);
    }

    [Theory]
    [InlineData(0UL, 1)]
    [InlineData(1UL, 1)]
    [InlineData(2UL, 2)]
    [InlineData(255UL, 8)]
    [InlineData(256UL, 9)]
    [InlineData(1_000_000UL, 20)]
    [InlineData((1UL << 63) - 1, 63)]
    [InlineData(ulong.MaxValue, 64)]
    public void BitsRequired_ShouldReturnTheNarrowestWidthThatHoldsTheValue(ulong maxValue, int expected)
    {
        int bits = BitPackedArray.BitsRequired(maxValue);

        Assert.Equal(expected, bits);
        Assert.True(new BitPackedArray(1, bits).MaxValue >= maxValue);
    }
}
