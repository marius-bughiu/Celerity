using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A high-performance generic hash set whose backing array is rented from
/// <see cref="ArrayPool{T}"/> instead of being allocated on the managed heap, so
/// repeated build-up / tear-down cycles in high-throughput code paths put far
/// less pressure on the garbage collector.
/// </summary>
/// <typeparam name="T">The type of elements in the set.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute element hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
/// <remarks>
/// <para>
/// The public surface is byte-for-byte identical to
/// <see cref="CeleritySet{T, THasher}"/> — open-addressed, linear-probing storage
/// with an out-of-band <c>default(T)</c> slot and backward-shift deletion, and the
/// full <see cref="ISet{T}"/> set-algebra surface — with one addition: the type
/// implements <see cref="IDisposable"/>. Because the backing array is
/// <em>borrowed</em> from <see cref="ArrayPool{T}.Shared"/>, you should call
/// <see cref="Dispose"/> (e.g. via a <c>using</c> statement) when finished so the
/// array returns to the pool for reuse. Failing to dispose is not a leak — the
/// array is reclaimed by the GC like any other managed array — you simply forfeit
/// the pooling benefit.
/// </para>
/// <para>
/// <see cref="ArrayPool{T}.Shared"/> never throws on exhaustion: when the pool has
/// no suitable buffer it allocates a fresh one, so callers never have to handle a
/// "pool empty" condition. Rented buffers may be <em>larger</em> than requested,
/// so this type tracks its logical power-of-two capacity (<c>_size</c> / its mask)
/// independently of the backing array's <c>Length</c> and only ever reads or
/// writes the live region. On return, an array of a reference type (or a type that
/// contains references) is cleared so the pool does not pin your elements alive
/// after disposal.
/// </para>
/// <para>
/// This type is not thread-safe. Like every Celerity collection, concurrent
/// mutation must be synchronized by the caller. After <see cref="Dispose"/> every
/// member throws <see cref="ObjectDisposedException"/>.
/// </para>
/// </remarks>
public class PooledCeleritySet<T, THasher> : ISet<T>, IDisposable
    where THasher : struct, IHashProvider<T>
{
    /// <summary>
    /// The default initial capacity of the set if no capacity is specified.
    /// </summary>
    protected const int DEFAULT_CAPACITY = 16;

    /// <summary>
    /// The default load factor of the set if no load factor is specified.
    /// </summary>
    protected const float DEFAULT_LOAD_FACTOR = 0.75f;

    private int _count = 0;
    private T?[] _slots;

    // The rented array may be longer than the logical table, so the power-of-two
    // size and its probe mask are tracked here rather than derived from
    // _slots.Length (which ArrayPool is free to over-provision). Only the region
    // [0, _size) of the rented array is ever read or written.
    private int _size;
    private int _mask;

    private readonly float _loadFactor;
    private int _threshold;
    private readonly THasher _hasher;

    // The value default(T) (null for reference types, 0 for primitives,
    // Guid.Empty for Guid, etc.) collides with the "empty slot" sentinel used
    // during probing, so it's stored out-of-band. _count includes this entry
    // when _hasDefaultValue is true.
    private bool _hasDefaultValue;

    // Bumped on every entry-point structural mutation so active enumerators can
    // detect concurrent modification and fast-fail, matching BCL HashSet<T>.
    private int _version;

    // Set by Dispose; every public member rejects calls once true.
    private bool _disposed;

    // Whether the element type holds managed references, computed once. When true,
    // the rented array is cleared on return so the pool does not keep elements
    // reachable (memory-leak prevention); a value-type array skips the clear.
    private static readonly bool ClearSlotsOnReturn =
        RuntimeHelpers.IsReferenceOrContainsReferences<T?>();

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="PooledCeleritySet{T, THasher}"/> class using the specified
    /// capacity and load factor. The backing array is rented from
    /// <see cref="ArrayPool{T}.Shared"/>.
    /// </summary>
    /// <param name="capacity">
    /// The initial capacity, automatically rounded to the next power of two.
    /// </param>
    /// <param name="loadFactor">
    /// The fraction of the set's size that can be filled before resizing.
    /// </param>
    public PooledCeleritySet(
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (loadFactor <= 0f || loadFactor >= 1f)
            throw new ArgumentOutOfRangeException(nameof(loadFactor), loadFactor, "Load factor must be between 0 (exclusive) and 1 (exclusive).");

        int size = FastUtils.NextPowerOfTwo(capacity);

        _slots = RentSlots(size);
        _size = size;
        _mask = size - 1;
        _loadFactor = loadFactor;
        _threshold = (int)(size * _loadFactor);
        _hasher = default;
    }

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="PooledCeleritySet{T, THasher}"/> class containing the elements
    /// copied from the specified <paramref name="source"/>. The backing array is
    /// rented from <see cref="ArrayPool{T}.Shared"/>.
    /// </summary>
    /// <param name="source">
    /// The collection whose elements are copied into the new set. If
    /// <paramref name="source"/> implements <see cref="ICollection{T}"/>, its
    /// <c>Count</c> is used to size the backing storage so the initial fill
    /// avoids resize work. Duplicate elements (including duplicate
    /// <c>default(T)</c> entries) are silently deduplicated, matching BCL
    /// <see cref="HashSet{T}"/> semantics.
    /// </param>
    /// <param name="capacity">
    /// The minimum initial capacity, rounded up to the next power of two. When
    /// the source's count is larger, the backing store is sized — including
    /// load-factor headroom — to hold the whole source without resizing.
    /// </param>
    /// <param name="loadFactor">
    /// Determines the maximum ratio of count to capacity before resizing.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is <c>null</c>.
    /// </exception>
    public PooledCeleritySet(
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

    // Runs as part of the chained-ctor argument expression so the null check
    // beats the primary ctor's capacity / loadFactor validation: a null source
    // must surface as ArgumentNullException, not ArgumentOutOfRangeException
    // when the user also passed an invalid loadFactor.
    private static int InitialCapacityForSource(IEnumerable<T> source, int capacity, float loadFactor)
    {
        ArgumentNullException.ThrowIfNull(source);
        int count = (source as ICollection<T>)?.Count ?? 0;

        // Size for the source count *including* load-factor headroom so the bulk
        // fill never rehashes mid-construction (matching CeleritySet, #27).
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
    /// <exception cref="ObjectDisposedException">The set has been disposed.</exception>
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
    /// <exception cref="ObjectDisposedException">The set has been disposed.</exception>
    public bool TryAdd(T item)
    {
        ThrowIfDisposed();
        if (IsDefaultValue(item))
        {
            if (_hasDefaultValue)
                return false;
            _hasDefaultValue = true;
            _count++;
            _version++;
            return true;
        }

        // Probe the current table first: if the item already exists, we
        // return without touching anything — no Resize, no _version bump,
        // no array swap. The threshold check is deferred to after the
        // duplicate check so a duplicate-at-threshold call cannot silently
        // swap out the backing array under an active enumerator (see #92).
        T?[] slots = _slots;
        ref T? slotsRef = ref MemoryMarshal.GetArrayDataReference(slots);
        int mask = _mask;
        var comparer = EqualityComparer<T>.Default;
        int index = _hasher.Hash(item) & mask;

        while (true)
        {
            T? slot = Unsafe.Add(ref slotsRef, (nint)(uint)index);
            if (comparer.Equals(slot, default(T))) break;
            if (comparer.Equals(slot, item)) return false;
            index = (index + 1) & mask;
        }

        if (_count >= _threshold)
        {
            Resize();
            slots = _slots;
            slotsRef = ref MemoryMarshal.GetArrayDataReference(slots);
            mask = _mask;
            index = _hasher.Hash(item) & mask;
            while (!comparer.Equals(Unsafe.Add(ref slotsRef, (nint)(uint)index), default(T)))
                index = (index + 1) & mask;
        }

        Unsafe.Add(ref slotsRef, (nint)(uint)index) = item;
        _count++;
        _version++;
        return true;
    }

    /// <summary>
    /// Determines whether the set contains the specified element.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    /// <returns><c>true</c> if the element is found; otherwise, <c>false</c>.</returns>
    /// <exception cref="ObjectDisposedException">The set has been disposed.</exception>
    public bool Contains(T item)
    {
        ThrowIfDisposed();
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
    /// <exception cref="ObjectDisposedException">The set has been disposed.</exception>
    public bool Remove(T item)
    {
        ThrowIfDisposed();
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
    /// Removes all elements from the set. The underlying rented capacity is
    /// preserved (the array is not returned to the pool).
    /// </summary>
    /// <exception cref="ObjectDisposedException">The set has been disposed.</exception>
    public void Clear()
    {
        ThrowIfDisposed();
        if (_count == 0)
            return;

        // Only the live region needs clearing; the tail is already treated as
        // out-of-bounds by every reader.
        Array.Clear(_slots, 0, _size);
        _hasDefaultValue = false;
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Ensures that the set can hold at least <paramref name="capacity"/> elements without resizing,
    /// renting a larger backing table in a single rehash if it is currently smaller. Pre-sizing before
    /// a bulk insert of a known size avoids the incremental rehashes — and the rent/return churn — an
    /// unsized set would otherwise pay as it grows. The set is never shrunk by this call.
    /// </summary>
    /// <param name="capacity">The minimum number of elements the set must hold without resizing.</param>
    /// <returns>The number of elements the set can now hold before the next resize.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    /// <exception cref="ObjectDisposedException">The set has been disposed.</exception>
    public int EnsureCapacity(int capacity)
    {
        ThrowIfDisposed();
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        if (_threshold < capacity)
        {
            int newSize = FastUtils.MinTableSizeFor(capacity, _loadFactor);
            if (newSize > _size)
            {
                Resize(newSize);
                _version++;
            }
        }

        return _threshold;
    }

    /// <summary>
    /// Reduces the backing table to the smallest power-of-two size that still holds the current
    /// <see cref="Count"/> without resizing, returning the larger rented buffer to the pool. The
    /// out-of-band default-value entry is preserved.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The set has been disposed.</exception>
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
    /// <exception cref="ObjectDisposedException">The set has been disposed.</exception>
    public void TrimExcess(int capacity)
    {
        ThrowIfDisposed();
        if (capacity < _count)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least the current Count.");

        int newSize = FastUtils.MinTableSizeFor(capacity, _loadFactor);
        if (newSize != _size)
        {
            Resize(newSize);
            _version++;
        }
    }

    /// <summary>
    /// Returns the rented backing array to <see cref="ArrayPool{T}.Shared"/> and
    /// marks the set as disposed. After disposal every member throws
    /// <see cref="ObjectDisposedException"/>. Calling <see cref="Dispose"/> more
    /// than once is safe and is a no-op after the first call.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        ArrayPool<T?>.Shared.Return(_slots, ClearSlotsOnReturn);

        // Drop the reference to the now-returned array so a use-after-dispose bug
        // cannot read or corrupt a buffer the pool may have handed to someone
        // else, and so the GC can reclaim anything still pinned.
        _slots = Array.Empty<T?>();
        _size = 0;
        _mask = 0;
        _count = 0;
        _hasDefaultValue = false;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Returns an allocation-free enumerator that yields each element stored in
    /// the set. The enumeration order is unspecified and may change across
    /// versions; do not rely on it. The out-of-band <c>default(T)</c> entry (if
    /// present) is yielded first — for reference-type elements that includes
    /// <c>null</c>. If the set is modified during enumeration,
    /// <see cref="Enumerator.MoveNext"/> throws
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over this set.</returns>
    /// <exception cref="ObjectDisposedException">The set has been disposed.</exception>
    public Enumerator GetEnumerator()
    {
        ThrowIfDisposed();
        return new Enumerator(this);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ── ISet<T> / ICollection<T> set-algebra surface ──────────────────────────
    // The set-algebra logic is shared across the mutable set family via
    // SetOperations, written once against the ISet<T> primitives every set
    // exposes; the semantics match BCL HashSet<T> exactly. Each entry point
    // rejects calls after disposal before delegating.

    /// <summary>
    /// Modifies the set to contain all elements that are present in itself, in
    /// <paramref name="other"/>, or in both.
    /// </summary>
    /// <param name="other">The collection to union into this set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">The set has been disposed.</exception>
    public void UnionWith(IEnumerable<T> other) { ThrowIfDisposed(); SetOperations.UnionWith(this, other); }

    /// <summary>
    /// Modifies the set to contain only elements that are also present in
    /// <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to intersect with this set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">The set has been disposed.</exception>
    public void IntersectWith(IEnumerable<T> other) { ThrowIfDisposed(); SetOperations.IntersectWith(this, other); }

    /// <summary>
    /// Removes every element in <paramref name="other"/> from the set.
    /// </summary>
    /// <param name="other">The collection of elements to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">The set has been disposed.</exception>
    public void ExceptWith(IEnumerable<T> other) { ThrowIfDisposed(); SetOperations.ExceptWith(this, other); }

    /// <summary>
    /// Modifies the set to contain only elements that are present either in itself
    /// or in <paramref name="other"/>, but not both.
    /// </summary>
    /// <param name="other">The collection to apply the symmetric difference with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">The set has been disposed.</exception>
    public void SymmetricExceptWith(IEnumerable<T> other) { ThrowIfDisposed(); SetOperations.SymmetricExceptWith(this, other); }

    /// <summary>
    /// Determines whether the set is a subset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if every element of this set is in <paramref name="other"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">The set has been disposed.</exception>
    public bool IsSubsetOf(IEnumerable<T> other) { ThrowIfDisposed(); return SetOperations.IsSubsetOf(this, other); }

    /// <summary>
    /// Determines whether the set is a proper (strict) subset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of this set is in <paramref name="other"/> and
    /// <paramref name="other"/> has at least one element this set does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">The set has been disposed.</exception>
    public bool IsProperSubsetOf(IEnumerable<T> other) { ThrowIfDisposed(); return SetOperations.IsProperSubsetOf(this, other); }

    /// <summary>
    /// Determines whether the set is a superset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if every element of <paramref name="other"/> is in this set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">The set has been disposed.</exception>
    public bool IsSupersetOf(IEnumerable<T> other) { ThrowIfDisposed(); return SetOperations.IsSupersetOf(this, other); }

    /// <summary>
    /// Determines whether the set is a proper (strict) superset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of <paramref name="other"/> is in this set and
    /// this set has at least one element <paramref name="other"/> does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">The set has been disposed.</exception>
    public bool IsProperSupersetOf(IEnumerable<T> other) { ThrowIfDisposed(); return SetOperations.IsProperSupersetOf(this, other); }

    /// <summary>
    /// Determines whether the set and <paramref name="other"/> share at least one element.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two share any element.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">The set has been disposed.</exception>
    public bool Overlaps(IEnumerable<T> other) { ThrowIfDisposed(); return SetOperations.Overlaps(this, other); }

    /// <summary>
    /// Determines whether the set and <paramref name="other"/> contain the same distinct elements.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two contain exactly the same elements.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ObjectDisposedException">The set has been disposed.</exception>
    public bool SetEquals(IEnumerable<T> other) { ThrowIfDisposed(); return SetOperations.SetEquals(this, other); }

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
    /// <exception cref="ObjectDisposedException">The set has been disposed.</exception>
    public void CopyTo(T[] array, int arrayIndex) { ThrowIfDisposed(); SetOperations.CopyTo(this, _count, array, arrayIndex); }

    // Adds the element, returning whether it was newly added (ISet<T> semantics) —
    // the non-throwing counterpart of the public throw-on-duplicate Add(T).
    bool ISet<T>.Add(T item) => TryAdd(item);

    // ICollection<T>.Add must not throw on a duplicate (unlike the public Add(T)),
    // so it maps to the non-throwing TryAdd.
    void ICollection<T>.Add(T item) => TryAdd(item);

    bool ICollection<T>.IsReadOnly => false;

    /// <summary>
    /// A struct enumerator over a <see cref="PooledCeleritySet{T, THasher}"/>.
    /// Because it is a struct, iterating it via <c>foreach</c> avoids the
    /// allocation that a compiler-generated <c>IEnumerator&lt;T&gt;</c> would
    /// incur. The out-of-band <c>default(T)</c> entry (if present) is yielded
    /// first.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly PooledCeleritySet<T, THasher> _set;
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

        internal Enumerator(PooledCeleritySet<T, THasher> set)
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
        /// <c>true</c> if the enumerator advanced to a new entry; <c>false</c>
        /// if it has passed the end of the set.
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
                T?[] slots = _set._slots;
                // Bound by the logical size, not slots.Length (rented tail is garbage).
                int length = _set._size;
                ref T? slotsRef = ref MemoryMarshal.GetArrayDataReference(slots);
                var comparer = EqualityComparer<T>.Default;
                while (++_index < length)
                {
                    T? slot = Unsafe.Add(ref slotsRef, (nint)(uint)_index);
                    if (!comparer.Equals(slot, default(T)))
                    {
                        _current = slot;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PooledCeleritySet<T, THasher>));
    }

    private static T?[] RentSlots(int size)
    {
        T?[] array = ArrayPool<T?>.Shared.Rent(size);
        // ArrayPool buffers are not zeroed; the probe treats default(T) as the
        // empty-slot sentinel, so the live region must be cleared after every rent.
        Array.Clear(array, 0, size);
        return array;
    }

    // Probes against the logical mask (_mask), not slots.Length - 1, because the
    // rented array may be larger than the logical table.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ProbeForItem(T item)
    {
        T?[] slots = _slots;
        ref T? slotsRef = ref MemoryMarshal.GetArrayDataReference(slots);
        int mask = _mask;
        var comparer = EqualityComparer<T>.Default;
        int index = _hasher.Hash(item) & mask;

        while (true)
        {
            T? slot = Unsafe.Add(ref slotsRef, (nint)(uint)index);
            if (comparer.Equals(slot, default(T))) return -1;
            if (comparer.Equals(slot, item)) return index;
            index = (index + 1) & mask;
        }
    }

    private void Resize() => Resize(FastUtils.DoubleCapacity(_size));

    // Rehashes every live element into a freshly rented array of the given power-of-two size, then
    // returns the old buffer to the pool. Shared by the doubling growth path and the
    // EnsureCapacity / TrimExcess re-sizers, which pass an explicit target. The caller guarantees
    // newSize is a power of two strictly greater than the in-table live count (so the probe loop
    // always finds a vacant slot).
    private void Resize(int newSize)
    {
        int mask = newSize - 1;
        int oldSize = _size;
        T?[] oldSlots = _slots;

        // Rent a fresh (cleared) array of the new size, rehash into it, then return
        // the old buffer to the pool. Every reinserted element is known unique (it
        // came from a valid set) so we bypass the equality / threshold bookkeeping.
        // The default-value entry lives out-of-band and is not touched here.
        T?[] newSlots = RentSlots(newSize);
        ref T? oldSlotsRef = ref MemoryMarshal.GetArrayDataReference(oldSlots);
        ref T? newSlotsRef = ref MemoryMarshal.GetArrayDataReference(newSlots);

        var comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < oldSize; i++)
        {
            T? item = Unsafe.Add(ref oldSlotsRef, (nint)(uint)i);
            if (comparer.Equals(item, default(T)))
                continue;

            int index = _hasher.Hash(item!) & mask;
            while (!comparer.Equals(Unsafe.Add(ref newSlotsRef, (nint)(uint)index), default(T)))
                index = (index + 1) & mask;

            Unsafe.Add(ref newSlotsRef, (nint)(uint)index) = item;
        }

        _slots = newSlots;
        _size = newSize;
        _mask = mask;
        _threshold = (int)(newSize * _loadFactor);

        ArrayPool<T?>.Shared.Return(oldSlots, ClearSlotsOnReturn);
    }

    // Backward-shift deletion (Knuth TAOCP Vol 3, §6.4 Algorithm R). The caller
    // has located the slot but has NOT cleared it; this helper writes the final
    // empty entry itself once the gap settles. Probes the logical mask (_mask),
    // not slots.Length - 1.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BackwardShiftRemove(int startIndex)
    {
        T?[] slots = _slots;
        ref T? slotsRef = ref MemoryMarshal.GetArrayDataReference(slots);
        int mask = _mask;
        var comparer = EqualityComparer<T>.Default;
        int i = startIndex;
        int j = i;

        while (true)
        {
            j = (j + 1) & mask;
            T? candidate = Unsafe.Add(ref slotsRef, (nint)(uint)j);
            if (comparer.Equals(candidate, default(T)))
                break;

            int k = _hasher.Hash(candidate!) & mask;

            // Shift slots[j] into the gap at i iff the probe chain from its natural
            // slot k to its current slot j passes through i (so leaving i empty
            // would orphan the entry).
            bool bypassesGap = (i <= j)
                ? (i < k && k <= j)
                : (i < k || k <= j);
            if (bypassesGap)
                continue;

            Unsafe.Add(ref slotsRef, (nint)(uint)i) = candidate;
            i = j;
        }

        Unsafe.Add(ref slotsRef, (nint)(uint)i) = default;
    }
}
