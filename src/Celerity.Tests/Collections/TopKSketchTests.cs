using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

public class TopKSketchTests
{
    [Fact]
    public void EmptySketch_HasZeroCounts()
    {
        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(16);

        Assert.Equal(0, sketch.Count);
        Assert.Equal(0, sketch.TotalCount);
        Assert.Equal(16, sketch.Capacity);
        Assert.Empty(sketch.GetTopK());
        Assert.False(sketch.TryGetCount(42, out long count, out long error));
        Assert.Equal(0, count);
        Assert.Equal(0, error);
    }

    [Fact]
    public void Add_SingleElement_TracksExactCount()
    {
        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(16);
        for (int i = 0; i < 5; i++)
            sketch.Add(42);

        Assert.True(sketch.TryGetCount(42, out long count, out long error));
        Assert.Equal(5, count);
        Assert.Equal(0, error);       // never shared a monitor -> exact
        Assert.Equal(1, sketch.Count);
        Assert.Equal(5, sketch.TotalCount);
    }

    [Fact]
    public void Add_WithinCapacity_CountsAreExact()
    {
        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(16);
        sketch.Add(1);
        sketch.Add(1);
        sketch.Add(2);
        sketch.Add(3, 4);

        Assert.Equal(3, sketch.Count);
        Assert.Equal(7, sketch.TotalCount);   // 1+1+1+4

        Assert.True(sketch.TryGetCount(1, out long c1, out long e1));
        Assert.Equal(2, c1);
        Assert.Equal(0, e1);
        Assert.True(sketch.TryGetCount(3, out long c3, out _));
        Assert.Equal(4, c3);
    }

    [Fact]
    public void GetTopK_ReturnsElementsInDescendingCountOrder()
    {
        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(16);
        sketch.Add(1, 10);
        sketch.Add(2, 30);
        sketch.Add(3, 20);

        TopKEntry<int>[] top = sketch.GetTopK();
        Assert.Equal(3, top.Length);
        Assert.Equal(2, top[0].Element);
        Assert.Equal(30, top[0].Count);
        Assert.Equal(3, top[1].Element);
        Assert.Equal(1, top[2].Element);
    }

    [Fact]
    public void GetTopK_WithLimit_TruncatesToRequestedCount()
    {
        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(16);
        sketch.Add(1, 10);
        sketch.Add(2, 30);
        sketch.Add(3, 20);

        TopKEntry<int>[] top2 = sketch.GetTopK(2);
        Assert.Equal(2, top2.Length);
        Assert.Equal(2, top2[0].Element);
        Assert.Equal(3, top2[1].Element);

        Assert.Empty(sketch.GetTopK(0));
        Assert.Equal(3, sketch.GetTopK(100).Length); // capped at Count
    }

    [Fact]
    public void GetTopK_NegativeCount_Throws()
    {
        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(16);
        Assert.Throws<ArgumentOutOfRangeException>(() => sketch.GetTopK(-1));
    }

    [Fact]
    public void Add_BeyondCapacity_EvictsSmallestAndInheritsErrorFloor()
    {
        // Capacity 2. Fill with two heavier elements, then a newcomer evicts the smaller.
        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(2);
        sketch.Add(1, 5);
        sketch.Add(2, 3);   // 2 is the minimum monitor (count 3)
        sketch.Add(9);      // evicts element 2 (min count 3); 9 inherits error 3, count 3+1=4

        Assert.Equal(2, sketch.Count);
        Assert.Equal(9, sketch.TotalCount);

        Assert.False(sketch.TryGetCount(2, out _, out _)); // evicted
        Assert.True(sketch.TryGetCount(9, out long c9, out long e9));
        Assert.Equal(4, c9);
        Assert.Equal(3, e9);   // inherited the evicted minimum as its error
        Assert.True(sketch.TryGetCount(1, out long c1, out _));
        Assert.Equal(5, c1);
    }

    [Fact]
    public void Add_Zero_DefaultIntElement_IsTracked()
    {
        // 0 is default(int) — must be handled by the dogfooded dictionary's out-of-band slot.
        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(16);
        sketch.Add(0);
        sketch.Add(0);
        sketch.Add(7);

        Assert.True(sketch.TryGetCount(0, out long count, out _));
        Assert.Equal(2, count);
        Assert.Equal(2, sketch.Count);
        Assert.Equal(0, sketch.GetTopK(1)[0].Element);
    }

    [Fact]
    public void Add_NullStringElement_IsTracked_WithoutInvokingHasher()
    {
        // The string hashers throw on null; a null element must be routed out-of-band by the
        // dogfooded CelerityDictionary rather than hashed.
        var sketch = new TopKSketch<string, StringMurmur3Hasher>(16);
        sketch.Add(null!);
        sketch.Add(null!);
        sketch.Add("hello");

        Assert.True(sketch.TryGetCount(null!, out long count, out _));
        Assert.Equal(2, count);
        Assert.Equal(2, sketch.Count);
    }

    [Fact]
    public void Add_NonPositiveCount_Throws()
    {
        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(16);
        Assert.Throws<ArgumentOutOfRangeException>(() => sketch.Add(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => sketch.Add(1, -3));
    }

    [Fact]
    public void Clear_ResetsToEmpty_PreservingCapacity()
    {
        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(16);
        for (int i = 0; i < 50; i++)
            sketch.Add(i % 10);

        sketch.Clear();

        Assert.Equal(0, sketch.Count);
        Assert.Equal(0, sketch.TotalCount);
        Assert.Equal(16, sketch.Capacity);
        Assert.Empty(sketch.GetTopK());
        Assert.False(sketch.TryGetCount(3, out _, out _));

        // Still usable after Clear.
        sketch.Add(99);
        Assert.True(sketch.TryGetCount(99, out long count, out _));
        Assert.Equal(1, count);
    }

    [Fact]
    public void Constructor_InvalidCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TopKSketch<int, Int32Murmur3Hasher>(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TopKSketch<int, Int32Murmur3Hasher>(-1));
    }

    [Fact]
    public void EnumerableConstructor_PrepopulatesFromSource()
    {
        int[] source = { 1, 1, 1, 2, 2, 3 };
        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(source, 16);

        Assert.Equal(3, sketch.Count);
        Assert.Equal(6, sketch.TotalCount);
        Assert.True(sketch.TryGetCount(1, out long c1, out _));
        Assert.Equal(3, c1);
        Assert.Equal(1, sketch.GetTopK(1)[0].Element);
    }

    [Fact]
    public void EnumerableConstructor_NullSource_Throws_EvenWithInvalidCapacity()
    {
        // Null-source check must beat capacity validation.
        Assert.Throws<ArgumentNullException>(
            () => new TopKSketch<int, Int32Murmur3Hasher>(null!, -5));
    }

    [Fact]
    public void Capacity_One_TracksOnlyTheRunningMinimumSurvivor()
    {
        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(1);
        sketch.Add(1);
        sketch.Add(2);  // evicts 1: count 1 + 1 = 2, error 1
        sketch.Add(2);  // now monitored: count 3

        Assert.Equal(1, sketch.Count);
        Assert.Equal(3, sketch.TotalCount);
        Assert.True(sketch.TryGetCount(2, out long count, out long error));
        Assert.Equal(3, count);
        Assert.Equal(1, error);
    }
}
