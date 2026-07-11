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
/// (offenders). Two trackers must be built with equal <see cref="AbuseTrackerOptions"/> to merge.
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

    /// <summary>
    /// Initializes a new <see cref="AbuseTracker{TKey, THasher}"/> with the specified options.
    /// </summary>
    /// <param name="options">
    /// The accuracy / memory configuration, or <c>null</c> for the defaults (see <see cref="AbuseTrackerOptions"/>).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// An option is out of range (for example <see cref="AbuseTrackerOptions.RateEpsilon"/> or
    /// <see cref="AbuseTrackerOptions.RateConfidence"/> outside <c>(0, 1)</c>, a non-positive
    /// <see cref="AbuseTrackerOptions.OffenderCapacity"/>, or a
    /// <see cref="AbuseTrackerOptions.DistinctPrecision"/> outside the supported range).
    /// </exception>
    public AbuseTracker(AbuseTrackerOptions? options = null)
    {
        _options = options ?? new AbuseTrackerOptions();
        if (_options.RateConfidence <= 0d || _options.RateConfidence >= 1d)
            throw new ArgumentOutOfRangeException(nameof(options), _options.RateConfidence, "RateConfidence must be between 0 and 1 (exclusive).");

        _rate = new CountMinSketch<TKey, THasher>(_options.RateEpsilon, 1d - _options.RateConfidence);
        _offenders = new TopKSketch<TKey, THasher>(_options.OffenderCapacity);
        _distinct = new HyperLogLog<TKey, THasher>(_options.DistinctPrecision);
        _firstSeen = _options.TrackFirstSeen
            ? new BloomFilter<TKey, THasher>(_options.ExpectedDistinctKeys, _options.FirstSeenFalsePositiveRate)
            : null;
    }

    /// <summary>Gets the total number of observations recorded since construction or the last <see cref="Clear"/>.</summary>
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
    /// first-seen setting), so the underlying structures cannot be combined.
    /// </exception>
    /// <remarks>
    /// The rate, distinct, and first-seen structures merge <em>exactly</em> (as if both streams had been fed to
    /// one tracker). The offenders merge with the standard Space-Saving approximation: each of
    /// <paramref name="other"/>'s monitored offenders is re-observed here with its estimated count, which
    /// combines the heavy hitters well but is not guaranteed to reproduce the exact top-k of the union.
    /// </remarks>
    public void Merge(AbuseTracker<TKey, THasher> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if ((_firstSeen is null) != (other._firstSeen is null))
            throw new ArgumentException("Both trackers must have the same first-seen setting to be merged.", nameof(other));

        _rate.UnionWith(other._rate);
        _distinct.UnionWith(other._distinct);
        _firstSeen?.UnionWith(other._firstSeen!);

        // Space-Saving has no exact merge: re-observe the other tracker's monitored offenders with their counts.
        foreach (TopKEntry<TKey> entry in other._offenders.GetTopK())
            _offenders.Add(entry.Element, entry.Count);

        _totalObservations += other._totalObservations;
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
    public StringAbuseTracker(AbuseTrackerOptions? options = null)
        : base(options)
    {
    }
}
