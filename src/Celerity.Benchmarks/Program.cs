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
