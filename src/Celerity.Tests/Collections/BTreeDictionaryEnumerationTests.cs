using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration coverage for <see cref="BTreeDictionary{TKey, TValue, TComparer}"/>: the in-order struct
/// enumerator and its range counterpart, the key/value view enumerators, the non-generic
/// <see cref="IEnumerable"/> paths, <c>Reset</c>, and the version checks that make a concurrent modification
/// throw rather than silently yield a torn traversal.
/// </summary>
public class BTreeDictionaryEnumerationTests
{
    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenDictionaryIsEmpty()
    {
        var dict = new BTreeDictionary<int, int>();

        var enumerator = dict.GetEnumerator();

        Assert.False(enumerator.MoveNext());
        Assert.Equal(default, enumerator.Current);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEveryEntryInKeyOrder_AcrossMultipleLevels()
    {
        var rand = new Random(1234);
        var dict = new BTreeDictionary<int, int>();
        foreach (int key in Enumerable.Range(0, 3000).OrderBy(_ => rand.Next()))
            dict.Add(key, key * 7);

        var seen = new List<KeyValuePair<int, int>>();
        foreach (KeyValuePair<int, int> entry in dict)
            seen.Add(entry);

        Assert.Equal(3000, seen.Count);
        Assert.Equal(Enumerable.Range(0, 3000), seen.Select(e => e.Key));
        Assert.Equal(Enumerable.Range(0, 3000).Select(i => i * 7), seen.Select(e => e.Value));
    }

    [Fact]
    public void GetEnumerator_ShouldStayExhausted_AfterTheLastEntry()
    {
        var dict = new BTreeDictionary<int, int>();
        dict.Add(1, 1);

        var enumerator = dict.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
        Assert.Equal(default, enumerator.Current);
    }

    [Fact]
    public void Reset_ShouldRestartTheTraversal()
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 100; i++)
            dict.Add(i, i);

        var enumerator = dict.GetEnumerator();
        for (int i = 0; i < 40; i++)
            Assert.True(enumerator.MoveNext());

        enumerator.Reset();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current.Key);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("clear")]
    public void MoveNext_ShouldThrow_WhenDictionaryIsModifiedDuringEnumeration(string mutation)
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 100; i++)
            dict.Add(i, i);

        var enumerator = dict.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        switch (mutation)
        {
            case "add":
                dict.Add(1000, 1000);
                break;
            case "remove":
                dict.Remove(50);
                break;
            default:
                dict.Clear();
                break;
        }

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void MoveNext_ShouldNotThrow_WhenAFailedTryAddLeavesTheContentUnchanged()
    {
        // A rejected duplicate changes nothing observable, so it must not invalidate a live enumerator —
        // which is only true because insertion splits bottom-up and never restructures on a duplicate.
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 200; i++)
            dict.Add(i, i);

        var enumerator = dict.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.False(dict.TryAdd(100, -1));
        Assert.False(dict.Remove(1000));

        // An in-place value overwrite is not a structural change either (issue #233).
        dict[100] = -1;

        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current.Key);
    }

    [Fact]
    public void NonGenericEnumeration_ShouldYieldTheSameEntries()
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 50; i++)
            dict.Add(i, i);

        var seen = new List<int>();
        foreach (object? entry in (IEnumerable)dict)
            seen.Add(((KeyValuePair<int, int>)entry!).Key);

        Assert.Equal(Enumerable.Range(0, 50), seen);

        IEnumerator<KeyValuePair<int, int>> generic =
            ((IEnumerable<KeyValuePair<int, int>>)dict).GetEnumerator();
        Assert.True(generic.MoveNext());
        Assert.Equal(0, generic.Current.Key);
        generic.Dispose();
    }

    [Fact]
    public void KeyAndValueEnumerators_ShouldYieldInKeyOrder()
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 9; i >= 0; i--)
            dict.Add(i, i * 5);

        var keys = new List<int>();
        foreach (int key in dict.Keys)
            keys.Add(key);

        var values = new List<int>();
        foreach (int value in dict.Values)
            values.Add(value);

        Assert.Equal(Enumerable.Range(0, 10), keys);
        Assert.Equal(Enumerable.Range(0, 10).Select(i => i * 5), values);

        // The views also flow through the non-generic and interface-typed paths.
        Assert.Equal(keys, ((IEnumerable)dict.Keys).Cast<int>());
        Assert.Equal(values, ((IEnumerable)dict.Values).Cast<int>());
    }

    [Fact]
    public void KeyAndValueEnumerators_ShouldSupportResetAndDispose()
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 10; i++)
            dict.Add(i, i);

        var keyEnumerator = dict.Keys.GetEnumerator();
        Assert.True(keyEnumerator.MoveNext());
        Assert.True(keyEnumerator.MoveNext());
        keyEnumerator.Reset();
        Assert.True(keyEnumerator.MoveNext());
        Assert.Equal(0, keyEnumerator.Current);
        keyEnumerator.Dispose();

        var valueEnumerator = dict.Values.GetEnumerator();
        Assert.True(valueEnumerator.MoveNext());
        valueEnumerator.Reset();
        Assert.True(valueEnumerator.MoveNext());
        Assert.Equal(0, valueEnumerator.Current);
        valueEnumerator.Dispose();
    }

    [Fact]
    public void RangeEnumerator_ShouldStopAtTheUpperBound_AndSupportReset()
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 500; i++)
            dict.Add(i, i);

        var range = dict.EnumerateRange(100, 105);
        var enumerator = range.GetEnumerator();

        var seen = new List<int>();
        while (enumerator.MoveNext())
            seen.Add(enumerator.Current.Key);

        Assert.Equal(new[] { 100, 101, 102, 103, 104 }, seen);
        Assert.False(enumerator.MoveNext());

        enumerator.Reset();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(100, enumerator.Current.Key);
        enumerator.Dispose();
    }

    [Fact]
    public void RangeEnumerator_ShouldThrow_WhenDictionaryIsModifiedDuringEnumeration()
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 200; i++)
            dict.Add(i, i);

        var enumerator = dict.EnumerateRange(10, 100).GetEnumerator();
        Assert.True(enumerator.MoveNext());

        dict.Add(500, 500);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void RangeEnumerable_ShouldFlowThroughTheInterfacePaths()
    {
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 100; i++)
            dict.Add(i, i);

        IEnumerable<KeyValuePair<int, int>> generic = dict.EnumerateRange(10, 15);
        Assert.Equal(new[] { 10, 11, 12, 13, 14 }, generic.Select(e => e.Key));

        var seen = new List<int>();
        foreach (object? entry in (IEnumerable)dict.EnumerateRange(10, 15))
            seen.Add(((KeyValuePair<int, int>)entry!).Key);

        Assert.Equal(new[] { 10, 11, 12, 13, 14 }, seen);
    }

    [Fact]
    public void RangeEnumeration_ShouldSeekCorrectly_WhenBoundsFallInsideInternalNodes()
    {
        // With 3000 entries the tree is three levels deep, so a range that starts and ends in the middle
        // exercises the seek's "descend past the matched key" path at every level.
        var dict = new BTreeDictionary<int, int>();
        for (int i = 0; i < 3000; i++)
            dict.Add(i, i);

        for (int from = 0; from < 3000; from += 97)
        {
            int to = Math.Min(from + 250, 3000);
            Assert.Equal(Enumerable.Range(from, to - from), dict.EnumerateRange(from, to).Select(e => e.Key));
        }
    }
}
