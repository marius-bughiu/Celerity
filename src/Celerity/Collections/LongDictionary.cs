using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A high-performance dictionary keyed by <see cref="long"/>, using
/// <see cref="Int64WangNaiveHasher"/> by default. Switch to
/// <see cref="Int64WangHasher"/> or <see cref="Int64Murmur3Hasher"/> via the
/// generic overload when keys are adversarial or clustered.
/// </summary>
/// <typeparam name="TValue">The type of the stored values.</typeparam>
public class LongDictionary<TValue> : LongDictionary<TValue, Int64WangNaiveHasher>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LongDictionary{TValue}"/> class
    /// with an optional capacity and load factor.
    /// </summary>
    /// <param name="capacity">
    /// The initial capacity of the dictionary. Automatically rounded up
    /// to the next power of two.
    /// </param>
    /// <param name="loadFactor">
    /// Determines the maximum ratio of count to capacity before resizing.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative, or <paramref name="loadFactor"/>
    /// is not in the open interval (0, 1).
    /// </exception>
    public LongDictionary(int capacity = DefaultCapacity,
        float loadFactor = DefaultLoadFactor)
        : base(capacity, loadFactor)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LongDictionary{TValue}"/> class
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
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative, or <paramref name="loadFactor"/>
    /// is not in the open interval (0, 1).
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> contains one or more duplicate keys.
    /// </exception>
    public LongDictionary(
        IEnumerable<KeyValuePair<long, TValue>> source,
        int capacity = DefaultCapacity,
        float loadFactor = DefaultLoadFactor)
        : base(source, capacity, loadFactor)
    {
    }
}

/// <summary>
/// A high-performance dictionary keyed by <see cref="long"/>, parameterized on a
/// custom <see cref="IHashProvider{T}"/> implementation.
/// </summary>
/// <typeparam name="TValue">The type of the stored values.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute key hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class LongDictionary<TValue, THasher>
    : IDictionary<long, TValue?>, IReadOnlyDictionary<long, TValue?>
    where THasher : struct, IHashProvider<long>
{
    /// <summary>
    /// The default initial capacity of the dictionary if no capacity is specified.
    /// </summary>
    protected const int DefaultCapacity = 16;

    /// <summary>
    /// The default load factor of the dictionary if no load factor is specified.
    /// </summary>
    protected const float DefaultLoadFactor = 0.75f;

    private const long EmptyKey = 0L;
    private static readonly TValue? EMPTY_VALUE = default;

    private int _count = 0;
    private long[] _keys;
    private TValue?[] _values;
    private readonly float _loadFactor;
    private int _threshold;
    private readonly THasher _hasher;

    // The key value 0 collides with EmptyKey, so it's stored out-of-band
    // in a dedicated slot. _count includes this entry when _hasZeroKey is true.
    private bool _hasZeroKey;
    private TValue? _zeroValue;

    // Incremented on every structural mutation so active enumerators can
    // detect concurrent modification and throw, matching BCL semantics.
    private int _version;

    /// <summary>
    /// Initializes a new instance of the <see cref="LongDictionary{TValue, THasher}"/> class
    /// using the specified capacity and load factor.
    /// </summary>
    /// <param name="capacity">
    /// The initial capacity of the dictionary. Automatically rounded up
    /// to the next power of two.
    /// </param>
    /// <param name="loadFactor">
    /// Determines the maximum ratio of count to capacity before resizing.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative, or <paramref name="loadFactor"/>
    /// is not in the open interval (0, 1).
    /// </exception>
    public LongDictionary(
        int capacity = DefaultCapacity,
        float loadFactor = DefaultLoadFactor)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (loadFactor <= 0f || loadFactor >= 1f)
            throw new ArgumentOutOfRangeException(nameof(loadFactor), loadFactor, "Load factor must be between 0 (exclusive) and 1 (exclusive).");

        int size = FastUtils.NextPowerOfTwo(capacity);

        _keys = new long[size];
        _values = new TValue?[size];
        _loadFactor = loadFactor;
        _threshold = (int)(size * _loadFactor);
        _hasher = default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LongDictionary{TValue, THasher}"/>
    /// class that contains the key/value pairs copied from the specified
    /// <paramref name="source"/>.
    /// </summary>
    /// <param name="source">
    /// The collection whose key/value pairs are copied into the new dictionary.
    /// If <paramref name="source"/> implements <see cref="ICollection{T}"/>, its
    /// <c>Count</c> is used to size the backing storage so inserts do not resize.
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
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative, or <paramref name="loadFactor"/>
    /// is not in the open interval (0, 1).
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> contains one or more duplicate keys.
    /// </exception>
    public LongDictionary(
        IEnumerable<KeyValuePair<long, TValue>> source,
        int capacity = DefaultCapacity,
        float loadFactor = DefaultLoadFactor)
        : this(InitialCapacityForSource(source, capacity, loadFactor), loadFactor)
    {
        foreach (KeyValuePair<long, TValue> kvp in source)
        {
            Add(kvp.Key, kvp.Value);
        }
    }

    // Runs as part of the chained-ctor argument expression so the null check
    // beats the primary ctor's capacity / loadFactor validation: a null source
    // must surface as ArgumentNullException, not ArgumentOutOfRangeException
    // when the user also passed an invalid loadFactor.
    private static int InitialCapacityForSource(
        IEnumerable<KeyValuePair<long, TValue>> source,
        int capacity,
        float loadFactor)
    {
        ArgumentNullException.ThrowIfNull(source);
        int count = (source as ICollection<KeyValuePair<long, TValue>>)?.Count ?? 0;

        // Size for the source count *including* load-factor headroom: the resize
        // threshold is size*loadFactor, so a table sized to the raw count would
        // still rehash on the last inserts of the bulk fill. Scaling the count up
        // by 1/loadFactor makes the "Count is used to size the backing storage so
        // inserts do not resize" contract actually hold (issue #27). A
        // non-collection source (count 0) or an out-of-range loadFactor — left for
        // the primary ctor to reject, so null-source-beats-bad-loadFactor ordering
        // is preserved — falls through to the plain capacity.
        if (count > 0 && loadFactor > 0f && loadFactor < 1f)
        {
            int withHeadroom = (int)Math.Ceiling(count / (double)loadFactor);
            if (withHeadroom > count)
                count = withHeadroom;
        }

        return Math.Max(capacity, count);
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
    public TValue this[long key]
    {
        get
        {
            if (key == EmptyKey)
            {
                if (_hasZeroKey)
                    return _zeroValue!;
                throw new KeyNotFoundException($"Key {key} not found.");
            }

            int index = ProbeForKey(key);
            if (index < 0)
                throw new KeyNotFoundException($"Key {key} not found.");

            return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_values), (nint)(uint)index)!;
        }
        set
        {
            if (key == EmptyKey)
            {
                if (!_hasZeroKey)
                {
                    _hasZeroKey = true;
                    _count++;
                    _version++;
                }
                _zeroValue = value;
                return;
            }

            // Probe before the threshold check so a pure overwrite of an
            // existing key never resizes: only a new entry can push Count over
            // the threshold. (Mirrors the TryAdd ordering fixed in #92, which
            // this parallel path had missed.) On a new-key-at-threshold insert
            // we resize and re-probe in the doubled table.
            int index = ProbeForInsert(key, out bool isNewEntry);
            if (isNewEntry && _count >= _threshold)
            {
                Resize();
                index = ProbeForInsert(key, out _);
            }

            long[] keys = _keys;
            TValue?[] values = _values;
            Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(keys), (nint)(uint)index) = key;
            Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(values), (nint)(uint)index) = value;

            // Only a structural change (a genuinely new entry) invalidates active
            // enumerators; a pure value overwrite of an existing key leaves
            // _version untouched, matching BCL Dictionary<,>. See #233.
            if (isNewEntry)
            {
                _count++;
                _version++;
            }
        }
    }

    /// <summary>
    /// Determines whether the specified key is present in the dictionary.
    /// </summary>
    /// <param name="key">The key to locate in the dictionary.</param>
    /// <returns><c>true</c> if the key is found; otherwise, <c>false</c>.</returns>
    public bool ContainsKey(long key)
    {
        if (key == EmptyKey)
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

        long[] keys = _keys;
        TValue?[] values = _values;
        ref long keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        ref TValue? valuesRef = ref MemoryMarshal.GetArrayDataReference(values);
        int length = keys.Length;
        for (int i = 0; i < length; i++)
        {
            if (Unsafe.Add(ref keysRef, (nint)(uint)i) != EmptyKey
                && comparer.Equals(Unsafe.Add(ref valuesRef, (nint)(uint)i), value))
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
    public bool TryGetValue(long key, out TValue? value)
    {
        if (key == EmptyKey)
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

        value = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_values), (nint)(uint)index);
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
    public bool Remove(long key) => Remove(key, out _);

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
    public bool Remove(long key, out TValue? value)
    {
        if (key == EmptyKey)
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

        value = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_values), (nint)(uint)index);
        _count--;

        BackwardShiftRemove(index);
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
    public void Add(long key, TValue value)
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
    public bool TryAdd(long key, TValue value)
    {
        if (key == EmptyKey)
        {
            if (_hasZeroKey)
                return false;
            _hasZeroKey = true;
            _zeroValue = value;
            _count++;
            _version++;
            return true;
        }

        // Probe the current table first: if the key already exists, we
        // return without touching anything — no Resize, no _version bump,
        // no array swap. The threshold check is deferred to after the
        // duplicate check so a duplicate-at-threshold call cannot silently
        // swap out the backing arrays under an active enumerator (see
        // issue #92).
        int index = ProbeForInsert(key, out bool wasEmpty);
        if (!wasEmpty)
            return false;

        if (_count >= _threshold)
        {
            Resize();
            index = ProbeForInsert(key, out _);
        }

        long[] keys = _keys;
        TValue?[] values = _values;
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(keys), (nint)(uint)index) = key;
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(values), (nint)(uint)index) = value;
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
    /// Ensures that the dictionary can hold at least <paramref name="capacity"/> entries without
    /// resizing, growing the backing table in a single rehash if it is currently smaller. Pre-sizing
    /// before a bulk insert of a known size avoids the incremental rehashes an unsized dictionary
    /// would otherwise pay as it grows. The dictionary is never shrunk by this call.
    /// </summary>
    /// <param name="capacity">The minimum number of entries the dictionary must hold without resizing.</param>
    /// <returns>The number of entries the dictionary can now hold before the next resize.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public int EnsureCapacity(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        if (_threshold < capacity)
        {
            int newSize = FastUtils.MinTableSizeFor(capacity, _loadFactor);
            if (newSize > _keys.Length)
            {
                Resize(newSize);
                _version++;
            }
        }

        return _threshold;
    }

    /// <summary>
    /// Reduces the backing table to the smallest power-of-two size that still holds the current
    /// <see cref="Count"/> without resizing, reclaiming memory after the dictionary has shrunk. The
    /// out-of-band zero-key entry is preserved.
    /// </summary>
    public void TrimExcess() => TrimExcess(_count);

    /// <summary>
    /// Reduces (or grows) the backing table to the smallest power-of-two size that holds at least
    /// <paramref name="capacity"/> entries without resizing.
    /// </summary>
    /// <param name="capacity">
    /// The number of entries to size the table for. Must be at least the current <see cref="Count"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is less than the current <see cref="Count"/>.
    /// </exception>
    public void TrimExcess(int capacity)
    {
        if (capacity < _count)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least the current Count.");

        int newSize = FastUtils.MinTableSizeFor(capacity, _loadFactor);
        if (newSize != _keys.Length)
        {
            Resize(newSize);
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
    public void CopyTo(KeyValuePair<long, TValue?>[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        if (arrayIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex,
                "Array index must be non-negative.");
        if (arrayIndex > array.Length)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex,
                "Array index is beyond the end of the destination array.");
        if (array.Length - arrayIndex < _count)
            throw new ArgumentException(
                "The destination array has insufficient space for the entries.", nameof(array));

        int i = arrayIndex;
        foreach (KeyValuePair<long, TValue?> entry in this)
            array[i++] = entry;
    }

    /// <summary>
    /// A struct enumerator over a <see cref="LongDictionary{TValue, THasher}"/>.
    /// Because it is a struct, iterating it via <c>foreach</c> avoids the
    /// allocation that a compiler-generated <c>IEnumerator&lt;T&gt;</c> would
    /// incur. The out-of-band zero-key entry (if present) is yielded first.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<long, TValue?>>
    {
        private readonly LongDictionary<TValue, THasher> _dict;
        private readonly int _version;
        private int _index;
        private KeyValuePair<long, TValue?> _current;
        private State _state;

        private enum State : byte
        {
            BeforeZeroKey,
            InArray,
            Done
        }

        internal Enumerator(LongDictionary<TValue, THasher> dict)
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
        public KeyValuePair<long, TValue?> Current => _current;

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
                    _current = new KeyValuePair<long, TValue?>(0L, _dict._zeroValue);
                    return true;
                }
            }

            if (_state == State.InArray)
            {
                long[] keys = _dict._keys;
                TValue?[] values = _dict._values;
                int length = keys.Length;
                ref long keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
                ref TValue? valuesRef = ref MemoryMarshal.GetArrayDataReference(values);
                while (++_index < length)
                {
                    long key = Unsafe.Add(ref keysRef, (nint)(uint)_index);
                    if (key != EmptyKey)
                    {
                        _current = new KeyValuePair<long, TValue?>(key, Unsafe.Add(ref valuesRef, (nint)(uint)_index));
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
    /// A struct enumerable view over the keys of a
    /// <see cref="LongDictionary{TValue, THasher}"/>. Iterating it does not
    /// allocate; passing it through <see cref="IEnumerable{T}"/> will box the
    /// enumerator and is therefore not zero-allocation. It is a read-only
    /// <see cref="ICollection{T}"/>: the mutating members throw
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    public readonly struct KeyCollection : ICollection<long>
    {
        private readonly LongDictionary<TValue, THasher> _dict;

        internal KeyCollection(LongDictionary<TValue, THasher> dict) => _dict = dict;

        /// <summary>
        /// Gets the number of keys in the view (equal to the dictionary's count).
        /// </summary>
        public int Count => _dict._count;

        /// <summary>Gets a value indicating whether the view is read-only. Always <c>true</c>.</summary>
        public bool IsReadOnly => true;

        /// <summary>
        /// Returns an allocation-free struct enumerator over the keys.
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(_dict);

        /// <summary>Determines whether the dictionary contains <paramref name="item"/> as a key.</summary>
        /// <param name="item">The key to look for.</param>
        /// <returns><c>true</c> if the key is present.</returns>
        public bool Contains(long item) => _dict.ContainsKey(item);

        /// <summary>
        /// Copies the keys into <paramref name="array"/> starting at <paramref name="arrayIndex"/>,
        /// in the dictionary's enumeration order.
        /// </summary>
        /// <param name="array">The destination array.</param>
        /// <param name="arrayIndex">The zero-based index at which copying begins.</param>
        /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.
        /// </exception>
        /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
        public void CopyTo(long[] array, int arrayIndex)
        {
            ArgumentNullException.ThrowIfNull(array);
            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex,
                    "Array index must be non-negative.");
            if (arrayIndex > array.Length)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex,
                    "Array index is beyond the end of the destination array.");
            if (array.Length - arrayIndex < _dict._count)
                throw new ArgumentException(
                    "The destination array has insufficient space for the keys.", nameof(array));

            int i = arrayIndex;
            foreach (KeyValuePair<long, TValue?> entry in _dict)
                array[i++] = entry.Key;
        }

        IEnumerator<long> IEnumerable<long>.GetEnumerator() => new Enumerator(_dict);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_dict);

        void ICollection<long>.Add(long item) =>
            throw new NotSupportedException("The key view is read-only.");

        void ICollection<long>.Clear() =>
            throw new NotSupportedException("The key view is read-only.");

        bool ICollection<long>.Remove(long item) =>
            throw new NotSupportedException("The key view is read-only.");

        /// <summary>
        /// A struct enumerator over the keys of a
        /// <see cref="LongDictionary{TValue, THasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<long>
        {
            private LongDictionary<TValue, THasher>.Enumerator _inner;

            internal Enumerator(LongDictionary<TValue, THasher> dict) => _inner = dict.GetEnumerator();

            /// <summary>Gets the current key.</summary>
            public long Current => _inner.Current.Key;

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
    /// A struct enumerable view over the values of a
    /// <see cref="LongDictionary{TValue, THasher}"/>. Iterating it does not
    /// allocate; passing it through <see cref="IEnumerable{T}"/> will box the
    /// enumerator and is therefore not zero-allocation. It is a read-only
    /// <see cref="ICollection{T}"/>: the mutating members throw
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    public readonly struct ValueCollection : ICollection<TValue?>
    {
        private readonly LongDictionary<TValue, THasher> _dict;

        internal ValueCollection(LongDictionary<TValue, THasher> dict) => _dict = dict;

        /// <summary>
        /// Gets the number of values in the view (equal to the dictionary's count).
        /// </summary>
        public int Count => _dict._count;

        /// <summary>Gets a value indicating whether the view is read-only. Always <c>true</c>.</summary>
        public bool IsReadOnly => true;

        /// <summary>
        /// Returns an allocation-free struct enumerator over the values.
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(_dict);

        /// <summary>Determines whether any entry holds <paramref name="item"/>. This is an <c>O(n)</c> scan.</summary>
        /// <param name="item">The value to look for.</param>
        /// <returns><c>true</c> if at least one entry holds the value.</returns>
        public bool Contains(TValue? item) => _dict.ContainsValue(item);

        /// <summary>
        /// Copies the values into <paramref name="array"/> starting at <paramref name="arrayIndex"/>,
        /// in the dictionary's enumeration order.
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
            ArgumentNullException.ThrowIfNull(array);
            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex,
                    "Array index must be non-negative.");
            if (arrayIndex > array.Length)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex,
                    "Array index is beyond the end of the destination array.");
            if (array.Length - arrayIndex < _dict._count)
                throw new ArgumentException(
                    "The destination array has insufficient space for the values.", nameof(array));

            int i = arrayIndex;
            foreach (KeyValuePair<long, TValue?> entry in _dict)
                array[i++] = entry.Value;
        }

        IEnumerator<TValue?> IEnumerable<TValue?>.GetEnumerator() => new Enumerator(_dict);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_dict);

        void ICollection<TValue?>.Add(TValue? item) =>
            throw new NotSupportedException("The value view is read-only.");

        void ICollection<TValue?>.Clear() =>
            throw new NotSupportedException("The value view is read-only.");

        bool ICollection<TValue?>.Remove(TValue? item) =>
            throw new NotSupportedException("The value view is read-only.");

        /// <summary>
        /// A struct enumerator over the values of a
        /// <see cref="LongDictionary{TValue, THasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TValue?>
        {
            private LongDictionary<TValue, THasher>.Enumerator _inner;

            internal Enumerator(LongDictionary<TValue, THasher> dict) => _inner = dict.GetEnumerator();

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

    // IReadOnlyDictionary<long, TValue?> explicit interface members. The primary
    // (non-interface) surface — the indexer, ContainsKey, TryGetValue, Count, the
    // struct enumerator, and the KeyCollection / ValueCollection views — already
    // cover the interface contract. These forwarders only widen those members to
    // the boxed IEnumerable<T> / IEnumerator<T> shapes the interface requires, so
    // users who prefer BCL ergonomics (e.g. consuming the dictionary as
    // `IReadOnlyDictionary<long, TValue?>` via LINQ or dependency injection) can do
    // so without losing the zero-allocation fast path for direct foreach.
    TValue? IReadOnlyDictionary<long, TValue?>.this[long key] => this[key];

    IEnumerable<long> IReadOnlyDictionary<long, TValue?>.Keys => Keys;

    IEnumerable<TValue?> IReadOnlyDictionary<long, TValue?>.Values => Values;

    IEnumerator<KeyValuePair<long, TValue?>> IEnumerable<KeyValuePair<long, TValue?>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // IDictionary<long, TValue?> explicit interface members. Everything the mutable
    // interface adds over the read-only one is already public — Add, Remove, Clear,
    // the indexer, CopyTo — so these forwarders only reshape Keys / Values to
    // ICollection<T> and supply the ICollection<KeyValuePair<,>> members, leaving
    // every existing public signature untouched.
    TValue? IDictionary<long, TValue?>.this[long key]
    {
        get => this[key];
        set => this[key] = value!;
    }

    ICollection<long> IDictionary<long, TValue?>.Keys => Keys;

    ICollection<TValue?> IDictionary<long, TValue?>.Values => Values;

    void IDictionary<long, TValue?>.Add(long key, TValue? value) => Add(key, value!);

    bool ICollection<KeyValuePair<long, TValue?>>.IsReadOnly => false;

    void ICollection<KeyValuePair<long, TValue?>>.Add(KeyValuePair<long, TValue?> item) =>
        Add(item.Key, item.Value!);

    bool ICollection<KeyValuePair<long, TValue?>>.Contains(KeyValuePair<long, TValue?> item) =>
        TryGetValue(item.Key, out TValue? value) && EqualityComparer<TValue?>.Default.Equals(value, item.Value);

    bool ICollection<KeyValuePair<long, TValue?>>.Remove(KeyValuePair<long, TValue?> item)
    {
        // ICollection<KVP> semantics: remove only when the *pair* matches, so a stale value must not
        // delete the current entry.
        if (!TryGetValue(item.Key, out TValue? value) ||
            !EqualityComparer<TValue?>.Default.Equals(value, item.Value))
        {
            return false;
        }

        return Remove(item.Key);
    }

    // Returns the slot the caller should write into. <paramref name="wasEmpty"/>
    // tells the caller whether the slot was previously empty (true → new entry,
    // bump _count) or already held the same key (false → overwrite). Hoisting
    // that signal out of the probe lets the indexer setter and TryAdd skip a
    // redundant `_keys[index]` re-read on the insert path.
    //
    // The probe walks `_keys` via `Unsafe.Add` against a single base reference
    // grabbed at the top, so per-iteration bounds checks disappear. The
    // bound is structural: `mask = keys.Length - 1` and `index = ... & mask`
    // keep `index ∈ [0, keys.Length)` for every iteration. `(nint)(uint)index`
    // gives the JIT the additional hint that `index` is non-negative.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ProbeForInsert(long key, out bool wasEmpty)
    {
        long[] keys = _keys;
        ref long keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        int mask = keys.Length - 1;
        int index = _hasher.Hash(key) & mask;

        while (true)
        {
            long slot = Unsafe.Add(ref keysRef, (nint)(uint)index);
            if (slot == EmptyKey) { wasEmpty = true; return index; }
            if (slot == key) { wasEmpty = false; return index; }
            index = (index + 1) & mask;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ProbeForKey(long key)
    {
        long[] keys = _keys;
        ref long keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        int mask = keys.Length - 1;
        int index = _hasher.Hash(key) & mask;

        while (true)
        {
            long slot = Unsafe.Add(ref keysRef, (nint)(uint)index);
            if (slot == EmptyKey) return -1;
            if (slot == key) return index;
            index = (index + 1) & mask;
        }
    }

    private void Resize() => Resize(FastUtils.DoubleCapacity(_keys.Length));

    // Rehashes every live entry into a freshly allocated table of the given power-of-two size.
    // Shared by the doubling growth path and the EnsureCapacity / TrimExcess re-sizers, which
    // pass an explicit target. The caller guarantees newSize is a power of two strictly greater
    // than the in-table live count (so the probe loop always finds a vacant slot).
    private void Resize(int newSize)
    {
        int mask = newSize - 1;
        long[] oldKeys = _keys;
        TValue?[] oldValues = _values;

        // Build into fresh local arrays then swap them in at the end. The loop
        // bypasses the indexer setter on purpose: every reinserted key is known
        // to be unique in the new table (it came from the previous arrays which
        // were themselves a valid dictionary), and _count / _version are
        // conserved across a resize, so the setter's equality check, threshold
        // check, isNewEntry test, _count++, and _version++ are all dead weight.
        // The zero-key entry lives out-of-band and is not touched here.
        //
        // Reads from the old arrays and probe/writes on the new arrays both go
        // through Unsafe.Add against a base reference grabbed at the top of the
        // method, so every per-iteration bounds check disappears. The bounds
        // are structural: `i < oldKeys.Length` is the for-loop condition, and
        // `index = ... & mask` keeps `index ∈ [0, newSize)`.
        long[] newKeys = new long[newSize];
        TValue?[] newValues = new TValue?[newSize];
        ref long oldKeysRef = ref MemoryMarshal.GetArrayDataReference(oldKeys);
        ref TValue? oldValuesRef = ref MemoryMarshal.GetArrayDataReference(oldValues);
        ref long newKeysRef = ref MemoryMarshal.GetArrayDataReference(newKeys);
        ref TValue? newValuesRef = ref MemoryMarshal.GetArrayDataReference(newValues);

        for (int i = 0; i < oldKeys.Length; i++)
        {
            long key = Unsafe.Add(ref oldKeysRef, (nint)(uint)i);
            if (key == EmptyKey)
                continue;

            int index = _hasher.Hash(key) & mask;
            while (Unsafe.Add(ref newKeysRef, (nint)(uint)index) != EmptyKey)
                index = (index + 1) & mask;

            Unsafe.Add(ref newKeysRef, (nint)(uint)index) = key;
            Unsafe.Add(ref newValuesRef, (nint)(uint)index) = Unsafe.Add(ref oldValuesRef, (nint)(uint)i);
        }

        _keys = newKeys;
        _values = newValues;
        _threshold = (int)(newSize * _loadFactor);
    }

    // Backward-shift deletion (Knuth TAOCP Vol 3, §6.4 Algorithm R). The
    // caller has captured the value at startIndex but has NOT cleared the
    // slot; this helper writes the final empty entry itself once the gap
    // settles. Compared to the previous rehash-and-reinsert pass, each
    // surviving cluster entry is visited exactly once and most are not
    // moved at all — the work-per-cluster collapses from quadratic to
    // linear, which is the bulk of the Remove speedup.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BackwardShiftRemove(int startIndex)
    {
        long[] keys = _keys;
        TValue?[] values = _values;
        ref long keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        ref TValue? valuesRef = ref MemoryMarshal.GetArrayDataReference(values);
        int mask = keys.Length - 1;
        int i = startIndex;
        int j = i;

        while (true)
        {
            j = (j + 1) & mask;
            long candidateKey = Unsafe.Add(ref keysRef, (nint)(uint)j);
            if (candidateKey == EmptyKey)
                break;

            int k = _hasher.Hash(candidateKey) & mask;

            // Shift keys[j] into the gap at i iff the probe chain from its
            // natural slot k to its current slot j passes through i (so
            // leaving i empty would orphan the entry). When the scan has
            // not wrapped (i <= j), that means k is outside the open
            // interval (i, j]; when it has wrapped (i > j), the test
            // mirrors across the array boundary.
            bool bypassesGap = (i <= j)
                ? (i < k && k <= j)
                : (i < k || k <= j);
            if (bypassesGap)
                continue;

            Unsafe.Add(ref keysRef, (nint)(uint)i) = candidateKey;
            Unsafe.Add(ref valuesRef, (nint)(uint)i) = Unsafe.Add(ref valuesRef, (nint)(uint)j);
            i = j;
        }

        Unsafe.Add(ref keysRef, (nint)(uint)i) = EmptyKey;
        Unsafe.Add(ref valuesRef, (nint)(uint)i) = EMPTY_VALUE;
    }
}
