using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// The enumerator, key/value view and interface surfaces of
/// <see cref="RankedDictionary{TKey, TValue, TComparer}"/>: walking every bucket in key order, invalidation on
/// structural change and not on a value overwrite, the range enumerator's seek and stop, the read-only
/// <c>Keys</c> / <c>Values</c> views, and the <see cref="IDictionary{TKey, TValue}"/> /
/// <see cref="ICollection{T}"/> adapters.
/// </summary>
public class RankedDictionaryEnumerationTests
{
    private static RankedDictionary<int, int> Ascending(int count)
    {
        var map = new RankedDictionary<int, int>();
        for (int i = 0; i < count; i++)
            map.Add(i, i * 10);

        return map;
    }

    [Fact]
    public void GetEnumerator_ShouldWalkEveryBucket_InKeyOrder()
    {
        // 1,200 ascending keys sit in four buckets, so the walk has to cross three bucket boundaries.
        var map = Ascending(1200);
        var keys = new List<int>();
        var values = new List<int>();

        foreach (KeyValuePair<int, int> entry in map)
        {
            keys.Add(entry.Key);
            values.Add(entry.Value);
        }

        Assert.Equal(Enumerable.Range(0, 1200), keys);
        Assert.Equal(Enumerable.Range(0, 1200).Select(i => i * 10), values);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_AndClearCurrent_WhenItRunsOut()
    {
        var empty = new RankedDictionary<int, int>();
        var none = empty.GetEnumerator();
        Assert.False(none.MoveNext());

        var map = Ascending(2);
        var e = map.GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.True(e.MoveNext());
        Assert.False(e.MoveNext());
        Assert.Equal(default, e.Current);
        Assert.False(e.MoveNext());
        e.Dispose();
    }

    [Fact]
    public void Reset_ShouldRestartTheWalk()
    {
        var map = Ascending(3);
        var e = map.GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.True(e.MoveNext());

        e.Reset();

        Assert.True(e.MoveNext());
        Assert.Equal(0, e.Current.Key);
    }

    [Theory]
    [InlineData("Add")]
    [InlineData("IndexerInsert")]
    [InlineData("Remove")]
    [InlineData("RemoveAt")]
    [InlineData("Clear")]
    [InlineData("TrimExcess")]
    public void MoveNextAndReset_ShouldThrowInvalidOperationException_WhenTheDictionaryWasModified(string mutation)
    {
        var map = Ascending(5);
        var e = map.GetEnumerator();
        Assert.True(e.MoveNext());

        switch (mutation)
        {
            case "Add": map.Add(99, 0); break;
            case "IndexerInsert": map[98] = 0; break;
            case "Remove": Assert.True(map.Remove(3)); break;
            case "RemoveAt": map.RemoveAt(0); break;
            case "Clear": map.Clear(); break;
            default: map.TrimExcess(); break;
        }

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
        Assert.Throws<InvalidOperationException>(() => e.Reset());
    }

    [Fact]
    public void MoveNext_ShouldNotThrow_WhenTheCallLeftTheKeysUnchanged()
    {
        // A failed TryAdd, a Remove of an absent key, a value overwrite and a lookup are not mutations.
        var map = Ascending(5);
        var e = map.GetEnumerator();
        Assert.True(e.MoveNext());

        Assert.False(map.TryAdd(2, -1));
        Assert.False(map.Remove(42));
        map[1] = 11;
        map.SetValueAtIndex(2, 22);
        _ = map.IndexOfKey(3);

        var seen = new List<KeyValuePair<int, int>> { e.Current };
        while (e.MoveNext())
            seen.Add(e.Current);

        Assert.Equal(
            [new(0, 0), new(1, 11), new(2, 22), new(3, 30), new(4, 40)],
            seen);
    }

    [Fact]
    public void NonGenericEnumeration_ShouldYieldTheBoxedEntries()
    {
        var map = Ascending(3);
        var items = new List<object>();

        IEnumerator e = ((IEnumerable)map).GetEnumerator();
        while (e.MoveNext())
            items.Add(e.Current);

        Assert.Equal(
            [new KeyValuePair<int, int>(0, 0), new KeyValuePair<int, int>(1, 10), new KeyValuePair<int, int>(2, 20)],
            items.Cast<KeyValuePair<int, int>>());

        IEnumerator<KeyValuePair<int, int>> generic =
            ((IEnumerable<KeyValuePair<int, int>>)map).GetEnumerator();
        Assert.True(generic.MoveNext());
        Assert.Equal(0, generic.Current.Key);
    }

    // ---- range enumeration -------------------------------------------------------------------------

    [Fact]
    public void RangeEnumerator_ShouldStopAtTheUpperBound_ClearCurrent_AndReset()
    {
        var map = Ascending(1200);
        var e = map.EnumerateRange(254, 258).GetEnumerator();
        var seen = new List<int>();

        while (e.MoveNext())
            seen.Add(e.Current.Key);

        Assert.Equal([254, 255, 256, 257], seen);
        Assert.Equal(default, e.Current);
        Assert.False(e.MoveNext()); // stays finished

        e.Reset();
        Assert.True(e.MoveNext());
        Assert.Equal(new KeyValuePair<int, int>(254, 2540), e.Current);
        e.Dispose();
    }

    [Fact]
    public void RangeEnumerator_ShouldRunOffTheEnd_AndStayFinishedAfterAResetPastTheLastKey()
    {
        var map = Ascending(3);

        var tail = map.EnumerateRange(1, 100).GetEnumerator();
        Assert.True(tail.MoveNext());
        Assert.True(tail.MoveNext());
        Assert.False(tail.MoveNext()); // ran off the last bucket rather than hitting the bound

        var past = map.EnumerateRange(50, 100).GetEnumerator();
        Assert.False(past.MoveNext());
        past.Reset();
        Assert.False(past.MoveNext());
    }

    [Fact]
    public void RangeEnumerator_ShouldThrowInvalidOperationException_WhenTheDictionaryWasModified()
    {
        var map = Ascending(5);
        var e = map.EnumerateRange(0, 5).GetEnumerator();
        Assert.True(e.MoveNext());

        map.Add(10, 0);

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
        Assert.Throws<InvalidOperationException>(() => e.Reset());
    }

    [Fact]
    public void RangeEnumerable_ShouldWalkTheRange_ThroughEitherInterface()
    {
        var map = Ascending(10);
        IEnumerable<KeyValuePair<int, int>> generic = map.EnumerateRange(2, 5);
        IEnumerable nonGeneric = map.EnumerateRange(2, 5);

        Assert.Equal([2, 3, 4], generic.Select(e => e.Key));

        var boxed = new List<object>();
        IEnumerator e = nonGeneric.GetEnumerator();
        while (e.MoveNext())
            boxed.Add(e.Current);

        Assert.Equal(3, boxed.Count);
        Assert.Equal(new KeyValuePair<int, int>(2, 20), boxed[0]);
    }

    // ---- the Keys and Values views -----------------------------------------------------------------

    [Fact]
    public void KeyAndValueViews_ShouldEnumerateInKeyOrder_AndReportTheCount()
    {
        var map = new RankedDictionary<int, string>();
        for (int i = 1199; i >= 0; i--)
            map[i] = "v" + i;

        Assert.Equal(1200, map.Keys.Count);
        Assert.Equal(1200, map.Values.Count);
        Assert.True(map.Keys.IsReadOnly);
        Assert.True(map.Values.IsReadOnly);
        Assert.Equal(Enumerable.Range(0, 1200), map.Keys);
        Assert.Equal(Enumerable.Range(0, 1200).Select(i => "v" + i), map.Values);
    }

    [Fact]
    public void KeyAndValueViews_ShouldAnswerContains()
    {
        var map = new RankedDictionary<int, string> { [1] = "one", [2] = "two" };

        Assert.True(map.Keys.Contains(2));
        Assert.False(map.Keys.Contains(3));
        Assert.True(map.Values.Contains("one"));
        Assert.False(map.Values.Contains("three"));
    }

    [Fact]
    public void KeyAndValueViews_ShouldCopyEveryBucket_AtTheGivenOffset()
    {
        var map = Ascending(1200);

        var keys = new int[1202];
        map.Keys.CopyTo(keys, 2);
        Assert.Equal(Enumerable.Range(0, 1200), keys.Skip(2));

        var values = new int[1201];
        map.Values.CopyTo(values, 1);
        Assert.Equal(Enumerable.Range(0, 1200).Select(i => i * 10), values.Skip(1));

        var entries = new KeyValuePair<int, int>[1200];
        map.CopyTo(entries, 0);
        Assert.Equal(map, entries);
    }

    [Fact]
    public void KeyAndValueViews_ShouldRejectMutationThroughICollection()
    {
        var map = Ascending(3);
        ICollection<int> keys = map.Keys;
        ICollection<int> values = map.Values;

        Assert.Throws<NotSupportedException>(() => keys.Add(9));
        Assert.Throws<NotSupportedException>(() => keys.Clear());
        Assert.Throws<NotSupportedException>(() => keys.Remove(1));
        Assert.Throws<NotSupportedException>(() => values.Add(9));
        Assert.Throws<NotSupportedException>(() => values.Clear());
        Assert.Throws<NotSupportedException>(() => values.Remove(10));
        Assert.Equal(3, map.Count);
    }

    [Fact]
    public void KeyAndValueEnumerators_ShouldSupportResetDisposeAndTheInterfacePaths()
    {
        var map = Ascending(3);

        var keys = map.Keys.GetEnumerator();
        Assert.True(keys.MoveNext());
        Assert.Equal(0, keys.Current);
        Assert.Equal(0, ((IEnumerator)keys).Current);
        keys.Reset();
        Assert.True(keys.MoveNext());
        Assert.Equal(0, keys.Current);
        keys.Dispose();

        var values = map.Values.GetEnumerator();
        Assert.True(values.MoveNext());
        Assert.True(values.MoveNext());
        Assert.Equal(10, values.Current);
        Assert.Equal(10, ((IEnumerator)values).Current);
        values.Reset();
        Assert.True(values.MoveNext());
        Assert.Equal(0, values.Current);
        values.Dispose();

        Assert.Equal([0, 1, 2], ((IEnumerable<int>)map.Keys).ToArray());
        Assert.Equal([0, 10, 20], ((IEnumerable<int>)map.Values).ToArray());
        Assert.Equal(3, Count((IEnumerable)map.Keys));
        Assert.Equal(3, Count((IEnumerable)map.Values));
    }

    // ---- the interface adapters --------------------------------------------------------------------

    [Fact]
    public void IDictionary_ShouldExposeTheMapThroughTheInterface()
    {
        IDictionary<int, string?> map = new RankedDictionary<int, string>();

        map[2] = "two";
        map.Add(1, "one");
        map.Add(new KeyValuePair<int, string?>(3, "three"));

        Assert.False(map.IsReadOnly);
        Assert.Equal("two", map[2]);
        Assert.Equal([1, 2, 3], map.Keys);
        Assert.Equal(["one", "two", "three"], map.Values);
        Assert.Throws<KeyNotFoundException>(() => map[42]);

        IReadOnlyDictionary<int, string?> ro = (IReadOnlyDictionary<int, string?>)map;
        Assert.Equal("three", ro[3]);
        Assert.Equal([1, 2, 3], ro.Keys);
        Assert.Equal(["one", "two", "three"], ro.Values);
    }

    [Fact]
    public void ICollectionOfPairs_ShouldMatchOnTheWholePair()
    {
        ICollection<KeyValuePair<int, string?>> map = new RankedDictionary<int, string> { [1] = "one" };

        Assert.True(map.Contains(new(1, "one")));
        Assert.False(map.Contains(new(1, "uno")));  // key present, value differs
        Assert.False(map.Contains(new(2, "one")));  // key absent

        Assert.False(map.Remove(new(1, "uno")));    // a stale value must not delete the live entry
        Assert.False(map.Remove(new(2, "two")));
        Assert.Single(map);
        Assert.True(map.Remove(new(1, "one")));
        Assert.Empty(map);
    }

    private static int Count(IEnumerable source)
    {
        int n = 0;
        IEnumerator e = source.GetEnumerator();
        while (e.MoveNext())
            n++;

        return n;
    }
}
