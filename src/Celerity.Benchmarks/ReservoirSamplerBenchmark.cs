using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Statistics;

// ReservoirSampler<T> vs the sample a caller takes instead. The BCL has no sampler, so there are two things
// people write, and the honest baseline is the better one.
//
// The naive version is `stream.OrderBy(_ => rand.Next()).Take(k)`, which sorts the whole sequence to keep k
// of it. Measuring against that would flatter the sampler for no reason, so the baseline here is the version
// a careful developer writes: materialize the stream into a List<T> and run a partial Fisher-Yates over the
// first k positions — O(n) time, one array of n, and exactly uniform. That is a good algorithm. Its problem
// is not its asymptotics, it is that it has to hold the stream.
//
// Which is the point of the comparison. The sampler's win is that its footprint is k, not n: the memory
// column shows a fixed allocation against one that grows with the stream, and Algorithm L keeps the time
// competitive by drawing a geometric skip rather than a random number per item — O(k log(n / k)) draws over
// the whole stream instead of n.
//
// Two ops:
//
//   Sample     — take a k-item uniform sample of the whole stream, end to end.
//   Accumulate — feed the stream in without asking for the sample. The baseline is the List<T> append the
//                two-pass approach cannot avoid; the sampler's cost here is the skip arithmetic alone,
//                which is what makes it usable on a stream with no end.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ReservoirSamplerBenchmark
{
    private const int SampleSize = 100;

    private int[] stream = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        stream = new int[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            stream[i] = i;
        }
    }

    [BenchmarkCategory("Sample")]
    [Benchmark(Baseline = true)]
    public int List_Sample()
    {
        var retained = new List<int>(ItemCount);
        foreach (int item in stream)
        {
            retained.Add(item);
        }

        // Partial Fisher-Yates: uniform, O(n) once the stream is held, and the best a caller can do
        // without giving up on holding it.
        var rand = new Random(42);
        int take = Math.Min(SampleSize, retained.Count);
        int total = 0;
        for (int i = 0; i < take; i++)
        {
            int j = rand.Next(i, retained.Count);
            (retained[i], retained[j]) = (retained[j], retained[i]);
            total += retained[i];
        }

        return total;
    }

    [BenchmarkCategory("Sample")]
    [Benchmark]
    public int ReservoirSampler_Sample()
    {
        var sampler = new ReservoirSampler<int>(SampleSize, 42UL);
        foreach (int item in stream)
        {
            sampler.Add(item);
        }

        int total = 0;
        foreach (int item in sampler.Sample)
        {
            total += item;
        }

        return total;
    }

    [BenchmarkCategory("Accumulate")]
    [Benchmark(Baseline = true)]
    public int List_Accumulate()
    {
        var retained = new List<int>();
        foreach (int item in stream)
        {
            retained.Add(item);
        }

        return retained.Count;
    }

    [BenchmarkCategory("Accumulate")]
    [Benchmark]
    public long ReservoirSampler_Accumulate()
    {
        var sampler = new ReservoirSampler<int>(SampleSize, 42UL);
        foreach (int item in stream)
        {
            sampler.Add(item);
        }

        return sampler.TotalSeen;
    }
}
