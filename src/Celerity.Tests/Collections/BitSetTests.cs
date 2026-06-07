using Celerity.Collections;

namespace Celerity.Tests.Collections;

public class BitSetTests
{
    [Fact]
    public void NewBitSet_HasGivenLength_AndAllBitsClear()
    {
        var bits = new BitSet(100);
        Assert.Equal(100, bits.Length);
        Assert.Equal(0, bits.Count);
        Assert.True(bits.None());
        for (int i = 0; i < 100; i++)
            Assert.False(bits[i]);
    }

    [Fact]
    public void Constructor_NegativeLength_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BitSet(-1));
    }

    [Fact]
    public void Constructor_ZeroLength_IsEmptyAndValid()
    {
        var bits = new BitSet(0);
        Assert.Equal(0, bits.Length);
        Assert.Equal(0, bits.Count);
        Assert.False(bits.Any());
        Assert.True(bits.All());  // vacuously true
        Assert.True(bits.None());
    }

    [Fact]
    public void Constructor_WithDefaultTrue_SetsEveryBit()
    {
        var bits = new BitSet(70, defaultValue: true);
        Assert.Equal(70, bits.Count);
        Assert.True(bits.All());
        for (int i = 0; i < 70; i++)
            Assert.True(bits[i]);
    }

    [Fact]
    public void Constructor_WithDefaultFalse_LeavesEveryBitClear()
    {
        var bits = new BitSet(70, defaultValue: false);
        Assert.Equal(0, bits.Count);
    }

    [Fact]
    public void Constructor_FromBoolArray_CopiesBits()
    {
        bool[] values = { true, false, true, true, false };
        var bits = new BitSet(values);
        Assert.Equal(5, bits.Length);
        Assert.Equal(3, bits.Count);
        Assert.True(bits[0]);
        Assert.False(bits[1]);
        Assert.True(bits[2]);
        Assert.True(bits[3]);
        Assert.False(bits[4]);
    }

    [Fact]
    public void Constructor_FromNullBoolArray_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BitSet(null!));
    }

    [Fact]
    public void Set_ThenGet_RoundTrips()
    {
        var bits = new BitSet(128);
        bits.Set(0, true);
        bits.Set(63, true);   // last bit of word 0
        bits.Set(64, true);   // first bit of word 1
        bits.Set(127, true);  // last bit

        Assert.True(bits.Get(0));
        Assert.True(bits.Get(63));
        Assert.True(bits.Get(64));
        Assert.True(bits.Get(127));
        Assert.False(bits.Get(1));
        Assert.False(bits.Get(62));
        Assert.Equal(4, bits.Count);
    }

    [Fact]
    public void Set_False_ClearsBit()
    {
        var bits = new BitSet(10);
        bits.Set(3, true);
        Assert.True(bits[3]);
        bits.Set(3, false);
        Assert.False(bits[3]);
        Assert.Equal(0, bits.Count);
    }

    [Fact]
    public void Indexer_SetAndGet_RoundTrips()
    {
        var bits = new BitSet(10);
        bits[5] = true;
        Assert.True(bits[5]);
        bits[5] = false;
        Assert.False(bits[5]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(64)]
    [InlineData(100)]
    public void Get_OutOfRange_Throws(int index)
    {
        var bits = new BitSet(64);
        Assert.Throws<ArgumentOutOfRangeException>(() => bits.Get(index));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(64)]
    public void Set_OutOfRange_Throws(int index)
    {
        var bits = new BitSet(64);
        Assert.Throws<ArgumentOutOfRangeException>(() => bits.Set(index, true));
    }

    [Fact]
    public void Flip_TogglesBit_AndReturnsNewValue()
    {
        var bits = new BitSet(10);
        Assert.True(bits.Flip(4));   // false -> true
        Assert.True(bits[4]);
        Assert.False(bits.Flip(4));  // true -> false
        Assert.False(bits[4]);
    }

    [Fact]
    public void Flip_OutOfRange_Throws()
    {
        var bits = new BitSet(10);
        Assert.Throws<ArgumentOutOfRangeException>(() => bits.Flip(10));
    }

    [Fact]
    public void SetAll_True_SetsEveryBit_AndClearsTailBitsBeyondLength()
    {
        // 130 bits => 3 words (192 slots), 62 unused tail bits in the last word that
        // must stay clear so Count and All observe exactly 130 set bits.
        var bits = new BitSet(130);
        bits.SetAll(true);
        Assert.Equal(130, bits.Count);
        Assert.True(bits.All());
        Assert.True(bits.Any());
    }

    [Fact]
    public void SetAll_False_ClearsEveryBit()
    {
        var bits = new BitSet(130, defaultValue: true);
        bits.SetAll(false);
        Assert.Equal(0, bits.Count);
        Assert.False(bits.Any());
    }

    [Fact]
    public void Clear_ClearsEveryBit()
    {
        var bits = new BitSet(50, defaultValue: true);
        bits.Clear();
        Assert.Equal(0, bits.Count);
        Assert.True(bits.None());
    }

    [Fact]
    public void Any_All_None_ReflectState()
    {
        var bits = new BitSet(10);
        Assert.False(bits.Any());
        Assert.False(bits.All());
        Assert.True(bits.None());

        bits[3] = true;
        Assert.True(bits.Any());
        Assert.False(bits.All());
        Assert.False(bits.None());

        bits.SetAll(true);
        Assert.True(bits.Any());
        Assert.True(bits.All());
        Assert.False(bits.None());
    }

    [Fact]
    public void Count_TracksSetBits()
    {
        var bits = new BitSet(200);
        for (int i = 0; i < 200; i += 3)
            bits[i] = true;

        int expected = 0;
        for (int i = 0; i < 200; i += 3)
            expected++;
        Assert.Equal(expected, bits.Count);
    }

    [Fact]
    public void All_WithExactlyWordBoundaryLength_IsCorrect()
    {
        // 64 bits is exactly one full word with no tail; All() must take the full-word
        // path only and not consult a (non-existent) partial word.
        var bits = new BitSet(64, defaultValue: true);
        Assert.True(bits.All());
        bits[0] = false;
        Assert.False(bits.All());
    }

    [Fact]
    public void All_ReturnsFalse_WhenOnlyTailBitMissing()
    {
        var bits = new BitSet(65, defaultValue: true);
        Assert.True(bits.All());
        bits[64] = false; // the lone bit in the second word's tail
        Assert.False(bits.All());
    }
}
