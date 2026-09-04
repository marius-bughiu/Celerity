using System.Buffers;
using System.Collections;
using System.Numerics;

namespace Celerity.Collections;

/// <summary>
/// An <b>immutable</b> succinct index over a fixed sequence of <see cref="int"/> values that answers the
/// questions a range fold cannot: <see cref="Quantile"/> — the <c>k</c>-th smallest value inside a positional
/// window — <see cref="RangeCount"/> — how many values inside a window fall in a band — and
/// <see cref="Rank(int, int)"/> for a single value. Each of those is <c>O(log sigma)</c> for an alphabet of
/// <c>sigma</c> distinct values, independent of how wide the window is — <see cref="Quantile"/> and
/// <see cref="Rank(int, int)"/> as a single descent, <see cref="RangeCount"/> as two, one per band edge.
/// <see cref="Select(int, int)"/> is the one query outside that bound: it binary-searches
/// <see cref="Rank(int, int)"/> and so costs <c>O(log n * log sigma)</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Build once, query many.</b> The sequence is a snapshot taken at construction and there is no mutating
/// member: a caller that changes an element must build a new <see cref="WaveletTree"/>, which costs
/// <c>O(Length * log Length)</c> — the sort that fixes the coordinate compression dominates the
/// <c>O(Length * log sigma)</c> of building the levels, whatever the alphabet. That makes this the wrong type
/// for a sequence that is still being written — a live metrics buffer, a ring of the last <c>n</c> samples —
/// where a rebuild per update is strictly worse than the scan it replaces. Collect into an array (or a
/// <see cref="Deque{T}"/>) there and build the index once the window has settled.
/// </para>
/// <para>
/// The BCL has no counterpart, and neither does the rest of this library. <see cref="SegmentTree{T, TMonoid}"/>
/// folds a range down to a single value, so it can answer the <i>smallest</i> value in a window and
/// structurally not the <c>k</c>-th smallest — a monoid fold has no way to keep the <c>k - 1</c> values it
/// discarded. <see cref="RankedSet{T, TComparer}"/> ranks and selects over a whole <i>set</i>, so it has no
/// positional window and cannot hold the duplicates a log of measurements is made of. The honest baseline is
/// therefore what a caller writes by hand: <see cref="Array.Sort{T}(T[])"/> over a copy of the window for a
/// quantile, and a counting <c>for</c> loop for a band count. Both are linear (or worse) in the window length,
/// which is the term this type removes.
/// </para>
/// <para>
/// <b>Layout.</b> The values are coordinate-compressed at construction into codes <c>[0, AlphabetSize)</c>, so
/// the level count is <c>ceil(log2(AlphabetSize))</c> rather than 32: a thousand distinct latencies cost ten
/// levels whatever the magnitude of the values. Each level is one <see cref="RankSelectBitVector"/> holding one
/// bit of the code per position, and the sequence is stably partitioned by that bit before the next level is
/// built — the wavelet <i>matrix</i> arrangement, which keeps every level contiguous and needs a single
/// zero-count per level instead of a node object per subtree. Every query is then a descent that maps a
/// position (or a half-open interval) from one level to the next with a rank, which is why the cost is set by
/// the alphabet and not by the data.
/// </para>
/// <para>
/// <b>Space.</b> One bit per element per level, plus each level's rank index at its documented 25% —
/// <c>Length * ceil(log2(AlphabetSize))</c> bits times 1.25, plus the distinct-value table. For a million
/// values over a thousand-symbol alphabet that is about 1.5&#160;MB against the 4&#160;MB of the
/// <see cref="int"/> array it indexes. <see cref="IndexSizeInBytes"/> reports the exact figure for a given
/// instance.
/// </para>
/// <para>
/// The type holds no mutable state after construction, so instances are safe to share across threads.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // A million request latencies in arrival order.
/// var index = new WaveletTree(latencies);
///
/// // The median latency of requests 400,000 through 500,000 — no sort, no copy.
/// int median = index.Quantile(400_000, 100_000, 50_000);
///
/// // How many of those requests breached the 250 ms SLO.
/// int breaches = index.RangeCount(400_000, 100_000, 251, int.MaxValue);
/// </code>
/// </example>
public sealed class WaveletTree : IReadOnlyList<int>
{
    // _symbols holds the distinct values in ascending order, so a code is an index into it and the mapping
    // both ways is a lookup or a binary search. _levels[0] carries the most significant bit of the code and
    // _levels[^1] the least; _zeros[l] is how many positions carry a clear bit at level l, which is where the
    // ones half of that level starts once the level below has been stably partitioned.
    private readonly int[] _symbols;
    private readonly RankSelectBitVector[] _levels;
    private readonly int[] _zeros;
    private readonly int _length;

    /// <summary>
    /// Builds an index over <paramref name="values"/>.
    /// </summary>
    /// <param name="values">
    /// The sequence to index, in its own order. It is copied, so the caller's buffer may be reused or freed.
    /// An <see cref="int"/> array converts implicitly; a <see cref="List{T}"/> reaches it through
    /// <see cref="System.Runtime.InteropServices.CollectionsMarshal.AsSpan{T}(List{T})"/> without a copy, and
    /// any other sequence through <c>ToArray()</c>. There is deliberately no <see cref="IEnumerable{T}"/>
    /// overload: an <c>int[]</c> argument converts to both, which is an ambiguity at every call site.
    /// </param>
    /// <remarks>
    /// Building is <c>O(n log n)</c>. The coordinate compression sorts a copy of the whole input and
    /// deduplicates it in place, which is the <c>O(n log n)</c> term and dominates whatever the alphabet is —
    /// a million copies of one value still pay for the sort. Each of the <c>ceil(log2(sigma))</c> levels then
    /// writes one bit per position and stably partitions the codes for the level below, which is the
    /// <c>O(n log sigma)</c> term.
    /// <para>
    /// The <b>level-building</b> buffers — the two code arrays and the bit buffer — are rented from
    /// <see cref="ArrayPool{T}"/> and returned. The coordinate compression is not pooled: it allocates the
    /// <c>n</c>-element array it sorts, and when the input holds duplicates it allocates the compact symbol
    /// table too and leaves the sorted array to the collector. Pooling is also not the same as allocating
    /// nothing — <see cref="ArrayPool{T}.Shared"/> allocates when it has no suitable buffer, so a first or
    /// contended build still allocates its scratch.
    /// </para>
    /// </remarks>
    public WaveletTree(ReadOnlySpan<int> values)
    {
        _length = values.Length;
        _symbols = DistinctAscending(values);

        int levelCount = BitsForAlphabet(_symbols.Length);
        _levels = new RankSelectBitVector[levelCount];
        _zeros = new int[levelCount];

        if (levelCount == 0)
            return;

        int[] current = ArrayPool<int>.Shared.Rent(values.Length);
        int[] scratch = ArrayPool<int>.Shared.Rent(values.Length);
        ulong[] words = ArrayPool<ulong>.Shared.Rent(WordCount(values.Length));

        try
        {
            for (int i = 0; i < values.Length; i++)
                current[i] = Encode(values[i]);

            for (int level = 0; level < levelCount; level++)
            {
                int bit = levelCount - 1 - level;
                BuildLevel(current, scratch, words, values.Length, bit, level);

                // The stable partition wrote the next level's codes into `scratch`; swap rather than copy.
                (current, scratch) = (scratch, current);
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(current);
            ArrayPool<int>.Shared.Return(scratch);
            ArrayPool<ulong>.Shared.Return(words);
        }
    }

    /// <summary>Gets the number of values in the indexed sequence.</summary>
    public int Length => _length;

    /// <summary>
    /// Gets the number of <i>distinct</i> values in the sequence — the alphabet size the level count and every
    /// query cost are set by.
    /// </summary>
    public int AlphabetSize => _symbols.Length;

    /// <summary>
    /// Gets the number of levels, <c>ceil(log2(AlphabetSize))</c> — the depth of a single descent, and so the
    /// unit every query cost is counted in: one descent for <see cref="Quantile"/> and
    /// <see cref="Rank(int, int)"/>, two for <see cref="RangeCount"/>, and one per binary-search step for
    /// <see cref="Select"/>. A sequence of at most one distinct value needs no levels and reports <c>0</c>.
    /// </summary>
    public int LevelCount => _levels.Length;

    /// <summary>
    /// Gets the distinct values, in ascending order. A code used internally is an index into this span, which
    /// is what makes the cost depend on how many distinct values there are rather than on how large they are.
    /// </summary>
    public ReadOnlySpan<int> Symbols => _symbols;

    /// <summary>
    /// Gets the size in bytes of the index payload: the level bit vectors, their rank/select indexes, the
    /// per-level zero counts, and the symbol table. Compare it against <c>Length * sizeof(int)</c>, the array
    /// it replaces.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is payload, not retained-object size: it excludes the CLR's per-array header and the
    /// <see cref="RankSelectBitVector"/> references themselves, the same accounting
    /// <see cref="RankSelectBitVector.IndexSizeInBytes"/> uses. Those are a fixed few tens of bytes per level
    /// and do not scale with the data.
    /// </para>
    /// <para>
    /// The figure is a <see cref="long"/>, as <see cref="CompressedIntSet.MemoryUsageInBytes"/> is, because an
    /// <see cref="int"/> cannot hold it: a wide enough alphabet needs a symbol table and enough levels to pass
    /// <see cref="int.MaxValue"/> bytes long before any single array does.
    /// </para>
    /// </remarks>
    public long IndexSizeInBytes
    {
        get
        {
            long total = (long)(_symbols.Length + _zeros.Length) * sizeof(int);
            if (_levels.Length == 0)
                return total;

            long bytesPerLevel = (long)WordCount(_length) * sizeof(ulong);
            for (int level = 0; level < _levels.Length; level++)
                total += bytesPerLevel + _levels[level].IndexSizeInBytes;

            return total;
        }
    }

    /// <summary>Gets the value at the specified position.</summary>
    /// <param name="index">The zero-based position, over <c>[0, Length)</c>.</param>
    /// <returns>The value stored there.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside <c>[0, Length)</c>.</exception>
    /// <remarks>
    /// <c>O(log sigma)</c> — one rank per level — rather than the array's <c>O(1)</c>. A caller that reads
    /// positions far more often than it queries ranges should keep the array alongside the index.
    /// </remarks>
    public int this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be within [0, Length).");

            int code = 0;
            int position = index;
            for (int level = 0; level < _levels.Length; level++)
            {
                RankSelectBitVector bits = _levels[level];

                // One rank serves both branches: the ones half starts at _zeros[level] and is indexed by the
                // set bits below, the zeros half by the clear ones, which is the complement.
                int ones = bits.Rank(position);
                if (bits.Get(position))
                {
                    position = _zeros[level] + ones;
                    code = (code << 1) | 1;
                }
                else
                {
                    position -= ones;
                    code <<= 1;
                }
            }

            return _symbols[code];
        }
    }

    int IReadOnlyCollection<int>.Count => _length;

    /// <summary>
    /// Returns how many times <paramref name="value"/> occurs at positions strictly below
    /// <paramref name="index"/>.
    /// </summary>
    /// <param name="index">
    /// The exclusive upper bound, from <c>0</c> to <see cref="Length"/> inclusive. <c>Rank(0, v)</c> is
    /// <c>0</c> and <c>Rank(Length, v)</c> is the total number of occurrences of <c>v</c>.
    /// </param>
    /// <param name="value">The value to count. A value absent from the sequence yields <c>0</c>.</param>
    /// <returns>The number of occurrences of <paramref name="value"/> below <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or greater than <see cref="Length"/>.
    /// </exception>
    /// <remarks><c>O(log sigma)</c>: two ranks per level, against the counting loop's <c>O(index)</c>.</remarks>
    public int Rank(int index, int value)
    {
        if ((uint)index > (uint)_length)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be within [0, Length].");

        return RangeRankCore(0, index, value);
    }

    /// <summary>
    /// Returns how many times <paramref name="value"/> occurs inside the window
    /// <c>[start, start + length)</c>.
    /// </summary>
    /// <param name="start">The zero-based start of the window, over <c>[0, Length]</c>.</param>
    /// <param name="length">The window length. Must not run past <see cref="Length"/>.</param>
    /// <param name="value">The value to count. A value absent from the sequence yields <c>0</c>.</param>
    /// <returns>The number of occurrences of <paramref name="value"/> inside the window.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="start"/> or <paramref name="length"/> is negative, or the window runs past
    /// <see cref="Length"/>.
    /// </exception>
    public int RangeRank(int start, int length, int value)
    {
        ValidateWindow(start, length);
        return RangeRankCore(start, start + length, value);
    }

    /// <summary>
    /// Returns the position of the <paramref name="rank"/>-th occurrence of <paramref name="value"/>, counting
    /// from zero.
    /// </summary>
    /// <param name="rank">The zero-based ordinal of the occurrence.</param>
    /// <param name="value">The value to locate.</param>
    /// <returns>
    /// The position <c>p</c> such that <c>this[p] == value</c> and <c>Rank(p, value) == rank</c>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="rank"/> is negative, or <paramref name="value"/> does not occur that many times. Use
    /// <see cref="TrySelect"/> to probe without an exception.
    /// </exception>
    /// <remarks>
    /// <c>O(log n * log sigma)</c>: a binary search over positions whose predicate is <see cref="Rank(int, int)"/>.
    /// That is the one query here the wavelet matrix does not answer in a single descent — the upward walk that
    /// would make it <c>O(log sigma)</c> needs a select over the <i>clear</i> bits of a level, which
    /// <see cref="RankSelectBitVector"/> deliberately does not carry an index for.
    /// </remarks>
    public int Select(int rank, int value)
    {
        if (!TrySelect(rank, value, out int position))
        {
            throw new ArgumentOutOfRangeException(
                nameof(rank),
                rank,
                "Rank must be within [0, n) for the number of occurrences n of the value.");
        }

        return position;
    }

    /// <summary>
    /// Attempts to locate the <paramref name="rank"/>-th occurrence of <paramref name="value"/>, counting from
    /// zero.
    /// </summary>
    /// <param name="rank">The zero-based ordinal of the occurrence.</param>
    /// <param name="value">The value to locate.</param>
    /// <param name="position">
    /// When this method returns <c>true</c>, the position of that occurrence; otherwise <c>-1</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> when <paramref name="value"/> occurs more than <paramref name="rank"/> times; otherwise
    /// <c>false</c>.
    /// </returns>
    public bool TrySelect(int rank, int value, out int position)
    {
        position = -1;
        if (rank < 0 || rank >= RangeRankCore(0, _length, value))
            return false;

        // The smallest position p with Rank(p + 1, value) == rank + 1 is the occurrence itself, and Rank is
        // non-decreasing in p, so a binary search over [0, Length) lands on it.
        int lo = 0;
        int hi = _length - 1;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (RangeRankCore(0, mid + 1, value) > rank)
                hi = mid;
            else
                lo = mid + 1;
        }

        position = lo;
        return true;
    }

    /// <summary>
    /// Returns the <paramref name="k"/>-th smallest value inside the window <c>[start, start + length)</c>,
    /// counting from zero — <c>Quantile(s, n, 0)</c> is the minimum and <c>Quantile(s, n, n - 1)</c> the
    /// maximum.
    /// </summary>
    /// <param name="start">The zero-based start of the window, over <c>[0, Length]</c>.</param>
    /// <param name="length">The window length. Must be positive and must not run past <see cref="Length"/>.</param>
    /// <param name="k">The zero-based order statistic, over <c>[0, length)</c>.</param>
    /// <returns>The <paramref name="k"/>-th smallest value in the window, counting duplicates.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The window is invalid or empty, or <paramref name="k"/> is outside <c>[0, length)</c>.
    /// </exception>
    /// <remarks>
    /// <c>O(log sigma)</c>, independent of the window length — against the <c>O(m log m)</c> of copying the
    /// window and sorting it, which is what a caller writes without this type. Duplicates count: the median of
    /// <c>[5, 5, 5]</c> is <c>5</c>.
    /// </remarks>
    public int Quantile(int start, int length, int k)
    {
        ValidateWindow(start, length);
        if (length == 0)
            throw new ArgumentOutOfRangeException(nameof(length), length, "The window must not be empty.");
        if ((uint)k >= (uint)length)
            throw new ArgumentOutOfRangeException(nameof(k), k, "k must be within [0, length).");

        int lo = start;
        int hi = start + length;
        int code = 0;
        for (int level = 0; level < _levels.Length; level++)
        {
            RankSelectBitVector bits = _levels[level];
            int zerosBelowLo = bits.Rank0(lo);
            int zerosBelowHi = bits.Rank0(hi);
            int zerosInWindow = zerosBelowHi - zerosBelowLo;

            if (k < zerosInWindow)
            {
                // The k-th smallest carries a clear bit here, so it stays in the zeros half.
                lo = zerosBelowLo;
                hi = zerosBelowHi;
                code <<= 1;
            }
            else
            {
                // Every zero in the window is smaller than every one, so they are all skipped over.
                k -= zerosInWindow;
                lo = _zeros[level] + (lo - zerosBelowLo);
                hi = _zeros[level] + (hi - zerosBelowHi);
                code = (code << 1) | 1;
            }
        }

        return _symbols[code];
    }

    /// <summary>
    /// Returns how many values inside the window <c>[start, start + length)</c> fall in the inclusive band
    /// <c>[minValue, maxValue]</c>.
    /// </summary>
    /// <param name="start">The zero-based start of the window, over <c>[0, Length]</c>.</param>
    /// <param name="length">The window length. Must not run past <see cref="Length"/>.</param>
    /// <param name="minValue">The inclusive lower bound of the value band.</param>
    /// <param name="maxValue">
    /// The inclusive upper bound of the value band. A band with <c>maxValue &lt; minValue</c> is empty and
    /// yields <c>0</c> rather than throwing.
    /// </param>
    /// <returns>The number of values in the window that fall in the band, counting duplicates.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="start"/> or <paramref name="length"/> is negative, or the window runs past
    /// <see cref="Length"/>.
    /// </exception>
    /// <remarks>
    /// <c>O(log sigma)</c> — two descents, one per band edge — against the <c>O(m)</c> counting loop over the
    /// window. The band bounds need not occur in the sequence; they are resolved to the surrounding codes.
    /// </remarks>
    public int RangeCount(int start, int length, int minValue, int maxValue)
    {
        ValidateWindow(start, length);
        if (length == 0 || maxValue < minValue)
            return 0;

        if (_levels.Length == 0)
        {
            // A single-symbol alphabet has no level to descend: every position in the window is that symbol,
            // so the band either takes all of them or none. (A zero-symbol alphabet means an empty sequence,
            // whose only legal window is the empty one already returned above.)
            int only = _symbols[0];
            return only >= minValue && only <= maxValue ? length : 0;
        }

        // Codes are ascending in value, so a band maps to the half-open code range [lowCode, highCode) where
        // lowCode is the first code >= minValue and highCode the first code > maxValue.
        int lowCode = LowerBound(minValue);
        int highCode = UpperBound(maxValue);
        if (lowCode >= highCode)
            return 0;

        return CountBelow(start, start + length, highCode) - CountBelow(start, start + length, lowCode);
    }

    /// <summary>Returns an enumerator over the indexed values, in their original order.</summary>
    /// <returns>A struct enumerator that allocates nothing.</returns>
    /// <remarks>
    /// Enumeration reconstructs each value through the levels, so it is <c>O(n log sigma)</c> overall. A caller
    /// that wants the sequence back in bulk should keep the array it built from.
    /// </remarks>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // Occurrences of `value` in the half-open position range [lo, hi), by mapping the interval down one level
    // at a time along the value's own code bits. Absent values are rejected up front, which is also what keeps
    // the descent from following a code that does not exist.
    private int RangeRankCore(int lo, int hi, int value)
    {
        int code = TryEncode(value);
        if (code < 0)
            return 0;

        // A single-symbol alphabet has no levels, so the loop below does not run and the whole interval is
        // occurrences of the one value there is — which is exactly what `hi - lo` reports.
        for (int level = 0; level < _levels.Length; level++)
        {
            RankSelectBitVector bits = _levels[level];
            int bit = _levels.Length - 1 - level;
            if (((code >> bit) & 1) == 0)
            {
                lo = bits.Rank0(lo);
                hi = bits.Rank0(hi);
            }
            else
            {
                lo = _zeros[level] + bits.Rank(lo);
                hi = _zeros[level] + bits.Rank(hi);
            }
        }

        return hi - lo;
    }

    // Values in [lo, hi) whose code is strictly below `code`. At each level the zeros half is entirely below
    // any code with a set bit here, so a set bit banks that whole half and follows the ones; a clear bit banks
    // nothing and follows the zeros.
    private int CountBelow(int lo, int hi, int code)
    {
        if (code >= _symbols.Length)
            return hi - lo;

        int count = 0;
        for (int level = 0; level < _levels.Length && lo < hi; level++)
        {
            RankSelectBitVector bits = _levels[level];
            int zerosBelowLo = bits.Rank0(lo);
            int zerosBelowHi = bits.Rank0(hi);

            int bit = _levels.Length - 1 - level;
            if (((code >> bit) & 1) == 1)
            {
                count += zerosBelowHi - zerosBelowLo;
                lo = _zeros[level] + (lo - zerosBelowLo);
                hi = _zeros[level] + (hi - zerosBelowHi);
            }
            else
            {
                lo = zerosBelowLo;
                hi = zerosBelowHi;
            }
        }

        return count;
    }

    // Writes one bit per position into `words`, snapshots it as this level's rank/select vector, then stably
    // partitions `current` into `next` — every clear bit first, in order, then every set bit, in order — which
    // is the arrangement the level below is built over.
    private void BuildLevel(int[] current, int[] next, ulong[] words, int count, int bit, int level)
    {
        int wordCount = WordCount(count);
        Array.Clear(words, 0, wordCount);

        int ones = 0;
        for (int i = 0; i < count; i++)
        {
            if (((current[i] >> bit) & 1) == 0)
                continue;

            words[i >> 6] |= 1UL << (i & 63);
            ones++;
        }

        int zeros = count - ones;
        _zeros[level] = zeros;
        _levels[level] = new RankSelectBitVector(count, words.AsSpan(0, wordCount));

        int zeroCursor = 0;
        int oneCursor = zeros;
        for (int i = 0; i < count; i++)
        {
            int code = current[i];
            if (((code >> bit) & 1) == 0)
                next[zeroCursor++] = code;
            else
                next[oneCursor++] = code;
        }
    }

    // The code of `value`, or -1 when the sequence never holds it.
    private int TryEncode(int value)
    {
        int code = Array.BinarySearch(_symbols, value);
        return code < 0 ? -1 : code;
    }

    // The code of a value known to be present. Only the constructor calls this, over the table it just built.
    private int Encode(int value) => Array.BinarySearch(_symbols, value);

    // The first code whose symbol is >= value, which is AlphabetSize when every symbol is smaller.
    private int LowerBound(int value)
    {
        int code = Array.BinarySearch(_symbols, value);
        return code >= 0 ? code : ~code;
    }

    // The first code whose symbol is > value, which is AlphabetSize when every symbol is <= it.
    private int UpperBound(int value)
    {
        int code = Array.BinarySearch(_symbols, value);
        return code >= 0 ? code + 1 : ~code;
    }

    private void ValidateWindow(int start, int length)
    {
        if ((uint)start > (uint)_length)
            throw new ArgumentOutOfRangeException(nameof(start), start, "Start must be within [0, Length].");
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), length, "Length must be non-negative.");
        if (length > _length - start)
            throw new ArgumentOutOfRangeException(nameof(length), length, "The window must not run past Length.");
    }

    private static int[] DistinctAscending(ReadOnlySpan<int> values)
    {
        if (values.Length == 0)
            return [];

        int[] sorted = values.ToArray();
        Array.Sort(sorted);

        int distinct = 1;
        for (int i = 1; i < sorted.Length; i++)
        {
            if (sorted[i] != sorted[distinct - 1])
                sorted[distinct++] = sorted[i];
        }

        if (distinct == sorted.Length)
            return sorted;

        int[] symbols = new int[distinct];
        Array.Copy(sorted, symbols, distinct);
        return symbols;
    }

    // ceil(log2(alphabet)) — the number of bits a code needs. An alphabet of 0 or 1 symbols needs none: every
    // position carries the same code, so there is nothing for a level to distinguish.
    private static int BitsForAlphabet(int alphabet) =>
        alphabet <= 1 ? 0 : 32 - BitOperations.LeadingZeroCount((uint)(alphabet - 1));

    // Only called where at least one level exists, which needs two distinct values and so `bits >= 2`.
    private static int WordCount(int bits) => (bits + 63) / 64;

    /// <summary>
    /// Enumerates the indexed values in their original order without allocating.
    /// </summary>
    public struct Enumerator : IEnumerator<int>
    {
        private readonly WaveletTree _tree;
        private int _index;
        private int _current;

        internal Enumerator(WaveletTree tree)
        {
            _tree = tree;
            _index = 0;
            _current = 0;
        }

        /// <summary>Gets the value at the current position.</summary>
        public readonly int Current => _current;

        readonly object IEnumerator.Current => _current;

        /// <summary>Advances to the next position.</summary>
        /// <returns><c>true</c> while a value remains; otherwise <c>false</c>.</returns>
        public bool MoveNext()
        {
            if (_index >= _tree._length)
                return false;

            _current = _tree[_index++];
            return true;
        }

        /// <summary>Resets the enumerator to before the first value.</summary>
        public void Reset()
        {
            _index = 0;
            _current = 0;
        }

        /// <summary>Does nothing; the enumerator holds no resources.</summary>
        public readonly void Dispose()
        {
        }
    }
}
