using System.Collections;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// A set of <b>non-negative integers over a bounded universe</b> <c>[0, Universe)</c>,
/// backed by the classic Briggs&#8211;Torczon sparse-set representation (a dense value
/// array paired with a sparse index array). It fills a BCL gap — .NET ships no sparse
/// set — and beats <see cref="HashSet{T}"/> on the two things that representation is
/// built for: an <c>O(1)</c> <see cref="Clear"/> that never scans or clears the backing
/// arrays, and dense, cache-friendly iteration over exactly the present elements.
/// </summary>
/// <remarks>
/// <para>
/// The set stores its members contiguously in a <em>dense</em> array (<c>[0, Count)</c>)
/// and keeps a <em>sparse</em> array, indexed by value, whose entry for a present value
/// points back at that value's slot in the dense array. Membership is the sparse&#8596;dense
/// round-trip <c>sparse[v] &lt; Count &amp;&amp; dense[sparse[v]] == v</c>, which is correct even
/// for a <em>stale</em> sparse entry — one left over from before a <see cref="Clear"/> or from a
/// slot never written since construction (the array is zero-initialized) — so <see cref="Clear"/>
/// need only reset the count (<c>O(1)</c>; the backing arrays are left untouched), and
/// <see cref="Add(int)"/> / <see cref="Contains(int)"/> / <see cref="Remove(int)"/> are each a
/// direct array index with no hashing, no probe chain, and no per-element allocation.
/// </para>
/// <para>
/// <b>The documented BCL-beating workload</b> is a set of small non-negative integers from a
/// known bounded universe that is <em>cleared and rebuilt frequently</em> and <em>iterated by
/// present element</em>: per-frame / per-query "visited" sets in graph traversal (BFS/DFS), ECS
/// entity membership, register-allocation liveness, and sweep-line algorithms. There,
/// <see cref="HashSet{T}"/>'s <c>Clear</c> is <c>O(capacity)</c> (it zeroes the whole entry
/// table) and its iteration walks a possibly-sparse table, whereas this type's <c>Clear</c> only
/// resets the count (the backing arrays are left as-is) and its enumeration is a
/// linear scan over the dense prefix.
/// </para>
/// <para>
/// <b>Tradeoffs.</b> The sparse index array is <c>O(Universe)</c> memory, and the type stores
/// only non-negative values below the fixed <see cref="Universe"/> chosen at construction. It is
/// an opt-in specialized type, not a <see cref="HashSet{T}"/> replacement: for an unbounded or
/// huge-and-sparse key space, <see cref="IntSet"/> / <see cref="HashSet{T}"/> remain the right
/// choice. A value outside <c>[0, Universe)</c> is rejected by <see cref="Add(int)"/> /
/// <see cref="TryAdd(int)"/> with <see cref="ArgumentOutOfRangeException"/>, and reported as
/// absent by <see cref="Contains(int)"/> / <see cref="Remove(int)"/> (the bounded-universe
/// analogue of <see cref="EnumSet{TEnum}"/>).
/// </para>
/// <para>
/// It implements the full <see cref="ISet{T}"/> surface (and therefore
/// <see cref="ICollection{T}"/> / <see cref="IEnumerable{T}"/>) with BCL
/// <see cref="HashSet{T}"/> semantics via the shared set-algebra helper, so it drops in wherever
/// an <see cref="ISet{T}"/> is expected — with the one caveat that a mutating operation which
/// would <em>add</em> an out-of-universe value (e.g. <see cref="UnionWith"/> with such an
/// element) throws <see cref="ArgumentOutOfRangeException"/> rather than silently growing an
/// unbounded set.
/// </para>
/// <para>
/// The type is single-threaded and does not guarantee enumeration order; in particular
/// <see cref="Remove(int)"/> moves the last dense element into the vacated slot, so the order
/// after a removal is unspecified.
/// </para>
/// </remarks>
public class SparseSet : ISet<int>
{
    // The dense array holds the present values in [0, _count); the sparse array is
    // indexed by value and, for a present value v, sparse[v] is v's slot in dense.
    // The dense array grows on demand (capped at _universe); the sparse array is sized
    // to the universe once at construction and is never cleared — the round-trip
    // membership check tolerates its garbage, which is what makes Clear O(1).
    private int[] _dense;
    private readonly int[] _sparse;
    private readonly int _universe;
    private int _count;

    // Incremented on every structural mutation so active enumerators can detect
    // concurrent modification and throw, matching BCL semantics.
    private int _version;

    /// <summary>
    /// Initializes a new, empty <see cref="SparseSet"/> whose storable values are the integers in
    /// <c>[0, universe)</c>.
    /// </summary>
    /// <param name="universe">
    /// The exclusive upper bound of storable values; the set can hold any non-negative integer
    /// strictly less than this. The sparse index array is sized to this length once, so it is the
    /// dominant memory cost — choose it to match the actual value range. A value of <c>0</c>
    /// creates a set that can store nothing.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="universe"/> is negative.</exception>
    public SparseSet(int universe)
    {
        if (universe < 0)
            throw new ArgumentOutOfRangeException(nameof(universe), universe, "Universe must be non-negative.");

        _universe = universe;
        _sparse = universe == 0 ? Array.Empty<int>() : new int[universe];
        _dense = Array.Empty<int>();
    }

    /// <summary>
    /// Initializes a new <see cref="SparseSet"/> over <c>[0, universe)</c> that contains the
    /// distinct values copied from the specified <paramref name="source"/>.
    /// </summary>
    /// <param name="universe">The exclusive upper bound of storable values (see <see cref="SparseSet(int)"/>).</param>
    /// <param name="source">
    /// The collection whose values are copied into the new set. If <paramref name="source"/>
    /// implements <see cref="ICollection{T}"/>, its <c>Count</c> is used to pre-size the dense
    /// backing array so the initial fill avoids resize work. Duplicate values are silently
    /// deduplicated, matching BCL <see cref="HashSet{T}"/> semantics.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="universe"/> is negative, or <paramref name="source"/> contains a value
    /// outside <c>[0, universe)</c>.
    /// </exception>
    public SparseSet(int universe, IEnumerable<int> source)
        : this(NonNullUniverse(universe, source))
    {
        if (source is ICollection<int> collection)
            EnsureDenseCapacity(Math.Min(_universe, collection.Count));

        foreach (int item in source)
            TryAdd(item);
    }

    // Runs as part of the chained-ctor argument expression so the null check beats the primary
    // ctor's universe validation: a null source must surface as ArgumentNullException, not
    // ArgumentOutOfRangeException, even when the caller also passed a negative universe. Mirrors
    // SmallSet's InitialCapacityForSource.
    private static int NonNullUniverse(int universe, IEnumerable<int> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return universe;
    }

    /// <summary>
    /// Gets the exclusive upper bound of the values this set can store; every element is a
    /// non-negative integer strictly less than this.
    /// </summary>
    public int Universe => _universe;

    /// <summary>
    /// Gets the number of elements contained in the set.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Adds the specified value to the set.
    /// Throws <see cref="ArgumentException"/> if the value already exists.
    /// </summary>
    /// <param name="item">The value to add; must be in <c>[0, Universe)</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="item"/> already exists in the set.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="item"/> is outside <c>[0, Universe)</c>.</exception>
    public void Add(int item)
    {
        if (!TryAdd(item))
            throw new ArgumentException($"The element {item} already exists in the set.", nameof(item));
    }

    /// <summary>
    /// Attempts to add the specified value to the set.
    /// </summary>
    /// <param name="item">The value to add; must be in <c>[0, Universe)</c>.</param>
    /// <returns>
    /// <c>true</c> if the value was added; <c>false</c> if it already exists (the set is unchanged).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="item"/> is outside <c>[0, Universe)</c>.</exception>
    public bool TryAdd(int item)
    {
        if ((uint)item >= (uint)_universe)
            throw new ArgumentOutOfRangeException(nameof(item), item,
                $"The value must be in [0, {_universe}).");

        if (ContainsUnchecked(item))
            return false;

        int index = _count;
        if (index == _dense.Length)
            GrowDense();

        _dense[index] = item;
        _sparse[item] = index;
        _count = index + 1;
        _version++;
        return true;
    }

    /// <summary>
    /// Determines whether the set contains the specified value.
    /// </summary>
    /// <param name="item">The value to locate.</param>
    /// <returns>
    /// <c>true</c> if the value is present; <c>false</c> otherwise (including for a value outside
    /// <c>[0, Universe)</c>).
    /// </returns>
    public bool Contains(int item)
    {
        if ((uint)item >= (uint)_universe)
            return false;

        return ContainsUnchecked(item);
    }

    /// <summary>
    /// Removes the specified value from the set.
    /// </summary>
    /// <param name="item">The value to remove.</param>
    /// <returns>
    /// <c>true</c> if the value was removed; <c>false</c> if it was not present (including for a
    /// value outside <c>[0, Universe)</c>).
    /// </returns>
    /// <remarks>
    /// Removal moves the last dense element into the vacated slot (an <c>O(1)</c> swap), so the
    /// relative order of the surviving elements is not preserved.
    /// </remarks>
    public bool Remove(int item)
    {
        if ((uint)item >= (uint)_universe)
            return false;

        // Inline the membership round-trip so _sparse[item] is read once (rather than once
        // in ContainsUnchecked and again below) on this hot path.
        int index = _sparse[item];
        if ((uint)index >= (uint)_count || _dense[index] != item)
            return false;

        int last = _count - 1;
        int lastElem = _dense[last];

        // Move the last dense element into the removed slot and repoint its sparse entry.
        _dense[index] = lastElem;
        _sparse[lastElem] = index;

        _count = last;
        _version++;
        return true;
    }

    /// <summary>
    /// Removes all elements from the set in <c>O(1)</c>. The backing arrays are neither scanned,
    /// cleared, nor shrunk — only the count is reset — which is the type's defining advantage over
    /// <see cref="HashSet{T}"/> for clear-and-rebuild workloads.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        // No array clearing needed: the sparse↔dense round-trip check treats every slot as
        // absent once _count is 0, regardless of the stale contents left behind.
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Ensures that the dense backing array can hold at least <paramref name="capacity"/> elements
    /// without growing, enlarging it in a single copy if it is currently smaller. Pre-sizing before
    /// a bulk insert of a known size avoids the incremental array doublings an unsized set would
    /// otherwise pay. The request is capped at <see cref="Universe"/> (the most elements the set
    /// can ever hold), and the array is never shrunk by this call.
    /// </summary>
    /// <param name="capacity">The minimum number of elements the dense array must hold.</param>
    /// <returns>The dense-array capacity the set can now hold before it grows.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public int EnsureCapacity(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        int target = Math.Min(_universe, capacity);
        if (target > _dense.Length)
        {
            EnsureDenseCapacity(target);
            _version++;
        }

        return _dense.Length;
    }

    /// <summary>
    /// Reduces the dense backing array to exactly the current <see cref="Count"/>, reclaiming
    /// memory after the set has shrunk. The sparse index array (sized to <see cref="Universe"/>)
    /// is unaffected.
    /// </summary>
    public void TrimExcess() => TrimExcess(_count);

    /// <summary>
    /// Reduces (or grows) the dense backing array to hold exactly <paramref name="capacity"/>
    /// elements. The sparse index array is unaffected.
    /// </summary>
    /// <param name="capacity">
    /// The number of elements to size the dense array for. Must be at least the current
    /// <see cref="Count"/> and no more than <see cref="Universe"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is less than the current <see cref="Count"/> or greater than
    /// <see cref="Universe"/>.
    /// </exception>
    public void TrimExcess(int capacity)
    {
        if (capacity < _count)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least the current Count.");
        if (capacity > _universe)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must not exceed Universe.");

        if (capacity != _dense.Length)
        {
            Array.Resize(ref _dense, capacity);
            _version++;
        }
    }

    /// <summary>
    /// Returns an allocation-free enumerator that yields each element stored in the set. The
    /// enumeration order is unspecified and may change after a <see cref="Remove(int)"/>; do not
    /// rely on it. If the set is modified during enumeration, <see cref="Enumerator.MoveNext"/>
    /// throws <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over this set.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ── ISet<int> / ICollection<int> set-algebra surface ──────────────────────
    // The set-algebra logic is shared across the mutable set family via
    // SetOperations, written once against the ISet<T> primitives every set
    // exposes; the semantics match BCL HashSet<int> exactly. A mutating op that
    // would add an out-of-universe value throws ArgumentOutOfRangeException via
    // TryAdd — the bounded-universe caveat noted on the type.

    /// <summary>
    /// Modifies the set to contain all elements that are present in itself, in
    /// <paramref name="other"/>, or in both.
    /// </summary>
    /// <param name="other">The collection to union into this set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="other"/> contains a value outside <c>[0, Universe)</c>.</exception>
    public void UnionWith(IEnumerable<int> other) => SetOperations.UnionWith(this, other);

    /// <summary>
    /// Modifies the set to contain only elements that are also present in
    /// <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to intersect with this set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void IntersectWith(IEnumerable<int> other) => SetOperations.IntersectWith(this, other);

    /// <summary>
    /// Removes every element in <paramref name="other"/> from the set.
    /// </summary>
    /// <param name="other">The collection of elements to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void ExceptWith(IEnumerable<int> other) => SetOperations.ExceptWith(this, other);

    /// <summary>
    /// Modifies the set to contain only elements that are present either in itself or in
    /// <paramref name="other"/>, but not both.
    /// </summary>
    /// <param name="other">The collection to apply the symmetric difference with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="other"/> contains a value outside <c>[0, Universe)</c> that must be added.</exception>
    public void SymmetricExceptWith(IEnumerable<int> other) => SetOperations.SymmetricExceptWith(this, other);

    /// <summary>
    /// Determines whether the set is a subset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if every element of this set is in <paramref name="other"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSubsetOf(IEnumerable<int> other) => SetOperations.IsSubsetOf(this, other);

    /// <summary>
    /// Determines whether the set is a proper (strict) subset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of this set is in <paramref name="other"/> and
    /// <paramref name="other"/> has at least one element this set does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSubsetOf(IEnumerable<int> other) => SetOperations.IsProperSubsetOf(this, other);

    /// <summary>
    /// Determines whether the set is a superset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if every element of <paramref name="other"/> is in this set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSupersetOf(IEnumerable<int> other) => SetOperations.IsSupersetOf(this, other);

    /// <summary>
    /// Determines whether the set is a proper (strict) superset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of <paramref name="other"/> is in this set and this set has at
    /// least one element <paramref name="other"/> does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSupersetOf(IEnumerable<int> other) => SetOperations.IsProperSupersetOf(this, other);

    /// <summary>
    /// Determines whether the set and <paramref name="other"/> share at least one element.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two share any element.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool Overlaps(IEnumerable<int> other) => SetOperations.Overlaps(this, other);

    /// <summary>
    /// Determines whether the set and <paramref name="other"/> contain the same distinct elements.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two contain exactly the same elements.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool SetEquals(IEnumerable<int> other) => SetOperations.SetEquals(this, other);

    /// <summary>
    /// Copies the elements of the set to the specified <paramref name="array"/>, starting at
    /// <paramref name="arrayIndex"/>.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
    public void CopyTo(int[] array, int arrayIndex) => SetOperations.CopyTo(this, _count, array, arrayIndex);

    // Adds the element, returning whether it was newly added (ISet<T> semantics) — the
    // non-throwing (on duplicates) counterpart of the public throw-on-duplicate Add(int).
    bool ISet<int>.Add(int item) => TryAdd(item);

    // ICollection<T>.Add must not throw on a duplicate (unlike the public Add(int)), so it maps to
    // the non-throwing TryAdd.
    void ICollection<int>.Add(int item) => TryAdd(item);

    bool ICollection<int>.IsReadOnly => false;

    // The Briggs–Torczon membership round-trip, assuming item is already known to be in
    // [0, _universe). sparse[item] may be a stale value left from before a Clear, or the
    // zero a never-written slot still holds; the (uint) bound and the dense[...] == item
    // confirmation reject both.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ContainsUnchecked(int item)
    {
        int index = _sparse[item];
        return (uint)index < (uint)_count && _dense[index] == item;
    }

    // Grows the dense array on a full insert. Dense never needs to hold more than _universe
    // elements (the set cannot contain more distinct values than the universe), so growth is
    // doubling capped at _universe, computed in long to avoid the *2 overflow.
    private void GrowDense()
    {
        int len = _dense.Length;
        int newCapacity = len == 0
            ? Math.Min(4, _universe)
            : (int)Math.Min(_universe, (long)len * 2);

        Array.Resize(ref _dense, newCapacity);
    }

    // Enlarges the dense array to at least `capacity` (already clamped to <= _universe by the
    // caller). No-op when it already fits.
    private void EnsureDenseCapacity(int capacity)
    {
        if (capacity > _dense.Length)
            Array.Resize(ref _dense, capacity);
    }

    /// <summary>
    /// A struct enumerator over a <see cref="SparseSet"/>. Because it is a struct, iterating it via
    /// <c>foreach</c> avoids the allocation that a compiler-generated <c>IEnumerator&lt;int&gt;</c>
    /// would incur. It walks the dense array, so it is a contiguous, cache-friendly scan over
    /// exactly the present elements.
    /// </summary>
    public struct Enumerator : IEnumerator<int>
    {
        private readonly SparseSet _set;
        private readonly int _version;
        private int _index;
        private int _current;

        internal Enumerator(SparseSet set)
        {
            _set = set;
            _version = set._version;
            _index = -1;
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

            SparseSet set = _set;
            if (++_index < set._count)
            {
                _current = set._dense[_index];
                return true;
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

            _index = -1;
            _current = 0;
        }

        /// <summary>
        /// Releases any resources held by the enumerator. No-op for this type.
        /// </summary>
        public void Dispose() { }
    }
}
