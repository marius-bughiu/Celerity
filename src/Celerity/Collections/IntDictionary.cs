using Celerity.Hashing;

namespace Celerity.Collections;

public class IntDictionary<TValue> : IntDictionary<TValue, Int32WangNaiveHasher>
{
    public IntDictionary(int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
        : base()
    {

    }
}

public class IntDictionary<TValue, THasher> where THasher : struct, IHashProvider<int>
{
    protected const int DEFAULT_CAPACITY = 16;
    protected const float DEFAULT_LOAD_FACTOR = 0.75f;
    private const int EMPTY_KEY = 0;
    private readonly TValue EMPTY_VALUE = default;

    private int _count = 0;
    private int[] _keys;
    private TValue[] _values;
    private readonly float _loadFactor;
    private int _threshold;
    private readonly THasher _hasher;

    unsafe public IntDictionary(
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
    /// 
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets or sets a value associated with the given key.
    /// Throws KeyNotFoundException if key does not exist when accessed.
    /// </summary>
    public TValue this[int key]
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
            bool isNewEntry = _keys[index] == EMPTY_KEY;

            _keys[index] = key;
            _values[index] = value;

            if (isNewEntry)
                _count++;
        }
    }

    public bool ContainsKey(int key) => ProbeForKey(key) >= 0;

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

    unsafe private int ProbeForInsert(int key)
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

    unsafe private int ProbeForKey(int key)
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
            TValue rehashedValue = _values[index];

            _keys[index] = EMPTY_KEY;
            _values[index] = EMPTY_VALUE;
            _count--;

            this[rehashedKey] = rehashedValue;
            index = (index + 1) & (size - 1);
        }
    }
}
