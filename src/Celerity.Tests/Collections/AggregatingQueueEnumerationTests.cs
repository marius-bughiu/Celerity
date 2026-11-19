using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration coverage for <see cref="AggregatingQueue{T, TMonoid}"/>: order across a wrapped ring, the three
/// enumerator surfaces, and which operations invalidate a live enumerator.
/// </summary>
public class AggregatingQueueEnumerationTests
{
    [Fact]
    public void Enumeration_ShouldYieldNothing_WhenEmpty()
    {
        var queue = new AggregatingQueue<int, SumMonoid<int>>();

        Assert.Empty(queue);

        var enumerator = queue.GetEnumerator();
        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);
    }

    [Fact]
    public void Enumeration_ShouldYieldOldestToNewest_AcrossTheWrap()
    {
        var queue = new AggregatingQueue<int, SumMonoid<int>>(4);
        for (int i = 0; i < 4; i++)
            queue.Enqueue(i);
        queue.Dequeue();
        queue.Dequeue();
        queue.Enqueue(4);
        queue.Enqueue(5);                                    // physical order is now 4 5 2 3

        Assert.Equal(new[] { 2, 3, 4, 5 }, queue);
        Assert.Equal(4, queue.Capacity);
    }

    [Fact]
    public void GenericAndNonGenericEnumerators_ShouldYieldTheSameElements()
    {
        var queue = new AggregatingQueue<string, ConcatMonoid>(new[] { "a", "b", "c" });

        var generic = new List<string>();
        using (IEnumerator<string> e = ((IEnumerable<string>)queue).GetEnumerator())
        {
            while (e.MoveNext())
                generic.Add(e.Current);
        }

        var nonGeneric = new List<object?>();
        IEnumerator legacy = ((IEnumerable)queue).GetEnumerator();
        while (legacy.MoveNext())
            nonGeneric.Add(legacy.Current);

        Assert.Equal(new[] { "a", "b", "c" }, generic);
        Assert.Equal(new object[] { "a", "b", "c" }, nonGeneric);
    }

    [Fact]
    public void Reset_ShouldRewind_WhenTheQueueWasNotModified()
    {
        var queue = new AggregatingQueue<int, MaxMonoid<int>>(new[] { 4, 6, 1 });
        var enumerator = queue.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());

        // Reads are not mutations.
        _ = queue.Aggregate;
        _ = queue.Peek();
        Assert.True(queue.TryPeek(out _));

        enumerator.Reset();

        var values = new List<int>();
        while (enumerator.MoveNext())
            values.Add(enumerator.Current);
        Assert.Equal(new[] { 4, 6, 1 }, values);
        enumerator.Dispose();
    }

    [Fact]
    public void Enqueue_ShouldInvalidateLiveEnumerators()
    {
        var queue = new AggregatingQueue<int, SumMonoid<int>>(new[] { 1, 2 });
        var enumerator = queue.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        queue.Enqueue(3);

        var ex = Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.Contains("modified during enumeration", ex.Message);
        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void Dequeue_ShouldInvalidateLiveEnumerators()
    {
        var queue = new AggregatingQueue<int, SumMonoid<int>>(new[] { 1, 2 });
        var enumerator = queue.GetEnumerator();

        queue.Dequeue();

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void TryDequeue_ShouldInvalidateLiveEnumerators_OnlyWhenItRemovesSomething()
    {
        var empty = new AggregatingQueue<int, SumMonoid<int>>();
        var survivor = empty.GetEnumerator();
        Assert.False(empty.TryDequeue(out _));
        Assert.False(survivor.MoveNext());

        var queue = new AggregatingQueue<int, SumMonoid<int>>(new[] { 1 });
        var enumerator = queue.GetEnumerator();
        Assert.True(queue.TryDequeue(out _));
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Clear_ShouldInvalidateLiveEnumerators_WhenItRemovesSomething()
    {
        var queue = new AggregatingQueue<int, SumMonoid<int>>(new[] { 1, 2 });
        var enumerator = queue.GetEnumerator();

        queue.Clear();

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Enumeration_ShouldBeUnaffectedByAFlip_ItCannotObserve()
    {
        // A dequeue is what flips; it is a mutation in its own right, so a flip never happens under a live
        // enumerator. Enumerating straight after one sees the logical order, not the fold layout.
        var queue = new AggregatingQueue<int, SumMonoid<int>>(new[] { 1, 2, 3, 4 });
        queue.Dequeue();
        queue.Enqueue(5);

        Assert.Equal(new[] { 2, 3, 4, 5 }, queue.ToList());
        Assert.Equal(14, queue.Aggregate);
    }
}
