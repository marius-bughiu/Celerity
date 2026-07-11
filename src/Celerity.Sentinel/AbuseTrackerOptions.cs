namespace Celerity.Sentinel;

/// <summary>
/// Configuration for an <see cref="AbuseTracker{TKey, THasher}"/>: the accuracy / memory trade-offs of the four
/// bounded sketches it fans each observation into. Every setting sizes a fixed-memory structure, so the whole
/// tracker's footprint is a constant chosen here — it never grows with the number of distinct keys, which is
/// the property that lets it survive an attacker rotating keys where a <c>Dictionary&lt;TKey,int&gt;</c> counter
/// would grow unbounded and OOM.
/// </summary>
/// <remarks>
/// Two trackers must be constructed with equal options to be merged (see
/// <see cref="AbuseTracker{TKey, THasher}.Merge"/>), because a merge combines the underlying sketches, which
/// requires identical geometry.
/// </remarks>
public sealed class AbuseTrackerOptions
{
    /// <summary>
    /// The Count-Min rate sketch's relative error factor (<c>epsilon</c>), strictly between 0 and 1. A per-key
    /// rate estimate never underestimates and overestimates by at most <c>epsilon × TotalObservations</c>.
    /// Smaller widens the sketch (more memory, tighter estimates). Default <c>0.001</c>.
    /// </summary>
    public double RateEpsilon { get; init; } = 0.001;

    /// <summary>
    /// The confidence that a rate estimate stays within its <see cref="RateEpsilon"/> bound, strictly between 0
    /// and 1 (the Count-Min <c>delta</c> is <c>1 − RateConfidence</c>). Higher adds rows to the sketch. Default
    /// <c>0.99</c>.
    /// </summary>
    public double RateConfidence { get; init; } = 0.99;

    /// <summary>
    /// The number of distinct offenders the Space-Saving sketch monitors at once (<c>k</c>) — its memory is
    /// <c>O(k)</c> regardless of stream cardinality, and it never misses a key whose true frequency exceeds
    /// <c>TotalObservations / OffenderCapacity</c>. Default <c>128</c>.
    /// </summary>
    public int OffenderCapacity { get; init; } = 128;

    /// <summary>
    /// The HyperLogLog precision used to estimate the distinct-key count from a fixed register array (memory is
    /// <c>2^precision</c> bytes, error ≈ <c>1.04 / sqrt(2^precision)</c>). Default <c>14</c> (16&#160;KB, ≈0.8%
    /// error).
    /// </summary>
    public int DistinctPrecision { get; init; } = 14;

    /// <summary>
    /// Whether to maintain a Bloom first-seen filter so <see cref="AbuseTracker{TKey, THasher}.Observe"/> can
    /// report whether a key had never been seen before (new-key friction). Default <c>true</c>.
    /// </summary>
    /// <remarks>
    /// A Bloom filter is sized for <see cref="ExpectedDistinctKeys"/> and saturates beyond it (everything then
    /// reads as "seen"). For an unbounded stream, reset the tracker on a tumbling interval (see
    /// <see cref="AbuseTracker{TKey, THasher}.Clear"/>) to keep the first-seen signal meaningful.
    /// </remarks>
    public bool TrackFirstSeen { get; init; } = true;

    /// <summary>
    /// The number of distinct keys the first-seen Bloom filter is sized for. Ignored when
    /// <see cref="TrackFirstSeen"/> is <c>false</c>. Default <c>1,000,000</c>.
    /// </summary>
    public int ExpectedDistinctKeys { get; init; } = 1_000_000;

    /// <summary>
    /// The target false-positive rate of the first-seen Bloom filter (the chance an actually-new key is reported
    /// as already seen). Ignored when <see cref="TrackFirstSeen"/> is <c>false</c>. Default <c>0.01</c>.
    /// </summary>
    public double FirstSeenFalsePositiveRate { get; init; } = 0.01;
}
