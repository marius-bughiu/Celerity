using Celerity.Collections;
using Celerity.Hashing;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for
/// <see cref="IndexedPriorityQueue{TElement, TPriority, THasher}"/> against an independent reference model
/// (a plain <see cref="Dictionary{TKey, TValue}"/> from element to priority). CsCheck generates the
/// starting capacity and the stream of enqueue / enqueue-or-update / update / remove / dequeue operations,
/// and after every operation asserts the two agree on <c>Count</c>, membership, and the current extremum
/// (element + priority). A final full drain asserts the dequeue sequence is monotonic under the comparer
/// and reproduces exactly the oracle's remaining contents — the strongest guard against a
/// sift-up/sift-down or index-bookkeeping bug that only surfaces after many interleaved heap mutations.
///
/// <para>
/// The same generated script is run against a min-heap and against a max-heap built from an inverted
/// comparer, so a sift that hard-codes a direction rather than consulting the comparer fails on one of the
/// two. The element universe is small relative to the operation count, so elements are frequently already
/// present and the update and reject paths are reached constantly.
/// </para>
/// </summary>
public class IndexedPriorityQueueDifferentialTests
{
    private enum Op { TryEnqueue, EnqueueOrUpdate, Update, Remove, Dequeue }

    private const int Universe = 40;

    private static readonly Gen<(Op Kind, int Element, int Priority)> GenOp =
        Gen.Select(Gen.Int[0, 4].Select(n => (Op)n), Gen.Int[0, Universe - 1], Gen.Int[0, 999]);

    private static readonly Gen<(int StartCapacity, List<(Op Kind, int Element, int Priority)> Ops)> GenScript =
        Gen.Select(Gen.Int[0, 3], GenOp.List[0, 400]);

    [Fact]
    public void IndexedPriorityQueue_ShouldMatch_AReferenceModel_AsAMinHeap() =>
        Run(Comparer<int>.Default);

    [Fact]
    public void IndexedPriorityQueue_ShouldMatch_AReferenceModel_AsAMaxHeap() =>
        Run(Comparer<int>.Create((a, b) => b.CompareTo(a)));

    private static void Run(IComparer<int> comparer)
    {
        GenScript.Sample(script =>
        {
            var pq = new IndexedPriorityQueue<int, int, Int32WangHasher>(script.StartCapacity, comparer);
            var oracle = new Dictionary<int, int>();

            foreach (var (kind, element, priority) in script.Ops)
            {
                switch (kind)
                {
                    case Op.TryEnqueue:
                        // Skips when already present, matching the throwing overload's contract.
                        if (pq.TryEnqueue(element, priority))
                            oracle[element] = priority;
                        break;

                    case Op.EnqueueOrUpdate:
                        pq.EnqueueOrUpdate(element, priority);
                        oracle[element] = priority;
                        break;

                    case Op.Update:
                        if (oracle.ContainsKey(element))
                        {
                            pq.Update(element, priority);
                            oracle[element] = priority;
                        }
                        else
                        {
                            Assert.False(pq.TryUpdate(element, priority));
                        }

                        break;

                    case Op.Remove:
                    {
                        bool present = oracle.Remove(element, out int expected);
                        Assert.Equal(present, pq.Remove(element, out int actual));
                        if (present)
                            Assert.Equal(expected, actual);
                        break;
                    }

                    case Op.Dequeue:
                        if (oracle.Count == 0)
                        {
                            Assert.False(pq.TryDequeue(out _, out _));
                        }
                        else
                        {
                            Assert.True(pq.TryDequeue(out int e, out int p));
                            AssertIsExtremum(oracle, comparer, e, p);
                            oracle.Remove(e);
                        }

                        break;
                }

                Assert.Equal(oracle.Count, pq.Count);
                AssertPeekMatches(pq, oracle, comparer);
            }

            // Full drain reconciliation: the sequence must be monotonic under the comparer and empty the
            // queue to exactly the oracle's remaining contents.
            var drained = new Dictionary<int, int>();
            bool first = true;
            int prev = 0;
            while (pq.TryDequeue(out int e, out int p))
            {
                if (!first)
                    Assert.True(comparer.Compare(prev, p) <= 0, "dequeue order not monotonic under the comparer");
                prev = p;
                first = false;
                drained[e] = p;
            }

            Assert.Equal(0, pq.Count);
            Assert.Equal(oracle.Count, drained.Count);
            foreach (KeyValuePair<int, int> kv in oracle)
            {
                Assert.True(drained.TryGetValue(kv.Key, out int dp));
                Assert.Equal(kv.Value, dp);
            }
        }, iter: 40);
    }

    // The peeked element/priority must equal the oracle's current extremum (min for a default comparer, max
    // for the inverted one): the priority is the extreme value, and the element genuinely holds it.
    private static void AssertPeekMatches(
        IndexedPriorityQueue<int, int, Int32WangHasher> pq, Dictionary<int, int> oracle, IComparer<int> comparer)
    {
        if (oracle.Count == 0)
        {
            Assert.False(pq.TryPeek(out _, out _));
            return;
        }

        Assert.True(pq.TryPeek(out int e, out int p));
        AssertIsExtremum(oracle, comparer, e, p);
    }

    private static void AssertIsExtremum(Dictionary<int, int> oracle, IComparer<int> comparer, int element, int priority)
    {
        int extreme = oracle.Values.Aggregate((a, b) => comparer.Compare(a, b) <= 0 ? a : b);
        Assert.Equal(extreme, priority);
        Assert.True(oracle.TryGetValue(element, out int actual));
        Assert.Equal(priority, actual);
    }
}
