using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Statistics;

// DDSketch vs the exact quantile a caller computes instead. The BCL ships no quantile type at all — no
// percentile, no median, nothing in System.Linq beyond Average — so the honest baseline is what everyone
// writes: retain every sample in a List<double>, sort it, and index. The baseline arms are named List_* /
// Array_* so the dashboard classifies them as the reference series.
//
// Four ops, because the answer genuinely depends on which one a caller is doing, and quoting only the
// flattering one would be dishonest:
//
//   Add            — appending a double to a List is a bounds check and a store; the sketch pays a log()
//                    and a ceil() on top of its own store. The sketch LOSES this op, and should.
//   Query          — the streaming case: values have arrived since the last query, so the list has to be
//                    re-sorted before it can be indexed. This is where the sketch wins, and the margin
//                    grows with the sample count because the baseline is O(n log n) against a bucket walk
//                    that depends only on the value range.
//   QueryPresorted — the static case, measured rather than waved away: if nothing has changed since the
//                    last query, the list is sorted once and every quantile after that is an array index.
//                    Nothing beats that, and the sketch LOSES by a wide margin. It is charted so the
//                    "just sort it" alternative is a number here rather than a footnote.
//   Mixed          — ingest the whole stream, then read p50 / p90 / p99. The end-to-end shape, and the one
//                    the type is actually for.
//
// The memory column is the other half of the story and the reason the type exists: the baseline's footprint
// is 8 bytes per sample and unbounded, while the sketch's is a few hundred buckets whatever the sample count
// — the difference between summarizing a million latencies and summarizing a stream that never ends.
//
// The generated values are lognormal, which is what request latencies actually look like: a dense body and a
// long right tail. That shape matters for the sketch, because the bucket count tracks the log of the value
// range rather than the sample count, and a heavy tail is exactly where a fixed-width histogram would need
// either far more buckets or a worse answer.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class DDSketchBenchmark
{
    private const double Accuracy = 0.01d;

    private double[] values = null!;
    private double[] presorted = null!;
    private double[] scratch = null!;
    private DDSketch sketch = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        values = new double[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            // Box-Muller into a lognormal: a dense body around 10 ms with a long right tail.
            double u1 = 1d - rand.NextDouble();
            double u2 = rand.NextDouble();
            double gaussian = Math.Sqrt(-2d * Math.Log(u1)) * Math.Sin(2d * Math.PI * u2);
            values[i] = 10d * Math.Exp(gaussian);
        }

        presorted = (double[])values.Clone();
        Array.Sort(presorted);

        scratch = new double[ItemCount];

        sketch = new DDSketch(Accuracy);
        sketch.Add(values);
    }

    [BenchmarkCategory("Add")]
    [Benchmark(Baseline = true)]
    public int List_Add()
    {
        var list = new List<double>();
        foreach (double value in values)
        {
            list.Add(value);
        }

        return list.Count;
    }

    [BenchmarkCategory("Add")]
    [Benchmark]
    public long DDSketch_Add()
    {
        var target = new DDSketch(Accuracy);
        foreach (double value in values)
        {
            target.Add(value);
        }

        return target.Count;
    }

    [BenchmarkCategory("Query")]
    [Benchmark(Baseline = true)]
    public double List_Query()
    {
        // The stream has moved since the last query, so the retained values have to be ordered again
        // before any of them can be indexed. Sorting a scratch copy rather than the source is what a
        // caller does who still needs the values afterwards.
        values.CopyTo(scratch, 0);
        Array.Sort(scratch);
        return scratch[(int)(0.99d * (scratch.Length - 1))];
    }

    [BenchmarkCategory("Query")]
    [Benchmark]
    public double DDSketch_Query() => sketch.GetQuantile(0.99d);

    [BenchmarkCategory("QueryPresorted")]
    [Benchmark(Baseline = true)]
    public double Array_QueryPresorted() => presorted[(int)(0.99d * (presorted.Length - 1))];

    [BenchmarkCategory("QueryPresorted")]
    [Benchmark]
    public double DDSketch_QueryPresorted() => sketch.GetQuantile(0.99d);

    [BenchmarkCategory("Mixed")]
    [Benchmark(Baseline = true)]
    public double List_Mixed()
    {
        var list = new List<double>();
        foreach (double value in values)
        {
            list.Add(value);
        }

        double[] ordered = [.. list];
        Array.Sort(ordered);

        return ordered[(int)(0.5d * (ordered.Length - 1))]
            + ordered[(int)(0.9d * (ordered.Length - 1))]
            + ordered[(int)(0.99d * (ordered.Length - 1))];
    }

    [BenchmarkCategory("Mixed")]
    [Benchmark]
    public double DDSketch_Mixed()
    {
        var target = new DDSketch(Accuracy);
        foreach (double value in values)
        {
            target.Add(value);
        }

        return target.GetQuantile(0.5d) + target.GetQuantile(0.9d) + target.GetQuantile(0.99d);
    }
}
