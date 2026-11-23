using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Statistics;

// WeightedReservoirSampler<T> vs the weighted sample a caller takes instead. The BCL has no sampler at all,
// weighted or not, so the baseline is the hand-roll a careful developer writes from the textbook: Efraimidis
// and Spirakis' A-Res.
//
// A-Res gives every item the key U^(1/w) and keeps the k largest. The careful version works in log space,
// ln(U) / w, so a large weight does not round the key to exactly 1, draws U strictly inside (0, 1) so the log
// is finite, and keeps the k largest keys in a PriorityQueue<int, double> min-heap: peek at the smallest
// retained key and DequeueEnqueue only when the new key beats it. That is exact, one pass, and k memory —
// the same footprint as the sampler. It is not a strawman.
//
// Its cost is per item: every item, retained or not, pays a random draw and a logarithm, and a heap
// operation when it gets in. The sampler is the same paper's A-ExpJ, which draws how much weight must pass
// before the next item can enter, so an item it skips costs one subtraction and one comparison. The gap is
// the price of a draw and a log per item, and it widens with the stream: with the comparable weights
// used here about k ln(n / k) items enter, against n draws for the hand-roll. (Weights that keep growing
// along the stream would make nearly every item enter, and erase the gap; that is a property of the
// workload, documented with the type.)
//
// Two ops:
//
//   Sample — per-item Add over the whole stream, then read the sample.
//   AddSpan — the batch overload, which runs the countdown in locals. The baseline is the same hand-roll
//             loop: the BCL offers no batch form to compare it with.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class WeightedReservoirSamplerBenchmark
{
    private const int SampleSize = 100;

    private int[] stream = null!;
    private double[] weights = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(17);
        stream = new int[ItemCount];
        weights = new double[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            stream[i] = i;
            weights[i] = 1 + rand.NextDouble() * 99;
        }
    }

    [BenchmarkCategory("Sample")]
    [Benchmark(Baseline = true)]
    // A-Res in log space: key ln(U) / w, the k largest kept in a min-heap so the threshold is its root. The
    // loop is written out in each arm rather than shared through a helper, so the JIT inlines neither side
    // differently from the other.
    public int PriorityQueue_Sample()
    {
        var rand = new Random(42);
        var heap = new PriorityQueue<int, double>(SampleSize);
        int[] items = stream;
        double[] w = weights;

        for (int i = 0; i < items.Length; i++)
        {
            double u;
            do
            {
                u = rand.NextDouble();
            }
            while (u == 0d);

            double key = Math.Log(u) / w[i];

            if (heap.Count < SampleSize)
            {
                heap.Enqueue(items[i], key);
            }
            else if (heap.TryPeek(out _, out double min) && key > min)
            {
                heap.DequeueEnqueue(items[i], key);
            }
        }

        int total = 0;
        foreach ((int item, double _) in heap.UnorderedItems)
        {
            total += item;
        }

        return total;
    }

    [BenchmarkCategory("Sample")]
    [Benchmark]
    public int WeightedReservoirSampler_Sample()
    {
        var sampler = new WeightedReservoirSampler<int>(SampleSize, 42UL);
        int[] items = stream;
        double[] w = weights;
        for (int i = 0; i < items.Length; i++)
        {
            sampler.Add(items[i], w[i]);
        }

        int total = 0;
        foreach (int item in sampler.Sample)
        {
            total += item;
        }

        return total;
    }

    [BenchmarkCategory("AddSpan")]
    [Benchmark(Baseline = true)]
    public int PriorityQueue_AddSpan()
    {
        var rand = new Random(42);
        var heap = new PriorityQueue<int, double>(SampleSize);
        int[] items = stream;
        double[] w = weights;

        for (int i = 0; i < items.Length; i++)
        {
            double u;
            do
            {
                u = rand.NextDouble();
            }
            while (u == 0d);

            double key = Math.Log(u) / w[i];

            if (heap.Count < SampleSize)
            {
                heap.Enqueue(items[i], key);
            }
            else if (heap.TryPeek(out _, out double min) && key > min)
            {
                heap.DequeueEnqueue(items[i], key);
            }
        }

        int total = 0;
        foreach ((int item, double _) in heap.UnorderedItems)
        {
            total += item;
        }

        return total;
    }

    [BenchmarkCategory("AddSpan")]
    [Benchmark]
    public int WeightedReservoirSampler_AddSpan()
    {
        var sampler = new WeightedReservoirSampler<int>(SampleSize, 42UL);
        sampler.Add(stream, weights);

        int total = 0;
        foreach (int item in sampler.Sample)
        {
            total += item;
        }

        return total;
    }
}
