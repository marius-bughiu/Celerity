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
}
