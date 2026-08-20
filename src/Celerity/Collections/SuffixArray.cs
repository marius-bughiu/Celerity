using System.Buffers;
using System.Collections;

namespace Celerity.Collections;

/// <summary>
/// A <b>suffix array</b>: a build-once, immutable index over a block of text that answers <i>where does this
/// substring occur</i> in time proportional to the <b>pattern</b> rather than to the text, plus the
/// longest-common-prefix array that makes repeated-substring questions answerable at all.
/// </summary>
/// <remarks>
/// <para>
/// .NET has no text index. <see cref="string.IndexOf(string, StringComparison)"/> and
/// <see cref="MemoryExtensions.IndexOf{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/> are vectorized <i>scans</i>:
/// every query re-reads the text, so the cost is <c>O(n)</c> per query no matter how many queries follow, and
/// counting every occurrence re-scans from each hit. <see cref="System.Text.RegularExpressions.Regex"/> is a
/// scan too, and .NET 9's <c>SearchValues&lt;string&gt;</c> indexes the <i>patterns</i>, not the text — it
/// answers "where is the first of these needles" over a haystack it has never seen before. Indexing the text
/// is the direction none of them go, and it is the one that pays off when the text is fixed and the queries
/// keep coming.
/// </para>
/// <para>
/// <b>The trade is build cost against query cost, so the crossover is the whole story.</b> Building is
/// <c>O(n log n)</c> and costs what many scans of the same text would; a single query is <c>O(m log n)</c> for
/// a pattern of length <c>m</c> — two binary searches over the suffix order — against the scan's <c>O(n)</c>.
/// One query against a text read once is <i>strictly worse</i> here than <c>IndexOf</c>, and it stays worse
/// until the query count amortizes the build. The benchmark publishes the build arm next to the query arms for
/// exactly that reason, and the ratios are in <c>docs/api/collections.md</c>.
/// </para>
/// <para>
/// <b>The realistic hand-roll is measured too.</b> A developer who has noticed the text is fixed does not
/// re-scan it — they build an inverted index of every <c>k</c>-gram into a
/// <see cref="Dictionary{TKey, TValue}"/> and look the pattern up in <c>O(1)</c>. On a pattern of exactly that
/// length, that hand-roll <b>wins</b>, and the benchmark says so. What it cannot do is answer for a pattern
/// shorter or longer than <c>k</c> without either a verification scan or a second index, and it holds a
/// dictionary entry and a substring key per position where this holds two <see cref="int"/>s. A suffix array
/// answers for <i>every</i> pattern length out of one structure.
/// </para>
/// <para>
/// <b>What only the index can do.</b> <see cref="TryGetLongestRepeatedSubstring"/> reads the longest repeat
/// off the longest-common-prefix array in one pass; there is no scan-shaped way to ask that question at all,
/// and the naive answer is quadratic. <see cref="LongestCommonPrefixes"/> is published for the same reason —
/// it is the primitive the repeat, distinct-substring-count and text-similarity questions are built from.
/// </para>
/// <para>
/// <b>Ordinal, over UTF-16 code units.</b> Suffixes are ordered by <see cref="char"/> value, which is what
/// <see cref="StringComparison.Ordinal"/> compares and what
/// <see cref="MemoryExtensions.SequenceCompareTo{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/> does. There is no
/// culture-aware, case-insensitive or normalizing mode and there will not be: a linguistic comparison is not a
/// total order over fixed-length units, so the suffixes could not be sorted once and binary-searched. Fold the
/// text and the pattern the same way before indexing when case-insensitive matching is wanted. A surrogate
/// pair sorts as its two code units, so a match can begin at a low surrogate — check the boundary in the
/// caller's own text when that matters.
/// </para>
/// <para>
/// <b>Footprint.</b> The text is copied (a span source may be transient), and the suffix and
/// longest-common-prefix arrays are one <see cref="int"/> per position each: about <b>10 bytes per
/// character</b> in total, against the 2 the text alone costs. That is what the index <i>retains</i>; the
/// build's scratch is rented from <see cref="ArrayPool{T}"/> and returned, so it is transient rather than
/// free. The footprint is the other half of the reason to check the crossover before reaching for it.
/// </para>
/// <para>
/// <b>Build-once.</b> The index is immutable; changing the text means building a new one, as with
/// <see cref="KdTree{TValue}"/>, <see cref="RTree{TValue}"/>, <see cref="IntervalTree{TKey, TValue}"/> and
/// <see cref="CompressedGraph"/>. Nothing mutates, so enumeration is never invalidated and concurrent readers
/// need no synchronization.
/// </para>
/// <para>
/// <b>The empty pattern matches at every start position.</b> It occurs <see cref="Length"/> times, so
/// <see cref="IndexOf"/> returns <c>0</c> and <see cref="Contains"/> returns <c>true</c> on a non-empty text,
/// and over an <i>empty</i> text it occurs nowhere: <see cref="CountOccurrences"/> is <c>0</c>,
/// <see cref="Contains"/> is <c>false</c> and <see cref="IndexOf"/> is <c>-1</c>. That is one rule with no
/// special case in the code, rather than <see cref="string"/>'s, which reports the empty needle as found at
/// <c>0</c> even in the empty string.
/// </para>
/// <example>
/// <code>
/// var index = new SuffixArray("the cat sat on the mat");
///
/// Console.WriteLine(index.CountOccurrences("at"));   // 3 — one binary search pair, no scan
/// Console.WriteLine(index.IndexOf("the"));           // 0
/// Console.WriteLine(index.Contains("dog"));          // False
///
/// foreach (int position in index.GetOccurrences("at"))
///     Console.WriteLine(position);                   // 5, 9, 20
///
/// if (index.TryGetLongestRepeatedSubstring(out int start, out int length))
///     Console.WriteLine(index.Text.Slice(start, length).ToString());   // "the "
/// </code>
/// </example>
/// </remarks>
public sealed class SuffixArray : IReadOnlyList<int>
{
    private readonly char[] _text;

    // _suffixes holds every start position [0, Length) ordered by the suffix beginning there, and _lcp[i] is
    // the number of leading characters suffix _suffixes[i] shares with _suffixes[i - 1] (_lcp[0] is 0). Both
    // are exactly Length long, so neither the text length nor the suffix count needs a field of its own.
    private readonly int[] _suffixes;
    private readonly int[] _lcp;

    /// <summary>Builds an index over <paramref name="text"/>.</summary>
    /// <param name="text">The text to index. It is copied, so the caller's buffer may be reused or freed.</param>
    /// <remarks>
    /// <para>
    /// Building is <c>O(n log n)</c>: the suffixes are ordered by prefix doubling — rank every position by its
    /// first character, then repeatedly sort by a pair of ranks that already covers twice the length, which is
    /// a counting sort per round because the ranks are dense. The longest-common-prefix array follows in
    /// <c>O(n)</c> by Kasai's algorithm. What the index <i>retains</i> is the text copy and the two result
    /// arrays and nothing else: every scratch buffer the build needs is rented from
    /// <see cref="ArrayPool{T}"/> and returned. That is not the same as allocating nothing —
    /// <see cref="ArrayPool{T}.Shared"/> allocates when it has no suitable buffer, so a first or contended
    /// build still allocates its scratch.
    /// </para>
    /// <para>
    /// A <c>null</c> <see cref="string"/> converts to an empty span rather than throwing, so it builds an empty
    /// index. There is no <see cref="ArgumentNullException"/> to raise: the parameter is a span.
    /// </para>
    /// </remarks>
    public SuffixArray(ReadOnlySpan<char> text)
    {
        _text = text.ToArray();
        _suffixes = BuildSuffixes(text);
        _lcp = BuildLongestCommonPrefixes(text, _suffixes);
    }

    /// <summary>Gets the number of characters in the indexed text, which is also the number of suffixes.</summary>
    public int Length => _text.Length;

    /// <summary>Gets the indexed text.</summary>
    /// <remarks>The span is over the index's own copy; slicing it is how a match is turned back into text.</remarks>
    public ReadOnlySpan<char> Text => _text;

    /// <summary>Gets every suffix start position, ordered by the suffix beginning there.</summary>
    /// <remarks>
    /// This is the index itself. <c>Suffixes[0]</c> is the start of the lexicographically smallest suffix.
    /// </remarks>
    public ReadOnlySpan<int> Suffixes => _suffixes;

    /// <summary>
    /// Gets the longest-common-prefix array: entry <c>i</c> is the number of leading characters the suffix at
    /// rank <c>i</c> shares with the one at rank <c>i - 1</c>, and entry <c>0</c> is <c>0</c>.
    /// </summary>
    /// <remarks>
    /// Adjacent suffixes in the order are the most similar pair, so the largest entry is the longest substring
    /// that occurs at least twice — which is what <see cref="TryGetLongestRepeatedSubstring"/> reads off it.
    /// </remarks>
    public ReadOnlySpan<int> LongestCommonPrefixes => _lcp;

    /// <summary>Gets the start position of the suffix at lexicographic rank <paramref name="rank"/>.</summary>
    /// <param name="rank">The rank, over <c>[0, Length)</c>.</param>
    /// <returns>The position in <see cref="Text"/> where that suffix begins.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rank"/> is outside <c>[0, Length)</c>.</exception>
    public int this[int rank]
    {
        get
        {
            if ((uint)rank >= (uint)_suffixes.Length)
                throw new ArgumentOutOfRangeException(nameof(rank), rank, "Rank must be within [0, Length).");

            return _suffixes[rank];
        }
    }

    int IReadOnlyCollection<int>.Count => _suffixes.Length;

    /// <summary>Reports whether <paramref name="pattern"/> occurs anywhere in the text.</summary>
    /// <param name="pattern">The substring to look for.</param>
    /// <returns><c>true</c> when it occurs at least once.</returns>
    /// <remarks>
    /// <c>O(m log n)</c> for a pattern of length <c>m</c>: one binary search, which is why this is cheaper than
    /// <see cref="CountOccurrences"/> rather than a wrapper over it. The strongest case against a scan is a
    /// pattern that is <i>absent</i> — the scan has to read the whole text to find that out.
    /// </remarks>
    public bool Contains(ReadOnlySpan<char> pattern)
    {
        int rank = LowerBound(pattern);
        return rank < _suffixes.Length && CompareSuffix(rank, pattern) == 0;
    }

    /// <summary>Counts the occurrences of <paramref name="pattern"/> in the text.</summary>
    /// <param name="pattern">The substring to look for.</param>
    /// <returns>The number of positions where it occurs; <c>0</c> when it does not.</returns>
    /// <remarks>
    /// <c>O(m log n)</c>: the matching suffixes are contiguous in the order, so the count is the width of that
    /// range and nothing is enumerated. A scan pays <c>O(n)</c> and re-scans from every hit.
    /// </remarks>
    public int CountOccurrences(ReadOnlySpan<char> pattern) => UpperBound(pattern) - LowerBound(pattern);

    /// <summary>Returns the position of the <b>first</b> occurrence of <paramref name="pattern"/>.</summary>
    /// <param name="pattern">The substring to look for.</param>
    /// <returns>The lowest position where it occurs, or <c>-1</c> when it does not occur.</returns>
    /// <remarks>
    /// <c>O(m log n + k)</c> for <c>k</c> occurrences: the suffix order groups the matches but does not order
    /// them by position, so the smallest is found by reading the range. When any occurrence will do, or only
    /// their number is wanted, <see cref="Contains"/> and <see cref="CountOccurrences"/> skip that pass.
    /// </remarks>
    public int IndexOf(ReadOnlySpan<char> pattern)
    {
        int low = LowerBound(pattern);
        int high = UpperBound(pattern);

        int first = -1;
        for (int rank = low; rank < high; rank++)
        {
            if (first < 0 || _suffixes[rank] < first)
                first = _suffixes[rank];
        }

        return first;
    }

    /// <summary>
    /// Exposes the occurrences of <paramref name="pattern"/> as a slice of the index itself, copying nothing.
    /// </summary>
    /// <param name="pattern">The substring to look for.</param>
    /// <param name="occurrences">
    /// The matching start positions, in <b>lexicographic</b> rather than positional order; empty when the
    /// pattern does not occur.
    /// </param>
    /// <returns><c>true</c> when the pattern occurs at least once.</returns>
    /// <remarks>
    /// This is the allocation-free tier and the fastest way to reach every match: the matching suffixes are
    /// contiguous in <see cref="Suffixes"/>, so the result is a slice of it. Use
    /// <see cref="CopyOccurrences"/> when the positions are needed in ascending order.
    /// </remarks>
    public bool TryGetOccurrences(ReadOnlySpan<char> pattern, out ReadOnlySpan<int> occurrences)
    {
        int low = LowerBound(pattern);
        int high = UpperBound(pattern);

        occurrences = _suffixes.AsSpan(low, high - low);
        return high > low;
    }

    /// <summary>
    /// Writes the ascending positions where <paramref name="pattern"/> occurs into
    /// <paramref name="destination"/>, allocating nothing.
    /// </summary>
    /// <param name="pattern">The substring to look for.</param>
    /// <param name="destination">The buffer to fill.</param>
    /// <param name="destinationIndex">The position in <paramref name="destination"/> to start writing at.</param>
    /// <returns>The number of positions written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="destinationIndex"/> is outside <c>[0, destination.Length]</c>.</exception>
    /// <remarks>
    /// Writing stops when the buffer is full, so a return value equal to the remaining room may mean the
    /// matches were truncated — size the buffer with <see cref="CountOccurrences"/> when every match is needed.
    /// Sorting the written positions costs <c>O(k log k)</c> on top of the search;
    /// <see cref="TryGetOccurrences"/> skips it when the order does not matter.
    /// </remarks>
    public int CopyOccurrences(ReadOnlySpan<char> pattern, int[] destination, int destinationIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if ((uint)destinationIndex > (uint)destination.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(destinationIndex), destinationIndex, "Destination index must be within [0, destination.Length].");
        }

        TryGetOccurrences(pattern, out ReadOnlySpan<int> occurrences);

        int written = Math.Min(occurrences.Length, destination.Length - destinationIndex);
        Span<int> target = destination.AsSpan(destinationIndex, written);
        occurrences[..written].CopyTo(target);
        target.Sort();
        return written;
    }

    /// <summary>Returns the ascending positions where <paramref name="pattern"/> occurs.</summary>
    /// <param name="pattern">The substring to look for.</param>
    /// <returns>The matching positions in ascending order, or an empty array when the pattern does not occur.</returns>
    /// <remarks>
    /// This is the convenience tier: it allocates the result. Use <see cref="TryGetOccurrences"/> or
    /// <see cref="CopyOccurrences"/> on a hot path.
    /// </remarks>
    public int[] GetOccurrences(ReadOnlySpan<char> pattern)
    {
        if (!TryGetOccurrences(pattern, out ReadOnlySpan<int> occurrences))
            return Array.Empty<int>();

        int[] result = occurrences.ToArray();
        result.AsSpan().Sort();
        return result;
    }

    /// <summary>Finds the longest substring that occurs at least twice in the text.</summary>
    /// <param name="start">The start position of one occurrence of that substring.</param>
    /// <param name="length">Its length.</param>
    /// <returns><c>true</c> when some substring occurs at least twice; <c>false</c> when no character repeats.</returns>
    /// <remarks>
    /// <c>O(n)</c>, over the longest-common-prefix array — the longest repeat is its largest entry, because the
    /// two suffixes sharing the longest prefix are necessarily adjacent in the order. The naive answer is
    /// quadratic, and no scan-shaped API can express the question at all. Occurrences may overlap
    /// (<c>"aaa"</c> reports <c>"aa"</c>); when several substrings tie at the longest length the one reported
    /// is the lexicographically smallest of them, and <paramref name="start"/> is one of its occurrences
    /// rather than necessarily the first.
    /// </remarks>
    public bool TryGetLongestRepeatedSubstring(out int start, out int length)
    {
        int bestRank = -1;
        int best = 0;
        for (int rank = 1; rank < _lcp.Length; rank++)
        {
            if (_lcp[rank] > best)
            {
                best = _lcp[rank];
                bestRank = rank;
            }
        }

        if (bestRank < 0)
        {
            start = 0;
            length = 0;
            return false;
        }

        start = _suffixes[bestRank];
        length = best;
        return true;
    }

    /// <summary>Returns an enumerator over every suffix start position in lexicographic order.</summary>
    /// <returns>A struct enumerator over the start positions.</returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // Orders the suffix at `rank` against the pattern, comparing at most pattern.Length characters: a suffix
    // that starts with the pattern compares equal however long it goes on, which is what makes the matching
    // suffixes one contiguous range. A suffix shorter than the pattern is compared in full and so sorts below
    // it, exactly where a suffix that cannot contain it belongs.
    private int CompareSuffix(int rank, ReadOnlySpan<char> pattern)
    {
        ReadOnlySpan<char> suffix = _text.AsSpan(_suffixes[rank]);
        if (suffix.Length > pattern.Length)
            suffix = suffix[..pattern.Length];

        return suffix.SequenceCompareTo(pattern);
    }

    // First rank whose suffix does not sort below the pattern; Length when every suffix does.
    private int LowerBound(ReadOnlySpan<char> pattern)
    {
        int low = 0;
        int high = _suffixes.Length;
        while (low < high)
        {
            int mid = (int)(((uint)low + (uint)high) >> 1);
            if (CompareSuffix(mid, pattern) < 0)
                low = mid + 1;
            else
                high = mid;
        }

        return low;
    }

    // First rank whose suffix sorts above the pattern; Length when none does.
    private int UpperBound(ReadOnlySpan<char> pattern)
    {
        int low = 0;
        int high = _suffixes.Length;
        while (low < high)
        {
            int mid = (int)(((uint)low + (uint)high) >> 1);
            if (CompareSuffix(mid, pattern) <= 0)
                low = mid + 1;
            else
                high = mid;
        }

        return low;
    }

    // Prefix doubling over the cyclic shifts of the text with a sentinel appended. The sentinel is smaller than
    // every character and occurs once, so no shift wraps past it into a tie and the cyclic order restricted to
    // the real positions *is* the suffix order — which is what lets each round reuse the previous round's ranks
    // for both halves of the pair instead of comparing characters again.
    private static int[] BuildSuffixes(ReadOnlySpan<char> text)
    {
        int shifts = text.Length + 1;

        int[] orderBuffer = ArrayPool<int>.Shared.Rent(shifts);
        int[] classBuffer = ArrayPool<int>.Shared.Rent(shifts);
        int[] nextClassBuffer = ArrayPool<int>.Shared.Rent(shifts);
        int[] rotatedBuffer = ArrayPool<int>.Shared.Rent(shifts);
        int[] bucketBuffer = ArrayPool<int>.Shared.Rent(shifts);
        try
        {
            Span<int> order = orderBuffer.AsSpan(0, shifts);
            Span<int> classes = classBuffer.AsSpan(0, shifts);
            Span<int> nextClasses = nextClassBuffer.AsSpan(0, shifts);
            Span<int> rotated = rotatedBuffer.AsSpan(0, shifts);
            Span<int> buckets = bucketBuffer.AsSpan(0, shifts);

            int classCount = RankBySymbol(text, order, classes, rotated);

            // Each round doubles the prefix length the ranks distinguish, so it takes log n of them; the loop
            // ends the moment every shift has its own rank, which is the point where the order is total.
            for (int width = 1; classCount < shifts; width <<= 1)
            {
                // Ordering by the second half of each pair is free: subtracting the width from an already
                // ordered sequence of starts produces exactly it, so the round only has to sort by the first
                // half, stably. That is the whole trick, and it is why a counting sort suffices.
                for (int i = 0; i < shifts; i++)
                {
                    int start = order[i] - width;
                    rotated[i] = start < 0 ? start + shifts : start;
                }

                buckets[..classCount].Clear();
                for (int i = 0; i < shifts; i++)
                    buckets[classes[rotated[i]]]++;
                for (int i = 1; i < classCount; i++)
                    buckets[i] += buckets[i - 1];
                for (int i = shifts - 1; i >= 0; i--)
                    order[--buckets[classes[rotated[i]]]] = rotated[i];

                nextClasses[order[0]] = 0;
                int reclassified = 1;
                for (int i = 1; i < shifts; i++)
                {
                    int currentSecond = classes[Wrap(order[i] + width, shifts)];
                    int previousSecond = classes[Wrap(order[i - 1] + width, shifts)];
                    if (classes[order[i]] != classes[order[i - 1]] || currentSecond != previousSecond)
                        reclassified++;

                    nextClasses[order[i]] = reclassified - 1;
                }

                // Swapped rather than copied: the round only ever reads the old ranks and writes the new, so
                // last round's buffer is exactly the scratch this one needs. A tuple swap cannot express it —
                // a Span<T> is a ref struct and so cannot be a tuple element.
                Span<int> previousClasses = classes;
                classes = nextClasses;
                nextClasses = previousClasses;
                classCount = reclassified;
            }

            // order[0] is the sentinel shift, which sorts first because the sentinel does; the rest are the
            // real suffixes in order.
            return order[1..].ToArray();
        }
        finally
        {
            ArrayPool<int>.Shared.Return(orderBuffer);
            ArrayPool<int>.Shared.Return(classBuffer);
            ArrayPool<int>.Shared.Return(nextClassBuffer);
            ArrayPool<int>.Shared.Return(rotatedBuffer);
            ArrayPool<int>.Shared.Return(bucketBuffer);
        }
    }

    // The first round, which has no previous ranks to reuse: order the shifts by their opening symbol, where a
    // character maps to its value plus one and the sentinel to zero. Sorting rather than counting into a
    // 65,537-entry table keeps the scratch proportional to the text instead of to the UTF-16 code unit range,
    // which is what a short text would otherwise be paying for.
    private static int RankBySymbol(ReadOnlySpan<char> text, Span<int> order, Span<int> classes, Span<int> symbols)
    {
        for (int i = 0; i < order.Length; i++)
        {
            symbols[i] = i < text.Length ? text[i] + 1 : 0;
            order[i] = i;
        }

        symbols.Sort(order);

        classes[order[0]] = 0;
        int classCount = 1;
        for (int i = 1; i < order.Length; i++)
        {
            if (symbols[i] != symbols[i - 1])
                classCount++;

            classes[order[i]] = classCount - 1;
        }

        return classCount;
    }

    // Kasai's algorithm. Walking the text in position order rather than rank order is what makes it linear:
    // dropping the first character of a suffix costs its predecessor's match at most one, so the comparison
    // length only ever falls by one per position and the total work is bounded by the text length.
    private static int[] BuildLongestCommonPrefixes(ReadOnlySpan<char> text, int[] suffixes)
    {
        int length = suffixes.Length;
        int[] lcp = new int[length];

        int[] rankBuffer = ArrayPool<int>.Shared.Rent(length + 1);
        try
        {
            Span<int> ranks = rankBuffer.AsSpan(0, length);
            for (int rank = 0; rank < length; rank++)
                ranks[suffixes[rank]] = rank;

            int matched = 0;
            for (int position = 0; position < length; position++)
            {
                if (ranks[position] == 0)
                {
                    matched = 0;
                    continue;
                }

                int previous = suffixes[ranks[position] - 1];
                while (position + matched < length && previous + matched < length &&
                       text[position + matched] == text[previous + matched])
                {
                    matched++;
                }

                lcp[ranks[position]] = matched;
                if (matched > 0)
                    matched--;
            }

            return lcp;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(rankBuffer);
        }
    }

    private static int Wrap(int position, int shifts) => position >= shifts ? position - shifts : position;

    /// <summary>Enumerates the suffix start positions of a <see cref="SuffixArray"/> in lexicographic order.</summary>
    /// <remarks>
    /// The index is immutable, so there is no version check here and no way for an enumerator to be invalidated.
    /// </remarks>
    public struct Enumerator : IEnumerator<int>
    {
        private readonly int[] _suffixes;
        private int _index;
        private int _current;

        internal Enumerator(SuffixArray index)
        {
            _suffixes = index._suffixes;
            _index = 0;
            _current = 0;
        }

        /// <summary>Gets the start position at the current lexicographic rank.</summary>
        public readonly int Current => _current;

        readonly object IEnumerator.Current => _current;

        /// <summary>Advances to the next rank.</summary>
        /// <returns><c>true</c> when a position was produced; <c>false</c> at the end of the index.</returns>
        public bool MoveNext()
        {
            if (_index >= _suffixes.Length)
                return false;

            _current = _suffixes[_index++];
            return true;
        }

        /// <summary>Resets the enumerator to before the first rank.</summary>
        public void Reset()
        {
            _index = 0;
            _current = 0;
        }

        /// <summary>Does nothing; the enumerator holds no unmanaged resource.</summary>
        public readonly void Dispose()
        {
        }
    }
}
