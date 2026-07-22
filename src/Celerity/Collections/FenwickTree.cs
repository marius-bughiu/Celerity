using System.Collections;
using System.Numerics;

namespace Celerity.Collections;

/// <summary>
/// A <b>Fenwick tree</b> (Binary Indexed Tree): a fixed-length, array-backed sequence of numeric values that
/// answers <b>prefix sums</b> (and therefore arbitrary range sums) and applies <b>point updates</b> in
/// <c>O(log n)</c> each, over a single <c>n</c>-element array with no per-node object overhead.
/// </summary>
/// <typeparam name="T">
/// The numeric element type. Constrained to <see cref="INumber{TSelf}"/>, so it works for <see cref="int"/>,
/// <see cref="long"/>, <see cref="uint"/>, <see cref="ulong"/>, <see cref="double"/>, <see cref="decimal"/>,
/// and any other value type that implements generic-math addition and subtraction.
/// </typeparam>
/// <remarks>
/// <para>
/// The BCL ships nothing for the <b>interleaved point-update + prefix-sum-query</b> workload, and a plain
/// <c>T[]</c> forces a losing tradeoff: keep the raw values and every <see cref="PrefixSum(int)"/> /
/// <see cref="RangeSum(int, int)"/> query is <c>O(n)</c> (sum a slice); precompute a running-total array and
/// queries are <c>O(1)</c> but every point <see cref="Add(int, T)"/> is <c>O(n)</c> (fix the whole suffix).
/// A Fenwick tree gives <b>both</b> in <c>O(log n)</c>. Each stored cell holds the partial sum of a range of
/// the logical sequence whose length is the lowest set bit of its (1-based) index, so a prefix query
/// accumulates <c>O(log n)</c> cells by repeatedly stripping the lowest set bit, and an update touches the
/// <c>O(log n)</c> cells whose ranges cover the changed index by repeatedly adding it back.
/// </para>
/// <para>
/// The documented BCL-beating workload is any stream that <b>mixes updates with range-sum queries</b>:
/// running / rolling aggregates, order-statistics and rank counters (counting inversions, "how many seen
/// values are ≤ x"), cumulative-frequency tables, sliding-window sums over a mutating history, and gradient
/// or weight accumulators. Against a plain array these are <c>O(n·q)</c>; against the Fenwick tree they are
/// <c>O(q·log n)</c>.
/// </para>
/// <para>
/// The length is fixed at construction (like <see cref="BitSet"/>); the tree does not grow. Reads never
/// mutate, so they never invalidate an enumerator; <see cref="Add(int, T)"/>, the indexer setter, and
/// <see cref="Clear"/> do. This type is not thread-safe; concurrent callers must synchronize externally.
/// </para>
/// </remarks>
public sealed class FenwickTree<T> : IReadOnlyCollection<T>
    where T : struct, INumber<T>
{
    // 1-based Fenwick storage: _tree[0] is unused, _tree[k] holds the sum of the logical elements in the
    // half-open range (k - (k & -k), k] (1-based). _length is the logical element count == _tree.Length - 1.
    private readonly T[] _tree;
    private readonly int _length;

    // Bumped on every mutation (Add / indexer set / Clear) so active enumerators throw on concurrent
    // modification. A pure query (PrefixSum / RangeSum / indexer get) is not a mutation and does not bump it.
    private int _version;

    /// <summary>
    /// Initializes a new Fenwick tree of <paramref name="length"/> logical elements, all zero.
    /// </summary>
    /// <param name="length">The number of logical elements. Must be non-negative.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    public FenwickTree(int length)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length), length, "Length must be non-negative.");

        _length = length;
        _tree = new T[length + 1];
    }

    /// <summary>
    /// Initializes a new Fenwick tree seeded with <paramref name="values"/>, built in <c>O(n)</c>. The logical
    /// element at index <c>i</c> starts equal to the <c>i</c>-th element of <paramref name="values"/>.
    /// </summary>
    /// <param name="values">The initial logical values, in enumeration order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <c>null</c>.</exception>
    public FenwickTree(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        T[] seed = values as T[] ?? values.ToArray();
        _length = seed.Length;
        _tree = new T[_length + 1];

        // Linear-time build: seed each cell with its own logical value, then push it into its parent. After
        // one ascending pass every cell holds its correct range sum — O(n), not O(n log n) point-inserts.
        Array.Copy(seed, 0, _tree, 1, _length);
        for (int k = 1; k <= _length; k++)
        {
            int parent = k + (k & -k);
            if (parent <= _length)
                _tree[parent] += _tree[k];
        }
    }

    /// <summary>Gets the number of logical elements in the tree (its fixed length).</summary>
    public int Count => _length;

    /// <summary>
    /// Gets the sum of every logical element — equivalent to <see cref="PrefixSum(int)"/> with the full length.
    /// </summary>
    public T Total => PrefixSum(_length);

    /// <summary>
    /// Gets or sets the logical value at <paramref name="index"/>. Both accessors are <c>O(log n)</c>: the
    /// getter is <c>RangeSum(index, index + 1)</c>; the setter applies the delta needed to reach the new value.
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

            return RangeSumCore(index, index + 1);
        }
        set
        {
            if ((uint)index >= (uint)_length)
                ThrowIndexOutOfRange(index);

            T current = RangeSumCore(index, index + 1);
            AddCore(index, value - current);
        }
    }

    /// <summary>
    /// Adds <paramref name="delta"/> to the logical value at <paramref name="index"/>, in <c>O(log n)</c>.
    /// A negative <paramref name="delta"/> subtracts (for signed <typeparamref name="T"/>).
    /// </summary>
    /// <param name="index">The zero-based logical index. Must be in <c>[0, Count)</c>.</param>
    /// <param name="delta">The amount to add to the current value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public void Add(int index, T delta)
    {
        if ((uint)index >= (uint)_length)
            ThrowIndexOutOfRange(index);

        AddCore(index, delta);
    }

    /// <summary>
    /// Returns the sum of the logical elements in <c>[0, endExclusive)</c>, in <c>O(log n)</c>. Passing
    /// <c>0</c> yields zero; passing <see cref="Count"/> yields <see cref="Total"/>.
    /// </summary>
    /// <param name="endExclusive">The exclusive upper bound of the prefix. Must be in <c>[0, Count]</c>.</param>
    /// <returns>The sum of the first <paramref name="endExclusive"/> logical elements.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="endExclusive"/> is out of range.</exception>
    public T PrefixSum(int endExclusive)
    {
        if ((uint)endExclusive > (uint)_length)
            throw new ArgumentOutOfRangeException(nameof(endExclusive), endExclusive,
                "endExclusive must be in the range [0, Count].");

        T sum = T.Zero;
        for (int k = endExclusive; k > 0; k -= k & -k)
            sum += _tree[k];

        return sum;
    }

    /// <summary>
    /// Returns the sum of the logical elements in <c>[start, endExclusive)</c>, in <c>O(log n)</c>. An empty
    /// range (<c>start == endExclusive</c>) sums to zero.
    /// </summary>
    /// <param name="start">The inclusive lower bound. Must be in <c>[0, endExclusive]</c>.</param>
    /// <param name="endExclusive">The exclusive upper bound. Must be in <c>[start, Count]</c>.</param>
    /// <returns>The sum of the logical elements in the half-open range.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The range is invalid or out of bounds.</exception>
    public T RangeSum(int start, int endExclusive)
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

        return RangeSumCore(start, endExclusive);
    }

    /// <summary>Resets every logical element to zero. Runs in <c>O(n)</c>.</summary>
    public void Clear()
    {
        Array.Clear(_tree, 0, _tree.Length);
        _version++;
    }

    /// <summary>
    /// Returns an enumerator over the logical values in index order. Enumeration is <c>O(n log n)</c> (each
    /// value is recovered by an <c>O(log n)</c> difference of adjacent prefix sums).
    /// </summary>
    /// <returns>A struct enumerator over the logical values.</returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- internals ---------------------------------------------------------------------------------

    // Point update without bounds validation (callers validate). Walks the O(log n) cells whose ranges cover
    // `index` by repeatedly adding back the lowest set bit of the 1-based position.
    private void AddCore(int index, T delta)
    {
        for (int k = index + 1; k <= _length; k += k & -k)
            _tree[k] += delta;

        _version++;
    }

    // Range sum without validation: PrefixSum(endExclusive) - PrefixSum(start), collapsed to two walks.
    private T RangeSumCore(int start, int endExclusive)
    {
        T sum = T.Zero;
        for (int k = endExclusive; k > 0; k -= k & -k)
            sum += _tree[k];
        for (int k = start; k > 0; k -= k & -k)
            sum -= _tree[k];

        return sum;
    }

    private static void ThrowIndexOutOfRange(int index) =>
        throw new ArgumentOutOfRangeException(nameof(index), index,
            "Index must be in the range [0, Count).");

    /// <summary>A struct enumerator over a <see cref="FenwickTree{T}"/>'s logical values in index order.</summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly FenwickTree<T> _tree;
        private readonly int _version;
        private int _index;
        private T _prefix;   // running PrefixSum(_index): lets each value be one O(log n) query, not two.
        private T _current;

        internal Enumerator(FenwickTree<T> tree)
        {
            _tree = tree;
            _version = tree._version;
            _index = 0;
            _prefix = T.Zero;
            _current = default;
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
                throw new InvalidOperationException("The Fenwick tree was modified during enumeration.");

            if (_index < _tree._length)
            {
                T nextPrefix = _tree.PrefixSum(_index + 1);
                _current = nextPrefix - _prefix;
                _prefix = nextPrefix;
                _index++;
                return true;
            }

            _current = default;
            return false;
        }

        /// <summary>Resets the enumerator to before the first value.</summary>
        /// <exception cref="InvalidOperationException">The tree was modified during enumeration.</exception>
        public void Reset()
        {
            if (_version != _tree._version)
                throw new InvalidOperationException("The Fenwick tree was modified during enumeration.");

            _index = 0;
            _prefix = T.Zero;
            _current = default;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }
}
