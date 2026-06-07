using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

internal class Program
{
    // ── CI-tracked core suite ───────────────────────────────────────────────────
    // The lean, fast, low-variance set run on every PR by the `--ci` path and pushed
    // to gh-pages for continuous regression tracking. Keep this list tight: every
    // entry is benchmarked twice (PR + base) on the same runner, so additions here
    // directly lengthen CI and widen the A/B comparison surface.
    private static readonly Type[] CoreBenchmarks =
    {
        typeof(CelerityDictionaryBenchmark),
        typeof(RobinHoodDictionaryBenchmark),
        typeof(SwissDictionaryBenchmark),
        typeof(HashCachingDictionaryBenchmark),
        typeof(PooledCelerityDictionaryBenchmark),
        typeof(IntDictionaryBenchmark),
        typeof(LongDictionaryBenchmark),
        typeof(FrozenCelerityDictionaryBenchmark),
        typeof(CelerityMultiMapBenchmark),
        typeof(SmallDictionaryBenchmark),
        typeof(CeleritySetBenchmark),
        typeof(FrozenCeleritySetBenchmark),
        typeof(IntSetBenchmark),
        typeof(LongSetBenchmark),
        typeof(BloomFilterBenchmark),
        typeof(StringHasherBenchmark),
        typeof(IntegerHasherBenchmark),
    };

    // ── Extended suite (local / on-demand) ──────────────────────────────────────
    // Heavier, more exploratory benchmarks: multi-distribution sweeps, million-item
    // scale tests, concurrency fan-out, allocation profiling, and mixed workloads.
    // These are too slow and/or too noisy for the per-PR regression gate, so they are
    // NOT part of the `--ci` joined run. Run them locally via the interactive switcher
    // or a filter, e.g.:
    //   dotnet run -c Release -- --filter '*LargeDataset*'
    //   dotnet run -c Release -- --filter '*Distribution*'
    private static readonly Type[] ExtendedBenchmarks =
    {
        typeof(DistributionBenchmark),
        typeof(AdversarialHasherBenchmark),
        typeof(LargeDatasetBenchmark),
        typeof(MemoryAllocationBenchmark),
        typeof(ConcurrentAccessBenchmark),
        typeof(CacheLocalityBenchmark),
        typeof(LibraryComparisonBenchmark),
        typeof(RealWorldWorkloadBenchmark),
    };

    static void Main(string[] args)
    {
        if (args.Contains("--hash-quality"))
        {
            // Offline distribution-quality report (deterministic; no BenchmarkDotNet run).
            // The throughput companion to this is the hasher benchmarks above.
            HashQualityReportRunner.Run();
            return;
        }

        if (args.Contains("--ci-extended"))
        {
            // Weekly extended mode: run the heavy extended suite and emit a joined
            // JSON report for github-action-benchmark, published to the separate
            // dev/bench-extended dashboard. Driven by benchmarks-extended.yml on a
            // cron schedule — intentionally NOT part of the per-PR --ci gate.
            new BenchmarkSwitcher(ExtendedBenchmarks).RunAllJoined(new ExtendedCiConfig());
        }
        else if (args.Contains("--ci"))
        {
            // CI mode: run the core suite and emit a single joined JSON report
            // that github-action-benchmark can consume (BenchmarkRun-joined-report-full.json).
            new BenchmarkSwitcher(CoreBenchmarks).RunAllJoined(new CiConfig());
        }
        else
        {
            // Local mode: expose every benchmark (core + extended) to the switcher,
            // forwarding args (interactive prompt if none given).
            var all = CoreBenchmarks.Concat(ExtendedBenchmarks).ToArray();
            new BenchmarkSwitcher(all).Run(args);
        }
    }
}
