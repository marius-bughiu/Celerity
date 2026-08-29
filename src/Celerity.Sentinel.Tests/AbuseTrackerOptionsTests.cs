namespace Celerity.Sentinel.Tests;

/// <summary>
/// Pins each <see cref="AbuseTrackerOptions"/> knob to the behaviour it advertises, at two levels: that an
/// out-of-range value is rejected with the exception type
/// <see cref="AbuseTracker{TKey, THasher}(AbuseTrackerOptions)"/> documents, and that a value the tracker
/// accepts actually reaches — and only reaches — the sketch it is supposed to size.
/// </summary>
/// <remarks>
/// <para>
/// Neither level is implied by the coverage gate. <see cref="AbuseTracker{TKey, THasher}"/>'s constructor reads
/// every option on the default path and hands it straight to a sketch constructor, so all of those lines run on
/// any test that builds a tracker, whatever the option values are. A knob could be validated by nobody, wired to
/// the wrong sketch, or silently ignored, and the line/branch numbers would not move.
/// </para>
/// <para>
/// The statistical quality of each sketch is not re-tested here — <c>CountMinSketchAccuracyTests</c>,
/// <c>TopKSketchAccuracyTests</c>, <c>HyperLogLogAccuracyTests</c>, and the Bloom filter's false-positive suite
/// own that, and they cover it far more thoroughly than a tracker-level test could. What those suites cannot
/// see is the wiring: that <see cref="AbuseTrackerOptions.DistinctPrecision"/> sizes the HyperLogLog rather than
/// the Space-Saving sketch (both are plain <c>int</c>s, so a swap compiles), that
/// <see cref="AbuseTrackerOptions.RateConfidence"/> is converted to the Count-Min <c>delta</c> as
/// <c>1 − confidence</c> and not passed through raw, and that the first-seen filter is sized for
/// <see cref="AbuseTrackerOptions.ExpectedDistinctKeys"/>. Each test below fails if its knob is unwired,
/// cross-wired, or mistranslated.
/// </para>
/// <para>
/// Every stream is deterministic — fixed keys through the seedless, canonical
/// <see cref="StringXxHash3Hasher"/> — so the measured estimates and error bands are stable across runs and
/// platforms.
/// </para>
/// </remarks>
public class AbuseTrackerOptionsTests
{
    /// <summary>Builds a tracker from <paramref name="options"/> and returns the rejection it must produce.</summary>
    private static ArgumentOutOfRangeException Rejected(AbuseTrackerOptions options) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new StringAbuseTracker(options));

    [Theory]
    [InlineData(0d)]
    [InlineData(1d)]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    [InlineData(double.NaN)]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenRateEpsilonIsNotStrictlyBetweenZeroAndOne(double epsilon)
    {
        Rejected(new AbuseTrackerOptions { RateEpsilon = epsilon });
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(1d)]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenRateConfidenceIsNotStrictlyBetweenZeroAndOne(double confidence)
    {
        ArgumentOutOfRangeException ex = Rejected(new AbuseTrackerOptions { RateConfidence = confidence });

        // The one knob the tracker validates itself, so it is also the one that reports the caller's parameter.
        Assert.Equal("options", ex.ParamName);
        Assert.Equal(confidence, Assert.IsType<double>(ex.ActualValue));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenRateConfidenceIsNaN()
    {
        // NaN fails every comparison, so it slips past the tracker's own `<= 0 || >= 1` guard and is caught one
        // level down by CountMinSketch's explicit NaN check on the derived delta (1 - NaN is NaN). The documented
        // exception type still holds, which is what a caller can rely on; only the reported parameter differs.
        ArgumentOutOfRangeException ex = Rejected(new AbuseTrackerOptions { RateConfidence = double.NaN });

        Assert.Equal("delta", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenOffenderCapacityIsNotPositive(int capacity)
    {
        Rejected(new AbuseTrackerOptions { OffenderCapacity = capacity });
    }

    [Theory]
    [InlineData(3)]   // one below HyperLogLog's MinPrecision
    [InlineData(17)]  // one above its MaxPrecision
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenDistinctPrecisionIsOutsideTheSupportedRange(int precision)
    {
        // "The supported range" is HyperLogLog's own; assert the boundaries this data encodes still hold, so a
        // widened range surfaces here rather than as a silently vacuous test.
        Assert.True(precision < HyperLogLog<string, StringXxHash3Hasher>.MinPrecision
                 || precision > HyperLogLog<string, StringXxHash3Hasher>.MaxPrecision,
            $"precision {precision} is inside the supported range, so this case no longer tests a rejection");

        Rejected(new AbuseTrackerOptions { DistinctPrecision = precision });
    }

    [Fact]
    public void Constructor_ShouldSucceed_WhenDistinctPrecisionIsAtEitherSupportedBoundary()
    {
        // The rejection tests above are only meaningful if the boundary itself is accepted.
        foreach (int precision in new[]
                 {
                     HyperLogLog<string, StringXxHash3Hasher>.MinPrecision,
                     HyperLogLog<string, StringXxHash3Hasher>.MaxPrecision,
                 })
        {
            var tracker = new StringAbuseTracker(new AbuseTrackerOptions { DistinctPrecision = precision });
            tracker.Observe("x");
            Assert.Equal(1, tracker.EstimateDistinctKeys());
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenExpectedDistinctKeysIsNotPositive(int expectedDistinctKeys)
    {
        Rejected(new AbuseTrackerOptions { TrackFirstSeen = true, ExpectedDistinctKeys = expectedDistinctKeys });
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(1d)]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    public void Constructor_ShouldThrowArgumentOutOfRange_WhenFirstSeenFalsePositiveRateIsNotStrictlyBetweenZeroAndOne(double rate)
    {
        Rejected(new AbuseTrackerOptions { TrackFirstSeen = true, FirstSeenFalsePositiveRate = rate });
    }

    [Fact]
    public void Constructor_ShouldIgnoreTheFirstSeenSizing_WhenTrackFirstSeenIsDisabled()
    {
        // Both knobs are documented as "ignored when TrackFirstSeen is false" — no filter is allocated, so
        // values that are rejected outright when the filter is on must not be validated when it is off.
        var tracker = new StringAbuseTracker(new AbuseTrackerOptions
        {
            TrackFirstSeen = false,
            ExpectedDistinctKeys = -1,
            FirstSeenFalsePositiveRate = 42d,
        });

        Assert.False(tracker.TracksFirstSeen);
        Assert.False(tracker.Observe("x").IsFirstSeen);
    }

    [Fact]
    public void Constructor_ShouldReportTheSketchesOwnParameterNames_WhenAnOptionIsRejectedDownstream()
    {
        // Documents the current shape of the rejection surface in one place. Apart from RateConfidence, every
        // option is validated by the sketch it sizes, so ParamName is that sketch's parameter (`epsilon`,
        // `capacity`, `precision`, …) rather than the option the caller set. The constructor's documented
        // contract is the exception *type*, which every case honours; this test exists so that changing which
        // parameter is reported is a deliberate act rather than an unnoticed side effect of a refactor.
        Assert.Equal("epsilon", Rejected(new AbuseTrackerOptions { RateEpsilon = 0d }).ParamName);
        Assert.Equal("capacity", Rejected(new AbuseTrackerOptions { OffenderCapacity = 0 }).ParamName);
        Assert.Equal("precision", Rejected(new AbuseTrackerOptions { DistinctPrecision = 3 }).ParamName);
        Assert.Equal("expectedItems", Rejected(new AbuseTrackerOptions { ExpectedDistinctKeys = 0 }).ParamName);
        Assert.Equal("falsePositiveRate", Rejected(new AbuseTrackerOptions { FirstSeenFalsePositiveRate = 1d }).ParamName);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(8, 8)]
    [InlineData(64, 64)]
    [InlineData(256, 200)]
    public void Snapshot_ShouldMonitorAtMostOffenderCapacityKeys_WhenTheStreamHasMoreDistinctKeys(int capacity, int expectedOffenders)
    {
        var tracker = new StringAbuseTracker(new AbuseTrackerOptions
        {
            OffenderCapacity = capacity,
            TrackFirstSeen = false,
        });

        for (int i = 0; i < 200; i++)
            tracker.Observe($"key-{i}");

        // The O(k) memory the option buys, made observable: Space-Saving monitors exactly k keys once the stream
        // has passed k distinct ones (and all of them before that), so the report cannot exceed the capacity no
        // matter how many offenders the caller asks for.
        Assert.Equal(expectedOffenders, tracker.Snapshot(1_000).Offenders.Count);
    }

    [Fact]
    public void Snapshot_ShouldRetainAnOffenderTheSmallerCapacityEvicts_WhenOffenderCapacityIsRaised()
    {
        const int smallCapacity = 8;
        const int largeCapacity = 64;

        StringAbuseTracker small = FeedSkewedStream(smallCapacity);
        StringAbuseTracker large = FeedSkewedStream(largeCapacity);

        long total = small.TotalObservations;
        Assert.Equal(total, large.TotalObservations);

        // The package's headline guarantee is a threshold, not a ranking: Space-Saving never misses a key whose
        // true frequency exceeds TotalObservations / OffenderCapacity. "mid-tier" is placed deliberately between
        // the two thresholds — below the small tracker's (~2,383) and above the large one's (~298) — so raising
        // the knob is exactly what moves it from "may be evicted" to "cannot be missed".
        Assert.True(MidTierFrequency < total / (double)smallCapacity, $"mid-tier {MidTierFrequency} is not below {total / (double)smallCapacity}");
        Assert.True(MidTierFrequency > total / (double)largeCapacity, $"mid-tier {MidTierFrequency} is not above {total / (double)largeCapacity}");

        string[] smallOffenders = small.Snapshot(1_000).Offenders.Select(o => o.Key).ToArray();
        string[] largeOffenders = large.Snapshot(1_000).Offenders.Select(o => o.Key).ToArray();

        Assert.Contains("mid-tier", largeOffenders);
        // Below the threshold there is no guarantee either way — but on this fixed stream the smaller sketch
        // does evict it, which is what makes the knob observable rather than decorative.
        Assert.DoesNotContain("mid-tier", smallOffenders);

        // "heavy-hitter" clears both thresholds, so neither capacity may miss it, and the rate estimate it is
        // reported with never underestimates the truth.
        Assert.True(HeavyHitterFrequency > total / (double)smallCapacity,
            $"heavy-hitter {HeavyHitterFrequency} is not above {total / (double)smallCapacity}");
        Assert.Equal("heavy-hitter", small.Snapshot(1).Offenders[0].Key);
        Assert.Equal("heavy-hitter", large.Snapshot(1).Offenders[0].Key);
        Assert.True(small.Snapshot(1).Offenders[0].EstimatedCount >= HeavyHitterFrequency);

        // The capacity sizes the Space-Saving sketch and nothing else: the distinct-key estimate, which comes
        // from the HyperLogLog, is untouched by it.
        Assert.Equal(small.EstimateDistinctKeys(), large.EstimateDistinctKeys());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(14)]
    [InlineData(16)]
    public void EstimateDistinctKeys_ShouldMatchAStandaloneHyperLogLogOfTheSamePrecision_WhenDistinctPrecisionIsSet(int precision)
    {
        const int distinct = 20_000;
        var tracker = new StringAbuseTracker(new AbuseTrackerOptions
        {
            DistinctPrecision = precision,
            TrackFirstSeen = false,
        });
        var reference = new HyperLogLog<string, StringXxHash3Hasher>(precision);

        for (int i = 0; i < distinct; i++)
        {
            tracker.Observe($"d-{i}");
            reference.Add($"d-{i}");
        }

        // Exact equality is the wiring assertion: the option reaches the HyperLogLog constructor unchanged, so
        // the tracker's estimate is precisely what a bare estimator of that precision produces on these keys.
        // Cross-wiring it to OffenderCapacity (also an int) or dropping it for the default would break this.
        Assert.Equal(precision, reference.Precision);
        Assert.Equal(reference.EstimateCardinality(), tracker.EstimateDistinctKeys());

        // And the estimate the caller gets stays inside the accuracy the option's doc promises: the relative
        // standard error is 1.04 / sqrt(2^precision), so allow two standard errors.
        double standardError = 1.04 / Math.Sqrt(1 << precision);
        double relativeError = Math.Abs(tracker.EstimateDistinctKeys() - distinct) / (double)distinct;
        Assert.True(relativeError <= 2 * standardError,
            $"precision {precision}: relative error {relativeError:P2} exceeds 2 x {standardError:P2}");
    }

    [Fact]
    public void EstimateDistinctKeys_ShouldNarrowTheErrorBand_WhenDistinctPrecisionIsRaised()
    {
        const int coarsePrecision = 4;   // 16 registers, ~26% standard error
        const int finePrecision = 14;    // 16 KB of registers, ~0.8%
        const int distinct = 20_000;

        var coarse = new StringAbuseTracker(new AbuseTrackerOptions { DistinctPrecision = coarsePrecision, TrackFirstSeen = false });
        var fine = new StringAbuseTracker(new AbuseTrackerOptions { DistinctPrecision = finePrecision, TrackFirstSeen = false });

        for (int i = 0; i < distinct; i++)
        {
            coarse.Observe($"d-{i}");
            fine.Observe($"d-{i}");
        }

        double coarseError = Math.Abs(coarse.EstimateDistinctKeys() - distinct) / (double)distinct;
        double fineError = Math.Abs(fine.EstimateDistinctKeys() - distinct) / (double)distinct;

        // HyperLogLog's error is a standard deviation, not a monotone function of precision, so this compares
        // two precisions whose bands do not overlap rather than sweeping for monotonicity: the coarse estimate
        // lands outside the band the fine one is confined to, which no amount of register luck would produce if
        // both sketches were built at the same precision.
        double fineBand = 2 * (1.04 / Math.Sqrt(1 << finePrecision));
        Assert.True(fineError <= fineBand, $"fine error {fineError:P2} exceeds {fineBand:P2}");
        Assert.True(coarseError > fineBand, $"coarse error {coarseError:P2} is inside the fine band {fineBand:P2}");
    }

    [Fact]
    public void EstimateCount_ShouldTightenAColdKeysOverestimate_WhenRateEpsilonIsLowered()
    {
        const double coarseEpsilon = 0.2;
        const double tightEpsilon = 0.001;
        const int distinctKeys = 20_000;

        var coarse = new StringAbuseTracker(new AbuseTrackerOptions { RateEpsilon = coarseEpsilon, TrackFirstSeen = false });
        var tight = new StringAbuseTracker(new AbuseTrackerOptions { RateEpsilon = tightEpsilon, TrackFirstSeen = false });

        for (int i = 0; i < distinctKeys; i++)
        {
            coarse.Observe($"r-{i}");
            tight.Observe($"r-{i}");
        }

        long coarseEstimate = coarse.EstimateCount("r-7");
        long tightEstimate = tight.EstimateCount("r-7");
        long total = coarse.TotalObservations;

        // Count-Min never underestimates, and overestimates by at most epsilon x TotalObservations — the bound
        // the option is documented to buy. Both settings honour it, against very different budgets.
        Assert.True(coarseEstimate >= 1, $"coarse estimate {coarseEstimate} underestimated the true count of 1");
        Assert.True(tightEstimate >= 1, $"tight estimate {tightEstimate} underestimated the true count of 1");
        Assert.True(coarseEstimate - 1 <= coarseEpsilon * total, $"coarse overestimate {coarseEstimate - 1} exceeds {coarseEpsilon * total}");
        Assert.True(tightEstimate - 1 <= tightEpsilon * total, $"tight overestimate {tightEstimate - 1} exceeds {tightEpsilon * total}");

        // The knob is live: on the identical stream, the narrower error factor buys a far tighter estimate of a
        // cold key, because it widens the counter grid and so collides it with fewer of the other 20,000 keys.
        Assert.True(tightEstimate < coarseEstimate, $"tight estimate {tightEstimate} did not beat coarse {coarseEstimate}");
    }

    [Fact]
    public void EstimateCount_ShouldMatchAStandaloneCountMinSketchOfTheSameGeometry_WhenTheRateKnobsAreSet()
    {
        const double epsilon = 0.01;
        const double confidence = 0.9;
        const int distinctKeys = 5_000;

        var tracker = new StringAbuseTracker(new AbuseTrackerOptions
        {
            RateEpsilon = epsilon,
            RateConfidence = confidence,
            TrackFirstSeen = false,
        });
        // The tracker's contract is that RateConfidence is a *confidence*, converted to the Count-Min delta as
        // 1 - confidence. Passing the confidence through raw would leave every existing assertion green (the
        // sketch still never underestimates) while quietly building a one-row sketch, so pin the translation by
        // reproducing the geometry here: at delta = 0.1 the sketch has 3 rows, at delta = 0.9 it would have 1.
        var reference = new CountMinSketch<string, StringXxHash3Hasher>(epsilon, 1d - confidence);

        for (int i = 0; i < distinctKeys; i++)
        {
            tracker.Observe($"c-{i}");
            reference.Add($"c-{i}");
        }

        for (int i = 0; i < distinctKeys; i += 250)
            Assert.Equal(reference.EstimateCount($"c-{i}"), tracker.EstimateCount($"c-{i}"));
    }

    [Fact]
    public void HasProbablySeen_ShouldSaturate_WhenTheStreamOutgrowsExpectedDistinctKeys()
    {
        const int stream = 20_000;

        // Undersized: a filter sized for 64 keys and fed 20,000 fills up, and the doc's caveat takes hold —
        // everything reads as "seen", so the first-seen signal is worthless past its sizing.
        var undersized = new StringAbuseTracker(new AbuseTrackerOptions { TrackFirstSeen = true, ExpectedDistinctKeys = 64 });
        var sized = new StringAbuseTracker(new AbuseTrackerOptions { TrackFirstSeen = true, ExpectedDistinctKeys = 1_000_000 });

        int undersizedFirstSeen = 0;
        int sizedFirstSeen = 0;
        for (int i = 0; i < stream; i++)
        {
            if (undersized.Observe($"b-{i}").IsFirstSeen) undersizedFirstSeen++;
            if (sized.Observe($"b-{i}").IsFirstSeen) sizedFirstSeen++;
        }

        // Every key in the stream is genuinely new, so a filter with room reports all of them as first-seen.
        Assert.Equal(stream, sizedFirstSeen);
        Assert.True(undersizedFirstSeen < stream / 10,
            $"undersized filter still reported {undersizedFirstSeen} of {stream} keys as new");

        // The same split on never-observed keys: saturated says "seen" to everything, sized says it to nothing.
        int undersizedFalsePositives = 0;
        int sizedFalsePositives = 0;
        for (int i = 0; i < 500; i++)
        {
            if (undersized.HasProbablySeen($"never-{i}")) undersizedFalsePositives++;
            if (sized.HasProbablySeen($"never-{i}")) sizedFalsePositives++;
        }

        Assert.Equal(500, undersizedFalsePositives);
        Assert.Equal(0, sizedFalsePositives);
    }

    [Fact]
    public void HasProbablySeen_ShouldReportFewerFalsePositives_WhenFirstSeenFalsePositiveRateIsTightened()
    {
        const int expectedDistinctKeys = 5_000;
        const int probes = 2_000;

        var loose = new StringAbuseTracker(new AbuseTrackerOptions { TrackFirstSeen = true, ExpectedDistinctKeys = expectedDistinctKeys, FirstSeenFalsePositiveRate = 0.5 });
        var strict = new StringAbuseTracker(new AbuseTrackerOptions { TrackFirstSeen = true, ExpectedDistinctKeys = expectedDistinctKeys, FirstSeenFalsePositiveRate = 0.0001 });

        // Fill both to exactly the load they were sized for, where the target rate is meant to hold.
        for (int i = 0; i < expectedDistinctKeys; i++)
        {
            loose.Observe($"f-{i}");
            strict.Observe($"f-{i}");
        }

        int looseFalsePositives = 0;
        int strictFalsePositives = 0;
        for (int i = 0; i < probes; i++)
        {
            if (loose.HasProbablySeen($"never-{i}")) looseFalsePositives++;
            if (strict.HasProbablySeen($"never-{i}")) strictFalsePositives++;
        }

        // Bloom filters have no false negatives, so every one of these is a false positive and the rate is the
        // only thing that can differ. The knob buys bits per key: at 0.5 a large share of never-seen keys read
        // as seen, at 0.0001 essentially none do.
        Assert.True(looseFalsePositives > probes / 10, $"loose filter produced only {looseFalsePositives} of {probes}");
        Assert.True(strictFalsePositives <= probes / 1_000, $"strict filter produced {strictFalsePositives} of {probes}");
    }

    // --- the fixed stream behind the OffenderCapacity tests -------------------------------------------------

    private const int FloodKeys = 8_000;
    private const int HeavyHitterFrequency = FloodKeys / 3 + 1;
    private const int MidTierFrequency = FloodKeys / 20;

    /// <summary>
    /// Feeds one fixed adversarial stream into a tracker built at <paramref name="offenderCapacity"/>: a flood
    /// of 8,000 rotating keys seen twice each, an "attacker" key every third iteration, and a "mid-tier" key
    /// every twentieth. Both repeat keys are interleaved through the whole stream rather than clustered at the
    /// front, because Space-Saving's eviction order is sensitive to arrival order and a real abuse stream is
    /// interleaved.
    /// </summary>
    private static StringAbuseTracker FeedSkewedStream(int offenderCapacity)
    {
        var tracker = new StringAbuseTracker(new AbuseTrackerOptions
        {
            OffenderCapacity = offenderCapacity,
            TrackFirstSeen = false,
        });

        for (int i = 0; i < FloodKeys; i++)
        {
            tracker.Observe($"rotating-{i}");
            tracker.Observe($"rotating-{i}");
            if (i % 3 == 0) tracker.Observe("heavy-hitter");
            if (i % 20 == 0) tracker.Observe("mid-tier");
        }

        return tracker;
    }
}
