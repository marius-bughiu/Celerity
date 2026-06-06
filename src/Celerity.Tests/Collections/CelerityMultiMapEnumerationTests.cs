using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// Milestone 1.2.0 — issue #18: enumeration surface of CelerityMultiMap.
// Exercises the struct grouping enumerator (one Grouping per distinct key,
// default-key group yielded first), the Keys view, the per-group ValueGroup
// enumerator, mid-enumeration mutation detection (including value-only mutation),
// and the boxed IEnumerable<IGrouping<,>> / non-generic paths via ILookup.
public class CelerityMultiMapEnumerationTests
{
    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenEmpty()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();

        int count = 0;
        foreach (var g in map)
            count++;

        Assert.Equal(0, count);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldOneGroupingPerKey_WithItsValues()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        map.Add(1, 10);
        map.Add(1, 11);
        map.Add(2, 20);

        var seen = new Dictionary<int, List<int>>();
        foreach (var g in map)
            seen[g.Key] = g.Values.ToArray().ToList();

        Assert.Equal(2, seen.Count);
        Assert.Equal(new[] { 10, 11 }, seen[1].ToArray());
        Assert.Equal(new[] { 20 }, seen[2].ToArray());
    }

    [Fact]
    public void GetEnumerator_ShouldYieldDefaultKeyGroupFirst()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            map.Add(i, i * 10);
        map.Add(0, 100); // zero key out-of-band

        var keysInOrder = new List<int>();
        foreach (var g in map)
            keysInOrder.Add(g.Key);

        Assert.Equal(4, keysInOrder.Count);
        Assert.Equal(0, keysInOrder[0]); // default-key group first
    }

    [Fact]
    public void Grouping_ShouldBeEnumerableOverItsValues()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 1);
        map.Add("a", 2);
        map.Add("a", 3);

        foreach (var g in map)
        {
            if (g.Key != "a") continue;
            var values = new List<int>();
            foreach (int v in g) // IGrouping<,> is itself IEnumerable<TValue>
                values.Add(v);
            Assert.Equal(new[] { 1, 2, 3 }, values.ToArray());
        }
    }

    [Fact]
    public void Keys_ShouldYieldEveryDistinctKey_IncludingDefault()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        map.Add(0, 100);
        for (int i = 1; i <= 5; i++)
        {
            map.Add(i, i);
            map.Add(i, i + 1);
        }

        var keys = new List<int>();
        foreach (int key in map.Keys)
            keys.Add(key);

        Assert.Equal(6, map.Keys.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, keys.OrderBy(k => k).ToArray());
    }

    [Fact]
    public void NullKey_ShouldBeEnumerated_AsFirstGrouping()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add(null!, 1);
        map.Add("a", 2);
        map.Add("b", 3);

        var keys = new List<string?>();
        foreach (var g in map)
            keys.Add(g.Key);

        Assert.Equal(3, keys.Count);
        Assert.Null(keys[0]); // null/default key group first
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
    }

    [Fact]
    public void Enumerator_ShouldThrow_WhenKeyAddedDuringEnumeration()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            map.Add(i, i * 10);

        var e = map.GetEnumerator();
        Assert.True(e.MoveNext());

        map.Add(99, 990);

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void Enumerator_ShouldThrow_WhenValueAddedToExistingKeyDuringEnumeration()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        map.Add(1, 10);
        map.Add(2, 20);

        var e = map.GetEnumerator();
        Assert.True(e.MoveNext());

        // Adding a value to an existing key is a structural mutation too.
        map.Add(1, 11);

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void Enumerator_ShouldThrow_WhenValueRemovedDuringEnumeration()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        map.Add(1, 10);
        map.Add(1, 11);
        map.Add(2, 20);

        var e = map.GetEnumerator();
        Assert.True(e.MoveNext());

        map.Remove(1, 10);

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void BoxedGroupingEnumeration_ShouldYieldEveryKey()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            map.Add(i, i * 10);

        IEnumerable<IGrouping<int, int>> boxed = map;

        var keys = new List<int>();
        foreach (IGrouping<int, int> g in boxed)
            keys.Add(g.Key);

        Assert.Equal(3, keys.Count);
        Assert.Equal(new[] { 1, 2, 3 }, keys.OrderBy(k => k).ToArray());
    }

    [Fact]
    public void NonGenericEnumeration_ShouldYieldEveryGrouping()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            map.Add(i, i * 10);

        IEnumerable enumerable = map;

        int count = 0;
        foreach (object g in enumerable)
        {
            Assert.IsAssignableFrom<IGrouping<int, int>>(g);
            count++;
        }

        Assert.Equal(3, count);
    }

    [Fact]
    public void ValueGroup_BoxedEnumeration_ShouldYieldValues()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 1);
        map.Add("a", 2);

        IEnumerable<int> values = map["a"];

        Assert.Equal(new[] { 1, 2 }, values.ToArray());
    }

    [Fact]
    public void Enumerator_ShouldRoundTrip_AfterReset()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        map.Add(0, 100); // default key exercises the BeforeDefaultKey state
        map.Add(1, 10);
        map.Add(2, 20);

        IEnumerator e = ((IEnumerable)map).GetEnumerator();
        var firstKeys = new List<int>();
        while (e.MoveNext()) firstKeys.Add(((IGrouping<int, int>)e.Current!).Key);
        e.Reset();
        var secondKeys = new List<int>();
        while (e.MoveNext()) secondKeys.Add(((IGrouping<int, int>)e.Current!).Key);

        firstKeys.Sort();
        secondKeys.Sort();
        Assert.Equal(new[] { 0, 1, 2 }, firstKeys);
        Assert.Equal(firstKeys, secondKeys);
    }

    [Fact]
    public void EnumeratorReset_ShouldThrow_WhenMapMutated()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        map.Add(1, 10);

        var e = map.GetEnumerator();
        e.MoveNext();
        map.Add(2, 20); // bumps the version

        Assert.Throws<InvalidOperationException>(() => e.Reset());
    }

    [Fact]
    public void ValueGroup_ShouldEnumerate_ViaPublicStructEnumerator()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        map.Add(1, 10);
        map.Add(1, 11);

        // foreach over the ValueGroup struct binds to its public GetEnumerator(),
        // distinct from the boxed IEnumerable<TValue> path tested above.
        int sum = 0;
        foreach (int? v in map[1])
            sum += v ?? 0;

        Assert.Equal(21, sum);
    }

    [Fact]
    public void ValueGroup_ShouldRoundTrip_ThroughNonGenericIEnumerable()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        map.Add(1, 10);
        map.Add(1, 11);

        var (first, second) = DrainNonGenericTwice<int>(map[1]);

        Assert.Equal(new[] { 10, 11 }, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Grouping_ShouldRoundTrip_ThroughNonGenericIEnumerable()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        map.Add(5, 50);
        map.Add(5, 51);

        foreach (var grouping in map)
        {
            var (first, second) = DrainNonGenericTwice<int>((IEnumerable)grouping);
            Assert.Equal(new[] { 50, 51 }, first);
            Assert.Equal(first, second);
        }
    }

    [Fact]
    public void Keys_ShouldRoundTrip_ThroughNonGenericIEnumerable()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        map.Add(1, 10);
        map.Add(2, 20);

        var (first, second) = DrainNonGenericTwice<int>(map.Keys);

        first.Sort();
        second.Sort();
        Assert.Equal(new[] { 1, 2 }, first);
        Assert.Equal(first, second);
    }

    // Drives a struct view through its boxed non-generic IEnumerable/IEnumerator
    // surface (GetEnumerator -> object Current), then Reset()s and drains again.
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
