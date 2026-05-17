using System.Collections;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A high-performance generic hash set, parameterized on a custom
/// <see cref="IHashProvider{T}"/> implementation.
/// </summary>
/// <typeparam name="T">The type of elements in the set.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute element hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class CeleritySet<T, THasher> : IEnumerable<T> where THasher : struct, IHashProvider<T>
{
    /// <summary>
    /// The default initial capacity of the set if no capacity is specified.
    /// </summary>
    protected const int DEFAULT_CAPACITY = 16;

    /// <summary>
    /// The default load factor of the set if no load factor is specified.
    /// </summary>
    protected const float DEFAULT_LOAD_FACTOR = 0.75f;

    private int _count = 0;
    private T?[] _slots;
    private readonly float _loadFactor;
    private int _threshold;
    private readonly THasher _hasher;

    // The value default(T) (null for reference types, 0 for primitives,
    // Guid.Empty for Guid, etc.) collides with the "empty slot" sentinel used
    // during probing, so it's stored out-of-band. _count includes this entry
    // when _hasDefaultValue is true.
    private bool _hasDefaultValue;

    // Bumped on every entry-point structural mutation (Add / TryAdd / Remove /
    // Clear). The struct enumerator captures this on construction and re-checks
    // it from MoveNext / Reset, so any concurrent modification fast-fails with
    // InvalidOperationException — matching BCL HashSet<T> semantics.
    private int _version;

    /// <summary>
    /// Initializes a new instance of the <see cref="CeleritySet{T,THasher}"/> class
    /// using the specified capacity and load factor.
    /// </summary>
    /// <param name="capacity">
    /// The initial capacity, automatically rounded to the next power of two.
    /// </param>
    /// <param name="loadFactor">
    /// The fraction of the set's size that can be filled before resizing.
    /// </param>
    public CeleritySet(
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (loadFactor <= 0f || loadFactor >= 1f)
            throw new ArgumentOutOfRangeException(nameof(loadFactor), loadFactor, "Load factor must be between 0 (exclusive) and 1 (exclusive).");

        int size = FastUtils.NextPowerOfTwo(capacity);

        _slots = new T?[size];
        _loadFactor = loadFactor;
        _threshold = (int)(size * _loadFactor);
        _hasher = default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CeleritySet{T,THasher}"/>
    /// class containing the elements copied from the specified
    /// <paramref name="source"/>.
    /// </summary>
    /// <param name="source">
    /// The collection whose elements are copied into the new set. If
    /// <paramref name="source"/> implements <see cref="ICollection{T}"/>, its
    /// <c>Count</c> is used to size the backing storage so the initial fill
    /// avoids resize work. Duplicate elements (including duplicate
    /// <c>default(T)</c> entries) are silently deduplicated, matching BCL
    /// <see cref="HashSet{T}"/> semantics.
    /// </param>
    /// <param name="capacity">
    /// The minimum initial capacity. The final capacity is the larger of this
    /// value and the source's count, rounded up to the next power of two.
    /// </param>
    /// <param name="loadFactor">
    /// Determines the maximum ratio of count to capacity before resizing.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is <c>null</c>.
    /// </exception>
    public CeleritySet(
        IEnumerable<T> source,
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
        : this(InitialCapacityForSource(source, capacity), loadFactor)
    {
        foreach (T item in source)
        {
            TryAdd(item);
        }
    }

    // Runs as part of the chained-ctor argument expression so the null check
    // beats the primary ctor's capacity / loadFactor validation: a null source
    // must surface as ArgumentNullException, not ArgumentOutOfRangeException
    // when the user also passed an invalid loadFactor.
    private static int InitialCapacityForSource(IEnumerable<T> source, int capacity)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Math.Max(capacity, (source as ICollection<T>)?.Count ?? 0);
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
    public void Add(T item)
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
    public bool TryAdd(T item)
    {
        if (IsDefaultValue(item))
        {
            if (_hasDefaultValue)
                return false;
            _hasDefaultValue = true;
            _count++;
            _version++;
            return true;
        }

        // Single probe: walk the probe chain once and either spot the existing
        // entry (return false) or land on an empty slot and insert in place.
        // Avoids the double walk of `if (Contains(item)) ...; then insert`.
        if (_count >= _threshold)
            Resize();

        int size = _slots.Length;
        int index = _hasher.Hash(item) & (size - 1);

        while (!EqualityComparer<T>.Default.Equals(_slots[index], default(T)))
        {
            if (EqualityComparer<T>.Default.Equals(_slots[index], item))
                return false;
            index = (index + 1) & (size - 1);
        }

        _slots[index] = item;
        _count++;
        _version++;
        return true;
    }

    /// <summary>
    /// Determines whether the set contains the specified element.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    /// <returns><c>true</c> if the element is found; otherwise, <c>false</c>.</returns>
    public bool Contains(T item)
    {
        if (IsDefaultValue(item))
            return _hasDefaultValue;

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
    public bool Remove(T item)
    {
        if (IsDefaultValue(item))
        {
            if (!_hasDefaultValue)
                return false;
            _hasDefaultValue = false;
            _count--;
            _version++;
            return true;
        }

        int index = ProbeForItem(item);
        if (index < 0)
            return false;

        _slots[index] = default(T);
        _count--;

        RehashAfterRemove(index);
        _version++;
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
        _hasDefaultValue = false;
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Returns an allocation-free enumerator that yields each element stored in
    /// the set. The enumeration order is unspecified and may change across
    /// versions; do not rely on it. The out-of-band <c>default(T)</c> entry (if
    /// present) is yielded first — for reference-type elements that includes
    /// <c>null</c>. If the set is modified during enumeration,
    /// <see cref="Enumerator.MoveNext"/> throws
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over this set.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// A struct enumerator over a <see cref="CeleritySet{T,THasher}"/>. Because
    /// it is a struct, iterating it via <c>foreach</c> avoids the allocation
    /// that a compiler-generated <c>IEnumerator&lt;T&gt;</c> would incur. The
    /// out-of-band <c>default(T)</c> entry (if present) is yielded first.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly CeleritySet<T, THasher> _set;
        private readonly int _version;
        private int _index;
        private T? _current;
        private State _state;

        private enum State : byte
        {
            BeforeDefault,
            InArray,
            Done
        }

        internal Enumerator(CeleritySet<T, THasher> set)
        {
            _set = set;
            _version = set._version;
            _index = -1;
            _current = default;
            _state = State.BeforeDefault;
        }

        /// <summary>
        /// Gets the element at the current position of the enumerator.
        /// </summary>
        public T Current => _current!;

        object? IEnumerator.Current => _current;

        /// <summary>
        /// Advances the enumerator to the next element.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the enumerator advanced to a new entry; <c>false</c>
        /// if it has passed the end of the set.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the set was modified since the enumerator was created.
        /// </exception>
        public bool MoveNext()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            if (_state == State.BeforeDefault)
            {
                _state = State.InArray;
                if (_set._hasDefaultValue)
                {
                    _current = default;
                    return true;
                }
            }

            if (_state == State.InArray)
            {
                T?[] slots = _set._slots;
                while (++_index < slots.Length)
                {
                    if (!EqualityComparer<T>.Default.Equals(slots[_index], default(T)))
                    {
                        _current = slots[_index];
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
        /// Thrown if the set was modified since the enumerator was created.
        /// </exception>
        public void Reset()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _index = -1;
            _current = default;
            _state = State.BeforeDefault;
        }

        /// <summary>
        /// Releases any resources held by the enumerator. No-op for this type.
        /// </summary>
        public void Dispose() { }
    }

    private static bool IsDefaultValue(T item) =>
        EqualityComparer<T>.Default.Equals(item, default(T));

    private int ProbeForItem(T item)
    {
        int size = _slots.Length;
        int index = _hasher.Hash(item) & (size - 1);

        while (!EqualityComparer<T>.Default.Equals(_slots[index], default(T)))
        {
            if (EqualityComparer<T>.Default.Equals(_slots[index], item))
                return index;
            index = (index + 1) & (size - 1);
        }

        return -1;
    }

    private void Resize()
    {
        int newSize = _slots.Length * 2;
        int mask = newSize - 1;
        T?[] oldSlots = _slots;

        // Build into a fresh local array then swap it in at the end. The loop
        // inlines a probe-for-empty-slot walk on purpose: every reinserted item
        // is known to be unique in the new table (it came from the previous
        // array which was itself a valid set), and _count / _version are
        // conserved across a resize, so the equality check inside a general
        // insert helper, the threshold check, and the per-call _count / _version
        // bumps are all dead weight. The default-value entry lives out-of-band
        // and is not touched here.
        T?[] newSlots = new T?[newSize];

        var comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < oldSlots.Length; i++)
        {
            T? item = oldSlots[i];
            if (comparer.Equals(item, default(T)))
                continue;

            int index = _hasher.Hash(item!) & mask;
            while (!comparer.Equals(newSlots[index], default(T)))
                index = (index + 1) & mask;

            newSlots[index] = item;
        }

        _slots = newSlots;
        _threshold = (int)(newSize * _loadFactor);
    }

    private void RehashAfterRemove(int startIndex)
    {
        int size = _slots.Length;
        int mask = size - 1;
        int index = (startIndex + 1) & mask;

        var comparer = EqualityComparer<T>.Default;
        while (!comparer.Equals(_slots[index], default(T)))
        {
            T rehashedItem = _slots[index]!;

            _slots[index] = default;

            // Reinsert at the item's natural position. The item was just
            // removed from its old slot, so it cannot match any remaining
            // entry — we probe for an empty slot only, skipping the equality
            // check a general insert helper would do. _count is a slot-shuffle
            // invariant here and the caller (Remove) bumps _version exactly
            // once for the user-visible operation, so neither is touched
            // per rehash.
            int target = _hasher.Hash(rehashedItem) & mask;
            while (!comparer.Equals(_slots[target], default(T)))
                target = (target + 1) & mask;

            _slots[target] = rehashedItem;

            index = (index + 1) & mask;
        }
    }
}
