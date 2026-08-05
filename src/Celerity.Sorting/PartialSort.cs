using System.Numerics;
using Celerity.Primitives;

namespace Celerity.Sorting;

/// <summary>
/// <b>Selection instead of sorting</b> — when only the smallest (or largest) <c>k</c> of <c>n</c>
/// elements are wanted, an <c>O(n)</c> in-place quickselect or an <c>O(n log k)</c> bounded heap
/// does a fraction of a full sort's work. <see cref="Select{T}(Span{T}, int)"/> and
/// <see cref="Sort{T}(Span{T}, int)"/> partition a span you own;
/// <see cref="TopK{T}(ReadOnlySpan{T}, Span{T})"/> streams a read-only source into a
/// caller-supplied destination without touching it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The documented BCL-beating workload</b> is top-<c>k</c> over a large span at small <c>k</c> —
/// leaderboards, nearest-neighbour shortlists, worst-offender reports, percentile probes.
/// <see cref="MemoryExtensions.Sort{T}(Span{T})"/> orders all <c>n</c> elements to answer a question
/// about <c>k</c> of them.
/// </para>
/// <para>
/// <b>One honest correction to the usual pitch:</b> LINQ's <c>OrderBy().Take(k)</c> is <i>not</i> a
/// full sort — since .NET 6 it applies its own partial-sort optimization. The win here is therefore
/// not asymptotic against LINQ; it is that this operates on a span in place, allocates nothing,
/// boxes no comparer, and materializes no intermediate sequence. Against a hand-rolled bounded heap
/// the win is smaller still and lives in the quickselect forms, which are <c>O(n)</c> rather than
/// <c>O(n log k)</c>; <see cref="TopK{T}(ReadOnlySpan{T}, Span{T})"/> <i>is</i> that bounded heap,
/// offered for the case where the source must not be reordered.
/// </para>
/// <para>
/// Quickselect degrades on adversarial input, so <see cref="Select{T}(Span{T}, int)"/> is an
/// <i>intro</i>select: past a depth limit proportional to <c>log n</c> it falls back to an in-place
/// heap sort of the remaining range, which bounds the worst case at <c>O(n log n)</c>.
/// Duplicate-heavy input is handled by a three-way partition, so an all-equal span finishes in a
/// single pass rather than quadratically.
/// </para>
/// <para>
/// <b>Every method here allocates nothing.</b> That is why the internal ordering is insertion sort
/// and heap sort rather than <see cref="MemoryExtensions.Sort{T, TComparer}(Span{T}, TComparer)"/>:
/// the BCL entry point reaches its sort through an <see cref="IComparer{T}"/>-typed helper and boxes
/// a struct comparer on every call.
/// </para>
/// <para>
/// Each entry point comes in two forms: one constrained to <see cref="IComparable{T}"/> for the
/// natural order, and one taking a <c>struct</c> comparer as a type parameter so the JIT
/// devirtualizes and inlines the comparison — the same zero-cost-abstraction rule the rest of
/// Celerity follows. Neither form is stable.
/// </para>
/// </remarks>
public static class PartialSort
{
    // Below this width the partition bookkeeping costs more than just sorting the range outright.
    private const int SortThreshold = 16;

    /// <summary>
    /// Rearranges <paramref name="keys"/> so that its first <paramref name="count"/> elements are
    /// the <paramref name="count"/> smallest, in unspecified order.
    /// </summary>
    /// <typeparam name="T">The element type, ordered by <see cref="IComparable{T}.CompareTo"/>.</typeparam>
    /// <param name="keys">The span to partition in place.</param>
    /// <param name="count">How many of the smallest elements to bring to the front.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative or greater than <c>keys.Length</c>.</exception>
    public static void Select<T>(Span<T> keys, int count)
        where T : IComparable<T> =>
        Select<T, ComparableComparer<T>>(keys, count, default);

    /// <summary>
    /// Rearranges <paramref name="keys"/> so that its first <paramref name="count"/> elements are
    /// the <paramref name="count"/> smallest under <paramref name="comparer"/>, in unspecified order.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TComparer">
    /// The order. Must be a value type implementing <see cref="IComparer{T}"/> so the JIT can
    /// devirtualize and inline every comparison.
    /// </typeparam>
    /// <param name="keys">The span to partition in place.</param>
    /// <param name="count">How many of the smallest elements to bring to the front.</param>
    /// <param name="comparer">The comparer defining the order.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative or greater than <c>keys.Length</c>.</exception>
    public static void Select<T, TComparer>(Span<T> keys, int count, TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        SortingGuard.RequireCountInRange(count, keys.Length, nameof(count));
        if (count == 0 || count == keys.Length)
        {
            return;
        }

        SelectCore(keys, count, comparer, DepthLimit(keys.Length));
    }

    /// <summary>
    /// Brings the <paramref name="count"/> smallest elements of <paramref name="keys"/> to the
    /// front <b>in ascending order</b>, leaving the rest in unspecified order.
    /// </summary>
    /// <typeparam name="T">The element type, ordered by <see cref="IComparable{T}.CompareTo"/>.</typeparam>
    /// <param name="keys">The span to partially sort in place.</param>
    /// <param name="count">How many of the smallest elements to sort to the front.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative or greater than <c>keys.Length</c>.</exception>
    public static void Sort<T>(Span<T> keys, int count)
        where T : IComparable<T> =>
        Sort<T, ComparableComparer<T>>(keys, count, default);

    /// <summary>
    /// Brings the <paramref name="count"/> smallest elements of <paramref name="keys"/> under
    /// <paramref name="comparer"/> to the front <b>in ascending order</b>, leaving the rest in
    /// unspecified order.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TComparer">
    /// The order. Must be a value type implementing <see cref="IComparer{T}"/> so the JIT can
    /// devirtualize and inline every comparison.
    /// </typeparam>
    /// <param name="keys">The span to partially sort in place.</param>
    /// <param name="count">How many of the smallest elements to sort to the front.</param>
    /// <param name="comparer">The comparer defining the order.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative or greater than <c>keys.Length</c>.</exception>
    public static void Sort<T, TComparer>(Span<T> keys, int count, TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        Select(keys, count, comparer);
        SortRange(keys[..count], comparer);
    }

    /// <summary>
    /// Copies the largest elements of <paramref name="source"/> into <paramref name="destination"/>
    /// in <b>descending</b> order, without modifying <paramref name="source"/>.
    /// </summary>
    /// <typeparam name="T">The element type, ordered by <see cref="IComparable{T}.CompareTo"/>.</typeparam>
    /// <param name="source">The elements to scan. Not modified.</param>
    /// <param name="destination">Receives the top <c>destination.Length</c> elements; its length is <c>k</c>.</param>
    /// <returns>The number of elements written — <c>destination.Length</c>, or <c>source.Length</c> when the source is shorter.</returns>
    public static int TopK<T>(ReadOnlySpan<T> source, Span<T> destination)
        where T : IComparable<T> =>
        TopK<T, ComparableComparer<T>>(source, destination, default);

    /// <summary>
    /// Copies the largest elements of <paramref name="source"/> under <paramref name="comparer"/>
    /// into <paramref name="destination"/> in <b>descending</b> order, without modifying
    /// <paramref name="source"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TComparer">
    /// The order. Must be a value type implementing <see cref="IComparer{T}"/> so the JIT can
    /// devirtualize and inline every comparison. Pass a reversing comparer to take the smallest.
    /// </typeparam>
    /// <param name="source">The elements to scan. Not modified.</param>
    /// <param name="destination">Receives the top <c>destination.Length</c> elements; its length is <c>k</c>.</param>
    /// <param name="comparer">The comparer defining the order.</param>
    /// <returns>The number of elements written — <c>destination.Length</c>, or <c>source.Length</c> when the source is shorter.</returns>
    public static int TopK<T, TComparer>(ReadOnlySpan<T> source, Span<T> destination, TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        int k = destination.Length;
        if (k == 0)
        {
            return 0;
        }

        if (source.Length <= k)
        {
            source.CopyTo(destination);
            Span<T> written = destination[..source.Length];
            SortRange(written, comparer);
            written.Reverse();
            return source.Length;
        }

        // A min-heap of the k best seen so far: its root is the weakest survivor, so one comparison
        // rejects the common case and no element is ever moved more than log k times.
        Span<T> heap = destination;
        source[..k].CopyTo(heap);
        for (int i = (k >> 1) - 1; i >= 0; i--)
        {
            SiftDownMin(heap, i, comparer);
        }

        for (int i = k; i < source.Length; i++)
        {
            if (comparer.Compare(source[i], heap[0]) > 0)
            {
                heap[0] = source[i];
                SiftDownMin(heap, 0, comparer);
            }
        }

        // The heap is already a min-heap of the answer, so draining it in place *is* the sort:
        // swapping the weakest survivor to the back k-1 times leaves the span in descending order.
        for (int i = k - 1; i > 0; i--)
        {
            (heap[0], heap[i]) = (heap[i], heap[0]);
            SiftDownMin(heap[..i], 0, comparer);
        }

        return k;
    }

    private static void SelectCore<T, TComparer>(Span<T> keys, int count, TComparer comparer, int depthLimit)
        where TComparer : struct, IComparer<T>
    {
        int lo = 0;
        int hi = keys.Length - 1;

        while (true)
        {
            // Two exits into a full sort of the remaining range. The width test is the ordinary
            // one. The depth budget is the introselect guarantee: it bounds the worst case at
            // O(n log n) on adversarial data, and — because a comparer that reports inconsistent
            // orders can defeat the progress argument entirely — it is also what stops an
            // ill-behaved comparer from spinning here forever.
            if (hi - lo < SortThreshold || depthLimit-- == 0)
            {
                SortRange(keys[lo..(hi + 1)], comparer);
                return;
            }

            ThreeWayPartition(keys, lo, hi, comparer, out int equalFirst, out int equalLast);

            if (count <= equalFirst)
            {
                hi = equalFirst - 1;
            }
            else if (count > equalLast + 1)
            {
                lo = equalLast + 1;
            }
            else
            {
                // The boundary falls inside the block of pivot-equal elements, so every element
                // before it is already among the smallest `count` and their order does not matter.
                return;
            }
        }
    }

    /// <summary>
    /// Dutch-national-flag partition of <c>keys[lo..hi]</c> around a median-of-three pivot. On
    /// return everything below <paramref name="equalFirst"/> compares less than the pivot,
    /// <c>[equalFirst, equalLast]</c> compares equal to it, and everything above compares greater.
    /// </summary>
    private static void ThreeWayPartition<T, TComparer>(
        Span<T> keys,
        int lo,
        int hi,
        TComparer comparer,
        out int equalFirst,
        out int equalLast)
        where TComparer : struct, IComparer<T>
    {
        T pivot = MedianOfThree(keys, lo, hi, comparer);

        int less = lo;
        int i = lo;
        int greater = hi;
        while (i <= greater)
        {
            int order = comparer.Compare(keys[i], pivot);
            if (order < 0)
            {
                (keys[less], keys[i]) = (keys[i], keys[less]);
                less++;
                i++;
            }
            else if (order > 0)
            {
                (keys[greater], keys[i]) = (keys[i], keys[greater]);
                greater--;
            }
            else
            {
                i++;
            }
        }

        equalFirst = less;
        equalLast = greater;
    }

    private static T MedianOfThree<T, TComparer>(Span<T> keys, int lo, int hi, TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        int mid = lo + ((hi - lo) >> 1);

        if (comparer.Compare(keys[mid], keys[lo]) < 0)
        {
            (keys[lo], keys[mid]) = (keys[mid], keys[lo]);
        }

        if (comparer.Compare(keys[hi], keys[lo]) < 0)
        {
            (keys[lo], keys[hi]) = (keys[hi], keys[lo]);
        }

        if (comparer.Compare(keys[hi], keys[mid]) < 0)
        {
            (keys[mid], keys[hi]) = (keys[hi], keys[mid]);
        }

        return keys[mid];
    }

    private static void SiftDownMin<T, TComparer>(Span<T> heap, int root, TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        int count = heap.Length;
        while (true)
        {
            int left = (2 * root) + 1;
            if (left >= count)
            {
                return;
            }

            // Which child is smaller is unpredictable by construction, so pick the index without a
            // branch; only the "are we done" test below stays a real branch.
            int right = left + 1;
            int child = right < count
                ? Branchless.Select(comparer.Compare(heap[right], heap[left]) < 0, right, left)
                : left;

            if (comparer.Compare(heap[child], heap[root]) >= 0)
            {
                return;
            }

            (heap[root], heap[child]) = (heap[child], heap[root]);
            root = child;
        }
    }

    /// <summary>
    /// Orders <paramref name="keys"/> ascending in place, allocating nothing.
    /// </summary>
    /// <remarks>
    /// This exists instead of <see cref="MemoryExtensions.Sort{T, TComparer}(Span{T}, TComparer)"/>,
    /// which reaches the BCL's sort through an <see cref="IComparer{T}"/>-typed helper and therefore
    /// boxes a struct comparer on every call — measurably, at roughly 88 bytes a call, which would
    /// have made every entry point in this type allocate. Insertion sort handles the short ranges the
    /// selector actually produces; heap sort covers the rest with a guaranteed <c>O(n log n)</c> and no
    /// recursion, which is also what keeps the depth-limit fallback a real bound. Callers who want
    /// introsort's constant factor over a whole span should call the BCL directly.
    /// </remarks>
    private static void SortRange<T, TComparer>(Span<T> keys, TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        if (keys.Length < 2)
        {
            return;
        }

        if (keys.Length <= SortThreshold)
        {
            for (int i = 1; i < keys.Length; i++)
            {
                T item = keys[i];
                int j = i - 1;
                while (j >= 0 && comparer.Compare(keys[j], item) > 0)
                {
                    keys[j + 1] = keys[j];
                    j--;
                }

                keys[j + 1] = item;
            }

            return;
        }

        for (int i = (keys.Length >> 1) - 1; i >= 0; i--)
        {
            SiftDownMax(keys, i, comparer);
        }

        for (int i = keys.Length - 1; i > 0; i--)
        {
            (keys[0], keys[i]) = (keys[i], keys[0]);
            SiftDownMax(keys[..i], 0, comparer);
        }
    }

    private static void SiftDownMax<T, TComparer>(Span<T> heap, int root, TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        int count = heap.Length;
        while (true)
        {
            int left = (2 * root) + 1;
            if (left >= count)
            {
                return;
            }

            int right = left + 1;
            int child = right < count
                ? Branchless.Select(comparer.Compare(heap[right], heap[left]) > 0, right, left)
                : left;

            if (comparer.Compare(heap[child], heap[root]) <= 0)
            {
                return;
            }

            (heap[root], heap[child]) = (heap[child], heap[root]);
            root = child;
        }
    }

    // 2 * log2(n), the classic introsort budget: enough that a well-behaved input never reaches it,
    // small enough that an adversarial one falls back before quickselect's quadratic case bites.
    private static int DepthLimit(int length) => 2 * (31 - BitOperations.LeadingZeroCount((uint)length));

    /// <summary>
    /// The natural order as a struct comparer, so the <see cref="IComparable{T}"/> overloads share
    /// one devirtualized implementation with the comparer-parameterized ones. Nulls sort first,
    /// matching <see cref="Comparer{T}.Default"/>.
    /// </summary>
    private readonly struct ComparableComparer<T> : IComparer<T>
        where T : IComparable<T>
    {
        public int Compare(T? x, T? y)
        {
            if (x is null)
            {
                return y is null ? 0 : -1;
            }

            return y is null ? 1 : x.CompareTo(y);
        }
    }
}
