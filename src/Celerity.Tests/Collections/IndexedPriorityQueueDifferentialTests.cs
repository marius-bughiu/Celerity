using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Deterministic, seeded differential coverage for
/// <see cref="IndexedPriorityQueue{TElement, TPriority, THasher}"/>. Each seed drives the same random stream
/// of enqueue / enqueue-or-update / update / remove / dequeue operations into the priority queue and into an
/// independent reference model (a plain <see cref="Dictionary{TKey, TValue}"/> from element to priority), and
/// after every operation asserts the two agree on <c>Count</c>, membership, and the current minimum
/// (element + priority). A final full drain asserts the dequeue sequence is monotonic under the comparer and
/// reproduces exactly the oracle's remaining contents — the strongest guard against a sift-up/sift-down or
/// index-bookkeeping bug that only surfaces after many interleaved heap mutations.
/// </summary>
public class IndexedPriorityQueueDifferentialTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RandomOps_MatchReferenceModel_MinHeap(int startCapacity)
    {
        RunDifferential(startCapacity, maxHeap: false);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void RandomOps_MatchReferenceModel_MaxHeap(int startCapacity)
    {
        RunDifferential(startCapacity, maxHeap: true);
    }

    private static void RunDifferential(int startCapacity, bool maxHeap)
    {
        const int Seeds = 40;
        const int OpsPerSeed = 800;
        const int Universe = 40; // small universe so elements are frequently already present

        IComparer<int> comparer = maxHeap
            ? Comparer<int>.Create((a, b) => b.CompareTo(a))
            : Comparer<int>.Default;

        for (int seed = 0; seed < Seeds; seed++)
        {
            var rng = new Random(seed * 6389 + startCapacity + (maxHeap ? 1 : 0));
            var pq = new IndexedPriorityQueue<int, int, Int32WangHasher>(startCapacity, comparer);
            var oracle = new Dictionary<int, int>();

            for (int op = 0; op < OpsPerSeed; op++)
            {
                int roll = rng.Next(5);
                switch (roll)
                {
                    case 0: // enqueue (skip if already present, matching the throwing contract)
                    {
                        int x = rng.Next(Universe);
                        int prio = rng.Next(1000);
                        if (pq.TryEnqueue(x, prio))
                            oracle[x] = prio;
                        break;
                    }

                    case 1: // enqueue-or-update
                    {
                        int x = rng.Next(Universe);
                        int prio = rng.Next(1000);
                        pq.EnqueueOrUpdate(x, prio);
                        oracle[x] = prio;
                        break;
                    }

                    case 2: // update (only when present)
                    {
                        int x = rng.Next(Universe);
                        int prio = rng.Next(1000);
                        if (oracle.ContainsKey(x))
                        {
                            pq.Update(x, prio);
                            oracle[x] = prio;
                        }
                        else
                        {
                            Assert.False(pq.TryUpdate(x, prio));
                        }
                        break;
                    }

                    case 3: // remove
                    {
                        int x = rng.Next(Universe);
                        bool present = oracle.Remove(x, out int expected);
                        bool removed = pq.Remove(x, out int actual);
                        Assert.Equal(present, removed);
                        if (present)
                            Assert.Equal(expected, actual);
                        break;
                    }

                    default: // dequeue the current minimum
                    {
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
        }
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
