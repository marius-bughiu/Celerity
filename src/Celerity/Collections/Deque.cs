using System.Collections;

namespace Celerity.Collections;

/// <summary>
/// A growable <b>double-ended queue</b> (deque) backed by a single <b>circular buffer</b>: an array with
/// a moving front index, so pushing and popping at <b>either</b> end is <c>O(1)</c> amortized and elements
/// stay contiguous.
/// </summary>
/// <typeparam name="T">The type of the elements.</typeparam>
/// <remarks>
/// <para>
/// The BCL ships no double-ended queue. <see cref="Queue{T}"/> is FIFO-only (no push-front / pop-back),
/// <see cref="Stack{T}"/> is LIFO-only, and the only type that supports <c>O(1)</c> at <b>both</b> ends —
/// <see cref="LinkedList{T}"/> — <b>heap-allocates a node per element</b> and threads its order through
/// pointers scattered across the managed heap. <see cref="Deque{T}"/> instead keeps every element in one
/// array indexed by a moving <c>head</c> plus a count, wrapping around the ends, so it is the array-backed
/// deque the BCL lacks — the .NET analogue of Java's <c>ArrayDeque</c> or C++'s <c>std::deque</c>.
/// </para>
/// <para>
/// The documented BCL-beating workload is any sequence that pushes and pops at both ends — a bounded FIFO
/// queue, a sliding window, a work-stealing / undo buffer — where <see cref="Deque{T}"/> wins on
/// <b>allocation</b> (a warm bounded churn reuses the array with wrap-around and allocates nothing, where
/// <see cref="LinkedList{T}"/> allocates and frees a node per operation) and on <b>cache locality</b>
/// (contiguous storage versus pointer-chased nodes). It also offers <c>O(1)</c> random access by index,
/// which a linked list cannot.
/// </para>
/// <para>
/// The buffer grows by doubling when it fills, re-linearizing the elements into a fresh array (so a push
/// is <c>O(1)</c> amortized, <c>O(n)</c> on the growth step). <see cref="TrimExcess"/> releases spare
/// capacity. This type is not thread-safe; concurrent callers must synchronize externally.
/// </para>
/// </remarks>
public sealed class Deque<T> : IReadOnlyList<T>
{
    private const int DefaultCapacity = 4;

    // Circular buffer: _items[_head] is the front element when _count > 0. Occupied logical slots are the
    // _count physical slots starting at _head and wrapping modulo _items.Length.
    private T[] _items;
    private int _head;
    private int _count;

    // Incremented on every structural mutation (push/pop/clear/grow/trim) so active enumerators can detect
    // concurrent modification and throw. An indexer set is an in-place element change, not structural, and
    // does not bump it — matching List<T>.
    private int _version;

    /// <summary>
    /// Initializes a new, empty deque with no pre-allocated capacity. The first push allocates the backing
    /// array.
    /// </summary>
    public Deque()
    {
        _items = Array.Empty<T>();
    }

    /// <summary>
    /// Initializes a new, empty deque whose backing array is pre-sized to hold at least
    /// <paramref name="capacity"/> elements before the first growth.
    /// </summary>
    /// <param name="capacity">The initial capacity. Must be non-negative; <c>0</c> allocates nothing yet.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public Deque(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        _items = capacity == 0 ? Array.Empty<T>() : new T[capacity];
    }

    /// <summary>
    /// Initializes a new deque containing the elements of <paramref name="collection"/> in enumeration
    /// order, so the first element yielded becomes the front and the last becomes the back.
    /// </summary>
    /// <param name="collection">The elements to copy into the deque.</param>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/> is <c>null</c>.</exception>
    public Deque(IEnumerable<T> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        if (collection is ICollection<T> c)
        {
            int n = c.Count;
            _items = n == 0 ? Array.Empty<T>() : new T[n];
            c.CopyTo(_items, 0);
            _count = n;
        }
        else
        {
            _items = Array.Empty<T>();
            foreach (T item in collection)
                PushBack(item);
            _version = 0;
        }
    }

    /// <summary>Gets the number of elements currently in the deque.</summary>
    public int Count => _count;

    /// <summary>
    /// Gets the number of elements the deque can hold before its backing array must grow.
    /// </summary>
    public int Capacity => _items.Length;

    /// <summary>
    /// Gets or sets the element at the specified position, counting from the front — index <c>0</c> is the
    /// front element and index <c><see cref="Count"/> - 1</c> is the back.
    /// </summary>
    /// <param name="index">The zero-based position from the front.</param>
    /// <returns>The element at <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or not less than <see cref="Count"/>.
    /// </exception>
    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index was out of range.");
            return _items[PhysicalIndex(index)];
        }
        set
        {
            if ((uint)index >= (uint)_count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index was out of range.");
            _items[PhysicalIndex(index)] = value;
        }
    }

    /// <summary>Adds <paramref name="item"/> at the front of the deque.</summary>
    /// <param name="item">The element to add.</param>
    public void PushFront(T item)
    {
        if (_count == _items.Length)
            Grow(_count + 1);

        _head = _head == 0 ? _items.Length - 1 : _head - 1;
        _items[_head] = item;
        _count++;
        _version++;
    }

    /// <summary>Adds <paramref name="item"/> at the back of the deque.</summary>
    /// <param name="item">The element to add.</param>
    public void PushBack(T item)
    {
        if (_count == _items.Length)
            Grow(_count + 1);

        _items[PhysicalIndex(_count)] = item;
        _count++;
        _version++;
    }

    /// <summary>Removes and returns the element at the front of the deque.</summary>
    /// <returns>The former front element.</returns>
    /// <exception cref="InvalidOperationException">The deque is empty.</exception>
    public T PopFront()
    {
        if (_count == 0)
            throw new InvalidOperationException("The deque is empty.");

        T item = _items[_head];
        _items[_head] = default!;
        _head = _head + 1 == _items.Length ? 0 : _head + 1;
        _count--;
        _version++;
        return item;
    }

    /// <summary>Removes and returns the element at the back of the deque.</summary>
    /// <returns>The former back element.</returns>
    /// <exception cref="InvalidOperationException">The deque is empty.</exception>
    public T PopBack()
    {
        if (_count == 0)
            throw new InvalidOperationException("The deque is empty.");

        int last = PhysicalIndex(_count - 1);
        T item = _items[last];
        _items[last] = default!;
        _count--;
        _version++;
        return item;
    }

    /// <summary>Returns the element at the front of the deque without removing it.</summary>
    /// <returns>The front element.</returns>
    /// <exception cref="InvalidOperationException">The deque is empty.</exception>
    public T PeekFront()
    {
        if (_count == 0)
            throw new InvalidOperationException("The deque is empty.");
        return _items[_head];
    }

    /// <summary>Returns the element at the back of the deque without removing it.</summary>
    /// <returns>The back element.</returns>
    /// <exception cref="InvalidOperationException">The deque is empty.</exception>
    public T PeekBack()
    {
        if (_count == 0)
            throw new InvalidOperationException("The deque is empty.");
        return _items[PhysicalIndex(_count - 1)];
    }

    /// <summary>
    /// Attempts to remove and return the front element without throwing when the deque is empty.
    /// </summary>
    /// <param name="item">
    /// When this method returns, the former front element if the deque was non-empty; otherwise the default
    /// value of <typeparamref name="T"/>.
    /// </param>
    /// <returns><c>true</c> if an element was removed; <c>false</c> if the deque was empty.</returns>
    public bool TryPopFront(out T item)
    {
        if (_count == 0)
        {
            item = default!;
            return false;
        }
        item = PopFront();
        return true;
    }

    /// <summary>
    /// Attempts to remove and return the back element without throwing when the deque is empty.
    /// </summary>
    /// <param name="item">
    /// When this method returns, the former back element if the deque was non-empty; otherwise the default
    /// value of <typeparamref name="T"/>.
    /// </param>
    /// <returns><c>true</c> if an element was removed; <c>false</c> if the deque was empty.</returns>
    public bool TryPopBack(out T item)
    {
        if (_count == 0)
        {
            item = default!;
            return false;
        }
        item = PopBack();
        return true;
    }

    /// <summary>Attempts to read the front element without removing it and without throwing when empty.</summary>
    /// <param name="item">
    /// When this method returns, the front element if the deque was non-empty; otherwise the default value of
    /// <typeparamref name="T"/>.
    /// </param>
    /// <returns><c>true</c> if the deque was non-empty; otherwise <c>false</c>.</returns>
    public bool TryPeekFront(out T item)
    {
        if (_count == 0)
        {
            item = default!;
            return false;
        }
        item = _items[_head];
        return true;
    }

    /// <summary>Attempts to read the back element without removing it and without throwing when empty.</summary>
    /// <param name="item">
    /// When this method returns, the back element if the deque was non-empty; otherwise the default value of
    /// <typeparamref name="T"/>.
    /// </param>
    /// <returns><c>true</c> if the deque was non-empty; otherwise <c>false</c>.</returns>
    public bool TryPeekBack(out T item)
    {
        if (_count == 0)
        {
            item = default!;
            return false;
        }
        item = _items[PhysicalIndex(_count - 1)];
        return true;
    }

    /// <summary>
    /// Removes all elements from the deque. The backing array is retained (use <see cref="TrimExcess"/> to
    /// release it).
    /// </summary>
    public void Clear()
    {
        if (_count != 0)
        {
            // Clear only the occupied slots (handling wrap-around) so references are released for GC.
            if (_head + _count <= _items.Length)
            {
                Array.Clear(_items, _head, _count);
            }
            else
            {
                int firstRun = _items.Length - _head;
                Array.Clear(_items, _head, firstRun);
                Array.Clear(_items, 0, _count - firstRun);
            }
        }

        _head = 0;
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Determines whether the deque contains <paramref name="item"/>, comparing with
    /// <see cref="EqualityComparer{T}.Default"/>. This is a linear <c>O(n)</c> scan from front to back.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    /// <returns><c>true</c> if the element is found; otherwise <c>false</c>.</returns>
    public bool Contains(T item)
    {
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < _count; i++)
        {
            if (comparer.Equals(_items[PhysicalIndex(i)], item))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Copies the deque's elements, front to back, into a new array.
    /// </summary>
    /// <returns>A new array of length <see cref="Count"/> holding the elements in front-to-back order.</returns>
    public T[] ToArray()
    {
        if (_count == 0)
            return Array.Empty<T>();

        var array = new T[_count];
        CopyToLinear(array, 0);
        return array;
    }

    /// <summary>
    /// Copies the deque's elements, front to back, into <paramref name="array"/> starting at
    /// <paramref name="arrayIndex"/>.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// The destination does not have enough room from <paramref name="arrayIndex"/> onward.
    /// </exception>
    public void CopyTo(T[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        if (arrayIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, "Index was out of range.");
        if (array.Length - arrayIndex < _count)
            throw new ArgumentException("The destination array has insufficient space.", nameof(array));

        CopyToLinear(array, arrayIndex);
    }

    /// <summary>
    /// Ensures the deque can hold at least <paramref name="capacity"/> elements without growing, allocating a
    /// larger backing array if needed.
    /// </summary>
    /// <param name="capacity">The minimum capacity to guarantee.</param>
    /// <returns>The capacity of the deque after the call (never less than <paramref name="capacity"/>).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public int EnsureCapacity(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (_items.Length < capacity)
            Grow(capacity);
        return _items.Length;
    }

    /// <summary>
    /// Releases unused capacity, shrinking the backing array to exactly <see cref="Count"/> and
    /// re-linearizing the elements so the front sits at index <c>0</c>. Does nothing if the deque already
    /// fills its array.
    /// </summary>
    public void TrimExcess()
    {
        if (_count == _items.Length)
            return;

        if (_count == 0)
        {
            _items = Array.Empty<T>();
            _head = 0;
        }
        else
        {
            var trimmed = new T[_count];
            CopyToLinear(trimmed, 0);
            _items = trimmed;
            _head = 0;
        }
        _version++;
    }

    /// <summary>
    /// Returns an allocation-free struct enumerator that yields the elements from <b>front to back</b>. If
    /// the deque is structurally modified during enumeration, <see cref="Enumerator.MoveNext"/> throws
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over this deque.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- internal circular-buffer machinery -----------------------------------------------------

    // Maps a logical index (0 == front) to the physical slot in _items, wrapping around the end.
    // Requires _items.Length > 0 and 0 <= logical < _items.Length.
    private int PhysicalIndex(int logical)
    {
        int i = _head + logical;
        if (i >= _items.Length)
            i -= _items.Length;
        return i;
    }

    // Copies the _count elements, front to back, into dest starting at destIndex (one or two runs).
    private void CopyToLinear(T[] dest, int destIndex)
    {
        if (_count == 0)
            return;

        if (_head + _count <= _items.Length)
        {
            Array.Copy(_items, _head, dest, destIndex, _count);
        }
        else
        {
            int firstRun = _items.Length - _head;
            Array.Copy(_items, _head, dest, destIndex, firstRun);
            Array.Copy(_items, 0, dest, destIndex + firstRun, _count - firstRun);
        }
    }

    // Grows the backing array to at least `min`, re-linearizing the elements so the front is at index 0.
    private void Grow(int min)
    {
        int newCapacity = _items.Length == 0 ? DefaultCapacity : _items.Length * 2;

        // Guard the doubling against overflow and honour the requested minimum.
        if ((uint)newCapacity > (uint)Array.MaxLength)
            newCapacity = Array.MaxLength;
        if (newCapacity < min)
            newCapacity = min;

        var newItems = new T[newCapacity];
        CopyToLinear(newItems, 0);
        _items = newItems;
        _head = 0;
        _version++;
    }

    /// <summary>
    /// A struct enumerator over a <see cref="Deque{T}"/> that yields elements from front to back. Because it
    /// is a struct, iterating via <c>foreach</c> avoids the allocation a compiler-generated
    /// <c>IEnumerator&lt;T&gt;</c> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly Deque<T> _deque;
        private readonly int _version;
        private int _index;   // next logical index to yield
        private T _current;

        internal Enumerator(Deque<T> deque)
        {
            _deque = deque;
            _version = deque._version;
            _index = 0;
            _current = default!;
        }

        /// <summary>Gets the element at the current position of the enumerator.</summary>
        public readonly T Current => _current;

        readonly object? IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next element (toward the back).</summary>
        /// <returns><c>true</c> if the enumerator advanced to a new element; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The deque was modified since the enumerator was created.</exception>
        public bool MoveNext()
        {
            if (_version != _deque._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            if (_index >= _deque._count)
            {
                _current = default!;
                return false;
            }

            _current = _deque._items[_deque.PhysicalIndex(_index)];
            _index++;
            return true;
        }

        /// <summary>Resets the enumerator to its initial position, before the front element.</summary>
        /// <exception cref="InvalidOperationException">The deque was modified since the enumerator was created.</exception>
        public void Reset()
        {
            if (_version != _deque._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _index = 0;
            _current = default!;
        }

        /// <summary>Releases any resources held by the enumerator. No-op for this type.</summary>
        public readonly void Dispose() { }
    }
}
