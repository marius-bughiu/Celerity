namespace Celerity.Sentinel;

/// <summary>
/// A concurrency-friendly wrapper over several independent <see cref="AbuseTracker{TKey, THasher}"/> lanes, so a
/// multi-core hot path can record observations without a shared lock, then roll the lanes up into one report.
/// This is the recommended way to use Sentinel under real edge QPS, since a single tracker (like every Celerity
/// collection) is single-threaded.
/// </summary>
/// <remarks>
/// <para>
/// The design is per-core striping plus periodic merge — the same shape high-throughput counters use. Each lane
/// is a standalone tracker; a producer routes its observations to a lane it <strong>exclusively</strong> owns
/// (typically a per-thread or per-core index), so within a lane everything stays single-threaded and lock-free.
/// A coordinator later calls <see cref="Snapshot"/>, which merges the lanes (exact for rate / distinct /
/// first-seen, approximate for offenders — see <see cref="AbuseTracker{TKey, THasher}.Merge"/>) into a single
/// view.
/// </para>
/// <para>
/// <b>Contract.</b> A given lane index must not be observed from two threads at once, and <see cref="Snapshot"/>
/// (which reads every lane) must not run concurrently with observations on those lanes — take a rollup at a
/// coordination point, or accept that it reads a lane mid-update. This is a deliberate trade: no locks on the
/// observe hot path in exchange for a caller-managed rollup boundary.
/// </para>
/// </remarks>
/// <typeparam name="TKey">The observed key type.</typeparam>
/// <typeparam name="THasher">
/// The hasher used across the lanes. Must be a value type implementing <see cref="IHashProvider{T}"/> so the JIT
/// can devirtualize and inline it.
/// </typeparam>
public class StripedAbuseTracker<TKey, THasher>
    where THasher : struct, IHashProvider<TKey>
{
    private readonly AbuseTrackerOptions _options;
    private readonly AbuseTracker<TKey, THasher>[] _lanes;

    /// <summary>
    /// Initializes a new <see cref="StripedAbuseTracker{TKey, THasher}"/> with the given number of lanes.
    /// </summary>
    /// <param name="laneCount">
    /// The number of independent lanes — typically the core count or the size of the producer thread pool. Must
    /// be at least 1.
    /// </param>
    /// <param name="options">The per-lane configuration, or <c>null</c> for the defaults.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="laneCount"/> is less than 1.</exception>
    public StripedAbuseTracker(int laneCount, AbuseTrackerOptions? options = null)
    {
        if (laneCount < 1)
            throw new ArgumentOutOfRangeException(nameof(laneCount), laneCount, "Lane count must be at least 1.");

        _options = options ?? new AbuseTrackerOptions();
        _lanes = new AbuseTracker<TKey, THasher>[laneCount];
        for (int i = 0; i < laneCount; i++)
            _lanes[i] = new AbuseTracker<TKey, THasher>(_options);
    }

    /// <summary>Gets the number of lanes.</summary>
    public int LaneCount => _lanes.Length;

    /// <summary>
    /// Gets the tracker for a lane, so a producer that owns a lane can observe into it directly.
    /// </summary>
    /// <param name="lane">The lane index, in <c>[0, LaneCount)</c>.</param>
    /// <returns>The lane's tracker.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lane"/> is out of range.</exception>
    public AbuseTracker<TKey, THasher> Lane(int lane)
    {
        if ((uint)lane >= (uint)_lanes.Length)
            throw new ArgumentOutOfRangeException(nameof(lane), lane, "Lane index is out of range.");

        return _lanes[lane];
    }

    /// <summary>
    /// Records one observation into the given lane. The caller must own that lane (no two threads observing the
    /// same lane concurrently).
    /// </summary>
    /// <param name="lane">The lane index, in <c>[0, LaneCount)</c>.</param>
    /// <param name="key">The observed key.</param>
    /// <returns>Whether the key was new to that lane and its estimated frequency within it.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lane"/> is out of range.</exception>
    public ObservationResult Observe(int lane, TKey key)
    {
        if ((uint)lane >= (uint)_lanes.Length)
            throw new ArgumentOutOfRangeException(nameof(lane), lane, "Lane index is out of range.");

        return _lanes[lane].Observe(key);
    }

    /// <summary>
    /// Merges every lane into a single tracker and returns its report. Must not run concurrently with
    /// observations on the lanes.
    /// </summary>
    /// <param name="topN">The maximum number of offenders to include.</param>
    /// <returns>The combined abuse report across all lanes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="topN"/> is negative.</exception>
    public AbuseReport<TKey> Snapshot(int topN)
    {
        if (topN < 0)
            throw new ArgumentOutOfRangeException(nameof(topN), topN, "topN must be non-negative.");

        var merged = new AbuseTracker<TKey, THasher>(_options);
        for (int i = 0; i < _lanes.Length; i++)
            merged.Merge(_lanes[i]);

        return merged.Snapshot(topN);
    }

    /// <summary>Resets every lane to empty. Must not run concurrently with observations.</summary>
    public void Clear()
    {
        for (int i = 0; i < _lanes.Length; i++)
            _lanes[i].Clear();
    }
}

/// <summary>
/// A <see cref="StripedAbuseTracker{TKey, THasher}"/> specialized for <see cref="string"/> keys with the
/// <see cref="StringXxHash3Hasher"/>.
/// </summary>
public sealed class StringStripedAbuseTracker : StripedAbuseTracker<string, StringXxHash3Hasher>
{
    /// <summary>Initializes a new <see cref="StringStripedAbuseTracker"/>.</summary>
    /// <param name="laneCount">The number of lanes; see the base constructor.</param>
    /// <param name="options">The per-lane configuration, or <c>null</c> for the defaults.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="laneCount"/> is less than 1.</exception>
    public StringStripedAbuseTracker(int laneCount, AbuseTrackerOptions? options = null)
        : base(laneCount, options)
    {
    }
}
