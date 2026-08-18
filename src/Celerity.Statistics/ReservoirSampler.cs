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
/// Sampling is <strong>seeded</strong>: the sampler owns a seedable struct PRNG from
/// <c>Celerity.Primitives</c>, so the same seed and the same stream produce the same sample on
/// the same runtime and platform — reproducible runs, reproducible tests, a reproducible
/// investigation of a sample somebody kept. <see cref="ReservoirSampler{T}"/> is the
/// convenience form that fixes the generator to <see cref="Pcg32"/>.
/// </para>
/// <para>
/// It is deliberately <em>not</em> promised to be byte-identical <em>across</em> platforms, the
/// way <c>Celerity.Ring</c> is. The skip arithmetic runs through <c>Math.Log</c> and
/// <c>Math.Exp</c>, whose last bit .NET does not contractually fix across runtime
/// versions or architectures, and a single ulp either side of a <c>Math.Floor</c>
/// boundary changes the next replacement index and every draw after it. The PRNG itself is
/// exactly portable; the transcendentals on top of it are not, so the guarantee stops where
/// they start.
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
    /// <summary>
    /// The skip returned when the drawn one is past what a stream position can hold — large
    /// enough that no stream reaches it, small enough that adding a position cannot overflow.
    /// </summary>
    private const long FrozenSkip = long.MaxValue / 2;

    /// <summary>
    /// The largest value the acceptance weight may take: <c>1 − 2^-53</c>, the double below 1.
    /// </summary>
    /// <remarks>
    /// A weight of exactly 1 is a state the recurrence cannot leave — <c>log(1 − w)</c> is
    /// <c>−∞</c> so every skip is zero, and <c>w *= factor</c> with a factor that also rounds
    /// to 1 never decays — so the sampler would accept every remaining item for the rest of the
    /// stream. Reaching it takes only a maximal draw: at a capacity of two or more, the
    /// exponent of <c>log(1 − 2^-53) / k</c> is close enough to zero that <c>Math.Exp</c> rounds
    /// back up to 1.
    /// </remarks>
    private const double MaxAcceptanceWeight = 1d - (1d / (1UL << 53));

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
    private double NextW()
        => Math.Min(Math.Exp(Math.Log(NextUnitInterval()) / _items.Length), MaxAcceptanceWeight);

    /// <summary>
    /// Draws the number of items to skip before the next replacement,
    /// <c>floor(ln(U) / ln(1 − w))</c>.
    /// </summary>
    private long NextSkip()
    {
        // Log1P, not Math.Log(1 - w). Once the weight drops below the spacing of 1 the direct
        // form collapses to exactly zero, and a sampler that read that as a zero acceptance
        // probability would stop accepting while the real one is still positive. That is not
        // unreachable: the probability tracks k / n, so it passes 2^-54 at around k · 2^54
        // items — a stream length TotalSeen can still count.
        double denominator = Log1P(-_w);
        double skip = Math.Floor(Math.Log(NextUnitInterval()) / denominator);

        // Saturate only when the drawn skip is genuinely past what a stream position holds.
        // Both logs are negative and neither can be zero — the draw is strictly inside (0, 1)
        // and the weight is strictly inside (0, 1 - 2^-53] — so the quotient is a positive
        // finite number, and this is the only way it can leave long's range. It is also
        // reached well before the weight itself could underflow to zero: the numerator is at
        // least 2^-53 in magnitude, so the quotient passes long.MaxValue / 2 while the weight
        // is still around 1e-35.
        return skip >= FrozenSkip ? FrozenSkip : (long)skip;
    }

    /// <summary>
    /// <c>log(1 + x)</c>, accurate for an <paramref name="x"/> far below the precision of
    /// <c>1 + x</c>.
    /// </summary>
    /// <remarks>
    /// .NET ships no true <c>log1p</c> — <c>double.LogP1</c> is defined as
    /// <c>Math.Log(x + 1)</c>, which returns exactly zero once <c>x</c> falls under the spacing
    /// of 1, so it is no help here. This is Kahan's correction: <c>log(u) / (u − 1)</c> is well
    /// conditioned even where <c>u − 1</c> has lost most of its bits to cancellation, so
    /// scaling it by the exact <paramref name="x"/> puts them back.
    /// </remarks>
    /// <param name="x">The offset from 1. Greater than <c>-1</c>.</param>
    /// <returns><c>log(1 + x)</c>.</returns>
    private static double Log1P(double x)
    {
        double shifted = 1d + x;

        if (shifted == 1d)
        {
            return x;
        }

        return x * (Math.Log(shifted) / (shifted - 1d));
    }

    /// <summary>
    /// Draws a uniform double strictly inside <c>(0, 1)</c>: the 52-bit grid offset by half a
    /// step, so the draws run from <c>2^-53</c> to <c>1 − 2^-53</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The offset is what makes the draw unbiased at the ends. <c>NextDouble</c> covers
    /// <c>[0, 1)</c>, and both logarithms here are undefined at zero, so a zero has to become
    /// something — and substituting a subnormal would hand that one outcome in 2^53 a skip
    /// astronomically larger than its neighbours, which is a bias, not a rounding detail.
    /// Shifting the whole grid by half a step gives equiprobable interior points instead, with
    /// no outcome needing special treatment.
    /// </para>
    /// <para>
    /// It is 52 bits rather than 53 so that <em>both</em> endpoints are representable. Half a
    /// step below 1 on the 53-bit grid is a midpoint that rounds to exactly <c>1.0</c>, which
    /// would make that draw's acceptance weight exactly one — and a weight of one never shrinks
    /// again, so the sampler would replace on every later item for the rest of the stream.
    /// </para>
    /// </remarks>
    private double NextUnitInterval() => ((_rng.NextUInt64() >> 12) + 0.5d) * (1.0d / (1UL << 52));
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
    /// the specified seed so the sample is reproducible on a given runtime and platform. See
    /// <see cref="ReservoirSampler{T, TRng}"/> for why that stops short of a cross-platform
    /// guarantee.
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
