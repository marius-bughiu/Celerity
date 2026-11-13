using System.Collections;
using System.Collections.Concurrent;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Regression tests for constructors that size their storage from a counted source's
/// <see cref="ICollection{T}.Count"/> and then fill it.
///
/// <para>
/// <c>Count</c> and <c>CopyTo</c> are two separate calls, and a collection another thread is changing need not
/// agree with itself across them. A <see cref="ConcurrentDictionary{TKey, TValue}"/> that loses an entry in between
/// copies one pair fewer than it counted and leaves the last slot of the destination untouched. A constructor that
/// trusted <c>CopyTo</c> to fill every slot then built that leftover <c>default</c> in as if the caller had supplied
/// it: a <c>(0, null)</c> key in a frozen map, a zero-width interval or box at the origin, a <c>0 → 0</c> edge, a
/// trailing <c>0</c> folded into every range query that reached it.
/// </para>
///
/// <para>
/// Each test hands a constructor a source whose count has gone stale — it reports more elements than it then
/// copies or yields — and checks that the result holds exactly what the source yielded, and that it still behaves
/// as a well-formed instance of its type afterwards. A source that reports fewer than it yields is the other half:
/// it gained elements after it was counted, and every constructor rejects it with <see cref="ArgumentException"/>
/// rather than dropping or inventing elements.
/// </para>
/// </summary>
public class StaleCountSourceTests
{
    // ---- the report: a live ConcurrentDictionary ----------------------------------------------------

    [Fact]
    public void FrozenSortedMap_ShouldHoldOnlyTheLiveEntries_WhenAConcurrentDictionaryLosesOneAfterItIsCounted()
    {
        var live = new ConcurrentDictionary<int, string>();
        live[1] = "one";
        live[2] = "two";
        live[3] = "three";

        var map = new FrozenSortedMap<int, string>(new RemovesAfterCount<int, string>(live, victim: 2));

        Assert.Equal(2, map.Count);
        Assert.False(map.ContainsKey(0));
        Assert.Equal(new[] { 1, 3 }, map.Keys);
        Assert.Equal("three", map[3]);
    }

    // ---- keys -------------------------------------------------------------------------------------

    [Fact]
    public void FrozenSortedMap_ShouldNotReportADuplicateKey_WhenACountedSourceShrinks()
    {
        // Two slots CopyTo never wrote are two default keys, which the duplicate check rejected.
        var map = new FrozenSortedMap<int, string>(Shrunk(new KeyValuePair<int, string>(5, "five")));

        Assert.Equal(new[] { 5 }, map.Keys);
        Assert.Equal("five", map[5]);
    }

    [Fact]
    public void FrozenSortedSet_ShouldNotAddADefaultKey_WhenACountedSourceShrinks()
    {
        var set = new FrozenSortedSet<int>(Shrunk(7, 3, 9));

        Assert.Equal(new[] { 3, 7, 9 }, set);
        Assert.False(set.Contains(0));
    }

    [Fact]
    public void FrozenSortedSet_ShouldCompareOnlyWhatACountedSourceYields_WhenItShrinks()
    {
        var set = new FrozenSortedSet<int>(new[] { 0, 3, 7 });

        // The other set is {3, 7}. Read with a default 0 appended, it looked equal to {0, 3, 7}, and a superset of it.
        Assert.False(set.SetEquals(Shrunk(3, 7)));
        Assert.False(set.IsSubsetOf(Shrunk(3, 7)));
        Assert.True(set.IsProperSupersetOf(Shrunk(3, 7)));
    }

    // ---- spatial and interval indexes ----------------------------------------------------------------

    [Fact]
    public void IntervalTree_ShouldNotIndexADefaultInterval_WhenACountedSourceShrinks()
    {
        var tree = new IntervalTree<int, string>(Shrunk(new Interval<int, string>(5, 8, "a")));

        Assert.Equal(1, tree.Count);
        Assert.Equal(0, tree.CountContaining(0));
        Assert.Equal(1, tree.CountContaining(6));
    }

    [Fact]
    public void RTree_ShouldNotIndexADefaultBox_WhenACountedSourceShrinks()
    {
        var tree = new RTree<string>(Shrunk(new SpatialBox<string>(1, 1, 2, 2, "a")));

        Assert.Equal(1, tree.Count);
        Assert.Equal(0, tree.CountAtPoint(0, 0));
        Assert.Equal(1, tree.CountAtPoint(1.5, 1.5));
    }

    [Fact]
    public void KdTree_ShouldNotIndexADefaultPoint_WhenACountedSourceShrinks()
    {
        var tree = new KdTree<string>(Shrunk(new SpatialPoint<string>(5, 5, "a")));

        Assert.Equal(1, tree.Count);
        Assert.True(tree.TryFindNearest(0, 0, out SpatialPoint<string> nearest));
        Assert.Equal("a", nearest.Value);
    }

    [Fact]
    public void CompressedGraph_ShouldNotAddADefaultEdge_WhenACountedSourceShrinks()
    {
        var graph = new CompressedGraph(3, Shrunk(new GraphEdge(1, 2)));

        Assert.Equal(1, graph.EdgeCount);
        Assert.False(graph.ContainsEdge(0, 0));
        Assert.True(graph.ContainsEdge(1, 2));

        // With no vertices at all, the default edge was not merely added but rejected as out of range.
        Assert.Equal(0, new CompressedGraph(0, Shrunk<GraphEdge>()).EdgeCount);
    }

    // ---- range-query structures -------------------------------------------------------------------

    [Fact]
    public void SegmentTree_ShouldNotFoldInDefaultElements_WhenACountedSourceShrinks()
    {
        var tree = new SegmentTree<int, MinMonoid<int>>(Shrunk(5, 3, 8));

        Assert.Equal(new[] { 5, 3, 8 }, tree);
        Assert.Equal(3, tree.Aggregate);
        Assert.Equal(5, tree.Query(0, 1));

        // The leaves were laid out again for the length the source really had, so updates refold correctly.
        tree[1] = 9;
        Assert.Equal(5, tree.Aggregate);
        Assert.Equal(8, tree.Query(1, 3));
    }

    [Fact]
    public void FenwickTree_ShouldNotHoldDefaultElements_WhenACountedSourceShrinks()
    {
        var tree = new FenwickTree<int>(Shrunk(5, 3, 8));

        Assert.Equal(new[] { 5, 3, 8 }, tree);
        Assert.Equal(16, tree.PrefixSum(3));

        tree.Add(2, 1);
        Assert.Equal(new[] { 5, 3, 9 }, tree);
        Assert.Equal(12, tree.RangeSum(1, 3));
    }

    [Fact]
    public void SparseTable_ShouldNotFoldInDefaultElements_WhenACountedSourceShrinks()
    {
        var table = new SparseTable<int, MinMonoid<int>>(Shrunk(5, 3, 8));

        Assert.Equal(new[] { 5, 3, 8 }, table);
        Assert.Equal(3, table.Aggregate);
        Assert.Equal(8, table.Query(2, 3));

        // floor(log2(3)) + 1 rows of three, not the rows a five-element table needs.
        Assert.Equal(2, table.LevelCount);
        Assert.Equal(2L * 3 * sizeof(int), table.IndexSizeInBytes);
    }

    // ---- containers ---------------------------------------------------------------------------------

    [Fact]
    public void BitPackedArray_ShouldNotHoldDefaultElements_WhenACountedSourceShrinks()
    {
        var array = new BitPackedArray(Shrunk(5UL, 3UL), bitsPerValue: 8);

        Assert.Equal(new[] { 5UL, 3UL }, array);
    }

    [Fact]
    public void Deque_ShouldNotHoldDefaultElements_WhenACountedSourceShrinks()
    {
        var deque = new Deque<string>(Shrunk("a", "b"));

        Assert.Equal(new[] { "a", "b" }, deque);

        deque.PushBack("c");
        deque.PushFront("z");
        Assert.Equal(new[] { "z", "a", "b", "c" }, deque);
    }

    [Fact]
    public void GapBuffer_ShouldNotHoldDefaultElements_WhenACountedSourceShrinks()
    {
        var buffer = new GapBuffer<string>(Shrunk("a", "b"));

        Assert.Equal(new[] { "a", "b" }, buffer);

        buffer.Add("c");
        buffer.Insert(0, "z");
        Assert.Equal(new[] { "z", "a", "b", "c" }, buffer);
    }

    [Fact]
    public void MinMaxHeap_ShouldNotHoldDefaultEntries_WhenACountedSourceShrinks()
    {
        var heap = new MinMaxHeap<string, int>(Shrunk(("a", 5), ("b", 3)));

        Assert.Equal(2, heap.Count);
        Assert.Equal("b", heap.PeekMin());
        Assert.Equal("a", heap.PeekMax());
    }

    // ---- the array fast path ----------------------------------------------------------------------------

    [Fact]
    public void Constructors_ShouldCopyACovariantArray()
    {
        // A string[] passed where an IEnumerable<object> is expected is still an object[] to a type test. The
        // array is copied directly, and through a read-only span, since a writable span over it would throw
        // ArrayTypeMismatchException.
        IEnumerable<object> source = new[] { "a", "b" };

        Assert.Equal(new object[] { "a", "b" }, new Deque<object>(source));
        Assert.Equal(new object[] { "a", "b" }, new GapBuffer<object>(source));
    }

    // ---- a source that grew --------------------------------------------------------------------------

    [Fact]
    public void Constructors_ShouldThrowArgumentException_WhenACountedSourceYieldsMoreThanItsCount()
    {
        // Each source counted one element and then produced two. There is no slot for the second, and keeping only
        // the first would drop an element the source still holds, so the constructor names its parameter instead.
        Assert.Throws<ArgumentException>("source",
            () => new FrozenSortedMap<int, string>(Grown(new KeyValuePair<int, string>(1, "a"), new(2, "b"))));
        Assert.Throws<ArgumentException>("source", () => new FrozenSortedSet<int>(Grown(1, 2)));
        Assert.Throws<ArgumentException>("other", () => new FrozenSortedSet<int>(new[] { 1 }).SetEquals(Grown(1, 2)));
        Assert.Throws<ArgumentException>("intervals",
            () => new IntervalTree<int, string>(Grown(new Interval<int, string>(1, 2, "a"), new(3, 4, "b"))));
        Assert.Throws<ArgumentException>("boxes",
            () => new RTree<string>(Grown(new SpatialBox<string>(0, 0, 1, 1, "a"), new(2, 2, 3, 3, "b"))));
        Assert.Throws<ArgumentException>("points",
            () => new KdTree<string>(Grown(new SpatialPoint<string>(0, 0, "a"), new(1, 1, "b"))));
        Assert.Throws<ArgumentException>("edges", () => new CompressedGraph(3, Grown(new GraphEdge(0, 1), new(1, 2))));
        Assert.Throws<ArgumentException>("values", () => new SegmentTree<int, MinMonoid<int>>(Grown(1, 2)));
        Assert.Throws<ArgumentException>("values", () => new FenwickTree<int>(Grown(1, 2)));
        Assert.Throws<ArgumentException>("values", () => new SparseTable<int, MinMonoid<int>>(Grown(1, 2)));
        Assert.Throws<ArgumentException>("values", () => new BitPackedArray(Grown(1UL, 2UL), bitsPerValue: 8));
        Assert.Throws<ArgumentException>("collection", () => new Deque<int>(Grown(1, 2)));
        Assert.Throws<ArgumentException>("collection", () => new GapBuffer<int>(Grown(1, 2)));
        Assert.Throws<ArgumentException>("items", () => new MinMaxHeap<string, int>(Grown(("a", 1), ("b", 2))));
    }

    [Fact]
    public void Constructors_ShouldThrowArgumentException_WhenAnEmptyCountedSourceYieldsAnElement()
    {
        // A count of zero sizes no storage at all, so the first element is already one too many.
        Assert.Throws<ArgumentException>("collection", () => new Deque<int>(new StaleCount<int>(0, 1)));
        Assert.Throws<ArgumentException>("items", () => new MinMaxHeap<string, int>(new StaleCount<(string, int)>(0, ("a", 1))));
    }

    // ---- sources ------------------------------------------------------------------------------------

    // Reports two more elements than it holds: two slots a constructor trusting CopyTo left at default.
    private static StaleCount<T> Shrunk<T>(params T[] items) => new(items.Length + 2, items);

    // Reports one element and holds more: a source that gained elements after it was counted.
    private static StaleCount<T> Grown<T>(params T[] items) => new(1, items);

    /// <summary>
    /// A counted source whose <see cref="Count"/> has gone stale by the time it is read: <see cref="Count"/> reports
    /// <c>count</c>, while <see cref="CopyTo"/> and the enumerator both produce <c>items</c>. Holding fewer than it
    /// reports is a concurrent collection that lost elements after it was counted — <see cref="CopyTo"/> writes what
    /// it has and leaves the rest of the destination alone, as <see cref="ConcurrentDictionary{TKey, TValue}"/>'s
    /// does. Holding more is one that gained some, and <see cref="CopyTo"/> throws for want of room.
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
    /// <see cref="Count"/> removes <c>victim</c>, so the constructor's next call — <see cref="CopyTo"/>, or the
    /// enumeration that replaces it — sees one entry fewer than it was told. Both go to the real dictionary.
    /// </summary>
    private sealed class RemovesAfterCount<TKey, TValue>(ConcurrentDictionary<TKey, TValue> live, TKey victim)
        : ICollection<KeyValuePair<TKey, TValue>>
        where TKey : notnull
    {
        public int Count
        {
            get
            {
                int count = live.Count;
                live.TryRemove(victim, out _);
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
}
