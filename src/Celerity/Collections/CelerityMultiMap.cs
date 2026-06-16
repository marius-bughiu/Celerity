using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A high-performance multi-map (a.k.a. multi-dictionary or one-to-many map):
/// each key maps to an ordered group of values rather than a single value.
/// Parameterized on a custom <see cref="IHashProvider{T}"/> so the JIT can
/// devirtualize and inline the key hash, exactly like
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/>.
/// </summary>
/// <remarks>
/// <para>
/// The keys live in the same open-addressed, linear-probing table that backs
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/>; alongside each key
/// slot is a value group (a <see cref="List{T}"/> of the values added under that
/// key, in insertion order). <see cref="Add(TKey, TValue)"/> always appends —
/// adding the same key twice groups the values rather than rejecting the second
/// add, and adding the same value twice under one key keeps both copies. This is
/// the behaviour callers reach for when modelling one-to-many relationships
/// (event handlers per event, members per group, postings per term).
/// </para>
/// <para>
/// Reads are allocation-free: the indexer and <see cref="TryGetValues"/> hand
/// back a lightweight <see cref="ValueGroup"/> struct view over the live backing
/// list, and the enumerator yields struct <see cref="Grouping"/> values. The map
/// is not allocation-free on the write path — each distinct key allocates one
/// backing list — which is inherent to storing a group per key.
/// </para>
/// <para>
/// The map implements <see cref="ILookup{TKey, TValue}"/> (over
/// <c>TValue?</c>), so it interoperates with LINQ and any consumer that accepts
/// an <c>ILookup</c>. The indexer returns an empty group for an absent key
/// (matching <see cref="ILookup{TKey, TValue}"/> semantics) rather than throwing.
/// </para>
/// </remarks>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TValue">The type of the values grouped under each key.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute key hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class CelerityMultiMap<TKey, TValue, THasher>
    : ILookup<TKey, TValue?>
    where THasher : struct, IHashProvider<TKey>
{
    /// <summary>
    /// The default initial capacity of the map if no capacity is specified.
    /// </summary>
    private const int DEFAULT_CAPACITY = 16;

    /// <summary>
    /// The default load factor of the map if no load factor is specified.
    /// </summary>
    private const float DEFAULT_LOAD_FACTOR = 0.75f;

    private int _count = 0;
    private int _valueCount = 0;
    private TKey?[] _keys;
    private List<TValue?>?[] _groups;
    private readonly float _loadFactor;
    private int _threshold;
    private readonly THasher _hasher;

    // default(TKey) (null for reference types, 0 for primitives, Guid.Empty for
    // Guid, etc.) collides with the "empty slot" sentinel used during probing,
    // so its value group is stored out-of-band in a dedicated field. _count
    // includes this entry when _hasDefaultKey is true.
    private bool _hasDefaultKey;
    private List<TValue?>? _defaultKeyGroup;

    // Incremented on every structural mutation so active enumerators can detect
    // concurrent modification and throw, matching BCL semantics. A change to a
    // value group (Add/Remove of a value) is a structural mutation too.
    private int _version;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="CelerityMultiMap{TKey, TValue, THasher}"/> class using the
    /// specified capacity and load factor.
    /// </summary>
    /// <param name="capacity">
    /// The initial key capacity, automatically rounded to the next power of two.
    /// </param>
    /// <param name="loadFactor">
    /// The fraction of the key table that can be filled before resizing.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative, or <paramref name="loadFactor"/>
    /// is not in the open interval (0, 1).
    /// </exception>
    public CelerityMultiMap(
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (loadFactor <= 0f || loadFactor >= 1f)
            throw new ArgumentOutOfRangeException(nameof(loadFactor), loadFactor, "Load factor must be between 0 (exclusive) and 1 (exclusive).");

        int size = FastUtils.NextPowerOfTwo(capacity);

        _keys = new TKey?[size];
        _groups = new List<TValue?>?[size];
        _loadFactor = loadFactor;
        _threshold = (int)(size * _loadFactor);
        _hasher = default;
    }

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="CelerityMultiMap{TKey, TValue, THasher}"/> class that groups the
    /// key/value pairs copied from the specified <paramref name="source"/>. Pairs
    /// that share a key are grouped together in source order; unlike a dictionary,
    /// duplicate keys are not an error.
    /// </summary>
    /// <param name="source">
    /// The collection whose key/value pairs are grouped into the new map.
    /// </param>
    /// <param name="capacity">
    /// The minimum initial key capacity, rounded up to the next power of two. When
    /// the source's count is larger, the key table is sized — including load-factor
    /// headroom — to hold the whole source without resizing.
    /// </param>
    /// <param name="loadFactor">
    /// The fraction of the key table that can be filled before resizing.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is <c>null</c>.
    /// </exception>
    public CelerityMultiMap(
        IEnumerable<KeyValuePair<TKey, TValue>> source,
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
        : this(InitialCapacityForSource(source, capacity, loadFactor), loadFactor)
    {
        foreach (KeyValuePair<TKey, TValue> kvp in source)
        {
            Add(kvp.Key, kvp.Value);
        }
    }

    // Runs as part of the chained-ctor argument expression so the null check
    // beats the primary ctor's capacity / loadFactor validation: a null source
    // must surface as ArgumentNullException, not ArgumentOutOfRangeException
    // when the user also passed an invalid loadFactor (issue #94).
    private static int InitialCapacityForSource(
        IEnumerable<KeyValuePair<TKey, TValue>> source,
        int capacity,
        float loadFactor)
    {
        ArgumentNullException.ThrowIfNull(source);
        int count = (source as ICollection<KeyValuePair<TKey, TValue>>)?.Count ?? 0;

        // Size the key table for the source count *including* load-factor headroom:
        // the resize threshold is size*loadFactor, so a table sized to the raw
        // count would still rehash on the last inserts of the bulk fill. Scaling
        // the count up by 1/loadFactor keeps a distinct-key bulk fill resize-free
        // (issue #27); a source whose keys mostly duplicate just leaves slack,
        // never a resize. A non-collection source (count 0) or an out-of-range
        // loadFactor — left for the primary ctor to reject, so
        // null-source-beats-bad-loadFactor ordering is preserved — falls through to
        // the plain capacity.
        if (count > 0 && loadFactor > 0f && loadFactor < 1f)
        {
            int withHeadroom = (int)Math.Ceiling(count / (double)loadFactor);
            if (withHeadroom > count)
                count = withHeadroom;
        }

        return Math.Max(capacity, count);
    }

    /// <summary>
    /// Gets the number of distinct keys contained in the map. This is the
    /// number of value groups, not the total number of values; see
    /// <see cref="ValueCount"/> for the latter.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets the total number of values stored across all keys. A key that has had
    /// <c>n</c> values added (counting duplicates) contributes <c>n</c>.
    /// </summary>
    public int ValueCount => _valueCount;

    /// <summary>
    /// Gets the group of values associated with the specified key. Returns an
    /// empty group if the key is not present (matching
    /// <see cref="ILookup{TKey, TValue}"/> semantics rather than throwing).
    /// </summary>
    /// <param name="key">The key whose value group to get.</param>
    /// <returns>
    /// A lightweight, allocation-free <see cref="ValueGroup"/> view over the
    /// values added under <paramref name="key"/>, in insertion order; empty if the
    /// key is absent.
    /// </returns>
    public ValueGroup this[TKey key]
    {
        get
        {
            List<TValue?>? group = FindGroup(key);
            return new ValueGroup(group);
        }
    }

    /// <summary>
    /// Determines whether the specified key has at least one value in the map.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns><c>true</c> if the key is present; otherwise, <c>false</c>.</returns>
    public bool ContainsKey(TKey key) => FindGroup(key) is not null;

    /// <summary>
    /// Determines whether the specified <paramref name="value"/> is present in the
    /// group of values associated with <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The key whose group to search.</param>
    /// <param name="value">
    /// The value to locate. Equality is determined via
    /// <see cref="EqualityComparer{T}.Default"/>.
    /// </param>
    /// <returns>
    /// <c>true</c> if <paramref name="key"/> is present and its group contains
    /// <paramref name="value"/>; otherwise, <c>false</c>.
    /// </returns>
    public bool Contains(TKey key, TValue? value)
    {
        List<TValue?>? group = FindGroup(key);
        return group is not null && group.Contains(value);
    }

    /// <summary>
    /// Determines whether the specified <paramref name="value"/> is present under
    /// any key in the map.
    /// </summary>
    /// <param name="value">
    /// The value to locate. Equality is determined via
    /// <see cref="EqualityComparer{T}.Default"/>, matching BCL
    /// <see cref="Dictionary{TKey, TValue}.ContainsValue(TValue)"/> semantics.
    /// </param>
    /// <returns><c>true</c> if a matching value is found; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This operation is <c>O(ValueCount)</c>: it scans every value group.
    /// </remarks>
    public bool ContainsValue(TValue? value)
    {
        if (_defaultKeyGroup is not null && _defaultKeyGroup.Contains(value))
            return true;

        List<TValue?>?[] groups = _groups;
        for (int i = 0; i < groups.Length; i++)
        {
            List<TValue?>? group = groups[i];
            if (group is not null && group.Contains(value))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the number of values associated with the specified key, or <c>0</c>
    /// if the key is not present.
    /// </summary>
    /// <param name="key">The key whose value count to get.</param>
    /// <returns>The number of values grouped under <paramref name="key"/>.</returns>
    public int CountValues(TKey key) => FindGroup(key)?.Count ?? 0;

    /// <summary>
    /// Attempts to get the group of values associated with the specified key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="values">
    /// When this method returns, contains a <see cref="ValueGroup"/> over the
    /// values associated with <paramref name="key"/> if found; otherwise an empty
    /// group.
    /// </param>
    /// <returns><c>true</c> if the key was found; otherwise, <c>false</c>.</returns>
    public bool TryGetValues(TKey key, out ValueGroup values)
    {
        List<TValue?>? group = FindGroup(key);
        values = new ValueGroup(group);
        return group is not null;
    }

    /// <summary>
    /// Adds the specified value to the group associated with <paramref name="key"/>,
    /// creating the group if the key is not yet present. Always succeeds; adding a
    /// duplicate key groups the values, and adding a duplicate value keeps both.
    /// </summary>
    /// <param name="key">The key to add the value under.</param>
    /// <param name="value">The value to append to the key's group.</param>
    public void Add(TKey key, TValue value)
    {
        GroupForAdd(key).Add(value);
        _valueCount++;
        _version++;
    }

    /// <summary>
    /// Adds all of the specified <paramref name="values"/> to the group associated
    /// with <paramref name="key"/>, creating the group if the key is not yet
    /// present.
    /// </summary>
    /// <param name="key">The key to add the values under.</param>
    /// <param name="values">The values to append to the key's group, in order.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="values"/> is <c>null</c>.
    /// </exception>
    public void AddRange(TKey key, IEnumerable<TValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        // Peek the first element before registering the key. GroupForAdd eagerly
        // creates the group, inserts the key, and bumps _count, so calling it for
        // an empty sequence would leave a phantom key with an empty group and an
        // inflated _count — breaking the "present key ⇒ at least one value"
        // invariant that Add / Remove / RemoveAll maintain. Touch the table only
        // once we know at least one value will actually be added.
        using IEnumerator<TValue> enumerator = values.GetEnumerator();
        if (!enumerator.MoveNext())
            return;

        List<TValue?> group = GroupForAdd(key);
        int added = 0;
        do
        {
            group.Add(enumerator.Current);
            added++;
        }
        while (enumerator.MoveNext());

        _valueCount += added;
        _version++;
    }

    /// <summary>
    /// Removes a single occurrence of <paramref name="value"/> from the group
    /// associated with <paramref name="key"/>. If that empties the group, the key
    /// itself is removed from the map.
    /// </summary>
    /// <param name="key">The key whose group to remove a value from.</param>
    /// <param name="value">
    /// The value to remove. Equality is determined via
    /// <see cref="EqualityComparer{T}.Default"/>. The first matching occurrence
    /// (in insertion order) is removed.
    /// </param>
    /// <returns>
    /// <c>true</c> if a matching value was found and removed; otherwise,
    /// <c>false</c> (including when the key is not present).
    /// </returns>
    public bool Remove(TKey key, TValue? value)
    {
        if (IsDefaultKey(key))
        {
            if (_defaultKeyGroup is null || !_defaultKeyGroup.Remove(value))
                return false;

            _valueCount--;
            if (_defaultKeyGroup.Count == 0)
            {
                _defaultKeyGroup = null;
                _hasDefaultKey = false;
                _count--;
            }
            _version++;
            return true;
        }

        int index = ProbeForKey(key);
        if (index < 0)
            return false;

        List<TValue?> group = _groups[index]!;
        if (!group.Remove(value))
            return false;

        _valueCount--;
        if (group.Count == 0)
        {
            BackwardShiftRemove(index);
            _count--;
        }
        _version++;
        return true;
    }

    /// <summary>
    /// Removes the specified key and all of its values from the map.
    /// </summary>
    /// <param name="key">The key to remove entirely.</param>
    /// <returns>
    /// <c>true</c> if the key was present and removed; otherwise, <c>false</c>.
    /// </returns>
    public bool RemoveAll(TKey key)
    {
        if (IsDefaultKey(key))
        {
            if (_defaultKeyGroup is null)
                return false;

            _valueCount -= _defaultKeyGroup.Count;
            _defaultKeyGroup = null;
            _hasDefaultKey = false;
            _count--;
            _version++;
            return true;
        }

        int index = ProbeForKey(key);
        if (index < 0)
            return false;

        _valueCount -= _groups[index]!.Count;
        BackwardShiftRemove(index);
        _count--;
        _version++;
        return true;
    }

    /// <summary>
    /// Removes all keys and values from the map. The underlying key capacity is
    /// preserved.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        Array.Clear(_keys, 0, _keys.Length);
        Array.Clear(_groups, 0, _groups.Length);
        _hasDefaultKey = false;
        _defaultKeyGroup = null;
        _count = 0;
        _valueCount = 0;
        _version++;
    }

    /// <summary>
    /// Returns an allocation-free enumerator that yields one
    /// <see cref="Grouping"/> per distinct key. The enumeration order is
    /// unspecified and may change across versions; do not rely on it. If the map
    /// is modified during enumeration, <see cref="Enumerator.MoveNext"/> throws
    /// <see cref="InvalidOperationException"/>. The out-of-band default-key group
    /// (if present) is yielded first.
    /// </summary>
    /// <returns>A struct enumerator over the groupings of this map.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    /// <summary>
    /// Gets an enumerable view over the distinct keys in the map. The view is a
    /// lightweight struct and iterating it does not allocate.
    /// </summary>
    public KeyCollection Keys => new KeyCollection(this);

    // ---- ILookup<TKey, TValue?> ----

    int ILookup<TKey, TValue?>.Count => _count;

    bool ILookup<TKey, TValue?>.Contains(TKey key) => ContainsKey(key);

    IEnumerable<TValue?> ILookup<TKey, TValue?>.this[TKey key] => this[key];

    IEnumerator<IGrouping<TKey, TValue?>> IEnumerable<IGrouping<TKey, TValue?>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- internal helpers ----

    private static bool IsDefaultKey(TKey key) =>
        EqualityComparer<TKey>.Default.Equals(key, default(TKey));

    // Returns the existing or newly-created value group for key, updating _count
    // (and the default-key bookkeeping) when a new key is introduced, but NOT
    // _valueCount or _version — the caller owns those once it has appended.
    private List<TValue?> GroupForAdd(TKey key)
    {
        if (IsDefaultKey(key))
        {
            if (_defaultKeyGroup is null)
            {
                _defaultKeyGroup = new List<TValue?>();
                _hasDefaultKey = true;
                _count++;
            }
            return _defaultKeyGroup;
        }

        int index = ProbeForInsert(key, out bool wasEmpty);
        if (wasEmpty)
        {
            if (_count >= _threshold)
            {
                Resize();
                index = ProbeForInsert(key, out _);
            }

            var group = new List<TValue?>();
            _keys[index] = key;
            _groups[index] = group;
            _count++;
            return group;
        }

        return _groups[index]!;
    }

    // Returns the value group for key (default-key group, table group, or null
    // if absent) without mutating the map.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private List<TValue?>? FindGroup(TKey key)
    {
        if (IsDefaultKey(key))
            return _defaultKeyGroup;

        int index = ProbeForKey(key);
        return index < 0 ? null : _groups[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ProbeForInsert(TKey key, out bool wasEmpty)
    {
        TKey?[] keys = _keys;
        ref TKey? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        int mask = keys.Length - 1;
        var comparer = EqualityComparer<TKey>.Default;
        int index = _hasher.Hash(key) & mask;

        while (true)
        {
            TKey? slot = Unsafe.Add(ref keysRef, (nint)(uint)index);
            if (comparer.Equals(slot, default(TKey))) { wasEmpty = true; return index; }
            if (comparer.Equals(slot, key)) { wasEmpty = false; return index; }
            index = (index + 1) & mask;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ProbeForKey(TKey key)
    {
        TKey?[] keys = _keys;
        ref TKey? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        int mask = keys.Length - 1;
        var comparer = EqualityComparer<TKey>.Default;
        int index = _hasher.Hash(key) & mask;

        while (true)
        {
            TKey? slot = Unsafe.Add(ref keysRef, (nint)(uint)index);
            if (comparer.Equals(slot, default(TKey))) return -1;
            if (comparer.Equals(slot, key)) return index;
            index = (index + 1) & mask;
        }
    }

    private void Resize()
    {
        int newSize = _keys.Length * 2;
        int mask = newSize - 1;
        TKey?[] oldKeys = _keys;
        List<TValue?>?[] oldGroups = _groups;

        // Reinsert each surviving key/group reference into the doubled table.
        // Every reinserted key is known unique (it came from a valid table), so
        // a bare probe-to-empty is sufficient — no equality check on duplicates.
        // The default-key group lives out-of-band and is not touched here.
        TKey?[] newKeys = new TKey?[newSize];
        List<TValue?>?[] newGroups = new List<TValue?>?[newSize];
        ref TKey? oldKeysRef = ref MemoryMarshal.GetArrayDataReference(oldKeys);
        ref TKey? newKeysRef = ref MemoryMarshal.GetArrayDataReference(newKeys);

        var comparer = EqualityComparer<TKey>.Default;
        for (int i = 0; i < oldKeys.Length; i++)
        {
            TKey? key = Unsafe.Add(ref oldKeysRef, (nint)(uint)i);
            if (comparer.Equals(key, default(TKey)))
                continue;

            int index = _hasher.Hash(key!) & mask;
            while (!comparer.Equals(Unsafe.Add(ref newKeysRef, (nint)(uint)index), default(TKey)))
                index = (index + 1) & mask;

            Unsafe.Add(ref newKeysRef, (nint)(uint)index) = key;
            newGroups[index] = oldGroups[i];
        }

        _keys = newKeys;
        _groups = newGroups;
        _threshold = (int)(newSize * _loadFactor);
    }

    // Backward-shift deletion (Knuth TAOCP Vol 3, §6.4 Algorithm R), mirroring
    // CelerityDictionary but shifting the parallel value-group references too.
    // The caller has already detached/emptied the group at startIndex; this
    // helper closes the gap and clears the final emptied slot.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BackwardShiftRemove(int startIndex)
    {
        TKey?[] keys = _keys;
        List<TValue?>?[] groups = _groups;
        ref TKey? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        int mask = keys.Length - 1;
        var comparer = EqualityComparer<TKey>.Default;
        int i = startIndex;
        int j = i;

        while (true)
        {
            j = (j + 1) & mask;
            TKey? candidateKey = Unsafe.Add(ref keysRef, (nint)(uint)j);
            if (comparer.Equals(candidateKey, default(TKey)))
                break;

            int k = _hasher.Hash(candidateKey!) & mask;

            bool bypassesGap = (i <= j)
                ? (i < k && k <= j)
                : (i < k || k <= j);
            if (bypassesGap)
                continue;

            Unsafe.Add(ref keysRef, (nint)(uint)i) = candidateKey;
            groups[i] = groups[j];
            i = j;
        }

        Unsafe.Add(ref keysRef, (nint)(uint)i) = default;
        groups[i] = null;
    }

    /// <summary>
    /// A lightweight, read-only struct view over the values associated with a
    /// single key. Iterating it does not allocate; passing it through
    /// <see cref="IEnumerable{T}"/> will box the enumerator and is therefore not
    /// zero-allocation. The view reflects the live backing group: mutating the
    /// map afterwards may change what a previously-obtained view yields.
    /// </summary>
    public readonly struct ValueGroup : IReadOnlyList<TValue?>
    {
        private readonly List<TValue?>? _group;

        internal ValueGroup(List<TValue?>? group) => _group = group;

        /// <summary>
        /// Gets the number of values in the group (<c>0</c> for an empty/absent group).
        /// </summary>
        public int Count => _group?.Count ?? 0;

        /// <summary>
        /// Gets the value at the specified index within the group, in insertion order.
        /// </summary>
        /// <param name="index">The zero-based index of the value to get.</param>
        /// <returns>The value at <paramref name="index"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/> is outside the bounds of the group.
        /// </exception>
        public TValue? this[int index]
        {
            get
            {
                if (_group is null || (uint)index >= (uint)_group.Count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return _group[index];
            }
        }

        /// <summary>
        /// Returns an allocation-free struct enumerator over the values in the group.
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(_group);

        IEnumerator<TValue?> IEnumerable<TValue?>.GetEnumerator() => new Enumerator(_group);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_group);

        /// <summary>
        /// A struct enumerator over the values of a <see cref="ValueGroup"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TValue?>
        {
            private readonly List<TValue?>? _group;
            private int _index;
            private TValue? _current;

            internal Enumerator(List<TValue?>? group)
            {
                _group = group;
                _index = -1;
                _current = default;
            }

            /// <summary>Gets the value at the current position.</summary>
            public TValue? Current => _current;

            object? IEnumerator.Current => _current;

            /// <summary>Advances to the next value in the group.</summary>
            /// <returns><c>true</c> if there was a next value; otherwise <c>false</c>.</returns>
            public bool MoveNext()
            {
                List<TValue?>? group = _group;
                if (group is not null && ++_index < group.Count)
                {
                    _current = group[_index];
                    return true;
                }
                _current = default;
                return false;
            }

            /// <summary>Resets the enumerator to its initial position.</summary>
            public void Reset()
            {
                _index = -1;
                _current = default;
            }

            /// <summary>No-op.</summary>
            public void Dispose() { }
        }
    }

    /// <summary>
    /// A key together with its group of values, yielded by the map's enumerator.
    /// Implements <see cref="IGrouping{TKey, TValue}"/> so the map's enumeration
    /// satisfies <see cref="ILookup{TKey, TValue}"/> and flows through LINQ.
    /// </summary>
    public readonly struct Grouping : IGrouping<TKey, TValue?>
    {
        private readonly TKey _key;
        private readonly List<TValue?>? _group;

        internal Grouping(TKey key, List<TValue?>? group)
        {
            _key = key;
            _group = group;
        }

        /// <summary>Gets the key of this grouping.</summary>
        public TKey Key => _key;

        /// <summary>Gets the group of values for this key as a struct view.</summary>
        public ValueGroup Values => new ValueGroup(_group);

        /// <summary>
        /// Returns an allocation-free struct enumerator over the values of this key.
        /// </summary>
        public ValueGroup.Enumerator GetEnumerator() => new ValueGroup.Enumerator(_group);

        IEnumerator<TValue?> IEnumerable<TValue?>.GetEnumerator()
            => new ValueGroup.Enumerator(_group);

        IEnumerator IEnumerable.GetEnumerator()
            => new ValueGroup.Enumerator(_group);
    }

    /// <summary>
    /// A struct enumerator over a <see cref="CelerityMultiMap{TKey, TValue, THasher}"/>
    /// that yields one <see cref="Grouping"/> per distinct key. Because it is a
    /// struct, iterating it via <c>foreach</c> avoids the allocation a
    /// compiler-generated <see cref="IEnumerator{T}"/> would incur. The
    /// out-of-band default-key group (if present) is yielded first.
    /// </summary>
    public struct Enumerator : IEnumerator<IGrouping<TKey, TValue?>>
    {
        private readonly CelerityMultiMap<TKey, TValue, THasher> _map;
        private readonly int _version;
        private int _index;
        private Grouping _current;
        private State _state;

        private enum State : byte
        {
            BeforeDefaultKey,
            InArray,
            Done
        }

        internal Enumerator(CelerityMultiMap<TKey, TValue, THasher> map)
        {
            _map = map;
            _version = map._version;
            _index = -1;
            _current = default;
            _state = State.BeforeDefaultKey;
        }

        /// <summary>Gets the grouping at the current position of the enumerator.</summary>
        public Grouping Current => _current;

        IGrouping<TKey, TValue?> IEnumerator<IGrouping<TKey, TValue?>>.Current => _current;

        object IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next grouping.</summary>
        /// <returns>
        /// <c>true</c> if the enumerator advanced to a new grouping; <c>false</c>
        /// if it has passed the end of the map.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the map was modified since the enumerator was created.
        /// </exception>
        public bool MoveNext()
        {
            if (_version != _map._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            if (_state == State.BeforeDefaultKey)
            {
                _state = State.InArray;
                if (_map._hasDefaultKey)
                {
                    _current = new Grouping(default(TKey)!, _map._defaultKeyGroup);
                    return true;
                }
            }

            if (_state == State.InArray)
            {
                TKey?[] keys = _map._keys;
                List<TValue?>?[] groups = _map._groups;
                int length = keys.Length;
                ref TKey? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
                var comparer = EqualityComparer<TKey>.Default;
                while (++_index < length)
                {
                    TKey? key = Unsafe.Add(ref keysRef, (nint)(uint)_index);
                    if (!comparer.Equals(key, default(TKey)))
                    {
                        _current = new Grouping(key!, groups[_index]);
                        return true;
                    }
                }
                _state = State.Done;
            }

            _current = default;
            return false;
        }

        /// <summary>Resets the enumerator to its initial position, before the first grouping.</summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the map was modified since the enumerator was created.
        /// </exception>
        public void Reset()
        {
            if (_version != _map._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _index = -1;
            _current = default;
            _state = State.BeforeDefaultKey;
        }

        /// <summary>Releases any resources held by the enumerator. No-op for this type.</summary>
        public void Dispose() { }
    }

    /// <summary>
    /// A struct enumerable view over the distinct keys of a
    /// <see cref="CelerityMultiMap{TKey, TValue, THasher}"/>. Iterating it does not
    /// allocate; passing it through <see cref="IEnumerable{T}"/> will box the
    /// enumerator and is therefore not zero-allocation.
    /// </summary>
    public readonly struct KeyCollection : IEnumerable<TKey>
    {
        private readonly CelerityMultiMap<TKey, TValue, THasher> _map;

        internal KeyCollection(CelerityMultiMap<TKey, TValue, THasher> map) => _map = map;

        /// <summary>
        /// Gets the number of distinct keys in the view (equal to the map's <see cref="Count"/>).
        /// </summary>
        public int Count => _map._count;

        /// <summary>Returns an allocation-free struct enumerator over the keys.</summary>
        public Enumerator GetEnumerator() => new Enumerator(_map);

        IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => new Enumerator(_map);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_map);

        /// <summary>
        /// A struct enumerator over the distinct keys of a
        /// <see cref="CelerityMultiMap{TKey, TValue, THasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TKey>
        {
            private CelerityMultiMap<TKey, TValue, THasher>.Enumerator _inner;

            internal Enumerator(CelerityMultiMap<TKey, TValue, THasher> map) => _inner = map.GetEnumerator();

            /// <summary>Gets the current key.</summary>
            public TKey Current => _inner.Current.Key;

            object? IEnumerator.Current => _inner.Current.Key;

            /// <summary>Advances to the next key.</summary>
            public bool MoveNext() => _inner.MoveNext();

            /// <summary>Resets the enumerator to its initial position.</summary>
            public void Reset() => _inner.Reset();

            /// <summary>No-op.</summary>
            public void Dispose() => _inner.Dispose();
        }
    }
}
