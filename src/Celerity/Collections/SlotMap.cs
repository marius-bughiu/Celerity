using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// A <b>generational slot map</b>: a container that hands out its own keys. <see cref="Add"/> stores a value
/// and returns a <see cref="SlotKey"/> &#8212; a slot index plus a version &#8212; that stays valid until that
/// value is removed, and is reported absent forever after, even once the slot is reused. A lookup is one array
/// index and one version comparison: no hash, no probe, no per-entry allocation.
/// </summary>
/// <typeparam name="TValue">The type of the stored values.</typeparam>
/// <remarks>
/// <para>
/// <b>The gap it fills.</b> .NET ships no container that issues stable handles. The idiomatic hand-roll is a
/// <see cref="Dictionary{TKey, TValue}"/> keyed by an incrementing <see cref="int"/> counter, which pays a
/// hash and a bucket-then-entry walk on every access to answer a question whose answer was known at insertion
/// time &#8212; where the value lives. The other hand-roll, a <see cref="List{T}"/> plus a free list of
/// indices, is fast but unsafe: a removed index is reused, and a caller still holding the old index silently
/// reads the new occupant (the ABA problem). A slot map is the second shape with the hazard closed: every slot
/// carries a version that is stepped when it is filled and again when it is vacated, and a key matches only
/// the version it was issued with.
/// </para>
/// <para>
/// <b>The documented workload</b> is an object pool or entity table addressed by handle: game entities and
/// component storage, graph nodes referenced from outside the graph, connection or session tables, resource
/// handles in a renderer, undo-stack references to objects that may since have been deleted. These add and
/// remove continuously and look up by handle far more often than they change &#8212; the two operations this
/// type makes a single array access.
/// </para>
/// <para>
/// <b>Layout.</b> One array of slots, indexed by <see cref="SlotKey"/>; each slot holds its version, its
/// value, and a link that threads the vacant slots into a free list. A slot's version is <em>odd exactly while
/// it holds a value</em>, and keys are only ever issued with odd versions, so one comparison of the stored
/// version against the key's answers both "is this slot live?" and "is it still the value this key was issued
/// for?" &#8212; the value sits beside the version, in the same cache line, so a lookup touches one place in
/// memory. Removal steps the version and pushes the slot onto the free list; nothing moves.
/// </para>
/// <para>
/// <b>Complexity.</b> <see cref="Add"/>, <see cref="Remove(SlotKey)"/>, <see cref="TryGetValue"/>,
/// <see cref="ContainsKey"/> and the indexer are <c>O(1)</c> (<see cref="Add"/> amortized, for the occasional
/// doubling of the slot array). Enumeration and <see cref="Clear"/> walk every slot issued so far &#8212; the
/// high-water mark of simultaneously live values, not <see cref="Count"/> &#8212; skipping the vacant ones,
/// in the same way <see cref="Dictionary{TKey, TValue}"/>'s enumeration walks entries freed by removals.
/// </para>
/// <para>
/// <b>Tradeoffs.</b> The caller does not choose keys &#8212; this is not a dictionary over a key the caller
/// already has; for that, use <see cref="Dictionary{TKey, TValue}"/> or <see cref="SparseMap{TValue}"/>. The
/// slot array never shrinks: a slot discarded and later recreated would restart its version count and could
/// resolve a key that had been retired. Versions are 32 bits wide and wrap, with the ceiling described on
/// <see cref="SlotKey"/>. Values are not packed: after heavy removal, a sweep over the values steps over the
/// vacant slots between them.
/// </para>
/// <para>
/// It implements <see cref="IReadOnlyDictionary{TKey, TValue}"/> over <c>&lt;SlotKey, TValue?&gt;</c> and
/// ships an allocation-free struct enumerator, which yields entries in ascending slot order. It does not
/// implement <see cref="IDictionary{TKey, TValue}"/>: that contract's <c>Add(key, value)</c> inserts under a
/// key the caller picks, which is exactly the operation a slot map does not offer.
/// </para>
/// <para>
/// The type is single-threaded.
/// </para>
/// </remarks>
public class SlotMap<TValue> : IReadOnlyDictionary<SlotKey, TValue?>
{
    private struct Slot
    {
        // Odd while the slot holds a value, even while it is vacant (0 for a slot never filled). Stepped by
        // one when the slot is filled and by one when it is vacated, so each use retires the key before it.
        public uint Version;

        // The next vacant slot when this one is vacant (-1 ends the free list); unused while it is live.
        public int NextFree;

        public TValue? Value;
    }

    private Slot[] _slots;

    // The high-water mark of issued slots. Slots below it are live or on the free list; slots at or above it
    // have never been used, so neither enumeration nor Clear needs to look at them.
    private int _slotCount;
    private int _freeHead;
    private int _count;

    // Incremented on every structural mutation so active enumerators can detect concurrent modification and
    // throw, matching BCL semantics. A pure value overwrite through the indexer does not bump it (matching
    // Dictionary<,>).
    private int _version;

    /// <summary>Initializes a new, empty <see cref="SlotMap{TValue}"/>.</summary>
    public SlotMap()
        : this(0)
    {
    }

    /// <summary>
    /// Initializes a new, empty <see cref="SlotMap{TValue}"/> that can hold <paramref name="capacity"/> values
    /// before it grows.
    /// </summary>
    /// <param name="capacity">The number of values to pre-size the slot array for.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public SlotMap(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        _slots = capacity == 0 ? Array.Empty<Slot>() : new Slot[capacity];
        _freeHead = -1;
    }

    /// <summary>Gets the number of values currently stored in the map.</summary>
    public int Count => _count;

    /// <summary>Gets the number of values the map can hold before its slot array grows.</summary>
    public int Capacity => _slots.Length;

    /// <summary>
    /// Gets or sets the value addressed by <paramref name="key"/>. Both the get and the set require the key to
    /// be live; the set overwrites the value in place and cannot insert, because a slot map issues its keys
    /// rather than accepting them.
    /// </summary>
    /// <param name="key">A key issued by this map's <see cref="Add"/>.</param>
    /// <returns>The value the key addresses.</returns>
    /// <exception cref="KeyNotFoundException">
    /// <paramref name="key"/> does not address a live value: it was removed, the map was cleared, it is the
    /// <c>default</c> key, or it was issued by another map.
    /// </exception>
    public TValue this[SlotKey key]
    {
        get
        {
            Slot[] slots = _slots;
            int slot = key.Index;
            if ((uint)slot >= (uint)slots.Length || !Matches(slots[slot].Version, key.Version))
                ThrowKeyNotFound(key);

            return slots[slot].Value!;
        }
        set
        {
            Slot[] slots = _slots;
            int slot = key.Index;
            if ((uint)slot >= (uint)slots.Length || !Matches(slots[slot].Version, key.Version))
                ThrowKeyNotFound(key);

            // A pure overwrite is not a structural change, so no version bump.
            slots[slot].Value = value;
        }
    }

    /// <summary>
    /// Stores <paramref name="value"/> and returns the key that addresses it.
    /// </summary>
    /// <param name="value">The value to store. Duplicates and <c>null</c> are allowed; each call gets its own key.</param>
    /// <returns>
    /// A key that addresses <paramref name="value"/> until it is removed or the map is cleared. A vacated slot
    /// is reused before a new one is issued, but always under a version no earlier key carried.
    /// </returns>
    public SlotKey Add(TValue value)
    {
        int slot = _freeHead;
        if (slot >= 0)
        {
            _freeHead = _slots[slot].NextFree;
        }
        else
        {
            if (_slotCount == _slots.Length)
                Grow(_slots.Length == 0 ? 4 : _slots.Length * 2);

            slot = _slotCount++;
        }

        // Vacant versions are even, so this makes the version odd: live. A never-used slot goes 0 -> 1, which
        // is why the default key (version 0) can never resolve.
        ref Slot entry = ref _slots[slot];
        uint version = entry.Version + 1;
        entry.Version = version;
        entry.Value = value;
        _count++;
        _version++;

        return new SlotKey(slot, version);
    }

    /// <summary>Determines whether <paramref name="key"/> addresses a live value in this map.</summary>
    /// <param name="key">The key to test.</param>
    /// <returns>
    /// <c>true</c> if the key is live; <c>false</c> if its value was removed, the map was cleared since, or it
    /// is the <c>default</c> key.
    /// </returns>
    public bool ContainsKey(SlotKey key)
    {
        Slot[] slots = _slots;
        int slot = key.Index;
        return (uint)slot < (uint)slots.Length && Matches(slots[slot].Version, key.Version);
    }

    /// <summary>
    /// Determines whether the map contains the specified value.
    /// </summary>
    /// <param name="value">
    /// The value to locate. Equality is determined via <see cref="EqualityComparer{T}.Default"/>, matching BCL
    /// <see cref="Dictionary{TKey, TValue}.ContainsValue(TValue)"/> semantics.
    /// </param>
    /// <returns><c>true</c> if a matching value is found; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// This operation is <c>O(n)</c> in the number of slots issued: it scans the slots, skipping vacant ones.
    /// </remarks>
    public bool ContainsValue(TValue? value)
    {
        var comparer = EqualityComparer<TValue?>.Default;
        Slot[] slots = _slots;

        for (int i = 0; i < _slotCount; i++)
        {
            if ((slots[i].Version & 1) != 0 && comparer.Equals(slots[i].Value, value))
                return true;
        }

        return false;
    }

    /// <summary>Attempts to get the value addressed by <paramref name="key"/>.</summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">
    /// When this method returns, contains the value the key addresses if it is live; otherwise the default value
    /// of <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><c>true</c> if the key is live; otherwise <c>false</c>.</returns>
    public bool TryGetValue(SlotKey key, out TValue? value)
    {
        Slot[] slots = _slots;
        int slot = key.Index;
        if ((uint)slot < (uint)slots.Length)
        {
            ref Slot entry = ref slots[slot];
            if (Matches(entry.Version, key.Version))
            {
                value = entry.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Removes the value addressed by <paramref name="key"/> and retires the key.
    /// </summary>
    /// <param name="key">The key whose value to remove.</param>
    /// <returns><c>true</c> if the value was removed; <c>false</c> if the key was not live.</returns>
    /// <remarks>No other value moves, and every other key stays valid.</remarks>
    public bool Remove(SlotKey key) => Remove(key, out _);

    /// <summary>
    /// Removes the value addressed by <paramref name="key"/>, retires the key, and copies the removed value to
    /// <paramref name="value"/>.
    /// </summary>
    /// <param name="key">The key whose value to remove.</param>
    /// <param name="value">
    /// When this method returns, contains the removed value if the key was live; otherwise the default value of
    /// <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><c>true</c> if the value was removed; <c>false</c> if the key was not live.</returns>
    /// <remarks>No other value moves, and every other key stays valid.</remarks>
    public bool Remove(SlotKey key, out TValue? value)
    {
        Slot[] slots = _slots;
        int slot = key.Index;
        if ((uint)slot < (uint)slots.Length)
        {
            ref Slot entry = ref slots[slot];
            if (Matches(entry.Version, key.Version))
            {
                value = entry.Value;
                Vacate(ref entry, slot);
                _count--;
                _version++;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Removes every value and retires every outstanding key.
    /// </summary>
    /// <remarks>
    /// This walks every slot issued so far: each live one has its version stepped, its value released, and goes
    /// back on the free list. Slots that were already vacant are not stepped, so a key retired before the call
    /// is not walked any closer to the version ceiling by it. Calling it on an empty map changes nothing and
    /// does not invalidate enumerators. The capacity is kept.
    /// </remarks>
    public void Clear()
    {
        if (_count == 0)
            return;

        Slot[] slots = _slots;
        for (int i = 0; i < _slotCount; i++)
        {
            if ((slots[i].Version & 1) != 0)
                Vacate(ref slots[i], i);
        }

        _count = 0;
        _version++;
    }

    /// <summary>
    /// Ensures the map can hold at least <paramref name="capacity"/> values without growing, enlarging its slot
    /// array in a single copy if it is currently smaller. Pre-sizing before a bulk insert of a known size
    /// avoids the incremental doublings an unsized map would otherwise pay. The array is never shrunk by this
    /// call.
    /// </summary>
    /// <param name="capacity">The minimum number of values the map must hold.</param>
    /// <returns>The capacity the map can now hold before it grows.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public int EnsureCapacity(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        if (capacity > _slots.Length)
        {
            Grow(capacity);
            _version++;
        }

        return _slots.Length;
    }

    /// <summary>
    /// Returns an allocation-free enumerator that yields each key and value stored in the map, in ascending slot
    /// order. That order is not insertion order once a slot has been reused; do not rely on it. If the map is
    /// modified during enumeration, <see cref="Enumerator.MoveNext"/> throws
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over this map.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    /// <summary>
    /// Gets an enumerable view over the live keys, in the map's enumeration order. The view is a lightweight
    /// struct and iterating it does not allocate.
    /// </summary>
    public KeyCollection Keys => new KeyCollection(this);

    /// <summary>
    /// Gets an enumerable view over the values, in the map's enumeration order. The view is a lightweight
    /// struct and iterating it does not allocate.
    /// </summary>
    public ValueCollection Values => new ValueCollection(this);

    /// <summary>
    /// Copies every key and value into <paramref name="array"/> starting at <paramref name="arrayIndex"/>. The
    /// order matches <see cref="GetEnumerator"/>.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
    public void CopyTo(KeyValuePair<SlotKey, TValue?>[] array, int arrayIndex)
    {
        CopyToGuard.Validate(array, arrayIndex, _count, CopyToGuard.EntriesMessage);

        Slot[] slots = _slots;
        for (int i = 0; i < _slotCount; i++)
        {
            if ((slots[i].Version & 1) != 0)
                array[arrayIndex++] = new KeyValuePair<SlotKey, TValue?>(new SlotKey(i, slots[i].Version), slots[i].Value);
        }
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    // Whether a slot at `slotVersion` is live and still holds the value a key at `keyVersion` was issued for.
    // Every issued key is odd, so the odd test only ever rejects the default key (version 0) — and it must:
    // a never-used slot sits at version 0, and so does a slot whose version has just wrapped past
    // uint.MaxValue, so equality alone would let the default key resolve a vacant slot. Testing the key
    // rather than the slot keeps it off the loaded value, and it is what lets the bounds test above be the
    // array's own length (a slot past the high-water mark is at version 0), which the JIT folds into the
    // index's range check.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Matches(uint slotVersion, uint keyVersion) =>
        slotVersion == keyVersion && (keyVersion & 1) != 0;

    // Steps a live slot's version to the next even value, which retires every key issued for it, and pushes
    // it onto the free list. The step is a plain wrapping increment: the odd versions run 1, 3, …,
    // uint.MaxValue, and uint.MaxValue + 1 wraps to 0 — even, so vacant, exactly as a never-used slot starts.
    // The default key, at version 0, can therefore never match a live slot, with no branch the coverage gate
    // could never see taken.
    private void Vacate(ref Slot entry, int slot)
    {
        entry.Version++;
        entry.NextFree = _freeHead;
        _freeHead = slot;

        // Release the value's reference so it can be collected. The intrinsic is constant-folded per closed
        // generic type, so a TValue that holds no reference skips the store.
        if (RuntimeHelpers.IsReferenceOrContainsReferences<TValue>())
            entry.Value = default;
    }

    private void Grow(int requested)
    {
        int capacity = ClampGrowth(requested, _slots.Length);
        Array.Resize(ref _slots, capacity);
    }

    // The growth ceiling. Doubling past 2^30 wraps the int negative, so the request is clamped to
    // Array.MaxLength; at Array.MaxLength itself there is nowhere left to grow. The doubling path needs over a
    // billion simultaneously live values — upwards of 12 GB of slots before TValue's own size — which no CI
    // runner has, and EnsureCapacity reaches the clamp only with a request that then fails allocating the
    // array. Both arms are kept because they are the only thing between a saturated map and a silently
    // negative capacity.
    [ExcludeFromCodeCoverage(Justification = "Needs 2^30 simultaneously live values (over 12 GB of slots), " +
        "beyond any CI runner's memory.")]
    private static int ClampGrowth(int capacity, int current)
    {
        if ((uint)capacity > (uint)Array.MaxLength)
            capacity = Array.MaxLength;
        if (capacity <= current)
            throw new InvalidOperationException("The slot map has reached its maximum capacity.");

        return capacity;
    }

    [DoesNotReturn]
    private static void ThrowKeyNotFound(SlotKey key) =>
        throw new KeyNotFoundException($"Key {key} does not address a live value in this map.");

    // ── IReadOnlyDictionary<SlotKey, TValue?> explicit members ────────────────
    // The primary (non-interface) surface already covers the contract; these forwarders only widen those
    // members to the boxed IEnumerable<T> / IEnumerator<T> shapes the interface requires, so users who prefer
    // BCL ergonomics (LINQ, DI over IReadOnlyDictionary<,>) keep working without losing the zero-allocation
    // direct foreach.
    TValue? IReadOnlyDictionary<SlotKey, TValue?>.this[SlotKey key] => this[key];

    IEnumerable<SlotKey> IReadOnlyDictionary<SlotKey, TValue?>.Keys => Keys;

    IEnumerable<TValue?> IReadOnlyDictionary<SlotKey, TValue?>.Values => Values;

    IEnumerator<KeyValuePair<SlotKey, TValue?>> IEnumerable<KeyValuePair<SlotKey, TValue?>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// A struct enumerator over a <see cref="SlotMap{TValue}"/>. Because it is a struct, iterating it via
    /// <c>foreach</c> avoids the allocation that a compiler-generated <see cref="IEnumerator{T}"/> would incur.
    /// It scans the slot array in order, skipping vacant slots.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<SlotKey, TValue?>>
    {
        private readonly SlotMap<TValue> _map;
        private readonly int _version;
        private int _index;
        private KeyValuePair<SlotKey, TValue?> _current;

        internal Enumerator(SlotMap<TValue> map)
        {
            _map = map;
            _version = map._version;
            _index = -1;
            _current = default;
        }

        /// <summary>Gets the key and value at the current position of the enumerator.</summary>
        public KeyValuePair<SlotKey, TValue?> Current => _current;

        object IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next key and value.</summary>
        /// <returns>
        /// <c>true</c> if the enumerator advanced to a new entry; <c>false</c> if it has passed the end of the
        /// map.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the map was modified since the enumerator was created.
        /// </exception>
        public bool MoveNext()
        {
            if (_version != _map._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            Slot[] slots = _map._slots;
            int end = _map._slotCount;
            while (++_index < end)
            {
                uint version = slots[_index].Version;
                if ((version & 1) != 0)
                {
                    _current = new KeyValuePair<SlotKey, TValue?>(new SlotKey(_index, version), slots[_index].Value);
                    return true;
                }
            }

            // Park the index at the end so a repeated MoveNext after exhaustion does not walk past it.
            _index = end;
            _current = default;
            return false;
        }

        /// <summary>Resets the enumerator to its initial position, before the first entry.</summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the map was modified since the enumerator was created.
        /// </exception>
        public void Reset()
        {
            if (_version != _map._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _index = -1;
            _current = default;
        }

        /// <summary>Releases any resources held by the enumerator. No-op for this type.</summary>
        public void Dispose() { }
    }

    /// <summary>
    /// A struct enumerable view over the live keys of a <see cref="SlotMap{TValue}"/>. Iterating it does not
    /// allocate; passing it through <see cref="IEnumerable{T}"/> will box the enumerator and is therefore not
    /// zero-allocation. It is a read-only <see cref="ICollection{T}"/>: the mutating members throw
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    public readonly struct KeyCollection : ICollection<SlotKey>
    {
        private readonly SlotMap<TValue> _map;

        internal KeyCollection(SlotMap<TValue> map) => _map = map;

        /// <summary>Gets the number of keys in the view (equal to the map's count).</summary>
        public int Count => _map._count;

        /// <summary>Gets a value indicating whether the view is read-only. Always <c>true</c>.</summary>
        public bool IsReadOnly => true;

        /// <summary>Returns an allocation-free struct enumerator over the keys, in the map's enumeration order.</summary>
        /// <returns>A struct enumerator over the keys.</returns>
        public Enumerator GetEnumerator() => new Enumerator(_map);

        /// <summary>Determines whether <paramref name="item"/> is a live key of the map.</summary>
        /// <param name="item">The key to look for.</param>
        /// <returns><c>true</c> if the key is live.</returns>
        public bool Contains(SlotKey item) => _map.ContainsKey(item);

        /// <summary>
        /// Copies the keys into <paramref name="array"/> starting at <paramref name="arrayIndex"/>, in the map's
        /// enumeration order.
        /// </summary>
        /// <param name="array">The destination array.</param>
        /// <param name="arrayIndex">The zero-based index at which copying begins.</param>
        /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.
        /// </exception>
        /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
        public void CopyTo(SlotKey[] array, int arrayIndex)
        {
            CopyToGuard.Validate(array, arrayIndex, _map._count, CopyToGuard.KeysMessage);

            foreach (KeyValuePair<SlotKey, TValue?> entry in _map)
                array[arrayIndex++] = entry.Key;
        }

        IEnumerator<SlotKey> IEnumerable<SlotKey>.GetEnumerator() => new Enumerator(_map);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_map);

        void ICollection<SlotKey>.Add(SlotKey item) =>
            throw new NotSupportedException("The key view is read-only.");

        void ICollection<SlotKey>.Clear() =>
            throw new NotSupportedException("The key view is read-only.");

        bool ICollection<SlotKey>.Remove(SlotKey item) =>
            throw new NotSupportedException("The key view is read-only.");

        /// <summary>A struct enumerator over the keys of a <see cref="SlotMap{TValue}"/>.</summary>
        public struct Enumerator : IEnumerator<SlotKey>
        {
            private SlotMap<TValue>.Enumerator _inner;

            internal Enumerator(SlotMap<TValue> map) => _inner = map.GetEnumerator();

            /// <summary>Gets the current key.</summary>
            public SlotKey Current => _inner.Current.Key;

            object IEnumerator.Current => _inner.Current.Key;

            /// <summary>Advances to the next key.</summary>
            /// <returns><c>true</c> if the enumerator advanced to a new key.</returns>
            public bool MoveNext() => _inner.MoveNext();

            /// <summary>Resets the enumerator to its initial position.</summary>
            public void Reset() => _inner.Reset();

            /// <summary>No-op.</summary>
            public void Dispose() => _inner.Dispose();
        }
    }

    /// <summary>
    /// A struct enumerable view over the values of a <see cref="SlotMap{TValue}"/>. Iterating it does not
    /// allocate; passing it through <see cref="IEnumerable{T}"/> will box the enumerator and is therefore not
    /// zero-allocation. It is a read-only <see cref="ICollection{T}"/>: the mutating members throw
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    public readonly struct ValueCollection : ICollection<TValue?>
    {
        private readonly SlotMap<TValue> _map;

        internal ValueCollection(SlotMap<TValue> map) => _map = map;

        /// <summary>Gets the number of values in the view (equal to the map's count).</summary>
        public int Count => _map._count;

        /// <summary>Gets a value indicating whether the view is read-only. Always <c>true</c>.</summary>
        public bool IsReadOnly => true;

        /// <summary>Returns an allocation-free struct enumerator over the values, in the map's enumeration order.</summary>
        /// <returns>A struct enumerator over the values.</returns>
        public Enumerator GetEnumerator() => new Enumerator(_map);

        /// <summary>Determines whether any live entry holds <paramref name="item"/>. This is an <c>O(n)</c> scan.</summary>
        /// <param name="item">The value to look for.</param>
        /// <returns><c>true</c> if at least one entry holds the value.</returns>
        public bool Contains(TValue? item) => _map.ContainsValue(item);

        /// <summary>
        /// Copies the values into <paramref name="array"/> starting at <paramref name="arrayIndex"/>, in the
        /// map's enumeration order.
        /// </summary>
        /// <param name="array">The destination array.</param>
        /// <param name="arrayIndex">The zero-based index at which copying begins.</param>
        /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.
        /// </exception>
        /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
        public void CopyTo(TValue?[] array, int arrayIndex)
        {
            CopyToGuard.Validate(array, arrayIndex, _map._count, CopyToGuard.ValuesMessage);

            foreach (KeyValuePair<SlotKey, TValue?> entry in _map)
                array[arrayIndex++] = entry.Value;
        }

        IEnumerator<TValue?> IEnumerable<TValue?>.GetEnumerator() => new Enumerator(_map);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_map);

        void ICollection<TValue?>.Add(TValue? item) =>
            throw new NotSupportedException("The value view is read-only.");

        void ICollection<TValue?>.Clear() =>
            throw new NotSupportedException("The value view is read-only.");

        bool ICollection<TValue?>.Remove(TValue? item) =>
            throw new NotSupportedException("The value view is read-only.");

        /// <summary>A struct enumerator over the values of a <see cref="SlotMap{TValue}"/>.</summary>
        public struct Enumerator : IEnumerator<TValue?>
        {
            private SlotMap<TValue>.Enumerator _inner;

            internal Enumerator(SlotMap<TValue> map) => _inner = map.GetEnumerator();

            /// <summary>Gets the current value.</summary>
            public TValue? Current => _inner.Current.Value;

            object? IEnumerator.Current => _inner.Current.Value;

            /// <summary>Advances to the next value.</summary>
            /// <returns><c>true</c> if the enumerator advanced to a new value.</returns>
            public bool MoveNext() => _inner.MoveNext();

            /// <summary>Resets the enumerator to its initial position.</summary>
            public void Reset() => _inner.Reset();

            /// <summary>No-op.</summary>
            public void Dispose() => _inner.Dispose();
        }
    }
}
