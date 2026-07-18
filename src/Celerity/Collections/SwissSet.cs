using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.InteropServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A high-performance generic hash set that resolves collisions with
/// <em>SIMD-accelerated group probing</em> in the spirit of Google's Swiss
/// Tables and Facebook's <c>F14</c>, instead of the scalar linear probing used by
/// <see cref="CeleritySet{T, THasher}"/>. It is the set counterpart of
/// <see cref="SwissDictionary{TKey, TValue, THasher}"/>.
/// </summary>
/// <remarks>
/// <para>
/// The table keeps a parallel array of one-byte <em>control</em> tags — one per
/// slot — separate from the element array. Each control byte is either
/// <c>EMPTY</c>, <c>DELETED</c> (a tombstone), or, for an occupied slot, the low
/// 7 bits of the element's hash (its <em>h2</em> fragment). Slots are grouped into
/// aligned blocks of <see cref="GROUP_WIDTH"/> (16), so a single
/// <see cref="Vector128{T}"/> compare tests all 16 control bytes in a group at
/// once: a membership test loads the 16 tags, compares them against the broadcast
/// h2, and turns the result into a 16-bit candidate mask via
/// <see cref="Vector128.ExtractMostSignificantBits{T}(Vector128{T})"/>. Only the
/// (usually one) candidate slots then pay a full element comparison; an
/// all-tags-checked group with any <c>EMPTY</c> slot ends the probe. The portable
/// <see cref="Vector128"/> API JITs to SSE2 / AVX2 on x86, AdvSimd on Arm, and a
/// scalar software fallback elsewhere, so the type is correct everywhere and fast
/// where hardware SIMD is available.
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Wins</b> on membership-heavy workloads with many slots per probe — exactly
/// what a set is used for: the group compare amortizes the per-slot tag test, and
/// the h2 tag filters out non-matching residents before any (potentially
/// expensive) element comparison, so negative <see cref="Contains"/> lookups and
/// lookups on clustered elements stay cheap.
/// </description></item>
/// <item><description>
/// <b>Costs</b> a one-byte control tag per slot (so it allocates a little more
/// than <see cref="CeleritySet{T, THasher}"/>) and uses tombstones for deletion,
/// which it reclaims by rehashing when they accumulate.
/// </description></item>
/// </list>
/// <para>
/// It is otherwise a drop-in peer of <see cref="CeleritySet{T, THasher}"/>: same
/// constructors, the same <see cref="ISet{T}"/> surface, the same
/// allocation-free struct enumerator, and the same out-of-band handling of
/// <c>default(T)</c> (so the hasher is never invoked with the zero / null element,
/// matching the rest of the family).
/// </para>
/// </remarks>
/// <typeparam name="T">The type of elements in the set.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute element hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class SwissSet<T, THasher> : ISet<T> where THasher : struct, IHashProvider<T>
{
    /// <summary>
    /// The default initial capacity of the set if no capacity is specified.
    /// </summary>
    protected const int DEFAULT_CAPACITY = 16;

    /// <summary>
    /// The default load factor of the set if no load factor is specified.
    /// </summary>
    protected const float DEFAULT_LOAD_FACTOR = 0.75f;

    // The SIMD group width: a Vector128<sbyte> tests 16 control bytes at once.
    // GROUP_SHIFT is log2(GROUP_WIDTH) so `group << GROUP_SHIFT` is the group's
    // first slot. The table size is always a power of two and at least one group.
    private const int GROUP_WIDTH = 16;
    private const int GROUP_SHIFT = 4;

    // Control-byte tags. The high (sign) bit distinguishes a free slot (EMPTY /
    // DELETED, both negative) from an occupied one (an h2 fragment in 0..127, so
    // non-negative). EMPTY ends a probe; DELETED (a tombstone) does not, so a
    // lookup walks past it but an insert may reuse it.
    private const sbyte EMPTY = -128;   // 0b1000_0000
    private const sbyte DELETED = -2;   // 0b1111_1110

    private int _count = 0;
    private sbyte[] _controls;
    private T?[] _items;
    private int _capacity;
    private int _numGroupsMask;
    private readonly float _loadFactor;
    private int _threshold;
    // Remaining EMPTY-slot budget before a resize: threshold minus the number of
    // occupied-or-tombstoned slots. Filling an EMPTY slot decrements it; reusing a
    // DELETED slot leaves it unchanged; an erase that frees a slot to EMPTY bumps
    // it back. The out-of-band default-element entry never touches it (it owns no
    // array slot). A resize is forced when it would go non-positive.
    private int _growthLeft;
    private readonly THasher _hasher;

    // default(T) (null for reference types, 0 for primitives, Guid.Empty for Guid,
    // ...) collides with the "empty slot" sentinel used during probing, so it is
    // stored out-of-band rather than in an array slot — the hasher is never invoked
    // with it, matching CeleritySet / SwissDictionary and keeping string hashers
    // (which throw on null) safe. _count includes this entry when _hasDefaultValue
    // is true.
    private bool _hasDefaultValue;

    // Incremented on every structural mutation so active enumerators can detect
    // concurrent modification and throw, matching BCL semantics.
    private int _version;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwissSet{T,THasher}"/> class
    /// using the specified capacity and load factor.
    /// </summary>
    /// <param name="capacity">
    /// The initial capacity, automatically rounded to the next power of two (and
    /// to at least one SIMD group of <see cref="GROUP_WIDTH"/> slots).
    /// </param>
    /// <param name="loadFactor">
    /// The fraction of the set's size that can be filled before resizing.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative, or <paramref name="loadFactor"/>
    /// is not in the open interval (0, 1).
    /// </exception>
    public SwissSet(
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (loadFactor <= 0f || loadFactor >= 1f)
            throw new ArgumentOutOfRangeException(nameof(loadFactor), loadFactor, "Load factor must be between 0 (exclusive) and 1 (exclusive).");

        int size = Math.Max(GROUP_WIDTH, FastUtils.NextPowerOfTwo(capacity));

        _controls = new sbyte[size];
        _controls.AsSpan().Fill(EMPTY);
        _items = new T?[size];
        _capacity = size;
        _numGroupsMask = (size >> GROUP_SHIFT) - 1;
        _loadFactor = loadFactor;
        _threshold = (int)(size * _loadFactor);
        _growthLeft = _threshold;
        _hasher = default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwissSet{T,THasher}"/> class
    /// containing the elements copied from the specified <paramref name="source"/>.
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
    public SwissSet(
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
    // user also passed an invalid loadFactor. Mirrors CeleritySet / SwissDictionary.
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

        // Probe once (single Hash() call). A duplicate returns without touching the
        // table — no resize, no _version bump, no array swap — so an active
        // enumerator stays valid (mirrors the #92 ordering on the rest of the family).
        int hash = _hasher.Hash(item);
        ProbeForInsert(item, hash, out int slot, out bool existing);
        if (existing)
            return false;

        InsertAbsent(item, hash, slot);
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

        return Find(item, _hasher.Hash(item)) >= 0;
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

        int index = Find(item, _hasher.Hash(item));
        if (index < 0)
            return false;

        EraseAt(index);
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

        _controls.AsSpan().Fill(EMPTY);
        Array.Clear(_items, 0, _items.Length);
        _hasDefaultValue = false;
        _count = 0;
        _growthLeft = _threshold;
        _version++;
    }

    /// <summary>
    /// Ensures that the set can hold at least <paramref name="capacity"/> elements without resizing,
    /// growing the backing table in a single rebuild if it is currently smaller. Pre-sizing before a
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
            int newCapacity = Math.Max(GROUP_WIDTH, FastUtils.MinTableSizeFor(capacity, _loadFactor));
            if (newCapacity > _capacity)
            {
                Resize(newCapacity);
                _version++;
            }
        }

        return _threshold;
    }

    /// <summary>
    /// Reduces the backing table to the smallest valid size (a power of two, at least one SIMD group)
    /// that still holds the current <see cref="Count"/> without resizing, dropping accumulated
    /// tombstones and reclaiming memory. The out-of-band default-element entry is preserved.
    /// </summary>
    public void TrimExcess() => TrimExcess(_count);

    /// <summary>
    /// Reduces (or grows) the backing table to the smallest valid size that holds at least
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

        int newCapacity = Math.Max(GROUP_WIDTH, FastUtils.MinTableSizeFor(capacity, _loadFactor));
        if (newCapacity != _capacity)
        {
            Resize(newCapacity);
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
    /// A struct enumerator over a <see cref="SwissSet{T,THasher}"/>. Because it is a
    /// struct, iterating it via <c>foreach</c> avoids the allocation that a
    /// compiler-generated <c>IEnumerator&lt;T&gt;</c> would incur. The out-of-band
    /// <c>default(T)</c> entry (if present) is yielded first.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly SwissSet<T, THasher> _set;
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

        internal Enumerator(SwissSet<T, THasher> set)
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
                sbyte[] controls = _set._controls;
                T?[] items = _set._items;
                int length = controls.Length;
                ref sbyte controlsRef = ref MemoryMarshal.GetArrayDataReference(controls);
                ref T? itemsRef = ref MemoryMarshal.GetArrayDataReference(items);
                while (++_index < length)
                {
                    // Full slots carry a non-negative control tag.
                    if (Unsafe.Add(ref controlsRef, (nint)(uint)_index) >= 0)
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

    // SIMD group lookup for a non-default element. Walks the aligned-group probe
    // sequence from the element's home group: a single Vector128 compare turns each
    // group's 16 control tags into a 16-bit candidate mask against the element's h2
    // fragment, and only candidate slots pay an element comparison. The probe ends
    // at the first group containing an EMPTY tag (the element is absent — insert
    // would have stopped there too). Returns the slot index, or -1 if not present.
    //
    // The hash splits into h1 (high bits, selecting the home group) and h2 (low 7
    // bits, the stored control tag). Termination is guaranteed: the load factor
    // keeps at least one EMPTY slot in the table at all times, and linear group
    // probing wraps through every group.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Find(T item, int hash)
    {
        sbyte[] controls = _controls;
        T?[] items = _items;
        ref sbyte controlsRef = ref MemoryMarshal.GetArrayDataReference(controls);
        ref T? itemsRef = ref MemoryMarshal.GetArrayDataReference(items);
        var comparer = EqualityComparer<T>.Default;
        int mask = _numGroupsMask;
        uint h1 = (uint)hash >> 7;
        Vector128<sbyte> wanted = Vector128.Create((sbyte)(hash & 0x7F));
        Vector128<sbyte> empty = Vector128.Create(EMPTY);
        int group = (int)(h1 & (uint)mask);

        while (true)
        {
            int baseSlot = group << GROUP_SHIFT;
            Vector128<sbyte> ctrl = Vector128.LoadUnsafe(ref Unsafe.Add(ref controlsRef, (nint)(uint)baseSlot));

            uint matches = Vector128.Equals(ctrl, wanted).ExtractMostSignificantBits();
            while (matches != 0)
            {
                int slot = baseSlot + BitOperations.TrailingZeroCount(matches);
                if (comparer.Equals(Unsafe.Add(ref itemsRef, (nint)(uint)slot), item))
                    return slot;
                matches &= matches - 1;
            }

            if (Vector128.Equals(ctrl, empty).ExtractMostSignificantBits() != 0)
                return -1;

            group = (group + 1) & mask;
        }
    }

    // SIMD group probe for an insert of a non-default element. In one walk it both
    // detects an existing element (sets <paramref name="existing"/> and returns its
    // slot) and, if absent, returns the slot to insert into: the first DELETED
    // (tombstone) slot seen along the sequence, or the first EMPTY slot if none.
    // Reusing a tombstone keeps the table compact without growing the EMPTY budget.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProbeForInsert(T item, int hash, out int slot, out bool existing)
    {
        sbyte[] controls = _controls;
        T?[] items = _items;
        ref sbyte controlsRef = ref MemoryMarshal.GetArrayDataReference(controls);
        ref T? itemsRef = ref MemoryMarshal.GetArrayDataReference(items);
        var comparer = EqualityComparer<T>.Default;
        int mask = _numGroupsMask;
        uint h1 = (uint)hash >> 7;
        Vector128<sbyte> wanted = Vector128.Create((sbyte)(hash & 0x7F));
        Vector128<sbyte> empty = Vector128.Create(EMPTY);
        Vector128<sbyte> deleted = Vector128.Create(DELETED);
        int group = (int)(h1 & (uint)mask);
        int firstDeleted = -1;

        while (true)
        {
            int baseSlot = group << GROUP_SHIFT;
            Vector128<sbyte> ctrl = Vector128.LoadUnsafe(ref Unsafe.Add(ref controlsRef, (nint)(uint)baseSlot));

            uint matches = Vector128.Equals(ctrl, wanted).ExtractMostSignificantBits();
            while (matches != 0)
            {
                int s = baseSlot + BitOperations.TrailingZeroCount(matches);
                if (comparer.Equals(Unsafe.Add(ref itemsRef, (nint)(uint)s), item))
                {
                    slot = s;
                    existing = true;
                    return;
                }
                matches &= matches - 1;
            }

            if (firstDeleted < 0)
            {
                uint delMask = Vector128.Equals(ctrl, deleted).ExtractMostSignificantBits();
                if (delMask != 0)
                    firstDeleted = baseSlot + BitOperations.TrailingZeroCount(delMask);
            }

            uint emptyMask = Vector128.Equals(ctrl, empty).ExtractMostSignificantBits();
            if (emptyMask != 0)
            {
                slot = firstDeleted >= 0 ? firstDeleted : baseSlot + BitOperations.TrailingZeroCount(emptyMask);
                existing = false;
                return;
            }

            group = (group + 1) & mask;
        }
    }

    // Places a known-absent non-default element into the slot the probe chose,
    // resizing first if filling an EMPTY slot would push the table over its growth
    // budget. Reusing a DELETED (tombstone) slot never needs a resize.
    private void InsertAbsent(T item, int hash, int slot)
    {
        bool targetEmpty = _controls[slot] == EMPTY;
        if (targetEmpty && _growthLeft <= 0)
        {
            Resize();
            // Re-probe in the freshly built (tombstone-free) table; the target is
            // now guaranteed to be an EMPTY slot.
            ProbeForInsert(item, hash, out slot, out _);
        }

        ref sbyte controlsRef = ref MemoryMarshal.GetArrayDataReference(_controls);
        Unsafe.Add(ref controlsRef, (nint)(uint)slot) = (sbyte)(hash & 0x7F);
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_items), (nint)(uint)slot) = item;

        _count++;
        if (targetEmpty)
            _growthLeft--;
        _version++;
    }

    // Tombstone-aware erase. If the slot's group still holds an EMPTY tag, no
    // element ever probed past this group, so the slot can be freed to EMPTY (and
    // the growth budget reclaimed). Otherwise the group was full when residents
    // probed through it, so the slot must become a DELETED tombstone — a lookup
    // walks past it but does not terminate, preserving every resident's
    // reachability. The element is cleared either way to release references.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EraseAt(int slot)
    {
        int baseSlot = (slot >> GROUP_SHIFT) << GROUP_SHIFT;
        ref sbyte controlsRef = ref MemoryMarshal.GetArrayDataReference(_controls);
        Vector128<sbyte> ctrl = Vector128.LoadUnsafe(ref Unsafe.Add(ref controlsRef, (nint)(uint)baseSlot));

        if (Vector128.Equals(ctrl, Vector128.Create(EMPTY)).ExtractMostSignificantBits() != 0)
        {
            Unsafe.Add(ref controlsRef, (nint)(uint)slot) = EMPTY;
            _growthLeft++;
        }
        else
        {
            Unsafe.Add(ref controlsRef, (nint)(uint)slot) = DELETED;
        }

        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_items), (nint)(uint)slot) = default;
    }

    // Rebuilds the table, dropping all tombstones. Doubles the capacity when the
    // live load justifies real growth; otherwise rehashes at the same size to
    // reclaim tombstones (so a churn of insert/delete cycles cannot grow the table
    // without bound). The out-of-band default-element entry is untouched.
    private void Resize() => Resize(
        _count - (_hasDefaultValue ? 1 : 0) >= (_threshold >> 1)
            ? FastUtils.DoubleCapacity(_capacity)
            : _capacity);

    // Rebuilds the table at the given power-of-two capacity, dropping all tombstones and recomputing
    // the group mask, threshold, and growth budget. Shared by the doubling/rehash growth path and
    // the EnsureCapacity / TrimExcess re-sizers, which pass an explicit target. The caller guarantees
    // newCapacity is a power of two no smaller than GROUP_WIDTH and strictly greater than the live
    // (non-default) element count, so every PlaceFresh terminates.
    private void Resize(int newCapacity)
    {
        sbyte[] oldControls = _controls;
        T?[] oldItems = _items;
        int oldCapacity = _capacity;

        sbyte[] newControls = new sbyte[newCapacity];
        newControls.AsSpan().Fill(EMPTY);
        T?[] newItems = new T?[newCapacity];

        _controls = newControls;
        _items = newItems;
        _capacity = newCapacity;
        _numGroupsMask = (newCapacity >> GROUP_SHIFT) - 1;
        _threshold = (int)(newCapacity * _loadFactor);
        // The fresh table has no tombstones, so the whole array budget is the live
        // (non-default) element count below the threshold.
        _growthLeft = _threshold - (_count - (_hasDefaultValue ? 1 : 0));

        ref sbyte oldControlsRef = ref MemoryMarshal.GetArrayDataReference(oldControls);
        ref T? oldItemsRef = ref MemoryMarshal.GetArrayDataReference(oldItems);
        for (int i = 0; i < oldCapacity; i++)
        {
            if (Unsafe.Add(ref oldControlsRef, (nint)(uint)i) >= 0)
            {
                T item = Unsafe.Add(ref oldItemsRef, (nint)(uint)i)!;
                PlaceFresh(item, _hasher.Hash(item));
            }
        }
    }

    // Places an element into the first EMPTY slot along its probe sequence in a
    // freshly built (tombstone-free) table. Elements are known unique, so no
    // equality check is needed; growth bookkeeping is owned by Resize, so this only
    // writes.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PlaceFresh(T item, int hash)
    {
        ref sbyte controlsRef = ref MemoryMarshal.GetArrayDataReference(_controls);
        int mask = _numGroupsMask;
        uint h1 = (uint)hash >> 7;
        Vector128<sbyte> empty = Vector128.Create(EMPTY);
        int group = (int)(h1 & (uint)mask);

        while (true)
        {
            int baseSlot = group << GROUP_SHIFT;
            Vector128<sbyte> ctrl = Vector128.LoadUnsafe(ref Unsafe.Add(ref controlsRef, (nint)(uint)baseSlot));
            uint emptyMask = Vector128.Equals(ctrl, empty).ExtractMostSignificantBits();
            if (emptyMask != 0)
            {
                int slot = baseSlot + BitOperations.TrailingZeroCount(emptyMask);
                Unsafe.Add(ref controlsRef, (nint)(uint)slot) = (sbyte)(hash & 0x7F);
                Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_items), (nint)(uint)slot) = item;
                return;
            }
            group = (group + 1) & mask;
        }
    }
}
