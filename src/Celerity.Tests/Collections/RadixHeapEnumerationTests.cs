using System.Collections;
using System.Numerics;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration coverage for <see cref="RadixHeap{TElement}"/>: the struct enumerator yields every entry exactly
/// once, in bucket order (lower buckets first, which is deliberately not priority order), the generic and
/// non-generic <see cref="IEnumerable"/> paths agree, LINQ composes over it, <see cref="IEnumerator.Reset"/>
/// restarts it, every kind of mutation invalidates an active enumerator, and the operations that change
/// nothing — reads, a <c>Clear</c> of an empty heap, a <c>TrimExcess</c> that frees nothing, and a failed
/// <c>TryDequeue</c> — leave it valid.
/// </summary>
public class RadixHeapEnumerationTests
{
    private static RadixHeap<int> Build(int n)
    {
        var heap = new RadixHeap<int>();
        for (int i = 0; i < n; i++)
            heap.Enqueue(i, (ulong)((i * 37) % 100));
        return heap;
    }

    private static int BucketOf(ulong priority, ulong floor) => 64 - BitOperations.LeadingZeroCount(priority ^ floor);

    [Fact]
    public void Enumerator_YieldsEveryEntryExactlyOnce()
    {
        var heap = Build(20);

        var seen = new Dictionary<int, ulong>();
        foreach (var (element, priority) in heap)
            seen.Add(element, priority);

        Assert.Equal(20, seen.Count);
        for (int i = 0; i < 20; i++)
            Assert.Equal((ulong)((i * 37) % 100), seen[i]);
    }

    [Fact]
    public void Enumerator_WalksTheBucketsInAscendingOrder()
    {
        var heap = new RadixHeap<int>();
        var rand = new Random(5);
        var expected = new List<ulong>();
        for (int i = 0; i < 200; i++)
        {
            ulong p = (ulong)rand.Next(1 << 16) << rand.Next(48);
            heap.Enqueue(i, p);
            expected.Add(p);
        }

        // Pull a few so the floor is non-zero and bucket 0 is populated by a redistribution.
        expected.Sort();
        for (int i = 0; i < 10; i++)
            heap.Dequeue();
        expected.RemoveRange(0, 10);

        var priorities = heap.Select(entry => entry.Priority).ToList();
        Assert.Equal(expected, priorities.OrderBy(p => p));

        int previous = 0;
        foreach (ulong p in priorities)
        {
            int bucket = BucketOf(p, heap.Floor);
            Assert.True(bucket >= previous, "the enumerator went back to a lower bucket");
            previous = bucket;
        }
    }

    [Fact]
    public void Enumerator_YieldsDuplicateEntriesSeparately()
    {
        var heap = new RadixHeap<string>();
        heap.Enqueue("same", 1);
        heap.Enqueue("same", 1);

        Assert.Equal(2, heap.Count(entry => entry == ("same", 1UL)));
    }

    [Fact]
    public void EmptyHeap_EnumeratesNothing()
    {
        var heap = new RadixHeap<int>();
        int count = 0;
        foreach (var _ in heap)
            count++;
        Assert.Equal(0, count);
    }

    [Fact]
    public void GenericAndNonGenericPaths_Agree()
    {
        var heap = Build(10);

        var generic = new HashSet<int>();
        IEnumerator<(int Element, ulong Priority)> ge = ((IEnumerable<(int Element, ulong Priority)>)heap).GetEnumerator();
        while (ge.MoveNext())
            generic.Add(ge.Current.Element);
        ge.Dispose();

        // Driven by hand: Cast<T>() short-circuits on the generic interface and never reaches this path.
        var nonGeneric = new HashSet<int>();
        IEnumerator nge = ((IEnumerable)heap).GetEnumerator();
        while (nge.MoveNext())
        {
            object boxed = nge.Current;
            nonGeneric.Add((((int Element, ulong Priority))boxed).Element);
        }

        nge.Reset();
        Assert.True(nge.MoveNext());

        Assert.Equal(Enumerable.Range(0, 10).ToHashSet(), generic);
        Assert.Equal(generic, nonGeneric);
    }

    [Fact]
    public void Linq_ComposesOverEnumerator()
    {
        var heap = Build(10);
        Assert.Equal(
            (ulong)Enumerable.Range(0, 10).Sum(i => (i * 37) % 100),
            heap.Aggregate(0UL, (sum, entry) => sum + entry.Priority));
    }

    [Fact]
    public void Current_IsDefault_BeforeTheFirstAndAfterTheLastMoveNext()
    {
        var heap = Build(1);
        RadixHeap<int>.Enumerator e = heap.GetEnumerator();
        Assert.Equal(default, e.Current);

        Assert.True(e.MoveNext());
        Assert.Equal((0, 0UL), e.Current);

        Assert.False(e.MoveNext());
        Assert.Equal(default, e.Current);
        Assert.False(e.MoveNext());
        e.Dispose();
    }

    [Fact]
    public void Reset_RestartsEnumeration()
    {
        var heap = Build(5);
        RadixHeap<int>.Enumerator e = heap.GetEnumerator();

        var first = new List<int>();
        while (e.MoveNext())
            first.Add(e.Current.Element);

        e.Reset();
        Assert.Equal(default, e.Current);

        var second = new List<int>();
        while (e.MoveNext())
            second.Add(e.Current.Element);

        Assert.Equal(first, second);
        Assert.Equal(5, first.Count);
    }

    [Fact]
    public void Reset_AfterAMutation_Throws()
    {
        var heap = Build(3);
        RadixHeap<int>.Enumerator e = heap.GetEnumerator();
        Assert.True(e.MoveNext());

        heap.Enqueue(9, 99);

        var ex = Assert.Throws<InvalidOperationException>(() => e.Reset());
        Assert.Equal("The heap was modified during enumeration.", ex.Message);
    }

    public static IEnumerable<object[]> MutationsThatInvalidate()
    {
        yield return new object[] { (Action<RadixHeap<int>>)(h => h.Enqueue(999, 1000)) };
        yield return new object[] { (Action<RadixHeap<int>>)(h => h.Dequeue()) };
        yield return new object[] { (Action<RadixHeap<int>>)(h => h.TryDequeue(out _, out _)) };
        yield return new object[] { (Action<RadixHeap<int>>)(h => h.Clear()) };
        yield return new object[] { (Action<RadixHeap<int>>)(h => h.TrimExcess()) };
    }

    [Theory]
    [MemberData(nameof(MutationsThatInvalidate))]
    public void Mutation_DuringEnumeration_Throws(Action<RadixHeap<int>> mutate)
    {
        // Seven entries leave at least one bucket array short of full, so TrimExcess has something to trim.
        var heap = Build(7);
        RadixHeap<int>.Enumerator e = heap.GetEnumerator();
        Assert.True(e.MoveNext());

        mutate(heap);

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void Reads_DoNotInvalidateEnumeration()
    {
        var heap = Build(8);
        RadixHeap<int>.Enumerator e = heap.GetEnumerator();
        Assert.True(e.MoveNext());

        _ = heap.Peek();
        _ = heap.TryPeek(out _, out _);
        _ = heap.Count;
        _ = heap.Floor;

        int remaining = 1;
        while (e.MoveNext())
            remaining++;
        Assert.Equal(8, remaining);
    }

    [Fact]
    public void NoOpMutations_DoNotInvalidateEnumeration()
    {
        var heap = new RadixHeap<int>();
        RadixHeap<int>.Enumerator e = heap.GetEnumerator();

        heap.Clear();                              // already empty
        heap.TrimExcess();                         // nothing allocated
        Assert.False(heap.TryDequeue(out _, out _));
        Assert.False(heap.TryPeek(out _, out _));
        Assert.False(e.MoveNext());

        // Four entries at the floor exactly fill bucket 0's default array, so this TrimExcess frees nothing.
        for (int i = 0; i < 4; i++)
            heap.Enqueue(i, 0);
        heap.Enqueue(10, 50);
        heap.Dequeue();
        heap.Dequeue();
        heap.Dequeue();
        heap.Dequeue();
        heap.Dequeue();                            // redistributes bucket 6 into bucket 0
        heap.Enqueue(11, 50);
        heap.Enqueue(12, 50);
        heap.Enqueue(13, 50);
        heap.Enqueue(14, 50);
        heap.TrimExcess();                         // bucket 0 is 4/4 again, bucket 6 was freed: trims bucket 6

        e = heap.GetEnumerator();
        Assert.True(e.MoveNext());
        heap.TrimExcess();                         // now every bucket is exact
        Assert.Throws<ArgumentOutOfRangeException>("priority", () => heap.Enqueue(15, 49));

        int seen = 1;
        while (e.MoveNext())
            seen++;
        Assert.Equal(4, seen);
    }
}
