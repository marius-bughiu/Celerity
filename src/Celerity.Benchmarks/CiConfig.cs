using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;

public class CiConfig : ManualConfig
{
    public CiConfig()
    {
        // The regression gate runs this whole suite TWICE on one runner (PR head +
        // main base, back-to-back, so hardware variance cancels). The old
        // Job.Default.WithLaunchCount(2) ran the full BenchmarkDotNet warmup +
        // iteration schedule over two process launches across ~300 cases, so a single
        // pass took ~3h and the PR A/B blew past GitHub's 6h job ceiling and was
        // cancelled on every PR — the gate never produced a result (#217). A
        // ShortRun-derived schedule (1 launch, 3 warmup, 5 measured iterations) cuts
        // per-case cost ~6-10x; the slightly higher run-to-run variance is absorbed by
        // the workflow's std-dev-aware thresholding (a row only flags when the delta
        // also exceeds the combined std-dev of both measurements), and the same-runner
        // A/B already removes the dominant variance source (the runner hardware).
        // Heavier, noisier sweeps stay in the on-demand extended suite (ExtendedCiConfig).
        AddJob(Job.ShortRun
            .WithWarmupCount(3)
            .WithIterationCount(5));

        AddExporter(JsonExporter.Full);
        AddLogger(ConsoleLogger.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);

        WithSummaryStyle(SummaryStyle.Default.WithRatioStyle(RatioStyle.Trend));
    }
}
