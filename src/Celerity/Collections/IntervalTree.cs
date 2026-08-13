using System.Collections;

namespace Celerity.Collections;

/// <summary>
/// An <see cref="IntervalTree{TKey, TValue, TComparer}"/> ordered by <see cref="Comparer{TKey}.Default"/> —
/// the convenience alias that closes over <see cref="DefaultComparer{T}"/>, exactly as
/// <see cref="BTreeSet{T}"/> fronts its comparer-parameterized form.
/// </summary>
/// <typeparam name="TKey">The endpoint type. Must be orderable by <see cref="Comparer{T}.Default"/>.</typeparam>
/// <typeparam name="TValue">The payload carried by each interval.</typeparam>
public sealed class IntervalTree<TKey, TValue> : IntervalTree<TKey, TValue, DefaultComparer<TKey>>
{
    /// <summary>
    /// Builds a tree over <paramref name="intervals"/>, ordered by <see cref="Comparer{TKey}.Default"/>.
    /// </summary>
    /// <param name="intervals">The intervals to index. The sequence is read once and copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="intervals"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">An interval's end precedes its start.</exception>
    public IntervalTree(IEnumerable<Interval<TKey, TValue>> intervals)
        : base(intervals)
    {
    }
}

/// <summary>
/// An <b>interval tree</b>: a build-once, immutable index over half-open <c>[start, end)</c> ranges that
/// answers <i>which ranges cover this point</i> and <i>which ranges overlap this window</i> in
/// <c>O(log n + k)</c> when the matches cluster and <c>O(min(n, k log n))</c> when they are scattered
/// (<c>k</c> being the number of matches) — instead of the <c>O(n)</c> those questions otherwise cost on
/// every query regardless.
/// </summary>
/// <typeparam name="TKey">The endpoint type, ordered by <typeparamref name="TComparer"/>.</typeparam>
/// <typeparam name="TValue">The payload carried by each interval.</typeparam>
/// <typeparam name="TComparer">
/// The comparer that orders the endpoints. Must be a value type implementing <see cref="IComparer{T}"/> so the
/// JIT can devirtualize and inline it — an interface-typed comparer would cost a virtual call for every
/// endpoint inspected on the way down. Use <see cref="DefaultComparer{T}"/> (or the two-parameter
/// <see cref="IntervalTree{TKey, TValue}"/> alias) for the natural order.
/// </typeparam>
/// <remarks>
/// <para>
/// .NET ships nothing for this question — there is no interval tree, no interval map, and no range-overlap
/// query anywhere in <c>System.Collections</c>. The idiomatic answer is a <see cref="List{T}"/> of ranges and
/// a linear scan, which is <c>O(n)</c> per query; keeping that list sorted by start does not help, because an
/// interval that begins far to the left can still cover the query point, so the scan cannot stop early. This
/// is the gap the type fills: conflict detection over a booking calendar, IP-range and CIDR-to-owner lookup,
/// effective-dated pricing and feature-flag windows, "which trace spans were live at time <c>t</c>", and the
/// genomics overlap query the structure is named for.
/// </para>
/// <para>
/// The sibling range structures index the <i>position</i> axis and cannot answer this.
/// <see cref="FenwickTree{T}"/> and <see cref="SegmentTree{T, TMonoid}"/> fold values stored <i>at</i>
/// positions; neither can enumerate the intervals that stab one. <see cref="BTreeSet{T, TComparer}"/> finds
/// endpoints in order, but ordering by start bounds nothing when one long interval reaches across the whole
/// domain.
/// </para>
/// <para>
/// <b>Layout.</b> The intervals are sorted by start into flat parallel arrays, and a balanced binary search
/// tree is laid over that array <i>implicitly</i>: the node for the index range <c>[lo, hi)</c> sits at its
/// midpoint and its subtree is exactly <c>[lo, hi)</c>. There are no nodes, no child pointers and no
/// per-interval heap object — the whole structure is four arrays. Each node additionally carries the maximum
/// end over its subtree, and that augmentation is what turns the scan into a search: a subtree whose maximum
/// end is at or before the query start cannot hold a match and is skipped whole, as is one whose starts are
/// all at or after the query end.
/// </para>
/// <para>
/// <b>What the bound really is.</b> The augmentation proves only that a subtree <i>contains</i> a candidate,
/// not where it sits, so <c>k</c> matches spread across the tree can force up to <c>k</c> separate
/// root-to-match descents: the worst case is <c>O(min(n, k log n))</c>, not the <c>O(log n + k)</c> a centered
/// interval tree with per-node sorted endpoint lists would guarantee. The clustered case is the common one
/// here because entries are stored in start order, so overlapping ranges are neighbours and their descents
/// share almost the whole path. A query never does more work than the full scan the baseline pays on every
/// query regardless. On the selective shapes this type is for, the measured point query is 151x a linear scan
/// at 100,000 intervals; on a shape with roughly 1,250 matches per point it is 8.3x.
/// </para>
/// <para>
/// <b>One input defeats the pruning outright: stored empty intervals.</b> An empty <c>[x, x)</c> raises its
/// subtree's maximum end exactly as a real interval would, but is then rejected by the per-node emptiness test
/// <i>after</i> the walk has already descended to it — so it can never be pruned in bulk, only discarded one
/// node at a time. A tree of nothing but empty intervals is therefore <c>O(n)</c> per query with <c>k = 0</c>
/// matches. Neutralizing that inside the tree would need a second per-node array to mark the subtrees holding
/// no real interval, which is a per-query cost on every caller to make a degenerate input asymptotically
/// nicer; the trade is not worth it. If your data carries many zero-length entries and you do not need them
/// back out of <see cref="IReadOnlyList{T}"/>, filter them before building — they can never match anything.
/// </para>
/// <para>
/// <b>Build-once.</b> The tree is immutable; adding an interval means building a new one, as with
/// <see cref="FrozenCelerityDictionary{TValue}"/>, <see cref="XorFilter{T, THasher}"/> and
/// <see cref="RankSelectBitVector"/>. Keeping the augmentation correct under insertion needs a rebalancing
/// tree with a fix-up per rotation, which is a different type with a different cost profile, not an overload
/// of this one. Because nothing mutates, enumeration is never invalidated, and concurrent readers need no
/// synchronization <i>as far as the tree itself is concerned</i> — with one caveat that is easy to miss: every
/// query calls <typeparamref name="TComparer"/>, so a comparer that is not itself thread-safe makes concurrent
/// queries unsafe however immutable the tree is. <see cref="DefaultComparer{T}"/> is stateless, so the default
/// case is safe; a stateful comparer, which the two-argument constructor exists to accept, is the caller's to
/// reason about.
/// </para>
/// <para>
/// <b>Two query tiers.</b> <see cref="Overlaps"/>, <see cref="ContainsPoint"/>, <see cref="CountOverlapping"/>,
/// <see cref="CountContaining"/> and the two <c>Copy</c> methods allocate nothing at all; the two <c>Get</c>
/// methods are the convenience tier and allocate the result array (they walk the tree twice — once to size it
/// exactly, once to fill it). All of them share a single traversal, generic over a <c>struct</c> visitor, so
/// the JIT specializes it per call site and inlines the per-match work rather than paying a delegate or an
/// interface call per hit.
/// </para>
/// <para>
/// Intervals are kept distinct: two overlapping ranges stay two entries and a query reports both. This is not
/// a coalescing interval map, and duplicates are preserved. Entries are exposed in ascending start order
/// through <see cref="IReadOnlyList{T}"/>; two entries with the same start and end have an unspecified
/// relative order.
/// </para>
/// </remarks>
public class IntervalTree<TKey, TValue, TComparer> : IReadOnlyList<Interval<TKey, TValue>>
    where TComparer : struct, IComparer<TKey>
{
    // Sorted by start. The implicit tree needs no links: the node covering the index range [lo, hi) is at
    // mid = lo + (hi - lo) / 2, its left child covers [lo, mid) and its right child [mid + 1, hi). Keeping the
    // endpoints in their own arrays keeps a descent off the payload entirely — a walk reads _maxEnds and
    // _starts on every node it visits and _values only for the handful it actually returns.
    private readonly TKey[] _starts;
    private readonly TKey[] _ends;
    private readonly TValue?[] _values;

    // _maxEnds[mid] is the largest end anywhere in the subtree rooted at mid. The augmentation the search
    // depends on: without it there is no way to know that a whole subtree ends before the query begins.
    private readonly TKey[] _maxEnds;

    // Not readonly: a readonly field of a struct type is defensively copied on every member call, which would
    // put a copy on the hottest path in the type. Matches BTreeSet's comparer field.
    private TComparer _comparer;

    /// <summary>Builds a tree over <paramref name="intervals"/>, ordered by a default-constructed comparer.</summary>
    /// <param name="intervals">The intervals to index. The sequence is read once and copied.</param>
    /// <exception cref="ArgumentNullException"><paramref name="intervals"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">An interval's end precedes its start.</exception>
    public IntervalTree(IEnumerable<Interval<TKey, TValue>> intervals)
        : this(intervals, default)
    {
    }

    /// <summary>Builds a tree over <paramref name="intervals"/>, ordered by <paramref name="comparer"/>.</summary>
    /// <param name="intervals">The intervals to index. The sequence is read once and copied.</param>
    /// <param name="comparer">The endpoint order. Pass this overload only when the comparer carries state.</param>
    /// <exception cref="ArgumentNullException"><paramref name="intervals"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">An interval's end precedes its start.</exception>
    public IntervalTree(IEnumerable<Interval<TKey, TValue>> intervals, TComparer comparer)
    {
        ArgumentNullException.ThrowIfNull(intervals);

        _comparer = comparer;

        // Deliberately the only build entry point. A ReadOnlySpan<Interval<,>> overload alongside it would be
        // ambiguous for the commonest argument of all — an array — under C# 12, which is what net8.0 (the
        // floor TFM) compiles as; the same call resolves on a newer language version, so the break would land
        // only on the oldest supported consumers.
        //
        // A counted source is sized and copied once, as the sibling trees' constructors do. Going through a
        // List<T> unconditionally would allocate and copy a second backing array for the commonest sources of
        // all — an array or a List — on a type whose build cost is already the tradeoff it asks callers to make.
        Interval<TKey, TValue>[] items;
        if (intervals is ICollection<Interval<TKey, TValue>> counted)
        {
            items = new Interval<TKey, TValue>[counted.Count];
            counted.CopyTo(items, 0);
        }
        else
        {
            items = new List<Interval<TKey, TValue>>(intervals).ToArray();
        }

        _starts = new TKey[items.Length];
        _ends = new TKey[items.Length];
        _values = new TValue?[items.Length];
        _maxEnds = new TKey[items.Length];
        Build(items, nameof(intervals));
    }

    /// <summary>Gets the number of intervals in the tree, counting duplicates and empty ranges.</summary>
    public int Count => _starts.Length;

    /// <summary>Gets the interval at <paramref name="index"/> in ascending start order.</summary>
    /// <param name="index">The zero-based position in start order.</param>
    /// <returns>The interval stored at that position.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside <c>[0, Count)</c>.</exception>
    public Interval<TKey, TValue> this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_starts.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be in the range [0, Count).");

            return EntryAt(index);
        }
    }

    /// <summary>
    /// Determines whether any interval covers <paramref name="point"/> — that is, starts at or before it and
    /// ends strictly after it. Stops at the first match.
    /// </summary>
    /// <param name="point">The point to stab.</param>
    /// <returns><c>true</c> if at least one interval covers the point; otherwise <c>false</c>.</returns>
    public bool ContainsPoint(TKey point)
    {
        AnyVisitor visitor = default;
        Search(point, point, isPoint: true, ref visitor);
        return visitor.Found;
    }

    /// <summary>
    /// Determines whether any interval overlaps the half-open window <c>[start, end)</c>. Stops at the first
    /// match, which is what makes this the right member for a conflict check.
    /// </summary>
    /// <param name="start">The inclusive lower endpoint of the window.</param>
    /// <param name="end">The exclusive upper endpoint of the window. Must not precede <paramref name="start"/>.</param>
    /// <returns><c>true</c> if at least one interval overlaps the window; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="end"/> precedes <paramref name="start"/>.</exception>
    /// <remarks>An empty window (<paramref name="start"/> equal to <paramref name="end"/>) covers no point and never overlaps.</remarks>
    public bool Overlaps(TKey start, TKey end)
    {
        if (IsEmptyWindow(start, end))
            return false;

        AnyVisitor visitor = default;
        Search(start, end, isPoint: false, ref visitor);
        return visitor.Found;
    }

    /// <summary>Counts the intervals that cover <paramref name="point"/>.</summary>
    /// <param name="point">The point to stab.</param>
    /// <returns>The number of intervals covering the point.</returns>
    public int CountContaining(TKey point)
    {
        CountVisitor visitor = default;
        Search(point, point, isPoint: true, ref visitor);
        return visitor.Count;
    }

    /// <summary>Counts the intervals that overlap the half-open window <c>[start, end)</c>.</summary>
    /// <param name="start">The inclusive lower endpoint of the window.</param>
    /// <param name="end">The exclusive upper endpoint of the window. Must not precede <paramref name="start"/>.</param>
    /// <returns>The number of intervals overlapping the window.</returns>
    /// <exception cref="ArgumentException"><paramref name="end"/> precedes <paramref name="start"/>.</exception>
    public int CountOverlapping(TKey start, TKey end)
    {
        if (IsEmptyWindow(start, end))
            return 0;

        CountVisitor visitor = default;
        Search(start, end, isPoint: false, ref visitor);
        return visitor.Count;
    }

    /// <summary>
    /// Writes the intervals covering <paramref name="point"/> into <paramref name="destination"/>, allocating
    /// nothing.
    /// </summary>
    /// <param name="point">The point to stab.</param>
    /// <param name="destination">The buffer to fill.</param>
    /// <param name="destinationIndex">The position in <paramref name="destination"/> to start writing at.</param>
    /// <returns>The number of intervals written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="destinationIndex"/> is outside <c>[0, destination.Length]</c>.</exception>
    /// <remarks>
    /// Writing stops when the buffer is full, so a return value equal to the remaining room may mean the
    /// matches were truncated. Size the buffer with <see cref="CountContaining"/> when every match is needed.
    /// </remarks>
    public int CopyContaining(TKey point, Interval<TKey, TValue>[] destination, int destinationIndex = 0)
    {
        CopyVisitor visitor = CreateCopyVisitor(destination, destinationIndex);
        Search(point, point, isPoint: true, ref visitor);
        return visitor.Written;
    }

    /// <summary>
    /// Writes the intervals overlapping the half-open window <c>[start, end)</c> into
    /// <paramref name="destination"/>, allocating nothing.
    /// </summary>
    /// <param name="start">The inclusive lower endpoint of the window.</param>
    /// <param name="end">The exclusive upper endpoint of the window. Must not precede <paramref name="start"/>.</param>
    /// <param name="destination">The buffer to fill.</param>
    /// <param name="destinationIndex">The position in <paramref name="destination"/> to start writing at.</param>
    /// <returns>The number of intervals written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="destinationIndex"/> is outside <c>[0, destination.Length]</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="end"/> precedes <paramref name="start"/>.</exception>
    /// <remarks>
    /// Writing stops when the buffer is full, so a return value equal to the remaining room may mean the
    /// matches were truncated. Size the buffer with <see cref="CountOverlapping"/> when every match is needed.
    /// </remarks>
    public int CopyOverlapping(TKey start, TKey end, Interval<TKey, TValue>[] destination, int destinationIndex = 0)
    {
        CopyVisitor visitor = CreateCopyVisitor(destination, destinationIndex);
        if (IsEmptyWindow(start, end))
            return 0;

        Search(start, end, isPoint: false, ref visitor);
        return visitor.Written;
    }

    /// <summary>Returns the intervals covering <paramref name="point"/>, in ascending start order.</summary>
    /// <param name="point">The point to stab.</param>
    /// <returns>The matching intervals, or an empty array when none match.</returns>
    /// <remarks>
    /// This is the convenience tier: it allocates the result and walks the tree twice, once to size the array
    /// exactly and once to fill it. Use <see cref="CopyContaining"/> on a hot path.
    /// </remarks>
    public Interval<TKey, TValue>[] GetContaining(TKey point)
    {
        int count = CountContaining(point);
        if (count == 0)
            return Array.Empty<Interval<TKey, TValue>>();

        var result = new Interval<TKey, TValue>[count];
        CopyContaining(point, result);
        return result;
    }

    /// <summary>
    /// Returns the intervals overlapping the half-open window <c>[start, end)</c>, in ascending start order.
    /// </summary>
    /// <param name="start">The inclusive lower endpoint of the window.</param>
    /// <param name="end">The exclusive upper endpoint of the window. Must not precede <paramref name="start"/>.</param>
    /// <returns>The matching intervals, or an empty array when none match.</returns>
    /// <exception cref="ArgumentException"><paramref name="end"/> precedes <paramref name="start"/>.</exception>
    /// <remarks>
    /// This is the convenience tier: it allocates the result and walks the tree twice, once to size the array
    /// exactly and once to fill it. Use <see cref="CopyOverlapping"/> on a hot path.
    /// </remarks>
    public Interval<TKey, TValue>[] GetOverlapping(TKey start, TKey end)
    {
        int count = CountOverlapping(start, end);
        if (count == 0)
            return Array.Empty<Interval<TKey, TValue>>();

        var result = new Interval<TKey, TValue>[count];
        CopyOverlapping(start, end, result);
        return result;
    }

    /// <summary>Returns an enumerator over every stored interval in ascending start order.</summary>
    /// <returns>A struct enumerator over the intervals.</returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<Interval<TKey, TValue>> IEnumerable<Interval<TKey, TValue>>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- internals ---------------------------------------------------------------------------------

    // Sorts the materialized input by start (then end, so equal starts land in a deterministic order) and
    // fills the parallel arrays, then computes the subtree maxima bottom-up.
    private void Build(Interval<TKey, TValue>[] items, string paramName)
    {
        for (int i = 0; i < items.Length; i++)
        {
            if (_comparer.Compare(items[i].Start, items[i].End) > 0)
                throw new ArgumentException("An interval's end must not precede its start.", paramName);
        }

        // The only comparer call in the type that is not devirtualized. It runs O(n log n) times at build and
        // never once at query time, which is the trade a build-once structure exists to make.
        Array.Sort(items, new StartOrder(_comparer));

        for (int i = 0; i < items.Length; i++)
        {
            _starts[i] = items[i].Start;
            _ends[i] = items[i].End;
            _values[i] = items[i].Value;
        }

        if (items.Length != 0)
            BuildMaxEnds(0, items.Length);
    }

    // Fills _maxEnds for the subtree covering [lo, hi) and returns its maximum end. Recursion depth is the
    // tree height — at most 31 for any array length, so the call stack is bounded by construction.
    private TKey BuildMaxEnds(int lo, int hi)
    {
        int mid = lo + ((hi - lo) >> 1);
        TKey max = _ends[mid];

        if (lo < mid)
        {
            TKey left = BuildMaxEnds(lo, mid);
            if (_comparer.Compare(left, max) > 0)
                max = left;
        }

        if (mid + 1 < hi)
        {
            TKey right = BuildMaxEnds(mid + 1, hi);
            if (_comparer.Compare(right, max) > 0)
                max = right;
        }

        _maxEnds[mid] = max;
        return max;
    }

    // Rejects an inverted window and reports an empty one, which is a well-defined query that matches nothing:
    // a half-open [x, x) covers no point, so nothing can share a point with it.
    private bool IsEmptyWindow(TKey start, TKey end)
    {
        int cmp = _comparer.Compare(start, end);
        if (cmp > 0)
            throw new ArgumentException("The window's end must not precede its start.", nameof(end));

        return cmp == 0;
    }

    private CopyVisitor CreateCopyVisitor(Interval<TKey, TValue>[] destination, int destinationIndex)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if ((uint)destinationIndex > (uint)destination.Length)
            throw new ArgumentOutOfRangeException(nameof(destinationIndex), destinationIndex,
                "Destination index must be in the range [0, destination.Length].");

        return new CopyVisitor(this, destination, destinationIndex);
    }

    private void Search<TVisitor>(TKey queryStart, TKey queryEnd, bool isPoint, ref TVisitor visitor)
        where TVisitor : struct, IIntervalVisitor
    {
        if (_starts.Length != 0)
            Walk(0, _starts.Length, queryStart, queryEnd, isPoint, ref visitor);
    }

    // The single traversal behind every query. A point query passes the same key as both endpoints and sets
    // isPoint, which loosens the right-hand prune from "starts at or after the window's end" to "starts after
    // the point" — the one place the two shapes differ.
    //
    // Returns false once the visitor has asked to stop, which unwinds the whole recursion rather than only the
    // current level. Matches are visited in ascending start order because the walk is an in-order traversal.
    private bool Walk<TVisitor>(int lo, int hi, TKey queryStart, TKey queryEnd, bool isPoint, ref TVisitor visitor)
        where TVisitor : struct, IIntervalVisitor
    {
        int mid = lo + ((hi - lo) >> 1);

        // Nothing in this subtree reaches past the query's lower bound, so no interval in it can match. This
        // is the augmentation earning its keep: it prunes on a bound no start-ordered structure can express.
        if (_comparer.Compare(_maxEnds[mid], queryStart) <= 0)
            return true;

        if (lo < mid && !Walk(lo, mid, queryStart, queryEnd, isPoint, ref visitor))
            return false;

        // Starts are sorted, so once this node begins at or past the query's upper bound, so does every node
        // to its right.
        int startVsEnd = _comparer.Compare(_starts[mid], queryEnd);
        if (startVsEnd > 0 || (startVsEnd == 0 && !isPoint))
            return true;

        // The node starts inside the window and its subtree reaches past the window's start; it matches when
        // this interval itself does. The last comparison excludes an empty stored interval, which covers no
        // point and so overlaps nothing — reachable only for a window query, since a point query already
        // implies start <= point < end and therefore a non-empty interval.
        if (_comparer.Compare(queryStart, _ends[mid]) < 0 &&
            _comparer.Compare(_starts[mid], _ends[mid]) < 0 &&
            !visitor.Visit(mid))
        {
            return false;
        }

        if (mid + 1 < hi && !Walk(mid + 1, hi, queryStart, queryEnd, isPoint, ref visitor))
            return false;

        return true;
    }

    private Interval<TKey, TValue> EntryAt(int index) => new(_starts[index], _ends[index], _values[index]);

    // Orders the build input by start, then end. Equal (start, end) pairs are left in whatever order the sort
    // produces — Array.Sort is not stable, so there is no order to preserve in the first place.
    private sealed class StartOrder : IComparer<Interval<TKey, TValue>>
    {
        private TComparer _comparer;

        internal StartOrder(TComparer comparer) => _comparer = comparer;

        public int Compare(Interval<TKey, TValue> x, Interval<TKey, TValue> y)
        {
            int cmp = _comparer.Compare(x.Start, y.Start);
            return cmp != 0 ? cmp : _comparer.Compare(x.End, y.End);
        }
    }

    // What each query does with a match. A struct implementation used as a generic type argument is
    // specialized by the JIT and inlined into the walk, so the shared traversal costs nothing over four
    // hand-written ones — the same rule the struct hashers, IMonoid<T> and DefaultComparer<T> follow.
    private interface IIntervalVisitor
    {
        // Returns false to stop the walk.
        bool Visit(int index);
    }

    private struct AnyVisitor : IIntervalVisitor
    {
        public bool Found;

        public bool Visit(int index)
        {
            Found = true;
            return false;
        }
    }

    private struct CountVisitor : IIntervalVisitor
    {
        public int Count;

        public bool Visit(int index)
        {
            Count++;
            return true;
        }
    }

    private struct CopyVisitor : IIntervalVisitor
    {
        private readonly IntervalTree<TKey, TValue, TComparer> _tree;
        private readonly Interval<TKey, TValue>[] _destination;
        private readonly int _start;
        private int _index;

        internal CopyVisitor(IntervalTree<TKey, TValue, TComparer> tree, Interval<TKey, TValue>[] destination, int destinationIndex)
        {
            _tree = tree;
            _destination = destination;
            _start = destinationIndex;
            _index = destinationIndex;
        }

        internal readonly int Written => _index - _start;

        public bool Visit(int index)
        {
            if (_index >= _destination.Length)
                return false;

            _destination[_index++] = _tree.EntryAt(index);
            return true;
        }
    }

    /// <summary>A struct enumerator over an interval tree's entries in ascending start order.</summary>
    /// <remarks>
    /// The tree is immutable, so there is no version to check and no concurrent-modification failure mode:
    /// an enumerator can never be invalidated.
    /// </remarks>
    public struct Enumerator : IEnumerator<Interval<TKey, TValue>>
    {
        private readonly IntervalTree<TKey, TValue, TComparer> _tree;
        private int _index;
        private Interval<TKey, TValue> _current;

        internal Enumerator(IntervalTree<TKey, TValue, TComparer> tree)
        {
            _tree = tree;
            _index = 0;
            _current = default;
        }

        /// <summary>Gets the interval at the current position of the enumerator.</summary>
        public readonly Interval<TKey, TValue> Current => _current;

        readonly object? IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next interval.</summary>
        /// <returns><c>true</c> if there is a next interval; otherwise <c>false</c>.</returns>
        public bool MoveNext()
        {
            if (_index < _tree._starts.Length)
            {
                _current = _tree.EntryAt(_index);
                _index++;
                return true;
            }

            _current = default;
            return false;
        }

        /// <summary>Resets the enumerator to before the first interval.</summary>
        public void Reset()
        {
            _index = 0;
            _current = default;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }
}
