using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;

/// <summary>
/// BenchmarkDotNet config for the weekly extended suite (<c>--ci-extended</c>).
/// </summary>
/// <remarks>
/// Mirrors <see cref="CiConfig"/> but with a single launch instead of two: the
/// extended benchmarks (million-item scale tests, O(n) adversarial runs,
/// thread fan-out, per-iteration workload rebuilds) are far heavier than the core
/// suite, and the weekly run is an exploratory trend, not the per-PR regression
/// gate, so it does not need the extra launch for variance reduction. The emitted
/// full JSON report is consumed by <c>github-action-benchmark</c> and published to
/// its own <c>dev/bench-extended</c> dashboard, separate from the core time series.
/// </remarks>
public class ExtendedCiConfig : ManualConfig
{
    public ExtendedCiConfig()
    {
        AddJob(Job.Default
            .WithLaunchCount(1));

        AddExporter(JsonExporter.Full);
        AddLogger(ConsoleLogger.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);

        WithSummaryStyle(SummaryStyle.Default.WithRatioStyle(RatioStyle.Trend));
    }
}
