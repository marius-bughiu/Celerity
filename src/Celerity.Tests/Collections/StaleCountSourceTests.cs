using System.Collections;
using System.Collections.Concurrent;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Regression tests for reading a counted source whose <see cref="ICollection{T}.Count"/> has gone stale.
///
/// <para>
/// <c>Count</c> and <c>CopyTo</c> are two separate calls, and a collection another thread is changing need not
/// agree with itself across them. A <see cref="ConcurrentDictionary{TKey, TValue}"/> that loses an entry in between
/// copies one pair fewer than it counted and leaves the last slot of the destination untouched. A constructor that
/// trusted <c>CopyTo</c> to fill every slot built that leftover <c>default</c> in as if the caller had supplied it:
/// a <c>(0, null)</c> key in a frozen map, a zero-width interval or box at the origin, a <c>0 → 0</c> edge, a
/// trailing <c>0</c> folded into every range query that reached it. One that gains an entry made <c>CopyTo</c>
/// throw for want of room, or, where the destination had room past the count, lost the extra elements.
/// </para>
///
/// <para>
/// Each theory hands its type the same three elements through a source that reports a stale count: <c>5</c>, as if
/// it lost two after it was counted; <c>2</c>, as if it gained one; and <c>0</c>, as if it was empty when counted.
/// Every case must hold exactly the three elements and still behave as a well-formed instance of its type.
/// </para>
/// </summary>
public class StaleCountSourceTests
{
    // ---- the report: a live ConcurrentDictionary ----------------------------------------------------

    [Fact]
    public void FrozenSortedMap_ShouldHoldTheLiveEntries_WhenAConcurrentDictionaryLosesOneAfterItIsCounted()
    {
        ConcurrentDictionary<int, string> live = Live();
        var source = new ChangesAfterCount<int, string>(live, d => d.TryRemove(2, out _));

        var map = new FrozenSortedMap<int, string>(source);

        Assert.Equal(new[] { 1, 3 }, map.Keys);
        Assert.False(map.ContainsKey(0));
        Assert.Equal("three", map[3]);
    }

    [Fact]
    public void FrozenSortedMap_ShouldHoldTheLiveEntries_WhenAConcurrentDictionaryGainsOneAfterItIsCounted()
    {
        ConcurrentDictionary<int, string> live = Live();
        var source = new ChangesAfterCount<int, string>(live, d => d[4] = "four");

        var map = new FrozenSortedMap<int, string>(source);

        Assert.Equal(new[] { 1, 2, 3, 4 }, map.Keys);
        Assert.Equal("four", map[4]);
    }

    // ---- keys ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(5)]
    [InlineData(2)]
    [InlineData(0)]
    public void FrozenSortedMap_ShouldHoldExactlyWhatACountedSourceYields(int count)
    {
        var map = new FrozenSortedMap<int, string>(new StaleCount<KeyValuePair<int, string>>(count,
            new(5, "five"), new(2, "two"), new(9, "nine")));

        Assert.Equal(new[] { 2, 5, 9 }, map.Keys);
        Assert.Equal(new[] { "two", "five", "nine" }, map.Values.ToArray());
    }

    [Theory]
    [InlineData(5)]
    [InlineData(2)]
    [InlineData(0)]
    public void FrozenSortedSet_ShouldHoldExactlyWhatACountedSourceYields(int count)
    {
        var set = new FrozenSortedSet<int>(new StaleCount<int>(count, 7, 3, 9));

        Assert.Equal(new[] { 3, 7, 9 }, set);
        Assert.False(set.Contains(0));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(2)]
    [InlineData(0)]
    public void FrozenSortedSet_ShouldCompareExactlyWhatACountedSourceYields(int count)
    {
        var set = new FrozenSortedSet<int>(new[] { 3, 7, 9 });
        var other = new StaleCount<int>(count, 9, 3, 7);

        Assert.True(set.SetEquals(other));
        Assert.True(set.IsSubsetOf(other));
        Assert.False(set.IsProperSubsetOf(other));
        Assert.False(set.IsProperSupersetOf(other));
    }

    // ---- ordered sets ----------------------------------------------------------------------------------
    //
    // A count of 0 is left out: IntersectWith and IsProperSupersetOf answer a collection that counts as empty
    // without reading it, which is a correct answer for the moment it was counted.

    [Theory]
    [InlineData(4)]
    [InlineData(1)]
    public void BTreeSet_ShouldApplyExactlyWhatACountedSourceYields(int count)
    {
        var intersected = new BTreeSet<int>(new[] { 0, 2, 4, 5 });
        intersected.IntersectWith(new StaleCount<int>(count, 6, 4, 2));
        Assert.Equal(new[] { 2, 4 }, intersected);

        // A default element read from `other` was added to the set by a symmetric difference: null, for strings.
        var toggled = new BTreeSet<string>(new[] { "a", "b" });
        toggled.SymmetricExceptWith(new StaleCount<string>(count, "c", "b"));
        Assert.Equal(new[] { "a", "c" }, toggled);

        Assert.True(new BTreeSet<int>(new[] { 3, 7 }).SetEquals(new StaleCount<int>(count, 7, 3)));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(1)]
    public void RankedSet_ShouldApplyExactlyWhatACountedSourceYields(int count)
    {
        var intersected = new RankedSet<int>(new[] { 0, 2, 4, 5 });
        intersected.IntersectWith(new StaleCount<int>(count, 6, 4, 2));
        Assert.Equal(new[] { 2, 4 }, intersected);

        var toggled = new RankedSet<int>(new[] { 1, 2 });
        toggled.SymmetricExceptWith(new StaleCount<int>(count, 5, 2));
        Assert.Equal(new[] { 1, 5 }, toggled);

        Assert.True(new RankedSet<int>(new[] { 3, 7 }).SetEquals(new StaleCount<int>(count, 7, 3)));
    }

    // ---- spatial and interval indexes ----------------------------------------------------------------

    [Theory]
    [InlineData(5)]
    [InlineData(2)]
    [InlineData(0)]
    public void IntervalTree_ShouldIndexExactlyWhatACountedSourceYields(int count)
    {
        var tree = new IntervalTree<int, string>(new StaleCount<Interval<int, string>>(count,
            new(5, 8, "a"), new(1, 2, "b"), new(10, 12, "c")));

        Assert.Equal(new[] { "b", "a", "c" }, tree.Select(i => i.Value));
        Assert.Equal(0, tree.CountContaining(0));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(2)]
    [InlineData(0)]
    public void RTree_ShouldIndexExactlyWhatACountedSourceYields(int count)
    {
        var tree = new RTree<string>(new StaleCount<SpatialBox<string>>(count,
            new(1, 1, 2, 2, "a"), new(3, 3, 4, 4, "b"), new(5, 5, 6, 6, "c")));

        Assert.Equal(new[] { "a", "b", "c" }, tree.Select(b => b.Value).Order());
        Assert.Equal(0, tree.CountAtPoint(0, 0));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(2)]
    [InlineData(0)]
    public void KdTree_ShouldIndexExactlyWhatACountedSourceYields(int count)
    {
        var tree = new KdTree<string>(new StaleCount<SpatialPoint<string>>(count,
            new(6, 6, "b"), new(5, 5, "a"), new(7, 7, "c")));

        Assert.Equal(new[] { "a", "b", "c" }, tree.Select(p => p.Value).Order());
        Assert.True(tree.TryFindNearest(0, 0, out SpatialPoint<string> nearest));
        Assert.Equal("a", nearest.Value);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(2)]
    [InlineData(0)]
    public void CompressedGraph_ShouldHoldExactlyTheEdgesACountedSourceYields(int count)
    {
        var graph = new CompressedGraph(4, new StaleCount<GraphEdge>(count, new(1, 2), new(2, 3), new(3, 1)));

        Assert.Equal(3, graph.EdgeCount);
        Assert.False(graph.ContainsEdge(0, 0));
        Assert.True(graph.ContainsEdge(3, 1));
    }

    [Fact]
    public void CompressedGraph_ShouldAcceptAShrunkSource_WhenThereAreNoVertices()
    {
        // The default edge was not merely added here but rejected as out of range, for an edge nobody passed.
        Assert.Equal(0, new CompressedGraph(0, new StaleCount<GraphEdge>(2)).EdgeCount);
    }

    // ---- range-query structures -------------------------------------------------------------------

    [Theory]
    [InlineData(5)]
    [InlineData(2)]
    [InlineData(0)]
    public void SegmentTree_ShouldFoldExactlyWhatACountedSourceYields(int count)
    {
        var tree = new SegmentTree<int, MinMonoid<int>>(new StaleCount<int>(count, 5, 3, 8));

        Assert.Equal(new[] { 5, 3, 8 }, tree);
        Assert.Equal(3, tree.Aggregate);

        // The leaves sit where the 2n layout of the real length puts them, so an update refolds correctly.
        tree[1] = 9;
        Assert.Equal(5, tree.Aggregate);
        Assert.Equal(8, tree.Query(1, 3));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(2)]
    [InlineData(0)]
    public void FenwickTree_ShouldHoldExactlyWhatACountedSourceYields(int count)
    {
        var tree = new FenwickTree<int>(new StaleCount<int>(count, 5, 3, 8));

        Assert.Equal(new[] { 5, 3, 8 }, tree);
        Assert.Equal(16, tree.PrefixSum(3));

        tree.Add(2, 1);
        Assert.Equal(new[] { 5, 3, 9 }, tree);
        Assert.Equal(12, tree.RangeSum(1, 3));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(2)]
    [InlineData(0)]
    public void SparseTable_ShouldFoldExactlyWhatACountedSourceYields(int count)
    {
        var table = new SparseTable<int, MinMonoid<int>>(new StaleCount<int>(count, 5, 3, 8));

        Assert.Equal(new[] { 5, 3, 8 }, table);
        Assert.Equal(3, table.Aggregate);
        Assert.Equal(8, table.Query(2, 3));

        // floor(log2(3)) + 1 rows of three: the shape of the real length, not of the count.
        Assert.Equal(2, table.LevelCount);
        Assert.Equal(2L * 3 * sizeof(int), table.IndexSizeInBytes);
    }

    // ---- containers ---------------------------------------------------------------------------------

    [Theory]
    [InlineData(5)]
    [InlineData(2)]
    [InlineData(0)]
    public void BitPackedArray_ShouldHoldExactlyWhatACountedSourceYields(int count)
    {
        var array = new BitPackedArray(new StaleCount<ulong>(count, 5, 3, 8), bitsPerValue: 8);

        Assert.Equal(new[] { 5UL, 3UL, 8UL }, array);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(2)]
    [InlineData(0)]
    public void Deque_ShouldHoldExactlyWhatACountedSourceYields(int count)
    {
        var deque = new Deque<string>(new StaleCount<string>(count, "a", "b", "c"));

        Assert.Equal(new[] { "a", "b", "c" }, deque);
        Assert.Equal(3, deque.Capacity);

        deque.PushBack("d");
        deque.PushFront("z");
        Assert.Equal(new[] { "z", "a", "b", "c", "d" }, deque);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(2)]
    [InlineData(0)]
    public void GapBuffer_ShouldHoldExactlyWhatACountedSourceYields(int count)
    {
        var buffer = new GapBuffer<string>(new StaleCount<string>(count, "a", "b", "c"));

        // Copied into an array of exactly what it yielded, as documented, so the gap starts empty.
        Assert.Equal(new[] { "a", "b", "c" }, buffer);
        Assert.Equal(3, buffer.Capacity);

        buffer.Add("d");
        buffer.Insert(0, "z");
        Assert.Equal(new[] { "z", "a", "b", "c", "d" }, buffer);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(2)]
    [InlineData(0)]
    public void MinMaxHeap_ShouldHoldExactlyWhatACountedSourceYields(int count)
    {
        var heap = new MinMaxHeap<string, int>(new StaleCount<(string, int)>(count, ("a", 5), ("b", 3), ("c", 8)));

        Assert.Equal(3, heap.Count);
        Assert.Equal("b", heap.PeekMin());
        Assert.Equal("c", heap.PeekMax());
    }

    // ---- what is copied directly, and what is not -----------------------------------------------------

    [Fact]
    public void Constructors_ShouldCopyAnArrayOrAList_IncludingACovariantArray()
    {
        // A string[] passed where an IEnumerable<object> is expected is still an object[] to a type test. It is
        // read through a read-only span, since a writable span over it would throw ArrayTypeMismatchException.
        IEnumerable<object> covariant = new[] { "a", "b" };
        Assert.Equal(new object[] { "a", "b" }, new Deque<object>(covariant));
        Assert.Equal(new object[] { "a", "b" }, new GapBuffer<object>(covariant));

        Assert.Equal(new[] { 1, 2, 3 }, new Deque<int>(new List<int> { 1, 2, 3 }));

        // Not a collection at all: left to Enumerable.ToArray.
        Assert.Equal(3, new MinMaxHeap<string, int>(new[] { ("a", 1), ("b", 2), ("c", 3) }.Select(p => p)).Count);
    }

    [Fact]
    public void Constructors_ShouldReadAListSubclassThroughTheInterfaceItImplements()
    {
        // A subclass can re-implement ICollection<T> and IEnumerable<T> to present other contents than its List<T>
        // storage. Its count describes that view, so the view is what is read: only an exact List<T> is copied
        // from its storage.
        var view = new EvensOnly(1, 2, 3, 4);

        Assert.Equal(new[] { 2, 4 }, new Deque<int>(view));
        Assert.Equal(new[] { 2, 4 }, new FrozenSortedSet<int>(view));
        Assert.Equal(new[] { 2, 4 }, new SegmentTree<int, MinMonoid<int>>(view));
    }

    // ---- sources ------------------------------------------------------------------------------------

    private static ConcurrentDictionary<int, string> Live()
    {
        var live = new ConcurrentDictionary<int, string>();
        live[1] = "one";
        live[2] = "two";
        live[3] = "three";
        return live;
    }

    /// <summary>
    /// A counted source whose <see cref="Count"/> has gone stale by the time it is read: <see cref="Count"/> reports
    /// <c>count</c>, while <see cref="CopyTo"/> and the enumerator both produce <c>items</c>. Holding fewer than it
    /// reports is a concurrent collection that lost elements after it was counted — <see cref="CopyTo"/> writes what
    /// it has and leaves the rest of the destination alone, as <see cref="ConcurrentDictionary{TKey, TValue}"/>'s
    /// does. Holding more is one that gained some, and <see cref="CopyTo"/> throws for want of room unless the
    /// destination has more room than the count asked for.
    /// </summary>
    private sealed class StaleCount<T>(int count, params T[] items) : ICollection<T>
    {
        public int Count => count;

        public bool IsReadOnly => true;

        public void CopyTo(T[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);

        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)items).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(T item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public bool Contains(T item) => throw new NotSupportedException();

        public bool Remove(T item) => throw new NotSupportedException();
    }

    /// <summary>
    /// A live <see cref="ConcurrentDictionary{TKey, TValue}"/> with the other thread folded in: reading
    /// <see cref="Count"/> runs <c>change</c> right after taking the count, so the constructor's next call —
    /// <see cref="CopyTo"/>, or the enumeration that replaces it — sees a different dictionary than it was told
    /// about. Both go to the real dictionary.
    /// </summary>
    private sealed class ChangesAfterCount<TKey, TValue>(
        ConcurrentDictionary<TKey, TValue> live, Action<ConcurrentDictionary<TKey, TValue>> change)
        : ICollection<KeyValuePair<TKey, TValue>>
        where TKey : notnull
    {
        public int Count
        {
            get
            {
                int count = live.Count;
                change(live);
                return count;
            }
        }

        public bool IsReadOnly => false;

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) =>
            ((ICollection<KeyValuePair<TKey, TValue>>)live).CopyTo(array, arrayIndex);

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => live.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(KeyValuePair<TKey, TValue> item) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public bool Contains(KeyValuePair<TKey, TValue> item) => throw new NotSupportedException();

        public bool Remove(KeyValuePair<TKey, TValue> item) => throw new NotSupportedException();
    }

    /// <summary>
    /// A <see cref="List{T}"/> that re-implements <see cref="ICollection{T}"/> and <see cref="IEnumerable{T}"/> to
    /// present only its even elements. <c>Count</c>, <c>CopyTo</c> and the enumerator agree with one another, and not
    /// with the list's own storage.
    /// </summary>
    private sealed class EvensOnly : List<int>, ICollection<int>, IEnumerable<int>
    {
        public EvensOnly(params int[] items)
            : base(items)
        {
        }

        int ICollection<int>.Count => Evens().Count();

        void ICollection<int>.CopyTo(int[] array, int arrayIndex)
        {
            foreach (int item in Evens())
                array[arrayIndex++] = item;
        }

        IEnumerator<int> IEnumerable<int>.GetEnumerator() => Evens().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => Evens().GetEnumerator();

        private IEnumerable<int> Evens()
        {
            foreach (int item in (List<int>)this)
            {
                if (item % 2 == 0)
                    yield return item;
            }
        }
    }
}
