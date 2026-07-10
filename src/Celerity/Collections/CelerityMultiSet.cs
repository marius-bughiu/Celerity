using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// A high-performance counting multiset (a.k.a. bag or counter): each distinct
/// element maps to its occurrence count (multiplicity) rather than being simply
/// present or absent. Parameterized on a custom <see cref="IHashProvider{T}"/> so
/// the JIT can devirtualize and inline the element hash, exactly like
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CelerityMultiSet{T, THasher}"/> is the natural sibling of
/// <see cref="CelerityMultiMap{TKey, TValue, THasher}"/>: where the multi-map maps
/// one key to many values, the multiset maps one element to a count. The distinct
/// elements live in the same open-addressed, linear-probing table that backs
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/>; alongside each element
/// slot is its multiplicity (a strictly positive <see cref="int"/> — an element
/// whose count would drop to zero is removed).
/// </para>
/// <para>
/// The headline workload is frequency / histogram counting. The idiomatic BCL
/// approach is <c>Dictionary&lt;T,int&gt;</c> with
/// <c>d[x] = d.GetValueOrDefault(x) + 1</c>, which performs <em>two</em> hash
/// probes per item (one to read, one to write); <see cref="Add(T)"/> does it in a
/// <em>single</em> probe-and-increment and runs the element hash through the
/// devirtualized struct hasher, so it also holds up on clustered / adversarial key
/// shapes where the struct hashers matter.
/// </para>
/// <para>
/// <see cref="Count"/> is the number of <em>distinct</em> elements (the number of
/// entries you enumerate); <see cref="TotalCount"/> is the sum of all
/// multiplicities, mirroring <see cref="CelerityMultiMap{TKey, TValue, THasher}"/>'s
/// <c>Count</c> / <c>ValueCount</c> split. Enumeration yields one
/// <see cref="KeyValuePair{TKey, TValue}"/> of (element, count) per distinct element.
/// </para>
/// <para>
/// The multiset is not thread-safe; concurrent mutation requires external
/// synchronization. It is also not cryptographically protected against hash
/// flooding — that is a property of the chosen hasher, not the collection.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the elements.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute element hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class CelerityMultiSet<T, THasher> : IEnumerable<KeyValuePair<T, int>>
    where THasher : struct, IHashProvider<T>
{
    /// <summary>
    /// The default initial capacity of the multiset if no capacity is specified.
    /// </summary>
    private const int DEFAULT_CAPACITY = 16;

    /// <summary>
    /// The default load factor of the multiset if no load factor is specified.
    /// </summary>
    private const float DEFAULT_LOAD_FACTOR = 0.75f;

    private int _count = 0;
    private long _totalCount = 0;
    private T?[] _elements;
    private int[] _counts;
    private readonly float _loadFactor;
    private int _threshold;
    private readonly THasher _hasher;

    // default(T) (null for reference types, 0 for primitives, Guid.Empty for
    // Guid, etc.) collides with the "empty slot" sentinel used during probing,
    // so its count is stored out-of-band in a dedicated field. _count includes
    // this entry when _hasDefaultKey is true.
    private bool _hasDefaultKey;
    private int _defaultKeyCount;

    // Incremented on every structural mutation so active enumerators can detect
    // concurrent modification and throw, matching BCL semantics. A change to an
    // element's multiplicity is a structural mutation too.
    private int _version;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="CelerityMultiSet{T, THasher}"/> class using the specified
    /// capacity and load factor.
    /// </summary>
    /// <param name="capacity">
    /// The initial distinct-element capacity, automatically rounded to the next
    /// power of two.
    /// </param>
    /// <param name="loadFactor">
    /// The fraction of the element table that can be filled before resizing.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative, or <paramref name="loadFactor"/>
    /// is not in the open interval (0, 1).
    /// </exception>
    public CelerityMultiSet(
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (loadFactor <= 0f || loadFactor >= 1f)
            throw new ArgumentOutOfRangeException(nameof(loadFactor), loadFactor, "Load factor must be between 0 (exclusive) and 1 (exclusive).");

        int size = FastUtils.NextPowerOfTwo(capacity);

        _elements = new T?[size];
        _counts = new int[size];
        _loadFactor = loadFactor;
        _threshold = (int)(size * _loadFactor);
        _hasher = default;
    }

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="CelerityMultiSet{T, THasher}"/> class that counts the occurrences
    /// of each element in the specified <paramref name="source"/>. Each occurrence
    /// of an element increments its multiplicity, so a source with duplicate
    /// elements produces counts greater than one.
    /// </summary>
    /// <param name="source">
    /// The collection whose elements are counted into the new multiset.
    /// </param>
    /// <param name="capacity">
    /// The minimum initial distinct-element capacity, rounded up to the next power
    /// of two. When the source's count is larger, the table is sized — including
    /// load-factor headroom — to hold the whole source without resizing (an
    /// upper bound, since the source may contain duplicates).
    /// </param>
    /// <param name="loadFactor">
    /// The fraction of the element table that can be filled before resizing.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative, or <paramref name="loadFactor"/>
    /// is not in the open interval (0, 1).
    /// </exception>
    public CelerityMultiSet(
        IEnumerable<T> source,
        int capacity = DEFAULT_CAPACITY,
        float loadFactor = DEFAULT_LOAD_FACTOR)
        : this(InitialCapacityForSource(source, capacity, loadFactor), loadFactor)
    {
        foreach (T element in source)
        {
            Add(element);
        }
    }

    // Runs as part of the chained-ctor argument expression so the null check
    // beats the primary ctor's capacity / loadFactor validation: a null source
    // must surface as ArgumentNullException, not ArgumentOutOfRangeException
    // when the user also passed an invalid loadFactor (issue #94).
    private static int InitialCapacityForSource(
        IEnumerable<T> source,
        int capacity,
        float loadFactor)
    {
        ArgumentNullException.ThrowIfNull(source);
        int count = (source as ICollection<T>)?.Count ?? 0;

        // Size the element table for the source count *including* load-factor
        // headroom: the resize threshold is size*loadFactor, so a table sized to
        // the raw count would still rehash on the last inserts of an all-distinct
        // bulk fill. Scaling the count up by 1/loadFactor keeps a distinct-element
        // bulk fill resize-free (issue #27); a source whose elements mostly
        // duplicate just leaves slack, never a resize. A non-collection source
        // (count 0) or an out-of-range loadFactor — left for the primary ctor to
        // reject, so null-source-beats-bad-loadFactor ordering is preserved —
        // falls through to the plain capacity.
        if (count > 0 && loadFactor > 0f && loadFactor < 1f)
        {
            int withHeadroom = (int)Math.Ceiling(count / (double)loadFactor);
            if (withHeadroom > count)
                count = withHeadroom;
        }

        return Math.Max(capacity, count);
    }

    /// <summary>
    /// Gets the number of distinct elements contained in the multiset. This is the
    /// number of entries enumeration yields, not the total of all multiplicities;
    /// see <see cref="TotalCount"/> for the latter.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets the total number of occurrences stored across all elements (the sum of
    /// every element's multiplicity). An element added <c>n</c> times contributes
    /// <c>n</c>.
    /// </summary>
    public long TotalCount => _totalCount;

    /// <summary>
    /// Gets or sets the multiplicity (occurrence count) of the specified element.
    /// Getting an absent element returns <c>0</c>; setting is equivalent to
    /// <see cref="SetCount(T, int)"/> (a value of <c>0</c> removes the element).
    /// </summary>
    /// <param name="element">The element whose multiplicity to get or set.</param>
    /// <returns>The number of occurrences of <paramref name="element"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// On set, the assigned value is negative.
    /// </exception>
    public int this[T element]
    {
        get => GetCount(element);
        set => SetCount(element, value);
    }

    /// <summary>
    /// Gets the multiplicity (occurrence count) of the specified element, or
    /// <c>0</c> if it is not present.
    /// </summary>
    /// <param name="element">The element whose multiplicity to get.</param>
    /// <returns>The number of occurrences of <paramref name="element"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetCount(T element)
    {
        if (IsDefaultKey(element))
            return _hasDefaultKey ? _defaultKeyCount : 0;

        int index = ProbeForKey(element);
        return index < 0 ? 0 : _counts[index];
    }

    /// <summary>
    /// Determines whether the specified element is present (has a multiplicity of
    /// at least one).
    /// </summary>
    /// <param name="element">The element to locate.</param>
    /// <returns><c>true</c> if the element is present; otherwise, <c>false</c>.</returns>
    public bool Contains(T element) => GetCount(element) > 0;

    /// <summary>
    /// Adds a single occurrence of <paramref name="element"/>, incrementing its
    /// multiplicity by one (creating it with a count of one if absent).
    /// </summary>
    /// <param name="element">The element to add.</param>
    /// <exception cref="OverflowException">
    /// The element's multiplicity would exceed <see cref="int.MaxValue"/>.
    /// </exception>
    public void Add(T element) => Add(element, 1);

    /// <summary>
    /// Adds <paramref name="count"/> occurrences of <paramref name="element"/>,
    /// increasing its multiplicity (creating it if absent). A <paramref name="count"/>
    /// of <c>0</c> is a no-op and does not register the element.
    /// </summary>
    /// <param name="element">The element to add.</param>
    /// <param name="count">The number of occurrences to add. Must be non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="count"/> is negative.
    /// </exception>
    /// <exception cref="OverflowException">
    /// The element's multiplicity would exceed <see cref="int.MaxValue"/>.
    /// </exception>
    public void Add(T element, int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be non-negative.");
        if (count == 0)
            return;

        if (IsDefaultKey(element))
        {
            if (!_hasDefaultKey)
            {
                _hasDefaultKey = true;
                _defaultKeyCount = 0;
                _count++;
            }
            _defaultKeyCount = CheckedAdd(_defaultKeyCount, count);
            _totalCount += count;
            _version++;
            return;
        }

        int index = ProbeForInsert(element, out bool wasEmpty);
        if (wasEmpty)
        {
            if (_count >= _threshold)
            {
                Resize();
                index = ProbeForInsert(element, out _);
            }

            _elements[index] = element;
            _counts[index] = count;
            _count++;
        }
        else
        {
            _counts[index] = CheckedAdd(_counts[index], count);
        }

        _totalCount += count;
        _version++;
    }

    /// <summary>
    /// Removes a single occurrence of <paramref name="element"/>, decrementing its
    /// multiplicity by one. If that empties the element (its count reaches zero),
    /// the element itself is removed from the multiset.
    /// </summary>
    /// <param name="element">The element to remove one occurrence of.</param>
    /// <returns>
    /// <c>true</c> if an occurrence was present and removed; otherwise <c>false</c>
    /// (including when the element is absent).
    /// </returns>
    public bool Remove(T element)
    {
        if (IsDefaultKey(element))
        {
            if (!_hasDefaultKey)
                return false;

            _defaultKeyCount--;
            _totalCount--;
            if (_defaultKeyCount == 0)
            {
                _hasDefaultKey = false;
                _count--;
            }
            _version++;
            return true;
        }

        int index = ProbeForKey(element);
        if (index < 0)
            return false;

        _counts[index]--;
        _totalCount--;
        if (_counts[index] == 0)
        {
            BackwardShiftRemove(index);
            _count--;
        }
        _version++;
        return true;
    }

    /// <summary>
    /// Removes the specified element entirely, discarding all of its occurrences.
    /// </summary>
    /// <param name="element">The element to remove completely.</param>
    /// <returns>
    /// <c>true</c> if the element was present and removed; otherwise, <c>false</c>.
    /// </returns>
    public bool RemoveAll(T element)
    {
        if (IsDefaultKey(element))
        {
            if (!_hasDefaultKey)
                return false;

            _totalCount -= _defaultKeyCount;
            _hasDefaultKey = false;
            _defaultKeyCount = 0;
            _count--;
            _version++;
            return true;
        }

        int index = ProbeForKey(element);
        if (index < 0)
            return false;

        _totalCount -= _counts[index];
        BackwardShiftRemove(index);
        _count--;
        _version++;
        return true;
    }

    /// <summary>
    /// Sets the multiplicity of <paramref name="element"/> to exactly
    /// <paramref name="count"/>, returning its previous multiplicity. A
    /// <paramref name="count"/> of <c>0</c> removes the element; a positive count
    /// creates or overwrites it.
    /// </summary>
    /// <param name="element">The element whose multiplicity to set.</param>
    /// <param name="count">The new multiplicity. Must be non-negative.</param>
    /// <returns>The multiplicity of <paramref name="element"/> before this call.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="count"/> is negative.
    /// </exception>
    public int SetCount(T element, int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be non-negative.");

        if (IsDefaultKey(element))
        {
            int previous = _hasDefaultKey ? _defaultKeyCount : 0;
            if (count == previous)
                return previous;

            if (count == 0)
            {
                _hasDefaultKey = false;
                _defaultKeyCount = 0;
                _count--;
            }
            else
            {
                if (!_hasDefaultKey)
                {
                    _hasDefaultKey = true;
                    _count++;
                }
                _defaultKeyCount = count;
            }
            _totalCount += count - previous;
            _version++;
            return previous;
        }

        int index = ProbeForKey(element);
        int prior = index < 0 ? 0 : _counts[index];
        if (count == prior)
            return prior;

        if (count == 0)
        {
            // index >= 0 here, because count != prior and count == 0 implies prior > 0.
            BackwardShiftRemove(index);
            _count--;
        }
        else if (index < 0)
        {
            // The element is known absent (ProbeForKey returned -1 above), so a
            // single probe-to-empty after any resize is sufficient.
            if (_count >= _threshold)
                Resize();
            int insertIndex = ProbeForInsert(element, out _);
            _elements[insertIndex] = element;
            _counts[insertIndex] = count;
            _count++;
        }
        else
        {
            _counts[index] = count;
        }

        _totalCount += count - prior;
        _version++;
        return prior;
    }

    /// <summary>
    /// Removes all elements and occurrences from the multiset. The underlying
    /// element capacity is preserved.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        Array.Clear(_elements, 0, _elements.Length);
        Array.Clear(_counts, 0, _counts.Length);
        _hasDefaultKey = false;
        _defaultKeyCount = 0;
        _count = 0;
        _totalCount = 0;
        _version++;
    }

    /// <summary>
    /// Ensures that the element table can hold at least <paramref name="capacity"/> distinct elements
    /// without resizing, growing it in a single rehash if it is currently smaller. Pre-sizing before
    /// bulk population with a known number of distinct elements avoids the incremental rehashes an
    /// unsized multiset would otherwise pay. The multiset is never shrunk by this call.
    /// </summary>
    /// <param name="capacity">The minimum number of distinct elements the table must hold without resizing.</param>
    /// <returns>The number of distinct elements the multiset can now hold before the next resize.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public int EnsureCapacity(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        if (_threshold < capacity)
        {
            int newSize = FastUtils.MinTableSizeFor(capacity, _loadFactor);
            if (newSize > _elements.Length)
            {
                Resize(newSize);
                _version++;
            }
        }

        return _threshold;
    }

    /// <summary>
    /// Reduces the element table to the smallest power-of-two size that still holds the current
    /// distinct-element <see cref="Count"/> without resizing, reclaiming memory after elements have
    /// been removed. The out-of-band default-element count and every element's multiplicity are
    /// preserved.
    /// </summary>
    public void TrimExcess() => TrimExcess(_count);

    /// <summary>
    /// Reduces (or grows) the element table to the smallest power-of-two size that holds at least
    /// <paramref name="capacity"/> distinct elements without resizing.
    /// </summary>
    /// <param name="capacity">
    /// The number of distinct elements to size the table for. Must be at least the current distinct
    /// element <see cref="Count"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is less than the current distinct element <see cref="Count"/>.
    /// </exception>
    public void TrimExcess(int capacity)
    {
        if (capacity < _count)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least the current Count.");

        int newSize = FastUtils.MinTableSizeFor(capacity, _loadFactor);
        if (newSize != _elements.Length)
        {
            Resize(newSize);
            _version++;
        }
    }

    /// <summary>
    /// Returns an allocation-free enumerator that yields one
    /// <see cref="KeyValuePair{TKey, TValue}"/> of (element, count) per distinct
    /// element. The enumeration order is unspecified and may change across versions;
    /// do not rely on it. If the multiset is modified during enumeration,
    /// <see cref="Enumerator.MoveNext"/> throws <see cref="InvalidOperationException"/>.
    /// The out-of-band default element (if present) is yielded first.
    /// </summary>
    /// <returns>A struct enumerator over the (element, count) pairs of this multiset.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<KeyValuePair<T, int>> IEnumerable<KeyValuePair<T, int>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Gets an enumerable view over the distinct elements in the multiset (each
    /// yielded once, regardless of its multiplicity). The view is a lightweight
    /// struct and iterating it does not allocate.
    /// </summary>
    public ElementCollection Elements => new ElementCollection(this);

    // ---- internal helpers ----

    private static bool IsDefaultKey(T element) =>
        EqualityComparer<T>.Default.Equals(element, default(T));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CheckedAdd(int current, int add)
    {
        if (add > int.MaxValue - current)
            throw new OverflowException("Element multiplicity would exceed Int32.MaxValue.");
        return current + add;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ProbeForInsert(T element, out bool wasEmpty)
    {
        T?[] elements = _elements;
        ref T? elementsRef = ref MemoryMarshal.GetArrayDataReference(elements);
        int mask = elements.Length - 1;
        var comparer = EqualityComparer<T>.Default;
        int index = _hasher.Hash(element) & mask;

        while (true)
        {
            T? slot = Unsafe.Add(ref elementsRef, (nint)(uint)index);
            if (comparer.Equals(slot, default(T))) { wasEmpty = true; return index; }
            if (comparer.Equals(slot, element)) { wasEmpty = false; return index; }
            index = (index + 1) & mask;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ProbeForKey(T element)
    {
        T?[] elements = _elements;
        ref T? elementsRef = ref MemoryMarshal.GetArrayDataReference(elements);
        int mask = elements.Length - 1;
        var comparer = EqualityComparer<T>.Default;
        int index = _hasher.Hash(element) & mask;

        while (true)
        {
            T? slot = Unsafe.Add(ref elementsRef, (nint)(uint)index);
            if (comparer.Equals(slot, default(T))) return -1;
            if (comparer.Equals(slot, element)) return index;
            index = (index + 1) & mask;
        }
    }

    private void Resize() => Resize(FastUtils.DoubleCapacity(_elements.Length));

    // Rehashes every live element (and its count) into a freshly allocated table of the given
    // power-of-two size. Shared by the doubling growth path and the EnsureCapacity / TrimExcess
    // re-sizers, which pass an explicit target. The caller guarantees newSize is a power of two
    // strictly greater than the in-table distinct-element count (so the probe loop terminates).
    private void Resize(int newSize)
    {
        int mask = newSize - 1;
        T?[] oldElements = _elements;
        int[] oldCounts = _counts;

        // Reinsert each surviving element/count into the new table. Every reinserted
        // element is known unique (it came from a valid table), so a bare probe-to-empty
        // is sufficient — no equality check on duplicates. The default element lives
        // out-of-band and is not touched here.
        T?[] newElements = new T?[newSize];
        int[] newCounts = new int[newSize];
        ref T? oldElementsRef = ref MemoryMarshal.GetArrayDataReference(oldElements);
        ref T? newElementsRef = ref MemoryMarshal.GetArrayDataReference(newElements);

        var comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < oldElements.Length; i++)
        {
            T? element = Unsafe.Add(ref oldElementsRef, (nint)(uint)i);
            if (comparer.Equals(element, default(T)))
                continue;

            int index = _hasher.Hash(element!) & mask;
            while (!comparer.Equals(Unsafe.Add(ref newElementsRef, (nint)(uint)index), default(T)))
                index = (index + 1) & mask;

            Unsafe.Add(ref newElementsRef, (nint)(uint)index) = element;
            newCounts[index] = oldCounts[i];
        }

        _elements = newElements;
        _counts = newCounts;
        _threshold = (int)(newSize * _loadFactor);
    }

    // Backward-shift deletion (Knuth TAOCP Vol 3, §6.4 Algorithm R), mirroring
    // CelerityDictionary but shifting the parallel count too. The caller has
    // already emptied the element at startIndex; this helper closes the gap and
    // clears the final emptied slot.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void BackwardShiftRemove(int startIndex)
    {
        T?[] elements = _elements;
        int[] counts = _counts;
        ref T? elementsRef = ref MemoryMarshal.GetArrayDataReference(elements);
        int mask = elements.Length - 1;
        var comparer = EqualityComparer<T>.Default;
        int i = startIndex;
        int j = i;

        while (true)
        {
            j = (j + 1) & mask;
            T? candidate = Unsafe.Add(ref elementsRef, (nint)(uint)j);
            if (comparer.Equals(candidate, default(T)))
                break;

            int k = _hasher.Hash(candidate!) & mask;

            bool bypassesGap = (i <= j)
                ? (i < k && k <= j)
                : (i < k || k <= j);
            if (bypassesGap)
                continue;

            Unsafe.Add(ref elementsRef, (nint)(uint)i) = candidate;
            counts[i] = counts[j];
            i = j;
        }

        Unsafe.Add(ref elementsRef, (nint)(uint)i) = default;
        counts[i] = 0;
    }

    /// <summary>
    /// A struct enumerator over a <see cref="CelerityMultiSet{T, THasher}"/> that
    /// yields one <see cref="KeyValuePair{TKey, TValue}"/> of (element, count) per
    /// distinct element. Because it is a struct, iterating it via <c>foreach</c>
    /// avoids the allocation a compiler-generated <see cref="IEnumerator{T}"/> would
    /// incur. The out-of-band default element (if present) is yielded first.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<T, int>>
    {
        private readonly CelerityMultiSet<T, THasher> _set;
        private readonly int _version;
        private int _index;
        private KeyValuePair<T, int> _current;
        private State _state;

        private enum State : byte
        {
            BeforeDefaultKey,
            InArray,
            Done
        }

        internal Enumerator(CelerityMultiSet<T, THasher> set)
        {
            _set = set;
            _version = set._version;
            _index = -1;
            _current = default;
            _state = State.BeforeDefaultKey;
        }

        /// <summary>Gets the (element, count) pair at the current position of the enumerator.</summary>
        public KeyValuePair<T, int> Current => _current;

        object IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next distinct element.</summary>
        /// <returns>
        /// <c>true</c> if the enumerator advanced to a new element; <c>false</c> if
        /// it has passed the end of the multiset.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the multiset was modified since the enumerator was created.
        /// </exception>
        public bool MoveNext()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            if (_state == State.BeforeDefaultKey)
            {
                _state = State.InArray;
                if (_set._hasDefaultKey)
                {
                    _current = new KeyValuePair<T, int>(default(T)!, _set._defaultKeyCount);
                    return true;
                }
            }

            if (_state == State.InArray)
            {
                T?[] elements = _set._elements;
                int[] counts = _set._counts;
                int length = elements.Length;
                ref T? elementsRef = ref MemoryMarshal.GetArrayDataReference(elements);
                var comparer = EqualityComparer<T>.Default;
                while (++_index < length)
                {
                    T? element = Unsafe.Add(ref elementsRef, (nint)(uint)_index);
                    if (!comparer.Equals(element, default(T)))
                    {
                        _current = new KeyValuePair<T, int>(element!, counts[_index]);
                        return true;
                    }
                }
                _state = State.Done;
            }

            _current = default;
            return false;
        }

        /// <summary>Resets the enumerator to its initial position, before the first element.</summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the multiset was modified since the enumerator was created.
        /// </exception>
        public void Reset()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _index = -1;
            _current = default;
            _state = State.BeforeDefaultKey;
        }

        /// <summary>Releases any resources held by the enumerator. No-op for this type.</summary>
        public void Dispose() { }
    }

    /// <summary>
    /// A struct enumerable view over the distinct elements of a
    /// <see cref="CelerityMultiSet{T, THasher}"/>. Iterating it does not allocate;
    /// passing it through <see cref="IEnumerable{T}"/> will box the enumerator and is
    /// therefore not zero-allocation.
    /// </summary>
    public readonly struct ElementCollection : IEnumerable<T>
    {
        private readonly CelerityMultiSet<T, THasher> _set;

        internal ElementCollection(CelerityMultiSet<T, THasher> set) => _set = set;

        /// <summary>
        /// Gets the number of distinct elements in the view (equal to the multiset's <see cref="Count"/>).
        /// </summary>
        public int Count => _set._count;

        /// <summary>Returns an allocation-free struct enumerator over the distinct elements.</summary>
        public Enumerator GetEnumerator() => new Enumerator(_set);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(_set);
        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_set);

        /// <summary>
        /// A struct enumerator over the distinct elements of a
        /// <see cref="CelerityMultiSet{T, THasher}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            private CelerityMultiSet<T, THasher>.Enumerator _inner;

            internal Enumerator(CelerityMultiSet<T, THasher> set) => _inner = set.GetEnumerator();

            /// <summary>Gets the current distinct element.</summary>
            public T Current => _inner.Current.Key;

            object? IEnumerator.Current => _inner.Current.Key;

            /// <summary>Advances to the next distinct element.</summary>
            public bool MoveNext() => _inner.MoveNext();

            /// <summary>Resets the enumerator to its initial position.</summary>
            public void Reset() => _inner.Reset();

            /// <summary>No-op.</summary>
            public void Dispose() => _inner.Dispose();
        }
    }
}
