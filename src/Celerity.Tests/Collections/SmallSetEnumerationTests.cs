using System.Collections;
using System.Collections.Generic;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

// Issue #257: GetEnumerator on SmallSet<T>.
//
// Mirrors IntSetEnumerationTests / SmallDictionaryEnumerationTests for the
// flat-array set. There is no out-of-band default-element slot (SmallSet hashes
// nothing), so a 0 / null element is enumerated as an ordinary entry, and the
// "growth" case asserts the grow-and-keep-scanning path.
public class SmallSetEnumerationTests
{
    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenEmpty()
    {
        var set = new SmallSet<int>();

        var items = new List<int>();
        foreach (int item in set)
            items.Add(item);

        Assert.Empty(items);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldSingleElement()
    {
        var set = new SmallSet<int>();
        set.Add(7);

        var items = new List<int>();
        foreach (int item in set)
            items.Add(item);

        Assert.Equal(7, Assert.Single(items));
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEveryElementExactlyOnce()
    {
        var set = new SmallSet<int>();
        for (int i = 1; i <= 50; i++)
            set.Add(i);

        var seen = new HashSet<int>();
        foreach (int item in set)
            Assert.True(seen.Add(item), $"Duplicate element {item} emitted.");

        Assert.Equal(50, seen.Count);
        for (int i = 1; i <= 50; i++)
            Assert.Contains(i, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldIncludeZeroAndNullElements()
    {
        var set = new SmallSet<string?>();
        set.Add(null);
        set.Add("one");
        set.Add("answer");

        var seen = new List<string?>();
        foreach (string? item in set)
            seen.Add(item);

        Assert.Equal(3, seen.Count);
        Assert.Contains(null, seen);
        Assert.Contains("one", seen);
        Assert.Contains("answer", seen);
    }

    [Fact]
    public void GetEnumerator_ShouldReflectRemoval()
    {
        var set = new SmallSet<int>();
        set.Add(1);
        set.Add(2);
        set.Add(3);
        set.Remove(2);

        var seen = new HashSet<int>();
        foreach (int item in set)
            seen.Add(item);

        Assert.Equal(new HashSet<int> { 1, 3 }, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldReflectClear()
    {
        var set = new SmallSet<int>();
        for (int i = 0; i < 10; i++)
            set.Add(i);
        set.Clear();

        int count = 0;
        foreach (var _ in set)
            count++;

        Assert.Equal(0, count);
    }

    [Fact]
    public void GetEnumerator_ShouldSurviveGrowth()
    {
        var set = new SmallSet<int>(capacity: 4);
        for (int i = 1; i <= 200; i++)
            set.Add(i);

        var seen = new HashSet<int>();
        foreach (int item in set)
            Assert.True(seen.Add(item), $"Duplicate element {item} emitted.");

        Assert.Equal(200, seen.Count);
    }

    [Fact]
    public void MoveNext_ShouldThrow_WhenSetIsMutatedMidEnumeration()
    {
        var set = new SmallSet<int>();
        set.Add(1);
        set.Add(2);
        set.Add(3);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        set.Add(4); // structural mutation

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterRemoveDuringEnumeration()
    {
        var set = new SmallSet<int>();
        set.Add(1);
        set.Add(2);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        set.Remove(2);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterClearDuringEnumeration()
    {
        var set = new SmallSet<int>();
        for (int i = 1; i <= 4; i++)
            set.Add(i);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        set.Clear();

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Reset_ShouldThrow_WhenSetIsMutatedMidEnumeration()
    {
        var set = new SmallSet<int>();
        set.Add(1);

        var enumerator = set.GetEnumerator();
        enumerator.MoveNext();

        set.Add(2);

        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void Enumerator_ShouldBeReusableViaReset()
    {
        var set = new SmallSet<int>();
        set.Add(1);
        set.Add(2);

        var enumerator = set.GetEnumerator();
        int firstPass = 0;
        while (enumerator.MoveNext()) firstPass++;

        enumerator.Reset();
        int secondPass = 0;
        while (enumerator.MoveNext()) secondPass++;

        Assert.Equal(2, firstPass);
        Assert.Equal(firstPass, secondPass);
    }

    [Fact]
    public void Set_ShouldRoundTrip_ThroughNonGenericIEnumerable()
    {
        var set = new SmallSet<int>();
        for (int i = 1; i <= 4; i++) set.Add(i * 10);

        var (first, second) = DrainNonGenericTwice<int>(set);

        first.Sort();
        second.Sort();
        Assert.Equal(new[] { 10, 20, 30, 40 }, first);
        Assert.Equal(first, second);
    }

    // Drives the struct enumerator through its boxed non-generic
    // IEnumerable/IEnumerator surface (GetEnumerator -> object Current), then
    // Reset()s and drains again.
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
