using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// A <see cref="MinMaxHeap{TElement, TPriority, TComparer}"/> ordered by <see cref="Comparer{T}.Default"/>.
/// </summary>
/// <typeparam name="TElement">The type of the queued elements.</typeparam>
/// <typeparam name="TPriority">The type of the priorities, ordered by <see cref="Comparer{T}.Default"/>.</typeparam>
public class MinMaxHeap<TElement, TPriority> : MinMaxHeap<TElement, TPriority, DefaultComparer<TPriority>>
{
    /// <summary>Initializes a new, empty heap ordered by <see cref="Comparer{T}.Default"/>.</summary>
    public MinMaxHeap()
    {
    }

    /// <summary>
    /// Initializes a new, empty heap ordered by <see cref="Comparer{T}.Default"/> whose backing array holds
    /// <paramref name="capacity"/> entries before the first growth.
    /// </summary>
    /// <param name="capacity">The initial capacity. Must be non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public MinMaxHeap(int capacity)
        : base(capacity)
    {
    }

    /// <summary>
    /// Initializes a new heap ordered by <see cref="Comparer{T}.Default"/> and seeded with
    /// <paramref name="items"/> in <c>O(n)</c>. Duplicate elements and duplicate priorities are both kept.
    /// </summary>
    /// <param name="items">The element/priority pairs to seed the heap with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <c>null</c>.</exception>
    public MinMaxHeap(IEnumerable<(TElement Element, TPriority Priority)> items)
        : base(items)
    {
    }
}

/// <summary>
/// A <b>double-ended priority queue</b>: a min-max heap that hands out both its <b>minimum</b> and its
/// <b>maximum</b> — <see cref="PeekMin"/> / <see cref="PeekMax"/> in <c>O(1)</c>, <see cref="DequeueMin"/> /
/// <see cref="DequeueMax"/> and <see cref="Enqueue"/> in <c>O(log n)</c> — from one flat array with no
/// per-entry object. The BCL <see cref="PriorityQueue{TElement, TPriority}"/> serves only its minimum.
/// </summary>
/// <typeparam name="TElement">
/// The type of the queued elements. Elements are payload, not keys: the same element may be queued any
/// number of times, and the heap never compares or hashes one.
/// </typeparam>
/// <typeparam name="TPriority">The type of the priorities, ordered by <typeparamref name="TComparer"/>.</typeparam>
/// <typeparam name="TComparer">
/// The comparer that orders priorities. Must be a value type implementing <see cref="IComparer{T}"/> so the
/// JIT can devirtualize and inline it — an interface-typed comparer would cost a virtual call for every
/// comparison on the sift paths. Use <see cref="DefaultComparer{T}"/> (or the two-parameter
/// <see cref="MinMaxHeap{TElement, TPriority}"/> alias) for the natural order.
/// </typeparam>
/// <remarks>
/// <para>
/// The layout is the min-max heap of Atkinson, Sack, Santoro and Strothotte (1986): an implicit binary heap
/// whose levels alternate between <b>min levels</b> (the root's level and every second one below it) and
/// <b>max levels</b>. Every entry on a min level is no greater than everything beneath it, and every entry on
/// a max level is no smaller, so the minimum is the root and the maximum is the larger of the root's two
/// children. Insertion bubbles a new leaf up through the grandparents of whichever ordering it belongs to;
/// removal trickles the displaced last leaf down through grandchildren, so both stay <c>O(log n)</c> with at
/// most about twice the comparisons of a one-ended binary heap.
/// </para>
/// <para>
/// The documented BCL-beating workload is a <b>bounded priority buffer served from one end and evicted from
/// the other</b>: a capped work queue that dequeues the most urgent item and, when full, drops the least
/// urgent (<see cref="EnqueueDequeueMax"/> does both halves of that admission in one sift); a fixed-width
/// beam or branch-and-bound frontier; a "best K so far" that is consumed while it fills. Without this type
/// the usual BCL substitute is <see cref="SortedSet{T}"/>, a red-black tree with a heap node per entry that
/// also <b>rejects duplicates</b> — so repeating priorities have to be packed into the key with a
/// tie-breaking sequence number — or two one-ended heaps kept in step by lazy deletion, which doubles the
/// memory and needs a tombstone probe per pop.
/// </para>
/// <para>
/// Where it does <b>not</b> win: a queue that is only ever served from one end. Being double-ended costs
/// comparisons, and the BCL <see cref="PriorityQueue{TElement, TPriority}"/> is a 4-ary heap tuned for
/// exactly that case — use it there. Ties are broken arbitrarily, as in every binary heap, so two entries
/// with equal priority come out in no guaranteed order; the enumerator walks the backing array in heap
/// order, which is neither priority nor insertion order. A <c>null</c> priority is ordered wherever the
/// comparer puts it (<see cref="DefaultComparer{T}"/> puts it first). This type is not thread-safe;
/// concurrent callers must synchronize externally.
/// </para>
/// </remarks>
public class MinMaxHeap<TElement, TPriority, TComparer> : IReadOnlyCollection<(TElement Element, TPriority Priority)>
    where TComparer : struct, IComparer<TPriority>
{
    private const int DefaultCapacity = 4;

    // The implicit heap, 0-based: node i's children are 2i+1 and 2i+2 and its parent (i-1)/2. Node i sits on
    // depth floor(log2(i+1)); even depths are min levels, odd depths max levels.
    private (TElement Element, TPriority Priority)[] _nodes;
    private readonly TComparer _comparer;
    private int _count;

    // Bumped on every observable mutation (and on any reallocation of _nodes) so live enumerators fail fast.
    // An operation that changes nothing — a fused push-pop that hands its own argument straight back, a Clear
    // of an empty heap — does not bump it.
    private int _version;

    /// <summary>Initializes a new, empty heap ordered by a default-constructed <typeparamref name="TComparer"/>.</summary>
    public MinMaxHeap()
        : this(0, default)
    {
    }

    /// <summary>
    /// Initializes a new, empty heap whose backing array holds <paramref name="capacity"/> entries before the
    /// first growth, ordered by a default-constructed <typeparamref name="TComparer"/>.
    /// </summary>
    /// <param name="capacity">The initial capacity. Must be non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public MinMaxHeap(int capacity)
        : this(capacity, default)
    {
    }

    /// <summary>Initializes a new, empty heap ordered by <paramref name="comparer"/>.</summary>
    /// <param name="comparer">The comparer that orders priorities.</param>
    public MinMaxHeap(TComparer comparer)
        : this(0, comparer)
    {
    }

    /// <summary>
    /// Initializes a new, empty heap whose backing array holds <paramref name="capacity"/> entries before the
    /// first growth, ordered by <paramref name="comparer"/>.
    /// </summary>
    /// <param name="capacity">The initial capacity. Must be non-negative.</param>
    /// <param name="comparer">The comparer that orders priorities.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public MinMaxHeap(int capacity, TComparer comparer)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        _nodes = capacity == 0 ? Array.Empty<(TElement, TPriority)>() : new (TElement, TPriority)[capacity];
        _comparer = comparer;
    }

    /// <summary>
    /// Initializes a new heap seeded with <paramref name="items"/>, ordered by a default-constructed
    /// <typeparamref name="TComparer"/>. The heap is built bottom-up in <c>O(n)</c> rather than by <c>n</c>
    /// insertions. Duplicate elements and duplicate priorities are both kept.
    /// </summary>
    /// <param name="items">The element/priority pairs to seed the heap with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <c>null</c>.</exception>
    public MinMaxHeap(IEnumerable<(TElement Element, TPriority Priority)> items)
        : this(items, default)
    {
    }

    /// <summary>
    /// Initializes a new heap seeded with <paramref name="items"/>, ordered by <paramref name="comparer"/>.
    /// The heap is built bottom-up in <c>O(n)</c> rather than by <c>n</c> insertions. Duplicate elements and
    /// duplicate priorities are both kept.
    /// </summary>
    /// <param name="items">The element/priority pairs to seed the heap with.</param>
    /// <param name="comparer">The comparer that orders priorities.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <c>null</c>.</exception>
    public MinMaxHeap(IEnumerable<(TElement Element, TPriority Priority)> items, TComparer comparer)
    {
        ArgumentNullException.ThrowIfNull(items);

        _comparer = comparer;
        _nodes = items.ToArray();
        _count = _nodes.Length;

        // Floyd's bottom-up construction carries over to the min-max layout: trickling every internal node
        // down, last first, leaves each subtree a valid min-max heap before its root is visited.
        for (int i = (_count >> 1) - 1; i >= 0; i--)
            TrickleDown(i);
    }

    /// <summary>Gets the number of entries in the heap.</summary>
    public int Count => _count;

    /// <summary>Gets the number of entries the backing array can hold before it must grow.</summary>
    public int Capacity => _nodes.Length;

    /// <summary>Gets the comparer that orders priorities.</summary>
    public TComparer Comparer => _comparer;

    /// <summary>Adds <paramref name="element"/> with the given <paramref name="priority"/> in <c>O(log n)</c>.</summary>
    /// <param name="element">The element to add. It need not be distinct from elements already queued.</param>
    /// <param name="priority">The priority to associate with <paramref name="element"/>.</param>
    public void Enqueue(TElement element, TPriority priority)
    {
        if (_count == _nodes.Length)
            Grow();

        BubbleUp(_count++, (element, priority));
        _version++;
    }

    /// <summary>Returns the element with the minimum priority without removing it, in <c>O(1)</c>.</summary>
    /// <returns>The minimum-priority element.</returns>
    /// <exception cref="InvalidOperationException">The heap is empty.</exception>
    public TElement PeekMin()
    {
        ThrowIfEmpty();
        return _nodes[0].Element;
    }

    /// <summary>Returns the element with the maximum priority without removing it, in <c>O(1)</c>.</summary>
    /// <returns>The maximum-priority element.</returns>
    /// <exception cref="InvalidOperationException">The heap is empty.</exception>
    public TElement PeekMax()
    {
        ThrowIfEmpty();
        return _nodes[MaxSlot()].Element;
    }

    /// <summary>Attempts to read the element with the minimum priority without removing it.</summary>
    /// <param name="element">When this method returns <c>true</c>, the minimum-priority element; otherwise <c>default</c>.</param>
    /// <param name="priority">When this method returns <c>true</c>, that element's priority; otherwise <c>default</c>.</param>
    /// <returns><c>true</c> if the heap was non-empty; otherwise <c>false</c>.</returns>
    public bool TryPeekMin([MaybeNullWhen(false)] out TElement element, [MaybeNullWhen(false)] out TPriority priority)
    {
        if (_count == 0)
        {
            element = default;
            priority = default;
            return false;
        }

        (element, priority) = _nodes[0];
        return true;
    }

    /// <summary>Attempts to read the element with the maximum priority without removing it.</summary>
    /// <param name="element">When this method returns <c>true</c>, the maximum-priority element; otherwise <c>default</c>.</param>
    /// <param name="priority">When this method returns <c>true</c>, that element's priority; otherwise <c>default</c>.</param>
    /// <returns><c>true</c> if the heap was non-empty; otherwise <c>false</c>.</returns>
    public bool TryPeekMax([MaybeNullWhen(false)] out TElement element, [MaybeNullWhen(false)] out TPriority priority)
    {
        if (_count == 0)
        {
            element = default;
            priority = default;
            return false;
        }

        (element, priority) = _nodes[MaxSlot()];
        return true;
    }

    /// <summary>Removes and returns the element with the minimum priority, in <c>O(log n)</c>.</summary>
    /// <returns>The minimum-priority element.</returns>
    /// <exception cref="InvalidOperationException">The heap is empty.</exception>
    public TElement DequeueMin()
    {
        ThrowIfEmpty();
        TElement element = _nodes[0].Element;
        RemoveAt(0);
        return element;
    }

    /// <summary>Removes and returns the element with the maximum priority, in <c>O(log n)</c>.</summary>
    /// <returns>The maximum-priority element.</returns>
    /// <exception cref="InvalidOperationException">The heap is empty.</exception>
    public TElement DequeueMax()
    {
        ThrowIfEmpty();
        int slot = MaxSlot();
        TElement element = _nodes[slot].Element;
        RemoveAt(slot);
        return element;
    }

    /// <summary>Attempts to remove and return the element with the minimum priority.</summary>
    /// <param name="element">When this method returns <c>true</c>, the removed minimum-priority element; otherwise <c>default</c>.</param>
    /// <param name="priority">When this method returns <c>true</c>, that element's priority; otherwise <c>default</c>.</param>
    /// <returns><c>true</c> if an entry was removed; <c>false</c> if the heap was empty.</returns>
    public bool TryDequeueMin([MaybeNullWhen(false)] out TElement element, [MaybeNullWhen(false)] out TPriority priority)
    {
        if (_count == 0)
        {
            element = default;
            priority = default;
            return false;
        }

        (element, priority) = _nodes[0];
        RemoveAt(0);
        return true;
    }

    /// <summary>Attempts to remove and return the element with the maximum priority.</summary>
    /// <param name="element">When this method returns <c>true</c>, the removed maximum-priority element; otherwise <c>default</c>.</param>
    /// <param name="priority">When this method returns <c>true</c>, that element's priority; otherwise <c>default</c>.</param>
    /// <returns><c>true</c> if an entry was removed; <c>false</c> if the heap was empty.</returns>
    public bool TryDequeueMax([MaybeNullWhen(false)] out TElement element, [MaybeNullWhen(false)] out TPriority priority)
    {
        if (_count == 0)
        {
            element = default;
            priority = default;
            return false;
        }

        int slot = MaxSlot();
        (element, priority) = _nodes[slot];
        RemoveAt(slot);
        return true;
    }

    /// <summary>
    /// Adds <paramref name="element"/> and then removes and returns the element with the minimum priority, in
    /// a single <c>O(log n)</c> sift — the min end's counterpart of
    /// <see cref="PriorityQueue{TElement, TPriority}.EnqueueDequeue"/>, with the same tie rule.
    /// </summary>
    /// <param name="element">The element to add.</param>
    /// <param name="priority">The priority to associate with <paramref name="element"/>.</param>
    /// <returns>
    /// The minimum-priority element after the add. When the heap is empty, or <paramref name="priority"/> is
    /// no greater than the current minimum, that is <paramref name="element"/> itself and the heap is left
    /// unchanged.
    /// </returns>
    public TElement EnqueueDequeueMin(TElement element, TPriority priority)
    {
        if (_count == 0 || _comparer.Compare(priority, _nodes[0].Priority) <= 0)
            return element;

        TElement removed = _nodes[0].Element;
        _nodes[0] = (element, priority);
        TrickleDownMin(0);
        _version++;
        return removed;
    }

    /// <summary>
    /// Adds <paramref name="element"/> and then removes and returns the element with the maximum priority, in
    /// a single <c>O(log n)</c> sift. This is the admission step of a capped buffer that evicts its worst
    /// entry: offer the newcomer, keep the better of it and the incumbent maximum.
    /// </summary>
    /// <param name="element">The element to add.</param>
    /// <param name="priority">The priority to associate with <paramref name="element"/>.</param>
    /// <returns>
    /// The maximum-priority element after the add. When the heap is empty, or <paramref name="priority"/> is
    /// no smaller than the current maximum, that is <paramref name="element"/> itself and the heap is left
    /// unchanged.
    /// </returns>
    public TElement EnqueueDequeueMax(TElement element, TPriority priority)
    {
        if (_count == 0)
            return element;

        int slot = MaxSlot();
        if (_comparer.Compare(priority, _nodes[slot].Priority) >= 0)
            return element;

        TElement removed = _nodes[slot].Element;
        _nodes[slot] = (element, priority);
        if (slot != 0)
        {
            // A max-level trickle cannot repair a newcomer that undercuts the root: hand it the root slot and
            // trickle the old minimum — no greater than anything below it — down the max level instead.
            if (Compare(slot, 0) < 0)
                Swap(slot, 0);

            TrickleDownMax(slot);
        }

        _version++;
        return removed;
    }

    /// <summary>Removes every entry. The backing array is retained.</summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        if (RuntimeHelpers.IsReferenceOrContainsReferences<(TElement, TPriority)>())
            Array.Clear(_nodes, 0, _count);

        _count = 0;
        _version++;
    }

    /// <summary>
    /// Ensures the heap can hold at least <paramref name="capacity"/> entries without growing, and returns the
    /// resulting capacity.
    /// </summary>
    /// <param name="capacity">The minimum capacity to ensure. Must be non-negative.</param>
    /// <returns>The heap's capacity after the call (at least <paramref name="capacity"/>).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public int EnsureCapacity(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        if (_nodes.Length < capacity)
        {
            Array.Resize(ref _nodes, capacity);
            _version++;
        }

        return _nodes.Length;
    }

    /// <summary>Shrinks the backing array to the current entry count, releasing unused capacity.</summary>
    public void TrimExcess()
    {
        if (_nodes.Length == _count)
            return;

        Array.Resize(ref _nodes, _count);
        _version++;
    }

    /// <summary>
    /// Returns an allocation-free struct enumerator over the heap's entries in <b>heap order</b>, which is
    /// neither priority order nor insertion order. To visit entries by priority, dequeue them from either end.
    /// </summary>
    /// <returns>A struct enumerator over the element/priority pairs.</returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<(TElement Element, TPriority Priority)> IEnumerable<(TElement Element, TPriority Priority)>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- internals ---------------------------------------------------------------------------------

    private void ThrowIfEmpty()
    {
        if (_count == 0)
            throw new InvalidOperationException("The heap is empty.");
    }

    // The slot holding the maximum of a non-empty heap: the root when it stands alone, otherwise the larger of
    // its children — the max level's tops. With one child there is nothing to compare.
    private int MaxSlot()
    {
        if (_count <= 2)
            return _count - 1;

        return Compare(1, 2) >= 0 ? 1 : 2;
    }

    // Removes the entry at slot (the root, or the max slot) by moving the last leaf into it and trickling that
    // leaf down. No bubble-up is needed: the leaf is no smaller than the root, so it is valid below a min
    // level, and at the root it is the only thing that can be out of place. Bumps _version.
    private void RemoveAt(int slot)
    {
        int last = --_count;
        if (slot < last)
        {
            _nodes[slot] = _nodes[last];
            ClearSlot(last);
            TrickleDown(slot);
        }
        else
        {
            ClearSlot(last);
        }

        _version++;
    }

    private static bool IsMinLevel(int slot) => (BitOperations.Log2((uint)slot + 1) & 1) == 0;

    // Places a new entry that belongs at slot (the next free leaf) by moving a hole up rather than swapping.
    // The entry first settles which ordering it belongs to by comparing with its parent — the parent sits on
    // the opposite kind of level — then climbs that ordering's grandparent chain.
    private void BubbleUp(int slot, (TElement Element, TPriority Priority) entry)
    {
        (TElement Element, TPriority Priority)[] nodes = _nodes;
        if (slot > 0)
        {
            bool minLevel = IsMinLevel(slot);
            int parent = (slot - 1) >> 1;
            int order = _comparer.Compare(entry.Priority, nodes[parent].Priority);
            if (minLevel ? order > 0 : order < 0)
            {
                nodes[slot] = nodes[parent];
                slot = parent;
                minLevel = !minLevel;
            }

            // A slot below 3 has no grandparent.
            while (slot >= 3)
            {
                int grandparent = (((slot - 1) >> 1) - 1) >> 1;
                order = _comparer.Compare(entry.Priority, nodes[grandparent].Priority);
                if (minLevel ? order >= 0 : order <= 0)
                    break;

                nodes[slot] = nodes[grandparent];
                slot = grandparent;
            }
        }

        nodes[slot] = entry;
    }

    private void TrickleDown(int slot)
    {
        if (IsMinLevel(slot))
            TrickleDownMin(slot);
        else
            TrickleDownMax(slot);
    }

    // Pushes the entry at a min-level slot down, moving a hole instead of swapping. Each step finds the
    // smallest of the slot's children and grandchildren. When all four grandchildren exist both children have
    // descendants, and a max-level child is no smaller than any of its own children, so the smallest is
    // among the grandchildren alone — three comparisons, not five, on every step above the last two levels.
    // A grandchild winner moves up into the hole and the entry, now one min level lower, is checked against
    // its new parent (a max level) before continuing; a child winner — which has no smaller descendant — ends
    // the walk. Index arithmetic is done in long because 2i+1 overflows int for slots past 2^30.
    private void TrickleDownMin(int slot)
    {
        (TElement Element, TPriority Priority)[] nodes = _nodes;
        int count = _count;
        (TElement Element, TPriority Priority) entry = nodes[slot];
        while (true)
        {
            long firstChild = 2L * slot + 1;
            if (firstChild >= count)
                break;

            int child = (int)firstChild;
            long firstGrandchild = 2L * child + 1;
            int best;
            if (firstGrandchild + 3 < count)
            {
                int g = (int)firstGrandchild;
                best = g;
                for (int k = g + 1; k < g + 4; k++)
                {
                    if (_comparer.Compare(nodes[k].Priority, nodes[best].Priority) < 0)
                        best = k;
                }
            }
            else
            {
                // The bottom of the heap: compare everything that exists.
                best = child;
                if (child + 1 < count && _comparer.Compare(nodes[child + 1].Priority, nodes[best].Priority) < 0)
                    best = child + 1;

                for (long k = firstGrandchild; k < count; k++)
                {
                    if (_comparer.Compare(nodes[k].Priority, nodes[best].Priority) < 0)
                        best = (int)k;
                }
            }

            if (_comparer.Compare(nodes[best].Priority, entry.Priority) >= 0)
                break;

            nodes[slot] = nodes[best];
            slot = best;
            if (best <= child + 1)
                break;

            int parent = (best - 1) >> 1;
            if (_comparer.Compare(entry.Priority, nodes[parent].Priority) > 0)
                (entry, nodes[parent]) = (nodes[parent], entry);
        }

        nodes[slot] = entry;
    }

    // The max-level mirror of TrickleDownMin: finds the largest child or grandchild.
    private void TrickleDownMax(int slot)
    {
        (TElement Element, TPriority Priority)[] nodes = _nodes;
        int count = _count;
        (TElement Element, TPriority Priority) entry = nodes[slot];
        while (true)
        {
            long firstChild = 2L * slot + 1;
            if (firstChild >= count)
                break;

            int child = (int)firstChild;
            long firstGrandchild = 2L * child + 1;
            int best;
            if (firstGrandchild + 3 < count)
            {
                int g = (int)firstGrandchild;
                best = g;
                for (int k = g + 1; k < g + 4; k++)
                {
                    if (_comparer.Compare(nodes[k].Priority, nodes[best].Priority) > 0)
                        best = k;
                }
            }
            else
            {
                // The bottom of the heap: compare everything that exists.
                best = child;
                if (child + 1 < count && _comparer.Compare(nodes[child + 1].Priority, nodes[best].Priority) > 0)
                    best = child + 1;

                for (long k = firstGrandchild; k < count; k++)
                {
                    if (_comparer.Compare(nodes[k].Priority, nodes[best].Priority) > 0)
                        best = (int)k;
                }
            }

            if (_comparer.Compare(nodes[best].Priority, entry.Priority) <= 0)
                break;

            nodes[slot] = nodes[best];
            slot = best;
            if (best <= child + 1)
                break;

            int parent = (best - 1) >> 1;
            if (_comparer.Compare(entry.Priority, nodes[parent].Priority) < 0)
                (entry, nodes[parent]) = (nodes[parent], entry);
        }

        nodes[slot] = entry;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Compare(int a, int b) => _comparer.Compare(_nodes[a].Priority, _nodes[b].Priority);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Swap(int a, int b) => (_nodes[a], _nodes[b]) = (_nodes[b], _nodes[a]);

    // Clears a vacated slot so a removed element or priority is not pinned by the array.
    private void ClearSlot(int slot)
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<(TElement, TPriority)>())
            _nodes[slot] = default;
    }

    private void Grow()
    {
        // No _version bump: Grow() is only reached from Enqueue, whose own bump covers the operation.
        int current = _nodes.Length;
        int newCapacity = current == 0 ? DefaultCapacity : current * 2;
        Array.Resize(ref _nodes, ClampGrowth(newCapacity, current));
    }

    // The growth ceiling, split out because neither arm is reachable from a test. Grow() fires only when the
    // array is full, so the doubling passes Array.MaxLength only with more than 2^30 *live* entries — 8 GiB of
    // backing array for (int, int) entries before the resize allocates the next one — and EnsureCapacity
    // cannot pre-inflate the array into it, since a larger array is simply not full. The throw needs the array
    // already at Array.MaxLength and full. Both are kept because they are the only thing between a saturated
    // heap and a silently negative capacity.
    [ExcludeFromCodeCoverage(Justification = "Unreachable in a test run: needs more than 2^30 live entries " +
        "before the doubling passes Array.MaxLength.")]
    private static int ClampGrowth(int newCapacity, int current)
    {
        if ((uint)newCapacity > (uint)Array.MaxLength)
            newCapacity = Array.MaxLength;
        if (newCapacity <= current)
            throw new InvalidOperationException("The heap has reached its maximum capacity.");

        return newCapacity;
    }

    /// <summary>
    /// A struct enumerator over a <see cref="MinMaxHeap{TElement, TPriority, TComparer}"/>'s entries in heap
    /// order (not priority order). Because it is a struct, iterating it via <c>foreach</c> avoids the
    /// allocation a compiler-generated <c>IEnumerator&lt;T&gt;</c> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<(TElement Element, TPriority Priority)>
    {
        private readonly MinMaxHeap<TElement, TPriority, TComparer> _heap;
        private readonly int _version;
        private int _index;
        private (TElement Element, TPriority Priority) _current;

        internal Enumerator(MinMaxHeap<TElement, TPriority, TComparer> heap)
        {
            _heap = heap;
            _version = heap._version;
            _index = 0;
            _current = default;
        }

        /// <summary>Gets the entry at the current position of the enumerator.</summary>
        public readonly (TElement Element, TPriority Priority) Current => _current;

        readonly object IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next entry.</summary>
        /// <returns><c>true</c> if there is a next entry; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The heap was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (_version != _heap._version)
                throw new InvalidOperationException("The heap was modified during enumeration.");

            if (_index < _heap._count)
            {
                _current = _heap._nodes[_index];
                _index++;
                return true;
            }

            _current = default;
            return false;
        }

        /// <summary>Resets the enumerator to before the first entry.</summary>
        /// <exception cref="InvalidOperationException">The heap was modified during enumeration.</exception>
        public void Reset()
        {
            if (_version != _heap._version)
                throw new InvalidOperationException("The heap was modified during enumeration.");

            _index = 0;
            _current = default;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }
}
