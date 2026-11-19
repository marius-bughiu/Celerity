using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// A <b>lazy segment tree</b>: a fixed-length, array-backed sequence that applies an update to every element of a
/// half-open range <i>and</i> answers the aggregate of any range, both in <c>O(log n)</c> — range add with range
/// minimum, range assign with range sum, and any other update that distributes over the fold.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <typeparam name="TUpdate">The update type the action applies.</typeparam>
/// <typeparam name="TMonoid">
/// The fold. Constrained to <c>struct, IMonoid&lt;T&gt;</c> so the JIT specializes the tree for it and inlines
/// the combine, as <see cref="SegmentTree{T, TMonoid}"/> does.
/// </typeparam>
/// <typeparam name="TAction">
/// The range update. Constrained to <c>struct, IRangeAction&lt;T, TUpdate&gt;</c> for the same reason; it must be
/// lawful with <typeparamref name="TMonoid"/> — see <see cref="IRangeAction{T, TUpdate}"/>.
/// </typeparam>
/// <remarks>
/// <para>
/// This is the type <see cref="SegmentTree{T, TMonoid}"/> points to when it declines range updates. That tree
/// updates one element at a time, so adding to every element of <c>[a, b)</c> costs <c>(b − a)</c> point updates,
/// <c>O((b − a) log n)</c>; a plain array makes the update a loop and every query a loop. Here both are
/// <c>O(log n)</c>: an update rewrites only the <c>O(log n)</c> nodes that tile its range and parks itself on them,
/// and a later operation pushes a parked update one level down only where it has to look underneath. The BCL has
/// no range-aggregate structure of either kind. The workloads are the ones where range writes and range reads
/// interleave: reservations ("book every slot of <c>[a, b)</c>", "what is the peak load over <c>[c, e)</c>?"),
/// span painting with span aggregates, and capacity checks over a schedule.
/// </para>
/// <para>
/// The update is a separate type argument with its own contract, <see cref="IRangeAction{T, TUpdate}"/>. Four
/// actions ship: <see cref="SumAddAction{T}"/> and <see cref="SumAssignAction{T}"/> with
/// <see cref="SumMonoid{T}"/>, <see cref="MinMaxAddAction{T}"/> with <see cref="MinMonoid{T}"/> or
/// <see cref="MaxMonoid{T}"/>, and <see cref="IdempotentAssignAction{T}"/> with any
/// <see cref="IIdempotentMonoid{T}"/>. Commutativity is not required of either the fold or the updates: queries
/// fold in index order, and pending updates compose in the order they were applied.
/// </para>
/// <para>
/// <b>Layout and cost.</b> The leaf count is padded to the next power of two, so the tree holds up to <c>4n</c>
/// aggregates plus up to <c>2n</c> pending updates — twice <see cref="SegmentTree{T, TMonoid}"/>'s cells, plus the
/// update array. The padding is what makes <see cref="Aggregate"/> a constant-time root read and lets every node
/// know how many elements it covers without storing it. Padding leaves hold the monoid's identity and never receive
/// an update: every update lands only on nodes that lie wholly inside its range.
/// </para>
/// <para>
/// <b>Reads write.</b> A query, the indexer's getter and <see cref="GetEnumerator"/> push pending updates down the
/// paths they read, so they write to the tree's internal arrays without changing any logical value. They therefore
/// do not bump the version or invalidate enumerators — but, unlike <see cref="SegmentTree{T, TMonoid}"/>, two
/// threads must not even <i>read</i> one tree concurrently. This type is not thread-safe; concurrent callers must
/// synchronize externally, reads included.
/// </para>
/// <para>
/// It implements <see cref="IReadOnlyCollection{T}"/>, not <see cref="IReadOnlyList{T}"/>: reading one element
/// pushes the updates above it, so the indexer is <c>O(log n)</c>, and an <c>IReadOnlyList</c> consumer indexing
/// in a loop would pay <c>O(n log n)</c> where it expects <c>O(n)</c>. Enumerate instead — the enumerator pushes
/// every pending update once, in <c>O(n)</c>, and then reads the leaves directly.
/// </para>
/// <para>
/// The length is fixed at construction, as it is for <see cref="SegmentTree{T, TMonoid}"/>. Every mutation bumps
/// the version — <see cref="Apply"/> over a non-empty range, an indexer assignment and <see cref="Clear"/> — even
/// when it happens to leave the values as they were, because neither contract carries an equality obligation.
/// </para>
/// </remarks>
public sealed class LazySegmentTree<T, TUpdate, TMonoid, TAction> : IReadOnlyCollection<T>
    where TMonoid : struct, IMonoid<T>
    where TAction : struct, IRangeAction<T, TUpdate>
{
    // Iterative layout over 2 * _size aggregate cells, _size a power of two: the logical element at index i lives at
    // _tree[_size + i], every internal node k in [1, _size) holds Combine(_tree[2k], _tree[2k + 1]) with its own
    // pending update already applied, and _tree[0] is unused. _pending[k] is the update parked on internal node k
    // that its children have not yet received; leaves need no slot, because an update applied to a leaf is final.
    //
    // A node at depth d covers _size >> d leaves, so the count Apply needs is derived from the node index rather than
    // stored. Leaves past _length are padding: they hold the identity, and no node that covers one ever has a
    // non-identity update parked on it, because updates land only on nodes wholly inside a range within [0, _length)
    // and are pushed only from such a node to its own children.
    private readonly T[] _tree;
    private readonly TUpdate[] _pending;
    private readonly int _length;
    private readonly int _size;
    private readonly int _log;

    // The fold and the action. Not readonly: a readonly field of a struct type is defensively copied on every member
    // call, which would put a copy on the hottest path in the type. Matches SegmentTree's monoid field.
    private TMonoid _monoid;
    private TAction _action;

    // Bumped on every mutation (Apply over a non-empty range / indexer set / Clear) so active enumerators throw on
    // concurrent modification. A push moves pending updates without changing a logical value and does not bump it.
    private int _version;

    /// <summary>
    /// The largest logical length a tree can hold. The leaf count rounds up to a power of two and the aggregate array
    /// stores two cells per leaf, so the ceiling is the largest power of two whose double fits in
    /// <see cref="Array.MaxLength"/>: <c>2^29</c>.
    /// </summary>
    private const int MaxLength = 1 << 29;

    /// <summary>
    /// Initializes a new lazy segment tree of <paramref name="length"/> logical elements, each equal to the monoid's
    /// identity.
    /// </summary>
    /// <param name="length">
    /// The number of logical elements. Must be non-negative and at most <c>2^29</c> (536,870,912).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="length"/> is negative, or exceeds the maximum supported length.
    /// </exception>
    public LazySegmentTree(int length)
        : this(length, default, default)
    {
    }

    /// <summary>
    /// Initializes a new lazy segment tree of <paramref name="length"/> logical elements, each equal to the identity
    /// of the supplied <paramref name="monoid"/>.
    /// </summary>
    /// <param name="length">The number of logical elements.</param>
    /// <param name="monoid">The fold to use. Pass this overload only when the fold or the action carries state.</param>
    /// <param name="action">The range update to use.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="length"/> is negative, or exceeds the maximum supported length.
    /// </exception>
    public LazySegmentTree(int length, TMonoid monoid, TAction action)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), length, "Length must be non-negative.");
        if (length > MaxLength)
            throw new ArgumentOutOfRangeException(nameof(length), length,
                $"Length must be at most {MaxLength} (the leaf count rounds up to a power of two and the layout stores two cells per leaf).");

        _monoid = monoid;
        _action = action;
        _length = length;
        (_size, _log) = SizeFor(length);
        _tree = new T[2 * _size];
        _pending = new TUpdate[_size];
        Array.Fill(_tree, _monoid.Identity);
        Array.Fill(_pending, _action.Identity);
    }

    /// <summary>
    /// Initializes a new lazy segment tree seeded with <paramref name="values"/>, built in <c>O(n)</c>. The logical
    /// element at index <c>i</c> starts equal to the <c>i</c>-th element of <paramref name="values"/>.
    /// </summary>
    /// <param name="values">The initial logical values, in enumeration order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="values"/> holds more than <c>2^29</c> elements.</exception>
    public LazySegmentTree(IEnumerable<T> values)
        : this(values, default, default)
    {
    }

    /// <summary>
    /// Initializes a new lazy segment tree seeded with <paramref name="values"/>, folded by the supplied
    /// <paramref name="monoid"/> and updated by the supplied <paramref name="action"/>, built in <c>O(n)</c>.
    /// </summary>
    /// <param name="values">The initial logical values, in enumeration order.</param>
    /// <param name="monoid">The fold to use. Pass this overload only when the fold or the action carries state.</param>
    /// <param name="action">The range update to use.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="values"/> holds more than <c>2^29</c> elements.</exception>
    public LazySegmentTree(IEnumerable<T> values, TMonoid monoid, TAction action)
    {
        ArgumentNullException.ThrowIfNull(values);

        _monoid = monoid;
        _action = action;

        T[] tree;
        int length;

        // A counted source (T[], List<T>, ...) is length-checked *before* anything is allocated and then read straight
        // into the leaf half of the backing array, so an oversized source reports the documented ArgumentException
        // instead of failing the allocation first, and no intermediate array is built.
        if (values is ICollection<T> collection)
        {
            int count = collection.Count;
            ThrowIfSourceTooLong(count, nameof(values));

            int size = SizeFor(count).Size;
            tree = new T[2 * size];
            length = CountedSource.Read(collection, ref tree, size);
            if (length != count)
            {
                // The source changed after it was counted, and the tree keeps what it yielded. The leaves start at
                // the padded size, so they move when the real length pads differently. A source that yielded more
                // than the leaf half holds always does: the buffer only grows once reading passes 2 * size.
                ThrowIfSourceTooLong(length, nameof(values));
                int exactSize = SizeFor(length).Size;
                if (exactSize != size)
                {
                    T[] exact = new T[2 * exactSize];
                    Array.Copy(tree, size, exact, exactSize, length);
                    tree = exact;
                }
            }
        }
        else
        {
            // Unknown length: materialize once, then apply the same ceiling.
            T[] seed = values.ToArray();
            ThrowIfSourceTooLong(seed.Length, nameof(values));

            length = seed.Length;
            tree = new T[2 * SizeFor(length).Size];
            seed.CopyTo(tree, tree.Length / 2);
        }

        _length = length;
        (_size, _log) = SizeFor(length);
        _tree = tree;
        _pending = new TUpdate[_size];

        Array.Fill(_tree, _monoid.Identity, _size + _length, _size - _length);
        Array.Fill(_pending, _action.Identity);

        // Linear-time build: one descending pass folds each pair of children into its parent.
        for (int k = _size - 1; k > 0; k--)
            Pull(k);
    }

    /// <summary>Gets the number of logical elements in the tree (its fixed length).</summary>
    public int Count => _length;

    /// <summary>
    /// Gets the aggregate of every logical element — equivalent to <c>Query(0, Count)</c>, and the monoid's identity
    /// for an empty tree. This is an <c>O(1)</c> root read: the root already has every update applied.
    /// </summary>
    public T Aggregate => _tree[1];

    /// <summary>
    /// Gets or sets the logical value at <paramref name="index"/>, in <c>O(log n)</c> either way: both first push
    /// the pending updates above the element down to it. Reading does not bump the version; assigning does, even when
    /// it stores the value already there.
    /// </summary>
    /// <param name="index">The zero-based logical index. Must be in <c>[0, Count)</c>.</param>
    /// <returns>The current logical value at <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_length)
                ThrowIndexOutOfRange(index);

            int leaf = index + _size;
            PushPath(leaf);
            return _tree[leaf];
        }
        set
        {
            if ((uint)index >= (uint)_length)
                ThrowIndexOutOfRange(index);

            int leaf = index + _size;
            PushPath(leaf);
            _tree[leaf] = value;
            for (int i = 1; i <= _log; i++)
                Pull(leaf >> i);

            _version++;
        }
    }

    /// <summary>
    /// Applies <paramref name="update"/> to every logical element in <c>[start, endExclusive)</c>, in
    /// <c>O(log n)</c>. An empty range (<c>start == endExclusive</c>) is a no-op and does not bump the version.
    /// </summary>
    /// <param name="start">The inclusive lower bound. Must be in <c>[0, endExclusive]</c>.</param>
    /// <param name="endExclusive">The exclusive upper bound. Must be in <c>[start, Count]</c>.</param>
    /// <param name="update">The update to apply to every element of the range.</param>
    /// <exception cref="ArgumentOutOfRangeException">The range is invalid or out of bounds.</exception>
    public void Apply(int start, int endExclusive, TUpdate update)
    {
        ValidateRange(start, endExclusive);
        if (start == endExclusive)
            return;

        int l = start + _size;
        int r = endExclusive + _size;

        // Push down every pending update on the two boundary paths, so the nodes the update lands on below them are
        // current and the refold afterwards combines up-to-date children. A boundary that falls on a node edge at a
        // level needs no push there: the whole node lies on one side of it.
        PushBoundaries(l, r);

        for (int left = l, right = r; left < right; left >>= 1, right >>= 1)
        {
            if ((left & 1) != 0)
                ApplyToNode(left++, update);
            if ((right & 1) != 0)
                ApplyToNode(--right, update);
        }

        for (int i = 1; i <= _log; i++)
        {
            if (((l >> i) << i) != l)
                Pull(l >> i);
            if (((r >> i) << i) != r)
                Pull((r - 1) >> i);
        }

        _version++;
    }

    /// <summary>
    /// Returns the aggregate of the logical elements in <c>[start, endExclusive)</c>, in <c>O(log n)</c>. An empty
    /// range (<c>start == endExclusive</c>) yields the monoid's identity. Pushes pending updates on the range's two
    /// boundary paths, which changes no logical value and does not bump the version.
    /// </summary>
    /// <param name="start">The inclusive lower bound. Must be in <c>[0, endExclusive]</c>.</param>
    /// <param name="endExclusive">The exclusive upper bound. Must be in <c>[start, Count]</c>.</param>
    /// <returns>The aggregate of the logical elements in the half-open range.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The range is invalid or out of bounds.</exception>
    public T Query(int start, int endExclusive)
    {
        ValidateRange(start, endExclusive);
        if (start == endExclusive)
            return _monoid.Identity;

        int l = start + _size;
        int r = endExclusive + _size;
        PushBoundaries(l, r);

        // Two accumulators, as in SegmentTree: nodes reached from the left arrive in increasing index order and
        // nodes reached from the right in decreasing order, so combining each side separately and the two at the end
        // keeps a non-commutative fold in index order.
        T resultLeft = _monoid.Identity;
        T resultRight = _monoid.Identity;

        for (; l < r; l >>= 1, r >>= 1)
        {
            if ((l & 1) != 0)
                resultLeft = _monoid.Combine(resultLeft, _tree[l++]);
            if ((r & 1) != 0)
                resultRight = _monoid.Combine(_tree[--r], resultRight);
        }

        return _monoid.Combine(resultLeft, resultRight);
    }

    /// <summary>
    /// Resets every logical element to the monoid's identity and discards every pending update. Runs in
    /// <c>O(n)</c>, and bumps the version unconditionally, as <see cref="SegmentTree{T, TMonoid}.Clear"/> does.
    /// </summary>
    public void Clear()
    {
        Array.Fill(_tree, _monoid.Identity);
        Array.Fill(_pending, _action.Identity);
        _version++;
    }

    /// <summary>
    /// Returns an enumerator over the logical values in index order. Pushes every pending update down to the leaves
    /// first — one <c>O(n)</c> pass, which changes no logical value and does not bump the version — so enumeration is
    /// <c>O(n)</c> overall rather than an <c>O(log n)</c> read per element.
    /// </summary>
    /// <returns>A struct enumerator over the logical values.</returns>
    public Enumerator GetEnumerator()
    {
        for (int k = 1; k < _size; k++)
            Push(k);

        return new Enumerator(this);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- internals ---------------------------------------------------------------------------------

    private static (int Size, int Log) SizeFor(int length)
    {
        int size = length <= 1 ? 1 : (int)BitOperations.RoundUpToPowerOf2((uint)length);
        return (size, BitOperations.Log2((uint)size));
    }

    // Refolds internal node k from its two children. The node's own pending update is already reflected in both
    // children only after a push; callers pull a node only once every pending update above its children has been
    // pushed past them, which PushBoundaries / PushPath guarantee.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Pull(int k) => _tree[k] = _monoid.Combine(_tree[2 * k], _tree[2 * k + 1]);

    // Applies an update to the whole of node k: rewrites its aggregate, and parks the update for its children when it
    // has any. A node at depth d covers _size >> d leaves, which is the count the action needs.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyToNode(int k, TUpdate update)
    {
        _tree[k] = _action.Apply(update, _tree[k], _size >> BitOperations.Log2((uint)k));
        if (k < _size)
            _pending[k] = _action.Compose(_pending[k], update);
    }

    // Hands internal node k's pending update to its two children and clears it.
    private void Push(int k)
    {
        TUpdate update = _pending[k];
        ApplyToNode(2 * k, update);
        ApplyToNode(2 * k + 1, update);
        _pending[k] = _action.Identity;
    }

    // Pushes every pending update on the path from the root down to (but not including) the given leaf.
    private void PushPath(int leaf)
    {
        for (int i = _log; i >= 1; i--)
            Push(leaf >> i);
    }

    // Pushes every pending update on the paths from the root to the two boundaries of [l, r) in leaf coordinates,
    // skipping the levels where a boundary falls on a node edge.
    private void PushBoundaries(int l, int r)
    {
        for (int i = _log; i >= 1; i--)
        {
            if (((l >> i) << i) != l)
                Push(l >> i);
            if (((r >> i) << i) != r)
                Push((r - 1) >> i);
        }
    }

    private void ValidateRange(int start, int endExclusive)
    {
        if ((uint)start > (uint)_length)
            throw new ArgumentOutOfRangeException(nameof(start), start,
                "start must be in the range [0, Count].");
        if ((uint)endExclusive > (uint)_length)
            throw new ArgumentOutOfRangeException(nameof(endExclusive), endExclusive,
                "endExclusive must be in the range [0, Count].");
        if (endExclusive < start)
            throw new ArgumentOutOfRangeException(nameof(endExclusive), endExclusive,
                "endExclusive must be greater than or equal to start.");
    }

    private static void ThrowIfSourceTooLong(int count, string paramName)
    {
        if (count > MaxLength)
            throw new ArgumentException(
                $"The source holds more than the maximum supported length of {MaxLength} elements.",
                paramName);
    }

    private static void ThrowIndexOutOfRange(int index) =>
        throw new ArgumentOutOfRangeException(nameof(index), index,
            "Index must be in the range [0, Count).");

    /// <summary>
    /// A struct enumerator over a <see cref="LazySegmentTree{T, TUpdate, TMonoid, TAction}"/>'s logical values in
    /// index order.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly LazySegmentTree<T, TUpdate, TMonoid, TAction> _tree;
        private readonly int _version;
        private int _index;
        private T _current;

        internal Enumerator(LazySegmentTree<T, TUpdate, TMonoid, TAction> tree)
        {
            _tree = tree;
            _version = tree._version;
            _index = 0;
            _current = default!;
        }

        /// <summary>Gets the logical value at the current position of the enumerator.</summary>
        public readonly T Current => _current;

        readonly object? IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next logical value.</summary>
        /// <returns><c>true</c> if there is a next value; otherwise <c>false</c>.</returns>
        /// <exception cref="InvalidOperationException">The tree was modified during enumeration.</exception>
        public bool MoveNext()
        {
            if (_version != _tree._version)
                throw new InvalidOperationException("The lazy segment tree was modified during enumeration.");

            // The leaves were made current when the enumerator was created, and nothing that keeps the version
            // unchanged can make them stale again: a query or an indexer read only pushes updates further down.
            if (_index < _tree._length)
            {
                _current = _tree._tree[_tree._size + _index];
                _index++;
                return true;
            }

            _current = default!;
            return false;
        }

        /// <summary>Resets the enumerator to before the first value.</summary>
        /// <exception cref="InvalidOperationException">The tree was modified during enumeration.</exception>
        public void Reset()
        {
            if (_version != _tree._version)
                throw new InvalidOperationException("The lazy segment tree was modified during enumeration.");

            _index = 0;
            _current = default!;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }
}
