using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

internal class Program
{
    static void Main(string[] args)
    {
        var benchmarkTypes = new[]
        {
            typeof(CelerityDictionaryBenchmark),
            typeof(IntDictionaryBenchmark),
            typeof(LongDictionaryBenchmark),
            typeof(CeleritySetBenchmark),
            typeof(IntSetBenchmark),
            typeof(LongSetBenchmark),
            typeof(StringHasherBenchmark),
            typeof(IntegerHasherBenchmark),
        };

        if (args.Contains("--hash-quality"))
        {
            // Offline distribution-quality report (deterministic; no BenchmarkDotNet run).
            // The throughput companion to this is the hasher benchmarks above.
            HashQualityReportRunner.Run();
            return;
        }

        bool ci = args.Contains("--ci");

        if (ci)
        {
            // CI mode: run every benchmark and emit a single joined JSON report
            // that github-action-benchmark can consume (BenchmarkRun-joined-report-full.json).
            new BenchmarkSwitcher(benchmarkTypes).RunAllJoined(new CiConfig());
        }
        else
        {
            // Local mode: forward args to the switcher (interactive prompt if none given).
            new BenchmarkSwitcher(benchmarkTypes).Run(args);
        }
    }
}
