using System.Collections;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// A <see cref="BTreeSet{T, TComparer}"/> ordered by <see cref="Comparer{T}.Default"/> — the convenience
/// alias that closes over <see cref="DefaultComparer{T}"/>, exactly as <see cref="IntSet{THasher}"/>'s
/// siblings front their hasher-parameterized forms.
/// </summary>
/// <typeparam name="T">The element type. Must be orderable by <see cref="Comparer{T}.Default"/>.</typeparam>
public class BTreeSet<T> : BTreeSet<T, DefaultComparer<T>>
{
    /// <summary>Initializes a new, empty set ordered by <see cref="Comparer{T}.Default"/>.</summary>
    public BTreeSet()
    {
    }

    /// <summary>
    /// Initializes a new set ordered by <see cref="Comparer{T}.Default"/> and seeded with
    /// <paramref name="source"/>. Duplicates in <paramref name="source"/> are ignored.
    /// </summary>
    /// <param name="source">The elements to insert. Enumeration order does not affect the result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public BTreeSet(IEnumerable<T> source)
        : base(source)
    {
    }
}

/// <summary>
/// A <b>sorted set backed by a B-tree</b>: elements are kept in ascending <typeparamref name="TComparer"/>
/// order across nodes that hold up to <c>31</c> elements each in flat arrays, so a lookup visits
/// <c>log₃₂(n)</c> nodes instead of chasing <c>log₂(n)</c> pointers. Adds the ordered surface a hash set
/// cannot answer — <see cref="Min"/>, <see cref="Max"/>, <see cref="TryGetLowerBound"/>,
/// <see cref="TryGetUpperBound"/>, <see cref="EnumerateRange"/>, and in-order enumeration.
/// </summary>
/// <typeparam name="T">The element type, ordered by <typeparamref name="TComparer"/>.</typeparam>
/// <typeparam name="TComparer">
/// The comparer that defines the element order. Must be a value type implementing
/// <see cref="IComparer{T}"/> so the JIT can devirtualize and inline it — an interface-typed comparer would
/// cost a virtual call for every element inspected inside a node. Use <see cref="DefaultComparer{T}"/> (or
/// the one-parameter <see cref="BTreeSet{T}"/> alias) for the natural order.
/// </typeparam>
/// <remarks>
/// <para>
/// This is <see cref="BTreeDictionary{TKey, TValue, TComparer}"/>'s set counterpart and the same argument
/// applies: the BCL's <see cref="SortedSet{T}"/> is a red-black tree with one heap object per element and
/// roughly <c>log₂(n)</c> dependent pointer chases per lookup, where this type packs up to <c>31</c> elements
/// per node and reaches the same element in about <c>log₃₂(n)</c> node visits — around <c>4</c> instead of
/// <c>20</c> potential cache misses at <c>n = 1M</c>. Because it stores no values, a node here is a single
/// element array plus its children, so the memory saving over <see cref="SortedSet{T}"/> is larger still.
/// </para>
/// <para>
/// The documented BCL-beating workload is a large ordered set under an <b>interleaved insert + membership +
/// in-order range-scan</b> load — sorted id sets, sweep-line event sets, interval endpoints. Where it does
/// <i>not</i> win: small sets (at a thousand elements <see cref="SortedSet{T}"/> is competitive), a
/// delete-dominated load (rebalancing by borrow/merge measures a few percent behind its rotations), and any
/// workload that never needs order (reach for <see cref="CeleritySet{T, THasher}"/> or
/// <see cref="IntSet{THasher}"/> instead — <c>O(1)</c> beats <c>O(log n)</c> when order is not part of the
/// question).
/// </para>
/// <para>
/// Membership is defined by <typeparamref name="TComparer"/> — two elements are the same element when the
/// comparer orders them equal. The <see cref="ISet{T}"/> algebra members materialize the right-hand side into
/// a <see cref="HashSet{T}"/>, so they compare <i>that</i> side with
/// <see cref="EqualityComparer{T}.Default"/>, matching the rest of the family. A custom comparer that treats
/// two values as equal when <see cref="EqualityComparer{T}.Default"/> does not — a case-insensitive order,
/// say — can therefore disagree with <see cref="SortedSet{T}"/> on those members alone. Like the rest of the family, <see cref="Add"/> throws
/// on a duplicate — use <see cref="TryAdd"/> when the element may already be present. A <c>null</c> element is
/// legal and <see cref="Comparer{T}.Default"/> orders it before every non-<c>null</c> one; there is no
/// out-of-band <c>default(T)</c> slot as in the hash-based family, so a value-type <c>default(T)</c> is an
/// ordinary element that sorts wherever the comparer puts it (<c>0</c> follows every negative
/// <see cref="int"/>). This type is not thread-safe; concurrent callers must synchronize externally.
/// </para>
/// </remarks>
public class BTreeSet<T, TComparer> : ISet<T>, IReadOnlySet<T>
    where TComparer : struct, IComparer<T>
{
    /// <summary>
    /// The B-tree minimum degree <c>t</c>. Every node except the root holds between <c>t - 1</c> and
    /// <c>2t - 1</c> elements, so the fan-out is up to <c>2t</c> children.
    /// </summary>
    /// <remarks>
    /// <c>t = 16</c> puts up to 31 elements in a node — 124–248 bytes for the 4- and 8-byte element types that
    /// dominate ordered-set use, i.e. two to four 64-byte cache lines, which the hardware prefetcher streams
    /// while the in-node binary search is still running. Wider nodes flatten the tree further but lengthen
    /// every split/merge memmove; narrower ones add levels, and each level is another dependent load — the
    /// cost the B-tree exists to remove. It matches <see cref="BTreeDictionary{TKey, TValue, TComparer}"/> so
    /// the two types have the same shape and the same benchmark story.
    /// </remarks>
    private const int MinDegree = 16;

    /// <summary>The maximum number of elements a node may hold in steady state (<c>2t - 1</c>).</summary>
    private const int MaxKeys = (2 * MinDegree) - 1;

    /// <summary>The minimum number of elements every non-root node holds (<c>t - 1</c>).</summary>
    private const int MinKeys = MinDegree - 1;

    // One slot of slack, so a node can transiently overflow after an insert and be split bottom-up by its
    // parent — see the matching note in BTreeDictionary.
    private const int NodeKeyCapacity = MaxKeys + 1;
    private const int NodeChildCapacity = MaxKeys + 2;

    // Enumerator path-stack depth; 16 frames is double the height an int-counted tree can ever reach.
    private const int MaxDepth = 16;

    private sealed class Node
    {
        internal readonly T[] Keys = new T[NodeKeyCapacity];
        internal Node?[]? Children;
        internal int Count;

        internal bool IsLeaf
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Children is null;
        }
    }

    // Null until the first insert, so an empty set owns no node arrays at all.
    private Node? _root;

    // Deliberately not `readonly`: an interface call through a readonly field of an unconstrained struct type
    // forces a defensive copy per call, which is the cost the struct-comparer design exists to remove.
    private TComparer _comparer;

    private int _count;

    // Bumped on every mutation that changes the observable content, so active enumerators detect concurrent
    // modification. A lookup, a failed TryAdd, and a Remove of an absent element are not mutations.
    private int _version;

    /// <summary>Initializes a new, empty set ordered by <c>default(TComparer)</c>.</summary>
    public BTreeSet()
        : this(default(TComparer))
    {
    }

    /// <summary>
    /// Initializes a new, empty set ordered by <paramref name="comparer"/>. Use this overload when
    /// <typeparamref name="TComparer"/> carries state (a culture, a sort direction, a key selector).
    /// </summary>
    /// <param name="comparer">The comparer instance defining the element order.</param>
    public BTreeSet(TComparer comparer)
    {
        _comparer = comparer;
    }

    /// <summary>
    /// Initializes a new set ordered by <c>default(TComparer)</c> and seeded with <paramref name="source"/>.
    /// Duplicates in <paramref name="source"/> are ignored.
    /// </summary>
    /// <param name="source">The elements to insert. Enumeration order does not affect the result.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public BTreeSet(IEnumerable<T> source)
        : this(source, default)
    {
    }

    /// <summary>
    /// Initializes a new set ordered by <paramref name="comparer"/> and seeded with <paramref name="source"/>.
    /// Duplicates in <paramref name="source"/> are ignored.
    /// </summary>
    /// <param name="source">The elements to insert. Enumeration order does not affect the result.</param>
    /// <param name="comparer">The comparer instance defining the element order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    public BTreeSet(IEnumerable<T> source, TComparer comparer)
    {
        ArgumentNullException.ThrowIfNull(source);

        _comparer = comparer;
        foreach (T item in source)
            Insert(item);
    }

    /// <summary>Gets the comparer that defines this set's element order.</summary>
    public TComparer Comparer => _comparer;

    /// <summary>Gets the number of elements in the set.</summary>
    public int Count => _count;

    /// <summary>
    /// Gets the smallest element, in <c>O(log n)</c>.
    /// </summary>
    /// <returns>The first element in order.</returns>
    /// <exception cref="InvalidOperationException">The set is empty.</exception>
    public T Min =>
        TryGetMin(out T item) ? item : throw new InvalidOperationException("The set is empty.");

    /// <summary>
    /// Gets the largest element, in <c>O(log n)</c>.
    /// </summary>
    /// <returns>The last element in order.</returns>
    /// <exception cref="InvalidOperationException">The set is empty.</exception>
    public T Max =>
        TryGetMax(out T item) ? item : throw new InvalidOperationException("The set is empty.");

    /// <summary>
    /// Adds an element, throwing when it is already present.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <exception cref="ArgumentException"><paramref name="item"/> is already present.</exception>
    public void Add(T item)
    {
        if (!Insert(item))
            throw new ArgumentException($"The element '{item}' already exists in the set.", nameof(item));
    }

    /// <summary>
    /// Adds an element only when it is absent. The non-throwing counterpart of <see cref="Add"/>.
    /// </summary>
    /// <param name="item">The element to add.</param>
    /// <returns><c>true</c> if the element was added; <c>false</c> if it was already present.</returns>
    public bool TryAdd(T item) => Insert(item);

    /// <summary>Determines whether <paramref name="item"/> is present, in <c>O(log n)</c>.</summary>
    /// <param name="item">The element to look for.</param>
    /// <returns><c>true</c> if the element is present.</returns>
    public bool Contains(T item)
    {
        Node? node = _root;
        while (node is not null)
        {
            int index = Find(node, item, out bool found);
            if (found)
                return true;

            node = node.IsLeaf ? null : node.Children![index];
        }

        return false;
    }

    /// <summary>Removes <paramref name="item"/>, in <c>O(log n)</c>.</summary>
    /// <param name="item">The element to remove.</param>
    /// <returns><c>true</c> if an element was removed; <c>false</c> if it was absent.</returns>
    public bool Remove(T item)
    {
        if (_root is null || !RemoveFrom(_root, item))
            return false;

        // The root is the one node allowed to fall below MinKeys. When it empties it is either dropped (the
        // set is now empty) or replaced by its only child, which is how the tree loses a level.
        if (_root.Count == 0)
            _root = _root.IsLeaf ? null : _root.Children![0];

        _count--;
        _version++;
        return true;
    }

    /// <summary>Removes every element. The tree releases all of its nodes.</summary>
    public void Clear()
    {
        if (_count == 0 && _root is null)
            return;

        _root = null;
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Gets the smallest element.
    /// </summary>
    /// <param name="item">The first element in order, or <c>default</c> when the set is empty.</param>
    /// <returns><c>true</c> if the set is non-empty.</returns>
    public bool TryGetMin(out T item)
    {
        Node? node = _root;
        if (node is null)
        {
            item = default!;
            return false;
        }

        while (!node.IsLeaf)
            node = node.Children![0]!;

        item = node.Keys[0];
        return true;
    }

    /// <summary>
    /// Gets the largest element.
    /// </summary>
    /// <param name="item">The last element in order, or <c>default</c> when the set is empty.</param>
    /// <returns><c>true</c> if the set is non-empty.</returns>
    public bool TryGetMax(out T item)
    {
        Node? node = _root;
        if (node is null)
        {
            item = default!;
            return false;
        }

        while (!node.IsLeaf)
            node = node.Children![node.Count]!;

        item = node.Keys[node.Count - 1];
        return true;
    }

    /// <summary>
    /// Finds the smallest element that is <b>greater than or equal to</b> <paramref name="item"/> — the
    /// <c>lower_bound</c> of the ordered containers — in <c>O(log n)</c>. An exact match is its own lower
    /// bound.
    /// </summary>
    /// <param name="item">The element to bound.</param>
    /// <param name="bound">The bounding element, or <c>default</c> when every element is smaller.</param>
    /// <returns><c>true</c> if such an element exists.</returns>
    public bool TryGetLowerBound(T item, out T bound)
    {
        Node? node = _root;
        bool haveCandidate = false;
        bound = default!;

        while (node is not null)
        {
            int index = Find(node, item, out bool found);
            if (found)
            {
                bound = node.Keys[index];
                return true;
            }

            // Keys[index] is this node's first element above `item`; anything found deeper is smaller still,
            // so each level can only improve the candidate.
            if (index < node.Count)
            {
                bound = node.Keys[index];
                haveCandidate = true;
            }

            node = node.IsLeaf ? null : node.Children![index];
        }

        return haveCandidate;
    }

    /// <summary>
    /// Finds the smallest element <b>strictly greater than</b> <paramref name="item"/> — the
    /// <c>upper_bound</c> of the ordered containers — in <c>O(log n)</c>.
    /// </summary>
    /// <param name="item">The element to bound.</param>
    /// <param name="bound">The bounding element, or <c>default</c> when no element is larger.</param>
    /// <returns><c>true</c> if such an element exists.</returns>
    public bool TryGetUpperBound(T item, out T bound)
    {
        Node? node = _root;
        bool haveCandidate = false;
        bound = default!;

        while (node is not null)
        {
            int index = Find(node, item, out bool found);

            // On an exact hit the bound lies strictly to the right of the match, so step past it.
            if (found)
                index++;

            if (index < node.Count)
            {
                bound = node.Keys[index];
                haveCandidate = true;
            }

            node = node.IsLeaf ? null : node.Children![index];
        }

        return haveCandidate;
    }

    /// <summary>
    /// Enumerates, in ascending order, every element in the half-open range
    /// <c>[fromInclusive, toExclusive)</c>. The scan seeks to the lower bound in <c>O(log n)</c> and then
    /// walks the tree's contiguous node arrays, so it costs <c>O(log n + k)</c> for <c>k</c> results rather
    /// than a full <c>O(n)</c> filter.
    /// </summary>
    /// <param name="fromInclusive">The inclusive lower bound of the range.</param>
    /// <param name="toExclusive">The exclusive upper bound of the range.</param>
    /// <returns>An allocation-free enumerable over the matching elements.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="toExclusive"/> orders before <paramref name="fromInclusive"/>.
    /// </exception>
    public RangeEnumerable EnumerateRange(T fromInclusive, T toExclusive)
    {
        if (_comparer.Compare(fromInclusive, toExclusive) > 0)
            throw new ArgumentException(
                "toExclusive must not order before fromInclusive.", nameof(toExclusive));

        return new RangeEnumerable(this, fromInclusive, toExclusive);
    }

    /// <summary>
    /// Returns a struct enumerator over the elements in ascending order. Because it is a struct and holds its
    /// traversal path inline, iterating it via <c>foreach</c> allocates nothing.
    /// </summary>
    /// <returns>A struct enumerator over this set.</returns>
    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ── ISet<T> / ICollection<T> set-algebra surface ──────────────────────────
    // Shared across the mutable set family via SetOperations, written once against the ISet<T> primitives
    // every set exposes; the semantics match BCL HashSet<T>.

    /// <summary>
    /// Modifies the set to contain all elements that are present in itself, in <paramref name="other"/>, or
    /// in both.
    /// </summary>
    /// <param name="other">The collection to union into this set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void UnionWith(IEnumerable<T> other) => SetOperations.UnionWith(this, other);

    /// <summary>Modifies the set to contain only elements that are also present in <paramref name="other"/>.</summary>
    /// <param name="other">The collection to intersect with this set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void IntersectWith(IEnumerable<T> other) => SetOperations.IntersectWith(this, other);

    /// <summary>Removes every element in <paramref name="other"/> from the set.</summary>
    /// <param name="other">The collection of elements to remove.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void ExceptWith(IEnumerable<T> other) => SetOperations.ExceptWith(this, other);

    /// <summary>
    /// Modifies the set to contain only elements that are present either in itself or in
    /// <paramref name="other"/>, but not both.
    /// </summary>
    /// <param name="other">The collection to apply the symmetric difference with.</param>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public void SymmetricExceptWith(IEnumerable<T> other) => SetOperations.SymmetricExceptWith(this, other);

    /// <summary>Determines whether the set is a subset of <paramref name="other"/>.</summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if every element of this set is in <paramref name="other"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSubsetOf(IEnumerable<T> other) => SetOperations.IsSubsetOf(this, other);

    /// <summary>Determines whether the set is a proper (strict) subset of <paramref name="other"/>.</summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of this set is in <paramref name="other"/> and <paramref name="other"/>
    /// has at least one element this set does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSubsetOf(IEnumerable<T> other) => SetOperations.IsProperSubsetOf(this, other);

    /// <summary>Determines whether the set is a superset of <paramref name="other"/>.</summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if every element of <paramref name="other"/> is in this set.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSupersetOf(IEnumerable<T> other) => SetOperations.IsSupersetOf(this, other);

    /// <summary>Determines whether the set is a proper (strict) superset of <paramref name="other"/>.</summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every element of <paramref name="other"/> is in this set and this set has at least one
    /// element <paramref name="other"/> does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSupersetOf(IEnumerable<T> other) => SetOperations.IsProperSupersetOf(this, other);

    /// <summary>Determines whether the set and <paramref name="other"/> share at least one element.</summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two share any element.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool Overlaps(IEnumerable<T> other) => SetOperations.Overlaps(this, other);

    /// <summary>Determines whether the set and <paramref name="other"/> contain the same distinct elements.</summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two contain exactly the same elements.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool SetEquals(IEnumerable<T> other) => SetOperations.SetEquals(this, other);

    /// <summary>
    /// Copies the elements of the set, in ascending order, to <paramref name="array"/> starting at
    /// <paramref name="arrayIndex"/>.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="arrayIndex"/> is negative or past the end of <paramref name="array"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="array"/> has insufficient space.</exception>
    public void CopyTo(T[] array, int arrayIndex) => SetOperations.CopyTo(this, _count, array, arrayIndex);

    // Adds the element, returning whether it was newly added (ISet<T> semantics) — the non-throwing
    // counterpart of the public throw-on-duplicate Add(T).
    bool ISet<T>.Add(T item) => TryAdd(item);

    // ICollection<T>.Add must not throw on a duplicate (unlike the public Add(T)), so it maps to TryAdd.
    void ICollection<T>.Add(T item) => TryAdd(item);

    bool ICollection<T>.IsReadOnly => false;

    // ---- internals ---------------------------------------------------------------------------------

    // Binary search among a node's Count elements. Returns the index of the match when `found`, and otherwise
    // the index of the first element greater than `item` — which doubles as the index of the child to descend
    // into. Binary search rather than a linear scan: at 31 elements it is 5 comparisons instead of up to 31,
    // and the whole array is already in cache by the time the second probe issues.
    private int Find(Node node, T item, out bool found)
    {
        T[] keys = node.Keys;
        int lo = 0;
        int hi = node.Count - 1;

        while (lo <= hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            int cmp = _comparer.Compare(keys[mid], item);
            if (cmp == 0)
            {
                found = true;
                return mid;
            }

            if (cmp < 0)
                lo = mid + 1;
            else
                hi = mid - 1;
        }

        found = false;
        return lo;
    }

    // The single insertion entry point. Returns true when the element was added, false when already present.
    private bool Insert(T item)
    {
        if (_root is null)
        {
            Node leaf = new Node();
            leaf.Keys[0] = item;
            leaf.Count = 1;
            _root = leaf;
            _count = 1;
            _version++;
            return true;
        }

        InsertOutcome outcome = InsertInto(_root, item);

        // The root split: the tree grows by one level, and the promoted median becomes the new root's only
        // element. This is the only place the tree gets taller.
        if (outcome.SplitRight is not null)
        {
            Node newRoot = new Node { Children = new Node?[NodeChildCapacity], Count = 1 };
            newRoot.Keys[0] = outcome.SplitKey;
            newRoot.Children![0] = _root;
            newRoot.Children[1] = outcome.SplitRight;
            _root = newRoot;
        }

        if (!outcome.Added)
            return false;

        _count++;
        _version++;
        return true;
    }

    // What a subtree insertion reports back to its parent.
    private struct InsertOutcome
    {
        public bool Added;          // a new element was inserted somewhere in this subtree
        public Node? SplitRight;    // non-null when this node overflowed and split
        public T SplitKey;          // the median promoted by that split
    }

    // Bottom-up insertion: descend to the leaf, insert there, and let overflow propagate back up one level at
    // a time. Nodes carry a spare slot so an overfull node is legal until its parent splits it on the way out —
    // which means a duplicate insert never restructures the tree.
    private InsertOutcome InsertInto(Node node, T item)
    {
        int index = Find(node, item, out bool found);
        if (found)
            return default;

        if (node.IsLeaf)
        {
            InsertIntoNode(node, index, item, null);
        }
        else
        {
            InsertOutcome inner = InsertInto(node.Children![index]!, item);
            if (!inner.Added)
                return inner;
            if (inner.SplitRight is null)
                return inner;

            InsertIntoNode(node, index, inner.SplitKey, inner.SplitRight);
        }

        if (node.Count <= MaxKeys)
            return new InsertOutcome { Added = true };

        SplitNode(node, out T medianKey, out Node right);
        return new InsertOutcome { Added = true, SplitKey = medianKey, SplitRight = right };
    }

    // Inserts `item` at `index`, shifting the tail right. `rightChild`, when supplied, is the right half of a
    // split child and lands at Children[index + 1].
    private static void InsertIntoNode(Node node, int index, T item, Node? rightChild)
    {
        int count = node.Count;
        Array.Copy(node.Keys, index, node.Keys, index + 1, count - index);
        node.Keys[index] = item;

        if (rightChild is not null)
        {
            Node?[] children = node.Children!;
            Array.Copy(children, index + 1, children, index + 2, count - index);
            children[index + 1] = rightChild;
        }

        node.Count = count + 1;
    }

    // Splits an overfull node in two, handing its median back to the caller for promotion. The left half stays
    // in `node`; the right half is a fresh sibling.
    private static void SplitNode(Node node, out T medianKey, out Node right)
    {
        int count = node.Count;
        int median = count / 2;
        int rightCount = count - median - 1;

        medianKey = node.Keys[median];

        right = new Node { Count = rightCount };
        Array.Copy(node.Keys, median + 1, right.Keys, 0, rightCount);

        if (!node.IsLeaf)
        {
            right.Children = new Node?[NodeChildCapacity];
            Array.Copy(node.Children!, median + 1, right.Children, 0, rightCount + 1);
            Array.Clear(node.Children!, median + 1, rightCount + 1);
        }

        // Clearing the vacated slots matters for reference-typed elements: a stale slot would otherwise keep
        // an evicted object alive for as long as the node lives.
        Array.Clear(node.Keys, median, count - median);
        node.Count = median;
    }

    // Removes `item` from the subtree rooted at `node`, which the caller guarantees is either the root or
    // holds more than MinKeys elements — the invariant that lets a deletion never back up the tree.
    private bool RemoveFrom(Node node, T item)
    {
        int index = Find(node, item, out bool found);

        if (found)
        {
            if (node.IsLeaf)
                RemoveFromLeaf(node, index);
            else
                RemoveFromInternal(node, index);

            return true;
        }

        if (node.IsLeaf)
            return false;

        // Top the child up to more than MinKeys *before* descending, so the removal below never needs to
        // rebalance upwards.
        if (node.Children![index]!.Count == MinKeys)
        {
            Fill(node, index);

            // A merge with the left sibling folds the target child into index - 1 and shortens the parent, so
            // the index can now point past the last child. Only the rightmost position can end up there.
            if (index > node.Count)
                index--;
        }

        return RemoveFrom(node.Children![index]!, item);
    }

    private static void RemoveFromLeaf(Node node, int index)
    {
        int last = node.Count - 1;
        Array.Copy(node.Keys, index + 1, node.Keys, index, last - index);
        node.Keys[last] = default!;
        node.Count = last;
    }

    // Deletes Keys[index] from an internal node by pulling up its in-order predecessor or successor — or, when
    // both flanking children are at the minimum, by merging them and deleting from the merged node.
    private void RemoveFromInternal(Node node, int index)
    {
        Node left = node.Children![index]!;
        Node right = node.Children[index + 1]!;

        if (left.Count > MinKeys)
        {
            Node cursor = left;
            while (!cursor.IsLeaf)
                cursor = cursor.Children![cursor.Count]!;

            T predecessor = cursor.Keys[cursor.Count - 1];
            node.Keys[index] = predecessor;
            RemoveFrom(left, predecessor);
        }
        else if (right.Count > MinKeys)
        {
            Node cursor = right;
            while (!cursor.IsLeaf)
                cursor = cursor.Children![0]!;

            T successor = cursor.Keys[0];
            node.Keys[index] = successor;
            RemoveFrom(right, successor);
        }
        else
        {
            // Both are minimal: fold the separator and the right child into the left one (2 * MinKeys + 1 ==
            // MaxKeys elements, so it still fits) and delete from there.
            T item = node.Keys[index];
            Merge(node, index);
            RemoveFrom(left, item);
        }
    }

    // Grows Children[index] past MinKeys by borrowing one element from a sibling, or by merging with one.
    private static void Fill(Node node, int index)
    {
        if (index > 0 && node.Children![index - 1]!.Count > MinKeys)
            BorrowFromPrevious(node, index);
        else if (index < node.Count && node.Children![index + 1]!.Count > MinKeys)
            BorrowFromNext(node, index);
        else if (index < node.Count)
            Merge(node, index);
        else
            Merge(node, index - 1);
    }

    // Rotates right: the parent's separator drops into the child's front, and the left sibling's last element
    // takes the parent's place.
    private static void BorrowFromPrevious(Node node, int index)
    {
        Node child = node.Children![index]!;
        Node sibling = node.Children[index - 1]!;

        Array.Copy(child.Keys, 0, child.Keys, 1, child.Count);
        child.Keys[0] = node.Keys[index - 1];

        if (!child.IsLeaf)
        {
            Array.Copy(child.Children!, 0, child.Children!, 1, child.Count + 1);
            child.Children![0] = sibling.Children![sibling.Count];
            sibling.Children[sibling.Count] = null;
        }

        node.Keys[index - 1] = sibling.Keys[sibling.Count - 1];
        sibling.Keys[sibling.Count - 1] = default!;

        child.Count++;
        sibling.Count--;
    }

    // Rotates left: the parent's separator is appended to the child, and the right sibling's first element
    // takes the parent's place.
    private static void BorrowFromNext(Node node, int index)
    {
        Node child = node.Children![index]!;
        Node sibling = node.Children[index + 1]!;

        child.Keys[child.Count] = node.Keys[index];

        if (!child.IsLeaf)
        {
            child.Children![child.Count + 1] = sibling.Children![0];
            Array.Copy(sibling.Children!, 1, sibling.Children!, 0, sibling.Count);
            sibling.Children[sibling.Count] = null;
        }

        node.Keys[index] = sibling.Keys[0];
        Array.Copy(sibling.Keys, 1, sibling.Keys, 0, sibling.Count - 1);
        sibling.Keys[sibling.Count - 1] = default!;

        child.Count++;
        sibling.Count--;
    }

    // Folds Children[index + 1] and the separator Keys[index] into Children[index], shrinking the parent by
    // one element and one child.
    private static void Merge(Node node, int index)
    {
        Node?[] children = node.Children!;
        Node left = children[index]!;
        Node right = children[index + 1]!;

        left.Keys[left.Count] = node.Keys[index];
        Array.Copy(right.Keys, 0, left.Keys, left.Count + 1, right.Count);

        if (!left.IsLeaf)
            Array.Copy(right.Children!, 0, left.Children!, left.Count + 1, right.Count + 1);

        left.Count += right.Count + 1;

        int lastKey = node.Count - 1;
        Array.Copy(node.Keys, index + 1, node.Keys, index, lastKey - index);
        Array.Copy(children, index + 2, children, index + 1, lastKey - index);
        node.Keys[lastKey] = default!;
        children[node.Count] = null;
        node.Count = lastKey;
    }

    // The traversal path of an in-order walk: one (node, next element index) frame per level. Held inline in
    // the enumerator structs so a foreach over a B-tree allocates nothing.
    [InlineArray(MaxDepth)]
    private struct NodeBuffer
    {
        private Node? _element0;
    }

    [InlineArray(MaxDepth)]
    private struct IndexBuffer
    {
        private int _element0;
    }

    // The shared in-order cursor behind both enumerators. The seed decides where the walk starts: the leftmost
    // leaf for a full enumeration, or the lower bound of an element for a range scan.
    private struct Cursor
    {
        private NodeBuffer _nodes;
        private IndexBuffer _indices;
        private int _depth;

        internal void PushLeftmost(Node? node)
        {
            while (node is not null)
            {
                _nodes[_depth] = node;
                _indices[_depth] = 0;
                _depth++;
                node = node.IsLeaf ? null : node.Children![0];
            }
        }

        // Seeds the path at the first element >= `item`, skipping the subtrees that lie entirely below it.
        internal void SeekLowerBound(Node? node, T item, BTreeSet<T, TComparer> owner)
        {
            while (node is not null)
            {
                int index = owner.Find(node, item, out bool found);
                _nodes[_depth] = node;
                _indices[_depth] = index;
                _depth++;

                if (found || node.IsLeaf)
                    return;

                node = node.Children![index];
            }
        }

        internal void Reset() => _depth = 0;

        // Advances to the next element in order, or reports exhaustion.
        internal bool MoveNext(out T item)
        {
            while (_depth > 0)
            {
                Node node = _nodes[_depth - 1]!;
                int index = _indices[_depth - 1];

                if (index < node.Count)
                {
                    item = node.Keys[index];
                    _indices[_depth - 1] = index + 1;

                    // Everything between this element and the next one lives in the child to its right.
                    if (!node.IsLeaf)
                        PushLeftmost(node.Children![index + 1]);

                    return true;
                }

                _depth--;
            }

            item = default!;
            return false;
        }
    }

    /// <summary>
    /// A struct enumerator over a <see cref="BTreeSet{T, TComparer}"/>'s elements in ascending order. Because
    /// it is a struct and holds its traversal path inline, iterating it via <c>foreach</c> allocates nothing.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly BTreeSet<T, TComparer> _set;
        private readonly int _version;
        private Cursor _cursor;
        private T _current;

        internal Enumerator(BTreeSet<T, TComparer> set)
        {
            _set = set;
            _version = set._version;
            _cursor = default;
            _current = default!;
            _cursor.PushLeftmost(set._root);
        }

        /// <summary>Gets the element at the current position of the enumerator.</summary>
        public readonly T Current => _current;

        readonly object? IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next element in order.</summary>
        /// <returns><c>true</c> if there is a next element; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The set was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("The set was modified during enumeration.");

            if (!_cursor.MoveNext(out T item))
            {
                _current = default!;
                return false;
            }

            _current = item;
            return true;
        }

        /// <summary>Resets the enumerator to before the first element.</summary>
        /// <exception cref="InvalidOperationException">The set was modified during enumeration.</exception>
        public void Reset()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("The set was modified during enumeration.");

            _cursor.Reset();
            _cursor.PushLeftmost(_set._root);
            _current = default!;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }

    /// <summary>
    /// The result of <see cref="EnumerateRange"/>: an allocation-free view over the elements of one range, in
    /// ascending order.
    /// </summary>
    public readonly struct RangeEnumerable : IEnumerable<T>
    {
        private readonly BTreeSet<T, TComparer> _set;
        private readonly T _from;
        private readonly T _toExclusive;

        internal RangeEnumerable(BTreeSet<T, TComparer> set, T from, T toExclusive)
        {
            _set = set;
            _from = from;
            _toExclusive = toExclusive;
        }

        /// <summary>Returns a struct enumerator over the elements in the range.</summary>
        /// <returns>A struct enumerator over the matching elements.</returns>
        public RangeEnumerator GetEnumerator() => new RangeEnumerator(_set, _from, _toExclusive);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A struct enumerator over the elements of one range of a <see cref="BTreeSet{T, TComparer}"/>, in
    /// ascending order.
    /// </summary>
    public struct RangeEnumerator : IEnumerator<T>
    {
        private readonly BTreeSet<T, TComparer> _set;
        private readonly T _from;
        private readonly T _toExclusive;
        private readonly int _version;
        private Cursor _cursor;
        private T _current;
        private bool _finished;

        internal RangeEnumerator(BTreeSet<T, TComparer> set, T from, T toExclusive)
        {
            _set = set;
            _from = from;
            _toExclusive = toExclusive;
            _version = set._version;
            _cursor = default;
            _current = default!;
            _finished = false;
            _cursor.SeekLowerBound(set._root, from, set);
        }

        /// <summary>Gets the element at the current position of the enumerator.</summary>
        public readonly T Current => _current;

        readonly object? IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next element in the range.</summary>
        /// <returns><c>true</c> if there is a next element; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The set was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("The set was modified during enumeration.");

            if (_finished || !_cursor.MoveNext(out T item))
            {
                _current = default!;
                return false;
            }

            // The walk is ascending, so the first element at or past the upper bound ends the scan for good.
            if (_set._comparer.Compare(item, _toExclusive) >= 0)
            {
                _finished = true;
                _current = default!;
                return false;
            }

            _current = item;
            return true;
        }

        /// <summary>Resets the enumerator to before the first element of the range.</summary>
        /// <exception cref="InvalidOperationException">The set was modified during enumeration.</exception>
        public void Reset()
        {
            if (_version != _set._version)
                throw new InvalidOperationException("The set was modified during enumeration.");

            _cursor.Reset();
            _cursor.SeekLowerBound(_set._root, _from, _set);
            _current = default!;
            _finished = false;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }
}
