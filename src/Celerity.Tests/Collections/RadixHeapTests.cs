using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Unit coverage for <see cref="RadixHeap{TElement}"/>: construction and validation, the
/// <see cref="RadixHeap{TElement}.Floor"/> contract (rejection below it, acceptance at it, its reset by
/// <see cref="RadixHeap{TElement}.Clear"/>, and <see cref="RadixHeap{TElement}.Peek"/> never raising it), the
/// priority extremes and the top-bit bucket, duplicates and ties, the seeded constructor, the capacity
/// surface, reference-typed elements, and a monotone Dijkstra-style interleaving checked against a sorted model.
/// </summary>
public class RadixHeapTests
{
    [Fact]
    public void NewHeap_IsEmpty()
    {
        var heap = new RadixHeap<string>();

        Assert.Equal(0, heap.Count);
        Assert.Equal(0UL, heap.Floor);
        Assert.False(heap.TryPeek(out string? element, out ulong priority));
        Assert.Null(element);
        Assert.Equal(0UL, priority);
        Assert.False(heap.TryDequeue(out element, out priority));
        Assert.Null(element);
        Assert.Equal(0UL, priority);
        Assert.Equal(0UL, heap.Floor);
    }

    [Fact]
    public void EmptyHeap_Throws_FromEveryNonTryAccessor()
    {
        var heap = new RadixHeap<int>();

        Assert.Throws<InvalidOperationException>(() => heap.Peek());
        var ex = Assert.Throws<InvalidOperationException>(() => heap.Dequeue());
        Assert.Equal("The heap is empty.", ex.Message);
    }

    [Fact]
    public void Constructor_RejectsANullSource()
    {
        Assert.Throws<ArgumentNullException>("items", () => new RadixHeap<int>(null!));
    }

    [Fact]
    public void SingleEntry_IsPeekedAndDequeued()
    {
        var heap = new RadixHeap<string>();
        heap.Enqueue("only", 5);

        Assert.Equal("only", heap.Peek());
        Assert.True(heap.TryPeek(out string? element, out ulong priority));
        Assert.Equal(("only", 5UL), (element, priority));
        Assert.Equal(0UL, heap.Floor);

        Assert.Equal("only", heap.Dequeue());
        Assert.Equal(0, heap.Count);
        Assert.Equal(5UL, heap.Floor);
    }

    [Fact]
    public void Entries_DrainInPriorityOrder()
    {
        ulong[] priorities = { 50, 10, 90, 30, 70, 20, 80, 40, 60, 0, 100, 1, 1023, 1024, 65535 };
        var heap = new RadixHeap<ulong>();
        foreach (ulong p in priorities)
            heap.Enqueue(p * 10, p);

        var drained = new List<ulong>();
        while (heap.TryDequeue(out ulong element, out ulong priority))
        {
            Assert.Equal(priority * 10, element);
            Assert.Equal(priority, heap.Floor);
            drained.Add(priority);
        }

        Assert.Equal(priorities.OrderBy(p => p), drained);
    }

    [Fact]
    public void Enqueue_RejectsAPriorityBelowTheFloor()
    {
        var heap = new RadixHeap<int>();
        heap.Enqueue(1, 10);
        heap.Enqueue(2, 20);
        Assert.Equal(1, heap.Dequeue());
        Assert.Equal(10UL, heap.Floor);

        var ex = Assert.Throws<ArgumentOutOfRangeException>("priority", () => heap.Enqueue(3, 9));
        Assert.Equal(9UL, ex.ActualValue);
        Assert.Contains("Floor (10)", ex.Message);
        Assert.Equal(1, heap.Count);

        heap.Enqueue(4, 10);   // equal to the floor is accepted
        Assert.Equal(4, heap.Dequeue());
        Assert.Equal(2, heap.Dequeue());
        Assert.Equal(20UL, heap.Floor);
        Assert.Throws<ArgumentOutOfRangeException>("priority", () => heap.Enqueue(5, 19));
    }

    [Fact]
    public void Enqueue_RejectsBelowTheFloor_EvenWhenTheHeapIsEmpty()
    {
        var heap = new RadixHeap<int>();
        heap.Enqueue(1, 1000);
        heap.Dequeue();
        Assert.Equal(0, heap.Count);

        Assert.Throws<ArgumentOutOfRangeException>("priority", () => heap.Enqueue(2, 0));
        heap.Enqueue(3, 1000);
        Assert.Equal(3, heap.Peek());
    }

    [Fact]
    public void Enqueue_AtTheFloor_ComesOutBeforeAnythingHigher()
    {
        var heap = new RadixHeap<string>();
        heap.Enqueue("a", 4);
        heap.Enqueue("b", 9);
        Assert.Equal("a", heap.Dequeue());

        heap.Enqueue("floor", 4);
        Assert.True(heap.TryPeek(out string? element, out ulong priority));
        Assert.Equal(("floor", 4UL), (element, priority));
        Assert.Equal("floor", heap.Dequeue());
        Assert.Equal("b", heap.Dequeue());
    }

    [Fact]
    public void ExtremePriorities_ZeroAndMaxValue_AreOrdered()
    {
        var heap = new RadixHeap<string>();
        heap.Enqueue("max", ulong.MaxValue);
        heap.Enqueue("zero", 0);
        heap.Enqueue("max2", ulong.MaxValue);

        Assert.Equal("zero", heap.Dequeue());
        Assert.Equal(0UL, heap.Floor);

        Assert.True(heap.TryDequeue(out string? element, out ulong priority));
        Assert.StartsWith("max", element);
        Assert.Equal(ulong.MaxValue, priority);
        Assert.Equal(ulong.MaxValue, heap.Floor);

        heap.Enqueue("again", ulong.MaxValue);
        Assert.Throws<ArgumentOutOfRangeException>("priority", () => heap.Enqueue("below", ulong.MaxValue - 1));
        Assert.Equal(2, heap.Count);
        heap.Dequeue();
        heap.Dequeue();
        Assert.Equal(ulong.MaxValue, heap.Floor);
    }

    [Fact]
    public void Priorities_DifferingOnlyInTheTopBit_UseTheHighestBucket()
    {
        const ulong Top = 1UL << 63;
        var heap = new RadixHeap<int>();
        heap.Enqueue(1, Top | 5);
        heap.Enqueue(2, 5);
        heap.Enqueue(3, Top | 3);
        heap.Enqueue(4, Top);

        Assert.Equal(2, heap.Dequeue());
        Assert.Equal(5UL, heap.Floor);

        // Every remaining entry differs from the floor at bit 63, so they all sit in bucket 64 until the next
        // dequeue redistributes it.
        Assert.True(heap.TryPeek(out int element, out ulong priority));
        Assert.Equal((4, Top), (element, priority));
        Assert.Equal(4, heap.Dequeue());
        Assert.Equal(Top, heap.Floor);
        Assert.Equal(3, heap.Dequeue());
        Assert.Equal(1, heap.Dequeue());
        Assert.Equal(Top | 5, heap.Floor);
    }

    [Fact]
    public void EveryBitPosition_DrainsInOrder()
    {
        var heap = new RadixHeap<int>();
        for (int bit = 63; bit >= 0; bit--)
            heap.Enqueue(bit, 1UL << bit);
        heap.Enqueue(-1, 0);

        Assert.Equal(-1, heap.Dequeue());
        for (int bit = 0; bit < 64; bit++)
        {
            Assert.True(heap.TryDequeue(out int element, out ulong priority));
            Assert.Equal(bit, element);
            Assert.Equal(1UL << bit, priority);
        }

        Assert.Equal(0, heap.Count);
    }

    [Fact]
    public void Duplicates_AndTies_AreAllKept()
    {
        var heap = new RadixHeap<string>();
        heap.Enqueue("a", 3);
        heap.Enqueue("a", 3);
        heap.Enqueue("b", 3);
        heap.Enqueue("c", 1);
        heap.Enqueue("d", 7);

        Assert.Equal(5, heap.Count);
        Assert.Equal("c", heap.Dequeue());

        var tied = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            Assert.True(heap.TryDequeue(out string? element, out ulong priority));
            Assert.Equal(3UL, priority);
            tied.Add(element);
        }

        tied.Sort(StringComparer.Ordinal);
        Assert.Equal(new[] { "a", "a", "b" }, tied);
        Assert.Equal("d", heap.Dequeue());
    }

    [Fact]
    public void ManyTies_AtTheFloor_GrowBucketZero()
    {
        var heap = new RadixHeap<int>();
        for (int i = 0; i < 100; i++)
            heap.Enqueue(i, 0);

        var seen = new HashSet<int>();
        while (heap.TryDequeue(out int element, out ulong priority))
        {
            Assert.Equal(0UL, priority);
            Assert.True(seen.Add(element));
        }

        Assert.Equal(100, seen.Count);
    }

    [Fact]
    public void Peek_DoesNotRaiseTheFloor()
    {
        var heap = new RadixHeap<string>();
        heap.Enqueue("high", 100);

        Assert.Equal("high", heap.Peek());
        Assert.True(heap.TryPeek(out _, out ulong peeked));
        Assert.Equal(100UL, peeked);
        Assert.Equal(0UL, heap.Floor);

        // A priority between the floor and the peeked minimum is still legal, and it is the new minimum.
        heap.Enqueue("between", 50);
        Assert.Equal("between", heap.Peek());
        Assert.Equal("between", heap.Dequeue());
        Assert.Equal(50UL, heap.Floor);
        Assert.Equal("high", heap.Dequeue());
    }

    [Fact]
    public void Peek_AfterARedistribution_ReadsBucketZero()
    {
        var heap = new RadixHeap<int>();
        heap.Enqueue(1, 8);
        heap.Enqueue(2, 8);
        heap.Enqueue(3, 9);

        Assert.True(heap.TryDequeue(out int first, out ulong p));
        Assert.Equal(8UL, p);

        // The redistribution left the other 8 in bucket 0, so this is the O(1) read.
        Assert.True(heap.TryPeek(out int second, out p));
        Assert.Equal(8UL, p);
        Assert.NotEqual(first, second);
        Assert.Equal(second, heap.Dequeue());
        Assert.Equal(3, heap.Peek());
    }

    [Fact]
    public void SourceConstructor_SeedsEverything()
    {
        var rand = new Random(11);
        var items = Enumerable.Range(0, 257)
            .Select(i => (Element: i, Priority: (ulong)rand.Next(50) << rand.Next(40)))
            .ToList();

        var heap = new RadixHeap<int>(items);
        Assert.Equal(items.Count, heap.Count);
        Assert.Equal(0UL, heap.Floor);

        var ascending = items.Select(x => x.Priority).OrderBy(p => p).ToList();
        foreach (ulong expected in ascending)
        {
            Assert.True(heap.TryDequeue(out int e, out ulong p));
            Assert.Equal(expected, p);
            Assert.Equal(items[e].Priority, p);
        }

        Assert.Equal(0, heap.Count);
    }

    [Fact]
    public void SourceConstructor_AcceptsALazySequence()
    {
        static IEnumerable<(int, ulong)> Lazy()
        {
            for (int i = 9; i >= 0; i--)
                yield return (i, (ulong)i);
        }

        var heap = new RadixHeap<int>(Lazy());
        Assert.Equal(10, heap.Count);
        Assert.Equal(0, heap.Peek());
    }

    [Fact]
    public void SourceConstructor_OfAnEmptySource_IsEmpty()
    {
        var heap = new RadixHeap<int>(Array.Empty<(int, ulong)>());
        Assert.Equal(0, heap.Count);
        heap.Enqueue(1, 1);
        Assert.Equal(1, heap.Peek());
    }

    [Fact]
    public void Clear_EmptiesTheHeap_ResetsTheFloor_AndKeepsWorking()
    {
        var heap = new RadixHeap<string>();
        for (int i = 0; i < 6; i++)
            heap.Enqueue("e" + i, (ulong)(i * 1000));
        heap.Dequeue();
        heap.Dequeue();
        Assert.Equal(1000UL, heap.Floor);

        heap.Clear();
        Assert.Equal(0, heap.Count);
        Assert.Equal(0UL, heap.Floor);
        Assert.False(heap.TryPeek(out _, out _));
        Assert.False(heap.TryDequeue(out _, out _));

        // Below the old floor is legal again.
        heap.Enqueue("again", 3);
        heap.Enqueue("zero", 0);
        Assert.Equal("zero", heap.Dequeue());
        Assert.Equal("again", heap.Dequeue());
    }

    [Fact]
    public void Clear_OfAnEmptyHeap_StillResetsTheFloor()
    {
        var heap = new RadixHeap<int>();
        heap.Enqueue(1, 77);
        heap.Dequeue();
        Assert.Equal(77UL, heap.Floor);

        heap.Clear();
        Assert.Equal(0UL, heap.Floor);
        heap.Enqueue(2, 0);
        Assert.Equal(2, heap.Dequeue());
    }

    [Fact]
    public void Clear_OfAValueTypedHeap_EmptiesIt()
    {
        var heap = new RadixHeap<int>();
        heap.Enqueue(1, 1);
        heap.Enqueue(2, 1 << 20);
        heap.Clear();
        Assert.Equal(0, heap.Count);
        Assert.False(heap.TryPeek(out _, out _));
    }

    [Fact]
    public void TrimExcess_ReleasesDrainedBuckets_AndKeepsTheContents()
    {
        var heap = new RadixHeap<int>();
        for (int i = 0; i < 5; i++)
            heap.Enqueue(i, 0);           // bucket 0: five entries in an array of eight
        heap.Enqueue(100, 1UL << 40);
        heap.Enqueue(101, 1UL << 41);
        heap.Enqueue(102, 7);

        for (int i = 0; i < 6; i++)
            heap.Dequeue();               // drains bucket 0 and redistributes the 7 into it

        heap.TrimExcess();
        Assert.Equal(2, heap.Count);
        Assert.Equal(100, heap.Dequeue());
        Assert.Equal(101, heap.Dequeue());

        heap.TrimExcess();                // every bucket now empty: each gives up its array
        heap.Enqueue(5, 1UL << 41);
        heap.Enqueue(6, 1UL << 50);
        Assert.Equal(5, heap.Dequeue());
        Assert.Equal(6, heap.Dequeue());
    }

    [Fact]
    public void TrimExcess_OfAFreshOrExactHeap_DoesNotInvalidateAnEnumerator()
    {
        var heap = new RadixHeap<int>();
        var fresh = heap.GetEnumerator();
        heap.TrimExcess();
        Assert.False(fresh.MoveNext());

        for (int i = 0; i < 4; i++)
            heap.Enqueue(i, 0);           // exactly fills the default bucket capacity

        var enumerator = heap.GetEnumerator();
        heap.TrimExcess();
        Assert.True(enumerator.MoveNext());
    }

    [Fact]
    public void ReferenceElements_SurviveRedistribution_AndClear()
    {
        var heap = new RadixHeap<string>();
        for (int i = 0; i < 40; i++)
            heap.Enqueue("s" + i, (ulong)(i * 997 % 401));

        var drained = new List<ulong>();
        for (int i = 0; i < 20; i++)
        {
            Assert.True(heap.TryDequeue(out string? element, out ulong priority));
            Assert.NotNull(element);
            drained.Add(priority);
        }

        Assert.Equal(drained.OrderBy(p => p), drained);
        heap.TrimExcess();
        heap.Clear();
        Assert.Equal(0, heap.Count);
    }

    [Fact]
    public void RemovedReferences_AreNotRetained_ByTheBucketArrays()
    {
        var heap = new RadixHeap<object>();
        WeakReference weak = EnqueueThenDequeue(heap);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(weak.IsAlive);
        Assert.Equal(1, heap.Count);
    }

    [Fact]
    public void ClearedReferences_AreNotRetained_ByTheBucketArrays()
    {
        var heap = new RadixHeap<object>();
        WeakReference weak = EnqueueThenClear(heap);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(weak.IsAlive);
        Assert.Equal(0, heap.Count);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference EnqueueThenDequeue(RadixHeap<object> heap)
    {
        var payload = new object();
        heap.Enqueue(payload, 3);           // redistributed out of bucket 2, then popped from bucket 0
        heap.Enqueue(new object(), 1UL << 30);
        Assert.Same(payload, heap.Dequeue());
        return new WeakReference(payload);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference EnqueueThenClear(RadixHeap<object> heap)
    {
        var payload = new object();
        heap.Enqueue(payload, 12345);
        heap.Clear();
        return new WeakReference(payload);
    }

    [Fact]
    public void DijkstraStyleInterleaving_MatchesASortedModel()
    {
        var rand = new Random(2026);
        var heap = new RadixHeap<int>();
        var model = new List<ulong>();
        int next = 0;
        var owner = new Dictionary<int, ulong>();

        for (int i = 0; i < 64; i++)
        {
            ulong p = (ulong)rand.Next(1000);
            heap.Enqueue(next, p);
            owner[next++] = p;
            model.Add(p);
        }

        while (model.Count > 0)
        {
            model.Sort();
            Assert.True(heap.TryPeek(out _, out ulong peeked));
            Assert.Equal(model[0], peeked);

            Assert.True(heap.TryDequeue(out int element, out ulong priority));
            Assert.Equal(model[0], priority);
            Assert.Equal(owner[element], priority);
            Assert.True(owner.Remove(element));
            model.RemoveAt(0);
            Assert.Equal(priority, heap.Floor);
            Assert.Equal(model.Count, heap.Count);

            // Relax a few "edges": every new tentative distance is the current one plus a non-negative weight.
            if (next < 4000)
            {
                int edges = rand.Next(4);
                for (int k = 0; k < edges; k++)
                {
                    ulong weight = rand.Next(8) switch
                    {
                        0 => 0,
                        1 => (ulong)rand.Next(1 << 20) << 20,
                        _ => (ulong)rand.Next(64),
                    };
                    ulong relaxed = priority + weight;
                    heap.Enqueue(next, relaxed);
                    owner[next++] = relaxed;
                    model.Add(relaxed);
                }
            }
        }

        Assert.Equal(0, heap.Count);
        Assert.Empty(owner);
    }
}
