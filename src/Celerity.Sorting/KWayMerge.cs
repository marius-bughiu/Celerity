using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace Celerity.Sorting;

/// <summary>
/// <b>A stable k-way merge</b> of already-sorted runs into one sorted output, through a
/// <i>loser tree</i> (a tournament tree): each output element costs at most <c>⌈log₂ k⌉</c>
/// comparisons, one per tree level, where a heap spends several per level sifting. Two forms ship —
/// <see cref="Merge{T}(ReadOnlySpan{T}, ReadOnlySpan{int}, Span{T})"/> merges runs packed back to
/// back in one span into a destination span and allocates nothing once the shared pool is warm, and
/// <see cref="Merge{T}(IEnumerable{IEnumerable{T}})"/> merges sorted sequences lazily, pulling one
/// element at a time from each.
/// </summary>
/// <remarks>
/// <para>
/// <b>The gap.</b> .NET has no multiway merge. The BCL answers are to concatenate and
/// <see cref="Array.Sort{T}(T[])"/> — which discards the order the runs already have and pays
/// <c>O(n log n)</c> for an <c>O(n log k)</c> problem — or to hand-roll the merge on
/// <see cref="PriorityQueue{TElement, TPriority}"/>, which costs a dequeue and an enqueue per
/// element, each a sift through a 4-ary heap, and is not stable. A loser tree keeps, at each internal
/// node, the run that <i>lost</i> the match played there, so replacing the winner replays exactly
/// one leaf-to-root path with one comparison per level and no sibling comparison.
/// </para>
/// <para>
/// <b>Workloads:</b> the merge phase of an external or run-based sort, combining per-shard or
/// per-partition sorted results, merging posting lists, sorted log or time-series files, and the
/// compaction step of a log-structured merge store.
/// </para>
/// <para>
/// <b>Stable.</b> Equal elements come out in run order — every equal element of run <c>i</c>
/// before any of run <c>j &gt; i</c> — and in their original order within a run. Ties are broken on
/// the run index inside the tree, so no comparison is spent on it.
/// </para>
/// <para>
/// <b>The runs are not verified to be sorted</b>; checking would cost a full extra pass. An unsorted
/// run gives an unspecified order, but the output is still a permutation of the input — nothing is
/// lost or duplicated.
/// </para>
/// <para>
/// <b>Where it does not win.</b> For primitive keys that are all in memory,
/// <see cref="RadixSort"/> over the concatenation ignores the runs entirely and is still several
/// times faster, because it does no comparisons at all; the merge wins when the keys are not radix-sortable
/// (strings, records, a custom comparer), when stability across runs matters, or when the runs are
/// streams that never all fit in memory. At <c>k = 2</c> a plain two-way merge is used internally.
/// </para>
/// <para>
/// Each entry point comes in two forms: one constrained to <see cref="IComparable{T}"/> for the
/// natural order, and one taking a <c>struct</c> comparer as a type parameter so the JIT
/// devirtualizes and inlines every comparison — the same zero-cost-abstraction rule the rest of
/// Celerity follows.
/// </para>
/// </remarks>
public static class KWayMerge
{
    // Up to this many runs the span merge keeps its run bookkeeping on the stack: two ints per run
    // for the cursors, one for the tree's losers, two for the build-time contestants — 2.5 KB at the
    // threshold. The tree's keys are a T, which may be a reference, so they are always rented.
    private const int StackRunLimit = 128;

    /// <summary>
    /// Merges the sorted runs packed back to back in <paramref name="source"/> into one sorted
    /// sequence in <paramref name="destination"/>, stably.
    /// </summary>
    /// <typeparam name="T">The element type, ordered by <see cref="IComparable{T}.CompareTo"/>.</typeparam>
    /// <param name="source">
    /// The runs, concatenated: the first <c>runLengths[0]</c> elements are run 0, the next
    /// <c>runLengths[1]</c> are run 1, and so on. Each run must already be sorted. Not modified.
    /// </param>
    /// <param name="runLengths">The length of each run, in order. Zero-length runs are allowed.</param>
    /// <param name="destination">
    /// Receives the merged sequence in its first <c>source.Length</c> elements; anything after
    /// that is left untouched.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">A run length is negative.</exception>
    /// <exception cref="ArgumentException">
    /// The run lengths do not sum to <c>source.Length</c>; <paramref name="destination"/> is
    /// shorter than <paramref name="source"/>; or <paramref name="destination"/> shares storage
    /// with <paramref name="source"/>.
    /// </exception>
    public static void Merge<T>(ReadOnlySpan<T> source, ReadOnlySpan<int> runLengths, Span<T> destination)
        where T : IComparable<T> =>
        Merge<T, ComparableComparer<T>>(source, runLengths, destination, default);

    /// <summary>
    /// Merges the sorted runs packed back to back in <paramref name="source"/> into one sequence in
    /// <paramref name="destination"/>, sorted under <paramref name="comparer"/>, stably.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TComparer">
    /// The order. Must be a value type implementing <see cref="IComparer{T}"/> so the JIT can
    /// devirtualize and inline every comparison.
    /// </typeparam>
    /// <param name="source">
    /// The runs, concatenated: the first <c>runLengths[0]</c> elements are run 0, the next
    /// <c>runLengths[1]</c> are run 1, and so on. Each run must already be sorted under
    /// <paramref name="comparer"/>. Not modified.
    /// </param>
    /// <param name="runLengths">The length of each run, in order. Zero-length runs are allowed.</param>
    /// <param name="destination">
    /// Receives the merged sequence in its first <c>source.Length</c> elements; anything after
    /// that is left untouched.
    /// </param>
    /// <param name="comparer">The comparer defining the order.</param>
    /// <exception cref="ArgumentOutOfRangeException">A run length is negative.</exception>
    /// <exception cref="ArgumentException">
    /// The run lengths do not sum to <c>source.Length</c>; <paramref name="destination"/> is
    /// shorter than <paramref name="source"/>; or <paramref name="destination"/> shares storage
    /// with <paramref name="source"/>.
    /// </exception>
    /// <remarks>
    /// Allocates nothing once the shared pool is warm: the tree's keys (three <typeparamref name="T"/>
    /// per run) are rented from <see cref="ArrayPool{T}.Shared"/> and cleared on return when
    /// <typeparamref name="T"/> holds references, and its run bookkeeping (five <see cref="int"/>s
    /// per run) lives on the stack for up to 128 runs and is rented past that.
    /// </remarks>
    public static void Merge<T, TComparer>(ReadOnlySpan<T> source, ReadOnlySpan<int> runLengths, Span<T> destination, TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        long total = 0;
        for (int i = 0; i < runLengths.Length; i++)
        {
            if (runLengths[i] < 0)
            {
                throw NegativeRunLength(i, runLengths[i]);
            }

            total += runLengths[i];
        }

        if (total != source.Length)
        {
            throw LengthsDoNotSum(total, source.Length);
        }

        SortingGuard.RequireLength(destination.Length, source.Length, nameof(destination));
        SortingGuard.RequireDistinctStorage(source, destination, nameof(destination));

        int k = runLengths.Length;
        if (source.IsEmpty)
        {
            return;
        }

        if (k <= 2)
        {
            // k is at least 1 here, since a non-empty source needs a run to hold it. A single run
            // is already the answer; two runs take the plain two-way merge, which spends one
            // comparison per element without any tree bookkeeping.
            int split = runLengths[0];
            MergeTwo(source[..split], source[split..], destination, comparer);
            return;
        }

        int[]? rentedInts = null;
        Span<int> ints = k <= StackRunLimit
            ? stackalloc int[5 * StackRunLimit]
            : (rentedInts = ArrayPool<int>.Shared.Rent(5 * k));
        T[] keys = ArrayPool<T>.Shared.Rent(3 * k);
        try
        {
            MergeRuns(source, runLengths, destination[..source.Length], comparer, ints[..(5 * k)], keys.AsSpan(0, 3 * k));
        }
        finally
        {
            ArrayPool<T>.Shared.Return(keys, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            if (rentedInts is not null)
            {
                ArrayPool<int>.Shared.Return(rentedInts);
            }
        }
    }

    /// <summary>
    /// Lazily merges sorted sequences into one sorted sequence, stably.
    /// </summary>
    /// <typeparam name="T">The element type, ordered by <see cref="IComparable{T}.CompareTo"/>.</typeparam>
    /// <param name="sources">
    /// The sequences to merge, each already sorted. The outer sequence is read once, when this
    /// method is called; each inner sequence is enumerated afresh every time the result is.
    /// </param>
    /// <returns>
    /// A sequence that yields the merged elements one at a time, pulling one element from a source
    /// only when the previous one from that source has been yielded.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sources"/> contains a <see langword="null"/> sequence.</exception>
    public static IEnumerable<T> Merge<T>(IEnumerable<IEnumerable<T>> sources)
        where T : IComparable<T> =>
        Merge<T, ComparableComparer<T>>(sources, default);

    /// <summary>
    /// Lazily merges sequences sorted under <paramref name="comparer"/> into one sorted sequence,
    /// stably.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TComparer">
    /// The order. Must be a value type implementing <see cref="IComparer{T}"/> so the JIT can
    /// devirtualize and inline every comparison.
    /// </typeparam>
    /// <param name="sources">
    /// The sequences to merge, each already sorted under <paramref name="comparer"/>. The outer
    /// sequence is read once, when this method is called; each inner sequence is enumerated afresh
    /// every time the result is.
    /// </param>
    /// <param name="comparer">The comparer defining the order.</param>
    /// <returns>
    /// A sequence that yields the merged elements one at a time, pulling one element from a source
    /// only when the previous one from that source has been yielded.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sources"/> contains a <see langword="null"/> sequence.</exception>
    /// <remarks>
    /// Holds one enumerator and one buffered element per source, so memory is <c>O(k)</c>
    /// whatever the sources' lengths — the shape for merging streams that never fit in memory at
    /// once. Each source enumerator is disposed as soon as that source runs out, and the rest when
    /// the merge's own enumerator is disposed, which <c>foreach</c> does on <c>break</c>. If a
    /// source, its <c>Current</c> or the comparer throws, every source is disposed before the
    /// exception propagates and the merge yields nothing more.
    /// </remarks>
    public static IEnumerable<T> Merge<T, TComparer>(IEnumerable<IEnumerable<T>> sources, TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        ArgumentNullException.ThrowIfNull(sources);

        IEnumerable<T>[] snapshot = sources.ToArray();
        for (int i = 0; i < snapshot.Length; i++)
        {
            if (snapshot[i] is null)
            {
                throw new ArgumentException($"The sequence at index {i} is null.", nameof(sources));
            }
        }

        return new SequenceMerge<T, TComparer>(snapshot, comparer);
    }

    private static void MergeTwo<T, TComparer>(ReadOnlySpan<T> left, ReadOnlySpan<T> right, Span<T> destination, TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        int i = 0, j = 0, o = 0;
        while (i < left.Length && j < right.Length)
        {
            // <= keeps it stable: on a tie the left run, which comes first, wins.
            if (comparer.Compare(left[i], right[j]) <= 0)
            {
                destination[o++] = left[i++];
            }
            else
            {
                destination[o++] = right[j++];
            }
        }

        left[i..].CopyTo(destination[o..]);
        right[j..].CopyTo(destination[(o + left.Length - i)..]);
    }

    private static void MergeRuns<T, TComparer>(
        ReadOnlySpan<T> source, ReadOnlySpan<int> runLengths, Span<T> destination, TComparer comparer,
        Span<int> ints, Span<T> keys)
        where TComparer : struct, IComparer<T>
    {
        int k = runLengths.Length;
        Span<int> position = ints[..k];
        Span<int> end = ints.Slice(k, k);
        Span<int> nodeRun = ints.Slice(2 * k, k);
        Span<int> contestantRun = ints.Slice(3 * k, 2 * k);
        Span<T> nodeKey = keys[..k];
        Span<T> contestantKey = keys.Slice(k, 2 * k);

        // Leaf k + i is run i, entered with its first element, or as spent if the run is empty.
        int offset = 0;
        for (int i = 0; i < k; i++)
        {
            position[i] = offset;
            offset += runLengths[i];
            end[i] = offset;
            if (position[i] != end[i])
            {
                contestantRun[k + i] = i;
                contestantKey[k + i] = source[position[i]];
            }
            else
            {
                contestantRun[k + i] = i + k;
            }
        }

        int winner = BuildTree(contestantRun, contestantKey, nodeRun, nodeKey, k, comparer, out T key);
        for (int o = 0; o < destination.Length; o++)
        {
            destination[o] = key;

            // The winner is live, since a spent run only wins once every run is spent and every
            // element has been written. Advance it, or retire it if that was its last element.
            int leaf = winner + k;
            int next = position[winner] + 1;
            position[winner] = next;
            if (next != end[winner])
            {
                key = source[next];
            }
            else
            {
                winner = leaf;
                key = default!;
            }

            winner = Replay(nodeRun, nodeKey, leaf >> 1, winner, ref key, k, comparer);
        }
    }

    // Plays the tournament bottom-up over contestants laid out as tree nodes: node t's children are
    // 2t and 2t + 1, run i enters as leaf k + i, and every internal node 1 .. k-1 has both children.
    // A contestant is a run index, or the index plus k once the run is spent, which one compare
    // against k tells apart without reading the key. Node t keeps the run that LOST the match played
    // there, with that run's head key, so a later replay reads one node per level and never follows
    // an index back into the input. Returns the overall winner and its key.
    private static int BuildTree<T, TComparer>(
        Span<int> contestantRun, Span<T> contestantKey, Span<int> nodeRun, Span<T> nodeKey, int k, TComparer comparer, out T key)
        where TComparer : struct, IComparer<T>
    {
        for (int t = k - 1; t >= 1; t--)
        {
            int left = 2 * t, right = left + 1;
            int winnerSide = Beats(contestantRun[left], contestantKey[left], contestantRun[right], contestantKey[right], k, comparer)
                ? left
                : right;
            int loserSide = winnerSide ^ 1;
            nodeRun[t] = contestantRun[loserSide];
            nodeKey[t] = contestantKey[loserSide];
            contestantRun[t] = contestantRun[winnerSide];
            contestantKey[t] = contestantKey[winnerSide];
        }

        // With one run there is no match to play: node 1 is its leaf.
        key = contestantKey[1];
        return contestantRun[1];
    }

    // Replays the path from node t to the root after the winner's head changed. Each node already
    // holds the loser of the match below it, so the advanced run plays that one stored loser per
    // level and the sibling subtree is never re-examined. Returns the new winner; key is updated.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Replay<T, TComparer>(Span<int> nodeRun, Span<T> nodeKey, int t, int winner, ref T key, int k, TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        for (; t > 0; t >>= 1)
        {
            int challenger = nodeRun[t];
            if (Beats(challenger, nodeKey[t], winner, key, k, comparer))
            {
                T challengerKey = nodeKey[t];
                nodeRun[t] = winner;
                nodeKey[t] = key;
                winner = challenger;
                key = challengerKey;
            }
        }

        return winner;
    }

    // Whether contestant a is ordered strictly before contestant b: a spent run (index >= k) loses
    // to every live one, and a tie goes to the lower run index — which is what makes the merge
    // stable. The spent-run tests are branches that almost never change direction.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Beats<T, TComparer>(int a, T aKey, int b, T bKey, int k, TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        if (a >= k)
        {
            return false;
        }

        if (b >= k)
        {
            return true;
        }

        int order = comparer.Compare(aKey, bKey);
        return order < 0 || (order == 0 && a < b);
    }

    // The lazy merge. Hand-written rather than a yield iterator: an iterator hoists every local into
    // a field, so its replay loop would read and write the winner and key through memory on every
    // level; here MoveNext works in locals and stores them back once per element.
    private sealed class SequenceMerge<T, TComparer>(IEnumerable<T>[] sources, TComparer comparer) : IEnumerable<T>
        where TComparer : struct, IComparer<T>
    {
        public IEnumerator<T> GetEnumerator() => new Enumerator(sources, comparer);

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class Enumerator(IEnumerable<T>[] sources, TComparer comparer) : IEnumerator<T>
        {
            private readonly IEnumerator<T>?[] enumerators = new IEnumerator<T>?[sources.Length];
            private int[] nodeRun = [];
            private T[] nodeKey = [];
            private int winner = -1;
            private T current = default!;

            public T Current => current;

            object? System.Collections.IEnumerator.Current => current;

            // Any exception — from a source, its Current, or the comparer — releases every source and
            // leaves the merge spent, the way a finally block would in an iterator. The work itself
            // is in MoveNextCore so its locals stay outside the protected region.
            public bool MoveNext()
            {
                try
                {
                    return MoveNextCore();
                }
                catch
                {
                    try
                    {
                        Dispose();
                    }
                    catch
                    {
                        // The failure that ended the merge is the one reported; a source that
                        // also fails to dispose must not replace it.
                    }

                    throw;
                }
            }

            private bool MoveNextCore()
            {
                int k = enumerators.Length;
                int run = winner;
                T key;
                IEnumerator<T>? finished = null;
                if (run < 0)
                {
                    run = Start(out key);
                }
                else if (run < k)
                {
                    IEnumerator<T> source = enumerators[run]!;
                    int leaf = run + k;
                    if (source.MoveNext())
                    {
                        key = source.Current;
                    }
                    else
                    {
                        // Released below, once the replay has succeeded: until then it stays
                        // registered, so a comparer that throws mid-replay still has it disposed.
                        finished = source;
                        run = leaf;
                        key = default!;
                    }

                    run = Replay(nodeRun.AsSpan(), nodeKey.AsSpan(), leaf >> 1, run, ref key, k, comparer);
                    if (finished is not null)
                    {
                        enumerators[leaf - k] = null;
                    }
                }
                else
                {
                    // Already exhausted: every source is spent, and stays so.
                    return false;
                }

                winner = run;
                current = key;
                finished?.Dispose();
                return run < k;
            }

            // Opens every source and plays the first tournament. With no sources the merge is
            // spent from the start, which the caller reads as winner >= k. If opening or reading a
            // source throws, MoveNext releases everything opened so far and leaves the merge spent,
            // so a retried MoveNext neither reopens the sources nor leaks the first set.
            private int Start(out T key)
            {
                int k = enumerators.Length;
                if (k == 0)
                {
                    key = default!;
                    return 0;
                }

                var contestantRun = new int[2 * k];
                var contestantKey = new T[2 * k];
                for (int i = 0; i < k; i++)
                {
                    IEnumerator<T> source = sources[i].GetEnumerator();
                    enumerators[i] = source;
                    if (source.MoveNext())
                    {
                        contestantRun[k + i] = i;
                        contestantKey[k + i] = source.Current;
                    }
                    else
                    {
                        // Empty from the start: released now rather than held to the end.
                        contestantRun[k + i] = i + k;
                        enumerators[i] = null;
                        source.Dispose();
                    }
                }

                nodeRun = new int[k];
                nodeKey = new T[k];
                return BuildTree(contestantRun.AsSpan(), contestantKey.AsSpan(), nodeRun.AsSpan(), nodeKey.AsSpan(), k, comparer, out key);
            }

            public void Reset() => throw new NotSupportedException();

            public void Dispose()
            {
                // Spent for good: a MoveNext after Dispose returns false rather than touching a
                // disposed source, and a second Dispose disposes nothing twice.
                winner = int.MaxValue;
                current = default!;
                DisposeAll(enumerators);
            }
        }
    }

    // Disposes every enumerator, still attempting the rest when one throws, and rethrows the first
    // failure once all have been tried. Each slot is cleared before its Dispose runs, so a second
    // call (or a reentrant one from inside a Dispose) finds nothing left to release and allocates
    // nothing. Iterative, so a wide merge whose sources all fail to dispose cannot run the stack out
    // the way a recursive retry would.
    private static void DisposeAll<T>(IEnumerator<T>?[] enumerators)
    {
        ExceptionDispatchInfo? first = null;
        for (int i = 0; i < enumerators.Length; i++)
        {
            IEnumerator<T>? enumerator = enumerators[i];
            enumerators[i] = null;
            try
            {
                enumerator?.Dispose();
            }
            catch (Exception ex)
            {
                first ??= ExceptionDispatchInfo.Capture(ex);
            }
        }

        first?.Throw();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ArgumentOutOfRangeException NegativeRunLength(int index, int length) =>
        new("runLengths", length, $"The run length at index {index} is negative.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ArgumentException LengthsDoNotSum(long total, int sourceLength) =>
        new($"The run lengths sum to {total}, but the source holds {sourceLength} element(s).", "runLengths");
}
