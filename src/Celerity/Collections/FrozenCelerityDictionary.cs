using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A build-once, read-many dictionary keyed by <see cref="string"/> using
/// <see cref="StringFnV1AHasher"/> by default. Supply a different string hasher via
/// the <see cref="FrozenCelerityDictionary{TValue, THasher}"/> generic overload when
/// your keys favour a different hash (e.g. <see cref="StringFnV1AFullHasher"/> for
/// non-ASCII keys, or one of the strong hashers for adversarial keys).
/// </summary>
/// <typeparam name="TValue">The type of the stored values.</typeparam>
public sealed class FrozenCelerityDictionary<TValue>
    : FrozenCelerityDictionary<TValue, StringFnV1AHasher>
{
    /// <summary>
    /// Initializes a new <see cref="FrozenCelerityDictionary{TValue}"/> from the
    /// specified key/value pairs, freezing the contents at construction.
    /// </summary>
    /// <param name="source">
    /// The key/value pairs to freeze. See
    /// <see cref="FrozenCelerityDictionary{TValue, THasher}(IEnumerable{KeyValuePair{string, TValue}})"/>
    /// for the duplicate-key and <c>null</c>-key contract.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> contains one or more duplicate keys.</exception>
    public FrozenCelerityDictionary(IEnumerable<KeyValuePair<string, TValue>> source)
        : base(source)
    {
    }
}

/// <summary>
/// A build-once, read-many dictionary for <see cref="string"/> keys, in the spirit
/// of <see cref="System.Collections.Frozen.FrozenDictionary{TKey, TValue}"/> but
/// tunable through Celerity's <see cref="IHashProvider{T}"/> so callers can pick a
/// hash function specialized for their key shape.
/// </summary>
/// <typeparam name="TValue">The type of the stored values.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute key hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
/// <remarks>
/// <para>
/// The dictionary is immutable: all key/value pairs are supplied at construction and
/// there are no mutating members. In exchange the constructor searches a small
/// parameter space (table size × a mixing seed) for a <em>perfect</em> — that is,
/// collision-free — placement of the keys. When one is found
/// (<see cref="IsPerfectlyHashed"/> is then <c>true</c>) a lookup is a single hash,
/// a single array index, and a single equality check — no probing and no probe
/// chains, the same shape as <see cref="System.Collections.Frozen.FrozenDictionary{TKey, TValue}"/>.
/// </para>
/// <para>
/// A perfect placement is impossible when two distinct keys collide on the chosen
/// hasher's raw 32-bit code (for example <c>"A"</c> and <c>"Ł"</c> under the low-byte
/// <see cref="StringFnV1AHasher"/>, which returns the same code for both), because the
/// mixing seed is a pure function of that code and so cannot separate them. In that
/// case the build falls back to an open-addressed linear-probing table
/// (<see cref="IsPerfectlyHashed"/> is <c>false</c>); lookups remain correct — the
/// equality check disambiguates the colliding keys — they simply cost a short probe
/// instead of a single index. Either way the result is always correct; supply a
/// full-width or strong hasher (<see cref="StringFnV1AFullHasher"/>,
/// <see cref="StringMurmur3Hasher"/>, …) if you want the perfect fast path for keys
/// the default collides.
/// </para>
/// <para>
/// The <c>null</c> key is stored out-of-band (the hasher is never invoked with
/// <c>null</c>), exactly as the mutable Celerity dictionaries handle
/// <c>default(TKey)</c>, so it never collides with the empty-slot sentinel. The empty
/// string <c>""</c> is an ordinary key.
/// </para>
/// </remarks>
public class FrozenCelerityDictionary<TValue, THasher>
    : IReadOnlyDictionary<string, TValue?>
    where THasher : struct, IHashProvider<string>
{
    // Number of distinct mixing seeds tried per candidate table size before giving
    // up on that size and trying the next-larger one. The search is one-time build
    // work, so a generous budget is cheap insurance for the single-probe fast path.
    private const int SEED_BUDGET = 512;

    // Largest power-of-two multiple of NextPowerOfTwo(n) the perfect-hash search will
    // grow the table to before falling back. 8 means up to 8× the minimum table size.
    private const int MAX_SIZE_MULTIPLIER = 8;

    private readonly string?[] _keys;
    private readonly TValue?[] _values;
    private readonly int _mask;
    private readonly int _seed;
    private readonly bool _isPerfect;
    private readonly THasher _hasher;

    private readonly int _count;

    // The null key collides with the empty-slot sentinel (a null array slot), so it
    // is stored out-of-band. _count includes this entry when _hasNullKey is true.
    private readonly bool _hasNullKey;
    private readonly TValue? _nullKeyValue;

    /// <summary>
    /// Initializes a new <see cref="FrozenCelerityDictionary{TValue, THasher}"/> from
    /// the specified key/value pairs, freezing the contents at construction.
    /// </summary>
    /// <param name="source">
    /// The key/value pairs to freeze. A single <c>null</c> key is allowed and stored
    /// out-of-band. Duplicate keys (including a duplicate <c>null</c> key) are
    /// rejected, matching the construction contract of the mutable Celerity
    /// dictionaries and BCL <see cref="System.Collections.Frozen.FrozenDictionary{TKey, TValue}"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="source"/> contains one or more duplicate keys.</exception>
    public FrozenCelerityDictionary(IEnumerable<KeyValuePair<string, TValue>> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _hasher = default;

        // Materialize and validate. The null key is split off out-of-band; the rest
        // are de-duplicated (ordinal) so the frozen table never has to.
        var keyList = new List<string>(source is ICollection<KeyValuePair<string, TValue>> c ? c.Count : 0);
        var valueList = new List<TValue>(keyList.Capacity);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, TValue> kvp in source)
        {
            if (kvp.Key is null)
            {
                if (_hasNullKey)
                    throw new ArgumentException("An element with the null key already exists.", nameof(source));
                _hasNullKey = true;
                _nullKeyValue = kvp.Value;
                continue;
            }

            if (!seen.Add(kvp.Key))
                throw new ArgumentException($"An element with key {kvp.Key} already exists.", nameof(source));

            keyList.Add(kvp.Key);
            valueList.Add(kvp.Value);
        }

        int n = keyList.Count;
        _count = n + (_hasNullKey ? 1 : 0);

        // Precompute the raw hash code of every key once; the perfect-hash search
        // re-mixes these with each candidate seed rather than re-hashing the strings.
        int[] baseHashes = new int[n];
        for (int i = 0; i < n; i++)
            baseHashes[i] = _hasher.Hash(keyList[i]);

        if (TryBuildPerfect(keyList, valueList, baseHashes, out _keys, out _values, out _mask, out _seed))
        {
            _isPerfect = true;
        }
        else
        {
            BuildFallback(keyList, valueList, baseHashes, out _keys, out _values, out _mask);
            _seed = 0;
            _isPerfect = false;
        }
    }

    /// <summary>
    /// Gets the number of key/value pairs in the dictionary (including the
    /// out-of-band <c>null</c>-key entry, if present).
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets a value indicating whether the build found a collision-free (perfect)
    /// placement, so lookups take the single-probe fast path. <c>false</c> means the
    /// dictionary fell back to linear probing because two distinct keys share the
    /// chosen hasher's raw hash code; lookups are still correct, just not single-probe.
    /// </summary>
    public bool IsPerfectlyHashed => _isPerfect;

    /// <summary>
    /// Gets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the value to get.</param>
    /// <returns>The value associated with <paramref name="key"/>.</returns>
    /// <exception cref="KeyNotFoundException"><paramref name="key"/> is not present.</exception>
    public TValue this[string key]
    {
        get
        {
            if (key is null)
            {
                if (_hasNullKey)
                    return _nullKeyValue!;
                throw new KeyNotFoundException("The null key was not found.");
            }

            int index = FindSlot(key);
            if (index < 0)
                throw new KeyNotFoundException($"Key {key} not found.");

            return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_values), (nint)(uint)index)!;
        }
    }

    /// <summary>
    /// Determines whether the specified key is present in the dictionary.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns><c>true</c> if the key is found; otherwise, <c>false</c>.</returns>
    public bool ContainsKey(string key)
    {
        if (key is null)
            return _hasNullKey;

        return FindSlot(key) >= 0;
    }

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">
    /// When this method returns, contains the value associated with
    /// <paramref name="key"/> if found; otherwise the default value of
    /// <typeparamref name="TValue"/>.
    /// </param>
    /// <returns><c>true</c> if the key was found; otherwise, <c>false</c>.</returns>
    public bool TryGetValue(string key, out TValue? value)
    {
        if (key is null)
        {
            if (_hasNullKey)
            {
                value = _nullKeyValue;
                return true;
            }
            value = default;
            return false;
        }

        int index = FindSlot(key);
        if (index < 0)
        {
            value = default;
            return false;
        }

        value = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_values), (nint)(uint)index);
        return true;
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
    /// This operation is <c>O(n)</c> in the dictionary's count: it scans the frozen
    /// table (skipping empty slots) and, when present, the out-of-band null-key slot.
    /// </remarks>
    public bool ContainsValue(TValue? value)
    {
        var valueComparer = EqualityComparer<TValue?>.Default;

        if (_hasNullKey && valueComparer.Equals(_nullKeyValue, value))
            return true;

        string?[] keys = _keys;
        TValue?[] values = _values;
        ref string? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        ref TValue? valuesRef = ref MemoryMarshal.GetArrayDataReference(values);
        int length = keys.Length;
        for (int i = 0; i < length; i++)
        {
            if (Unsafe.Add(ref keysRef, (nint)(uint)i) is not null &&
                valueComparer.Equals(Unsafe.Add(ref valuesRef, (nint)(uint)i), value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns an allocation-free enumerator that yields each key/value pair stored
    /// in the dictionary. The enumeration order is unspecified and may change across
    /// versions; do not rely on it. The out-of-band <c>null</c>-key entry (if present)
    /// is yielded first.
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

    // ── Perfect-hash construction ─────────────────────────────────────────────

    // Integer finalizer used to map a key's raw hash code (combined with the search
    // seed) to a table slot. It must be the SAME mapping at build and lookup time.
    // Based on the "lowbias32" mix; the seed is folded in so different seeds produce
    // different — but each individually well-spread — placements of the same keys.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Mix(uint hash, int seed)
    {
        uint h = hash + unchecked((uint)seed * 0x9E3779B9u);
        h ^= h >> 16;
        h *= 0x7FEB352Du;
        h ^= h >> 15;
        h *= 0x846CA68Bu;
        h ^= h >> 16;
        return h;
    }

    private bool TryBuildPerfect(
        List<string> keyList,
        List<TValue> valueList,
        int[] baseHashes,
        out string?[] keys,
        out TValue?[] values,
        out int mask,
        out int seed)
    {
        int n = keyList.Count;
        int minSize = FastUtils.NextPowerOfTwo(n == 0 ? 1 : n);

        for (int size = minSize, mult = 1;
             mult <= MAX_SIZE_MULTIPLIER && size <= (1 << 30);
             size <<= 1, mult <<= 1)
        {
            int m = size - 1;
            for (int s = 0; s <= SEED_BUDGET; s++)
            {
                if (TryPlace(keyList, valueList, baseHashes, size, m, s, out keys, out values))
                {
                    mask = m;
                    seed = s;
                    return true;
                }
            }
        }

        keys = null!;
        values = null!;
        mask = 0;
        seed = 0;
        return false;
    }

    private static bool TryPlace(
        List<string> keyList,
        List<TValue> valueList,
        int[] baseHashes,
        int size,
        int mask,
        int seed,
        out string?[] keys,
        out TValue?[] values)
    {
        var tableKeys = new string?[size];
        var tableValues = new TValue?[size];

        int n = keyList.Count;
        for (int i = 0; i < n; i++)
        {
            int slot = (int)(Mix(unchecked((uint)baseHashes[i]), seed) & (uint)mask);
            if (tableKeys[slot] is not null)
            {
                // Collision — this (size, seed) is not perfect.
                keys = null!;
                values = null!;
                return false;
            }

            tableKeys[slot] = keyList[i];
            tableValues[slot] = valueList[i];
        }

        keys = tableKeys;
        values = tableValues;
        return true;
    }

    private static void BuildFallback(
        List<string> keyList,
        List<TValue> valueList,
        int[] baseHashes,
        out string?[] keys,
        out TValue?[] values,
        out int mask)
    {
        int n = keyList.Count;

        // Size with headroom so at least one slot stays empty — linear probing needs
        // an empty slot to terminate a miss, and to keep chains short. NextPowerOfTwo
        // returns a power of two strictly greater than n for every constructible size
        // (it caps at 2^30, the ceiling a frozen dictionary could ever reach), so the
        // table always has a vacant slot.
        int size = FastUtils.NextPowerOfTwo(n + 1);

        int m = size - 1;
        var tableKeys = new string?[size];
        var tableValues = new TValue?[size];

        for (int i = 0; i < n; i++)
        {
            int slot = (int)(Mix(unchecked((uint)baseHashes[i]), 0) & (uint)m);
            while (tableKeys[slot] is not null)
                slot = (slot + 1) & m;

            tableKeys[slot] = keyList[i];
            tableValues[slot] = valueList[i];
        }

        keys = tableKeys;
        values = tableValues;
        mask = m;
    }

    // Returns the slot holding <paramref name="key"/>, or -1 if absent. In perfect
    // mode this is a single index + equality check; in fallback mode it linear-probes
    // until it finds the key or hits an empty slot. The caller guarantees key != null.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindSlot(string key)
    {
        string?[] keys = _keys;
        ref string? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
        int mask = _mask;
        var comparer = EqualityComparer<string>.Default;
        int slot = (int)(Mix(unchecked((uint)_hasher.Hash(key)), _seed) & (uint)mask);

        if (_isPerfect)
        {
            string? candidate = Unsafe.Add(ref keysRef, (nint)(uint)slot);
            return candidate is not null && comparer.Equals(candidate, key) ? slot : -1;
        }

        while (true)
        {
            string? candidate = Unsafe.Add(ref keysRef, (nint)(uint)slot);
            if (candidate is null)
                return -1;
            if (comparer.Equals(candidate, key))
                return slot;
            slot = (slot + 1) & mask;
        }
    }

    /// <summary>
    /// A struct enumerator over a <see cref="FrozenCelerityDictionary{TValue, THasher}"/>.
    /// Because it is a struct, iterating it via <c>foreach</c> avoids the allocation a
    /// compiler-generated <c>IEnumerator&lt;T&gt;</c> would incur. The out-of-band
    /// <c>null</c>-key entry (if present) is yielded first. The dictionary is immutable,
    /// so the enumerator needs no concurrent-modification check.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<string, TValue?>>
    {
        private readonly FrozenCelerityDictionary<TValue, THasher> _dict;
        private int _index;
        private KeyValuePair<string, TValue?> _current;
        private State _state;

        private enum State : byte
        {
            BeforeNullKey,
            InArray,
            Done
        }

        internal Enumerator(FrozenCelerityDictionary<TValue, THasher> dict)
        {
            _dict = dict;
            _index = -1;
            _current = default;
            _state = State.BeforeNullKey;
        }

        /// <summary>
        /// Gets the key/value pair at the current position of the enumerator.
        /// </summary>
        public KeyValuePair<string, TValue?> Current => _current;

        object IEnumerator.Current => _current;

        /// <summary>
        /// Advances the enumerator to the next key/value pair.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the enumerator advanced to a new entry; <c>false</c> if it
        /// has passed the end of the dictionary.
        /// </returns>
        public bool MoveNext()
        {
            if (_state == State.BeforeNullKey)
            {
                _state = State.InArray;
                if (_dict._hasNullKey)
                {
                    _current = new KeyValuePair<string, TValue?>(null!, _dict._nullKeyValue);
                    return true;
                }
            }

            if (_state == State.InArray)
            {
                string?[] keys = _dict._keys;
                TValue?[] values = _dict._values;
                int length = keys.Length;
                ref string? keysRef = ref MemoryMarshal.GetArrayDataReference(keys);
                ref TValue? valuesRef = ref MemoryMarshal.GetArrayDataReference(values);
                while (++_index < length)
                {
                    string? key = Unsafe.Add(ref keysRef, (nint)(uint)_index);
                    if (key is not null)
                    {
                        _current = new KeyValuePair<string, TValue?>(key, Unsafe.Add(ref valuesRef, (nint)(uint)_index));
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
        public void Reset()
        {
            _index = -1;
            _current = default;
            _state = State.BeforeNullKey;
        }

        /// <summary>
        /// Releases any resources held by the enumerator. No-op for this type.
        /// </summary>
        public void Dispose() { }
    }

    /// <summary>
    /// A struct enumerable view over the keys of a
    /// <see cref="FrozenCelerityDictionary{TValue, THasher}"/>. Iterating it does not
    /// allocate; passing it through <see cref="IEnumerable{T}"/> will box the
    /// enumerator and is therefore not zero-allocation.
    /// </summary>
    public readonly struct KeyCollection : IEnumerable<string>
    {
        private readonly FrozenCelerityDictionary<TValue, THasher> _dict;

        internal KeyCollection(FrozenCelerityDictionary<TValue, THasher> dict) => _dict = dict;

        /// <summary>
        /// Gets the number of keys in the view (equal to the dictionary's count).
        /// </summary>
        public int Count => _dict._count;

        /// <summary>
        /// Returns an allocation-free struct enumerator over the keys.
        /// </summary>
        public Enumerator GetEnumerator() => new Enumerator(_dict);

        IEnumerator<string> IEnumerable<string>.GetEnumerator() => new Enumerator(_dict);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_dict);

        /// <summary>
        /// A struct enumerator over the keys of a
        /// <see cref="FrozenCelerityDictionary{TValue, THasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<string>
        {
            private FrozenCelerityDictionary<TValue, THasher>.Enumerator _inner;

            internal Enumerator(FrozenCelerityDictionary<TValue, THasher> dict) => _inner = dict.GetEnumerator();

            /// <summary>Gets the current key.</summary>
            public string Current => _inner.Current.Key;

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
    /// <see cref="FrozenCelerityDictionary{TValue, THasher}"/>. Iterating it does not
    /// allocate; passing it through <see cref="IEnumerable{T}"/> will box the
    /// enumerator and is therefore not zero-allocation.
    /// </summary>
    public readonly struct ValueCollection : IEnumerable<TValue?>
    {
        private readonly FrozenCelerityDictionary<TValue, THasher> _dict;

        internal ValueCollection(FrozenCelerityDictionary<TValue, THasher> dict) => _dict = dict;

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
        /// <see cref="FrozenCelerityDictionary{TValue, THasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TValue?>
        {
            private FrozenCelerityDictionary<TValue, THasher>.Enumerator _inner;

            internal Enumerator(FrozenCelerityDictionary<TValue, THasher> dict) => _inner = dict.GetEnumerator();

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

    // IReadOnlyDictionary<string, TValue?> explicit interface members. The primary
    // (non-interface) surface — the indexer, ContainsKey, TryGetValue, Count, the
    // struct enumerator, and the KeyCollection / ValueCollection views — already cover
    // the interface contract. These forwarders only widen those members to the boxed
    // IEnumerable<T> / IEnumerator<T> shapes the interface requires.
    TValue? IReadOnlyDictionary<string, TValue?>.this[string key] => this[key];

    IEnumerable<string> IReadOnlyDictionary<string, TValue?>.Keys => Keys;

    IEnumerable<TValue?> IReadOnlyDictionary<string, TValue?>.Values => Values;

    IEnumerator<KeyValuePair<string, TValue?>> IEnumerable<KeyValuePair<string, TValue?>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
