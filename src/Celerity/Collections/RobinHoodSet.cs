using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A high-performance generic hash set that resolves collisions with
/// <em>Robin Hood</em> open addressing instead of the plain linear probing used
/// by <see cref="CeleritySet{T, THasher}"/>. It is the set counterpart of
/// <see cref="RobinHoodDictionary{TKey, TValue, THasher}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Robin Hood hashing keeps, for every occupied slot, the number of steps it
/// sits away from its ideal (hash) slot — its <em>probe sequence length</em>
/// (PSL). On insert, an incoming element that has travelled further than the
/// element already occupying a slot <em>displaces</em> it ("robs from the rich"):
/// the resident is evicted and re-inserted further along. This bounds the
/// variance of probe lengths, so the worst-case lookup is much closer to the
/// average than under linear probing. The pay-offs and costs are
/// workload-dependent:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Wins</b> on clustered / adversarial element distributions, where vanilla
/// linear probing grows long runs and tail-latency lookups degrade toward O(n).
/// The PSL invariant also lets a <em>negative</em> <see cref="Contains"/> stop
/// early — as soon as the probe distance exceeds the resident slot's PSL the
/// element cannot be present — which is the common case for a set ("have I seen
/// this?" dedup guards, presence checks).
/// </description></item>
/// <item><description>
/// <b>Costs</b> a per-slot <see cref="int"/> of PSL bookkeeping (so it allocates
/// more than <see cref="CeleritySet{T, THasher}"/>) and a small amount of extra
/// work per insert for the displacement swaps. On uniform distributions it is
/// typically a wash or a slight loss versus linear probing.
/// </description></item>
/// </list>
/// <para>
/// It is otherwise a drop-in peer of <see cref="CeleritySet{T, THasher}"/>: same
/// constructors, the same <see cref="ISet{T}"/> surface, the same allocation-free
/// struct enumerator, and the same out-of-band handling of <c>default(T)</c> (so
/// the hasher is never invoked with the zero / null element, matching the rest of
/// the family). The default is unchanged — this is an additional opt-in type for
/// the clustered case, not a replacement.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of elements in the set.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute element hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class RobinHoodSet<T, THasher> : ISet<T> where THasher : struct, IHashProvider<T>
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
    private T?[] _items;
    // Probe sequence length (distance from the ideal slot) for each occupied
    // slot. Only meaningful where the parallel _items slot is non-default; empty
    // slots carry 0. Stored explicitly so the probe loop can compare distances
    // without recomputing the resident element's hash — that is what makes the
    // Robin Hood early-exit on negative lookups cheap.
    private int[] _distances;
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
    /// Initializes a new instance of the <see cref="RobinHoodSet{T,THasher}"/> class
    /// using the specified capacity and load factor.
    /// </summary>
    /// <param name="capacity">
    /// The initial capacity, automatically rounded to the next power of two.
    /// </param>
    /// <param name="loadFactor">
    /// The fraction of the set's size that can be filled before resizing.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative, or <paramref name="loadFactor"/>
    /// is not in the open interval (0, 1).
    /// </exception>
    public RobinHoodSet(
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (loadFactor <= 0f || loadFactor >= 1f)
            throw new ArgumentOutOfRangeException(nameof(loadFactor), loadFactor, "Load factor must be between 0 (exclusive) and 1 (exclusive).");

        int size = FastUtils.NextPowerOfTwo(capacity);

        _items = new T?[size];
        _distances = new int[size];
        _loadFactor = loadFactor;
        _threshold = (int)(size * _loadFactor);
        _hasher = default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RobinHoodSet{T,THasher}"/>
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
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative, or <paramref name="loadFactor"/>
    /// is not in the open interval (0, 1).
    /// </exception>
    public RobinHoodSet(
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
    // user also passed an invalid loadFactor. Mirrors CeleritySet / RobinHoodDictionary.
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
        // hash is computed once and reused by the probe and the insert so a new
        // element costs exactly one Hash() call.
        int hash = _hasher.Hash(item);
        if (ProbeForItem(item, hash) >= 0)
            return false;

        if (_count >= _threshold)
            Resize();

        InsertAbsent(_items, _distances, item, hash);
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
        Array.Clear(_distances, 0, _distances.Length);
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
    /// A struct enumerator over a <see cref="RobinHoodSet{T,THasher}"/>. Because it is
    /// a struct, iterating it via <c>foreach</c> avoids the allocation that a
    /// compiler-generated <c>IEnumerator&lt;T&gt;</c> would incur. The out-of-band
    /// <c>default(T)</c> entry (if present) is yielded first.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly RobinHoodSet<T, THasher> _set;
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

        internal Enumerator(RobinHoodSet<T, THasher> set)
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
                T?[] items = _set._items;
                int length = items.Length;
                ref T? itemsRef = ref MemoryMarshal.GetArrayDataReference(items);
                var comparer = EqualityComparer<T>.Default;
                while (++_index < length)
                {
                    T? item = Unsafe.Add(ref itemsRef, (nint)(uint)_index);
                    if (!comparer.Equals(item, default(T)))
                    {
                        _current = item;
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

    // Robin Hood lookup. Walks the probe chain from the element's ideal slot and
    // stops on one of three conditions: an empty slot (element absent), a resident
    // whose stored probe distance is *smaller* than the distance we have already
    // travelled (element absent — the Robin Hood invariant guarantees the element,
    // if present, would have displaced this shorter-distance resident), or a match
    // (found). The early distance cut is what bounds negative-lookup cost on
    // clustered distributions.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ProbeForItem(T item) => ProbeForItem(item, _hasher.Hash(item));

    // Overload taking a precomputed hash so the insert paths can share a single
    // Hash() call between the existence probe and the subsequent insertion.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ProbeForItem(T item, int hash)
    {
        T?[] items = _items;
        int[] distances = _distances;
        ref T? itemsRef = ref MemoryMarshal.GetArrayDataReference(items);
        ref int distRef = ref MemoryMarshal.GetArrayDataReference(distances);
        int mask = items.Length - 1;
        var comparer = EqualityComparer<T>.Default;
        int index = hash & mask;
        int dist = 0;

        while (true)
        {
            T? slot = Unsafe.Add(ref itemsRef, (nint)(uint)index);
            if (comparer.Equals(slot, default(T)))
                return -1;
            if (Unsafe.Add(ref distRef, (nint)(uint)index) < dist)
                return -1;
            if (comparer.Equals(slot, item))
                return index;
            index = (index + 1) & mask;
            dist++;
        }
    }

    // Robin Hood insertion of an element known to be ABSENT from the table (callers
    // probe for existence first, so this never needs an equality check). The
    // incoming entry walks forward carrying its probe distance; whenever it meets a
    // resident with a *shorter* distance it swaps in (robs the rich) and the
    // displaced resident becomes the new carried entry, continuing until an empty
    // slot is found. Operates on the arrays passed in so Resize can reuse it
    // against freshly allocated storage before the field swap. Only the incoming
    // element's hash is reused; displaced (robbed) residents carry their stored
    // probe distance forward and are never re-hashed, so the whole insertion costs
    // a single Hash() call regardless of how many residents it displaces.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InsertAbsent(T?[] items, int[] distances, T item, int hash)
    {
        ref T? itemsRef = ref MemoryMarshal.GetArrayDataReference(items);
        ref int distRef = ref MemoryMarshal.GetArrayDataReference(distances);
        int mask = items.Length - 1;
        var comparer = EqualityComparer<T>.Default;
        int index = hash & mask;
        int dist = 0;

        while (true)
        {
            ref T? slotItem = ref Unsafe.Add(ref itemsRef, (nint)(uint)index);
            if (comparer.Equals(slotItem, default(T)))
            {
                slotItem = item;
                Unsafe.Add(ref distRef, (nint)(uint)index) = dist;
                return;
            }

            ref int slotDist = ref Unsafe.Add(ref distRef, (nint)(uint)index);
            if (slotDist < dist)
            {
                // Rob: drop the carried entry here and pick up the resident.
                T? displacedItem = slotItem;
                int displacedDist = slotDist;

                slotItem = item;
                slotDist = dist;

                item = displacedItem!;
                dist = displacedDist;
            }

            index = (index + 1) & mask;
            dist++;
        }
    }

    private void Resize() => Resize(FastUtils.DoubleCapacity(_items.Length));

    // Rehashes every live element into freshly allocated tables of the given power-of-two size via the
    // Robin Hood insertion path. Shared by the doubling growth path and the EnsureCapacity /
    // TrimExcess re-sizers, which pass an explicit target. The caller guarantees newSize is a power
    // of two strictly greater than the in-table live count (so every InsertAbsent terminates).
    private void Resize(int newSize)
    {
        T?[] oldItems = _items;

        // Reinsert every live element into freshly allocated arrays via the Robin
        // Hood insertion path, then swap them in. Each old element is known unique,
        // so InsertAbsent's no-equality-check contract holds; _count / _version are
        // conserved across a resize. The out-of-band default-value entry is
        // untouched.
        T?[] newItems = new T?[newSize];
        int[] newDistances = new int[newSize];
        ref T? oldItemsRef = ref MemoryMarshal.GetArrayDataReference(oldItems);

        var comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < oldItems.Length; i++)
        {
            T? item = Unsafe.Add(ref oldItemsRef, (nint)(uint)i);
            if (comparer.Equals(item, default(T)))
                continue;

            InsertAbsent(newItems, newDistances, item!, _hasher.Hash(item!));
        }

        _items = newItems;
        _distances = newDistances;
        _threshold = (int)(newSize * _loadFactor);
    }

    // Robin Hood backward-shift deletion. Starting at the removed slot, pull each
    // following entry back by one position and decrement its stored probe distance,
    // stopping at the first slot that is either empty or already in its ideal
    // position (distance 0) — neither can move back without breaking the invariant.
    // The final settled slot is cleared. Compared with tombstones this keeps the
    // table contiguous so lookups never pay for deleted-slot skips.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BackwardShiftRemove(int startIndex)
    {
        T?[] items = _items;
        int[] distances = _distances;
        ref T? itemsRef = ref MemoryMarshal.GetArrayDataReference(items);
        ref int distRef = ref MemoryMarshal.GetArrayDataReference(distances);
        int mask = items.Length - 1;
        var comparer = EqualityComparer<T>.Default;
        int i = startIndex;

        while (true)
        {
            int next = (i + 1) & mask;
            T? nextItem = Unsafe.Add(ref itemsRef, (nint)(uint)next);
            int nextDist = Unsafe.Add(ref distRef, (nint)(uint)next);
            if (comparer.Equals(nextItem, default(T)) || nextDist == 0)
                break;

            Unsafe.Add(ref itemsRef, (nint)(uint)i) = nextItem;
            Unsafe.Add(ref distRef, (nint)(uint)i) = nextDist - 1;
            i = next;
        }

        Unsafe.Add(ref itemsRef, (nint)(uint)i) = default;
        Unsafe.Add(ref distRef, (nint)(uint)i) = 0;
    }
}
