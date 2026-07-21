using System.Collections;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration coverage for <see cref="IndexedPriorityQueue{TElement, TPriority, THasher}"/>: the struct
/// enumerator yields every element/priority pair exactly once (in heap order, which is deliberately not
/// priority order), the generic and non-generic <see cref="IEnumerable"/> paths agree, LINQ composes over
/// it, <see cref="IEnumerator.Reset"/> restarts it, and every kind of concurrent mutation invalidates an
/// active enumerator.
/// </summary>
public class IndexedPriorityQueueEnumerationTests
{
    private static IndexedPriorityQueue<int, int, Int32WangHasher> Build(int n)
    {
        var pq = new IndexedPriorityQueue<int, int, Int32WangHasher>();
        for (int i = 0; i < n; i++)
            pq.Enqueue(i, (i * 37) % 100);
        return pq;
    }

    [Fact]
    public void Enumerator_YieldsEveryPairExactlyOnce()
    {
        var pq = Build(20);

        var elements = new List<int>();
        var priorities = new Dictionary<int, int>();
        foreach (KeyValuePair<int, int> pair in pq)
        {
            elements.Add(pair.Key);
            priorities[pair.Key] = pair.Value;
        }

        Assert.Equal(20, elements.Count);
        Assert.Equal(20, elements.Distinct().Count());
        for (int i = 0; i < 20; i++)
        {
            Assert.Contains(i, priorities.Keys);
            Assert.Equal((i * 37) % 100, priorities[i]);
        }
    }

    [Fact]
    public void EmptyQueue_EnumeratesNothing()
    {
        var pq = new IndexedPriorityQueue<int, int, Int32WangHasher>();
        int count = 0;
        foreach (KeyValuePair<int, int> _ in pq)
            count++;
        Assert.Equal(0, count);
    }

    [Fact]
    public void GenericAndNonGenericPaths_Agree()
    {
        var pq = Build(10);

        var generic = new HashSet<int>();
        IEnumerator<KeyValuePair<int, int>> ge = ((IEnumerable<KeyValuePair<int, int>>)pq).GetEnumerator();
        while (ge.MoveNext())
            generic.Add(ge.Current.Key);

        var nonGeneric = new HashSet<int>();
        IEnumerator nge = ((IEnumerable)pq).GetEnumerator();
        while (nge.MoveNext())
            nonGeneric.Add(((KeyValuePair<int, int>)nge.Current!).Key);

        Assert.Equal(Enumerable.Range(0, 10).ToHashSet(), generic);
        Assert.Equal(generic, nonGeneric);
    }

    [Fact]
    public void Linq_ComposesOverEnumerator()
    {
        var pq = Build(10);
        int sumOfPriorities = pq.Sum(p => p.Value);
        int expected = Enumerable.Range(0, 10).Sum(i => (i * 37) % 100);
        Assert.Equal(expected, sumOfPriorities);
    }

    [Fact]
    public void Reset_RestartsEnumeration()
    {
        var pq = Build(5);
        IndexedPriorityQueue<int, int, Int32WangHasher>.Enumerator e = pq.GetEnumerator();

        var first = new List<int>();
        while (e.MoveNext())
            first.Add(e.Current.Key);

        e.Reset();

        var second = new List<int>();
        while (e.MoveNext())
            second.Add(e.Current.Key);

        Assert.Equal(first, second);
        Assert.Equal(5, first.Count);
    }

    public static IEnumerable<object[]> MutationsThatInvalidate()
    {
        yield return new object[] { (Action<IndexedPriorityQueue<int, int, Int32WangHasher>>)(pq => pq.Enqueue(999, 1)) };
        yield return new object[] { (Action<IndexedPriorityQueue<int, int, Int32WangHasher>>)(pq => pq.Dequeue()) };
        yield return new object[] { (Action<IndexedPriorityQueue<int, int, Int32WangHasher>>)(pq => pq.Update(0, 999)) };
        yield return new object[] { (Action<IndexedPriorityQueue<int, int, Int32WangHasher>>)(pq => pq.Remove(0)) };
        yield return new object[] { (Action<IndexedPriorityQueue<int, int, Int32WangHasher>>)(pq => pq.Clear()) };
    }

    [Theory]
    [MemberData(nameof(MutationsThatInvalidate))]
    public void Mutation_DuringEnumeration_Throws(Action<IndexedPriorityQueue<int, int, Int32WangHasher>> mutate)
    {
        var pq = Build(8);
        IndexedPriorityQueue<int, int, Int32WangHasher>.Enumerator e = pq.GetEnumerator();
        Assert.True(e.MoveNext());

        mutate(pq);

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void PeekAndContains_DoNotInvalidateEnumeration()
    {
        var pq = Build(8);
        IndexedPriorityQueue<int, int, Int32WangHasher>.Enumerator e = pq.GetEnumerator();
        Assert.True(e.MoveNext());

        _ = pq.Peek();
        _ = pq.Contains(3);
        _ = pq.TryGetPriority(3, out _);

        // A pure read must not bump the version, so enumeration continues.
        int remaining = 1;
        while (e.MoveNext())
            remaining++;
        Assert.Equal(8, remaining);
    }
}
