namespace Celerity.Collections;

public class IntDictionary<TValue>
{
    private const int DEFAULT_CAPACITY = 16;
    private const float LOAD_FACTOR = 0.75f;
    private const int EMPTY_KEY = 0; 
    private TValue EMPTY_VALUE = default;

    private int[] keys;
    private TValue[] values;
    private int count;
    private int threshold;

    public IntDictionary(int capacity = DEFAULT_CAPACITY)
    {
        int size = NextPowerOfTwo(capacity);
        keys = new int[size];
        values = new TValue[size];
        threshold = (int)(size * LOAD_FACTOR);
        count = 0;
    }

    /// <summary>
    /// 
    /// </summary>
    public int Count => count;

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

            return values[index];
        }
        set
        {
            if (count >= threshold)
            {
                Resize();
            }

            int index = ProbeForInsert(key);
            bool isNewEntry = keys[index] == EMPTY_KEY;

            keys[index] = key;
            values[index] = value;

            if (isNewEntry)
                count++;
        }
    }

    public bool ContainsKey(int key) => ProbeForKey(key) >= 0;

    public bool Remove(int key)
    {
        int index = ProbeForKey(key);
        if (index < 0)
            return false;

        keys[index] = EMPTY_KEY;
        values[index] = EMPTY_VALUE;
        count--;

        RehashAfterRemove(index);
        return true;
    }

    private int ProbeForInsert(int key)
    {
        int size = keys.Length;
        int index = Hash(key) & (size - 1);

        while (keys[index] != EMPTY_KEY && keys[index] != key)
        {
            index = (index + 1) & (size - 1);
        }

        return index;
    }

    private int ProbeForKey(int key)
    {
        int size = keys.Length;
        int index = Hash(key) & (size - 1);

        while (keys[index] != EMPTY_KEY)
        {
            if (keys[index] == key)
                return index;
            index = (index + 1) & (size - 1);
        }

        return -1;
    }

    private void Resize()
    {
        int newSize = keys.Length * 2;
        int[] oldKeys = keys;
        TValue[] oldValues = values;

        keys = new int[newSize];
        values = new TValue[newSize];
        threshold = (int)(newSize * LOAD_FACTOR);
        count = 0;

        for (int i = 0; i < oldKeys.Length; i++)
        {
            if (oldKeys[i] != EMPTY_KEY)
            {
                this[oldKeys[i]] = oldValues[i]; // Use indexer to reinsert
            }
        }
    }

    private void RehashAfterRemove(int startIndex)
    {
        int size = keys.Length;
        int index = (startIndex + 1) & (size - 1);

        while (keys[index] != EMPTY_KEY)
        {
            int rehashedKey = keys[index];
            TValue rehashedValue = values[index];

            keys[index] = EMPTY_KEY;
            values[index] = EMPTY_VALUE;
            count--;

            this[rehashedKey] = rehashedValue; // Reinserting with indexer
            index = (index + 1) & (size - 1);
        }
    }

    private static int Hash(int key) => key ^ (key >> 16);

    private static int NextPowerOfTwo(int n)
    {
        int power = 1;
        while (power < n) power *= 2;
        return power;
    }
}
