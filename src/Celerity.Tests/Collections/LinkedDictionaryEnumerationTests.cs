using System.Collections;
using System.Collections.Generic;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// Keys / Values / GetEnumerator on LinkedDictionary.
//
// Mirror of SwissDictionaryEnumerationTests. The difference that matters is that
// LinkedDictionary has a defined order — insertion order, as rearranged by
// MoveToEnd / MoveToFront — so these tests assert the exact sequence where the
// Swiss mirror can only assert set equality. The default key is an ordinary
// in-table entry here, so it appears at its insertion position rather than first.
public class LinkedDictionaryEnumerationTests
{
    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenEmpty()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();

        var pairs = new List<KeyValuePair<int, int>>();
        foreach (var kvp in map)
            pairs.Add(kvp);

        Assert.Empty(pairs);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldSingleEntry()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[7] = 70;

        var pairs = new List<KeyValuePair<int, int>>();
        foreach (var kvp in map)
            pairs.Add(kvp);

        var single = Assert.Single(pairs);
        Assert.Equal(7, single.Key);
        Assert.Equal(70, single.Value);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEveryEntryExactlyOnce_InInsertionOrder()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 50; i >= 1; i--)
            map[i] = i * 10;

        var seen = new List<KeyValuePair<int, int>>();
        foreach (var kvp in map)
            seen.Add(kvp);

        Assert.Equal(Enumerable.Range(1, 50).Reverse().Select(i => new KeyValuePair<int, int>(i, i * 10)), seen);
    }

    [Fact]
    public void GetEnumerator_ShouldIncludeDefaultKeyEntry_AtItsInsertionPosition_ForIntKey()
    {
        var map = new LinkedDictionary<int, string, Int32WangNaiveHasher>();
        map[1] = "one";
        map[0] = "zero";
        map[42] = "answer";

        var seen = new List<KeyValuePair<int, string?>>();
        foreach (var kvp in map)
            seen.Add(kvp);

        Assert.Equal(new[]
        {
            new KeyValuePair<int, string?>(1, "one"),
            new KeyValuePair<int, string?>(0, "zero"),
            new KeyValuePair<int, string?>(42, "answer"),
        }, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldOnlyDefaultKey_WhenItIsTheOnlyEntry()
    {
        var map = new LinkedDictionary<int, string, Int32WangNaiveHasher>();
        map[0] = "zero";

        var pairs = new List<KeyValuePair<int, string?>>();
        foreach (var kvp in map)
            pairs.Add(kvp);

        var single = Assert.Single(pairs);
        Assert.Equal(0, single.Key);
        Assert.Equal("zero", single.Value);
    }

    [Fact]
    public void GetEnumerator_ShouldIncludeNullKeyEntry_AtItsInsertionPosition_ForStringKey()
    {
        var map = new LinkedDictionary<string, int, StringFnV1AHasher>();
        map["alpha"] = 1;
        map[null!] = 999;
        map["beta"] = 2;

        var keys = new List<string?>();
        var values = new List<int>();
        foreach (var kvp in map)
        {
            keys.Add(kvp.Key);
            values.Add(kvp.Value);
        }

        Assert.Equal(new[] { "alpha", null, "beta" }, keys);
        Assert.Equal(new[] { 1, 999, 2 }, values);
    }

    [Fact]
    public void GetEnumerator_ShouldReflectRemoval_AndKeepOrder()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;
        map[2] = 20;
        map[3] = 30;
        map.Remove(2);

        var seen = new List<int>();
        foreach (var kvp in map)
            seen.Add(kvp.Key);

        Assert.Equal(new[] { 1, 3 }, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldReflectMoves()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 5; i++)
            map[i] = i;

        map.MoveToEnd(2);
        map.MoveToFront(4);
        map.MoveToEnd(4);

        var seen = new List<int>();
        foreach (var kvp in map)
            seen.Add(kvp.Key);

        Assert.Equal(new[] { 1, 3, 5, 2, 4 }, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldReflectClear()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 0; i < 10; i++)
            map[i] = i;
        map.Clear();

        int count = 0;
        foreach (var _ in map)
            count++;

        Assert.Equal(0, count);
    }

    [Fact]
    public void GetEnumerator_ShouldSurviveResize_InOrder()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>(capacity: 16);
        for (int i = 1; i <= 200; i++)
            map[i] = -i;

        var seen = new List<int>();
        foreach (var kvp in map)
        {
            Assert.Equal(-kvp.Key, kvp.Value);
            seen.Add(kvp.Key);
        }

        Assert.Equal(Enumerable.Range(1, 200), seen);
    }

    [Fact]
    public void Current_ShouldBeDefault_BeforeFirstMoveNextAndAfterTheEnd()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[3] = 30;

        var enumerator = map.GetEnumerator();
        Assert.Equal(default, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(new KeyValuePair<int, int>(3, 30), enumerator.Current);
        Assert.False(enumerator.MoveNext());
        Assert.Equal(default, enumerator.Current);
        Assert.False(enumerator.MoveNext()); // stays exhausted
        enumerator.Dispose();
    }

    [Fact]
    public void MoveNext_ShouldThrow_WhenDictionaryIsMutatedMidEnumeration()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;
        map[2] = 20;
        map[3] = 30;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        map[4] = 40; // structural mutation

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_WhenDefaultKeyInsertedDuringEnumeration()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        map[0] = 0; // the default key is an ordinary in-table insert here

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterClearDuringEnumeration()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 4; i++)
            map[i] = i;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        map.Clear();

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterRemoveDuringEnumeration()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;
        map[2] = 20;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        map.Remove(2);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterTryRemoveLastDuringEnumeration()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;
        map[2] = 20;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(map.TryRemoveLast(out _, out _));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldNotThrow_AfterFailedMutationsDuringEnumeration()
    {
        // Misses and no-ops change nothing, so they must not invalidate the enumerator.
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;
        map[2] = 20;
        map[3] = 30;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.False(map.Remove(99));
        Assert.False(map.TryAdd(2, 0));
        Assert.False(map.MoveToEnd(99));
        Assert.False(map.MoveToFront(99));
        Assert.True(map.MoveToEnd(3));   // already last
        Assert.True(map.MoveToFront(1)); // already first
        Assert.True(map.TryGetFirst(out _, out _));
        Assert.True(map.TryGetLast(out _, out _));
        map[2] = 200;                    // value overwrite
        map.EnsureCapacity(1);           // already large enough

        var rest = new List<KeyValuePair<int, int>>();
        while (enumerator.MoveNext())
            rest.Add(enumerator.Current);

        Assert.Equal(new[] { new KeyValuePair<int, int>(2, 200), new KeyValuePair<int, int>(3, 30) }, rest);
    }

    [Fact]
    public void Reset_ShouldThrow_WhenDictionaryIsMutatedMidEnumeration()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;

        var enumerator = map.GetEnumerator();
        enumerator.MoveNext();

        map[2] = 20;

        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void Keys_ShouldYieldEveryKeyInOrder_IncludingDefaultKey()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[5] = 105;
        map[0] = 100;
        map[42] = 142;
        map[1] = 101;

        var seen = new List<int>();
        foreach (int k in map.Keys)
            seen.Add(k);

        Assert.Equal(new[] { 5, 0, 42, 1 }, seen);
    }

    [Fact]
    public void Keys_ShouldIncludeNullKey_ForStringKey()
    {
        var map = new LinkedDictionary<string, int, StringFnV1AHasher>();
        map[null!] = 0;
        map["alpha"] = 1;

        var seen = new List<string?>();
        foreach (string k in map.Keys)
            seen.Add(k);

        Assert.Equal(new[] { null, "alpha" }, seen);
    }

    [Fact]
    public void Values_ShouldYieldEveryValueInOrder_IncludingDefaultKeyValue()
    {
        var map = new LinkedDictionary<int, string, Int32WangNaiveHasher>();
        map[2] = "two";
        map[0] = "zero";
        map[1] = "one";

        var seen = new List<string?>();
        foreach (string? v in map.Values)
            seen.Add(v);

        Assert.Equal(new[] { "two", "zero", "one" }, seen);
    }

    [Fact]
    public void Keys_Count_ShouldMatchDictionaryCount()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        Assert.Equal(0, map.Keys.Count);

        map[0] = 0;
        map[1] = 1;
        Assert.Equal(2, map.Keys.Count);

        map.Remove(0);
        Assert.Equal(1, map.Keys.Count);
    }

    [Fact]
    public void Values_Count_ShouldMatchDictionaryCount()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        Assert.Equal(0, map.Values.Count);

        map[0] = 0;
        map[1] = 1;
        Assert.Equal(2, map.Values.Count);

        map.Remove(0);
        Assert.Equal(1, map.Values.Count);
    }

    [Fact]
    public void KeysAndValues_ShouldBeReadOnlyCollections()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;

        ICollection<int> keys = map.Keys;
        ICollection<int> values = map.Values;

        Assert.True(map.Keys.IsReadOnly);
        Assert.True(map.Values.IsReadOnly);
        Assert.True(keys.IsReadOnly);
        Assert.True(values.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => keys.Add(2));
        Assert.Throws<NotSupportedException>(() => keys.Clear());
        Assert.Throws<NotSupportedException>(() => keys.Remove(1));
        Assert.Throws<NotSupportedException>(() => values.Add(2));
        Assert.Throws<NotSupportedException>(() => values.Clear());
        Assert.Throws<NotSupportedException>(() => values.Remove(10));
        Assert.Single(map);
    }

    [Fact]
    public void KeysAndValues_Contains_ShouldForwardToTheDictionary()
    {
        var map = new LinkedDictionary<int, string, Int32WangNaiveHasher>();
        map[1] = "one";
        map[0] = "zero";

        Assert.True(map.Keys.Contains(1));
        Assert.True(map.Keys.Contains(0));
        Assert.False(map.Keys.Contains(2));
        Assert.True(map.Values.Contains("zero"));
        Assert.False(map.Values.Contains("two"));
        Assert.False(map.Values.Contains(null));
    }

    [Fact]
    public void KeyCollection_ShouldBeIterableThroughIEnumerable_InOrder()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;
        map[2] = 20;
        map[0] = 0;

        IEnumerable<int> keys = map.Keys;
        var seen = new List<int>();
        foreach (int k in keys)
            seen.Add(k);

        Assert.Equal(new[] { 1, 2, 0 }, seen);
    }

    [Fact]
    public void ValueCollection_ShouldBeIterableThroughIEnumerable_InOrder()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;
        map[2] = 20;
        map[0] = 99;

        IEnumerable<int> values = map.Values;
        var seen = new List<int>();
        foreach (int v in values)
            seen.Add(v);

        Assert.Equal(new[] { 10, 20, 99 }, seen);
    }

    [Fact]
    public void Enumerator_ShouldBeReusableViaReset()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;
        map[2] = 20;

        var enumerator = map.GetEnumerator();
        var firstPass = new List<int>();
        while (enumerator.MoveNext()) firstPass.Add(enumerator.Current.Key);

        enumerator.Reset();
        var secondPass = new List<int>();
        while (enumerator.MoveNext()) secondPass.Add(enumerator.Current.Key);

        Assert.Equal(new[] { 1, 2 }, firstPass);
        Assert.Equal(firstPass, secondPass);
    }

    [Fact]
    public void Enumerator_ResetMidway_ShouldRestartFromTheCurrentHead()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            map[i] = i;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        enumerator.Reset();
        Assert.Equal(default, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current.Key);
    }

    [Fact]
    public void KeyAndValueEnumerators_ShouldSupportResetAndDispose()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;
        map[2] = 20;

        var keys = map.Keys.GetEnumerator();
        Assert.True(keys.MoveNext());
        Assert.Equal(1, keys.Current);
        keys.Reset();
        Assert.True(keys.MoveNext());
        Assert.Equal(1, keys.Current);
        keys.Dispose();

        var values = map.Values.GetEnumerator();
        Assert.True(values.MoveNext());
        Assert.True(values.MoveNext());
        Assert.Equal(20, values.Current);
        Assert.False(values.MoveNext());
        values.Reset();
        Assert.True(values.MoveNext());
        Assert.Equal(10, values.Current);
        values.Dispose();
    }

    [Fact]
    public void KeyAndValueEnumerators_ShouldThrow_WhenDictionaryIsMutated()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;

        var keys = map.Keys.GetEnumerator();
        var values = map.Values.GetEnumerator();
        map.MoveToEnd(1); // single entry: already last, so this is a no-op
        Assert.True(keys.MoveNext());
        Assert.True(values.MoveNext());

        map[2] = 20;

        Assert.Throws<InvalidOperationException>(() => keys.MoveNext());
        Assert.Throws<InvalidOperationException>(() => values.MoveNext());
    }

    [Fact]
    public void KeyCollection_ShouldRoundTrip_ThroughNonGenericIEnumerable()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 4; i >= 1; i--) map[i] = i * 10;

        var (first, second) = DrainNonGenericTwice<int>(map.Keys);

        Assert.Equal(new[] { 4, 3, 2, 1 }, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void ValueCollection_ShouldRoundTrip_ThroughNonGenericIEnumerable()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 4; i >= 1; i--) map[i] = i * 10;

        var (first, second) = DrainNonGenericTwice<int>(map.Values);

        Assert.Equal(new[] { 40, 30, 20, 10 }, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Dictionary_ShouldRoundTrip_ThroughNonGenericIEnumerable()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[2] = 20;
        map[1] = 10;

        var (first, second) = DrainNonGenericTwice<KeyValuePair<int, int>>(map);

        Assert.Equal(new[] { new KeyValuePair<int, int>(2, 20), new KeyValuePair<int, int>(1, 10) }, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Dictionary_ShouldEnumerateThroughGenericIEnumerable_InOrder()
    {
        var map = new LinkedDictionary<int, int, Int32WangNaiveHasher>();
        map[3] = 30;
        map[1] = 10;
        map[2] = 20;

        IEnumerable<KeyValuePair<int, int>> pairs = map;

        Assert.Equal(new[] { 3, 1, 2 }, pairs.Select(p => p.Key).ToArray());
    }

    private static (List<T> first, List<T> second) DrainNonGenericTwice<T>(IEnumerable view)
    {
        IEnumerator e = view.GetEnumerator();
        var first = new List<T>();
        while (e.MoveNext()) first.Add((T)e.Current!);
        e.Reset();
        var second = new List<T>();
        while (e.MoveNext()) second.Add((T)e.Current!);
        return (first, second);
    }
}
