using System.Numerics;
using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="MinMaxHeap{TElement, TPriority, TComparer}"/>
/// against an independent reference model: a plain list of the live <c>(element, priority)</c> entries.
/// CsCheck generates the seed contents (built with the <c>O(n)</c> heapify constructor), the starting state
/// and a stream of enqueue / dequeue-min / dequeue-max / fused push-pop / trim operations. After every
/// operation it asserts the two agree on <c>Count</c> and on both extremes, and it re-checks the
/// <b>min-max heap property at every node</b> by reading the backing array through the enumerator, which
/// walks it in slot order — so a sift that leaves the structure wrong is caught at the step that broke it,
/// not only when a later pop surfaces it. A final drain from both ends reconciles the remaining contents.
///
/// <para>
/// Elements are unique sequence numbers so a removed entry can be identified exactly, while priorities come
/// from a deliberately narrow range so ties are constant and the arbitrary-tie paths are exercised. The
/// script runs once under the natural order and once under an inverted struct comparer, so a sift that
/// hard-codes a direction rather than consulting the comparer fails on one of the two.
/// </para>
/// </summary>
public class MinMaxHeapDifferentialTests
{
    private enum Op { Enqueue, DequeueMin, DequeueMax, EnqueueDequeueMin, EnqueueDequeueMax, TrimExcess }

    private readonly struct Descending : IComparer<int>
    {
        public int Compare(int x, int y) => y.CompareTo(x);
    }

    private static readonly Gen<(Op Kind, int Priority)> GenOp =
        Gen.Select(Gen.Frequency((4, Gen.Const(Op.Enqueue)), (2, Gen.Const(Op.DequeueMin)), (2, Gen.Const(Op.DequeueMax)),
            (2, Gen.Const(Op.EnqueueDequeueMin)), (2, Gen.Const(Op.EnqueueDequeueMax)), (1, Gen.Const(Op.TrimExcess))),
            Gen.Int[0, 30]);

    private static readonly Gen<(List<int> Seed, List<(Op Kind, int Priority)> Ops)> GenScript =
        Gen.Select(Gen.Int[0, 30].List[0, 70], GenOp.List[0, 300]);

    [Fact]
    public void MinMaxHeap_ShouldMatch_AReferenceModel_UnderTheNaturalOrder() =>
        Run<DefaultComparer<int>>(Comparer<int>.Default);

    [Fact]
    public void MinMaxHeap_ShouldMatch_AReferenceModel_UnderAnInvertedComparer() =>
        Run<Descending>(new Descending());

    private static void Run<TComparer>(IComparer<int> order)
        where TComparer : struct, IComparer<int>
    {
        GenScript.Sample(script =>
        {
            int next = 0;
            var oracle = new List<(int Element, int Priority)>();
            foreach (int p in script.Seed)
                oracle.Add((next++, p));

            var heap = new MinMaxHeap<int, int, TComparer>(oracle, default);
            AssertAgrees(heap, oracle, order);

            foreach (var (kind, priority) in script.Ops)
            {
                switch (kind)
                {
                    case Op.Enqueue:
                        heap.Enqueue(next, priority);
                        oracle.Add((next++, priority));
                        break;

                    case Op.DequeueMin:
                        if (heap.TryDequeueMin(out int minElement, out int minPriority))
                            TakeExtreme(oracle, order, minElement, minPriority, wantMax: false);
                        else
                            Assert.Empty(oracle);
                        break;

                    case Op.DequeueMax:
                        if (heap.TryDequeueMax(out int maxElement, out int maxPriority))
                            TakeExtreme(oracle, order, maxElement, maxPriority, wantMax: true);
                        else
                            Assert.Empty(oracle);
                        break;

                    case Op.EnqueueDequeueMin:
                    {
                        int element = next++;
                        oracle.Add((element, priority));
                        int returned = heap.EnqueueDequeueMin(element, priority);
                        TakeExtreme(oracle, order, returned, PriorityOf(oracle, returned), wantMax: false);
                        break;
                    }

                    case Op.EnqueueDequeueMax:
                    {
                        int element = next++;
                        oracle.Add((element, priority));
                        int returned = heap.EnqueueDequeueMax(element, priority);
                        TakeExtreme(oracle, order, returned, PriorityOf(oracle, returned), wantMax: true);
                        break;
                    }

                    case Op.TrimExcess:
                        heap.TrimExcess();
                        Assert.Equal(heap.Count, heap.Capacity);
                        break;
                }

                AssertAgrees(heap, oracle, order);
            }

            // Drain from alternating ends; each pop must be the oracle's current extreme.
            bool fromMin = true;
            while (heap.Count > 0)
            {
                int element, priority;
                if (fromMin)
                    Assert.True(heap.TryDequeueMin(out element, out priority));
                else
                    Assert.True(heap.TryDequeueMax(out element, out priority));

                TakeExtreme(oracle, order, element, priority, wantMax: !fromMin);
                fromMin = !fromMin;
            }

            Assert.Empty(oracle);
        }, iter: 60);
    }

    private static int PriorityOf(List<(int Element, int Priority)> oracle, int element) =>
        oracle.First(entry => entry.Element == element).Priority;

    // Asserts (element, priority) is a live entry holding the oracle's current minimum (or maximum), then
    // removes it from the oracle.
    private static void TakeExtreme(
        List<(int Element, int Priority)> oracle, IComparer<int> order, int element, int priority, bool wantMax)
    {
        Assert.Equal(Extreme(oracle, order, wantMax), priority);
        int at = oracle.FindIndex(entry => entry.Element == element);
        Assert.True(at >= 0, "the heap returned an element the model does not hold");
        Assert.Equal(priority, oracle[at].Priority);
        oracle.RemoveAt(at);
    }

    private static int Extreme(List<(int Element, int Priority)> oracle, IComparer<int> order, bool wantMax) =>
        oracle.Select(entry => entry.Priority)
            .Aggregate((a, b) => (order.Compare(a, b) < 0) == wantMax ? b : a);

    private static void AssertAgrees<TComparer>(
        MinMaxHeap<int, int, TComparer> heap, List<(int Element, int Priority)> oracle, IComparer<int> order)
        where TComparer : struct, IComparer<int>
    {
        Assert.Equal(oracle.Count, heap.Count);
        if (oracle.Count == 0)
        {
            Assert.False(heap.TryPeekMin(out _, out _));
            Assert.False(heap.TryPeekMax(out _, out _));
            return;
        }

        Assert.True(heap.TryPeekMin(out _, out int min));
        Assert.True(heap.TryPeekMax(out _, out int max));
        Assert.Equal(Extreme(oracle, order, wantMax: false), min);
        Assert.Equal(Extreme(oracle, order, wantMax: true), max);

        // The enumerator walks the backing array in slot order, so slot i is the i-th entry. A min-level node
        // must be no greater than every descendant and a max-level node no smaller; checking each node against
        // its parent and grandparent is equivalent, since every deeper descendant is reached through a chain
        // of grandchildren on the same kind of level.
        var slots = heap.Select(entry => entry.Priority).ToArray();
        Assert.Equal(oracle.Select(e => e.Priority).OrderBy(p => p), slots.OrderBy(p => p));
        for (int i = 1; i < slots.Length; i++)
        {
            AssertOrdered(slots, order, (i - 1) >> 1, i);
            if (i >= 3)
                AssertOrdered(slots, order, (((i - 1) >> 1) - 1) >> 1, i);
        }
    }

    // An ancestor on a min level must not exceed its descendant; one on a max level must not undercut it.
    private static void AssertOrdered(int[] slots, IComparer<int> order, int ancestor, int descendant)
    {
        bool minLevel = (BitOperations.Log2((uint)ancestor + 1) & 1) == 0;
        int cmp = order.Compare(slots[ancestor], slots[descendant]);
        Assert.True(minLevel ? cmp <= 0 : cmp >= 0,
            $"min-max property broken between slot {ancestor} and slot {descendant}");
    }
}
