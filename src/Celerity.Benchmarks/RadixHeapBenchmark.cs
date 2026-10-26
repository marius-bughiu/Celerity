using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// RadixHeap<int> against the BCL PriorityQueue<int, ulong>, the comparison-based 4-ary heap a caller would
// otherwise reach for. Every workload is monotone — no priority enqueued is below the last one dequeued — since
// that is the only shape a radix heap accepts.
//
// - Drain: enqueue ItemCount entries with random priorities in [0, 2^30), then dequeue them all.
// - Hold: the steady-state "hold" model of an event simulation — fill, then ItemCount times take the minimum and
//   re-enqueue it a random increment in [0, 1000) later, so the queue size stays at ItemCount.
// - Dijkstra: lazy-deletion single-source shortest path (duplicates pushed, stale pops skipped) over a fixed
//   random directed graph of ItemCount vertices, eight out-edges each, integer weights in [1, 1000).
//
// Both arms construct a fresh queue inside the measured method with the parameterless constructor. RadixHeap has
// no capacity constructor, so PriorityQueue is not pre-sized either: each type grows from empty, the same way.
// All inputs (priorities, increments, the CSR graph) are generated once in GlobalSetup from a fixed seed. The
// baseline arms are named PriorityQueue_* so the dashboard classifies them as the BCL reference.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RadixHeapBenchmark
{
    private const int OutDegree = 8;

    private ulong[] priorities = null!;
    private ulong[] increments = null!;

    // The graph in CSR form: vertex v's out-edges are edgeTargets/edgeWeights[edgeStart[v] .. edgeStart[v + 1]).
    private int[] edgeStart = null!;
    private int[] edgeTargets = null!;
    private ulong[] edgeWeights = null!;
    private ulong[] dist = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        priorities = new ulong[ItemCount];
        increments = new ulong[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            priorities[i] = (ulong)rand.Next(1 << 30);
            increments[i] = (ulong)rand.Next(1000);
        }

        // The first out-edge of each vertex closes a Hamiltonian cycle (v -> v + 1), so every vertex is reachable
        // from the source; the other seven go to random targets.
        edgeStart = new int[ItemCount + 1];
        edgeTargets = new int[ItemCount * OutDegree];
        edgeWeights = new ulong[ItemCount * OutDegree];
        for (int v = 0; v < ItemCount; v++)
        {
            int start = v * OutDegree;
            edgeStart[v] = start;
            edgeTargets[start] = (v + 1) % ItemCount;
            edgeWeights[start] = (ulong)rand.Next(1, 1000);
            for (int e = 1; e < OutDegree; e++)
            {
                edgeTargets[start + e] = rand.Next(ItemCount);
                edgeWeights[start + e] = (ulong)rand.Next(1, 1000);
            }
        }

        edgeStart[ItemCount] = ItemCount * OutDegree;
        dist = new ulong[ItemCount];
    }

    // ---- Drain: fill with random priorities, then dequeue everything ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Drain")]
    public ulong PriorityQueue_Drain()
    {
        var queue = new PriorityQueue<int, ulong>();
        for (int i = 0; i < priorities.Length; i++)
            queue.Enqueue(i, priorities[i]);

        ulong acc = 0;
        while (queue.TryDequeue(out int element, out ulong priority))
            acc += priority ^ (ulong)element;

        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Drain")]
    public ulong RadixHeap_Drain()
    {
        var heap = new RadixHeap<int>();
        for (int i = 0; i < priorities.Length; i++)
            heap.Enqueue(i, priorities[i]);

        ulong acc = 0;
        while (heap.TryDequeue(out int element, out ulong priority))
            acc += priority ^ (ulong)element;

        return acc;
    }

    // ---- Hold: steady-state dequeue-min / re-enqueue-later at constant size ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Hold")]
    public ulong PriorityQueue_Hold()
    {
        var queue = new PriorityQueue<int, ulong>();
        for (int i = 0; i < priorities.Length; i++)
            queue.Enqueue(i, priorities[i]);

        ulong acc = 0;
        for (int i = 0; i < increments.Length; i++)
        {
            queue.TryDequeue(out int element, out ulong priority);
            acc += priority;
            queue.Enqueue(element, priority + increments[i]);
        }

        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Hold")]
    public ulong RadixHeap_Hold()
    {
        var heap = new RadixHeap<int>();
        for (int i = 0; i < priorities.Length; i++)
            heap.Enqueue(i, priorities[i]);

        ulong acc = 0;
        for (int i = 0; i < increments.Length; i++)
        {
            heap.TryDequeue(out int element, out ulong priority);
            acc += priority;
            heap.Enqueue(element, priority + increments[i]);
        }

        return acc;
    }

    // ---- Dijkstra: lazy-deletion single-source shortest path from vertex 0 ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Dijkstra")]
    public ulong PriorityQueue_Dijkstra()
    {
        Array.Fill(dist, ulong.MaxValue);
        var queue = new PriorityQueue<int, ulong>();
        dist[0] = 0;
        queue.Enqueue(0, 0);

        while (queue.TryDequeue(out int v, out ulong d))
        {
            if (d != dist[v])
                continue;

            for (int e = edgeStart[v]; e < edgeStart[v + 1]; e++)
            {
                int w = edgeTargets[e];
                ulong candidate = d + edgeWeights[e];
                if (candidate < dist[w])
                {
                    dist[w] = candidate;
                    queue.Enqueue(w, candidate);
                }
            }
        }

        return SumDistances();
    }

    [Benchmark]
    [BenchmarkCategory("Dijkstra")]
    public ulong RadixHeap_Dijkstra()
    {
        Array.Fill(dist, ulong.MaxValue);
        var heap = new RadixHeap<int>();
        dist[0] = 0;
        heap.Enqueue(0, 0);

        while (heap.TryDequeue(out int v, out ulong d))
        {
            if (d != dist[v])
                continue;

            for (int e = edgeStart[v]; e < edgeStart[v + 1]; e++)
            {
                int w = edgeTargets[e];
                ulong candidate = d + edgeWeights[e];
                if (candidate < dist[w])
                {
                    dist[w] = candidate;
                    heap.Enqueue(w, candidate);
                }
            }
        }

        return SumDistances();
    }

    private ulong SumDistances()
    {
        ulong sum = 0;
        for (int i = 0; i < dist.Length; i++)
            sum += dist[i];

        return sum;
    }
}
