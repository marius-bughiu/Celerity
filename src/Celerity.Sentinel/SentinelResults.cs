namespace Celerity.Sentinel;

/// <summary>
/// The outcome of a single <see cref="AbuseTracker{TKey, THasher}.Observe"/> call: whether the key was new and
/// its current estimated frequency.
/// </summary>
public readonly struct ObservationResult
{
    /// <summary>Initializes a new <see cref="ObservationResult"/>.</summary>
    /// <param name="isFirstSeen">Whether the key had not been observed before this call.</param>
    /// <param name="estimatedCount">The key's estimated frequency after this observation.</param>
    public ObservationResult(bool isFirstSeen, long estimatedCount)
    {
        IsFirstSeen = isFirstSeen;
        EstimatedCount = estimatedCount;
    }

    /// <summary>
    /// Gets a value indicating whether this observation was the first time the key was seen (from the Bloom
    /// first-seen filter). Always <c>false</c> when first-seen tracking is disabled. Subject to the filter's
    /// false-positive rate — a genuinely new key can occasionally read as already seen.
    /// </summary>
    public bool IsFirstSeen { get; }

    /// <summary>
    /// Gets the key's estimated frequency (from the Count-Min rate sketch) after this observation. Never an
    /// underestimate of the true count.
    /// </summary>
    public long EstimatedCount { get; }
}

/// <summary>
/// A single heavy hitter in an <see cref="AbuseReport{TKey}"/>: a key, its estimated frequency, and the bound on
/// how much that estimate may overshoot the truth.
/// </summary>
/// <typeparam name="TKey">The key type.</typeparam>
public readonly struct Offender<TKey>
{
    /// <summary>Initializes a new <see cref="Offender{TKey}"/>.</summary>
    /// <param name="key">The offending key.</param>
    /// <param name="estimatedCount">The estimated occurrence count (an upper bound on the true frequency).</param>
    /// <param name="error">The maximum amount <paramref name="estimatedCount"/> overestimates the truth.</param>
    public Offender(TKey key, long estimatedCount, long error)
    {
        Key = key;
        EstimatedCount = estimatedCount;
        Error = error;
    }

    /// <summary>Gets the offending key.</summary>
    public TKey Key { get; }

    /// <summary>
    /// Gets the estimated number of occurrences of <see cref="Key"/> — an upper bound on its true frequency
    /// (never an underestimate).
    /// </summary>
    public long EstimatedCount { get; }

    /// <summary>
    /// Gets the maximum amount by which <see cref="EstimatedCount"/> may exceed the true frequency; the truth
    /// lies in <c>[EstimatedCount − Error, EstimatedCount]</c>.
    /// </summary>
    public long Error { get; }

    /// <summary>Returns a string of the form <c>Key (count, err error)</c>.</summary>
    /// <returns>A human-readable representation of the offender.</returns>
    public override string ToString() => $"{Key} ({EstimatedCount}, err {Error})";
}

/// <summary>
/// A point-in-time snapshot from an <see cref="AbuseTracker{TKey, THasher}"/>: the ranked heavy hitters plus the
/// stream's distinct-key and total-observation counts.
/// </summary>
/// <typeparam name="TKey">The key type.</typeparam>
public sealed class AbuseReport<TKey>
{
    /// <summary>Initializes a new <see cref="AbuseReport{TKey}"/>.</summary>
    /// <param name="offenders">The heavy hitters, most frequent first.</param>
    /// <param name="distinctKeys">The estimated number of distinct keys observed.</param>
    /// <param name="totalObservations">The total number of observations recorded.</param>
    public AbuseReport(IReadOnlyList<Offender<TKey>> offenders, long distinctKeys, long totalObservations)
    {
        Offenders = offenders;
        DistinctKeys = distinctKeys;
        TotalObservations = totalObservations;
    }

    /// <summary>Gets the heavy hitters, ordered by estimated frequency descending.</summary>
    public IReadOnlyList<Offender<TKey>> Offenders { get; }

    /// <summary>Gets the estimated number of distinct keys observed (from HyperLogLog).</summary>
    public long DistinctKeys { get; }

    /// <summary>Gets the total number of observations recorded since construction or the last reset.</summary>
    public long TotalObservations { get; }
}
