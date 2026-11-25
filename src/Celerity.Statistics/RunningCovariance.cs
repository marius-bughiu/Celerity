using System.Numerics;
using System.Runtime.InteropServices;

namespace Celerity.Statistics;

/// <summary>
/// A single-pass, allocation-free accumulator for how two streams of <see cref="double"/>
/// values move together: both means and variances, their covariance, the Pearson correlation,
/// and the least-squares line of <c>y</c> on <c>x</c> (slope, intercept and R²).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="RunningStatistics"/> summarises one variable; the BCL summarises none. Relating
/// two leaves a caller two idioms, each with a problem. A LINQ computation — average both
/// sequences, then <c>Zip</c> and sum the products of deviations — is a <em>two-pass</em> shape
/// that needs both sequences in memory and re-enumerable, so it cannot summarise a stream. The
/// one-pass shortcut, accumulating <c>Σx</c>, <c>Σy</c>, <c>Σxy</c> and <c>Σx²</c> and
/// subtracting, cancels catastrophically once the means are large relative to the spread: it
/// can report a correlation outside <c>[−1, 1]</c> or a slope with the wrong sign.
/// </para>
/// <para>
/// This type uses the bivariate form of Welford's recurrence, which updates both means and the
/// three central co-moments directly and never forms a large intermediate to subtract away.
/// Adding a pair is a handful of multiplications with no allocation, and the state is six
/// fields regardless of how many pairs pass through it. Adding a span is faster still: it
/// computes each 256-pair block directly and merges it, rather than running the recurrence.
/// </para>
/// <para>
/// <strong>This is a mutable struct, deliberately</strong>, for the reasons given on
/// <see cref="RunningStatistics"/>: <c>default</c> is a valid empty accumulator, an array of
/// per-bucket accumulators lives inline, and the add path has no indirection. A copy is an
/// independent snapshot, so <c>list[i].Add(x, y)</c> updates a temporary and is lost —
/// accumulate through a <c>ref</c>.
/// </para>
/// <para>
/// <strong>The domain is pairs of finite doubles.</strong> <see cref="Add(double, double)"/>
/// rejects <see cref="double.NaN"/> and the infinities in either coordinate rather than
/// accumulating them. As with <see cref="RunningStatistics"/>, second moments square their
/// deviations, so a spread approaching <c>sqrt(double.MaxValue)</c> — about <c>1.3e154</c> —
/// overflows the variances and covariance whatever algorithm computes them; scale values at
/// that magnitude before accumulating.
/// </para>
/// <para>
/// Undefined statistics return <see cref="double.NaN"/> rather than throwing:
/// <see cref="MeanX"/>, <see cref="MeanY"/>, the population variances and
/// <see cref="PopulationCovariance"/> on an empty accumulator; the sample variances, standard
/// deviations and <see cref="Covariance"/> below two pairs; <see cref="Slope"/> and
/// <see cref="Intercept"/> when every <c>x</c> was identical (a vertical line has no slope);
/// and <see cref="Correlation"/> and <see cref="RSquared"/> when either coordinate has zero
/// variance.
/// </para>
/// <para>
/// Two accumulators over disjoint parts of a stream combine exactly with
/// <see cref="Merge(in RunningCovariance)"/>, which applies Chan's pairwise update to the
/// co-moment as well as the variances, so a sharded or parallel pass matches a single
/// sequential one up to floating-point associativity.
/// </para>
/// <para>
/// Not thread-safe. Concurrent readers are fine; a concurrent <see cref="Add(double, double)"/>
/// is not.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var fit = new RunningCovariance();
/// foreach (Request request in stream)
/// {
///     fit.Add(request.PayloadKilobytes, request.LatencyMilliseconds);
/// }
///
/// Console.WriteLine($"r = {fit.Correlation:F3}; latency ≈ {fit.Slope:F2} ms/KB × size + {fit.Intercept:F1} ms");
/// </code>
/// </example>
public struct RunningCovariance
{
    // Pairs per span block: small enough that the second pass re-reads the block from L1, and
    // a multiple of every Vector<double> width so only the final block has a scalar tail.
    private const int BlockLength = 256;

    private long _count;
    private double _meanX;
    private double _meanY;
    private double _m2X;
    private double _m2Y;
    private double _cXY;

    /// <summary>
    /// Initializes an empty accumulator. Identical to <c>default(RunningCovariance)</c>, which
    /// is also a valid empty accumulator.
    /// </summary>
    public RunningCovariance()
    {
    }

    /// <summary>
    /// Initializes an accumulator that has already consumed the pairs
    /// <c>(x[i], y[i])</c>.
    /// </summary>
    /// <param name="x">The first coordinate of each pair. All must be finite.</param>
    /// <param name="y">The second coordinate of each pair. All must be finite.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="x"/> and <paramref name="y"/> have different lengths.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A coordinate is <see cref="double.NaN"/> or infinite.
    /// </exception>
    public RunningCovariance(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
    {
        this = default;
        Add(x, y);
    }

    /// <summary>Gets the number of pairs accumulated.</summary>
    public readonly long Count => _count;

    /// <summary>
    /// Gets the mean of the <c>x</c> coordinates, or <see cref="double.NaN"/> if no pairs have
    /// been added.
    /// </summary>
    public readonly double MeanX => _count == 0 ? double.NaN : _meanX;

    /// <summary>
    /// Gets the mean of the <c>y</c> coordinates, or <see cref="double.NaN"/> if no pairs have
    /// been added.
    /// </summary>
    public readonly double MeanY => _count == 0 ? double.NaN : _meanY;

    /// <summary>
    /// Gets the sample variance of <c>x</c> (the <c>n − 1</c> denominator), or
    /// <see cref="double.NaN"/> if fewer than two pairs have been added.
    /// </summary>
    public readonly double VarianceX => _count < 2 ? double.NaN : _m2X / (_count - 1);

    /// <summary>
    /// Gets the sample variance of <c>y</c> (the <c>n − 1</c> denominator), or
    /// <see cref="double.NaN"/> if fewer than two pairs have been added.
    /// </summary>
    public readonly double VarianceY => _count < 2 ? double.NaN : _m2Y / (_count - 1);

    /// <summary>
    /// Gets the population variance of <c>x</c> (the <c>n</c> denominator), or
    /// <see cref="double.NaN"/> if no pairs have been added.
    /// </summary>
    public readonly double PopulationVarianceX => _count == 0 ? double.NaN : _m2X / _count;

    /// <summary>
    /// Gets the population variance of <c>y</c> (the <c>n</c> denominator), or
    /// <see cref="double.NaN"/> if no pairs have been added.
    /// </summary>
    public readonly double PopulationVarianceY => _count == 0 ? double.NaN : _m2Y / _count;

    /// <summary>
    /// Gets the sample standard deviation of <c>x</c> — the square root of
    /// <see cref="VarianceX"/>.
    /// </summary>
    public readonly double StandardDeviationX => Math.Sqrt(VarianceX);

    /// <summary>
    /// Gets the sample standard deviation of <c>y</c> — the square root of
    /// <see cref="VarianceY"/>.
    /// </summary>
    public readonly double StandardDeviationY => Math.Sqrt(VarianceY);

    /// <summary>
    /// Gets the sample covariance (the <c>n − 1</c> denominator), or <see cref="double.NaN"/>
    /// if fewer than two pairs have been added.
    /// </summary>
    public readonly double Covariance => _count < 2 ? double.NaN : _cXY / (_count - 1);

    /// <summary>
    /// Gets the population covariance (the <c>n</c> denominator), or <see cref="double.NaN"/>
    /// if no pairs have been added.
    /// </summary>
    public readonly double PopulationCovariance => _count == 0 ? double.NaN : _cXY / _count;

    /// <summary>
    /// Gets the Pearson correlation coefficient in <c>[−1, 1]</c>, or <see cref="double.NaN"/>
    /// if fewer than two pairs have been added or either coordinate has zero variance.
    /// </summary>
    /// <remarks>
    /// Clamped to <c>[−1, 1]</c>: for perfectly collinear data, rounding can otherwise land a
    /// unit or two in the last place outside the range a correlation is defined on.
    /// </remarks>
    public readonly double Correlation
    {
        get
        {
            if (_count < 2 || _m2X == 0d || _m2Y == 0d)
            {
                return double.NaN;
            }

            // Divided down one root at a time rather than by sqrt(_m2X * _m2Y): that product
            // can overflow or underflow while the ratio is an ordinary number, and
            // |_cXY| <= sqrt(_m2X) * sqrt(_m2Y) (Cauchy–Schwarz) keeps each step in range.
            return Math.Clamp(_cXY / Math.Sqrt(_m2X) / Math.Sqrt(_m2Y), -1d, 1d);
        }
    }

    /// <summary>
    /// Gets the slope of the ordinary-least-squares line of <c>y</c> on <c>x</c>, or
    /// <see cref="double.NaN"/> if fewer than two pairs have been added or every <c>x</c> was
    /// identical.
    /// </summary>
    public readonly double Slope => _count < 2 || _m2X == 0d ? double.NaN : _cXY / _m2X;

    /// <summary>
    /// Gets the intercept of the ordinary-least-squares line of <c>y</c> on <c>x</c> — the
    /// fitted <c>y</c> at <c>x = 0</c> — or <see cref="double.NaN"/> whenever
    /// <see cref="Slope"/> is.
    /// </summary>
    public readonly double Intercept => _meanY - (Slope * _meanX);

    /// <summary>
    /// Gets the coefficient of determination of the least-squares line — the fraction of the
    /// variance in <c>y</c> it explains, which for a one-variable fit is the square of
    /// <see cref="Correlation"/> — or <see cref="double.NaN"/> whenever
    /// <see cref="Correlation"/> is.
    /// </summary>
    /// <remarks>
    /// A constant <c>y</c> reports <see cref="double.NaN"/> rather than <c>1</c>: the line fits
    /// exactly, but there is no variance for it to explain, and <c>0 / 0</c> is the honest
    /// answer.
    /// </remarks>
    public readonly double RSquared
    {
        get
        {
            double r = Correlation;
            return r * r;
        }
    }

    /// <summary>Accumulates a single pair.</summary>
    /// <param name="x">The first coordinate. Must be finite.</param>
    /// <param name="y">The second coordinate. Must be finite.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="x"/> or <paramref name="y"/> is <see cref="double.NaN"/> or infinite.
    /// Nothing is accumulated.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The accumulator already holds <see cref="long.MaxValue"/> pairs.
    /// </exception>
    public void Add(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            throw NotFinite(x, y);
        }

        long count = _count;
        if (count == long.MaxValue)
        {
            throw new InvalidOperationException(
                "The accumulator already holds long.MaxValue pairs and cannot take another.");
        }

        count++;
        _count = count;

        // From the empty state the means start at zero, so the first pair needs no special
        // case: the means become (x, y) exactly and the co-moment weight (n - 1) / n is zero.
        //
        // The reciprocal depends only on the count, so it is computed off the critical path:
        // each mean's loop-carried chain is a subtract and a multiply-add rather than a
        // division, which is what bounds a per-pair recurrence. It costs at most one extra
        // rounding per increment, an increment that is already a 1/n share of the delta.
        double reciprocal = 1d / count;
        double dx = x - _meanX;
        double dy = y - _meanY;
        _meanX += dx * reciprocal;
        _meanY += dy * reciprocal;

        // The co-moments grow by dx * dy * (n - 1) / n, formed from the deltas taken before
        // the means moved. The equivalent residual form, dx * (x - newMean), depends on how the
        // new mean rounded: when it lands exactly on x (the mean of 1e16 + 2 and 1e16 is
        // 1e16), the residual is zero and the spread is lost. The weight is applied to one
        // factor before the product, so no intermediate exceeds the result. For the cross term
        // that factor is the larger delta: weighting a subnormal one first can round it to
        // zero and erase a covariance the product itself could represent.
        double weight = (count - 1) * reciprocal;
        double weightedDx = dx * weight;
        double weightedDy = dy * weight;
        _m2X += dx * weightedDx;
        _m2Y += dy * weightedDy;
        _cXY += Math.Abs(dx) >= Math.Abs(dy) ? dy * weightedDx : dx * weightedDy;
    }

    /// <summary>
    /// Accumulates every pair <c>(x[i], y[i])</c>, in order.
    /// </summary>
    /// <param name="x">The first coordinate of each pair. All must be finite.</param>
    /// <param name="y">The second coordinate of each pair. All must be finite.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="x"/> and <paramref name="y"/> have different lengths. Nothing is
    /// accumulated.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A coordinate is <see cref="double.NaN"/> or infinite. The pairs before it have already
    /// been accumulated.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The spans hold more pairs than the accumulator has room for before
    /// <see cref="long.MaxValue"/>. Nothing is accumulated.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Faster than one <see cref="Add(double, double)"/> per pair, because it does not run the
    /// per-pair recurrence at all. It cuts the spans into blocks of 256 pairs, computes each
    /// block's means and co-moments directly with a corrected two-pass over data already in
    /// cache (SIMD-wide where the hardware allows), and folds
    /// the block in with the same update <see cref="Merge(in RunningCovariance)"/> uses. The
    /// result therefore agrees with per-pair adds up to floating-point rounding rather than
    /// bit-for-bit, and its last bits can differ between machines with different vector
    /// widths.
    /// </para>
    /// <para>
    /// Each block's mean is held as a single <see cref="double"/>. When the spread of the data
    /// is down at the resolution of the values themselves — <c>1e16</c> and <c>1e16 + 2</c>,
    /// whose mean is not representable — the rounding of each block mean drops variance
    /// <em>between</em> blocks that per-pair adds keep. Subtract a reference value (any one
    /// sample will do) from each coordinate before accumulating such data; the covariance,
    /// correlation and slope do not change under a shift.
    /// </para>
    /// </remarks>
    public void Add(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
    {
        if (x.Length != y.Length)
        {
            throw new ArgumentException(
                "The two coordinate spans must have the same length.",
                nameof(y));
        }

        if (x.Length > long.MaxValue - _count)
        {
            throw new InvalidOperationException(
                "The accumulator does not have room for this many more pairs before long.MaxValue.");
        }

        while (x.Length > BlockLength)
        {
            AddBlock(x[..BlockLength], y[..BlockLength]);
            x = x[BlockLength..];
            y = y[BlockLength..];
        }

        if (!x.IsEmpty)
        {
            AddBlock(x, y);
        }
    }

    /// <summary>
    /// Folds another accumulator's state into this one, as if every pair it consumed had been
    /// added to this accumulator.
    /// </summary>
    /// <param name="other">The accumulator to fold in. Merging an empty one is a no-op.</param>
    /// <exception cref="ArgumentException">
    /// The combined <see cref="Count"/> would overflow. Repeatedly merging an accumulator into
    /// itself doubles the count, so this is reachable in sixty-odd calls.
    /// </exception>
    /// <remarks>
    /// Uses Chan's pairwise update for both variances and the co-moment, so the result does
    /// not depend on how the stream was split — only on floating-point associativity, which
    /// makes it close to, rather than bit-identical with, a single sequential pass.
    /// </remarks>
    public void Merge(in RunningCovariance other)
    {
        // Checked before anything is written, so a rejected merge leaves the accumulator whole.
        if (other._count > long.MaxValue - _count)
        {
            throw new ArgumentException(
                "Merging these accumulators would overflow the count.",
                nameof(other));
        }

        MergeMoments(other._count, other._meanX, other._meanY, other._m2X, other._m2Y, other._cXY);
    }

    /// <summary>
    /// Returns a new accumulator holding the combined state of two accumulators, leaving both
    /// operands unchanged.
    /// </summary>
    /// <param name="left">The first accumulator.</param>
    /// <param name="right">The second accumulator.</param>
    /// <returns>The combined accumulator.</returns>
    /// <exception cref="ArgumentException">
    /// The combined <see cref="Count"/> would overflow — see
    /// <see cref="Merge(in RunningCovariance)"/>, which this delegates to.
    /// </exception>
    public static RunningCovariance Combine(in RunningCovariance left, in RunningCovariance right)
    {
        RunningCovariance combined = left;
        combined.Merge(right);
        return combined;
    }

    /// <summary>Resets the accumulator to its empty state.</summary>
    public void Clear()
    {
        this = default;
    }

    /// <summary>
    /// Folds in the moments of <paramref name="n"/> pairs. The caller has already checked
    /// that the combined count fits.
    /// </summary>
    private void MergeMoments(long n, double meanX, double meanY, double m2X, double m2Y, double cXY)
    {
        if (n == 0)
        {
            return;
        }

        long nA = _count;
        if (nA == 0)
        {
            // Adopted outright rather than run through the update below, where a zero weight
            // times an overflowed squared delta would be NaN.
            _count = n;
            _meanX = meanX;
            _meanY = meanY;
            _m2X = m2X;
            _m2Y = m2Y;
            _cXY = cXY;
            return;
        }

        long total = nA + n;

        double dx = meanX - _meanX;
        double dy = meanY - _meanY;

        // nA * n / total, formed from the fraction n / total so no intermediate exceeds the
        // larger count and the mean shift never multiplies a delta by a raw count.
        double fraction = (double)n / total;

        // Each delta is scaled by the square root of the weight before the product, so the
        // only way the correction overflows is if the correction itself does: dx * dx alone
        // overflows from a spread of about 1.3e154, where a weight of 1/2 brings it back in.
        double scale = Math.Sqrt(fraction * nA);
        double scaledDx = dx * scale;
        double scaledDy = dy * scale;

        _m2X += m2X + (scaledDx * scaledDx);
        _m2Y += m2Y + (scaledDy * scaledDy);
        _cXY += cXY + (scaledDx * scaledDy);
        _meanX += dx * fraction;
        _meanY += dy * fraction;
        _count = total;
    }

    /// <summary>
    /// Accumulates one block of at most <see cref="BlockLength"/> pairs: a corrected two-pass
    /// over the block, then a merge. Falls back to per-pair adds — which validate, and stop at
    /// the first bad pair with the pairs before it accumulated — when a block sum is not
    /// finite (the block holds a NaN or an infinity, or finite values large enough to overflow
    /// a sum), or when its squared deviations overflow or underflow.
    /// </summary>
    private void AddBlock(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
    {
        int length = x.Length;
        y = y[..length];

        int vectorized = length - (length % Vector<double>.Count);
        ReadOnlySpan<Vector<double>> vx = MemoryMarshal.Cast<double, Vector<double>>(x[..vectorized]);
        ReadOnlySpan<Vector<double>> vy = MemoryMarshal.Cast<double, Vector<double>>(y[..vectorized]);

        Vector<double> sumXv = Vector<double>.Zero;
        Vector<double> sumYv = Vector<double>.Zero;
        Vector<double> minXv = new(x[0]);
        Vector<double> maxXv = minXv;
        Vector<double> minYv = new(y[0]);
        Vector<double> maxYv = minYv;
        for (int i = 0; i < vx.Length; i++)
        {
            Vector<double> xi = vx[i];
            Vector<double> yi = vy[i];
            sumXv += xi;
            sumYv += yi;
            minXv = Vector.Min(minXv, xi);
            maxXv = Vector.Max(maxXv, xi);
            minYv = Vector.Min(minYv, yi);
            maxYv = Vector.Max(maxYv, yi);
        }

        double sumX = Vector.Sum(sumXv);
        double sumY = Vector.Sum(sumYv);
        double minX = minXv[0];
        double maxX = maxXv[0];
        double minY = minYv[0];
        double maxY = maxYv[0];
        for (int lane = 1; lane < Vector<double>.Count; lane++)
        {
            minX = Math.Min(minX, minXv[lane]);
            maxX = Math.Max(maxX, maxXv[lane]);
            minY = Math.Min(minY, minYv[lane]);
            maxY = Math.Max(maxY, maxYv[lane]);
        }

        for (int i = vectorized; i < length; i++)
        {
            double xi = x[i];
            double yi = y[i];
            sumX += xi;
            sumY += yi;
            minX = Math.Min(minX, xi);
            maxX = Math.Max(maxX, xi);
            minY = Math.Min(minY, yi);
            maxY = Math.Max(maxY, yi);
        }

        if (!double.IsFinite(sumX) || !double.IsFinite(sumY))
        {
            AddPairs(x, y);
            return;
        }

        // A mean always lies within the range, and clamping it there makes a block with no
        // spread exact: sum / length can land an ulp off a repeated value (3 x 0.1 / 3 is not
        // 0.1), which would leave every deviation a rounding away from zero and report a tiny
        // variance — and a meaningless slope — where the answer is exactly zero.
        double meanX = Math.Clamp(sumX / length, minX, maxX);
        double meanY = Math.Clamp(sumY / length, minY, maxY);

        // Second pass over deviations from the block means. The deviation sums would be zero
        // with exact arithmetic; folding them back in (the "corrected" two-pass) cancels the
        // rounding error in the means themselves.
        var meanXv = new Vector<double>(meanX);
        var meanYv = new Vector<double>(meanY);
        Vector<double> devXv = Vector<double>.Zero;
        Vector<double> devYv = Vector<double>.Zero;
        Vector<double> xxv = Vector<double>.Zero;
        Vector<double> yyv = Vector<double>.Zero;
        Vector<double> xyv = Vector<double>.Zero;
        for (int i = 0; i < vx.Length; i++)
        {
            Vector<double> dx = vx[i] - meanXv;
            Vector<double> dy = vy[i] - meanYv;
            devXv += dx;
            devYv += dy;
            xxv += dx * dx;
            yyv += dy * dy;
            xyv += dx * dy;
        }

        double devX = Vector.Sum(devXv);
        double devY = Vector.Sum(devYv);
        double xx = Vector.Sum(xxv);
        double yy = Vector.Sum(yyv);
        double xy = Vector.Sum(xyv);
        for (int i = vectorized; i < length; i++)
        {
            double dx = x[i] - meanX;
            double dy = y[i] - meanY;
            devX += dx;
            devY += dy;
            xx += dx * dx;
            yy += dy * dy;
            xy += dx * dy;
        }

        // Squared deviations live at the square of the data's scale, so they can leave the
        // double range where the data and the per-pair recurrence do not: a spread near 1e154
        // overflows them, and one near 1e-162 underflows them to zero. Either way the block's
        // answer is wrong where per-pair adds are not, so the block falls back to them. A
        // coordinate with spread whose squares summed to zero is exactly the underflow case.
        if (!double.IsFinite(xx + yy) || (xx == 0d && maxX > minX) || (yy == 0d && maxY > minY))
        {
            AddPairs(x, y);
            return;
        }

        double correctionX = devX / length;
        double correctionY = devY / length;

        // The corrections are tiny next to the sums they adjust, but they can still push a
        // sum of squares a rounding below zero when the block has no spread; a co-moment of a
        // single variable is never negative, so that is clamped away.
        MergeMoments(
            length,
            meanX + correctionX,
            meanY + correctionY,
            Math.Max(xx - (devX * correctionX), 0d),
            Math.Max(yy - (devY * correctionY), 0d),
            xy - (devX * correctionY));
    }

    /// <summary>
    /// The span path's fallback: one <see cref="Add(double, double)"/> per pair, which
    /// validates each pair and stops at the first bad one with the pairs before it accumulated.
    /// </summary>
    private void AddPairs(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
    {
        for (int i = 0; i < x.Length; i++)
        {
            Add(x[i], y[i]);
        }
    }

    // Built out of line so the add path stays small enough to inline. Called only when at
    // least one coordinate is not finite; names the first one that is not.
    private static ArgumentOutOfRangeException NotFinite(double x, double y)
        => double.IsFinite(x)
            ? new ArgumentOutOfRangeException(nameof(y), y, "Only finite values can be accumulated.")
            : new ArgumentOutOfRangeException(nameof(x), x, "Only finite values can be accumulated.");
}
