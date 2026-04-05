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
public class IntDictionary<TValue, THasher> where THasher : struct, IHashProvider<int>
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
        int size = FastUtils.NextPowerOfTwo(capacity);

        _keys = new int[size];
        _values = new TValue?[size];
        _loadFactor = loadFactor;
        _threshold = (int)(size * _loadFactor);
        _hasher = default;
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
    public bool Remove(int key)
    {
        if (key == EMPTY_KEY)
        {
            if (!_hasZeroKey)
                return false;
            _hasZeroKey = false;
            _zeroValue = EMPTY_VALUE;
            _count--;
            return true;
        }

        int index = ProbeForKey(key);
        if (index < 0)
            return false;

        _keys[index] = EMPTY_KEY;
        _values[index] = EMPTY_VALUE;
        _count--;

        RehashAfterRemove(index);
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
    }

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
        int[] oldKeys = _keys;
        TValue?[] oldValues = _values;

        _keys = new int[newSize];
        _values = new TValue?[newSize];
        _threshold = (int)(newSize * _loadFactor);

        // Reinsert every non-empty slot. We decrement _count for each reinsertion
        // because the indexer setter will increment it again via its isNewEntry path.
        // The zero-key entry lives out-of-band and is not in the arrays, so we
        // don't touch _hasZeroKey / _zeroValue here.
        int carriedZeroKey = _hasZeroKey ? 1 : 0;
        _count = carriedZeroKey;

        for (int i = 0; i < oldKeys.Length; i++)
        {
            if (oldKeys[i] != EMPTY_KEY)
            {
                this[oldKeys[i]] = oldValues[i]!;
            }
        }
    }

    private void RehashAfterRemove(int startIndex)
    {
        int size = _keys.Length;
        int index = (startIndex + 1) & (size - 1);

        while (_keys[index] != EMPTY_KEY)
        {
            int rehashedKey = _keys[index];
            TValue rehashedValue = _values[index]!;

            _keys[index] = EMPTY_KEY;
            _values[index] = EMPTY_VALUE;
            _count--;

            this[rehashedKey] = rehashedValue;
            index = (index + 1) & (size - 1);
        }
    }
}
