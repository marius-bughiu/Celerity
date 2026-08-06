using System.Collections;

namespace Celerity.Collections;

/// <summary>
/// A <b>segment tree</b>: a fixed-length, array-backed sequence that answers the aggregate of any half-open
/// range under an arbitrary <b>associative</b> operation, and applies point updates, in <c>O(log n)</c> each —
/// over a single flat array of <c>2n</c> elements with no per-node object overhead.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <typeparam name="TMonoid">
/// The fold. Constrained to <c>struct, IMonoid&lt;T&gt;</c> so the JIT specializes the tree for it and inlines
/// the combine instead of emitting an interface call per level.
/// </typeparam>
/// <remarks>
/// <para>
/// This is the half of the range-query space <see cref="FenwickTree{T}"/> cannot reach. A Fenwick range query
/// is the <i>difference</i> of two prefix folds, so the operation must have an inverse — which is why that type
/// is constrained to <c>INumber&lt;T&gt;</c> and answers sums only. A segment tree stores each node's fold
/// outright and never subtracts, so range <b>minimum</b>, <b>maximum</b>, <b>gcd</b>, bitwise
/// <b>and</b>/<b>or</b>, and any user-written associative fold are all in reach. Where sums are what you want,
/// prefer <see cref="FenwickTree{T}"/> — it does the same job in half the memory.
/// </para>
/// <para>
/// The BCL has no range-aggregate structure at all, so the baseline is a plain <c>T[]</c> and a scan:
/// <c>values.AsSpan(start, length).Min()</c> is <c>O(n)</c> per query, and precomputing the answers is
/// <c>O(n)</c> per update. The tree gives <b>both</b> in <c>O(log n)</c>, so it wins precisely when updates and
/// range queries interleave — sliding-window minima and maxima over a mutating history, per-window
/// capability masks, "cheapest offer in this price band" over a live order book, and the same rank / windowed
/// aggregate shapes <see cref="FenwickTree{T}"/> serves for sums.
/// </para>
/// <para>
/// Commutativity is not required: the query folds the nodes it takes from the left and from the right into
/// two separate accumulators and combines them in index order at the end, so a non-commutative monoid gets the
/// same answer a left-to-right scan would.
/// </para>
/// <para>
/// <b>Range updates are deliberately not supported.</b> Applying an operation to every element of a range in
/// <c>O(log n)</c> needs lazy propagation, which needs a second monoid describing how updates compose plus a
/// distributive law relating the two — a different type with a different contract, not an overload of this one.
/// Update point by point, or apply this tree to a difference sequence.
/// </para>
/// <para>
/// It implements <see cref="IReadOnlyList{T}"/>, not merely <see cref="IReadOnlyCollection{T}"/> as
/// <see cref="FenwickTree{T}"/> does, because the leaves are stored outright: the indexer is a direct array
/// read, so an <c>IReadOnlyList</c> consumer that indexes in a loop pays what it expects. A Fenwick tree
/// recovers each value from a difference of prefix folds, which would make the same loop <c>O(n log n)</c>.
/// </para>
/// <para>
/// The length is fixed at construction (like <see cref="BitSet"/> and <see cref="FenwickTree{T}"/>); the tree
/// does not grow. Reads never mutate, so they never invalidate an enumerator. Every mutation bumps the version:
/// unlike <see cref="FenwickTree{T}"/>, an assignment that stores the value already there is not detected as a
/// no-op, because <see cref="IMonoid{T}"/> carries no equality obligation and the tree will not impose one.
/// This type is not thread-safe; concurrent callers must synchronize externally.
/// </para>
/// </remarks>
public sealed class SegmentTree<T, TMonoid> : IReadOnlyList<T>
    where TMonoid : struct, IMonoid<T>
{
    // Flat iterative layout over exactly 2n cells: the logical element at index i lives at _tree[_length + i],
    // and every internal node k in [1, _length) holds Combine(_tree[2k], _tree[2k + 1]). _tree[0] is unused.
    //
    // The alternative layout pads the leaf count up to a power of two, which costs up to 4n cells. It is
    // usually preferred because the 2n layout leaves the elements in a rotated order when _length is not a
    // power of two, so an internal node can span a wrapped, non-contiguous range — _tree[1] is the aggregate
    // of the whole sequence only when _length is a power of two, which is why Aggregate is a query rather than
    // a root read. That rotation does not reach the answer: the query below never combines a wrapped node into
    // the wrong side, because it walks outward from the two ends and keeps the two directions in separate
    // accumulators. SegmentTreeDifferentialTests pins that against a deliberately non-commutative monoid,
    // which is the only kind that can observe the difference.
    private readonly T[] _tree;
    private readonly int _length;

    // The fold. Not readonly: a readonly field of a struct type is defensively copied on every member call,
    // which would put a copy on the hottest path in the type. Matches BTreeSet's comparer field.
    private TMonoid _monoid;

    // Bumped on every mutation (indexer set / Combine / Clear) so active enumerators throw on concurrent
    // modification. A pure query is not a mutation and does not bump it.
    private int _version;

    /// <summary>
    /// The largest logical length a tree can hold. The layout stores two cells per logical element, so the
    /// ceiling is half of <see cref="Array.MaxLength"/>.
    /// </summary>
    private static readonly int MaxLength = Array.MaxLength / 2;

    /// <summary>
    /// Initializes a new segment tree of <paramref name="length"/> logical elements, each equal to the monoid's
    /// identity.
    /// </summary>
    /// <param name="length">
    /// The number of logical elements. Must be non-negative and at most half of <see cref="Array.MaxLength"/>
    /// (the layout stores two cells per element).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="length"/> is negative, or exceeds the maximum supported length.
    /// </exception>
    public SegmentTree(int length)
        : this(length, default)
    {
    }

    /// <summary>
    /// Initializes a new segment tree of <paramref name="length"/> logical elements, each equal to the identity
    /// of the supplied <paramref name="monoid"/>.
    /// </summary>
    /// <param name="length">The number of logical elements.</param>
    /// <param name="monoid">The fold to use. Pass this overload only when the monoid carries state.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="length"/> is negative, or exceeds the maximum supported length.
    /// </exception>
    public SegmentTree(int length, TMonoid monoid)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), length, "Length must be non-negative.");
        if (length > MaxLength)
            throw new ArgumentOutOfRangeException(nameof(length), length,
                $"Length must be at most {MaxLength} (half of Array.MaxLength — the layout stores two cells per element).");

        _monoid = monoid;
        _length = length;
        _tree = new T[2 * length];
        Array.Fill(_tree, _monoid.Identity);
    }

    /// <summary>
    /// Initializes a new segment tree seeded with <paramref name="values"/>, built in <c>O(n)</c>. The logical
    /// element at index <c>i</c> starts equal to the <c>i</c>-th element of <paramref name="values"/>.
    /// </summary>
    /// <param name="values">The initial logical values, in enumeration order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="values"/> holds more than half of <see cref="Array.MaxLength"/> elements.
    /// </exception>
    public SegmentTree(IEnumerable<T> values)
        : this(values, default)
    {
    }

    /// <summary>
    /// Initializes a new segment tree seeded with <paramref name="values"/> and folded by the supplied
    /// <paramref name="monoid"/>, built in <c>O(n)</c>.
    /// </summary>
    /// <param name="values">The initial logical values, in enumeration order.</param>
    /// <param name="monoid">The fold to use. Pass this overload only when the monoid carries state.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="values"/> holds more than half of <see cref="Array.MaxLength"/> elements.
    /// </exception>
    public SegmentTree(IEnumerable<T> values, TMonoid monoid)
    {
        ArgumentNullException.ThrowIfNull(values);

        _monoid = monoid;

        // A counted source (T[], List<T>, ...) is length-checked *before* anything is allocated and then copied
        // straight into the leaf half of the backing array, so an oversized source reports the documented
        // ArgumentException instead of failing the allocation first, and no intermediate array is built.
        if (values is ICollection<T> collection)
        {
            int count = collection.Count;
            ThrowIfSourceTooLong(count, nameof(values));

            _length = count;
            _tree = new T[2 * count];
            collection.CopyTo(_tree, count);
        }
        else
        {
            // Unknown length: materialize once, then apply the same ceiling.
            T[] seed = values.ToArray();
            ThrowIfSourceTooLong(seed.Length, nameof(values));

            _length = seed.Length;
            _tree = new T[2 * _length];
            Array.Copy(seed, 0, _tree, _length, _length);
        }

        // Linear-time build: every leaf now holds its own logical value, and one descending pass folds each
        // pair into its parent — O(n), not O(n log n) point-inserts.
        for (int k = _length - 1; k > 0; k--)
            _tree[k] = _monoid.Combine(_tree[2 * k], _tree[2 * k + 1]);
    }

    /// <summary>Gets the number of logical elements in the tree (its fixed length).</summary>
    public int Count => _length;

    /// <summary>
    /// Gets the aggregate of every logical element — equivalent to <c>Query(0, Count)</c>, and the monoid's
    /// identity for an empty tree. This is <c>O(log n)</c>, not a constant-time root read: the <c>2n</c> layout
    /// only makes the root the whole-sequence fold when <see cref="Count"/> is a power of two.
    /// </summary>
    public T Aggregate => QueryCore(0, _length);

    /// <summary>
    /// Gets or sets the logical value at <paramref name="index"/>. The getter is <c>O(1)</c> (a direct leaf
    /// read); the setter is <c>O(log n)</c> (it refolds the path to the root). Assigning the value already
    /// stored still bumps the version and so invalidates active enumerators — see the type remarks.
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

            return _tree[_length + index];
        }
        set
        {
            if ((uint)index >= (uint)_length)
                ThrowIndexOutOfRange(index);

            SetLeaf(index, value);
        }
    }

    /// <summary>
    /// Folds <paramref name="value"/> into the logical element at <paramref name="index"/> — the element
    /// becomes <c>Combine(current, value)</c> — in <c>O(log n)</c>. This is the monoid-native update: unlike
    /// <see cref="FenwickTree{T}.Add(int, T)"/> it needs no inverse, and the current value stays on the left so
    /// a non-commutative fold behaves as written.
    /// </summary>
    /// <param name="index">The zero-based logical index. Must be in <c>[0, Count)</c>.</param>
    /// <param name="value">The value to fold into the element.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public void Combine(int index, T value)
    {
        if ((uint)index >= (uint)_length)
            ThrowIndexOutOfRange(index);

        SetLeaf(index, _monoid.Combine(_tree[_length + index], value));
    }

    /// <summary>
    /// Returns the aggregate of the logical elements in <c>[start, endExclusive)</c>, in <c>O(log n)</c>. An
    /// empty range (<c>start == endExclusive</c>) yields the monoid's identity.
    /// </summary>
    /// <param name="start">The inclusive lower bound. Must be in <c>[0, endExclusive]</c>.</param>
    /// <param name="endExclusive">The exclusive upper bound. Must be in <c>[start, Count]</c>.</param>
    /// <returns>The aggregate of the logical elements in the half-open range.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The range is invalid or out of bounds.</exception>
    public T Query(int start, int endExclusive)
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

        return QueryCore(start, endExclusive);
    }

    /// <summary>
    /// Resets every logical element to the monoid's identity. Runs in <c>O(n)</c>, and bumps the version
    /// unconditionally — the tree is fixed-length, so establishing "already all identity" would cost the same
    /// scan as the reset it would skip.
    /// </summary>
    public void Clear()
    {
        Array.Fill(_tree, _monoid.Identity);
        _version++;
    }

    /// <summary>
    /// Returns an enumerator over the logical values in index order. Enumeration is <c>O(n)</c> — the leaves are
    /// stored outright, so no value has to be recovered from the folds above it.
    /// </summary>
    /// <returns>A struct enumerator over the logical values.</returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- internals ---------------------------------------------------------------------------------

    // Point assignment without bounds validation (callers validate). Writes the leaf, then refolds each
    // ancestor from its two children — O(log n) combines, no inverse needed.
    private void SetLeaf(int index, T value)
    {
        int node = _length + index;
        _tree[node] = value;

        // node starts at most at 2 * _length - 1, so the first parent is at most _length - 1 and the child
        // indices below stay inside the array. A tree of length 1 has no internal node and skips the loop.
        for (node >>= 1; node >= 1; node >>= 1)
            _tree[node] = _monoid.Combine(_tree[2 * node], _tree[2 * node + 1]);

        _version++;
    }

    // The range fold, without validation — shared by Query, Aggregate and nothing else. Walks outward from
    // both ends, taking each node that is fully inside the range and halving the bounds one level per step.
    //
    // The two accumulators are what makes this correct for a non-commutative monoid: nodes reached from the
    // left bound arrive in increasing index order and nodes reached from the right bound in decreasing order,
    // so folding each side into its own accumulator and combining left-then-right at the end reproduces the
    // order a linear scan would use. Collapsing them into one accumulator would interleave the two directions.
    private T QueryCore(int start, int endExclusive)
    {
        T resultLeft = _monoid.Identity;
        T resultRight = _monoid.Identity;

        for (int l = start + _length, r = endExclusive + _length; l < r; l >>= 1, r >>= 1)
        {
            if ((l & 1) != 0)
                resultLeft = _monoid.Combine(resultLeft, _tree[l++]);
            if ((r & 1) != 0)
                resultRight = _monoid.Combine(_tree[--r], resultRight);
        }

        return _monoid.Combine(resultLeft, resultRight);
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

    /// <summary>A struct enumerator over a <see cref="SegmentTree{T, TMonoid}"/>'s logical values in index order.</summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly SegmentTree<T, TMonoid> _tree;
        private readonly int _version;
        private int _index;
        private T _current;

        internal Enumerator(SegmentTree<T, TMonoid> tree)
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
                throw new InvalidOperationException("The segment tree was modified during enumeration.");

            if (_index < _tree._length)
            {
                _current = _tree._tree[_tree._length + _index];
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
                throw new InvalidOperationException("The segment tree was modified during enumeration.");

            _index = 0;
            _current = default!;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }
}
