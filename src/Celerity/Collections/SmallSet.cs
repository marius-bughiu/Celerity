using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Celerity.Collections;

/// <summary>
/// A set optimized for the very-small case (<c>n &lt;= ~16</c>), where a linear
/// scan over a flat backing array beats a probe-based hash table. The set
/// counterpart to <see cref="SmallDictionary{TKey, TValue}"/>: per-scope "seen"
/// sets, small membership guards, and deduplicating a handful of items are the
/// shapes it is built for, where most instances stay tiny.
/// </summary>
/// <typeparam name="T">The type of elements in the set.</typeparam>
/// <remarks>
/// <para>
/// Unlike the open-addressed Celerity sets, <see cref="SmallSet{T}"/> stores
/// elements in an insertion-dense flat array and answers every query with a linear
/// scan using <see cref="EqualityComparer{T}.Default"/>. For small element counts
/// this is faster than hashing: there is no hash to compute, no modulo / mask, and
/// the whole array fits in a cache line or two, so the branch predictor and
/// prefetcher do the rest. The trade-off is that lookups are <c>O(n)</c> rather than
/// <c>O(1)</c>, so the type degrades for large sets — keep it to the small-<c>n</c>
/// workloads it is built for (see the choosing-a-collection guidance in the README).
/// It does <em>not</em> auto-promote to a hash table; it simply grows its array and
/// keeps scanning.
/// </para>
/// <para>
/// Because lookups never hash, there is no empty-slot sentinel and therefore no
/// special-casing of <c>default(T)</c>: a <c>0</c>, <c>null</c>, or
/// <see cref="System.Guid.Empty"/> element is stored inline like any other. This is a
/// small simplification over the hash-table sets, which keep the default element
/// out-of-band.
/// </para>
/// <para>
/// It implements <see cref="ISet{T}"/> (and therefore <see cref="ICollection{T}"/> /
/// <see cref="IEnumerable{T}"/>) with the full BCL <see cref="HashSet{T}"/>
/// set-algebra surface — <c>UnionWith</c> / <c>IntersectWith</c> / <c>ExceptWith</c> /
/// <c>SymmetricExceptWith</c>, the subset / superset / <c>Overlaps</c> / <c>SetEquals</c>
/// query family, and <c>CopyTo</c> — so it drops in wherever an <see cref="ISet{T}"/>
/// is expected.
/// </para>
/// <para>
/// The type is single-threaded and does not guarantee enumeration order; in
/// particular <see cref="Remove(T)"/> moves the last element into the vacated slot,
/// so the order after a removal is unspecified.
/// </para>
/// </remarks>
public class SmallSet<T> : ISet<T>
{
    /// <summary>
    /// The capacity the set grows to on its first insert when constructed with no
    /// (or a zero) capacity hint.
    /// </summary>
    protected const int DEFAULT_CAPACITY = 4;

    private T?[] _items;
    private int _count;

    // Incremented on every structural mutation so active enumerators can detect
    // concurrent modification and throw, matching BCL semantics.
    private int _version;

    /// <summary>
    /// Initializes a new, empty <see cref="SmallSet{T}"/> with the specified initial
    /// capacity.
    /// </summary>
    /// <param name="capacity">
    /// The number of elements the backing array is sized for up front. Unlike the
    /// hash-table sets this is used verbatim (it is not rounded to a power of two),
    /// since there is no probe mask. A value of <c>0</c> defers allocation until the
    /// first insert.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative.
    /// </exception>
    public SmallSet(int capacity = DEFAULT_CAPACITY)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        _items = capacity == 0 ? Array.Empty<T?>() : new T?[capacity];
    }

    /// <summary>
    /// Initializes a new <see cref="SmallSet{T}"/> that contains the distinct
    /// elements copied from the specified <paramref name="source"/>.
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
    /// The minimum initial capacity. The final capacity is the larger of this value
    /// and the source's count.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is <c>null</c>.
    /// </exception>
    public SmallSet(IEnumerable<T> source, int capacity = DEFAULT_CAPACITY)
        : this(InitialCapacityForSource(source, capacity))
    {
        foreach (T item in source)
        {
            TryAdd(item);
        }
    }

    // Runs as part of the chained-ctor argument expression so the null check beats
    // the primary ctor's capacity validation: a null source must surface as
    // ArgumentNullException, not ArgumentOutOfRangeException, even when the caller
    // also passed a negative capacity. Mirrors SmallDictionary.
    private static int InitialCapacityForSource(IEnumerable<T> source, int capacity)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Math.Max(capacity, (source as ICollection<T>)?.Count ?? 0);
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
        if (IndexOf(item) >= 0)
            return false;

        Append(item);
        return true;
    }

    /// <summary>
    /// Determines whether the set contains the specified element.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    /// <returns><c>true</c> if the element is found; otherwise, <c>false</c>.</returns>
    public bool Contains(T item) => IndexOf(item) >= 0;

    /// <summary>
    /// Removes the specified element from the set.
    /// </summary>
    /// <param name="item">The element to remove.</param>
    /// <returns>
    /// <c>true</c> if the element was successfully removed; otherwise, <c>false</c>.
    /// Also returns <c>false</c> if the element was not found.
    /// </returns>
    /// <remarks>
    /// Removal moves the last element into the vacated slot (an <c>O(1)</c> swap once
    /// the element is found), so the relative order of the surviving elements is not
    /// preserved.
    /// </remarks>
    public bool Remove(T item)
    {
        int index = IndexOf(item);
        if (index < 0)
            return false;

        int last = _count - 1;
        _items[index] = _items[last];
        _items[last] = default;
        _count--;
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

        Array.Clear(_items, 0, _count);
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Ensures that the set's backing array can hold at least <paramref name="capacity"/>
    /// elements without growing, enlarging it in a single copy if it is currently smaller.
    /// Pre-sizing before a bulk insert of a known size avoids the incremental array doublings an
    /// unsized set would otherwise pay. The set is never shrunk by this call.
    /// </summary>
    /// <param name="capacity">The minimum number of elements the backing array must hold.</param>
    /// <returns>The capacity (backing-array length) the set can now hold before it grows.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public int EnsureCapacity(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        if (capacity > _items.Length)
        {
            // Sized verbatim, mirroring the constructor: SmallSet has no probe mask, so the
            // backing array needs no power-of-two length.
            Array.Resize(ref _items, capacity);
            _version++;
        }

        return _items.Length;
    }

    /// <summary>
    /// Reduces the backing array to exactly the current <see cref="Count"/>, reclaiming memory after
    /// the set has shrunk.
    /// </summary>
    public void TrimExcess() => TrimExcess(_count);

    /// <summary>
    /// Reduces (or grows) the backing array to hold exactly <paramref name="capacity"/> elements.
    /// </summary>
    /// <param name="capacity">
    /// The number of elements to size the array for. Must be at least the current <see cref="Count"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is less than the current <see cref="Count"/>.
    /// </exception>
    public void TrimExcess(int capacity)
    {
        if (capacity < _count)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least the current Count.");

        if (capacity != _items.Length)
        {
            Array.Resize(ref _items, capacity);
            _version++;
        }
    }

    /// <summary>
    /// Returns an allocation-free enumerator that yields each element stored in the
    /// set. The enumeration order is unspecified and may change across versions; do
    /// not rely on it. If the set is modified during enumeration,
    /// <see cref="Enumerator.MoveNext"/> throws <see cref="InvalidOperationException"/>.
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
    /// <paramref name="arrayIndex"/>.
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

    // Appends a known-new element, growing the backing array first when it is full.
    // Callers must have already confirmed the element is absent.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Append(T item)
    {
        if (_count == _items.Length)
            Grow();

        _items[_count] = item;
        _count++;
        _version++;
    }

    private void Grow()
    {
        int newCapacity = _items.Length == 0 ? DEFAULT_CAPACITY : FastUtils.DoubleCapacity(_items.Length);
        Array.Resize(ref _items, newCapacity);
    }

    // Linear scan over the dense [0, _count) prefix. The scan walks _items via
    // Unsafe.Add against a single base reference grabbed at the top, so the
    // per-iteration bounds check disappears; i < _count keeps the index in range.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int IndexOf(T item)
    {
        T?[] items = _items;
        ref T? itemsRef = ref MemoryMarshal.GetArrayDataReference(items);
        var comparer = EqualityComparer<T>.Default;
        int count = _count;
        for (int i = 0; i < count; i++)
        {
            if (comparer.Equals(Unsafe.Add(ref itemsRef, (nint)(uint)i), item))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// A struct enumerator over a <see cref="SmallSet{T}"/>. Because it is a struct,
    /// iterating it via <c>foreach</c> avoids the allocation that a
    /// compiler-generated <c>IEnumerator&lt;T&gt;</c> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly SmallSet<T> _set;
        private readonly int _version;
        private int _index;
        private T? _current;

        internal Enumerator(SmallSet<T> set)
        {
            _set = set;
            _version = set._version;
            _index = -1;
            _current = default;
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

            SmallSet<T> set = _set;
            if (++_index < set._count)
            {
                _current = set._items[_index];
                return true;
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
        }

        /// <summary>
        /// Releases any resources held by the enumerator. No-op for this type.
        /// </summary>
        public void Dispose() { }
    }
}
