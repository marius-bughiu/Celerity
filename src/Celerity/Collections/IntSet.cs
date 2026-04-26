using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A high-performance set of <see cref="int"/> values, using
/// <see cref="Int32WangNaiveHasher"/> by default.
/// </summary>
public class IntSet : IntSet<Int32WangNaiveHasher>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IntSet"/> class
    /// with an optional capacity and load factor.
    /// </summary>
    /// <param name="capacity">
    /// The initial capacity of the set. Automatically rounded up
    /// to the next power of two.
    /// </param>
    /// <param name="loadFactor">
    /// Determines the maximum ratio of count to capacity before resizing.
    /// </param>
    public IntSet(int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
        : base(capacity, loadFactor)
    {
    }
}

/// <summary>
/// A high-performance set of <see cref="int"/> values, parameterized on a
/// custom <see cref="IHashProvider{T}"/> implementation.
/// </summary>
/// <typeparam name="THasher">
/// The hasher used to compute element hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class IntSet<THasher> where THasher : struct, IHashProvider<int>
{
    /// <summary>
    /// The default initial capacity of the set if no capacity is specified.
    /// </summary>
    protected const int DEFAULT_CAPACITY = 16;

    /// <summary>
    /// The default load factor of the set if no load factor is specified.
    /// </summary>
    protected const float DEFAULT_LOAD_FACTOR = 0.75f;

    private const int EMPTY_SLOT = 0;

    private int _count = 0;
    private int[] _slots;
    private readonly float _loadFactor;
    private int _threshold;
    private readonly THasher _hasher;

    // The value 0 collides with EMPTY_SLOT, so it's stored out-of-band
    // in a dedicated flag. _count includes this entry when _hasZero is true.
    private bool _hasZero;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntSet{THasher}"/> class
    /// using the specified capacity and load factor.
    /// </summary>
    /// <param name="capacity">
    /// The initial capacity of the set. Automatically rounded up
    /// to the next power of two.
    /// </param>
    /// <param name="loadFactor">
    /// Determines the maximum ratio of count to capacity before resizing.
    /// </param>
    public IntSet(
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (loadFactor <= 0f || loadFactor >= 1f)
            throw new ArgumentOutOfRangeException(nameof(loadFactor), loadFactor, "Load factor must be between 0 (exclusive) and 1 (exclusive).");

        int size = FastUtils.NextPowerOfTwo(capacity);

        _slots = new int[size];
        _loadFactor = loadFactor;
        _threshold = (int)(size * _loadFactor);
        _hasher = default;
    }

    /// <summary>
    /// Gets the number of elements contained in the set.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Adds the specified element to the set.
    /// Throws <see cref="ArgumentException"/> if the element already exists.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="item"/> already exists in the set.
    /// </exception>
    public void Add(int item)
    {
        if (!TryAdd(item))
            throw new ArgumentException($"The element {item} already exists in the set.", nameof(item));
    }

    /// <summary>
    /// Attempts to add the specified element to the set.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <returns>
    /// <c>true</c> if the element was added successfully;
    /// <c>false</c> if the element already exists (the set is unchanged).
    /// </returns>
    public bool TryAdd(int item)
    {
        if (item == EMPTY_SLOT)
        {
            if (_hasZero)
                return false;
            _hasZero = true;
            _count++;
            return true;
        }

        // Single probe: walk the probe chain once and either spot the existing
        // entry (return false) or land on an empty slot and insert in place.
        // Avoids the double walk of `if (Contains(item)) ...; InsertNonZero(item);`.
        if (_count >= _threshold)
            Resize();

        int size = _slots.Length;
        int index = _hasher.Hash(item) & (size - 1);

        while (_slots[index] != EMPTY_SLOT)
        {
            if (_slots[index] == item)
                return false;
            index = (index + 1) & (size - 1);
        }

        _slots[index] = item;
        _count++;
        return true;
    }

    /// <summary>
    /// Determines whether the set contains the specified element.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    /// <returns><c>true</c> if the element is found; otherwise, <c>false</c>.</returns>
    public bool Contains(int item)
    {
        if (item == EMPTY_SLOT)
            return _hasZero;

        return ProbeForItem(item) >= 0;
    }

    /// <summary>
    /// Removes the specified element from the set.
    /// </summary>
    /// <param name="item">The element to remove.</param>
    /// <returns>
    /// <c>true</c> if the element was successfully removed; otherwise, <c>false</c>.
    /// Also returns <c>false</c> if the element was not found.
    /// </returns>
    public bool Remove(int item)
    {
        if (item == EMPTY_SLOT)
        {
            if (!_hasZero)
                return false;
            _hasZero = false;
            _count--;
            return true;
        }

        int index = ProbeForItem(item);
        if (index < 0)
            return false;

        _slots[index] = EMPTY_SLOT;
        _count--;

        RehashAfterRemove(index);
        return true;
    }

    /// <summary>
    /// Removes all elements from the set. The underlying capacity is preserved.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        Array.Clear(_slots, 0, _slots.Length);
        _hasZero = false;
        _count = 0;
    }

    private void InsertNonZero(int item)
    {
        if (_count >= _threshold)
        {
            Resize();
        }

        int size = _slots.Length;
        int index = _hasher.Hash(item) & (size - 1);

        while (_slots[index] != EMPTY_SLOT && _slots[index] != item)
        {
            index = (index + 1) & (size - 1);
        }

        bool isNewEntry = _slots[index] == EMPTY_SLOT;
        _slots[index] = item;

        if (isNewEntry)
            _count++;
    }

    private int ProbeForItem(int item)
    {
        int size = _slots.Length;
        int index = _hasher.Hash(item) & (size - 1);

        while (_slots[index] != EMPTY_SLOT)
        {
            if (_slots[index] == item)
                return index;
            index = (index + 1) & (size - 1);
        }

        return -1;
    }

    private void Resize()
    {
        int newSize = _slots.Length * 2;
        int[] oldSlots = _slots;

        _slots = new int[newSize];
        _threshold = (int)(newSize * _loadFactor);

        // The zero entry lives out-of-band, so preserve its contribution to _count.
        int carriedZero = _hasZero ? 1 : 0;
        _count = carriedZero;

        for (int i = 0; i < oldSlots.Length; i++)
        {
            if (oldSlots[i] != EMPTY_SLOT)
            {
                InsertNonZero(oldSlots[i]);
            }
        }
    }

    private void RehashAfterRemove(int startIndex)
    {
        int size = _slots.Length;
        int index = (startIndex + 1) & (size - 1);

        while (_slots[index] != EMPTY_SLOT)
        {
            int rehashedItem = _slots[index];

            _slots[index] = EMPTY_SLOT;
            _count--;

            InsertNonZero(rehashedItem);
            index = (index + 1) & (size - 1);
        }
    }
}
