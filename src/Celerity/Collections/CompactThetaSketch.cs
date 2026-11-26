using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// An immutable, sorted snapshot of a <see cref="ThetaSketch{T,THasher}"/>, and the operand
/// and result type of theta-sketch set expressions: <see cref="Union"/>,
/// <see cref="Intersect"/> and <see cref="Except"/> estimate the distinct counts of
/// <c>A ∪ B</c>, <c>A ∩ B</c> and <c>A \ B</c> from two sketches without either set.
/// </summary>
/// <remarks>
/// <para>
/// Create one with <see cref="ThetaSketch{T,THasher}.Compact"/>. A compact sketch holds the
/// retained hash values in ascending order and the sampling threshold; it cannot be added to,
/// which is what lets it also stand for the result of an intersection or difference — a
/// sample of a set that no stream of additions produced. Each set operation is one merge pass
/// over the two sorted value arrays, <c>O(n + m)</c> in their retained counts, and returns a
/// new compact sketch, so expressions compose: <c>Intersect(Union(a, b), c)</c> estimates
/// <c>|(A ∪ B) ∩ C|</c>.
/// </para>
/// <para>
/// Every operation first restricts both operands to the smaller of their two sampling
/// thresholds, so the result samples at the coarser rate of the two. Its error is governed by
/// how many retained values land in the result: an intersection that holds a small share of
/// the union is estimated from few values, which <see cref="GetLowerBound"/> and
/// <see cref="GetUpperBound"/> widen to reflect. When an operand is empty and exact — a
/// sketch of the empty set — an intersection with it, or a difference taken from it, is
/// exactly empty.
/// </para>
/// <para>
/// <see cref="Union"/> keeps every value below the common threshold, so its result can hold
/// as many values as both operands together. To union many sketches in bounded memory,
/// accumulate them into a <see cref="ThetaSketch{T,THasher}"/> with
/// <see cref="ThetaSketch{T,THasher}.UnionWith(CompactThetaSketch{T,THasher})"/>, which trims
/// to its own nominal size.
/// </para>
/// <para>
/// Instances are immutable and safe to share across threads.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the set's elements.</typeparam>
/// <typeparam name="THasher">
/// The hasher the source sketches were built with. Only sketches that share it can be
/// combined, which the type system enforces.
/// </typeparam>
public sealed class CompactThetaSketch<T, THasher> where THasher : struct, IHashProvider<T>
{
    private static readonly CompactThetaSketch<T, THasher> EmptySketch = new([], ThetaMath.FullThreshold);

    private readonly ulong[] _values;   // ascending, distinct, each in [1, _threshold]
    private readonly ulong _threshold;

    internal CompactThetaSketch(ulong[] values, ulong threshold)
    {
        _values = values;
        _threshold = threshold;
    }

    /// <summary>
    /// Gets the number of hash values the sketch holds. When <see cref="IsEstimationMode"/>
    /// is <c>false</c> this is the exact number of distinct elements (up to hash collisions).
    /// </summary>
    public int RetainedCount => _values.Length;

    /// <summary>
    /// Gets the sampling rate <c>θ</c> in <c>(0, 1]</c>: the probability with which any one
    /// distinct element of the summarized set is among the retained values.
    /// </summary>
    public double Theta => ThetaMath.ToTheta(_threshold);

    /// <summary>
    /// Gets a value indicating whether the sketch is sampling — whether <see cref="Theta"/> is
    /// below <c>1</c> and the cardinality is an estimate rather than an exact count.
    /// </summary>
    public bool IsEstimationMode => _threshold < ThetaMath.FullThreshold;

    /// <summary>
    /// Gets a value indicating whether the sketch is known to summarize the empty set: it holds
    /// no values and is not sampling. An estimation-mode sketch with no values — an
    /// intersection that caught nothing, say — is not empty, since the set may still have
    /// elements the sample missed; <see cref="GetUpperBound"/> says how many.
    /// </summary>
    public bool IsEmpty => _values.Length == 0 && !IsEstimationMode;

    /// <summary>
    /// Estimates the number of distinct elements in the summarized set: the retained count
    /// divided by <see cref="Theta"/>, which is exact while the sketch is not in estimation
    /// mode.
    /// </summary>
    /// <returns>The estimated number of distinct elements.</returns>
    public double EstimateCardinality() => ThetaMath.Estimate(_values.Length, _threshold);

    /// <summary>
    /// Gets an approximate lower bound on the number of distinct elements, at the given number
    /// of standard deviations. It is never below <see cref="RetainedCount"/>.
    /// </summary>
    /// <param name="standardDeviations">1, 2 or 3. The interval between the lower and upper
    /// bounds at that width covers the true count with roughly 68%, 95% or 99.7% confidence.</param>
    /// <returns>The lower bound.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="standardDeviations"/> is not 1, 2 or 3.
    /// </exception>
    public double GetLowerBound(int standardDeviations) =>
        ThetaMath.LowerBound(_values.Length, _threshold, standardDeviations);

    /// <summary>
    /// Gets an approximate upper bound on the number of distinct elements, at the given number
    /// of standard deviations. For an estimation-mode sketch with no retained values it is
    /// still positive: the sample saw nothing, but at the sampling rate it could have missed a
    /// few elements.
    /// </summary>
    /// <param name="standardDeviations">1, 2 or 3. The interval between the lower and upper
    /// bounds at that width covers the true count with roughly 68%, 95% or 99.7% confidence.</param>
    /// <returns>The upper bound.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="standardDeviations"/> is not 1, 2 or 3.
    /// </exception>
    public double GetUpperBound(int standardDeviations) =>
        ThetaMath.UpperBound(_values.Length, _threshold, standardDeviations);

    /// <summary>
    /// Returns a sketch of the union <c>A ∪ B</c> of the sets two sketches summarize.
    /// </summary>
    /// <param name="a">The first operand.</param>
    /// <param name="b">The second operand.</param>
    /// <returns>A new compact sketch holding every value of either operand below their common
    /// threshold.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="a"/> or <paramref name="b"/> is
    /// <c>null</c>.</exception>
    public static CompactThetaSketch<T, THasher> Union(CompactThetaSketch<T, THasher> a, CompactThetaSketch<T, THasher> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        ulong threshold = Math.Min(a._threshold, b._threshold);
        ReadOnlySpan<ulong> x = Below(a._values, threshold);
        ReadOnlySpan<ulong> y = Below(b._values, threshold);

        var result = new ulong[x.Length + y.Length];
        int i = 0, j = 0, n = 0;
        while (i < x.Length && j < y.Length)
        {
            ulong u = x[i], v = y[j];
            if (u < v)
            {
                result[n++] = u;
                i++;
            }
            else if (v < u)
            {
                result[n++] = v;
                j++;
            }
            else
            {
                result[n++] = u;
                i++;
                j++;
            }
        }

        x[i..].CopyTo(result.AsSpan(n));
        n += x.Length - i;
        y[j..].CopyTo(result.AsSpan(n));
        n += y.Length - j;

        return new CompactThetaSketch<T, THasher>(Trim(result, n), threshold);
    }

    /// <summary>
    /// Returns a sketch of the intersection <c>A ∩ B</c> of the sets two sketches summarize.
    /// </summary>
    /// <param name="a">The first operand.</param>
    /// <param name="b">The second operand.</param>
    /// <returns>A new compact sketch holding the values both operands retain below their common
    /// threshold, or an exact empty sketch when either operand is <see cref="IsEmpty"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="a"/> or <paramref name="b"/> is
    /// <c>null</c>.</exception>
    public static CompactThetaSketch<T, THasher> Intersect(CompactThetaSketch<T, THasher> a, CompactThetaSketch<T, THasher> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.IsEmpty || b.IsEmpty)
            return EmptySketch;

        ulong threshold = Math.Min(a._threshold, b._threshold);
        ReadOnlySpan<ulong> x = Below(a._values, threshold);
        ReadOnlySpan<ulong> y = Below(b._values, threshold);

        var result = new ulong[Math.Min(x.Length, y.Length)];
        int i = 0, j = 0, n = 0;
        while (i < x.Length && j < y.Length)
        {
            ulong u = x[i], v = y[j];
            if (u < v)
            {
                i++;
            }
            else if (v < u)
            {
                j++;
            }
            else
            {
                result[n++] = u;
                i++;
                j++;
            }
        }

        return new CompactThetaSketch<T, THasher>(Trim(result, n), threshold);
    }

    /// <summary>
    /// Returns a sketch of the difference <c>A \ B</c>: the elements of the set
    /// <paramref name="a"/> summarizes that are not in the set <paramref name="b"/> summarizes.
    /// </summary>
    /// <param name="a">The set to take elements from.</param>
    /// <param name="b">The set whose elements are removed.</param>
    /// <returns>A new compact sketch holding the values <paramref name="a"/> retains below the
    /// common threshold that <paramref name="b"/> does not, or an exact empty sketch when
    /// <paramref name="a"/> is <see cref="IsEmpty"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="a"/> or <paramref name="b"/> is
    /// <c>null</c>.</exception>
    public static CompactThetaSketch<T, THasher> Except(CompactThetaSketch<T, THasher> a, CompactThetaSketch<T, THasher> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.IsEmpty)
            return EmptySketch;

        ulong threshold = Math.Min(a._threshold, b._threshold);
        ReadOnlySpan<ulong> x = Below(a._values, threshold);
        ReadOnlySpan<ulong> y = Below(b._values, threshold);

        var result = new ulong[x.Length];
        int i = 0, j = 0, n = 0;
        while (i < x.Length && j < y.Length)
        {
            ulong u = x[i], v = y[j];
            if (u < v)
            {
                result[n++] = u;
                i++;
            }
            else
            {
                // v < u: b's value is not in a; equal: the element is in both. Either way
                // advance b, and on a match also skip a's copy.
                if (u == v)
                    i++;
                j++;
            }
        }

        x[i..].CopyTo(result.AsSpan(n));
        n += x.Length - i;

        return new CompactThetaSketch<T, THasher>(Trim(result, n), threshold);
    }

    internal ulong Threshold => _threshold;

    internal ReadOnlySpan<ulong> Values => _values;

    // The prefix of an ascending array at or below the threshold.
    private static ReadOnlySpan<ulong> Below(ulong[] values, ulong threshold)
    {
        int index = Array.BinarySearch(values, threshold);
        return values.AsSpan(0, index >= 0 ? index + 1 : ~index);
    }

    private static ulong[] Trim(ulong[] values, int length) =>
        length == values.Length ? values : values[..length];
}
