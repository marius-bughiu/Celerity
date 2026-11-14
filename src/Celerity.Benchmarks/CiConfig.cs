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
        // the accurate, low-variance measurement the published time series needs. One
        // pass over the core suite takes far longer than GitHub's 6h job ceiling (#217
        // first hit it, when every PR measured its head and a main base back-to-back on
        // one runner). Rather than trade away accuracy, benchmarks.yml shards the core
        // suite across a parallel matrix (see Program.cs `--shard`) and runs it on merges
        // to main, so each runner measures only a fraction of the suite and the wall time
        // drops to one pass / shardCount — at full accuracy. The shared factory extends
        // warmup only for single-invocation jobs, whose tiny iterations can otherwise
        // finish before tier-up; preserve the launch count and scale the matrix if the
        // suite grows.
        AddJob(Job.Default
            .WithEngineFactory(new TieredPgoWarmupFactory())
            .WithLaunchCount(2));

        AddExporter(JsonExporter.Full);
        AddLogger(ConsoleLogger.Default);
        AddColumnProvider(DefaultColumnProviders.Instance);

        WithSummaryStyle(SummaryStyle.Default.WithRatioStyle(RatioStyle.Trend));
    }
}
