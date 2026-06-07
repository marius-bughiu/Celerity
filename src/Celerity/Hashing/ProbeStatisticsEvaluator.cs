namespace Celerity.Hashing;

/// <summary>
/// Measures how an <see cref="IHashProvider{T}"/> behaves when its keys are placed into an
/// open-addressed, power-of-two, linearly-probed table — the layout the Celerity collections
/// actually use — returning a <see cref="ProbeStatistics"/> of probe-length and collision metrics.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="HashQualityEvaluator"/> answers "how evenly does this hasher spread codes across
/// buckets?" using a separate-chaining model (one counter per bucket). That is the right question
/// for a chained table, but Celerity's tables linearly probe: on a collision they walk to the next
/// slot, so adjacent clusters merge and the cost a lookup pays is its <em>probe length</em>, not the
/// load of a single bucket. This evaluator answers the question users actually feel — "how many slots
/// does a lookup read on average / in the worst case?" — by replaying the real placement.
/// </para>
/// <para>
/// It is a diagnostic / benchmarking tool, not a hot-path API: it materializes the key sequence and
/// allocates a table-sized working buffer, so call it offline (in tests, benchmarks, or a one-off
/// analysis) to compare candidate hashers for a given key shape. The placement uses
/// <c>index = hash &amp; (TableSize - 1)</c> and linear <c>(index + 1) &amp; mask</c> probing, byte-for-byte
/// the scheme in <see cref="Collections.IntDictionary{TValue}"/> and friends, so the reported numbers
/// are the probe lengths a real table of that size would exhibit.
/// </para>
/// </remarks>
public static class ProbeStatisticsEvaluator
{
    /// <summary>The load factor used when the caller does not specify one. Matches the collections' default.</summary>
    public const float DefaultLoadFactor = 0.75f;

    /// <summary>
    /// Places every key in <paramref name="keys"/> into a linearly-probed table sized for the key
    /// count at <paramref name="loadFactor"/> and reports the resulting probe-length distribution.
    /// </summary>
    /// <typeparam name="T">The key type.</typeparam>
    /// <typeparam name="THasher">The hash provider to evaluate. Instantiated via <c>default</c>; the built-in hashers are stateless structs.</typeparam>
    /// <param name="keys">
    /// The sample of keys to place. Keys equal to an already-placed key (by
    /// <see cref="System.Collections.Generic.EqualityComparer{T}.Default"/>) are counted as
    /// duplicates and not inserted a second time, matching the dictionaries' set semantics.
    /// </param>
    /// <param name="capacity">
    /// An optional minimum table capacity. The final <see cref="ProbeStatistics.TableSize"/> is the
    /// larger of this and the key-count-with-load-factor-headroom, rounded up to the next power of two
    /// — the same sizing the collections' bulk constructors use. When <see langword="null"/> the size
    /// is driven entirely by the key count.
    /// </param>
    /// <param name="loadFactor">
    /// The target fill ratio used to size the table, in the open interval <c>(0, 1)</c>. Defaults to
    /// <see cref="DefaultLoadFactor"/>.
    /// </param>
    /// <returns>A <see cref="ProbeStatistics"/> describing the probe-length distribution.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="keys"/> is <see langword="null"/>.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative, or <paramref name="loadFactor"/> is not in <c>(0, 1)</c>.
    /// </exception>
    public static ProbeStatistics Evaluate<T, THasher>(
        IEnumerable<T> keys,
        int? capacity = null,
        float loadFactor = DefaultLoadFactor)
        where THasher : struct, IHashProvider<T>
    {
        if (keys is null)
        {
            throw new ArgumentNullException(nameof(keys));
        }

        if (capacity is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        }

        // Written as the negation of the valid range so NaN (for which every comparison is false) is
        // rejected too — otherwise a NaN load factor would slip through, size the table to 1, and the
        // linear probe would loop forever on the second key.
        if (!(loadFactor > 0f && loadFactor < 1f))
        {
            throw new ArgumentOutOfRangeException(nameof(loadFactor), loadFactor, "Load factor must be between 0 (exclusive) and 1 (exclusive).");
        }

        // Materialize so the count is known before sizing (mirrors the bulk constructor reading
        // ICollection<T>.Count) and so a one-shot enumerable can be walked exactly once.
        List<T> materialized = new(keys);
        int keyCount = materialized.Count;

        // Size for the key count *including* load-factor headroom — exactly as the collections'
        // IEnumerable constructors do (issue #27) — so the achieved load matches a real table and
        // there is always at least one empty slot for the probe loop to terminate on.
        int desired = capacity ?? 0;
        if (keyCount > 0)
        {
            int withHeadroom = (int)Math.Ceiling(keyCount / (double)loadFactor);
            if (withHeadroom > desired)
            {
                desired = withHeadroom;
            }
        }

        int tableSize = FastUtils.NextPowerOfTwo(Math.Max(1, desired));
        int mask = tableSize - 1;

        bool[] occupied = new bool[tableSize];
        T[] slots = new T[tableSize];
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        THasher hasher = default;

        int entryCount = 0;
        int duplicateCount = 0;
        int collisionCount = 0;
        long totalProbeLength = 0;
        int maxProbeLength = 0;

        foreach (T key in materialized)
        {
            int index = hasher.Hash(key) & mask;
            int probeLength = 1;
            bool duplicate = false;

            while (occupied[index])
            {
                if (comparer.Equals(slots[index], key))
                {
                    duplicate = true;
                    break;
                }

                index = (index + 1) & mask;
                probeLength++;
            }

            if (duplicate)
            {
                duplicateCount++;
                continue;
            }

            occupied[index] = true;
            slots[index] = key;
            entryCount++;
            totalProbeLength += probeLength;
            if (probeLength > 1)
            {
                collisionCount++;
            }

            if (probeLength > maxProbeLength)
            {
                maxProbeLength = probeLength;
            }
        }

        double loadFactorAchieved = (double)entryCount / tableSize;
        double averageProbeLength = entryCount == 0 ? 0.0 : (double)totalProbeLength / entryCount;
        double collisionRate = entryCount == 0 ? 0.0 : (double)collisionCount / entryCount;

        return new ProbeStatistics(
            keyCount,
            entryCount,
            duplicateCount,
            tableSize,
            loadFactorAchieved,
            collisionCount,
            collisionRate,
            totalProbeLength,
            averageProbeLength,
            maxProbeLength);
    }
}
