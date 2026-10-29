using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration of <see cref="PersistentSortedMap{TKey, TValue, TComparer}"/>: the struct enumerator over the
/// whole map and over a range, the boxed generic and non-generic paths, the key / value views, and the
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/> members that hand them out.
///
/// <para>
/// The map is immutable, so there is no version check to test and no way to invalidate an enumerator — which
/// is what makes the <i>other</i> contract worth pinning. The walk is a root-to-leaf path held inline in the
/// struct, so the states a caller can observe are: before the first <c>MoveNext</c>, mid-walk, and after the
/// last — and the last must be sticky. The walk also has to climb out of a spent leaf into the next one, at
/// every level of a three-level tree, and a range walk has to start from a leaf its lower bound may already
/// have run off the end of. Unlike the hashed family the order is specified, so every assertion here is
/// ordered.
/// </para>
/// </summary>
public class PersistentSortedMapEnumerationTests
{
    // Three levels deep, so the walk climbs through a grandparent as well as a parent.
    private const int LargeCount = 5_000;

    private static PersistentSortedMap<int, string, DefaultComparer<int>> EmptyMap =>
        PersistentSortedMap<int, string, DefaultComparer<int>>.Empty;

    [Fact]
    public void EmptyMap_ShouldYieldNothing()
    {
        Assert.Empty(EmptyMap);
        Assert.Empty(EmptyMap.Keys);
        Assert.Empty(EmptyMap.Values);
        Assert.Empty(EmptyMap.EnumerateRange(int.MinValue, int.MaxValue));
        Assert.Equal(0, EmptyMap.Keys.Count);
        Assert.Equal(0, EmptyMap.Values.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(1_025)]
    [InlineData(LargeCount)]
    public void Enumeration_ShouldYieldEveryEntryInAscendingOrder(int count)
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = Build(count);

        var seen = new List<KeyValuePair<int, string?>>();
        foreach (KeyValuePair<int, string?> entry in map)
            seen.Add(entry);

        Assert.Equal(Enumerable.Range(0, count), seen.Select(entry => entry.Key));
        foreach (KeyValuePair<int, string?> entry in seen)
            Assert.Equal(entry.Key.ToString(), entry.Value);
    }

    [Fact]
    public void Current_ShouldBeDefault_BeforeTheFirstMoveNextAndAfterTheLast()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = EmptyMap.Add(1, "one");

        PersistentSortedMap<int, string, DefaultComparer<int>>.Enumerator enumerator = map.GetEnumerator();
        Assert.Equal(default, enumerator.Current);

        Assert.True(enumerator.MoveNext());
        Assert.Equal(new KeyValuePair<int, string?>(1, "one"), enumerator.Current);

        Assert.False(enumerator.MoveNext());
        Assert.Equal(default, enumerator.Current);
    }

    [Fact]
    public void MoveNext_ShouldKeepReturningFalse_OnceTheWalkIsOver()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = Build(LargeCount);

        PersistentSortedMap<int, string, DefaultComparer<int>>.Enumerator enumerator = map.GetEnumerator();
        int yielded = 0;
        while (enumerator.MoveNext())
            yielded++;

        Assert.Equal(map.Count, yielded);

        // A restart here would silently double every enumeration of the map.
        for (int i = 0; i < 5; i++)
        {
            Assert.False(enumerator.MoveNext());
            Assert.Equal(default, enumerator.Current);
        }
    }

    [Fact]
    public void Reset_ShouldRestartTheWalk()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = Build(LargeCount);

        PersistentSortedMap<int, string, DefaultComparer<int>>.Enumerator enumerator = map.GetEnumerator();
        for (int i = 0; i < 1_000; i++)
            Assert.True(enumerator.MoveNext());

        enumerator.Reset();
        Assert.Equal(default, enumerator.Current);
        Assert.Equal(Enumerable.Range(0, LargeCount), Drain(ref enumerator));

        // And again from after the end, where the finished flag has to be cleared too.
        enumerator.Reset();
        Assert.Equal(Enumerable.Range(0, LargeCount), Drain(ref enumerator));
    }

    [Fact]
    public void Dispose_ShouldBeANoOp()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>>.Enumerator enumerator = Build(4).GetEnumerator();

        enumerator.Dispose();
        enumerator.Dispose();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current.Key);
    }

    // ── Ranges ───────────────────────────────────────────────────────────────────

    [Fact]
    public void RangeEnumerator_ShouldFollowTheSameStateContract()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = Build(LargeCount);

        PersistentSortedMap<int, string, DefaultComparer<int>>.Enumerator enumerator =
            map.EnumerateRange(1_000, 1_100).GetEnumerator();
        Assert.Equal(default, enumerator.Current);

        Assert.True(enumerator.MoveNext());
        Assert.Equal(1_000, enumerator.Current.Key);

        enumerator.Reset();
        Assert.Equal(default, enumerator.Current);
        Assert.Equal(Enumerable.Range(1_000, 100), Drain(ref enumerator));

        Assert.False(enumerator.MoveNext());
        Assert.Equal(default, enumerator.Current);

        enumerator.Reset();
        Assert.Equal(Enumerable.Range(1_000, 100), Drain(ref enumerator));
        enumerator.Dispose();
    }

    [Theory]
    [InlineData(40)]
    [InlineData(1_200)]
    public void EveryRange_ShouldMatchAFilteredScan(int count)
    {
        // Even keys, so a bound can sit on a key or in a gap. A lower bound in the gap after a leaf's last key
        // seeks into a leaf with nothing left in range, and the walk must step on to the next leaf before
        // yielding; an upper bound past the last key ends the walk by running out of leaves.
        PersistentSortedMap<int, string, DefaultComparer<int>> map = EmptyMap;
        for (int i = 0; i < count; i++)
            map = map.Add(2 * i, (2 * i).ToString());

        int step = count > 100 ? 7 : 1;
        for (int from = -1; from <= 2 * count + 1; from += step)
        {
            foreach (int width in new[] { 0, 1, 3, 70, 3 * count })
            {
                int to = from + width;
                IEnumerable<int> expected = Enumerable.Range(0, count).Select(i => 2 * i).Where(k => k >= from && k < to);
                Assert.Equal(expected, map.EnumerateRange(from, to).Select(entry => entry.Key));
            }
        }
    }

    // ── Boxed and non-generic paths ──────────────────────────────────────────────

    [Fact]
    public void BoxedGenericEnumeration_ShouldYieldEveryEntryInOrder()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = Build(100);

        var seen = new List<int>();
        using (IEnumerator<KeyValuePair<int, string?>> enumerator =
            ((IEnumerable<KeyValuePair<int, string?>>)map).GetEnumerator())
        {
            while (enumerator.MoveNext())
                seen.Add(enumerator.Current.Key);
        }

        Assert.Equal(Enumerable.Range(0, 100), seen);
    }

    [Fact]
    public void NonGenericEnumeration_ShouldYieldEveryEntryInOrder()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = Build(100);

        var seen = new List<int>();
        IEnumerator enumerator = ((IEnumerable)map).GetEnumerator();
        while (enumerator.MoveNext())
            seen.Add(((KeyValuePair<int, string?>)enumerator.Current!).Key);

        Assert.Equal(Enumerable.Range(0, 100), seen);
    }

    [Fact]
    public void RangeEnumerable_ShouldYieldTheRange_ThroughAllThreeEnumeratorPaths()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = Build(200);
        PersistentSortedMap<int, string, DefaultComparer<int>>.RangeEnumerable range = map.EnumerateRange(20, 90);

        var direct = new List<int>();
        foreach (KeyValuePair<int, string?> entry in range)
            direct.Add(entry.Key);

        var boxed = new List<int>();
        using (IEnumerator<KeyValuePair<int, string?>> enumerator =
            ((IEnumerable<KeyValuePair<int, string?>>)range).GetEnumerator())
        {
            while (enumerator.MoveNext())
                boxed.Add(enumerator.Current.Key);
        }

        var nonGeneric = new List<int>();
        IEnumerator plain = ((IEnumerable)range).GetEnumerator();
        while (plain.MoveNext())
            nonGeneric.Add(((KeyValuePair<int, string?>)plain.Current!).Key);

        Assert.Equal(Enumerable.Range(20, 70), direct);
        Assert.Equal(direct, boxed);
        Assert.Equal(direct, nonGeneric);
    }

    [Fact]
    public void KeyCollection_ShouldYieldEveryKey_ThroughAllThreeEnumeratorPaths()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = Build(60);

        var direct = new List<int>();
        foreach (int key in map.Keys)
            direct.Add(key);

        var boxed = new List<int>();
        using (IEnumerator<int> enumerator = ((IEnumerable<int>)map.Keys).GetEnumerator())
        {
            while (enumerator.MoveNext())
                boxed.Add(enumerator.Current);
        }

        var nonGeneric = new List<int>();
        IEnumerator plain = ((IEnumerable)map.Keys).GetEnumerator();
        while (plain.MoveNext())
            nonGeneric.Add((int)plain.Current!);

        Assert.Equal(Enumerable.Range(0, 60), direct);
        Assert.Equal(direct, boxed);
        Assert.Equal(direct, nonGeneric);
        Assert.Equal(map.Count, map.Keys.Count);
    }

    [Fact]
    public void ValueCollection_ShouldYieldEveryValue_ThroughAllThreeEnumeratorPaths()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = Build(60);

        var direct = new List<string?>();
        foreach (string? value in map.Values)
            direct.Add(value);

        var boxed = new List<string?>();
        using (IEnumerator<string?> enumerator = ((IEnumerable<string?>)map.Values).GetEnumerator())
        {
            while (enumerator.MoveNext())
                boxed.Add(enumerator.Current);
        }

        var nonGeneric = new List<string?>();
        IEnumerator plain = ((IEnumerable)map.Values).GetEnumerator();
        while (plain.MoveNext())
            nonGeneric.Add((string?)plain.Current);

        // Values come out in the order of their keys, not their own order.
        Assert.Equal(Enumerable.Range(0, 60).Select(value => (string?)value.ToString()), direct);
        Assert.Equal(direct, boxed);
        Assert.Equal(direct, nonGeneric);
        Assert.Equal(map.Count, map.Values.Count);
    }

    [Fact]
    public void KeyAndValueEnumerators_ShouldSupportResetAndDispose()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = Build(10);

        PersistentSortedMap<int, string, DefaultComparer<int>>.KeyCollection.Enumerator keys =
            map.Keys.GetEnumerator();
        Assert.True(keys.MoveNext());
        Assert.True(keys.MoveNext());
        keys.Reset();
        Assert.True(keys.MoveNext());
        Assert.Equal(0, keys.Current);
        keys.Dispose();

        PersistentSortedMap<int, string, DefaultComparer<int>>.ValueCollection.Enumerator values =
            map.Values.GetEnumerator();
        Assert.True(values.MoveNext());
        Assert.True(values.MoveNext());
        values.Reset();
        Assert.True(values.MoveNext());
        Assert.Equal("0", values.Current);
        values.Dispose();
    }

    [Fact]
    public void EveryEnumerator_ShouldExposeCurrentThroughTheNonGenericInterface()
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = EmptyMap.Add(5, "five");

        IEnumerator entries = ((IEnumerable)map).GetEnumerator();
        Assert.True(entries.MoveNext());
        Assert.Equal(new KeyValuePair<int, string?>(5, "five"), entries.Current);

        IEnumerator range = ((IEnumerable)map.EnumerateRange(0, 10)).GetEnumerator();
        Assert.True(range.MoveNext());
        Assert.Equal(new KeyValuePair<int, string?>(5, "five"), range.Current);

        IEnumerator keys = ((IEnumerable)map.Keys).GetEnumerator();
        Assert.True(keys.MoveNext());
        Assert.Equal(5, keys.Current);

        IEnumerator values = ((IEnumerable)map.Values).GetEnumerator();
        Assert.True(values.MoveNext());
        Assert.Equal("five", values.Current);
    }

    // ── IReadOnlyDictionary ──────────────────────────────────────────────────────

    [Fact]
    public void ReadOnlyDictionaryMembers_ShouldForwardToTheMap()
    {
        IReadOnlyDictionary<int, string?> dictionary = Build(50);

        Assert.Equal(50, dictionary.Count);
        Assert.Equal("7", dictionary[7]);
        Assert.Throws<KeyNotFoundException>(() => dictionary[50]);
        Assert.True(dictionary.ContainsKey(49));
        Assert.True(dictionary.TryGetValue(3, out string? value));
        Assert.Equal("3", value);
        Assert.Equal(Enumerable.Range(0, 50), dictionary.Keys);
        Assert.Equal(Enumerable.Range(0, 50).Select(key => (string?)key.ToString()), dictionary.Values);
    }

    // By ref, so the caller's enumerator is the one left at the end of the walk.
    private static List<int> Drain(ref PersistentSortedMap<int, string, DefaultComparer<int>>.Enumerator enumerator)
    {
        var keys = new List<int>();
        while (enumerator.MoveNext())
            keys.Add(enumerator.Current.Key);

        return keys;
    }

    // Keys 0..count-1 in ascending order, valued with their decimal text.
    private static PersistentSortedMap<int, string, DefaultComparer<int>> Build(int count)
    {
        PersistentSortedMap<int, string, DefaultComparer<int>> map = EmptyMap;
        for (int key = 0; key < count; key++)
            map = map.Add(key, key.ToString());

        return map;
    }
}
