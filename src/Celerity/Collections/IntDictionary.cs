using System.Collections;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A high-performance dictionary keyed by <see cref="int"/>, using
/// <see cref="Int32WangNaiveHasher"/> by default.
/// </summary>
/// <typeparam name="TValue">The type of the stored values.</typeparam>
public class IntDictionary<TValue> : IntDictionary<TValue, Int32WangNaiveHasher>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IntDictionary{TValue}"/> class
    /// with an optional capacity and load factor.
    /// </summary>
    /// <param name="capacity">
    /// The initial capacity of the dictionary. Automatically rounded up
    /// to the next power of two.
    /// </param>
    /// <param name="loadFactor">
    /// Determines the maximum ratio of count to capacity before resizing.
    /// </param>
    public IntDictionary(int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
        : base(capacity, loadFactor)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntDictionary{TValue}"/> class
    /// that contains the key/value pairs copied from the specified
    /// <paramref name="source"/>.
    /// </summary>
    /// <param name="source">
    /// The collection whose key/value pairs are copied into the new dictionary.
    /// If <paramref name="source"/> implements <see cref="ICollection{T}"/>, its
    /// <c>Count</c> is used to size the backing storage so inserts do not resize.
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
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> contains one or more duplicate keys.
    /// </exception>
    public IntDictionary(
        IEnumerable<KeyValuePair<int, TValue>> source,
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
        : base(source, capacity, loadFactor)
    {
    }
}

/// <summary>
/// A high-performance dictionary keyed by <see cref="int"/>, parameterized on a
/// custom <see cref="IHashProvider{T}"/> implementation.
/// </summary>
/// <typeparam name="TValue">The type of the stored values.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute key hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class IntDictionary<TValue, THasher>
    : IReadOnlyDictionary<int, TValue?>
    where THasher : struct, IHashProvider<int>
{
    /// <summary>
    /// The default initial capacity of the dictionary if no capacity is specified.
    /// </summary>
    protected const int DEFAULT_CAPACITY = 16;

    /// <summary>
    /// The default load factor of the dictionary if no load factor is specified.
    /// </summary>
    protected const float DEFAULT_LOAD_FACTOR = 0.75f;

    private const int EMPTY_KEY = 0;
    private static readonly TValue? EMPTY_VALUE = default;

    private int _count = 0;
    private int[] _keys;
    private TValue?[] _values;
    private readonly float _loadFactor;
    private int _threshold;
    private readonly THasher _hasher;

    // The key value 0 collides with EMPTY_KEY, so it's stored out-of-band
    // in a dedicated slot. _count includes this entry when _hasZeroKey is true.
    private bool _hasZeroKey;
    private TValue? _zeroValue;

    // Incremented on every structural mutation so active enumerators can
    // detect concurrent modification and throw, matching BCL semantics.
    private int _version;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntDictionary{TValue, THasher}"/> class
    /// using the specified capacity and load factor.
    /// </summary>
    /// <param name="capacity">
    /// The initial capacity of the dictionary. Automatically rounded up
    /// to the next power of two.
    /// </param>
    /// <param name="loadFactor">
    /// Determines the maximum ratio of count to capacity before resizing.
    /// </param>
    public IntDictionary(
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (loadFactor <= 0f || loadFactor >= 1f)
            throw new ArgumentOutOfRangeException(nameof(loadFactor), loadFactor, "Load factor must be between 0 (exclusive) and 1 (exclusive).");

        int size = FastUtils.NextPowerOfTwo(capacity);

        _keys = new int[size];
        _values = new TValue?[size];
        _loadFactor = loadFactor;
        _threshold = (int)(size * _loadFactor);
        _hasher = default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntDictionary{TValue, THasher}"/>
    /// class that contains the key/value pairs copied from the specified
    /// <paramref name="source"/>.
    /// </summary>
    /// <param name="source">
    /// The collection whose key/value pairs are copied into the new dictionary.
    /// If <paramref name="source"/> implements <see cref="ICollection{T}"/>, its
    /// <c>Count</c> is used to size the backing storage so inserts do not resize.
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
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> contains one or more duplicate keys.
    /// </exception>
    public IntDictionary(
        IEnumerable<KeyValuePair<int, TValue>> source,
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
        : this(InitialCapacityForSource(source, capacity), loadFactor)
    {
        foreach (KeyValuePair<int, TValue> kvp in source)
        {
            Add(kvp.Key, kvp.Value);
        }
    }

    // Runs as part of the chained-ctor argument expression so the null check
    // beats the primary ctor's capacity / loadFactor validation: a null source
    // must surface as ArgumentNullException, not ArgumentOutOfRangeException
    // when the user also passed an invalid loadFactor.
    private static int InitialCapacityForSource(
        IEnumerable<KeyValuePair<int, TValue>> source,
        int capacity)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Math.Max(capacity, (source as ICollection<KeyValuePair<int, TValue>>)?.Count ?? 0);
    }

    /// <summary>
    /// Gets the number of entries currently stored in the dictionary.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// Throws <see cref="KeyNotFoundException"/> if the key is not present on get.
    /// </summary>
    /// <param name="key">The key whose value to get or set.</param>
    /// <exception cref="KeyNotFoundException">Thrown if the key does not exist.</exception>
    /// <returns>The value associated with the specified key.</returns>
    public TValue this[int key]
    {
        get
        {
            if (key == EMPTY_KEY)
            {
                if (_hasZeroKey)
                    return _zeroValue!;
                throw new KeyNotFoundException($"Key {key} not found.");
            }

            int index = ProbeForKey(key);
            if (index < 0)
                throw new KeyNotFoundException($"Key {key} not found.");

            return _values[index]!;
        }
        set
        {
            if (key == EMPTY_KEY)
            {
                if (!_hasZeroKey)
                {
                    _hasZeroKey = true;
                    _count++;
                }
                _zeroValue = value;
                _version++;
                return;
            }

            if (_count >= _threshold)
            {
                Resize();
            }

            int index = ProbeForInsert(key);
            bool isNewEntry = _keys[index] == EMPTY_KEY;

            _keys[index] = key;
            _values[index] = value;

            if (isNewEntry)
                _count++;
            _version++;
        }
    }

    /// <summary>
    /// Determines whether the specified key is present in the dictionary.
    /// </summary>
    /// <param name="key">The key to locate in the dictionary.</param>
    /// <returns><c>true</c> if the key is found; otherwise, <c>false</c>.</returns>
    public bool ContainsKey(int key)
    {
        if (key == EMPTY_KEY)
            return _hasZeroKey;

        return ProbeForKey(key) >= 0;
    }

    /// <summary>
    /// Determines whether the dictionary contains the specified value.
    /// </summary>
    /// <param name="value">
    /// The value to locate. Equality is determined via
    /// <see cref="EqualityComparer{T}.Default"/>, matching BCL
    /// <see cref="Dictionary{TKey, TValue}.ContainsValue(TValue)"/> semantics.
    /// </param>
    /// <returns><c>true</c> if a matching value is found; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This operation is <c>O(n)</c> in the dictionary's count: it scans the
    /// probe table (skipping empty slots) and, when present, the out-of-band
    /// zero-key slot.
    /// </remarks>
    public bool ContainsValue(TValue? value)
    {
        var comparer = EqualityComparer<TValue?>.Default;

        if (_hasZeroKey && comparer.Equals(_zeroValue, value))
            return true;

        int[] keys = _keys;
        TValue?[] values = _values;
        for (int i = 0; i < keys.Length; i++)
        {
            if (keys[i] != EMPTY_KEY && comparer.Equals(values[i], value))
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
    public bool TryGetValue(int key, out TValue? value)
    {
        if (key == EMPTY_KEY)
        {
            if (_hasZeroKey)
            {
                value = _zeroValue;
                return true;
            }
            value = default;
            return false;
        }

        int index = ProbeForKey(key);
        if (index < 0)
        {
            value = default;
            return false;
        }

        value = _values[index];
        return true;
    }

    /// <summary>
    /// Removes the value with the specified key from the dictionary.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <returns>
    /// <c>true</c> if the item was successfully removed; otherwise, <c>false</c>.
    /// This method also returns <c>false</c> if <paramref name="key"/> was not found.
    /// </returns>
    public bool Remove(int key) => Remove(key, out _);

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
    /// <c>true</c> if the item was successfully removed; otherwise, <c>false</c>.
    /// This method also returns <c>false</c> if <paramref name="key"/> was not found.
    /// </returns>
    public bool Remove(int key, out TValue? value)
    {
        if (key == EMPTY_KEY)
        {
            if (!_hasZeroKey)
            {
                value = default;
                return false;
            }
            value = _zeroValue;
            _hasZeroKey = false;
            _zeroValue = EMPTY_VALUE;
            _count--;
            _version++;
            return true;
        }

        int index = ProbeForKey(key);
        if (index < 0)
        {
            value = default;
            return false;
        }

        value = _values[index];
        _keys[index] = EMPTY_KEY;
        _values[index] = EMPTY_VALUE;
        _count--;

        RehashAfterRemove(index);
        _version++;
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
    public void Add(int key, TValue value)
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
    public bool TryAdd(int key, TValue value)
    {
        if (key == EMPTY_KEY)
        {
            if (_hasZeroKey)
                return false;
            _hasZeroKey = true;
            _zeroValue = value;
            _count++;
            _version++;
            return true;
        }

        // Single probe: ProbeForInsert returns either the slot of an existing
        // entry or the first empty slot in the chain. If it's the former, the
        // key already exists; otherwise we insert here directly. This avoids
        // the double probe-chain walk that `if (ContainsKey(key)) ...; this[key] = value;`
        // would do.
        if (_count >= _threshold)
            Resize();

        int index = ProbeForInsert(key);
        if (_keys[index] != EMPTY_KEY)
            return false;

        _keys[index] = key;
        _values[index] = value;
        _count++;
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

        Array.Clear(_keys, 0, _keys.Length);
        Array.Clear(_values, 0, _values.Length);
        _hasZeroKey = false;
        _zeroValue = EMPTY_VALUE;
        _count = 0;
        _version++;
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

    /// <summary>
    /// A struct enumerator over an <see cref="IntDictionary{TValue, THasher}"/>.
    /// Because it is a struct, iterating it via <c>foreach</c> avoids the
    /// allocation that a compiler-generated <c>IEnumerator&lt;T&gt;</c> would
    /// incur. The out-of-band zero-key entry (if present) is yielded first.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<int, TValue?>>
    {
        private readonly IntDictionary<TValue, THasher> _dict;
        private readonly int _version;
        private int _index;
        private KeyValuePair<int, TValue?> _current;
        private State _state;

        private enum State : byte
        {
            BeforeZeroKey,
            InArray,
            Done
        }

        internal Enumerator(IntDictionary<TValue, THasher> dict)
        {
            _dict = dict;
            _version = dict._version;
            _index = -1;
            _current = default;
            _state = State.BeforeZeroKey;
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

            if (_state == State.BeforeZeroKey)
            {
                _state = State.InArray;
                if (_dict._hasZeroKey)
                {
                    _current = new KeyValuePair<int, TValue?>(0, _dict._zeroValue);
                    return true;
                }
            }

            if (_state == State.InArray)
            {
                int[] keys = _dict._keys;
                TValue?[] values = _dict._values;
                while (++_index < keys.Length)
                {
                    if (keys[_index] != EMPTY_KEY)
                    {
                        _current = new KeyValuePair<int, TValue?>(keys[_index], values[_index]);
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
        /// Thrown if the dictionary was modified since the enumerator was created.
        /// </exception>
        public void Reset()
        {
            if (_version != _dict._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _index = -1;
            _current = default;
            _state = State.BeforeZeroKey;
        }

        /// <summary>
        /// Releases any resources held by the enumerator. No-op for this type.
        /// </summary>
        public void Dispose() { }
    }

    /// <summary>
    /// A struct enumerable view over the keys of an
    /// <see cref="IntDictionary{TValue, THasher}"/>. Iterating it does not
    /// allocate; passing it through <see cref="IEnumerable{T}"/> will box the
    /// enumerator and is therefore not zero-allocation.
    /// </summary>
    public readonly struct KeyCollection : IEnumerable<int>
    {
        private readonly IntDictionary<TValue, THasher> _dict;

        internal KeyCollection(IntDictionary<TValue, THasher> dict) => _dict = dict;

        /// <summary>
        /// Gets the number of keys in the view (equal to the dictionary's count).
        /// </summary>
        public int Count => _dict._count;

        /// <summary>
        /// Returns an allocation-free struct enumerator over the keys.
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(_dict);

        IEnumerator<int> IEnumerable<int>.GetEnumerator() => new Enumerator(_dict);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_dict);

        /// <summary>
        /// A struct enumerator over the keys of an
        /// <see cref="IntDictionary{TValue, THasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<int>
        {
            private IntDictionary<TValue, THasher>.Enumerator _inner;

            internal Enumerator(IntDictionary<TValue, THasher> dict) => _inner = dict.GetEnumerator();

            /// <summary>Gets the current key.</summary>
            public int Current => _inner.Current.Key;

            object IEnumerator.Current => _inner.Current.Key;

            /// <summary>Advances to the next key.</summary>
            public bool MoveNext() => _inner.MoveNext();

            /// <summary>Resets the enumerator to its initial position.</summary>
            public void Reset() => _inner.Reset();

            /// <summary>No-op.</summary>
            public void Dispose() => _inner.Dispose();
        }
    }

    /// <summary>
    /// A struct enumerable view over the values of an
    /// <see cref="IntDictionary{TValue, THasher}"/>. Iterating it does not
    /// allocate; passing it through <see cref="IEnumerable{T}"/> will box the
    /// enumerator and is therefore not zero-allocation.
    /// </summary>
    public readonly struct ValueCollection : IEnumerable<TValue?>
    {
        private readonly IntDictionary<TValue, THasher> _dict;

        internal ValueCollection(IntDictionary<TValue, THasher> dict) => _dict = dict;

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
        /// A struct enumerator over the values of an
        /// <see cref="IntDictionary{TValue, THasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TValue?>
        {
            private IntDictionary<TValue, THasher>.Enumerator _inner;

            internal Enumerator(IntDictionary<TValue, THasher> dict) => _inner = dict.GetEnumerator();

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

    // IReadOnlyDictionary<int, TValue?> explicit interface members. The primary
    // (non-interface) surface — the indexer, ContainsKey, TryGetValue, Count, the
    // struct enumerator, and the KeyCollection / ValueCollection views — already
    // cover the interface contract. These forwarders only widen those members to
    // the boxed IEnumerable<T> / IEnumerator<T> shapes the interface requires, so
    // users who prefer BCL ergonomics (e.g. consuming the dictionary as
    // `IReadOnlyDictionary<int, TValue?>` via LINQ or dependency injection) can do
    // so without losing the zero-allocation fast path for direct foreach.
    TValue? IReadOnlyDictionary<int, TValue?>.this[int key] => this[key];

    IEnumerable<int> IReadOnlyDictionary<int, TValue?>.Keys => Keys;

    IEnumerable<TValue?> IReadOnlyDictionary<int, TValue?>.Values => Values;

    IEnumerator<KeyValuePair<int, TValue?>> IEnumerable<KeyValuePair<int, TValue?>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private int ProbeForInsert(int key)
    {
        int size = _keys.Length;

        // Only works when size is a power of two
        int index = _hasher.Hash(key) & (size - 1);

        while (_keys[index] != EMPTY_KEY && _keys[index] != key)
        {
            index = (index + 1) & (size - 1);
        }

        return index;
    }

    private int ProbeForKey(int key)
    {
        int size = _keys.Length;

        // Only works when size is a power of two
        int index = _hasher.Hash(key) & (size - 1);

        while (_keys[index] != EMPTY_KEY)
        {
            if (_keys[index] == key)
                return index;
            index = (index + 1) & (size - 1);
        }

        return -1;
    }

    private void Resize()
    {
        int newSize = _keys.Length * 2;
        int mask = newSize - 1;
        int[] oldKeys = _keys;
        TValue?[] oldValues = _values;

        // Build into fresh local arrays then swap them in at the end. The loop
        // bypasses the indexer setter on purpose: every reinserted key is known
        // to be unique in the new table (it came from the previous arrays which
        // were themselves a valid dictionary), and _count / _version are
        // conserved across a resize, so the setter's equality check, threshold
        // check, isNewEntry test, _count++, and _version++ are all dead weight.
        // The zero-key entry lives out-of-band and is not touched here.
        int[] newKeys = new int[newSize];
        TValue?[] newValues = new TValue?[newSize];

        for (int i = 0; i < oldKeys.Length; i++)
        {
            int key = oldKeys[i];
            if (key == EMPTY_KEY)
                continue;

            int index = _hasher.Hash(key) & mask;
            while (newKeys[index] != EMPTY_KEY)
                index = (index + 1) & mask;

            newKeys[index] = key;
            newValues[index] = oldValues[i];
        }

        _keys = newKeys;
        _values = newValues;
        _threshold = (int)(newSize * _loadFactor);
    }

    private void RehashAfterRemove(int startIndex)
    {
        int size = _keys.Length;
        int mask = size - 1;
        int index = (startIndex + 1) & mask;

        while (_keys[index] != EMPTY_KEY)
        {
            int rehashedKey = _keys[index];
            TValue? rehashedValue = _values[index];

            _keys[index] = EMPTY_KEY;
            _values[index] = EMPTY_VALUE;

            // Reinsert at the key's natural position. The key was just removed
            // from its old slot, so it cannot match any remaining entry — we
            // probe for an empty slot only, skipping the equality check that
            // ProbeForInsert would do. _count is a slot-shuffle invariant here
            // and the caller (Remove) bumps _version exactly once for the
            // user-visible operation, so neither is touched per rehash.
            int target = _hasher.Hash(rehashedKey) & mask;
            while (_keys[target] != EMPTY_KEY)
                target = (target + 1) & mask;

            _keys[target] = rehashedKey;
            _values[target] = rehashedValue;

            index = (index + 1) & mask;
        }
    }
}
