using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A high-performance generic dictionary that resolves collisions with
/// <em>Robin Hood</em> open addressing instead of the plain linear probing used
/// by <see cref="CelerityDictionary{TKey, TValue, THasher}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Robin Hood hashing keeps, for every occupied slot, the number of steps it
/// sits away from its ideal (hash) slot — its <em>probe sequence length</em>
/// (PSL). On insert, an incoming key that has travelled further than the key
/// already occupying a slot <em>displaces</em> it ("robs from the rich"): the
/// resident is evicted and re-inserted further along. This bounds the variance
/// of probe lengths, so the worst-case lookup is much closer to the average than
/// under linear probing. The pay-offs and costs are workload-dependent:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Wins</b> on clustered / adversarial key distributions, where vanilla linear
/// probing grows long runs and tail-latency lookups degrade toward O(n). The PSL
/// invariant also lets a <em>negative</em> lookup stop early — as soon as the
/// probe distance exceeds the resident slot's PSL the key cannot be present.
/// </description></item>
/// <item><description>
/// <b>Costs</b> a per-slot <see cref="int"/> of PSL bookkeeping (so it allocates
/// more than <see cref="CelerityDictionary{TKey, TValue, THasher}"/>) and a small
/// amount of extra work per insert for the displacement swaps. On uniform
/// distributions it is typically a wash or a slight loss versus linear probing.
/// </description></item>
/// </list>
/// <para>
/// It is otherwise a drop-in peer of <see cref="CelerityDictionary{TKey, TValue, THasher}"/>:
/// same constructors, the same <see cref="IReadOnlyDictionary{TKey, TValue}"/>
/// surface, the same allocation-free struct enumerators, and the same
/// out-of-band handling of <c>default(TKey)</c> so the zero / null key never
/// collides with the empty-slot sentinel.
/// </para>
/// </remarks>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TValue">The type of the values.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute key hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class RobinHoodDictionary<TKey, TValue, THasher>
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

    private int _count = 0;
    private TKey?[] _keys;
    private TValue?[] _values;
    // Probe sequence length (distance from the ideal slot) for each occupied
    // slot. Only meaningful where the parallel _keys slot is non-default; empty
    // slots carry 0. Stored explicitly so the probe loop can compare distances
    // without recomputing the resident key's hash — that is what makes the
    // Robin Hood early-exit on negative lookups cheap.
    private int[] _distances;
    private readonly float _loadFactor;
    private int _threshold;
    private readonly THasher _hasher;

    // The key value default(TKey) (null for reference types, 0 for primitives,
    // Guid.Empty for Guid, etc.) collides with the "empty slot" sentinel used
    // during probing, so it's stored out-of-band in a dedicated slot. _count
    // includes this entry when _hasDefaultKey is true.
    private bool _hasDefaultKey;
    private TValue? _defaultKeyValue;

    // Incremented on every structural mutation so active enumerators can
    // detect concurrent modification and throw, matching BCL semantics.
    private int _version;

    /// <summary>
    /// Initializes a new instance of the <see cref="RobinHoodDictionary{TKey,TValue,THasher}"/> class
    /// using the specified capacity and load factor.
    /// </summary>
    /// <param name="capacity">
    /// The initial capacity, automatically rounded to the next power of two.
    /// </param>
    /// <param name="loadFactor">
    /// The fraction of the dictionary's size that can be filled before resizing.
    /// </param>
    public RobinHoodDictionary(
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
        _distances = new int[size];
        _loadFactor = loadFactor;
        _threshold = (int)(size * _loadFactor);
        _hasher = default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RobinHoodDictionary{TKey, TValue, THasher}"/>
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
    public RobinHoodDictionary(
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
    // when the user also passed an invalid loadFactor. Mirrors the same helper
    // on CelerityDictionary (issue #27).
    private static int InitialCapacityForSource(
        IEnumerable<KeyValuePair<TKey, TValue>> source,
        int capacity,
        float loadFactor)
    {
        ArgumentNullException.ThrowIfNull(source);
        int count = (source as ICollection<KeyValuePair<TKey, TValue>>)?.Count ?? 0;

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
                    _version++;
                }
                _defaultKeyValue = value;
                return;
            }

            // Probe first: a pure overwrite of an existing key never resizes and
            // never touches PSL bookkeeping. Only a genuinely new entry can push
            // Count over the threshold; on that path we resize and then run the
            // Robin Hood insertion into the (possibly doubled) table. The key's
            // hash is computed once and threaded through both the probe and the
            // insert so a new-key set costs exactly one Hash() call.
            int hash = _hasher.Hash(key);
            int index = ProbeForKey(key, hash);
            if (index >= 0)
            {
                // Pure value overwrite of an existing key: no structural change,
                // so _version is left untouched and active enumerators stay
                // valid, matching BCL Dictionary<,>. See #233.
                Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_values), (nint)(uint)index) = value;
                return;
            }

            if (_count >= _threshold)
                Resize();

            InsertAbsent(_keys, _values, _distances, key, value, hash);
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
    /// probe table (skipping empty slots) and, when present, the out-of-band
    /// default-key slot.
    /// </remarks>
    public bool ContainsValue(TValue? value)
    {
        var valueComparer = EqualityComparer<TValue?>.Default;

        if (_hasDefaultKey && valueComparer.Equals(_defaultKeyValue, value))
            return true;

        var keyComparer = EqualityComparer<TKey>.Default;
        TKey?[] keys = _keys;
        TValue?[] values = _values;
        ref TKey? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        ref TValue? valuesRef = ref MemoryMarshal.GetArrayDataReference(values);
        int length = keys.Length;
        for (int i = 0; i < length; i++)
        {
            if (!keyComparer.Equals(Unsafe.Add(ref keysRef, (nint)(uint)i), default(TKey)) &&
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

        // Probe the current table first: if the key already exists we return
        // without resizing, bumping _version, or swapping arrays — matching the
        // duplicate-at-threshold guarantee the other collections make (#92). The
        // hash is computed once and reused by the probe and the insert so a new
        // entry costs exactly one Hash() call.
        int hash = _hasher.Hash(key);
        if (ProbeForKey(key, hash) >= 0)
            return false;

        if (_count >= _threshold)
            Resize();

        InsertAbsent(_keys, _values, _distances, key, value, hash);
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
        Array.Clear(_distances, 0, _distances.Length);
        _hasDefaultKey = false;
        _defaultKeyValue = default;
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
    /// out-of-band default-key entry is preserved.
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
    /// A struct enumerator over a <see cref="RobinHoodDictionary{TKey, TValue, THasher}"/>.
    /// Because it is a struct, iterating it via <c>foreach</c> avoids the
    /// allocation that a compiler-generated <c>IEnumerator&lt;T&gt;</c> would
    /// incur. The out-of-band default-key entry (if present) is yielded first.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue?>>
    {
        private readonly RobinHoodDictionary<TKey, TValue, THasher> _dict;
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

        internal Enumerator(RobinHoodDictionary<TKey, TValue, THasher> dict)
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
                TKey?[] keys = _dict._keys;
                TValue?[] values = _dict._values;
                int length = keys.Length;
                ref TKey? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
                ref TValue? valuesRef = ref MemoryMarshal.GetArrayDataReference(values);
                var comparer = EqualityComparer<TKey>.Default;
                while (++_index < length)
                {
                    TKey? key = Unsafe.Add(ref keysRef, (nint)(uint)_index);
                    if (!comparer.Equals(key, default(TKey)))
                    {
                        _current = new KeyValuePair<TKey, TValue?>(key!, Unsafe.Add(ref valuesRef, (nint)(uint)_index));
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
    /// <see cref="RobinHoodDictionary{TKey, TValue, THasher}"/>. Iterating it does
    /// not allocate; passing it through <see cref="IEnumerable{T}"/> will box the
    /// enumerator and is therefore not zero-allocation.
    /// </summary>
    public readonly struct KeyCollection : IEnumerable<TKey>
    {
        private readonly RobinHoodDictionary<TKey, TValue, THasher> _dict;

        internal KeyCollection(RobinHoodDictionary<TKey, TValue, THasher> dict) => _dict = dict;

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
        /// <see cref="RobinHoodDictionary{TKey, TValue, THasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TKey>
        {
            private RobinHoodDictionary<TKey, TValue, THasher>.Enumerator _inner;

            internal Enumerator(RobinHoodDictionary<TKey, TValue, THasher> dict) => _inner = dict.GetEnumerator();

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
    /// <see cref="RobinHoodDictionary{TKey, TValue, THasher}"/>. Iterating it does
    /// not allocate; passing it through <see cref="IEnumerable{T}"/> will box the
    /// enumerator and is therefore not zero-allocation.
    /// </summary>
    public readonly struct ValueCollection : IEnumerable<TValue?>
    {
        private readonly RobinHoodDictionary<TKey, TValue, THasher> _dict;

        internal ValueCollection(RobinHoodDictionary<TKey, TValue, THasher> dict) => _dict = dict;

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
        /// <see cref="RobinHoodDictionary{TKey, TValue, THasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TValue?>
        {
            private RobinHoodDictionary<TKey, TValue, THasher>.Enumerator _inner;

            internal Enumerator(RobinHoodDictionary<TKey, TValue, THasher> dict) => _inner = dict.GetEnumerator();

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

    // Robin Hood lookup. Walks the probe chain from the key's ideal slot and
    // stops on one of three conditions: an empty slot (key absent), a resident
    // whose stored probe distance is *smaller* than the distance we have already
    // travelled (key absent — the Robin Hood invariant guarantees the key, if
    // present, would have displaced this shorter-distance resident), or a key
    // match (found). The early distance cut is what bounds negative-lookup cost
    // on clustered distributions.
    //
    // The probe reads `_keys` / `_distances` via `Unsafe.Add` against base
    // references grabbed at the top, so per-iteration bounds checks disappear.
    // The bound is structural: `mask = keys.Length - 1` and `index = ... & mask`
    // keep `index ∈ [0, keys.Length)`; `(nint)(uint)index` hints non-negativity.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ProbeForKey(TKey key) => ProbeForKey(key, _hasher.Hash(key));

    // Overload taking a precomputed hash so the insert paths can share a single
    // Hash() call between the existence probe and the subsequent insertion.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ProbeForKey(TKey key, int hash)
    {
        TKey?[] keys = _keys;
        int[] distances = _distances;
        ref TKey? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        ref int distRef = ref MemoryMarshal.GetArrayDataReference(distances);
        int mask = keys.Length - 1;
        var comparer = EqualityComparer<TKey>.Default;
        int index = hash & mask;
        int dist = 0;

        while (true)
        {
            TKey? slot = Unsafe.Add(ref keysRef, (nint)(uint)index);
            if (comparer.Equals(slot, default(TKey)))
                return -1;
            if (Unsafe.Add(ref distRef, (nint)(uint)index) < dist)
                return -1;
            if (comparer.Equals(slot, key))
                return index;
            index = (index + 1) & mask;
            dist++;
        }
    }

    // Robin Hood insertion of a key known to be ABSENT from the table (callers
    // probe for existence first, so this never needs a key-equality check). The
    // incoming entry walks forward carrying its probe distance; whenever it meets
    // a resident with a *shorter* distance it swaps in (robs the rich) and the
    // displaced resident becomes the new carried entry, continuing until an empty
    // slot is found. Operates on the arrays passed in so Resize can reuse it
    // against freshly allocated storage before the field swap.
    private void InsertAbsent(TKey?[] keys, TValue?[] values, int[] distances, TKey key, TValue? value)
        => InsertAbsent(keys, values, distances, key, value, _hasher.Hash(key));

    // Overload taking a precomputed hash for the incoming key. Only the incoming
    // key's hash is reused; displaced (robbed) residents carry their stored probe
    // distance forward and are never re-hashed, so the whole insertion costs a
    // single Hash() call regardless of how many residents it displaces.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InsertAbsent(TKey?[] keys, TValue?[] values, int[] distances, TKey key, TValue? value, int hash)
    {
        ref TKey? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        ref TValue? valuesRef = ref MemoryMarshal.GetArrayDataReference(values);
        ref int distRef = ref MemoryMarshal.GetArrayDataReference(distances);
        int mask = keys.Length - 1;
        var comparer = EqualityComparer<TKey>.Default;
        int index = hash & mask;
        int dist = 0;

        while (true)
        {
            ref TKey? slotKey = ref Unsafe.Add(ref keysRef, (nint)(uint)index);
            if (comparer.Equals(slotKey, default(TKey)))
            {
                slotKey = key;
                Unsafe.Add(ref valuesRef, (nint)(uint)index) = value;
                Unsafe.Add(ref distRef, (nint)(uint)index) = dist;
                return;
            }

            ref int slotDist = ref Unsafe.Add(ref distRef, (nint)(uint)index);
            if (slotDist < dist)
            {
                // Rob: drop the carried entry here and pick up the resident.
                ref TValue? slotVal = ref Unsafe.Add(ref valuesRef, (nint)(uint)index);

                TKey? displacedKey = slotKey;
                TValue? displacedVal = slotVal;
                int displacedDist = slotDist;

                slotKey = key;
                slotVal = value;
                slotDist = dist;

                key = displacedKey!;
                value = displacedVal;
                dist = displacedDist;
            }

            index = (index + 1) & mask;
            dist++;
        }
    }

    private void Resize() => Resize(FastUtils.DoubleCapacity(_keys.Length));

    // Rehashes every live entry into freshly allocated tables of the given power-of-two size via the
    // Robin Hood insertion path. Shared by the doubling growth path and the EnsureCapacity /
    // TrimExcess re-sizers, which pass an explicit target. The caller guarantees newSize is a power
    // of two strictly greater than the in-table live count (so every InsertAbsent terminates).
    private void Resize(int newSize)
    {
        TKey?[] oldKeys = _keys;
        TValue?[] oldValues = _values;

        // Reinsert every live entry into freshly allocated arrays via the Robin
        // Hood insertion path, then swap them in. Each old key is known unique,
        // so InsertAbsent's no-equality-check contract holds; _count / _version
        // are conserved across a resize. The out-of-band default-key entry is
        // untouched. Reads from the old keys array go through Unsafe.Add against
        // a base reference, eliding the per-iteration bounds check (the bound is
        // the for-loop condition `i < oldKeys.Length`).
        TKey?[] newKeys = new TKey?[newSize];
        TValue?[] newValues = new TValue?[newSize];
        int[] newDistances = new int[newSize];
        ref TKey? oldKeysRef = ref MemoryMarshal.GetArrayDataReference(oldKeys);
        ref TValue? oldValuesRef = ref MemoryMarshal.GetArrayDataReference(oldValues);

        var comparer = EqualityComparer<TKey>.Default;
        for (int i = 0; i < oldKeys.Length; i++)
        {
            TKey? key = Unsafe.Add(ref oldKeysRef, (nint)(uint)i);
            if (comparer.Equals(key, default(TKey)))
                continue;

            InsertAbsent(newKeys, newValues, newDistances, key!, Unsafe.Add(ref oldValuesRef, (nint)(uint)i));
        }

        _keys = newKeys;
        _values = newValues;
        _distances = newDistances;
        _threshold = (int)(newSize * _loadFactor);
    }

    // Robin Hood backward-shift deletion. Starting at the removed slot, pull each
    // following entry back by one position and decrement its stored probe
    // distance, stopping at the first slot that is either empty or already in its
    // ideal position (distance 0) — neither can move back without breaking the
    // invariant. The final settled slot is cleared. Compared with tombstones this
    // keeps the table contiguous so lookups never pay for deleted-slot skips.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BackwardShiftRemove(int startIndex)
    {
        TKey?[] keys = _keys;
        TValue?[] values = _values;
        int[] distances = _distances;
        ref TKey? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        ref TValue? valuesRef = ref MemoryMarshal.GetArrayDataReference(values);
        ref int distRef = ref MemoryMarshal.GetArrayDataReference(distances);
        int mask = keys.Length - 1;
        var comparer = EqualityComparer<TKey>.Default;
        int i = startIndex;

        while (true)
        {
            int next = (i + 1) & mask;
            TKey? nextKey = Unsafe.Add(ref keysRef, (nint)(uint)next);
            int nextDist = Unsafe.Add(ref distRef, (nint)(uint)next);
            if (comparer.Equals(nextKey, default(TKey)) || nextDist == 0)
                break;

            Unsafe.Add(ref keysRef, (nint)(uint)i) = nextKey;
            Unsafe.Add(ref valuesRef, (nint)(uint)i) = Unsafe.Add(ref valuesRef, (nint)(uint)next);
            Unsafe.Add(ref distRef, (nint)(uint)i) = nextDist - 1;
            i = next;
        }

        Unsafe.Add(ref keysRef, (nint)(uint)i) = default;
        Unsafe.Add(ref valuesRef, (nint)(uint)i) = default;
        Unsafe.Add(ref distRef, (nint)(uint)i) = 0;
    }
}
