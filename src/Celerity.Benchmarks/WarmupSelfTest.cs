using System.Diagnostics;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;

/// <summary>Exercises the warmup policy with the real BenchmarkDotNet engine.</summary>
internal static class WarmupSelfTest
{
    internal static void Run()
    {
        VerifyPreparedJob(explicitWarmupCount: null);
        VerifyPreparedJob(explicitWarmupCount: 2);
        VerifySkippedJob(RunStrategy.ColdStart, invocationCount: 1);
        VerifySkippedJob(RunStrategy.Monitoring, invocationCount: 2);
        VerifyFailureCleanup();
        Console.WriteLine("Warmup self-test passed.");
    }

    private static void VerifyPreparedJob(int? explicitWarmupCount)
    {
        var fixture = new Fixture();
        var host = new RecordingHost();
        // Throughput performs one stock jitting invocation for this explicit job.
        // Monitoring avoids that invocation and exercises an explicit short run.
        Job job = CreateJob(explicitWarmupCount.HasValue ? RunStrategy.Monitoring : RunStrategy.Throughput);
        if (explicitWarmupCount.HasValue)
            job = job.WithWarmupCount(explicitWarmupCount.Value);
        job.Freeze();

        EngineParameters parameters = CreateParameters(job, fixture, host);
        var preparation = Stopwatch.StartNew();
        using (IEngine engine = new TieredPgoWarmupFactory().CreateReadyToRun(parameters))
        {
            preparation.Stop();
            int stockJittingInvocations = explicitWarmupCount.HasValue ? 0 : 1;
            Check(fixture.Workloads - stockJittingInvocations >= 32, "Preparation ran fewer than 32 invocations.");
            Check(preparation.Elapsed >= TimeSpan.FromSeconds(2), "Preparation finished before two seconds elapsed.");
            Check(fixture.Setups == fixture.Workloads && fixture.Cleanups == fixture.Workloads,
                "Preparation left an unpaired setup or cleanup.");
            Check(!fixture.Active, "Preparation left an active fixture behind.");
            Check(!ReferenceEquals(engine.TargetJob, job), "The frozen selected job was not cloned.");
            Check(engine.TargetJob.ResolveValue(RunMode.MinWarmupIterationCountCharacteristic, engine.Resolver) >= 64,
                "The normal warmup floor was not applied.");
            Check(engine.TargetJob.ResolveValue(RunMode.MaxWarmupIterationCountCharacteristic, engine.Resolver) >= 64,
                "The normal warmup maximum is below its floor.");

            RunResults results = engine.Run();
            int warmups = results.EngineMeasurements.Count(measurement =>
                measurement.IterationMode == IterationMode.Workload && measurement.IterationStage == IterationStage.Warmup);
            Check(explicitWarmupCount.HasValue ? warmups == explicitWarmupCount.Value : warmups >= 64,
                "The stock engine did not honor the normal warmup policy.");
            Check(results.Workload.Count == 1, "Preparation changed the number of measured workload iterations.");
        }

        VerifyFinalLifecycle(fixture, host);
    }

    private static void VerifySkippedJob(RunStrategy strategy, int invocationCount)
    {
        var fixture = new Fixture();
        var host = new RecordingHost();
        Job job = CreateJob(strategy, invocationCount);
        job.Freeze();
        using (IEngine engine = new TieredPgoWarmupFactory().CreateReadyToRun(CreateParameters(job, fixture, host)))
        {
            Check(fixture.Workloads == 0 && fixture.Setups == 0 && fixture.Cleanups == 0,
                "A cold or multi-invocation job received preparation.");
            Check(ReferenceEquals(engine.TargetJob, job), "A skipped job's schedule was changed.");
        }

        VerifyFinalLifecycle(fixture, host);
    }

    private static void VerifyFailureCleanup()
    {
        var workloadFailure = new InvalidOperationException("Expected preparation workload failure.");
        var cleanupFailure = new InvalidOperationException("Expected global cleanup failure.");
        var fixture = new Fixture(workloadFailure, cleanupFailure);
        var host = new RecordingHost();
        Exception? caught = null;
        try
        {
            // Monitoring skips stock jitting so the failure occurs in preparation.
            using IEngine engine = new TieredPgoWarmupFactory().CreateReadyToRun(
                CreateParameters(CreateJob(RunStrategy.Monitoring), fixture, host));
        }
        catch (Exception exception)
        {
            caught = exception;
        }

        Check(ReferenceEquals(caught, workloadFailure), "Cleanup replaced or swallowed the workload failure.");
        Check(fixture.Setups == 1 && fixture.Workloads == 1 && fixture.Cleanups == 1 && !fixture.Active,
            "A failed preparation workload did not clean up its fixture.");
        Check(fixture.GlobalSetups == 1 && fixture.GlobalCleanups == 1, "The failed engine was not disposed exactly once.");
        // Engine.Dispose reports global-cleanup errors instead of rethrowing them.
        Check(host.Errors.Contains(cleanupFailure.Message), "The host did not receive the global-cleanup failure.");
    }

    private static Job CreateJob(RunStrategy strategy, int invocationCount = 1) => Job.Default
        .WithStrategy(strategy)
        .WithInvocationCount(invocationCount)
        .WithUnrollFactor(1)
        .WithIterationCount(1)
        .WithGcForce(false);

    private static EngineParameters CreateParameters(Job job, Fixture fixture, RecordingHost host) => new()
    {
        Host = host,
        TargetJob = job,
        BenchmarkName = nameof(WarmupSelfTest),
        GlobalSetupAction = fixture.GlobalSetup,
        GlobalCleanupAction = fixture.GlobalCleanup,
        // These lambdas deliberately have no IterationSetup attribute, like the
        // wrappers generated by InProcessEmit. Selection must use job settings.
        IterationSetupAction = () => fixture.Setup(),
        IterationCleanupAction = () => fixture.Cleanup(),
        WorkloadActionNoUnroll = fixture.Workload,
        WorkloadActionUnroll = fixture.Workload,
        OverheadActionNoUnroll = static _ => { },
        OverheadActionUnroll = static _ => { },
        Dummy1Action = static () => { },
        Dummy2Action = static () => { },
        Dummy3Action = static () => { }
    };

    private static void VerifyFinalLifecycle(Fixture fixture, RecordingHost host)
    {
        Check(fixture.GlobalSetups == 1 && fixture.GlobalCleanups == 1, "Global setup and cleanup were not paired.");
        Check(fixture.Setups == fixture.Workloads && fixture.Cleanups == fixture.Workloads && !fixture.Active,
            "The engine left an unpaired fixture after its run.");
        Check(host.Errors.Count == 0, "BenchmarkDotNet reported an unexpected error: " + string.Join("; ", host.Errors));
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Warmup self-test: " + message);
    }

    private sealed class Fixture(Exception? workloadFailure = null, Exception? cleanupFailure = null)
    {
        private int value;

        internal int GlobalSetups { get; private set; }
        internal int GlobalCleanups { get; private set; }
        internal int Setups { get; private set; }
        internal int Workloads { get; private set; }
        internal int Cleanups { get; private set; }
        internal bool Active { get; private set; }

        internal void GlobalSetup() => GlobalSetups++;

        internal void GlobalCleanup()
        {
            GlobalCleanups++;
            Check(!Active, "Global cleanup found an undisposed iteration fixture.");
            if (cleanupFailure is not null)
                throw cleanupFailure;
        }

        internal void Setup()
        {
            Check(!Active, "Setup overwrote an active fixture without cleanup.");
            Setups++;
            Active = true;
            value = 1;
        }

        internal void Workload(long invocationCount)
        {
            Check(invocationCount == 1, "The prepared workload did not run exactly once.");
            Check(Active && value == 1, "A destructive workload ran without a fresh fixture.");
            Workloads++;
            value = 0;
            if (workloadFailure is not null)
                throw workloadFailure;
        }

        internal void Cleanup()
        {
            Check(Active, "Cleanup ran without an active fixture.");
            Active = false;
            Cleanups++;
        }
    }

    private sealed class RecordingHost : IHost
    {
        internal List<string> Errors { get; } = new();
        public void Write(string message) { }
        public void WriteLine() { }
        public void WriteLine(string message) { }
        public void SendSignal(HostSignal hostSignal) { }
        public void SendError(string message) => Errors.Add(message);
        public void ReportResults(RunResults runResults) { }
        public void Dispose() { }
    }
}
