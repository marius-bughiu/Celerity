using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Celerity.Collections;

/// <summary>
/// A dictionary optimized for the very-small case (<c>n &lt;= ~16</c>), where a
/// linear scan over a flat backing array beats a probe-based hash table. Used
/// heavily in compilers, IL emitters, AST attribute bags, and per-request maps
/// where most instances stay tiny.
/// </summary>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TValue">The type of the values.</typeparam>
/// <remarks>
/// <para>
/// Unlike the open-addressed Celerity dictionaries, <see cref="SmallDictionary{TKey, TValue}"/>
/// stores entries in insertion-dense parallel arrays and answers every query with
/// a linear scan using <see cref="EqualityComparer{T}.Default"/>. For small key
/// counts this is faster than hashing: there is no hash to compute, no modulo /
/// mask, and the whole key array fits in a cache line or two, so the branch
/// predictor and prefetcher do the rest. The trade-off is that lookups are
/// <c>O(n)</c> rather than <c>O(1)</c>, so the type degrades for large key sets —
/// keep it to the small-<c>n</c> workloads it is built for (see the choosing-a-collection
/// guidance in the README). It does <em>not</em> auto-promote to a hash table; it
/// simply grows its arrays and keeps scanning.
/// </para>
/// <para>
/// Because lookups never hash, there is no empty-slot sentinel and therefore no
/// special-casing of <c>default(TKey)</c>: a <c>0</c>, <c>null</c>, or
/// <see cref="System.Guid.Empty"/> key is stored inline like any other. This is a
/// small simplification over the hash-table dictionaries, which keep the default
/// key out-of-band.
/// </para>
/// <para>
/// The type is single-threaded and does not guarantee enumeration order; in
/// particular <see cref="Remove(TKey)"/> moves the last entry into the vacated
/// slot, so the order after a removal is unspecified.
/// </para>
/// </remarks>
public class SmallDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue?>
{
    /// <summary>
    /// The capacity the dictionary grows to on its first insert when constructed
    /// with no (or a zero) capacity hint.
    /// </summary>
    protected const int DEFAULT_CAPACITY = 4;

    private TKey?[] _keys;
    private TValue?[] _values;
    private int _count;

    // Incremented on every structural mutation so active enumerators can
    // detect concurrent modification and throw, matching BCL semantics.
    private int _version;

    /// <summary>
    /// Initializes a new, empty <see cref="SmallDictionary{TKey, TValue}"/> with
    /// the specified initial capacity.
    /// </summary>
    /// <param name="capacity">
    /// The number of entries the backing arrays are sized for up front. Unlike the
    /// hash-table dictionaries this is used verbatim (it is not rounded to a power
    /// of two), since there is no probe mask. A value of <c>0</c> defers allocation
    /// until the first insert.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative.
    /// </exception>
    public SmallDictionary(int capacity = DEFAULT_CAPACITY)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        _keys = capacity == 0 ? Array.Empty<TKey?>() : new TKey?[capacity];
        _values = capacity == 0 ? Array.Empty<TValue?>() : new TValue?[capacity];
    }

    /// <summary>
    /// Initializes a new <see cref="SmallDictionary{TKey, TValue}"/> that contains
    /// the key/value pairs copied from the specified <paramref name="source"/>.
    /// </summary>
    /// <param name="source">
    /// The collection whose key/value pairs are copied into the new dictionary.
    /// If <paramref name="source"/> implements <see cref="ICollection{T}"/>, its
    /// <c>Count</c> is used to size the backing storage so inserts do not resize.
    /// </param>
    /// <param name="capacity">
    /// The minimum initial capacity. The final capacity is the larger of this
    /// value and the source's count.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> contains one or more duplicate keys.
    /// </exception>
    public SmallDictionary(
        IEnumerable<KeyValuePair<TKey, TValue>> source,
        int capacity = DEFAULT_CAPACITY)
        : this(InitialCapacityForSource(source, capacity))
    {
        foreach (KeyValuePair<TKey, TValue> kvp in source)
        {
            Add(kvp.Key, kvp.Value);
        }
    }

    // Runs as part of the chained-ctor argument expression so the null check
    // beats the primary ctor's capacity validation: a null source must surface
    // as ArgumentNullException, not ArgumentOutOfRangeException, even when the
    // caller also passed a negative capacity.
    private static int InitialCapacityForSource(
        IEnumerable<KeyValuePair<TKey, TValue>> source,
        int capacity)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Math.Max(capacity, (source as ICollection<KeyValuePair<TKey, TValue>>)?.Count ?? 0);
    }

    /// <summary>
    /// Gets the number of key/value pairs contained in the dictionary.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// Throws a <see cref="KeyNotFoundException"/> if the key does not exist on get.
    /// </summary>
    /// <param name="key">The key of the value to get or set.</param>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when attempting to get a value for a key that is not found in the dictionary.
    /// </exception>
    /// <returns>The value associated with the specified key.</returns>
    public TValue this[TKey key]
    {
        get
        {
            int index = IndexOfKey(key);
            if (index < 0)
                throw new KeyNotFoundException($"Key {key} not found.");

            return _values[index]!;
        }
        set
        {
            int index = IndexOfKey(key);
            if (index >= 0)
            {
                _values[index] = value;
                _version++;
                return;
            }

            Append(key, value);
        }
    }

    /// <summary>
    /// Determines whether the specified key is present in the dictionary.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns><c>true</c> if the key is found; otherwise, <c>false</c>.</returns>
    public bool ContainsKey(TKey key) => IndexOfKey(key) >= 0;

    /// <summary>
    /// Determines whether the dictionary contains the specified value.
    /// </summary>
    /// <param name="value">
    /// The value to locate. Equality is determined via
    /// <see cref="EqualityComparer{T}.Default"/>, matching BCL
    /// <see cref="Dictionary{TKey, TValue}.ContainsValue(TValue)"/> semantics.
    /// </param>
    /// <returns><c>true</c> if a matching value is found; otherwise, <c>false</c>.</returns>
    /// <remarks>This operation is <c>O(n)</c> in the dictionary's count.</remarks>
    public bool ContainsValue(TValue? value)
    {
        var comparer = EqualityComparer<TValue?>.Default;
        TValue?[] values = _values;
        ref TValue? valuesRef = ref MemoryMarshal.GetArrayDataReference(values);
        int count = _count;
        for (int i = 0; i < count; i++)
        {
            if (comparer.Equals(Unsafe.Add(ref valuesRef, (nint)(uint)i), value))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">
    /// When this method returns, contains the value associated with <paramref name="key"/>
    /// if found; otherwise the default value of <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><c>true</c> if the key was found; otherwise, <c>false</c>.</returns>
    public bool TryGetValue(TKey key, out TValue? value)
    {
        int index = IndexOfKey(key);
        if (index < 0)
        {
            value = default;
            return false;
        }

        value = _values[index];
        return true;
    }

    /// <summary>
    /// Adds the specified key and value to the dictionary.
    /// Throws <see cref="ArgumentException"/> if the key already exists.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when an element with the same <paramref name="key"/> already exists.
    /// </exception>
    public void Add(TKey key, TValue value)
    {
        if (!TryAdd(key, value))
            throw new ArgumentException($"An element with key {key} already exists.", nameof(key));
    }

    /// <summary>
    /// Attempts to add the specified key and value to the dictionary.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <returns>
    /// <c>true</c> if the key/value pair was added successfully;
    /// <c>false</c> if the key already exists (the dictionary is unchanged).
    /// </returns>
    public bool TryAdd(TKey key, TValue value)
    {
        if (IndexOfKey(key) >= 0)
            return false;

        Append(key, value);
        return true;
    }

    /// <summary>
    /// Removes the value with the specified key from the dictionary.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <returns>
    /// <c>true</c> if the element was successfully removed; otherwise, <c>false</c>.
    /// Also returns <c>false</c> if the key was not found.
    /// </returns>
    public bool Remove(TKey key) => Remove(key, out _);

    /// <summary>
    /// Removes the value with the specified key from the dictionary and copies
    /// the removed value to the <paramref name="value"/> parameter.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <param name="value">
    /// When this method returns, contains the value that was associated with
    /// <paramref name="key"/> before removal if the key was found; otherwise the
    /// default value of <typeparamref name="TValue"/>.
    /// </param>
    /// <returns>
    /// <c>true</c> if the element was successfully removed; otherwise, <c>false</c>.
    /// Also returns <c>false</c> if the key was not found.
    /// </returns>
    /// <remarks>
    /// Removal moves the last entry into the vacated slot (an <c>O(1)</c> swap once
    /// the key is found), so the relative order of the surviving entries is not
    /// preserved.
    /// </remarks>
    public bool Remove(TKey key, out TValue? value)
    {
        int index = IndexOfKey(key);
        if (index < 0)
        {
            value = default;
            return false;
        }

        value = _values[index];

        int last = _count - 1;
        _keys[index] = _keys[last];
        _values[index] = _values[last];
        _keys[last] = default;
        _values[last] = default;
        _count--;
        _version++;
        return true;
    }

    /// <summary>
    /// Removes all keys and values from the dictionary. The underlying
    /// capacity is preserved.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        Array.Clear(_keys, 0, _count);
        Array.Clear(_values, 0, _count);
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Ensures that the dictionary's backing arrays can hold at least <paramref name="capacity"/>
    /// entries without growing, enlarging them in a single copy if they are currently smaller.
    /// Pre-sizing before a bulk insert of a known size avoids the incremental array doublings an
    /// unsized dictionary would otherwise pay. The dictionary is never shrunk by this call.
    /// </summary>
    /// <param name="capacity">The minimum number of entries the backing arrays must hold.</param>
    /// <returns>The capacity (backing-array length) the dictionary can now hold before it grows.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public int EnsureCapacity(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        if (capacity > _keys.Length)
        {
            // Sized verbatim, mirroring the constructor: SmallDictionary has no probe mask, so the
            // backing arrays need no power-of-two length.
            Array.Resize(ref _keys, capacity);
            Array.Resize(ref _values, capacity);
            _version++;
        }

        return _keys.Length;
    }

    /// <summary>
    /// Reduces the backing arrays to exactly the current <see cref="Count"/>, reclaiming memory after
    /// the dictionary has shrunk.
    /// </summary>
    public void TrimExcess() => TrimExcess(_count);

    /// <summary>
    /// Reduces (or grows) the backing arrays to hold exactly <paramref name="capacity"/> entries.
    /// </summary>
    /// <param name="capacity">
    /// The number of entries to size the arrays for. Must be at least the current <see cref="Count"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is less than the current <see cref="Count"/>.
    /// </exception>
    public void TrimExcess(int capacity)
    {
        if (capacity < _count)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least the current Count.");

        if (capacity != _keys.Length)
        {
            Array.Resize(ref _keys, capacity);
            Array.Resize(ref _values, capacity);
            _version++;
        }
    }

    /// <summary>
    /// Returns an allocation-free enumerator that yields each key/value pair
    /// stored in the dictionary. The enumeration order is unspecified and may
    /// change across versions; do not rely on it. If the dictionary is modified
    /// during enumeration, <see cref="Enumerator.MoveNext"/> throws
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over this dictionary.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    /// <summary>
    /// Gets an enumerable view over the keys in the dictionary. The view is a
    /// lightweight struct and iterating it does not allocate.
    /// </summary>
    public KeyCollection Keys => new KeyCollection(this);

    /// <summary>
    /// Gets an enumerable view over the values in the dictionary. The view is a
    /// lightweight struct and iterating it does not allocate.
    /// </summary>
    public ValueCollection Values => new ValueCollection(this);

    // Appends a known-new key/value pair, growing the backing arrays first when
    // they are full. Callers must have already confirmed the key is absent.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Append(TKey key, TValue? value)
    {
        if (_count == _keys.Length)
            Grow();

        _keys[_count] = key;
        _values[_count] = value;
        _count++;
        _version++;
    }

    private void Grow()
    {
        int newCapacity = _keys.Length == 0 ? DEFAULT_CAPACITY : FastUtils.DoubleCapacity(_keys.Length);
        Array.Resize(ref _keys, newCapacity);
        Array.Resize(ref _values, newCapacity);
    }

    // Linear scan over the dense [0, _count) prefix. The scan walks _keys via
    // Unsafe.Add against a single base reference grabbed at the top, so the
    // per-iteration bounds check disappears; i < _count keeps the index in range.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int IndexOfKey(TKey key)
    {
        TKey?[] keys = _keys;
        ref TKey? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        var comparer = EqualityComparer<TKey>.Default;
        int count = _count;
        for (int i = 0; i < count; i++)
        {
            if (comparer.Equals(Unsafe.Add(ref keysRef, (nint)(uint)i), key))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// A struct enumerator over a <see cref="SmallDictionary{TKey, TValue}"/>.
    /// Because it is a struct, iterating it via <c>foreach</c> avoids the
    /// allocation that a compiler-generated <c>IEnumerator&lt;T&gt;</c> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue?>>
    {
        private readonly SmallDictionary<TKey, TValue> _dict;
        private readonly int _version;
        private int _index;
        private KeyValuePair<TKey, TValue?> _current;

        internal Enumerator(SmallDictionary<TKey, TValue> dict)
        {
            _dict = dict;
            _version = dict._version;
            _index = -1;
            _current = default;
        }

        /// <summary>
        /// Gets the key/value pair at the current position of the enumerator.
        /// </summary>
        public KeyValuePair<TKey, TValue?> Current => _current;

        object IEnumerator.Current => _current;

        /// <summary>
        /// Advances the enumerator to the next key/value pair.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the enumerator advanced to a new entry; <c>false</c>
        /// if it has passed the end of the dictionary.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the dictionary was modified since the enumerator was created.
        /// </exception>
        public bool MoveNext()
        {
            if (_version != _dict._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            SmallDictionary<TKey, TValue> dict = _dict;
            if (++_index < dict._count)
            {
                _current = new KeyValuePair<TKey, TValue?>(dict._keys[_index]!, dict._values[_index]);
                return true;
            }

            _current = default;
            return false;
        }

        /// <summary>
        /// Resets the enumerator to its initial position, before the first entry.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the dictionary was modified since the enumerator was created.
        /// </exception>
        public void Reset()
        {
            if (_version != _dict._version)
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
    /// A struct enumerable view over the keys of a
    /// <see cref="SmallDictionary{TKey, TValue}"/>. Iterating it does not allocate;
    /// passing it through <see cref="IEnumerable{T}"/> will box the enumerator and
    /// is therefore not zero-allocation.
    /// </summary>
    public readonly struct KeyCollection : IEnumerable<TKey>
    {
        private readonly SmallDictionary<TKey, TValue> _dict;

        internal KeyCollection(SmallDictionary<TKey, TValue> dict) => _dict = dict;

        /// <summary>
        /// Gets the number of keys in the view (equal to the dictionary's count).
        /// </summary>
        public int Count => _dict._count;

        /// <summary>
        /// Returns an allocation-free struct enumerator over the keys.
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(_dict);

        IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => new Enumerator(_dict);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_dict);

        /// <summary>
        /// A struct enumerator over the keys of a
        /// <see cref="SmallDictionary{TKey, TValue}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TKey>
        {
            private SmallDictionary<TKey, TValue>.Enumerator _inner;

            internal Enumerator(SmallDictionary<TKey, TValue> dict) => _inner = dict.GetEnumerator();

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

    /// <summary>
    /// A struct enumerable view over the values of a
    /// <see cref="SmallDictionary{TKey, TValue}"/>. Iterating it does not allocate;
    /// passing it through <see cref="IEnumerable{T}"/> will box the enumerator and
    /// is therefore not zero-allocation.
    /// </summary>
    public readonly struct ValueCollection : IEnumerable<TValue?>
    {
        private readonly SmallDictionary<TKey, TValue> _dict;

        internal ValueCollection(SmallDictionary<TKey, TValue> dict) => _dict = dict;

        /// <summary>
        /// Gets the number of values in the view (equal to the dictionary's count).
        /// </summary>
        public int Count => _dict._count;

        /// <summary>
        /// Returns an allocation-free struct enumerator over the values.
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(_dict);

        IEnumerator<TValue?> IEnumerable<TValue?>.GetEnumerator() => new Enumerator(_dict);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_dict);

        /// <summary>
        /// A struct enumerator over the values of a
        /// <see cref="SmallDictionary{TKey, TValue}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TValue?>
        {
            private SmallDictionary<TKey, TValue>.Enumerator _inner;

            internal Enumerator(SmallDictionary<TKey, TValue> dict) => _inner = dict.GetEnumerator();

            /// <summary>Gets the current value.</summary>
            public TValue? Current => _inner.Current.Value;

            object? IEnumerator.Current => _inner.Current.Value;

            /// <summary>Advances to the next value.</summary>
            public bool MoveNext() => _inner.MoveNext();

            /// <summary>Resets the enumerator to its initial position.</summary>
            public void Reset() => _inner.Reset();

            /// <summary>No-op.</summary>
            public void Dispose() => _inner.Dispose();
        }
    }

    // IReadOnlyDictionary<TKey, TValue?> explicit interface members. The primary
    // (non-interface) surface — the indexer, ContainsKey, TryGetValue, Count, the
    // struct enumerator, and the KeyCollection / ValueCollection views — already
    // cover the interface contract. These forwarders only widen those members to
    // the boxed IEnumerable<T> / IEnumerator<T> shapes the interface requires.
    TValue? IReadOnlyDictionary<TKey, TValue?>.this[TKey key] => this[key];

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue?>.Keys => Keys;

    IEnumerable<TValue?> IReadOnlyDictionary<TKey, TValue?>.Values => Values;

    IEnumerator<KeyValuePair<TKey, TValue?>> IEnumerable<KeyValuePair<TKey, TValue?>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
