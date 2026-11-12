using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Coverage for <see cref="FrozenSortedMap{TKey, TValue}"/>'s enumeration surface: the struct enumerator and both
/// boxed interface paths.
///
/// <para>
/// The map is immutable, so there is no version counter and no invalidation to test. What is left is that the
/// enumerator walks the entries in key order, stops at <see cref="FrozenSortedMap{TKey, TValue}.Count"/> rather
/// than running on into the key padding, stays stopped with
/// <see cref="FrozenSortedMap{TKey, TValue}.Enumerator.Current"/> cleared, and can be restarted.
/// </para>
/// </summary>
public class FrozenSortedMapEnumerationTests
{
    private static readonly KeyValuePair<int, string>[] Source = [new(9, "nine"), new(-4, "minus four"), new(70, "seventy")];

    private static readonly KeyValuePair<int, string>[] Sorted = [new(-4, "minus four"), new(9, "nine"), new(70, "seventy")];

    [Fact]
    public void GetEnumerator_ShouldYieldTheEntriesInAscendingKeyOrder()
    {
        var walked = new List<KeyValuePair<int, string>>();
        foreach (KeyValuePair<int, string> entry in new FrozenSortedMap<int, string>(Source))
            walked.Add(entry);

        Assert.Equal(Sorted, walked);
    }

    [Fact]
    public void GetEnumerator_ShouldStopBeforeThePadding()
    {
        // 17 keys fill one node and one key of the next; the other fifteen slots are int.MaxValue padding,
        // which has no value behind it.
        var map = new FrozenSortedMap<int, int>(Enumerable.Range(0, 17).Select(i => new KeyValuePair<int, int>(i, -i)));

        var walked = new List<KeyValuePair<int, int>>();
        foreach (KeyValuePair<int, int> entry in map)
            walked.Add(entry);

        Assert.Equal(Enumerable.Range(0, 17).Select(i => new KeyValuePair<int, int>(i, -i)), walked);
    }

    [Fact]
    public void Current_ShouldBeDefault_BeforeTheFirstMoveNext_AndAfterTheLast()
    {
        FrozenSortedMap<int, string>.Enumerator enumerator = new FrozenSortedMap<int, string>(Source).GetEnumerator();

        Assert.Equal(default, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(Sorted[0], enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
        Assert.Equal(default, enumerator.Current);

        // Exhaustion is sticky.
        Assert.False(enumerator.MoveNext());
        enumerator.Dispose();
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenTheMapIsEmpty()
    {
        FrozenSortedMap<int, string>.Enumerator enumerator = new FrozenSortedMap<int, string>([]).GetEnumerator();

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Reset_ShouldReplayTheEntries()
    {
        FrozenSortedMap<int, string>.Enumerator enumerator = new FrozenSortedMap<int, string>(Source).GetEnumerator();
        while (enumerator.MoveNext())
        {
        }

        enumerator.Reset();

        Assert.Equal(default, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(Sorted[0], enumerator.Current);
    }

    [Fact]
    public void GenericInterfaceEnumerator_ShouldYieldTheEntriesInOrder()
    {
        IEnumerable<KeyValuePair<int, string>> boxed = new FrozenSortedMap<int, string>(Source);

        Assert.Equal(Sorted, boxed.ToList());
    }

    [Fact]
    public void NonGenericInterfaceEnumerator_ShouldYieldBoxedEntries()
    {
        // Driven by hand: Cast<T> would take the generic path and leave IEnumerator.Current unexercised.
        IEnumerable boxed = new FrozenSortedMap<int, string>(Source);
        IEnumerator enumerator = boxed.GetEnumerator();

        var walked = new List<object>();
        while (enumerator.MoveNext())
            walked.Add(enumerator.Current);

        Assert.Equal(Sorted.Cast<object>(), walked);
    }
}
