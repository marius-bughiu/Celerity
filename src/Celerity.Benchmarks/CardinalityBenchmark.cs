using BenchmarkDotNet.Attributes;
using Celerity.Cardinality;

/// <summary>
/// Approximate <c>COUNT(DISTINCT)</c> with <see cref="StringDistinct"/> against the exact
/// <see cref="HashSet{T}"/> everyone reaches for. <see cref="HashSet{T}"/> stores every distinct
/// element, so its memory grows without bound with the cardinality (a few MB at 100k, tens of MB at 1M,
/// and eventually OOM); <c>Distinct</c> is exact for small inputs and then promotes to a fixed
/// HyperLogLog of tens of KB, so a billion distinct values still cost the same tens of KB — the
/// run-vs-cannot-run difference the <c>Allocated</c> column makes plain (an alloc ratio around 0.002 at
/// 1M). The trade is CPU for that bounded memory: the estimate carries a small relative error and each
/// <c>Add</c> feeds the estimator through the generic hasher, so <c>Distinct</c> is the tool when the
/// exact set would exhaust memory — not when it comfortably fits. Isolated, memory-heavy baseline, so it
/// lives in the extended suite.
/// </summary>
[MemoryDiagnoser]
public class CardinalityBenchmark
{
    [Params(100_000, 1_000_000)]
    public int Cardinality;

    private string[] stream = null!;

    [GlobalSetup]
    public void Setup()
    {
        stream = new string[Cardinality];
        for (int i = 0; i < Cardinality; i++)
            stream[i] = $"user-{i:x}";
    }

    [Benchmark(Baseline = true)]
    public int HashSet_DistinctCount()
    {
        var set = new HashSet<string>();
        foreach (var s in stream)
            set.Add(s);
        return set.Count;
    }

    [Benchmark]
    public long Distinct_DistinctCount()
    {
        var distinct = new StringDistinct();
        foreach (var s in stream)
            distinct.Add(s);
        return distinct.Count();
    }
}
