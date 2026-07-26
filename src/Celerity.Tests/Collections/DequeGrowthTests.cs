using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Growth, wrap-around, and capacity-management coverage for <see cref="Deque{T}"/>: the circular buffer
/// must stay correct across doubling growth, across a head that has wrapped past the physical end of the
/// array, and through <see cref="Deque{T}.EnsureCapacity"/> / <see cref="Deque{T}.TrimExcess"/>. These are
/// the paths where an off-by-one in the modular index arithmetic would surface.
/// </summary>
public class DequeGrowthTests
{
    [Fact]
    public void Grows_PreservingOrder_OverManyPushBacks()
    {
        var deque = new Deque<int>();
        for (int i = 0; i < 1000; i++)
            deque.PushBack(i);

        Assert.Equal(1000, deque.Count);
        for (int i = 0; i < 1000; i++)
            Assert.Equal(i, deque[i]);
    }

    [Fact]
    public void Grows_PreservingOrder_OverManyPushFronts()
    {
        var deque = new Deque<int>();
        for (int i = 0; i < 1000; i++)
            deque.PushFront(i);

        // Front-to-back is 999, 998, ..., 0.
        Assert.Equal(1000, deque.Count);
        for (int i = 0; i < 1000; i++)
            Assert.Equal(999 - i, deque[i]);
    }

    [Fact]
    public void GrowsWhileWrapped_KeepsElementsContiguous()
    {
        // Force the head to wrap: fill a small capacity, then alternate front/back pushes so the logical
        // window straddles the physical end of the array before a growth re-linearizes it.
        var deque = new Deque<int>(4);
        deque.PushBack(0);
        deque.PushBack(1);
        deque.PushFront(-1); // head wraps to the end of the 4-slot array
        deque.PushFront(-2);
        // Array is now full at capacity 4 and wrapped; the next push grows.
        deque.PushBack(2);
        deque.PushFront(-3);

        Assert.Equal(new[] { -3, -2, -1, 0, 1, 2 }, deque.ToArray());
    }

    [Fact]
    public void WrapAround_FifoChurn_StaysConsistent()
    {
        // A bounded FIFO churn that repeatedly wraps the head around a small buffer.
        var deque = new Deque<int>(8);
        for (int i = 0; i < 8; i++)
            deque.PushBack(i);

        int next = 8;
        for (int step = 0; step < 10_000; step++)
        {
            Assert.Equal(step, deque.PopFront());
            deque.PushBack(next++);
            Assert.Equal(8, deque.Count);
        }

        // The eight survivors are the last eight pushed, in order.
        for (int i = 0; i < 8; i++)
            Assert.Equal(10_000 + i, deque[i]);
    }

    [Fact]
    public void WrapAround_DoubleEndedChurn_MatchesReference()
    {
        var deque = new Deque<int>(4);
        var reference = new LinkedList<int>();
        var rng = new Random(1234);

        for (int i = 0; i < 5000; i++)
        {
            int op = rng.Next(4);
            switch (op)
            {
                case 0:
                    int f = rng.Next();
                    deque.PushFront(f);
                    reference.AddFirst(f);
                    break;
                case 1:
                    int b = rng.Next();
                    deque.PushBack(b);
                    reference.AddLast(b);
                    break;
                case 2 when reference.Count > 0:
                    Assert.Equal(reference.First!.Value, deque.PopFront());
                    reference.RemoveFirst();
                    break;
                case 3 when reference.Count > 0:
                    Assert.Equal(reference.Last!.Value, deque.PopBack());
                    reference.RemoveLast();
                    break;
            }

            Assert.Equal(reference.Count, deque.Count);
        }

        Assert.Equal(reference.ToArray(), deque.ToArray());
    }

    [Fact]
    public void ToArray_And_CopyTo_HandleWrappedLayout()
    {
        var deque = new Deque<int>(4);
        deque.PushBack(1);
        deque.PushBack(2);
        deque.PushFront(0); // wraps head
        // Logical [0,1,2] with a wrapped physical layout, still not grown.

        Assert.Equal(new[] { 0, 1, 2 }, deque.ToArray());

        var dest = new int[3];
        deque.CopyTo(dest, 0);
        Assert.Equal(new[] { 0, 1, 2 }, dest);
    }

    [Fact]
    public void EnsureCapacity_GrowsAndPreservesElements()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);
        deque.PushBack(2);

        int returned = deque.EnsureCapacity(100);

        Assert.True(returned >= 100);
        Assert.True(deque.Capacity >= 100);
        Assert.Equal(new[] { 1, 2 }, deque.ToArray());
    }

    [Fact]
    public void EnsureCapacity_NoOpWhenAlreadyLargeEnough()
    {
        var deque = new Deque<int>(64);
        int cap = deque.Capacity;
        int returned = deque.EnsureCapacity(10);

        Assert.Equal(cap, returned);
        Assert.Equal(cap, deque.Capacity);
    }

    [Fact]
    public void EnsureCapacity_Negative_Throws()
    {
        var deque = new Deque<int>();
        Assert.Throws<ArgumentOutOfRangeException>(() => deque.EnsureCapacity(-1));
    }

    [Fact]
    public void TrimExcess_ShrinksToCountAndReLinearizes()
    {
        var deque = new Deque<int>(64);
        deque.PushBack(1);
        deque.PushFront(0); // wrapped layout
        deque.PushBack(2);

        deque.TrimExcess();

        Assert.Equal(3, deque.Capacity);
        Assert.Equal(new[] { 0, 1, 2 }, deque.ToArray());
        // Still usable after the trim.
        deque.PushBack(3);
        Assert.Equal(new[] { 0, 1, 2, 3 }, deque.ToArray());
    }

    [Fact]
    public void TrimExcess_OnEmpty_ReleasesArray()
    {
        var deque = new Deque<int>(64);
        deque.TrimExcess();
        Assert.Equal(0, deque.Capacity);
        Assert.Equal(0, deque.Count);

        // Still usable.
        deque.PushBack(5);
        Assert.Equal(5, deque.PeekFront());
    }

    [Fact]
    public void TrimExcess_NoOpWhenFull()
    {
        var deque = new Deque<int>(2);
        deque.PushBack(1);
        deque.PushBack(2);
        // Capacity == Count, nothing to trim.
        deque.TrimExcess();
        Assert.Equal(2, deque.Capacity);
        Assert.Equal(new[] { 1, 2 }, deque.ToArray());
    }

    /// <summary>
    /// The doubling clamp: once the backing array passes half of <see cref="Array.MaxLength"/>, the naive
    /// <c>_items.Length * 2</c> would exceed the largest array the runtime can allocate (and, one doubling
    /// later, wrap negative). Growth must saturate at <see cref="Array.MaxLength"/> instead.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The element type must be <see cref="byte"/>: <see cref="Array.MaxLength"/> (0x7FFFFFC7) is chosen so a
    /// <c>byte[]</c> of that length just fits under the 2 GiB single-object limit, so any wider element type
    /// would fail the clamped allocation on every machine regardless of memory. That also rules this test out
    /// of any generic sweep over element types.
    /// </para>
    /// <para>
    /// The deque is left empty, so the growth copies nothing — the cost is the two allocations, not a 3 GiB
    /// memcpy. Peak footprint is ~3.0 GiB: the 1 GiB source array is still rooted while the 2 GiB replacement
    /// is allocated. An empty deque cannot be used as a shortcut, because <c>Grow</c> takes the
    /// <c>DefaultCapacity</c> path when the array is zero-length and never reaches the clamp.
    /// </para>
    /// </remarks>
    [MemoryIntensiveFact(3100)]
    public void EnsureCapacity_ShouldSaturateAtArrayMaxLength_WhenDoublingWouldOverflowIt()
    {
        // Half of Array.MaxLength, rounded up: doubling this gives Array.MaxLength + 1, which is the smallest
        // input that trips the clamp.
        int justOverHalf = (Array.MaxLength / 2) + 1;

        var deque = new Deque<byte>(justOverHalf);
        Assert.Equal(justOverHalf, deque.Capacity);

        int reported = deque.EnsureCapacity(justOverHalf + 1);

        Assert.Equal(Array.MaxLength, reported);
        Assert.Equal(Array.MaxLength, deque.Capacity);

        // Saturated, but still a working deque.
        Assert.Equal(0, deque.Count);
        deque.PushBack(7);
        Assert.Equal(7, deque.PeekFront());
        Assert.Equal(1, deque.Count);
    }
}
