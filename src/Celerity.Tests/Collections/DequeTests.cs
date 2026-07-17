using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Core behavioural coverage for <see cref="Deque{T}"/>: the four end operations
/// (<see cref="Deque{T}.PushFront"/> / <see cref="Deque{T}.PushBack"/> / <see cref="Deque{T}.PopFront"/> /
/// <see cref="Deque{T}.PopBack"/>), their peek and non-throwing <c>Try*</c> variants, the front-based
/// indexer, <see cref="Deque{T}.Contains"/> / <see cref="Deque{T}.ToArray"/> / <see cref="Deque{T}.CopyTo"/>,
/// <see cref="Deque{T}.Clear"/>, the constructors, and the empty-deque exception contracts.
/// </summary>
public class DequeTests
{
    [Fact]
    public void NewDeque_IsEmpty()
    {
        var deque = new Deque<int>();
        Assert.Equal(0, deque.Count);
        Assert.False(deque.TryPeekFront(out _));
        Assert.False(deque.TryPeekBack(out _));
    }

    [Fact]
    public void PushBack_ThenPopFront_IsFifo()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);
        deque.PushBack(2);
        deque.PushBack(3);

        Assert.Equal(3, deque.Count);
        Assert.Equal(1, deque.PopFront());
        Assert.Equal(2, deque.PopFront());
        Assert.Equal(3, deque.PopFront());
        Assert.Equal(0, deque.Count);
    }

    [Fact]
    public void PushBack_ThenPopBack_IsLifo()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);
        deque.PushBack(2);
        deque.PushBack(3);

        Assert.Equal(3, deque.PopBack());
        Assert.Equal(2, deque.PopBack());
        Assert.Equal(1, deque.PopBack());
    }

    [Fact]
    public void PushFront_PrependsInReverse()
    {
        var deque = new Deque<int>();
        deque.PushFront(1);
        deque.PushFront(2);
        deque.PushFront(3);

        // Front-to-back is now 3, 2, 1.
        Assert.Equal(new[] { 3, 2, 1 }, deque.ToArray());
        Assert.Equal(3, deque.PopFront());
        Assert.Equal(1, deque.PopBack());
    }

    [Fact]
    public void MixedEndOperations_MaintainOrder()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);   // [1]
        deque.PushFront(0);  // [0, 1]
        deque.PushBack(2);   // [0, 1, 2]
        deque.PushFront(-1); // [-1, 0, 1, 2]

        Assert.Equal(new[] { -1, 0, 1, 2 }, deque.ToArray());
        Assert.Equal(-1, deque.PeekFront());
        Assert.Equal(2, deque.PeekBack());
    }

    [Fact]
    public void PeekFront_And_PeekBack_DoNotRemove()
    {
        var deque = new Deque<int>();
        deque.PushBack(10);
        deque.PushBack(20);

        Assert.Equal(10, deque.PeekFront());
        Assert.Equal(20, deque.PeekBack());
        Assert.Equal(2, deque.Count);
    }

    [Fact]
    public void Indexer_IsFrontRelative()
    {
        var deque = new Deque<string>();
        deque.PushBack("a");
        deque.PushBack("b");
        deque.PushBack("c");

        Assert.Equal("a", deque[0]);
        Assert.Equal("b", deque[1]);
        Assert.Equal("c", deque[2]);
    }

    [Fact]
    public void Indexer_Set_ReplacesElementInPlace()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);
        deque.PushBack(2);
        deque.PushBack(3);

        deque[1] = 20;

        Assert.Equal(new[] { 1, 20, 3 }, deque.ToArray());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Indexer_OutOfRange_Throws(int index)
    {
        var deque = new Deque<int>();
        deque.PushBack(1);
        deque.PushBack(2);
        deque.PushBack(3);

        Assert.Throws<ArgumentOutOfRangeException>(() => deque[index]);
        Assert.Throws<ArgumentOutOfRangeException>(() => deque[index] = 0);
    }

    [Fact]
    public void PopFront_OnEmpty_Throws()
    {
        var deque = new Deque<int>();
        Assert.Throws<InvalidOperationException>(() => deque.PopFront());
    }

    [Fact]
    public void PopBack_OnEmpty_Throws()
    {
        var deque = new Deque<int>();
        Assert.Throws<InvalidOperationException>(() => deque.PopBack());
    }

    [Fact]
    public void PeekFront_OnEmpty_Throws()
    {
        var deque = new Deque<int>();
        Assert.Throws<InvalidOperationException>(() => deque.PeekFront());
    }

    [Fact]
    public void PeekBack_OnEmpty_Throws()
    {
        var deque = new Deque<int>();
        Assert.Throws<InvalidOperationException>(() => deque.PeekBack());
    }

    [Fact]
    public void TryPopFront_Empty_ReturnsFalseWithDefault()
    {
        var deque = new Deque<int>();
        Assert.False(deque.TryPopFront(out int value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryPopBack_Empty_ReturnsFalseWithDefault()
    {
        var deque = new Deque<int>();
        Assert.False(deque.TryPopBack(out int value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryPop_And_TryPeek_ReturnEndElements()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);
        deque.PushBack(2);
        deque.PushBack(3);

        Assert.True(deque.TryPeekFront(out int front));
        Assert.Equal(1, front);
        Assert.True(deque.TryPeekBack(out int back));
        Assert.Equal(3, back);

        Assert.True(deque.TryPopFront(out int popFront));
        Assert.Equal(1, popFront);
        Assert.True(deque.TryPopBack(out int popBack));
        Assert.Equal(3, popBack);
        Assert.Equal(1, deque.Count);
    }

    [Fact]
    public void Clear_EmptiesButLeavesUsable()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);
        deque.PushBack(2);

        deque.Clear();

        Assert.Equal(0, deque.Count);
        Assert.False(deque.TryPeekFront(out _));

        deque.PushBack(9);
        Assert.Equal(9, deque.PeekFront());
        Assert.Equal(1, deque.Count);
    }

    [Fact]
    public void Contains_FindsPresentAndRejectsAbsent()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);
        deque.PushFront(2);
        deque.PushBack(3);

        Assert.True(deque.Contains(1));
        Assert.True(deque.Contains(2));
        Assert.True(deque.Contains(3));
        Assert.False(deque.Contains(4));
    }

    [Fact]
    public void Contains_WorksWithReferenceTypesAndNull()
    {
        var deque = new Deque<string?>();
        deque.PushBack("a");
        deque.PushBack(null);

        Assert.True(deque.Contains(null));
        Assert.True(deque.Contains("a"));
        Assert.False(deque.Contains("b"));
    }

    [Fact]
    public void ToArray_Empty_ReturnsEmpty()
    {
        var deque = new Deque<int>();
        Assert.Empty(deque.ToArray());
    }

    [Fact]
    public void CopyTo_CopiesFrontToBackAtOffset()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);
        deque.PushBack(2);
        deque.PushBack(3);

        var dest = new int[5];
        deque.CopyTo(dest, 1);

        Assert.Equal(new[] { 0, 1, 2, 3, 0 }, dest);
    }

    [Fact]
    public void CopyTo_Validation()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);

        Assert.Throws<ArgumentNullException>(() => deque.CopyTo(null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => deque.CopyTo(new int[2], -1));
        Assert.Throws<ArgumentException>(() => deque.CopyTo(new int[1], 1));
    }

    [Fact]
    public void CapacityConstructor_NegativeThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Deque<int>(-1));
    }

    [Fact]
    public void CapacityConstructor_PreSizesWithoutAffectingCount()
    {
        var deque = new Deque<int>(16);
        Assert.Equal(0, deque.Count);
        Assert.True(deque.Capacity >= 16);
    }

    [Fact]
    public void EnumerableConstructor_CopiesInOrder()
    {
        var deque = new Deque<int>(new[] { 5, 6, 7 });

        Assert.Equal(3, deque.Count);
        Assert.Equal(new[] { 5, 6, 7 }, deque.ToArray());
        Assert.Equal(5, deque.PeekFront());
        Assert.Equal(7, deque.PeekBack());
    }

    [Fact]
    public void EnumerableConstructor_FromLazySequence_CopiesInOrder()
    {
        static IEnumerable<int> Lazy()
        {
            yield return 1;
            yield return 2;
            yield return 3;
        }

        var deque = new Deque<int>(Lazy());
        Assert.Equal(new[] { 1, 2, 3 }, deque.ToArray());
    }

    [Fact]
    public void EnumerableConstructor_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Deque<int>((IEnumerable<int>)null!));
    }

    [Fact]
    public void EnumerableConstructor_Empty_IsEmpty()
    {
        var deque = new Deque<int>(Array.Empty<int>());
        Assert.Equal(0, deque.Count);
    }
}
