using System.Buffers;
using System.Runtime.CompilerServices;

namespace Celerity.Sorting;

/// <summary>
/// <b>Stable comparison sort</b>: a run-adaptive merge sort (Powersort, Munro &amp; Wild 2018, the merge
/// policy CPython's <c>list.sort</c> has used since 3.11) that keeps elements which compare equal in
/// their input order. <see cref="Array.Sort{T}(T[])"/>, <see cref="List{T}.Sort()"/> and
/// <see cref="MemoryExtensions.Sort{T}(Span{T})"/> are introsorts and may reorder equal elements; this
/// sort never does, sorts in place, and takes any order a struct <see cref="IComparer{T}"/> can express.
/// Supports keys alone and keys with a parallel payload.
/// </summary>
/// <remarks>
/// <para>
/// <b>The documented BCL-beating workload</b> is a stable sort of records by a comparer — grid rows by
/// one column after another, events by timestamp without shuffling same-tick events, any multi-pass sort
/// by secondary then primary key. The BCL's stable answers are LINQ's
/// <see cref="Enumerable.OrderBy{TSource, TKey}(IEnumerable{TSource}, Func{TSource, TKey})"/>, which
/// allocates a key array, an index map and the output on every call, and tagging each element with its
/// position so that <see cref="Array.Sort{T}(T[], IComparer{T})"/> can break ties on it, which copies
/// every element into a wider tuple and back. This sort beats both on random input and by a wide margin
/// on input that is already partly ordered. Measured figures are on the
/// <a href="https://marius-bughiu.github.io/Celerity/dev/bench/">benchmark dashboard</a>.
/// </para>
/// <para>
/// <b>Run-adaptive.</b> The sort finds the runs already in the input — ascending, or strictly descending,
/// which it reverses — and merges them, so it does <c>O(n)</c> work on sorted or reversed input,
/// <c>O(n + n·H)</c> on input made of runs whose lengths have entropy <c>H</c> (a sorted array with an
/// appended tail, a concatenation of sorted batches), and <c>O(n log n)</c> in the worst case. Introsort
/// does <c>O(n log n)</c> work on all of them.
/// </para>
/// <para>
/// <b>Where it does not win.</b> With an expensive comparison, such as a string compare, and no order in
/// the input, the <i>unstable</i> introsort can be about 5% faster at small sizes. When stability is not
/// needed and the input has no order, <see cref="MemoryExtensions.Sort{T, TComparer}(Span{T}, TComparer)"/>
/// is the right tool. For primitive keys, <see cref="RadixSort"/> is stable too and beats any comparison
/// sort; for ordinal string keys, <see cref="StringSort"/> is stable and reads shared prefixes once.
/// </para>
/// <para>
/// <b>Cost.</b> Input of up to 16 elements, and input that is a single run, is sorted with no buffer. Above
/// that, each call rents a merge buffer of <c>n / 2</c> elements (and a second one for the payload) from
/// <see cref="ArrayPool{T}"/>. The buffers are cleared before they go back to the pool when the element
/// type holds references, so the pool never keeps the caller's objects alive, and once the pool is warm a
/// call allocates nothing.
/// </para>
/// <para>
/// <b>Comparer contract.</b> The comparer must be a value type, so the JIT can devirtualize and inline
/// every comparison. If it throws, the exception propagates unchanged and the span holds a permutation of
/// its input: no element is lost or duplicated. If it is not a consistent total order, the resulting order
/// is unspecified, but the span still holds a permutation of its input.
/// </para>
/// </remarks>
public static class StableSort
{
    // Natural runs shorter than this are extended to it by binary insertion sort. Powersort needs no
    // balancing minimum, so this only trades merge overhead against insertion shifts; a sweep of 16, 32 and
    // 48 on records and strings put 16 ahead at both 1k and 100k elements.
    private const int MinRun = 16;

    // The pending-run stack holds runs with strictly increasing node powers, and a node power is at most
    // one more than the bit length of the input length, so 33 entries cover any span; 64 leaves headroom.
    private const int MaxPendingRuns = 64;

    /// <summary>Sorts <paramref name="keys"/> in ascending order, keeping equal elements in input order.</summary>
    /// <typeparam name="T">The element type, ordered by <see cref="IComparable{T}.CompareTo"/>. <see langword="null"/> sorts first.</typeparam>
    /// <param name="keys">The span to sort in place.</param>
    public static void Sort<T>(Span<T> keys)
        where T : IComparable<T> =>
        SortCore<T, NoPayload, ComparableComparer<T>>(keys, default, default);

    /// <summary>
    /// Sorts <paramref name="keys"/> in ascending order under <paramref name="comparer"/>, keeping elements
    /// that compare equal in input order.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TComparer">
    /// The order. Must be a value type implementing <see cref="IComparer{T}"/> so the JIT can devirtualize
    /// and inline every comparison.
    /// </typeparam>
    /// <param name="keys">The span to sort in place.</param>
    /// <param name="comparer">The comparer defining the order.</param>
    public static void Sort<T, TComparer>(Span<T> keys, TComparer comparer)
        where TComparer : struct, IComparer<T> =>
        SortCore<T, NoPayload, TComparer>(keys, default, comparer);

    /// <summary>
    /// Sorts <paramref name="keys"/> in ascending order, moving <paramref name="values"/> with them and keeping
    /// equal keys, with their payloads, in input order.
    /// </summary>
    /// <typeparam name="TKey">The key type, ordered by <see cref="IComparable{T}.CompareTo"/>. <see langword="null"/> sorts first.</typeparam>
    /// <typeparam name="TValue">The payload type. Never compared; only moved.</typeparam>
    /// <param name="keys">The keys to sort in place.</param>
    /// <param name="values">
    /// The payload, parallel to <paramref name="keys"/> and at least as long. Its first <c>keys.Length</c>
    /// elements are permuted to match; any beyond that are left alone.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="values"/> is shorter than <paramref name="keys"/>, or shares storage with it.</exception>
    public static void Sort<TKey, TValue>(Span<TKey> keys, Span<TValue> values)
        where TKey : IComparable<TKey> =>
        Sort<TKey, TValue, ComparableComparer<TKey>>(keys, values, default);

    /// <summary>
    /// Sorts <paramref name="keys"/> in ascending order under <paramref name="comparer"/>, moving
    /// <paramref name="values"/> with them and keeping keys that compare equal, with their payloads, in
    /// input order.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The payload type. Never compared; only moved.</typeparam>
    /// <typeparam name="TComparer">
    /// The order. Must be a value type implementing <see cref="IComparer{T}"/> so the JIT can devirtualize
    /// and inline every comparison.
    /// </typeparam>
    /// <param name="keys">The keys to sort in place.</param>
    /// <param name="values">
    /// The payload, parallel to <paramref name="keys"/> and at least as long. Its first <c>keys.Length</c>
    /// elements are permuted to match; any beyond that are left alone.
    /// </param>
    /// <param name="comparer">The comparer defining the order.</param>
    /// <exception cref="ArgumentException"><paramref name="values"/> is shorter than <paramref name="keys"/>, or shares storage with it.</exception>
    public static void Sort<TKey, TValue, TComparer>(Span<TKey> keys, Span<TValue> values, TComparer comparer)
        where TComparer : struct, IComparer<TKey>
    {
        SortingGuard.RequireLength(values.Length, keys.Length, nameof(values));
        SortingGuard.RequireDistinctStorage(keys, values, nameof(values));
        SortCore<TKey, TValue, TComparer>(keys, values[..keys.Length], comparer);
    }

    // The keys-only forms instantiate the shared core with this payload type and an empty payload span.
    // Every payload access sits behind HasValues, a JIT-time constant per instantiation, so the keys-only
    // code carries no payload work at all.
    private readonly struct NoPayload
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasValues<TValue>() => typeof(TValue) != typeof(NoPayload);

    private static void SortCore<TKey, TValue, TComparer>(Span<TKey> keys, Span<TValue> values, TComparer comparer)
        where TComparer : struct, IComparer<TKey>
    {
        int n = keys.Length;
        if (n < 2)
        {
            return;
        }

        // The first run is found before anything is rented, so short input (one extended run) and input
        // that is already a single run cost no buffer at all.
        int firstRun = ExtendRun(keys, values, 0, comparer);
        if (firstRun == n)
        {
            return;
        }

        // A merge buffers only the shorter of its two runs, which is never more than half the input.
        int half = n / 2;
        TKey[] keyBuffer = ArrayPool<TKey>.Shared.Rent(half);
        TValue[]? valueBuffer = HasValues<TValue>() ? ArrayPool<TValue>.Shared.Rent(half) : null;
        try
        {
            MergeRuns(keys, values, firstRun, keyBuffer, valueBuffer, comparer);
        }
        finally
        {
            ReturnBuffer(keyBuffer, half);
            if (valueBuffer is not null)
            {
                ReturnBuffer(valueBuffer, half);
            }
        }
    }

    private static void ReturnBuffer<T>(T[] buffer, int used)
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Array.Clear(buffer, 0, used);
        }

        ArrayPool<T>.Shared.Return(buffer);
    }

    // Powersort's main loop: walk the runs left to right, give each boundary between two adjacent runs a
    // node power (the depth at which the boundary would split a perfectly balanced merge tree over the
    // whole input), and merge pending runs whenever the stack's top boundary is deeper than the new one.
    // The stack only ever holds runs of strictly increasing power.
    private static void MergeRuns<TKey, TValue, TComparer>(
        Span<TKey> keys, Span<TValue> values, int firstRun, Span<TKey> keyBuffer, Span<TValue> valueBuffer, TComparer comparer)
        where TComparer : struct, IComparer<TKey>
    {
        int n = keys.Length;
        Span<int> starts = stackalloc int[MaxPendingRuns];
        Span<int> powers = stackalloc int[MaxPendingRuns];
        int pending = 0;

        // The current run is [start, start + length); every pending run ends where the next one begins.
        int start = 0;
        int length = firstRun;
        do
        {
            int nextStart = start + length;
            int nextLength = ExtendRun(keys, values, nextStart, comparer);
            int power = NodePower(start, length, nextLength, n);
            while (pending > 0 && powers[pending - 1] > power)
            {
                pending--;
                int left = starts[pending];
                Merge(keys, values, left, start, start + length, keyBuffer, valueBuffer, comparer);
                length += start - left;
                start = left;
            }

            starts[pending] = start;
            powers[pending] = power;
            pending++;
            start = nextStart;
            length = nextLength;
        }
        while (start + length < n);

        while (pending > 0)
        {
            pending--;
            int left = starts[pending];
            Merge(keys, values, left, start, start + length, keyBuffer, valueBuffer, comparer);
            length += start - left;
            start = left;
        }
    }

    // The node power of the boundary between runs [s1, s1 + n1) and [s1 + n1, s1 + n1 + n2) in an input of
    // length n: one more than the number of leading bits the two runs' midpoints, as fractions of n, share.
    // Computed by long division on doubled midpoints so it stays in integers; long because the doubled
    // midpoint of a run near the end of a 2^31 element span does not fit in an int.
    private static int NodePower(int s1, int n1, int n2, int n)
    {
        long a = 2L * s1 + n1;
        long b = a + n1 + n2;
        int power = 0;
        while (true)
        {
            power++;
            if (a >= n)
            {
                a -= n;
                b -= n;
            }
            else if (b >= n)
            {
                return power;
            }

            a <<= 1;
            b <<= 1;
        }
    }

    // Finds the run starting at lo and, if it is shorter than MinRun, extends it to MinRun elements (or to
    // the end of the input) by binary insertion. Returns the run's length.
    private static int ExtendRun<TKey, TValue, TComparer>(Span<TKey> keys, Span<TValue> values, int lo, TComparer comparer)
        where TComparer : struct, IComparer<TKey>
    {
        int n = keys.Length;
        int length = CountRun(keys, values, lo, comparer);
        if (length < MinRun)
        {
            // Written as lo + min(remaining, MinRun) so the bound cannot overflow near int.MaxValue.
            int end = lo + Math.Min(n - lo, MinRun);
            InsertionSort(keys, values, lo, lo + length, end, comparer);
            length = end - lo;
        }

        return length;
    }

    // Returns the length of the natural run starting at lo: the longest non-descending prefix, or the
    // longest strictly descending one, which is reversed in place. Only a strictly descending run may be
    // reversed, since reversing equal elements would break stability.
    private static int CountRun<TKey, TValue, TComparer>(Span<TKey> keys, Span<TValue> values, int lo, TComparer comparer)
        where TComparer : struct, IComparer<TKey>
    {
        int n = keys.Length;
        int hi = lo + 1;
        if (hi == n)
        {
            return 1;
        }

        if (comparer.Compare(keys[hi], keys[lo]) < 0)
        {
            do
            {
                hi++;
            }
            while (hi < n && comparer.Compare(keys[hi], keys[hi - 1]) < 0);

            keys[lo..hi].Reverse();
            if (HasValues<TValue>())
            {
                values[lo..hi].Reverse();
            }
        }
        else
        {
            do
            {
                hi++;
            }
            while (hi < n && comparer.Compare(keys[hi], keys[hi - 1]) >= 0);
        }

        return hi - lo;
    }

    // Binary insertion sort of [lo, end), whose prefix [lo, sorted) is already in order. Each element is
    // inserted after every element that compares equal to it, which keeps the sort stable. The search runs
    // before anything moves, so a comparer that throws leaves every element in place.
    private static void InsertionSort<TKey, TValue, TComparer>(
        Span<TKey> keys, Span<TValue> values, int lo, int sorted, int end, TComparer comparer)
        where TComparer : struct, IComparer<TKey>
    {
        for (int i = sorted; i < end; i++)
        {
            TKey key = keys[i];
            int at = UpperBound(keys, lo, i, key, comparer);
            for (int k = i; k > at; k--)
            {
                keys[k] = keys[k - 1];
            }

            keys[at] = key;
            if (HasValues<TValue>())
            {
                TValue value = values[i];
                for (int k = i; k > at; k--)
                {
                    values[k] = values[k - 1];
                }

                values[at] = value;
            }
        }
    }

    // The first index in [lo, hi) whose key orders strictly after key, or hi if none does.
    private static int UpperBound<TKey, TComparer>(Span<TKey> keys, int lo, int hi, TKey key, TComparer comparer)
        where TComparer : struct, IComparer<TKey>
    {
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (comparer.Compare(key, keys[mid]) < 0)
            {
                hi = mid;
            }
            else
            {
                lo = mid + 1;
            }
        }

        return lo;
    }

    // The first index in [lo, hi) whose key does not order before key, or hi if every one does.
    private static int LowerBound<TKey, TComparer>(Span<TKey> keys, int lo, int hi, TKey key, TComparer comparer)
        where TComparer : struct, IComparer<TKey>
    {
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (comparer.Compare(keys[mid], key) < 0)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    // Merges the adjacent sorted runs [lo, mid) and [mid, hi). Elements of the left run that order at or
    // before the right run's first element are already in place, as are elements of the right run that
    // order at or after the left run's last; both are trimmed off by binary search first. That makes an
    // already-ordered pair cost two searches and no moves, and on input with a long sorted prefix it keeps
    // the buffered side short. The shorter remaining side is buffered.
    private static void Merge<TKey, TValue, TComparer>(
        Span<TKey> keys, Span<TValue> values, int lo, int mid, int hi, Span<TKey> keyBuffer, Span<TValue> valueBuffer, TComparer comparer)
        where TComparer : struct, IComparer<TKey>
    {
        lo = UpperBound(keys, lo, mid, keys[mid], comparer);
        if (lo == mid)
        {
            return;
        }

        hi = LowerBound(keys, mid, hi, keys[mid - 1], comparer);
        if (mid - lo <= hi - mid)
        {
            MergeLow(keys, values, lo, mid, hi, keyBuffer, valueBuffer, comparer);
        }
        else
        {
            MergeHigh(keys, values, lo, mid, hi, keyBuffer, valueBuffer, comparer);
        }
    }

    // How far a merge loop has got, kept in memory rather than in the loop's locals. A merge that has
    // buffered one run has to write that buffer back if the comparer throws, and a try/finally around the
    // loop itself would make the JIT spill every local that is live into the handler on every iteration,
    // which measured about 20% slower. The loop instead runs in a method of its own with no exception
    // handling and records its two cursors here as it advances; only the caller has a finally block.
    private struct MergeProgress
    {
        public int Left;
        public int Right;
    }

    // Buffers the left run and merges forward. With a consistent comparer the right run runs out first
    // (its last element orders before the left run's last); the left-side bound only matters for a
    // comparer that is not a total order. At every step the slots from lo + left + (right - mid) up to
    // right are exactly the buffered elements not yet written back, so the finally block restores a
    // permutation of the input whether the loop finishes or the comparer throws.
    private static void MergeLow<TKey, TValue, TComparer>(
        Span<TKey> keys, Span<TValue> values, int lo, int mid, int hi, Span<TKey> keyBuffer, Span<TValue> valueBuffer, TComparer comparer)
        where TComparer : struct, IComparer<TKey>
    {
        int leftLength = mid - lo;
        keys[lo..mid].CopyTo(keyBuffer);
        if (HasValues<TValue>())
        {
            values[lo..mid].CopyTo(valueBuffer);
        }

        var progress = new MergeProgress { Left = 0, Right = mid };
        try
        {
            MergeLowLoop(keys, values, lo, mid, hi, keyBuffer, valueBuffer, comparer, ref progress);
        }
        finally
        {
            int written = lo + progress.Left + (progress.Right - mid);
            keyBuffer[progress.Left..leftLength].CopyTo(keys[written..]);
            if (HasValues<TValue>())
            {
                valueBuffer[progress.Left..leftLength].CopyTo(values[written..]);
            }
        }
    }

    private static void MergeLowLoop<TKey, TValue, TComparer>(
        Span<TKey> keys, Span<TValue> values, int lo, int mid, int hi, Span<TKey> keyBuffer, Span<TValue> valueBuffer, TComparer comparer, ref MergeProgress progress)
        where TComparer : struct, IComparer<TKey>
    {
        int leftLength = mid - lo;
        int left = 0;
        int right = mid;
        int written = lo;
        while (left < leftLength && right < hi)
        {
            // Strictly less: on a tie the left (earlier) element goes first, which is what keeps it stable.
            if (comparer.Compare(keys[right], keyBuffer[left]) < 0)
            {
                keys[written] = keys[right];
                if (HasValues<TValue>())
                {
                    values[written] = values[right];
                }

                progress.Right = ++right;
            }
            else
            {
                keys[written] = keyBuffer[left];
                if (HasValues<TValue>())
                {
                    values[written] = valueBuffer[left];
                }

                progress.Left = ++left;
            }

            written++;
        }
    }

    // Buffers the right run and merges backward from the end. With a consistent comparer the left run
    // runs out first (its first element orders after the right run's first); the right-side bound only
    // matters for a comparer that is not a total order. At every step the slots just after left, as many
    // as remain in the buffer, are exactly the buffered elements not yet written back, so the finally
    // block restores a permutation of the input whether the loop finishes or the comparer throws.
    private static void MergeHigh<TKey, TValue, TComparer>(
        Span<TKey> keys, Span<TValue> values, int lo, int mid, int hi, Span<TKey> keyBuffer, Span<TValue> valueBuffer, TComparer comparer)
        where TComparer : struct, IComparer<TKey>
    {
        keys[mid..hi].CopyTo(keyBuffer);
        if (HasValues<TValue>())
        {
            values[mid..hi].CopyTo(valueBuffer);
        }

        var progress = new MergeProgress { Left = mid - 1, Right = hi - mid - 1 };
        try
        {
            MergeHighLoop(keys, values, lo, mid, hi, keyBuffer, valueBuffer, comparer, ref progress);
        }
        finally
        {
            keyBuffer[..(progress.Right + 1)].CopyTo(keys[(progress.Left + 1)..]);
            if (HasValues<TValue>())
            {
                valueBuffer[..(progress.Right + 1)].CopyTo(values[(progress.Left + 1)..]);
            }
        }
    }

    private static void MergeHighLoop<TKey, TValue, TComparer>(
        Span<TKey> keys, Span<TValue> values, int lo, int mid, int hi, Span<TKey> keyBuffer, Span<TValue> valueBuffer, TComparer comparer, ref MergeProgress progress)
        where TComparer : struct, IComparer<TKey>
    {
        int left = mid - 1;
        int right = hi - mid - 1;
        int written = hi - 1;
        while (right >= 0 && left >= lo)
        {
            // Strictly less: on a tie the right (later) element is written to the later slot first,
            // which is what keeps it stable.
            if (comparer.Compare(keyBuffer[right], keys[left]) < 0)
            {
                keys[written] = keys[left];
                if (HasValues<TValue>())
                {
                    values[written] = values[left];
                }

                progress.Left = --left;
            }
            else
            {
                keys[written] = keyBuffer[right];
                if (HasValues<TValue>())
                {
                    values[written] = valueBuffer[right];
                }

                progress.Right = --right;
            }

            written--;
        }
    }
}
