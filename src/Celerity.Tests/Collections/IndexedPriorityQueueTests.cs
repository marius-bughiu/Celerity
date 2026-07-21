using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Core behavioural coverage for <see cref="IndexedPriorityQueue{TElement, TPriority, THasher}"/>:
/// construction and validation, enqueue / try-enqueue / enqueue-or-update, peek / dequeue min-order, the
/// addressable <see cref="IndexedPriorityQueue{TElement, TPriority, THasher}.Update"/> (decrease- and
/// increase-key) and <see cref="IndexedPriorityQueue{TElement, TPriority, THasher}.Remove(TElement)"/>
/// operations, priority lookups, custom comparers, capacity management, and the out-of-band default/null
/// element.
/// </summary>
public class IndexedPriorityQueueTests
{
    private static IndexedPriorityQueue<int, int, Int32WangHasher> NewIntQueue(int capacity = 0)
        => new(capacity);

    [Fact]
    public void NewQueue_IsEmpty()
    {
        var pq = NewIntQueue();

        Assert.Equal(0, pq.Count);
        Assert.False(pq.Contains(1));
        Assert.Same(Comparer<int>.Default, pq.Comparer);
    }

    [Fact]
    public void Enqueue_ThenPeek_ReturnsElement()
    {
        var pq = NewIntQueue();
        pq.Enqueue(42, 5);

        Assert.Equal(1, pq.Count);
        Assert.True(pq.Contains(42));
        Assert.Equal(42, pq.Peek());
        Assert.Equal(5, pq.GetPriority(42));
    }

    [Fact]
    public void Dequeue_ReturnsElementsInAscendingPriorityOrder()
    {
        var pq = NewIntQueue();
        pq.Enqueue(1, 30);
        pq.Enqueue(2, 10);
        pq.Enqueue(3, 20);
        pq.Enqueue(4, 5);

        Assert.Equal(4, pq.Dequeue());
        Assert.Equal(2, pq.Dequeue());
        Assert.Equal(3, pq.Dequeue());
        Assert.Equal(1, pq.Dequeue());
        Assert.Equal(0, pq.Count);
    }

    [Fact]
    public void Enqueue_DuplicateElement_Throws()
    {
        var pq = NewIntQueue();
        pq.Enqueue(7, 1);

        Assert.Throws<ArgumentException>(() => pq.Enqueue(7, 2));
        // The failed enqueue must not have changed the existing priority.
        Assert.Equal(1, pq.GetPriority(7));
        Assert.Equal(1, pq.Count);
    }

    [Fact]
    public void TryEnqueue_ReturnsFalseOnDuplicate_TrueOnNew()
    {
        var pq = NewIntQueue();

        Assert.True(pq.TryEnqueue(7, 1));
        Assert.False(pq.TryEnqueue(7, 99));
        Assert.Equal(1, pq.GetPriority(7));
        Assert.Equal(1, pq.Count);
    }

    [Fact]
    public void EnqueueOrUpdate_AddsThenUpdates()
    {
        var pq = NewIntQueue();

        Assert.True(pq.EnqueueOrUpdate(7, 10));   // new
        Assert.False(pq.EnqueueOrUpdate(7, 3));   // update
        Assert.Equal(3, pq.GetPriority(7));
        Assert.Equal(1, pq.Count);
        Assert.Equal(7, pq.Peek());
    }

    [Fact]
    public void PeekAndDequeue_OnEmpty_Throw()
    {
        var pq = NewIntQueue();

        Assert.Throws<InvalidOperationException>(() => pq.Peek());
        Assert.Throws<InvalidOperationException>(() => pq.Dequeue());
    }

    [Fact]
    public void TryPeekAndTryDequeue_OnEmpty_ReturnFalse()
    {
        var pq = NewIntQueue();

        Assert.False(pq.TryPeek(out int e1, out int p1));
        Assert.Equal(0, e1);
        Assert.Equal(0, p1);

        Assert.False(pq.TryDequeue(out int e2, out int p2));
        Assert.Equal(0, e2);
        Assert.Equal(0, p2);
    }

    [Fact]
    public void TryPeekAndTryDequeue_ReturnMinAndPriority()
    {
        var pq = NewIntQueue();
        pq.Enqueue(1, 30);
        pq.Enqueue(2, 10);

        Assert.True(pq.TryPeek(out int pe, out int pp));
        Assert.Equal(2, pe);
        Assert.Equal(10, pp);
        Assert.Equal(2, pq.Count); // peek does not remove

        Assert.True(pq.TryDequeue(out int de, out int dp));
        Assert.Equal(2, de);
        Assert.Equal(10, dp);
        Assert.Equal(1, pq.Count);
    }

    [Fact]
    public void GetPriority_OnAbsent_Throws()
    {
        var pq = NewIntQueue();
        Assert.Throws<KeyNotFoundException>(() => pq.GetPriority(99));
    }

    [Fact]
    public void TryGetPriority_ReflectsPresence()
    {
        var pq = NewIntQueue();
        pq.Enqueue(5, 50);

        Assert.True(pq.TryGetPriority(5, out int p));
        Assert.Equal(50, p);
        Assert.False(pq.TryGetPriority(6, out int missing));
        Assert.Equal(0, missing);
    }

    [Fact]
    public void Update_DecreaseKey_MovesElementToFront()
    {
        var pq = NewIntQueue();
        pq.Enqueue(1, 10);
        pq.Enqueue(2, 20);
        pq.Enqueue(3, 30);

        pq.Update(3, 1); // decrease-key: 3 becomes the new minimum

        Assert.Equal(3, pq.Peek());
        Assert.Equal(3, pq.Dequeue());
        Assert.Equal(1, pq.Dequeue());
        Assert.Equal(2, pq.Dequeue());
    }

    [Fact]
    public void Update_IncreaseKey_SinksElement()
    {
        var pq = NewIntQueue();
        pq.Enqueue(1, 10);
        pq.Enqueue(2, 20);
        pq.Enqueue(3, 30);

        pq.Update(1, 100); // increase-key: 1 sinks to the back

        Assert.Equal(2, pq.Dequeue());
        Assert.Equal(3, pq.Dequeue());
        Assert.Equal(1, pq.Dequeue());
    }

    [Fact]
    public void Update_OnAbsent_Throws()
    {
        var pq = NewIntQueue();
        Assert.Throws<KeyNotFoundException>(() => pq.Update(99, 1));
    }

    [Fact]
    public void TryUpdate_ReflectsPresence()
    {
        var pq = NewIntQueue();
        pq.Enqueue(5, 50);

        Assert.True(pq.TryUpdate(5, 1));
        Assert.Equal(1, pq.GetPriority(5));
        Assert.False(pq.TryUpdate(6, 1));
    }

    [Fact]
    public void Remove_ArbitraryElement_KeepsHeapValid()
    {
        var pq = NewIntQueue();
        for (int i = 0; i < 10; i++)
            pq.Enqueue(i, i * 10);

        Assert.True(pq.Remove(5));
        Assert.False(pq.Contains(5));
        Assert.Equal(9, pq.Count);

        // Draining the rest must still yield ascending priorities, with 5 absent.
        int prev = int.MinValue;
        var seen = new List<int>();
        while (pq.TryDequeue(out int e, out int p))
        {
            Assert.True(p >= prev);
            prev = p;
            seen.Add(e);
        }

        Assert.DoesNotContain(5, seen);
        Assert.Equal(9, seen.Count);
    }

    [Fact]
    public void Remove_Absent_ReturnsFalse()
    {
        var pq = NewIntQueue();
        pq.Enqueue(1, 1);
        Assert.False(pq.Remove(99));
        Assert.Equal(1, pq.Count);
    }

    [Fact]
    public void Remove_OutPriority_ReturnsOldPriority()
    {
        var pq = NewIntQueue();
        pq.Enqueue(1, 10);
        pq.Enqueue(2, 20);

        Assert.True(pq.Remove(2, out int removed));
        Assert.Equal(20, removed);
        Assert.False(pq.Remove(2, out int missing));
        Assert.Equal(0, missing);
    }

    [Fact]
    public void Remove_MinElement_MatchesDequeue()
    {
        var pq = NewIntQueue();
        pq.Enqueue(1, 10);
        pq.Enqueue(2, 5);
        pq.Enqueue(3, 20);

        Assert.True(pq.Remove(2)); // removing the current min
        Assert.Equal(1, pq.Peek());
    }

    [Fact]
    public void Clear_EmptiesAndIsReusable()
    {
        var pq = NewIntQueue();
        pq.Enqueue(1, 1);
        pq.Enqueue(2, 2);

        pq.Clear();
        Assert.Equal(0, pq.Count);
        Assert.False(pq.Contains(1));

        pq.Enqueue(3, 3);
        Assert.Equal(1, pq.Count);
        Assert.Equal(3, pq.Peek());
    }

    [Fact]
    public void CustomComparer_ProducesMaxHeap()
    {
        var pq = new IndexedPriorityQueue<int, int, Int32WangHasher>(
            Comparer<int>.Create((a, b) => b.CompareTo(a)));
        pq.Enqueue(1, 10);
        pq.Enqueue(2, 30);
        pq.Enqueue(3, 20);

        Assert.Equal(2, pq.Dequeue()); // highest priority first
        Assert.Equal(3, pq.Dequeue());
        Assert.Equal(1, pq.Dequeue());
    }

    [Fact]
    public void Capacity_GrowsAsElementsAreAdded()
    {
        var pq = NewIntQueue();
        int initial = pq.Capacity;
        for (int i = 0; i < 100; i++)
            pq.Enqueue(i, i);

        Assert.True(pq.Capacity >= 100);
        Assert.True(pq.Capacity >= initial);
        Assert.Equal(100, pq.Count);
    }

    [Fact]
    public void EnsureCapacity_GrowsToAtLeastRequested()
    {
        var pq = NewIntQueue();
        int result = pq.EnsureCapacity(64);

        Assert.True(result >= 64);
        Assert.True(pq.Capacity >= 64);
    }

    [Fact]
    public void EnsureCapacity_Negative_Throws()
    {
        var pq = NewIntQueue();
        Assert.Throws<ArgumentOutOfRangeException>(() => pq.EnsureCapacity(-1));
    }

    [Fact]
    public void TrimExcess_ShrinksToCount()
    {
        var pq = NewIntQueue(64);
        pq.Enqueue(1, 1);
        pq.Enqueue(2, 2);

        pq.TrimExcess();
        Assert.Equal(2, pq.Capacity);
        // Contents survive the trim and stay ordered.
        Assert.Equal(1, pq.Dequeue());
        Assert.Equal(2, pq.Dequeue());
    }

    [Fact]
    public void Constructor_NegativeCapacity_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IndexedPriorityQueue<int, int, Int32WangHasher>(-1));
    }

    [Fact]
    public void SeedingConstructor_LoadsAllPairs_LastPriorityWinsOnDuplicate()
    {
        var items = new[]
        {
            new KeyValuePair<int, int>(1, 100),
            new KeyValuePair<int, int>(2, 50),
            new KeyValuePair<int, int>(1, 1), // duplicate key: last priority wins
        };

        var pq = new IndexedPriorityQueue<int, int, Int32WangHasher>(items);

        Assert.Equal(2, pq.Count);
        Assert.Equal(1, pq.GetPriority(1));
        Assert.Equal(1, pq.Peek()); // element 1 with the winning priority 1 is the min
    }

    [Fact]
    public void SeedingConstructor_NullItems_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new IndexedPriorityQueue<int, int, Int32WangHasher>((IEnumerable<KeyValuePair<int, int>>)null!));
    }

    [Fact]
    public void DefaultOrNullElement_IsHandled()
    {
        // Reference-type element: the null element must route through the dogfooded index like any other.
        var pq = new IndexedPriorityQueue<string, int, DefaultHasher<string>>();
        pq.Enqueue(null!, 5);
        pq.Enqueue("a", 10);
        pq.Enqueue("b", 1);

        Assert.True(pq.Contains(null!));
        Assert.Equal(3, pq.Count);
        Assert.Equal(5, pq.GetPriority(null!));

        pq.Update(null!, 0); // the null element becomes the min
        Assert.Null(pq.Peek());
        Assert.Null(pq.Dequeue());

        Assert.True(pq.Remove("a", out int ap));
        Assert.Equal(10, ap);
        Assert.Equal("b", pq.Dequeue());
    }

    [Fact]
    public void DijkstraStyleRelaxation_ProducesShortestFirstOrder()
    {
        // Frontier seeded at "infinity", then relaxed (decrease-key) as shorter paths are found — the
        // canonical addressable-heap workload the BCL PriorityQueue cannot express without lazy deletion.
        var pq = NewIntQueue();
        for (int v = 0; v < 5; v++)
            pq.Enqueue(v, int.MaxValue);

        pq.Update(0, 0);   // source
        pq.Update(3, 2);
        pq.Update(1, 4);
        pq.Update(3, 1);   // a shorter path to 3 is found (second decrease-key)
        pq.Update(2, 3);

        Assert.Equal(0, pq.Dequeue()); // dist 0
        Assert.Equal(3, pq.Dequeue()); // dist 1
        Assert.Equal(2, pq.Dequeue()); // dist 3
        Assert.Equal(1, pq.Dequeue()); // dist 4
        Assert.Equal(4, pq.Dequeue()); // dist int.MaxValue (unreached)
    }
}
