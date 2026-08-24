using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Pins the two "cold" halves of the mutation-tracking contract that the rest of the suite exercises only
/// from their happy side: the version check inside <c>Enumerator.Reset()</c>, and the early-out /
/// reference-clearing arms of <c>Clear()</c>, <c>TrimExcess()</c> and the removal path.
///
/// <para>
/// <b>Why <c>Reset()</c> specifically.</b> Every enumerator in the family snapshots the collection's
/// <c>_version</c> at construction and re-checks it on <i>both</i> <c>MoveNext()</c> and <c>Reset()</c>.
/// The <c>MoveNext()</c> guard is hit constantly — every <c>foreach</c> in the suite runs it on the
/// "unmodified" side, and the dedicated enumeration test classes run it on the throwing side. The
/// <c>Reset()</c> guard is not: <c>Reset()</c> is only reachable through the explicit struct method or the
/// boxed <see cref="System.Collections.IEnumerator"/> surface, and no existing test resets an enumerator
/// that has been invalidated. That leaves the throw unproven, which matters because <c>Reset()</c> rewinds
/// the enumerator's cursor to a position derived from state the mutation may have invalidated (a shrunk
/// element array, a rebuilt free-node list, a reallocated heap). These tests assert the documented
/// <see cref="InvalidOperationException"/> — carrying the type's own diagnostic message, so a copy/paste
/// slip between collections is caught — and, for <see cref="FenwickTree{T}"/>, assert the deliberate
/// <i>exception</i> to the rule: a zero delta changes nothing observable, so by design it does not bump the
/// version and must leave live enumerators usable. <see cref="SegmentTree{T, TMonoid}"/> deliberately has no
/// such exception (its fold carries no equality contract), so its companion test pins the weaker guarantee
/// that survives: pure queries are not mutations.
/// </para>
///
/// <para>
/// <b>Why these <c>Clear()</c> / capacity paths.</b> Each of these collections short-circuits a
/// <c>Clear()</c> on an already-empty instance, and the short-circuit is not merely an optimization: taking
/// it means the version is <i>not</i> bumped, so a live enumerator survives. That is the observable
/// difference pinned here (a redundant bump would be a silent behavioural regression for callers who clear
/// defensively). The same reasoning covers
/// <see cref="IndexedPriorityQueue{TElement, TPriority, THasher}.TrimExcess"/> when the backing arrays are
/// already exactly sized. The remaining arms are the
/// <c>RuntimeHelpers.IsReferenceOrContainsReferences&lt;T&gt;()</c> guards that null out vacated slots so a
/// removed element or priority is not pinned by the retained arrays; they only execute for reference-typed
/// instantiations, so each is driven through a <c>string</c>-parameterized collection. Pinning them keeps
/// the storage-retaining <c>Clear()</c> from quietly turning into a memory leak.
/// </para>
/// </summary>
public class EnumeratorInvalidationAndClearCoverageTests
{
    // ---- Target A: Reset() version checks ----------------------------------------------------------

    [Fact]
    public void BitSetEnumeratorReset_ShouldThrowInvalidOperationException_WhenSetModified()
    {
        var bits = new BitSet(8);
        var enumerator = bits.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        bits.Set(3, true);

        var ex = Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
        Assert.Contains("Collection was modified", ex.Message);

        // The same guard on the advance path, from a freshly created (and then invalidated) enumerator.
        var second = bits.GetEnumerator();
        bits.Flip(0);
        Assert.Throws<InvalidOperationException>(() => second.MoveNext());
    }

    [Fact]
    public void DisjointSetEnumeratorReset_ShouldThrowInvalidOperationException_WhenSetModified()
    {
        var ds = new DisjointSet<int>();
        ds.Add(1);
        ds.Add(2);

        var enumerator = ds.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        ds.Union(1, 2);

        var ex = Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
        Assert.Contains("The disjoint-set was modified during enumeration.", ex.Message);

        var second = ds.GetEnumerator();
        ds.Add(3);
        Assert.Throws<InvalidOperationException>(() => second.MoveNext());
    }

    [Fact]
    public void FenwickTreeEnumeratorReset_ShouldThrowInvalidOperationException_WhenTreeModified()
    {
        var tree = new FenwickTree<int>(4);
        tree.Add(0, 5);

        var enumerator = tree.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        tree.Add(1, 7);

        var ex = Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
        Assert.Contains("The Fenwick tree was modified during enumeration.", ex.Message);

        var second = tree.GetEnumerator();
        tree[2] = 9;
        Assert.Throws<InvalidOperationException>(() => second.MoveNext());
    }

    [Fact]
    public void SegmentTreeEnumeratorReset_ShouldThrowInvalidOperationException_WhenTreeModified()
    {
        var tree = new SegmentTree<int, MinMonoid<int>>(4);
        tree[0] = 5;

        var enumerator = tree.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        tree.Combine(1, 7);

        var ex = Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
        Assert.Contains("The segment tree was modified during enumeration.", ex.Message);

        var second = tree.GetEnumerator();
        tree[2] = 9;
        Assert.Throws<InvalidOperationException>(() => second.MoveNext());
    }

    [Fact]
    public void SegmentTreeEnumeratorReset_ShouldRewindWithoutThrowing_WhenTreeWasNotModified()
    {
        // The counterpart to the FenwickTree case below, and deliberately weaker: the segment tree has no
        // no-op detection to lean on (IMonoid<T> carries no equality contract), so the only mutation-free
        // window is one with no mutation in it at all. Queries must not close it.
        var tree = new SegmentTree<int, MinMonoid<int>>(new[] { 4, 6, 1 });

        var enumerator = tree.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        _ = tree.Query(0, 3);
        _ = tree.Aggregate;
        _ = tree[1];

        enumerator.Reset();

        var values = new List<int>();
        while (enumerator.MoveNext())
            values.Add(enumerator.Current);

        Assert.Equal(new[] { 4, 6, 1 }, values);
    }

    [Fact]
    public void FenwickTreeEnumeratorReset_ShouldRewindWithoutThrowing_WhenMutationWasZeroDelta()
    {
        // A zero delta (and an indexer assignment of the value already stored) leaves every cell
        // untouched, so by design it does not bump the version and must not invalidate enumerators.
        var tree = new FenwickTree<int>(3);
        tree.Add(0, 4);
        tree.Add(1, 6);

        var enumerator = tree.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        tree.Add(2, 0);
        tree[0] = 4;

        enumerator.Reset();

        var values = new List<int>();
        while (enumerator.MoveNext())
            values.Add(enumerator.Current);

        Assert.Equal(new[] { 4, 6, 0 }, values);
    }

    [Fact]
    public void IndexedPriorityQueueEnumeratorReset_ShouldThrowInvalidOperationException_WhenQueueModified()
    {
        var pq = new IndexedPriorityQueue<int, int, Int32WangHasher>();
        pq.Enqueue(1, 10);
        pq.Enqueue(2, 20);

        var enumerator = pq.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        pq.Update(1, 30);

        var ex = Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
        Assert.Contains("The priority queue was modified during enumeration.", ex.Message);

        var second = pq.GetEnumerator();
        pq.Dequeue();
        Assert.Throws<InvalidOperationException>(() => second.MoveNext());
    }

    [Fact]
    public void SpatialGridEnumeratorReset_ShouldThrowInvalidOperationException_WhenGridModified()
    {
        var grid = new SpatialGrid<int>(0, 0, 10, 10, 1);
        grid.Add(1, 1, 1);
        grid.Add(2, 2, 2);

        var enumerator = grid.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        grid.Add(3, 3, 3);

        var ex = Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
        Assert.Contains("Collection was modified", ex.Message);

        var second = grid.GetEnumerator();
        grid.Add(4, 4, 4);
        Assert.Throws<InvalidOperationException>(() => second.MoveNext());
    }

    /// <summary>
    /// The grid's own deliberate exception to the rule, and the reason it needs pinning next to
    /// <see cref="FenwickTree{T}"/>'s: a <c>Move</c> relocates a point but changes neither the set of entries
    /// nor the slot each one occupies, so the sequence an enumerator is walking is untouched and invalidating
    /// would be gratuitous. This is the mutation the type exists for, so the guarantee is load-bearing rather
    /// than incidental.
    /// </summary>
    [Fact]
    public void TimerWheelEnumeratorReset_ShouldThrowInvalidOperationException_WhenWheelModified()
    {
        var wheel = new TimerWheel<int>(4, 2);
        wheel.Schedule(1, 1);
        wheel.Schedule(2, 2);

        var enumerator = wheel.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        wheel.Schedule(3, 3);

        var ex = Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
        Assert.Contains("Collection was modified", ex.Message);

        var second = wheel.GetEnumerator();
        wheel.Schedule(3, 4);
        Assert.Throws<InvalidOperationException>(() => second.MoveNext());
    }

    /// <summary>
    /// The wheel's own deliberate exception to the rule, alongside <see cref="SpatialGrid{TValue}"/>'s
    /// <c>Move</c>: an advance that fires nothing can still <i>relocate</i> timers, cascading a level-1 slot's
    /// contents down into level 0 as the clock reaches it, but it changes neither the set of pending timers
    /// nor the slot each one occupies. The sequence an enumerator is walking is therefore untouched, and this
    /// is the mutation the type exists for, so the guarantee is load-bearing rather than incidental.
    /// </summary>
    [Fact]
    public void TimerWheelAdvance_ShouldNotBumpTheVersion_WhenItFiresNothing()
    {
        var wheel = new TimerWheel<int>(4, 2);
        wheel.Schedule(9, 1);
        wheel.Schedule(11, 2);

        var enumerator = wheel.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.Equal(0, wheel.Advance(8, new List<int>()));

        enumerator.Reset();

        int seen = 0;
        while (enumerator.MoveNext())
            seen++;
        Assert.Equal(2, seen);
    }

    [Fact]
    public void SpatialGridMove_ShouldNotBumpTheVersion_BecauseTheSequenceIsUnchanged()
    {
        var grid = new SpatialGrid<int>(0, 0, 10, 10, 1);
        var handle = grid.Add(1, 1, 1);
        grid.Add(2, 2, 2);

        var enumerator = grid.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        grid.Move(handle, 9, 9);

        enumerator.Reset();

        int seen = 0;
        while (enumerator.MoveNext())
            seen++;
        Assert.Equal(2, seen);
    }

    [Fact]
    public void LruCacheEnumeratorReset_ShouldThrowInvalidOperationException_WhenCacheModified()
    {
        var cache = new LruCache<int, string, Int32WangHasher>(4);
        cache.Add(1, "a");
        cache.Add(2, "b");

        var enumerator = cache.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        cache.AddOrUpdate(3, "c");

        var ex = Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
        Assert.Contains("Collection was modified", ex.Message);

        var second = cache.GetEnumerator();
        cache.Remove(1);
        Assert.Throws<InvalidOperationException>(() => second.MoveNext());
    }

    // ---- Target B: Clear() / capacity early-outs and reference-clearing arms ------------------------

    [Fact]
    public void DisjointSetClear_ShouldBeNoOpAndKeepEnumeratorsValid_WhenAlreadyEmpty()
    {
        var ds = new DisjointSet<int>();
        var enumerator = ds.GetEnumerator();

        ds.Clear();

        Assert.Equal(0, ds.Count);
        Assert.Equal(0, ds.SetCount);

        // The early-out must not bump the version: the live enumerator still runs to completion.
        int seen = 0;
        while (enumerator.MoveNext())
            seen++;
        Assert.Equal(0, seen);

        // ...and the set is still fully usable afterwards.
        Assert.True(ds.Add(7));
        Assert.Equal(1, ds.Count);
        Assert.Equal(7, ds.Find(7));
    }

    [Fact]
    public void DisjointSetClear_ShouldReleaseElementsAndResetPartition_WhenElementsAreReferenceTyped()
    {
        var ds = new DisjointSet<string>();
        ds.Union("a", "b");
        ds.Add("c");
        Assert.Equal(3, ds.Count);
        Assert.Equal(2, ds.SetCount);

        int capacityBefore = ds.Capacity;
        ds.Clear();

        Assert.Equal(0, ds.Count);
        Assert.Equal(0, ds.SetCount);
        Assert.False(ds.Contains("a"));
        Assert.False(ds.Contains("c"));
        Assert.Empty(ds.GetComponents());
        Assert.Empty(ds);

        // Storage is retained, and the cleared elements are genuinely gone rather than shadowed.
        Assert.Equal(capacityBefore, ds.Capacity);
        Assert.True(ds.Add("a"));
        Assert.Equal(1, ds.ComponentSize("a"));
        Assert.False(ds.Connected("a", "b"));
    }

    [Fact]
    public void LruCacheClear_ShouldBeNoOpAndKeepEnumeratorsValid_WhenAlreadyEmpty()
    {
        var cache = new LruCache<int, string, Int32WangHasher>(4);
        var enumerator = cache.GetEnumerator();

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.Equal(4, cache.Capacity);

        int seen = 0;
        while (enumerator.MoveNext())
            seen++;
        Assert.Equal(0, seen);

        cache.Add(1, "a");
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void TopKSketchClear_ShouldBeNoOp_WhenSketchNeverObservedAnything()
    {
        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(8);

        sketch.Clear();

        Assert.Equal(0, sketch.Count);
        Assert.Equal(0, sketch.TotalCount);
        Assert.Equal(8, sketch.Capacity);
        Assert.Empty(sketch.GetTopK());

        // Still usable: the no-op must not have disturbed the monitor arrays or the element index.
        sketch.Add(42, 3);
        Assert.True(sketch.TryGetCount(42, out long count, out long error));
        Assert.Equal(3, count);
        Assert.Equal(0, error);
    }

    [Fact]
    public void TopKSketchClear_ShouldBeIdempotent_WhenCalledTwiceAfterAdds()
    {
        var sketch = new TopKSketch<int, Int32Murmur3Hasher>(4);
        for (int i = 0; i < 10; i++)
            sketch.Add(i % 3);

        Assert.Equal(3, sketch.Count);
        Assert.Equal(10, sketch.TotalCount);

        sketch.Clear();
        Assert.Equal(0, sketch.Count);
        Assert.Equal(0, sketch.TotalCount);

        // Second clear takes the already-empty early-out and must leave the same state.
        sketch.Clear();
        Assert.Equal(0, sketch.Count);
        Assert.Equal(0, sketch.TotalCount);
        Assert.Empty(sketch.GetTopK());
        Assert.False(sketch.TryGetCount(0, out long count, out long error));
        Assert.Equal(0, count);
        Assert.Equal(0, error);
    }

    [Fact]
    public void IndexedPriorityQueueClear_ShouldBeNoOpAndKeepEnumeratorsValid_WhenAlreadyEmpty()
    {
        var pq = new IndexedPriorityQueue<int, int, Int32WangHasher>(4);
        pq.Enqueue(1, 10);
        pq.Dequeue();
        Assert.Equal(0, pq.Count);

        var enumerator = pq.GetEnumerator();
        pq.Clear();

        Assert.Equal(0, pq.Count);
        Assert.Equal(4, pq.Capacity);

        int seen = 0;
        while (enumerator.MoveNext())
            seen++;
        Assert.Equal(0, seen);
    }

    [Fact]
    public void SpatialGridClear_ShouldBeNoOpAndKeepEnumeratorsValid_WhenAlreadyEmpty()
    {
        var grid = new SpatialGrid<int>(0, 0, 10, 10, 1, capacity: 4);
        var handle = grid.Add(1, 1, 1);
        grid.Remove(handle);
        Assert.Equal(0, grid.Count);

        var enumerator = grid.GetEnumerator();
        grid.Clear();

        Assert.Equal(0, grid.Count);

        int seen = 0;
        while (enumerator.MoveNext())
            seen++;
        Assert.Equal(0, seen);
    }

    [Fact]
    public void SpatialGridClearAndRemove_ShouldReleaseThePayloads_WhenTheValueIsReferenceTyped()
    {
        var grid = new SpatialGrid<string>(0, 0, 10, 10, 1);
        var alpha = grid.Add(1, 1, "alpha");
        grid.Add(2, 2, "beta");
        grid.Add(3, 3, "gamma");

        // The removal arm of the reference-clearing guard.
        grid.Remove(alpha);
        Assert.Equal(2, grid.Count);
        Assert.False(grid.TryGetPoint(alpha, out SpatialPoint<string> vacated));
        Assert.Null(vacated.Value);

        // ...and the Clear() arm, which vacates every slot at once.
        grid.Clear();
        Assert.Equal(0, grid.Count);
        Assert.Empty(grid);

        // The storage is retained and reused, so a refilled grid still reads back correctly.
        var reused = grid.Add(4, 4, "delta");
        Assert.True(grid.TryGetPoint(reused, out SpatialPoint<string> point));
        Assert.Equal("delta", point.Value);
    }

    [Fact]
    public void IndexedPriorityQueueClear_ShouldReleaseElementsAndPriorities_WhenBothAreReferenceTyped()
    {
        var pq = new IndexedPriorityQueue<string, string, StringFnV1AHasher>();
        pq.Enqueue("alpha", "p2");
        pq.Enqueue("beta", "p1");
        pq.Enqueue("gamma", "p3");
        Assert.Equal(3, pq.Count);

        int capacityBefore = pq.Capacity;
        pq.Clear();

        Assert.Equal(0, pq.Count);
        Assert.Equal(capacityBefore, pq.Capacity);
        Assert.False(pq.Contains("alpha"));
        Assert.False(pq.TryGetPriority("beta", out string? priority));
        Assert.Null(priority);
        Assert.False(pq.TryPeek(out string? peeked, out string? peekedPriority));
        Assert.Null(peeked);
        Assert.Null(peekedPriority);
        Assert.Empty(pq);

        // The index was cleared too, so the same elements can be re-enqueued rather than rejected.
        pq.Enqueue("alpha", "p9");
        Assert.Equal("alpha", pq.Peek());
    }

    [Fact]
    public void IndexedPriorityQueueRemove_ShouldReleaseVacatedPriority_WhenPriorityIsReferenceTyped()
    {
        var pq = new IndexedPriorityQueue<string, string, StringFnV1AHasher>();
        pq.Enqueue("alpha", "p1");
        pq.Enqueue("beta", "p2");
        pq.Enqueue("gamma", "p3");

        // Removing the root moves the tail into slot 0 and clears the vacated tail slot.
        Assert.True(pq.Remove("alpha", out string? removedPriority));
        Assert.Equal("p1", removedPriority);
        Assert.Equal(2, pq.Count);
        Assert.False(pq.Contains("alpha"));
        Assert.Equal("beta", pq.Peek());

        // Removing the last remaining slot takes the "slot == last" arm of the same clearing path.
        Assert.True(pq.Remove("gamma"));
        Assert.True(pq.Remove("beta", out string? lastPriority));
        Assert.Equal("p2", lastPriority);
        Assert.Equal(0, pq.Count);
        Assert.False(pq.Remove("beta", out string? absent));
        Assert.Null(absent);
    }

    [Fact]
    public void IndexedPriorityQueueTrimExcess_ShouldBeNoOpAndKeepEnumeratorsValid_WhenAlreadyExactlySized()
    {
        var pq = new IndexedPriorityQueue<int, int, Int32WangHasher>(4);
        for (int i = 0; i < 4; i++)
            pq.Enqueue(i, i);

        Assert.Equal(4, pq.Capacity);
        Assert.Equal(4, pq.Count);

        var enumerator = pq.GetEnumerator();
        pq.TrimExcess();

        Assert.Equal(4, pq.Capacity);
        Assert.Equal(4, pq.Count);

        // Nothing was reallocated, so the version must not have moved and the enumerator still drains.
        int seen = 0;
        while (enumerator.MoveNext())
            seen++;
        Assert.Equal(4, seen);
    }

    [Fact]
    public void IndexedPriorityQueueTrimExcess_ShouldBeNoOp_WhenQueueIsEmptyWithNoStorage()
    {
        var pq = new IndexedPriorityQueue<int, int, Int32WangHasher>();
        Assert.Equal(0, pq.Capacity);

        pq.TrimExcess();

        Assert.Equal(0, pq.Capacity);
        Assert.Equal(0, pq.Count);

        // The trimmed-to-nothing queue still grows normally on the next enqueue.
        pq.Enqueue(1, 1);
        Assert.Equal(1, pq.Count);
        Assert.Equal(1, pq.Peek());
    }
}
