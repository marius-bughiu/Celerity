using System.Collections;
using Celerity.Primitives;

namespace Celerity.Statistics;

/// <summary>
/// A fixed-size uniform random sample of a stream whose length is not known in advance,
/// parameterized on a custom <see cref="IRandomSource"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// After any number of items have passed through it, the retained sample is a uniformly
/// random subset of everything seen: each of the <c>n</c> items has probability
/// <c>k / n</c> of being one of the <c>k</c> retained. The sampler never needs to know
/// <c>n</c> in advance and never stores more than <c>k</c> items, so it summarizes a stream
/// that does not fit in memory — or one that has no end.
/// </para>
/// <para>
/// The BCL has no sampler. <c>OrderBy(_ =&gt; Random.Shared.Next()).Take(k)</c>, the usual
/// substitute, materializes and sorts the entire sequence: <c>O(n)</c> memory and
/// <c>O(n log n)</c> time to produce <c>k</c> items, and it cannot run at all on a stream.
/// </para>
/// <para>
/// This is Li's <strong>Algorithm L</strong>, not the textbook Algorithm R. Algorithm R draws
/// one random number per item; Algorithm L draws a geometric <em>skip</em> and jumps over the
/// items that cannot win, so it makes <c>O(k · log(n / k))</c> draws over the whole stream
/// instead of <c>n</c>. Both are exactly uniform — the difference is only the cost. The skip
/// counter is maintained inside <see cref="Add(T)"/>, so the sampler is still fed one item at
/// a time and a caller does not have to be able to skip forward in its source.
/// </para>
/// <para>
/// Sampling is reproducible: the sampler owns a seedable struct PRNG from
/// <c>Celerity.Primitives</c>, so the same seed and the same stream produce the same sample
/// on every OS, architecture and runtime. <see cref="ReservoirSampler{T}"/> is the
/// convenience form that fixes the generator to <see cref="Pcg32"/>.
/// </para>
/// <para>
/// There is deliberately <strong>no <c>Merge</c></strong>. Combining two reservoirs into one
/// that is still uniform over the union requires drawing how many of the <c>k</c> output slots
/// come from each side from a hypergeometric distribution over the two stream lengths — not
/// simply replaying one side's retained items into the other, which over-weights the shorter
/// stream. Rather than ship a subtly biased merge, the sampler ships without one; sample the
/// shards separately, or feed one sampler.
/// </para>
/// <para>
/// The retained sample is exposed as a <see cref="ReadOnlySpan{T}"/> over the sampler's own
/// storage — no copy, no allocation — and the type implements
/// <see cref="IReadOnlyList{T}"/> for the ergonomic path. It holds items in an arbitrary
/// order, not stream order.
/// </para>
/// <para>
/// Not thread-safe.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the sampled items.</typeparam>
/// <typeparam name="TRng">
/// The random source used to drive the sampling decisions. Must be a value type implementing
/// <see cref="IRandomSource"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class ReservoirSampler<T, TRng> : IReadOnlyList<T>
    where TRng : struct, IRandomSource
{
    private readonly T[] _items;
    private TRng _rng;
    private long _seen;
    private long _nextIndex;
    private double _w;
    private int _filled;

    /// <summary>
    /// Initializes a sampler retaining up to <paramref name="capacity"/> items, driven by the
    /// specified random source.
    /// </summary>
    /// <param name="capacity">The maximum number of items to retain. Must be positive.</param>
    /// <param name="rng">The random source, already seeded.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is less than <c>1</c>.
    /// </exception>
    public ReservoirSampler(int capacity, TRng rng)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                capacity,
                "Reservoir capacity must be at least 1.");
        }

        _items = new T[capacity];
        _rng = rng;
        ResetSkipState();
    }

    /// <summary>Gets the maximum number of items the sampler retains.</summary>
    public int Capacity => _items.Length;

    /// <summary>
    /// Gets the number of items currently retained — <see cref="Capacity"/> once the stream
    /// has been long enough to fill the reservoir, and the number of items seen before that.
    /// </summary>
    public int Count => _filled;

    /// <summary>Gets the total number of items that have passed through the sampler.</summary>
    public long TotalSeen => _seen;

    /// <summary>
    /// Gets a value indicating whether the reservoir is full, i.e. whether at least
    /// <see cref="Capacity"/> items have been seen.
    /// </summary>
    public bool IsFull => _filled == _items.Length;

    /// <summary>
    /// Gets the retained sample as a span over the sampler's own storage. The span is
    /// invalidated by any subsequent <see cref="Add(T)"/> or <see cref="Clear"/>.
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

    /// <summary>Offers a single item to the sampler.</summary>
    /// <param name="item">The item to offer. It is retained only if the sampling decision keeps it.</param>
    /// <returns>
    /// <c>true</c> if the item was retained (either filling an empty slot or replacing a
    /// previously retained item); otherwise <c>false</c>.
    /// </returns>
    public bool Add(T item)
    {
        long position = _seen;
        _seen = position + 1;

        if (_filled < _items.Length)
        {
            _items[_filled++] = item;

            if (_filled == _items.Length)
            {
                // The reservoir just filled: start the skip sequence from this point.
                _w = NextW();
                _nextIndex = _items.Length + NextSkip();
            }

            return true;
        }

        if (position != _nextIndex)
        {
            return false;
        }

        _items[_rng.NextInt(_items.Length)] = item;
        _w *= NextW();
        _nextIndex = position + 1 + NextSkip();
        return true;
    }

    /// <summary>Offers every item in a span to the sampler, in order.</summary>
    /// <param name="items">The items to offer.</param>
    /// <remarks>
    /// There is no <see cref="IEnumerable{T}"/> overload: an overload set holding both a span
    /// and a sequence makes an array argument ambiguous under C# 12, and a sequence is a
    /// one-line <c>foreach</c> over <see cref="Add(T)"/>.
    /// </remarks>
    public void Add(ReadOnlySpan<T> items)
    {
        foreach (T item in items)
        {
            Add(item);
        }
    }

    /// <summary>
    /// Discards the retained sample and the stream position, leaving the random source where
    /// it is so a cleared sampler does not replay the same decisions.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_items, 0, _filled);
        _filled = 0;
        _seen = 0;
        ResetSkipState();
    }

    /// <summary>Returns an enumerator over the retained sample.</summary>
    /// <returns>An enumerator over the retained items, in arbitrary order.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < _filled; i++)
        {
            yield return _items[i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void ResetSkipState()
    {
        _w = 1d;
        _nextIndex = long.MaxValue;
    }

    /// <summary>
    /// Draws the multiplicative factor <c>exp(ln(U) / k)</c> that shrinks the acceptance
    /// window after each replacement.
    /// </summary>
    private double NextW() => Math.Exp(Math.Log(NextUnitInterval()) / _items.Length);

    /// <summary>
    /// Draws the number of items to skip before the next replacement,
    /// <c>floor(ln(U) / ln(1 − w))</c>.
    /// </summary>
    private long NextSkip()
    {
        double skip = Math.Floor(Math.Log(NextUnitInterval()) / Math.Log(1d - _w));

        // w is in (0, 1), so log(1 - w) is negative and the quotient is non-negative; the
        // clamp guards the degenerate ends (w rounding to 0 or 1) rather than the normal path.
        if (!(skip >= 0d) || skip > long.MaxValue / 2d)
        {
            return long.MaxValue / 2;
        }

        return (long)skip;
    }

    /// <summary>
    /// Draws a uniform double in <c>(0, 1)</c>. Both logarithms above are undefined at zero,
    /// so the open interval is the contract, not a rounding detail.
    /// </summary>
    private double NextUnitInterval()
    {
        double u = _rng.NextDouble();
        return u <= 0d ? double.Epsilon : u;
    }
}

/// <summary>
/// A fixed-size uniform random sample of a stream whose length is not known in advance,
/// using <see cref="Pcg32"/> as its random source.
/// </summary>
/// <remarks>
/// Supply a different generator via the <see cref="ReservoirSampler{T, TRng}"/> generic
/// overload — <see cref="Xoshiro256StarStar"/> and <see cref="WyRand"/> are the faster
/// options, <see cref="Pcg32"/> the better-distributed default. See
/// <see cref="ReservoirSampler{T, TRng}"/> for the algorithm and the full contract.
/// </remarks>
/// <typeparam name="T">The type of the sampled items.</typeparam>
public sealed class ReservoirSampler<T> : ReservoirSampler<T, Pcg32>
{
    /// <summary>
    /// Initializes a sampler retaining up to <paramref name="capacity"/> items, seeded from
    /// the specified seed so the sample is reproducible.
    /// </summary>
    /// <param name="capacity">The maximum number of items to retain. Must be positive.</param>
    /// <param name="seed">The seed for the underlying <see cref="Pcg32"/> generator.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is less than <c>1</c>.
    /// </exception>
    public ReservoirSampler(int capacity, ulong seed)
        : base(capacity, new Pcg32(seed))
    {
    }
}
