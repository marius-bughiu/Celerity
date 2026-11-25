using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Statistics;

// RunningCovariance vs the idioms the BCL leaves a caller to write. System.Linq has no covariance, no
// correlation and no regression, so there is no drop-in counterpart — the baselines are the code that fills
// the gap, named Linq_* / List_* so the dashboard classifies them as the reference series.
//
//   Fit        — the least-squares slope of y on x over two arrays already in memory. The LINQ baseline is the
//                two-pass shape: Average both, then Zip-and-Sum the cross deviations and Sum the squared x
//                deviations. RunningCovariance reads each pair once through its span overload.
//   Accumulate — the streaming shape: pairs arrive one at a time and there is nothing to enumerate twice. The
//                baseline is the List<(double, double)> a two-pass computation has to build first, and the
//                memory column is the point: it grows with the stream, the accumulator's does not.
//
// What no arm shows is the reason to prefer this over the fastest one-pass code a developer writes — four
// running sums and a subtraction — because that shortcut is wrong rather than slow: with x around 1e9 and a
// unit spread it reports a slope with the wrong sign. That is pinned by a test, not charted here.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RunningCovarianceBenchmark
{
    private double[] xs = null!;
    private double[] ys = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        xs = new double[ItemCount];
        ys = new double[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            double x = 10d + (rand.NextDouble() * 90d);
            xs[i] = x;
            ys[i] = (2.5d * x) + 40d + ((rand.NextDouble() - 0.5d) * 20d);
        }
    }

    [BenchmarkCategory("Fit")]
    [Benchmark(Baseline = true)]
    public double Linq_Fit()
    {
        double meanX = xs.Average();
        double meanY = ys.Average();
        double cross = xs.Zip(ys, (x, y) => (x - meanX) * (y - meanY)).Sum();
        double squares = xs.Sum(x => (x - meanX) * (x - meanX));
        return cross / squares;
    }

    [BenchmarkCategory("Fit")]
    [Benchmark]
    public double RunningCovariance_Fit()
    {
        var fit = new RunningCovariance(xs, ys);
        return fit.Slope;
    }

    [BenchmarkCategory("Accumulate")]
    [Benchmark(Baseline = true)]
    public int List_Accumulate()
    {
        // What a two-pass computation has to do first when the pairs arrive one at a time.
        double[] x = xs;
        double[] y = ys;
        var retained = new List<(double, double)>();
        for (int i = 0; i < x.Length; i++)
        {
            retained.Add((x[i], y[i]));
        }

        return retained.Count;
    }

    [BenchmarkCategory("Accumulate")]
    [Benchmark]
    public long RunningCovariance_Accumulate()
    {
        double[] x = xs;
        double[] y = ys;
        var fit = new RunningCovariance();
        for (int i = 0; i < x.Length; i++)
        {
            fit.Add(x[i], y[i]);
        }

        return fit.Count;
    }
}
