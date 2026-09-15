using System.Collections;

namespace Celerity.Collections;

/// <summary>
/// A <see cref="RangeMap{TKey, TValue, TComparer}"/> ordered by <see cref="Comparer{T}.Default"/> — the
/// convenience alias that closes over <see cref="DefaultComparer{T}"/>, exactly as
/// <see cref="IntervalTree{TKey, TValue}"/> fronts its comparer-parameterized form.
/// </summary>
/// <typeparam name="TKey">The key type. Must be orderable by <see cref="Comparer{T}.Default"/>.</typeparam>
/// <typeparam name="TValue">The value mapped over each range.</typeparam>
public sealed class RangeMap<TKey, TValue> : RangeMap<TKey, TValue, DefaultComparer<TKey>>
{
    /// <summary>Initializes a new, empty map ordered by <see cref="Comparer{T}.Default"/>.</summary>
    public RangeMap()
    {
    }

    /// <summary>
    /// Initializes a new map ordered by <see cref="Comparer{T}.Default"/> and fills it by assigning each of
    /// <paramref name="source"/>'s intervals in turn, so a later interval overwrites an earlier one where they
    /// overlap.
    /// </summary>
    /// <param name="source">The assignments to apply, in order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">An interval's end orders before its start.</exception>
    public RangeMap(IEnumerable<Interval<TKey, TValue>> source)
        : base(source)
    {
    }
}

/// <summary>
/// A <b>range map</b>: a mutable map from <b>disjoint half-open ranges</b> <c>[start, end)</c> of keys to
/// values. Assigning a value to a range overwrites whatever that range held — splitting any stored range that
/// straddles either edge — and ranges that end up adjacent with equal values merge into one. A point lookup
/// is a single <c>O(log n)</c> descent, and an assignment costs <c>O((k + 1) log n)</c> for the <c>k</c>
/// stored ranges it overwrites.
/// </summary>
/// <typeparam name="TKey">The key type, ordered by <typeparamref name="TComparer"/>.</typeparam>
/// <typeparam name="TValue">The value mapped over each range.</typeparam>
/// <typeparam name="TComparer">
/// The comparer that orders the keys. Must be a value type implementing <see cref="IComparer{T}"/> so the JIT
/// can devirtualize and inline it. Use <see cref="DefaultComparer{T}"/> (or the two-parameter
/// <see cref="RangeMap{TKey, TValue}"/> alias) for the natural order.
/// </typeparam>
/// <remarks>
/// <para>
/// .NET ships nothing for this. The idiomatic hand-roll is a <see cref="List{T}"/> of ranges kept sorted by
/// start: binary search answers a lookup well, but every assignment in the middle memmoves the tail, so an
/// edit is <c>O(n)</c>. <see cref="SortedDictionary{TKey, TValue}"/> cannot even answer the lookup, because it
/// has no floor query. This is Guava's <c>TreeRangeMap</c> and boost's <c>interval_map</c>: an IP-range or
/// keyspace ownership table edited as shards move, an allocator's used-versus-free map, effective-dated
/// configuration edited in place, a text editor's style runs, a calendar's availability.
/// </para>
/// <para>
/// It is not <see cref="IntervalTree{TKey, TValue, TComparer}"/>. That type is build-once and keeps
/// overlapping ranges as distinct entries, answering <i>which ranges cover this point</i>. This one is mutable
/// and never holds two ranges over the same key, so a point maps to at most one value.
/// </para>
/// <para>
/// <b>Canonical form.</b> Stored ranges are non-empty, pairwise disjoint and in ascending order, and no two
/// adjacent ranges — one ending exactly where the next begins — carry equal values; those are always merged.
/// Equality is the value comparer's, <see cref="EqualityComparer{T}.Default"/> unless one is supplied. So the
/// same sequence of point-to-value facts always produces the same ranges, whatever order it was assigned in,
/// and <see cref="Count"/> is the number of maximal runs rather than the number of assignments made. A merged
/// range carries the value most recently stored in it.
/// </para>
/// <para>
/// <b>A write that changes nothing is a no-op.</b> Assigning a value to a range that one stored range already
/// covers with an equal value, removing a range that holds nothing, assigning or removing an empty range, and
/// clearing an empty map all leave the map untouched — including which of two equal values it holds — and do
/// not invalidate active enumerators, matching the library's family-wide rule.
/// </para>
/// <para>
/// <b>Layout.</b> The ranges live in a <see cref="BTreeDictionary{TKey, TValue, TComparer}"/> keyed by each
/// range's <i>end</i>. Because the ranges are disjoint, ordering them by end is ordering them by start, and the
/// range that covers a key <c>x</c>, if any, is simply the first one ending after <c>x</c> — one upper-bound
/// descent through nodes of up to 31 keys. The map allocates only as that tree grows; lookups, the enumerators
/// and <see cref="EnumerateOverlapping"/> allocate nothing.
/// </para>
/// <para>
/// <b>What it wins and what it does not.</b> The write is the reason to use it: at 100,000 ranges an assignment
/// measures 8.8x a sorted <see cref="List{T}"/> patched in place, and the gap grows with the map, since the
/// list's assignment is an <c>O(n)</c> memmove. Every read loses to that list's binary search — a lookup by
/// 1.5x, a window walk by about 3x — and at 1,000 ranges the list wins the write too. A map built once and then
/// only read belongs in a sorted array.
/// </para>
/// <para>
/// A <c>null</c> key is legal wherever <typeparamref name="TComparer"/> orders it, as in
/// <see cref="BTreeDictionary{TKey, TValue, TComparer}"/>. This type is not thread-safe; concurrent callers
/// must synchronize externally.
/// </para>
/// </remarks>
public class RangeMap<TKey, TValue, TComparer> : IReadOnlyCollection<Interval<TKey, TValue>>
    where TComparer : struct, IComparer<TKey>
{
    // One stored range: its inclusive start and its value. The exclusive end is the tree key.
    private readonly struct Segment
    {
        internal readonly TKey Start;
        internal readonly TValue? Value;

        internal Segment(TKey start, TValue? value)
        {
            Start = start;
            Value = value;
        }
    }

    private readonly BTreeDictionary<TKey, Segment, TComparer> _segments;
    private readonly IEqualityComparer<TValue> _valueComparer;

    // Not readonly: a readonly field of a struct type is defensively copied on every member call, which would
    // put a copy on every comparison. Matches BTreeDictionary's comparer field.
    private TComparer _comparer;

    // Bumped by every write that changes what the map holds, and by nothing else. The tree has a version of
    // its own, but checking this one first is what lets an enumerator report the map, not its storage.
    private int _version;

    /// <summary>Initializes a new, empty map ordered by <c>default(TComparer)</c>.</summary>
    public RangeMap()
        : this(default(TComparer), null)
    {
    }

    /// <summary>
    /// Initializes a new, empty map ordered by <paramref name="comparer"/>. Use this overload when
    /// <typeparamref name="TComparer"/> carries state.
    /// </summary>
    /// <param name="comparer">The comparer instance defining the key order.</param>
    public RangeMap(TComparer comparer)
        : this(comparer, null)
    {
    }

    /// <summary>
    /// Initializes a new, empty map ordered by <paramref name="comparer"/> that decides which adjacent ranges
    /// merge by <paramref name="valueComparer"/>.
    /// </summary>
    /// <param name="comparer">The comparer instance defining the key order.</param>
    /// <param name="valueComparer">
    /// The equality that decides when two adjacent ranges carry the same value and merge, and when an
    /// assignment is a no-op; <c>null</c> for <see cref="EqualityComparer{T}.Default"/>.
    /// </param>
    public RangeMap(TComparer comparer, IEqualityComparer<TValue>? valueComparer)
    {
        _comparer = comparer;
        _segments = new BTreeDictionary<TKey, Segment, TComparer>(comparer);
        _valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
    }

    /// <summary>
    /// Initializes a new map ordered by <c>default(TComparer)</c> and fills it by assigning each of
    /// <paramref name="source"/>'s intervals in turn, so a later interval overwrites an earlier one where they
    /// overlap.
    /// </summary>
    /// <param name="source">The assignments to apply, in order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">An interval's end orders before its start.</exception>
    public RangeMap(IEnumerable<Interval<TKey, TValue>> source)
        : this(source, default)
    {
    }

    /// <summary>
    /// Initializes a new map ordered by <paramref name="comparer"/> and fills it by assigning each of
    /// <paramref name="source"/>'s intervals in turn, so a later interval overwrites an earlier one where they
    /// overlap.
    /// </summary>
    /// <param name="source">The assignments to apply, in order.</param>
    /// <param name="comparer">The comparer instance defining the key order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">An interval's end orders before its start.</exception>
    public RangeMap(IEnumerable<Interval<TKey, TValue>> source, TComparer comparer)
        : this(comparer, null)
    {
        ArgumentNullException.ThrowIfNull(source);

        foreach (Interval<TKey, TValue> interval in source)
        {
            if (_comparer.Compare(interval.Start, interval.End) > 0)
                throw new ArgumentException("An interval's end must not order before its start.", nameof(source));

            Set(interval.Start, interval.End, interval.Value);
        }
    }

    /// <summary>Gets the comparer that defines this map's key order.</summary>
    public TComparer Comparer => _comparer;

    /// <summary>
    /// Gets the number of stored ranges — maximal runs of keys mapped to one value, which is fewer than the
    /// assignments made whenever adjacent equal values merged.
    /// </summary>
    public int Count => _segments.Count;

    /// <summary>Gets the value mapped at <paramref name="key"/>, in <c>O(log n)</c>.</summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The value of the range containing <paramref name="key"/>.</returns>
    /// <exception cref="KeyNotFoundException">No stored range contains <paramref name="key"/>.</exception>
    public TValue this[TKey key]
    {
        get
        {
            if (!TryGetValue(key, out TValue? value))
                throw new KeyNotFoundException($"No range in the map contains the key '{key}'.");

            return value!;
        }
    }

    /// <summary>Looks up the value mapped at <paramref name="key"/>, in <c>O(log n)</c>.</summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">The value of the range containing the key, or <c>default</c> when none does.</param>
    /// <returns><c>true</c> if a stored range contains <paramref name="key"/>.</returns>
    public bool TryGetValue(TKey key, out TValue? value)
    {
        if (TryFindContaining(key, out KeyValuePair<TKey, Segment> entry))
        {
            value = entry.Value.Value;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Determines whether any stored range contains <paramref name="key"/>, in <c>O(log n)</c>.</summary>
    /// <param name="key">The key to look for.</param>
    /// <returns><c>true</c> if <paramref name="key"/> is mapped.</returns>
    public bool ContainsKey(TKey key) => TryFindContaining(key, out _);

    /// <summary>
    /// Gets the whole stored range that contains <paramref name="key"/> — its bounds and its value — in
    /// <c>O(log n)</c>. Because adjacent equal values are always merged, it is the maximal run of keys that
    /// map to that value.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="range">The containing range, or <c>default</c> when no range contains the key.</param>
    /// <returns><c>true</c> if a stored range contains <paramref name="key"/>.</returns>
    public bool TryGetRange(TKey key, out Interval<TKey, TValue> range)
    {
        if (TryFindContaining(key, out KeyValuePair<TKey, Segment> entry))
        {
            range = new Interval<TKey, TValue>(entry.Value.Start, entry.Key, entry.Value.Value);
            return true;
        }

        range = default;
        return false;
    }

    /// <summary>
    /// Maps every key in <c>[start, end)</c> to <paramref name="value"/>, overwriting whatever that range
    /// held. A stored range that straddles either edge is split and keeps its value outside the assignment,
    /// and the result is merged with an adjacent range carrying an equal value. Costs
    /// <c>O((k + 1) log n)</c> for the <c>k</c> stored ranges overwritten.
    /// </summary>
    /// <param name="start">The inclusive lower bound of the range.</param>
    /// <param name="end">The exclusive upper bound of the range.</param>
    /// <param name="value">The value to map over the range.</param>
    /// <remarks>
    /// An empty range (<paramref name="start"/> equal to <paramref name="end"/>) covers no key, so assigning
    /// it changes nothing. Neither does assigning a value to a range one stored range already covers with an
    /// equal value; in both cases the map, and every active enumerator, is left untouched.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="end"/> orders before <paramref name="start"/>.</exception>
    public void Set(TKey start, TKey end, TValue? value)
    {
        if (IsEmptyRange(start, end))
            return;

        // The only no-op a non-empty assignment can be: in canonical form, a range already mapped to an equal
        // value throughout is necessarily a single stored range, since two adjacent equal ones would have been
        // merged.
        if (_segments.TryGetUpperBound(start, out KeyValuePair<TKey, Segment> covering)
            && _comparer.Compare(covering.Value.Start, start) <= 0
            && _comparer.Compare(covering.Key, end) >= 0
            && _valueComparer.Equals(covering.Value.Value, value))
        {
            return;
        }

        ClearRange(start, end);

        // Merge left with a range ending exactly at start — it is keyed by start — when its value is equal.
        TKey mergedStart = start;
        if (_segments.TryGetValue(start, out Segment left) && _valueComparer.Equals(left.Value, value))
        {
            _segments.Remove(start);
            mergedStart = left.Start;
        }

        // Merge right with the range beginning exactly at end. Its key is its own end, which the merged range
        // shares, so the merge is an in-place overwrite rather than a remove and an insert.
        if (_segments.TryGetUpperBound(end, out KeyValuePair<TKey, Segment> right)
            && _comparer.Compare(right.Value.Start, end) == 0
            && _valueComparer.Equals(right.Value.Value, value))
        {
            _segments[right.Key] = new Segment(mergedStart, value);
        }
        else
        {
            _segments.Add(end, new Segment(mergedStart, value));
        }

        _version++;
    }

    /// <summary>
    /// Unmaps every key in <c>[start, end)</c>. A stored range that straddles either edge is split and keeps
    /// its value outside the removed range. Costs <c>O((k + 1) log n)</c> for the <c>k</c> stored ranges it
    /// touches.
    /// </summary>
    /// <param name="start">The inclusive lower bound of the range.</param>
    /// <param name="end">The exclusive upper bound of the range.</param>
    /// <returns>
    /// <c>true</c> if any key in the range was mapped; <c>false</c> if the range held nothing, in which case
    /// the map and every active enumerator are left untouched.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="end"/> orders before <paramref name="start"/>.</exception>
    public bool Remove(TKey start, TKey end)
    {
        if (IsEmptyRange(start, end) || !ClearRange(start, end))
            return false;

        _version++;
        return true;
    }

    /// <summary>
    /// Determines whether any key in <c>[start, end)</c> is mapped, in <c>O(log n)</c>. An empty range
    /// overlaps nothing.
    /// </summary>
    /// <param name="start">The inclusive lower bound of the range.</param>
    /// <param name="end">The exclusive upper bound of the range.</param>
    /// <returns><c>true</c> if at least one stored range overlaps <c>[start, end)</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="end"/> orders before <paramref name="start"/>.</exception>
    public bool Overlaps(TKey start, TKey end)
    {
        if (IsEmptyRange(start, end))
            return false;

        // The first range ending after start is the only candidate: every later one begins later still.
        return _segments.TryGetUpperBound(start, out KeyValuePair<TKey, Segment> first)
            && _comparer.Compare(first.Value.Start, end) < 0;
    }

    /// <summary>
    /// Enumerates, in ascending order, every stored range that overlaps <c>[start, end)</c>. The ranges are
    /// reported whole — a range straddling either edge is not clipped to the window — so the first may begin
    /// before <paramref name="start"/> and the last may end after <paramref name="end"/>. The scan seeks in
    /// <c>O(log n)</c> and then walks in order, so it costs <c>O(log n + k)</c> for <c>k</c> results.
    /// </summary>
    /// <param name="start">The inclusive lower bound of the window.</param>
    /// <param name="end">The exclusive upper bound of the window.</param>
    /// <returns>An allocation-free enumerable over the overlapping ranges.</returns>
    /// <exception cref="ArgumentException"><paramref name="end"/> orders before <paramref name="start"/>.</exception>
    public OverlapEnumerable EnumerateOverlapping(TKey start, TKey end)
    {
        ValidateRange(start, end);
        return new OverlapEnumerable(this, start, end);
    }

    /// <summary>Removes every range. Clearing an empty map is a no-op.</summary>
    public void Clear()
    {
        if (_segments.Count == 0)
            return;

        _segments.Clear();
        _version++;
    }

    /// <summary>
    /// Returns a struct enumerator over the stored ranges in ascending order. Iterating it via <c>foreach</c>
    /// allocates nothing.
    /// </summary>
    /// <returns>A struct enumerator over this map.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<Interval<TKey, TValue>> IEnumerable<Interval<TKey, TValue>>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // The range containing key is the first one ending after it, provided it also starts at or before it.
    private bool TryFindContaining(TKey key, out KeyValuePair<TKey, Segment> entry) =>
        _segments.TryGetUpperBound(key, out entry) && _comparer.Compare(entry.Value.Start, key) <= 0;

    private bool IsEmptyRange(TKey start, TKey end)
    {
        int order = _comparer.Compare(start, end);
        if (order > 0)
            ThrowEndBeforeStart();

        return order == 0;
    }

    private void ValidateRange(TKey start, TKey end)
    {
        if (_comparer.Compare(start, end) > 0)
            ThrowEndBeforeStart();
    }

    private static void ThrowEndBeforeStart() =>
        throw new ArgumentException("end must not order before start.", "end");

    // Unmaps the non-empty range [start, end), splitting a straddler at either edge, and reports whether
    // anything was mapped there. Does not bump the version: Set calls it as one step of a larger write.
    private bool ClearRange(TKey start, TKey end)
    {
        bool changed = false;

        // Each pass takes the first range ending after start. Removing it (or trimming it to end at start)
        // makes the next overlapping range the first one again, so the loop never needs a cursor that a
        // structural edit could invalidate.
        while (_segments.TryGetUpperBound(start, out KeyValuePair<TKey, Segment> entry))
        {
            TKey segmentEnd = entry.Key;
            Segment segment = entry.Value;
            if (_comparer.Compare(segment.Start, end) >= 0)
                break;

            changed = true;
            bool keepsLeft = _comparer.Compare(segment.Start, start) < 0;
            int endOrder = _comparer.Compare(segmentEnd, end);

            if (endOrder > 0)
            {
                // It runs past end, so its key survives and only its start moves up to end. Nothing later can
                // overlap.
                _segments[segmentEnd] = new Segment(end, segment.Value);
                if (keepsLeft)
                    _segments.Add(start, new Segment(segment.Start, segment.Value));

                break;
            }

            _segments.Remove(segmentEnd);
            if (keepsLeft)
                _segments.Add(start, new Segment(segment.Start, segment.Value));

            // Ending exactly at end, it was the last range the window could reach.
            if (endOrder == 0)
                break;
        }

        return changed;
    }

    /// <summary>
    /// A struct enumerator over a <see cref="RangeMap{TKey, TValue, TComparer}"/>'s stored ranges in ascending
    /// order. Iterating it via <c>foreach</c> allocates nothing.
    /// </summary>
    public struct Enumerator : IEnumerator<Interval<TKey, TValue>>
    {
        private readonly RangeMap<TKey, TValue, TComparer> _map;
        private readonly int _version;
        private BTreeDictionary<TKey, Segment, TComparer>.Enumerator _inner;
        private Interval<TKey, TValue> _current;

        internal Enumerator(RangeMap<TKey, TValue, TComparer> map)
        {
            _map = map;
            _version = map._version;
            _inner = map._segments.GetEnumerator();
            _current = default;
        }

        /// <summary>Gets the range at the current position of the enumerator.</summary>
        public readonly Interval<TKey, TValue> Current => _current;

        readonly object IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next range.</summary>
        /// <returns><c>true</c> if there is a next range; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The map was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (_version != _map._version)
                ThrowModified();

            if (!_inner.MoveNext())
            {
                _current = default;
                return false;
            }

            KeyValuePair<TKey, Segment> entry = _inner.Current;
            _current = new Interval<TKey, TValue>(entry.Value.Start, entry.Key, entry.Value.Value);
            return true;
        }

        /// <summary>Resets the enumerator to before the first range.</summary>
        /// <exception cref="InvalidOperationException">The map was modified during enumeration.</exception>
        public void Reset()
        {
            if (_version != _map._version)
                ThrowModified();

            _inner.Reset();
            _current = default;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }

    /// <summary>
    /// The result of <see cref="EnumerateOverlapping"/>: an allocation-free view over the stored ranges that
    /// overlap one window, in ascending order.
    /// </summary>
    public readonly struct OverlapEnumerable : IEnumerable<Interval<TKey, TValue>>
    {
        private readonly RangeMap<TKey, TValue, TComparer> _map;
        private readonly TKey _start;
        private readonly TKey _end;

        internal OverlapEnumerable(RangeMap<TKey, TValue, TComparer> map, TKey start, TKey end)
        {
            _map = map;
            _start = start;
            _end = end;
        }

        /// <summary>Returns a struct enumerator over the overlapping ranges.</summary>
        /// <returns>A struct enumerator over the ranges that overlap the window.</returns>
        public OverlapEnumerator GetEnumerator() => new OverlapEnumerator(_map, _start, _end);

        IEnumerator<Interval<TKey, TValue>> IEnumerable<Interval<TKey, TValue>>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A struct enumerator over the stored ranges of a <see cref="RangeMap{TKey, TValue, TComparer}"/> that
    /// overlap one window, in ascending order.
    /// </summary>
    public struct OverlapEnumerator : IEnumerator<Interval<TKey, TValue>>
    {
        // The ranges overlapping [start, end) are exactly those ending after start and beginning before end. One
        // seek to the first end key at or past start, then an in-order walk that skips a range ending exactly at
        // start (it stops where the window begins) and closes at the first range beginning at or past end —
        // ascending by end means ascending by start, so every later range begins later still.
        private readonly RangeMap<TKey, TValue, TComparer> _map;
        private readonly TKey _start;
        private readonly TKey _end;
        private readonly int _version;

        // An empty window overlaps nothing — without this the walk would report the range straddling the
        // window's single point, which Overlaps denies.
        private readonly bool _emptyWindow;
        private BTreeDictionary<TKey, Segment, TComparer>.RangeEnumerator _inner;
        private bool _done;
        private Interval<TKey, TValue> _current;

        internal OverlapEnumerator(RangeMap<TKey, TValue, TComparer> map, TKey start, TKey end)
        {
            _map = map;
            _start = start;
            _end = end;
            _version = map._version;
            _emptyWindow = map._comparer.Compare(start, end) == 0;
            _inner = map._segments.EnumerateFrom(start);
            _done = _emptyWindow;
            _current = default;
        }

        /// <summary>Gets the range at the current position of the enumerator.</summary>
        public readonly Interval<TKey, TValue> Current => _current;

        readonly object IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next overlapping range.</summary>
        /// <returns><c>true</c> if there is a next range; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The map was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (_version != _map._version)
                ThrowModified();

            while (!_done && _inner.MoveNext())
            {
                KeyValuePair<TKey, Segment> entry = _inner.Current;
                if (_map._comparer.Compare(entry.Value.Start, _end) >= 0)
                    break;

                if (_map._comparer.Compare(entry.Key, _start) == 0)
                    continue;

                _current = new Interval<TKey, TValue>(entry.Value.Start, entry.Key, entry.Value.Value);
                return true;
            }

            _done = true;
            _current = default;
            return false;
        }

        /// <summary>Resets the enumerator to before the first overlapping range.</summary>
        /// <exception cref="InvalidOperationException">The map was modified during enumeration.</exception>
        public void Reset()
        {
            if (_version != _map._version)
                ThrowModified();

            _inner.Reset();
            _done = _emptyWindow;
            _current = default;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }

    private static void ThrowModified() =>
        throw new InvalidOperationException("The map was modified during enumeration.");
}
