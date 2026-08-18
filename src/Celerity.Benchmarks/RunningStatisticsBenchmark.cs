using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Statistics;

// RunningStatistics vs the two things the BCL leaves a caller to write. System.Linq has Average and Sum and
// no variance, no standard deviation and no higher moment, so there is no drop-in counterpart to compare
// against — the baselines are the idioms that fill the gap. They are named Linq_* / List_* so the dashboard
// classifies them as the reference series.
//
// This is deliberately not a benchmark the Celerity type is expected to sweep, and the class remarks say so
// rather than letting the chart imply it:
//
//   Moments    — mean and variance over a span that is already in memory. The LINQ baseline is the two-pass
//                shape everyone writes: Average, then Sum of squared deviations. Welford reads the data once
//                and allocates nothing, and it still LOSES: CI measures 909 us against 469 us at 100k, a
//                consistent 1.9x, because this accumulator maintains all four moments on every Add while the
//                caller here reads two. Two tight LINQ passes beat one pass doing twice the arithmetic. That
//                is the honest shape of this type — it buys correctness and streaming, not speed.
//   Accumulate — the streaming shape: values arrive one at a time and there is nothing to enumerate twice.
//                The baseline is the List<double> a two-pass computation must build first. It loses here too
//                on time (650 us against 368 us at 100k), for the same reason; the memory column is the real
//                result — 1 KB against 2 MB at 100k, and flat as the stream grows.
//
// What no arm can show is the reason to prefer Welford, because it is not a speed property. The one-pass
// shortcut a developer reaches for instead — accumulate sum and sumOfSquares, subtract — is faster than
// both arms here and catastrophically wrong when the mean is large relative to the spread: at 1e10 ± 6, the
// two terms agree to fifteen digits and the answer is assembled entirely out of rounding error. That is
// covered by a test, not a benchmark, because "returns a negative variance" is not a number this chart can
// draw. The honest summary: this type is the slowest of the three ways to compute a variance and the only one
// that always gets it right.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RunningStatisticsBenchmark
{
    private double[] values = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        values = new double[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            values[i] = 100d + (rand.NextDouble() * 50d);
        }
    }

    [BenchmarkCategory("Moments")]
    [Benchmark(Baseline = true)]
    public double Linq_Moments()
    {
        double mean = values.Average();
        double variance = values.Sum(v => (v - mean) * (v - mean)) / (values.Length - 1);
        return mean + variance;
    }

    [BenchmarkCategory("Moments")]
    [Benchmark]
    public double RunningStatistics_Moments()
    {
        var stats = new RunningStatistics(values);
        return stats.Mean + stats.Variance;
    }

    [BenchmarkCategory("Accumulate")]
    [Benchmark(Baseline = true)]
    public int List_Accumulate()
    {
        // What a two-pass computation has to do first when the values arrive one at a time.
        var retained = new List<double>();
        foreach (double value in values)
        {
            retained.Add(value);
        }

        return retained.Count;
    }

    [BenchmarkCategory("Accumulate")]
    [Benchmark]
    public long RunningStatistics_Accumulate()
    {
        var stats = new RunningStatistics();
        foreach (double value in values)
        {
            stats.Add(value);
        }

        return stats.Count;
    }
}
