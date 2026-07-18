using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// Deque<int> vs the BCL's only double-ended sequence, LinkedList<int>. Queue<T> (FIFO-only) and Stack<T>
// (LIFO-only) cannot serve as a both-ends baseline, so LinkedList is the honest comparison: it is the one
// BCL type offering O(1) at both ends, and it does so by heap-allocating a node per element and threading
// its order through pointers scattered across the managed heap. Deque keeps every element in one circular
// buffer, so its bounded churn allocates nothing after warm-up and its enumeration walks contiguous memory.
// The [MemoryDiagnoser] Allocated column is the headline on the Queue (bounded-FIFO-churn) category: a node
// per enqueue in the baseline versus zero for Deque once the buffer is warm. The baseline arms are named
// LinkedList_* so the dashboard classifies them as the BCL reference.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class DequeBenchmark
{
    private int[] items = null!;
    private int window;

    // Churn subjects, rebuilt per iteration by the [IterationSetup]s below.
    private Deque<int> deque = null!;
    private LinkedList<int> linked = null!;

    // Full, warm subjects for the non-destructive Enumerate category, built once in [GlobalSetup].
    private Deque<int> dequeFull = null!;
    private LinkedList<int> linkedFull = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        items = new int[ItemCount];
        var rand = new Random(42);
        for (int i = 0; i < ItemCount; i++)
            items[i] = rand.Next();

        window = Math.Min(1024, ItemCount);

        dequeFull = new Deque<int>(ItemCount);
        linkedFull = new LinkedList<int>();
        for (int i = 0; i < ItemCount; i++)
        {
            dequeFull.PushBack(items[i]);
            linkedFull.AddLast(items[i]);
        }
    }

    // ---- PushFront: build from empty at the front (the end List<T> cannot do in O(1)) --------------

    [IterationSetup(Target = nameof(LinkedList_PushFront))]
    public void ResetLinkedForPushFront() => linked = new LinkedList<int>();

    [IterationSetup(Target = nameof(Deque_PushFront))]
    public void ResetDequeForPushFront() => deque = new Deque<int>();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PushFront")]
    public void LinkedList_PushFront()
    {
        foreach (int k in items)
            linked.AddFirst(k);
    }

    [Benchmark]
    [BenchmarkCategory("PushFront")]
    public void Deque_PushFront()
    {
        foreach (int k in items)
            deque.PushFront(k);
    }

    // ---- Queue: bounded FIFO churn (enqueue back, dequeue front) over a warm window ----------------

    [IterationSetup(Target = nameof(LinkedList_Queue))]
    public void ResetLinkedForQueue()
    {
        linked = new LinkedList<int>();
        for (int i = 0; i < window; i++)
            linked.AddLast(i);
    }

    [IterationSetup(Target = nameof(Deque_Queue))]
    public void ResetDequeForQueue()
    {
        deque = new Deque<int>(window);
        for (int i = 0; i < window; i++)
            deque.PushBack(i);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Queue")]
    public void LinkedList_Queue()
    {
        foreach (int k in items)
        {
            linked.AddLast(k);
            linked.RemoveFirst();
        }
    }

    [Benchmark]
    [BenchmarkCategory("Queue")]
    public void Deque_Queue()
    {
        foreach (int k in items)
        {
            deque.PushBack(k);
            deque.PopFront();
        }
    }

    // ---- Enumerate: front-to-back walk of a full container (contiguous vs pointer-chased) ----------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public long LinkedList_Enumerate()
    {
        long acc = 0;
        foreach (int k in linkedFull)
            acc += k;
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public long Deque_Enumerate()
    {
        long acc = 0;
        foreach (int k in dequeFull)
            acc += k;
        return acc;
    }
}
