using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A high-performance set of <see cref="int"/> values, using
/// <see cref="Int32WangNaiveHasher"/> by default. Switch to
/// <see cref="Int32WangHasher"/> or <see cref="Int32Murmur3Hasher"/> via the
/// generic overload when elements are adversarial or clustered.
/// </summary>
public class IntSet : IntSet<Int32WangNaiveHasher>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IntSet"/> class
    /// with an optional capacity and load factor.
    /// </summary>
    /// <param name="capacity">
    /// The initial capacity of the set. Automatically rounded up
    /// to the next power of two.
    /// </param>
    /// <param name="loadFactor">
    /// Determines the maximum ratio of count to capacity before resizing.
    /// </param>
    public IntSet(int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
        : base(capacity, loadFactor)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntSet"/> class
    /// containing the elements copied from the specified <paramref name="source"/>.
    /// </summary>
    /// <param name="source">
    /// The collection whose elements are copied into the new set. Duplicate
    /// elements in <paramref name="source"/> are silently deduplicated, matching
    /// BCL <see cref="HashSet{T}"/> semantics.
    /// </param>
    /// <param name="capacity">
    /// The minimum initial capacity. The final capacity is the larger of this
    /// value and the source's count, rounded up to the next power of two.
    /// </param>
    /// <param name="loadFactor">
    /// Determines the maximum ratio of count to capacity before resizing.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is <c>null</c>.
    /// </exception>
    public IntSet(
        IEnumerable<int> source,
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
        : base(source, capacity, loadFactor)
    {
    }
}

/// <summary>
/// A high-performance set of <see cref="int"/> values, parameterized on a
/// custom <see cref="IHashProvider{T}"/> implementation.
/// </summary>
/// <typeparam name="THasher">
/// The hasher used to compute element hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class IntSet<THasher> : ISet<int> where THasher : struct, IHashProvider<int>
{
    /// <summary>
    /// The default initial capacity of the set if no capacity is specified.
    /// </summary>
    protected const int DEFAULT_CAPACITY = 16;

    /// <summary>
    /// The default load factor of the set if no load factor is specified.
    /// </summary>
    protected const float DEFAULT_LOAD_FACTOR = 0.75f;

    private const int EMPTY_SLOT = 0;

    private int _count = 0;
    private int[] _slots;
    private readonly float _loadFactor;
    private int _threshold;
    private readonly THasher _hasher;

    // The value 0 collides with EMPTY_SLOT, so it's stored out-of-band
    // in a dedicated flag. _count includes this entry when _hasZero is true.
    private bool _hasZero;

    // Bumped on every entry-point structural mutation (Add / TryAdd / Remove /
    // Clear). The struct enumerator captures this on construction and re-checks
    // it from MoveNext / Reset, so any concurrent modification fast-fails with
    // InvalidOperationException — matching BCL HashSet<int> semantics.
    private int _version;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntSet{THasher}"/> class
    /// using the specified capacity and load factor.
    /// </summary>
    /// <param name="capacity">
    /// The initial capacity of the set. Automatically rounded up
    /// to the next power of two.
    /// </param>
    /// <param name="loadFactor">
    /// Determines the maximum ratio of count to capacity before resizing.
    /// </param>
    public IntSet(
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (loadFactor <= 0f || loadFactor >= 1f)
            throw new ArgumentOutOfRangeException(nameof(loadFactor), loadFactor, "Load factor must be between 0 (exclusive) and 1 (exclusive).");

        int size = FastUtils.NextPowerOfTwo(capacity);

        _slots = new int[size];
        _loadFactor = loadFactor;
        _threshold = (int)(size * _loadFactor);
        _hasher = default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntSet{THasher}"/> class
    /// containing the elements copied from the specified <paramref name="source"/>.
    /// </summary>
    /// <param name="source">
    /// The collection whose elements are copied into the new set. If
    /// <paramref name="source"/> implements <see cref="ICollection{T}"/>, its
    /// <c>Count</c> is used to size the backing storage so the initial fill
    /// avoids resize work. Duplicate elements are silently deduplicated,
    /// matching BCL <see cref="HashSet{T}"/> semantics.
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
    public IntSet(
        IEnumerable<int> source,
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
        : this(InitialCapacityForSource(source, capacity, loadFactor), loadFactor)
    {
        foreach (int item in source)
        {
            TryAdd(item);
        }
    }

    // Runs as part of the chained-ctor argument expression so the null check
    // beats the primary ctor's capacity / loadFactor validation: a null source
    // must surface as ArgumentNullException, not ArgumentOutOfRangeException
    // when the user also passed an invalid loadFactor.
    private static int InitialCapacityForSource(IEnumerable<int> source, int capacity, float loadFactor)
    {
        ArgumentNullException.ThrowIfNull(source);
        int count = (source as ICollection<int>)?.Count ?? 0;

        // Size for the source count *including* load-factor headroom: the resize
        // threshold is size*loadFactor, so a table sized to the raw count would
        // still rehash on the last inserts of the bulk fill. Scaling the count up
        // by 1/loadFactor makes the "Count is used to size the backing storage so
        // the initial fill avoids resize work" contract actually hold (issue #27).
        // A non-collection source (count 0) or an out-of-range loadFactor — left
        // for the primary ctor to reject, so null-source-beats-bad-loadFactor
        // ordering is preserved — falls through to the plain capacity.
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
    public void Add(int item)
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
    public bool TryAdd(int item)
    {
        if (item == EMPTY_SLOT)
        {
            if (_hasZero)
                return false;
            _hasZero = true;
            _count++;
            _version++;
            return true;
        }

        // Probe the current table first: if the item already exists, we
        // return without touching anything — no Resize, no _version bump,
        // no array swap. The threshold check is deferred to after the
        // duplicate check so a duplicate-at-threshold call cannot silently
        // swap out the backing array under an active enumerator (see
        // issue #92).
        int[] slots = _slots;
        ref int slotsRef = ref MemoryMarshal.GetArrayDataReference(slots);
        int mask = slots.Length - 1;
        int index = _hasher.Hash(item) & mask;

        while (true)
        {
            int slot = Unsafe.Add(ref slotsRef, (nint)(uint)index);
            if (slot == EMPTY_SLOT) break;
            if (slot == item) return false;
            index = (index + 1) & mask;
        }

        if (_count >= _threshold)
        {
            Resize();
            slots = _slots;
            slotsRef = ref MemoryMarshal.GetArrayDataReference(slots);
            mask = slots.Length - 1;
            index = _hasher.Hash(item) & mask;
            while (Unsafe.Add(ref slotsRef, (nint)(uint)index) != EMPTY_SLOT)
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
    public bool Contains(int item)
    {
        if (item == EMPTY_SLOT)
            return _hasZero;

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
    public bool Remove(int item)
    {
        if (item == EMPTY_SLOT)
        {
            if (!_hasZero)
                return false;
            _hasZero = false;
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

        Array.Clear(_slots, 0, _slots.Length);
        _hasZero = false;
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
            if (newSize > _slots.Length)
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
    /// out-of-band zero entry is preserved.
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
        if (newSize != _slots.Length)
        {
            Resize(newSize);
            _version++;
        }
    }

    /// <summary>
    /// Returns an allocation-free enumerator that yields each element stored in
    /// the set. The enumeration order is unspecified and may change across
    /// versions; do not rely on it. The out-of-band zero entry (if present) is
    /// yielded first. If the set is modified during enumeration,
    /// <see cref="Enumerator.MoveNext"/> throws
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over this set.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<int> IEnumerable<int>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ── ISet<int> / ICollection<int> set-algebra surface ──────────────────────
    // The set-algebra logic is shared across the mutable set family via
    // SetOperations, written once against the ISet<T> primitives every set
    // exposes; the semantics match BCL HashSet<int> exactly.

    /// <summary>
    /// Modifies the set to contain all elements that are present in itself, in
    /// <paramref name="other"/>, or in both.
    /// </summary>
    /// <param name="other">The collection to union into this set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
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
    /// Modifies the set to contain only elements that are present either in itself
    /// or in <paramref name="other"/>, but not both.
    /// </summary>
    /// <param name="other">The collection to apply the symmetric difference with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
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
    /// <c>true</c> if every element of <paramref name="other"/> is in this set and
    /// this set has at least one element <paramref name="other"/> does not.
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
    /// <paramref name="arrayIndex"/>. The out-of-band zero element (if present) is copied first.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
    public void CopyTo(int[] array, int arrayIndex) => SetOperations.CopyTo(this, _count, array, arrayIndex);

    // Adds the element, returning whether it was newly added (ISet<int> semantics) —
    // the non-throwing counterpart of the public throw-on-duplicate Add(int).
    bool ISet<int>.Add(int item) => TryAdd(item);

    // ICollection<int>.Add must not throw on a duplicate (unlike the public Add(int)),
    // so it maps to the non-throwing TryAdd.
    void ICollection<int>.Add(int item) => TryAdd(item);

    bool ICollection<int>.IsReadOnly => false;

    /// <summary>
    /// A struct enumerator over an <see cref="IntSet{THasher}"/>. Because it is
    /// a struct, iterating it via <c>foreach</c> avoids the allocation that a
    /// compiler-generated <c>IEnumerator&lt;T&gt;</c> would incur. The
    /// out-of-band zero entry (if present) is yielded first.
    /// </summary>
    public struct Enumerator : IEnumerator<int>
    {
        private readonly IntSet<THasher> _set;
        private readonly int _version;
        private int _index;
        private int _current;
        private State _state;

        private enum State : byte
        {
            BeforeZero,
            InArray,
            Done
        }

        internal Enumerator(IntSet<THasher> set)
        {
            _set = set;
            _version = set._version;
            _index = -1;
            _current = 0;
            _state = State.BeforeZero;
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

            if (_state == State.BeforeZero)
            {
                _state = State.InArray;
                if (_set._hasZero)
                {
                    _current = 0;
                    return true;
                }
            }

            if (_state == State.InArray)
            {
                int[] slots = _set._slots;
                int length = slots.Length;
                ref int slotsRef = ref MemoryMarshal.GetArrayDataReference(slots);
                while (++_index < length)
                {
                    int slot = Unsafe.Add(ref slotsRef, (nint)(uint)_index);
                    if (slot != EMPTY_SLOT)
                    {
                        _current = slot;
                        return true;
                    }
                }
                _state = State.Done;
            }

            _current = 0;
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
            _current = 0;
            _state = State.BeforeZero;
        }

        /// <summary>
        /// Releases any resources held by the enumerator. No-op for this type.
        /// </summary>
        public void Dispose() { }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ProbeForItem(int item)
    {
        int[] slots = _slots;
        ref int slotsRef = ref MemoryMarshal.GetArrayDataReference(slots);
        int mask = slots.Length - 1;
        int index = _hasher.Hash(item) & mask;

        while (true)
        {
            int slot = Unsafe.Add(ref slotsRef, (nint)(uint)index);
            if (slot == EMPTY_SLOT) return -1;
            if (slot == item) return index;
            index = (index + 1) & mask;
        }
    }

    private void Resize() => Resize(FastUtils.DoubleCapacity(_slots.Length));

    // Rehashes every live element into a freshly allocated table of the given power-of-two size.
    // Shared by the doubling growth path and the EnsureCapacity / TrimExcess re-sizers, which pass
    // an explicit target. The caller guarantees newSize is a power of two strictly greater than the
    // in-table live count (so the probe loop always finds a vacant slot).
    private void Resize(int newSize)
    {
        int mask = newSize - 1;
        int[] oldSlots = _slots;

        // Build into a fresh local array then swap it in at the end. The loop
        // inlines a probe-for-empty-slot walk on purpose: every reinserted item
        // is known to be unique in the new table (it came from the previous
        // array which was itself a valid set), and _count / _version are
        // conserved across a resize, so the equality check inside a general
        // insert helper, the threshold check, and the per-call _count / _version
        // bumps are all dead weight. The zero entry lives out-of-band and is
        // not touched here.
        //
        // Reads from the old array and probe/writes on the new array both go
        // through Unsafe.Add against a base reference grabbed at the top of the
        // method, so every per-iteration bounds check disappears. The bounds
        // are structural: `i < oldSlots.Length` is the for-loop condition, and
        // `index = ... & mask` keeps `index ∈ [0, newSize)`.
        int[] newSlots = new int[newSize];
        ref int oldSlotsRef = ref MemoryMarshal.GetArrayDataReference(oldSlots);
        ref int newSlotsRef = ref MemoryMarshal.GetArrayDataReference(newSlots);

        for (int i = 0; i < oldSlots.Length; i++)
        {
            int item = Unsafe.Add(ref oldSlotsRef, (nint)(uint)i);
            if (item == EMPTY_SLOT)
                continue;

            int index = _hasher.Hash(item) & mask;
            while (Unsafe.Add(ref newSlotsRef, (nint)(uint)index) != EMPTY_SLOT)
                index = (index + 1) & mask;

            Unsafe.Add(ref newSlotsRef, (nint)(uint)index) = item;
        }

        _slots = newSlots;
        _threshold = (int)(newSize * _loadFactor);
    }

    // Backward-shift deletion (Knuth TAOCP Vol 3, §6.4 Algorithm R). The
    // caller has located the slot but has NOT cleared it; this helper
    // writes the final empty entry itself once the gap settles. Compared
    // to the previous rehash-and-reinsert pass, each surviving cluster
    // entry is visited exactly once and most are not moved at all — the
    // work-per-cluster collapses from quadratic to linear, which is the
    // bulk of the Remove speedup.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BackwardShiftRemove(int startIndex)
    {
        int[] slots = _slots;
        ref int slotsRef = ref MemoryMarshal.GetArrayDataReference(slots);
        int mask = slots.Length - 1;
        int i = startIndex;
        int j = i;

        while (true)
        {
            j = (j + 1) & mask;
            int candidate = Unsafe.Add(ref slotsRef, (nint)(uint)j);
            if (candidate == EMPTY_SLOT)
                break;

            int k = _hasher.Hash(candidate) & mask;

            // Shift slots[j] into the gap at i iff the probe chain from
            // its natural slot k to its current slot j passes through i
            // (so leaving i empty would orphan the entry). When the scan
            // has not wrapped (i <= j), that means k is outside the open
            // interval (i, j]; when it has wrapped (i > j), the test
            // mirrors across the array boundary.
            bool bypassesGap = (i <= j)
                ? (i < k && k <= j)
                : (i < k || k <= j);
            if (bypassesGap)
                continue;

            Unsafe.Add(ref slotsRef, (nint)(uint)i) = candidate;
            i = j;
        }

        Unsafe.Add(ref slotsRef, (nint)(uint)i) = EMPTY_SLOT;
    }
}
