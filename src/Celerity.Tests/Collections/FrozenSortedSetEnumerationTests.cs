using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Coverage for <see cref="FrozenSortedSet{T}"/>'s enumeration surface: the struct enumerator and both boxed
/// interface paths.
///
/// <para>
/// The set is immutable, so there is no version counter and no invalidation to test. What is left is that the
/// enumerator walks the sorted keys and stops at <see cref="FrozenSortedSet{T}.Count"/> rather than running on
/// into the <c>T.MaxValue</c> padding that fills the last node, stays stopped with
/// <see cref="FrozenSortedSet{T}.Enumerator.Current"/> cleared, and can be restarted.
/// </para>
/// </summary>
public class FrozenSortedSetEnumerationTests
{
    // Unsorted and repeated, so the walk proves it reads the built leaves rather than the source; four keys
    // leave twelve padding slots in the one int node.
    private static readonly int[] Source = [9, -4, 9, 70, -4, 2];

    private static readonly int[] Sorted = [-4, 2, 9, 70];

    [Fact]
    public void GetEnumerator_ShouldYieldTheKeysInAscendingOrder()
    {
        var walked = new List<int>();
        foreach (int key in new FrozenSortedSet<int>(Source))
            walked.Add(key);

        Assert.Equal(Sorted, walked);
    }

    [Fact]
    public void GetEnumerator_ShouldStopBeforeThePadding_WhenTheLastKeyIsBelowMaxValue()
    {
        // 17 keys fill one node and one key of the next; the other fifteen slots are int.MaxValue padding.
        var set = new FrozenSortedSet<int>(Enumerable.Range(0, 17));

        var walked = new List<int>();
        foreach (int key in set)
            walked.Add(key);

        Assert.Equal(Enumerable.Range(0, 17), walked);
    }

    [Fact]
    public void Current_ShouldBeDefault_BeforeTheFirstMoveNext()
    {
        FrozenSortedSet<int>.Enumerator enumerator = new FrozenSortedSet<int>(Source).GetEnumerator();

        Assert.Equal(0, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(-4, enumerator.Current);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenTheSetIsEmpty()
    {
        FrozenSortedSet<int>.Enumerator enumerator = new FrozenSortedSet<int>([]).GetEnumerator();

        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldKeepReturningFalse_AndClearCurrent_WhenTheWalkHasRunOut()
    {
        FrozenSortedSet<int>.Enumerator enumerator = new FrozenSortedSet<int>(Source).GetEnumerator();

        while (enumerator.MoveNext())
        {
        }

        Assert.Equal(0, enumerator.Current);
        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);
    }

    [Fact]
    public void Reset_ShouldRestartTheWalk()
    {
        FrozenSortedSet<int>.Enumerator enumerator = new FrozenSortedSet<int>(Source).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        Assert.Equal(2, enumerator.Current);

        enumerator.Reset();
        Assert.Equal(0, enumerator.Current);

        var walked = new List<int>();
        while (enumerator.MoveNext())
            walked.Add(enumerator.Current);

        Assert.Equal(Sorted, walked);

        // Reset also brings an exhausted enumerator back.
        enumerator.Reset();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(-4, enumerator.Current);

        enumerator.Dispose();
    }

    [Fact]
    public void GenericInterface_ShouldEnumerateTheSameKeys()
    {
        IEnumerable<int> set = new FrozenSortedSet<int>(Source);

        Assert.Equal(Sorted, set.ToArray());

        using IEnumerator<int> enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(-4, enumerator.Current);
    }

    [Fact]
    public void NonGenericInterface_ShouldEnumerateBoxedKeys()
    {
        // Driven by hand: Cast<T> would take the generic enumerator and never reach the explicit
        // IEnumerable.GetEnumerator or the boxed IEnumerator.Current.
        IEnumerable set = new FrozenSortedSet<int>(Source);

        IEnumerator enumerator = set.GetEnumerator();
        var walked = new List<object>();
        while (enumerator.MoveNext())
            walked.Add(enumerator.Current);

        Assert.Equal(Sorted.Cast<object>(), walked);

        enumerator.Reset();
        Assert.Equal(0, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(-4, enumerator.Current);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldMaxValue_WhenItIsARealKey()
    {
        // T.MaxValue is also the padding, so the walk must be bounded by Count, not by the value it reads.
        var set = new FrozenSortedSet<byte>([byte.MaxValue, 0, 1]);

        var walked = new List<byte>();
        foreach (byte key in set)
            walked.Add(key);

        Assert.Equal(new byte[] { 0, 1, byte.MaxValue }, walked);
    }

    [Fact]
    public void Enumerators_ShouldBeIndependent()
    {
        var set = new FrozenSortedSet<long>([5, 1, 3]);

        FrozenSortedSet<long>.Enumerator first = set.GetEnumerator();
        FrozenSortedSet<long>.Enumerator second = set.GetEnumerator();

        Assert.True(first.MoveNext());
        Assert.True(first.MoveNext());
        Assert.True(second.MoveNext());

        Assert.Equal(3L, first.Current);
        Assert.Equal(1L, second.Current);
    }
}
