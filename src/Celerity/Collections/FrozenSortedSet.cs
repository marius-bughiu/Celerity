using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Celerity.Primitives;

namespace Celerity.Collections;

/// <summary>
/// An <b>immutable</b> sorted set of integer keys that answers membership, rank and successor queries
/// (<see cref="Contains"/>, <see cref="CountLessThan"/>, <see cref="TryGetLowerBound"/>) several times faster
/// than a binary search over the same sorted array, by laying the keys out as a static B+ tree of
/// cache-line-sized nodes that are searched with SIMD compares.
/// </summary>
/// <remarks>
/// <para>
/// <b>Build once, query many.</b> The keys are sorted, deduplicated and laid out at construction, and there is
/// no mutating member; a changed key set means building a new one, which costs a sort. Use it for a key set
/// that is read far more often than it changes: an allow or deny list of IDs, the boundaries of a range
/// partition, a sorted dimension table, a timestamp index.
/// </para>
/// <para>
/// The BCL's frozen collections are unordered. <c>FrozenSet&lt;T&gt;</c> answers membership but not "the
/// first key at or above <c>x</c>", "how many keys are below <c>x</c>" or "the keys in <c>[a, b)</c>".
/// <see cref="SortedSet{T}"/> and <see cref="BTreeSet{T, TComparer}"/> answer those, but they are built to be
/// mutated and pay for it on every read. The honest baseline is therefore a sorted <c>T[]</c> searched with
/// <see cref="MemoryExtensions.BinarySearch{T}(ReadOnlySpan{T}, IComparable{T})"/>. A binary search is bound by
/// latency, not by work: <c>log2(n)</c> loads, each depending on the one before it and each a likely cache miss
/// once the array outgrows L1, with a branch at every step that mispredicts about half the time.
/// </para>
/// <para>
/// <b>Layout.</b> This is the static B+ tree that Algorithmica calls the <i>S+ tree</i>. The keys are cut into
/// nodes of <c>B = 64 / sizeof(T)</c> keys, one 64-byte cache line each (16 for <see cref="int"/>, 8 for
/// <see cref="long"/>), and these leaves stay in order in one array, which is what <see cref="AsSpan"/> and the
/// indexer read. Above them sit the internal levels. Each internal node holds <c>B</c> separator keys for
/// <c>B + 1</c> children: separator <c>i</c> is the smallest key under child <c>i + 1</c>. A search reads one
/// node per level and counts the keys in it that are below the probe, using four <see cref="Vector128{T}"/>
/// compares and one horizontal add, with no branch. That count is the child to descend into, and at the leaf
/// it is the answer's position. The depth is <c>ceil(log_(B+1)(n / B))</c> plus one: 5 nodes for a million
/// <see cref="int"/> keys and 6 for ten million, where a binary search takes 20 and 24 dependent steps. Key types
/// that have no hardware <see cref="Vector128{T}"/> support (<see cref="char"/>, <see cref="Int128"/>,
/// <see cref="UInt128"/>) use the same layout with a scalar scan of each node.
/// </para>
/// <para>
/// <b>Space.</b> The leaves are the keys themselves, padded to a whole node with <c>T.MaxValue</c>. The internal
/// levels add about one key in <c>B + 1</c>: 6% for <see cref="int"/> and 11% for <see cref="long"/>. So the
/// set costs about <c>1 + 1 / B</c> times the sorted array it replaces, plus at most one node of padding.
/// <see cref="MemoryUsageInBytes"/> reports the exact figure.
/// </para>
/// <para>
/// The keys must be integers (<see cref="IBinaryInteger{TSelf}"/>), so the order is the type's own and there is
/// no comparer to supply. <c>T.MaxValue</c> pads the nodes but is still a valid key: the search counts keys
/// <i>strictly</i> below the probe, so padding is never counted. The type holds no mutable state after
/// construction, so instances are safe to share across threads.
/// </para>
/// </remarks>
/// <typeparam name="T">
/// The key type: an unmanaged binary integer such as <see cref="int"/>, <see cref="long"/>,
/// <see cref="uint"/>, <see cref="ulong"/>, <see cref="short"/> or <see cref="byte"/>.
/// </typeparam>
/// <example>
/// <code>
/// var blocked = new FrozenSortedSet&lt;long&gt;(blockedAccountIds);
/// if (blocked.Contains(accountId))
///     return Forbidden();
///
/// // Range partitions: which shard owns key k? The last boundary at or below it.
/// var boundaries = new FrozenSortedSet&lt;int&gt;(shardStartKeys);
/// int shard = boundaries.CountLessThan(k + 1) - 1;
///
/// // Every key in [from, to), as a span over the sorted keys, without copying.
/// ReadOnlySpan&lt;int&gt; window = boundaries.SliceRange(from, to);
/// </code>
/// </example>
public sealed class FrozenSortedSet<T> : IReadOnlyList<T>, IReadOnlySet<T>
    where T : unmanaged, IBinaryInteger<T>, IMinMaxValue<T>
{
    // The keys, ascending and distinct, padded with T.MaxValue to a whole number of nodes (at least one, so an
    // empty set still has a leaf to search). _internal holds the internal levels root first; level h starts at
    // _levelStarts[h], and node k of a level occupies the NodeSize keys from there + k * NodeSize. Node k's
    // children are nodes k * (NodeSize + 1) + 0 .. NodeSize of the level below, so every subtree covers a
    // contiguous run of leaves.
    private readonly T[] _keys;
    private readonly T[] _internal;
    private readonly int[] _levelStarts;
    private readonly int _count;

    /// <summary>
    /// Builds the set from <paramref name="source"/>, which may be in any order and may repeat keys.
    /// </summary>
    /// <param name="source">
    /// The keys. They are copied, sorted and deduplicated, so the caller's collection may be reused. A counted
    /// source (any <see cref="ICollection{T}"/>, including an array) is copied straight into the leaf array with
    /// one allocation.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="source"/> holds more keys than one padded array can hold, a little under
    /// <see cref="Array.MaxLength"/>. A counted source is checked before anything is allocated.
    /// </exception>
    /// <remarks>
    /// Building costs one sort, <c>O(n log n)</c>, then one pass to deduplicate and one over the internal
    /// levels, which are about <c>n / B</c> keys. Duplicates are dropped after the sort, so a source with many
    /// repeats may be copied once more into a smaller leaf array.
    /// </remarks>
    public FrozenSortedSet(IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        T[] buffer;
        int length;
        if (source is ICollection<T> collection)
        {
            length = collection.Count;
            ThrowIfTooLong(length);
            buffer = new T[PaddedLength(length)];
            collection.CopyTo(buffer, 0);
        }
        else
        {
            buffer = source.ToArray();
            length = buffer.Length;
            ThrowIfTooLong(length);
        }

        _count = SortDistinct(buffer.AsSpan(0, length));
        int padded = PaddedLength(_count);
        if (buffer.Length != padded)
            Array.Resize(ref buffer, padded);
        buffer.AsSpan(_count).Fill(T.MaxValue);
        _keys = buffer;

        (_internal, _levelStarts) = BuildInternalLevels(buffer, _count);
    }

    /// <summary>
    /// Gets the number of keys a node holds: <c>64 / sizeof(T)</c>, one cache line. Each internal node has one
    /// child more than this.
    /// </summary>
    public static int NodeSize
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Math.Max(1, 64 / Unsafe.SizeOf<T>());
    }

    /// <summary>Gets the number of distinct keys in the set.</summary>
    public int Count => _count;

    /// <summary>
    /// Gets the number of nodes a search reads, root to leaf: <c>1</c> while every key fits in one node, and one
    /// more for each internal level above the leaves.
    /// </summary>
    public int Depth => _levelStarts.Length + 1;

    /// <summary>
    /// Gets the size in bytes of the key storage: the padded leaves, the internal levels and the level index.
    /// Compare it against <c>Count * sizeof(T)</c>, the sorted array the set replaces.
    /// </summary>
    /// <remarks>
    /// This is payload, not retained-object size: it excludes the CLR's per-array header and the instance's own
    /// fields, the same accounting <see cref="EliasFanoSequence.MemoryUsageInBytes"/> uses.
    /// </remarks>
    public long MemoryUsageInBytes =>
        (((long)_keys.Length + _internal.Length) * Unsafe.SizeOf<T>()) + ((long)_levelStarts.Length * sizeof(int));

    /// <summary>Gets the smallest key.</summary>
    /// <exception cref="InvalidOperationException">The set is empty.</exception>
    public T Min => _count != 0 ? _keys[0] : throw new InvalidOperationException("The set is empty.");

    /// <summary>Gets the largest key.</summary>
    /// <exception cref="InvalidOperationException">The set is empty.</exception>
    public T Max => _count != 0 ? _keys[_count - 1] : throw new InvalidOperationException("The set is empty.");

    /// <summary>Gets the key at the specified position in ascending order.</summary>
    /// <param name="index">The zero-based position, over <c>[0, Count)</c>.</param>
    /// <returns>The <paramref name="index"/>-th smallest key.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside <c>[0, Count)</c>.</exception>
    /// <remarks>One array read: the leaves are the sorted keys, in order.</remarks>
    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be within [0, Count).");

            return _keys[index];
        }
    }

    /// <summary>Returns the keys in ascending order as a read-only span, without copying.</summary>
    /// <returns>A span of exactly <see cref="Count"/> keys, ascending and distinct.</returns>
    /// <remarks>
    /// The span is sorted and duplicate-free, so it can be passed straight to <c>SortedSpan</c>'s set algebra
    /// (<c>Intersect</c>, <c>Union</c>, <c>Except</c>) in <c>Celerity.Primitives</c>.
    /// </remarks>
    public ReadOnlySpan<T> AsSpan() => new(_keys, 0, _count);

    /// <summary>Determines whether the set holds <paramref name="item"/>.</summary>
    /// <param name="item">The key to locate.</param>
    /// <returns><c>true</c> if the key is present.</returns>
    /// <remarks>One node read per level, <see cref="Depth"/> in all, then one compare.</remarks>
    public bool Contains(T item)
    {
        int index = LowerBound(item);
        return index < _count && _keys[index] == item;
    }

    /// <summary>
    /// Returns the position of <paramref name="item"/> in ascending order, or <c>-1</c> when the set does not
    /// hold it.
    /// </summary>
    /// <param name="item">The key to locate.</param>
    /// <returns>The zero-based position of the key, or <c>-1</c>.</returns>
    /// <remarks>The cost of <see cref="Contains"/>.</remarks>
    public int IndexOf(T item)
    {
        int index = LowerBound(item);
        return index < _count && _keys[index] == item ? index : -1;
    }

    /// <summary>
    /// Returns how many keys are strictly less than <paramref name="value"/>, which is also the position of the
    /// first key at or above it (the <c>lower_bound</c> of the ordered containers).
    /// </summary>
    /// <param name="value">The bound. Any value of <typeparamref name="T"/> is accepted.</param>
    /// <returns>A count in <c>[0, Count]</c>; <see cref="Count"/> when every key is smaller.</returns>
    /// <remarks>One node read per level, <see cref="Depth"/> in all.</remarks>
    public int CountLessThan(T value) => LowerBound(value);

    /// <summary>
    /// Finds the smallest key that is <b>greater than or equal to</b> <paramref name="value"/>, named after
    /// <see cref="BTreeSet{T, TComparer}.TryGetLowerBound"/>. An exact match is its own lower bound.
    /// </summary>
    /// <param name="value">The value to bound.</param>
    /// <param name="bound">The bounding key, or <c>default</c> when every key is smaller.</param>
    /// <returns><c>true</c> if such a key exists.</returns>
    public bool TryGetLowerBound(T value, out T bound)
    {
        int index = LowerBound(value);
        if (index == _count)
        {
            bound = default;
            return false;
        }

        bound = _keys[index];
        return true;
    }

    /// <summary>
    /// Finds the smallest key that is <b>strictly greater than</b> <paramref name="value"/>, named after
    /// <see cref="BTreeSet{T, TComparer}.TryGetUpperBound"/>.
    /// </summary>
    /// <param name="value">The value to bound.</param>
    /// <param name="bound">The bounding key, or <c>default</c> when no key is larger.</param>
    /// <returns><c>true</c> if such a key exists.</returns>
    public bool TryGetUpperBound(T value, out T bound)
    {
        // Nothing is above T.MaxValue, and value + 1 would wrap to T.MinValue.
        if (value == T.MaxValue)
        {
            bound = default;
            return false;
        }

        return TryGetLowerBound(value + T.One, out bound);
    }

    /// <summary>
    /// Returns the keys in the half-open range <c>[fromInclusive, toExclusive)</c>, ascending, as a span over the
    /// set's own storage. Its <c>Length</c> is the number of keys in the range.
    /// </summary>
    /// <param name="fromInclusive">The inclusive lower bound of the range.</param>
    /// <param name="toExclusive">The exclusive upper bound of the range.</param>
    /// <returns>A read-only span of the matching keys; empty when none fall in the range.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="toExclusive"/> is less than <paramref name="fromInclusive"/>.
    /// </exception>
    /// <remarks>
    /// Two searches, one per bound, and no copy, so the cost does not depend on how many keys the range holds.
    /// A range that should include <c>T.MaxValue</c> cannot be written half-open; take
    /// <c>AsSpan()[CountLessThan(fromInclusive)..]</c> instead.
    /// </remarks>
    public ReadOnlySpan<T> SliceRange(T fromInclusive, T toExclusive)
    {
        if (toExclusive < fromInclusive)
            throw new ArgumentException("toExclusive must not be less than fromInclusive.", nameof(toExclusive));

        int start = LowerBound(fromInclusive);
        return new ReadOnlySpan<T>(_keys, start, LowerBound(toExclusive) - start);
    }

    /// <summary>Returns an enumerator over the keys in ascending order, without allocating.</summary>
    /// <returns>A struct enumerator over the keys.</returns>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ── IReadOnlySet<T> set-algebra members ──────────────────────────────────
    // The set is the left-hand operand and `other` the right-hand one. IsSupersetOf and Overlaps stream `other`
    // against Contains and may stop early. The other four need `other`'s distinct keys: another FrozenSortedSet
    // is used as it stands, and anything else is copied once, sorted and deduplicated. All four then count the
    // common keys with one merge (SortedSpan.IntersectCount), galloping when one side is much longer.

    /// <summary>
    /// Determines whether the set and <paramref name="other"/> contain the same distinct keys.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two contain exactly the same keys.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool SetEquals(IEnumerable<T> other)
    {
        ReadOnlySpan<T> o = SortedDistinct(other);
        return o.Length == _count && o.SequenceEqual(AsSpan());
    }

    /// <summary>
    /// Determines whether every key of the set is also in <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the set is a subset of <paramref name="other"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSubsetOf(IEnumerable<T> other)
    {
        ReadOnlySpan<T> o = SortedDistinct(other);
        return _count <= o.Length && SortedSpan.IntersectCount(AsSpan(), o) == _count;
    }

    /// <summary>
    /// Determines whether the set is a proper (strict) subset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every key of the set is in <paramref name="other"/> and <paramref name="other"/> has at least
    /// one key the set does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        ReadOnlySpan<T> o = SortedDistinct(other);
        return _count < o.Length && SortedSpan.IntersectCount(AsSpan(), o) == _count;
    }

    /// <summary>
    /// Determines whether every key of <paramref name="other"/> is also in the set.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the set is a superset of <paramref name="other"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsSupersetOf(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        foreach (T item in other)
        {
            if (!Contains(item))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether the set is a proper (strict) superset of <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns>
    /// <c>true</c> if every key of <paramref name="other"/> is in the set and the set has at least one key
    /// <paramref name="other"/> does not.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        ReadOnlySpan<T> o = SortedDistinct(other);
        return _count > o.Length && SortedSpan.IntersectCount(AsSpan(), o) == o.Length;
    }

    /// <summary>
    /// Determines whether the set and <paramref name="other"/> share at least one key.
    /// </summary>
    /// <param name="other">The collection to compare against.</param>
    /// <returns><c>true</c> if the two share any key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <c>null</c>.</exception>
    public bool Overlaps(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        foreach (T item in other)
        {
            if (Contains(item))
                return true;
        }

        return false;
    }

    // ── Search ──────────────────────────────────────────────────────────────

    // The position of the first key >= value. Each level counts the separators below `value`: separator i is the
    // smallest key under child i + 1, so that count names the child whose subtree holds the answer, or holds only
    // keys below it — in which case the answer is the first key of the next subtree, and because subtrees cover
    // consecutive leaves, the leaf count simply runs off the end of the node onto it. Padding is T.MaxValue,
    // which is never strictly below anything, so the descent never enters a child that does not exist.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int LowerBound(T value)
    {
        int fanOut = NodeSize + 1;
        int node = 0;
        ref T levels = ref MemoryMarshal.GetArrayDataReference(_internal);
        foreach (int start in _levelStarts)
            node = (node * fanOut) + CountBelow(ref Unsafe.Add(ref levels, start + (node * NodeSize)), value);

        int leaf = node * NodeSize;
        return leaf + CountBelow(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_keys), leaf), value);
    }

    // How many of the NodeSize keys at `node` are strictly less than `value`. A node is one cache line, four
    // vectors, so with hardware support this is four compares (each lane all ones where a key is below), three
    // adds and one horizontal sum. The sum is minus the count, in T's own wrapping arithmetic; a node holds at
    // most 64 keys, so the count fits even in a byte.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountBelow(ref T node, T value)
    {
        if (Vector128<T>.IsSupported)
        {
            nuint lanes = (nuint)Vector128<T>.Count;
            Vector128<T> probe = Vector128.Create(value);
            Vector128<T> a = Vector128.LessThan(Vector128.LoadUnsafe(ref node), probe);
            Vector128<T> b = Vector128.LessThan(Vector128.LoadUnsafe(ref node, lanes), probe);
            Vector128<T> c = Vector128.LessThan(Vector128.LoadUnsafe(ref node, 2 * lanes), probe);
            Vector128<T> d = Vector128.LessThan(Vector128.LoadUnsafe(ref node, 3 * lanes), probe);
            return int.CreateTruncating(T.Zero - Vector128.Sum((a + b) + (c + d)));
        }

        int count = 0;
        for (int i = 0; i < NodeSize; i++)
        {
            if (Unsafe.Add(ref node, i) < value)
                count++;
        }

        return count;
    }

    // ── Construction ────────────────────────────────────────────────────────

    // The largest key count whose padded leaf array still fits in one array.
    private static int MaxCount => Array.MaxLength / NodeSize * NodeSize;

    private static void ThrowIfTooLong(int length)
    {
        if (length > MaxCount)
        {
            throw new ArgumentException(
                $"A FrozenSortedSet<{typeof(T).Name}> holds at most {MaxCount} keys.", "source");
        }
    }

    // The leaf array length for `count` keys: whole nodes, and at least one so an empty set still has a leaf.
    private static int PaddedLength(int count) => Math.Max(1, (count + NodeSize - 1) / NodeSize) * NodeSize;

    // Sorts `keys` and moves the distinct ones to the front; returns how many there are.
    private static int SortDistinct(Span<T> keys)
    {
        keys.Sort();
        int distinct = 0;
        foreach (T key in keys)
        {
            if (distinct == 0 || keys[distinct - 1] != key)
                keys[distinct++] = key;
        }

        return distinct;
    }

    // Lays out the internal levels above `keys`, root first. Level l (1 = just above the leaves) has
    // ceil(nodes below / (B + 1)) nodes; levels are added until one node remains, so a set that fits in one leaf
    // has none. Separator i of node k on level l is the smallest key under child c = k * (B + 1) + i + 1, which is
    // the first key of that child's leftmost leaf, c * (B + 1)^(l - 1). A separator for a child past the last
    // leaf is padding.
    private static (T[] Levels, int[] Starts) BuildInternalLevels(T[] keys, int count)
    {
        int b = NodeSize;
        int leafNodes = keys.Length / b;

        var nodesPerLevel = new List<int>();   // bottom-up
        for (int nodes = leafNodes; nodes > 1;)
        {
            nodes = (nodes + b) / (b + 1);
            nodesPerLevel.Add(nodes);
        }

        int height = nodesPerLevel.Count;
        var starts = new int[height];
        int total = 0;
        for (int h = 0; h < height; h++)
        {
            starts[h] = total;
            total += nodesPerLevel[height - 1 - h] * b;
        }

        var levels = new T[total];
        long leavesPerChild = 1;   // (B + 1)^(l - 1) for the level being filled
        for (int l = 1; l <= height; l++)
        {
            int start = starts[height - l];
            int nodes = nodesPerLevel[l - 1];
            for (int k = 0; k < nodes; k++)
            {
                for (int i = 0; i < b; i++)
                {
                    long firstKey = (((long)k * (b + 1)) + i + 1) * leavesPerChild * b;
                    levels[start + (k * b) + i] = firstKey < count ? keys[firstKey] : T.MaxValue;
                }
            }

            leavesPerChild *= b + 1;
        }

        return (levels, starts);
    }

    // `other`'s distinct keys in ascending order: another FrozenSortedSet as it stands, anything else copied once.
    private static ReadOnlySpan<T> SortedDistinct(IEnumerable<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other is FrozenSortedSet<T> frozen)
            return frozen.AsSpan();

        T[] copy = other.ToArray();
        return copy.AsSpan(0, SortDistinct(copy));
    }

    /// <summary>
    /// Enumerates the keys in ascending order without allocating. The set is immutable, so there is no version
    /// to check.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly T[] _keys;
        private readonly int _count;
        private int _index;
        private T _current;

        internal Enumerator(FrozenSortedSet<T> set)
        {
            _keys = set._keys;
            _count = set._count;
            _index = -1;
            _current = default;
        }

        /// <summary>Gets the key at the current position.</summary>
        public readonly T Current => _current;

        readonly object IEnumerator.Current => _current;

        /// <summary>Advances to the next key.</summary>
        /// <returns><c>true</c> while a key remains; otherwise <c>false</c>.</returns>
        public bool MoveNext()
        {
            int next = _index + 1;
            if (next < _count)
            {
                _index = next;
                _current = _keys[next];
                return true;
            }

            _index = _count;
            _current = default;
            return false;
        }

        /// <summary>Resets the enumerator to before the first key.</summary>
        public void Reset()
        {
            _index = -1;
            _current = default;
        }

        /// <summary>Does nothing; the enumerator holds no resources.</summary>
        public readonly void Dispose()
        {
        }
    }
}
