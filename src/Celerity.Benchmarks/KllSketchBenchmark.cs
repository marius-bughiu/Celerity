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
    private List<double> sortedList = null!;
    private int listCursor;
    private int pendingIndex;
    private KllSketch<double> sketch = null!;
    private int cursor;

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

        sortedList = new List<double>(presorted);
        listCursor = 0;
        pendingIndex = -1;

        sketch = new KllSketch<double>();
        sketch.Add(values);
        cursor = 0;
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

    [BenchmarkCategory("Query")]
    [Benchmark(Baseline = true)]
    public double List_Query()
    {
        // The careful caller's moving-stream query: the list is kept sorted, so a new value is one
        // BinarySearch and one Insert (a memmove of the tail), and the p99 is an index. Re-sorting a copy
        // per query would be far slower and would flatter the sketch. To keep the list at a steady size
        // without charging a second memmove, invocations alternate: one inserts the next value, the next
        // removes it again from the same position. Either way an invocation is one memmove of a
        // random-length tail plus an index.
        if (pendingIndex >= 0)
        {
            sortedList.RemoveAt(pendingIndex);
            pendingIndex = -1;
        }
        else
        {
            double value = values[listCursor];
            listCursor = listCursor + 1 == values.Length ? 0 : listCursor + 1;
            int at = sortedList.BinarySearch(value);
            pendingIndex = at < 0 ? ~at : at;
            sortedList.Insert(pendingIndex, value);
        }

        return sortedList[(int)(0.99d * (sortedList.Count - 1))];
    }

    [BenchmarkCategory("Query")]
    [Benchmark]
    public double KllSketch_Query()
    {
        // One value arrives, which invalidates the cached view, then the p99 is read: the sketch's
        // counterpart of the baseline's re-sort. The cursor cycles through the same values so every
        // invocation does the same work.
        sketch.Add(values[cursor]);
        cursor = cursor + 1 == values.Length ? 0 : cursor + 1;
        return sketch.GetQuantile(0.99d);
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
