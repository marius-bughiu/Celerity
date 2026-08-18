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
/// instead of <c>n</c>. As algorithms both are exactly uniform — the difference is only the
/// cost. The skip counter is maintained inside <see cref="Add(T)"/>, so the sampler is still
/// fed one item at a time and a caller does not have to be able to skip forward in its source.
/// </para>
/// <para>
/// <strong>Uniform to the precision of the generator, not beyond it.</strong> Algorithm L's
/// retention probabilities are exact in real arithmetic, but the skip is drawn by comparing
/// logarithms of draws taken from a 2^52-point grid, so each decision lands within about
/// <c>2^-53</c> of its true probability. On a two-item stream at capacity 1, for instance, the
/// second item is retained exactly when <c>U₁ + U₂ &gt; 1</c>; the tie at <c>1</c> has nonzero
/// mass on a discrete grid, so the realized probability differs from <c>½</c> by about
/// <c>2^-53</c>. Which way that tie falls is decided by the last bit of the library
/// logarithms and is not fixed across runtimes — the same reason this type promises no
/// cross-platform reproducibility. Algorithm R would have none of this, its decision being an
/// exact integer comparison with no floating point anywhere, but it costs a draw per item,
/// which is the entire reason this type is Algorithm L. The deviation sits some fourteen
/// orders of magnitude below the sampling noise a k-item sample already carries.
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
    /// The next-replacement index used once the drawn skip runs past the end of what a stream
    /// can address: a position no item can occupy, so the reservoir is closed rather than
    /// merely postponed.
    /// </summary>
    /// <remarks>
    /// This is an absolute index rather than a large skip on purpose. Truncating the skip
    /// instead would leave a real index behind it — a long enough stream would arrive there and
    /// take a replacement the algorithm never sampled — and adding a truncated skip to a
    /// position already past the midpoint would wrap the sum negative.
    /// </remarks>
    private const long ClosedIndex = long.MaxValue;

    private readonly T[] _items;
    private TRng _rng;
    private long _seen;
    private long _nextIndex;
    /// <summary>
    /// The logarithm of Algorithm L's acceptance weight, rather than the weight itself.
    /// </summary>
    /// <remarks>
    /// Holding the log is what keeps the sampler from altering its own draws at either end of
    /// the range. The weight is a
    /// running product of <c>U^(1/k)</c> factors, and formed directly it rounds to 1 for every
    /// draw within <c>k · 2^-54</c> of the top — collapsing a range of distinct draws, wider
    /// the larger the reservoir, onto one acceptance decision — and underflows to 0 at the
    /// other end. In log space it is a running <em>sum</em> of strictly negative terms: it
    /// cannot reach either boundary, so no draw has to be clamped and the distribution is left
    /// alone.
    /// </remarks>
    private double _logWeight;
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
                _logWeight = NextLogWeight();
                _nextIndex = NextIndexAfter(_items.Length - 1);
            }

            return true;
        }

        if (position != _nextIndex)
        {
            return false;
        }

        _items[_rng.NextInt(_items.Length)] = item;
        _logWeight += NextLogWeight();
        _nextIndex = NextIndexAfter(position);
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
        _logWeight = 0d;
        _nextIndex = long.MaxValue;
    }

    /// <summary>
    /// Draws the log of the factor <c>U^(1/k)</c> that shrinks the acceptance window after each
    /// replacement — <c>ln(U) / k</c>, which needs no exponential and so cannot round to zero.
    /// </summary>
    private double NextLogWeight() => Math.Log(NextUnitInterval()) / _items.Length;

    /// <summary>
    /// Draws the stream position of the next replacement: <paramref name="position"/> plus one,
    /// plus a geometric skip of <c>floor(ln(U) / ln(1 − w))</c>.
    /// </summary>
    /// <param name="position">The position just consumed.</param>
    /// <returns>
    /// The next position to replace at, or <see cref="ClosedIndex"/> when the drawn skip runs
    /// past the end of the stream.
    /// </returns>
    private long NextIndexAfter(long position)
    {
        // log(1 - e^L) computed from the log-weight directly, so neither end of the range
        // has to be approximated by a representable weight. The naive form collapses to zero
        // once the weight drops below the spacing of 1 — and a sampler reading that as a zero
        // acceptance probability would stop accepting while the real one is still positive,
        // which is reachable: the probability tracks k / n, so it passes 2^-54 at around
        // k · 2^54 items, a stream length TotalSeen can still count.
        double denominator = LogOneMinusExp(_logWeight);
        double skip = Math.Floor(Math.Log(NextUnitInterval()) / denominator);

        // Close the reservoir only when the skip runs past the last addressable position.
        // Both logs are negative and neither can be zero — the draw is strictly inside (0, 1)
        // and the weight strictly inside (0, 1 - 2^-53] — so the quotient is a positive finite
        // number and this is the only way it can leave long's range. It is also reached well
        // before the weight itself could underflow to zero: the numerator is at least 2^-53 in
        // magnitude, so the quotient outgrows the stream while the weight is still near 1e-35.
        long remaining = ClosedIndex - position - 1;
        return skip >= remaining ? ClosedIndex : position + 1 + (long)skip;
    }

    /// <summary>
    /// <c>log(1 − e^x)</c> for a strictly negative <paramref name="x"/>, accurate at both ends
    /// of its range.
    /// </summary>
    /// <remarks>
    /// Neither single form works throughout. Near zero, <c>e^x</c> is close to 1 and the
    /// subtraction cancels, so the difference has to come from <c>expm1</c>. Far from zero,
    /// <c>1 − e^x</c> is close to 1 and it is the logarithm that loses its bits, so it has to
    /// come from <c>log1p</c>. The split is at <c>e^x = ½</c>, where both are well conditioned.
    /// </remarks>
    /// <param name="x">The log of the weight. Strictly negative.</param>
    /// <returns><c>log(1 − e^x)</c>.</returns>
    private static double LogOneMinusExp(double x)
    {
        const double LogHalf = -0.6931471805599453d;

        if (x > LogHalf)
        {
            return Math.Log(-ExpM1(x));
        }

        return Log1P(-Math.Exp(x));
    }

    /// <summary>
    /// <c>e^x − 1</c> for an <paramref name="x"/> in <c>(−0.7, 0]</c>, accurate where the
    /// subtraction alone would cancel.
    /// </summary>
    /// <remarks>
    /// .NET's <c>double.ExpM1</c> is <c>Math.Exp(x) - 1</c> and gives nothing back here, the
    /// same way <c>double.LogP1</c> is <c>Math.Log(x + 1)</c>. This is Kahan's form: the ratio
    /// <c>(u − 1) / log(u)</c> stays well conditioned where <c>u − 1</c> does not, so scaling
    /// it by the exact <paramref name="x"/> restores the lost bits.
    /// </remarks>
    /// <param name="x">The exponent. In <c>(−0.7, 0]</c>, so <c>e^x</c> never approaches 0.</param>
    /// <returns><c>e^x − 1</c>.</returns>
    private static double ExpM1(double x)
    {
        double u = Math.Exp(x);

        if (u == 1d)
        {
            return x;
        }

        return (u - 1d) * x / Math.Log(u);
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
