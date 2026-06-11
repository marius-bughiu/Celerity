namespace Celerity.Hashing;

/// <summary>
/// An immutable summary of how a hash function behaves <em>inside an open-addressed,
/// power-of-two, linearly-probed table</em> — the layout every Celerity dictionary and
/// set actually uses — produced by <see cref="ProbeStatisticsEvaluator"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the complement to <see cref="HashQualityReport"/>. That report models a
/// <em>separate-chaining</em> table (one counter per bucket), so its <see cref="HashQualityReport.MaxBucketLoad"/>
/// measures how many keys <em>share</em> a slot. A real Celerity table does not chain — it
/// linearly probes the next slot on a collision, which makes neighbouring clusters
/// <em>merge</em> (primary clustering), so the cost a lookup actually pays is the
/// <em>probe length</em>: the number of slots it must read, starting at the key's natural
/// slot, before it finds the key.
/// </para>
/// <para>
/// The metrics here are computed by replaying the exact placement
/// (<c>index = hash &amp; (TableSize - 1)</c>, then linear <c>(index + 1) &amp; mask</c> on a
/// collision) the collections use, so <see cref="AverageProbeLength"/> /
/// <see cref="MaxProbeLength"/> are the number of probes a successful lookup performs on
/// that key set at that table size — the metric users feel as insert/lookup throughput.
/// </para>
/// </remarks>
public readonly struct ProbeStatistics
{
    internal ProbeStatistics(
        int keyCount,
        int entryCount,
        int duplicateKeyCount,
        int tableSize,
        double loadFactor,
        int collisionCount,
        double collisionRate,
        long totalProbeLength,
        double averageProbeLength,
        int maxProbeLength)
    {
        KeyCount = keyCount;
        EntryCount = entryCount;
        DuplicateKeyCount = duplicateKeyCount;
        TableSize = tableSize;
        LoadFactor = loadFactor;
        CollisionCount = collisionCount;
        CollisionRate = collisionRate;
        TotalProbeLength = totalProbeLength;
        AverageProbeLength = averageProbeLength;
        MaxProbeLength = maxProbeLength;
    }

    /// <summary>The number of keys read from the input sequence (duplicates included).</summary>
    public int KeyCount { get; }

    /// <summary>
    /// The number of distinct entries actually placed in the table
    /// (<see cref="KeyCount"/> minus <see cref="DuplicateKeyCount"/>). Probe-length and
    /// collision metrics are computed over these entries.
    /// </summary>
    public int EntryCount { get; }

    /// <summary>
    /// The number of input keys that were equal to an already-placed key and therefore not
    /// inserted a second time, matching the dictionaries' set semantics.
    /// </summary>
    public int DuplicateKeyCount { get; }

    /// <summary>
    /// The power-of-two table size the entries were placed into — sized, including
    /// load-factor headroom, exactly as the Celerity collections size their backing arrays.
    /// </summary>
    public int TableSize { get; }

    /// <summary>
    /// The achieved fill ratio (<see cref="EntryCount"/> divided by <see cref="TableSize"/>).
    /// Probe length grows steeply as this approaches <c>1.0</c>.
    /// </summary>
    public double LoadFactor { get; }

    /// <summary>
    /// The number of entries that did <em>not</em> land in their natural slot
    /// (<c>hash &amp; mask</c> was already occupied by a different key), i.e. entries whose
    /// probe length is greater than one. This is the open-addressing notion of a collision,
    /// and unlike <see cref="HashQualityReport.CollisionCount"/> it accounts for primary
    /// clustering, not just raw-code or bucket coincidences.
    /// </summary>
    public int CollisionCount { get; }

    /// <summary>
    /// <see cref="CollisionCount"/> as a fraction of <see cref="EntryCount"/>, in <c>[0, 1]</c>.
    /// Zero when no entries were placed.
    /// </summary>
    public double CollisionRate { get; }

    /// <summary>The sum of every placed entry's probe length — the total work a full scan of lookups would do.</summary>
    public long TotalProbeLength { get; }

    /// <summary>
    /// The mean number of probes a successful lookup performs
    /// (<see cref="TotalProbeLength"/> divided by <see cref="EntryCount"/>). <c>1.0</c> means
    /// every entry sits in its natural slot (no probing); higher is worse. Zero when no
    /// entries were placed.
    /// </summary>
    public double AverageProbeLength { get; }

    /// <summary>
    /// The worst-case probe length across all entries — the longest run a single lookup can
    /// walk. This is the number that turns into a tail-latency spike, and the one an
    /// adversarial key set is built to inflate.
    /// </summary>
    public int MaxProbeLength { get; }

    /// <summary>Returns a single-line, human-readable summary of the report.</summary>
    /// <returns>A formatted string containing the key metrics.</returns>
    public override string ToString() =>
        $"Entries={EntryCount}/{KeyCount}, TableSize={TableSize}, Load={LoadFactor:F3}, " +
        $"Collisions={CollisionCount} ({CollisionRate:P2}), " +
        $"AvgProbe={AverageProbeLength:F3}, MaxProbe={MaxProbeLength}";
}
