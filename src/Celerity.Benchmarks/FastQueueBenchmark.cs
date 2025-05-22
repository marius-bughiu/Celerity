using System.Linq;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Celerity.Collections;

[MemoryDiagnoser(false)]
public class FastQueueBenchmark
{
    private int[] _values;
    private Queue<int> _queue;
    private FastQueue<int> _fastQueue;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        _values = Enumerable.Range(0, ItemCount).ToArray();
        _queue = new Queue<int>(ItemCount);
        _fastQueue = new FastQueue<int>(ItemCount);
        foreach (var value in _values)
        {
            _queue.Enqueue(value);
            _fastQueue.Enqueue(value);
        }
    }

    [Benchmark]
    public void Queue_Enqueue()
    {
        var q = new Queue<int>();
        foreach (var value in _values)
            q.Enqueue(value);
    }

    [Benchmark]
    public void FastQueue_Enqueue()
    {
        var q = new FastQueue<int>();
        foreach (var value in _values)
            q.Enqueue(value);
    }

    [Benchmark]
    public void Queue_Dequeue()
    {
        while (_queue.Count > 0)
            _queue.Dequeue();
    }

    [Benchmark]
    public void FastQueue_Dequeue()
    {
        while (_fastQueue.Count > 0)
            _fastQueue.Dequeue();
    }
}
