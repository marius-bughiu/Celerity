using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Celerity.Hashing;
using Celerity.Primitives;

namespace Celerity.Collections;

/// <summary>
/// A generic hash set that remembers insertion order: it enumerates its elements in the order they
/// were first added, and keeps that order through any number of removals, at <c>O(1)</c> per
/// <see cref="Remove(T)"/>. It can also move an element to either end (<see cref="MoveToEnd"/>,
/// <see cref="MoveToFront"/>) and read or remove the oldest and newest elements
/// (<see cref="TryGetFirst"/>, <see cref="TryRemoveFirst"/>, <see cref="TryGetLast"/>,
/// <see cref="TryRemoveLast"/>), all in <c>O(1)</c>. It is the set counterpart of
/// <see cref="LinkedDictionary{TKey, TValue, THasher}"/>.
/// </summary>
/// <remarks>
/// <para>
/// .NET ships no insertion-ordered set. <see cref="HashSet{T}"/> only enumerates in insertion order
/// until the first removal: a freed entry is reused by the next add, so the new element appears where
/// the removed one was. <see cref="SortedSet{T}"/> keeps an order, but a comparer's order, not arrival
/// order. The usual stand-in is a <see cref="HashSet{T}"/> beside a <see cref="List{T}"/>, whose
/// <c>Remove</c> is <c>O(n)</c>, or a <see cref="Dictionary{TKey, TValue}"/> of
/// <see cref="LinkedListNode{T}"/>s, which allocates a node per element. This type is the shape of
/// Java's <c>LinkedHashSet</c>: <c>O(1)</c> removal and re-ordering, no per-element allocation, and
/// no index access.
/// </para>
/// <para>
/// Elements live in one array of structs, each holding the element, its full hash, the next entry in
/// the same hash bucket, and the previous and next entries in insertion order. A lookup walks a
/// bucket's chain and touches one entry per step. A removal unlinks the entry from its chain and from
/// the order list, and puts the slot on a free list for the next add, so a remove/add churn never
/// grows the table. Enumeration follows the order list, so it costs <c>O(Count)</c> however many
/// elements have been removed.
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Wins</b> wherever the order must survive removals: dedup that must keep first-seen order, work
/// queues that also need "is this id already queued?" and cancellation by id, and "touch to move to
/// the end" recency lists.
/// </description></item>
/// <item><description>
/// <b>Costs</b> two extra <see cref="int"/> links and a cached hash per element over an
/// open-addressed set, and the work of keeping them on every add and remove. When order does not
/// matter, use <see cref="CeleritySet{T, THasher}"/> or <see cref="SwissSet{T, THasher}"/>.
/// </description></item>
/// </list>
/// <para>
/// Unlike the open-addressed sets, this type stores the default element (<c>0</c>, a
/// <see langword="null"/> reference, or a <see cref="Nullable{T}"/> with no value) in the table like
/// any other element, so it keeps its place in the order. It still never hands that element to the
/// hasher: the default element is given hash code <c>0</c> directly, so a hasher that rejects
/// <see langword="null"/> is safe here too.
/// </para>
/// <para>
/// Adding an element that is already present leaves it where it is in the order, as
/// <c>LinkedHashSet</c> does. To move it, call <see cref="MoveToEnd"/>, or remove it and add it
/// again.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of elements in the set.</typeparam>
/// <typeparam name="THasher">
/// The hasher used to compute element hashes. Must be a value type implementing
/// <see cref="IHashProvider{T}"/> so the JIT can devirtualize and inline it.
/// </typeparam>
public class LinkedSet<T, THasher> : ISet<T>, IReadOnlySet<T>
    where THasher : struct, IHashProvider<T>
{
    /// <summary>
    /// The default initial capacity of the set if no capacity is specified.
    /// </summary>
    protected const int DefaultCapacity = 16;

    /// <summary>
    /// The default load factor of the set if no load factor is specified.
    /// </summary>
    protected const float DefaultLoadFactor = 0.75f;

    // Sentinel for "no entry", in a hash chain, the order list or the free list. Buckets store
    // entry + 1 instead, so a freshly allocated bucket array already reads as all-empty.
    private const int Nil = -1;

    private struct Entry
    {
        // The element's full hash, kept so a rebuild never calls the hasher and a reference-element
        // lookup can skip Equals on a hash mismatch.
        public int Hash;

        // The next entry in the same bucket's chain. A free entry reuses it as the free-list link.
        public int ChainNext;

        // The neighbours in insertion order.
        public int Prev;
        public int Next;

        public T Item;
    }

    // Bucket b holds (first entry of its chain) + 1, or 0 when the bucket is empty. Its length is a
    // power of two, so a hash picks its bucket with a mask.
    private int[] _buckets;
    private Entry[] _entries;
    private int _mask;
    private readonly float _loadFactor;

    // Entries [0, _used) have been handed out at least once; the free ones among them are chained
    // through ChainNext from _freeList. An add takes a free entry first, then the next unused one, and
    // only when both run out does the table grow.
    private int _used;
    private int _freeList = Nil;
    private int _count;

    // The oldest and newest entries in insertion order.
    private int _head = Nil;
    private int _tail = Nil;

    // Incremented on every structural mutation so active enumerators can
    // detect concurrent modification and throw, matching BCL semantics.
    private int _version;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkedSet{T,THasher}"/> class using the specified
    /// capacity and load factor.
    /// </summary>
    /// <param name="capacity">
    /// The initial number of hash buckets, automatically rounded up to the next power of two. The set
    /// holds <c>capacity * loadFactor</c> elements (at least one) before it grows.
    /// </param>
    /// <param name="loadFactor">
    /// The number of elements per bucket the set allows before it grows.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative, or <paramref name="loadFactor"/>
    /// is not in the open interval (0, 1).
    /// </exception>
    public LinkedSet(
        int capacity = DefaultCapacity,
        float loadFactor = DefaultLoadFactor)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");
        if (!(loadFactor > 0f && loadFactor < 1f))
            throw new ArgumentOutOfRangeException(nameof(loadFactor), loadFactor, "Load factor must be between 0 (exclusive) and 1 (exclusive).");

        _loadFactor = loadFactor;
        int buckets = FastUtils.NextPowerOfTwo(capacity);
        _buckets = new int[buckets];
        _mask = buckets - 1;
        _entries = new Entry[EntryCapacityFor(buckets, loadFactor)];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkedSet{T,THasher}"/> class containing the
    /// elements copied from the specified <paramref name="source"/>, in the order
    /// <paramref name="source"/> first enumerates them.
    /// </summary>
    /// <param name="source">
    /// The collection whose elements are copied into the new set. If
    /// <paramref name="source"/> implements <see cref="ICollection{T}"/>, its
    /// <c>Count</c> is used to size the backing storage so the initial fill
    /// avoids resize work. Duplicate elements (including duplicate
    /// <c>default(T)</c> entries) are silently deduplicated, matching BCL
    /// <see cref="HashSet{T}"/> semantics; each element keeps the position of its first occurrence.
    /// </param>
    /// <param name="capacity">
    /// The minimum initial number of hash buckets, rounded up to the next power of two. When
    /// the source's count is larger, the table is sized — including
    /// load-factor headroom — to hold the whole source without resizing.
    /// </param>
    /// <param name="loadFactor">
    /// The number of elements per bucket the set allows before it grows.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative, or <paramref name="loadFactor"/>
    /// is not in the open interval (0, 1).
    /// </exception>
    public LinkedSet(
        IEnumerable<T> source,
        int capacity = DefaultCapacity,
        float loadFactor = DefaultLoadFactor)
        : this(InitialCapacityForSource(source, capacity, loadFactor), loadFactor)
    {
        foreach (T item in source)
        {
            TryAdd(item);
        }
    }

    // Runs as part of the chained-ctor argument expression so the null check
    // beats the primary ctor's capacity / loadFactor validation: a null source
    // must surface as ArgumentNullException, not ArgumentOutOfRangeException
    // when the user also passed an invalid loadFactor. Mirrors the same helper
    // on LinkedDictionary (issue #27).
    private static int InitialCapacityForSource(IEnumerable<T> source, int capacity, float loadFactor)
    {
        ArgumentNullException.ThrowIfNull(source);
        int count = (source as ICollection<T>)?.Count ?? 0;

        if (count > 0 && loadFactor > 0f && loadFactor < 1f)
            count = BucketsFor(count, loadFactor);

        // A negative capacity passes through untouched so the primary ctor rejects it,
        // exactly as the capacity-only overload does (issue #460).
        return capacity < 0 ? capacity : Math.Max(capacity, count);
    }

    /// <summary>
    /// Gets the number of elements contained in the set.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Adds the specified element to the end of the set.
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
    /// Attempts to add the specified element to the end of the set.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <returns>
    /// <c>true</c> if the element was added successfully;
    /// <c>false</c> if the element already exists (the set, and the element's place in the order,
    /// are unchanged).
    /// </returns>
    public bool TryAdd(T item)
    {
        // One hash, one chain walk. A duplicate returns without touching the
        // table — no resize, no _version bump — so an active enumerator stays
        // valid (mirrors the #92 ordering on the rest of the family).
        int hash = HashOf(item);
        if (Find(item, hash) >= 0)
            return false;

        AddNew(item, hash);
        return true;
    }

    /// <summary>
    /// Determines whether the set contains the specified element.
    /// </summary>
    /// <param name="item">The element to locate.</param>
    /// <returns><c>true</c> if the element is found; otherwise, <c>false</c>.</returns>
    public bool Contains(T item) => Find(item, HashOf(item)) >= 0;

    /// <summary>
    /// Removes the specified element from the set. The other elements keep their relative order.
    /// This is an <c>O(1)</c> operation.
    /// </summary>
    /// <param name="item">The element to remove.</param>
    /// <returns>
    /// <c>true</c> if the element was successfully removed; otherwise, <c>false</c>.
    /// Also returns <c>false</c> if the element was not found.
    /// </returns>
    public bool Remove(T item)
    {
        int hash = HashOf(item);
        Entry[] entries = _entries;
        int prev = Nil;
        int index = _buckets[hash & _mask] - 1;
        while (index != Nil)
        {
            ref Entry entry = ref entries[index];
            if (Matches(ref entry, item, hash))
            {
                RemoveEntry(index, prev);
                return true;
            }
            prev = index;
            index = entry.ChainNext;
        }

        return false;
    }

    /// <summary>
    /// Moves <paramref name="item"/> to the end of the order, making it the newest element. This is
    /// an <c>O(1)</c> operation.
    /// </summary>
    /// <param name="item">The element to move.</param>
    /// <returns>
    /// <c>true</c> if the element is present (including when it was already the newest element, in
    /// which case nothing changes and active enumerators stay valid); <c>false</c> if it is not.
    /// </returns>
    public bool MoveToEnd(T item)
    {
        int index = Find(item, HashOf(item));
        if (index < 0)
            return false;

        if (index != _tail)
        {
            Unlink(index);
            LinkLast(index);
            _version++;
        }
        return true;
    }

    /// <summary>
    /// Moves <paramref name="item"/> to the front of the order, making it the oldest element. This is
    /// an <c>O(1)</c> operation.
    /// </summary>
    /// <param name="item">The element to move.</param>
    /// <returns>
    /// <c>true</c> if the element is present (including when it was already the oldest element, in
    /// which case nothing changes and active enumerators stay valid); <c>false</c> if it is not.
    /// </returns>
    public bool MoveToFront(T item)
    {
        int index = Find(item, HashOf(item));
        if (index < 0)
            return false;

        if (index != _head)
        {
            Unlink(index);
            LinkFirst(index);
            _version++;
        }
        return true;
    }

    /// <summary>
    /// Gets the oldest element: the first one <see cref="GetEnumerator"/> yields.
    /// </summary>
    /// <param name="item">When this method returns <c>true</c>, the oldest element.</param>
    /// <returns><c>true</c> if the set is not empty; otherwise, <c>false</c>.</returns>
    public bool TryGetFirst(out T item) => TryGetAt(_head, out item);

    /// <summary>
    /// Gets the newest element: the last one <see cref="GetEnumerator"/> yields.
    /// </summary>
    /// <param name="item">When this method returns <c>true</c>, the newest element.</param>
    /// <returns><c>true</c> if the set is not empty; otherwise, <c>false</c>.</returns>
    public bool TryGetLast(out T item) => TryGetAt(_tail, out item);

    /// <summary>
    /// Removes and returns the oldest element. Calling it in a loop drains the set in
    /// first-in, first-out order. This is an <c>O(1)</c> operation.
    /// </summary>
    /// <param name="item">When this method returns <c>true</c>, the removed element.</param>
    /// <returns><c>true</c> if an element was removed; <c>false</c> if the set was empty.</returns>
    public bool TryRemoveFirst(out T item) => TryRemoveAt(_head, out item);

    /// <summary>
    /// Removes and returns the newest element. Calling it in a loop drains the set in
    /// last-in, first-out order. This is an <c>O(1)</c> operation.
    /// </summary>
    /// <param name="item">When this method returns <c>true</c>, the removed element.</param>
    /// <returns><c>true</c> if an element was removed; <c>false</c> if the set was empty.</returns>
    public bool TryRemoveLast(out T item) => TryRemoveAt(_tail, out item);

    /// <summary>
    /// Removes all elements from the set. The underlying capacity is preserved.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        Array.Clear(_buckets);
        Array.Clear(_entries, 0, _used);
        _used = 0;
        _freeList = Nil;
        _head = Nil;
        _tail = Nil;
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Ensures that the set can hold at least <paramref name="capacity"/> elements without resizing,
    /// growing the backing table in a single rebuild if it is currently smaller. Pre-sizing before a
    /// bulk insert of a known size avoids the incremental rebuilds an unsized set would otherwise pay
    /// as it grows. The set is never shrunk by this call, and the order is preserved.
    /// </summary>
    /// <param name="capacity">The minimum number of elements the set must hold without resizing.</param>
    /// <returns>The number of elements the set can now hold before the next resize.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is negative, or more than the largest table (2<sup>30</sup> slots) can hold at
    /// this load factor.
    /// </exception>
    public int EnsureCapacity(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        // The smallest table that holds `capacity` elements is necessarily larger than the current
        // one whenever the current one holds fewer, so this rebuild always grows.
        if (_entries.Length < capacity)
        {
            Rebuild(CheckedBucketsFor(capacity));
            _version++;
        }

        return _entries.Length;
    }

    /// <summary>
    /// Reduces the backing table to the smallest valid size that still holds the current
    /// <see cref="Count"/> without resizing, reclaiming memory. The order is preserved.
    /// </summary>
    public void TrimExcess() => TrimExcess(_count);

    /// <summary>
    /// Reduces (or grows) the backing table to the smallest valid size that holds at least
    /// <paramref name="capacity"/> elements without resizing. The order is preserved.
    /// </summary>
    /// <param name="capacity">
    /// The number of elements to size the table for. Must be at least the current <see cref="Count"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> is less than the current <see cref="Count"/>, or more than
    /// the largest table (2<sup>30</sup> slots) can hold at this load factor.
    /// </exception>
    public void TrimExcess(int capacity)
    {
        if (capacity < _count)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be at least the current Count.");

        int buckets = CheckedBucketsFor(capacity);
        if (buckets != _buckets.Length)
        {
            Rebuild(buckets);
            _version++;
        }
    }

    /// <summary>
    /// Returns an allocation-free enumerator that yields each element in insertion order, oldest
    /// first (as rearranged by <see cref="MoveToEnd"/> and <see cref="MoveToFront"/>). If the set is
    /// modified during enumeration, <see cref="Enumerator.MoveNext"/> throws
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>A struct enumerator over this set.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ── ISet<T> / ICollection<T> set-algebra surface ──────────────────────────
    // The set-algebra logic is shared across the mutable set family via
    // SetOperations, written once against the ISet<T> primitives every set
    // exposes; the semantics match BCL HashSet<T> exactly. Elements an operation
    // adds are appended in the order it adds them, and the survivors of a removal
    // keep their relative order.

    /// <summary>
    /// Modifies the set to contain all elements that are present in itself, in
    /// <paramref name="other"/>, or in both. New elements are appended in the order
    /// <paramref name="other"/> first enumerates them.
    /// </summary>
    /// <param name="other">The collection to union into this set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void UnionWith(IEnumerable<T> other) => SetOperations.UnionWith(this, other);

    /// <summary>
    /// Modifies the set to contain only elements that are also present in
    /// <paramref name="other"/>. The survivors keep their order.
    /// </summary>
    /// <param name="other">The collection to intersect with this set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void IntersectWith(IEnumerable<T> other) => SetOperations.IntersectWith(this, other);

    /// <summary>
    /// Removes every element in <paramref name="other"/> from the set. The survivors keep their order.
    /// </summary>
    /// <param name="other">The collection of elements to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void ExceptWith(IEnumerable<T> other) => SetOperations.ExceptWith(this, other);

    /// <summary>
    /// Modifies the set to contain only elements that are present either in itself
    /// or in <paramref name="other"/>, but not both. The survivors keep their order, and the elements
    /// only <paramref name="other"/> held are appended after them.
    /// </summary>
    /// <param name="other">The collection to apply the symmetric difference with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void SymmetricExceptWith(IEnumerable<T> other) => SetOperations.SymmetricExceptWith(this, other);

    /// <summary>
    /// Determines whether the set is a subset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if every element of this set is in <paramref name="other"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSubsetOf(IEnumerable<T> other) => SetOperations.IsSubsetOf(this, other);

    /// <summary>
    /// Determines whether the set is a proper (strict) subset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of this set is in <paramref name="other"/> and
    /// <paramref name="other"/> has at least one element this set does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSubsetOf(IEnumerable<T> other) => SetOperations.IsProperSubsetOf(this, other);

    /// <summary>
    /// Determines whether the set is a superset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if every element of <paramref name="other"/> is in this set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSupersetOf(IEnumerable<T> other) => SetOperations.IsSupersetOf(this, other);

    /// <summary>
    /// Determines whether the set is a proper (strict) superset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of <paramref name="other"/> is in this set and
    /// this set has at least one element <paramref name="other"/> does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSupersetOf(IEnumerable<T> other) => SetOperations.IsProperSupersetOf(this, other);

    /// <summary>
    /// Determines whether the set and <paramref name="other"/> share at least one element.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two share any element.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool Overlaps(IEnumerable<T> other) => SetOperations.Overlaps(this, other);

    /// <summary>
    /// Determines whether the set and <paramref name="other"/> contain the same distinct elements.
    /// Order is not compared.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two contain exactly the same elements.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool SetEquals(IEnumerable<T> other) => SetOperations.SetEquals(this, other);

    /// <summary>
    /// Copies the elements of the set to the specified <paramref name="array"/>, starting at
    /// <paramref name="arrayIndex"/>, in insertion order.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
    public void CopyTo(T[] array, int arrayIndex) => SetOperations.CopyTo(this, _count, array, arrayIndex);

    // Adds the element, returning whether it was newly added (ISet<T> semantics) —
    // the non-throwing counterpart of the public throw-on-duplicate Add(T).
    bool ISet<T>.Add(T item) => TryAdd(item);

    // ICollection<T>.Add must not throw on a duplicate (unlike the public Add(T)),
    // so it maps to the non-throwing TryAdd.
    void ICollection<T>.Add(T item) => TryAdd(item);

    bool ICollection<T>.IsReadOnly => false;

    /// <summary>
    /// A struct enumerator over a <see cref="LinkedSet{T,THasher}"/>, in insertion order. Because it
    /// is a struct, iterating it via <c>foreach</c> avoids the allocation that a compiler-generated
    /// <c>IEnumerator&lt;T&gt;</c> would incur.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly LinkedSet<T, THasher> _set;
        private readonly int _version;

        // The entry array as of construction. Only a rebuild replaces it, and a rebuild bumps the
        // version, so MoveNext never reads an entry from a stale array.
        private readonly Entry[] _entries;
        private int _next;
        private T _current;

        internal Enumerator(LinkedSet<T, THasher> set)
        {
            _set = set;
            _version = set._version;
            _entries = set._entries;
            _next = set._head;
            _current = default!;
        }

        /// <summary>
        /// Gets the element at the current position of the enumerator.
        /// </summary>
        public T Current => _current;

        object? IEnumerator.Current => _current;

        /// <summary>
        /// Advances the enumerator to the next element.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the enumerator advanced to a new element; <c>false</c>
        /// if it has passed the end of the set.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the set was modified since the enumerator was created.
        /// </exception>
        public bool MoveNext()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            if (_next == Nil)
            {
                _current = default!;
                return false;
            }

            // _next is always a live entry of _entries: the order list only links live entries, and
            // any change to it bumps the version checked above. The step is one dependent load per
            // entry, so skipping the bounds check is what keeps it close to a pointer chase.
            ref Entry entry = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_entries), (nint)(uint)_next);
            _current = entry.Item;
            _next = entry.Next;
            return true;
        }

        /// <summary>
        /// Resets the enumerator to its initial position, before the first element.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the set was modified since the enumerator was created.
        /// </exception>
        public void Reset()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

            _next = _set._head;
            _current = default!;
        }

        /// <summary>
        /// Releases any resources held by the enumerator. No-op for this type.
        /// </summary>
        public void Dispose() { }
    }

    // The number of entries a table of `buckets` buckets holds before it grows. Never zero, so even a
    // one-bucket table with a tiny load factor can take its first element.
    private static int EntryCapacityFor(int buckets, float loadFactor) =>
        Math.Max(1, (int)(buckets * loadFactor));

    // The fewest buckets whose entry capacity holds `entries`. One bucket always holds one entry (see
    // EntryCapacityFor), so zero or one element needs no load-factor headroom; past that, the table
    // must reach the load factor, which a tiny one makes large.
    private static int BucketsFor(int entries, float loadFactor) =>
        entries <= 1 ? 1 : FastUtils.MinTableSizeFor(entries, loadFactor);

    // BucketsFor for EnsureCapacity and TrimExcess(int), which promise the table holds `capacity`:
    // rejects a count that even the capped table cannot hold at this load factor (#570).
    private int CheckedBucketsFor(int capacity)
    {
        int buckets = BucketsFor(capacity, _loadFactor);
        TableSizeGuard.ThrowIfUnreachable(EntryCapacityFor(buckets, _loadFactor), capacity, _loadFactor);
        return buckets;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HashOf(T item)
    {
        // default(T) — a null reference, a Nullable<T> without a value, or 0 for an integer — never
        // reaches the hasher, which may not accept it. Hash code 0 puts it in the table like any other
        // element, so it keeps its place in the order. EmptySlot.Is costs one intrinsic comparison and
        // never boxes, even in an unoptimized build.
        if (EmptySlot.Is(item))
            return 0;
        return default(THasher).Hash(item);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Matches(ref Entry entry, T item, int hash) =>
        // For a value-type element the comparer is a JIT intrinsic, as cheap as the stored-hash test,
        // so the hash test only pays for itself on reference elements, whose Equals may be expensive.
        // typeof(T).IsValueType is a JIT-time constant, so each instantiation keeps one form.
        typeof(T).IsValueType
            ? EqualityComparer<T>.Default.Equals(entry.Item, item)
            : entry.Hash == hash && EqualityComparer<T>.Default.Equals(entry.Item, item);

    // Returns the entry holding `item`, or Nil when it is absent.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Find(T item, int hash)
    {
        Entry[] entries = _entries;
        int index = _buckets[hash & _mask] - 1;
        while (index != Nil)
        {
            ref Entry entry = ref entries[index];
            if (Matches(ref entry, item, hash))
                return index;
            index = entry.ChainNext;
        }
        return Nil;
    }

    // Appends an element the caller has just found absent, growing the table first when every entry
    // is in use.
    private void AddNew(T item, int hash)
    {
        int index;
        if (_freeList != Nil)
        {
            index = _freeList;
            _freeList = _entries[index].ChainNext;
        }
        else
        {
            if (_used == _entries.Length)
                Grow();
            index = _used++;
        }

        ref Entry entry = ref _entries[index];
        entry.Hash = hash;
        entry.Item = item;

        ref int bucket = ref _buckets[hash & _mask];
        entry.ChainNext = bucket - 1;
        bucket = index + 1;

        LinkLast(index);
        _count++;
        _version++;
    }

    // Removes entry `index`, whose predecessor in its hash chain is `chainPrev` (Nil when it heads the
    // chain), from the chain and the order list, and frees its slot.
    private void RemoveEntry(int index, int chainPrev)
    {
        ref Entry entry = ref _entries[index];
        if (chainPrev == Nil)
            _buckets[entry.Hash & _mask] = entry.ChainNext + 1;
        else
            _entries[chainPrev].ChainNext = entry.ChainNext;

        Unlink(index);

        // Release the element so the table keeps no reference alive, and push the slot onto the free
        // list for the next add.
        entry.Item = default!;
        entry.ChainNext = _freeList;
        _freeList = index;
        _count--;
        _version++;
    }

    private bool TryGetAt(int index, out T item)
    {
        if (index == Nil)
        {
            item = default!;
            return false;
        }

        item = _entries[index].Item;
        return true;
    }

    private bool TryRemoveAt(int index, out T item)
    {
        if (!TryGetAt(index, out item))
            return false;

        // Find the entry's predecessor in its hash chain. Chains average under one entry at the
        // load factors the constructor admits, so this walk is short.
        int chainPrev = Nil;
        int cursor = _buckets[_entries[index].Hash & _mask] - 1;
        while (cursor != index)
        {
            chainPrev = cursor;
            cursor = _entries[cursor].ChainNext;
        }

        RemoveEntry(index, chainPrev);
        return true;
    }

    // Takes entry `index` out of the order list, leaving its own links stale.
    private void Unlink(int index)
    {
        ref Entry entry = ref _entries[index];
        int prev = entry.Prev;
        int next = entry.Next;

        if (prev == Nil)
            _head = next;
        else
            _entries[prev].Next = next;

        if (next == Nil)
            _tail = prev;
        else
            _entries[next].Prev = prev;
    }

    private void LinkLast(int index)
    {
        ref Entry entry = ref _entries[index];
        entry.Prev = _tail;
        entry.Next = Nil;

        if (_tail == Nil)
            _head = index;
        else
            _entries[_tail].Next = index;
        _tail = index;
    }

    private void LinkFirst(int index)
    {
        ref Entry entry = ref _entries[index];
        entry.Prev = Nil;
        entry.Next = _head;

        // Only called for an entry that was not already the head, so the list it rejoins is
        // never empty.
        _entries[_head].Prev = index;
        _head = index;
    }

    // Doubles the bucket count until the entry capacity it buys exceeds Count. One doubling is
    // enough at ordinary load factors; a tiny one can need several before the truncated capacity
    // moves past one entry.
    private void Grow()
    {
        int buckets = _buckets.Length;
        do
        {
            buckets = FastUtils.DoubleCapacity(buckets);
        }
        while (EntryCapacityFor(buckets, _loadFactor) <= _count);

        Rebuild(buckets);
    }

    // Rebuilds the table at `buckets` buckets, copying the live entries in insertion order into a
    // fresh array. That compacts away every free slot and leaves entry i followed by entry i + 1,
    // so a later enumeration walks memory sequentially. The hasher is not called: each entry keeps
    // its hash. The caller guarantees the new entry capacity is at least Count.
    private void Rebuild(int buckets)
    {
        Entry[] oldEntries = _entries;
        var newBuckets = new int[buckets];
        var newEntries = new Entry[EntryCapacityFor(buckets, _loadFactor)];
        int mask = buckets - 1;

        int j = 0;
        for (int i = _head; i != Nil; i = oldEntries[i].Next)
        {
            ref Entry source = ref oldEntries[i];
            ref Entry target = ref newEntries[j];
            target.Hash = source.Hash;
            target.Item = source.Item;
            target.Prev = j - 1;
            target.Next = j + 1;

            ref int bucket = ref newBuckets[source.Hash & mask];
            target.ChainNext = bucket - 1;
            bucket = j + 1;
            j++;
        }

        if (j > 0)
            newEntries[j - 1].Next = Nil;

        _buckets = newBuckets;
        _entries = newEntries;
        _mask = mask;
        _used = j;
        _freeList = Nil;
        _head = j > 0 ? 0 : Nil;
        _tail = j - 1;
    }
}
