using System.Collections;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// An <b>exact, compressed set of 64-bit integers</b> — the <see cref="long"/> sibling of
/// <see cref="CompressedIntSet"/>, with the same containers and the same API. The value space is
/// partitioned into 65,536-value chunks, each stored in the form that suits its density: a sorted
/// <c>ushort[]</c> while it is sparse, a 1024-word bitmap once it is dense, and (opt-in, through
/// <see cref="Optimize"/> and <see cref="AddRange"/>) run-length pairs for clustered data. It fills
/// a BCL gap: .NET ships no compressed integer set at any width, so the alternatives for 64-bit ids
/// are <see cref="HashSet{T}"/> (one entry and one bucket per element, and one hash probe per
/// element for set algebra) or <see cref="SortedSet{T}"/> (a red-black node per element).
/// </summary>
/// <remarks>
/// <para>
/// <b>When to use it rather than <see cref="CompressedIntSet"/>.</b> Only when the values do not
/// fit an <see cref="int"/> — database row ids past 2^31, 64-bit document or event ids, file
/// offsets. The containers and the set algebra are shared code, so per-chunk behaviour is identical;
/// the difference is the chunk index, whose keys here are the value's top 48 bits rather than its top
/// 16. A 32-bit set has at most 65,536 chunks; this one has one per populated 65,536-value window,
/// with no fixed ceiling.
/// </para>
/// <para>
/// <b>It compresses <em>clustered</em> 64-bit data, not uniformly random 64-bit data.</b> The win
/// comes from many values sharing a chunk. Values spread uniformly over the whole 64-bit space land
/// one per chunk, and each then costs a chunk-index entry plus a small array — more than a
/// <see cref="HashSet{T}"/> entry. Sequential ids, ids allocated in blocks, timestamps at a fixed
/// resolution and row ids in a range are the shapes this type is for; hashes and random GUID halves
/// are not. <see cref="MemoryUsageInBytes"/> reports what the data actually costs.
/// </para>
/// <para>
/// <b>The documented BCL-beating workload</b> is the same as <see cref="CompressedIntSet"/>'s, moved
/// past the 32-bit boundary: set algebra over large, sparse sets of ~1M values drawn from a
/// ~100M-value window — inverted-index posting lists, column-store row-id sets, cohort intersection.
/// Inside a chunk the work is word- or merge-parallel rather than one hash probe per element, and the
/// sorted chunk index skips an entire 65,536-value window with a <em>single</em> comparison when one
/// side has nothing there.
/// </para>
/// <para>
/// <b>Not a Roaring codec, and no serialization.</b> As with <see cref="CompressedIntSet"/>, the
/// layout is Roaring-shaped (the flat-index counterpart of Roaring's 64-bit variants), but the type
/// cannot read or write the portable Roaring format; Celerity does not ship serializers.
/// </para>
/// <para>
/// <b>Enumeration is in ascending signed order.</b> Chunk keys are derived from the value with its
/// sign bit flipped, so the chunk index is sorted by signed value and the whole set enumerates
/// ascending from <see cref="long.MinValue"/> to <see cref="long.MaxValue"/> — something
/// <see cref="HashSet{T}"/> does not offer. The full 64-bit range is addressable, negatives included.
/// </para>
/// <para>
/// <b>Compression is explicit.</b> Run containers are produced by <see cref="Optimize"/> and by
/// <see cref="AddRange"/>, never speculatively on a single <see cref="TryAdd(long)"/>. A
/// single-element <see cref="TryAdd(long)"/> or <see cref="Remove(long)"/> that lands in a
/// run-encoded chunk expands that chunk back to its natural form first; call <see cref="Optimize"/>
/// again after a burst of mutation.
/// </para>
/// <para>
/// <b>Counting.</b> A set built with <see cref="AddRange"/> can hold far more than
/// <see cref="int.MaxValue"/> values, which overflows the <see cref="int"/> that
/// <see cref="ICollection{T}.Count"/> must return. <see cref="Cardinality"/> is therefore the
/// always-correct count, and <see cref="Count"/> throws <see cref="OverflowException"/> in the case
/// it cannot answer. <see cref="Cardinality"/> itself cannot overflow: the chunk index is an array,
/// so at most <see cref="Array.MaxLength"/> × 65,536 values (about 1.4 × 10^14) are storable.
/// </para>
/// <para>
/// The type is single-threaded. It implements the full <see cref="ISet{T}"/> and
/// <see cref="IReadOnlySet{T}"/> surface with BCL <see cref="HashSet{T}"/> semantics, and every
/// set-algebra operation takes a chunk-wise fast path when the other side is also a
/// <see cref="CompressedLongSet"/>, falling back to an element-at-a-time implementation for any
/// other <see cref="IEnumerable{T}"/>. Fallbacks that depend on the set's own size read the
/// <see langword="long"/> <see cref="Cardinality"/>, so they keep working past
/// <see cref="int.MaxValue"/> elements.
/// </para>
/// </remarks>
public sealed class CompressedLongSet : ISet<long>, IReadOnlySet<long>
{
    // The chunk index and every container live in the shared engine, which CompressedIntSet wraps
    // too. This type owns only the long ↔ (key, offset) mapping and the enumerator version.
    private CompressedChunkStore _store = new();
    private int _version;

    /// <summary>
    /// Initializes a new, empty <see cref="CompressedLongSet"/>. No chunk storage is allocated until
    /// the first value is added.
    /// </summary>
    public CompressedLongSet()
    {
    }

    /// <summary>
    /// Initializes a new <see cref="CompressedLongSet"/> containing the distinct values copied from
    /// the specified <paramref name="source"/>.
    /// </summary>
    /// <param name="source">
    /// The values to copy. Duplicates are silently deduplicated, matching BCL
    /// <see cref="HashSet{T}"/> semantics.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public CompressedLongSet(IEnumerable<long> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (long item in source)
            TryAdd(item);
    }

    /// <summary>
    /// Gets the number of elements in the set, as a <see cref="long"/>. This is the always-correct
    /// count: unlike <see cref="Count"/>, it stays correct past <see cref="int.MaxValue"/>
    /// elements, up to the roughly 1.4 × 10^14 values the chunk index can address.
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
    public void Add(long item)
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
    public bool TryAdd(long item)
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
    /// <para>
    /// A range that lands in a chunk the set does not yet touch is stored as a single run pair —
    /// four bytes of payload, whatever the range's width (plus the array header, which
    /// <see cref="MemoryUsageInBytes"/> excludes too) — which is why this is the cheap way to build a
    /// clustered set. A range overlapping an existing chunk merges into it and the chunk is left in
    /// its natural form; call <see cref="Optimize"/> afterwards to re-compress those.
    /// </para>
    /// <para>
    /// Unlike the 32-bit <see cref="CompressedIntSet.AddRange"/>, the cost is not bounded by a
    /// constant: every 65,536-value chunk the range touches gets its own index entry, so a range one
    /// billion values wide creates about 15,000 chunks, and a range spanning more than
    /// <see cref="Array.MaxLength"/> chunks (about 1.4 × 10^14 values) can never be stored and is
    /// rejected up front, before the set is touched. Each new chunk is inserted into the sorted index on
    /// its own, so a wide range is cheapest added above the set's existing chunks (or into an empty
    /// set); added below many of them, every new chunk shifts those that follow.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="endInclusive"/> is less than <paramref name="start"/>, or the range spans more
    /// than <see cref="Array.MaxLength"/> chunks.
    /// </exception>
    public long AddRange(long start, long endInclusive)
    {
        if (endInclusive < start)
            throw new ArgumentOutOfRangeException(nameof(endInclusive), endInclusive, "The end of the range must not be less than the start.");

        // ChunkKey is monotone in the signed value, so the range spans a contiguous run of keys.
        ulong firstKey = ChunkKey(start), lastKey = ChunkKey(endInclusive);
        if (lastKey - firstKey >= (ulong)Array.MaxLength)
            throw new ArgumentOutOfRangeException(nameof(endInclusive), endInclusive, $"The range spans {lastKey - firstKey + 1} chunks of 65,536 values; at most {Array.MaxLength} chunks can be stored.");

        // The store keeps Cardinality in step chunk by chunk, so if a wide range runs out of memory
        // part-way, the version still moves for the chunks that did land and live enumerators fail
        // fast instead of reading a half-built index.
        long before = _store.Cardinality;
        try
        {
            return _store.AddRange(firstKey, Low(start), lastKey, Low(endInclusive));
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
    public bool Contains(long item)
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
    public bool Remove(long item)
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
    public long IntersectCount(CompressedLongSet other)
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

    IEnumerator<long> IEnumerable<long>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ── ISet<long> / IReadOnlySet<long> surface ───────────────────────────────
    // Each operation takes the chunk-wise path when `other` is a CompressedLongSet — the case the
    // type exists for — and otherwise handles an arbitrary IEnumerable<long> with BCL HashSet<long>
    // semantics.
    //
    // The two fallbacks that need nothing from Count (SymmetricExceptWith) or only stream `other`
    // against Contains (IsSupersetOf) route through the shared SetOperations helper the rest of the
    // set family uses. The other six do not, and that is deliberate rather than drift: SetOperations
    // is written against ICollection<T>.Count, which on this type throws once the cardinality passes
    // int.MaxValue — a state AddRange reaches cheaply. Every one of those six answers is perfectly
    // computable there, so each compares against the long Cardinality instead and stays usable at
    // any cardinality the type can hold.

    /// <summary>
    /// Modifies the set to contain all elements present in itself, in <paramref name="other"/>, or
    /// in both.
    /// </summary>
    /// <param name="other">The collection to union into this set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void UnionWith(IEnumerable<long> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (ReferenceEquals(this, other))
            return;

        if (other is CompressedLongSet o)
        {
            if (_store.UnionWith(ref o._store))
                _version++;

            return;
        }

        foreach (long item in other)
            TryAdd(item);
    }

    /// <summary>
    /// Modifies the set to contain only elements that are also present in <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to intersect with this set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void IntersectWith(IEnumerable<long> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (_store.Cardinality == 0 || ReferenceEquals(this, other))
            return;

        if (other is CompressedLongSet o)
        {
            if (_store.IntersectWith(ref o._store))
                _version++;

            return;
        }

        // Not SetOperations.IntersectWith: that snapshots this set into a List<T> and reads its
        // Count, neither of which a set holding more than int.MaxValue elements can do. Building the
        // survivors from `other` instead is bounded by `other`, so it works at any cardinality.
        var survivors = new CompressedLongSet();
        foreach (long item in other)
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
    public void ExceptWith(IEnumerable<long> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (ReferenceEquals(this, other))
        {
            Clear();
            return;
        }

        if (_store.Cardinality == 0)
            return;

        if (other is CompressedLongSet o)
        {
            if (_store.ExceptWith(ref o._store))
                _version++;

            return;
        }

        foreach (long item in other)
            Remove(item);
    }

    /// <summary>
    /// Modifies the set to contain only elements present either in itself or in
    /// <paramref name="other"/>, but not both.
    /// </summary>
    /// <param name="other">The collection to apply the symmetric difference with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void SymmetricExceptWith(IEnumerable<long> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (ReferenceEquals(this, other))
        {
            Clear();
            return;
        }

        if (other is CompressedLongSet o)
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
    public bool IsSubsetOf(IEnumerable<long> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (_store.Cardinality == 0)
            return true; // the empty set is a subset of everything, so `other` is never enumerated

        if (other is CompressedLongSet o)
            return _store.Cardinality <= o._store.Cardinality && _store.IsSubsetOf(ref o._store);

        HashSet<long> materialized = new(other);
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
    public bool IsProperSubsetOf(IEnumerable<long> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is CompressedLongSet o)
            return _store.Cardinality < o._store.Cardinality && _store.IsSubsetOf(ref o._store);

        HashSet<long> materialized = new(other);
        return _store.Cardinality < materialized.Count && AllElementsIn(materialized);
    }

    /// <summary>
    /// Determines whether the set is a superset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if every element of <paramref name="other"/> is in this set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSupersetOf(IEnumerable<long> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is CompressedLongSet o)
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
    public bool IsProperSupersetOf(IEnumerable<long> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is CompressedLongSet o)
            return o._store.Cardinality < _store.Cardinality && o._store.IsSubsetOf(ref _store);

        // An empty right-hand side: a proper superset iff the set is non-empty.
        if (other is ICollection<long> collection && collection.Count == 0)
            return _store.Cardinality > 0;

        HashSet<long> materialized = new(other);
        if (materialized.Count >= _store.Cardinality)
            return false;

        foreach (long item in materialized)
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
    public bool Overlaps(IEnumerable<long> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (_store.Cardinality == 0)
            return false;

        if (other is CompressedLongSet o)
            return _store.Overlaps(ref o._store);

        foreach (long item in other)
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
    public bool SetEquals(IEnumerable<long> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is CompressedLongSet o)
            return _store.Cardinality == o._store.Cardinality && _store.IsSubsetOf(ref o._store);

        HashSet<long> materialized = new(other);
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
    /// <see cref="int.MaxValue"/> elements reports, since no <c>long[]</c> can hold it.
    /// </exception>
    public void CopyTo(long[] array, int arrayIndex)
    {
        // Guarded directly rather than through SetOperations.CopyTo for the same reason as the six
        // set operations above: that helper takes an int count, so passing it Count would evaluate —
        // and throw from — the overflow guard *before* any argument was validated, turning
        // CopyTo(null, 0) on a very large set into an OverflowException. CopyToGuard takes a long,
        // so passing _store.Cardinality keeps every argument validated before the count is read, and a
        // set too large for a long[] correctly reports insufficient space rather than overflowing.
        CopyToGuard.Validate(array, arrayIndex, _store.Cardinality, CopyToGuard.SetElementsMessage);

        int i = arrayIndex;
        foreach (long item in this)
            array[i++] = item;
    }

    // Adds the element, returning whether it was newly added (ISet<T> semantics) — the
    // non-throwing (on duplicates) counterpart of the public throw-on-duplicate Add(long).
    bool ISet<long>.Add(long item) => TryAdd(item);

    // ICollection<T>.Add must not throw on a duplicate (unlike the public Add(long)), so it maps to
    // the non-throwing TryAdd.
    void ICollection<long>.Add(long item) => TryAdd(item);

    bool ICollection<long>.IsReadOnly => false;

    // Whether every element of this set is in `other`. Each caller compares cardinalities first, so
    // this is never reached for a set too large to walk.
    private bool AllElementsIn(HashSet<long> other)
    {
        foreach (long item in this)
        {
            if (!other.Contains(item))
                return false;
        }

        return true;
    }

    // ── Value ↔ (chunk key, offset) ───────────────────────────────────────────
    // The sign bit is flipped into the key so that ordering the chunk index by the unsigned key
    // orders the set by the *signed* value: long.MinValue lands in key 0 and long.MaxValue in key
    // 2^48 - 1. The low 16 bits are unaffected by the flip, so the offset is just a truncation.

    private const ulong SignBit = 0x8000_0000_0000_0000UL;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ChunkKey(long value) => ((ulong)value ^ SignBit) >> 16;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort Low(long value) => (ushort)value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long Compose(ulong key, ushort low) => (long)(((key << 16) ^ SignBit) | low);

    /// <summary>
    /// A struct enumerator over a <see cref="CompressedLongSet"/>, yielding every element in
    /// ascending signed order. Because it is a struct, iterating with <c>foreach</c> avoids the
    /// allocation a compiler-generated <c>IEnumerator&lt;long&gt;</c> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<long>
    {
        private readonly CompressedLongSet _set;
        private readonly int _version;
        private CompressedChunkStore.ContainerCursor _cursor;
        private int _chunkIndex;
        private long _current;

        internal Enumerator(CompressedLongSet set)
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
        public long Current => _current;

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
