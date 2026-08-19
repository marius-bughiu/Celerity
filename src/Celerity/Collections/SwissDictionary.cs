using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.InteropServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A high-performance generic dictionary that resolves collisions with
/// <em>SIMD-accelerated group probing</em> in the spirit of Google's Swiss
/// Tables and Facebook's <c>F14</c>, instead of the scalar linear probing used by
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/>.
/// </summary>
/// <remarks>
/// <para>
/// The table keeps a parallel array of one-byte <em>control</em> tags — one per
/// slot — separate from the key/value arrays. Each control byte is either
/// <c>Empty</c>, <c>Deleted</c> (a tombstone), or, for an occupied slot, the low
/// 7 bits of the key's hash (its <em>h2</em> fragment). Slots are grouped into
/// aligned blocks of <see cref="GroupWidth"/> (16), so a single
/// <see cref="Vector128{T}"/> compare tests all 16 control bytes in a group at
/// once: a lookup loads the 16 tags, compares them against the broadcast h2, and
/// turns the result into a 16-bit candidate mask via
/// <see cref="Vector128.ExtractMostSignificantBits{T}(Vector128{T})"/>. Only the
/// (usually one) candidate slots then pay a full key comparison; an
/// all-tags-checked group with any <c>Empty</c> slot ends the probe. The portable
/// <see cref="Vector128"/> API JITs to SSE2 / AVX2 on x86, AdvSimd on Arm, and a
/// scalar software fallback elsewhere, so the type is correct everywhere and fast
/// where hardware SIMD is available.
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Wins</b> on lookup-heavy workloads with many slots per probe: the group
/// compare amortizes the per-slot tag test, and the h2 tag filters out
/// non-matching residents before any (potentially expensive) key comparison, so
/// negative lookups and lookups on clustered keys stay cheap.
/// </description></item>
/// <item><description>
/// <b>Costs</b> a one-byte control tag per slot (so it allocates a little more
/// than <see cref="CelerityDictionary{TKey, TValue, THasher}"/>) and uses
/// tombstones for deletion, which it reclaims by rehashing when they accumulate.
/// </description></item>
/// </list>
/// <para>
/// It is otherwise a drop-in peer of <see cref="CelerityDictionary{TKey, TValue, THasher}"/>:
/// same constructors, the same <see cref="IReadOnlyDictionary{TKey, TValue}"/>
/// surface, the same allocation-free struct enumerators, and the same
/// out-of-band handling of <c>default(TKey)</c> (so the hasher is never invoked
/// with the zero / null key, matching the rest of the family).
/// </para>
/// </remarks>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TValue">The type of the values.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute key hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class SwissDictionary<TKey, TValue, THasher>
    : IDictionary<TKey, TValue?>, IReadOnlyDictionary<TKey, TValue?>
    where THasher : struct, IHashProvider<TKey>
{
    /// <summary>
    /// The default initial capacity of the dictionary if no capacity is specified.
    /// </summary>
    protected const int DefaultCapacity = 16;

    /// <summary>
    /// The default load factor of the dictionary if no load factor is specified.
    /// </summary>
    protected const float DefaultLoadFactor = 0.75f;

    // The SIMD group width: a Vector128<sbyte> tests 16 control bytes at once.
    // GroupShift is log2(GroupWidth) so `group << GroupShift` is the group's
    // first slot. The table size is always a power of two and at least one group.
    private const int GroupWidth = 16;
    private const int GroupShift = 4;

    // Control-byte tags. The high (sign) bit distinguishes a free slot (Empty /
    // Deleted, both negative) from an occupied one (an h2 fragment in 0..127, so
    // non-negative). Empty ends a probe; Deleted (a tombstone) does not, so a
    // lookup walks past it but an insert may reuse it.
    private const sbyte Empty = -128;   // 0b1000_0000
    private const sbyte Deleted = -2;   // 0b1111_1110

    private int _count = 0;
    private sbyte[] _controls;
    private TKey?[] _keys;
    private TValue?[] _values;
    private int _capacity;
    private int _numGroupsMask;
    private readonly float _loadFactor;
    private int _threshold;
    // Remaining Empty-slot budget before a resize: threshold minus the number of
    // occupied-or-tombstoned slots. Filling an Empty slot decrements it; reusing a
    // Deleted slot leaves it unchanged; an erase that frees a slot to Empty bumps
    // it back. The out-of-band default-key entry never touches it (it owns no
    // array slot). A resize is forced when it would go non-positive.
    private int _growthLeft;
    private readonly THasher _hasher;

    // default(TKey) (null for reference types, 0 for primitives, Guid.Empty for
    // Guid, ...) is stored out-of-band rather than in an array slot, so the hasher
    // is never invoked with it — matching CelerityDictionary / RobinHoodDictionary
    // and keeping string hashers (which throw on null) safe. _count includes this
    // entry when _hasDefaultKey is true.
    private bool _hasDefaultKey;
    private TValue? _defaultKeyValue;

    // Incremented on every structural mutation so active enumerators can
    // detect concurrent modification and throw, matching BCL semantics.
    private int _version;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwissDictionary{TKey,TValue,THasher}"/> class
    /// using the specified capacity and load factor.
    /// </summary>
    /// <param name="capacity">
    /// The initial capacity, automatically rounded to the next power of two (and
    /// to at least one SIMD group of <see cref="GroupWidth"/> slots).
    /// </param>
    /// <param name="loadFactor">
    /// The fraction of the dictionary's size that can be filled before resizing.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative, or <paramref name="loadFactor"/>
    /// is not in the open interval (0, 1).
    /// </exception>
    public SwissDictionary(
        int capacity = DefaultCapacity,
        float loadFactor = DefaultLoadFactor)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (loadFactor <= 0f || loadFactor >= 1f)
            throw new ArgumentOutOfRangeException(nameof(loadFactor), loadFactor, "Load factor must be between 0 (exclusive) and 1 (exclusive).");

        int size = Math.Max(GroupWidth, FastUtils.NextPowerOfTwo(capacity));

        _controls = new sbyte[size];
        _controls.AsSpan().Fill(Empty);
        _keys = new TKey?[size];
        _values = new TValue?[size];
        _capacity = size;
        _numGroupsMask = (size >> GroupShift) - 1;
        _loadFactor = loadFactor;
        _threshold = (int)(size * _loadFactor);
        _growthLeft = _threshold;
        _hasher = default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwissDictionary{TKey, TValue, THasher}"/>
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
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative, or <paramref name="loadFactor"/>
    /// is not in the open interval (0, 1).
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> contains one or more duplicate keys.
    /// </exception>
    public SwissDictionary(
        IEnumerable<KeyValuePair<TKey, TValue>> source,
        int capacity = DefaultCapacity,
        float loadFactor = DefaultLoadFactor)
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

            int hash = _hasher.Hash(key);
            int index = Find(key, hash);
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

            // One Hash() call threaded through the existence probe and (if absent)
            // the insertion: a pure overwrite never resizes, only a genuinely new
            // entry can.
            int hash = _hasher.Hash(key);
            ProbeForInsert(key, hash, out int slot, out bool existing);
            if (existing)
            {
                // Pure value overwrite of an existing key: no structural change,
                // so _version is left untouched and active enumerators stay
                // valid, matching BCL Dictionary<,>. See #233.
                Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_values), (nint)(uint)slot) = value;
                return;
            }

            InsertAbsent(key, value, hash, slot);
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

        return Find(key, _hasher.Hash(key)) >= 0;
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
    /// This operation is <c>O(n)</c> in the dictionary's capacity: it scans the
    /// control bytes (skipping empty / tombstone slots) and, when present, the
    /// out-of-band default-key slot.
    /// </remarks>
    public bool ContainsValue(TValue? value)
    {
        var valueComparer = EqualityComparer<TValue?>.Default;

        if (_hasDefaultKey && valueComparer.Equals(_defaultKeyValue, value))
            return true;

        sbyte[] controls = _controls;
        TValue?[] values = _values;
        ref sbyte controlsRef = ref MemoryMarshal.GetArrayDataReference(controls);
        ref TValue? valuesRef = ref MemoryMarshal.GetArrayDataReference(values);
        int length = controls.Length;
        for (int i = 0; i < length; i++)
        {
            // A full slot has a non-negative control tag; Empty / Deleted are negative.
            if (Unsafe.Add(ref controlsRef, (nint)(uint)i) >= 0 &&
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

        int index = Find(key, _hasher.Hash(key));
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

        int index = Find(key, _hasher.Hash(key));
        if (index < 0)
        {
            value = default;
            return false;
        }

        value = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_values), (nint)(uint)index);
        EraseAt(index);
        _count--;
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

        // Probe once (single Hash() call). A duplicate returns without touching the
        // table — no resize, no _version bump, no array swap — so an active
        // enumerator stays valid (mirrors the #92 ordering on the rest of the family).
        int hash = _hasher.Hash(key);
        ProbeForInsert(key, hash, out int slot, out bool existing);
        if (existing)
            return false;

        InsertAbsent(key, value, hash, slot);
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

        _controls.AsSpan().Fill(Empty);
        Array.Clear(_keys, 0, _keys.Length);
        Array.Clear(_values, 0, _values.Length);
        _hasDefaultKey = false;
        _defaultKeyValue = default;
        _count = 0;
        _growthLeft = _threshold;
        _version++;
    }

    /// <summary>
    /// Ensures that the dictionary can hold at least <paramref name="capacity"/> entries without
    /// resizing, growing the backing table in a single rebuild if it is currently smaller. Pre-sizing
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
            int newCapacity = Math.Max(GroupWidth, FastUtils.MinTableSizeFor(capacity, _loadFactor));
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
    /// tombstones and reclaiming memory. The out-of-band default-key entry is preserved.
    /// </summary>
    public void TrimExcess() => TrimExcess(_count);

    /// <summary>
    /// Reduces (or grows) the backing table to the smallest valid size that holds at least
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

        int newCapacity = Math.Max(GroupWidth, FastUtils.MinTableSizeFor(capacity, _loadFactor));
        if (newCapacity != _capacity)
        {
            Resize(newCapacity);
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
    public void CopyTo(KeyValuePair<TKey, TValue?>[] array, int arrayIndex)
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
        foreach (KeyValuePair<TKey, TValue?> entry in this)
            array[i++] = entry;
    }

    /// <summary>
    /// A struct enumerator over a <see cref="SwissDictionary{TKey, TValue, THasher}"/>.
    /// Because it is a struct, iterating it via <c>foreach</c> avoids the
    /// allocation that a compiler-generated <c>IEnumerator&lt;T&gt;</c> would
    /// incur. The out-of-band default-key entry (if present) is yielded first.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue?>>
    {
        private readonly SwissDictionary<TKey, TValue, THasher> _dict;
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

        internal Enumerator(SwissDictionary<TKey, TValue, THasher> dict)
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
                sbyte[] controls = _dict._controls;
                TKey?[] keys = _dict._keys;
                TValue?[] values = _dict._values;
                int length = controls.Length;
                ref sbyte controlsRef = ref MemoryMarshal.GetArrayDataReference(controls);
                ref TKey? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
                ref TValue? valuesRef = ref MemoryMarshal.GetArrayDataReference(values);
                while (++_index < length)
                {
                    // Full slots carry a non-negative control tag.
                    if (Unsafe.Add(ref controlsRef, (nint)(uint)_index) >= 0)
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
    /// <see cref="SwissDictionary{TKey, TValue, THasher}"/>. Iterating it does
    /// not allocate; passing it through <see cref="IEnumerable{T}"/> will box the
    /// enumerator and is therefore not zero-allocation. It is a read-only
    /// <see cref="ICollection{T}"/>: the mutating members throw
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    public readonly struct KeyCollection : ICollection<TKey>
    {
        private readonly SwissDictionary<TKey, TValue, THasher> _dict;

        internal KeyCollection(SwissDictionary<TKey, TValue, THasher> dict) => _dict = dict;

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
        public bool Contains(TKey item) => _dict.ContainsKey(item);

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
        public void CopyTo(TKey[] array, int arrayIndex)
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
            foreach (KeyValuePair<TKey, TValue?> entry in _dict)
                array[i++] = entry.Key;
        }

        IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => new Enumerator(_dict);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_dict);

        void ICollection<TKey>.Add(TKey item) =>
            throw new NotSupportedException("The key view is read-only.");

        void ICollection<TKey>.Clear() =>
            throw new NotSupportedException("The key view is read-only.");

        bool ICollection<TKey>.Remove(TKey item) =>
            throw new NotSupportedException("The key view is read-only.");

        /// <summary>
        /// A struct enumerator over the keys of a
        /// <see cref="SwissDictionary{TKey, TValue, THasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TKey>
        {
            private SwissDictionary<TKey, TValue, THasher>.Enumerator _inner;

            internal Enumerator(SwissDictionary<TKey, TValue, THasher> dict) => _inner = dict.GetEnumerator();

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
    /// <see cref="SwissDictionary{TKey, TValue, THasher}"/>. Iterating it does
    /// not allocate; passing it through <see cref="IEnumerable{T}"/> will box the
    /// enumerator and is therefore not zero-allocation. It is a read-only
    /// <see cref="ICollection{T}"/>: the mutating members throw
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    public readonly struct ValueCollection : ICollection<TValue?>
    {
        private readonly SwissDictionary<TKey, TValue, THasher> _dict;

        internal ValueCollection(SwissDictionary<TKey, TValue, THasher> dict) => _dict = dict;

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
            foreach (KeyValuePair<TKey, TValue?> entry in _dict)
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
        /// <see cref="SwissDictionary{TKey, TValue, THasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TValue?>
        {
            private SwissDictionary<TKey, TValue, THasher>.Enumerator _inner;

            internal Enumerator(SwissDictionary<TKey, TValue, THasher> dict) => _inner = dict.GetEnumerator();

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
    // (non-interface) surface already covers the interface contract; these
    // forwarders only widen those members to the boxed IEnumerable<T> /
    // IEnumerator<T> shapes the interface requires.
    TValue? IReadOnlyDictionary<TKey, TValue?>.this[TKey key] => this[key];

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue?>.Keys => Keys;

    IEnumerable<TValue?> IReadOnlyDictionary<TKey, TValue?>.Values => Values;

    IEnumerator<KeyValuePair<TKey, TValue?>> IEnumerable<KeyValuePair<TKey, TValue?>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // IDictionary<TKey, TValue?> explicit interface members. Everything the mutable
    // interface adds over the read-only one is already public — Add, Remove, Clear,
    // the indexer, CopyTo — so these forwarders only reshape Keys / Values to
    // ICollection<T> and supply the ICollection<KeyValuePair<,>> members, leaving
    // every existing public signature untouched.
    TValue? IDictionary<TKey, TValue?>.this[TKey key]
    {
        get => this[key];
        set => this[key] = value!;
    }

    ICollection<TKey> IDictionary<TKey, TValue?>.Keys => Keys;

    ICollection<TValue?> IDictionary<TKey, TValue?>.Values => Values;

    void IDictionary<TKey, TValue?>.Add(TKey key, TValue? value) => Add(key, value!);

    bool ICollection<KeyValuePair<TKey, TValue?>>.IsReadOnly => false;

    void ICollection<KeyValuePair<TKey, TValue?>>.Add(KeyValuePair<TKey, TValue?> item) =>
        Add(item.Key, item.Value!);

    bool ICollection<KeyValuePair<TKey, TValue?>>.Contains(KeyValuePair<TKey, TValue?> item) =>
        TryGetValue(item.Key, out TValue? value) && EqualityComparer<TValue?>.Default.Equals(value, item.Value);

    bool ICollection<KeyValuePair<TKey, TValue?>>.Remove(KeyValuePair<TKey, TValue?> item)
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

    private static bool IsDefaultKey(TKey key) =>
        EmptySlot.Is(key);

    // SIMD group lookup for a non-default key. Walks the aligned-group probe
    // sequence from the key's home group: a single Vector128 compare turns each
    // group's 16 control tags into a 16-bit candidate mask against the key's h2
    // fragment, and only candidate slots pay a key comparison. The probe ends at
    // the first group containing an Empty tag (the key is absent — insert would
    // have stopped there too). Returns the slot index, or -1 if not present.
    //
    // The hash splits into h1 (high bits, selecting the home group) and h2 (low 7
    // bits, the stored control tag). Termination is guaranteed: the load factor
    // keeps at least one Empty slot in the table at all times, and linear group
    // probing wraps through every group.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Find(TKey key, int hash)
    {
        sbyte[] controls = _controls;
        TKey?[] keys = _keys;
        ref sbyte controlsRef = ref MemoryMarshal.GetArrayDataReference(controls);
        ref TKey? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        var comparer = EqualityComparer<TKey>.Default;
        int mask = _numGroupsMask;
        uint h1 = (uint)hash >> 7;
        Vector128<sbyte> wanted = Vector128.Create((sbyte)(hash & 0x7F));
        Vector128<sbyte> empty = Vector128.Create(Empty);
        int group = (int)(h1 & (uint)mask);

        while (true)
        {
            int baseSlot = group << GroupShift;
            Vector128<sbyte> ctrl = Vector128.LoadUnsafe(ref Unsafe.Add(ref controlsRef, (nint)(uint)baseSlot));

            uint matches = Vector128.Equals(ctrl, wanted).ExtractMostSignificantBits();
            while (matches != 0)
            {
                int slot = baseSlot + BitOperations.TrailingZeroCount(matches);
                if (comparer.Equals(Unsafe.Add(ref keysRef, (nint)(uint)slot), key))
                    return slot;
                matches &= matches - 1;
            }

            if (Vector128.Equals(ctrl, empty).ExtractMostSignificantBits() != 0)
                return -1;

            group = (group + 1) & mask;
        }
    }

    // SIMD group probe for an insert of a non-default key. In one walk it both
    // detects an existing key (sets <paramref name="existing"/> and returns its
    // slot) and, if absent, returns the slot to insert into: the first Deleted
    // (tombstone) slot seen along the sequence, or the first Empty slot if none.
    // Reusing a tombstone keeps the table compact without growing the Empty budget.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProbeForInsert(TKey key, int hash, out int slot, out bool existing)
    {
        sbyte[] controls = _controls;
        TKey?[] keys = _keys;
        ref sbyte controlsRef = ref MemoryMarshal.GetArrayDataReference(controls);
        ref TKey? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        var comparer = EqualityComparer<TKey>.Default;
        int mask = _numGroupsMask;
        uint h1 = (uint)hash >> 7;
        Vector128<sbyte> wanted = Vector128.Create((sbyte)(hash & 0x7F));
        Vector128<sbyte> empty = Vector128.Create(Empty);
        Vector128<sbyte> deleted = Vector128.Create(Deleted);
        int group = (int)(h1 & (uint)mask);
        int firstDeleted = -1;

        while (true)
        {
            int baseSlot = group << GroupShift;
            Vector128<sbyte> ctrl = Vector128.LoadUnsafe(ref Unsafe.Add(ref controlsRef, (nint)(uint)baseSlot));

            uint matches = Vector128.Equals(ctrl, wanted).ExtractMostSignificantBits();
            while (matches != 0)
            {
                int s = baseSlot + BitOperations.TrailingZeroCount(matches);
                if (comparer.Equals(Unsafe.Add(ref keysRef, (nint)(uint)s), key))
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

    // Places a known-absent non-default key into the slot the probe chose,
    // resizing first if filling an Empty slot would push the table over its
    // growth budget. Reusing a Deleted (tombstone) slot never needs a resize.
    private void InsertAbsent(TKey key, TValue? value, int hash, int slot)
    {
        bool targetEmpty = _controls[slot] == Empty;
        if (targetEmpty && _growthLeft <= 0)
        {
            Resize();
            // Re-probe in the freshly built (tombstone-free) table; the target is
            // now guaranteed to be an Empty slot.
            ProbeForInsert(key, hash, out slot, out _);
        }

        ref sbyte controlsRef = ref MemoryMarshal.GetArrayDataReference(_controls);
        Unsafe.Add(ref controlsRef, (nint)(uint)slot) = (sbyte)(hash & 0x7F);
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_keys), (nint)(uint)slot) = key;
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_values), (nint)(uint)slot) = value;

        _count++;
        if (targetEmpty)
            _growthLeft--;
        _version++;
    }

    // Tombstone-aware erase. If the slot's group still holds an Empty tag, no key
    // ever probed past this group, so the slot can be freed to Empty (and the
    // growth budget reclaimed). Otherwise the group was full when residents
    // probed through it, so the slot must become a Deleted tombstone — a lookup
    // walks past it but does not terminate, preserving every resident's
    // reachability. The key / value are cleared either way to release references.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EraseAt(int slot)
    {
        int baseSlot = (slot >> GroupShift) << GroupShift;
        ref sbyte controlsRef = ref MemoryMarshal.GetArrayDataReference(_controls);
        Vector128<sbyte> ctrl = Vector128.LoadUnsafe(ref Unsafe.Add(ref controlsRef, (nint)(uint)baseSlot));

        if (Vector128.Equals(ctrl, Vector128.Create(Empty)).ExtractMostSignificantBits() != 0)
        {
            Unsafe.Add(ref controlsRef, (nint)(uint)slot) = Empty;
            _growthLeft++;
        }
        else
        {
            Unsafe.Add(ref controlsRef, (nint)(uint)slot) = Deleted;
        }

        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_keys), (nint)(uint)slot) = default;
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_values), (nint)(uint)slot) = default;
    }

    // Rebuilds the table, dropping all tombstones. Doubles the capacity when the
    // live load justifies real growth; otherwise rehashes at the same size to
    // reclaim tombstones (so a churn of insert/delete cycles cannot grow the table
    // without bound). The out-of-band default-key entry is untouched.
    private void Resize() => Resize(
        _count - (_hasDefaultKey ? 1 : 0) >= (_threshold >> 1)
            ? FastUtils.DoubleCapacity(_capacity)
            : _capacity);

    // Rebuilds the table at the given power-of-two capacity, dropping all tombstones and recomputing
    // the group mask, threshold, and growth budget. Shared by the doubling/rehash growth path and
    // the EnsureCapacity / TrimExcess re-sizers, which pass an explicit target. The caller guarantees
    // newCapacity is a power of two no smaller than GroupWidth and strictly greater than the live
    // (non-default) entry count, so every PlaceFresh terminates.
    private void Resize(int newCapacity)
    {
        sbyte[] oldControls = _controls;
        TKey?[] oldKeys = _keys;
        TValue?[] oldValues = _values;
        int oldCapacity = _capacity;

        sbyte[] newControls = new sbyte[newCapacity];
        newControls.AsSpan().Fill(Empty);
        TKey?[] newKeys = new TKey?[newCapacity];
        TValue?[] newValues = new TValue?[newCapacity];

        _controls = newControls;
        _keys = newKeys;
        _values = newValues;
        _capacity = newCapacity;
        _numGroupsMask = (newCapacity >> GroupShift) - 1;
        _threshold = (int)(newCapacity * _loadFactor);
        // The fresh table has no tombstones, so the whole array budget is the live
        // (non-default) entry count below the threshold.
        _growthLeft = _threshold - (_count - (_hasDefaultKey ? 1 : 0));

        ref sbyte oldControlsRef = ref MemoryMarshal.GetArrayDataReference(oldControls);
        ref TKey? oldKeysRef = ref MemoryMarshal.GetArrayDataReference(oldKeys);
        ref TValue? oldValuesRef = ref MemoryMarshal.GetArrayDataReference(oldValues);
        for (int i = 0; i < oldCapacity; i++)
        {
            if (Unsafe.Add(ref oldControlsRef, (nint)(uint)i) >= 0)
            {
                TKey key = Unsafe.Add(ref oldKeysRef, (nint)(uint)i)!;
                PlaceFresh(key, Unsafe.Add(ref oldValuesRef, (nint)(uint)i), _hasher.Hash(key));
            }
        }
    }

    // Places an entry into the first Empty slot along its probe sequence in a
    // freshly built (tombstone-free) table. Keys are known unique, so no equality
    // check is needed; growth bookkeeping is owned by Resize, so this only writes.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PlaceFresh(TKey key, TValue? value, int hash)
    {
        ref sbyte controlsRef = ref MemoryMarshal.GetArrayDataReference(_controls);
        int mask = _numGroupsMask;
        uint h1 = (uint)hash >> 7;
        Vector128<sbyte> empty = Vector128.Create(Empty);
        int group = (int)(h1 & (uint)mask);

        while (true)
        {
            int baseSlot = group << GroupShift;
            Vector128<sbyte> ctrl = Vector128.LoadUnsafe(ref Unsafe.Add(ref controlsRef, (nint)(uint)baseSlot));
            uint emptyMask = Vector128.Equals(ctrl, empty).ExtractMostSignificantBits();
            if (emptyMask != 0)
            {
                int slot = baseSlot + BitOperations.TrailingZeroCount(emptyMask);
                Unsafe.Add(ref controlsRef, (nint)(uint)slot) = (sbyte)(hash & 0x7F);
                Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_keys), (nint)(uint)slot) = key;
                Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_values), (nint)(uint)slot) = value;
                return;
            }
            group = (group + 1) & mask;
        }
    }
}
