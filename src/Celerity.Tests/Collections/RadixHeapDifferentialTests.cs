using System.Numerics;
using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="RadixHeap{TElement}"/> against an independent reference
/// model: a plain list of the live <c>(element, priority)</c> entries plus the floor the heap should report.
/// CsCheck generates the seed contents (built with the seeded constructor) and a stream of enqueue / rejected
/// enqueue / peek / dequeue / clear / trim / rebuild operations. Every generated enqueue respects the monotone
/// contract by construction — it is the current floor plus an offset — and the offsets mix zero (a tie with the
/// floor), small steps, single-bit jumps up to bit 63 and arbitrary 64-bit values, so every bucket from 0 to 64
/// is reached. After every operation it asserts the two agree on <c>Count</c>, <c>Floor</c> and the minimum, that
/// the enumerator yields exactly the model's multiset, and that it walks the buckets in non-decreasing order — so
/// a redistribution that leaves an entry in the wrong bucket is caught at the step that broke it, not only when a
/// later dequeue surfaces it. A final drain reconciles the remaining contents in priority order.
///
/// <para>
/// Elements are unique sequence numbers so a removed entry can be identified exactly; ties come from the
/// zero-offset enqueues and from seed priorities drawn out of a narrow range.
/// </para>
/// </summary>
public class RadixHeapDifferentialTests
{
    private enum Op { Enqueue, EnqueueBelowFloor, Peek, Dequeue, Clear, TrimExcess, Rebuild }

    private static readonly Gen<ulong> GenOffset = Gen.Frequency(
        (3, Gen.Const(0UL)),
        (6, Gen.ULong[0, 16]),
        (3, Gen.Int[0, 63].Select(bit => 1UL << bit)),
        (2, Gen.Select(Gen.Int[0, 63], Gen.ULong[0, 7], (bit, low) => (1UL << bit) | low)),
        (1, Gen.ULong));

    private static readonly Gen<(Op Kind, ulong Offset)> GenOp =
        Gen.Select(Gen.Frequency((6, Gen.Const(Op.Enqueue)), (1, Gen.Const(Op.EnqueueBelowFloor)),
            (2, Gen.Const(Op.Peek)), (5, Gen.Const(Op.Dequeue)), (1, Gen.Const(Op.Clear)),
            (1, Gen.Const(Op.TrimExcess)), (1, Gen.Const(Op.Rebuild))),
            GenOffset);

    private static readonly Gen<(List<ulong> Seed, List<(Op Kind, ulong Offset)> Ops)> GenScript =
        Gen.Select(Gen.Frequency((3, Gen.ULong[0, 30]), (1, GenOffset)).List[0, 70], GenOp.List[0, 300]);

    [Fact]
    public void RadixHeap_ShouldMatch_AReferenceModel()
    {
        GenScript.Sample(script =>
        {
            int next = 0;
            var oracle = new List<(int Element, ulong Priority)>();
            foreach (ulong p in script.Seed)
                oracle.Add((next++, p));

            var heap = new RadixHeap<int>(oracle);
            ulong floor = 0;
            AssertAgrees(heap, oracle, floor);

            foreach (var (kind, offset) in script.Ops)
            {
                switch (kind)
                {
                    case Op.Enqueue:
                    {
                        ulong priority = floor + Math.Min(offset, ulong.MaxValue - floor);
                        heap.Enqueue(next, priority);
                        oracle.Add((next++, priority));
                        break;
                    }

                    case Op.EnqueueBelowFloor:
                        if (floor > 0)
                        {
                            ulong below = floor - 1 - Math.Min(offset, floor - 1);
                            Assert.Throws<ArgumentOutOfRangeException>("priority", () => heap.Enqueue(-1, below));
                        }

                        break;

                    case Op.Peek:
                        if (heap.TryPeek(out int peekedElement, out ulong peekedPriority))
                        {
                            Assert.Equal(Min(oracle), peekedPriority);
                            Assert.Contains((peekedElement, peekedPriority), oracle);
                            Assert.Equal(peekedElement, heap.Peek());
                        }
                        else
                        {
                            Assert.Empty(oracle);
                            Assert.Throws<InvalidOperationException>(() => heap.Peek());
                        }

                        break;

                    case Op.Dequeue:
                        if (heap.TryDequeue(out int element, out ulong dequeued))
                        {
                            TakeMin(oracle, element, dequeued);
                            floor = dequeued;
                        }
                        else
                        {
                            Assert.Empty(oracle);
                        }

                        break;

                    case Op.Clear:
                        heap.Clear();
                        oracle.Clear();
                        floor = 0;
                        break;

                    case Op.TrimExcess:
                        heap.TrimExcess();
                        break;

                    case Op.Rebuild:
                        // The seeded constructor over the live contents: same multiset, floor back to 0.
                        heap = new RadixHeap<int>(heap.ToList());
                        floor = 0;
                        break;
                }

                AssertAgrees(heap, oracle, floor);
            }

            // Drain: each pop must be the oracle's current minimum, and the floor must follow it.
            while (heap.Count > 0)
            {
                Assert.True(heap.TryDequeue(out int element, out ulong priority));
                TakeMin(oracle, element, priority);
                Assert.Equal(priority, heap.Floor);
            }

            Assert.Empty(oracle);
            Assert.False(heap.TryDequeue(out _, out _));
        }, iter: 100);
    }

    private static ulong Min(List<(int Element, ulong Priority)> oracle) => oracle.Min(entry => entry.Priority);

    // Asserts (element, priority) is a live entry holding the oracle's current minimum, then removes it.
    private static void TakeMin(List<(int Element, ulong Priority)> oracle, int element, ulong priority)
    {
        Assert.Equal(Min(oracle), priority);
        int at = oracle.FindIndex(entry => entry.Element == element);
        Assert.True(at >= 0, "the heap returned an element the model does not hold");
        Assert.Equal(priority, oracle[at].Priority);
        oracle.RemoveAt(at);
    }

    private static void AssertAgrees(RadixHeap<int> heap, List<(int Element, ulong Priority)> oracle, ulong floor)
    {
        Assert.Equal(oracle.Count, heap.Count);
        Assert.Equal(floor, heap.Floor);

        if (oracle.Count == 0)
        {
            Assert.False(heap.TryPeek(out _, out _));
        }
        else
        {
            Assert.True(heap.TryPeek(out _, out ulong min));
            Assert.Equal(Min(oracle), min);
            Assert.True(min >= floor);
        }

        // The enumerator yields the exact multiset, lowest bucket first.
        var entries = heap.ToList();
        Assert.Equal(oracle.OrderBy(e => e.Element), entries.OrderBy(e => e.Element));

        int previous = 0;
        foreach (var (_, priority) in entries)
        {
            Assert.True(priority >= floor, "an entry below the floor is queued");
            int bucket = 64 - BitOperations.LeadingZeroCount(priority ^ floor);
            Assert.True(bucket >= previous, "the enumerator went back to a lower bucket");
            previous = bucket;
        }
    }
}
