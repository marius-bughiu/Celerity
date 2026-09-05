using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Celerity.Collections;

/// <summary>
/// A <b>sparse table</b>: an immutable, array-backed sequence that answers the aggregate of any half-open range
/// under an <b>idempotent</b> associative operation in <c>O(1)</c> — two array reads and one combine, with no
/// loop and no descent.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <typeparam name="TMonoid">
/// The fold. Constrained to <c>struct, IIdempotentMonoid&lt;T&gt;</c> so the JIT specializes the table for it
/// and inlines the combine, and so a non-idempotent fold is a compile error rather than a wrong answer.
/// </typeparam>
/// <remarks>
/// <para>
/// This is the <b>build-once</b> half of the range-aggregate space.
/// <see cref="SegmentTree{T, TMonoid}"/> answers the same question in <c>O(log n)</c> and accepts point
/// updates; when the sequence never changes after you build it, that descent is paid on every query for a
/// mutability nobody uses. A sparse table precomputes the fold of every window whose length is a power of two,
/// and answers an arbitrary range from the two such windows that cover it.
/// </para>
/// <para>
/// The catch is in that word <i>cover</i>: the two windows <b>always overlap</b>, so some element is folded in
/// twice on every non-empty query. Writing <c>p</c> for the window width, their intersection is
/// <c>2p - length</c> elements, and since <c>p &lt;= length &lt; 2p</c> that is never zero — at an exact power
/// of two the two windows are the <i>same</i> window and the whole range is folded twice, which is the
/// maximal case rather than an exception to it. A one-element query is the extreme: it combines a value with
/// itself. That is harmless exactly when the operation is idempotent — <c>Combine(a, a) == a</c> — which is why
/// this type takes <see cref="IIdempotentMonoid{T}"/> rather than <see cref="IMonoid{T}"/>. Minimum, maximum,
/// gcd and bitwise and/or qualify; <b>sum does not</b>, and
/// <c>SparseTable&lt;int, SumMonoid&lt;int&gt;&gt;</c> therefore does not compile.
/// For range sums over an immutable sequence, a precomputed prefix array answers in <c>O(1)</c>
/// with two lines of code; for sums over a mutable one, use <see cref="FenwickTree{T}"/>.
/// </para>
/// <para>
/// The BCL has no range-aggregate structure of any kind, so the baseline outside Celerity is a plain
/// <c>T[]</c> and a hand-written loop that folds the slice element by element — <c>O(n)</c> per query, and
/// there is not even a span helper to lean on, since <c>Span&lt;T&gt;</c> has no <c>Min</c> or <c>Max</c>, let
/// alone an arbitrary combine.
/// </para>
/// <para>
/// <b>What it costs.</b> The table holds <c>levels * n</c> cells, where <c>levels</c> is
/// <c>floor(log2(n)) + 1</c>, and the build performs one combine per meaningful cell above the first row:
/// <c>O(n log n)</c> time and memory, against a segment tree's <c>O(n)</c> build into <c>2n</c> cells. At a
/// million elements that is twenty rows rather than two, so this type earns its place when a sequence is
/// built once and queried <i>many</i> times.
/// A handful of queries does not repay the build, and a range short enough to scan does not repay the table at
/// all. <see cref="IndexSizeInBytes"/> reports the figure so the trade can be measured rather than guessed.
/// </para>
/// <para>
/// Commutativity is not required, and the two windows are combined in index order — left window first — so a
/// non-commutative idempotent fold — "keep the leftmost value that qualifies" is the shape, and the one the
/// differential suite runs on — gets the answer a left-to-right scan would give. Note that overlap makes the
/// ordering weaker than a segment tree's: elements in the overlap are visited on both sides, so a fold that
/// distinguishes <i>how many times</i> it saw a value is already excluded by the idempotence requirement.
/// </para>
/// <para>
/// It implements <see cref="IReadOnlyList{T}"/> because the original values are stored outright as the first
/// row of the table: the indexer is a direct array read, and enumeration streams a contiguous slice. Nothing
/// mutates after construction, so there is no version counter and an enumerator can never be invalidated, and
/// concurrent readers need no synchronization <i>as far as the table itself is concerned</i> — with the same
/// caveat <see cref="IntervalTree{TKey, TValue}"/> carries: every query calls
/// <typeparamref name="TMonoid"/>, so a monoid that is not itself thread-safe makes concurrent queries unsafe
/// however immutable the table is. The five shipped folds are stateless, so the ordinary case is safe; a
/// stateful monoid, which the two-argument constructor exists to accept, is the caller's to reason about.
/// </para>
/// </remarks>
public sealed class SparseTable<T, TMonoid> : IReadOnlyList<T>
    where TMonoid : struct, IIdempotentMonoid<T>
{
    // One flat array of _levelCount rows, each _length wide: row k starting at k * _length holds, at offset i,
    // the fold of the window [i, i + 2^k). Only the first _length - 2^k + 1 entries of row k are meaningful;
    // the tail of each row is left at default(T) and never read, because a query only ever indexes a row with
    // an offset its window fits inside.
    //
    // Flat rather than jagged: the meaningful entries number sum(_length - 2^k + 1) over the rows, so the
    // padding is exactly 2^_levelCount - 1 - _levelCount cells. Since 2^_levelCount lies in
    // (_length, 2 * _length], that is between roughly one and two rows' worth — at _length = 8 it is 11 of the
    // 32 cells. It buys a single allocation, one bounds check per read, and rows contiguous with each other.
    private readonly T[] _table;
    private readonly int _length;
    private readonly int _levelCount;

    // The fold. Not readonly: a readonly field of a struct type is defensively copied on every member call,
    // which would put a copy on the hottest path in the type. Matches SegmentTree's monoid field.
    private TMonoid _monoid;

    /// <summary>
    /// Initializes a new sparse table over <paramref name="values"/>, built in <c>O(n log n)</c>.
    /// </summary>
    /// <param name="values">
    /// The sequence to index, in enumeration order. Copied, so later changes to the source are not observed.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="values"/> holds enough elements that the table would exceed
    /// <see cref="Array.MaxLength"/> cells.
    /// </exception>
    public SparseTable(IEnumerable<T> values)
        : this(values, default)
    {
    }

    /// <summary>
    /// Initializes a new sparse table over <paramref name="values"/>, folded by the supplied
    /// <paramref name="monoid"/> and built in <c>O(n log n)</c>.
    /// </summary>
    /// <param name="values">
    /// The sequence to index, in enumeration order. Copied, so later changes to the source are not observed.
    /// </param>
    /// <param name="monoid">The fold to use. Pass this overload only when the monoid carries state.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="values"/> holds enough elements that the table would exceed
    /// <see cref="Array.MaxLength"/> cells.
    /// </exception>
    /// <remarks>
    /// The constructors take <see cref="IEnumerable{T}"/> rather than <see cref="ReadOnlySpan{T}"/> — unlike
    /// <see cref="WaveletTree"/>, and like <see cref="SegmentTree{T, TMonoid}"/>, the type a caller arrives
    /// here from. Offering both would make <c>new SparseTable&lt;…&gt;(anArray)</c> ambiguous on
    /// <c>net8.0</c>, and the table copies its input either way.
    /// </remarks>
    public SparseTable(IEnumerable<T> values, TMonoid monoid)
    {
        ArgumentNullException.ThrowIfNull(values);

        _monoid = monoid;

        // A counted source (T[], List<T>, ...) is measured and length-checked *before* anything is allocated
        // and then copied straight into the first row, so an oversized source reports the documented
        // ArgumentException instead of failing the allocation first, and no intermediate array is built.
        if (values is ICollection<T> collection)
        {
            _length = collection.Count;
            _levelCount = LevelsFor(_length, nameof(values));
            _table = new T[_levelCount * _length];

            collection.CopyTo(_table, 0);
        }
        else
        {
            // Unknown length: materialize once, then apply the same ceiling.
            T[] seed = values.ToArray();

            _length = seed.Length;
            _levelCount = LevelsFor(_length, nameof(values));
            _table = new T[_levelCount * _length];

            Array.Copy(seed, _table, _length);
        }

        Build();
    }

    /// <summary>Gets the number of elements in the indexed sequence.</summary>
    public int Count => _length;

    /// <summary>
    /// Gets the number of precomputed window rows — <c>floor(log2(Count)) + 1</c>, and zero for an empty
    /// table. Row <c>k</c> holds the fold of every window of length <c>2^k</c>, and the table stores
    /// <c>LevelCount * Count</c> cells in all.
    /// </summary>
    public int LevelCount => _levelCount;

    /// <summary>
    /// Gets the aggregate of every element — equivalent to <c>Query(0, Count)</c>, and the monoid's identity
    /// for an empty table. Unlike <see cref="SegmentTree{T, TMonoid}.Aggregate"/> this is <c>O(1)</c>.
    /// </summary>
    public T Aggregate => QueryCore(0, _length);

    /// <summary>
    /// Gets the size of the table's backing array in bytes: <c>LevelCount * Count</c> cells, each the size of
    /// one <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the quantity being traded for the <c>O(1)</c> query, so it is worth measuring rather than
    /// estimating: a <see cref="SegmentTree{T, TMonoid}"/> over the same sequence holds <c>2n</c> cells, and
    /// this holds <c>n * (floor(log2(n)) + 1)</c> — <c>10n</c> against <c>2n</c> at a thousand elements, so
    /// five times as many, and <c>20n</c> against <c>2n</c> at a million, so ten times.
    /// </para>
    /// <para>
    /// The figure is a <see cref="long"/>, as <see cref="WaveletTree.IndexSizeInBytes"/> is, because the
    /// product can exceed <see cref="int.MaxValue"/> for a wide <typeparamref name="T"/> well before the cell
    /// count itself does. For a reference-type <typeparamref name="T"/> it counts the references the table
    /// holds, not the objects they point at — the table stores each element once per row, but they are the
    /// same object.
    /// </para>
    /// </remarks>
    public long IndexSizeInBytes => (long)_table.Length * Unsafe.SizeOf<T>();

    /// <summary>
    /// Gets the value at <paramref name="index"/>, in <c>O(1)</c> — the original values are the table's first
    /// row, so this is a direct array read.
    /// </summary>
    /// <param name="index">The zero-based index. Must be in <c>[0, Count)</c>.</param>
    /// <returns>The value stored there.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_length)
                throw new ArgumentOutOfRangeException(nameof(index), index,
                    "Index must be in the range [0, Count).");

            return _table[index];
        }
    }

    /// <summary>
    /// Returns the aggregate of the elements in <c>[start, endExclusive)</c>, in <c>O(1)</c>. An empty range
    /// (<c>start == endExclusive</c>) yields the monoid's identity.
    /// </summary>
    /// <param name="start">The inclusive lower bound. Must be in <c>[0, endExclusive]</c>.</param>
    /// <param name="endExclusive">The exclusive upper bound. Must be in <c>[start, Count]</c>.</param>
    /// <returns>The aggregate of the elements in the half-open range.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The range is invalid or out of bounds.</exception>
    /// <remarks>
    /// The same signature and the same half-open convention as
    /// <see cref="SegmentTree{T, TMonoid}.Query(int, int)"/>, so a caller who discovers their sequence is
    /// static can change the type and leave the call sites alone.
    /// </remarks>
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
    /// Returns an enumerator over the indexed values in index order. Enumeration is <c>O(n)</c> over a
    /// contiguous slice — the values are stored outright as the table's first row.
    /// </summary>
    /// <returns>A struct enumerator over the indexed values.</returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ---- internals ---------------------------------------------------------------------------------

    // Fills rows 1..._levelCount-1 from row 0, which the constructor has already seeded with the values.
    // Row k's entry at i spans [i, i + 2^k) and is the fold of the two half-width windows below it, so the
    // whole build is one combine per meaningful cell — O(n log n) total.
    private void Build()
    {
        for (int level = 1; level < _levelCount; level++)
        {
            int half = 1 << (level - 1);
            int row = level * _length;
            int previous = row - _length;

            // Row `level` is meaningful only where a window of 2^level still fits, i.e. i + 2^level <= _length.
            int last = _length - (half << 1);
            for (int i = 0; i <= last; i++)
                _table[row + i] = _monoid.Combine(_table[previous + i], _table[previous + i + half]);
        }
    }

    // The range fold, without validation — shared by Query and Aggregate.
    //
    // Take the largest power of two p that fits inside the range, and cover the range with the p-wide window
    // at each end. Their union is exactly [start, endExclusive); their intersection is 2p - length elements,
    // which p <= length < 2p makes strictly positive, so *every* non-empty query folds something in twice and
    // relies on idempotence. When length is exactly p the two windows coincide and both reads below hit the
    // same cell — the degenerate case, deliberately not special-cased: a branch to skip the second combine
    // would cost more on the hot path than the combine it saves, and idempotence is required regardless.
    // The left window is combined first, so index order is preserved for a non-commutative fold.
    private T QueryCore(int start, int endExclusive)
    {
        int length = endExclusive - start;
        if (length == 0)
            return _monoid.Identity;

        // Log2 of a positive int is the row whose windows are the widest that still fit within `length`.
        int level = BitOperations.Log2((uint)length);
        int row = level * _length;

        return _monoid.Combine(_table[row + start], _table[row + endExclusive - (1 << level)]);
    }

    // The number of rows a table of `length` elements needs, with the ceiling check that keeps the flat
    // rectangle inside a single array. Zero rows for an empty table: it has no windows and every query on it
    // is the empty range.
    private static int LevelsFor(int length, string paramName)
    {
        if (length == 0)
            return 0;

        int levels = BitOperations.Log2((uint)length) + 1;
        if ((long)levels * length > Array.MaxLength)
            throw new ArgumentException(
                $"A sparse table over {length} elements needs {levels} rows of {length} cells, which exceeds " +
                $"the maximum array length of {Array.MaxLength}. Use SegmentTree<T, TMonoid> instead — it " +
                "stores 2n cells and answers the same query in O(log n).",
                paramName);

        return levels;
    }

    /// <summary>A struct enumerator over a <see cref="SparseTable{T, TMonoid}"/>'s values in index order.</summary>
    /// <remarks>
    /// The table is immutable, so — unlike <see cref="SegmentTree{T, TMonoid}.Enumerator"/> — this enumerator
    /// carries no version and can never throw for concurrent modification.
    /// </remarks>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly SparseTable<T, TMonoid> _table;
        private int _index;
        private T _current;

        internal Enumerator(SparseTable<T, TMonoid> table)
        {
            _table = table;
            _index = 0;
            _current = default!;
        }

        /// <summary>Gets the value at the current position of the enumerator.</summary>
        public readonly T Current => _current;

        readonly object? IEnumerator.Current => _current;

        /// <summary>Advances the enumerator to the next value.</summary>
        /// <returns><c>true</c> if there is a next value; otherwise <c>false</c>.</returns>
        public bool MoveNext()
        {
            if (_index < _table._length)
            {
                _current = _table._table[_index];
                _index++;
                return true;
            }

            _current = default!;
            return false;
        }

        /// <summary>Resets the enumerator to before the first value.</summary>
        public void Reset()
        {
            _index = 0;
            _current = default!;
        }

        /// <summary>Releases resources used by the enumerator. This is a no-op.</summary>
        public readonly void Dispose()
        {
        }
    }
}
