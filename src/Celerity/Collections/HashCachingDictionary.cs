using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A high-performance generic dictionary that takes the struct-of-arrays layout
/// one step further than <see cref="CelerityDictionary{TKey, TValue, THasher}"/>:
/// alongside the parallel <c>keys</c> / <c>values</c> arrays it keeps a dense
/// side array of 32-bit hash <em>fingerprints</em>. A probe scan touches only
/// that compact metadata buffer — comparing the cached fingerprint before it
/// ever reads a key — so cache-cold lookups and lookups with expensive key
/// equality (long strings, large structs) short-circuit on a single integer
/// compare instead of dereferencing every candidate key.
/// </summary>
/// <remarks>
/// <para>
/// The fingerprint of an occupied slot is the key's hash with its top bit forced
/// set (<c>hash | 0x80000000</c>), which makes it always non-zero; an empty slot
/// is the array default of <c>0</c>. The fingerprint array therefore doubles as
/// the occupancy bitmap — probing, enumeration, and <see cref="ContainsValue"/>
/// test it rather than comparing keys against <c>default(TKey)</c>. Because the
/// forced bit lives above every power-of-two table mask, the cached fingerprint
/// also yields the slot index directly (<c>fingerprint &amp; mask</c>), so a
/// resize re-homes every entry without recomputing a single hash.
/// </para>
/// <para>
/// This is an additional opt-in type, not a replacement for
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/>: it trades four bytes
/// of metadata per slot for the cache-friendlier probe, which wins on
/// lookup-dominated workloads and expensive-equality keys and is roughly a wash
/// on tiny tables of cheap (e.g. <see cref="int"/>) keys.
/// </para>
/// <para>
/// The key <c>default(TKey)</c> (null for reference types, 0 for primitives,
/// <see cref="Guid.Empty"/> for <see cref="Guid"/>, etc.) is stored out-of-band
/// in a dedicated slot so the hasher is never invoked with it and it never
/// collides with the empty-slot sentinel.
/// </para>
/// </remarks>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TValue">The type of the values.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute key hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class HashCachingDictionary<TKey, TValue, THasher>
    : IReadOnlyDictionary<TKey, TValue?>
    where THasher : struct, IHashProvider<TKey>
{
    /// <summary>
    /// The default initial capacity of the dictionary if no capacity is specified.
    /// </summary>
    protected const int DEFAULT_CAPACITY = 16;

    /// <summary>
    /// The default load factor of the dictionary if no load factor is specified.
    /// </summary>
    protected const float DEFAULT_LOAD_FACTOR = 0.75f;

    // The bit forced on for every occupied slot's fingerprint so the stored
    // value is always non-zero and a zero entry unambiguously means "empty".
    // It sits above every power-of-two table mask, so `fingerprint & mask`
    // recovers the natural slot index without masking the hash separately.
    private const int OCCUPIED_BIT = unchecked((int)0x80000000);

    private int _count = 0;
    private TKey?[] _keys;
    private TValue?[] _values;

    // The struct-of-arrays metadata buffer: one cached hash fingerprint per
    // slot. Zero marks an empty slot; an occupied slot stores `hash | OCCUPIED_BIT`.
    private int[] _fingerprints;

    private readonly float _loadFactor;
    private int _threshold;
    private readonly THasher _hasher;

    // The key value default(TKey) collides with the "empty slot" sentinel used
    // during probing, so it's stored out-of-band in a dedicated slot. _count
    // includes this entry when _hasDefaultKey is true.
    private bool _hasDefaultKey;
    private TValue? _defaultKeyValue;

    // Incremented on every structural mutation so active enumerators can
    // detect concurrent modification and throw, matching BCL semantics.
    private int _version;

    /// <summary>
    /// Initializes a new instance of the <see cref="HashCachingDictionary{TKey,TValue,THasher}"/> class
    /// using the specified capacity and load factor.
    /// </summary>
    /// <param name="capacity">
    /// The initial capacity, automatically rounded to the next power of two.
    /// </param>
    /// <param name="loadFactor">
    /// The fraction of the dictionary's size that can be filled before resizing.
    /// </param>
    public HashCachingDictionary(
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (loadFactor <= 0f || loadFactor >= 1f)
            throw new ArgumentOutOfRangeException(nameof(loadFactor), loadFactor, "Load factor must be between 0 (exclusive) and 1 (exclusive).");

        int size = FastUtils.NextPowerOfTwo(capacity);

        _keys = new TKey?[size];
        _values = new TValue?[size];
        _fingerprints = new int[size];
        _loadFactor = loadFactor;
        _threshold = (int)(size * _loadFactor);
        _hasher = default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HashCachingDictionary{TKey, TValue, THasher}"/>
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
    /// The fraction of the dictionary's size that can be filled before resizing.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> contains one or more duplicate keys.
    /// </exception>
    public HashCachingDictionary(
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
    // when the user also passed an invalid loadFactor.
    private static int InitialCapacityForSource(
        IEnumerable<KeyValuePair<TKey, TValue>> source,
        int capacity,
        float loadFactor)
    {
        ArgumentNullException.ThrowIfNull(source);
        int count = (source as ICollection<KeyValuePair<TKey, TValue>>)?.Count ?? 0;

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
            if (IsDefaultKey(key))
            {
                if (_hasDefaultKey)
                    return _defaultKeyValue!;
                throw new KeyNotFoundException($"Key {key} not found.");
            }

            int index = ProbeForKey(key);
            if (index < 0)
                throw new KeyNotFoundException($"Key {key} not found.");

            return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_values), (nint)(uint)index)!;
        }
        set
        {
            if (IsDefaultKey(key))
            {
                if (!_hasDefaultKey)
                {
                    _hasDefaultKey = true;
                    _count++;
                }
                _defaultKeyValue = value;
                _version++;
                return;
            }

            // Probe before the threshold check so a pure overwrite of an
            // existing key never resizes: only a new entry can push Count over
            // the threshold. On a new-key-at-threshold insert we resize and
            // re-probe in the doubled table.
            int fingerprint = Fingerprint(key);
            int index = ProbeForInsert(fingerprint, key, out bool isNewEntry);
            if (isNewEntry && _count >= _threshold)
            {
                Resize();
                index = ProbeForInsert(fingerprint, key, out _);
            }

            WriteSlot(index, fingerprint, key, value);

            if (isNewEntry)
                _count++;
            _version++;
        }
    }

    /// <summary>
    /// Determines whether the specified key is present in the dictionary.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns><c>true</c> if the key is found; otherwise, <c>false</c>.</returns>
    public bool ContainsKey(TKey key)
    {
        if (IsDefaultKey(key))
            return _hasDefaultKey;

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
    /// fingerprint metadata (skipping empty slots) and, when present, the
    /// out-of-band default-key slot.
    /// </remarks>
    public bool ContainsValue(TValue? value)
    {
        var valueComparer = EqualityComparer<TValue?>.Default;

        if (_hasDefaultKey && valueComparer.Equals(_defaultKeyValue, value))
            return true;

        int[] fingerprints = _fingerprints;
        TValue?[] values = _values;
        ref int fpRef = ref MemoryMarshal.GetArrayDataReference(fingerprints);
        ref TValue? valuesRef = ref MemoryMarshal.GetArrayDataReference(values);
        int length = fingerprints.Length;
        for (int i = 0; i < length; i++)
        {
            if (Unsafe.Add(ref fpRef, (nint)(uint)i) != 0 &&
                valueComparer.Equals(Unsafe.Add(ref valuesRef, (nint)(uint)i), value))
            {
                return true;
            }
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
        if (IsDefaultKey(key))
        {
            if (_hasDefaultKey)
            {
                value = _defaultKeyValue;
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
    public bool Remove(TKey key, out TValue? value)
    {
        if (IsDefaultKey(key))
        {
            if (!_hasDefaultKey)
            {
                value = default;
                return false;
            }
            value = _defaultKeyValue;
            _hasDefaultKey = false;
            _defaultKeyValue = default;
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
        if (IsDefaultKey(key))
        {
            if (_hasDefaultKey)
                return false;
            _hasDefaultKey = true;
            _defaultKeyValue = value;
            _count++;
            _version++;
            return true;
        }

        // Probe the current table first: if the key already exists, we
        // return without touching anything — no Resize, no _version bump,
        // no array swap. The threshold check is deferred to after the
        // duplicate check so a duplicate-at-threshold call cannot silently
        // swap out the backing arrays under an active enumerator.
        int fingerprint = Fingerprint(key);
        int index = ProbeForInsert(fingerprint, key, out bool wasEmpty);
        if (!wasEmpty)
            return false;

        if (_count >= _threshold)
        {
            Resize();
            index = ProbeForInsert(fingerprint, key, out _);
        }

        WriteSlot(index, fingerprint, key, value);
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
        Array.Clear(_fingerprints, 0, _fingerprints.Length);
        _hasDefaultKey = false;
        _defaultKeyValue = default;
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
    /// A struct enumerator over a <see cref="HashCachingDictionary{TKey, TValue, THasher}"/>.
    /// Because it is a struct, iterating it via <c>foreach</c> avoids the
    /// allocation that a compiler-generated <c>IEnumerator&lt;T&gt;</c> would
    /// incur. The out-of-band default-key entry (if present) is yielded first.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue?>>
    {
        private readonly HashCachingDictionary<TKey, TValue, THasher> _dict;
        private readonly int _version;
        private int _index;
        private KeyValuePair<TKey, TValue?> _current;
        private State _state;

        private enum State : byte
        {
            BeforeDefaultKey,
            InArray,
            Done
        }

        internal Enumerator(HashCachingDictionary<TKey, TValue, THasher> dict)
        {
            _dict = dict;
            _version = dict._version;
            _index = -1;
            _current = default;
            _state = State.BeforeDefaultKey;
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

            if (_state == State.BeforeDefaultKey)
            {
                _state = State.InArray;
                if (_dict._hasDefaultKey)
                {
                    _current = new KeyValuePair<TKey, TValue?>(default(TKey)!, _dict._defaultKeyValue);
                    return true;
                }
            }

            if (_state == State.InArray)
            {
                int[] fingerprints = _dict._fingerprints;
                TKey?[] keys = _dict._keys;
                TValue?[] values = _dict._values;
                int length = fingerprints.Length;
                ref int fpRef = ref MemoryMarshal.GetArrayDataReference(fingerprints);
                ref TKey? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
                ref TValue? valuesRef = ref MemoryMarshal.GetArrayDataReference(values);
                while (++_index < length)
                {
                    if (Unsafe.Add(ref fpRef, (nint)(uint)_index) != 0)
                    {
                        _current = new KeyValuePair<TKey, TValue?>(
                            Unsafe.Add(ref keysRef, (nint)(uint)_index)!,
                            Unsafe.Add(ref valuesRef, (nint)(uint)_index));
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
            _state = State.BeforeDefaultKey;
        }

        /// <summary>
        /// Releases any resources held by the enumerator. No-op for this type.
        /// </summary>
        public void Dispose() { }
    }

    /// <summary>
    /// A struct enumerable view over the keys of a
    /// <see cref="HashCachingDictionary{TKey, TValue, THasher}"/>. Iterating it does
    /// not allocate; passing it through <see cref="IEnumerable{T}"/> will box the
    /// enumerator and is therefore not zero-allocation.
    /// </summary>
    public readonly struct KeyCollection : IEnumerable<TKey>
    {
        private readonly HashCachingDictionary<TKey, TValue, THasher> _dict;

        internal KeyCollection(HashCachingDictionary<TKey, TValue, THasher> dict) => _dict = dict;

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
        /// <see cref="HashCachingDictionary{TKey, TValue, THasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TKey>
        {
            private HashCachingDictionary<TKey, TValue, THasher>.Enumerator _inner;

            internal Enumerator(HashCachingDictionary<TKey, TValue, THasher> dict) => _inner = dict.GetEnumerator();

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
    /// <see cref="HashCachingDictionary{TKey, TValue, THasher}"/>. Iterating it does
    /// not allocate; passing it through <see cref="IEnumerable{T}"/> will box the
    /// enumerator and is therefore not zero-allocation.
    /// </summary>
    public readonly struct ValueCollection : IEnumerable<TValue?>
    {
        private readonly HashCachingDictionary<TKey, TValue, THasher> _dict;

        internal ValueCollection(HashCachingDictionary<TKey, TValue, THasher> dict) => _dict = dict;

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
        /// <see cref="HashCachingDictionary{TKey, TValue, THasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TValue?>
        {
            private HashCachingDictionary<TKey, TValue, THasher>.Enumerator _inner;

            internal Enumerator(HashCachingDictionary<TKey, TValue, THasher> dict) => _inner = dict.GetEnumerator();

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

    private static bool IsDefaultKey(TKey key) =>
        EqualityComparer<TKey>.Default.Equals(key, default(TKey));

    // The cached fingerprint for a key: its hash with the top bit forced set so
    // the stored metadata is always non-zero (zero is reserved for "empty").
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Fingerprint(TKey key) => _hasher.Hash(key) | OCCUPIED_BIT;

    // Writes a complete entry — fingerprint, key, value — into the given slot.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteSlot(int index, int fingerprint, TKey key, TValue? value)
    {
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_fingerprints), (nint)(uint)index) = fingerprint;
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_keys), (nint)(uint)index) = key;
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_values), (nint)(uint)index) = value;
    }

    // Returns the slot the caller should write into. <paramref name="wasEmpty"/>
    // tells the caller whether the slot was previously empty (true → new entry,
    // bump _count) or already held the same key (false → overwrite).
    //
    // The probe scans only the dense fingerprint array — `index = fingerprint &
    // mask` recovers the natural slot because the forced occupied bit sits above
    // the mask. A key is touched (and the full equality check run) only when the
    // cached fingerprint matches, so cache-cold misses and expensive-equality
    // keys short-circuit on a single integer compare. The bound is structural:
    // `mask = length - 1` and `index = ... & mask` keep `index ∈ [0, length)`.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ProbeForInsert(int fingerprint, TKey key, out bool wasEmpty)
    {
        int[] fingerprints = _fingerprints;
        TKey?[] keys = _keys;
        ref int fpRef = ref MemoryMarshal.GetArrayDataReference(fingerprints);
        ref TKey? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        int mask = fingerprints.Length - 1;
        var comparer = EqualityComparer<TKey>.Default;
        int index = fingerprint & mask;

        while (true)
        {
            int slotFp = Unsafe.Add(ref fpRef, (nint)(uint)index);
            if (slotFp == 0) { wasEmpty = true; return index; }
            if (slotFp == fingerprint &&
                comparer.Equals(Unsafe.Add(ref keysRef, (nint)(uint)index), key))
            {
                wasEmpty = false;
                return index;
            }
            index = (index + 1) & mask;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ProbeForKey(TKey key)
    {
        int[] fingerprints = _fingerprints;
        TKey?[] keys = _keys;
        ref int fpRef = ref MemoryMarshal.GetArrayDataReference(fingerprints);
        ref TKey? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        int mask = fingerprints.Length - 1;
        var comparer = EqualityComparer<TKey>.Default;
        int fingerprint = Fingerprint(key);
        int index = fingerprint & mask;

        while (true)
        {
            int slotFp = Unsafe.Add(ref fpRef, (nint)(uint)index);
            if (slotFp == 0) return -1;
            if (slotFp == fingerprint &&
                comparer.Equals(Unsafe.Add(ref keysRef, (nint)(uint)index), key))
            {
                return index;
            }
            index = (index + 1) & mask;
        }
    }

    private void Resize()
    {
        int newSize = _keys.Length * 2;
        int mask = newSize - 1;
        TKey?[] oldKeys = _keys;
        TValue?[] oldValues = _values;
        int[] oldFingerprints = _fingerprints;

        // Build into fresh local arrays then swap them in at the end. Because
        // every reinserted entry is known to be unique in the new table, the
        // loop bypasses the public insert path. The cached fingerprint carries
        // the hash, so `oldFp & mask` recovers the new natural slot without
        // recomputing a single hash — the resize never invokes the hasher. The
        // default-key entry lives out-of-band and is not touched here.
        TKey?[] newKeys = new TKey?[newSize];
        TValue?[] newValues = new TValue?[newSize];
        int[] newFingerprints = new int[newSize];
        ref TKey? oldKeysRef = ref MemoryMarshal.GetArrayDataReference(oldKeys);
        ref TValue? oldValuesRef = ref MemoryMarshal.GetArrayDataReference(oldValues);
        ref int oldFpRef = ref MemoryMarshal.GetArrayDataReference(oldFingerprints);
        ref TKey? newKeysRef = ref MemoryMarshal.GetArrayDataReference(newKeys);
        ref TValue? newValuesRef = ref MemoryMarshal.GetArrayDataReference(newValues);
        ref int newFpRef = ref MemoryMarshal.GetArrayDataReference(newFingerprints);

        for (int i = 0; i < oldFingerprints.Length; i++)
        {
            int fp = Unsafe.Add(ref oldFpRef, (nint)(uint)i);
            if (fp == 0)
                continue;

            int index = fp & mask;
            while (Unsafe.Add(ref newFpRef, (nint)(uint)index) != 0)
                index = (index + 1) & mask;

            Unsafe.Add(ref newFpRef, (nint)(uint)index) = fp;
            Unsafe.Add(ref newKeysRef, (nint)(uint)index) = Unsafe.Add(ref oldKeysRef, (nint)(uint)i);
            Unsafe.Add(ref newValuesRef, (nint)(uint)index) = Unsafe.Add(ref oldValuesRef, (nint)(uint)i);
        }

        _keys = newKeys;
        _values = newValues;
        _fingerprints = newFingerprints;
        _threshold = (int)(newSize * _loadFactor);
    }

    // Backward-shift deletion (Knuth TAOCP Vol 3, §6.4 Algorithm R). The
    // caller has captured the value at startIndex but has NOT cleared the
    // slot; this helper writes the final empty entry itself once the gap
    // settles. The natural slot of each candidate comes straight from its
    // cached fingerprint (`fp & mask`), so the shift logic never rehashes.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BackwardShiftRemove(int startIndex)
    {
        int[] fingerprints = _fingerprints;
        TKey?[] keys = _keys;
        TValue?[] values = _values;
        ref int fpRef = ref MemoryMarshal.GetArrayDataReference(fingerprints);
        ref TKey? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        ref TValue? valuesRef = ref MemoryMarshal.GetArrayDataReference(values);
        int mask = fingerprints.Length - 1;
        int i = startIndex;
        int j = i;

        while (true)
        {
            j = (j + 1) & mask;
            int candidateFp = Unsafe.Add(ref fpRef, (nint)(uint)j);
            if (candidateFp == 0)
                break;

            int k = candidateFp & mask;

            // Shift slot j into the gap at i iff the probe chain from its
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

            Unsafe.Add(ref fpRef, (nint)(uint)i) = candidateFp;
            Unsafe.Add(ref keysRef, (nint)(uint)i) = Unsafe.Add(ref keysRef, (nint)(uint)j);
            Unsafe.Add(ref valuesRef, (nint)(uint)i) = Unsafe.Add(ref valuesRef, (nint)(uint)j);
            i = j;
        }

        Unsafe.Add(ref fpRef, (nint)(uint)i) = 0;
        Unsafe.Add(ref keysRef, (nint)(uint)i) = default;
        Unsafe.Add(ref valuesRef, (nint)(uint)i) = default;
    }
}
