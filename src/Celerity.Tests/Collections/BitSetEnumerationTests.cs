using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

public class BitSetEnumerationTests
{
    [Fact]
    public void GetEnumerator_YieldsEveryBitValueInOrder()
    {
        bool[] values = { true, false, false, true, true };
        var bits = new BitSet(values);

        var observed = new List<bool>();
        foreach (bool b in bits)
            observed.Add(b);

        Assert.Equal(values, observed);
    }

    [Fact]
    public void GetEnumerator_OnEmptySet_YieldsNothing()
    {
        var bits = new BitSet(0);
        foreach (bool _ in bits)
            Assert.Fail("empty set should yield no bits");
    }

    [Fact]
    public void GenericEnumerator_YieldsEveryBitValue()
    {
        var bits = new BitSet(new[] { false, true, false });
        IEnumerable<bool> enumerable = bits;
        Assert.Equal(new[] { false, true, false }, enumerable.ToList());
    }

    [Fact]
    public void NonGenericEnumerator_YieldsEveryBitValue()
    {
        var bits = new BitSet(new[] { true, false });
        IEnumerable enumerable = bits;
        var observed = new List<object?>();
        foreach (object? o in enumerable)
            observed.Add(o);
        Assert.Equal(new object?[] { true, false }, observed);
    }

    [Fact]
    public void EnumerateSetBits_YieldsIndicesOfSetBitsInAscendingOrder()
    {
        var bits = new BitSet(200);
        int[] expected = { 0, 5, 63, 64, 127, 128, 199 };
        foreach (int i in expected)
            bits[i] = true;

        Assert.Equal(expected, bits.EnumerateSetBits().ToList());
    }

    [Fact]
    public void EnumerateSetBits_OnEmptySet_YieldsNothing()
    {
        var bits = new BitSet(128);
        Assert.Empty(bits.EnumerateSetBits().ToList());
    }

    [Fact]
    public void EnumerateSetBits_AllBitsSet_YieldsEveryIndex()
    {
        var bits = new BitSet(70, defaultValue: true);
        Assert.Equal(Enumerable.Range(0, 70), bits.EnumerateSetBits().ToList());
    }

    [Fact]
    public void EnumerateSetBits_SkipsClearWords()
    {
        // Only the very last word is populated; the enumerator must skip the empty
        // leading words rather than scan every bit.
        var bits = new BitSet(300);
        bits[256] = true;
        bits[299] = true;
        Assert.Equal(new[] { 256, 299 }, bits.EnumerateSetBits().ToList());
    }

    [Fact]
    public void GenericSetBitEnumerator_YieldsIndices()
    {
        var bits = new BitSet(10);
        bits[2] = true;
        bits[8] = true;
        IEnumerable<int> enumerable = bits.EnumerateSetBits();
        Assert.Equal(new[] { 2, 8 }, enumerable.ToList());
    }

    [Fact]
    public void NonGenericSetBitEnumerator_YieldsIndices()
    {
        var bits = new BitSet(10);
        bits[1] = true;
        IEnumerable enumerable = bits.EnumerateSetBits();
        var observed = new List<object?>();
        foreach (object? o in enumerable)
            observed.Add(o);
        Assert.Equal(new object?[] { 1 }, observed);
    }

    [Fact]
    public void Enumerator_Reset_RestartsIteration()
    {
        var bits = new BitSet(new[] { true, false, true });
        BitSet.Enumerator e = bits.GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.True(e.Current);
        e.Reset();
        Assert.True(e.MoveNext());
        Assert.True(e.Current); // back at index 0
    }

    [Fact]
    public void SetBitEnumerator_Reset_RestartsIteration()
    {
        var bits = new BitSet(10);
        bits[4] = true;
        BitSet.SetBitEnumerator e = bits.EnumerateSetBits().GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.Equal(4, e.Current);
        Assert.False(e.MoveNext());
        e.Reset();
        Assert.True(e.MoveNext());
        Assert.Equal(4, e.Current);
    }

    [Fact]
    public void Enumerator_ThrowsWhenModifiedDuringIteration()
    {
        var bits = new BitSet(10);
        bits[1] = true;
        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (bool _ in bits)
                bits[2] = true; // structural mutation invalidates the enumerator
        });
    }

    [Fact]
    public void SetBitEnumerator_ThrowsWhenModifiedDuringIteration()
    {
        var bits = new BitSet(10);
        bits[1] = true;
        bits[5] = true;
        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (int _ in bits.EnumerateSetBits())
                bits.Set(7, true);
        });
    }

    [Fact]
    public void SetBitEnumerator_ThrowsOnResetAfterModification()
    {
        var bits = new BitSet(10);
        bits[1] = true;
        BitSet.SetBitEnumerator e = bits.EnumerateSetBits().GetEnumerator();
        e.MoveNext();
        bits.Clear();
        Assert.Throws<InvalidOperationException>(() => e.Reset());
    }
}
