using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

// IndexedPriorityQueue<int, int, ...> vs the BCL PriorityQueue<int, int>. The two share the plain
// enqueue/dequeue heap path (the Enqueue category), where the addressable heap carries a small constant
// overhead for maintaining its element->slot index. Its headline win is the DecreaseKey category:
// the BCL PriorityQueue has no way to change a queued element's priority, so the idiomatic substitute is
// *lazy deletion* — re-enqueue the element at its new priority and skip stale copies when they surface at
// the top. That grows the heap by one entry per update (O(updates) memory) and allocates a companion
// "current priority" map, whereas IndexedPriorityQueue updates in place in O(log n) with the heap held at
// O(distinct elements). This is the priority-relaxation loop at the core of Dijkstra / Prim / A*. The
// baseline arms are named PriorityQueue_* so the dashboard classifies them as the BCL reference.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class IndexedPriorityQueueBenchmark
{
    private int[] elements = null!;      // distinct element ids [0, ItemCount)
    private int[] priorities = null!;    // an initial priority per element
    private int[] updateTargets = null!; // elements whose priority gets decreased in the DecreaseKey workload
    private int[] updateValues = null!;  // the new (strictly smaller) priorities

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        elements = new int[ItemCount];
        priorities = new int[ItemCount];
        var rand = new Random(42);
        for (int i = 0; i < ItemCount; i++)
        {
            elements[i] = i;
            priorities[i] = rand.Next(1, int.MaxValue);
        }

        // A stream of decrease-key relaxations: each picks a random element and lowers its priority, exactly
        // the shape a Dijkstra/Prim edge-relaxation loop produces.
        int updates = ItemCount;
        updateTargets = new int[updates];
        updateValues = new int[updates];
        for (int i = 0; i < updates; i++)
        {
            int target = rand.Next(ItemCount);
            updateTargets[i] = target;
            updateValues[i] = rand.Next(0, priorities[target]); // strictly smaller -> a genuine decrease-key
        }
    }

    // ---- Enqueue: build the heap from the element/priority stream ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enqueue")]
    public int PriorityQueue_Enqueue()
    {
        var pq = new PriorityQueue<int, int>();
        for (int i = 0; i < elements.Length; i++)
            pq.Enqueue(elements[i], priorities[i]);
        return pq.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Enqueue")]
    public int IndexedPriorityQueue_Enqueue()
    {
        var pq = new IndexedPriorityQueue<int, int, Int32WangHasher>(elements.Length);
        for (int i = 0; i < elements.Length; i++)
            pq.Enqueue(elements[i], priorities[i]);
        return pq.Count;
    }

    // ---- DecreaseKey: the addressable-heap headline. Build, then relax priorities, then drain. ----
    // The BCL arm emulates the only option it has: lazy deletion over a re-enqueueing heap plus a
    // current-priority map to recognize and skip stale entries on the way out.

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("DecreaseKey")]
    public long PriorityQueue_DecreaseKey()
    {
        var pq = new PriorityQueue<int, int>();
        var best = new Dictionary<int, int>(elements.Length);
        for (int i = 0; i < elements.Length; i++)
        {
            pq.Enqueue(elements[i], priorities[i]);
            best[elements[i]] = priorities[i];
        }

        // Lazy decrease-key: push a new (element, lower-priority) entry and record the new best.
        for (int i = 0; i < updateTargets.Length; i++)
        {
            int t = updateTargets[i];
            if (updateValues[i] < best[t])
            {
                best[t] = updateValues[i];
                pq.Enqueue(t, updateValues[i]);
            }
        }

        // Drain, skipping stale entries whose priority no longer matches the recorded best.
        long acc = 0;
        while (pq.TryDequeue(out int e, out int p))
        {
            if (p != best[e])
                continue; // stale duplicate left behind by a decrease-key
            acc += e;
        }

        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("DecreaseKey")]
    public long IndexedPriorityQueue_DecreaseKey()
    {
        var pq = new IndexedPriorityQueue<int, int, Int32WangHasher>(elements.Length);
        for (int i = 0; i < elements.Length; i++)
            pq.Enqueue(elements[i], priorities[i]);

        // In-place decrease-key: no heap growth, no stale entries, no companion map.
        for (int i = 0; i < updateTargets.Length; i++)
        {
            int t = updateTargets[i];
            if (updateValues[i] < pq.GetPriority(t))
                pq.Update(t, updateValues[i]);
        }

        long acc = 0;
        while (pq.TryDequeue(out int e, out _))
            acc += e;

        return acc;
    }
}
