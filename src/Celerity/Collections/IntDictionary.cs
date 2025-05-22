using Celerity.Hashing;

namespace Celerity.Collections;

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
    { }
}

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
    private readonly TValue? EMPTY_VALUE = default;

    private int _count = 0;
    private int[] _keys;
    private TValue?[] _values;
    private readonly float _loadFactor;
    private int _threshold;
    private readonly THasher _hasher;

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
        _values = new TValue[size];
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
            int index = ProbeForKey(key);
            if (index < 0)
                throw new KeyNotFoundException($"Key {key} not found.");

            return _values[index]!;
        }
        set
        {
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
    public bool ContainsKey(int key) => ProbeForKey(key) >= 0;

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
        int index = ProbeForKey(key);
        if (index < 0)
            return false;

        _keys[index] = EMPTY_KEY;
        _values[index] = EMPTY_VALUE;
        _count--;

        RehashAfterRemove(index);
        return true;
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
        TValue[] oldValues = _values;

        _keys = new int[newSize];
        _values = new TValue[newSize];
        _threshold = (int)(newSize * _loadFactor);
        _count = 0;

        for (int i = 0; i < oldKeys.Length; i++)
        {
            if (oldKeys[i] != EMPTY_KEY)
            {
                this[oldKeys[i]] = oldValues[i];
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
