using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Celerity.Sorting;

/// <summary>
/// <b>Ordinal string sort</b>: a multikey quicksort (Bentley–Sedgewick 3-way radix quicksort) over
/// a cached integer key, ordering strings by UTF-16 code unit exactly as
/// <see cref="StringComparer.Ordinal"/> and <see cref="string.CompareOrdinal(string, string)"/> do.
/// Where <see cref="Array.Sort{T}(T[], IComparer{T})"/> with <see cref="StringComparer.Ordinal"/>
/// dereferences two strings per comparison and re-reads the prefix they share every time, this sort
/// packs the next four characters of each string into one <see cref="ulong"/> and partitions on that
/// integer, so each string is dereferenced once per four characters of the prefix that tells it apart.
/// Supports keys alone, keys with a parallel payload, and keys with an index permutation
/// (<see cref="ArgSort(ReadOnlySpan{string}, Span{int})"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The documented BCL-beating workload</b> is a large sort of string keys — ids, URLs, file
/// paths, composite cache keys like <c>tenant:42:user:000123</c> — from roughly <b>1,000 elements
/// upward</b>. The margin is largest on keys that share long prefixes, because that shared prefix is
/// exactly what a comparison sort re-scans on every comparison and what this sort reads once per
/// partition level. Measured figures, including the shapes where the margin is small, are on the
/// <a href="https://marius-bughiu.github.io/Celerity/dev/bench/">benchmark dashboard</a>.
/// </para>
/// <para>
/// <b>Where it does not win.</b> Input that is already in order is detected by one linear scan
/// and left alone, but input that is <i>nearly</i> in order pays for that scan and then sorts in
/// full, where the BCL's introsort stays cheap. Inputs that are mostly duplicates of a
/// few values gain less than distinct keys, because a run of equal strings has to be read to its end
/// before it is known to be equal.
/// </para>
/// <para>
/// <b>Ordering contract.</b> The order is ordinal: by UTF-16 code unit, with a proper prefix before
/// any longer string that extends it. It is not culture-aware and not case-insensitive. For those
/// orders a comparer is unavoidable, and with a comparer the BCL's introsort is the right tool.
/// <see langword="null"/> elements are allowed and sort before every string, including the empty
/// string, which matches <see cref="StringComparer.Ordinal"/>. Sorting is <b>stable</b>: strings
/// that compare equal keep their input order, so the key+payload form keeps equal-keyed payloads in
/// order and <see cref="ArgSort(ReadOnlySpan{string}, Span{int})"/> lists the indices of equal keys
/// ascending. <see cref="Array.Sort{T}(T[], IComparer{T})"/> guarantees neither.
/// </para>
/// <para>
/// <b>Nullability.</b> The parameters are annotated as non-nullable <see cref="string"/>, so the
/// common call site, a <c>string[]</c>, compiles without a warning; C# does not convert a span of
/// <c>string</c> to a span of <c>string?</c> without one. Null elements are still handled at run
/// time and sort first. To pass a <c>string?[]</c>, use the null-forgiving operator:
/// <c>StringSort.Sort(keys.AsSpan()!)</c>.
/// </para>
/// <para>
/// <b>Cost.</b> Each call needs 16 bytes of working state per element. Up to 256 elements it is
/// stack-allocated; above that it is rented from <see cref="ArrayPool{T}"/>. A run of more than 32
/// identical strings also rents 8 bytes per string in the run, to radix-sort their input positions.
/// <c>Sort</c> also rents a copy of the keys (and of the payload) to gather the result from; the
/// copies are cleared before they go back to the pool, so it never holds the caller's references.
/// Every buffer comes from the pool, so once the pool is warm a call allocates nothing. Partitioning compares integers and reads each distinguishing prefix about once
/// per four characters. It carries an introsort-style depth budget that falls back to a heap sort, so
/// no input, adversarial or otherwise, makes the number of partition steps or comparisons quadratic
/// in <c>n</c>; a fallback comparison between strings whose packed windows tie does read their tails.
/// </para>
/// </remarks>
public static class StringSort
{
    // At or below this many entries a range is finished by insertion sort.
    private const int InsertionThreshold = 16;

    // Up to this many entries (16 bytes each, so 4 KB) the working state lives on the stack.
    private const int StackEntryLimit = 256;

    // The low lane of a packed key. Zero there means the window ran past the end of at least one
    // string in the group — or that the string holds a literal U+0000 at that position.
    private const ulong LastLane = 0xFFFF;

    // How many characters a packed key covers.
    private const int LaneCount = 4;

    // A group of ended strings at or below this size is ordered by insertion sort; above it, by a
    // radix sort of input positions.
    private const int SmallGroup = 32;

    /// <summary>Sorts <paramref name="keys"/> in ascending ordinal order.</summary>
    /// <param name="keys">The strings to sort in place. <see langword="null"/> elements are tolerated and sort first.</param>
    public static void Sort(Span<string> keys)
    {
        if (keys.Length < 2 || IsOrdered(keys!))
        {
            return;
        }

        Entry[]? rented = null;
        Span<Entry> entries = keys.Length <= StackEntryLimit
            ? stackalloc Entry[StackEntryLimit]
            : (rented = ArrayPool<Entry>.Shared.Rent(keys.Length));
        entries = entries[..keys.Length];
        try
        {
            Rank(keys!, entries);
            Gather(keys, entries);
        }
        finally
        {
            Return(rented);
        }
    }

    /// <summary>Sorts <paramref name="keys"/> in ascending ordinal order, moving <paramref name="values"/> with them.</summary>
    /// <typeparam name="TValue">The payload type. Never compared; only moved.</typeparam>
    /// <param name="keys">The strings to sort in place. <see langword="null"/> elements are tolerated and sort first.</param>
    /// <param name="values">The payload, parallel to <paramref name="keys"/> and at least as long. Permuted to match; equal keys keep their payloads in input order.</param>
    /// <exception cref="ArgumentException"><paramref name="values"/> is shorter than <paramref name="keys"/>, or shares storage with it.</exception>
    public static void Sort<TValue>(Span<string> keys, Span<TValue> values)
    {
        SortingGuard.RequireLength(values.Length, keys.Length, nameof(values));
        SortingGuard.RequireDistinctStorage(keys, values, nameof(values));
        if (keys.Length < 2 || IsOrdered(keys!))
        {
            return;
        }

        Entry[]? rented = null;
        Span<Entry> entries = keys.Length <= StackEntryLimit
            ? stackalloc Entry[StackEntryLimit]
            : (rented = ArrayPool<Entry>.Shared.Rent(keys.Length));
        entries = entries[..keys.Length];
        try
        {
            Rank(keys!, entries);
            Gather(keys, values, entries);
        }
        finally
        {
            Return(rented);
        }
    }

    /// <summary>
    /// Writes into <paramref name="indices"/> the permutation that sorts <paramref name="keys"/> in
    /// ascending ordinal order, leaving <paramref name="keys"/> untouched.
    /// </summary>
    /// <param name="keys">The strings to rank. Not modified. <see langword="null"/> elements are tolerated and rank first.</param>
    /// <param name="indices">Receives <c>keys.Length</c> indices into <paramref name="keys"/>, in ascending key order; the indices of equal keys appear in ascending order.</param>
    /// <exception cref="ArgumentException"><paramref name="indices"/> is shorter than <paramref name="keys"/>.</exception>
    /// <remarks>
    /// The point of an argsort is to avoid moving a wide payload: rank once, then gather. No
    /// aliasing check is needed or made, because a span of strings and a span of integers can only
    /// share storage when one buffer has been deliberately reinterpreted as the other type.
    /// </remarks>
    public static void ArgSort(ReadOnlySpan<string> keys, Span<int> indices)
    {
        SortingGuard.RequireLength(indices.Length, keys.Length, nameof(indices));
        if (keys.Length < 2 || IsOrdered(keys!))
        {
            for (int i = 0; i < keys.Length; i++)
            {
                indices[i] = i;
            }

            return;
        }

        Entry[]? rented = null;
        Span<Entry> entries = keys.Length <= StackEntryLimit
            ? stackalloc Entry[StackEntryLimit]
            : (rented = ArrayPool<Entry>.Shared.Rent(keys.Length));
        entries = entries[..keys.Length];
        try
        {
            Rank(keys!, entries);
            for (int i = 0; i < entries.Length; i++)
            {
                indices[i] = entries[i].Index;
            }
        }
        finally
        {
            Return(rented);
        }
    }

    // ---- ranking ------------------------------------------------------------------------------

    /// <summary>
    /// Whether <paramref name="keys"/> is already in ascending ordinal order, in which case the input
    /// order is the stable answer and there is nothing to do.
    /// </summary>
    /// <remarks>
    /// On unordered input the first inversion ends the scan, usually within a few keys, so the cost
    /// is negligible. On ordered input it replaces the whole sort with one linear pass, where
    /// partitioning would otherwise shuffle an input that introsort handles cheaply.
    /// </remarks>
    private static bool IsOrdered(ReadOnlySpan<string?> keys)
    {
        for (int i = 1; i < keys.Length; i++)
        {
            if (string.CompareOrdinal(keys[i - 1], keys[i]) > 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Fills <paramref name="entries"/> so that entry <c>i</c> names the input position of the
    /// <c>i</c>-th key in sorted order. Nulls go first, in input order, and are never looked at
    /// again; every other key gets its first packed window.
    /// </summary>
    /// <remarks>
    /// The internals take <c>string?</c> spans because the public annotation is a convenience for
    /// callers, not a guarantee: nulls are part of the contract, so every entry point hands its keys
    /// over with the null-forgiving operator and the null checks below are real.
    /// </remarks>
    private static void Rank(ReadOnlySpan<string?> keys, Span<Entry> entries)
    {
        int nulls = 0;
        foreach (string? key in keys)
        {
            if (key is null)
            {
                nulls++;
            }
        }

        int nullCursor = 0;
        int cursor = nulls;
        for (int i = 0; i < keys.Length; i++)
        {
            string? key = keys[i];
            if (key is null)
            {
                entries[nullCursor++] = new Entry(0, i);
            }
            else
            {
                entries[cursor++] = new Entry(Pack(key, 0), i);
            }
        }

        Span<Entry> strings = entries[nulls..];
        SortRange(strings, keys, 0, DepthLimit(strings.Length));
    }

    /// <summary>
    /// Sorts <paramref name="range"/>, whose keys all agree on their first <paramref name="depth"/>
    /// characters and whose packed keys hold the window starting there.
    /// </summary>
    /// <remarks>
    /// Each step splits the range three ways on the packed key. The two outer parts stay at this
    /// depth; the equal part moves four characters deeper. The step continues with the largest part
    /// and recurses into the other two, each of which therefore holds at most half the range, so the
    /// stack stays <c>O(log n)</c> deep however long the shared prefixes are.
    /// </remarks>
    private static void SortRange(Span<Entry> range, ReadOnlySpan<string?> keys, int depth, int budget)
    {
        while (range.Length > InsertionThreshold)
        {
            if (budget == 0)
            {
                HeapSort(range, keys, depth);
                return;
            }

            budget--;
            ulong pivot = Partition(range, out int equalFirst, out int equalEnd);
            Span<Entry> less = range[..equalFirst];
            Span<Entry> equal = range[equalFirst..equalEnd];
            Span<Entry> greater = range[equalEnd..];

            if (equal.Length >= less.Length && equal.Length >= greater.Length)
            {
                SortRange(less, keys, depth, budget);
                SortRange(greater, keys, depth, budget);
                range = Descend(equal, keys, ref depth, pivot);
                budget = DepthLimit(range.Length);
            }
            else
            {
                int equalDepth = depth;
                Span<Entry> deeper = Descend(equal, keys, ref equalDepth, pivot);
                SortRange(deeper, keys, equalDepth, DepthLimit(deeper.Length));
                if (less.Length < greater.Length)
                {
                    SortRange(less, keys, depth, budget);
                    range = greater;
                }
                else
                {
                    SortRange(greater, keys, depth, budget);
                    range = less;
                }
            }
        }

        InsertionSort(range, keys, depth);
    }

    /// <summary>
    /// Dutch-national-flag partition of <paramref name="range"/> on its packed keys around a
    /// median-of-three pivot. On return <c>[0, equalFirst)</c> is less than the pivot,
    /// <c>[equalFirst, equalEnd)</c> equals it, and <c>[equalEnd, Length)</c> is greater.
    /// </summary>
    private static ulong Partition(Span<Entry> range, out int equalFirst, out int equalEnd)
    {
        ulong pivot = MedianOfThree(range[0].Key, range[range.Length >> 1].Key, range[^1].Key);
        int less = 0;
        int i = 0;
        int greater = range.Length - 1;
        while (i <= greater)
        {
            ulong key = range[i].Key;
            if (key < pivot)
            {
                (range[less], range[i]) = (range[i], range[less]);
                less++;
                i++;
            }
            else if (key > pivot)
            {
                (range[greater], range[i]) = (range[i], range[greater]);
                greater--;
            }
            else
            {
                i++;
            }
        }

        equalFirst = less;
        equalEnd = greater + 1;
        return pivot;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong MedianOfThree(ulong a, ulong b, ulong c) =>
        a < b
            ? (b < c ? b : Math.Max(a, c))
            : (a < c ? a : Math.Max(b, c));

    /// <summary>
    /// Takes a group whose packed keys are all <paramref name="pivot"/> to the next window: returns
    /// the entries that still need sorting, re-packed four characters deeper, and advances
    /// <paramref name="depth"/> to match.
    /// </summary>
    /// <remarks>
    /// When the pivot's last lane is non-zero, every string in the group runs past the window, so
    /// the whole group goes deeper. When it is zero, the group mixes strings that end inside the
    /// window with strings that hold a literal U+0000 there; <see cref="SplitEnded"/> settles the
    /// first kind and hands back only the second.
    /// </remarks>
    private static Span<Entry> Descend(Span<Entry> group, ReadOnlySpan<string?> keys, scoped ref int depth, ulong pivot)
    {
        Span<Entry> deeper = (pivot & LastLane) == 0 ? SplitEnded(group, keys, depth) : group;
        depth += LaneCount;
        for (int i = 0; i < deeper.Length; i++)
        {
            deeper[i].Key = Pack(keys[deeper[i].Index]!, depth);
        }

        return deeper;
    }

    /// <summary>
    /// Orders a group that agrees on a window whose last lane is zero, and returns the tail of it
    /// that is still undecided.
    /// </summary>
    /// <remarks>
    /// Two strings with the same packed key differ only in where they end inside the window: a lane
    /// past the end packs as zero, which is also how a literal U+0000 packs. So each entry is
    /// classed by how many of the four lanes it actually fills — fewer first, since a proper prefix
    /// sorts first — and within a class below four the strings are identical, so they are put in
    /// input order and are done. The class of four, strings that fill the window with a U+0000 in its
    /// last lane, is returned for the next window.
    /// <para>
    /// A small group is ordered by insertion sort on a key of (class, input position), which is
    /// unique, so the comparison never reads a string. A large one — a run of duplicates — is placed
    /// by class and then each class's positions are radix-sorted, which keeps a million copies of
    /// one string linear. Both orderings are in place or pooled. The BCL's span sort is avoided
    /// because it boxes a struct comparer on every call.
    /// </para>
    /// </remarks>
    private static Span<Entry> SplitEnded(Span<Entry> group, ReadOnlySpan<string?> keys, int depth)
    {
        // The common case by far: one string that ends here. It is already in place.
        if (group.Length == 1)
        {
            return keys[group[0].Index]!.Length - depth >= LaneCount ? group : default;
        }

        if (group.Length > SmallGroup)
        {
            return SplitEndedRun(group, keys, depth);
        }

        int full = 0;
        for (int i = 0; i < group.Length; i++)
        {
            int filled = Math.Min(keys[group[i].Index]!.Length - depth, LaneCount);
            full += filled == LaneCount ? 1 : 0;
            group[i].Key = ((ulong)(uint)filled << 32) | (uint)group[i].Index;
        }

        // (class, input position) is unique, so the comparison never reads a string.
        InsertionSort(group, keys, depth);
        return group[(group.Length - full)..];
    }

    /// <summary>
    /// <see cref="SplitEnded"/> for a run of more than <see cref="SmallGroup"/> entries: placed by
    /// class, then each class's input positions are radix-sorted, which keeps a million copies of
    /// one string linear.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Span<Entry> SplitEndedRun(Span<Entry> group, ReadOnlySpan<string?> keys, int depth)
    {
        // classStart[c + 1] counts class c; after the prefix sum, classStart[c] is where class c starts.
        Span<int> classStart = stackalloc int[LaneCount + 2];
        classStart.Clear();
        for (int i = 0; i < group.Length; i++)
        {
            int filled = Math.Min(keys[group[i].Index]!.Length - depth, LaneCount);
            group[i].Key = (ulong)(uint)filled;
            classStart[filled + 1]++;
        }

        int full = classStart[LaneCount + 1];
        for (int c = 1; c < classStart.Length; c++)
        {
            classStart[c] += classStart[c - 1];
        }

        // Two rents rather than one of twice the size, so a run can be as long as any span.
        uint[] rentedPositions = ArrayPool<uint>.Shared.Rent(group.Length);
        uint[] rentedScratch = ArrayPool<uint>.Shared.Rent(group.Length);
        try
        {
            Span<uint> positions = rentedPositions.AsSpan(0, group.Length);
            Span<uint> scratch = rentedScratch.AsSpan(0, group.Length);
            Span<int> cursor = stackalloc int[LaneCount + 1];
            classStart[..cursor.Length].CopyTo(cursor);
            foreach (Entry entry in group)
            {
                positions[cursor[(int)entry.Key]++] = (uint)entry.Index;
            }

            for (int c = 0; c <= LaneCount; c++)
            {
                int start = classStart[c];
                int count = classStart[c + 1] - start;
                if (count > 1)
                {
                    RadixKernel.Sort32<byte>(
                        positions.Slice(start, count),
                        default,
                        scratch.Slice(start, count),
                        default,
                        hasValues: false,
                        signed: false);
                }
            }

            for (int i = 0; i < group.Length; i++)
            {
                group[i].Index = (int)positions[i];
            }
        }
        finally
        {
            ArrayPool<uint>.Shared.Return(rentedPositions);
            ArrayPool<uint>.Shared.Return(rentedScratch);
        }

        return group[(group.Length - full)..];
    }

    // ---- small and fallback sorts -------------------------------------------------------------

    private static void InsertionSort(Span<Entry> range, ReadOnlySpan<string?> keys, int depth)
    {
        for (int i = 1; i < range.Length; i++)
        {
            Entry item = range[i];
            int j = i - 1;
            while (j >= 0 && Compare(range[j], item, keys, depth) > 0)
            {
                range[j + 1] = range[j];
                j--;
            }

            range[j + 1] = item;
        }
    }

    /// <summary>
    /// The depth budget's fallback: an in-place heap sort on the full ordering, which bounds the
    /// worst case at <c>O(n log n)</c> comparisons when partitioning keeps splitting badly.
    /// </summary>
    private static void HeapSort(Span<Entry> range, ReadOnlySpan<string?> keys, int depth)
    {
        for (int root = (range.Length >> 1) - 1; root >= 0; root--)
        {
            SiftDown(range, root, range.Length, keys, depth);
        }

        for (int end = range.Length - 1; end > 0; end--)
        {
            (range[0], range[end]) = (range[end], range[0]);
            SiftDown(range, 0, end, keys, depth);
        }
    }

    private static void SiftDown(Span<Entry> heap, int root, int count, ReadOnlySpan<string?> keys, int depth)
    {
        while (true)
        {
            int child = (2 * root) + 1;
            if (child >= count)
            {
                return;
            }

            if (child + 1 < count && Compare(heap[child + 1], heap[child], keys, depth) > 0)
            {
                child++;
            }

            if (Compare(heap[child], heap[root], keys, depth) <= 0)
            {
                return;
            }

            (heap[root], heap[child]) = (heap[child], heap[root]);
            root = child;
        }
    }

    /// <summary>
    /// The full order between two entries of a range that agree on their first
    /// <paramref name="depth"/> characters: the packed window, then the rest of the strings, then
    /// input position — so equal strings never compare equal, which is what makes the result stable.
    /// </summary>
    private static int Compare(in Entry left, in Entry right, ReadOnlySpan<string?> keys, int depth)
    {
        if (left.Key != right.Key)
        {
            return left.Key < right.Key ? -1 : 1;
        }

        int order = keys[left.Index].AsSpan(depth).SequenceCompareTo(keys[right.Index].AsSpan(depth));
        return order != 0 ? order : left.Index.CompareTo(right.Index);
    }

    // ---- packing, gathering, plumbing ---------------------------------------------------------

    /// <summary>
    /// Packs the four characters of <paramref name="key"/> starting at <paramref name="depth"/>
    /// into one integer, first character in the high lane, so integer order is ordinal order over
    /// the window. A lane past the end of the string is zero.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Pack(string key, int depth)
    {
        int remaining = key.Length - depth;
        ref char first = ref Unsafe.Add(ref MemoryMarshal.GetReference(key.AsSpan()), depth);
        if (remaining >= LaneCount)
        {
            return ((ulong)first << 48)
                | ((ulong)Unsafe.Add(ref first, 1) << 32)
                | ((ulong)Unsafe.Add(ref first, 2) << 16)
                | Unsafe.Add(ref first, 3);
        }

        ulong packed = 0;
        for (int i = 0; i < remaining; i++)
        {
            packed |= (ulong)Unsafe.Add(ref first, i) << (48 - (16 * i));
        }

        return packed;
    }

    /// <summary>
    /// Rearranges <paramref name="keys"/> so position <c>i</c> holds the key that entry <c>i</c>
    /// names, through a pooled copy of the keys.
    /// </summary>
    /// <remarks>
    /// Copying first and then gathering keeps every load independent, so the reads of a large span
    /// overlap in the memory system. Following the permutation's cycles in place would avoid the copy,
    /// but each step's address there depends on the previous load, which made a million-key sort a
    /// serial chain of cache misses and cost about a third of its time.
    /// </remarks>
    private static void Gather(Span<string> keys, Span<Entry> entries)
    {
        string[] copy = ArrayPool<string>.Shared.Rent(keys.Length);
        try
        {
            keys.CopyTo(copy);
            for (int i = 0; i < entries.Length; i++)
            {
                keys[i] = copy[entries[i].Index];
            }
        }
        finally
        {
            // Clear so the pool does not keep the caller's strings alive.
            ArrayPool<string>.Shared.Return(copy, clearArray: true);
        }
    }

    /// <inheritdoc cref="Gather(Span{string}, Span{Entry})"/>
    private static void Gather<TValue>(Span<string> keys, Span<TValue> values, Span<Entry> entries)
    {
        Gather(keys, entries);
        TValue[] copy = ArrayPool<TValue>.Shared.Rent(keys.Length);
        try
        {
            values[..keys.Length].CopyTo(copy);
            for (int i = 0; i < entries.Length; i++)
            {
                values[i] = copy[entries[i].Index];
            }
        }
        finally
        {
            ArrayPool<TValue>.Shared.Return(copy, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<TValue>());
        }
    }

    private static void Return(Entry[]? rented)
    {
        if (rented is not null)
        {
            ArrayPool<Entry>.Shared.Return(rented);
        }
    }

    // Two levels of partitioning per halving of the range, the same budget introsort and
    // PartialSort's introselect use. Only ranges above InsertionThreshold ever spend it, so the
    // meaningless value it yields for an empty range is never read.
    private static int DepthLimit(int length) => 2 * (31 - BitOperations.LeadingZeroCount((uint)length));

    /// <summary>One string's place in the sort: its packed window and its input position.</summary>
    private struct Entry
    {
        public ulong Key;
        public int Index;

        public Entry(ulong key, int index)
        {
            Key = key;
            Index = index;
        }
    }
}
