using System.Collections;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// An <b>exact, compressed set of 32-bit integers</b> that partitions the value space into
/// 65,536-value chunks and stores each chunk in the form that suits its density: a sorted
/// <c>ushort[]</c> while it is sparse, a 1024-word bitmap once it is dense. A third, run-length form
/// is available for clustered data, but it is <b>opt-in</b> — only <see cref="Optimize"/> and
/// <see cref="AddRange"/> produce it, never an ordinary <see cref="TryAdd(int)"/> or
/// <see cref="Remove(int)"/>. It fills a BCL gap: .NET ships no compressed integer set, so the alternatives are
/// <see cref="HashSet{T}"/> (~32&#8211;48 bytes per element and one hash probe per element for set
/// algebra), <see cref="BitArray"/> (<c>O(universe)</c> memory), or <see cref="SortedSet{T}"/> (a
/// red-black tree with a pointer chase per node).
/// </summary>
/// <remarks>
/// <para>
/// <b>Two things to know before reaching for this type.</b>
/// </para>
/// <list type="number">
/// <item><description>
/// <b>This is not a Roaring codec.</b> The layout is Roaring-shaped and a maintained pure-C#
/// Roaring implementation already exists (<c>Equativ.RoaringBitmaps</c>). What this type offers over
/// it is integration rather than novelty: full mutability, verified Native AOT and trim
/// compatibility, published benchmarks against
/// <see cref="HashSet{T}"/> on the project dashboard, and membership of the same
/// <see cref="BitSet"/> / <see cref="SparseSet"/> / <see cref="IntSet{THasher}"/> family and shared
/// test suites.
/// </description></item>
/// <item><description>
/// <b>There is no portable serialization format.</b> Celerity does not ship serializers, so this
/// type cannot read or write the portable Roaring format — the Lucene / Druid / Spark posting-list
/// interop that is the single most common reason to reach for Roaring is <em>not</em> available
/// here. The type is deliberately named for what it is, a compressed integer set, and is worth using
/// for its in-process memory and set-algebra behaviour alone.
/// </description></item>
/// </list>
/// <para>
/// <b>The documented BCL-beating workload</b> is set algebra over large, sparse integer sets:
/// intersecting or unioning sets of ~1M values drawn from a ~100M-value space — inverted-index
/// posting lists, bitmap analytics, column-store row-id sets, cohort intersection. Two things make
/// it win. Inside a chunk the work is word- or merge-parallel rather than one hash probe per
/// element, and the sorted chunk index lets an entire 65,536-value range be skipped with a
/// <em>single</em> comparison when one side has nothing there — so the cost tracks the number of
/// populated chunks, not the number of elements. Memory drops roughly 10x against
/// <see cref="HashSet{T}"/> for the sparse case and far more for dense or clustered data, because a
/// dense region collapses to one bit per value and a clustered one to a pair of <c>ushort</c>s per
/// run.
/// </para>
/// <para>
/// <b>Enumeration is in ascending signed order.</b> Chunk keys are derived from the value with its
/// sign bit flipped, so the chunk index is sorted by signed value and the whole set enumerates
/// ascending from <see cref="int.MinValue"/> to <see cref="int.MaxValue"/> — something
/// <see cref="HashSet{T}"/> does not offer. The full 32-bit range is storable, negatives included.
/// </para>
/// <para>
/// <b>Compression is explicit.</b> Run containers — the clustered form — are produced by
/// <see cref="Optimize"/> and by <see cref="AddRange"/>, never speculatively on a single
/// <see cref="TryAdd(int)"/>. Deciding on every insert would cost more than it saves, so the type
/// follows the same "compress once it has settled" contract as Roaring's own <c>runOptimize</c>.
/// A single-element <see cref="TryAdd(int)"/> or <see cref="Remove(int)"/> that lands in a
/// run-encoded chunk expands that chunk back to its natural form first; call <see cref="Optimize"/>
/// again after a burst of mutation. <see cref="MemoryUsageInBytes"/> reports what the current
/// representation costs.
/// </para>
/// <para>
/// <b>Counting.</b> The set can hold every one of the 2^32 distinct <see cref="int"/> values, which
/// overflows the <see cref="int"/> that <see cref="ICollection{T}.Count"/> must return.
/// <see cref="Cardinality"/> is therefore the always-correct count, and <see cref="Count"/> throws
/// <see cref="OverflowException"/> in the one case it cannot answer.
/// </para>
/// <para>
/// The type is single-threaded. It implements the full <see cref="ISet{T}"/> and
/// <see cref="IReadOnlySet{T}"/> surface with BCL <see cref="HashSet{T}"/> semantics, and every
/// set-algebra operation takes a chunk-wise fast path when the other side is also a
/// <see cref="CompressedIntSet"/>, falling back to an element-at-a-time implementation for any
/// other <see cref="IEnumerable{T}"/>. Fallbacks that depend on the set's own size read the
/// <see langword="long"/> <see cref="Cardinality"/>, so they keep working past
/// <see cref="int.MaxValue"/> elements.
/// </para>
/// </remarks>
public sealed class CompressedIntSet : ISet<int>, IReadOnlySet<int>
{
    // The chunk index and every container live in the shared engine, which CompressedLongSet wraps
    // too. This type owns only the int ↔ (key, offset) mapping and the enumerator version.
    private CompressedChunkStore _store = new();
    private int _version;

    /// <summary>
    /// Initializes a new, empty <see cref="CompressedIntSet"/>. No chunk storage is allocated until
    /// the first value is added.
    /// </summary>
    public CompressedIntSet()
    {
    }

    /// <summary>
    /// Initializes a new <see cref="CompressedIntSet"/> containing the distinct values copied from
    /// the specified <paramref name="source"/>.
    /// </summary>
    /// <param name="source">
    /// The values to copy. Duplicates are silently deduplicated, matching BCL
    /// <see cref="HashSet{T}"/> semantics.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public CompressedIntSet(IEnumerable<int> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (int item in source)
            TryAdd(item);
    }

    /// <summary>
    /// Gets the number of elements in the set, as a <see cref="long"/>. This is the always-correct
    /// count: unlike <see cref="Count"/>, it can represent a set holding every one of the 2^32
    /// distinct <see cref="int"/> values.
    /// </summary>
    public long Cardinality => _store.Cardinality;

    /// <summary>
    /// Gets the number of elements in the set.
    /// </summary>
    /// <exception cref="OverflowException">
    /// The set holds more than <see cref="int.MaxValue"/> elements, which only a
    /// <see cref="AddRange"/> over a very wide range can produce. Use <see cref="Cardinality"/>,
    /// which is a <see cref="long"/> and always correct.
    /// </exception>
    public int Count => _store.Cardinality <= int.MaxValue
        ? (int)_store.Cardinality
        : throw new OverflowException($"The set holds {_store.Cardinality} elements, which does not fit an Int32. Use {nameof(Cardinality)} instead.");

    /// <summary>
    /// Gets the approximate memory the current representation occupies, in bytes: the chunk index
    /// plus each chunk's container payload. Object headers are not counted, so this is a measure of
    /// how well the data compressed rather than an exact managed-heap figure. It changes as
    /// containers switch form, and <see cref="Optimize"/> is what drives it down after a bulk load.
    /// The index is counted at its full capacity rather than its used length, so this stays non-zero
    /// after a <see cref="Clear"/> until <see cref="Optimize"/> trims the index away.
    /// </summary>
    public long MemoryUsageInBytes => _store.MemoryUsageInBytes;

    /// <summary>
    /// Adds the specified value to the set.
    /// </summary>
    /// <param name="item">The value to add.</param>
    /// <exception cref="ArgumentException"><paramref name="item"/> already exists in the set.</exception>
    public void Add(int item)
    {
        if (!TryAdd(item))
            throw new ArgumentException($"The element {item} already exists in the set.", nameof(item));
    }

    /// <summary>
    /// Attempts to add the specified value to the set.
    /// </summary>
    /// <param name="item">The value to add.</param>
    /// <returns>
    /// <c>true</c> if the value was added; <c>false</c> if it was already present (the set is
    /// unchanged).
    /// </returns>
    public bool TryAdd(int item)
    {
        if (!_store.Add(ChunkKey(item), Low(item)))
            return false;

        _version++;
        return true;
    }

    /// <summary>
    /// Adds every value in the inclusive range <c>[start, endInclusive]</c> to the set.
    /// </summary>
    /// <param name="start">The first value of the range.</param>
    /// <param name="endInclusive">The last value of the range; must not be less than <paramref name="start"/>.</param>
    /// <returns>The number of values the call actually added (values already present are not counted).</returns>
    /// <remarks>
    /// A range that lands in a chunk the set does not yet touch is stored as a single run pair —
    /// four bytes of payload, whatever the range's width (plus the array header, which
    /// <see cref="MemoryUsageInBytes"/> excludes too) — which is why this is the cheap way to build a
    /// clustered set. A range overlapping an existing chunk merges into it and the chunk is left in
    /// its natural form; call <see cref="Optimize"/> afterwards to re-compress those.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="endInclusive"/> is less than <paramref name="start"/>.</exception>
    public long AddRange(int start, int endInclusive)
    {
        if (endInclusive < start)
            throw new ArgumentOutOfRangeException(nameof(endInclusive), endInclusive, "The end of the range must not be less than the start.");

        // ChunkKey is monotone in the signed value, so the range spans a contiguous run of keys.
        // As in CompressedLongSet: the version moves whenever the contents did, even if an allocation
        // fails part-way through the range.
        long before = _store.Cardinality;
        try
        {
            return _store.AddRange(ChunkKey(start), Low(start), ChunkKey(endInclusive), Low(endInclusive));
        }
        finally
        {
            if (_store.Cardinality != before)
                _version++;
        }
    }

    /// <summary>
    /// Determines whether the set contains the specified value.
    /// </summary>
    /// <param name="item">The value to locate.</param>
    /// <returns><c>true</c> if the value is present.</returns>
    public bool Contains(int item)
    {
        return _store.Contains(ChunkKey(item), Low(item));
    }

    /// <summary>
    /// Removes the specified value from the set.
    /// </summary>
    /// <param name="item">The value to remove.</param>
    /// <returns><c>true</c> if the value was removed; <c>false</c> if it was not present.</returns>
    /// <remarks>
    /// Removal never shrinks a container's representation — a chunk that grew into a bitmap stays a
    /// bitmap until <see cref="Optimize"/> or a bulk set operation rewrites it — so a remove costs
    /// the same whatever the chunk's density. A chunk emptied by its last removal is dropped.
    /// </remarks>
    public bool Remove(int item)
    {
        if (!_store.Remove(ChunkKey(item), Low(item)))
            return false;

        _version++;
        return true;
    }

    /// <summary>
    /// Removes all elements from the set, dropping every container payload. The chunk index keeps
    /// its capacity so the set can be refilled without regrowing it, which means
    /// <see cref="MemoryUsageInBytes"/> does not fall to zero — call <see cref="Optimize"/>
    /// afterwards to hand that back. A <see cref="Clear"/> on an already-empty set changes nothing
    /// and leaves active enumerators valid.
    /// </summary>
    public void Clear()
    {
        if (_store.Clear())
            _version++;
    }

    /// <summary>
    /// Re-encodes every chunk in whichever container form is smallest for the data it now holds —
    /// including the run form, which no other operation produces — and trims the chunk index and
    /// each array container to their exact sizes.
    /// </summary>
    /// <remarks>
    /// This is the "the data has settled" call: it costs a linear pass over the set and pays for
    /// itself on clustered data, where a chunk of consecutive values collapses from thousands of
    /// entries to a handful of run pairs. Watch <see cref="MemoryUsageInBytes"/> across the call to
    /// see what it recovered. Optimizing is purely a representation change — no element is added or
    /// removed, and active enumerators stay valid.
    /// </remarks>
    public void Optimize()
    {
        _store.Optimize();
    }

    /// <summary>
    /// Counts the elements this set and <paramref name="other"/> have in common, without building
    /// the intersection.
    /// </summary>
    /// <param name="other">The set to intersect with.</param>
    /// <returns>The number of values present in both sets.</returns>
    /// <remarks>
    /// Allocation-free, and it skips a whole 65,536-value range with a single key comparison
    /// wherever one side has nothing — which is why counting an overlap is far cheaper than
    /// materializing it.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public long IntersectCount(CompressedIntSet other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (ReferenceEquals(this, other))
            return _store.Cardinality;

        return _store.IntersectCount(ref other._store);
    }

    /// <summary>
    /// Returns an allocation-free enumerator that yields every element of the set in ascending
    /// signed order.
    /// </summary>
    /// <returns>A struct enumerator over this set.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ── ISet<int> / IReadOnlySet<int> surface ─────────────────────────────────
    // Each operation takes the chunk-wise path when `other` is a CompressedIntSet — the case the
    // type exists for — and otherwise handles an arbitrary IEnumerable<int> with BCL HashSet<int>
    // semantics.
    //
    // The two fallbacks that need nothing from Count (SymmetricExceptWith) or only stream `other`
    // against Contains (IsSupersetOf) route through the shared SetOperations helper the rest of the
    // set family uses. The other six do not, and that is deliberate rather than drift: SetOperations
    // is written against ICollection<T>.Count, which on this type throws once the cardinality passes
    // int.MaxValue — a state AddRange reaches cheaply. Every one of those six answers is perfectly
    // computable there, so each compares against the long Cardinality instead and stays usable at the
    // full 32-bit cardinality the type supports.

    /// <summary>
    /// Modifies the set to contain all elements present in itself, in <paramref name="other"/>, or
    /// in both.
    /// </summary>
    /// <param name="other">The collection to union into this set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void UnionWith(IEnumerable<int> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (ReferenceEquals(this, other))
            return;

        if (other is CompressedIntSet o)
        {
            if (_store.UnionWith(ref o._store))
                _version++;

            return;
        }

        foreach (int item in other)
            TryAdd(item);
    }

    /// <summary>
    /// Modifies the set to contain only elements that are also present in <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to intersect with this set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void IntersectWith(IEnumerable<int> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (_store.Cardinality == 0 || ReferenceEquals(this, other))
            return;

        if (other is CompressedIntSet o)
        {
            if (_store.IntersectWith(ref o._store))
                _version++;

            return;
        }

        // Not SetOperations.IntersectWith: that snapshots this set into a List<T> and reads its
        // Count, neither of which a set holding more than int.MaxValue elements can do. Building the
        // survivors from `other` instead is bounded by `other`, so it works at any cardinality.
        var survivors = new CompressedIntSet();
        foreach (int item in other)
        {
            if (Contains(item))
                survivors.TryAdd(item);
        }

        if (_store.TakeOver(ref survivors._store))
            _version++;
    }

    /// <summary>
    /// Removes every element in <paramref name="other"/> from the set.
    /// </summary>
    /// <param name="other">The collection of elements to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void ExceptWith(IEnumerable<int> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (ReferenceEquals(this, other))
        {
            Clear();
            return;
        }

        if (_store.Cardinality == 0)
            return;

        if (other is CompressedIntSet o)
        {
            if (_store.ExceptWith(ref o._store))
                _version++;

            return;
        }

        foreach (int item in other)
            Remove(item);
    }

    /// <summary>
    /// Modifies the set to contain only elements present either in itself or in
    /// <paramref name="other"/>, but not both.
    /// </summary>
    /// <param name="other">The collection to apply the symmetric difference with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void SymmetricExceptWith(IEnumerable<int> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (ReferenceEquals(this, other))
        {
            Clear();
            return;
        }

        if (other is CompressedIntSet o)
        {
            if (_store.SymmetricExceptWith(ref o._store))
                _version++;

            return;
        }

        SetOperations.SymmetricExceptWith(this, other);
    }

    /// <summary>
    /// Determines whether the set is a subset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if every element of this set is in <paramref name="other"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSubsetOf(IEnumerable<int> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (_store.Cardinality == 0)
            return true; // the empty set is a subset of everything, so `other` is never enumerated

        if (other is CompressedIntSet o)
            return _store.Cardinality <= o._store.Cardinality && _store.IsSubsetOf(ref o._store);

        HashSet<int> materialized = new(other);
        return _store.Cardinality <= materialized.Count && AllElementsIn(materialized);
    }

    /// <summary>
    /// Determines whether the set is a proper (strict) subset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of this set is in <paramref name="other"/> and
    /// <paramref name="other"/> has at least one element this set does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSubsetOf(IEnumerable<int> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is CompressedIntSet o)
            return _store.Cardinality < o._store.Cardinality && _store.IsSubsetOf(ref o._store);

        HashSet<int> materialized = new(other);
        return _store.Cardinality < materialized.Count && AllElementsIn(materialized);
    }

    /// <summary>
    /// Determines whether the set is a superset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if every element of <paramref name="other"/> is in this set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSupersetOf(IEnumerable<int> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is CompressedIntSet o)
            return o._store.Cardinality <= _store.Cardinality && o._store.IsSubsetOf(ref _store);

        return SetOperations.IsSupersetOf(this, other);
    }

    /// <summary>
    /// Determines whether the set is a proper (strict) superset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of <paramref name="other"/> is in this set and this set has at
    /// least one element <paramref name="other"/> does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSupersetOf(IEnumerable<int> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is CompressedIntSet o)
            return o._store.Cardinality < _store.Cardinality && o._store.IsSubsetOf(ref _store);

        // An empty right-hand side: a proper superset iff the set is non-empty.
        if (other is ICollection<int> collection && collection.Count == 0)
            return _store.Cardinality > 0;

        HashSet<int> materialized = new(other);
        if (materialized.Count >= _store.Cardinality)
            return false;

        foreach (int item in materialized)
        {
            if (!Contains(item))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether the set and <paramref name="other"/> share at least one element.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two share any element.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool Overlaps(IEnumerable<int> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (_store.Cardinality == 0)
            return false;

        if (other is CompressedIntSet o)
            return _store.Overlaps(ref o._store);

        foreach (int item in other)
        {
            if (Contains(item))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Determines whether the set and <paramref name="other"/> contain the same distinct elements.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two contain exactly the same elements.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool SetEquals(IEnumerable<int> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is CompressedIntSet o)
            return _store.Cardinality == o._store.Cardinality && _store.IsSubsetOf(ref o._store);

        HashSet<int> materialized = new(other);
        return _store.Cardinality == materialized.Count && AllElementsIn(materialized);
    }

    /// <summary>
    /// Copies the elements of the set to the specified <paramref name="array"/>, starting at
    /// <paramref name="arrayIndex"/>, in ascending signed order.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="array"/> has insufficient space — which is also what a set holding more than
    /// <see cref="int.MaxValue"/> elements reports, since no <c>int[]</c> can hold it.
    /// </exception>
    public void CopyTo(int[] array, int arrayIndex)
    {
        // Guarded directly rather than through SetOperations.CopyTo for the same reason as the six
        // set operations above: that helper takes an int count, so passing it Count would evaluate —
        // and throw from — the overflow guard *before* any argument was validated, turning
        // CopyTo(null, 0) on a very large set into an OverflowException. CopyToGuard takes a long,
        // so passing _store.Cardinality keeps every argument validated before the count is read, and a
        // set too large for an int[] correctly reports insufficient space rather than overflowing.
        CopyToGuard.Validate(array, arrayIndex, _store.Cardinality, CopyToGuard.SetElementsMessage);

        int i = arrayIndex;
        foreach (int item in this)
            array[i++] = item;
    }

    // Adds the element, returning whether it was newly added (ISet<T> semantics) — the
    // non-throwing (on duplicates) counterpart of the public throw-on-duplicate Add(int).
    bool ISet<int>.Add(int item) => TryAdd(item);

    // ICollection<T>.Add must not throw on a duplicate (unlike the public Add(int)), so it maps to
    // the non-throwing TryAdd.
    void ICollection<int>.Add(int item) => TryAdd(item);

    bool ICollection<int>.IsReadOnly => false;

    // Whether every element of this set is in `other`. Each caller compares cardinalities first, so
    // this is never reached for a set too large to walk.
    private bool AllElementsIn(HashSet<int> other)
    {
        foreach (int item in this)
        {
            if (!other.Contains(item))
                return false;
        }

        return true;
    }

    // ── Value ↔ (chunk key, offset) ───────────────────────────────────────────
    // The sign bit is flipped into the key so that ordering the chunk index by the unsigned key
    // orders the set by the *signed* value: int.MinValue lands in key 0 and int.MaxValue in key
    // 0xFFFF. The low 16 bits are unaffected by the flip, so the offset is just a truncation.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ChunkKey(int value) => ((uint)value ^ 0x8000_0000u) >> 16;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort Low(int value) => (ushort)value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Compose(ulong key, ushort low) => (int)((((uint)key << 16) ^ 0x8000_0000u) | low);

    /// <summary>
    /// A struct enumerator over a <see cref="CompressedIntSet"/>, yielding every element in
    /// ascending signed order. Because it is a struct, iterating with <c>foreach</c> avoids the
    /// allocation a compiler-generated <c>IEnumerator&lt;int&gt;</c> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<int>
    {
        private readonly CompressedIntSet _set;
        private readonly int _version;
        private CompressedChunkStore.ContainerCursor _cursor;
        private int _chunkIndex;
        private int _current;

        internal Enumerator(CompressedIntSet set)
        {
            _set = set;
            _version = set._version;
            _chunkIndex = 0;
            _cursor = set._store.ChunkCount > 0 ? new CompressedChunkStore.ContainerCursor(in set._store.Chunks[0]) : default;
            _current = 0;
        }

        /// <summary>
        /// Gets the element at the current position of the enumerator.
        /// </summary>
        public int Current => _current;

        object IEnumerator.Current => _current;

        /// <summary>
        /// Advances the enumerator to the next element.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the enumerator advanced to a new element; <c>false</c> if it has passed
        /// the end of the set.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the set was modified since the enumerator was created.
        /// </exception>
        public bool MoveNext()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            while (_chunkIndex < _set._store.ChunkCount)
            {
                if (_cursor.MoveNext(out ushort low))
                {
                    _current = Compose(_set._store.Chunks[_chunkIndex].Key, low);
                    return true;
                }

                _chunkIndex++;
                if (_chunkIndex < _set._store.ChunkCount)
                    _cursor = new CompressedChunkStore.ContainerCursor(in _set._store.Chunks[_chunkIndex]);
            }

            _current = 0;
            return false;
        }

        /// <summary>
        /// Resets the enumerator to its initial position, before the first element.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the set was modified since the enumerator was created.
        /// </exception>
        public void Reset()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _chunkIndex = 0;
            _cursor = _set._store.ChunkCount > 0 ? new CompressedChunkStore.ContainerCursor(in _set._store.Chunks[0]) : default;
            _current = 0;
        }

        /// <summary>
        /// Releases any resources held by the enumerator. No-op for this type.
        /// </summary>
        public void Dispose() { }
    }
}
