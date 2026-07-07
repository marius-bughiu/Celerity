using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A high-performance generic hash set that takes the struct-of-arrays layout one
/// step further than <see cref="CeleritySet{T, THasher}"/>: alongside the
/// <c>items</c> array it keeps a dense side array of 32-bit hash
/// <em>fingerprints</em>. A probe scan touches only that compact metadata buffer —
/// comparing the cached fingerprint before it ever reads an element — so
/// cache-cold lookups and lookups with expensive element equality (long strings,
/// large structs) short-circuit on a single integer compare instead of
/// dereferencing every candidate element. It is the set counterpart of
/// <see cref="HashCachingDictionary{TKey, TValue, THasher}"/>.
/// </summary>
/// <remarks>
/// <para>
/// The fingerprint of an occupied slot is the element's hash with its top bit
/// forced set (<c>hash | 0x80000000</c>), which makes it always non-zero; an empty
/// slot is the array default of <c>0</c>. The fingerprint array therefore doubles
/// as the occupancy bitmap — probing, enumeration, and the set-algebra operations
/// test it rather than comparing elements against <c>default(T)</c>. Because the
/// forced bit lives above every power-of-two table mask, the cached fingerprint
/// also yields the slot index directly (<c>fingerprint &amp; mask</c>), so a resize
/// re-homes every entry without recomputing a single hash.
/// </para>
/// <para>
/// This is an additional opt-in type, not a replacement for
/// <see cref="CeleritySet{T, THasher}"/>: it trades four bytes of metadata per slot
/// for the cache-friendlier probe, which wins on lookup-dominated workloads (large
/// tables, many negative "have I seen this?" checks, expensive-equality elements)
/// and is roughly a wash on tiny tables of cheap (e.g. <see cref="int"/>) elements.
/// It is otherwise a drop-in peer of <see cref="CeleritySet{T, THasher}"/>: same
/// constructors, the same <see cref="ISet{T}"/> surface, the same allocation-free
/// struct enumerator, and the same out-of-band handling of <c>default(T)</c>.
/// </para>
/// <para>
/// The element <c>default(T)</c> (null for reference types, 0 for primitives,
/// <see cref="Guid.Empty"/> for <see cref="Guid"/>, etc.) is stored out-of-band in
/// a dedicated slot so the hasher is never invoked with it and it never collides
/// with the empty-slot sentinel.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of elements in the set.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute element hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class HashCachingSet<T, THasher> : ISet<T> where THasher : struct, IHashProvider<T>
{
    /// <summary>
    /// The default initial capacity of the set if no capacity is specified.
    /// </summary>
    protected const int DEFAULT_CAPACITY = 16;

    /// <summary>
    /// The default load factor of the set if no load factor is specified.
    /// </summary>
    protected const float DEFAULT_LOAD_FACTOR = 0.75f;

    // The bit forced on for every occupied slot's fingerprint so the stored
    // value is always non-zero and a zero entry unambiguously means "empty".
    // It sits above every power-of-two table mask, so `fingerprint & mask`
    // recovers the natural slot index without masking the hash separately.
    private const int OCCUPIED_BIT = unchecked((int)0x80000000);

    private int _count = 0;
    private T?[] _items;

    // The struct-of-arrays metadata buffer: one cached hash fingerprint per
    // slot. Zero marks an empty slot; an occupied slot stores `hash | OCCUPIED_BIT`.
    private int[] _fingerprints;

    private readonly float _loadFactor;
    private int _threshold;
    private readonly THasher _hasher;

    // The value default(T) (null for reference types, 0 for primitives,
    // Guid.Empty for Guid, etc.) collides with the "empty slot" sentinel used
    // during probing, so it is stored out-of-band. _count includes this entry
    // when _hasDefaultValue is true.
    private bool _hasDefaultValue;

    // Incremented on every structural mutation so active enumerators can detect
    // concurrent modification and throw, matching BCL semantics.
    private int _version;

    /// <summary>
    /// Initializes a new instance of the <see cref="HashCachingSet{T,THasher}"/> class
    /// using the specified capacity and load factor.
    /// </summary>
    /// <param name="capacity">
    /// The initial capacity, automatically rounded to the next power of two.
    /// </param>
    /// <param name="loadFactor">
    /// The fraction of the set's size that can be filled before resizing.
    /// </param>
    public HashCachingSet(
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (loadFactor <= 0f || loadFactor >= 1f)
            throw new ArgumentOutOfRangeException(nameof(loadFactor), loadFactor, "Load factor must be between 0 (exclusive) and 1 (exclusive).");

        int size = FastUtils.NextPowerOfTwo(capacity);

        _items = new T?[size];
        _fingerprints = new int[size];
        _loadFactor = loadFactor;
        _threshold = (int)(size * _loadFactor);
        _hasher = default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HashCachingSet{T,THasher}"/>
    /// class containing the elements copied from the specified
    /// <paramref name="source"/>.
    /// </summary>
    /// <param name="source">
    /// The collection whose elements are copied into the new set. If
    /// <paramref name="source"/> implements <see cref="ICollection{T}"/>, its
    /// <c>Count</c> is used to size the backing storage so the initial fill avoids
    /// resize work. Duplicate elements (including duplicate <c>default(T)</c>
    /// entries) are silently deduplicated, matching BCL <see cref="HashSet{T}"/>
    /// semantics.
    /// </param>
    /// <param name="capacity">
    /// The minimum initial capacity, rounded up to the next power of two. When the
    /// source's count is larger, the backing store is sized — including load-factor
    /// headroom — to hold the whole source without resizing.
    /// </param>
    /// <param name="loadFactor">
    /// Determines the maximum ratio of count to capacity before resizing.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is <c>null</c>.
    /// </exception>
    public HashCachingSet(
        IEnumerable<T> source,
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
        : this(InitialCapacityForSource(source, capacity, loadFactor), loadFactor)
    {
        foreach (T item in source)
        {
            TryAdd(item);
        }
    }

    // Runs as part of the chained-ctor argument expression so the null check beats
    // the primary ctor's capacity / loadFactor validation: a null source must
    // surface as ArgumentNullException, not ArgumentOutOfRangeException when the
    // user also passed an invalid loadFactor. Mirrors CeleritySet / HashCachingDictionary.
    private static int InitialCapacityForSource(IEnumerable<T> source, int capacity, float loadFactor)
    {
        ArgumentNullException.ThrowIfNull(source);
        int count = (source as ICollection<T>)?.Count ?? 0;

        if (count > 0 && loadFactor > 0f && loadFactor < 1f)
        {
            int withHeadroom = (int)Math.Ceiling(count / (double)loadFactor);
            if (withHeadroom > count)
                count = withHeadroom;
        }

        return Math.Max(capacity, count);
    }

    /// <summary>
    /// Gets the number of elements contained in the set.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Adds the specified element to the set.
    /// Throws <see cref="ArgumentException"/> if the element already exists.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="item"/> already exists in the set.
    /// </exception>
    public void Add(T item)
    {
        if (!TryAdd(item))
            throw new ArgumentException($"The element {item} already exists in the set.", nameof(item));
    }

    /// <summary>
    /// Attempts to add the specified element to the set.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <returns>
    /// <c>true</c> if the element was added successfully;
    /// <c>false</c> if the element already exists (the set is unchanged).
    /// </returns>
    public bool TryAdd(T item)
    {
        if (IsDefaultValue(item))
        {
            if (_hasDefaultValue)
                return false;
            _hasDefaultValue = true;
            _count++;
            _version++;
            return true;
        }

        // Probe the current table first: if the element already exists we return
        // without resizing, bumping _version, or swapping arrays — matching the
        // duplicate-at-threshold guarantee the rest of the family makes (#92). The
        // fingerprint carries the hash, so the probe and the insert share a single
        // Hash() call.
        int fingerprint = Fingerprint(item);
        int index = ProbeForInsert(fingerprint, item, out bool wasEmpty);
        if (!wasEmpty)
            return false;

        if (_count >= _threshold)
        {
            Resize();
            index = ProbeForInsert(fingerprint, item, out _);
        }

        WriteSlot(index, fingerprint, item);
        _count++;
        _version++;
        return true;
    }

    /// <summary>
    /// Determines whether the set contains the specified element.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    /// <returns><c>true</c> if the element is found; otherwise, <c>false</c>.</returns>
    public bool Contains(T item)
    {
        if (IsDefaultValue(item))
            return _hasDefaultValue;

        return ProbeForItem(item) >= 0;
    }

    /// <summary>
    /// Removes the specified element from the set.
    /// </summary>
    /// <param name="item">The element to remove.</param>
    /// <returns>
    /// <c>true</c> if the element was successfully removed; otherwise, <c>false</c>.
    /// Also returns <c>false</c> if the element was not found.
    /// </returns>
    public bool Remove(T item)
    {
        if (IsDefaultValue(item))
        {
            if (!_hasDefaultValue)
                return false;
            _hasDefaultValue = false;
            _count--;
            _version++;
            return true;
        }

        int index = ProbeForItem(item);
        if (index < 0)
            return false;

        _count--;

        BackwardShiftRemove(index);
        _version++;
        return true;
    }

    /// <summary>
    /// Removes all elements from the set. The underlying capacity is preserved.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        Array.Clear(_items, 0, _items.Length);
        Array.Clear(_fingerprints, 0, _fingerprints.Length);
        _hasDefaultValue = false;
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Ensures that the set can hold at least <paramref name="capacity"/> elements without resizing,
    /// growing the backing table in a single rehash if it is currently smaller. Pre-sizing before a
    /// bulk insert of a known size avoids the incremental rehashes an unsized set would otherwise pay
    /// as it grows. The set is never shrunk by this call.
    /// </summary>
    /// <param name="capacity">The minimum number of elements the set must hold without resizing.</param>
    /// <returns>The number of elements the set can now hold before the next resize.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public int EnsureCapacity(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        if (_threshold < capacity)
        {
            int newSize = FastUtils.MinTableSizeFor(capacity, _loadFactor);
            if (newSize > _items.Length)
            {
                Resize(newSize);
                _version++;
            }
        }

        return _threshold;
    }

    /// <summary>
    /// Reduces the backing table to the smallest power-of-two size that still holds the current
    /// <see cref="Count"/> without resizing, reclaiming memory after the set has shrunk. The
    /// out-of-band default-value entry is preserved.
    /// </summary>
    public void TrimExcess() => TrimExcess(_count);

    /// <summary>
    /// Reduces (or grows) the backing table to the smallest power-of-two size that holds at least
    /// <paramref name="capacity"/> elements without resizing.
    /// </summary>
    /// <param name="capacity">
    /// The number of elements to size the table for. Must be at least the current <see cref="Count"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is less than the current <see cref="Count"/>.
    /// </exception>
    public void TrimExcess(int capacity)
    {
        if (capacity < _count)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least the current Count.");

        int newSize = FastUtils.MinTableSizeFor(capacity, _loadFactor);
        if (newSize != _items.Length)
        {
            Resize(newSize);
            _version++;
        }
    }

    /// <summary>
    /// Returns an allocation-free enumerator that yields each element stored in the
    /// set. The enumeration order is unspecified and may change across versions; do
    /// not rely on it. The out-of-band <c>default(T)</c> entry (if present) is
    /// yielded first — for reference-type elements that includes <c>null</c>. If the
    /// set is modified during enumeration, <see cref="Enumerator.MoveNext"/> throws
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over this set.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ── ISet<T> / ICollection<T> set-algebra surface ──────────────────────────
    // The set-algebra logic is shared across the mutable set family via
    // SetOperations, written once against the ISet<T> primitives every set
    // exposes; the semantics match BCL HashSet<T> exactly.

    /// <summary>
    /// Modifies the set to contain all elements that are present in itself, in
    /// <paramref name="other"/>, or in both.
    /// </summary>
    /// <param name="other">The collection to union into this set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void UnionWith(IEnumerable<T> other) => SetOperations.UnionWith(this, other);

    /// <summary>
    /// Modifies the set to contain only elements that are also present in
    /// <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to intersect with this set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void IntersectWith(IEnumerable<T> other) => SetOperations.IntersectWith(this, other);

    /// <summary>
    /// Removes every element in <paramref name="other"/> from the set.
    /// </summary>
    /// <param name="other">The collection of elements to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void ExceptWith(IEnumerable<T> other) => SetOperations.ExceptWith(this, other);

    /// <summary>
    /// Modifies the set to contain only elements that are present either in itself
    /// or in <paramref name="other"/>, but not both.
    /// </summary>
    /// <param name="other">The collection to apply the symmetric difference with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void SymmetricExceptWith(IEnumerable<T> other) => SetOperations.SymmetricExceptWith(this, other);

    /// <summary>
    /// Determines whether the set is a subset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if every element of this set is in <paramref name="other"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSubsetOf(IEnumerable<T> other) => SetOperations.IsSubsetOf(this, other);

    /// <summary>
    /// Determines whether the set is a proper (strict) subset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of this set is in <paramref name="other"/> and
    /// <paramref name="other"/> has at least one element this set does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSubsetOf(IEnumerable<T> other) => SetOperations.IsProperSubsetOf(this, other);

    /// <summary>
    /// Determines whether the set is a superset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if every element of <paramref name="other"/> is in this set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSupersetOf(IEnumerable<T> other) => SetOperations.IsSupersetOf(this, other);

    /// <summary>
    /// Determines whether the set is a proper (strict) superset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of <paramref name="other"/> is in this set and
    /// this set has at least one element <paramref name="other"/> does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSupersetOf(IEnumerable<T> other) => SetOperations.IsProperSupersetOf(this, other);

    /// <summary>
    /// Determines whether the set and <paramref name="other"/> share at least one element.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two share any element.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool Overlaps(IEnumerable<T> other) => SetOperations.Overlaps(this, other);

    /// <summary>
    /// Determines whether the set and <paramref name="other"/> contain the same distinct elements.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two contain exactly the same elements.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool SetEquals(IEnumerable<T> other) => SetOperations.SetEquals(this, other);

    /// <summary>
    /// Copies the elements of the set to the specified <paramref name="array"/>, starting at
    /// <paramref name="arrayIndex"/>. The out-of-band <c>default(T)</c> element (if present) is
    /// copied first.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
    public void CopyTo(T[] array, int arrayIndex) => SetOperations.CopyTo(this, _count, array, arrayIndex);

    // Adds the element, returning whether it was newly added (ISet<T> semantics) —
    // the non-throwing counterpart of the public throw-on-duplicate Add(T).
    bool ISet<T>.Add(T item) => TryAdd(item);

    // ICollection<T>.Add must not throw on a duplicate (unlike the public Add(T)),
    // so it maps to the non-throwing TryAdd.
    void ICollection<T>.Add(T item) => TryAdd(item);

    bool ICollection<T>.IsReadOnly => false;

    /// <summary>
    /// A struct enumerator over a <see cref="HashCachingSet{T,THasher}"/>. Because it
    /// is a struct, iterating it via <c>foreach</c> avoids the allocation that a
    /// compiler-generated <c>IEnumerator&lt;T&gt;</c> would incur. The out-of-band
    /// <c>default(T)</c> entry (if present) is yielded first.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly HashCachingSet<T, THasher> _set;
        private readonly int _version;
        private int _index;
        private T? _current;
        private State _state;

        private enum State : byte
        {
            BeforeDefault,
            InArray,
            Done
        }

        internal Enumerator(HashCachingSet<T, THasher> set)
        {
            _set = set;
            _version = set._version;
            _index = -1;
            _current = default;
            _state = State.BeforeDefault;
        }

        /// <summary>
        /// Gets the element at the current position of the enumerator.
        /// </summary>
        public T Current => _current!;

        object? IEnumerator.Current => _current;

        /// <summary>
        /// Advances the enumerator to the next element.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the enumerator advanced to a new entry; <c>false</c> if it
        /// has passed the end of the set.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the set was modified since the enumerator was created.
        /// </exception>
        public bool MoveNext()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            if (_state == State.BeforeDefault)
            {
                _state = State.InArray;
                if (_set._hasDefaultValue)
                {
                    _current = default;
                    return true;
                }
            }

            if (_state == State.InArray)
            {
                int[] fingerprints = _set._fingerprints;
                T?[] items = _set._items;
                int length = fingerprints.Length;
                ref int fpRef = ref MemoryMarshal.GetArrayDataReference(fingerprints);
                ref T? itemsRef = ref MemoryMarshal.GetArrayDataReference(items);
                while (++_index < length)
                {
                    if (Unsafe.Add(ref fpRef, (nint)(uint)_index) != 0)
                    {
                        _current = Unsafe.Add(ref itemsRef, (nint)(uint)_index);
                        return true;
                    }
                }
                _state = State.Done;
            }

            _current = default;
            return false;
        }

        /// <summary>
        /// Resets the enumerator to its initial position, before the first entry.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the set was modified since the enumerator was created.
        /// </exception>
        public void Reset()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _index = -1;
            _current = default;
            _state = State.BeforeDefault;
        }

        /// <summary>
        /// Releases any resources held by the enumerator. No-op for this type.
        /// </summary>
        public void Dispose() { }
    }

    private static bool IsDefaultValue(T item) =>
        EqualityComparer<T>.Default.Equals(item, default(T));

    // The cached fingerprint for an element: its hash with the top bit forced set
    // so the stored metadata is always non-zero (zero is reserved for "empty").
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Fingerprint(T item) => _hasher.Hash(item) | OCCUPIED_BIT;

    // Writes a complete entry — fingerprint, item — into the given slot.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteSlot(int index, int fingerprint, T item)
    {
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_fingerprints), (nint)(uint)index) = fingerprint;
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_items), (nint)(uint)index) = item;
    }

    // Returns the slot the caller should write into. <paramref name="wasEmpty"/>
    // tells the caller whether the slot was previously empty (true → new entry,
    // bump _count) or already held the same element (false → duplicate).
    //
    // The probe scans only the dense fingerprint array — `index = fingerprint &
    // mask` recovers the natural slot because the forced occupied bit sits above
    // the mask. An element is touched (and the full equality check run) only when
    // the cached fingerprint matches, so cache-cold misses and expensive-equality
    // elements short-circuit on a single integer compare. The bound is structural:
    // `mask = length - 1` and `index = ... & mask` keep `index ∈ [0, length)`.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ProbeForInsert(int fingerprint, T item, out bool wasEmpty)
    {
        int[] fingerprints = _fingerprints;
        T?[] items = _items;
        ref int fpRef = ref MemoryMarshal.GetArrayDataReference(fingerprints);
        ref T? itemsRef = ref MemoryMarshal.GetArrayDataReference(items);
        int mask = fingerprints.Length - 1;
        var comparer = EqualityComparer<T>.Default;
        int index = fingerprint & mask;

        while (true)
        {
            int slotFp = Unsafe.Add(ref fpRef, (nint)(uint)index);
            if (slotFp == 0) { wasEmpty = true; return index; }
            if (slotFp == fingerprint &&
                comparer.Equals(Unsafe.Add(ref itemsRef, (nint)(uint)index), item))
            {
                wasEmpty = false;
                return index;
            }
            index = (index + 1) & mask;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ProbeForItem(T item)
    {
        int[] fingerprints = _fingerprints;
        T?[] items = _items;
        ref int fpRef = ref MemoryMarshal.GetArrayDataReference(fingerprints);
        ref T? itemsRef = ref MemoryMarshal.GetArrayDataReference(items);
        int mask = fingerprints.Length - 1;
        var comparer = EqualityComparer<T>.Default;
        int fingerprint = Fingerprint(item);
        int index = fingerprint & mask;

        while (true)
        {
            int slotFp = Unsafe.Add(ref fpRef, (nint)(uint)index);
            if (slotFp == 0) return -1;
            if (slotFp == fingerprint &&
                comparer.Equals(Unsafe.Add(ref itemsRef, (nint)(uint)index), item))
            {
                return index;
            }
            index = (index + 1) & mask;
        }
    }

    private void Resize() => Resize(FastUtils.DoubleCapacity(_items.Length));

    // Rehashes every live element into freshly allocated tables of the given power-of-two size.
    // Shared by the doubling growth path and the EnsureCapacity / TrimExcess re-sizers, which pass
    // an explicit target. The caller guarantees newSize is a power of two strictly greater than the
    // in-table live count (so the probe loop always finds a vacant slot).
    private void Resize(int newSize)
    {
        int mask = newSize - 1;
        T?[] oldItems = _items;
        int[] oldFingerprints = _fingerprints;

        // Build into fresh local arrays then swap them in at the end. Because every
        // reinserted entry is known to be unique in the new table, the loop bypasses
        // the public insert path. The cached fingerprint carries the hash, so
        // `oldFp & mask` recovers the new natural slot without recomputing a single
        // hash — the resize never invokes the hasher. The default-value entry lives
        // out-of-band and is not touched here.
        T?[] newItems = new T?[newSize];
        int[] newFingerprints = new int[newSize];
        ref T? oldItemsRef = ref MemoryMarshal.GetArrayDataReference(oldItems);
        ref int oldFpRef = ref MemoryMarshal.GetArrayDataReference(oldFingerprints);
        ref T? newItemsRef = ref MemoryMarshal.GetArrayDataReference(newItems);
        ref int newFpRef = ref MemoryMarshal.GetArrayDataReference(newFingerprints);

        for (int i = 0; i < oldFingerprints.Length; i++)
        {
            int fp = Unsafe.Add(ref oldFpRef, (nint)(uint)i);
            if (fp == 0)
                continue;

            int index = fp & mask;
            while (Unsafe.Add(ref newFpRef, (nint)(uint)index) != 0)
                index = (index + 1) & mask;

            Unsafe.Add(ref newFpRef, (nint)(uint)index) = fp;
            Unsafe.Add(ref newItemsRef, (nint)(uint)index) = Unsafe.Add(ref oldItemsRef, (nint)(uint)i);
        }

        _items = newItems;
        _fingerprints = newFingerprints;
        _threshold = (int)(newSize * _loadFactor);
    }

    // Backward-shift deletion (Knuth TAOCP Vol 3, §6.4 Algorithm R). The caller has
    // located the slot but has NOT cleared it; this helper writes the final empty
    // entry itself once the gap settles. The natural slot of each candidate comes
    // straight from its cached fingerprint (`fp & mask`), so the shift logic never
    // rehashes.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BackwardShiftRemove(int startIndex)
    {
        int[] fingerprints = _fingerprints;
        T?[] items = _items;
        ref int fpRef = ref MemoryMarshal.GetArrayDataReference(fingerprints);
        ref T? itemsRef = ref MemoryMarshal.GetArrayDataReference(items);
        int mask = fingerprints.Length - 1;
        int i = startIndex;
        int j = i;

        while (true)
        {
            j = (j + 1) & mask;
            int candidateFp = Unsafe.Add(ref fpRef, (nint)(uint)j);
            if (candidateFp == 0)
                break;

            int k = candidateFp & mask;

            // Shift slot j into the gap at i iff the probe chain from its natural
            // slot k to its current slot j passes through i (so leaving i empty
            // would orphan the entry). When the scan has not wrapped (i <= j), that
            // means k is outside the open interval (i, j]; when it has wrapped
            // (i > j), the test mirrors across the array boundary.
            bool bypassesGap = (i <= j)
                ? (i < k && k <= j)
                : (i < k || k <= j);
            if (bypassesGap)
                continue;

            Unsafe.Add(ref fpRef, (nint)(uint)i) = candidateFp;
            Unsafe.Add(ref itemsRef, (nint)(uint)i) = Unsafe.Add(ref itemsRef, (nint)(uint)j);
            i = j;
        }

        Unsafe.Add(ref fpRef, (nint)(uint)i) = 0;
        Unsafe.Add(ref itemsRef, (nint)(uint)i) = default;
    }
}
