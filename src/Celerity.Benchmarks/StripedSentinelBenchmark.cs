using BenchmarkDotNet.Attributes;
using Celerity.Sentinel;

/// <summary>
/// The rollup half of <see cref="StringStripedAbuseTracker"/>: what one <c>Snapshot</c> costs a coordinator that
/// takes a report on an interval, against building a fresh <see cref="StringAbuseTracker"/> as the merge target —
/// the shape <c>Snapshot</c> had before it kept one to reuse.
/// </summary>
/// <remarks>
/// <see cref="SentinelBenchmark"/> measures a single tracker's observe-then-report pass and never touches the
/// striped type, so the rollup had no benchmark at all. The lanes are filled once in <c>[GlobalSetup]</c> and never
/// mutated, so every invocation merges identical state and only the rollup is timed. At the defaults a tracker is
/// ~2.3&#160;MB of sketches, most of it the Bloom filter sized for a million keys; the <c>Allocated</c> column is the
/// story — the fresh target pays it on every call, the reused one pays only for the report and the per-lane
/// offender lists the merge reads. Isolated showcase-package demo, so it rides the extended suite.
/// </remarks>
[MemoryDiagnoser]
public class StripedSentinelBenchmark
{
    [Params(4, 16)]
    public int LaneCount;

    private StringStripedAbuseTracker striped = null!;

    [GlobalSetup]
    public void Setup()
    {
        striped = new StringStripedAbuseTracker(LaneCount);

        // Each lane sees its own share of a rotating-token flood with one persistent heavy hitter interleaved in,
        // as independent producers pinned to their own lanes would.
        for (int k = 0; k < 100_000; k++)
        {
            int lane = k % LaneCount;
            striped.Observe(lane, $"tok-{k:x}");
            if (k % 20 == 0)
                striped.Observe(lane, "attacker-token");
        }

        _ = striped.Snapshot(20);   // the first rollup builds the merge target the reused arm then keeps
    }

    // Before: a fresh ~2.3 MB tracker per rollup, merged into and then dropped.
    [Benchmark(Baseline = true)]
    public long Rollup_FreshMergeTarget()
    {
        var merged = new StringAbuseTracker();
        for (int i = 0; i < LaneCount; i++)
            merged.Merge(striped.Lane(i));

        return Fold(merged.Snapshot(20));
    }

    // After: Snapshot clears and reuses the merge target it parked on the previous call.
    [Benchmark]
    public long Rollup_StripedSnapshot() => Fold(striped.Snapshot(20));

    private static long Fold(AbuseReport<string> report)
    {
        long acc = report.DistinctKeys + report.TotalObservations;
        foreach (Offender<string> offender in report.Offenders)
            acc += offender.EstimatedCount;
        return acc;
    }
}
