using System.Diagnostics;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;

/// <summary>
/// Prepares single-invocation benchmarks without disabling tiered compilation or PGO.
/// </summary>
/// <remarks>
/// Iteration setup/cleanup hooks pin InvocationCount and UnrollFactor to one, so
/// BenchmarkDotNet's usual warmups can finish before background tier-up completes.
/// Those jobs need elapsed-time preparation and additional normal warmups after
/// overhead measurement. Other throughput jobs keep the default schedule.
/// </remarks>
public sealed class TieredPgoWarmupFactory : IEngineFactory
{
    private const int MinimumPreparationInvocations = 32;
    private const int MinimumWarmupIterations = 64;
    private static readonly TimeSpan MinimumPreparationDuration = TimeSpan.FromSeconds(2);

    public IEngine CreateReadyToRun(EngineParameters parameters)
    {
        // InProcessEmit wraps iteration hooks in generated methods without their
        // attributes, so select using the characteristics BDN assigns to those jobs.
        // Explicit single-invocation jobs benefit from the same preparation.
        bool runsOnce = parameters.HasInvocationCount
            && parameters.TargetJob.Run.InvocationCount == 1
            && parameters.UnrollFactor == 1;
        var strategy = parameters.TargetJob.ResolveValue(
            RunMode.RunStrategyCharacteristic, EngineParameters.DefaultResolver);
        if (!runsOnce || strategy == RunStrategy.ColdStart)
            return new EngineFactory().CreateReadyToRun(parameters);

        int minimumWarmups = Math.Max(MinimumWarmupIterations, parameters.TargetJob.ResolveValue(
            RunMode.MinWarmupIterationCountCharacteristic, EngineParameters.DefaultResolver));
        int maximumWarmups = Math.Max(minimumWarmups, parameters.TargetJob.ResolveValue(
            RunMode.MaxWarmupIterationCountCharacteristic, EngineParameters.DefaultResolver));
        // Clone the frozen job. An explicit WarmupCount still takes precedence over
        // these bounds, so command-line short runs retain their requested schedule.
        parameters.TargetJob = parameters.TargetJob
            .WithMinWarmupCount(minimumWarmups)
            .WithMaxWarmupCount(maximumWarmups);

        // The stock factory performs global setup and initial jitting. The stock
        // engine retains overhead measurement, GC policy and result collection.
        var engine = new EngineFactory().CreateReadyToRun(parameters);
        try
        {
            int invocations = 0;
            var elapsed = Stopwatch.StartNew();
            do
            {
                parameters.IterationSetupAction?.Invoke();
                try
                {
                    // The selected job has UnrollFactor=1; use the same generated
                    // workload delegate as the measured iterations, including DCE.
                    engine.WorkloadAction(1);
                }
                finally
                {
                    parameters.IterationCleanupAction?.Invoke();
                }

                invocations++;
            }
            while (invocations < MinimumPreparationInvocations || elapsed.Elapsed < MinimumPreparationDuration);

            elapsed.Stop();
            // Allow queued background tier-up to finish before the normal warmups.
            Thread.Sleep(100);
            string warmupPolicy = parameters.TargetJob.HasValue(RunMode.WarmupCountCharacteristic)
                ? $"explicit normal warmups={parameters.TargetJob.Run.WarmupCount}"
                : $"normal warmup floor={minimumWarmups}";
            engine.WriteLine(FormattableString.Invariant(
                $"// Tiered-PGO preparation: {invocations} invocations in {elapsed.Elapsed.TotalSeconds:F3} s; {warmupPolicy}."));

            // The next stock workload iteration runs its own setup. Do not leave an
            // extra, unpaired setup here: resource-owning fixtures need their cleanup.
            return engine;
        }
        catch
        {
            engine.Dispose();
            throw;
        }
    }
}
