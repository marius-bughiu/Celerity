using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Unit coverage for <see cref="MinMaxHeap{TElement, TPriority, TComparer}"/> and its
/// <see cref="MinMaxHeap{TElement, TPriority}"/> alias: construction and validation, both ends of the queue,
/// the fused push-pop forms and their tie rules, duplicates, a custom struct comparer, a <c>null</c>
/// priority, the bottom-up heapify constructor, and the capacity surface.
/// </summary>
public class MinMaxHeapTests
{
    private readonly struct Descending : IComparer<int>
    {
        public int Compare(int x, int y) => y.CompareTo(x);
    }

    [Fact]
    public void NewHeap_IsEmpty()
    {
        var heap = new MinMaxHeap<string, int>();

        Assert.Equal(0, heap.Count);
        Assert.Equal(0, heap.Capacity);
        Assert.False(heap.TryPeekMin(out _, out _));
        Assert.False(heap.TryPeekMax(out _, out _));
        Assert.False(heap.TryDequeueMin(out _, out _));
        Assert.False(heap.TryDequeueMax(out _, out _));
    }

    [Fact]
    public void EmptyHeap_Throws_FromEveryNonTryAccessor()
    {
        var heap = new MinMaxHeap<int, int>();

        Assert.Throws<InvalidOperationException>(() => heap.PeekMin());
        Assert.Throws<InvalidOperationException>(() => heap.PeekMax());
        Assert.Throws<InvalidOperationException>(() => heap.DequeueMin());
        var ex = Assert.Throws<InvalidOperationException>(() => heap.DequeueMax());
        Assert.Equal("The heap is empty.", ex.Message);
    }

    [Fact]
    public void Constructors_RejectANegativeCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>("capacity", () => new MinMaxHeap<int, int>(-1));
        Assert.Throws<ArgumentOutOfRangeException>("capacity", () => new MinMaxHeap<int, int, Descending>(-1));
        Assert.Throws<ArgumentOutOfRangeException>("capacity", () => new MinMaxHeap<int, int, Descending>(-1, default));
    }

    [Fact]
    public void Constructors_RejectANullSource()
    {
        Assert.Throws<ArgumentNullException>("items", () => new MinMaxHeap<int, int>(null!));
        Assert.Throws<ArgumentNullException>("items", () => new MinMaxHeap<int, int, Descending>(null!));
        Assert.Throws<ArgumentNullException>("items", () => new MinMaxHeap<int, int, Descending>(null!, default));
    }

    [Fact]
    public void CapacityConstructor_PreSizesTheBackingArray()
    {
        var heap = new MinMaxHeap<int, int>(10);
        Assert.Equal(10, heap.Capacity);

        for (int i = 0; i < 10; i++)
            heap.Enqueue(i, i);
        Assert.Equal(10, heap.Capacity);

        heap.Enqueue(10, 10);
        Assert.Equal(20, heap.Capacity);
    }

    [Fact]
    public void Enqueue_FromZeroCapacity_GrowsToTheDefault()
    {
        var heap = new MinMaxHeap<int, int>();
        heap.Enqueue(1, 1);
        Assert.Equal(4, heap.Capacity);
    }

    [Fact]
    public void SingleEntry_IsBothTheMinimumAndTheMaximum()
    {
        var heap = new MinMaxHeap<string, int>();
        heap.Enqueue("only", 5);

        Assert.Equal("only", heap.PeekMin());
        Assert.Equal("only", heap.PeekMax());
        Assert.True(heap.TryPeekMax(out string? element, out int priority));
        Assert.Equal(("only", 5), (element, priority));

        Assert.Equal("only", heap.DequeueMax());
        Assert.Equal(0, heap.Count);
    }

    [Fact]
    public void TwoEntries_TheMaximumIsTheRootsOnlyChild()
    {
        var heap = new MinMaxHeap<string, int>();
        heap.Enqueue("high", 9);
        heap.Enqueue("low", 1);

        Assert.Equal("low", heap.PeekMin());
        Assert.Equal("high", heap.PeekMax());
    }

    [Fact]
    public void BothEnds_DrainInOrder()
    {
        int[] priorities = { 50, 10, 90, 30, 70, 20, 80, 40, 60, 0, 100 };
        var heap = new MinMaxHeap<int, int>();
        foreach (int p in priorities)
            heap.Enqueue(p * 10, p);

        Assert.Equal(0, heap.PeekMin());
        Assert.Equal(1000, heap.PeekMax());

        var lows = new List<int>();
        var highs = new List<int>();
        while (heap.Count > 0)
        {
            Assert.True(heap.TryDequeueMin(out int element, out int priority));
            Assert.Equal(priority * 10, element);
            lows.Add(priority);
            if (heap.TryDequeueMax(out element, out priority))
            {
                Assert.Equal(priority * 10, element);
                highs.Add(priority);
            }
        }

        Assert.Equal(new[] { 0, 10, 20, 30, 40, 50 }, lows);
        Assert.Equal(new[] { 100, 90, 80, 70, 60 }, highs);
    }

    [Fact]
    public void Duplicates_AreAllKept()
    {
        var heap = new MinMaxHeap<string, int>();
        heap.Enqueue("a", 3);
        heap.Enqueue("a", 3);
        heap.Enqueue("b", 3);
        heap.Enqueue("c", 1);

        Assert.Equal(4, heap.Count);
        Assert.Equal("c", heap.DequeueMin());

        var rest = new List<string> { heap.DequeueMax(), heap.DequeueMin(), heap.DequeueMax() };
        rest.Sort(StringComparer.Ordinal);
        Assert.Equal(new[] { "a", "a", "b" }, rest);
    }

    [Fact]
    public void StructComparer_InvertsBothEnds()
    {
        var heap = new MinMaxHeap<int, int, Descending>(new Descending());
        foreach (int p in new[] { 5, 1, 9, 3, 7 })
            heap.Enqueue(p, p);

        // Under a descending comparer the "minimum" is the largest value.
        Assert.Equal(9, heap.PeekMin());
        Assert.Equal(1, heap.PeekMax());
        Assert.Equal(9, heap.DequeueMin());
        Assert.Equal(1, heap.DequeueMax());
        Assert.Equal(7, heap.DequeueMin());
    }

    [Fact]
    public void ComparerProperty_ReturnsTheSuppliedComparer()
    {
        var heap = new MinMaxHeap<int, int, Descending>(4, new Descending());
        Assert.Equal(1, heap.Comparer.Compare(0, 1));
        Assert.Equal(4, heap.Capacity);

        Assert.Equal(-1, new MinMaxHeap<int, int>().Comparer.Compare(0, 1));
    }

    [Fact]
    public void NullPriority_IsOrderedFirst_ByTheDefaultComparer()
    {
        var heap = new MinMaxHeap<int, string?>();
        heap.Enqueue(1, "m");
        heap.Enqueue(2, null);
        heap.Enqueue(3, "z");

        Assert.Equal(2, heap.PeekMin());
        Assert.Equal(3, heap.PeekMax());
    }

    [Fact]
    public void EnqueueDequeueMin_OnAnEmptyHeap_ReturnsTheArgument_AndLeavesTheHeapEmpty()
    {
        var heap = new MinMaxHeap<string, int>();
        Assert.Equal("x", heap.EnqueueDequeueMin("x", 5));
        Assert.Equal(0, heap.Count);
    }

    [Fact]
    public void EnqueueDequeueMin_ReturnsTheArgument_WhenItIsNoGreaterThanTheMinimum()
    {
        var heap = new MinMaxHeap<string, int>();
        heap.Enqueue("a", 5);
        heap.Enqueue("b", 9);

        Assert.Equal("lower", heap.EnqueueDequeueMin("lower", 1));
        Assert.Equal("tie", heap.EnqueueDequeueMin("tie", 5));   // the BCL EnqueueDequeue tie rule
        Assert.Equal(2, heap.Count);
        Assert.Equal("a", heap.PeekMin());
    }

    [Fact]
    public void EnqueueDequeueMin_SwapsOutTheMinimum_WhenTheArgumentIsGreater()
    {
        var heap = new MinMaxHeap<string, int>();
        heap.Enqueue("a", 1);
        heap.Enqueue("b", 5);
        heap.Enqueue("c", 9);

        // The newcomer outranks the maximum, so the trickle has to carry it to the max level.
        Assert.Equal("a", heap.EnqueueDequeueMin("top", 20));
        Assert.Equal(3, heap.Count);
        Assert.Equal("b", heap.PeekMin());
        Assert.Equal("top", heap.PeekMax());
    }

    [Fact]
    public void EnqueueDequeueMax_OnAnEmptyHeap_ReturnsTheArgument_AndLeavesTheHeapEmpty()
    {
        var heap = new MinMaxHeap<string, int>();
        Assert.Equal("x", heap.EnqueueDequeueMax("x", 5));
        Assert.Equal(0, heap.Count);
    }

    [Fact]
    public void EnqueueDequeueMax_ReturnsTheArgument_WhenItIsNoSmallerThanTheMaximum()
    {
        var heap = new MinMaxHeap<string, int>();
        heap.Enqueue("a", 5);
        heap.Enqueue("b", 9);

        Assert.Equal("higher", heap.EnqueueDequeueMax("higher", 12));
        Assert.Equal("tie", heap.EnqueueDequeueMax("tie", 9));
        Assert.Equal(2, heap.Count);
        Assert.Equal("b", heap.PeekMax());
    }

    [Fact]
    public void EnqueueDequeueMax_OnASingleEntry_ReplacesTheRoot()
    {
        var heap = new MinMaxHeap<string, int>();
        heap.Enqueue("old", 9);

        Assert.Equal("old", heap.EnqueueDequeueMax("new", 3));
        Assert.Equal(1, heap.Count);
        Assert.Equal("new", heap.PeekMin());
        Assert.Equal("new", heap.PeekMax());
    }

    [Fact]
    public void EnqueueDequeueMax_KeepsTheMinimum_WhenTheNewcomerUndercutsTheRoot()
    {
        var heap = new MinMaxHeap<string, int>();
        foreach (var (e, p) in new[] { ("a", 4), ("b", 9), ("c", 7), ("d", 6), ("e", 8) })
            heap.Enqueue(e, p);

        Assert.Equal("b", heap.EnqueueDequeueMax("floor", 1));
        Assert.Equal("floor", heap.PeekMin());
        Assert.Equal("e", heap.PeekMax());

        var drained = new List<int>();
        while (heap.TryDequeueMin(out _, out int p))
            drained.Add(p);
        Assert.Equal(new[] { 1, 4, 6, 7, 8 }, drained);
    }

    [Fact]
    public void EnqueueDequeueMax_ActsAsACappedBuffer_ThatKeepsTheSmallest()
    {
        const int Cap = 8;
        var heap = new MinMaxHeap<int, int>(Cap);
        var rand = new Random(7);
        var all = new List<int>();
        for (int i = 0; i < 500; i++)
        {
            int p = rand.Next(1000);
            all.Add(p);
            if (heap.Count < Cap)
                heap.Enqueue(p, p);
            else
                heap.EnqueueDequeueMax(p, p);
        }

        var kept = new List<int>();
        while (heap.TryDequeueMin(out int e, out _))
            kept.Add(e);

        all.Sort();
        Assert.Equal(all.Take(Cap), kept);
    }

    [Fact]
    public void SourceConstructor_HeapifiesEverything()
    {
        var rand = new Random(11);
        var items = Enumerable.Range(0, 257).Select(i => (Element: i, Priority: rand.Next(50))).ToList();

        var heap = new MinMaxHeap<int, int>(items);
        Assert.Equal(items.Count, heap.Count);
        Assert.Equal(items.Count, heap.Capacity);

        var ascending = items.Select(x => x.Priority).OrderBy(p => p).ToList();
        for (int i = 0; i < 64; i++)
        {
            Assert.True(heap.TryDequeueMin(out int e, out int p));
            Assert.Equal(ascending[i], p);
            Assert.Equal(items[e].Priority, p);
            Assert.True(heap.TryDequeueMax(out e, out p));
            Assert.Equal(ascending[ascending.Count - 1 - i], p);
            Assert.Equal(items[e].Priority, p);
        }
    }

    [Fact]
    public void SourceConstructor_AcceptsALazySequence_AndAComparer()
    {
        IEnumerable<(int, int)> Lazy()
        {
            for (int i = 0; i < 10; i++)
                yield return (i, i);
        }

        var heap = new MinMaxHeap<int, int, Descending>(Lazy(), new Descending());
        Assert.Equal(10, heap.Count);
        Assert.Equal(9, heap.PeekMin());
        Assert.Equal(0, heap.PeekMax());

        var defaulted = new MinMaxHeap<int, int, Descending>(Lazy());
        Assert.Equal(9, defaulted.PeekMin());
    }

    [Fact]
    public void SourceConstructor_OfAnEmptySource_IsEmpty()
    {
        var heap = new MinMaxHeap<int, int>(Array.Empty<(int, int)>());
        Assert.Equal(0, heap.Count);
        heap.Enqueue(1, 1);
        Assert.Equal(1, heap.PeekMax());
    }

    [Fact]
    public void Clear_EmptiesTheHeap_AndRetainsCapacity()
    {
        var heap = new MinMaxHeap<string, string>();
        for (int i = 0; i < 6; i++)
            heap.Enqueue("e" + i, "p" + i);
        int capacity = heap.Capacity;

        heap.Clear();
        Assert.Equal(0, heap.Count);
        Assert.Equal(capacity, heap.Capacity);
        Assert.False(heap.TryPeekMin(out _, out _));

        heap.Enqueue("again", "p");
        Assert.Equal("again", heap.PeekMax());
    }

    [Fact]
    public void Clear_OfAValueTypedHeap_EmptiesIt()
    {
        var heap = new MinMaxHeap<int, int>();
        heap.Enqueue(1, 1);
        heap.Clear();
        Assert.Equal(0, heap.Count);
    }

    [Fact]
    public void EnsureCapacity_GrowsOnlyWhenNeeded()
    {
        var heap = new MinMaxHeap<int, int>(4);
        Assert.Equal(4, heap.EnsureCapacity(2));
        Assert.Equal(16, heap.EnsureCapacity(16));
        Assert.Equal(16, heap.Capacity);
        Assert.Throws<ArgumentOutOfRangeException>("capacity", () => heap.EnsureCapacity(-1));
    }

    [Fact]
    public void TrimExcess_ShrinksToTheCount_AndIsANoOpWhenExact()
    {
        var heap = new MinMaxHeap<int, int>(32);
        for (int i = 0; i < 5; i++)
            heap.Enqueue(i, i);

        heap.TrimExcess();
        Assert.Equal(5, heap.Capacity);

        var enumerator = heap.GetEnumerator();
        heap.TrimExcess();   // already exact: must not invalidate the enumerator
        Assert.True(enumerator.MoveNext());

        Assert.Equal(0, heap.PeekMin());
        Assert.Equal(4, heap.PeekMax());
    }

    [Fact]
    public void RemovedReferences_AreNotRetained_ByTheBackingArray()
    {
        var heap = new MinMaxHeap<object, string>();
        WeakReference weak = EnqueueThenDequeueMax(heap);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(weak.IsAlive);
        Assert.Equal(1, heap.Count);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference EnqueueThenDequeueMax(MinMaxHeap<object, string> heap)
    {
        var payload = new object();
        heap.Enqueue(new object(), "a");
        heap.Enqueue(payload, "z");
        Assert.Same(payload, heap.DequeueMax());
        return new WeakReference(payload);
    }
}
