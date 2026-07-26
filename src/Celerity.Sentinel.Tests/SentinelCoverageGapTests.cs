namespace Celerity.Sentinel.Tests;

/// <summary>
/// Pins the parts of the Sentinel surface that the behavioural suites never touch, so they cannot silently
/// regress: the <see cref="AbuseTracker{TKey, THasher}.TracksFirstSeen"/> and
/// <see cref="StripedAbuseTracker{TKey, THasher}.LaneCount"/> introspection properties, the
/// <see cref="Offender{TKey}.ToString"/> formatting, and — most importantly — both arms of the optional
/// first-seen Bloom filter inside <see cref="AbuseTracker{TKey, THasher}.Merge"/> and
/// <see cref="AbuseTracker{TKey, THasher}.Clear"/>.
/// </summary>
/// <remarks>
/// <para>
/// The first-seen filter is created only when <see cref="AbuseTrackerOptions.TrackFirstSeen"/> is set, so
/// <c>Merge</c> and <c>Clear</c> reach it through a null-conditional call. The existing suites only ever merge
/// and clear trackers built with the defaults (filter present), which leaves the "first-seen disabled" path
/// unexercised — the exact configuration a caller picks when they want the rate/offender/distinct signals
/// without paying for a megabyte-scale Bloom filter. These tests drive both configurations through both
/// operations and assert the observable outcome each time: that a merge really unions the first-seen knowledge
/// of the two streams, that a clear really forgets it, and that neither operation resurrects (or trips over) a
/// filter that was never allocated.
/// </para>
/// <para>
/// <see cref="StripedAbuseTracker{TKey, THasher}.Snapshot"/>'s <c>topN</c> guard is pinned from both sides,
/// because the guard's whole purpose is to place the boundary at zero: negative is rejected, zero is a legal
/// request for "no offenders, just the stream totals".
/// </para>
/// </remarks>
public class SentinelCoverageGapTests
{
    /// <summary>Options with the first-seen filter on, sized small so the tests stay cheap.</summary>
    private static AbuseTrackerOptions WithFirstSeen() => new()
    {
        TrackFirstSeen = true,
        ExpectedDistinctKeys = 4_096,
        FirstSeenFalsePositiveRate = 0.01,
    };

    /// <summary>Options with the first-seen filter off; the rest of the geometry matches <see cref="WithFirstSeen"/>.</summary>
    private static AbuseTrackerOptions WithoutFirstSeen() => new()
    {
        TrackFirstSeen = false,
        ExpectedDistinctKeys = 4_096,
        FirstSeenFalsePositiveRate = 0.01,
    };

    [Fact]
    public void TracksFirstSeen_ShouldBeTrue_WhenTrackFirstSeenIsEnabled()
    {
        var configured = new StringAbuseTracker(WithFirstSeen());
        var defaulted = new StringAbuseTracker();

        Assert.True(configured.TracksFirstSeen);
        // TrackFirstSeen defaults to true, so the parameterless tracker reports the same.
        Assert.True(defaulted.TracksFirstSeen);
    }

    [Fact]
    public void TracksFirstSeen_ShouldBeFalse_WhenTrackFirstSeenIsDisabled()
    {
        var tracker = new StringAbuseTracker(WithoutFirstSeen());

        Assert.False(tracker.TracksFirstSeen);
        // The property agrees with the behaviour it advertises: querying the signal is an error.
        Assert.Throws<InvalidOperationException>(() => tracker.HasProbablySeen("x"));
    }

    [Fact]
    public void Merge_ShouldUnionTheFirstSeenFilter_WhenFirstSeenTrackingIsEnabled()
    {
        var a = new StringAbuseTracker(WithFirstSeen());
        var b = new StringAbuseTracker(WithFirstSeen());

        for (int i = 0; i < 50; i++) a.Observe("only-a");
        for (int i = 0; i < 30; i++) b.Observe("only-b");

        // Before the merge, a has no knowledge of b's keys.
        Assert.False(a.HasProbablySeen("only-b"));

        a.Merge(b);

        // The Bloom filters are unioned, so b's keys are now "seen" in a — and a re-observation is no longer new.
        Assert.True(a.TracksFirstSeen);
        Assert.True(a.HasProbablySeen("only-b"));
        Assert.True(a.HasProbablySeen("only-a"));
        Assert.False(a.Observe("only-b").IsFirstSeen);

        // A key neither stream ever saw stays unseen (Bloom has no false negatives; at this load no false positive).
        Assert.False(a.HasProbablySeen("never-observed-key"));

        Assert.Equal(81, a.TotalObservations);
        Assert.True(a.EstimateCount("only-b") >= 31, $"merged 'only-b' estimate {a.EstimateCount("only-b")} < 31");
    }

    [Fact]
    public void Merge_ShouldCombineStreamsWithoutAFirstSeenFilter_WhenFirstSeenTrackingIsDisabled()
    {
        var a = new StringAbuseTracker(WithoutFirstSeen());
        var b = new StringAbuseTracker(WithoutFirstSeen());

        for (int i = 0; i < 50; i++) a.Observe("only-a");
        for (int i = 0; i < 30; i++) b.Observe("only-b");

        a.Merge(b);

        // Rate, distinct, and totals still merge; the absent filter is simply skipped rather than allocated.
        Assert.Equal(80, a.TotalObservations);
        Assert.True(a.EstimateCount("only-a") >= 50, $"merged 'only-a' estimate {a.EstimateCount("only-a")} < 50");
        Assert.True(a.EstimateCount("only-b") >= 30, $"merged 'only-b' estimate {a.EstimateCount("only-b")} < 30");
        Assert.Equal("only-a", a.Snapshot(2).Offenders[0].Key);

        // The merge did not turn first-seen tracking on.
        Assert.False(a.TracksFirstSeen);
        Assert.Throws<InvalidOperationException>(() => a.HasProbablySeen("only-b"));
        Assert.False(a.Observe("only-b").IsFirstSeen);
    }

    [Fact]
    public void Clear_ShouldResetTheFirstSeenFilter_WhenFirstSeenTrackingIsEnabled()
    {
        var tracker = new StringAbuseTracker(WithFirstSeen());
        for (int i = 0; i < 20; i++) tracker.Observe("x");
        Assert.True(tracker.HasProbablySeen("x"));

        tracker.Clear();

        // The filter is cleared, not merely detached: the key reads as unseen and observing it is "first seen" again.
        Assert.True(tracker.TracksFirstSeen);
        Assert.False(tracker.HasProbablySeen("x"));
        Assert.True(tracker.Observe("x").IsFirstSeen);
        Assert.Equal(1, tracker.TotalObservations);
    }

    [Fact]
    public void Clear_ShouldResetCountsAndLeaveFirstSeenDisabled_WhenFirstSeenTrackingIsDisabled()
    {
        var tracker = new StringAbuseTracker(WithoutFirstSeen());
        for (int i = 0; i < 20; i++) tracker.Observe("x");

        tracker.Clear();

        Assert.Equal(0, tracker.TotalObservations);
        Assert.Equal(0, tracker.EstimateCount("x"));
        Assert.Equal(0, tracker.EstimateDistinctKeys());
        Assert.Empty(tracker.Snapshot(5).Offenders);

        // Clearing did not materialize a filter that was never configured.
        Assert.False(tracker.TracksFirstSeen);
        Assert.Throws<InvalidOperationException>(() => tracker.HasProbablySeen("x"));
    }

    [Fact]
    public void ToString_ShouldFormatKeyCountAndError_WhenOffenderComesFromASnapshot()
    {
        var tracker = new StringAbuseTracker();
        for (int i = 0; i < 7; i++) tracker.Observe("sole-offender");

        Offender<string> offender = Assert.Single(tracker.Snapshot(5).Offenders);

        // One monitored key, no evictions and no hash collisions: both the Space-Saving and the Count-Min
        // estimates are exactly 7 and the error bound collapses to 0.
        Assert.Equal("sole-offender", offender.Key);
        Assert.Equal(7, offender.EstimatedCount);
        Assert.Equal(0, offender.Error);
        Assert.Equal("sole-offender (7, err 0)", offender.ToString());
    }

    [Fact]
    public void LaneCount_ShouldEqualTheConstructedLaneCount()
    {
        Assert.Equal(1, new StringStripedAbuseTracker(1).LaneCount);
        Assert.Equal(6, new StringStripedAbuseTracker(6).LaneCount);

        var striped = new StringStripedAbuseTracker(3, WithoutFirstSeen());
        Assert.Equal(3, striped.LaneCount);

        // LaneCount is exactly the range Lane accepts: the last index in range works, one past it throws.
        AbuseTracker<string, StringXxHash3Hasher> lastLane = striped.Lane(striped.LaneCount - 1);
        lastLane.Observe("x");
        Assert.Equal(1, lastLane.TotalObservations);
        Assert.Throws<ArgumentOutOfRangeException>(() => striped.Lane(striped.LaneCount));
    }

    [Fact]
    public void Snapshot_ShouldThrowArgumentOutOfRange_WhenTopNIsNegative()
    {
        var striped = new StringStripedAbuseTracker(2);
        striped.Observe(0, "x");

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => striped.Snapshot(-1));

        Assert.Equal("topN", ex.ParamName);
        Assert.Equal(-1, Assert.IsType<int>(ex.ActualValue));
        Assert.Contains("topN must be non-negative.", ex.Message);
    }

    [Fact]
    public void Snapshot_ShouldReturnAnEmptyOffenderListWithLiveTotals_WhenTopNIsZero()
    {
        var striped = new StringStripedAbuseTracker(2);
        for (int i = 0; i < 10; i++) striped.Observe(i % 2, "attacker");

        AbuseReport<string> report = striped.Snapshot(0);

        // Zero is the accepted boundary of the guard: no offenders requested, but the stream totals still roll up.
        Assert.Empty(report.Offenders);
        Assert.Equal(10, report.TotalObservations);
        Assert.Equal(1, report.DistinctKeys);
    }
}
