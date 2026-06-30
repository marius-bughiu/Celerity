using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// Issue #235: enumeration surface of CelerityMultiSet. Exercises the struct
// (element, count) enumerator (default element yielded first), the Elements view,
// mid-enumeration mutation detection (including count-only mutation), Reset, and
// the boxed IEnumerable<KeyValuePair<,>> / non-generic paths.
public class CelerityMultiSetEnumerationTests
{
    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenEmpty()
    {
        var set = new CelerityMultiSet<int, Int32WangNaiveHasher>();

        int count = 0;
        foreach (var _ in set)
            count++;

        Assert.Equal(0, count);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEachElementWithItsCount()
    {
        var set = new CelerityMultiSet<int, Int32WangNaiveHasher>();
        set.Add(1, 2);
        set.Add(2, 5);

        var seen = new Dictionary<int, int>();
        foreach (KeyValuePair<int, int> pair in set)
            seen[pair.Key] = pair.Value;

        Assert.Equal(2, seen.Count);
        Assert.Equal(2, seen[1]);
        Assert.Equal(5, seen[2]);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldDefaultElementFirst()
    {
        var set = new CelerityMultiSet<int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            set.Add(i, i);
        set.Add(0, 9); // zero element out-of-band

        var elementsInOrder = new List<int>();
        foreach (KeyValuePair<int, int> pair in set)
            elementsInOrder.Add(pair.Key);

        Assert.Equal(4, elementsInOrder.Count);
        Assert.Equal(0, elementsInOrder[0]); // default element first
    }

    [Fact]
    public void Elements_ShouldYieldEveryDistinctElement_IncludingDefault()
    {
        var set = new CelerityMultiSet<int, Int32WangNaiveHasher>();
        set.Add(0, 1);
        for (int i = 1; i <= 5; i++)
            set.Add(i, i);

        var elements = new List<int>();
        foreach (int e in set.Elements)
            elements.Add(e);

        Assert.Equal(6, set.Elements.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, elements.OrderBy(e => e).ToArray());
    }

    [Fact]
    public void NullElement_ShouldBeEnumerated_First()
    {
        var set = new CelerityMultiSet<string, StringFnV1AHasher>();
        set.Add(null!, 1);
        set.Add("a");
        set.Add("b");

        var elements = new List<string?>();
        foreach (KeyValuePair<string, int> pair in set)
            elements.Add(pair.Key);

        Assert.Equal(3, elements.Count);
        Assert.Null(elements[0]); // null/default element first
        Assert.Contains("a", elements);
        Assert.Contains("b", elements);
    }

    [Fact]
    public void Enumerator_ShouldThrow_WhenElementAddedDuringEnumeration()
    {
        var set = new CelerityMultiSet<int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            set.Add(i);

        var e = set.GetEnumerator();
        Assert.True(e.MoveNext());

        set.Add(99);

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void Enumerator_ShouldThrow_WhenCountOfExistingElementChangedDuringEnumeration()
    {
        var set = new CelerityMultiSet<int, Int32WangNaiveHasher>();
        set.Add(1);
        set.Add(2);

        var e = set.GetEnumerator();
        Assert.True(e.MoveNext());

        // Incrementing an existing element's count is a structural mutation too.
        set.Add(1);

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void Enumerator_ShouldThrow_WhenElementRemovedDuringEnumeration()
    {
        var set = new CelerityMultiSet<int, Int32WangNaiveHasher>();
        set.Add(1, 2);
        set.Add(2, 1);

        var e = set.GetEnumerator();
        Assert.True(e.MoveNext());

        set.Remove(1);

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void BoxedEnumeration_ShouldYieldEveryElement()
    {
        var set = new CelerityMultiSet<int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            set.Add(i, i);

        IEnumerable<KeyValuePair<int, int>> boxed = set;

        var elements = new List<int>();
        foreach (KeyValuePair<int, int> pair in boxed)
            elements.Add(pair.Key);

        Assert.Equal(3, elements.Count);
        Assert.Equal(new[] { 1, 2, 3 }, elements.OrderBy(e => e).ToArray());
    }

    [Fact]
    public void NonGenericEnumeration_ShouldYieldEveryPair()
    {
        var set = new CelerityMultiSet<int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            set.Add(i, i);

        IEnumerable enumerable = set;

        int count = 0;
        foreach (object pair in enumerable)
        {
            Assert.IsType<KeyValuePair<int, int>>(pair);
            count++;
        }

        Assert.Equal(3, count);
    }

    [Fact]
    public void Enumerator_ShouldRoundTrip_AfterReset()
    {
        var set = new CelerityMultiSet<int, Int32WangNaiveHasher>();
        set.Add(0, 100); // default element exercises the BeforeDefaultKey state
        set.Add(1, 10);
        set.Add(2, 20);

        IEnumerator e = ((IEnumerable)set).GetEnumerator();
        var firstElements = new List<int>();
        while (e.MoveNext()) firstElements.Add(((KeyValuePair<int, int>)e.Current!).Key);
        e.Reset();
        var secondElements = new List<int>();
        while (e.MoveNext()) secondElements.Add(((KeyValuePair<int, int>)e.Current!).Key);

        firstElements.Sort();
        secondElements.Sort();
        Assert.Equal(new[] { 0, 1, 2 }, firstElements);
        Assert.Equal(firstElements, secondElements);
    }

    [Fact]
    public void EnumeratorReset_ShouldThrow_WhenSetMutated()
    {
        var set = new CelerityMultiSet<int, Int32WangNaiveHasher>();
        set.Add(1);

        var e = set.GetEnumerator();
        e.MoveNext();
        set.Add(2); // bumps the version

        Assert.Throws<InvalidOperationException>(() => e.Reset());
    }

    [Fact]
    public void Elements_ShouldRoundTrip_ThroughNonGenericIEnumerable()
    {
        var set = new CelerityMultiSet<int, Int32WangNaiveHasher>();
        set.Add(1, 3);
        set.Add(2, 1);

        var (first, second) = DrainNonGenericTwice<int>(set.Elements);

        first.Sort();
        second.Sort();
        Assert.Equal(new[] { 1, 2 }, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Elements_ShouldEnumerate_ViaGenericBoxedEnumerator()
    {
        var set = new CelerityMultiSet<int, Int32WangNaiveHasher>();
        set.Add(1, 3);
        set.Add(2, 1);

        IEnumerable<int> boxed = set.Elements;
        var elements = boxed.ToList();
        elements.Sort();

        Assert.Equal(new[] { 1, 2 }, elements);
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
