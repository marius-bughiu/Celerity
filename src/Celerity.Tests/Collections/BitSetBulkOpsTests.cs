using Celerity.Collections;

namespace Celerity.Tests.Collections;

public class BitSetBulkOpsTests
{
    private static BitSet FromIndices(int length, params int[] setBits)
    {
        var bits = new BitSet(length);
        foreach (int i in setBits)
            bits[i] = true;
        return bits;
    }

    [Fact]
    public void And_KeepsOnlyCommonBits()
    {
        var a = FromIndices(10, 1, 2, 3, 4);
        var b = FromIndices(10, 3, 4, 5, 6);
        a.And(b);

        Assert.False(a[1]);
        Assert.False(a[2]);
        Assert.True(a[3]);
        Assert.True(a[4]);
        Assert.False(a[5]);
        Assert.Equal(2, a.Count);
    }

    [Fact]
    public void Or_UnionsBits()
    {
        var a = FromIndices(10, 1, 2);
        var b = FromIndices(10, 2, 3);
        a.Or(b);

        Assert.True(a[1]);
        Assert.True(a[2]);
        Assert.True(a[3]);
        Assert.Equal(3, a.Count);
    }

    [Fact]
    public void Xor_KeepsBitsInExactlyOneSet()
    {
        var a = FromIndices(10, 1, 2, 3);
        var b = FromIndices(10, 3, 4);
        a.Xor(b);

        Assert.True(a[1]);
        Assert.True(a[2]);
        Assert.False(a[3]); // in both -> cleared
        Assert.True(a[4]);
        Assert.Equal(3, a.Count);
    }

    [Fact]
    public void Not_InvertsEveryBit_AndCount()
    {
        var bits = FromIndices(10, 1, 3, 5);
        bits.Not();

        Assert.False(bits[1]);
        Assert.False(bits[3]);
        Assert.False(bits[5]);
        Assert.True(bits[0]);
        Assert.True(bits[9]);
        Assert.Equal(7, bits.Count); // 10 - 3
    }

    [Fact]
    public void Not_DoesNotLeakTailBitsBeyondLength()
    {
        // 10 bits live in one 64-bit word; Not() must clear the 54 tail bits so Count
        // and Any never observe out-of-range set bits.
        var bits = new BitSet(10);
        bits.Not();
        Assert.Equal(10, bits.Count);
        Assert.True(bits.All());
    }

    [Fact]
    public void Not_Twice_RestoresOriginal()
    {
        var bits = FromIndices(200, 0, 7, 63, 64, 199);
        bits.Not().Not();

        Assert.True(bits[0]);
        Assert.True(bits[7]);
        Assert.True(bits[63]);
        Assert.True(bits[64]);
        Assert.True(bits[199]);
        Assert.Equal(5, bits.Count);
    }

    [Fact]
    public void BulkOps_ReturnSameInstance_ForChaining()
    {
        var a = FromIndices(10, 1, 2, 3);
        var b = FromIndices(10, 2);
        Assert.Same(a, a.And(b));
        Assert.Same(a, a.Or(b));
        Assert.Same(a, a.Xor(b));
        Assert.Same(a, a.Not());
    }

    [Theory]
    [InlineData(10)]      // sub-word: scalar tail only
    [InlineData(1000)]    // multi-word: exercises the SIMD path plus a scalar remainder
    [InlineData(4096)]    // word-count divisible by the vector width
    public void BulkOps_AgreeWithScalarReference_AcrossSizes(int length)
    {
        var rand = new Random(length);
        var aRef = new bool[length];
        var bRef = new bool[length];
        var a = new BitSet(length);
        var b = new BitSet(length);
        for (int i = 0; i < length; i++)
        {
            aRef[i] = rand.Next(2) == 0;
            bRef[i] = rand.Next(2) == 0;
            a[i] = aRef[i];
            b[i] = bRef[i];
        }

        // OR
        var or = new BitSet((bool[])aRef.Clone()).Or(b);
        for (int i = 0; i < length; i++)
            Assert.Equal(aRef[i] || bRef[i], or[i]);

        // AND
        var and = new BitSet((bool[])aRef.Clone()).And(b);
        for (int i = 0; i < length; i++)
            Assert.Equal(aRef[i] && bRef[i], and[i]);

        // XOR
        var xor = new BitSet((bool[])aRef.Clone()).Xor(b);
        for (int i = 0; i < length; i++)
            Assert.Equal(aRef[i] ^ bRef[i], xor[i]);

        // NOT
        var not = new BitSet((bool[])aRef.Clone()).Not();
        for (int i = 0; i < length; i++)
            Assert.Equal(!aRef[i], not[i]);
    }

    [Fact]
    public void And_LengthMismatch_Throws()
    {
        var a = new BitSet(10);
        var b = new BitSet(11);
        Assert.Throws<ArgumentException>(() => a.And(b));
    }

    [Fact]
    public void Or_LengthMismatch_Throws()
    {
        Assert.Throws<ArgumentException>(() => new BitSet(10).Or(new BitSet(11)));
    }

    [Fact]
    public void Xor_LengthMismatch_Throws()
    {
        Assert.Throws<ArgumentException>(() => new BitSet(10).Xor(new BitSet(11)));
    }

    [Fact]
    public void BulkOps_NullOther_Throws()
    {
        var a = new BitSet(10);
        Assert.Throws<ArgumentNullException>(() => a.And(null!));
        Assert.Throws<ArgumentNullException>(() => a.Or(null!));
        Assert.Throws<ArgumentNullException>(() => a.Xor(null!));
    }
}
