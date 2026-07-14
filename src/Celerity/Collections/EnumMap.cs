using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// A dictionary specialized for <b>enum keys</b>, backed by a dense value array indexed on the
/// enum's underlying integer value plus a parallel occupancy bit vector — the .NET analogue of
/// Java's <c>java.util.EnumMap</c>. It is the dictionary counterpart of
/// <see cref="EnumSet{TEnum}"/>: where <see cref="EnumSet{TEnum}"/> stores one <i>bit</i> per
/// possible element, <see cref="EnumMap{TEnum, TValue}"/> stores one <i>value slot</i> per
/// possible key. A lookup is a direct array index — no hash, no probe chain, no collisions.
/// </summary>
/// <typeparam name="TEnum">
/// The enum type whose values key the map. Constrained to <c>struct, Enum</c>.
/// </typeparam>
/// <typeparam name="TValue">The type of the stored values.</typeparam>
/// <remarks>
/// <para>
/// Where <see cref="Dictionary{TKey, TValue}"/> runs every key through
/// <see cref="EqualityComparer{T}"/> and an open-addressed / bucketed hash table,
/// <see cref="EnumMap{TEnum, TValue}"/> maps the enum's underlying integer value straight to an
/// array slot. For an enum whose members are small non-negative integers (the default
/// <c>0, 1, 2, …</c> declaration — the overwhelmingly common case),
/// <see cref="this[TEnum]"/> / <see cref="TryGetValue(TEnum, out TValue)"/> /
/// <see cref="ContainsKey(TEnum)"/> / <see cref="Add(TEnum, TValue)"/> /
/// <see cref="Remove(TEnum)"/> are a shift, a mask, a single-<see cref="ulong"/> bit test, and a
/// contiguous array access: no hash, no probe chain, no per-entry node allocation.
/// </para>
/// <para>
/// Presence is tracked out-of-band in a <see cref="ulong"/> occupancy bit vector so a key mapped
/// to <c>default(TValue)</c> is distinguished from an absent key.
/// </para>
/// <para>
/// <b>Supported enums.</b> The backing store is sized once from the enum's maximum defined
/// underlying value (shared with <see cref="EnumSet{TEnum}"/> via <c>EnumSetInfo&lt;TEnum&gt;</c>).
/// The type therefore supports enums whose underlying values are small, non-negative integers. An
/// enum that declares a negative member, or whose maximum value exceeds <c>65535</c> (a sparse or
/// <c>[Flags]</c> power-of-two enum, for which a dense array is the wrong tool), throws
/// <see cref="NotSupportedException"/> from the constructor — use
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/> for those. A runtime key outside the
/// supported range (an out-of-range cast) is rejected by the write surface with
/// <see cref="ArgumentOutOfRangeException"/> and reported as absent by the read surface.
/// </para>
/// <para>
/// It implements <see cref="IReadOnlyDictionary{TKey, TValue}"/> over
/// <c>&lt;TEnum, TValue?&gt;</c>, ships an allocation-free struct enumerator, and — unlike the
/// hash-table dictionaries — enumerates in <b>ascending underlying-value order</b>, a
/// deterministic bonus that falls out of walking the occupancy vector low bit first.
/// </para>
/// <para>
/// The type is single-threaded.
/// </para>
/// </remarks>
public class EnumMap<TEnum, TValue> : IReadOnlyDictionary<TEnum, TValue?>
    where TEnum : struct, Enum
{
    // One value slot per addressable bit position (EnumSetInfo.TotalBits), indexed by the enum's
    // underlying value. Occupancy lives in a parallel bit vector so default(TValue) is a valid
    // stored value rather than a sentinel for "absent".
    private readonly TValue?[] _values;
    private readonly ulong[] _occupied;
    private int _count;

    // Incremented on every structural mutation so active enumerators can detect concurrent
    // modification and throw, matching BCL semantics. A pure value overwrite of an existing key
    // does not bump it (matching Dictionary<,>).
    private int _version;

    /// <summary>
    /// Initializes a new, empty <see cref="EnumMap{TEnum, TValue}"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// <typeparamref name="TEnum"/> declares a negative underlying value, or its maximum
    /// underlying value exceeds the supported range (a sparse / <c>[Flags]</c> enum) — a dense
    /// array is unsuitable; use <see cref="CelerityDictionary{TKey, TValue, THasher}"/> instead.
    /// </exception>
    public EnumMap()
    {
        string? reason = EnumSetInfo<TEnum>.UnsupportedReason;
        if (reason is not null)
            throw new NotSupportedException(reason);

        int totalBits = EnumSetInfo<TEnum>.TotalBits;
        int wordCount = EnumSetInfo<TEnum>.WordCount;
        _values = totalBits == 0 ? Array.Empty<TValue?>() : new TValue?[totalBits];
        _occupied = wordCount == 0 ? Array.Empty<ulong>() : new ulong[wordCount];
    }

    /// <summary>
    /// Initializes a new <see cref="EnumMap{TEnum, TValue}"/> that contains the key/value pairs
    /// copied from the specified <paramref name="source"/>.
    /// </summary>
    /// <param name="source">
    /// The collection whose key/value pairs are copied into the new map.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="NotSupportedException">
    /// <typeparamref name="TEnum"/> is not a supported enum (see <see cref="EnumMap()"/>).
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> contains one or more duplicate keys.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="source"/> contains a key outside the supported range.
    /// </exception>
    public EnumMap(IEnumerable<KeyValuePair<TEnum, TValue>> source)
        : this()
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is EnumMap<TEnum, TValue> other)
        {
            // Same TEnum ⇒ identical layout: copy both arrays wholesale.
            Array.Copy(other._values, _values, _values.Length);
            Array.Copy(other._occupied, _occupied, _occupied.Length);
            _count = other._count;
            return;
        }

        foreach (KeyValuePair<TEnum, TValue> kvp in source)
            Add(kvp.Key, kvp.Value);
    }

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
    /// <paramref name="key"/> is outside the enum's supported range.
    /// </exception>
    public TValue this[TEnum key]
    {
        get
        {
            if (!TryGetBit(key, out int bit) || !IsOccupied(bit))
                throw new KeyNotFoundException($"Key {key} not found.");

            return _values[bit]!;
        }
        set
        {
            if (!TryGetBit(key, out int bit))
                throw new ArgumentOutOfRangeException(nameof(key), key,
                    $"The value is outside the range supported by EnumMap<{typeof(TEnum).Name}, …>.");

            _values[bit] = value;
            if (!SetOccupied(bit))
            {
                // Newly occupied slot — a structural change.
                _count++;
                _version++;
            }
        }
    }

    /// <summary>
    /// Determines whether the specified key is present in the map.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns>
    /// <c>true</c> if the key is found; otherwise <c>false</c> (including for a key outside the
    /// enum's supported range).
    /// </returns>
    public bool ContainsKey(TEnum key)
        => TryGetBit(key, out int bit) && IsOccupied(bit);

    /// <summary>
    /// Determines whether the map contains the specified value.
    /// </summary>
    /// <param name="value">
    /// The value to locate. Equality is determined via <see cref="EqualityComparer{T}.Default"/>,
    /// matching BCL <see cref="Dictionary{TKey, TValue}.ContainsValue(TValue)"/> semantics.
    /// </param>
    /// <returns><c>true</c> if a matching value is found; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// This operation is <c>O(n)</c> in the map's count: it walks the occupancy vector and tests
    /// only occupied value slots.
    /// </remarks>
    public bool ContainsValue(TValue? value)
    {
        if (_count == 0)
            return false;

        var comparer = EqualityComparer<TValue?>.Default;
        ulong[] occupied = _occupied;
        TValue?[] values = _values;

        for (int w = 0; w < occupied.Length; w++)
        {
            ulong bits = occupied[w];
            while (bits != 0)
            {
                int tz = BitOperations.TrailingZeroCount(bits);
                bits &= bits - 1; // clear the lowest set bit
                if (comparer.Equals(values[(w << 6) + tz], value))
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
    /// When this method returns, contains the value associated with <paramref name="key"/> if
    /// found; otherwise the default value of <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><c>true</c> if the key was found; otherwise <c>false</c>.</returns>
    public bool TryGetValue(TEnum key, out TValue? value)
    {
        if (TryGetBit(key, out int bit) && IsOccupied(bit))
        {
            value = _values[bit];
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Adds the specified key and value to the map.
    /// Throws <see cref="ArgumentException"/> if the key already exists.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <exception cref="ArgumentException">
    /// An element with the same <paramref name="key"/> already exists.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="key"/> is outside the enum's supported range.
    /// </exception>
    public void Add(TEnum key, TValue value)
    {
        if (!TryAdd(key, value))
            throw new ArgumentException($"An element with key {key} already exists.", nameof(key));
    }

    /// <summary>
    /// Attempts to add the specified key and value to the map.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <returns>
    /// <c>true</c> if the key/value pair was added; <c>false</c> if the key already exists (the
    /// map is unchanged).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="key"/> is outside the enum's supported range.
    /// </exception>
    public bool TryAdd(TEnum key, TValue value)
    {
        if (!TryGetBit(key, out int bit))
            throw new ArgumentOutOfRangeException(nameof(key), key,
                $"The value is outside the range supported by EnumMap<{typeof(TEnum).Name}, …>.");

        if (IsOccupied(bit))
            return false;

        _values[bit] = value;
        SetOccupied(bit);
        _count++;
        _version++;
        return true;
    }

    /// <summary>
    /// Removes the value with the specified key from the map.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <returns>
    /// <c>true</c> if the item was removed; otherwise <c>false</c> (including if
    /// <paramref name="key"/> was not present or is outside the enum's supported range).
    /// </returns>
    public bool Remove(TEnum key) => Remove(key, out _);

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
    /// <c>true</c> if the item was removed; otherwise <c>false</c> (including if
    /// <paramref name="key"/> was not present or is outside the enum's supported range).
    /// </returns>
    public bool Remove(TEnum key, out TValue? value)
    {
        if (!TryGetBit(key, out int bit) || !IsOccupied(bit))
        {
            value = default;
            return false;
        }

        value = _values[bit];
        _values[bit] = default; // release any reference so it can be collected
        ClearOccupied(bit);
        _count--;
        _version++;
        return true;
    }

    /// <summary>
    /// Removes all keys and values from the map. The backing storage is preserved.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        Array.Clear(_values, 0, _values.Length);
        Array.Clear(_occupied, 0, _occupied.Length);
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Returns an allocation-free enumerator that yields each key/value pair in <b>ascending
    /// underlying-value order</b>. If the map is modified during enumeration,
    /// <see cref="Enumerator.MoveNext"/> throws <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over this map.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    /// <summary>
    /// Gets an enumerable view over the keys in the map, in ascending underlying-value order. The
    /// view is a lightweight struct and iterating it does not allocate.
    /// </summary>
    public KeyCollection Keys => new KeyCollection(this);

    /// <summary>
    /// Gets an enumerable view over the values in the map, ordered by ascending key. The view is a
    /// lightweight struct and iterating it does not allocate.
    /// </summary>
    public ValueCollection Values => new ValueCollection(this);

    // ── Occupancy bit-vector helpers ──────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsOccupied(int bit)
        => (_occupied[bit >> 6] & (1UL << (bit & 63))) != 0;

    // Sets the occupancy bit; returns whether it was already set (so callers can tell a new entry
    // from an overwrite without a second read).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool SetOccupied(int bit)
    {
        ref ulong word = ref _occupied[bit >> 6];
        ulong mask = 1UL << (bit & 63);
        bool was = (word & mask) != 0;
        word |= mask;
        return was;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearOccupied(int bit)
        => _occupied[bit >> 6] &= ~(1UL << (bit & 63));

    // Reads the enum's underlying bits at its natural width (unsigned, so every value maps to a
    // non-negative index) and range-checks against the backing store. The switch is on a
    // per-instantiation JIT constant, so it folds to a single read. Returns false for any value
    // outside [0, TotalBits) — negatives read as huge and fail the unsigned bound too. Mirrors
    // EnumSet<TEnum>.TryGetBit; the two types are self-contained by design.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetBit(TEnum value, out int index)
    {
        long v = Unsafe.SizeOf<TEnum>() switch
        {
            1 => Unsafe.As<TEnum, byte>(ref value),
            2 => Unsafe.As<TEnum, ushort>(ref value),
            4 => Unsafe.As<TEnum, uint>(ref value),
            _ => unchecked((long)Unsafe.As<TEnum, ulong>(ref value)),
        };

        if ((ulong)v < (ulong)(uint)EnumSetInfo<TEnum>.TotalBits)
        {
            index = (int)v;
            return true;
        }

        index = 0;
        return false;
    }

    // Reconstructs the enum value from a bit index in [0, TotalBits). The switch is a
    // per-instantiation JIT constant. Mirrors EnumSet<TEnum>.FromBit.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TEnum FromBit(int index)
    {
        switch (Unsafe.SizeOf<TEnum>())
        {
            case 1: { byte b = (byte)index; return Unsafe.As<byte, TEnum>(ref b); }
            case 2: { ushort s = (ushort)index; return Unsafe.As<ushort, TEnum>(ref s); }
            case 4: { uint i = (uint)index; return Unsafe.As<uint, TEnum>(ref i); }
            default: { ulong l = (uint)index; return Unsafe.As<ulong, TEnum>(ref l); }
        }
    }

    // ── IReadOnlyDictionary<TEnum, TValue?> explicit members ──────────────────
    // The primary (non-interface) surface already covers the contract; these forwarders only
    // widen those members to the boxed IEnumerable<T> / IEnumerator<T> shapes the interface
    // requires, so users who prefer BCL ergonomics (LINQ, DI over IReadOnlyDictionary<,>) keep
    // working without losing the zero-allocation direct foreach.
    TValue? IReadOnlyDictionary<TEnum, TValue?>.this[TEnum key] => this[key];

    IEnumerable<TEnum> IReadOnlyDictionary<TEnum, TValue?>.Keys => Keys;

    IEnumerable<TValue?> IReadOnlyDictionary<TEnum, TValue?>.Values => Values;

    IEnumerator<KeyValuePair<TEnum, TValue?>> IEnumerable<KeyValuePair<TEnum, TValue?>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// A struct enumerator over an <see cref="EnumMap{TEnum, TValue}"/>. Because it is a struct,
    /// iterating it via <c>foreach</c> avoids the allocation a compiler-generated
    /// <see cref="IEnumerator{T}"/> would incur. Pairs are yielded in ascending underlying-value
    /// order.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<TEnum, TValue?>>
    {
        private readonly EnumMap<TEnum, TValue> _map;
        private readonly int _version;
        private int _wordIndex;
        private ulong _remaining;
        private KeyValuePair<TEnum, TValue?> _current;

        internal Enumerator(EnumMap<TEnum, TValue> map)
        {
            _map = map;
            _version = map._version;
            _wordIndex = -1;
            _remaining = 0;
            _current = default;
        }

        /// <summary>
        /// Gets the key/value pair at the current position of the enumerator.
        /// </summary>
        public KeyValuePair<TEnum, TValue?> Current => _current;

        object IEnumerator.Current => _current;

        /// <summary>
        /// Advances the enumerator to the next key/value pair.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the enumerator advanced to a new entry; <c>false</c> if it has passed
        /// the end of the map.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the map was modified since the enumerator was created.
        /// </exception>
        public bool MoveNext()
        {
            if (_version != _map._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            ulong[] occupied = _map._occupied;
            while (true)
            {
                if (_remaining != 0)
                {
                    int tz = BitOperations.TrailingZeroCount(_remaining);
                    _remaining &= _remaining - 1; // clear the lowest set bit
                    int bit = (_wordIndex << 6) + tz;
                    _current = new KeyValuePair<TEnum, TValue?>(FromBit(bit), _map._values[bit]);
                    return true;
                }

                _wordIndex++;
                if (_wordIndex >= occupied.Length)
                {
                    _current = default;
                    return false;
                }

                _remaining = occupied[_wordIndex];
            }
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

            _wordIndex = -1;
            _remaining = 0;
            _current = default;
        }

        /// <summary>
        /// Releases any resources held by the enumerator. No-op for this type.
        /// </summary>
        public void Dispose() { }
    }

    /// <summary>
    /// A struct enumerable view over the keys of an <see cref="EnumMap{TEnum, TValue}"/>. Iterating
    /// it does not allocate; passing it through <see cref="IEnumerable{T}"/> will box the
    /// enumerator and is therefore not zero-allocation.
    /// </summary>
    public readonly struct KeyCollection : IEnumerable<TEnum>
    {
        private readonly EnumMap<TEnum, TValue> _map;

        internal KeyCollection(EnumMap<TEnum, TValue> map) => _map = map;

        /// <summary>
        /// Gets the number of keys in the view (equal to the map's count).
        /// </summary>
        public int Count => _map._count;

        /// <summary>
        /// Returns an allocation-free struct enumerator over the keys, in ascending order.
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(_map);

        IEnumerator<TEnum> IEnumerable<TEnum>.GetEnumerator() => new Enumerator(_map);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_map);

        /// <summary>
        /// A struct enumerator over the keys of an <see cref="EnumMap{TEnum, TValue}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TEnum>
        {
            private EnumMap<TEnum, TValue>.Enumerator _inner;

            internal Enumerator(EnumMap<TEnum, TValue> map) => _inner = map.GetEnumerator();

            /// <summary>Gets the current key.</summary>
            public TEnum Current => _inner.Current.Key;

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
    /// A struct enumerable view over the values of an <see cref="EnumMap{TEnum, TValue}"/>.
    /// Iterating it does not allocate; passing it through <see cref="IEnumerable{T}"/> will box the
    /// enumerator and is therefore not zero-allocation.
    /// </summary>
    public readonly struct ValueCollection : IEnumerable<TValue?>
    {
        private readonly EnumMap<TEnum, TValue> _map;

        internal ValueCollection(EnumMap<TEnum, TValue> map) => _map = map;

        /// <summary>
        /// Gets the number of values in the view (equal to the map's count).
        /// </summary>
        public int Count => _map._count;

        /// <summary>
        /// Returns an allocation-free struct enumerator over the values, ordered by ascending key.
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(_map);

        IEnumerator<TValue?> IEnumerable<TValue?>.GetEnumerator() => new Enumerator(_map);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_map);

        /// <summary>
        /// A struct enumerator over the values of an <see cref="EnumMap{TEnum, TValue}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TValue?>
        {
            private EnumMap<TEnum, TValue>.Enumerator _inner;

            internal Enumerator(EnumMap<TEnum, TValue> map) => _inner = map.GetEnumerator();

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
}
