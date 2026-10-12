using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

// GetEnumerator on SlotMap<TValue>, and the Keys / Values views.
//
// Mirrors SparseMapEnumerationTests. Enumeration scans the slot array in order and skips vacant
// slots, so the post-Remove and slot-reuse cases assert that every key emitted is the key the
// caller was handed for that value — including one reissued into a reused slot.
public class SlotMapEnumerationTests
{
    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenEmpty()
    {
        var map = new SlotMap<string>();

        var entries = new List<KeyValuePair<SlotKey, string?>>();
        foreach (KeyValuePair<SlotKey, string?> entry in map)
            entries.Add(entry);

        Assert.Empty(entries);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldSingleEntry()
    {
        var map = new SlotMap<string>();
        SlotKey key = map.Add("seven");

        KeyValuePair<SlotKey, string?> only = Assert.Single(map);
        Assert.Equal(key, only.Key);
        Assert.Equal("seven", only.Value);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEveryEntryExactlyOnce_UnderTheKeyItWasIssued()
    {
        var map = new SlotMap<int>();
        var issued = new Dictionary<SlotKey, int>();
        for (int i = 0; i < 50; i++)
            issued.Add(map.Add(i * 2), i * 2);

        var seen = new Dictionary<SlotKey, int>();
        foreach (KeyValuePair<SlotKey, int> entry in map)
            Assert.True(seen.TryAdd(entry.Key, entry.Value), $"Duplicate key {entry.Key} emitted.");

        Assert.Equal(issued, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldSkipVacantSlots_AndEmitAReusedSlotsNewKey()
    {
        var map = new SlotMap<int>();
        var keys = new List<SlotKey>();
        for (int i = 0; i < 6; i++)
            keys.Add(map.Add(i));

        map.Remove(keys[0]);
        map.Remove(keys[3]);
        map.Remove(keys[5]);

        var seen = map.ToDictionary(e => e.Key, e => e.Value);
        Assert.Equal(new[] { 1, 2, 4 }, seen.Values.Order());
        foreach (int i in new[] { 1, 2, 4 })
            Assert.Equal(i, seen[keys[i]]);

        // Slot 5 was vacated last, so it is reused first, under a new key.
        SlotKey reused = map.Add(50);
        seen = map.ToDictionary(e => e.Key, e => e.Value);
        Assert.Equal(50, seen[reused]);
        Assert.False(seen.ContainsKey(keys[5]));
    }

    [Fact]
    public void GetEnumerator_ShouldYieldAscendingSlotOrder()
    {
        var map = new SlotMap<int>();
        for (int i = 0; i < 5; i++)
            map.Add(i);

        Assert.Equal(new[] { "#0.1", "#1.1", "#2.1", "#3.1", "#4.1" }, map.Keys.Select(k => k.ToString()));
    }

    [Fact]
    public void GetEnumerator_ShouldReflectClear()
    {
        var map = new SlotMap<int>();
        map.Add(1);
        map.Add(2);

        map.Clear();

        Assert.Empty(map);
        Assert.Empty(map.Keys);
        Assert.Empty(map.Values);
    }

    [Fact]
    public void GetEnumerator_ShouldSurviveGrowth()
    {
        var map = new SlotMap<int>();
        for (int i = 0; i < 200; i++)
            map.Add(i);

        Assert.Equal(Enumerable.Range(0, 200), map.Values.Order());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterAddDuringEnumeration()
    {
        var map = new SlotMap<int>();
        map.Add(1);
        map.Add(2);

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        map.Add(3);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterRemoveDuringEnumeration()
    {
        var map = new SlotMap<int>();
        SlotKey key = map.Add(1);
        map.Add(2);

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        map.Remove(key);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldNotThrow_AfterAMissedRemove()
    {
        var map = new SlotMap<int>();
        SlotKey key = map.Add(1);
        map.Add(2);
        map.Remove(key);

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.False(map.Remove(key));

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterClearDuringEnumeration()
    {
        var map = new SlotMap<int>();
        map.Add(1);
        map.Add(2);

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        map.Clear();

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterAModificationPastTheEnd()
    {
        // The hand-written enumerator checks the version before its exhausted short-circuit, so a
        // modification after the walk finished is still reported — what a yield iterator cannot do.
        var map = new SlotMap<int>();
        map.Add(1);

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
        map.Add(2);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Reset_ShouldThrow_WhenMapIsMutatedMidEnumeration()
    {
        var map = new SlotMap<int>();
        map.Add(1);

        var enumerator = map.GetEnumerator();
        map.Add(2);

        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void Enumerator_ShouldBeReusableViaReset()
    {
        var map = new SlotMap<int>();
        map.Add(1);
        map.Add(2);

        var enumerator = map.GetEnumerator();
        var first = new List<int>();
        while (enumerator.MoveNext())
            first.Add(enumerator.Current.Value);

        enumerator.Reset();
        Assert.Equal(default, enumerator.Current);

        var second = new List<int>();
        while (enumerator.MoveNext())
            second.Add(enumerator.Current.Value);

        Assert.Equal(first, second);
        Assert.Equal(default, enumerator.Current);
        enumerator.Dispose();
    }

    [Fact]
    public void Map_ShouldRoundTrip_ThroughTheNonGenericIEnumerable()
    {
        var map = new SlotMap<int>();
        SlotKey key = map.Add(5);

        var items = new List<object>();
        foreach (object item in (IEnumerable)map)
            items.Add(item);

        var only = Assert.IsType<KeyValuePair<SlotKey, int>>(Assert.Single(items));
        Assert.Equal(key, only.Key);
        Assert.Equal(5, only.Value);
    }

    [Fact]
    public void KeysView_ShouldEnumerateThroughBothInterfaceShapes()
    {
        var map = new SlotMap<int>();
        SlotKey a = map.Add(1);
        SlotKey b = map.Add(2);

        var generic = ((IEnumerable<SlotKey>)map.Keys).ToList();
        var nonGeneric = new List<object>();
        foreach (object key in (IEnumerable)map.Keys)
            nonGeneric.Add(key);

        Assert.Equal(new[] { a, b }, generic);
        Assert.Equal(new object[] { a, b }, nonGeneric);
    }

    [Fact]
    public void ValuesView_ShouldEnumerateThroughBothInterfaceShapes()
    {
        var map = new SlotMap<string>();
        map.Add("a");
        map.Add("b");

        var generic = ((IEnumerable<string?>)map.Values).ToList();
        var nonGeneric = new List<object?>();
        foreach (object? value in (IEnumerable)map.Values)
            nonGeneric.Add(value);

        Assert.Equal(new[] { "a", "b" }, generic);
        Assert.Equal(new object[] { "a", "b" }, nonGeneric);
    }

    [Fact]
    public void KeysAndValuesEnumerators_ShouldSupportResetAndDispose()
    {
        var map = new SlotMap<int>();
        SlotKey key = map.Add(9);

        var keys = map.Keys.GetEnumerator();
        Assert.True(keys.MoveNext());
        Assert.Equal(key, keys.Current);
        keys.Reset();
        Assert.True(keys.MoveNext());
        Assert.False(keys.MoveNext());
        keys.Dispose();

        var values = map.Values.GetEnumerator();
        Assert.True(values.MoveNext());
        Assert.Equal(9, values.Current);
        values.Reset();
        Assert.True(values.MoveNext());
        Assert.False(values.MoveNext());
        values.Dispose();
    }

    [Fact]
    public void KeysAndValuesEnumerators_ShouldExposeCurrent_ThroughTheNonGenericInterface()
    {
        var map = new SlotMap<int>();
        SlotKey key = map.Add(9);

        IEnumerator keys = map.Keys.GetEnumerator();
        Assert.True(keys.MoveNext());
        Assert.Equal(key, keys.Current);

        IEnumerator values = map.Values.GetEnumerator();
        Assert.True(values.MoveNext());
        Assert.Equal(9, values.Current);
    }

    [Fact]
    public void Enumerator_ShouldExposeCurrent_ThroughTheNonGenericInterface()
    {
        var map = new SlotMap<int>();
        SlotKey key = map.Add(9);

        IEnumerator enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(new KeyValuePair<SlotKey, int>(key, 9), enumerator.Current);
    }
}
