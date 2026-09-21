using System.Collections;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// A dictionary keyed by <b>non-negative integers over a bounded universe</b> <c>[0, Universe)</c>,
/// backed by the classic Briggs&#8211;Torczon sparse representation (a dense key array and a parallel
/// dense value array, paired with a sparse index array). It is the dictionary counterpart of
/// <see cref="SparseSet"/>: where <see cref="SparseSet"/> stores the key alone,
/// <see cref="SparseMap{TValue}"/> carries a value alongside it. A lookup is an array index and a
/// confirming comparison &#8212; no hash, no probe chain, no per-entry node allocation.
/// </summary>
/// <typeparam name="TValue">The type of the stored values.</typeparam>
/// <remarks>
/// <para>
/// The map stores its present keys contiguously in a <em>dense</em> array (<c>[0, Count)</c>), the
/// matching values at the same offsets in a parallel array, and keeps a <em>sparse</em> array,
/// indexed by key, whose entry for a present key points back at that key's dense slot. Membership
/// is the sparse&#8596;dense round-trip <c>sparse[k] &lt; Count &amp;&amp; dense[sparse[k]] == k</c>,
/// which is correct even for a <em>stale</em> sparse entry &#8212; one left over from before a
/// <see cref="Clear"/>, or from a slot never written since construction (the array is
/// zero-initialized) &#8212; so <see cref="ContainsKey(int)"/> /
/// <see cref="TryGetValue(int, out TValue)"/> / <see cref="Add(int, TValue)"/> /
/// <see cref="Remove(int)"/> are each a direct array index with no hashing.
/// </para>
/// <para>
/// <b>The documented BCL-beating workload</b> is a map from small non-negative integers over a
/// known bounded universe that is <em>cleared and rebuilt frequently</em> and <em>iterated by
/// present entry</em>: per-frame / per-query side tables in graph traversal (distance, parent,
/// colour), ECS component storage, register-allocation state, and sweep-line algorithms. There,
/// <see cref="Dictionary{TKey, TValue}"/>'s <c>Clear</c> is <c>O(capacity)</c> (it zeroes the whole
/// entry table) and its enumeration walks a possibly-sparse table, whereas this type's enumeration
/// is a linear scan over the dense prefix.
/// </para>
/// <para>
/// <b><see cref="Clear"/> and value references.</b> <see cref="SparseSet.Clear"/> is
/// unconditionally <c>O(1)</c> because the round-trip check tolerates whatever the arrays still
/// hold. A map cannot leave references behind without retaining them, so the contract is split and
/// documented rather than hidden: when <typeparamref name="TValue"/> contains no references,
/// <see cref="Clear"/> is <c>O(1)</c> and touches nothing but the count; otherwise it clears the
/// dense prefix <c>[0, Count)</c> so no value survives the call &#8212; still <c>O(Count)</c> rather
/// than <see cref="Dictionary{TKey, TValue}"/>'s <c>O(capacity)</c>, and the key and sparse arrays
/// are untouched either way.
/// </para>
/// <para>
/// <b>Tradeoffs.</b> The sparse index array is <c>O(Universe)</c> memory, and the type stores only
/// non-negative keys below the fixed <see cref="Universe"/> chosen at construction. It is an opt-in
/// specialized type, not a <see cref="Dictionary{TKey, TValue}"/> replacement: for an unbounded or
/// huge-and-sparse key space, <see cref="IntDictionary{TValue}"/> /
/// <see cref="Dictionary{TKey, TValue}"/> remain the right choice. A key outside
/// <c>[0, Universe)</c> is rejected by the write surface (<see cref="Add(int, TValue)"/>,
/// <see cref="TryAdd(int, TValue)"/>, the indexer's set) with
/// <see cref="ArgumentOutOfRangeException"/>, and reported as absent by the read surface
/// (<see cref="ContainsKey(int)"/>, <see cref="TryGetValue(int, out TValue)"/>,
/// <see cref="Remove(int)"/>) &#8212; the bounded-universe analogue of
/// <see cref="EnumMap{TEnum, TValue}"/>.
/// </para>
/// <para>
/// It implements <see cref="IDictionary{TKey, TValue}"/> and
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/> over <c>&lt;int, TValue?&gt;</c> and ships an
/// allocation-free struct enumerator. Enumeration order is unspecified; in particular
/// <see cref="Remove(int)"/> moves the last dense entry into the vacated slot, so the order after a
/// removal is not the insertion order.
/// </para>
/// <para>
/// The type is single-threaded.
/// </para>
/// </remarks>
public class SparseMap<TValue> : IDictionary<int, TValue?>, IReadOnlyDictionary<int, TValue?>
{
    // The dense arrays hold the present keys and their values in [0, _count); the sparse array is
    // indexed by key and, for a present key k, sparse[k] is k's slot in dense. The dense arrays
    // grow together on demand (capped at _universe); the sparse array is sized to the universe
    // once at construction and is never cleared — the round-trip membership check tolerates its
    // garbage, which is what keeps Clear off the O(capacity) path.
    private int[] _dense;
    private TValue?[] _values;
    private readonly int[] _sparse;
    private readonly int _universe;
    private int _count;

    // Incremented on every structural mutation so active enumerators can detect concurrent
    // modification and throw, matching BCL semantics. A pure value overwrite of an existing key
    // does not bump it (matching Dictionary<,>).
    private int _version;

    /// <summary>
    /// Initializes a new, empty <see cref="SparseMap{TValue}"/> whose storable keys are the
    /// integers in <c>[0, universe)</c>.
    /// </summary>
    /// <param name="universe">
    /// The exclusive upper bound of storable keys; the map can hold any non-negative integer key
    /// strictly less than this. The sparse index array is sized to this length once, so it is the
    /// dominant memory cost — choose it to match the actual key range. A value of <c>0</c> creates
    /// a map that can store nothing.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="universe"/> is negative.</exception>
    public SparseMap(int universe)
    {
        if (universe < 0)
            throw new ArgumentOutOfRangeException(nameof(universe), universe, "Universe must be non-negative.");

        _universe = universe;
        _sparse = universe == 0 ? Array.Empty<int>() : new int[universe];
        _dense = Array.Empty<int>();
        _values = Array.Empty<TValue?>();
    }

    /// <summary>
    /// Initializes a new <see cref="SparseMap{TValue}"/> over <c>[0, universe)</c> that contains
    /// the key/value pairs copied from the specified <paramref name="source"/>.
    /// </summary>
    /// <param name="universe">The exclusive upper bound of storable keys (see <see cref="SparseMap{TValue}(int)"/>).</param>
    /// <param name="source">
    /// The collection whose key/value pairs are copied into the new map. If
    /// <paramref name="source"/> implements <see cref="ICollection{T}"/>, its <c>Count</c> is used
    /// to pre-size the dense backing arrays so the initial fill avoids resize work.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> contains one or more duplicate keys, matching BCL
    /// <see cref="Dictionary{TKey, TValue}"/> semantics.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="universe"/> is negative, or <paramref name="source"/> contains a key outside
    /// <c>[0, universe)</c>.
    /// </exception>
    public SparseMap(int universe, IEnumerable<KeyValuePair<int, TValue>> source)
        : this(NonNullUniverse(universe, source))
    {
        if (source is ICollection<KeyValuePair<int, TValue>> collection)
            EnsureDenseCapacity(Math.Min(_universe, collection.Count));

        foreach (KeyValuePair<int, TValue> entry in source)
            Add(entry.Key, entry.Value);
    }

    // Runs as part of the chained-ctor argument expression so the null check beats the primary
    // ctor's universe validation: a null source must surface as ArgumentNullException, not
    // ArgumentOutOfRangeException, even when the caller also passed a negative universe. Mirrors
    // SparseSet's NonNullUniverse.
    private static int NonNullUniverse(int universe, IEnumerable<KeyValuePair<int, TValue>> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return universe;
    }

    /// <summary>
    /// Gets the exclusive upper bound of the keys this map can store; every key is a non-negative
    /// integer strictly less than this.
    /// </summary>
    public int Universe => _universe;

    /// <summary>
    /// Gets the number of entries currently stored in the map.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets or sets the value associated with the specified key. On get, throws
    /// <see cref="KeyNotFoundException"/> if the key is not present. On set, adds a new entry or
    /// overwrites an existing one.
    /// </summary>
    /// <param name="key">The key whose value to get or set.</param>
    /// <returns>The value associated with the specified key.</returns>
    /// <exception cref="KeyNotFoundException">The key does not exist (get only).</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="key"/> is outside <c>[0, Universe)</c> (set only; the get reports such a key
    /// as not found).
    /// </exception>
    public TValue this[int key]
    {
        get
        {
            if ((uint)key >= (uint)_universe)
                throw new KeyNotFoundException($"Key {key} not found.");

            int index = _sparse[key];
            if ((uint)index >= (uint)_count || _dense[index] != key)
                throw new KeyNotFoundException($"Key {key} not found.");

            return _values[index]!;
        }
        set
        {
            ThrowIfKeyOutOfRange(key);

            int index = _sparse[key];
            if ((uint)index < (uint)_count && _dense[index] == key)
            {
                // A pure overwrite is not a structural change, so no version bump.
                _values[index] = value;
                return;
            }

            Insert(key, value);
        }
    }

    /// <summary>
    /// Determines whether the specified key is present in the map.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns>
    /// <c>true</c> if the key is found; otherwise <c>false</c> (including for a key outside
    /// <c>[0, Universe)</c>).
    /// </returns>
    public bool ContainsKey(int key)
    {
        if ((uint)key >= (uint)_universe)
            return false;

        return ContainsUnchecked(key);
    }

    /// <summary>
    /// Determines whether the map contains the specified value.
    /// </summary>
    /// <param name="value">
    /// The value to locate. Equality is determined via <see cref="EqualityComparer{T}.Default"/>,
    /// matching BCL <see cref="Dictionary{TKey, TValue}.ContainsValue(TValue)"/> semantics.
    /// </param>
    /// <returns><c>true</c> if a matching value is found; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// This operation is <c>O(n)</c> in the map's count: it scans the dense value prefix, which
    /// holds exactly the present entries and nothing else.
    /// </remarks>
    public bool ContainsValue(TValue? value)
    {
        var comparer = EqualityComparer<TValue?>.Default;
        TValue?[] values = _values;

        for (int i = 0; i < _count; i++)
        {
            if (comparer.Equals(values[i], value))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">
    /// When this method returns, contains the value associated with <paramref name="key"/> if
    /// found; otherwise the default value of <typeparamref name="TValue"/>.
    /// </param>
    /// <returns>
    /// <c>true</c> if the key was found; otherwise <c>false</c> (including for a key outside
    /// <c>[0, Universe)</c>).
    /// </returns>
    public bool TryGetValue(int key, out TValue? value)
    {
        if ((uint)key < (uint)_universe)
        {
            int index = _sparse[key];
            if ((uint)index < (uint)_count && _dense[index] == key)
            {
                value = _values[index];
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Adds the specified key and value to the map.
    /// Throws <see cref="ArgumentException"/> if the key already exists.
    /// </summary>
    /// <param name="key">The key of the element to add; must be in <c>[0, Universe)</c>.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <exception cref="ArgumentException">
    /// An element with the same <paramref name="key"/> already exists.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is outside <c>[0, Universe)</c>.</exception>
    public void Add(int key, TValue value)
    {
        if (!TryAdd(key, value))
            throw new ArgumentException($"An element with key {key} already exists.", nameof(key));
    }

    /// <summary>
    /// Attempts to add the specified key and value to the map.
    /// </summary>
    /// <param name="key">The key of the element to add; must be in <c>[0, Universe)</c>.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <returns>
    /// <c>true</c> if the key/value pair was added; <c>false</c> if the key already exists (the map
    /// is unchanged).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is outside <c>[0, Universe)</c>.</exception>
    public bool TryAdd(int key, TValue value)
    {
        ThrowIfKeyOutOfRange(key);

        if (ContainsUnchecked(key))
            return false;

        Insert(key, value);
        return true;
    }

    /// <summary>
    /// Removes the value with the specified key from the map.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <returns>
    /// <c>true</c> if the entry was removed; otherwise <c>false</c> (including if
    /// <paramref name="key"/> was not present or is outside <c>[0, Universe)</c>).
    /// </returns>
    /// <remarks>
    /// Removal moves the last dense entry into the vacated slot (an <c>O(1)</c> swap), so the
    /// relative order of the surviving entries is not preserved.
    /// </remarks>
    public bool Remove(int key) => Remove(key, out _);

    /// <summary>
    /// Removes the value with the specified key from the map and copies the removed value to the
    /// <paramref name="value"/> parameter.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <param name="value">
    /// When this method returns, contains the value that was associated with <paramref name="key"/>
    /// before removal if the key was found; otherwise the default value of
    /// <typeparamref name="TValue"/>.
    /// </param>
    /// <returns>
    /// <c>true</c> if the entry was removed; otherwise <c>false</c> (including if
    /// <paramref name="key"/> was not present or is outside <c>[0, Universe)</c>).
    /// </returns>
    /// <remarks>
    /// Removal moves the last dense entry into the vacated slot (an <c>O(1)</c> swap), so the
    /// relative order of the surviving entries is not preserved.
    /// </remarks>
    public bool Remove(int key, out TValue? value)
    {
        if ((uint)key >= (uint)_universe)
        {
            value = default;
            return false;
        }

        // Inline the membership round-trip so _sparse[key] is read once (rather than once in
        // ContainsUnchecked and again below) on this hot path.
        int index = _sparse[key];
        if ((uint)index >= (uint)_count || _dense[index] != key)
        {
            value = default;
            return false;
        }

        value = _values[index];

        int last = _count - 1;
        int lastKey = _dense[last];

        // Move the last dense entry into the removed slot and repoint its sparse entry.
        _dense[index] = lastKey;
        _values[index] = _values[last];
        _sparse[lastKey] = index;

        // Release the vacated tail slot's reference so it can be collected. The key array needs no
        // such clearing — an int holds nothing.
        _values[last] = default;

        _count = last;
        _version++;
        return true;
    }

    /// <summary>
    /// Removes all entries from the map. The key and sparse arrays are neither scanned, cleared,
    /// nor shrunk — only the count is reset — which is the type's defining advantage over
    /// <see cref="Dictionary{TKey, TValue}"/> for clear-and-rebuild workloads.
    /// </summary>
    /// <remarks>
    /// The value array is the one exception, and only when it can retain something: for a
    /// <typeparamref name="TValue"/> that contains no references this is <c>O(1)</c> and clears
    /// nothing, and otherwise the dense prefix <c>[0, Count)</c> is cleared so the map holds no
    /// value past the call. That is <c>O(Count)</c> — the number of entries actually present —
    /// where <see cref="Dictionary{TKey, TValue}.Clear"/> zeroes the whole entry table regardless
    /// of how few entries it holds.
    /// </remarks>
    public void Clear()
    {
        if (_count == 0)
            return;

        // No key or sparse clearing needed: the sparse↔dense round-trip check treats every slot as
        // absent once _count is 0, regardless of the stale contents left behind.
        if (RuntimeHelpers.IsReferenceOrContainsReferences<TValue>())
            Array.Clear(_values, 0, _count);

        _count = 0;
        _version++;
    }

    /// <summary>
    /// Ensures that the dense backing arrays can hold at least <paramref name="capacity"/> entries
    /// without growing, enlarging them in a single copy if they are currently smaller. Pre-sizing
    /// before a bulk insert of a known size avoids the incremental array doublings an unsized map
    /// would otherwise pay. The request is capped at <see cref="Universe"/> (the most entries the
    /// map can ever hold), and the arrays are never shrunk by this call.
    /// </summary>
    /// <param name="capacity">The minimum number of entries the dense arrays must hold.</param>
    /// <returns>The dense-array capacity the map can now hold before it grows.</returns>
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
    /// Reduces the dense backing arrays to exactly the current <see cref="Count"/>, reclaiming
    /// memory after the map has shrunk. The sparse index array (sized to <see cref="Universe"/>) is
    /// unaffected.
    /// </summary>
    public void TrimExcess() => TrimExcess(_count);

    /// <summary>
    /// Reduces (or grows) the dense backing arrays to hold exactly <paramref name="capacity"/>
    /// entries. The sparse index array is unaffected.
    /// </summary>
    /// <param name="capacity">
    /// The number of entries to size the dense arrays for. Must be at least the current
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
            Array.Resize(ref _values, capacity);
            _version++;
        }
    }

    /// <summary>
    /// Returns an allocation-free enumerator that yields each key/value pair stored in the map. The
    /// enumeration order is unspecified and may change after a <see cref="Remove(int)"/>; do not
    /// rely on it. If the map is modified during enumeration,
    /// <see cref="Enumerator.MoveNext"/> throws <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over this map.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    /// <summary>
    /// Gets an enumerable view over the keys in the map, in the map's enumeration order. The view
    /// is a lightweight struct and iterating it does not allocate.
    /// </summary>
    public KeyCollection Keys => new KeyCollection(this);

    /// <summary>
    /// Gets an enumerable view over the values in the map, in the map's enumeration order. The view
    /// is a lightweight struct and iterating it does not allocate.
    /// </summary>
    public ValueCollection Values => new ValueCollection(this);

    /// <summary>
    /// Copies every key/value pair into <paramref name="array"/> starting at
    /// <paramref name="arrayIndex"/>. The order matches <see cref="GetEnumerator"/>.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
    public void CopyTo(KeyValuePair<int, TValue?>[] array, int arrayIndex)
    {
        CopyToGuard.Validate(array, arrayIndex, _count, CopyToGuard.EntriesMessage);

        for (int i = 0; i < _count; i++)
            array[arrayIndex + i] = new KeyValuePair<int, TValue?>(_dense[i], _values[i]);
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    // The Briggs–Torczon membership round-trip, assuming key is already known to be in
    // [0, _universe). sparse[key] may be a stale value left from before a Clear, or the zero a
    // never-written slot still holds; the (uint) bound and the dense[...] == key confirmation
    // reject both.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ContainsUnchecked(int key)
    {
        int index = _sparse[key];
        return (uint)index < (uint)_count && _dense[index] == key;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfKeyOutOfRange(int key)
    {
        if ((uint)key >= (uint)_universe)
            throw new ArgumentOutOfRangeException(nameof(key), key,
                $"The key must be in [0, {_universe}).");
    }

    // Appends a known-absent, known-in-range key at the dense tail. Shared by TryAdd and the
    // indexer's insert path so the growth and bookkeeping live in one place.
    private void Insert(int key, TValue value)
    {
        int index = _count;
        if (index == _dense.Length)
            GrowDense();

        _dense[index] = key;
        _values[index] = value;
        _sparse[key] = index;
        _count = index + 1;
        _version++;
    }

    // Grows the dense arrays on a full insert. Dense never needs to hold more than _universe
    // entries (the map cannot contain more distinct keys than the universe), so growth is doubling
    // capped at _universe, computed in long to avoid the *2 overflow.
    private void GrowDense()
    {
        int len = _dense.Length;
        int newCapacity = len == 0
            ? Math.Min(4, _universe)
            : (int)Math.Min(_universe, (long)len * 2);

        Array.Resize(ref _dense, newCapacity);
        Array.Resize(ref _values, newCapacity);
    }

    // Enlarges the dense arrays to at least `capacity` (already clamped to <= _universe by the
    // caller). No-op when they already fit.
    private void EnsureDenseCapacity(int capacity)
    {
        if (capacity > _dense.Length)
        {
            Array.Resize(ref _dense, capacity);
            Array.Resize(ref _values, capacity);
        }
    }

    // ── IReadOnlyDictionary<int, TValue?> explicit members ────────────────────
    // The primary (non-interface) surface already covers the contract; these forwarders only widen
    // those members to the boxed IEnumerable<T> / IEnumerator<T> shapes the interface requires, so
    // users who prefer BCL ergonomics (LINQ, DI over IReadOnlyDictionary<,>) keep working without
    // losing the zero-allocation direct foreach.
    TValue? IReadOnlyDictionary<int, TValue?>.this[int key] => this[key];

    IEnumerable<int> IReadOnlyDictionary<int, TValue?>.Keys => Keys;

    IEnumerable<TValue?> IReadOnlyDictionary<int, TValue?>.Values => Values;

    IEnumerator<KeyValuePair<int, TValue?>> IEnumerable<KeyValuePair<int, TValue?>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // IDictionary<int, TValue?> explicit interface members. Everything the mutable interface adds
    // over the read-only one is already public — Add, Remove, Clear, the indexer, CopyTo — so these
    // forwarders only reshape Keys / Values to ICollection<T> and supply the
    // ICollection<KeyValuePair<,>> members, leaving every existing public signature untouched.
    TValue? IDictionary<int, TValue?>.this[int key]
    {
        get => this[key];
        set => this[key] = value!;
    }

    ICollection<int> IDictionary<int, TValue?>.Keys => Keys;

    ICollection<TValue?> IDictionary<int, TValue?>.Values => Values;

    void IDictionary<int, TValue?>.Add(int key, TValue? value) => Add(key, value!);

    bool ICollection<KeyValuePair<int, TValue?>>.IsReadOnly => false;

    void ICollection<KeyValuePair<int, TValue?>>.Add(KeyValuePair<int, TValue?> item) =>
        Add(item.Key, item.Value!);

    bool ICollection<KeyValuePair<int, TValue?>>.Contains(KeyValuePair<int, TValue?> item) =>
        TryGetValue(item.Key, out TValue? value) && EqualityComparer<TValue?>.Default.Equals(value, item.Value);

    bool ICollection<KeyValuePair<int, TValue?>>.Remove(KeyValuePair<int, TValue?> item)
    {
        // ICollection<KVP> semantics: remove only when the *pair* matches, so a stale value must
        // not delete the current entry.
        if (!TryGetValue(item.Key, out TValue? value) ||
            !EqualityComparer<TValue?>.Default.Equals(value, item.Value))
        {
            return false;
        }

        return Remove(item.Key);
    }

    /// <summary>
    /// A struct enumerator over a <see cref="SparseMap{TValue}"/>. Because it is a struct, iterating
    /// it via <c>foreach</c> avoids the allocation that a compiler-generated
    /// <see cref="IEnumerator{T}"/> would incur. It walks the dense arrays, so it is a contiguous,
    /// cache-friendly scan over exactly the present entries.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<int, TValue?>>
    {
        private readonly SparseMap<TValue> _map;
        private readonly int _version;
        private int _index;
        private KeyValuePair<int, TValue?> _current;

        internal Enumerator(SparseMap<TValue> map)
        {
            _map = map;
            _version = map._version;
            _index = -1;
            _current = default;
        }

        /// <summary>
        /// Gets the key/value pair at the current position of the enumerator.
        /// </summary>
        public KeyValuePair<int, TValue?> Current => _current;

        object IEnumerator.Current => _current;

        /// <summary>
        /// Advances the enumerator to the next key/value pair.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the enumerator advanced to a new entry; <c>false</c> if it has passed the
        /// end of the map.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the map was modified since the enumerator was created.
        /// </exception>
        public bool MoveNext()
        {
            if (_version != _map._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            SparseMap<TValue> map = _map;
            if (++_index < map._count)
            {
                _current = new KeyValuePair<int, TValue?>(map._dense[_index], map._values[_index]);
                return true;
            }

            _current = default;
            return false;
        }

        /// <summary>
        /// Resets the enumerator to its initial position, before the first entry.
        /// </summary>
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

        /// <summary>
        /// Releases any resources held by the enumerator. No-op for this type.
        /// </summary>
        public void Dispose() { }
    }

    /// <summary>
    /// A struct enumerable view over the keys of a <see cref="SparseMap{TValue}"/>. Iterating it
    /// does not allocate; passing it through <see cref="IEnumerable{T}"/> will box the enumerator
    /// and is therefore not zero-allocation. It is a read-only <see cref="ICollection{T}"/>: the
    /// mutating members throw <see cref="NotSupportedException"/>.
    /// </summary>
    public readonly struct KeyCollection : ICollection<int>
    {
        private readonly SparseMap<TValue> _map;

        internal KeyCollection(SparseMap<TValue> map) => _map = map;

        /// <summary>
        /// Gets the number of keys in the view (equal to the map's count).
        /// </summary>
        public int Count => _map._count;

        /// <summary>Gets a value indicating whether the view is read-only. Always <c>true</c>.</summary>
        public bool IsReadOnly => true;

        /// <summary>
        /// Returns an allocation-free struct enumerator over the keys, in the map's enumeration
        /// order.
        /// </summary>
        /// <returns>A struct enumerator over the keys.</returns>
        public Enumerator GetEnumerator() => new Enumerator(_map);

        /// <summary>Determines whether the map contains <paramref name="item"/> as a key.</summary>
        /// <param name="item">The key to look for.</param>
        /// <returns><c>true</c> if the key is present.</returns>
        public bool Contains(int item) => _map.ContainsKey(item);

        /// <summary>
        /// Copies the keys into <paramref name="array"/> starting at <paramref name="arrayIndex"/>,
        /// in the map's enumeration order.
        /// </summary>
        /// <param name="array">The destination array.</param>
        /// <param name="arrayIndex">The zero-based index at which copying begins.</param>
        /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.
        /// </exception>
        /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
        public void CopyTo(int[] array, int arrayIndex)
        {
            CopyToGuard.Validate(array, arrayIndex, _map._count, CopyToGuard.KeysMessage);

            Array.Copy(_map._dense, 0, array, arrayIndex, _map._count);
        }

        IEnumerator<int> IEnumerable<int>.GetEnumerator() => new Enumerator(_map);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_map);

        void ICollection<int>.Add(int item) =>
            throw new NotSupportedException("The key view is read-only.");

        void ICollection<int>.Clear() =>
            throw new NotSupportedException("The key view is read-only.");

        bool ICollection<int>.Remove(int item) =>
            throw new NotSupportedException("The key view is read-only.");

        /// <summary>
        /// A struct enumerator over the keys of a <see cref="SparseMap{TValue}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<int>
        {
            private SparseMap<TValue>.Enumerator _inner;

            internal Enumerator(SparseMap<TValue> map) => _inner = map.GetEnumerator();

            /// <summary>Gets the current key.</summary>
            public int Current => _inner.Current.Key;

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
    /// A struct enumerable view over the values of a <see cref="SparseMap{TValue}"/>. Iterating it
    /// does not allocate; passing it through <see cref="IEnumerable{T}"/> will box the enumerator
    /// and is therefore not zero-allocation. It is a read-only <see cref="ICollection{T}"/>: the
    /// mutating members throw <see cref="NotSupportedException"/>.
    /// </summary>
    public readonly struct ValueCollection : ICollection<TValue?>
    {
        private readonly SparseMap<TValue> _map;

        internal ValueCollection(SparseMap<TValue> map) => _map = map;

        /// <summary>
        /// Gets the number of values in the view (equal to the map's count).
        /// </summary>
        public int Count => _map._count;

        /// <summary>Gets a value indicating whether the view is read-only. Always <c>true</c>.</summary>
        public bool IsReadOnly => true;

        /// <summary>
        /// Returns an allocation-free struct enumerator over the values, in the map's enumeration
        /// order.
        /// </summary>
        /// <returns>A struct enumerator over the values.</returns>
        public Enumerator GetEnumerator() => new Enumerator(_map);

        /// <summary>Determines whether any entry holds <paramref name="item"/>. This is an <c>O(n)</c> scan.</summary>
        /// <param name="item">The value to look for.</param>
        /// <returns><c>true</c> if at least one entry holds the value.</returns>
        public bool Contains(TValue? item) => _map.ContainsValue(item);

        /// <summary>
        /// Copies the values into <paramref name="array"/> starting at
        /// <paramref name="arrayIndex"/>, in the map's enumeration order.
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

            Array.Copy(_map._values, 0, array, arrayIndex, _map._count);
        }

        IEnumerator<TValue?> IEnumerable<TValue?>.GetEnumerator() => new Enumerator(_map);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_map);

        void ICollection<TValue?>.Add(TValue? item) =>
            throw new NotSupportedException("The value view is read-only.");

        void ICollection<TValue?>.Clear() =>
            throw new NotSupportedException("The value view is read-only.");

        bool ICollection<TValue?>.Remove(TValue? item) =>
            throw new NotSupportedException("The value view is read-only.");

        /// <summary>
        /// A struct enumerator over the values of a <see cref="SparseMap{TValue}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TValue?>
        {
            private SparseMap<TValue>.Enumerator _inner;

            internal Enumerator(SparseMap<TValue> map) => _inner = map.GetEnumerator();

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
