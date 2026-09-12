namespace Celerity.Sentinel.Tests;

public class StripedAbuseTrackerTests
{
    [Fact]
    public void Constructor_NonPositiveLaneCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringStripedAbuseTracker(0));
    }

    [Fact]
    public void Lane_OutOfRange_Throws()
    {
        var striped = new StringStripedAbuseTracker(4);
        Assert.Throws<ArgumentOutOfRangeException>(() => striped.Lane(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => striped.Observe(-1, "x"));
    }

    [Fact]
    public void Snapshot_MergesAllLanes()
    {
        var striped = new StringStripedAbuseTracker(4);

        // Spread a heavy hitter and some noise across lanes, as independent producers would.
        for (int i = 0; i < 4_000; i++)
            striped.Observe(i % 4, "attacker");
        for (int i = 0; i < 4_000; i++)
            striped.Observe(i % 4, $"noise-{i}");

        AbuseReport<string> report = striped.Snapshot(3);

        Assert.Equal(8_000, report.TotalObservations);
        Assert.Equal("attacker", report.Offenders[0].Key);
        Assert.True(report.Offenders[0].EstimatedCount >= 4_000,
            $"merged heavy-hitter estimate {report.Offenders[0].EstimatedCount} < 4000");
    }

    [Fact]
    public void Snapshot_DistinctAcrossLanes_IsApproximatelyCorrect()
    {
        var striped = new StringStripedAbuseTracker(8);
        const int distinct = 80_000;
        for (int i = 0; i < distinct; i++)
            striped.Observe(i % 8, $"user-{i}");

        long estimate = striped.Snapshot(1).DistinctKeys;
        double relativeError = Math.Abs(estimate - distinct) / (double)distinct;
        Assert.True(relativeError < 0.05, $"distinct estimate {estimate} vs {distinct} (error {relativeError:P1})");
    }

    [Fact]
    public void Lane_ReturnsUsableTracker()
    {
        var striped = new StringStripedAbuseTracker(2);
        AbuseTracker<string, StringXxHash3Hasher> lane0 = striped.Lane(0);
        for (int i = 0; i < 10; i++) lane0.Observe("x");

        Assert.Equal(10, lane0.TotalObservations);
        Assert.Equal(10, striped.Snapshot(1).TotalObservations);
    }

    [Fact]
    public void Clear_ResetsAllLanes()
    {
        var striped = new StringStripedAbuseTracker(3);
        for (int i = 0; i < 30; i++) striped.Observe(i % 3, "x");

        striped.Clear();

        Assert.Equal(0, striped.Snapshot(1).TotalObservations);
    }

    [Fact]
    public void Snapshot_ShouldNotAllocateAMergeTracker_OnceTheFirstRollupHasRun()
    {
        // A tracker at the defaults is ~2.3 MB of sketches (the Bloom filter sized for a million keys dominates).
        // Rolling the lanes up into a fresh one on every call made the rollup cadence, not the observed stream,
        // the thing that drove Sentinel's allocation rate.
        var striped = new StringStripedAbuseTracker(4);
        for (int i = 0; i < 400; i++)
            striped.Observe(i % 4, i % 3 == 0 ? "attacker" : $"noise-{i % 40}");

        _ = striped.Snapshot(5);   // the first rollup builds the merge target

        long before = GC.GetAllocatedBytesForCurrentThread();
        AbuseReport<string> report = striped.Snapshot(5);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(400, report.TotalObservations);
        Assert.Equal("attacker", report.Offenders[0].Key);

        // What is left is the report and the per-lane offender lists the merge reads — a few KB against the
        // ~2.3 MB a fresh tracker costs.
        Assert.InRange(allocated, 0, 64 * 1024);
    }

    [Fact]
    public void Snapshot_ShouldReflectOnlyTheLanesCurrentState_WhenCalledRepeatedly()
    {
        // The merge target is reused across rollups, so every sketch in it must start each one empty: a leftover
        // count would double the totals, a leftover register would inflate the distinct estimate, and a leftover
        // monitor would resurrect an offender the lanes have forgotten.
        var striped = new StringStripedAbuseTracker(3);
        for (int i = 0; i < 3_000; i++)
            striped.Observe(i % 3, $"user-{i}");
        for (int i = 0; i < 300; i++)
            striped.Observe(i % 3, "old-attacker");

        AbuseReport<string> first = striped.Snapshot(3);
        AbuseReport<string> second = striped.Snapshot(3);

        Assert.Equal(3_300, first.TotalObservations);
        Assert.Equal(first.TotalObservations, second.TotalObservations);
        Assert.Equal(first.DistinctKeys, second.DistinctKeys);
        Assert.Equal(first.Offenders, second.Offenders);

        striped.Clear();
        for (int i = 0; i < 6; i++)
            striped.Observe(i % 3, "new-attacker");

        AbuseReport<string> afterClear = striped.Snapshot(3);

        Assert.Equal(6, afterClear.TotalObservations);
        Assert.Equal(1, afterClear.DistinctKeys);
        Offender<string> offender = Assert.Single(afterClear.Offenders);
        Assert.Equal("new-attacker", offender.Key);
        Assert.Equal(6, offender.EstimatedCount);
    }

    [Fact]
    public void Snapshot_ShouldReturnTheSameReport_WhenCalledFromSeveralThreadsAtOnce()
    {
        // Snapshot only reads the lanes, so concurrent rollups over quiescent lanes were safe while each built its
        // own merge target. Sharing one must not take that away: a caller that loses the race for the parked
        // target builds its own rather than merging into the one another thread is using.
        var striped = new StringStripedAbuseTracker(4, new AbuseTrackerOptions { ExpectedDistinctKeys = 4_096 });
        for (int i = 0; i < 4_000; i++)
            striped.Observe(i % 4, i % 2 == 0 ? "attacker" : $"noise-{i % 100}");

        AbuseReport<string> expected = striped.Snapshot(5);

        var reports = new AbuseReport<string>[64];
        Parallel.For(0, reports.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 },
            i => reports[i] = striped.Snapshot(5));

        foreach (AbuseReport<string> report in reports)
        {
            Assert.Equal(expected.TotalObservations, report.TotalObservations);
            Assert.Equal(expected.DistinctKeys, report.DistinctKeys);
            Assert.Equal(expected.Offenders, report.Offenders);
        }
    }
}
