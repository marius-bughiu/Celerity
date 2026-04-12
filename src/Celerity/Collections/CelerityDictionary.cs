using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A high-performance generic dictionary, parameterized on a custom
/// <see cref="IHashProvider{T}"/> implementation.
/// </summary>
/// <typeparam name="TKey">The type of the keys.</typeparam>
/// <typeparam name="TValue">The type of the values.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute key hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class CelerityDictionary<TKey, TValue, THasher> where THasher : struct, IHashProvider<TKey>
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
    private readonly float _loadFactor;
    private int _threshold;
    private readonly THasher _hasher;

    // The key value default(TKey) (null for reference types, 0 for primitives,
    // Guid.Empty for Guid, etc.) collides with the "empty slot" sentinel used
    // during probing, so it's stored out-of-band in a dedicated slot. _count
    // includes this entry when _hasDefaultKey is true.
    private bool _hasDefaultKey;
    private TValue? _defaultKeyValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="CelerityDictionary{TKey,TValue,THasher}"/> class
    /// using the specified capacity and load factor.
    /// </summary>
    /// <param name="capacity">
    /// The initial capacity, automatically rounded to the next power of two.
    /// </param>
    /// <param name="loadFactor">
    /// The fraction of the dictionary's size that can be filled before resizing.
    /// </param>
    public CelerityDictionary(
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
        _loadFactor = loadFactor;
        _threshold = (int)(size * _loadFactor);
        _hasher = default;
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
    public TValue? this[TKey key]
    {
        get
        {
            if (IsDefaultKey(key))
            {
                if (_hasDefaultKey)
                    return _defaultKeyValue;
                throw new KeyNotFoundException($"Key {key} not found.");
            }

            int index = ProbeForKey(key);
            if (index < 0)
                throw new KeyNotFoundException($"Key {key} not found.");

            return _values[index];
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
                return;
            }

            if (_count >= _threshold)
            {
                Resize();
            }

            int index = ProbeForInsert(key);
            bool isNewEntry = EqualityComparer<TKey>.Default.Equals(_keys[index], default(TKey));

            _keys[index] = key;
            _values[index] = value;

            if (isNewEntry)
                _count++;
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

        value = _values[index];
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
    public bool Remove(TKey key)
    {
        if (IsDefaultKey(key))
        {
            if (!_hasDefaultKey)
                return false;
            _hasDefaultKey = false;
            _defaultKeyValue = default;
            _count--;
            return true;
        }

        int index = ProbeForKey(key);
        if (index < 0)
            return false;

        _keys[index] = default(TKey);
        _values[index] = default(TValue);
        _count--;

        RehashAfterRemove(index);
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
            return true;
        }

        if (ContainsKey(key))
            return false;

        this[key] = value;
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
        _hasDefaultKey = false;
        _defaultKeyValue = default;
        _count = 0;
    }

    private static bool IsDefaultKey(TKey key) =>
        EqualityComparer<TKey>.Default.Equals(key, default(TKey));

    private int ProbeForInsert(TKey key)
    {
        int size = _keys.Length;

        // Only works when size is a power of two
        int index = _hasher.Hash(key) & (size - 1);

        while (!EqualityComparer<TKey>.Default.Equals(_keys[index], default(TKey)) &&
               !EqualityComparer<TKey>.Default.Equals(_keys[index], key))
        {
            index = (index + 1) & (size - 1);
        }

        return index;
    }

    private int ProbeForKey(TKey key)
    {
        int size = _keys.Length;
        int index = _hasher.Hash(key) & (size - 1);

        while (!EqualityComparer<TKey>.Default.Equals(_keys[index], default(TKey)))
        {
            if (EqualityComparer<TKey>.Default.Equals(_keys[index], key))
                return index;
            index = (index + 1) & (size - 1);
        }

        return -1;
    }

    private void Resize()
    {
        int newSize = _keys.Length * 2;
        TKey?[] oldKeys = _keys;
        TValue?[] oldValues = _values;

        _keys = new TKey?[newSize];
        _values = new TValue?[newSize];
        _threshold = (int)(newSize * _loadFactor);

        // The default-key entry lives out-of-band and is not in the arrays,
        // so preserve its contribution to _count across the rehash.
        int carriedDefaultKey = _hasDefaultKey ? 1 : 0;
        _count = carriedDefaultKey;

        for (int i = 0; i < oldKeys.Length; i++)
        {
            if (!EqualityComparer<TKey>.Default.Equals(oldKeys[i], default(TKey)))
            {
                this[oldKeys[i]!] = oldValues[i];
            }
        }
    }

    private void RehashAfterRemove(int startIndex)
    {
        int size = _keys.Length;
        int index = (startIndex + 1) & (size - 1);

        while (!EqualityComparer<TKey>.Default.Equals(_keys[index], default))
        {
            TKey rehashedKey = _keys[index]!;
            TValue? rehashedValue = _values[index];

            _keys[index] = default;
            _values[index] = default;
            _count--;

            this[rehashedKey] = rehashedValue;
            index = (index + 1) & (size - 1);
        }
    }
}
