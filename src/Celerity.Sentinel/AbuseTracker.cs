namespace Celerity.Sentinel;

/// <summary>
/// A streaming abuse / heavy-hitter detector that flags the busiest keys (IPs, tokens, request fingerprints) in
/// a <strong>fixed</strong> amount of memory, no matter how many distinct keys the stream contains — generic
/// over the caller's key type and a zero-cost inlined <see cref="IHashProvider{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// A single <see cref="Observe"/> call fans a key into four bounded Celerity sketches:
/// </para>
/// <list type="bullet">
/// <item><description>a <see cref="CountMinSketch{T, THasher}"/> for per-key <em>rate</em> (never underestimated);</description></item>
/// <item><description>a <see cref="TopKSketch{T, THasher}"/> (Space-Saving) for the <em>top offenders</em> in <c>O(k)</c> memory;</description></item>
/// <item><description>a <see cref="HyperLogLog{T, THasher}"/> for the <em>distinct-key volume</em>;</description></item>
/// <item><description>an optional <see cref="BloomFilter{T, THasher}"/> for a <em>first-seen</em> (new-key) signal.</description></item>
/// </list>
/// <para>
/// The categorical win over the naive approach: a <c>Dictionary&lt;TKey,int&gt;</c> (or
/// <c>ConcurrentDictionary</c>) frequency counter stores an entry per distinct key, so an attacker who rotates
/// through millions of keys grows it without bound until the process OOMs. Every structure here is sized once
/// (a couple of megabytes total at the defaults) and <strong>never grows with cardinality</strong>, so the
/// tracker <em>survives</em> exactly the adversarial input that kills the exact counter — while still surfacing
/// the true heavy hitters, because Space-Saving cannot miss a key above the <c>Total / OffenderCapacity</c>
/// threshold.
/// </para>
/// <para>
/// <b>Threading.</b> Like the underlying Celerity collections this type is single-threaded. For a concurrent
/// hot path (edge QPS), give each core/thread its own tracker and merge them periodically — see
/// <see cref="StripedAbuseTracker{TKey, THasher}"/>, which ships that pattern — using <see cref="Merge"/>, which
/// combines two trackers exactly (rate / distinct / first-seen) or with the standard Space-Saving approximation
/// (offenders). Two trackers merge when their sketch geometry and first-seen setting match; building both from
/// equal <see cref="AbuseTrackerOptions"/> is the simple way to guarantee that.
/// </para>
/// <para>
/// The rate and offender counts are cumulative since construction or the last <see cref="Clear"/>. For a
/// time-windowed rate, reset the tracker on a tumbling interval (a fresh instance, or <see cref="Clear"/>).
/// </para>
/// </remarks>
/// <typeparam name="TKey">The observed key type (IP, token, fingerprint, …).</typeparam>
/// <typeparam name="THasher">
/// The hasher used across the sketches. Must be a value type implementing <see cref="IHashProvider{T}"/> so the
/// JIT can devirtualize and inline it.
/// </typeparam>
public class AbuseTracker<TKey, THasher>
    where THasher : struct, IHashProvider<TKey>
{
    private readonly AbuseTrackerOptions _options;
    private readonly CountMinSketch<TKey, THasher> _rate;
    private readonly TopKSketch<TKey, THasher> _offenders;
    private readonly HyperLogLog<TKey, THasher> _distinct;
    private readonly BloomFilter<TKey, THasher>? _firstSeen;

    private long _totalObservations;

    // 2^-54: the largest RateConfidence for which the Count-Min delta, 1 - RateConfidence, rounds to exactly 1.
    private const double MinRateConfidence = 5.5511151231257827E-17;

    /// <summary>
    /// Initializes a new <see cref="AbuseTracker{TKey, THasher}"/> with the specified options.
    /// </summary>
    /// <param name="options">
    /// The accuracy / memory configuration, or <c>null</c> for the defaults (see <see cref="AbuseTrackerOptions"/>).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An option is out of range. The exception's <see cref="ArgumentException.ParamName"/> is <c>options</c> and
    /// its message names the option. The rejected values are: <see cref="AbuseTrackerOptions.RateEpsilon"/> outside
    /// <c>(0, 1)</c>; <see cref="AbuseTrackerOptions.RateConfidence"/> outside <c>(0, 1)</c> or at most
    /// <c>2^-54</c> (about <c>5.55e-17</c>), where <c>1 − RateConfidence</c> rounds to 1; a non-positive
    /// <see cref="AbuseTrackerOptions.OffenderCapacity"/>; a
    /// <see cref="AbuseTrackerOptions.DistinctPrecision"/> outside the supported range; a non-positive
    /// <see cref="AbuseTrackerOptions.ExpectedDistinctKeys"/> or a
    /// <see cref="AbuseTrackerOptions.FirstSeenFalsePositiveRate"/> outside <c>(0, 1)</c> while
    /// <see cref="AbuseTrackerOptions.TrackFirstSeen"/> is set (both are ignored when it is not); a
    /// <see cref="AbuseTrackerOptions.RateEpsilon"/> and <see cref="AbuseTrackerOptions.RateConfidence"/> that
    /// together need more than the rate sketch's maximum of <c>2^30</c> counters; or, while
    /// <see cref="AbuseTrackerOptions.TrackFirstSeen"/> is set, an
    /// <see cref="AbuseTrackerOptions.ExpectedDistinctKeys"/> and
    /// <see cref="AbuseTrackerOptions.FirstSeenFalsePositiveRate"/> that together need more than the
    /// first-seen Bloom filter's maximum of <c>2^30</c> bits.
    /// </exception>
    public AbuseTracker(AbuseTrackerOptions? options = null)
    {
        _options = options ?? new AbuseTrackerOptions();
        Validate(_options);

        // The range checks above leave only the two sizing ceilings for the sketches to reject, and each of those
        // depends on more than one option, so they are reported against the options rather than against the
        // sketch's own parameter names.
        try
        {
            _rate = new CountMinSketch<TKey, THasher>(_options.RateEpsilon, 1d - _options.RateConfidence);
        }
        catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "epsilon")
        {
            // A single row already exceeds the ceiling, so no RateConfidence can bring the grid under it.
            throw new ArgumentOutOfRangeException(nameof(options), _options.RateEpsilon,
                $"RateEpsilon {_options.RateEpsilon} needs a rate sketch row larger than its maximum of 2^30 counters. Increase RateEpsilon.");
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArgumentOutOfRangeException(nameof(options), _options.RateConfidence,
                $"RateEpsilon {_options.RateEpsilon} at RateConfidence {_options.RateConfidence} needs a rate sketch larger than its maximum of 2^30 counters. Increase RateEpsilon or lower RateConfidence.");
        }

        _offenders = new TopKSketch<TKey, THasher>(_options.OffenderCapacity);
        _distinct = new HyperLogLog<TKey, THasher>(_options.DistinctPrecision);
        if (_options.TrackFirstSeen)
        {
            try
            {
                _firstSeen = new BloomFilter<TKey, THasher>(_options.ExpectedDistinctKeys, _options.FirstSeenFalsePositiveRate);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new ArgumentOutOfRangeException(nameof(options), _options.ExpectedDistinctKeys,
                    $"ExpectedDistinctKeys {_options.ExpectedDistinctKeys} at FirstSeenFalsePositiveRate {_options.FirstSeenFalsePositiveRate} needs a first-seen filter larger than its maximum of 2^30 bits. Lower ExpectedDistinctKeys or raise FirstSeenFalsePositiveRate.");
            }
        }
    }

    // Every range check names the option it failed on and reports it against `options`, the only parameter the
    // caller passed; left to the sketches, the exception would name `epsilon`, `capacity` or `expectedItems`.
    private static void Validate(AbuseTrackerOptions options)
    {
        if (!(options.RateEpsilon > 0d && options.RateEpsilon < 1d))
            throw new ArgumentOutOfRangeException(nameof(options), options.RateEpsilon, "RateEpsilon must be between 0 and 1 (exclusive).");

        if (!(options.RateConfidence > 0d && options.RateConfidence < 1d))
            throw new ArgumentOutOfRangeException(nameof(options), options.RateConfidence, "RateConfidence must be between 0 and 1 (exclusive).");

        // The Count-Min delta is 1 - RateConfidence, which rounds to exactly 1 for a confidence at or below 2^-54,
        // and a delta of 1 is no sketch at all. Such a confidence is meaningless anyway, but it is inside (0, 1),
        // so it gets its own message rather than a range error that would contradict the value the caller passed.
        if (1d - options.RateConfidence >= 1d)
            throw new ArgumentOutOfRangeException(nameof(options), options.RateConfidence,
                $"RateConfidence must be greater than {MinRateConfidence:R}; at or below that, 1 - RateConfidence rounds to 1.");

        if (options.OffenderCapacity < 1)
            throw new ArgumentOutOfRangeException(nameof(options), options.OffenderCapacity, "OffenderCapacity must be at least 1.");

        if (options.DistinctPrecision < HyperLogLog<TKey, THasher>.MinPrecision || options.DistinctPrecision > HyperLogLog<TKey, THasher>.MaxPrecision)
            throw new ArgumentOutOfRangeException(nameof(options), options.DistinctPrecision,
                $"DistinctPrecision must be between {HyperLogLog<TKey, THasher>.MinPrecision} and {HyperLogLog<TKey, THasher>.MaxPrecision} inclusive.");

        if (!options.TrackFirstSeen)
            return;

        if (options.ExpectedDistinctKeys < 1)
            throw new ArgumentOutOfRangeException(nameof(options), options.ExpectedDistinctKeys, "ExpectedDistinctKeys must be at least 1.");

        if (!(options.FirstSeenFalsePositiveRate > 0d && options.FirstSeenFalsePositiveRate < 1d))
            throw new ArgumentOutOfRangeException(nameof(options), options.FirstSeenFalsePositiveRate,
                "FirstSeenFalsePositiveRate must be between 0 and 1 (exclusive).");
    }

    /// <summary>
    /// Gets the total number of observations recorded since construction or the last <see cref="Clear"/>. Saturates
    /// at <see cref="long.MaxValue"/> rather than wrapping negative, as the underlying sketches' counts do.
    /// </summary>
    public long TotalObservations => _totalObservations;

    /// <summary>Gets a value indicating whether the first-seen (new-key) signal is enabled.</summary>
    public bool TracksFirstSeen => _firstSeen is not null;

    /// <summary>
    /// Records one observation of a key, updating the rate, offender, distinct, and first-seen structures.
    /// </summary>
    /// <param name="key">The observed key.</param>
    /// <returns>Whether the key was new and its estimated frequency after this observation.</returns>
    public ObservationResult Observe(TKey key)
    {
        bool isFirstSeen = false;
        if (_firstSeen is not null)
        {
            isFirstSeen = !_firstSeen.Contains(key);
            _firstSeen.Add(key);
        }

        _rate.Add(key);
        _offenders.Add(key);
        _distinct.Add(key);
        if (_totalObservations != long.MaxValue)
            _totalObservations++;

        return new ObservationResult(isFirstSeen, _rate.EstimateCount(key));
    }

    /// <summary>Estimates how many times a key has been observed (never an underestimate).</summary>
    /// <param name="key">The key to query.</param>
    /// <returns>The estimated occurrence count.</returns>
    public long EstimateCount(TKey key) => _rate.EstimateCount(key);

    /// <summary>Estimates the number of distinct keys observed so far.</summary>
    /// <returns>The distinct-key estimate from HyperLogLog.</returns>
    public long EstimateDistinctKeys() => _distinct.EstimateCardinality();

    /// <summary>
    /// Determines whether a key has <em>probably</em> been seen before (from the first-seen Bloom filter).
    /// </summary>
    /// <param name="key">The key to test.</param>
    /// <returns>
    /// <c>false</c> if the key was definitely never observed (no false negatives); <c>true</c> if it probably
    /// was, subject to the filter's false-positive rate.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// First-seen tracking is disabled (<see cref="AbuseTrackerOptions.TrackFirstSeen"/> was <c>false</c>).
    /// </exception>
    public bool HasProbablySeen(TKey key)
    {
        if (_firstSeen is null)
            throw new InvalidOperationException("First-seen tracking is disabled; set AbuseTrackerOptions.TrackFirstSeen to true.");

        return _firstSeen.Contains(key);
    }

    /// <summary>
    /// Produces a snapshot of the current heavy hitters plus the distinct-key and total-observation counts.
    /// </summary>
    /// <param name="topN">The maximum number of offenders to include, ordered by estimated frequency descending.</param>
    /// <returns>The abuse report.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="topN"/> is negative.</exception>
    public AbuseReport<TKey> Snapshot(int topN)
    {
        if (topN < 0)
            throw new ArgumentOutOfRangeException(nameof(topN), topN, "topN must be non-negative.");

        TopKEntry<TKey>[] top = _offenders.GetTopK(topN);
        var offenders = new Offender<TKey>[top.Length];
        for (int i = 0; i < top.Length; i++)
        {
            TKey element = top[i].Element;

            // Lift the offender count to the Count-Min rate estimate. Count-Min is merged exactly (UnionWith
            // sums counters) and never underestimates, so after a Merge — where the Space-Saving count alone
            // can fall below the true union frequency for a key evicted from some lane's top-k — this keeps
            // EstimatedCount a genuine upper bound, honoring Offender's never-underestimate contract. The true
            // frequency still lies in [count - error, count]: the lower edge stays the Space-Saving bound
            // (top.Count - top.Error), which never exceeds the true frequency of the processed sub-stream.
            long rate = _rate.EstimateCount(element);
            long count = Math.Max(top[i].Count, rate);
            long error = count - (top[i].Count - top[i].Error);
            offenders[i] = new Offender<TKey>(element, count, error);
        }

        // Re-rank by the (possibly lifted) estimate so the report stays most-frequent-first.
        Array.Sort(offenders, static (a, b) => b.EstimatedCount.CompareTo(a.EstimatedCount));

        return new AbuseReport<TKey>(offenders, _distinct.EstimateCardinality(), _totalObservations);
    }

    /// <summary>
    /// Merges another tracker into this one in place, so this tracker afterwards reflects both input streams.
    /// </summary>
    /// <param name="other">The tracker to merge in. Left unmodified.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="other"/> was built with incompatible options (different sketch geometry, or a different
    /// first-seen setting), so the underlying structures cannot be combined. Every component's compatibility is
    /// checked before any of them is written to, so a rejected merge leaves this tracker exactly as it was.
    /// </exception>
    /// <remarks>
    /// The rate, distinct, and first-seen structures merge <em>exactly</em> (as if both streams had been fed to
    /// one tracker). The offenders merge with the standard Space-Saving approximation: each of
    /// <paramref name="other"/>'s monitored offenders is re-observed here with a guaranteed positive lower bound on
    /// its count — normally its estimate less its error, floored at one when a saturated count makes the two equal
    /// — which combines the heavy hitters well but is not guaranteed to reproduce the exact top-k of the union. Re-observing the lower bound is what keeps every reported <see cref="Offender{TKey}.Error"/>
    /// honest afterwards; <see cref="Snapshot"/> restores the upper bound from the exactly merged rate sketch.
    /// </remarks>
    public void Merge(AbuseTracker<TKey, THasher> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if ((_firstSeen is null) != (other._firstSeen is null))
            throw new ArgumentException("Both trackers must have the same first-seen setting to be merged.", nameof(other));

        // Check every component's geometry before writing to any of them. Each sketch's UnionWith rejects a
        // mismatch of its own, but only once the components ahead of it in the sequence have already been summed
        // in, leaving a tracker whose rate estimates no longer match its own TotalObservations and Clear() the
        // only way back. Merge is all-or-nothing, as DDSketch.Merge and RunningStatistics.Merge are.
        if (_rate.Width != other._rate.Width || _rate.Depth != other._rate.Depth)
            throw new ArgumentException("Both trackers must have the same rate sketch geometry (RateEpsilon and RateConfidence) to be merged.", nameof(other));

        if (_distinct.Precision != other._distinct.Precision)
            throw new ArgumentException("Both trackers must have the same DistinctPrecision to be merged.", nameof(other));

        // Both filters are non-null here, or both are null: the setting check above has already run.
        if (_firstSeen is not null &&
            (_firstSeen.BitCount != other._firstSeen!.BitCount || _firstSeen.HashCount != other._firstSeen.HashCount))
        {
            throw new ArgumentException("Both trackers must have the same first-seen filter geometry (ExpectedDistinctKeys and FirstSeenFalsePositiveRate) to be merged.", nameof(other));
        }

        _rate.UnionWith(other._rate);
        _distinct.UnionWith(other._distinct);
        _firstSeen?.UnionWith(other._firstSeen!);

        // Space-Saving has no exact merge: re-observe the other tracker's monitored offenders. Only the lower bound
        // (Count - Error) is known to have occurred; re-observing the upper bound would record the other side's
        // overestimate as fact, and Snapshot's [count - error, count] range would then exclude the truth. A monitored
        // key occurred at least once, so the floor of 1 is still a lower bound — and it is needed, because a count
        // saturated at long.MaxValue can hand its evictee Count == Error, which would otherwise be a rejected 0.
        foreach (TopKEntry<TKey> entry in other._offenders.GetTopK())
            _offenders.Add(entry.Element, Math.Max(1, entry.Count - entry.Error));

        // Saturate rather than wrap, as the rate and offender sketches do: a negative total would turn the
        // documented epsilon × TotalObservations overestimate bound negative. Both totals are non-negative, so a
        // negative sum can only be an overflow.
        long total = unchecked(_totalObservations + other._totalObservations);
        _totalObservations = total < 0 ? long.MaxValue : total;
    }

    /// <summary>Resets the tracker to empty, clearing every structure. Use on a tumbling window boundary.</summary>
    public void Clear()
    {
        _rate.Clear();
        _offenders.Clear();
        _distinct.Clear();
        _firstSeen?.Clear();
        _totalObservations = 0;
    }
}

/// <summary>
/// An <see cref="AbuseTracker{TKey, THasher}"/> specialized for <see cref="string"/> keys with the strong,
/// throughput-oriented <see cref="StringXxHash3Hasher"/> — the common case for IPs, tokens, and fingerprints, so
/// callers avoid spelling out the type arguments.
/// </summary>
public sealed class StringAbuseTracker : AbuseTracker<string, StringXxHash3Hasher>
{
    /// <summary>Initializes a new <see cref="StringAbuseTracker"/>.</summary>
    /// <param name="options">The accuracy / memory configuration, or <c>null</c> for the defaults.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An option is out of range; see the base constructor.
    /// </exception>
    public StringAbuseTracker(AbuseTrackerOptions? options = null)
        : base(options)
    {
    }
}
