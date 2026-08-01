using System.Collections;
using System.Numerics;
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
/// compatibility on all three target frameworks, published benchmarks against
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
/// <see cref="CompressedIntSet"/>, falling back to the shared element-at-a-time implementation for
/// any other <see cref="IEnumerable{T}"/>.
/// </para>
/// </remarks>
public sealed class CompressedIntSet : ISet<int>, IReadOnlySet<int>
{
    // Values are split into a 16-bit chunk key and a 16-bit offset within the chunk, so one chunk
    // spans 65,536 consecutive values and the whole int range needs at most 65,536 chunks.
    private const int BitmapWords = 1024;          // 65,536 bits / 64
    private const int BitmapBytes = BitmapWords * 8;

    // The array/bitmap crossover, and the reason it is 4096: 4096 ushorts and 1024 ulongs are both
    // 8 KB, so above this point the bitmap is never larger and answers Contains in O(1) rather than
    // O(log n). It is also the cap on how many ushorts an array container ever holds (one more,
    // transiently, on the insert that tips it over before it is promoted).
    private const int ArrayToBitmapThreshold = 4096;

    private const int InitialValueCapacity = 4;

    private enum ContainerKind : byte
    {
        /// <summary>Sorted distinct offsets in <c>Values[0, Length)</c>.</summary>
        Array,

        /// <summary>A 1024-word bitmap over the chunk's 65,536 offsets.</summary>
        Bitmap,

        /// <summary>(start, lengthMinusOne) pairs in <c>Values[0, Length)</c>, ascending and disjoint.</summary>
        Run,
    }

    private struct Chunk
    {
        public ushort Key;
        public ContainerKind Kind;
        public int Cardinality;
        public ushort[]? Values;
        public int Length;
        public ulong[]? Bitmap;
    }

    // Chunks, ascending by Key, in [0, _chunkCount). A chunk is never empty: the last removal from
    // one drops it, so a missing key means an empty range and a present key means at least one value.
    private Chunk[] _chunks = Array.Empty<Chunk>();
    private int _chunkCount;
    private long _cardinality;
    private int _version;

    // Scratch used by the bulk container paths to assemble a result before it replaces the target,
    // so a set-algebra pass over thousands of chunks does not allocate a temporary per chunk. Values
    // are appended in ascending order and the builder switches to the bitmap once it outgrows the
    // array form, which caps the scratch at 8 KB + 8 KB regardless of the operand sizes.
    private ushort[]? _scratchValues;
    private ulong[]? _scratchBitmap;
    private int _scratchCount;
    private bool _scratchIsBitmap;

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
    public long Cardinality => _cardinality;

    /// <summary>
    /// Gets the number of elements in the set.
    /// </summary>
    /// <exception cref="OverflowException">
    /// The set holds more than <see cref="int.MaxValue"/> elements, which only a
    /// <see cref="AddRange"/> over a very wide range can produce. Use <see cref="Cardinality"/>,
    /// which is a <see cref="long"/> and always correct.
    /// </exception>
    public int Count => _cardinality <= int.MaxValue
        ? (int)_cardinality
        : throw new OverflowException($"The set holds {_cardinality} elements, which does not fit an Int32. Use {nameof(Cardinality)} instead.");

    /// <summary>
    /// Gets the approximate memory the current representation occupies, in bytes: the chunk index
    /// plus each chunk's container payload. Object headers are not counted, so this is a measure of
    /// how well the data compressed rather than an exact managed-heap figure. It changes as
    /// containers switch form, and <see cref="Optimize"/> is what drives it down after a bulk load.
    /// </summary>
    public long MemoryUsageInBytes
    {
        get
        {
            long total = (long)_chunks.Length * Unsafe.SizeOf<Chunk>();
            for (int i = 0; i < _chunkCount; i++)
            {
                ref Chunk c = ref _chunks[i];
                total += c.Kind == ContainerKind.Bitmap ? BitmapBytes : (long)c.Values!.Length * sizeof(ushort);
            }

            return total;
        }
    }

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
        ushort key = ChunkKey(item);
        int index = FindChunk(key);
        if (index < 0)
        {
            index = ~index;
            InsertEmptyChunk(index, key);
        }

        if (!ContainerAdd(ref _chunks[index], Low(item)))
            return false;

        _cardinality++;
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
    /// four bytes, whatever the range's width — which is why this is the cheap way to build a
    /// clustered set. A range overlapping an existing chunk merges into it and the chunk is left in
    /// its natural form; call <see cref="Optimize"/> afterwards to re-compress those.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="endInclusive"/> is less than <paramref name="start"/>.</exception>
    public long AddRange(int start, int endInclusive)
    {
        if (endInclusive < start)
            throw new ArgumentOutOfRangeException(nameof(endInclusive), endInclusive, "The end of the range must not be less than the start.");

        // ChunkKey is monotone in the signed value, so the range spans a contiguous run of keys.
        int firstKey = ChunkKey(start);
        int lastKey = ChunkKey(endInclusive);
        long added = 0;

        for (int k = firstKey; k <= lastKey; k++)
        {
            ushort lo = k == firstKey ? Low(start) : (ushort)0;
            ushort hi = k == lastKey ? Low(endInclusive) : (ushort)0xFFFF;
            added += AddRunToChunk((ushort)k, lo, hi);
        }

        if (added != 0)
        {
            _cardinality += added;
            _version++;
        }

        return added;
    }

    /// <summary>
    /// Determines whether the set contains the specified value.
    /// </summary>
    /// <param name="item">The value to locate.</param>
    /// <returns><c>true</c> if the value is present.</returns>
    public bool Contains(int item)
    {
        int index = FindChunk(ChunkKey(item));
        return index >= 0 && ContainerContains(in _chunks[index], Low(item));
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
        int index = FindChunk(ChunkKey(item));
        if (index < 0)
            return false;

        if (!ContainerRemove(ref _chunks[index], Low(item)))
            return false;

        _cardinality--;
        if (_chunks[index].Cardinality == 0)
            RemoveChunkAt(index);

        _version++;
        return true;
    }

    /// <summary>
    /// Removes all elements from the set, releasing every container. A <see cref="Clear"/> on an
    /// already-empty set changes nothing and leaves active enumerators valid.
    /// </summary>
    public void Clear()
    {
        if (_cardinality == 0)
            return;

        Array.Clear(_chunks, 0, _chunkCount);
        _chunkCount = 0;
        _cardinality = 0;
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
        for (int i = 0; i < _chunkCount; i++)
            OptimizeChunk(ref _chunks[i]);

        if (_chunks.Length != _chunkCount)
            Array.Resize(ref _chunks, _chunkCount);
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
            return _cardinality;

        long total = 0;
        int i = 0, j = 0;
        while (i < _chunkCount && j < other._chunkCount)
        {
            ushort a = _chunks[i].Key, b = other._chunks[j].Key;
            if (a < b)
                i++;
            else if (a > b)
                j++;
            else
                total += ContainerIntersectCount(in _chunks[i++], in other._chunks[j++]);
        }

        return total;
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
            UnionWithCore(o);
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
        if (_cardinality == 0 || ReferenceEquals(this, other))
            return;

        if (other is CompressedIntSet o)
        {
            IntersectWithCore(o);
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

        ReplaceWith(survivors);
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

        if (_cardinality == 0)
            return;

        if (other is CompressedIntSet o)
        {
            ExceptWithCore(o);
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
            SymmetricExceptWithCore(o);
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
        if (other is CompressedIntSet o)
            return _cardinality <= o._cardinality && IsSubsetOfCore(o);

        HashSet<int> materialized = new(other);
        return _cardinality <= materialized.Count && AllElementsIn(materialized);
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
            return _cardinality < o._cardinality && IsSubsetOfCore(o);

        HashSet<int> materialized = new(other);
        return _cardinality < materialized.Count && AllElementsIn(materialized);
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
            return o._cardinality <= _cardinality && o.IsSubsetOfCore(this);

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
            return o._cardinality < _cardinality && o.IsSubsetOfCore(this);

        HashSet<int> materialized = new(other);
        if (materialized.Count >= _cardinality)
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
        if (_cardinality == 0)
            return false;

        if (other is CompressedIntSet o)
            return OverlapsCore(o);

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
            return _cardinality == o._cardinality && IsSubsetOfCore(o);

        HashSet<int> materialized = new(other);
        return _cardinality == materialized.Count && AllElementsIn(materialized);
    }

    /// <summary>
    /// Copies the elements of the set to the specified <paramref name="array"/>, starting at
    /// <paramref name="arrayIndex"/>, in ascending signed order.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
    /// <exception cref="OverflowException">The set holds more than <see cref="int.MaxValue"/> elements.</exception>
    public void CopyTo(int[] array, int arrayIndex)
    {
        // Written out rather than delegated to SetOperations.CopyTo for the same reason as the six
        // set operations above: that helper takes an int count, so passing it Count would evaluate —
        // and throw from — the overflow guard *before* any argument was validated, turning
        // CopyTo(null, 0) on a very large set into an OverflowException. Checking the long
        // _cardinality keeps the argument-validation order identical to HashSet<int>.CopyTo, and a
        // set too large for an int[] correctly reports insufficient space rather than overflowing.
        ArgumentNullException.ThrowIfNull(array);
        if (arrayIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, "Array index must be non-negative.");
        if (arrayIndex > array.Length)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, "Array index is beyond the end of the destination array.");
        if (array.Length - arrayIndex < _cardinality)
            throw new ArgumentException("The destination array has insufficient space to copy the set's elements.", nameof(array));

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

    // ── Value ↔ (chunk key, offset) ───────────────────────────────────────────
    // The sign bit is flipped into the key so that ordering the chunk index by the unsigned key
    // orders the set by the *signed* value: int.MinValue lands in key 0 and int.MaxValue in key
    // 0xFFFF. The low 16 bits are unaffected by the flip, so the offset is just a truncation.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort ChunkKey(int value) => (ushort)(((uint)value ^ 0x8000_0000u) >> 16);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ushort Low(int value) => (ushort)value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Compose(ushort key, ushort low) => (int)((((uint)key << 16) ^ 0x8000_0000u) | low);

    // ── Chunk index ───────────────────────────────────────────────────────────

    // Binary search over the ascending chunk keys. Returns the index of the chunk, or the bitwise
    // complement of the index it would be inserted at.
    private int FindChunk(ushort key)
    {
        int lo = 0, hi = _chunkCount - 1;
        while (lo <= hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            ushort probe = _chunks[mid].Key;
            if (probe < key)
                lo = mid + 1;
            else if (probe > key)
                hi = mid - 1;
            else
                return mid;
        }

        return ~lo;
    }

    private void InsertEmptyChunk(int index, ushort key)
    {
        MakeRoomForChunk(index);
        _chunks[index] = new Chunk
        {
            Key = key,
            Kind = ContainerKind.Array,
            Values = new ushort[InitialValueCapacity],
            Length = 0,
            Cardinality = 0,
        };
    }

    private void InsertRunChunk(int index, ushort key, ushort lo, ushort hi)
    {
        MakeRoomForChunk(index);
        _chunks[index] = new Chunk
        {
            Key = key,
            Kind = ContainerKind.Run,
            Values = new ushort[] { lo, (ushort)(hi - lo) },
            Length = 2,
            Cardinality = hi - lo + 1,
        };
    }

    private void MakeRoomForChunk(int index)
    {
        if (_chunkCount == _chunks.Length)
            Array.Resize(ref _chunks, _chunks.Length == 0 ? 4 : _chunks.Length * 2);

        Array.Copy(_chunks, index, _chunks, index + 1, _chunkCount - index);
        _chunkCount++;
    }

    private void RemoveChunkAt(int index)
    {
        Array.Copy(_chunks, index + 1, _chunks, index, _chunkCount - index - 1);
        _chunkCount--;
        _chunks[_chunkCount] = default;
    }

    // ── Container reads ───────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool BitmapContains(ulong[] bitmap, ushort low) =>
        (bitmap[low >> 6] & (1UL << (low & 63))) != 0;

    private static bool ContainerContains(in Chunk c, ushort low)
    {
        if (c.Kind == ContainerKind.Bitmap)
            return BitmapContains(c.Bitmap!, low);

        if (c.Kind == ContainerKind.Array)
            return BinarySearchValues(c.Values!, c.Length, low) >= 0;

        // Run: locate the pair whose start is the greatest one not above `low`.
        ushort[] runs = c.Values!;
        int lo = 0, hi = (c.Length >> 1) - 1;
        while (lo <= hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            int start = runs[mid << 1];
            if (low < start)
                hi = mid - 1;
            else if (low > start + runs[(mid << 1) + 1])
                lo = mid + 1;
            else
                return true;
        }

        return false;
    }

    // Returns the index of `value` in the sorted prefix, or the bitwise complement of its
    // insertion point.
    private static int BinarySearchValues(ushort[] values, int length, ushort value)
    {
        int lo = 0, hi = length - 1;
        while (lo <= hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            ushort probe = values[mid];
            if (probe < value)
                lo = mid + 1;
            else if (probe > value)
                hi = mid - 1;
            else
                return mid;
        }

        return ~lo;
    }

    private static int PopCount(ulong[] bitmap)
    {
        int count = 0;
        for (int w = 0; w < BitmapWords; w++)
            count += BitOperations.PopCount(bitmap[w]);

        return count;
    }

    // ── Container form changes ────────────────────────────────────────────────

    // Materializes the chunk as a bitmap. A no-op when it already is one.
    private static void ToBitmap(ref Chunk c)
    {
        if (c.Kind == ContainerKind.Bitmap)
            return;

        ulong[] bits = new ulong[BitmapWords];
        var cursor = new ContainerCursor(in c);
        while (cursor.MoveNext(out ushort v))
            bits[v >> 6] |= 1UL << (v & 63);

        c.Bitmap = bits;
        c.Kind = ContainerKind.Bitmap;
        c.Values = null;
        c.Length = 0;
    }

    // Materializes the chunk as a sorted array. Only ever called when the cardinality fits one.
    private static void ToArray(ref Chunk c)
    {
        ushort[] values = new ushort[c.Cardinality];
        int n = 0;
        var cursor = new ContainerCursor(in c);
        while (cursor.MoveNext(out ushort v))
            values[n++] = v;

        c.Values = values;
        c.Length = n;
        c.Kind = ContainerKind.Array;
        c.Bitmap = null;
    }

    // Expands a run container back to whichever of array/bitmap suits its cardinality, because
    // every write path below assumes one of those two. A no-op for a chunk already in that form.
    private static void ToNaturalForm(ref Chunk c)
    {
        if (c.Kind != ContainerKind.Run)
            return;

        if (c.Cardinality > ArrayToBitmapThreshold)
            ToBitmap(ref c);
        else
            ToArray(ref c);
    }

    // Demotes a bitmap that a bulk rewrite left below the 4096 crossover. Every caller has just
    // produced a bitmap, so this is the only direction the crossover can be crossed here.
    private static void NormalizeBitmap(ref Chunk c)
    {
        if (c.Cardinality <= ArrayToBitmapThreshold)
            ToArray(ref c);
    }

    // Counts the maximal runs of consecutive values in the chunk.
    private static int CountRuns(in Chunk c)
    {
        int runs = 0;
        int previous = -2;
        var cursor = new ContainerCursor(in c);
        while (cursor.MoveNext(out ushort v))
        {
            if (v != previous + 1)
                runs++;
            previous = v;
        }

        return runs;
    }

    private static void ToRun(ref Chunk c, int runs)
    {
        ushort[] pairs = new ushort[runs << 1];
        int n = 0;
        int start = -1;
        int previous = -2;
        var cursor = new ContainerCursor(in c);
        while (cursor.MoveNext(out ushort v))
        {
            if (v != previous + 1)
            {
                if (start >= 0)
                {
                    pairs[n++] = (ushort)start;
                    pairs[n++] = (ushort)(previous - start);
                }

                start = v;
            }

            previous = v;
        }

        pairs[n++] = (ushort)start;
        pairs[n++] = (ushort)(previous - start);

        c.Values = pairs;
        c.Length = n;
        c.Kind = ContainerKind.Run;
        c.Bitmap = null;
    }

    private static void OptimizeChunk(ref Chunk c)
    {
        int naturalBytes = c.Cardinality <= ArrayToBitmapThreshold ? c.Cardinality * sizeof(ushort) : BitmapBytes;
        int runs = CountRuns(in c);

        // A run pair is two ushorts, so run encoding wins exactly when the data is clustered enough
        // that fewer than half as many pairs as the natural form needs entries.
        if (runs * 2 * sizeof(ushort) < naturalBytes)
        {
            ToRun(ref c, runs);
            return;
        }

        if (c.Cardinality > ArrayToBitmapThreshold)
            ToBitmap(ref c);
        else
            ToArray(ref c); // also trims an oversized array container to its exact length
    }

    // ── Single-element container writes ───────────────────────────────────────

    private static bool ContainerAdd(ref Chunk c, ushort low)
    {
        ToNaturalForm(ref c);

        if (c.Kind == ContainerKind.Bitmap)
        {
            ref ulong word = ref c.Bitmap![low >> 6];
            ulong mask = 1UL << (low & 63);
            if ((word & mask) != 0)
                return false;

            word |= mask;
            c.Cardinality++;
            return true;
        }

        int found = BinarySearchValues(c.Values!, c.Length, low);
        if (found >= 0)
            return false;

        int insert = ~found;
        if (c.Length == c.Values!.Length)
            Array.Resize(ref c.Values, Math.Min(ArrayToBitmapThreshold + 1, c.Values.Length * 2));

        Array.Copy(c.Values, insert, c.Values, insert + 1, c.Length - insert);
        c.Values[insert] = low;
        c.Length++;
        c.Cardinality++;

        if (c.Cardinality > ArrayToBitmapThreshold)
            ToBitmap(ref c);

        return true;
    }

    private static bool ContainerRemove(ref Chunk c, ushort low)
    {
        ToNaturalForm(ref c);

        if (c.Kind == ContainerKind.Bitmap)
        {
            ref ulong word = ref c.Bitmap![low >> 6];
            ulong mask = 1UL << (low & 63);
            if ((word & mask) == 0)
                return false;

            word &= ~mask;
            c.Cardinality--;
            return true;
        }

        int found = BinarySearchValues(c.Values!, c.Length, low);
        if (found < 0)
            return false;

        Array.Copy(c.Values!, found + 1, c.Values!, found, c.Length - found - 1);
        c.Length--;
        c.Cardinality--;
        return true;
    }

    // ── Range insertion ───────────────────────────────────────────────────────

    private long AddRunToChunk(ushort key, ushort lo, ushort hi)
    {
        int index = FindChunk(key);
        if (index < 0)
        {
            InsertRunChunk(~index, key, lo, hi);
            return hi - lo + 1;
        }

        ref Chunk c = ref _chunks[index];
        int before = c.Cardinality;
        ToBitmap(ref c);
        SetBitRange(c.Bitmap!, lo, hi);
        c.Cardinality = PopCount(c.Bitmap!);
        NormalizeBitmap(ref c);
        return c.Cardinality - before;
    }

    private static void SetBitRange(ulong[] bits, int lo, int hi)
    {
        int loWord = lo >> 6, hiWord = hi >> 6;
        ulong loMask = ulong.MaxValue << (lo & 63);

        if (loWord == hiWord)
        {
            bits[loWord] |= loMask & LowMask(hi & 63);
            return;
        }

        bits[loWord] |= loMask;
        for (int w = loWord + 1; w < hiWord; w++)
            bits[w] = ulong.MaxValue;

        bits[hiWord] |= LowMask(hi & 63);
    }

    // Bits 0..bit inclusive.
    private static ulong LowMask(int bit) => bit == 63 ? ulong.MaxValue : (1UL << (bit + 1)) - 1;

    // ── Result scratch ────────────────────────────────────────────────────────

    private void ScratchReset()
    {
        _scratchValues ??= new ushort[ArrayToBitmapThreshold];
        _scratchCount = 0;
        if (_scratchIsBitmap)
        {
            Array.Clear(_scratchBitmap!, 0, BitmapWords);
            _scratchIsBitmap = false;
        }
    }

    // Appends the next value of the result. Callers must append in ascending order.
    private void ScratchAppend(ushort value)
    {
        if (_scratchIsBitmap)
        {
            _scratchBitmap![value >> 6] |= 1UL << (value & 63);
            _scratchCount++;
            return;
        }

        if (_scratchCount == ArrayToBitmapThreshold)
        {
            _scratchBitmap ??= new ulong[BitmapWords];
            for (int i = 0; i < _scratchCount; i++)
            {
                ushort v = _scratchValues![i];
                _scratchBitmap[v >> 6] |= 1UL << (v & 63);
            }

            _scratchIsBitmap = true;
            _scratchBitmap[value >> 6] |= 1UL << (value & 63);
            _scratchCount++;
            return;
        }

        _scratchValues![_scratchCount++] = value;
    }

    // Replaces the chunk's container with whatever the scratch accumulated, reusing the chunk's
    // existing storage when it is big enough so a steady-state set-algebra loop stops allocating.
    private void ScratchWriteTo(ref Chunk c)
    {
        if (_scratchIsBitmap)
        {
            ulong[] bits = c.Bitmap ?? new ulong[BitmapWords];
            Array.Copy(_scratchBitmap!, bits, BitmapWords);
            c.Bitmap = bits;
            c.Kind = ContainerKind.Bitmap;
            c.Values = null;
            c.Length = 0;
        }
        else
        {
            ushort[] values = c.Values is { } existing && existing.Length >= _scratchCount
                ? existing
                : new ushort[_scratchCount];
            Array.Copy(_scratchValues!, values, _scratchCount);
            c.Values = values;
            c.Kind = ContainerKind.Array;
            c.Length = _scratchCount;
            c.Bitmap = null;
        }

        c.Cardinality = _scratchCount;
    }

    // ── Container set algebra ─────────────────────────────────────────────────
    // Each operation has a word-parallel path for the dense (bitmap ⊕ bitmap) case and a
    // merge/filter path for everything else, driven by ContainerCursor so a run container on either
    // side needs no special case and is never mutated just because it was read.

    private void UnionInto(ref Chunk lhs, in Chunk rhs)
    {
        if (lhs.Kind == ContainerKind.Bitmap || rhs.Kind == ContainerKind.Bitmap ||
            lhs.Cardinality + rhs.Cardinality > ArrayToBitmapThreshold)
        {
            ToBitmap(ref lhs);
            ulong[] bits = lhs.Bitmap!;
            if (rhs.Kind == ContainerKind.Bitmap)
            {
                ulong[] other = rhs.Bitmap!;
                for (int w = 0; w < BitmapWords; w++)
                    bits[w] |= other[w];
            }
            else
            {
                var cursor = new ContainerCursor(in rhs);
                while (cursor.MoveNext(out ushort v))
                    bits[v >> 6] |= 1UL << (v & 63);
            }

            lhs.Cardinality = PopCount(bits);
            NormalizeBitmap(ref lhs);
            return;
        }

        ScratchReset();
        var a = new ContainerCursor(in lhs);
        var b = new ContainerCursor(in rhs);
        bool hasA = a.MoveNext(out ushort va);
        bool hasB = b.MoveNext(out ushort vb);
        while (hasA && hasB)
        {
            if (va < vb)
            {
                ScratchAppend(va);
                hasA = a.MoveNext(out va);
            }
            else if (va > vb)
            {
                ScratchAppend(vb);
                hasB = b.MoveNext(out vb);
            }
            else
            {
                ScratchAppend(va);
                hasA = a.MoveNext(out va);
                hasB = b.MoveNext(out vb);
            }
        }

        while (hasA)
        {
            ScratchAppend(va);
            hasA = a.MoveNext(out va);
        }

        while (hasB)
        {
            ScratchAppend(vb);
            hasB = b.MoveNext(out vb);
        }

        ScratchWriteTo(ref lhs);
    }

    private void IntersectInto(ref Chunk lhs, in Chunk rhs)
    {
        if (lhs.Kind == ContainerKind.Bitmap && rhs.Kind == ContainerKind.Bitmap)
        {
            ulong[] bits = lhs.Bitmap!, other = rhs.Bitmap!;
            for (int w = 0; w < BitmapWords; w++)
                bits[w] &= other[w];

            lhs.Cardinality = PopCount(bits);
            NormalizeBitmap(ref lhs);
            return;
        }

        ScratchReset();
        if (rhs.Kind == ContainerKind.Bitmap)
        {
            // Probing a bitmap is a single masked load, so walk the sparse side against it.
            var cursor = new ContainerCursor(in lhs);
            while (cursor.MoveNext(out ushort v))
            {
                if (BitmapContains(rhs.Bitmap!, v))
                    ScratchAppend(v);
            }
        }
        else if (lhs.Kind == ContainerKind.Bitmap)
        {
            var cursor = new ContainerCursor(in rhs);
            while (cursor.MoveNext(out ushort v))
            {
                if (BitmapContains(lhs.Bitmap!, v))
                    ScratchAppend(v);
            }
        }
        else
        {
            // Both sides are sorted, so a single linear merge beats a binary search per element —
            // and this is the shape the sparse workload the type is sold for actually hits.
            var a = new ContainerCursor(in lhs);
            var b = new ContainerCursor(in rhs);
            bool hasA = a.MoveNext(out ushort va);
            bool hasB = b.MoveNext(out ushort vb);
            while (hasA && hasB)
            {
                if (va < vb)
                {
                    hasA = a.MoveNext(out va);
                }
                else if (va > vb)
                {
                    hasB = b.MoveNext(out vb);
                }
                else
                {
                    ScratchAppend(va);
                    hasA = a.MoveNext(out va);
                    hasB = b.MoveNext(out vb);
                }
            }
        }

        ScratchWriteTo(ref lhs);
    }

    private void ExceptInto(ref Chunk lhs, in Chunk rhs)
    {
        if (lhs.Kind == ContainerKind.Bitmap && rhs.Kind == ContainerKind.Bitmap)
        {
            ulong[] bits = lhs.Bitmap!, other = rhs.Bitmap!;
            for (int w = 0; w < BitmapWords; w++)
                bits[w] &= ~other[w];

            lhs.Cardinality = PopCount(bits);
            NormalizeBitmap(ref lhs);
            return;
        }

        ScratchReset();
        if (rhs.Kind == ContainerKind.Bitmap)
        {
            var cursor = new ContainerCursor(in lhs);
            while (cursor.MoveNext(out ushort v))
            {
                if (!BitmapContains(rhs.Bitmap!, v))
                    ScratchAppend(v);
            }
        }
        else
        {
            // Both sides sorted: one linear merge, emitting the values only the left side has.
            var a = new ContainerCursor(in lhs);
            var b = new ContainerCursor(in rhs);
            bool hasA = a.MoveNext(out ushort va);
            bool hasB = b.MoveNext(out ushort vb);
            while (hasA && hasB)
            {
                if (va < vb)
                {
                    ScratchAppend(va);
                    hasA = a.MoveNext(out va);
                }
                else if (va > vb)
                {
                    hasB = b.MoveNext(out vb);
                }
                else
                {
                    hasA = a.MoveNext(out va);
                    hasB = b.MoveNext(out vb);
                }
            }

            while (hasA)
            {
                ScratchAppend(va);
                hasA = a.MoveNext(out va);
            }
        }

        ScratchWriteTo(ref lhs);
    }

    private void SymmetricExceptInto(ref Chunk lhs, in Chunk rhs)
    {
        if (lhs.Kind == ContainerKind.Bitmap || rhs.Kind == ContainerKind.Bitmap ||
            lhs.Cardinality + rhs.Cardinality > ArrayToBitmapThreshold)
        {
            ToBitmap(ref lhs);
            ulong[] bits = lhs.Bitmap!;
            if (rhs.Kind == ContainerKind.Bitmap)
            {
                ulong[] other = rhs.Bitmap!;
                for (int w = 0; w < BitmapWords; w++)
                    bits[w] ^= other[w];
            }
            else
            {
                var cursor = new ContainerCursor(in rhs);
                while (cursor.MoveNext(out ushort v))
                    bits[v >> 6] ^= 1UL << (v & 63);
            }

            lhs.Cardinality = PopCount(bits);
            NormalizeBitmap(ref lhs);
            return;
        }

        ScratchReset();
        var a = new ContainerCursor(in lhs);
        var b = new ContainerCursor(in rhs);
        bool hasA = a.MoveNext(out ushort va);
        bool hasB = b.MoveNext(out ushort vb);
        while (hasA && hasB)
        {
            if (va < vb)
            {
                ScratchAppend(va);
                hasA = a.MoveNext(out va);
            }
            else if (va > vb)
            {
                ScratchAppend(vb);
                hasB = b.MoveNext(out vb);
            }
            else
            {
                hasA = a.MoveNext(out va);
                hasB = b.MoveNext(out vb);
            }
        }

        while (hasA)
        {
            ScratchAppend(va);
            hasA = a.MoveNext(out va);
        }

        while (hasB)
        {
            ScratchAppend(vb);
            hasB = b.MoveNext(out vb);
        }

        ScratchWriteTo(ref lhs);
    }

    private static int ContainerIntersectCount(in Chunk a, in Chunk b)
    {
        if (a.Kind == ContainerKind.Bitmap && b.Kind == ContainerKind.Bitmap)
        {
            ulong[] x = a.Bitmap!, y = b.Bitmap!;
            int count = 0;
            for (int w = 0; w < BitmapWords; w++)
                count += BitOperations.PopCount(x[w] & y[w]);

            return count;
        }

        // The two counting queries below probe the larger side rather than merging the two sorted
        // cursors the way the mutating operators do. That is deliberate: probing lets Overlaps bail
        // on the first hit (a merge would have to keep stepping the smaller side), and neither query
        // writes a result, so the merge's one advantage — producing the output in order for free —
        // buys nothing here.
        int total = 0;
        if (a.Cardinality <= b.Cardinality)
        {
            var cursor = new ContainerCursor(in a);
            while (cursor.MoveNext(out ushort v))
            {
                if (ContainerContains(in b, v))
                    total++;
            }
        }
        else
        {
            var cursor = new ContainerCursor(in b);
            while (cursor.MoveNext(out ushort v))
            {
                if (ContainerContains(in a, v))
                    total++;
            }
        }

        return total;
    }

    private static bool ContainerOverlaps(in Chunk a, in Chunk b)
    {
        if (a.Kind == ContainerKind.Bitmap && b.Kind == ContainerKind.Bitmap)
        {
            ulong[] x = a.Bitmap!, y = b.Bitmap!;
            for (int w = 0; w < BitmapWords; w++)
            {
                if ((x[w] & y[w]) != 0)
                    return true;
            }

            return false;
        }

        if (a.Cardinality <= b.Cardinality)
        {
            var cursor = new ContainerCursor(in a);
            while (cursor.MoveNext(out ushort v))
            {
                if (ContainerContains(in b, v))
                    return true;
            }
        }
        else
        {
            var cursor = new ContainerCursor(in b);
            while (cursor.MoveNext(out ushort v))
            {
                if (ContainerContains(in a, v))
                    return true;
            }
        }

        return false;
    }

    // ── Whole-set set algebra ─────────────────────────────────────────────────

    private static Chunk CloneChunk(in Chunk source)
    {
        Chunk copy = source;
        if (source.Kind == ContainerKind.Bitmap)
            copy.Bitmap = (ulong[])source.Bitmap!.Clone();
        else
            copy.Values = source.Values![..source.Length];

        return copy;
    }

    private void UnionWithCore(CompressedIntSet other)
    {
        if (other._chunkCount == 0)
            return;

        long before = _cardinality;
        Chunk[] merged = new Chunk[_chunkCount + other._chunkCount];
        int i = 0, j = 0, n = 0;
        while (i < _chunkCount && j < other._chunkCount)
        {
            ushort a = _chunks[i].Key, b = other._chunks[j].Key;
            if (a < b)
            {
                merged[n++] = _chunks[i++];
            }
            else if (a > b)
            {
                merged[n++] = CloneChunk(in other._chunks[j++]);
            }
            else
            {
                Chunk combined = _chunks[i++];
                UnionInto(ref combined, in other._chunks[j++]);
                merged[n++] = combined;
            }
        }

        while (i < _chunkCount)
            merged[n++] = _chunks[i++];

        while (j < other._chunkCount)
            merged[n++] = CloneChunk(in other._chunks[j++]);

        _chunks = merged;
        _chunkCount = n;
        _cardinality = SumCardinality();
        if (_cardinality != before)
            _version++;
    }

    private void IntersectWithCore(CompressedIntSet other)
    {
        long before = _cardinality;
        int i = 0, j = 0, n = 0;
        while (i < _chunkCount && j < other._chunkCount)
        {
            ushort a = _chunks[i].Key, b = other._chunks[j].Key;
            if (a < b)
            {
                i++;
            }
            else if (a > b)
            {
                j++;
            }
            else
            {
                // The compaction cursor `n` only ever trails `i`, so writing the surviving chunk
                // back here never overwrites one the loop has yet to read.
                Chunk combined = _chunks[i++];
                IntersectInto(ref combined, in other._chunks[j++]);
                if (combined.Cardinality != 0)
                    _chunks[n++] = combined;
            }
        }

        TruncateChunks(n);
        _cardinality = SumCardinality();
        if (_cardinality != before)
            _version++;
    }

    private void ExceptWithCore(CompressedIntSet other)
    {
        long before = _cardinality;
        int i = 0, j = 0, n = 0;
        while (i < _chunkCount && j < other._chunkCount)
        {
            ushort a = _chunks[i].Key, b = other._chunks[j].Key;
            if (a < b)
            {
                _chunks[n++] = _chunks[i++];
            }
            else if (a > b)
            {
                j++;
            }
            else
            {
                Chunk combined = _chunks[i++];
                ExceptInto(ref combined, in other._chunks[j++]);
                if (combined.Cardinality != 0)
                    _chunks[n++] = combined;
            }
        }

        while (i < _chunkCount)
            _chunks[n++] = _chunks[i++];

        TruncateChunks(n);
        _cardinality = SumCardinality();
        if (_cardinality != before)
            _version++;
    }

    private void SymmetricExceptWithCore(CompressedIntSet other)
    {
        if (other._chunkCount == 0)
            return;

        Chunk[] merged = new Chunk[_chunkCount + other._chunkCount];
        int i = 0, j = 0, n = 0;
        while (i < _chunkCount && j < other._chunkCount)
        {
            ushort a = _chunks[i].Key, b = other._chunks[j].Key;
            if (a < b)
            {
                merged[n++] = _chunks[i++];
            }
            else if (a > b)
            {
                merged[n++] = CloneChunk(in other._chunks[j++]);
            }
            else
            {
                Chunk combined = _chunks[i++];
                SymmetricExceptInto(ref combined, in other._chunks[j++]);
                if (combined.Cardinality != 0)
                    merged[n++] = combined;
            }
        }

        while (i < _chunkCount)
            merged[n++] = _chunks[i++];

        while (j < other._chunkCount)
            merged[n++] = CloneChunk(in other._chunks[j++]);

        _chunks = merged;
        _chunkCount = n;
        _cardinality = SumCardinality();

        // A symmetric difference against a non-empty set always toggles at least one element.
        _version++;
    }

    private bool IsSubsetOfCore(CompressedIntSet other)
    {
        for (int i = 0; i < _chunkCount; i++)
        {
            int j = other.FindChunk(_chunks[i].Key);
            if (j < 0)
                return false;

            ref Chunk mine = ref _chunks[i];
            ref Chunk theirs = ref other._chunks[j];
            if (mine.Cardinality > theirs.Cardinality)
                return false;

            var cursor = new ContainerCursor(in mine);
            while (cursor.MoveNext(out ushort v))
            {
                if (!ContainerContains(in theirs, v))
                    return false;
            }
        }

        return true;
    }

    private bool OverlapsCore(CompressedIntSet other)
    {
        int i = 0, j = 0;
        while (i < _chunkCount && j < other._chunkCount)
        {
            ushort a = _chunks[i].Key, b = other._chunks[j].Key;
            if (a < b)
                i++;
            else if (a > b)
                j++;
            else if (ContainerOverlaps(in _chunks[i++], in other._chunks[j++]))
                return true;
        }

        return false;
    }

    // Takes over `replacement`'s storage. Only ever called with a set built locally by the caller,
    // so stealing its arrays cannot alias anything anyone else still holds.
    private void ReplaceWith(CompressedIntSet replacement)
    {
        bool changed = replacement._cardinality != _cardinality;
        _chunks = replacement._chunks;
        _chunkCount = replacement._chunkCount;
        _cardinality = replacement._cardinality;
        if (changed)
            _version++;
    }

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

    private void TruncateChunks(int count)
    {
        Array.Clear(_chunks, count, _chunkCount - count);
        _chunkCount = count;
    }

    private long SumCardinality()
    {
        long total = 0;
        for (int i = 0; i < _chunkCount; i++)
            total += _chunks[i].Cardinality;

        return total;
    }

    // ── Cursors ───────────────────────────────────────────────────────────────

    // Yields a container's values in ascending order, whichever form it is stored in. Writing the
    // generic set-algebra paths against this is what keeps a run container from needing its own
    // case in every operation — and from being decompressed just because it was read.
    private struct ContainerCursor
    {
        private readonly Chunk _chunk;
        private int _index;
        private ulong _word;
        private int _wordIndex;
        private int _runNext;
        private int _runEnd;

        internal ContainerCursor(in Chunk chunk)
        {
            _chunk = chunk;
            _index = 0;
            _word = 0;
            _wordIndex = -1;
            _runNext = 0;
            _runEnd = -1;
        }

        internal bool MoveNext(out ushort value)
        {
            if (_chunk.Kind == ContainerKind.Array)
            {
                if (_index < _chunk.Length)
                {
                    value = _chunk.Values![_index++];
                    return true;
                }

                value = 0;
                return false;
            }

            if (_chunk.Kind == ContainerKind.Run)
            {
                if (_runNext > _runEnd)
                {
                    if (_index >= _chunk.Length)
                    {
                        value = 0;
                        return false;
                    }

                    _runNext = _chunk.Values![_index];
                    _runEnd = _runNext + _chunk.Values[_index + 1];
                    _index += 2;
                }

                value = (ushort)_runNext++;
                return true;
            }

            while (_word == 0)
            {
                if (++_wordIndex >= BitmapWords)
                {
                    value = 0;
                    return false;
                }

                _word = _chunk.Bitmap![_wordIndex];
            }

            int bit = BitOperations.TrailingZeroCount(_word);
            _word &= _word - 1;
            value = (ushort)((_wordIndex << 6) + bit);
            return true;
        }
    }

    /// <summary>
    /// A struct enumerator over a <see cref="CompressedIntSet"/>, yielding every element in
    /// ascending signed order. Because it is a struct, iterating with <c>foreach</c> avoids the
    /// allocation a compiler-generated <c>IEnumerator&lt;int&gt;</c> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<int>
    {
        private readonly CompressedIntSet _set;
        private readonly int _version;
        private ContainerCursor _cursor;
        private int _chunkIndex;
        private int _current;

        internal Enumerator(CompressedIntSet set)
        {
            _set = set;
            _version = set._version;
            _chunkIndex = 0;
            _cursor = set._chunkCount > 0 ? new ContainerCursor(in set._chunks[0]) : default;
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

            while (_chunkIndex < _set._chunkCount)
            {
                if (_cursor.MoveNext(out ushort low))
                {
                    _current = Compose(_set._chunks[_chunkIndex].Key, low);
                    return true;
                }

                _chunkIndex++;
                if (_chunkIndex < _set._chunkCount)
                    _cursor = new ContainerCursor(in _set._chunks[_chunkIndex]);
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
            _cursor = _set._chunkCount > 0 ? new ContainerCursor(in _set._chunks[0]) : default;
            _current = 0;
        }

        /// <summary>
        /// Releases any resources held by the enumerator. No-op for this type.
        /// </summary>
        public void Dispose() { }
    }
}
