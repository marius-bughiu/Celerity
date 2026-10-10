using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// MinMaxHeap<int, int> against the BCL types a caller would otherwise reach for. Its workload is double-ended:
// served from one end and evicted from the other, which the BCL PriorityQueue cannot do at all. The BCL's
// usual double-ended substitute is SortedSet<T>, a red-black tree that rejects duplicates, so the baseline stores
// (Priority, Seq) pairs — the sequence number breaks ties between equal priorities, exactly what a caller has
// to write. Priorities are drawn from [0, ItemCount), so they repeat.
//
// - DoubleEnded: fill, then alternately take the minimum and the maximum until empty.
// - BoundedQueue: a capped buffer (a tenth of ItemCount) fed one arrival per step; when full, an arrival is
//   admitted only if it beats the current maximum, which it evicts; every second step serves the minimum.
// - DrainMin: fill, then take only the minimum, against PriorityQueue<int, int>. This is the one-ended case
//   the BCL heap is built for, measured so the cost of double-endedness is published, not hidden.
//
// The baseline arms are named SortedSet_* / PriorityQueue_* so the dashboard classifies them as the BCL
// reference; every arm pre-sizes where its type allows it.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class MinMaxHeapBenchmark
{
    private int[] priorities = null!;
    private int bound;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        priorities = new int[ItemCount];
        var rand = new Random(42);
        for (int i = 0; i < ItemCount; i++)
            priorities[i] = rand.Next(ItemCount);

        bound = ItemCount / 10;
    }

    // ---- DoubleEnded: fill, then alternate DequeueMin / DequeueMax until empty ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("DoubleEnded")]
    public long SortedSet_DoubleEnded()
    {
        var set = new SortedSet<(int Priority, int Seq)>();
        for (int i = 0; i < priorities.Length; i++)
            set.Add((priorities[i], i));

        long acc = 0;
        while (set.Count > 0)
        {
            var min = set.Min;
            set.Remove(min);
            acc += min.Seq;
            if (set.Count == 0)
                break;

            var max = set.Max;
            set.Remove(max);
            acc -= max.Seq;
        }

        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("DoubleEnded")]
    public long MinMaxHeap_DoubleEnded()
    {
        var heap = new MinMaxHeap<int, int>(priorities.Length);
        for (int i = 0; i < priorities.Length; i++)
            heap.Enqueue(i, priorities[i]);

        long acc = 0;
        while (heap.Count > 0)
        {
            acc += heap.DequeueMin();
            if (heap.Count == 0)
                break;

            acc -= heap.DequeueMax();
        }

        return acc;
    }

    // ---- BoundedQueue: serve the minimum, evict the maximum when full ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("BoundedQueue")]
    public long SortedSet_BoundedQueue()
    {
        var set = new SortedSet<(int Priority, int Seq)>();
        long acc = 0;
        for (int i = 0; i < priorities.Length; i++)
        {
            var arrival = (priorities[i], i);
            if (set.Count < bound)
            {
                set.Add(arrival);
            }
            else
            {
                // An arrival tied with the maximum compares greater (its Seq is newer), so it is turned away —
                // the same rule as EnqueueDequeueMax.
                var max = set.Max;
                if (arrival.CompareTo(max) < 0)
                {
                    set.Remove(max);
                    set.Add(arrival);
                    acc -= max.Seq;
                }
            }

            if ((i & 1) == 1)
            {
                var min = set.Min;
                set.Remove(min);
                acc += min.Seq;
            }
        }

        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("BoundedQueue")]
    public long MinMaxHeap_BoundedQueue()
    {
        var heap = new MinMaxHeap<int, int>(bound);
        long acc = 0;
        for (int i = 0; i < priorities.Length; i++)
        {
            if (heap.Count < bound)
            {
                heap.Enqueue(i, priorities[i]);
            }
            else
            {
                int evicted = heap.EnqueueDequeueMax(i, priorities[i]);
                if (evicted != i)
                    acc -= evicted;
            }

            if ((i & 1) == 1)
                acc += heap.DequeueMin();
        }

        return acc;
    }

    // ---- DrainMin: the one-ended case, against the BCL heap built for it ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("DrainMin")]
    public long PriorityQueue_DrainMin()
    {
        var queue = new PriorityQueue<int, int>(priorities.Length);
        for (int i = 0; i < priorities.Length; i++)
            queue.Enqueue(i, priorities[i]);

        long acc = 0;
        while (queue.Count > 0)
            acc += queue.Dequeue();

        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("DrainMin")]
    public long MinMaxHeap_DrainMin()
    {
        var heap = new MinMaxHeap<int, int>(priorities.Length);
        for (int i = 0; i < priorities.Length; i++)
            heap.Enqueue(i, priorities[i]);

        long acc = 0;
        while (heap.Count > 0)
            acc += heap.DequeueMin();

        return acc;
    }
}
