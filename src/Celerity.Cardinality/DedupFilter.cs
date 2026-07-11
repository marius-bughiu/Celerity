namespace Celerity.Cardinality;

/// <summary>
/// A fixed-memory, removable deduplication filter — "have I already seen this key?" — for streams where the
/// HyperLogLog error of <see cref="Distinct{TKey, THasher}"/> is unacceptable and you need a per-key answer that
/// can also <em>expire</em>. Generic over the caller's key type and a zero-cost inlined
/// <see cref="IHashProvider{T}"/>, backed by a Celerity <see cref="CuckooFilter{T, THasher}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TryMarkSeen"/> is the dedup primitive: it returns <c>true</c> the first time a key is presented
/// and <c>false</c> for a repeat, in a footprint that never grows with the stream. Because it stores short
/// fingerprints rather than keys it has <strong>no false negatives</strong> (a repeat is never missed) and a
/// small, tunable false-positive rate (a genuinely new key can occasionally be treated as a duplicate — so
/// this suits dedup where dropping a rare non-duplicate is acceptable, not exact set membership).
/// </para>
/// <para>
/// Unlike a Bloom filter it supports <see cref="Remove"/>, so it fits a <em>sliding window</em>: mark keys as
/// they arrive and remove them as they age out, keeping the filter's fill (and false-positive rate) bounded to
/// the live window rather than the whole stream. Only remove keys you actually marked.
/// </para>
/// </remarks>
/// <typeparam name="TKey">The key type being deduplicated.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to hash keys. Must be a value type implementing <see cref="IHashProvider{T}"/> so the JIT can
/// devirtualize and inline it.
/// </typeparam>
public class DedupFilter<TKey, THasher>
    where THasher : struct, IHashProvider<TKey>
{
    /// <summary>The default target false-positive rate used when a constructor does not specify one: 1%.</summary>
    public const double DefaultFalsePositiveRate = 0.01;

    private readonly CuckooFilter<TKey, THasher> _filter;

    /// <summary>
    /// Initializes a new <see cref="DedupFilter{TKey, THasher}"/> sized for the expected number of live keys.
    /// </summary>
    /// <param name="expectedItems">
    /// The number of distinct keys expected to be live at once (the window size for a sliding window). Must be
    /// positive.
    /// </param>
    /// <param name="falsePositiveRate">
    /// The target probability a genuinely new key is treated as a duplicate. Must be strictly between 0 and 1.
    /// Default <see cref="DefaultFalsePositiveRate"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="expectedItems"/> is not positive, or <paramref name="falsePositiveRate"/> is not strictly
    /// between 0 and 1.
    /// </exception>
    public DedupFilter(int expectedItems, double falsePositiveRate = DefaultFalsePositiveRate)
    {
        _filter = new CuckooFilter<TKey, THasher>(expectedItems, falsePositiveRate);
    }

    /// <summary>Gets the number of keys currently marked in the filter.</summary>
    public int Count => _filter.Count;

    /// <summary>Gets the expected live-key count the filter was sized for.</summary>
    public int Capacity => _filter.Capacity;

    /// <summary>Gets the target false-positive rate the filter was sized for.</summary>
    public double FalsePositiveRate => _filter.FalsePositiveRate;

    /// <summary>
    /// Gets a value indicating whether the filter is full (an insertion exhausted its eviction budget). While
    /// <c>true</c>, <see cref="TryMarkSeen"/> throws for a new key; <see cref="Remove"/> keys to free space.
    /// </summary>
    public bool IsFull => _filter.IsFull;

    /// <summary>
    /// Determines whether a key has <em>probably</em> been marked, without changing the filter.
    /// </summary>
    /// <param name="key">The key to test.</param>
    /// <returns>
    /// <c>false</c> if the key was definitely never marked (no false negatives); <c>true</c> if it probably was,
    /// subject to the false-positive rate.
    /// </returns>
    public bool Contains(TKey key) => _filter.Contains(key);

    /// <summary>
    /// Marks a key as seen if it has not (probably) been seen before — the deduplication primitive.
    /// </summary>
    /// <param name="key">The key to deduplicate.</param>
    /// <returns>
    /// <c>true</c> if the key is new (it was not present and has now been marked); <c>false</c> if it was already
    /// present (a duplicate, or — with the filter's false-positive probability — a fingerprint collision).
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The filter is full and cannot mark a new key; <see cref="Remove"/> aged-out keys or size the filter larger.
    /// </exception>
    public bool TryMarkSeen(TKey key)
    {
        if (_filter.Contains(key))
            return false;

        if (!_filter.TryAdd(key))
            throw new InvalidOperationException("The dedup filter is full; remove aged-out keys or size it larger.");

        return true;
    }

    /// <summary>
    /// Removes one marking of a key so it can be seen fresh again — use as keys age out of a sliding window.
    /// </summary>
    /// <param name="key">The key to remove. Only remove keys you previously marked.</param>
    /// <returns><c>true</c> if a marking was found and removed; <c>false</c> if the key was definitely never marked.</returns>
    public bool Remove(TKey key) => _filter.Remove(key);

    /// <summary>Resets the filter to empty, clearing every marked key.</summary>
    public void Clear() => _filter.Clear();

    /// <summary>
    /// Merges another dedup filter into this one, so this filter afterwards reports every key either filter had.
    /// Both must have been constructed with identical geometry (same expected item count and false-positive rate).
    /// </summary>
    /// <param name="other">The filter to merge in. Left unmodified.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="other"/> has incompatible geometry.</exception>
    /// <exception cref="InvalidOperationException">This filter becomes full before every key from <paramref name="other"/> is absorbed.</exception>
    public void UnionWith(DedupFilter<TKey, THasher> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        _filter.UnionWith(other._filter);
    }
}

/// <summary>
/// A <see cref="DedupFilter{TKey, THasher}"/> specialized for <see cref="string"/> keys with the
/// <see cref="StringXxHash3Hasher"/>.
/// </summary>
public sealed class StringDedupFilter : DedupFilter<string, StringXxHash3Hasher>
{
    /// <summary>Initializes a new <see cref="StringDedupFilter"/>.</summary>
    /// <param name="expectedItems">The expected live-key count; see the base constructor.</param>
    /// <param name="falsePositiveRate">The target false-positive rate; see the base constructor.</param>
    /// <exception cref="ArgumentOutOfRangeException">An argument is out of range; see the base constructor.</exception>
    public StringDedupFilter(int expectedItems, double falsePositiveRate = DefaultFalsePositiveRate)
        : base(expectedItems, falsePositiveRate)
    {
    }
}
