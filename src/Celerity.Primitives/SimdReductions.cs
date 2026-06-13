using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Primitives;

/// <summary>
/// Fused and specialized SIMD reductions over a span that fill genuine gaps in
/// <c>System.Numerics.Tensors.TensorPrimitives</c>: a single-pass simultaneous
/// <see cref="MinMax(System.ReadOnlySpan{int})">minimum-and-maximum</see> and an overflow-checked
/// <see cref="CheckedSum(System.ReadOnlySpan{int})">integer sum</see> (issue #197).
/// </summary>
/// <remarks>
/// <para>
/// <c>System.Numerics.Tensors.TensorPrimitives</c> is generic over <see cref="INumber{TSelf}"/> and
/// SIMD/AVX-512 accelerated for <c>Sum</c>, <c>Min</c>, <c>Max</c>, <c>Dot</c>, <c>IndexOfMax</c>, etc. — do
/// <strong>not</strong> reimplement those. <see cref="SimdReductions"/> ships only the candidates that beat the
/// BCL on a documented workload:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <strong><see cref="MinMax(System.ReadOnlySpan{int})"/></strong> — the textbook <em>fused</em> reduction:
/// computing both extrema as <c>TensorPrimitives.Min(s)</c> + <c>TensorPrimitives.Max(s)</c> reads the span
/// <strong>twice</strong>; this folds both running vectors in a <strong>single pass</strong>, so on a
/// bandwidth-bound span it does the same work for roughly half the memory traffic.
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong><see cref="CheckedSum(System.ReadOnlySpan{int})"/></strong> — fills a correctness gap:
/// <c>TensorPrimitives.Sum</c> wraps silently on integer overflow, and a scalar <c>checked</c> loop cannot
/// vectorize (the per-element overflow check has a side effect). This widens each <see cref="int"/> lane to
/// <see cref="long"/> so the SIMD accumulation provably cannot overflow, then range-checks only the final
/// narrowing — a vectorized sum that still throws <see cref="OverflowException"/> on a true overflow.
/// </description>
/// </item>
/// </list>
/// <para>
/// Both methods use portable <see cref="Vector{T}"/> with a scalar tail, so they accelerate on any
/// hardware-SIMD platform and remain correct (and allocation-free) everywhere else. They are AOT-safe.
/// </para>
/// <para>
/// Deliberately <strong>not</strong> shipped from the #197 spike: an integer <em>histogram / bincount</em>. Its
/// only BCL alternative is LINQ <c>GroupBy().Count()</c>, and the win there is purely allocation avoidance,
/// achievable with a one-line <c>counts[values[i]]++</c> loop — not enough novelty to warrant a named primitive
/// (the histogram's scatter pattern does not vectorize portably). Reach for that loop directly.
/// </para>
/// </remarks>
public static class SimdReductions
{
    /// <summary>
    /// Returns the minimum and maximum of <paramref name="values"/> in a <strong>single pass</strong> — the
    /// fused alternative to a separate <c>Min</c> and <c>Max</c>, which would read the span twice.
    /// </summary>
    /// <param name="values">The values to reduce. Must be non-empty.</param>
    /// <returns>A tuple of the smallest (<c>Min</c>) and largest (<c>Max</c>) element.</returns>
    /// <exception cref="ArgumentException"><paramref name="values"/> is empty.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (int Min, int Max) MinMax(ReadOnlySpan<int> values) => MinMaxCore(values);

    /// <inheritdoc cref="MinMax(System.ReadOnlySpan{int})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (long Min, long Max) MinMax(ReadOnlySpan<long> values) => MinMaxCore(values);

    /// <inheritdoc cref="MinMax(System.ReadOnlySpan{int})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (uint Min, uint Max) MinMax(ReadOnlySpan<uint> values) => MinMaxCore(values);

    /// <inheritdoc cref="MinMax(System.ReadOnlySpan{int})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (ulong Min, ulong Max) MinMax(ReadOnlySpan<ulong> values) => MinMaxCore(values);

    /// <summary>
    /// Sums <paramref name="values"/> and throws if the true sum does not fit in an <see cref="int"/> —
    /// a SIMD-accelerated, overflow-checked alternative to the silently-wrapping <c>TensorPrimitives.Sum</c>.
    /// </summary>
    /// <param name="values">The values to sum. An empty span sums to <c>0</c>.</param>
    /// <returns>The sum of every element.</returns>
    /// <exception cref="OverflowException">The mathematical sum is outside the range of <see cref="int"/>.</exception>
    /// <remarks>
    /// Each <see cref="int"/> is widened to a <see cref="long"/> lane before accumulation, so the SIMD reduction
    /// itself cannot overflow for any reachable span (at most <see cref="int.MaxValue"/> elements, each within
    /// <see cref="int"/> range, sum to a magnitude below 2<sup>62</sup>); only the final narrowing to
    /// <see cref="int"/> is range-checked. The result therefore equals the exact mathematical sum whenever it is
    /// returned.
    /// </remarks>
    public static int CheckedSum(ReadOnlySpan<int> values)
    {
        long sum = 0;
        int i = 0;

        if (Vector.IsHardwareAccelerated && values.Length >= Vector<int>.Count)
        {
            Vector<long> acc = Vector<long>.Zero;
            int upper = values.Length - Vector<int>.Count;
            for (; i <= upper; i += Vector<int>.Count)
            {
                var v = new Vector<int>(values.Slice(i, Vector<int>.Count));
                // Widen the int lanes to long so the running total can never overflow mid-pass.
                Vector.Widen(v, out Vector<long> lo, out Vector<long> hi);
                acc += lo + hi;
            }

            for (int lane = 0; lane < Vector<long>.Count; lane++)
                sum += acc[lane];
        }

        // Scalar tail (and the whole span when SIMD is unavailable or the span is shorter than one vector).
        for (; i < values.Length; i++)
            sum += values[i];

        return checked((int)sum);
    }

    /// <summary>
    /// Shared fused min/max body. Folds two running <see cref="Vector{T}"/> accumulators over the span in one
    /// pass, then horizontally reduces the lanes, with a scalar tail for the remainder. Only instantiated for the
    /// vector-supported integer overloads above, so <see cref="Vector{T}"/> is always a supported element type.
    /// </summary>
    private static (T Min, T Max) MinMaxCore<T>(ReadOnlySpan<T> values)
        where T : struct, INumber<T>
    {
        if (values.IsEmpty)
            throw new ArgumentException("Span must be non-empty to compute a minimum and maximum.", nameof(values));

        int i = 0;
        T min, max;

        if (Vector.IsHardwareAccelerated && values.Length >= Vector<T>.Count)
        {
            // Seed both running vectors with the first block, then fold the rest a vector at a time.
            var vMin = new Vector<T>(values.Slice(0, Vector<T>.Count));
            var vMax = vMin;
            i = Vector<T>.Count;

            int upper = values.Length - Vector<T>.Count;
            for (; i <= upper; i += Vector<T>.Count)
            {
                var v = new Vector<T>(values.Slice(i, Vector<T>.Count));
                vMin = Vector.Min(vMin, v);
                vMax = Vector.Max(vMax, v);
            }

            // Horizontally reduce the per-lane extrema to scalars.
            min = vMin[0];
            max = vMax[0];
            for (int lane = 1; lane < Vector<T>.Count; lane++)
            {
                if (vMin[lane] < min) min = vMin[lane];
                if (vMax[lane] > max) max = vMax[lane];
            }
        }
        else
        {
            min = max = values[0];
            i = 1;
        }

        for (; i < values.Length; i++)
        {
            T v = values[i];
            if (v < min) min = v;
            if (v > max) max = v;
        }

        return (min, max);
    }
}
