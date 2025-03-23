using Celerity.Hashing;

namespace Celerity.Collections;

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
            int index = ProbeForKey(key);
            if (index < 0)
                throw new KeyNotFoundException($"Key {key} not found.");

            return _values[index];
        }
        set
        {
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
    public bool ContainsKey(TKey key) => ProbeForKey(key) >= 0;

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
        int index = ProbeForKey(key);
        if (index < 0)
            return false;

        _keys[index] = default(TKey);
        _values[index] = default(TValue);
        _count--;

        RehashAfterRemove(index);
        return true;
    }

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
        _count = 0;

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
            TValue rehashedValue = _values[index]!;

            _keys[index] = default;
            _values[index] = default;
            _count--;

            this[rehashedKey] = rehashedValue;
            index = (index + 1) & (size - 1);
        }
    }
}
