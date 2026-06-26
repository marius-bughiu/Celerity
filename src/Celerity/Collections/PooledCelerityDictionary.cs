using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A high-performance generic dictionary whose backing arrays are rented from
/// <see cref="ArrayPool{T}"/> instead of being allocated on the managed heap, so
/// repeated build-up / tear-down cycles in high-throughput code paths put far
/// less pressure on the garbage collector.
/// </summary>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TValue">The type of the values.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute key hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
/// <remarks>
/// <para>
/// The public surface is byte-for-byte identical to
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/> — open-addressed,
/// linear-probing storage with an out-of-band <c>default(TKey)</c> slot and
/// backward-shift deletion — with one addition: the type implements
/// <see cref="IDisposable"/>. Because the backing arrays are <em>borrowed</em>
/// from <see cref="ArrayPool{T}.Shared"/>, you should call <see cref="Dispose"/>
/// (e.g. via a <c>using</c> statement) when finished so the arrays return to the
/// pool for reuse. Failing to dispose is not a leak — the arrays are reclaimed by
/// the GC like any other managed array — you simply forfeit the pooling benefit.
/// </para>
/// <para>
/// <see cref="ArrayPool{T}.Shared"/> never throws on exhaustion: when the pool has
/// no suitable buffer it allocates a fresh one, so callers never have to handle a
/// "pool empty" condition. Rented buffers may be <em>larger</em> than requested,
/// so this type tracks its logical power-of-two capacity (<c>_size</c> / its mask)
/// independently of the backing array's <c>Length</c> and only ever reads or
/// writes the live region. On return, arrays of a reference type (or a type that
/// contains references) are cleared so the pool does not pin your keys / values
/// alive after disposal.
/// </para>
/// <para>
/// This type is not thread-safe. Like every Celerity collection, concurrent
/// mutation must be synchronized by the caller. After <see cref="Dispose"/> every
/// member throws <see cref="ObjectDisposedException"/>.
/// </para>
/// </remarks>
public class PooledCelerityDictionary<TKey, TValue, THasher>
    : IReadOnlyDictionary<TKey, TValue?>, IDisposable
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

    // The rented arrays may be longer than the logical table, so the power-of-two
    // size and its probe mask are tracked here rather than derived from
    // _keys.Length (which ArrayPool is free to over-provision). Only the region
    // [0, _size) of each rented array is ever read or written.
    private int _size;
    private int _mask;

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

    // Set by Dispose; every public member rejects calls once true.
    private bool _disposed;

    // Whether the element type holds managed references, computed once. When true,
    // rented arrays are cleared on return so the pool does not keep keys / values
    // reachable (memory-leak prevention); value-type arrays skip the clear.
    private static readonly bool ClearKeysOnReturn =
        RuntimeHelpers.IsReferenceOrContainsReferences<TKey?>();
    private static readonly bool ClearValuesOnReturn =
        RuntimeHelpers.IsReferenceOrContainsReferences<TValue?>();

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="PooledCelerityDictionary{TKey,TValue,THasher}"/> class using the
    /// specified capacity and load factor. The backing arrays are rented from
    /// <see cref="ArrayPool{T}.Shared"/>.
    /// </summary>
    /// <param name="capacity">
    /// The initial capacity, automatically rounded to the next power of two.
    /// </param>
    /// <param name="loadFactor">
    /// The fraction of the dictionary's size that can be filled before resizing.
    /// </param>
    public PooledCelerityDictionary(
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (loadFactor <= 0f || loadFactor >= 1f)
            throw new ArgumentOutOfRangeException(nameof(loadFactor), loadFactor, "Load factor must be between 0 (exclusive) and 1 (exclusive).");

        int size = FastUtils.NextPowerOfTwo(capacity);

        _keys = RentKeys(size);
        _values = RentValues(size);
        _size = size;
        _mask = size - 1;
        _loadFactor = loadFactor;
        _threshold = (int)(size * _loadFactor);
        _hasher = default;
    }

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="PooledCelerityDictionary{TKey, TValue, THasher}"/> class that
    /// contains the key/value pairs copied from the specified
    /// <paramref name="source"/>. The backing arrays are rented from
    /// <see cref="ArrayPool{T}.Shared"/>.
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
    public PooledCelerityDictionary(
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

        // Size for the source count *including* load-factor headroom so the bulk
        // fill never rehashes mid-construction (matching CelerityDictionary, #27).
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
    /// <exception cref="ObjectDisposedException">The dictionary has been disposed.</exception>
    /// <returns>The value associated with the specified key.</returns>
    public TValue this[TKey key]
    {
        get
        {
            ThrowIfDisposed();
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
            ThrowIfDisposed();
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

            // Probe before the threshold check so a pure overwrite of an
            // existing key never resizes: only a new entry can push Count over
            // the threshold. On a new-key-at-threshold insert we resize and
            // re-probe in the doubled table.
            int index = ProbeForInsert(key, out bool isNewEntry);
            if (isNewEntry && _count >= _threshold)
            {
                Resize();
                index = ProbeForInsert(key, out _);
            }

            TKey?[] keys = _keys;
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
    /// <param name="key">The key to locate.</param>
    /// <returns><c>true</c> if the key is found; otherwise, <c>false</c>.</returns>
    /// <exception cref="ObjectDisposedException">The dictionary has been disposed.</exception>
    public bool ContainsKey(TKey key)
    {
        ThrowIfDisposed();
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
    /// <exception cref="ObjectDisposedException">The dictionary has been disposed.</exception>
    /// <remarks>
    /// This operation is <c>O(n)</c> in the dictionary's count: it scans the
    /// live probe table (skipping empty slots) and, when present, the out-of-band
    /// default-key slot.
    /// </remarks>
    public bool ContainsValue(TValue? value)
    {
        ThrowIfDisposed();
        var valueComparer = EqualityComparer<TValue?>.Default;

        if (_hasDefaultKey && valueComparer.Equals(_defaultKeyValue, value))
            return true;

        var keyComparer = EqualityComparer<TKey>.Default;
        TKey?[] keys = _keys;
        TValue?[] values = _values;
        ref TKey? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        ref TValue? valuesRef = ref MemoryMarshal.GetArrayDataReference(values);
        // Bound by the logical size, not keys.Length: the rented array may have a
        // garbage tail beyond _size that was never cleared.
        int length = _size;
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
    /// <exception cref="ObjectDisposedException">The dictionary has been disposed.</exception>
    public bool TryGetValue(TKey key, out TValue? value)
    {
        ThrowIfDisposed();
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
    /// <exception cref="ObjectDisposedException">The dictionary has been disposed.</exception>
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
    /// <exception cref="ObjectDisposedException">The dictionary has been disposed.</exception>
    public bool Remove(TKey key, out TValue? value)
    {
        ThrowIfDisposed();
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
    /// <exception cref="ObjectDisposedException">The dictionary has been disposed.</exception>
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
    /// <exception cref="ObjectDisposedException">The dictionary has been disposed.</exception>
    public bool TryAdd(TKey key, TValue value)
    {
        ThrowIfDisposed();
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

        // Probe the current table first: if the key already exists, return
        // without touching anything — no Resize, no _version bump, no array
        // swap. The threshold check is deferred to after the duplicate check.
        int index = ProbeForInsert(key, out bool wasEmpty);
        if (!wasEmpty)
            return false;

        if (_count >= _threshold)
        {
            Resize();
            index = ProbeForInsert(key, out _);
        }

        TKey?[] keys = _keys;
        TValue?[] values = _values;
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(keys), (nint)(uint)index) = key;
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(values), (nint)(uint)index) = value;
        _count++;
        _version++;
        return true;
    }

    /// <summary>
    /// Removes all keys and values from the dictionary. The underlying rented
    /// capacity is preserved (the arrays are not returned to the pool).
    /// </summary>
    /// <exception cref="ObjectDisposedException">The dictionary has been disposed.</exception>
    public void Clear()
    {
        ThrowIfDisposed();
        if (_count == 0)
            return;

        // Only the live region needs clearing; the tail is already treated as
        // out-of-bounds by every reader.
        Array.Clear(_keys, 0, _size);
        Array.Clear(_values, 0, _size);
        _hasDefaultKey = false;
        _defaultKeyValue = default;
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Ensures that the dictionary can hold at least <paramref name="capacity"/> entries without
    /// resizing, renting a larger backing table in a single rehash if it is currently smaller.
    /// Pre-sizing before a bulk insert of a known size avoids the incremental rehashes — and the
    /// rent/return churn — an unsized dictionary would otherwise pay as it grows. The dictionary is
    /// never shrunk by this call.
    /// </summary>
    /// <param name="capacity">The minimum number of entries the dictionary must hold without resizing.</param>
    /// <returns>The number of entries the dictionary can now hold before the next resize.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    /// <exception cref="ObjectDisposedException">The dictionary has been disposed.</exception>
    public int EnsureCapacity(int capacity)
    {
        ThrowIfDisposed();
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        if (_threshold < capacity)
        {
            int newSize = FastUtils.MinTableSizeFor(capacity, _loadFactor);
            if (newSize > _size)
            {
                Resize(newSize);
                _version++;
            }
        }

        return _threshold;
    }

    /// <summary>
    /// Reduces the backing table to the smallest power-of-two size that still holds the current
    /// <see cref="Count"/> without resizing, returning the larger rented buffers to the pool. The
    /// out-of-band default-key entry is preserved.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The dictionary has been disposed.</exception>
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
    /// <exception cref="ObjectDisposedException">The dictionary has been disposed.</exception>
    public void TrimExcess(int capacity)
    {
        ThrowIfDisposed();
        if (capacity < _count)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least the current Count.");

        int newSize = FastUtils.MinTableSizeFor(capacity, _loadFactor);
        if (newSize != _size)
        {
            Resize(newSize);
            _version++;
        }
    }

    /// <summary>
    /// Returns the rented backing arrays to <see cref="ArrayPool{T}.Shared"/> and
    /// marks the dictionary as disposed. After disposal every member throws
    /// <see cref="ObjectDisposedException"/>. Calling <see cref="Dispose"/> more
    /// than once is safe and is a no-op after the first call.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        ArrayPool<TKey?>.Shared.Return(_keys, ClearKeysOnReturn);
        ArrayPool<TValue?>.Shared.Return(_values, ClearValuesOnReturn);

        // Drop references to the now-returned arrays so a use-after-dispose bug
        // cannot read or corrupt a buffer the pool may have handed to someone
        // else, and so the GC can reclaim anything still pinned.
        _keys = Array.Empty<TKey?>();
        _values = Array.Empty<TValue?>();
        _size = 0;
        _mask = 0;
        _count = 0;
        _hasDefaultKey = false;
        _defaultKeyValue = default;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Returns an allocation-free enumerator that yields each key/value pair
    /// stored in the dictionary. The enumeration order is unspecified and may
    /// change across versions; do not rely on it. If the dictionary is modified
    /// during enumeration, <see cref="Enumerator.MoveNext"/> throws
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over this dictionary.</returns>
    /// <exception cref="ObjectDisposedException">The dictionary has been disposed.</exception>
    public Enumerator GetEnumerator()
    {
        ThrowIfDisposed();
        return new Enumerator(this);
    }

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
    /// A struct enumerator over a
    /// <see cref="PooledCelerityDictionary{TKey, TValue, THasher}"/>. Because it is
    /// a struct, iterating it via <c>foreach</c> avoids the allocation that a
    /// compiler-generated <c>IEnumerator&lt;T&gt;</c> would incur. The out-of-band
    /// default-key entry (if present) is yielded first.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue?>>
    {
        private readonly PooledCelerityDictionary<TKey, TValue, THasher> _dict;
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

        internal Enumerator(PooledCelerityDictionary<TKey, TValue, THasher> dict)
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
                // Bound by the logical size, not keys.Length (rented tail is garbage).
                int length = _dict._size;
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
    /// <see cref="PooledCelerityDictionary{TKey, TValue, THasher}"/>. Iterating it
    /// does not allocate; passing it through <see cref="IEnumerable{T}"/> will box
    /// the enumerator and is therefore not zero-allocation.
    /// </summary>
    public readonly struct KeyCollection : IEnumerable<TKey>
    {
        private readonly PooledCelerityDictionary<TKey, TValue, THasher> _dict;

        internal KeyCollection(PooledCelerityDictionary<TKey, TValue, THasher> dict) => _dict = dict;

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
        /// <see cref="PooledCelerityDictionary{TKey, TValue, THasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TKey>
        {
            private PooledCelerityDictionary<TKey, TValue, THasher>.Enumerator _inner;

            internal Enumerator(PooledCelerityDictionary<TKey, TValue, THasher> dict) => _inner = dict.GetEnumerator();

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
    /// <see cref="PooledCelerityDictionary{TKey, TValue, THasher}"/>. Iterating it
    /// does not allocate; passing it through <see cref="IEnumerable{T}"/> will box
    /// the enumerator and is therefore not zero-allocation.
    /// </summary>
    public readonly struct ValueCollection : IEnumerable<TValue?>
    {
        private readonly PooledCelerityDictionary<TKey, TValue, THasher> _dict;

        internal ValueCollection(PooledCelerityDictionary<TKey, TValue, THasher> dict) => _dict = dict;

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
        /// <see cref="PooledCelerityDictionary{TKey, TValue, THasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TValue?>
        {
            private PooledCelerityDictionary<TKey, TValue, THasher>.Enumerator _inner;

            internal Enumerator(PooledCelerityDictionary<TKey, TValue, THasher> dict) => _inner = dict.GetEnumerator();

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

    // IReadOnlyDictionary<TKey, TValue?> explicit interface members — widen the
    // primary surface to the boxed IEnumerable<T> / IEnumerator<T> shapes the
    // interface requires, matching CelerityDictionary.
    TValue? IReadOnlyDictionary<TKey, TValue?>.this[TKey key] => this[key];

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue?>.Keys => Keys;

    IEnumerable<TValue?> IReadOnlyDictionary<TKey, TValue?>.Values => Values;

    IEnumerator<KeyValuePair<TKey, TValue?>> IEnumerable<KeyValuePair<TKey, TValue?>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static bool IsDefaultKey(TKey key) =>
        EqualityComparer<TKey>.Default.Equals(key, default(TKey));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PooledCelerityDictionary<TKey, TValue, THasher>));
    }

    private static TKey?[] RentKeys(int size)
    {
        TKey?[] array = ArrayPool<TKey?>.Shared.Rent(size);
        // ArrayPool buffers are not zeroed; the probe treats default(TKey) as the
        // empty-slot sentinel, so the live region must be cleared after every rent.
        Array.Clear(array, 0, size);
        return array;
    }

    private static TValue?[] RentValues(int size)
    {
        TValue?[] array = ArrayPool<TValue?>.Shared.Rent(size);
        Array.Clear(array, 0, size);
        return array;
    }

    // Returns the slot the caller should write into. <paramref name="wasEmpty"/>
    // tells the caller whether the slot was previously empty (true → new entry)
    // or already held the same key (false → overwrite). Probes against the logical
    // mask (_mask), not keys.Length - 1, because the rented array may be larger.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ProbeForInsert(TKey key, out bool wasEmpty)
    {
        TKey?[] keys = _keys;
        ref TKey? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        int mask = _mask;
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
        int mask = _mask;
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

    private void Resize() => Resize(FastUtils.DoubleCapacity(_size));

    // Rehashes every live entry into freshly rented arrays of the given power-of-two size, then
    // returns the old buffers to the pool. Shared by the doubling growth path and the
    // EnsureCapacity / TrimExcess re-sizers, which pass an explicit target. The caller guarantees
    // newSize is a power of two strictly greater than the in-table live count (so the probe loop
    // always finds a vacant slot).
    private void Resize(int newSize)
    {
        int mask = newSize - 1;
        int oldSize = _size;
        TKey?[] oldKeys = _keys;
        TValue?[] oldValues = _values;

        // Rent fresh (cleared) arrays of the doubled size, rehash into them, then
        // return the old buffers to the pool. Every reinserted key is known unique
        // (it came from a valid table) so we bypass the indexer's bookkeeping.
        TKey?[] newKeys = RentKeys(newSize);
        TValue?[] newValues = RentValues(newSize);
        ref TKey? oldKeysRef = ref MemoryMarshal.GetArrayDataReference(oldKeys);
        ref TValue? oldValuesRef = ref MemoryMarshal.GetArrayDataReference(oldValues);
        ref TKey? newKeysRef = ref MemoryMarshal.GetArrayDataReference(newKeys);
        ref TValue? newValuesRef = ref MemoryMarshal.GetArrayDataReference(newValues);

        var comparer = EqualityComparer<TKey>.Default;
        for (int i = 0; i < oldSize; i++)
        {
            TKey? key = Unsafe.Add(ref oldKeysRef, (nint)(uint)i);
            if (comparer.Equals(key, default(TKey)))
                continue;

            int index = _hasher.Hash(key!) & mask;
            while (!comparer.Equals(Unsafe.Add(ref newKeysRef, (nint)(uint)index), default(TKey)))
                index = (index + 1) & mask;

            Unsafe.Add(ref newKeysRef, (nint)(uint)index) = key;
            Unsafe.Add(ref newValuesRef, (nint)(uint)index) = Unsafe.Add(ref oldValuesRef, (nint)(uint)i);
        }

        _keys = newKeys;
        _values = newValues;
        _size = newSize;
        _mask = mask;
        _threshold = (int)(newSize * _loadFactor);

        ArrayPool<TKey?>.Shared.Return(oldKeys, ClearKeysOnReturn);
        ArrayPool<TValue?>.Shared.Return(oldValues, ClearValuesOnReturn);
    }

    // Backward-shift deletion (Knuth TAOCP Vol 3, §6.4 Algorithm R). The caller
    // has captured the value at startIndex but has NOT cleared the slot; this
    // helper writes the final empty entry itself once the gap settles. Probes the
    // logical mask (_mask), not keys.Length - 1.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BackwardShiftRemove(int startIndex)
    {
        TKey?[] keys = _keys;
        TValue?[] values = _values;
        ref TKey? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        ref TValue? valuesRef = ref MemoryMarshal.GetArrayDataReference(values);
        int mask = _mask;
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

            // Shift keys[j] into the gap at i iff the probe chain from its natural
            // slot k to its current slot j passes through i (so leaving i empty
            // would orphan the entry).
            bool bypassesGap = (i <= j)
                ? (i < k && k <= j)
                : (i < k || k <= j);
            if (bypassesGap)
                continue;

            Unsafe.Add(ref keysRef, (nint)(uint)i) = candidateKey;
            Unsafe.Add(ref valuesRef, (nint)(uint)i) = Unsafe.Add(ref valuesRef, (nint)(uint)j);
            i = j;
        }

        Unsafe.Add(ref keysRef, (nint)(uint)i) = default;
        Unsafe.Add(ref valuesRef, (nint)(uint)i) = default;
    }
}
