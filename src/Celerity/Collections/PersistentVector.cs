using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Celerity.Collections;

/// <summary>
/// An <b>immutable indexed sequence</b> backed by a <b>32-way bit-partitioned trie</b> with a tail buffer:
/// every operation returns a new vector that <b>shares</b> all but one root-to-leaf path of the old one's
/// storage, an indexed read is at most seven array loads, and 31 appends out of 32 touch nothing but the
/// tail.
/// </summary>
/// <typeparam name="T">The type of the elements.</typeparam>
/// <remarks>
/// <para>
/// <c>System.Collections.Immutable</c> ships two array-shaped sequences and they sit at opposite ends of one
/// trade with nothing in between. <c>ImmutableArray&lt;T&gt;</c> is a <c>T[]</c> in a struct: indexing is a
/// single array load, but <b>every</b> append copies the whole array, so building <c>n</c> elements one at a
/// time is <c>O(n²)</c>. <c>ImmutableList&lt;T&gt;</c> is an <b>AVL tree with one heap node per element</b>:
/// appending is <c>O(log n)</c>, but so is <c>this[int]</c> — a pointer chase per level over nodes scattered
/// across the heap, each carrying a balance field and two child references, paid for every element.
/// </para>
/// <para>
/// <see cref="PersistentVector{T}"/> is the structure that closes that gap: the 32-way bit-partitioned vector
/// trie (Clojure's <c>PersistentVector</c>, Scala's <c>Vector</c>), which .NET does not ship. Elements live in
/// <b>32-element leaf arrays</b> — one object header per 32 elements rather than one AVL node per element —
/// and the index of an element is read five bits at a time to walk from the root to its leaf. A vector of
/// 100,000 elements is three internal levels deep — three node hops plus the read inside the leaf — and six
/// levels address every index an <c>int</c> can hold, so a read is never more than seven array loads.
/// The last up-to-32 elements additionally live in a <b>tail buffer</b> hanging off the root, so 31 appends
/// out of 32 copy only the tail and never touch the trie at all.
/// </para>
/// <para>
/// The documented BCL-beating workload is any sequence that is <b>built by appending and then read by
/// index</b> — an accumulated log or event list handed to readers as a snapshot, a parsed token stream, an
/// undo history, a versioned document model — where <c>ImmutableArray&lt;T&gt;</c> loses on the build and
/// <c>ImmutableList&lt;T&gt;</c> loses on every read. Measured figures are in
/// <see href="https://github.com/marius-bughiu/Celerity/blob/main/docs/api/collections.md">the API reference</see>.
/// </para>
/// <para>
/// <b>Concurrent readers need no synchronization</b>, for the reason a value needs none: no instance is ever
/// mutated after its constructor returns, so there is no state for two threads to race over. It shares that
/// with the library's build-once types (<see cref="KdTree{TValue}"/>, <see cref="SuffixArray"/>,
/// <see cref="SparseTable{T, TMonoid}"/> and the rest), and differs from them in <i>how</i> it gets there: they
/// are built once and then frozen, while this one is never mutated at all — an edit produces a new vector. That
/// does not make it a concurrency abstraction; a shared <i>variable</i> holding successive vectors still needs
/// the usual publication rules. It makes each vector a snapshot that can be handed across a thread boundary
/// without copying or locking.
/// </para>
/// <para>
/// Two deliberate omissions. There is no <c>Clear()</c>: everywhere else in this library <c>Clear()</c> means
/// in-place mutation, so the empty vector is spelled <see cref="Empty"/> rather than given a method that
/// would read like the family's mutating one. And <see cref="PersistentVector{T}"/> does not implement
/// <c>IImmutableList&lt;T&gt;</c>: that interface promises <c>Insert</c> and <c>RemoveAt</c> at an arbitrary
/// index, which a vector can only answer in <c>O(n)</c>, and shipping them behind an interface whose other
/// implementation answers them in <c>O(log n)</c> would invite exactly the misuse this type exists to avoid.
/// Appends and updates at the end are what it is for; <see cref="Builder"/> is the way to do many of them.
/// </para>
/// </remarks>
public sealed class PersistentVector<T> : IReadOnlyList<T>
{
    // The trie is 32-way: an index contributes five bits per level, so a level is indexed by
    // (index >> shift) & BranchMask and a leaf holds BranchFactor elements.
    private const int BranchBits = 5;
    private const int BranchFactor = 1 << BranchBits;
    private const int BranchMask = BranchFactor - 1;

    // An empty internal node, shared by every empty vector. Never written to: the trie is path-copied, so a
    // node reached from any vector is only ever read.
    private static readonly object?[] EmptyNode = new object?[BranchFactor];

    /// <summary>
    /// The empty vector. <see cref="RemoveLast"/> on a single-element vector returns this instance, so
    /// draining a vector one element at a time ends at the same object every empty vector starts from.
    /// </summary>
    public static readonly PersistentVector<T> Empty = new(EmptyNode, BranchBits, Array.Empty<T>(), 0);

    // Internal nodes are object?[BranchFactor] whose children are internal nodes (above _shift == BranchBits)
    // or T[] leaves (at _shift == BranchBits). The root is always an internal node, even when the vector holds
    // nothing but a tail.
    private readonly object?[] _root;

    // Bits to shift an index by to select the root's child: BranchBits for a one-level trie, then one more
    // BranchBits per level. Never zero — a leaf is reached by the last iteration of the descent, not by _shift.
    private readonly int _shift;

    // The last up-to-BranchFactor elements, exactly sized: _tail.Length is the number of elements in it, which
    // is what makes TailOffset a subtraction rather than a masked count. Elements [TailOffset, _count) live
    // here; elements [0, TailOffset) live in the trie.
    private readonly T[] _tail;

    private readonly int _count;

    private PersistentVector(object?[] root, int shift, T[] tail, int count)
    {
        _root = root;
        _shift = shift;
        _tail = tail;
        _count = count;
    }

    /// <summary>
    /// Initializes a new vector containing the elements of <paramref name="items"/> in enumeration order.
    /// </summary>
    /// <param name="items">The elements to copy into the vector.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The source is appended through a <see cref="Builder"/>, so no intermediate vector is allocated per
    /// element. What is allocated is the <c>n / 32</c> leaves the result keeps, the internal nodes it keeps
    /// (about <c>n / 1024</c> at the level above the leaves, plus the thinner levels above that), and the
    /// root-to-leaf path the builder copies on each of its <c>n / 32</c> tail pushes — <c>O(log32 n)</c> nodes
    /// per push, not <c>O(log32 n)</c> in total.
    /// </remarks>
    public PersistentVector(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var builder = new Builder();
        builder.AddRange(items);
        PersistentVector<T> built = builder.ToImmutable();

        _root = built._root;
        _shift = built._shift;
        _tail = built._tail;
        _count = built._count;
    }

    /// <summary>
    /// Gets the number of elements in the vector.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets a value indicating whether the vector contains no elements.
    /// </summary>
    public bool IsEmpty => _count == 0;

    /// <summary>
    /// Gets the element at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The zero-based index of the element to read.</param>
    /// <returns>The element at <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or not less than <see cref="Count"/>.
    /// </exception>
    /// <remarks>
    /// The most recently appended (up to) 32 elements live in the tail buffer and are answered with no trie
    /// descent at all. Everything below that costs one array load per level, at most seven.
    /// </remarks>
    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be within the vector.");

            return LeafFor(index)[index & BranchMask];
        }
    }

    // The first index that lives in the tail. Always a multiple of BranchFactor, because the trie holds whole
    // leaves — which is what lets the enumerator refresh its leaf exactly when (index & BranchMask) == 0.
    private int TailOffset => _count - _tail.Length;

    /// <summary>
    /// Returns a vector with <paramref name="value"/> appended to the end. This vector is unchanged.
    /// </summary>
    /// <param name="value">The element to append.</param>
    /// <returns>A vector of <c>Count + 1</c> elements sharing this one's storage.</returns>
    /// <exception cref="InvalidOperationException">
    /// The vector already holds <see cref="int.MaxValue"/> elements.
    /// </exception>
    /// <remarks>
    /// Thirty-one appends out of thirty-two are <c>O(1)</c>: they copy only the tail — at most 32 elements —
    /// and reuse the trie by reference. The thirty-second pushes the full tail into the trie as a leaf, which
    /// path-copies <c>O(log32 n)</c> internal nodes. Dividing that by 32 does not remove the logarithm, so the
    /// amortized bound is <c>O(log32 n)</c> rather than <c>O(1)</c> — but the base is 32, so the term is at
    /// most seven nodes for any vector an <c>int</c> can index, and it is paid once per 32 appends.
    /// </remarks>
    public PersistentVector<T> Add(T value)
    {
        ThrowIfFull(_count);

        // Room in the tail: copy it, and the trie is shared by reference.
        if (_tail.Length < BranchFactor)
        {
            var grownTail = new T[_tail.Length + 1];
            Array.Copy(_tail, grownTail, _tail.Length);
            grownTail[_tail.Length] = value;
            return new PersistentVector<T>(_root, _shift, grownTail, _count + 1);
        }

        // The tail is full and becomes a leaf. It is never written to again, so it can be adopted by reference.
        object?[] newRoot;
        int newShift = _shift;

        if ((_count >> BranchBits) > (1 << _shift))
        {
            // The trie is full at this depth: grow a level, with the old root as the new root's first child.
            newRoot = new object?[BranchFactor];
            newRoot[0] = _root;
            newRoot[1] = NewPath(_shift, _tail);
            newShift += BranchBits;
        }
        else
        {
            newRoot = PushTail(_shift, _root, _tail, _count);
        }

        return new PersistentVector<T>(newRoot, newShift, [value], _count + 1);
    }

    /// <summary>
    /// Returns a vector with the elements of <paramref name="items"/> appended in enumeration order. This
    /// vector is unchanged.
    /// </summary>
    /// <param name="items">The elements to append.</param>
    /// <returns>
    /// A vector holding this one's elements followed by <paramref name="items"/>, or this vector when
    /// <paramref name="items"/> is empty.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The appends run through one <see cref="Builder"/>, so a range of <c>m</c> elements allocates one
    /// intermediate vector rather than <c>m</c> of them.
    /// </remarks>
    public PersistentVector<T> AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var builder = new Builder(this);
        builder.AddRange(items);
        return builder.Count == _count ? this : builder.ToImmutable();
    }

    /// <summary>
    /// Returns a vector identical to this one except that the element at <paramref name="index"/> is
    /// <paramref name="value"/>. This vector is unchanged.
    /// </summary>
    /// <param name="index">The zero-based index of the element to replace.</param>
    /// <param name="value">The replacement element.</param>
    /// <returns>A vector of the same length sharing all storage outside the replaced element's path.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is negative or not less than <see cref="Count"/>.
    /// </exception>
    /// <remarks>
    /// Copying is confined to the root-to-leaf path: <c>O(log32 n)</c> internal nodes and one leaf, each of 32
    /// slots. Every other leaf is shared with this vector.
    /// </remarks>
    public PersistentVector<T> SetItem(int index, T value)
    {
        if ((uint)index >= (uint)_count)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be within the vector.");

        if (index >= TailOffset)
        {
            var newTail = new T[_tail.Length];
            Array.Copy(_tail, newTail, _tail.Length);
            newTail[index & BranchMask] = value;
            return new PersistentVector<T>(_root, _shift, newTail, _count);
        }

        return new PersistentVector<T>((object?[])DoAssoc(_shift, _root, index, value), _shift, _tail, _count);
    }

    /// <summary>
    /// Returns a vector with the last element removed. This vector is unchanged.
    /// </summary>
    /// <returns>
    /// A vector of <c>Count - 1</c> elements sharing this one's storage, or <see cref="Empty"/> when this
    /// vector holds a single element.
    /// </returns>
    /// <exception cref="InvalidOperationException">The vector is empty.</exception>
    /// <remarks>
    /// When the tail holds more than one element the trie is untouched. When it does not, the trie's last leaf
    /// becomes the new tail <i>by reference</i> — it is immutable, so it needs no copy — and the path down to
    /// it is dropped, collapsing a level when that leaves the root with a single child.
    /// </remarks>
    public PersistentVector<T> RemoveLast()
    {
        if (_count == 0)
            throw new InvalidOperationException("The vector is empty.");

        if (_count == 1)
            return Empty;

        if (_tail.Length > 1)
        {
            var shrunkTail = new T[_tail.Length - 1];
            Array.Copy(_tail, shrunkTail, shrunkTail.Length);
            return new PersistentVector<T>(_root, _shift, shrunkTail, _count - 1);
        }

        // The tail holds the single element being removed, so the trie's last leaf becomes the new tail.
        T[] newTail = LeafFor(_count - 2);
        object?[] newRoot = PopTail(_shift, _root, _count) ?? EmptyNode;
        int newShift = _shift;

        if (newShift > BranchBits && newRoot[1] is null)
        {
            newRoot = (object?[])newRoot[0]!;
            newShift -= BranchBits;
        }

        return new PersistentVector<T>(newRoot, newShift, newTail, _count - 1);
    }

    /// <summary>
    /// Copies the vector's elements into <paramref name="array"/>, starting at <paramref name="arrayIndex"/>.
    /// </summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> to start writing at.</param>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="array"/> has too little room after <paramref name="arrayIndex"/>.
    /// </exception>
    /// <remarks>
    /// Whole leaves are copied with <see cref="Array.Copy(Array, int, Array, int, int)"/>, so the cost is one
    /// bulk copy per 32 elements rather than an indexed read per element.
    /// </remarks>
    public void CopyTo(T[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);

        if (arrayIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex), arrayIndex, "Index must be non-negative.");

        if (array.Length - arrayIndex < _count)
            throw new ArgumentException("The destination array is too small.", nameof(array));

        for (int i = 0; i < _count; i += BranchFactor)
        {
            T[] leaf = LeafFor(i);
            Array.Copy(leaf, 0, array, arrayIndex + i, leaf.Length);
        }
    }

    /// <summary>
    /// Copies the vector's elements into a new array.
    /// </summary>
    /// <returns>A new array holding the vector's elements in order.</returns>
    public T[] ToArray()
    {
        if (_count == 0)
            return Array.Empty<T>();

        var array = new T[_count];
        CopyTo(array, 0);
        return array;
    }

    /// <summary>
    /// Creates a mutable builder seeded with this vector's elements, for making many changes without
    /// allocating a vector per change.
    /// </summary>
    /// <returns>A builder holding this vector's elements.</returns>
    public Builder ToBuilder() => new(this);

    /// <summary>
    /// Returns an enumerator that walks the vector in index order.
    /// </summary>
    /// <returns>A struct enumerator over the elements.</returns>
    public Enumerator GetEnumerator() => new(this);

    /// <inheritdoc/>
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // The leaf array holding `index`. Reads five bits of the index per level; the last iteration lands on the
    // T[] leaf, which is why the loop stops at level > 0 rather than descending _shift / BranchBits times.
    private T[] LeafFor(int index)
    {
        if (index >= TailOffset)
            return _tail;

        object node = _root;
        for (int level = _shift; level > 0; level -= BranchBits)
            node = ((object?[])node)[(index >> level) & BranchMask]!;

        return (T[])node;
    }

    // Refuses an append that would wrap the count negative. Both Add paths route through it so the rule is
    // stated once.
    [ExcludeFromCodeCoverage(Justification = "Needs int.MaxValue elements — 8 GiB of leaf arrays for a " +
        "four-byte element type, and more for anything wider. The check exists so the count cannot silently " +
        "wrap rather than because a test can reach it.")]
    private static void ThrowIfFull(int count)
    {
        if (count == int.MaxValue)
            throw new InvalidOperationException("A vector cannot hold more than int.MaxValue elements.");
    }

    // A fresh internal node holding the same children. Array.Clone goes through the runtime's general
    // cloning path and returns object; an explicit allocation plus Array.Copy is measurably cheaper at this
    // size, and every path copy in the type runs through here.
    private static object?[] CopyNode(object?[] node)
    {
        var copy = new object?[BranchFactor];
        Array.Copy(node, copy, BranchFactor);
        return copy;
    }

    // Wraps `leaf` in `level / BranchBits` fresh internal nodes, so it hangs at the right depth under a root
    // that has just grown. level == 0 means the leaf is already at the right depth.
    private static object NewPath(int level, T[] leaf)
    {
        if (level == 0)
            return leaf;

        var node = new object?[BranchFactor];
        node[0] = NewPath(level - BranchBits, leaf);
        return node;
    }

    // Path-copies `parent` with `leaf` inserted at the slot the last element's index selects, creating any
    // missing internal nodes on the way down. `count` is the length before the append, so `count - 1` is that
    // index.
    private static object?[] PushTail(int level, object?[] parent, T[] leaf, int count)
    {
        int subIndex = ((count - 1) >> level) & BranchMask;
        var copy = CopyNode(parent);

        if (level == BranchBits)
        {
            copy[subIndex] = leaf;
        }
        else
        {
            object? child = parent[subIndex];
            copy[subIndex] = child is null
                ? NewPath(level - BranchBits, leaf)
                : PushTail(level - BranchBits, (object?[])child, leaf, count);
        }

        return copy;
    }

    // Path-copies `node` with the trie's last leaf removed, returning null when that empties the node so the
    // caller can drop it in turn. `count` is the length before the removal.
    private static object?[]? PopTail(int level, object?[] node, int count)
    {
        int subIndex = ((count - 2) >> level) & BranchMask;

        if (level > BranchBits)
        {
            object?[]? newChild = PopTail(level - BranchBits, (object?[])node[subIndex]!, count);
            if (newChild is null && subIndex == 0)
                return null;

            var copy = CopyNode(node);
            copy[subIndex] = newChild;
            return copy;
        }

        if (subIndex == 0)
            return null;

        var trimmed = CopyNode(node);
        trimmed[subIndex] = null;
        return trimmed;
    }

    // Path-copies the root-to-leaf path for `index`, replacing the element there. Typed as object because the
    // recursion bottoms out on a T[] leaf while every level above it is an object?[].
    private static object DoAssoc(int level, object node, int index, T value)
    {
        if (level == 0)
        {
            var source = (T[])node;
            var leaf = new T[source.Length];
            Array.Copy(source, leaf, source.Length);
            leaf[index & BranchMask] = value;
            return leaf;
        }

        var copy = CopyNode((object?[])node);
        int subIndex = (index >> level) & BranchMask;
        copy[subIndex] = DoAssoc(level - BranchBits, copy[subIndex]!, index, value);
        return copy;
    }

    /// <summary>
    /// A mutable accumulator that produces <see cref="PersistentVector{T}"/> instances without allocating one
    /// per element.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The builder owns its tail buffer outright and writes into it in place, so 31 appends out of 32 are a
    /// single array store. The thirty-second pushes the tail into the trie — path-copying <c>O(log32 n)</c>
    /// internal nodes, exactly as <see cref="Add"/> does — and then takes a fresh buffer, so no array is ever
    /// shared between the builder and a vector it has already handed out.
    /// </para>
    /// <para>
    /// <see cref="ToImmutable"/> is therefore <c>O(32)</c> — it copies the live part of the tail and adopts the
    /// trie by reference — and may be called as often as you like, including between further appends. The
    /// builder is not thread-safe; the vectors it produces are.
    /// </para>
    /// </remarks>
    public sealed class Builder
    {
        private object?[] _root;
        private int _shift;

        // Always BranchFactor long and owned by this builder, with _tailLength elements live. Distinct from
        // the vector's exactly-sized tail, which is why ToImmutable copies rather than adopts it.
        private T[] _tail;
        private int _tailLength;
        private int _count;

        /// <summary>
        /// Initializes a new, empty builder.
        /// </summary>
        public Builder()
            : this(Empty)
        {
        }

        internal Builder(PersistentVector<T> source)
        {
            _root = source._root;
            _shift = source._shift;
            _count = source._count;
            _tail = new T[BranchFactor];
            _tailLength = source._tail.Length;
            Array.Copy(source._tail, _tail, _tailLength);
        }

        /// <summary>
        /// Gets the number of elements the builder currently holds.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Gets or sets the element at <paramref name="index"/>.
        /// </summary>
        /// <param name="index">The zero-based index of the element.</param>
        /// <returns>The element at <paramref name="index"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/> is negative or not less than <see cref="Count"/>.
        /// </exception>
        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)_count)
                    throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be within the builder.");

                return index >= _count - _tailLength
                    ? _tail[index & BranchMask]
                    : LeafIn(_root, _shift, index)[index & BranchMask];
            }

            set
            {
                if ((uint)index >= (uint)_count)
                    throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be within the builder.");

                if (index >= _count - _tailLength)
                    _tail[index & BranchMask] = value;
                else
                    _root = (object?[])DoAssoc(_shift, _root, index, value);
            }
        }

        /// <summary>
        /// Appends <paramref name="value"/> to the end of the builder.
        /// </summary>
        /// <param name="value">The element to append.</param>
        /// <exception cref="InvalidOperationException">
        /// The builder already holds <see cref="int.MaxValue"/> elements.
        /// </exception>
        public void Add(T value)
        {
            ThrowIfFull(_count);

            if (_tailLength == BranchFactor)
            {
                PushOwnedTail();
                _tail = new T[BranchFactor];
                _tailLength = 0;
            }

            _tail[_tailLength++] = value;
            _count++;
        }

        /// <summary>
        /// Appends the elements of <paramref name="items"/> in enumeration order.
        /// </summary>
        /// <param name="items">The elements to append.</param>
        /// <exception cref="ArgumentNullException"><paramref name="items"/> is <c>null</c>.</exception>
        public void AddRange(IEnumerable<T> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            foreach (T item in items)
                Add(item);
        }

        /// <summary>
        /// Produces an immutable vector holding the builder's current elements. The builder stays usable, and
        /// further changes to it do not affect any vector it has already returned.
        /// </summary>
        /// <returns>A vector holding the builder's elements.</returns>
        public PersistentVector<T> ToImmutable()
        {
            if (_count == 0)
                return Empty;

            var exactTail = new T[_tailLength];
            Array.Copy(_tail, exactTail, _tailLength);
            return new PersistentVector<T>(_root, _shift, exactTail, _count);
        }

        // Moves the full owned tail into the trie. The builder hands the array over by reference and takes a
        // fresh one, so nothing that has been published can be written to again.
        private void PushOwnedTail()
        {
            if ((_count >> BranchBits) > (1 << _shift))
            {
                var grown = new object?[BranchFactor];
                grown[0] = _root;
                grown[1] = NewPath(_shift, _tail);
                _root = grown;
                _shift += BranchBits;
            }
            else
            {
                _root = PushTail(_shift, _root, _tail, _count);
            }
        }

        // The builder's trie descent. Separate from the instance LeafFor because the builder's tail is not
        // exactly sized, so the "is this in the tail?" test differs; the descent itself is identical.
        private static T[] LeafIn(object?[] root, int shift, int index)
        {
            object node = root;
            for (int level = shift; level > 0; level -= BranchBits)
                node = ((object?[])node)[(index >> level) & BranchMask]!;

            return (T[])node;
        }
    }

    /// <summary>
    /// Walks a <see cref="PersistentVector{T}"/> in index order, refreshing its leaf once per 32 elements
    /// rather than descending the trie per element.
    /// </summary>
    /// <remarks>
    /// The vector cannot be modified, so this enumerator carries no version check and nothing can invalidate
    /// it. Once <see cref="MoveNext"/> has returned <c>false</c> it keeps returning <c>false</c>, and
    /// <see cref="Current"/> is <c>default(T)</c> both before the first <see cref="MoveNext"/> and after the
    /// last.
    /// </remarks>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly PersistentVector<T> _vector;
        private T[]? _leaf;
        private int _index;

        // Held in a field rather than recomputed from _leaf and _index on every read, which is what the rest
        // of the collection family does and what gives Current a defined value outside the sequence: before
        // the first MoveNext and after the last, it is default(T) rather than a dereference of a null leaf or
        // a stale re-read of the final element.
        private T _current;

        internal Enumerator(PersistentVector<T> vector)
        {
            _vector = vector;
            _leaf = null;
            _index = -1;
            _current = default!;
        }

        /// <summary>
        /// Gets the element at the enumerator's current position, or <c>default(T)</c> before the first
        /// <see cref="MoveNext"/> and after the last — matching the rest of the collection family and
        /// <see cref="List{T}.Enumerator"/>'s public <c>Current</c>.
        /// </summary>
        public readonly T Current => _current;

        /// <inheritdoc/>
        readonly object? IEnumerator.Current => Current;

        /// <summary>
        /// Advances to the next element.
        /// </summary>
        /// <returns><c>true</c> if there is another element; otherwise <c>false</c>.</returns>
        public bool MoveNext()
        {
            int next = _index + 1;
            if (next >= _vector._count)
            {
                _current = default!;
                return false;
            }

            // Leaves start at multiples of BranchFactor and the tail starts at one too, so a new leaf is needed
            // exactly when the low index bits wrap — including on the first call, which always lands on zero.
            if ((next & BranchMask) == 0)
                _leaf = _vector.LeafFor(next);

            _current = _leaf![next & BranchMask];
            _index = next;
            return true;
        }

        /// <summary>
        /// Resets the enumerator to its initial position, before the first element.
        /// </summary>
        public void Reset()
        {
            _leaf = null;
            _index = -1;
            _current = default!;
        }

        /// <summary>
        /// Releases the resources used by the enumerator. The enumerator holds none.
        /// </summary>
        public readonly void Dispose()
        {
        }
    }
}
