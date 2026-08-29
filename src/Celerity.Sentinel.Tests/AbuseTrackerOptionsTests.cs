namespace Celerity.Sentinel.Tests;

/// <summary>
/// Pins every knob on <see cref="AbuseTrackerOptions"/> — both the values it rejects and the behaviour it
/// selects — so a mis-wired option cannot ship behind a green build.
/// </summary>
/// <remarks>
/// <para>
/// The 100% line/branch gate cannot catch a mis-wired option here: <see cref="AbuseTracker{TKey, THasher}"/>'s
/// constructor reads every option on the default path and passes it straight into a sketch constructor, so
/// each line executes on any test that builds a tracker. Coverage therefore stays green whether the option
/// reaches the right sketch or not — which is the wiring these tests exist to check. The sketches themselves are
/// correct in isolation and separately covered by the <c>Celerity.Collections</c> suites, so nothing here
/// re-tests Count-Min, Space-Saving, HyperLogLog or Bloom internals: each test varies one knob and asserts the
/// tracker's own observable output moves in the documented direction.
/// </para>
/// <para>
/// The invalid-value tests assert the exception <em>type</em>, which is what
/// <see cref="AbuseTracker{TKey, THasher}"/>'s constructor documents. Only <c>RateConfidence</c> is validated
/// by the tracker itself; the rest are validated downstream by the sketch each one sizes, so the type is the
/// part of the contract that holds across both paths. Pinning it is what makes a downstream change to the
/// exception type a red test rather than a silently wrong doc comment.
/// </para>
/// </remarks>
public class AbuseTrackerOptionsTests
{
    // ---------------------------------------------------------------------------------------------------
    // Validation: every knob rejects what the documentation says it rejects.
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0d)]
    [InlineData(1d)]
    [InlineData(-0.5d)]
    [InlineData(2d)]
    [InlineData(double.NaN)]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenRateEpsilonIsOutsideTheUnitInterval(double epsilon) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new StringAbuseTracker(new AbuseTrackerOptions { RateEpsilon = epsilon }));

    [Fact]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenRateEpsilonDemandsAnOversizedCounterGrid()
    {
        // 1e-9 is inside (0, 1), so the range check passes — but a relative error that small asks for more
        // Count-Min counters than the sketch will allocate. This is the one rejection that is not a simple
        // range test, and the only reason a caller sees it is that RateEpsilon sizes the grid.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new StringAbuseTracker(new AbuseTrackerOptions { RateEpsilon = 1e-9 }));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(1d)]
    [InlineData(-0.5d)]
    [InlineData(2d)]
    [InlineData(double.NaN)]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenRateConfidenceIsOutsideTheUnitInterval(double confidence) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new StringAbuseTracker(new AbuseTrackerOptions { RateConfidence = confidence }));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenOffenderCapacityIsNotPositive(int capacity) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new StringAbuseTracker(new AbuseTrackerOptions { OffenderCapacity = capacity }));

    [Theory]
    [InlineData(HyperLogLog<string, StringXxHash3Hasher>.MinPrecision - 1)]
    [InlineData(HyperLogLog<string, StringXxHash3Hasher>.MaxPrecision + 1)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenDistinctPrecisionIsOutsideTheSupportedRange(int precision) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new StringAbuseTracker(new AbuseTrackerOptions { DistinctPrecision = precision }));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenExpectedDistinctKeysIsNotPositive(int expectedDistinctKeys) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringAbuseTracker(
            new AbuseTrackerOptions { TrackFirstSeen = true, ExpectedDistinctKeys = expectedDistinctKeys }));

    [Theory]
    [InlineData(0d)]
    [InlineData(1d)]
    [InlineData(-0.5d)]
    [InlineData(2d)]
    [InlineData(double.NaN)]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenFirstSeenFalsePositiveRateIsOutsideTheUnitInterval(double rate) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringAbuseTracker(
            new AbuseTrackerOptions { TrackFirstSeen = true, FirstSeenFalsePositiveRate = rate }));

    [Fact]
    public void Constructor_ShouldIgnoreTheFirstSeenKnobs_WhenTrackFirstSeenIsFalse()
    {
        // Both knobs are documented as ignored when the filter is off. With no filter to size, values that
        // would otherwise be rejected must not be looked at at all.
        var tracker = new StringAbuseTracker(new AbuseTrackerOptions
        {
            TrackFirstSeen = false,
            ExpectedDistinctKeys = -1,
            FirstSeenFalsePositiveRate = 2d,
        });

        Assert.False(tracker.TracksFirstSeen);
        Assert.Equal(1, tracker.Observe("k").EstimatedCount);
    }

    [Fact]
    public void Constructor_ShouldAcceptEveryKnobAtOnce_WhenAllAreValidAndNonDefault()
    {
        var tracker = new StringAbuseTracker(new AbuseTrackerOptions
        {
            RateEpsilon = 0.01,
            RateConfidence = 0.95,
            OffenderCapacity = 32,
            DistinctPrecision = 12,
            TrackFirstSeen = true,
            ExpectedDistinctKeys = 4_096,
            FirstSeenFalsePositiveRate = 0.05,
        });

        Assert.True(tracker.TracksFirstSeen);
        Assert.True(tracker.Observe("k").IsFirstSeen);
        Assert.False(tracker.Observe("k").IsFirstSeen);
    }

    // ---------------------------------------------------------------------------------------------------
    // Wiring: each knob reaches the sketch it is documented to size.
    // ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public void Snapshot_ShouldMonitorExactlyOffenderCapacityKeys_WhenTheStreamHasMoreDistinctKeys(int capacity)
    {
        // Space-Saving monitors k keys and no more, so the report length is the knob itself: at the default
        // capacity of 128 a tracker that ignored the option would report all 32 keys in the stream.
        AbuseReport<string> report = FrequencyGradient(capacity).Snapshot(GradientKeys * 2);

        Assert.Equal(capacity, report.Offenders.Count);
    }

    [Fact]
    public void Snapshot_ShouldRetainMidTierOffenders_WhenOffenderCapacityIsLarger()
    {
        // The same stream through a 4-key and a 64-key tracker: both keep the heaviest key, only the larger
        // still knows about the middle of the distribution.
        AbuseReport<string> small = FrequencyGradient(4).Snapshot(GradientKeys * 2);
        AbuseReport<string> large = FrequencyGradient(64).Snapshot(GradientKeys * 2);

        // Presence, not position: descending order is Snapshot's own contract and is pinned by
        // AbuseTrackerTests.Snapshot_OffendersAreRankedDescending. What OffenderCapacity decides is which
        // keys are still monitored at all.
        Assert.Contains(small.Offenders, o => o.Key == GradientKey(0));
        Assert.Contains(large.Offenders, o => o.Key == GradientKey(0));

        string midTier = GradientKey(GradientKeys / 2);
        Assert.DoesNotContain(small.Offenders, o => o.Key == midTier);
        Assert.Contains(large.Offenders, o => o.Key == midTier);
    }

    [Fact]
    public void Snapshot_ShouldStillSurfaceAKeyAboveTheFrequencyThreshold_WhenOffenderCapacityIsTiny()
    {
        // The package's headline guarantee, at a capacity a caller would only pick to make it bite: a key
        // whose true frequency exceeds TotalObservations / OffenderCapacity cannot be missed, however many
        // distinct keys wash through the four monitored slots.
        const int capacity = 4;
        const int rotating = 10_000;
        var tracker = new StringAbuseTracker(
            new AbuseTrackerOptions { OffenderCapacity = capacity, TrackFirstSeen = false });

        for (int i = 0; i < rotating; i++)
        {
            tracker.Observe($"rotating-{i}");
            if (i % 2 == 0)
                tracker.Observe("attacker");
        }

        long attackerHits = rotating / 2;
        Assert.True(attackerHits > tracker.TotalObservations / (double)capacity,
            "the test stream must put the attacker above the Space-Saving threshold");

        // The guarantee is that the key cannot be *missed*, so that is what is asserted — not the rank it
        // lands at, which Snapshot decides from the (never-underestimated) Count-Min lift.
        AbuseReport<string> report = tracker.Snapshot(capacity);
        Offender<string> attacker = Assert.Single(report.Offenders, o => o.Key == "attacker");
        Assert.True(attacker.EstimatedCount >= attackerHits,
            $"offender estimate {attacker.EstimatedCount} underestimated the true {attackerHits}");
    }

    [Fact]
    public void EstimateDistinctKeys_ShouldBeMoreAccurate_WhenDistinctPrecisionIsHigher()
    {
        // HyperLogLog's error falls as 1.04 / sqrt(2^precision) — in expectation. A single run is not monotone
        // across neighbouring precisions (8, 10 and 14 all land within a percent of each other on this
        // stream), so the assertion compares the two ends of the supported range, where the gap is three
        // orders of magnitude and not a coin flip.
        //
        // Both bands come from that curve — the one AbuseTrackerOptions.DistinctPrecision documents — rather
        // than from what this stream happens to measure, so they track the contract instead of a snapshot of
        // the implementation. Generous in both directions: the coarse end need only be half as bad as theory
        // predicts, the fine end may be three times worse.
        const int distinct = 20_000;
        const int coarsePrecision = HyperLogLog<string, StringXxHash3Hasher>.MinPrecision;
        const int finePrecision = HyperLogLog<string, StringXxHash3Hasher>.MaxPrecision;

        double coarse = DistinctError(coarsePrecision, distinct);
        double fine = DistinctError(finePrecision, distinct);

        Assert.True(fine < coarse, $"precision did not tighten the estimate: coarse {coarse:P2}, fine {fine:P2}");
        Assert.True(coarse > 0.5 * StandardError(coarsePrecision),
            $"{1 << coarsePrecision} registers estimated {distinct} distinct keys to within {coarse:P2}");
        Assert.True(fine < 3 * StandardError(finePrecision),
            $"{1 << finePrecision} registers were {fine:P2} off, against a {StandardError(finePrecision):P2} standard error");
    }

    [Fact]
    public void EstimateCount_ShouldOverestimateLess_WhenRateEpsilonIsNarrower()
    {
        // One key observed once, buried under a flood of distinct keys. Count-Min never underestimates, so the
        // whole difference between the two trackers is collision noise from the row width RateEpsilon buys.
        //
        // The epsilon bound below holds with probability 1 - delta, not always — delta here is the default
        // RateConfidence's 0.01. It is asserted anyway, because it is the only assertion that catches a
        // *narrow* tracker built wide (the wide/narrow comparison alone passes if both are wide), and the
        // stream leaves it far from the edge: the narrow arm measures 1 against a bound of 3, the wide arm
        // 6,209 against 10,001.
        const int noise = 20_000;
        const double wideEpsilon = 0.5;
        const double narrowEpsilon = 0.0001;
        long total = noise + 1;

        long wide = RareKeyEstimate(wideEpsilon, noise);
        long narrow = RareKeyEstimate(narrowEpsilon, noise);

        Assert.True(wide >= 1, "Count-Min underestimated the rare key");
        Assert.True(narrow >= 1, "Count-Min underestimated the rare key");
        Assert.True(wide <= 1 + (wideEpsilon * total), $"estimate {wide} exceeded the epsilon bound");
        Assert.True(narrow <= 1 + (narrowEpsilon * total), $"estimate {narrow} exceeded the epsilon bound");

        // The wiring proof: the wide tracker blows through the narrow tracker's bound, so the two really are
        // sized by the knob rather than both by the default.
        Assert.True(wide > 1 + (narrowEpsilon * total), $"the wide sketch was as tight as the narrow one ({wide})");
    }

    [Fact]
    public void Observe_ShouldStopReportingNewKeysAsFirstSeen_WhenExpectedDistinctKeysIsUndersized()
    {
        // A Bloom filter sized for eight keys saturates almost immediately: past its capacity everything reads
        // as already seen. Sized for the stream, the same keys are all correctly new.
        const int fresh = 500;

        Assert.True(FalseSeenCount(expectedDistinctKeys: 8, fresh) > fresh / 2,
            "an eight-key filter should have saturated long before the 500th distinct key");
        Assert.Equal(0, FalseSeenCount(expectedDistinctKeys: 1_000_000, fresh));
    }

    [Fact]
    public void Observe_ShouldReportFewerFalseSeens_WhenFirstSeenFalsePositiveRateIsStricter()
    {
        // Same filter capacity, same stream: only the target false-positive rate differs, and it decides how
        // many bits the filter buys per key.
        const int fresh = 200;

        int lax = FalseSeenCount(expectedDistinctKeys: fresh, fresh, falsePositiveRate: 0.5);
        int strict = FalseSeenCount(expectedDistinctKeys: fresh, fresh, falsePositiveRate: 0.0001);

        Assert.True(lax > 0, "a 50% target rate produced no false 'already seen' at all");
        Assert.True(strict < lax, $"strict rate reported {strict} false seens, lax reported {lax}");
    }

    [Fact]
    public void Merge_ShouldThrowArgumentException_WhenTheOptionsProduceDifferentSketchGeometry()
    {
        // AbuseTrackerOptions documents that two trackers must be built with equal options to merge. The
        // first-seen mismatch is checked by AbuseTracker itself; these four are caught by the sketches they
        // size, which is the rest of that contract.
        //
        // OffenderCapacity is deliberately not in this list: Space-Saving has no exact merge, so Merge
        // re-observes the other tracker's monitored offenders instead of combining arrays, and that works
        // whatever capacity either side was built with. Asserting either outcome for it would pin a
        // behaviour the documentation does not promise.
        AssertMergeRejects(new AbuseTrackerOptions { RateEpsilon = 0.01 });
        AssertMergeRejects(new AbuseTrackerOptions { RateConfidence = 0.5 });
        AssertMergeRejects(new AbuseTrackerOptions { DistinctPrecision = 10 });
        AssertMergeRejects(new AbuseTrackerOptions { ExpectedDistinctKeys = 4_096 });

        static void AssertMergeRejects(AbuseTrackerOptions other)
        {
            var baseline = new StringAbuseTracker();
            Assert.Throws<ArgumentException>(() => baseline.Merge(new StringAbuseTracker(other)));
        }
    }

    // ---------------------------------------------------------------------------------------------------
    // Helpers.
    // ---------------------------------------------------------------------------------------------------

    private const int GradientKeys = 32;

    private static string GradientKey(int rank) => $"key-{rank:D2}";

    /// <summary>
    /// Feeds <see cref="GradientKeys"/> keys in strictly descending frequency, interleaved so no key arrives
    /// as one contiguous burst, into a tracker with the given offender capacity.
    /// </summary>
    private static StringAbuseTracker FrequencyGradient(int capacity)
    {
        var tracker = new StringAbuseTracker(
            new AbuseTrackerOptions { OffenderCapacity = capacity, TrackFirstSeen = false });

        for (int round = 0; round < GradientKeys * 2; round++)
            for (int rank = 0; rank < GradientKeys; rank++)
                if (round < (GradientKeys * 2) - (rank * 2))
                    tracker.Observe(GradientKey(rank));

        return tracker;
    }

    /// <summary>HyperLogLog's documented standard error at a precision: <c>1.04 / sqrt(2^precision)</c>.</summary>
    private static double StandardError(int precision) => 1.04 / Math.Sqrt(1 << precision);

    private static double DistinctError(int precision, int distinct)
    {
        var tracker = new StringAbuseTracker(
            new AbuseTrackerOptions { DistinctPrecision = precision, TrackFirstSeen = false });

        for (int i = 0; i < distinct; i++)
            tracker.Observe($"key-{i}");

        return Math.Abs(tracker.EstimateDistinctKeys() - distinct) / (double)distinct;
    }

    private static long RareKeyEstimate(double epsilon, int noise)
    {
        var tracker = new StringAbuseTracker(
            new AbuseTrackerOptions { RateEpsilon = epsilon, TrackFirstSeen = false });

        for (int i = 0; i < noise; i++)
            tracker.Observe($"noise-{i}");
        tracker.Observe("rare");

        return tracker.EstimateCount("rare");
    }

    private static int FalseSeenCount(int expectedDistinctKeys, int fresh, double falsePositiveRate = 0.01)
    {
        var tracker = new StringAbuseTracker(new AbuseTrackerOptions
        {
            TrackFirstSeen = true,
            ExpectedDistinctKeys = expectedDistinctKeys,
            FirstSeenFalsePositiveRate = falsePositiveRate,
        });

        int falseSeens = 0;
        for (int i = 0; i < fresh; i++)
            if (!tracker.Observe($"fresh-{i}").IsFirstSeen)
                falseSeens++;

        return falseSeens;
    }
}
