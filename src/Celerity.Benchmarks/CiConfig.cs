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
        // Full BenchmarkDotNet warmup + iteration schedule over two process launches —
        // the accurate, low-variance measurement the regression gate needs. The cost of
        // running this whole suite (~300 cases) is ~3h per pass, and the PR path runs it
        // TWICE on one runner (PR head + main base, back-to-back, so hardware variance
        // cancels). That serial 2x exceeded GitHub's 6h job ceiling and was cancelled on
        // every PR (#217). Rather than trade away accuracy, benchmarks.yml now shards the
        // core suite across a parallel matrix (see Program.cs `--shard`), so each runner
        // measures only a fraction of the suite (head + base) and the wall time drops to
        // ~3h / shardCount — at full accuracy. Keep this job schedule as-is; scale the
        // matrix in benchmarks.yml if the suite grows.
        AddJob(Job.Default
            .WithLaunchCount(2));

        AddExporter(JsonExporter.Full);
        AddLogger(ConsoleLogger.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);

        WithSummaryStyle(SummaryStyle.Default.WithRatioStyle(RatioStyle.Trend));
    }
}
