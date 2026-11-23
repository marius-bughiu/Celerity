using System.Collections;
using Celerity.Primitives;

namespace Celerity.Statistics;

/// <summary>
/// A fixed-size <strong>weighted</strong> random sample, without replacement, of a stream whose
/// length and total weight are not known in advance, parameterized on a custom
/// <see cref="IRandomSource"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Each item is offered with a non-negative weight, and the retained sample has exactly the
/// distribution of <c>k</c> successive draws <em>without replacement</em> in which every draw
/// picks a remaining item with probability proportional to its weight. An item twice as heavy is
/// twice as likely to be the first one chosen. The sampler never needs to know the stream's
/// length or its total weight and never stores more than <c>k</c> items, so it samples a stream
/// that does not fit in memory, or one that has no end: log lines weighted by bytes, requests
/// weighted by latency, accounts weighted by spend.
/// </para>
/// <para>
/// Neither .NET nor the rest of this package can do this. <see cref="ReservoirSampler{T, TRng}"/>
/// is uniform only, and <see cref="AliasTable"/> needs every weight before its first draw. The
/// textbook hand-roll is Efraimidis and Spirakis' <strong>A-Res</strong>: give every item the key
/// <c>U^(1/w)</c> and keep the <c>k</c> largest in a <see cref="PriorityQueue{TElement, TPriority}"/>.
/// It is exact, but it draws a random number and takes a logarithm for every item.
/// </para>
/// <para>
/// This type is the same paper's <strong>A-ExpJ</strong>, the exponential-jumps form, which is to
/// A-Res what Li's Algorithm L is to Algorithm R. Once the reservoir is full it draws how much
/// <em>weight</em> must pass before the next item can enter, and every item in between costs one
/// subtraction and one comparison: no random draw, no logarithm, no heap. Over the whole stream it
/// makes <c>O(k · log(n / k))</c> draws instead of <c>n</c>, and the sample it keeps has exactly
/// A-Res's distribution.
/// </para>
/// <para>
/// <strong>A weight of zero</strong> is accepted and never retained: the item is counted in
/// <see cref="TotalSeen"/> and changes nothing else. A negative, <c>NaN</c> or infinite weight
/// throws.
/// </para>
/// <para>
/// <strong>Unlike the uniform sampler, this one merges.</strong> A-Res keys are drawn
/// independently per item, so the <c>k</c> smallest keys of two reservoirs together are the
/// <c>k</c> smallest of the two streams together, and <see cref="Merge"/> is exact. Sample
/// shards in parallel and merge the results, giving each shard its <em>own</em> seed: two shards
/// seeded alike draw the same keys in the same order, and their merge is correlated rather than
/// random.
/// </para>
/// <para>
/// Sampling is <strong>seeded</strong>: the same seed and the same stream of items and weights
/// produce the same sample on the same runtime and platform. As with
/// <see cref="ReservoirSampler{T, TRng}"/>, that stops short of a cross-platform promise: the
/// keys and jumps run through <c>Math.Log</c> and <c>Math.Exp</c>, whose last bit .NET does not
/// fix across runtimes or architectures. The decisions are exact in real arithmetic and land
/// within a few ulps of their true probability in floating point, some fourteen orders of
/// magnitude below the sampling noise of the sample itself.
/// </para>
/// <para>
/// The retained sample is exposed as a <see cref="ReadOnlySpan{T}"/> over the sampler's own
/// storage, with no copy and no allocation, and the type implements
/// <see cref="IReadOnlyList{T}"/> for the ergonomic path. Items are held in an arbitrary order,
/// not stream order and not weight order.
/// </para>
/// <para>
/// Not thread-safe.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the sampled items.</typeparam>
/// <typeparam name="TRng">
/// The random source used to draw the keys and jumps. Must be a value type implementing
/// <see cref="IRandomSource"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class WeightedReservoirSampler<T, TRng> : IReadOnlyList<T>
    where TRng : struct, IRandomSource
{
    /// <summary><c>ln 2</c>: where <c>1 − e^−a</c> switches from the <c>expm1</c> form to the direct one.</summary>
    private const double Ln2 = 0.6931471805599453d;

    private readonly T[] _items;

    /// <summary>
    /// Each retained item's key as the <em>logarithm</em> of an exponential variate,
    /// <c>ln(−ln U) − ln w</c>, kept as a max-heap so the threshold, the largest retained key,
    /// sits at index 0.
    /// </summary>
    /// <remarks>
    /// A-Res's key <c>U^(1/w)</c> and its exponential form <c>−ln(U) / w</c> both leave the double
    /// range at ordinary weights: a weight of <c>10^-310</c> sends the exponential key past
    /// <c>double.MaxValue</c>, and a weight of <c>10^300</c> rounds A-Res's key to exactly 1. The
    /// logarithm of the exponential key is finite for every positive finite weight, subnormals
    /// included, and it orders items the same way: a <em>smaller</em> key is a
    /// stronger claim on a place in the sample.
    /// </remarks>
    private readonly double[] _keys;

    private TRng _rng;
    private long _seen;
    private double _totalWeight;

    /// <summary>
    /// The weight still to pass, once the reservoir is full, before the next item enters it. Each
    /// offered weight is subtracted; the item that takes it to zero or below is the one that
    /// enters.
    /// </summary>
    private double _remaining;

    private int _filled;

    // Bumped by every Add, Merge and Clear, so an enumerator can tell the sampler moved under it.
    private int _version;

    /// <summary>
    /// Initializes a sampler retaining up to <paramref name="capacity"/> items, driven by the
    /// specified random source.
    /// </summary>
    /// <param name="capacity">The maximum number of items to retain. Must be positive.</param>
    /// <param name="rng">The random source, already seeded.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is less than <c>1</c>.
    /// </exception>
    public WeightedReservoirSampler(int capacity, TRng rng)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                capacity,
                "Reservoir capacity must be at least 1.");
        }

        _items = new T[capacity];
        _keys = new double[capacity];
        _rng = rng;
    }

    /// <summary>Gets the maximum number of items the sampler retains.</summary>
    public int Capacity => _items.Length;

    /// <summary>
    /// Gets the number of items currently retained: <see cref="Capacity"/> once enough items of
    /// positive weight have been offered to fill the reservoir, and the number of them before that.
    /// </summary>
    public int Count => _filled;

    /// <summary>
    /// Gets the total number of items that have passed through the sampler, zero-weight items
    /// included.
    /// </summary>
    public long TotalSeen => _seen;

    /// <summary>
    /// Gets the sum of every weight offered so far, accumulated in <see cref="double"/>
    /// arithmetic.
    /// </summary>
    /// <remarks>
    /// This is the denominator a Horvitz–Thompson estimate needs, and it is a floating-point
    /// running sum: exact for integer weights while it stays below <c>2^53</c>, and subject to
    /// the usual rounding beyond that or for fractional weights. It is <c>+∞</c> once the sum
    /// overflows, which needs weights near <see cref="double.MaxValue"/>; sampling is unaffected.
    /// </remarks>
    public double TotalWeight => _totalWeight;

    /// <summary>
    /// Gets a value indicating whether the reservoir is full, i.e. whether at least
    /// <see cref="Capacity"/> items of positive weight have been seen.
    /// </summary>
    public bool IsFull => _filled == _items.Length;

    /// <summary>
    /// Gets the retained sample as a span over the sampler's own storage. The span is
    /// invalidated by any subsequent <see cref="Add(T, double)"/>, <see cref="Merge"/> or
    /// <see cref="Clear"/>.
    /// </summary>
    public ReadOnlySpan<T> Sample => new(_items, 0, _filled);

    /// <summary>Gets the retained item at the specified position in the sample.</summary>
    /// <param name="index">The position within the retained sample.</param>
    /// <returns>The retained item.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or not less than <see cref="Count"/>.
    /// </exception>
    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_filled)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    index,
                    "Index is outside the retained sample.");
            }

            return _items[index];
        }
    }

    /// <summary>Offers a single item to the sampler with the specified weight.</summary>
    /// <param name="item">The item to offer. It is retained only if the sampling decision keeps it.</param>
    /// <param name="weight">
    /// The item's weight: a non-negative finite number. Only the ratios between weights matter,
    /// so they need not sum to anything. A weight of zero is counted and never retained.
    /// </param>
    /// <returns>
    /// <c>true</c> if the item was retained (either filling an empty slot or replacing a
    /// previously retained item); otherwise <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="weight"/> is negative, <c>NaN</c> or infinite. The sampler is unchanged.
    /// </exception>
    public bool Add(T item, double weight)
    {
        // One comparison pair admits exactly the positive finite weights; zero and the invalid
        // ones share the rare path.
        if (!(weight > 0d && weight <= double.MaxValue))
        {
            return AddZeroWeight(weight);
        }

        _seen++;
        _version++;
        _totalWeight += weight;

        if (_filled == _items.Length)
        {
            double remaining = _remaining - weight;
            if (remaining > 0d)
            {
                _remaining = remaining;
                return false;
            }

            Enter(item, weight);
            return true;
        }

        Fill(item, weight);
        return true;
    }

    /// <summary>
    /// Offers every item in a span to the sampler, in order, each with the weight at the same
    /// position in <paramref name="weights"/>.
    /// </summary>
    /// <param name="items">The items to offer.</param>
    /// <param name="weights">The items' weights, one per item. Each must be non-negative and finite.</param>
    /// <remarks>
    /// Every weight is validated before the first item is offered, so a bad weight anywhere in
    /// the batch leaves the sampler exactly as it was. Otherwise this is the same as calling
    /// <see cref="Add(T, double)"/> once per item.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="weights"/> is not the same length as <paramref name="items"/>, or one of
    /// its weights is negative, <c>NaN</c> or infinite.
    /// </exception>
    public void Add(ReadOnlySpan<T> items, ReadOnlySpan<double> weights)
    {
        if (weights.Length != items.Length)
        {
            throw new ArgumentException(
                "There must be exactly one weight per item.",
                nameof(weights));
        }

        foreach (double weight in weights)
        {
            if (!(weight >= 0d && weight <= double.MaxValue))
            {
                throw new ArgumentException(
                    "Every weight must be non-negative and finite.",
                    nameof(weights));
            }
        }

        int i = 0;
        while (i < items.Length && _filled < _items.Length)
        {
            Add(items[i], weights[i]);
            i++;
        }

        if (i == items.Length)
        {
            return;
        }

        // The reservoir is full: run the jump countdown in locals, so the loop is one subtraction,
        // one addition and one well-predicted branch per item, with no field traffic between them.
        _seen += items.Length - i;
        _version++;

        double remaining = _remaining;
        double total = _totalWeight;

        for (; i < items.Length; i++)
        {
            double weight = weights[i];
            total += weight;
            remaining -= weight;

            // A zero weight never enters, even when the jump has already run down to zero.
            if (remaining <= 0d && weight != 0d)
            {
                Enter(items[i], weight);
                remaining = _remaining;
            }
        }

        _remaining = remaining;
        _totalWeight = total;
    }

    /// <summary>
    /// Folds another sampler's retained sample into this one, so this sampler holds a weighted
    /// sample of both streams together.
    /// </summary>
    /// <param name="other">
    /// The sampler to merge in. It must have the same <see cref="Capacity"/>, and it is left
    /// unchanged.
    /// </param>
    /// <remarks>
    /// <para>
    /// The result is exact: it has the same distribution as one sampler fed both streams. Every
    /// retained item carries the key that decided its place, the keys are independent across
    /// items, and the <c>k</c> smallest keys of the union are the <c>k</c> smallest among the two
    /// sets of <c>k</c> retained ones. <see cref="TotalSeen"/> and <see cref="TotalWeight"/>
    /// become the sums of both, and the next jump is drawn afresh from the merged threshold, which
    /// the exponential jump's memorylessness permits at any point in the stream.
    /// </para>
    /// <para>
    /// Exactness assumes the two samplers' random sources are independent. Give each shard its own
    /// seed: two samplers seeded alike draw the same keys in the same order.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="other"/> is this sampler, or its capacity differs from this one's.
    /// </exception>
    public void Merge(WeightedReservoirSampler<T, TRng> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (ReferenceEquals(other, this))
        {
            throw new ArgumentException(
                "A sampler cannot be merged with itself: every item would be counted twice.",
                nameof(other));
        }

        if (other._items.Length != _items.Length)
        {
            throw new ArgumentException(
                "Only samplers of the same capacity can be merged.",
                nameof(other));
        }

        for (int i = 0; i < other._filled; i++)
        {
            double key = other._keys[i];

            if (_filled < _items.Length)
            {
                Push(other._items[i], key);
            }
            else if (key < _keys[0])
            {
                ReplaceRoot(other._items[i], key);
            }
        }

        _seen += other._seen;
        _totalWeight += other._totalWeight;
        _version++;

        if (_filled == _items.Length)
        {
            DrawJump();
        }
    }

    /// <summary>
    /// Discards the retained sample, the stream position and the weight total, leaving the random
    /// source where it is so a cleared sampler does not replay the same decisions.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_items, 0, _filled);
        _filled = 0;
        _seen = 0;
        _totalWeight = 0d;
        _remaining = 0d;
        _version++;
    }

    /// <summary>Returns an enumerator over the retained sample.</summary>
    /// <returns>An enumerator over the retained items, in arbitrary order.</returns>
    /// <remarks>
    /// The enumerator fails fast, as the BCL's collections and Celerity's mutable collections do:
    /// after any <see cref="Add(T, double)"/>, <see cref="Merge"/> or <see cref="Clear"/>, its next
    /// <see cref="IEnumerator.MoveNext"/> throws <see cref="InvalidOperationException"/>. That
    /// includes an <see cref="Add(T, double)"/> that discards its item, so the failure does not
    /// depend on what the sampling decision happened to be, and it holds after the walk has
    /// finished, as it does for <see cref="List{T}"/>. <see cref="IEnumerator.Reset"/> is
    /// supported and checked the same way.
    /// </remarks>
    public IEnumerator<T> GetEnumerator() => new Enumerator(this);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static void ThrowModified() =>
        throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

    /// <summary>
    /// The rare path of <see cref="Add(T, double)"/>: counts a zero weight, or throws for one that
    /// is negative, <c>NaN</c> or infinite.
    /// </summary>
    private bool AddZeroWeight(double weight)
    {
        // -0.0 compares equal to zero and is accepted with it.
        if (weight != 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weight),
                weight,
                "Weight must be non-negative and finite.");
        }

        _seen++;
        _version++;
        return false;
    }

    /// <summary>Retains an item while the reservoir still has room, drawing its key.</summary>
    private void Fill(T item, double weight)
    {
        Push(item, LogExponential(-Math.Log(NextUnitInterval()), weight));

        if (_filled == _items.Length)
        {
            DrawJump();
        }
    }

    /// <summary>
    /// Retains the item whose weight completed the jump. Its key is drawn conditioned on beating
    /// the threshold, which is what A-ExpJ's correctness rests on: the jump decided <em>that</em>
    /// this item enters, and the key decides how strongly it holds its place afterwards.
    /// </summary>
    private void Enter(T item, double weight)
    {
        // The threshold as an exponential variate is E_T = e^{_keys[0]}, and the item's key E is
        // exponential with rate w, conditioned on E < E_T. That is a truncated exponential:
        // E = −ln(1 − p·V) / w for V uniform, with p = 1 − e^{−a}, a = w · E_T, the probability
        // mass below the threshold. When the threshold itself is not a normal double, a is formed
        // in log space instead, so neither factor has to be representable on its own.
        double threshold = Math.Exp(_keys[0]);
        double a = double.IsNormal(threshold) ? weight * threshold : Math.Exp(Math.Log(weight) + _keys[0]);
        double p = a < Ln2 ? -StableLogMath.ExpM1(-a) : 1d - Math.Exp(-a);

        ReplaceRoot(item, LogExponential(-StableLogMath.Log1P(-p * NextUnitInterval()), weight));
        DrawJump();
    }

    /// <summary>
    /// Draws the weight that must pass before the next item enters: exponential with rate equal
    /// to the threshold's exponential key, <c>−ln(U) / E_T</c>.
    /// </summary>
    /// <remarks>
    /// It may round to <c>+∞</c> (the reservoir is then closed for the rest of a stream, which is
    /// the true limit) or to <c>0</c> (the next item of positive weight enters). Both are what
    /// the real-valued jump does at those magnitudes.
    /// </remarks>
    private void DrawJump() =>
        _remaining = -Math.Log(NextUnitInterval()) * Math.Exp(-_keys[0]);

    /// <summary>
    /// The stored key for an exponential variate <c>numerator / weight</c>: its logarithm, taken
    /// from the quotient when that is a normal double and from the difference of logarithms when
    /// the quotient would overflow, underflow or lose precision as a subnormal.
    /// </summary>
    /// <param name="numerator">A positive normal double: <c>−ln U</c>, or its truncated form.</param>
    /// <param name="weight">The item's weight, positive and finite.</param>
    /// <returns><c>ln(numerator) − ln(weight)</c>.</returns>
    private static double LogExponential(double numerator, double weight)
    {
        double exponential = numerator / weight;

        // One logarithm on the common path; the two-logarithm form only at the extremes, where
        // the quotient has left the normal range.
        return double.IsNormal(exponential)
            ? Math.Log(exponential)
            : Math.Log(numerator) - Math.Log(weight);
    }

    /// <summary>Appends a key to the max-heap and sifts it up.</summary>
    private void Push(T item, double key)
    {
        int index = _filled++;

        while (index > 0)
        {
            int parent = (index - 1) >> 1;
            if (_keys[parent] >= key)
            {
                break;
            }

            _keys[index] = _keys[parent];
            _items[index] = _items[parent];
            index = parent;
        }

        _keys[index] = key;
        _items[index] = item;
    }

    /// <summary>
    /// Evicts the threshold, the largest retained key, in favour of a smaller one, and sifts the
    /// newcomer down to its place.
    /// </summary>
    private void ReplaceRoot(T item, double key)
    {
        int count = _filled;
        int index = 0;

        while (true)
        {
            int child = (2 * index) + 1;
            if (child >= count)
            {
                break;
            }

            int right = child + 1;
            if (right < count && _keys[right] > _keys[child])
            {
                child = right;
            }

            if (_keys[child] <= key)
            {
                break;
            }

            _keys[index] = _keys[child];
            _items[index] = _items[child];
            index = child;
        }

        _keys[index] = key;
        _items[index] = item;
    }

    /// <summary>
    /// Draws a uniform double strictly inside <c>(0, 1)</c>: the 52-bit grid offset by half a
    /// step, so the draws run from <c>2^-53</c> to <c>1 − 2^-53</c> and both logarithms of a
    /// key are finite.
    /// </summary>
    /// <remarks>
    /// The same draw as <see cref="ReservoirSampler{T, TRng}"/>'s, and for the same reason: an
    /// endpoint would make <c>−ln U</c> either zero or infinite, and substituting a neighbour for
    /// that one outcome would bias it.
    /// </remarks>
    private double NextUnitInterval() => ((_rng.NextUInt64() >> 12) + 0.5d) * (1.0d / (1UL << 52));

    // Hand-written rather than an iterator block: a compiler-generated iterator goes terminal after
    // its first false and never re-enters its body, so it could not report a modification made
    // after the walk ended. The version is captured at GetEnumerator and checked before anything
    // else on every MoveNext and Reset.
    private sealed class Enumerator : IEnumerator<T>
    {
        private readonly WeightedReservoirSampler<T, TRng> _sampler;
        private readonly int _version;
        private int _index;
        private T _current;

        internal Enumerator(WeightedReservoirSampler<T, TRng> sampler)
        {
            _sampler = sampler;
            _version = sampler._version;
            _current = default!;
        }

        public T Current => _current;

        object? IEnumerator.Current => _current;

        public bool MoveNext()
        {
            if (_version != _sampler._version)
            {
                ThrowModified();
            }

            if (_index < _sampler._filled)
            {
                _current = _sampler._items[_index++];
                return true;
            }

            _current = default!;
            return false;
        }

        public void Reset()
        {
            if (_version != _sampler._version)
            {
                ThrowModified();
            }

            _index = 0;
            _current = default!;
        }

        public void Dispose()
        {
        }
    }
}

/// <summary>
/// A fixed-size weighted random sample, without replacement, of a stream whose length and total
/// weight are not known in advance, using <see cref="Pcg32"/> as its random source.
/// </summary>
/// <remarks>
/// Supply a different generator via the <see cref="WeightedReservoirSampler{T, TRng}"/> generic
/// overload: <see cref="Xoshiro256StarStar"/> and <see cref="WyRand"/> are the faster options,
/// <see cref="Pcg32"/> the better-distributed default. See
/// <see cref="WeightedReservoirSampler{T, TRng}"/> for the algorithm and the full contract.
/// </remarks>
/// <typeparam name="T">The type of the sampled items.</typeparam>
public sealed class WeightedReservoirSampler<T> : WeightedReservoirSampler<T, Pcg32>
{
    /// <summary>
    /// Initializes a sampler retaining up to <paramref name="capacity"/> items, seeded from the
    /// specified seed so the sample is reproducible on a given runtime and platform. See
    /// <see cref="WeightedReservoirSampler{T, TRng}"/> for why that stops short of a
    /// cross-platform guarantee.
    /// </summary>
    /// <param name="capacity">The maximum number of items to retain. Must be positive.</param>
    /// <param name="seed">The seed for the underlying <see cref="Pcg32"/> generator.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is less than <c>1</c>.
    /// </exception>
    public WeightedReservoirSampler(int capacity, ulong seed)
        : base(capacity, new Pcg32(seed))
    {
    }
}
