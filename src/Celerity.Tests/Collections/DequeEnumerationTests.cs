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
