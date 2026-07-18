using System.Collections;

namespace Celerity.Collections;

/// <summary>
/// A <b>disjoint-set</b> (union-find) over arbitrary elements: it partitions the elements it holds into
/// non-overlapping sets and answers "are these two in the same set?" and "merge these two sets" in
/// near-constant amortized time.
/// </summary>
/// <typeparam name="T">
/// The type of the elements; must be non-null. Equality uses <see cref="EqualityComparer{T}.Default"/>.
/// </typeparam>
/// <remarks>
/// <para>
/// The BCL ships no union-find structure. The idiomatic alternatives are both super-linear for a run of
/// merges: keeping a <see cref="Dictionary{TKey, TValue}"/> from element to its <see cref="HashSet{T}"/>
/// group and copying the smaller group into the larger on every <see cref="Union"/> is <c>O(n)</c> per
/// merge (<c>O(n²)</c> to build one component from <c>n</c> singletons), and rebuilding a graph to run a
/// BFS/DFS per connectivity query is <c>O(V + E)</c> <b>every</b> query. <see cref="DisjointSet{T}"/>
/// instead threads each set through a forest of parent pointers packed into dense <see cref="int"/> arrays
/// and applies the two classic optimizations — <b>union by size</b> (the smaller tree is hung under the
/// larger root) and <b>path halving</b> (every <see cref="Find"/> points each node it walks at its
/// grandparent) — so <see cref="Union"/>, <see cref="Find"/>, and <see cref="Connected"/> run in
/// <c>O(α(n))</c> amortized, where <c>α</c> is the inverse-Ackermann function and is ≤ 4 for any practical
/// <c>n</c>: effectively <c>O(1)</c>.
/// </para>
/// <para>
/// The documented BCL-beating workload is any <b>incremental connectivity / connected-components</b> pass
/// — a stream of <see cref="Union"/> operations interleaved with <see cref="Connected"/> queries (union of
/// equivalence classes, Kruskal's minimum spanning tree, clustering, image segmentation, cycle detection
/// in an undirected graph). <see cref="DisjointSet{T}"/> runs the whole stream in near-linear total time
/// where the <see cref="Dictionary{TKey, TValue}"/>-of-<see cref="HashSet{T}"/> merge approach is
/// quadratic.
/// </para>
/// <para>
/// Elements are stored densely in insertion order; enumerating the set yields them in that order.
/// <see cref="GetComponents"/> materializes the current partition as a grouped, representative-keyed view.
/// This type is not thread-safe; concurrent callers must synchronize externally.
/// </para>
/// </remarks>
public sealed class DisjointSet<T> : IReadOnlyCollection<T>
    where T : notnull
{
    private const int DefaultCapacity = 4;

    // Dense, parallel arrays indexed by an element's assigned slot in [0, _count):
    //   _elements[i] is the element itself (also gives enumeration and representative-element lookup),
    //   _parent[i]   is the slot of i's parent in the union-find forest (_parent[i] == i marks a root),
    //   _size[i]     is the number of elements in i's tree and is meaningful only when i is a root.
    private T[] _elements;
    private int[] _parent;
    private int[] _size;

    // Element -> slot. Kept separate from the union-find forest so arbitrary keys map to dense indices.
    private readonly Dictionary<T, int> _indexOf;

    private int _count;      // number of elements (== distinct slots in use).
    private int _setCount;   // number of disjoint sets (roots); decremented on every effective union.

    // Bumped on every structural mutation (Add / Union that merges / Clear) so active enumerators throw on
    // concurrent modification. A Find that only compresses paths is not a structural change and does not
    // bump it — the observable partition is unchanged.
    private int _version;

    /// <summary>Initializes a new, empty disjoint-set with a small default capacity.</summary>
    public DisjointSet()
        : this(0)
    {
    }

    /// <summary>
    /// Initializes a new, empty disjoint-set whose backing storage is pre-sized to hold at least
    /// <paramref name="capacity"/> elements before the first growth.
    /// </summary>
    /// <param name="capacity">The initial capacity. Must be non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is negative.</exception>
    public DisjointSet(int capacity)
    {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be non-negative.");

        int initial = capacity == 0 ? 0 : Math.Max(capacity, DefaultCapacity);
        _elements = initial == 0 ? Array.Empty<T>() : new T[initial];
        _parent = initial == 0 ? Array.Empty<int>() : new int[initial];
        _size = initial == 0 ? Array.Empty<int>() : new int[initial];
        _indexOf = new Dictionary<T, int>(capacity);
    }

    /// <summary>
    /// Initializes a new disjoint-set containing each distinct element of <paramref name="elements"/> as its
    /// own singleton set, in enumeration order. Duplicate elements are ignored after the first.
    /// </summary>
    /// <param name="elements">The elements to seed as singletons.</param>
    /// <exception cref="ArgumentNullException"><paramref name="elements"/> is <c>null</c>.</exception>
    public DisjointSet(IEnumerable<T> elements)
        : this(elements is ICollection<T> c ? c.Count : 0)
    {
        ArgumentNullException.ThrowIfNull(elements);

        foreach (T element in elements)
            Add(element);
    }

    /// <summary>Gets the number of elements in the disjoint-set.</summary>
    public int Count => _count;

    /// <summary>
    /// Gets the number of disjoint sets (connected components) — the number of distinct groups the elements
    /// are currently partitioned into. Starts equal to <see cref="Count"/> (every element its own singleton)
    /// and drops by one on every <see cref="Union"/> that merges two previously separate sets.
    /// </summary>
    public int SetCount => _setCount;

    /// <summary>
    /// Gets the number of elements the disjoint-set can hold before its backing storage must grow.
    /// </summary>
    public int Capacity => _elements.Length;

    /// <summary>
    /// Adds <paramref name="element"/> as a new singleton set. If it is already present, nothing changes.
    /// </summary>
    /// <param name="element">The element to add.</param>
    /// <returns><c>true</c> if the element was added; <c>false</c> if it was already present.</returns>
    public bool Add(T element)
    {
        if (_indexOf.ContainsKey(element))
            return false;

        AddNew(element);
        _version++;
        return true;
    }

    /// <summary>Determines whether <paramref name="element"/> is present in the disjoint-set.</summary>
    /// <param name="element">The element to locate.</param>
    /// <returns><c>true</c> if the element is present; otherwise <c>false</c>.</returns>
    public bool Contains(T element) => _indexOf.ContainsKey(element);

    /// <summary>
    /// Merges the sets containing <paramref name="a"/> and <paramref name="b"/>. Either element that is not
    /// already present is added as a singleton first, so this doubles as the edge-insertion primitive when
    /// building connectivity from a stream of pairs.
    /// </summary>
    /// <param name="a">The first element.</param>
    /// <param name="b">The second element.</param>
    /// <returns>
    /// <c>true</c> if the two were in different sets and have now been merged; <c>false</c> if they were
    /// already in the same set (the call was a no-op on the partition).
    /// </returns>
    public bool Union(T a, T b)
    {
        int ra = FindRoot(GetOrAddIndex(a));
        int rb = FindRoot(GetOrAddIndex(b));

        if (ra == rb)
        {
            // Both elements may still have been freshly added above (two new singletons collapse here only
            // when a and b are equal); _version is bumped by GetOrAddIndex in that case, so nothing to do.
            return false;
        }

        // Union by size: hang the smaller tree under the larger root so trees stay shallow.
        if (_size[ra] < _size[rb])
            (ra, rb) = (rb, ra);

        _parent[rb] = ra;
        _size[ra] += _size[rb];
        _setCount--;
        _version++;
        return true;
    }

    /// <summary>
    /// Returns the representative element of the set containing <paramref name="element"/>. Two elements are
    /// in the same set if and only if their representatives are equal. The representative is stable only
    /// between mutations — a later <see cref="Union"/> may change which element represents the set.
    /// </summary>
    /// <param name="element">The element whose set representative to return.</param>
    /// <returns>The representative element of <paramref name="element"/>'s set.</returns>
    /// <exception cref="KeyNotFoundException"><paramref name="element"/> is not present.</exception>
    public T Find(T element)
    {
        if (!_indexOf.TryGetValue(element, out int index))
            throw new KeyNotFoundException("The element is not present in the disjoint-set.");

        return _elements[FindRoot(index)];
    }

    /// <summary>
    /// Returns the representative element of the set containing <paramref name="element"/> without throwing
    /// when it is absent.
    /// </summary>
    /// <param name="element">The element whose set representative to return.</param>
    /// <param name="representative">
    /// When this method returns <c>true</c>, the representative element; otherwise <c>default</c>.
    /// </param>
    /// <returns><c>true</c> if the element is present; otherwise <c>false</c>.</returns>
    public bool TryFind(T element, out T representative)
    {
        if (!_indexOf.TryGetValue(element, out int index))
        {
            representative = default!;
            return false;
        }

        representative = _elements[FindRoot(index)];
        return true;
    }

    /// <summary>
    /// Determines whether <paramref name="a"/> and <paramref name="b"/> are in the same set. This is a pure
    /// query: unlike <see cref="Union"/> it never adds a missing element, so if either is absent the result
    /// is <c>false</c> and the partition is left untouched.
    /// </summary>
    /// <param name="a">The first element.</param>
    /// <param name="b">The second element.</param>
    /// <returns><c>true</c> if both are present and in the same set; otherwise <c>false</c>.</returns>
    public bool Connected(T a, T b)
    {
        if (!_indexOf.TryGetValue(a, out int ia) || !_indexOf.TryGetValue(b, out int ib))
            return false;

        return FindRoot(ia) == FindRoot(ib);
    }

    /// <summary>
    /// Returns the number of elements in the set containing <paramref name="element"/>.
    /// </summary>
    /// <param name="element">The element whose set size to return.</param>
    /// <returns>The number of elements in <paramref name="element"/>'s set (at least <c>1</c>).</returns>
    /// <exception cref="KeyNotFoundException"><paramref name="element"/> is not present.</exception>
    public int ComponentSize(T element)
    {
        if (!_indexOf.TryGetValue(element, out int index))
            throw new KeyNotFoundException("The element is not present in the disjoint-set.");

        return _size[FindRoot(index)];
    }

    /// <summary>
    /// Materializes the current partition as a list of sets, each an <see cref="IReadOnlyList{T}"/> of the
    /// elements in one disjoint set. Elements appear in insertion order within each group, and the groups
    /// themselves in the order their first (earliest-inserted) member appears. Runs in <c>O(n)</c> and takes
    /// a snapshot — the returned lists do not track later mutations.
    /// </summary>
    /// <returns>The disjoint sets as a list of element groups. Its <c>Count</c> equals <see cref="SetCount"/>.</returns>
    public IReadOnlyList<IReadOnlyList<T>> GetComponents()
    {
        var result = new List<IReadOnlyList<T>>(_setCount);
        if (_count == 0)
            return result;

        // Map each root slot to its group's position in the result, then bucket every element under its
        // root. Elements are scanned in ascending (insertion-order) slot order, so a group is created the
        // first time any of its members is seen — deterministic ordering, no allocation-order dependence.
        var groupOf = new Dictionary<int, int>(_setCount);
        for (int i = 0; i < _count; i++)
        {
            int root = FindRoot(i);
            if (!groupOf.TryGetValue(root, out int g))
            {
                g = result.Count;
                groupOf[root] = g;
                result.Add(new List<T>());
            }

            ((List<T>)result[g]).Add(_elements[i]);
        }

        return result;
    }

    /// <summary>Removes all elements, resetting the disjoint-set to empty.</summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        // Clear reference slots so the arrays don't pin removed elements; ints need no clearing.
        if (System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            Array.Clear(_elements, 0, _count);

        _indexOf.Clear();
        _count = 0;
        _setCount = 0;
        _version++;
    }

    /// <summary>Returns an enumerator over the elements in insertion order.</summary>
    /// <returns>A struct enumerator over the elements.</returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- internals ---------------------------------------------------------------------------------

    // Walks to the root of index's tree, applying path halving (point each node at its grandparent) so
    // repeated queries flatten the tree. Iterative and allocation-free.
    private int FindRoot(int index)
    {
        while (_parent[index] != index)
        {
            _parent[index] = _parent[_parent[index]];
            index = _parent[index];
        }

        return index;
    }

    private int GetOrAddIndex(T element)
    {
        if (_indexOf.TryGetValue(element, out int index))
            return index;

        index = AddNew(element);
        _version++;
        return index;
    }

    // Appends element as a fresh singleton root and returns its slot. Does not bump _version (callers do).
    private int AddNew(T element)
    {
        if (_count == _elements.Length)
            Grow();

        int index = _count;
        _elements[index] = element;
        _parent[index] = index;
        _size[index] = 1;
        _indexOf[element] = index;
        _count++;
        _setCount++;
        return index;
    }

    private void Grow()
    {
        int newCapacity = _elements.Length == 0 ? DefaultCapacity : _elements.Length * 2;
        Array.Resize(ref _elements, newCapacity);
        Array.Resize(ref _parent, newCapacity);
        Array.Resize(ref _size, newCapacity);
    }

    /// <summary>A struct enumerator over a <see cref="DisjointSet{T}"/>'s elements in insertion order.</summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly DisjointSet<T> _set;
        private readonly int _version;
        private int _index;
        private T _current;

        internal Enumerator(DisjointSet<T> set)
        {
            _set = set;
            _version = set._version;
            _index = 0;
            _current = default!;
        }

        /// <summary>Gets the element at the current position of the enumerator.</summary>
        public readonly T Current => _current;

        readonly object? IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next element.</summary>
        /// <returns><c>true</c> if there is a next element; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The disjoint-set was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("The disjoint-set was modified during enumeration.");

            if (_index < _set._count)
            {
                _current = _set._elements[_index];
                _index++;
                return true;
            }

            _current = default!;
            return false;
        }

        /// <summary>Resets the enumerator to before the first element.</summary>
        /// <exception cref="InvalidOperationException">The disjoint-set was modified during enumeration.</exception>
        public void Reset()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("The disjoint-set was modified during enumeration.");

            _index = 0;
            _current = default!;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }
}
