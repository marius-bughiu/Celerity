namespace Celerity.Sentinel.Tests;

public class AbuseTrackerTests
{
    [Fact]
    public void Observe_ReportsFirstSeenThenSeen()
    {
        var tracker = new StringAbuseTracker();

        ObservationResult first = tracker.Observe("1.2.3.4");
        Assert.True(first.IsFirstSeen);

        ObservationResult second = tracker.Observe("1.2.3.4");
        Assert.False(second.IsFirstSeen);
    }

    [Fact]
    public void Observe_EstimatedCount_NeverUnderestimates()
    {
        var tracker = new StringAbuseTracker();
        const int hits = 250;
        ObservationResult last = default;
        for (int i = 0; i < hits; i++)
            last = tracker.Observe("hot-key");

        Assert.True(last.EstimatedCount >= hits, $"estimate {last.EstimatedCount} < true {hits}");
        Assert.True(tracker.EstimateCount("hot-key") >= hits);
    }

    [Fact]
    public void TotalObservations_CountsEveryObserve()
    {
        var tracker = new StringAbuseTracker();
        for (int i = 0; i < 1000; i++)
            tracker.Observe($"k{i % 10}");

        Assert.Equal(1000, tracker.TotalObservations);
    }

    [Fact]
    public void HeavyHitter_SurvivesAFloodOfDistinctKeys()
    {
        // The categorical win: an attacker rotating through 100k unique keys would blow up a
        // Dictionary<string,int>, but the fixed-memory tracker still surfaces the real heavy hitter.
        var tracker = new StringAbuseTracker();

        const int flood = 100_000;
        const int heavyHits = 3_000;
        for (int i = 0; i < flood; i++)
        {
            tracker.Observe($"rotating-{i}");
            if (i % 33 == 0)
                tracker.Observe("attacker-token");
        }

        int extra = heavyHits - (flood / 33 + 1);
        for (int i = 0; i < extra; i++)
            tracker.Observe("attacker-token");

        AbuseReport<string> report = tracker.Snapshot(5);
        Assert.NotEmpty(report.Offenders);
        Assert.Equal("attacker-token", report.Offenders[0].Key);
        Assert.True(report.Offenders[0].EstimatedCount >= heavyHits,
            $"top offender estimate {report.Offenders[0].EstimatedCount} < {heavyHits}");
    }

    [Fact]
    public void EstimateDistinctKeys_IsApproximatelyCorrect()
    {
        var tracker = new StringAbuseTracker();
        const int distinct = 100_000;
        for (int i = 0; i < distinct; i++)
            tracker.Observe($"key-{i}");

        long estimate = tracker.EstimateDistinctKeys();
        double relativeError = Math.Abs(estimate - distinct) / (double)distinct;
        Assert.True(relativeError < 0.05, $"distinct estimate {estimate} vs {distinct} (error {relativeError:P1})");
    }

    [Fact]
    public void Snapshot_OffendersAreRankedDescending()
    {
        var tracker = new StringAbuseTracker();
        for (int i = 0; i < 500; i++) tracker.Observe("a");
        for (int i = 0; i < 300; i++) tracker.Observe("b");
        for (int i = 0; i < 100; i++) tracker.Observe("c");

        AbuseReport<string> report = tracker.Snapshot(3);
        Assert.Equal(new[] { "a", "b", "c" }, report.Offenders.Select(o => o.Key).ToArray());
        Assert.True(report.Offenders[0].EstimatedCount >= report.Offenders[1].EstimatedCount);
        Assert.True(report.Offenders[1].EstimatedCount >= report.Offenders[2].EstimatedCount);
    }

    [Fact]
    public void Merge_CombinesBothStreams()
    {
        var a = new StringAbuseTracker();
        var b = new StringAbuseTracker();

        for (int i = 0; i < 400; i++) a.Observe("shared");
        for (int i = 0; i < 100; i++) a.Observe("only-a");
        for (int i = 0; i < 600; i++) b.Observe("shared");
        for (int i = 0; i < 200; i++) b.Observe("only-b");

        a.Merge(b);

        Assert.Equal(1300, a.TotalObservations);
        Assert.True(a.EstimateCount("shared") >= 1000, $"merged 'shared' estimate {a.EstimateCount("shared")} < 1000");
        Assert.True(a.EstimateCount("only-b") >= 200);

        AbuseReport<string> report = a.Snapshot(3);
        Assert.Equal("shared", report.Offenders[0].Key);
        // The reported offender count must not underestimate the true union frequency, even though the merged
        // Space-Saving count alone could: it is lifted to the exactly-merged Count-Min estimate.
        Assert.True(report.Offenders[0].EstimatedCount >= 1000,
            $"merged offender count {report.Offenders[0].EstimatedCount} underestimated true 1000");
    }

    [Fact]
    public void Merge_MismatchedFirstSeenSetting_Throws()
    {
        var withBloom = new StringAbuseTracker(new AbuseTrackerOptions { TrackFirstSeen = true });
        var withoutBloom = new StringAbuseTracker(new AbuseTrackerOptions { TrackFirstSeen = false });

        Assert.Throws<ArgumentException>(() => withBloom.Merge(withoutBloom));
    }

    [Fact]
    public void Clear_ResetsEverything()
    {
        var tracker = new StringAbuseTracker();
        for (int i = 0; i < 100; i++) tracker.Observe("x");

        tracker.Clear();

        Assert.Equal(0, tracker.TotalObservations);
        Assert.Equal(0, tracker.EstimateCount("x"));
        Assert.Empty(tracker.Snapshot(5).Offenders);
        Assert.True(tracker.Observe("x").IsFirstSeen);
    }

    [Fact]
    public void HasProbablySeen_WhenDisabled_Throws()
    {
        var tracker = new StringAbuseTracker(new AbuseTrackerOptions { TrackFirstSeen = false });
        tracker.Observe("x");
        Assert.Throws<InvalidOperationException>(() => tracker.HasProbablySeen("x"));
    }

    [Fact]
    public void HasProbablySeen_NoFalseNegatives()
    {
        var tracker = new StringAbuseTracker();
        tracker.Observe("known");
        Assert.True(tracker.HasProbablySeen("known"));
        // A never-observed key has no false negative: it must report not-seen (bar the small FP rate; this
        // specific key is chosen to be clearly absent).
        Assert.False(tracker.HasProbablySeen("definitely-never-observed-xyz"));
    }

    [Fact]
    public void Constructor_InvalidRateConfidence_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StringAbuseTracker(new AbuseTrackerOptions { RateConfidence = 1.0 }));
    }

    [Fact]
    public void Snapshot_NegativeTopN_Throws()
    {
        var tracker = new StringAbuseTracker();
        Assert.Throws<ArgumentOutOfRangeException>(() => tracker.Snapshot(-1));
    }

    [Fact]
    public void Works_WithIntegerKeys()
    {
        var tracker = new AbuseTracker<long, Int64WangNaiveHasher>();
        for (int i = 0; i < 200; i++) tracker.Observe(42L);
        for (int i = 0; i < 50; i++) tracker.Observe(7L);

        AbuseReport<long> report = tracker.Snapshot(2);
        Assert.Equal(42L, report.Offenders[0].Key);
        Assert.True(report.Offenders[0].EstimatedCount >= 200);
    }
}
