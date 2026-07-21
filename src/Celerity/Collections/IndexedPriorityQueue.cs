using System.Collections;
using Celerity.Hashing;

namespace Celerity.Collections;

/// <summary>
/// An <b>addressable (indexed) priority queue</b>: a binary min-heap that maps each element to its position
/// in the heap, so — unlike the BCL <see cref="PriorityQueue{TElement, TPriority}"/> — it can
/// <b>change an element's priority</b> (<see cref="Update"/> / decrease-key / increase-key) and
/// <b>remove an arbitrary element</b> (<see cref="Remove(TElement)"/>) in <c>O(log n)</c>, and answer
/// <see cref="Contains"/> / <see cref="TryGetPriority"/> in <c>O(1)</c>. Parameterized on a custom
/// <see cref="IHashProvider{T}"/> so the element hashing behind the index devirtualizes and inlines.
/// </summary>
/// <typeparam name="TElement">
/// The type of the queued elements. Each element is a key: it appears in the queue at most once, and equality
/// uses <see cref="EqualityComparer{T}.Default"/> (through the supplied <typeparamref name="THasher"/>).
/// </typeparam>
/// <typeparam name="TPriority">The type of the priorities ordered by <see cref="Comparer"/>.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute element hashes for the internal element&#8594;slot index. Must be a value type
/// implementing <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
/// <remarks>
/// <para>
/// The BCL <see cref="PriorityQueue{TElement, TPriority}"/> is a plain binary heap with no handle to an
/// element already inside it: it exposes neither a priority update nor an arbitrary remove. The idiomatic
/// workaround is <b>lazy deletion</b> — re-enqueue the element with its new priority and skip stale copies
/// when they surface at the top — which lets the heap grow to <c>O(operations)</c> rather than
/// <c>O(distinct elements)</c> (a real cost on dense graphs) and still cannot answer "what is this element's
/// current priority?". <see cref="IndexedPriorityQueue{TElement, TPriority, THasher}"/> instead keeps an
/// <b>element&#8594;heap-slot index</b> — a dogfooded <see cref="CelerityDictionary{TKey, TValue, THasher}"/>
/// — beside the parallel element / priority heap arrays, updating it on every sift so an element can be
/// located in <c>O(1)</c> and re-heapified in place. The heap therefore holds exactly the live elements.
/// </para>
/// <para>
/// The documented BCL-beating workload is the <b>priority-relaxation loop at the heart of Dijkstra's
/// shortest paths, Prim's minimum spanning tree, A*, and discrete-event simulation</b>: seed the frontier,
/// then repeatedly <see cref="Update"/> (decrease-key) an element's priority and <see cref="Dequeue"/> the
/// current minimum. The addressable heap keeps its size at <c>O(distinct elements)</c> and updates a
/// priority in <c>O(log n)</c>, where the lazy-deletion substitute over a BCL
/// <see cref="PriorityQueue{TElement, TPriority}"/> grows the heap by one entry per relaxation and pays to
/// skip the stale ones. It pairs with <see cref="DisjointSet{T}"/> (union-find / Kruskal's MST) to cover the
/// graph-algorithm primitives the BCL omits.
/// </para>
/// <para>
/// It is a <b>min-heap</b> by default (<see cref="Comparer{T}.Default"/>): <see cref="Peek"/> and
/// <see cref="Dequeue"/> return the element with the smallest priority. Pass a custom
/// <see cref="IComparer{TPriority}"/> to invert the order (a max-heap) or to order by any other key. Because
/// each element is a key, <see cref="Enqueue"/> throws on a duplicate element; use <see cref="TryEnqueue"/>
/// or <see cref="EnqueueOrUpdate"/> when the element may already be queued. The out-of-band
/// <c>default(TElement)</c> / <c>null</c> element is handled by the dogfooded index exactly as in the rest of
/// the family. This type is not thread-safe; concurrent callers must synchronize externally.
/// </para>
/// </remarks>
public sealed class IndexedPriorityQueue<TElement, TPriority, THasher>
    : IReadOnlyCollection<KeyValuePair<TElement, TPriority>>
    where THasher : struct, IHashProvider<TElement>
{
    private const int DefaultCapacity = 4;

    // Parallel heap arrays indexed by slot in [0, _count): _elements[i] is the element, _priorities[i] its
    // priority. The binary heap is 0-based: node i's children are 2i+1 and 2i+2, its parent (i-1)/2.
    private TElement[] _elements;
    private TPriority[] _priorities;

    // Element -> its current heap slot. Dogfoods CelerityDictionary so the element hash devirtualizes and the
    // out-of-band default(TElement)/null element is handled for free. Kept in lockstep with every sift/swap.
    private readonly CelerityDictionary<TElement, int, THasher> _index;

    private readonly IComparer<TPriority> _comparer;
    private int _count;

    // Bumped on every observable mutation (enqueue, dequeue, update, remove, clear, and any capacity change
    // that reallocates the heap arrays) so active enumerators detect concurrent modification. An update bumps
    // it even when the element does not move, since its priority — observable via GetPriority — has changed.
    private int _version;

    /// <summary>Initializes a new, empty priority queue ordered by <see cref="Comparer{T}.Default"/>.</summary>
    public IndexedPriorityQueue()
        : this(0, null)
    {
    }

    /// <summary>
    /// Initializes a new, empty priority queue whose backing storage is pre-sized to hold at least
    /// <paramref name="capacity"/> elements before the first growth, ordered by <see cref="Comparer{T}.Default"/>.
    /// </summary>
    /// <param name="capacity">The initial capacity. Must be non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public IndexedPriorityQueue(int capacity)
        : this(capacity, null)
    {
    }

    /// <summary>
    /// Initializes a new, empty priority queue ordered by <paramref name="comparer"/>.
    /// </summary>
    /// <param name="comparer">
    /// The comparer used to order priorities, or <c>null</c> to use <see cref="Comparer{T}.Default"/>. Invert
    /// it (or supply any custom order) to build a max-heap.
    /// </param>
    public IndexedPriorityQueue(IComparer<TPriority>? comparer)
        : this(0, comparer)
    {
    }

    /// <summary>
    /// Initializes a new, empty priority queue pre-sized to hold at least <paramref name="capacity"/>
    /// elements and ordered by <paramref name="comparer"/>.
    /// </summary>
    /// <param name="capacity">The initial capacity. Must be non-negative.</param>
    /// <param name="comparer">
    /// The comparer used to order priorities, or <c>null</c> to use <see cref="Comparer{T}.Default"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public IndexedPriorityQueue(int capacity, IComparer<TPriority>? comparer)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        int initial = capacity == 0 ? 0 : Math.Max(capacity, DefaultCapacity);
        _elements = initial == 0 ? Array.Empty<TElement>() : new TElement[initial];
        _priorities = initial == 0 ? Array.Empty<TPriority>() : new TPriority[initial];
        _comparer = comparer ?? Comparer<TPriority>.Default;

        // Size the index for `capacity` entries in a single allocation. Passing `capacity` to the ctor would
        // size the table by NextPowerOfTwo(capacity), which is too small to hold that many entries under the
        // load factor — EnsureCapacity would then reallocate and rehash. Building it empty and sizing once via
        // EnsureCapacity does the entry-count-with-headroom math a single time.
        _index = new CelerityDictionary<TElement, int, THasher>();
        if (capacity > 0)
            _index.EnsureCapacity(capacity);
    }

    /// <summary>
    /// Initializes a new priority queue seeded with the given element/priority pairs, ordered by
    /// <see cref="Comparer{T}.Default"/>. A duplicate element keeps its last-seen priority (the seeding is an
    /// upsert, matching <see cref="EnqueueOrUpdate"/>).
    /// </summary>
    /// <param name="items">The element/priority pairs to seed the queue with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <c>null</c>.</exception>
    public IndexedPriorityQueue(IEnumerable<KeyValuePair<TElement, TPriority>> items)
        : this(items, null)
    {
    }

    /// <summary>
    /// Initializes a new priority queue seeded with the given element/priority pairs, ordered by
    /// <paramref name="comparer"/>. A duplicate element keeps its last-seen priority.
    /// </summary>
    /// <param name="items">The element/priority pairs to seed the queue with.</param>
    /// <param name="comparer">
    /// The comparer used to order priorities, or <c>null</c> to use <see cref="Comparer{T}.Default"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <c>null</c>.</exception>
    public IndexedPriorityQueue(IEnumerable<KeyValuePair<TElement, TPriority>> items, IComparer<TPriority>? comparer)
        : this(items is ICollection<KeyValuePair<TElement, TPriority>> c ? c.Count : 0, comparer)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (KeyValuePair<TElement, TPriority> pair in items)
            EnqueueOrUpdate(pair.Key, pair.Value);
    }

    /// <summary>Gets the number of elements currently in the queue.</summary>
    public int Count => _count;

    /// <summary>Gets the number of elements the queue can hold before its backing storage must grow.</summary>
    public int Capacity => _elements.Length;

    /// <summary>Gets the comparer used to order priorities.</summary>
    public IComparer<TPriority> Comparer => _comparer;

    /// <summary>
    /// Adds <paramref name="element"/> with the given <paramref name="priority"/>.
    /// </summary>
    /// <param name="element">The element to enqueue. Must not already be present.</param>
    /// <param name="priority">The priority to associate with <paramref name="element"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="element"/> is already in the queue.</exception>
    public void Enqueue(TElement element, TPriority priority)
    {
        if (!TryEnqueue(element, priority))
            throw new ArgumentException("An element with the same value is already in the queue.", nameof(element));
    }

    /// <summary>
    /// Attempts to add <paramref name="element"/> with the given <paramref name="priority"/>.
    /// </summary>
    /// <param name="element">The element to enqueue.</param>
    /// <param name="priority">The priority to associate with <paramref name="element"/>.</param>
    /// <returns>
    /// <c>true</c> if the element was added; <c>false</c> if it was already present (the queue is unchanged).
    /// </returns>
    public bool TryEnqueue(TElement element, TPriority priority)
    {
        if (_index.ContainsKey(element))
            return false;

        InsertNew(element, priority);
        return true;
    }

    /// <summary>
    /// Adds <paramref name="element"/> with the given <paramref name="priority"/> if it is absent, or changes
    /// its priority if it is already present. The element's queue position is restored either way.
    /// </summary>
    /// <param name="element">The element to enqueue or update.</param>
    /// <param name="priority">The priority to associate with <paramref name="element"/>.</param>
    /// <returns>
    /// <c>true</c> if the element was newly added; <c>false</c> if an existing element's priority was updated.
    /// </returns>
    public bool EnqueueOrUpdate(TElement element, TPriority priority)
    {
        if (_index.TryGetValue(element, out int slot))
        {
            UpdateAt(slot, priority);
            return false;
        }

        // Element is known absent — insert directly rather than re-probing the index via TryEnqueue.
        InsertNew(element, priority);
        return true;
    }

    /// <summary>Returns the element with the minimum priority without removing it.</summary>
    /// <returns>The minimum-priority element.</returns>
    /// <exception cref="InvalidOperationException">The queue is empty.</exception>
    public TElement Peek()
    {
        if (_count == 0)
            throw new InvalidOperationException("The priority queue is empty.");

        return _elements[0];
    }

    /// <summary>
    /// Attempts to read the element with the minimum priority without removing it.
    /// </summary>
    /// <param name="element">When this method returns <c>true</c>, the minimum-priority element; otherwise <c>default</c>.</param>
    /// <param name="priority">When this method returns <c>true</c>, that element's priority; otherwise <c>default</c>.</param>
    /// <returns><c>true</c> if the queue was non-empty; otherwise <c>false</c>.</returns>
    public bool TryPeek(out TElement element, out TPriority priority)
    {
        if (_count == 0)
        {
            element = default!;
            priority = default!;
            return false;
        }

        element = _elements[0];
        priority = _priorities[0];
        return true;
    }

    /// <summary>Removes and returns the element with the minimum priority.</summary>
    /// <returns>The minimum-priority element.</returns>
    /// <exception cref="InvalidOperationException">The queue is empty.</exception>
    public TElement Dequeue()
    {
        if (_count == 0)
            throw new InvalidOperationException("The priority queue is empty.");

        TElement element = _elements[0];
        RemoveAt(0);
        _version++;
        return element;
    }

    /// <summary>
    /// Attempts to remove and return the element with the minimum priority.
    /// </summary>
    /// <param name="element">When this method returns <c>true</c>, the removed minimum-priority element; otherwise <c>default</c>.</param>
    /// <param name="priority">When this method returns <c>true</c>, that element's priority; otherwise <c>default</c>.</param>
    /// <returns><c>true</c> if an element was dequeued; <c>false</c> if the queue was empty.</returns>
    public bool TryDequeue(out TElement element, out TPriority priority)
    {
        if (_count == 0)
        {
            element = default!;
            priority = default!;
            return false;
        }

        element = _elements[0];
        priority = _priorities[0];
        RemoveAt(0);
        _version++;
        return true;
    }

    /// <summary>Determines whether <paramref name="element"/> is currently in the queue.</summary>
    /// <param name="element">The element to locate.</param>
    /// <returns><c>true</c> if the element is present; otherwise <c>false</c>.</returns>
    public bool Contains(TElement element) => _index.ContainsKey(element);

    /// <summary>Returns the current priority of <paramref name="element"/>.</summary>
    /// <param name="element">The element whose priority to return.</param>
    /// <returns>The priority currently associated with <paramref name="element"/>.</returns>
    /// <exception cref="KeyNotFoundException"><paramref name="element"/> is not present.</exception>
    public TPriority GetPriority(TElement element)
    {
        if (!_index.TryGetValue(element, out int slot))
            throw new KeyNotFoundException("The element is not present in the priority queue.");

        return _priorities[slot];
    }

    /// <summary>
    /// Attempts to read the current priority of <paramref name="element"/> without removing it.
    /// </summary>
    /// <param name="element">The element whose priority to read.</param>
    /// <param name="priority">When this method returns <c>true</c>, the element's priority; otherwise <c>default</c>.</param>
    /// <returns><c>true</c> if the element is present; otherwise <c>false</c>.</returns>
    public bool TryGetPriority(TElement element, out TPriority priority)
    {
        if (!_index.TryGetValue(element, out int slot))
        {
            priority = default!;
            return false;
        }

        priority = _priorities[slot];
        return true;
    }

    /// <summary>
    /// Changes the priority of <paramref name="element"/> and restores its position in the queue. Handles
    /// both a decrease-key and an increase-key in <c>O(log n)</c>.
    /// </summary>
    /// <param name="element">The element whose priority to change. Must be present.</param>
    /// <param name="priority">The new priority.</param>
    /// <exception cref="KeyNotFoundException"><paramref name="element"/> is not present.</exception>
    public void Update(TElement element, TPriority priority)
    {
        if (!_index.TryGetValue(element, out int slot))
            throw new KeyNotFoundException("The element is not present in the priority queue.");

        UpdateAt(slot, priority);
    }

    /// <summary>
    /// Attempts to change the priority of <paramref name="element"/> and restore its position in the queue.
    /// </summary>
    /// <param name="element">The element whose priority to change.</param>
    /// <param name="priority">The new priority.</param>
    /// <returns><c>true</c> if the element was present and updated; otherwise <c>false</c>.</returns>
    public bool TryUpdate(TElement element, TPriority priority)
    {
        if (!_index.TryGetValue(element, out int slot))
            return false;

        UpdateAt(slot, priority);
        return true;
    }

    /// <summary>Removes <paramref name="element"/> from the queue, wherever it sits in the heap.</summary>
    /// <param name="element">The element to remove.</param>
    /// <returns><c>true</c> if the element was present and removed; otherwise <c>false</c>.</returns>
    public bool Remove(TElement element) => Remove(element, out _);

    /// <summary>
    /// Removes <paramref name="element"/> from the queue and returns its priority.
    /// </summary>
    /// <param name="element">The element to remove.</param>
    /// <param name="priority">
    /// When this method returns <c>true</c>, the priority the element had before removal; otherwise <c>default</c>.
    /// </param>
    /// <returns><c>true</c> if the element was present and removed; otherwise <c>false</c>.</returns>
    public bool Remove(TElement element, out TPriority priority)
    {
        if (!_index.TryGetValue(element, out int slot))
        {
            priority = default!;
            return false;
        }

        priority = _priorities[slot];
        RemoveAt(slot);
        _version++;
        return true;
    }

    /// <summary>Removes all elements. The backing storage is retained.</summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        if (System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<TElement>())
            Array.Clear(_elements, 0, _count);
        if (System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<TPriority>())
            Array.Clear(_priorities, 0, _count);

        _index.Clear();
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Ensures the queue can hold at least <paramref name="capacity"/> elements without growing, and returns
    /// the resulting capacity.
    /// </summary>
    /// <param name="capacity">The minimum capacity to ensure. Must be non-negative.</param>
    /// <returns>The queue's capacity after the call (at least <paramref name="capacity"/>).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public int EnsureCapacity(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        if (_elements.Length < capacity)
        {
            Resize(capacity);
            _index.EnsureCapacity(capacity);
            _version++;
        }

        return _elements.Length;
    }

    /// <summary>
    /// Shrinks the backing storage to fit the current element count, releasing unused capacity.
    /// </summary>
    public void TrimExcess()
    {
        if (_elements.Length == _count)
            return;

        Resize(_count);
        _index.TrimExcess();
        _version++;
    }

    /// <summary>
    /// Returns an allocation-free struct enumerator over the queue's element/priority pairs in <b>heap
    /// order</b>, which is <b>not</b> priority order. To visit elements by priority, dequeue them (which
    /// empties the queue) or copy the pairs out and sort them.
    /// </summary>
    /// <returns>A struct enumerator over the element/priority pairs.</returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<KeyValuePair<TElement, TPriority>> IEnumerable<KeyValuePair<TElement, TPriority>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- internals ---------------------------------------------------------------------------------

    // Changes the priority at a known slot and re-heapifies from there. Setting a priority is an observable
    // mutation (even when the element does not move), so this always bumps _version to invalidate enumerators.
    private void UpdateAt(int slot, TPriority priority)
    {
        _priorities[slot] = priority;
        SiftUpOrDown(slot);
        _version++;
    }

    // Removes the element occupying slot: overwrite it with the last heap element, drop the tail, and restore
    // the heap property at slot (the moved element may need to travel up or down). Also drops the removed
    // element from the index. Caller bumps _version.
    private void RemoveAt(int slot)
    {
        TElement removed = _elements[slot];
        int last = _count - 1;

        if (slot != last)
        {
            MoveInto(slot, last);
            ClearSlot(last);
            _count--;
            _index.Remove(removed);
            SiftUpOrDown(slot);
        }
        else
        {
            ClearSlot(last);
            _count--;
            _index.Remove(removed);
        }
    }

    // Restores the min-heap invariant at slot after its priority changed or it was overwritten. Sifts up when
    // the element is smaller than its parent, otherwise down. Returns whether it moved.
    private bool SiftUpOrDown(int slot)
    {
        if (slot > 0 && Less(slot, Parent(slot)))
            return SiftUp(slot);

        return SiftDown(slot);
    }

    // Bubbles the element at slot toward the root while it is smaller than its parent. Returns whether it moved.
    private bool SiftUp(int slot)
    {
        bool moved = false;
        while (slot > 0)
        {
            int parent = Parent(slot);
            if (!Less(slot, parent))
                break;

            Swap(slot, parent);
            slot = parent;
            moved = true;
        }

        return moved;
    }

    // Pushes the element at slot toward the leaves while a child is smaller than it. Returns whether it moved.
    private bool SiftDown(int slot)
    {
        bool moved = false;
        while (true)
        {
            int left = (slot << 1) + 1;
            if (left >= _count)
                break;

            int smallest = left;
            int right = left + 1;
            if (right < _count && Less(right, left))
                smallest = right;

            if (!Less(smallest, slot))
                break;

            Swap(slot, smallest);
            slot = smallest;
            moved = true;
        }

        return moved;
    }

    private static int Parent(int slot) => (slot - 1) >> 1;

    // True when the priority at slot a orders before the priority at slot b under the comparer.
    private bool Less(int a, int b) => _comparer.Compare(_priorities[a], _priorities[b]) < 0;

    // Swaps two heap slots, keeping the element->slot index in lockstep.
    private void Swap(int a, int b)
    {
        (_elements[a], _elements[b]) = (_elements[b], _elements[a]);
        (_priorities[a], _priorities[b]) = (_priorities[b], _priorities[a]);
        _index[_elements[a]] = a;
        _index[_elements[b]] = b;
    }

    // Copies the element/priority at slot `from` into slot `to` and repoints the index. Does not touch `from`.
    private void MoveInto(int to, int from)
    {
        _elements[to] = _elements[from];
        _priorities[to] = _priorities[from];
        _index[_elements[to]] = to;
    }

    // Clears reference-bearing storage in a slot so a removed element/priority is not pinned by the array.
    private void ClearSlot(int slot)
    {
        if (System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<TElement>())
            _elements[slot] = default!;
        if (System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<TPriority>())
            _priorities[slot] = default!;
    }

    // Appends a known-absent element as a fresh leaf and sifts it up into place. The caller guarantees the
    // element is not already in the index (so both TryEnqueue and EnqueueOrUpdate reach it after a single
    // index probe, not two).
    private void InsertNew(TElement element, TPriority priority)
    {
        if (_count == _elements.Length)
            Grow();

        int slot = _count;
        _elements[slot] = element;
        _priorities[slot] = priority;
        _index[element] = slot;
        _count++;
        SiftUp(slot);
        _version++;
    }

    private void Grow()
    {
        // Double the capacity, but clamp to Array.MaxLength and guard the int overflow that a naive `* 2`
        // would hit once the queue passes ~1 billion elements (the doubled value wraps negative). Throw
        // rather than silently corrupt if the queue is already at the array ceiling.
        int current = _elements.Length;
        int newCapacity = current == 0 ? DefaultCapacity : current * 2;
        if ((uint)newCapacity > (uint)Array.MaxLength)
            newCapacity = Array.MaxLength;
        if (newCapacity <= current)
            throw new InvalidOperationException("The priority queue has reached its maximum capacity.");

        // No _version bump here: Grow() is only ever called from InsertNew(), whose own _version++ covers the
        // enqueue, so bumping here too would double-count a single operation.
        Resize(newCapacity);
    }

    private void Resize(int newCapacity)
    {
        Array.Resize(ref _elements, newCapacity);
        Array.Resize(ref _priorities, newCapacity);
    }

    /// <summary>
    /// A struct enumerator over an <see cref="IndexedPriorityQueue{TElement, TPriority, THasher}"/>'s
    /// element/priority pairs in heap order (not priority order). Because it is a struct, iterating it via
    /// <c>foreach</c> avoids the allocation a compiler-generated <c>IEnumerator&lt;T&gt;</c> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<KeyValuePair<TElement, TPriority>>
    {
        private readonly IndexedPriorityQueue<TElement, TPriority, THasher> _queue;
        private readonly int _version;
        private int _index;
        private KeyValuePair<TElement, TPriority> _current;

        internal Enumerator(IndexedPriorityQueue<TElement, TPriority, THasher> queue)
        {
            _queue = queue;
            _version = queue._version;
            _index = 0;
            _current = default;
        }

        /// <summary>Gets the element/priority pair at the current position of the enumerator.</summary>
        public readonly KeyValuePair<TElement, TPriority> Current => _current;

        readonly object IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next element/priority pair.</summary>
        /// <returns><c>true</c> if there is a next pair; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The queue was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (_version != _queue._version)
                throw new InvalidOperationException("The priority queue was modified during enumeration.");

            if (_index < _queue._count)
            {
                _current = new KeyValuePair<TElement, TPriority>(_queue._elements[_index], _queue._priorities[_index]);
                _index++;
                return true;
            }

            _current = default;
            return false;
        }

        /// <summary>Resets the enumerator to before the first pair.</summary>
        /// <exception cref="InvalidOperationException">The queue was modified during enumeration.</exception>
        public void Reset()
        {
            if (_version != _queue._version)
                throw new InvalidOperationException("The priority queue was modified during enumeration.");

            _index = 0;
            _current = default;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }
}
