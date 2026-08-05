using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Primitives;

/// <summary>
/// Set algebra over spans that are <strong>already sorted in ascending order</strong> — intersection,
/// union, difference, a common-element count and an overlap test — computed by a two-cursor linear
/// merge straight into caller-owned memory, with no allocation and no hash table.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Every input span must be sorted ascending. Sorted by construction, or this is worthless:</strong>
/// the merge exploits ordering to touch each element once, so unsorted input silently produces a wrong
/// answer rather than an error. Debug builds assert the precondition (an <c>O(n)</c> scan per call);
/// Release builds do not check it at all, which is the whole point.
/// </para>
/// <para>
/// <strong>The destination must not overlap either input.</strong> The merge writes its result while it
/// is still reading both sources, so an aliasing buffer can overwrite elements that have not been
/// consumed yet — silently, in the same way unsorted input does. This is asserted in Debug builds and
/// unchecked in Release, exactly as the ordering precondition is.
/// </para>
/// <para>
/// The BCL has no set algebra over spans. <see cref="MemoryExtensions"/> gained <c>CommonPrefixLength</c>,
/// <c>Split</c> and <c>SearchValues</c> but nothing that intersects two ranges, and
/// <c>System.Numerics.Tensors.TensorPrimitives</c> has none either. The alternatives are
/// <c>HashSet&lt;T&gt;.IntersectWith</c> (allocate a table, then hash and probe every element of one side)
/// or LINQ <c>Intersect</c> (a <c>Set&lt;T&gt;</c> plus an iterator chain) — neither exploits sortedness at
/// all. These methods walk both sides in order instead: sequential, prefetch-friendly reads, results
/// written straight into a caller-provided <see cref="Span{T}"/>, and — for
/// <see cref="IntersectCount"/> and <see cref="Overlaps"/> — nothing allocated anywhere.
/// </para>
/// <para>
/// <strong>Set semantics.</strong> Repeated equal values are allowed in an input and are treated as one
/// element: every result is strictly ascending (no duplicates), matching what the same operation on
/// <c>HashSet&lt;T&gt;</c> would produce.
/// </para>
/// <para>
/// <strong>Galloping.</strong> When one side is at least <c>32×</c> the length of the other,
/// <see cref="Intersect"/>, <see cref="Except"/>, <see cref="IntersectCount"/> and
/// <see cref="Overlaps"/> switch from the linear merge to exponential ("galloping") search of the long
/// side, turning the asymmetric posting-list shape (1k against 10M) into <c>O(k log m)</c> instead of
/// <c>O(n + m)</c>. <see cref="Union"/> has no galloping path on purpose: its result is at least as long
/// as the longer input, so the cost is dominated by writing those elements out and skipping comparisons
/// cannot help. <see cref="Except"/> gallops only when the <em>subtrahend</em> is the long side, for the
/// same reason — when the left side is the long one, every one of its elements is a candidate output and
/// the linear merge is already proportional to the result.
/// </para>
/// <para>
/// The element type is constrained to <see cref="IComparisonOperators{TSelf, TOther, TResult}"/> rather
/// than <see cref="IComparable{T}"/> so the merge compares with the type's own <c>&lt;</c> / <c>==</c>
/// operators; for the primitive integer types (<see cref="int"/>, <see cref="long"/>, <see cref="uint"/>,
/// <see cref="ulong"/>, and the narrower ones) the JIT specializes the generic per value type and each
/// comparison becomes a single machine instruction, so there is no need for hand-written per-type
/// overloads. Floating-point element types work but are not the intended use: <c>NaN</c> compares false
/// against everything, so a span containing one is not ordered under <c>&lt;</c> and violates the
/// precondition.
/// </para>
/// <para>
/// This composes with the sorted output of <c>CompressedIntSet</c> and <c>BTreeSet</c> (in the
/// <c>Celerity.Collections</c> package): both enumerate in ascending order, so their contents can be fed
/// here without a re-sort.
/// </para>
/// </remarks>
public static class SortedSpan
{
    /// <summary>
    /// Length ratio at which the linear merge is abandoned for exponential search of the longer side.
    /// Below it the merge's one-comparison-per-element cost wins; above it the short side's
    /// <c>log(m)</c> probes do.
    /// </summary>
    private const int GallopThreshold = 32;

    /// <summary>
    /// Writes the values present in <strong>both</strong> of two <strong>ascending-sorted</strong> spans
    /// to <paramref name="destination"/>, in ascending order and without duplicates.
    /// </summary>
    /// <typeparam name="T">The element type, ordered by its own comparison operators.</typeparam>
    /// <param name="a">The first source span. Must be sorted ascending.</param>
    /// <param name="b">The second source span. Must be sorted ascending.</param>
    /// <param name="destination">
    /// Receives the result. <c>min(a.Length, b.Length)</c> elements is always enough. It must not
    /// overlap <paramref name="a"/> or <paramref name="b"/> — the merge writes while it is still
    /// reading both sources.
    /// </param>
    /// <returns>The number of values written — the result is <c>destination[..returned]</c>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> is too short to hold the result. Its contents are then undefined:
    /// the shortfall is discovered while writing, not up front.
    /// </exception>
    /// <remarks>Allocation-free. Gallops when one side is at least <c>32×</c> the other.</remarks>
    public static int Intersect<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, Span<T> destination)
        where T : IComparisonOperators<T, T, bool>
    {
        AssertSorted(a, nameof(a));
        AssertSorted(b, nameof(b));
        AssertNoOverlap(destination, a, b);

        if (a.IsEmpty || b.IsEmpty)
            return 0;

        if (b.Length >= (long)a.Length * GallopThreshold)
            return GallopIntersect(a, b, destination);
        if (a.Length >= (long)b.Length * GallopThreshold)
            return GallopIntersect(b, a, destination);

        return MergeIntersect(a, b, destination);
    }

    /// <summary>
    /// Writes the values present in <strong>either</strong> of two <strong>ascending-sorted</strong> spans
    /// to <paramref name="destination"/>, in ascending order and without duplicates.
    /// </summary>
    /// <typeparam name="T">The element type, ordered by its own comparison operators.</typeparam>
    /// <param name="a">The first source span. Must be sorted ascending.</param>
    /// <param name="b">The second source span. Must be sorted ascending.</param>
    /// <param name="destination">
    /// Receives the result. <c>a.Length + b.Length</c> elements is always enough. It must not overlap
    /// <paramref name="a"/> or <paramref name="b"/> — the merge writes while it is still reading both
    /// sources.
    /// </param>
    /// <returns>The number of values written — the result is <c>destination[..returned]</c>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> is too short to hold the result. Its contents are then undefined:
    /// the shortfall is discovered while writing, not up front.
    /// </exception>
    /// <remarks>Allocation-free. Always the linear merge — see the type remarks for why union does not gallop.</remarks>
    public static int Union<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, Span<T> destination)
        where T : IComparisonOperators<T, T, bool>
    {
        AssertSorted(a, nameof(a));
        AssertSorted(b, nameof(b));
        AssertNoOverlap(destination, a, b);

        if (a.IsEmpty)
            return AppendDistinct(b, destination, 0);
        if (b.IsEmpty)
            return AppendDistinct(a, destination, 0);

        return MergeUnion(a, b, destination);
    }

    /// <summary>
    /// Writes the values present in <paramref name="a"/> but <strong>not</strong> in <paramref name="b"/>
    /// — both <strong>ascending-sorted</strong> — to <paramref name="destination"/>, in ascending order
    /// and without duplicates.
    /// </summary>
    /// <typeparam name="T">The element type, ordered by its own comparison operators.</typeparam>
    /// <param name="a">The span to subtract from. Must be sorted ascending.</param>
    /// <param name="b">The span of values to remove. Must be sorted ascending.</param>
    /// <param name="destination">
    /// Receives the result. <c>a.Length</c> elements is always enough. It must not overlap
    /// <paramref name="a"/> or <paramref name="b"/> — the merge writes while it is still reading both
    /// sources.
    /// </param>
    /// <returns>The number of values written — the result is <c>destination[..returned]</c>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="destination"/> is too short to hold the result. Its contents are then undefined:
    /// the shortfall is discovered while writing, not up front.
    /// </exception>
    /// <remarks>Allocation-free. Gallops when <paramref name="b"/> is at least <c>32×</c> the length of <paramref name="a"/>.</remarks>
    public static int Except<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, Span<T> destination)
        where T : IComparisonOperators<T, T, bool>
    {
        AssertSorted(a, nameof(a));
        AssertSorted(b, nameof(b));
        AssertNoOverlap(destination, a, b);

        if (a.IsEmpty)
            return 0;
        if (b.IsEmpty)
            return AppendDistinct(a, destination, 0);

        if (b.Length >= (long)a.Length * GallopThreshold)
            return GallopExcept(a, b, destination);

        return MergeExcept(a, b, destination);
    }

    /// <summary>
    /// Counts the distinct values present in <strong>both</strong> of two
    /// <strong>ascending-sorted</strong> spans, without materializing the intersection.
    /// </summary>
    /// <typeparam name="T">The element type, ordered by its own comparison operators.</typeparam>
    /// <param name="a">The first source span. Must be sorted ascending.</param>
    /// <param name="b">The second source span. Must be sorted ascending.</param>
    /// <returns>The number of distinct values common to both spans.</returns>
    /// <remarks>
    /// Allocates nothing at all — no destination buffer, where both BCL alternatives must build a set
    /// first. Gallops when one side is at least <c>32×</c> the other.
    /// </remarks>
    public static int IntersectCount<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b)
        where T : IComparisonOperators<T, T, bool>
        => CountCommon(a, b, stopAtFirst: false);

    /// <summary>
    /// Reports whether two <strong>ascending-sorted</strong> spans share at least one value, stopping at
    /// the first one found.
    /// </summary>
    /// <typeparam name="T">The element type, ordered by its own comparison operators.</typeparam>
    /// <param name="a">The first source span. Must be sorted ascending.</param>
    /// <param name="b">The second source span. Must be sorted ascending.</param>
    /// <returns><see langword="true"/> if some value occurs in both spans.</returns>
    /// <remarks>
    /// Allocates nothing at all, and returns as soon as a common value is seen. Gallops when one side is
    /// at least <c>32×</c> the other.
    /// </remarks>
    public static bool Overlaps<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b)
        where T : IComparisonOperators<T, T, bool>
        => CountCommon(a, b, stopAtFirst: true) != 0;

    // ---- shared dispatch ----------------------------------------------------------------

    private static int CountCommon<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, bool stopAtFirst)
        where T : IComparisonOperators<T, T, bool>
    {
        AssertSorted(a, nameof(a));
        AssertSorted(b, nameof(b));

        if (a.IsEmpty || b.IsEmpty)
            return 0;

        if (b.Length >= (long)a.Length * GallopThreshold)
            return GallopCount(a, b, stopAtFirst);
        if (a.Length >= (long)b.Length * GallopThreshold)
            return GallopCount(b, a, stopAtFirst);

        return MergeCount(a, b, stopAtFirst);
    }

    // ---- linear two-cursor merges -------------------------------------------------------
    // Each is entered with both spans non-empty, so the cursors can be advanced before the
    // bounds are re-tested and the loop needs one length comparison per step rather than two.
    //
    // Intersect and Count advance one element on a mismatch rather than skipping the whole
    // equal run. Skipping would cost an equality test per element that can only pay off on
    // duplicates, and this branch *is* the loop on distinct inputs — measured a wash on
    // duplicate-heavy data (1M values over a 1k / 10k domain) and 1.66x slower on
    // interleaved-disjoint 1M x 1M spans. Union and Except skip the run instead, because
    // they emit in that branch and must not emit the same value twice.

    private static int MergeIntersect<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, Span<T> destination)
        where T : IComparisonOperators<T, T, bool>
    {
        int i = 0, j = 0, count = 0;

        while (true)
        {
            T x = a[i];
            T y = b[j];

            if (x < y)
            {
                if (++i == a.Length)
                    break;
            }
            else if (y < x)
            {
                if (++j == b.Length)
                    break;
            }
            else
            {
                Append(destination, ref count, x);
                i = SkipEqual(a, i, x);
                j = SkipEqual(b, j, x);
                if (i == a.Length || j == b.Length)
                    break;
            }
        }

        return count;
    }

    private static int MergeUnion<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, Span<T> destination)
        where T : IComparisonOperators<T, T, bool>
    {
        int i = 0, j = 0, count = 0;

        while (true)
        {
            T x = a[i];
            T y = b[j];

            if (x < y)
            {
                Append(destination, ref count, x);
                i = SkipEqual(a, i, x);
                if (i == a.Length)
                    break;
            }
            else if (y < x)
            {
                Append(destination, ref count, y);
                j = SkipEqual(b, j, y);
                if (j == b.Length)
                    break;
            }
            else
            {
                Append(destination, ref count, x);
                i = SkipEqual(a, i, x);
                j = SkipEqual(b, j, x);
                if (i == a.Length || j == b.Length)
                    break;
            }
        }

        // Whichever side still has elements, they are all strictly greater than the last value
        // written (both cursors were advanced past it), so the tail only needs de-duplicating
        // against itself.
        if (i < a.Length)
            return AppendDistinct(a.Slice(i), destination, count);

        return AppendDistinct(b.Slice(j), destination, count);
    }

    private static int MergeExcept<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, Span<T> destination)
        where T : IComparisonOperators<T, T, bool>
    {
        int i = 0, j = 0, count = 0;

        while (true)
        {
            T x = a[i];
            T y = b[j];

            if (x < y)
            {
                Append(destination, ref count, x);
                i = SkipEqual(a, i, x);
                if (i == a.Length)
                    break;
            }
            else if (y < x)
            {
                if (++j == b.Length)
                    break;
            }
            else
            {
                i = SkipEqual(a, i, x);
                j = SkipEqual(b, j, x);
                if (i == a.Length || j == b.Length)
                    break;
            }
        }

        return AppendDistinct(a.Slice(i), destination, count);
    }

    private static int MergeCount<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, bool stopAtFirst)
        where T : IComparisonOperators<T, T, bool>
    {
        int i = 0, j = 0, count = 0;

        while (true)
        {
            T x = a[i];
            T y = b[j];

            if (x < y)
            {
                if (++i == a.Length)
                    break;
            }
            else if (y < x)
            {
                if (++j == b.Length)
                    break;
            }
            else
            {
                count++;
                if (stopAtFirst)
                    break;

                i = SkipEqual(a, i, x);
                j = SkipEqual(b, j, x);
                if (i == a.Length || j == b.Length)
                    break;
            }
        }

        return count;
    }

    // ---- galloping paths ----------------------------------------------------------------
    // Walk the short side one distinct value at a time and exponential-search the long side for
    // it, resuming each search where the previous one landed so the long side is still scanned
    // monotonically (the probes across the whole call amortize to O(k log(m/k))).

    private static int GallopIntersect<T>(ReadOnlySpan<T> small, ReadOnlySpan<T> large, Span<T> destination)
        where T : IComparisonOperators<T, T, bool>
    {
        int count = 0, j = 0;

        for (int i = 0; i < small.Length; i++)
        {
            T x = small[i];
            if (i > 0 && x == small[i - 1])
                continue;

            j = LowerBound(large, x, j);
            if (j == large.Length)
                break;

            if (large[j] == x)
                Append(destination, ref count, x);
        }

        return count;
    }

    private static int GallopExcept<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, Span<T> destination)
        where T : IComparisonOperators<T, T, bool>
    {
        int count = 0, j = 0;

        for (int i = 0; i < a.Length; i++)
        {
            T x = a[i];
            if (i > 0 && x == a[i - 1])
                continue;

            j = LowerBound(b, x, j);
            if (j == b.Length || b[j] != x)
                Append(destination, ref count, x);
        }

        return count;
    }

    private static int GallopCount<T>(ReadOnlySpan<T> small, ReadOnlySpan<T> large, bool stopAtFirst)
        where T : IComparisonOperators<T, T, bool>
    {
        int count = 0, j = 0;

        for (int i = 0; i < small.Length; i++)
        {
            T x = small[i];
            if (i > 0 && x == small[i - 1])
                continue;

            j = LowerBound(large, x, j);
            if (j == large.Length)
                break;

            if (large[j] == x)
            {
                count++;
                if (stopAtFirst)
                    break;
            }
        }

        return count;
    }

    /// <summary>
    /// Returns the index of the first element of <paramref name="span"/> at or after
    /// <paramref name="from"/> that is not less than <paramref name="target"/>, or the span length if
    /// there is none. Brackets the answer by doubling the probe distance, then bisects the bracket, so a
    /// target close to <paramref name="from"/> costs a couple of comparisons rather than a full
    /// <c>log(n)</c> binary search over the whole span.
    /// </summary>
    private static int LowerBound<T>(ReadOnlySpan<T> span, T target, int from)
        where T : IComparisonOperators<T, T, bool>
    {
        if (from >= span.Length)
            return span.Length;
        if (target <= span[from])
            return from;

        // Invariant maintained below: span[lo] < target, and hi is either the span length or an
        // index whose element is >= target.
        int lo = from;
        int hi;
        long step = 1;

        while (true)
        {
            // Widened: lo + step can exceed int.MaxValue once the probe distance passes 2^30.
            long next = lo + step;
            if (next >= span.Length)
            {
                hi = span.Length;
                break;
            }

            if (span[(int)next] < target)
            {
                lo = (int)next;
                step <<= 1;
            }
            else
            {
                hi = (int)next;
                break;
            }
        }

        while (hi - lo > 1)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (span[mid] < target)
                lo = mid;
            else
                hi = mid;
        }

        return hi;
    }

    // ---- writing helpers ----------------------------------------------------------------

    /// <summary>Advances past every element equal to <paramref name="value"/>, starting at <paramref name="index"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SkipEqual<T>(ReadOnlySpan<T> span, int index, T value)
        where T : IComparisonOperators<T, T, bool>
    {
        do
        {
            index++;
        }
        while (index < span.Length && span[index] == value);

        return index;
    }

    /// <summary>
    /// Copies the distinct values of an ascending-sorted <paramref name="source"/> onto the end of
    /// <paramref name="destination"/>.
    /// </summary>
    private static int AppendDistinct<T>(ReadOnlySpan<T> source, Span<T> destination, int count)
        where T : IComparisonOperators<T, T, bool>
    {
        if (source.IsEmpty)
            return count;

        T previous = source[0];
        Append(destination, ref count, previous);

        for (int i = 1; i < source.Length; i++)
        {
            T value = source[i];
            if (value == previous)
                continue;

            Append(destination, ref count, value);
            previous = value;
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Append<T>(Span<T> destination, ref int count, T value)
    {
        if (count == destination.Length)
            ThrowDestinationTooSmall(nameof(destination));

        destination[count++] = value;
    }

    [DoesNotReturn]
    private static void ThrowDestinationTooSmall(string paramName)
        => throw new ArgumentException("Destination is too short to hold the result.", paramName);

    /// <summary>
    /// Debug-build verification that the result buffer does not alias either input. The merge writes to
    /// <paramref name="destination"/> while it is still reading both sources, so an overlapping buffer
    /// can overwrite elements that have not been consumed yet and silently produce a wrong answer.
    /// Elided from Release builds along with the rest of the precondition checking.
    /// </summary>
    [Conditional("DEBUG")]
    [ExcludeFromCodeCoverage(Justification = "Debug-only precondition check whose failing path calls Debug.Assert, which no test can drive without tearing down the test host.")]
    private static void AssertNoOverlap<T>(Span<T> destination, ReadOnlySpan<T> a, ReadOnlySpan<T> b)
    {
        Debug.Assert(
            !destination.Overlaps(a),
            "SortedSpan writes the result while still reading 'a'; 'destination' must not overlap it.");
        Debug.Assert(
            !destination.Overlaps(b),
            "SortedSpan writes the result while still reading 'b'; 'destination' must not overlap it.");
    }

    /// <summary>
    /// Debug-build verification of the ascending-order precondition. Elided entirely from Release builds
    /// — including the scan, since <see cref="ConditionalAttribute"/> removes the call site and with it
    /// the argument evaluation — so the shipped code pays nothing for it.
    /// </summary>
    [Conditional("DEBUG")]
    [ExcludeFromCodeCoverage(Justification = "Debug-only precondition check whose failing path calls Debug.Assert, which no test can drive without tearing down the test host.")]
    private static void AssertSorted<T>(ReadOnlySpan<T> span, string paramName)
        where T : IComparisonOperators<T, T, bool>
    {
        // Tested as "not ordered after" rather than "smaller than", so a pair that compares
        // false both ways is caught too: a span containing NaN is not ordered under `<` and
        // violates the precondition just as a descending pair does, but `span[i] < span[i - 1]`
        // would wave it through.
        int unorderedAt = 0;
        for (int i = 1; i < span.Length; i++)
        {
            if (!(span[i - 1] <= span[i]))
            {
                unorderedAt = i;
                break;
            }
        }

        Debug.Assert(
            unorderedAt == 0,
            $"SortedSpan requires '{paramName}' to be sorted in ascending order; element {unorderedAt} is not ordered after element {unorderedAt - 1} (it is smaller, or the two are unordered — e.g. NaN).");
    }
}
