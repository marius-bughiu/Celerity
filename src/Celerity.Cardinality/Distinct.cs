namespace Celerity.Cardinality;

/// <summary>
/// A mergeable distinct-count estimator — approximate <c>COUNT(DISTINCT)</c> — that is <strong>exact while
/// small</strong> and switches to a fixed ~16&#160;KB <see cref="HyperLogLog{T, THasher}"/> once the distinct
/// count crosses a threshold. Generic over the caller's key type and a zero-cost inlined
/// <see cref="IHashProvider{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// The win is categorical, not a constant factor. Counting distinct values over a large or unbounded stream
/// with a <see cref="HashSet{T}"/> stores every distinct value — tens of gigabytes and an eventual OOM at high
/// cardinality. This estimator holds a flat, fixed register array whose size never grows with the data, so it
/// is a <em>run-vs-cannot-run</em> difference: it estimates a billion distinct keys from the same 16&#160;KB it
/// uses for a thousand.
/// </para>
/// <para>
/// Below <see cref="ExactThreshold"/> distinct keys it keeps an exact <see cref="CeleritySet{T, THasher}"/>, so
/// small streams get a <em>precise</em> count with no estimation error (the common "did anything actually
/// duplicate?" case). The first time the distinct count exceeds the threshold it promotes to HyperLogLog and
/// releases the exact set, capping memory from then on. <see cref="IsExact"/> reports which mode it is in.
/// </para>
/// <para>
/// It is mergeable: <see cref="Merge"/> combines two estimators for cross-shard / cross-window roll-ups. When
/// both are still exact and stay under the threshold the merge is exact; otherwise it takes the HyperLogLog
/// union (the per-register maximum), which is byte-identical across runtimes given a deterministic hasher — so
/// shard sub-totals combine to the same global estimate on every machine. Estimators must share the same
/// <see cref="Precision"/> to merge.
/// </para>
/// </remarks>
/// <typeparam name="TKey">The key type whose distinct count is estimated.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to hash keys. Must be a value type implementing <see cref="IHashProvider{T}"/> so the JIT can
/// devirtualize and inline it. Use a deterministic hasher for identical cross-shard merges.
/// </typeparam>
public class Distinct<TKey, THasher>
    where THasher : struct, IHashProvider<TKey>
{
    /// <summary>The default HyperLogLog precision used when a constructor does not specify one (16&#160;KB, ≈0.8% error).</summary>
    public const int DefaultPrecision = 14;

    /// <summary>The default distinct-count threshold at which the exact set promotes to HyperLogLog.</summary>
    public const int DefaultExactThreshold = 2048;

    private readonly int _precision;
    private readonly int _exactThreshold;

    private CeleritySet<TKey, THasher>? _exact;
    private HyperLogLog<TKey, THasher>? _hll;

    /// <summary>
    /// Initializes a new, empty <see cref="Distinct{TKey, THasher}"/> in exact mode.
    /// </summary>
    /// <param name="precision">
    /// The HyperLogLog register-index precision used once the estimator promotes (memory is <c>2^precision</c>
    /// bytes, relative standard error ≈ <c>1.04 / sqrt(2^precision)</c>). Must be in the range supported by
    /// <see cref="HyperLogLog{T, THasher}"/>. Default <see cref="DefaultPrecision"/>.
    /// </param>
    /// <param name="exactThreshold">
    /// The distinct-key count above which the exact set promotes to HyperLogLog. Below it the count is precise.
    /// Must be non-negative. Default <see cref="DefaultExactThreshold"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="precision"/> is outside the range supported by <see cref="HyperLogLog{T, THasher}"/>, or
    /// <paramref name="exactThreshold"/> is negative.
    /// </exception>
    public Distinct(int precision = DefaultPrecision, int exactThreshold = DefaultExactThreshold)
    {
        if (precision < HyperLogLog<TKey, THasher>.MIN_PRECISION || precision > HyperLogLog<TKey, THasher>.MAX_PRECISION)
            throw new ArgumentOutOfRangeException(nameof(precision), precision,
                $"Precision must be between {HyperLogLog<TKey, THasher>.MIN_PRECISION} and {HyperLogLog<TKey, THasher>.MAX_PRECISION} inclusive.");
        if (exactThreshold < 0)
            throw new ArgumentOutOfRangeException(nameof(exactThreshold), exactThreshold, "Exact threshold must be non-negative.");

        _precision = precision;
        _exactThreshold = exactThreshold;
        _exact = new CeleritySet<TKey, THasher>();
    }

    /// <summary>Gets the HyperLogLog precision the estimator promotes to.</summary>
    public int Precision => _precision;

    /// <summary>Gets the distinct-count threshold at which the exact set promotes to HyperLogLog.</summary>
    public int ExactThreshold => _exactThreshold;

    /// <summary>
    /// Gets a value indicating whether the estimator is still in exact mode (its count is precise). Becomes
    /// <c>false</c> once the distinct count has crossed <see cref="ExactThreshold"/> and it promoted to
    /// HyperLogLog.
    /// </summary>
    public bool IsExact => _hll is null;

    /// <summary>
    /// Gets the estimator's relative standard error: <c>0</c> while exact, otherwise the HyperLogLog error
    /// (≈ <c>1.04 / sqrt(2^Precision)</c>).
    /// </summary>
    public double StandardError => _hll?.StandardError ?? 0d;

    /// <summary>Adds a key to the estimator.</summary>
    /// <param name="key">The key to add. Adding a key already counted does not change the estimate.</param>
    public void Add(TKey key)
    {
        if (_hll is not null)
        {
            _hll.Add(key);
            return;
        }

        _exact!.TryAdd(key);
        if (_exact.Count > _exactThreshold)
            Promote();
    }

    /// <summary>
    /// Returns the distinct-key count: an exact value while <see cref="IsExact"/> is <c>true</c>, otherwise the
    /// HyperLogLog estimate (within about <see cref="StandardError"/>).
    /// </summary>
    /// <returns>The (exact or estimated) number of distinct keys added.</returns>
    public long Count() => _hll is not null ? _hll.EstimateCardinality() : _exact!.Count;

    /// <summary>
    /// Merges another estimator into this one in place, so this estimator afterwards counts the distinct keys of
    /// the union of both inputs.
    /// </summary>
    /// <param name="other">The estimator to merge in. Left unmodified.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="other"/> has a different <see cref="Precision"/>.</exception>
    /// <remarks>
    /// Exact when both operands are exact and the union stays under <see cref="ExactThreshold"/>; otherwise the
    /// result is a HyperLogLog estimate. Because the underlying HyperLogLog union is a per-register maximum, a
    /// merge is byte-identical across runtimes given a deterministic hasher.
    /// </remarks>
    public void Merge(Distinct<TKey, THasher> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other._precision != _precision)
            throw new ArgumentException("Both estimators must have the same precision to be merged.", nameof(other));

        if (other._hll is not null)
        {
            // The other side is already an estimate: this side must be an estimate too, then take the union.
            if (_hll is null)
                Promote();
            _hll!.UnionWith(other._hll);
            return;
        }

        // The other side is exact: fold each of its keys in through the normal Add path, which keeps this side
        // exact when the union stays small and promotes it if not.
        foreach (TKey key in other._exact!)
            Add(key);
    }

    /// <summary>Resets the estimator to empty and back to exact mode.</summary>
    public void Clear()
    {
        _hll = null;
        if (_exact is null)
            _exact = new CeleritySet<TKey, THasher>();
        else
            _exact.Clear();
    }

    // Builds the HyperLogLog from the current exact set, then releases the set so memory is capped from here on.
    private void Promote()
    {
        var hll = new HyperLogLog<TKey, THasher>(_precision);
        foreach (TKey key in _exact!)
            hll.Add(key);

        _hll = hll;
        _exact = null;
    }
}

/// <summary>
/// A <see cref="Distinct{TKey, THasher}"/> specialized for <see cref="string"/> keys with the deterministic,
/// cross-runtime <see cref="StringXxHash3Hasher"/>.
/// </summary>
public sealed class StringDistinct : Distinct<string, StringXxHash3Hasher>
{
    /// <summary>Initializes a new, empty <see cref="StringDistinct"/> in exact mode.</summary>
    /// <param name="precision">The HyperLogLog precision used after promotion; see the base constructor.</param>
    /// <param name="exactThreshold">The distinct count at which the exact set promotes; see the base constructor.</param>
    /// <exception cref="ArgumentOutOfRangeException">An argument is out of range; see the base constructor.</exception>
    public StringDistinct(int precision = DefaultPrecision, int exactThreshold = DefaultExactThreshold)
        : base(precision, exactThreshold)
    {
    }
}
