using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Statistics;

// KllSketch<T> vs the exact quantile a caller computes instead. The BCL has no quantile type, so the honest
// baseline is the same one DDSketchBenchmark uses: keep every value in a List<double>, sort it and index. The
// baseline arms are named List_* / Array_* so the dashboard classifies them as the reference series.
//
// The values are doubles so the two sketches' rows read side by side, but this type's reason to exist is the
// other half of the quantile question: it takes any IComparable<T>, and its error is on the rank rather than
// the value. The same four ops as DDSketch, because the answer depends on which one a caller is doing:
//
//   Add            — appending to a List is a bounds check and a store; the sketch writes into its level-0
//                    buffer and, every K values or so, sorts and halves a level. The sketch LOSES this op.
//   Query          — the streaming case: a value has arrived since the last query. The baseline keeps its
//                    list sorted and inserts the value in place, an O(n) memmove; the sketch re-sorts only
//                    the ~3K values it retains. Which wins depends on n.
//   QueryPresorted — the static case: nothing has changed, so the list is sorted once and every quantile
//                    after that is an array index, and the sketch is a binary search over its cached view.
//                    Nothing beats the index, and the sketch LOSES. It is charted so "just sort it" is a
//                    number here rather than a footnote.
//   Mixed          — ingest the whole stream, then read p50 / p90 / p99. The end-to-end shape.
//
// The memory column is the other half of the story: the baseline holds every value, the sketch about 3K
// (around 600 at the default K = 200) however long the stream runs.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class KllSketchBenchmark
{
    private double[] values = null!;
    private double[] presorted = null!;
    private const int QuerySteps = 1_000;

    private List<double> sortedList = null!;
    private KllSketch<double> streaming = null!;
    private KllSketch<double> sketch = null!;

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


        sketch = new KllSketch<double>();
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
    public long KllSketch_Add()
    {
        var target = new KllSketch<double>();
        foreach (double value in values)
        {
            target.Add(value);
        }

        return target.Count;
    }

    // Both Query arms start every iteration from the same state, the ItemCount values ingested, and then
    // absorb the same QuerySteps further values, reading p99 after each. Rebuilding in IterationSetup keeps
    // the workload bounded and matched: without it the sketch's stream would grow by one value per
    // invocation for millions of invocations while the list stayed the same size. The batch is long enough
    // (milliseconds) that the one-invocation-per-iteration rule IterationSetup imposes costs no precision.
    [IterationSetup(Target = nameof(List_Query))]
    public void ResetList()
    {
        sortedList = new List<double>(ItemCount + QuerySteps);
        sortedList.AddRange(presorted);
    }

    [IterationSetup(Target = nameof(KllSketch_Query))]
    public void ResetSketch()
    {
        streaming = new KllSketch<double>();
        streaming.Add(values);
    }

    [BenchmarkCategory("Query")]
    [Benchmark(Baseline = true, OperationsPerInvoke = QuerySteps)]
    public double List_Query()
    {
        // The careful caller's moving-stream query: the list is kept sorted, so a new value is one
        // BinarySearch and one Insert (a memmove of the tail), and the p99 is an index. Re-sorting a copy
        // per query would be far slower and would flatter the sketch.
        double total = 0;
        for (int i = 0; i < QuerySteps; i++)
        {
            double value = values[i % values.Length];
            int at = sortedList.BinarySearch(value);
            sortedList.Insert(at < 0 ? ~at : at, value);
            total += sortedList[(int)(0.99d * (sortedList.Count - 1))];
        }

        return total;
    }

    [BenchmarkCategory("Query")]
    [Benchmark(OperationsPerInvoke = QuerySteps)]
    public double KllSketch_Query()
    {
        // The same values, each added and followed by a p99 read. Adds that compact force the next query
        // to re-sort the retained values; the others patch the cached view in place.
        double total = 0;
        for (int i = 0; i < QuerySteps; i++)
        {
            streaming.Add(values[i % values.Length]);
            total += streaming.GetQuantile(0.99d);
        }

        return total;
    }

    [BenchmarkCategory("QueryPresorted")]
    [Benchmark(Baseline = true)]
    public double Array_QueryPresorted() => presorted[(int)(0.99d * (presorted.Length - 1))];

    [BenchmarkCategory("QueryPresorted")]
    [Benchmark]
    public double KllSketch_QueryPresorted() => sketch.GetQuantile(0.99d);

    [BenchmarkCategory("Mixed")]
    [Benchmark(Baseline = true)]
    public double List_Mixed()
    {
        var list = new List<double>();
        foreach (double value in values)
        {
            list.Add(value);
        }

        // Sorted in place: copying to an array first would charge the baseline an allocation it does not need.
        list.Sort();

        return list[(int)(0.5d * (list.Count - 1))]
            + list[(int)(0.9d * (list.Count - 1))]
            + list[(int)(0.99d * (list.Count - 1))];
    }

    [BenchmarkCategory("Mixed")]
    [Benchmark]
    public double KllSketch_Mixed()
    {
        var target = new KllSketch<double>();
        foreach (double value in values)
        {
            target.Add(value);
        }

        return target.GetQuantile(0.5d) + target.GetQuantile(0.9d) + target.GetQuantile(0.99d);
    }
}
