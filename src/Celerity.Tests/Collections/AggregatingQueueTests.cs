using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural coverage for <see cref="AggregatingQueue{T, TMonoid}"/>: construction, the queue operations, and the
/// aggregate across the front / back split that the two-stacks layout keeps. The order-sensitive cases run on
/// <see cref="ConcatMonoid"/>, because a commutative fold cannot tell whether the front and back were combined in
/// the right order.
/// </summary>
public class AggregatingQueueTests
{
    // ---- construction ------------------------------------------------------------------------------

    [Fact]
    public void DefaultConstructor_ShouldCreateAnEmptyQueueWithNoCapacity()
    {
        var queue = new AggregatingQueue<int, SumMonoid<int>>();

        Assert.Empty(queue);
        Assert.Equal(0, queue.Capacity);
        Assert.Equal(0, queue.Aggregate);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 4)]
    [InlineData(4, 4)]
    [InlineData(1000, 1024)]
    public void CapacityConstructor_ShouldRoundUpToAPowerOfTwo(int requested, int expected)
    {
        var queue = new AggregatingQueue<int, SumMonoid<int>>(requested);

        Assert.Equal(expected, queue.Capacity);
        Assert.Empty(queue);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData((1 << 30) + 1)]
    [InlineData(int.MaxValue)]
    public void CapacityConstructor_ShouldThrow_WhenCapacityIsOutOfRange(int capacity)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new AggregatingQueue<int, SumMonoid<int>>(capacity));

        Assert.Equal("capacity", ex.ParamName);
    }

    [Fact]
    public void CapacityAndMonoidConstructor_ShouldThrow_WhenCapacityIsNegative()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new AggregatingQueue<int, SaturatingSumMonoid>(-1, new SaturatingSumMonoid(10)));

        Assert.Equal("capacity", ex.ParamName);
    }

    [Fact]
    public void CollectionConstructor_ShouldThrow_WhenCollectionIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new AggregatingQueue<int, SumMonoid<int>>(null!));
        Assert.Throws<ArgumentNullException>(
            () => new AggregatingQueue<int, SaturatingSumMonoid>(null!, new SaturatingSumMonoid(10)));
    }

    [Fact]
    public void CollectionConstructor_ShouldEnqueueInEnumerationOrder_FromACountedSource()
    {
        var queue = new AggregatingQueue<string, ConcatMonoid>(new[] { "a", "b", "c" });

        Assert.Equal(new[] { "a", "b", "c" }, queue);
        Assert.Equal("abc", queue.Aggregate);
        Assert.Equal(4, queue.Capacity);
    }

    [Fact]
    public void CollectionConstructor_ShouldEnqueueInEnumerationOrder_FromAnUncountedSource()
    {
        var queue = new AggregatingQueue<string, ConcatMonoid>(Yield("x", "y", "z", "w", "v"));

        Assert.Equal(new[] { "x", "y", "z", "w", "v" }, queue);
        Assert.Equal("xyzwv", queue.Aggregate);
        Assert.Equal(8, queue.Capacity);
    }

    [Fact]
    public void CollectionConstructor_ShouldAllocateNothing_ForAnEmptyCountedSource()
    {
        var queue = new AggregatingQueue<int, SumMonoid<int>>(new List<int>());

        Assert.Empty(queue);
        Assert.Equal(0, queue.Capacity);
    }

    [Fact]
    public void CollectionConstructor_ShouldLeaveLiveEnumeratorsUnaffectedByItsOwnEnqueues()
    {
        // The constructor enqueues through the public path, then resets the version, so a fresh queue reads as
        // never modified. An enumerator taken now must survive a read-only aggregate.
        var queue = new AggregatingQueue<int, SumMonoid<int>>(new[] { 1, 2, 3 });
        var enumerator = queue.GetEnumerator();

        _ = queue.Aggregate;

        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current);
    }

    [Fact]
    public void StatefulMonoidConstructors_ShouldFoldWithTheSuppliedInstance()
    {
        var empty = new AggregatingQueue<int, SaturatingSumMonoid>(0, new SaturatingSumMonoid(10));
        empty.Enqueue(7);
        empty.Enqueue(7);
        Assert.Equal(10, empty.Aggregate);

        var seeded = new AggregatingQueue<int, SaturatingSumMonoid>(new[] { 4, 4, 4 }, new SaturatingSumMonoid(10));
        Assert.Equal(10, seeded.Aggregate);

        // The default instance has a ceiling of 0, which is how a stateful monoid passed through the default
        // constructor would show up.
        var defaulted = new AggregatingQueue<int, SaturatingSumMonoid>(new[] { 4, 4 });
        Assert.Equal(0, defaulted.Aggregate);
    }

    // ---- queue operations --------------------------------------------------------------------------

    [Fact]
    public void Dequeue_ShouldReturnElementsOldestFirst()
    {
        var queue = new AggregatingQueue<int, SumMonoid<int>>();
        for (int i = 1; i <= 5; i++)
            queue.Enqueue(i);

        for (int i = 1; i <= 5; i++)
            Assert.Equal(i, queue.Dequeue());

        Assert.Empty(queue);
    }

    [Fact]
    public void Dequeue_ShouldThrow_WhenEmpty()
    {
        var queue = new AggregatingQueue<int, SumMonoid<int>>();

        var ex = Assert.Throws<InvalidOperationException>(() => queue.Dequeue());
        Assert.Contains("empty", ex.Message);
    }

    [Fact]
    public void Peek_ShouldReturnTheOldestElementWithoutRemovingIt()
    {
        var queue = new AggregatingQueue<int, SumMonoid<int>>(new[] { 3, 4 });

        Assert.Equal(3, queue.Peek());
        Assert.Equal(2, queue.Count);

        queue.Dequeue();
        Assert.Equal(4, queue.Peek());
    }

    [Fact]
    public void Peek_ShouldThrow_WhenEmpty()
    {
        var queue = new AggregatingQueue<int, SumMonoid<int>>();

        Assert.Throws<InvalidOperationException>(() => queue.Peek());
    }

    [Fact]
    public void TryDequeue_ShouldReportEmptiness()
    {
        var queue = new AggregatingQueue<string, ConcatMonoid>();

        Assert.False(queue.TryDequeue(out string? missing));
        Assert.Null(missing);

        queue.Enqueue("a");
        Assert.True(queue.TryDequeue(out string? item));
        Assert.Equal("a", item);
        Assert.Empty(queue);
    }

    [Fact]
    public void TryPeek_ShouldReportEmptiness()
    {
        var queue = new AggregatingQueue<string, ConcatMonoid>();

        Assert.False(queue.TryPeek(out string? missing));
        Assert.Null(missing);

        queue.Enqueue("a");
        Assert.True(queue.TryPeek(out string? item));
        Assert.Equal("a", item);
        Assert.Single(queue);
    }

    // ---- the aggregate -----------------------------------------------------------------------------

    [Fact]
    public void Aggregate_ShouldBeTheIdentity_WhenEmpty()
    {
        Assert.Equal(int.MinValue, new AggregatingQueue<int, MaxMonoid<int>>().Aggregate);
        Assert.Equal(string.Empty, new AggregatingQueue<string, ConcatMonoid>().Aggregate);
    }

    [Fact]
    public void Aggregate_ShouldFoldOldestToNewest_AcrossTheFrontAndBack()
    {
        var queue = new AggregatingQueue<string, ConcatMonoid>();
        queue.Enqueue("a");
        queue.Enqueue("b");
        queue.Enqueue("c");
        Assert.Equal("abc", queue.Aggregate);                // all in the back

        Assert.Equal("a", queue.Dequeue());                  // flips: b, c become the front
        Assert.Equal("bc", queue.Aggregate);                 // front only

        queue.Enqueue("d");
        queue.Enqueue("e");
        Assert.Equal("bcde", queue.Aggregate);               // front "bc" then back "de"

        Assert.Equal("b", queue.Dequeue());
        Assert.Equal("c", queue.Dequeue());                  // front exhausted, back untouched
        Assert.Equal("de", queue.Aggregate);

        Assert.Equal("d", queue.Dequeue());                  // flips again
        Assert.Equal("e", queue.Aggregate);

        Assert.Equal("e", queue.Dequeue());
        Assert.Equal(string.Empty, queue.Aggregate);
    }

    [Fact]
    public void Aggregate_ShouldTrackASlidingWindowMaximum()
    {
        int[] samples = { 5, 1, 3, 9, 2, 2, 8, 0, 7, 4 };
        const int Window = 3;
        var queue = new AggregatingQueue<int, MaxMonoid<int>>(Window);

        for (int i = 0; i < samples.Length; i++)
        {
            if (queue.Count == Window)
                queue.Dequeue();
            queue.Enqueue(samples[i]);

            int expected = samples.Skip(Math.Max(0, i - Window + 1)).Take(Math.Min(Window, i + 1)).Max();
            Assert.Equal(expected, queue.Aggregate);
        }

        // A window that never outgrows its pre-sized ring never reallocates.
        Assert.Equal(4, queue.Capacity);
    }

    [Fact]
    public void Aggregate_ShouldAnswerTheDocumentedGcdMonoid()
    {
        var queue = new AggregatingQueue<uint, GcdMonoid>(new uint[] { 12, 18, 30 });
        Assert.Equal(6u, queue.Aggregate);

        queue.Dequeue();
        queue.Enqueue(45);
        Assert.Equal(3u, queue.Aggregate);
    }

    [Fact]
    public void Aggregate_ShouldBeOrderPreserving_ForAValueTypedNonCommutativeFold()
    {
        var queue = new AggregatingQueue<int, FirstNonZeroMonoid>(new[] { 0, 0, 7, 3 });
        Assert.Equal(7, queue.Aggregate);

        queue.Dequeue();
        queue.Dequeue();
        queue.Dequeue();
        queue.Enqueue(5);
        Assert.Equal(3, queue.Aggregate);
    }

    // ---- growth ------------------------------------------------------------------------------------

    [Fact]
    public void Enqueue_ShouldGrowAWrappedRing_KeepingOrderAndTheFrontFolds()
    {
        // Wrap the head away from slot 0, leave a non-empty front with its suffix folds in place, then fill the
        // ring so the growth has to unwrap both arrays together.
        var queue = new AggregatingQueue<string, ConcatMonoid>(4);
        foreach (string s in new[] { "a", "b", "c", "d" })
            queue.Enqueue(s);

        Assert.Equal("a", queue.Dequeue());                  // front: b c d, head at slot 1
        queue.Enqueue("e");                                  // wraps into slot 0
        Assert.Equal(4, queue.Capacity);

        queue.Enqueue("f");                                  // full: grows to 8
        Assert.Equal(8, queue.Capacity);
        Assert.Equal(new[] { "b", "c", "d", "e", "f" }, queue);
        Assert.Equal("bcdef", queue.Aggregate);

        Assert.Equal("b", queue.Dequeue());
        Assert.Equal("cdef", queue.Aggregate);               // the copied front folds still answer
        Assert.Equal("c", queue.Dequeue());
        Assert.Equal("d", queue.Dequeue());
        Assert.Equal("ef", queue.Aggregate);
        Assert.Equal("e", queue.Dequeue());
        Assert.Equal("f", queue.Aggregate);
    }

    [Fact]
    public void Enqueue_ShouldGrowFromZeroCapacityToTheDefault()
    {
        var queue = new AggregatingQueue<int, SumMonoid<int>>();
        queue.Enqueue(1);

        Assert.Equal(4, queue.Capacity);
    }

    // ---- Clear -------------------------------------------------------------------------------------

    [Fact]
    public void Clear_ShouldEmptyTheQueueAndResetTheAggregate_KeepingTheCapacity()
    {
        var queue = new AggregatingQueue<string, ConcatMonoid>(new[] { "a", "b", "c" });
        queue.Dequeue();                                     // leave a populated front behind
        queue.Enqueue("d");

        queue.Clear();

        Assert.Empty(queue);
        Assert.Equal(string.Empty, queue.Aggregate);
        Assert.Equal(4, queue.Capacity);

        queue.Enqueue("x");
        queue.Enqueue("y");
        Assert.Equal("xy", queue.Aggregate);
        Assert.Equal("x", queue.Dequeue());
        Assert.Equal("y", queue.Aggregate);
    }

    [Fact]
    public void Clear_ShouldWorkForAValueType()
    {
        var queue = new AggregatingQueue<int, SumMonoid<int>>(new[] { 1, 2, 3 });
        queue.Dequeue();

        queue.Clear();

        Assert.Empty(queue);
        Assert.Equal(0, queue.Aggregate);
        queue.Enqueue(5);
        Assert.Equal(5, queue.Aggregate);
    }

    [Fact]
    public void DequeueAndClear_ShouldReleaseReferences()
    {
        // A dequeued element must not stay reachable from either array: the value slot or its suffix fold.
        var queue = new AggregatingQueue<object, LastObjectMonoid>();
        WeakReference dequeued = EnqueueThenDequeue(queue);
        WeakReference cleared = EnqueueThenClear(queue);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(dequeued.IsAlive);
        Assert.False(cleared.IsAlive);
        GC.KeepAlive(queue);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference EnqueueThenDequeue(AggregatingQueue<object, LastObjectMonoid> queue)
    {
        // Enqueued last, so after the flip it is both a value slot and its own suffix fold. (Under this monoid an
        // older element's fold is the newest element, so it would never test the fold slot.)
        var item = new object();
        queue.Enqueue(new object());
        queue.Enqueue(item);
        queue.Dequeue();                                     // flips: item's fold slot now holds item
        queue.Dequeue();
        return new WeakReference(item);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference EnqueueThenClear(AggregatingQueue<object, LastObjectMonoid> queue)
    {
        var item = new object();
        queue.Enqueue(new object());
        queue.Enqueue(item);
        queue.Dequeue();                                     // flips: item survives in the front, as value and fold
        queue.Clear();
        return new WeakReference(item);
    }

    [Fact]
    public void Clear_ShouldReleaseEveryReference_InAWrappedRing()
    {
        // Clear only touches the occupied run, so the run here wraps and spans both the front and the back.
        var queue = new AggregatingQueue<object, LastObjectMonoid>(4);
        WeakReference[] refs = FillWrappedThenClear(queue);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.All(refs, r => Assert.False(r.IsAlive));
        Assert.Empty(queue);
        Assert.Equal(4, queue.Capacity);
        GC.KeepAlive(queue);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference[] FillWrappedThenClear(AggregatingQueue<object, LastObjectMonoid> queue)
    {
        var items = new object[5];
        for (int i = 0; i < items.Length; i++)
            items[i] = new object();

        queue.Enqueue(items[0]);
        queue.Enqueue(items[1]);
        queue.Enqueue(items[2]);
        queue.Dequeue();                                     // flips: items 1, 2 are the front, head at slot 1
        queue.Enqueue(items[3]);
        queue.Enqueue(items[4]);                             // wraps into slot 0: the run is slots 1, 2, 3, 0
        queue.Clear();

        var refs = new WeakReference[5];
        for (int i = 0; i < refs.Length; i++)
            refs[i] = new WeakReference(items[i]);

        return refs;
    }

    [Fact]
    public void Enqueue_ShouldCommitNothing_WhenTheFoldThrows()
    {
        // SumMonoid<decimal> overflows. The failed enqueue must leave the queue exactly as it was, or it would hold
        // an element its Aggregate does not include.
        var queue = new AggregatingQueue<decimal, SumMonoid<decimal>>();
        queue.Enqueue(decimal.MaxValue);
        var enumerator = queue.GetEnumerator();

        Assert.Throws<OverflowException>(() => queue.Enqueue(1m));

        Assert.Equal(new[] { decimal.MaxValue }, queue);
        Assert.Equal(decimal.MaxValue, queue.Aggregate);
        Assert.True(enumerator.MoveNext());                  // nothing changed, so the enumerator survives

        Assert.Equal(decimal.MaxValue, queue.Dequeue());
        queue.Enqueue(1m);
        Assert.Equal(1m, queue.Aggregate);
    }

    [Fact]
    public void Dequeue_ShouldCommitNothing_WhenTheIdentityThrowsDuringAFlip()
    {
        var control = new IdentityControl();
        var queue = new AggregatingQueue<string, ThrowingIdentityMonoid>(
            new[] { "a", "b", "c" }, new ThrowingIdentityMonoid(control));
        var enumerator = queue.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        control.ShouldThrow = true;
        Assert.Throws<InvalidOperationException>(() => queue.Dequeue());
        control.ShouldThrow = false;

        Assert.Equal(3, queue.Count);
        Assert.Equal(new[] { "a", "b", "c" }, queue);
        Assert.Equal("abc", queue.Aggregate);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("b", enumerator.Current);

        Assert.Equal("a", queue.Dequeue());
        Assert.Equal("bc", queue.Aggregate);
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Clear_ShouldCommitNothing_WhenTheIdentityThrows()
    {
        var control = new IdentityControl();
        var queue = new AggregatingQueue<string, ThrowingIdentityMonoid>(
            new[] { "a", "b", "c", "d" }, new ThrowingIdentityMonoid(control));
        queue.Dequeue();
        queue.Enqueue("e");                                  // wrapped ring with both front and back populated
        var enumerator = queue.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        control.ShouldThrow = true;
        Assert.Throws<InvalidOperationException>(() => queue.Clear());
        control.ShouldThrow = false;

        Assert.Equal(4, queue.Count);
        Assert.Equal(new[] { "b", "c", "d", "e" }, queue);
        Assert.Equal("bcde", queue.Aggregate);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("c", enumerator.Current);

        queue.Clear();
        Assert.Empty(queue);
        Assert.Equal(string.Empty, queue.Aggregate);
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        queue.Enqueue("f");
        Assert.Equal("f", queue.Aggregate);
    }

    private sealed class IdentityControl
    {
        public bool ShouldThrow;
    }

    private readonly struct ThrowingIdentityMonoid(IdentityControl control) : IMonoid<string>
    {
        public string Identity => control.ShouldThrow
            ? throw new InvalidOperationException("identity unavailable")
            : string.Empty;

        public string Combine(string left, string right) => left + right;
    }

    private static IEnumerable<string> Yield(params string[] items)
    {
        foreach (string item in items)
            yield return item;
    }

    /// <summary>"Last non-null wins" over objects — a reference-typed fold whose folds hold the elements themselves.</summary>
    private readonly struct LastObjectMonoid : IMonoid<object>
    {
        private static readonly object Sentinel = new();

        public object Identity => Sentinel;

        public object Combine(object left, object right) => ReferenceEquals(right, Sentinel) ? left : right;
    }
}
