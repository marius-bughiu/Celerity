using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration coverage for <see cref="Deque{T}"/>: elements are yielded front to back, the struct
/// enumerator honours the version guard on structural mutation (but not on an in-place indexer set), and the
/// boxed <see cref="IEnumerable"/> paths agree with the struct fast path.
/// </summary>
public class DequeEnumerationTests
{
    private static List<int> Enumerate(Deque<int> deque)
    {
        var items = new List<int>();
        foreach (int x in deque)
            items.Add(x);
        return items;
    }

    [Fact]
    public void EmptyDeque_YieldsNothing()
    {
        var deque = new Deque<int>();
        Assert.Empty(Enumerate(deque));
    }

    [Fact]
    public void Enumerates_FrontToBack()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);
        deque.PushBack(2);
        deque.PushFront(0);

        Assert.Equal(new[] { 0, 1, 2 }, Enumerate(deque));
    }

    [Fact]
    public void Enumerates_AcrossWrappedLayout()
    {
        var deque = new Deque<int>(4);
        deque.PushBack(1);
        deque.PushBack(2);
        deque.PushFront(0); // head wraps
        deque.PushBack(3);  // grows

        Assert.Equal(new[] { 0, 1, 2, 3 }, Enumerate(deque));
    }

    [Fact]
    public void StructuralMutation_DuringEnumeration_Throws()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);
        deque.PushBack(2);

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (int x in deque)
                deque.PushBack(3);
        });
    }

    [Fact]
    public void PopDuringEnumeration_Throws()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);
        deque.PushBack(2);

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (int x in deque)
                deque.PopFront();
        });
    }

    [Fact]
    public void IndexerSet_DuringEnumeration_DoesNotThrow()
    {
        // An in-place element replacement is not a structural change, so it must not invalidate the
        // enumerator — matching List<T>.
        var deque = new Deque<int>();
        deque.PushBack(1);
        deque.PushBack(2);
        deque.PushBack(3);

        int seen = 0;
        foreach (int x in deque)
        {
            deque[0] = 100;
            seen++;
        }
        Assert.Equal(3, seen);
    }

    [Fact]
    public void Clear_ShouldNotInvalidateEnumerator_WhenTheDequeIsAlreadyEmpty()
    {
        // A Clear() that removes nothing is not a structural modification, so it must not bump the version —
        // the rule the other 28 count-based collections in the family already follow.
        var deque = new Deque<int>();

        Deque<int>.Enumerator neverPopulated = deque.GetEnumerator();
        deque.Clear();
        Assert.False(neverPopulated.MoveNext());

        // The same holds for the far more likely shape: a defensive Clear() on a deque that some earlier
        // Clear() (or a drain by popping) already emptied.
        deque.PushBack(1);
        deque.PushBack(2);
        deque.Clear();

        Deque<int>.Enumerator afterRealClear = deque.GetEnumerator();
        deque.Clear();
        Assert.False(afterRealClear.MoveNext());

        // And when the deque was drained by popping instead, which leaves the head parked mid-buffer rather
        // than at index 0 — the state the old unconditional `_head = 0` reset was normalizing.
        deque.PushBack(3);
        deque.PushBack(4);
        Assert.Equal(3, deque.PopFront());
        Assert.Equal(4, deque.PopFront());
        Assert.Equal(0, deque.Count);

        Deque<int>.Enumerator afterDrain = deque.GetEnumerator();
        deque.Clear();
        Assert.False(afterDrain.MoveNext());

        // The un-normalized head is not observable: the deque still behaves correctly afterwards.
        deque.PushBack(5);
        deque.PushFront(4);
        Assert.Equal(new[] { 4, 5 }, deque.ToArray());
    }

    [Fact]
    public void Clear_ShouldInvalidateEnumerator_WhenTheDequeHeldElements()
    {
        // The positive control for the guard above: a Clear() that actually removes something is a structural
        // modification and must still invalidate live enumerators.
        var deque = new Deque<int>();
        deque.PushBack(1);
        deque.PushBack(2);

        Deque<int>.Enumerator e = deque.GetEnumerator();
        deque.Clear();

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void Clear_ShouldReleaseEveryOccupiedSlot_WhenTheLayoutIsWrapped()
    {
        // Guards the early-out against being placed above the reference-releasing Array.Clear calls: a wrapped
        // layout clears two runs, and both must still be reached for a non-empty deque.
        var deque = new Deque<string>(4);
        deque.PushBack("b");
        deque.PushBack("c");
        deque.PushFront("a");   // head wraps to the end of the buffer
        Assert.Equal(new[] { "a", "b", "c" }, deque.ToArray());

        int capacityBefore = deque.Capacity;
        deque.Clear();

        Assert.Equal(0, deque.Count);
        Assert.Equal(capacityBefore, deque.Capacity);
        Assert.False(deque.Contains("a"));
        Assert.Empty(deque);

        // Still usable, and the front lands back at a sane slot.
        deque.PushBack("z");
        Assert.Equal("z", deque.PeekFront());
        Assert.Equal("z", deque.PeekBack());
    }

    [Fact]
    public void MoveNext_PastEnd_StaysFalse()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);

        Deque<int>.Enumerator e = deque.GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.False(e.MoveNext());
        Assert.False(e.MoveNext()); // idempotent after exhaustion
    }

    [Fact]
    public void Reset_RestartsFromFront()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);
        deque.PushBack(2);

        Deque<int>.Enumerator e = deque.GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.Equal(1, e.Current);
        e.Reset();
        Assert.True(e.MoveNext());
        Assert.Equal(1, e.Current);
    }

    [Fact]
    public void Reset_AfterMutation_Throws()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);
        deque.PushBack(2);

        Deque<int>.Enumerator e = deque.GetEnumerator();
        Assert.True(e.MoveNext());
        deque.PushBack(3);
        Assert.Throws<InvalidOperationException>(() => e.Reset());
    }

    [Fact]
    public void BoxedEnumerable_AgreesWithStructPath()
    {
        var deque = new Deque<int>();
        deque.PushBack(1);
        deque.PushBack(2);
        deque.PushFront(0);

        IEnumerable<int> boxed = deque;
        Assert.Equal(new[] { 0, 1, 2 }, boxed.ToList());

        IEnumerable nonGeneric = deque;
        var viaNonGeneric = new List<int>();
        foreach (int x in nonGeneric)
            viaNonGeneric.Add(x);
        Assert.Equal(new[] { 0, 1, 2 }, viaNonGeneric);
    }

    [Fact]
    public void ImplementsIReadOnlyList()
    {
        var deque = new Deque<int>();
        deque.PushBack(10);
        deque.PushBack(20);

        IReadOnlyList<int> list = deque;
        Assert.Equal(2, list.Count);
        Assert.Equal(10, list[0]);
        Assert.Equal(20, list[1]);
    }
}
