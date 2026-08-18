namespace Celerity.Statistics;

/// <summary>
/// A single-pass, allocation-free accumulator for the first four moments of a stream of
/// <see cref="double"/> values — count, mean, variance, standard deviation, skewness and
/// kurtosis, plus the running minimum and maximum.
/// </summary>
/// <remarks>
/// <para>
/// The BCL has <see cref="System.Linq.Enumerable.Average(IEnumerable{double})"/> and nothing
/// else: no variance, no standard deviation, no higher moment, and no accumulator that can be
/// fed one value at a time. The two things a caller writes instead both have a problem.
/// A LINQ variance is a <em>two-pass</em> shape — average the sequence, then average the squared
/// deviations — which needs the sequence to be re-enumerable and so cannot summarize a stream.
/// The one-pass alternative, accumulating <c>sum</c> and <c>sumOfSquares</c> and subtracting,
/// is numerically catastrophic when the mean is large relative to the spread: the two terms of
/// <c>sumOfSquares / n - mean²</c> agree in their leading digits and cancel, so the answer is
/// built entirely out of the digits that rounding already destroyed. It can and does return a
/// negative variance.
/// </para>
/// <para>
/// This type uses Welford's recurrence extended to the fourth moment (Terriberry), which
/// updates the mean and the central moments directly and never forms a large intermediate to
/// subtract away. Adding a value is a handful of multiplications and comparisons with no
/// allocation, and the accumulated state is seven fields regardless of how many values pass
/// through it.
/// </para>
/// <para>
/// <strong>This is a mutable struct, deliberately.</strong> It makes <c>default</c> a valid
/// empty accumulator, lets an array of per-bucket statistics live inline with zero
/// allocation, and keeps the add path free of an indirection. The usual caveat applies: a
/// copy is an independent snapshot, so <c>list[i].Add(x)</c> updates a temporary and is lost.
/// Accumulate through a <c>ref</c> — <see cref="System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault{TKey, TValue}(Dictionary{TKey, TValue}, TKey, out bool)"/>
/// for a dictionary value, or a plain array element, which is already a <c>ref</c>.
/// </para>
/// <para>
/// <strong>The domain is the finite doubles.</strong> <see cref="Add(double)"/> rejects
/// <see cref="double.NaN"/> and the infinities rather than accumulating them, matching
/// <c>DDSketch</c> in the same package. A recurrence over deltas has no good answer for them —
/// <c>∞ − ∞</c> is <see cref="double.NaN"/>, so a second infinity destroys the mean a first one
/// survived, and merging an infinite accumulator gives a different answer depending on which
/// side it is on. Rejecting at the boundary is the only version of that whose behaviour can be
/// stated in a sentence, and it turns a silently poisoned statistic into a stack trace at the
/// value that caused it.
/// </para>
/// <para>
/// Every statistic that is undefined for the number of values seen returns
/// <see cref="double.NaN"/> rather than throwing: <see cref="Mean"/>, <see cref="Min"/> and
/// <see cref="Max"/> on an empty accumulator, <see cref="Variance"/> below two values,
/// <see cref="Skewness"/> below three and <see cref="Kurtosis"/> below four.
/// </para>
/// <para>
/// Two accumulators over disjoint parts of a stream combine exactly with
/// <see cref="Merge(in RunningStatistics)"/>, which applies Chan's parallel formulas rather
/// than replaying either side, so a sharded or parallel pass produces the same moments as a
/// single sequential one (up to floating-point associativity).
/// </para>
/// <para>
/// Not thread-safe. Concurrent readers are fine; a concurrent <see cref="Add(double)"/> is not.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var stats = new RunningStatistics();
/// foreach (double latency in stream)
/// {
///     stats.Add(latency);
/// }
///
/// Console.WriteLine($"{stats.Count} samples, mean {stats.Mean:F2} ± {stats.StandardDeviation:F2}");
/// </code>
/// </example>
public struct RunningStatistics
{
    private long _count;
    private double _mean;
    private double _m2;
    private double _m3;
    private double _m4;
    private double _min;
    private double _max;

    /// <summary>
    /// Initializes an empty accumulator. Identical to <c>default(RunningStatistics)</c>, which
    /// is also a valid empty accumulator.
    /// </summary>
    public RunningStatistics()
    {
    }

    /// <summary>
    /// Initializes an accumulator that has already consumed the specified values.
    /// </summary>
    /// <param name="values">The values to accumulate.</param>
    public RunningStatistics(ReadOnlySpan<double> values)
    {
        this = default;
        Add(values);
    }

    /// <summary>Gets the number of values accumulated.</summary>
    public readonly long Count => _count;

    /// <summary>
    /// Gets the arithmetic mean, or <see cref="double.NaN"/> if no values have been added.
    /// </summary>
    public readonly double Mean => _count == 0 ? double.NaN : _mean;

    /// <summary>
    /// Gets the sum of the accumulated values, or <c>0</c> if none have been added.
    /// </summary>
    /// <remarks>
    /// Recovered as <see cref="Mean"/> × <see cref="Count"/> rather than accumulated
    /// separately, so it carries the mean's rounding rather than a running sum's.
    /// </remarks>
    public readonly double Sum => _count == 0 ? 0d : _mean * _count;

    /// <summary>
    /// Gets the smallest value added, or <see cref="double.NaN"/> if none have been.
    /// </summary>
    public readonly double Min => _count == 0 ? double.NaN : _min;

    /// <summary>
    /// Gets the largest value added, or <see cref="double.NaN"/> if none have been.
    /// </summary>
    public readonly double Max => _count == 0 ? double.NaN : _max;

    /// <summary>
    /// Gets the sample variance (the unbiased, <c>n − 1</c> denominator estimator), or
    /// <see cref="double.NaN"/> if fewer than two values have been added.
    /// </summary>
    public readonly double Variance => _count < 2 ? double.NaN : _m2 / (_count - 1);

    /// <summary>
    /// Gets the population variance (the <c>n</c> denominator form), or
    /// <see cref="double.NaN"/> if no values have been added.
    /// </summary>
    public readonly double PopulationVariance => _count == 0 ? double.NaN : _m2 / _count;

    /// <summary>
    /// Gets the sample standard deviation — the square root of <see cref="Variance"/>.
    /// </summary>
    public readonly double StandardDeviation => Math.Sqrt(Variance);

    /// <summary>
    /// Gets the population standard deviation — the square root of
    /// <see cref="PopulationVariance"/>.
    /// </summary>
    public readonly double PopulationStandardDeviation => Math.Sqrt(PopulationVariance);

    /// <summary>
    /// Gets the population skewness (the biased <c>g₁</c> estimator), or
    /// <see cref="double.NaN"/> if fewer than three values have been added or every value
    /// added was identical.
    /// </summary>
    /// <remarks>
    /// Positive skew means the long tail is to the right of the mean. A distribution with no
    /// spread has no defined skew, so a constant stream reports <see cref="double.NaN"/>
    /// rather than zero.
    /// </remarks>
    public readonly double Skewness
    {
        get
        {
            if (_count < 3 || _m2 == 0d)
            {
                return double.NaN;
            }

            return Math.Sqrt((double)_count) * _m3 / Math.Pow(_m2, 1.5);
        }
    }

    /// <summary>
    /// Gets the population excess kurtosis (the biased <c>g₂</c> estimator, so a normal
    /// distribution scores <c>0</c> rather than <c>3</c>), or <see cref="double.NaN"/> if
    /// fewer than four values have been added or every value added was identical.
    /// </summary>
    public readonly double Kurtosis
    {
        get
        {
            if (_count < 4 || _m2 == 0d)
            {
                return double.NaN;
            }

            return (double)_count * _m4 / (_m2 * _m2) - 3d;
        }
    }

    /// <summary>Accumulates a single value.</summary>
    /// <param name="value">The value to accumulate. Must be finite.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="double.NaN"/> or infinite.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The accumulator already holds <see cref="long.MaxValue"/> values.
    /// </exception>
    public void Add(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Only finite values can be accumulated.");
        }

        long n1 = _count;

        if (n1 == long.MaxValue)
        {
            throw new InvalidOperationException(
                "The accumulator already holds long.MaxValue values and cannot take another.");
        }

        _count = n1 + 1;

        if (n1 == 0)
        {
            _min = value;
            _max = value;
            _mean = value;
            return;
        }

        if (value < _min)
        {
            _min = value;
        }

        if (value > _max)
        {
            _max = value;
        }

        double delta = value - _mean;
        double deltaOverN = delta / _count;
        double deltaOverNSquared = deltaOverN * deltaOverN;
        double term = delta * deltaOverN * n1;

        _mean += deltaOverN;
        _m4 += term * deltaOverNSquared * ((double)_count * _count - 3d * _count + 3d)
            + 6d * deltaOverNSquared * _m2
            - 4d * deltaOverN * _m3;
        _m3 += term * deltaOverN * (_count - 2d) - 3d * deltaOverN * _m2;
        _m2 += term;
    }

    /// <summary>Accumulates every value in a span, in order.</summary>
    /// <param name="values">The values to accumulate. All must be finite.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// One of <paramref name="values"/> is <see cref="double.NaN"/> or infinite. Values before
    /// it have already been accumulated.
    /// </exception>
    public void Add(ReadOnlySpan<double> values)
    {
        foreach (double value in values)
        {
            Add(value);
        }
    }

    /// <summary>
    /// Folds another accumulator's state into this one, as if every value it consumed had
    /// been added to this accumulator.
    /// </summary>
    /// <param name="other">The accumulator to fold in. Merging an empty one is a no-op.</param>
    /// <exception cref="ArgumentException">
    /// The combined <see cref="Count"/> would overflow. Repeatedly merging an accumulator into
    /// itself doubles the count, so this is reachable in sixty-odd calls rather than by
    /// accumulating that many values.
    /// </exception>
    /// <remarks>
    /// Uses Chan's parallel formulas for the central moments, so the result does not depend on
    /// how the stream was split — only on floating-point associativity, which makes it close
    /// to, rather than bit-identical with, a single sequential pass.
    /// </remarks>
    public void Merge(in RunningStatistics other)
    {
        if (other._count == 0)
        {
            return;
        }

        if (_count == 0)
        {
            this = other;
            return;
        }

        long nA = _count;
        long nB = other._count;

        // Checked before anything is written, so a rejected merge leaves the accumulator whole.
        if (nB > long.MaxValue - nA)
        {
            throw new ArgumentException(
                "Merging these accumulators would overflow the count.",
                nameof(other));
        }

        long n = nA + nB;

        double delta = other._mean - _mean;
        double deltaOverN = delta / n;
        double deltaSquaredOverN = delta * deltaOverN;

        double m2 = _m2 + other._m2 + deltaSquaredOverN * nA * nB;

        double m3 = _m3 + other._m3
            + deltaSquaredOverN * deltaOverN * nA * nB * (nA - nB)
            + 3d * deltaOverN * (nA * other._m2 - nB * _m2);

        double m4 = _m4 + other._m4
            + deltaSquaredOverN * deltaOverN * deltaOverN * nA * nB * ((double)nA * nA - (double)nA * nB + (double)nB * nB)
            + 6d * deltaOverN * deltaOverN * ((double)nA * nA * other._m2 + (double)nB * nB * _m2)
            + 4d * deltaOverN * (nA * other._m3 - nB * _m3);

        _mean += deltaOverN * nB;
        _m2 = m2;
        _m3 = m3;
        _m4 = m4;
        _count = n;

        if (other._min < _min)
        {
            _min = other._min;
        }

        if (other._max > _max)
        {
            _max = other._max;
        }
    }

    /// <summary>
    /// Returns a new accumulator holding the combined state of two accumulators, leaving both
    /// operands unchanged.
    /// </summary>
    /// <param name="left">The first accumulator.</param>
    /// <param name="right">The second accumulator.</param>
    /// <returns>The combined accumulator.</returns>
    public static RunningStatistics Combine(in RunningStatistics left, in RunningStatistics right)
    {
        RunningStatistics combined = left;
        combined.Merge(right);
        return combined;
    }

    /// <summary>Resets the accumulator to its empty state.</summary>
    public void Clear()
    {
        this = default;
    }
}
