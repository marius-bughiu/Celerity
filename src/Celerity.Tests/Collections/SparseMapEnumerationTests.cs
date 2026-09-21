using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

// Issue #473: GetEnumerator on SparseMap<TValue>, and the Keys / Values views.
//
// Mirrors SparseSetEnumerationTests / EnumMapEnumerationTests for the dense-array map.
// Enumeration walks the dense prefix (a contiguous scan over exactly the present
// entries), so the growth and post-Remove / post-Clear cases assert the dense-prefix
// walk. Key 0 is enumerated as an ordinary key.
public class SparseMapEnumerationTests
{
    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenEmpty()
    {
        var map = new SparseMap<string>(100);

        var entries = new List<KeyValuePair<int, string?>>();
        foreach (KeyValuePair<int, string?> entry in map)
            entries.Add(entry);

        Assert.Empty(entries);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldSingleEntry()
    {
        var map = new SparseMap<string>(100) { [7] = "seven" };

        KeyValuePair<int, string?> only = Assert.Single(map);
        Assert.Equal(7, only.Key);
        Assert.Equal("seven", only.Value);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEveryEntryExactlyOnce()
    {
        var map = new SparseMap<int>(100);
        for (int i = 1; i <= 50; i++)
            map[i] = i * 2;

        var seen = new Dictionary<int, int>();
        foreach (KeyValuePair<int, int> entry in map)
            Assert.True(seen.TryAdd(entry.Key, entry.Value), $"Duplicate key {entry.Key} emitted.");

        Assert.Equal(50, seen.Count);
        for (int i = 1; i <= 50; i++)
            Assert.Equal(i * 2, seen[i]);
    }

    [Fact]
    public void GetEnumerator_ShouldIncludeZeroKey()
    {
        var map = new SparseMap<string>(100) { [0] = "zero", [1] = "one" };

        var keys = new List<int>();
        foreach (KeyValuePair<int, string?> entry in map)
            keys.Add(entry.Key);

        Assert.Equal(2, keys.Count);
        Assert.Contains(0, keys);
        Assert.Contains(1, keys);
    }

    [Fact]
    public void GetEnumerator_ShouldReflectRemoval()
    {
        var map = new SparseMap<int>(100);
        for (int i = 0; i < 10; i++)
            map[i] = i;

        map.Remove(3);
        map.Remove(9);

        var keys = new List<int>();
        foreach (KeyValuePair<int, int> entry in map)
            keys.Add(entry.Key);

        Assert.Equal(8, keys.Count);
        Assert.DoesNotContain(3, keys);
        Assert.DoesNotContain(9, keys);
    }

    [Fact]
    public void GetEnumerator_ShouldReflectClear()
    {
        var map = new SparseMap<int>(100);
        for (int i = 0; i < 10; i++)
            map[i] = i;

        map.Clear();

        var entries = new List<KeyValuePair<int, int>>();
        foreach (KeyValuePair<int, int> entry in map)
            entries.Add(entry);

        Assert.Empty(entries);
    }

    [Fact]
    public void GetEnumerator_ShouldSurviveGrowth()
    {
        var map = new SparseMap<int>(1000);
        for (int i = 0; i < 300; i++)
            map[i] = i * 7;

        int count = 0;
        foreach (KeyValuePair<int, int> entry in map)
        {
            Assert.Equal(entry.Key * 7, entry.Value);
            count++;
        }

        Assert.Equal(300, count);
    }

    [Fact]
    public void MoveNext_ShouldThrow_WhenMapIsMutatedMidEnumeration()
    {
        var map = new SparseMap<int>(100) { [1] = 1, [2] = 2 };

        SparseMap<int>.Enumerator enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        map[3] = 3;

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterRemoveDuringEnumeration()
    {
        var map = new SparseMap<int>(100) { [1] = 1, [2] = 2 };

        SparseMap<int>.Enumerator enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        map.Remove(2);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterClearDuringEnumeration()
    {
        var map = new SparseMap<int>(100) { [1] = 1, [2] = 2 };

        SparseMap<int>.Enumerator enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        map.Clear();

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterACapacityChange()
    {
        // EnsureCapacity / TrimExcess move the backing arrays, so they count as structural.
        var map = new SparseMap<int>(100) { [1] = 1 };

        SparseMap<int>.Enumerator grown = map.GetEnumerator();
        map.EnsureCapacity(64);
        Assert.Throws<InvalidOperationException>(() => grown.MoveNext());

        SparseMap<int>.Enumerator trimmed = map.GetEnumerator();
        map.TrimExcess();
        Assert.Throws<InvalidOperationException>(() => trimmed.MoveNext());
    }

    [Fact]
    public void Reset_ShouldThrow_WhenMapIsMutatedMidEnumeration()
    {
        var map = new SparseMap<int>(100) { [1] = 1 };

        SparseMap<int>.Enumerator enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        map[2] = 2;

        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void Enumerator_ShouldBeReusableViaReset()
    {
        var map = new SparseMap<int>(100) { [1] = 10, [2] = 20 };

        SparseMap<int>.Enumerator enumerator = map.GetEnumerator();
        var first = new List<int>();
        while (enumerator.MoveNext())
            first.Add(enumerator.Current.Key);

        enumerator.Reset();
        var second = new List<int>();
        while (enumerator.MoveNext())
            second.Add(enumerator.Current.Key);

        Assert.Equal(first, second);
        Assert.Equal(2, first.Count);

        enumerator.Dispose();
    }

    [Fact]
    public void Map_ShouldRoundTrip_ThroughTheNonGenericIEnumerable()
    {
        var map = new SparseMap<string>(100) { [1] = "a", [2] = "b" };

        var entries = new List<KeyValuePair<int, string?>>();
        IEnumerator enumerator = ((IEnumerable)map).GetEnumerator();
        while (enumerator.MoveNext())
            entries.Add((KeyValuePair<int, string?>)enumerator.Current!);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Key == 1 && e.Value == "a");
        Assert.Contains(entries, e => e.Key == 2 && e.Value == "b");
    }

    [Fact]
    public void KeysView_ShouldEnumerateThroughBothInterfaceShapes()
    {
        var map = new SparseMap<string>(100) { [4] = "d", [8] = "h" };

        var direct = new List<int>();
        foreach (int key in map.Keys)
            direct.Add(key);

        var boxed = new List<int>();
        foreach (int key in (IEnumerable<int>)map.Keys)
            boxed.Add(key);

        var nonGeneric = new List<int>();
        IEnumerator enumerator = ((IEnumerable)map.Keys).GetEnumerator();
        while (enumerator.MoveNext())
            nonGeneric.Add((int)enumerator.Current!);

        Assert.Equal(direct, boxed);
        Assert.Equal(direct, nonGeneric);
        Assert.Equal(2, direct.Count);
    }

    [Fact]
    public void ValuesView_ShouldEnumerateThroughBothInterfaceShapes()
    {
        var map = new SparseMap<string>(100) { [4] = "d", [8] = "h" };

        var direct = new List<string?>();
        foreach (string? value in map.Values)
            direct.Add(value);

        var boxed = new List<string?>();
        foreach (string? value in (IEnumerable<string?>)map.Values)
            boxed.Add(value);

        var nonGeneric = new List<string?>();
        IEnumerator enumerator = ((IEnumerable)map.Values).GetEnumerator();
        while (enumerator.MoveNext())
            nonGeneric.Add((string?)enumerator.Current);

        Assert.Equal(direct, boxed);
        Assert.Equal(direct, nonGeneric);
        Assert.Equal(2, direct.Count);
    }

    [Fact]
    public void KeysAndValuesEnumerators_ShouldSupportResetAndDispose()
    {
        var map = new SparseMap<string>(100) { [4] = "d", [8] = "h" };

        SparseMap<string>.KeyCollection.Enumerator keys = map.Keys.GetEnumerator();
        Assert.True(keys.MoveNext());
        int firstKey = keys.Current;
        keys.Reset();
        Assert.True(keys.MoveNext());
        Assert.Equal(firstKey, keys.Current);
        keys.Dispose();

        SparseMap<string>.ValueCollection.Enumerator values = map.Values.GetEnumerator();
        Assert.True(values.MoveNext());
        string? firstValue = values.Current;
        values.Reset();
        Assert.True(values.MoveNext());
        Assert.Equal(firstValue, values.Current);
        values.Dispose();
    }

    [Fact]
    public void KeysAndValuesEnumerators_ShouldExposeCurrent_ThroughTheNonGenericInterface()
    {
        var map = new SparseMap<string>(100) { [4] = "d" };

        IEnumerator keys = map.Keys.GetEnumerator();
        Assert.True(keys.MoveNext());
        Assert.Equal(4, keys.Current);

        IEnumerator values = map.Values.GetEnumerator();
        Assert.True(values.MoveNext());
        Assert.Equal("d", values.Current);
    }

    [Fact]
    public void Enumerator_ShouldExposeCurrent_ThroughTheNonGenericInterface()
    {
        var map = new SparseMap<string>(100) { [4] = "d" };

        IEnumerator enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(new KeyValuePair<int, string?>(4, "d"), enumerator.Current);
    }
}
