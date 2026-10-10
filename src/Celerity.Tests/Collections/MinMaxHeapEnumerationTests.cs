using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration coverage for <see cref="MinMaxHeap{TElement, TPriority, TComparer}"/>: the struct enumerator
/// yields every entry exactly once (in heap order, which is deliberately not priority order), the generic and
/// non-generic <see cref="IEnumerable"/> paths agree, LINQ composes over it, <see cref="IEnumerator.Reset"/>
/// restarts it, every kind of mutation invalidates an active enumerator, and the operations that change
/// nothing — reads, and a fused push-pop that hands its own argument straight back — leave it valid.
/// </summary>
public class MinMaxHeapEnumerationTests
{
    private static MinMaxHeap<int, int> Build(int n)
    {
        var heap = new MinMaxHeap<int, int>();
        for (int i = 0; i < n; i++)
            heap.Enqueue(i, (i * 37) % 100);
        return heap;
    }

    [Fact]
    public void Enumerator_YieldsEveryEntryExactlyOnce()
    {
        var heap = Build(20);

        var seen = new Dictionary<int, int>();
        foreach (var (element, priority) in heap)
            seen.Add(element, priority);

        Assert.Equal(20, seen.Count);
        for (int i = 0; i < 20; i++)
            Assert.Equal((i * 37) % 100, seen[i]);
    }

    [Fact]
    public void Enumerator_YieldsDuplicateEntriesSeparately()
    {
        var heap = new MinMaxHeap<string, int>();
        heap.Enqueue("same", 1);
        heap.Enqueue("same", 1);

        Assert.Equal(2, heap.Count(entry => entry == ("same", 1)));
    }

    [Fact]
    public void EmptyHeap_EnumeratesNothing()
    {
        var heap = new MinMaxHeap<int, int>();
        int count = 0;
        foreach (var _ in heap)
            count++;
        Assert.Equal(0, count);
    }

    [Fact]
    public void GenericAndNonGenericPaths_Agree()
    {
        var heap = Build(10);

        var generic = new HashSet<int>();
        IEnumerator<(int Element, int Priority)> ge = ((IEnumerable<(int Element, int Priority)>)heap).GetEnumerator();
        while (ge.MoveNext())
            generic.Add(ge.Current.Element);

        var nonGeneric = new HashSet<int>();
        IEnumerator nge = ((IEnumerable)heap).GetEnumerator();
        while (nge.MoveNext())
            nonGeneric.Add((((int Element, int Priority))nge.Current!).Element);

        Assert.Equal(Enumerable.Range(0, 10).ToHashSet(), generic);
        Assert.Equal(generic, nonGeneric);
    }

    [Fact]
    public void Linq_ComposesOverEnumerator()
    {
        var heap = Build(10);
        Assert.Equal(Enumerable.Range(0, 10).Sum(i => (i * 37) % 100), heap.Sum(entry => entry.Priority));
    }

    [Fact]
    public void Current_IsDefault_BeforeTheFirstAndAfterTheLastMoveNext()
    {
        var heap = Build(1);
        MinMaxHeap<int, int>.Enumerator e = heap.GetEnumerator();
        Assert.Equal(default, e.Current);

        Assert.True(e.MoveNext());
        Assert.Equal((0, 0), e.Current);

        Assert.False(e.MoveNext());
        Assert.Equal(default, e.Current);
        e.Dispose();
    }

    [Fact]
    public void Reset_RestartsEnumeration()
    {
        var heap = Build(5);
        MinMaxHeap<int, int>.Enumerator e = heap.GetEnumerator();

        var first = new List<int>();
        while (e.MoveNext())
            first.Add(e.Current.Element);

        e.Reset();

        var second = new List<int>();
        while (e.MoveNext())
            second.Add(e.Current.Element);

        Assert.Equal(first, second);
        Assert.Equal(5, first.Count);
    }

    public static IEnumerable<object[]> MutationsThatInvalidate()
    {
        yield return new object[] { (Action<MinMaxHeap<int, int>>)(h => h.Enqueue(999, 1)) };
        yield return new object[] { (Action<MinMaxHeap<int, int>>)(h => h.DequeueMin()) };
        yield return new object[] { (Action<MinMaxHeap<int, int>>)(h => h.DequeueMax()) };
        yield return new object[] { (Action<MinMaxHeap<int, int>>)(h => h.TryDequeueMin(out _, out _)) };
        yield return new object[] { (Action<MinMaxHeap<int, int>>)(h => h.TryDequeueMax(out _, out _)) };
        yield return new object[] { (Action<MinMaxHeap<int, int>>)(h => h.EnqueueDequeueMin(999, 50)) };
        yield return new object[] { (Action<MinMaxHeap<int, int>>)(h => h.EnqueueDequeueMax(999, 50)) };
        yield return new object[] { (Action<MinMaxHeap<int, int>>)(h => h.Clear()) };
        yield return new object[] { (Action<MinMaxHeap<int, int>>)(h => h.EnsureCapacity(1024)) };
        yield return new object[] { (Action<MinMaxHeap<int, int>>)(h => h.TrimExcess()) };
    }

    [Theory]
    [MemberData(nameof(MutationsThatInvalidate))]
    public void Mutation_DuringEnumeration_Throws(Action<MinMaxHeap<int, int>> mutate)
    {
        // Seven entries leave the doubled array one slot short of full, so TrimExcess has something to trim.
        var heap = Build(7);
        MinMaxHeap<int, int>.Enumerator e = heap.GetEnumerator();
        Assert.True(e.MoveNext());

        mutate(heap);

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void ReadsAndRejectedPushPops_DoNotInvalidateEnumeration()
    {
        var heap = Build(8);
        MinMaxHeap<int, int>.Enumerator e = heap.GetEnumerator();
        Assert.True(e.MoveNext());

        _ = heap.PeekMin();
        _ = heap.PeekMax();
        _ = heap.TryPeekMin(out _, out _);
        _ = heap.TryPeekMax(out _, out _);
        _ = heap.EnsureCapacity(1);

        // Each of these hands its own argument straight back without touching the heap.
        Assert.Equal(-1, heap.EnqueueDequeueMin(-1, -1));
        Assert.Equal(-2, heap.EnqueueDequeueMax(-2, 1000));

        int remaining = 1;
        while (e.MoveNext())
            remaining++;
        Assert.Equal(8, remaining);
    }
}
