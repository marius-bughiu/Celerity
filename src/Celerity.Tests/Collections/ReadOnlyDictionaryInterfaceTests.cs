using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// Milestone 1.1.0 — issue #9: IReadOnlyDictionary<TKey, TValue> implementation
// on CelerityDictionary, RobinHoodDictionary, IntDictionary, and LongDictionary.
// This test file
// exercises the dictionaries through their IReadOnlyDictionary interface surface: the
// indexer, ContainsKey, TryGetValue, Count, Keys / Values widened to
// IEnumerable<T>, and both the generic and non-generic GetEnumerator paths.
// The out-of-band default-key / zero-key entry is explicitly covered for each
// dictionary shape so that boxed enumeration stays consistent with the struct
// foreach path.
//
// Nullable annotation note: the dictionaries implement
// IReadOnlyDictionary<TKey, TValue?>, but TValue? for an unconstrained TValue
// is TValue itself at the IL level (it is Nullable<T> only when a struct
// constraint is present). Tests therefore use non-nullable value types
// (IReadOnlyDictionary<int, int>, not <int, int?>) and nullable-annotated
// reference types (IReadOnlyDictionary<int, string?>).
public class ReadOnlyDictionaryInterfaceTests
{
    // -------- CelerityDictionary --------

    [Fact]
    public void CelerityDictionary_ShouldBeAssignableToIReadOnlyDictionary()
    {
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
        map[1] = "one";

        IReadOnlyDictionary<int, string?> ro = map;

        Assert.Single(ro);
        Assert.True(ro.ContainsKey(1));
        Assert.Equal("one", ro[1]);
    }

    [Fact]
    public void CelerityDictionary_InterfaceIndexer_ShouldThrowForMissingKey()
    {
        IReadOnlyDictionary<int, string?> ro = new CelerityDictionary<int, string, Int32WangNaiveHasher>();

        Assert.Throws<KeyNotFoundException>(() => ro[42]);
    }

    [Fact]
    public void CelerityDictionary_InterfaceTryGetValue_ShouldReturnFalseForMissingKey()
    {
        IReadOnlyDictionary<int, string?> ro = new CelerityDictionary<int, string, Int32WangNaiveHasher>();

        bool found = ro.TryGetValue(42, out string? value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void CelerityDictionary_InterfaceTryGetValue_ShouldReturnTrueForPresentKey()
    {
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
        map[7] = "seven";
        IReadOnlyDictionary<int, string?> ro = map;

        bool found = ro.TryGetValue(7, out string? value);

        Assert.True(found);
        Assert.Equal("seven", value);
    }

    [Fact]
    public void CelerityDictionary_InterfaceKeys_ShouldYieldEveryKey_IncludingDefault()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100;
        for (int i = 1; i <= 5; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<int, int> ro = map;

        var keys = new List<int>();
        foreach (int key in ro.Keys)
            keys.Add(key);

        Assert.Equal(6, keys.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, keys.OrderBy(k => k).ToArray());
    }

    [Fact]
    public void CelerityDictionary_InterfaceValues_ShouldYieldEveryValue_IncludingDefault()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100;
        for (int i = 1; i <= 5; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<int, int> ro = map;

        var values = new List<int>();
        foreach (int v in ro.Values)
            values.Add(v);

        Assert.Equal(6, values.Count);
        Assert.Equal(new[] { 10, 20, 30, 40, 50, 100 }, values.OrderBy(v => v).ToArray());
    }

    [Fact]
    public void CelerityDictionary_InterfaceEnumeration_ShouldYieldEveryPair_IncludingDefault()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100;
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable<KeyValuePair<int, int>> enumerable = map;

        var pairs = new List<KeyValuePair<int, int>>();
        foreach (var kvp in enumerable)
            pairs.Add(kvp);

        Assert.Equal(4, pairs.Count);
        Assert.Contains(pairs, p => p.Key == 0 && p.Value == 100);
        Assert.Contains(pairs, p => p.Key == 1 && p.Value == 10);
        Assert.Contains(pairs, p => p.Key == 2 && p.Value == 20);
        Assert.Contains(pairs, p => p.Key == 3 && p.Value == 30);
    }

    [Fact]
    public void CelerityDictionary_NonGenericEnumeration_ShouldYieldEveryPair()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable enumerable = map;

        var pairs = new List<KeyValuePair<int, int>>();
        foreach (object kvp in enumerable)
            pairs.Add((KeyValuePair<int, int>)kvp);

        Assert.Equal(3, pairs.Count);
    }

    [Fact]
    public void CelerityDictionary_InterfaceKeys_ShouldIncludeNullReferenceKey()
    {
        var map = new CelerityDictionary<string, int, StringFnV1AHasher>();
        map[null!] = 999;
        map["a"] = 1;
        map["b"] = 2;
        IReadOnlyDictionary<string, int> ro = map;

        Assert.True(ro.ContainsKey(null!));
        Assert.Equal(999, ro[null!]);

        var keys = new List<string?>();
        foreach (string key in ro.Keys)
            keys.Add(key);

        Assert.Equal(3, keys.Count);
        Assert.Contains(null, keys);
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
    }

    [Fact]
    public void CelerityDictionary_InterfaceEnumeration_ShouldThrow_IfDictionaryMutated()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable<KeyValuePair<int, int>> enumerable = map;

        using IEnumerator<KeyValuePair<int, int>> e = enumerable.GetEnumerator();
        Assert.True(e.MoveNext());

        map[99] = 990;

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void CelerityDictionary_InterfaceLinqCount_ShouldMatchDictionaryCount()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100;
        for (int i = 1; i <= 10; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<int, int> ro = map;

        // Enumerable.Count() forces IEnumerable<KeyValuePair<,>> dispatch, not
        // the struct GetEnumerator fast path, so this is a boxing-path sanity
        // check of the explicit interface implementation.
        int enumeratedCount = ro.Count();

        Assert.Equal(ro.Count, enumeratedCount);
        Assert.Equal(11, enumeratedCount);
    }

    // -------- RobinHoodDictionary --------

    [Fact]
    public void RobinHoodDictionary_ShouldBeAssignableToIReadOnlyDictionary()
    {
        var map = new RobinHoodDictionary<int, string, Int32WangNaiveHasher>();
        map[1] = "one";

        IReadOnlyDictionary<int, string?> ro = map;

        Assert.Single(ro);
        Assert.True(ro.ContainsKey(1));
        Assert.Equal("one", ro[1]);
    }

    [Fact]
    public void RobinHoodDictionary_InterfaceIndexer_ShouldThrowForMissingKey()
    {
        IReadOnlyDictionary<int, string?> ro = new RobinHoodDictionary<int, string, Int32WangNaiveHasher>();

        Assert.Throws<KeyNotFoundException>(() => ro[42]);
    }

    [Fact]
    public void RobinHoodDictionary_InterfaceTryGetValue_ShouldReturnFalseForMissingKey()
    {
        IReadOnlyDictionary<int, string?> ro = new RobinHoodDictionary<int, string, Int32WangNaiveHasher>();

        bool found = ro.TryGetValue(42, out string? value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void RobinHoodDictionary_InterfaceTryGetValue_ShouldReturnTrueForPresentKey()
    {
        var map = new RobinHoodDictionary<int, string, Int32WangNaiveHasher>();
        map[7] = "seven";
        IReadOnlyDictionary<int, string?> ro = map;

        bool found = ro.TryGetValue(7, out string? value);

        Assert.True(found);
        Assert.Equal("seven", value);
    }

    [Fact]
    public void RobinHoodDictionary_InterfaceKeys_ShouldYieldEveryKey_IncludingDefault()
    {
        var map = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100;
        for (int i = 1; i <= 5; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<int, int> ro = map;

        var keys = new List<int>();
        foreach (int key in ro.Keys)
            keys.Add(key);

        Assert.Equal(6, keys.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, keys.OrderBy(k => k).ToArray());
    }

    [Fact]
    public void RobinHoodDictionary_InterfaceValues_ShouldYieldEveryValue_IncludingDefault()
    {
        var map = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100;
        for (int i = 1; i <= 5; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<int, int> ro = map;

        var values = new List<int>();
        foreach (int v in ro.Values)
            values.Add(v);

        Assert.Equal(6, values.Count);
        Assert.Equal(new[] { 10, 20, 30, 40, 50, 100 }, values.OrderBy(v => v).ToArray());
    }

    [Fact]
    public void RobinHoodDictionary_InterfaceEnumeration_ShouldYieldEveryPair_IncludingDefault()
    {
        var map = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100;
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable<KeyValuePair<int, int>> enumerable = map;

        var pairs = new List<KeyValuePair<int, int>>();
        foreach (var kvp in enumerable)
            pairs.Add(kvp);

        Assert.Equal(4, pairs.Count);
        Assert.Contains(pairs, p => p.Key == 0 && p.Value == 100);
        Assert.Contains(pairs, p => p.Key == 1 && p.Value == 10);
        Assert.Contains(pairs, p => p.Key == 2 && p.Value == 20);
        Assert.Contains(pairs, p => p.Key == 3 && p.Value == 30);
    }

    [Fact]
    public void RobinHoodDictionary_NonGenericEnumeration_ShouldYieldEveryPair()
    {
        var map = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable enumerable = map;

        var pairs = new List<KeyValuePair<int, int>>();
        foreach (object kvp in enumerable)
            pairs.Add((KeyValuePair<int, int>)kvp);

        Assert.Equal(3, pairs.Count);
    }

    [Fact]
    public void RobinHoodDictionary_InterfaceKeys_ShouldIncludeNullReferenceKey()
    {
        var map = new RobinHoodDictionary<string, int, StringFnV1AHasher>();
        map[null!] = 999;
        map["a"] = 1;
        map["b"] = 2;
        IReadOnlyDictionary<string, int> ro = map;

        Assert.True(ro.ContainsKey(null!));
        Assert.Equal(999, ro[null!]);

        var keys = new List<string?>();
        foreach (string key in ro.Keys)
            keys.Add(key);

        Assert.Equal(3, keys.Count);
        Assert.Contains(null, keys);
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
    }

    [Fact]
    public void RobinHoodDictionary_InterfaceEnumeration_ShouldThrow_IfDictionaryMutated()
    {
        var map = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable<KeyValuePair<int, int>> enumerable = map;

        using IEnumerator<KeyValuePair<int, int>> e = enumerable.GetEnumerator();
        Assert.True(e.MoveNext());

        map[99] = 990;

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void RobinHoodDictionary_InterfaceLinqCount_ShouldMatchDictionaryCount()
    {
        var map = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100;
        for (int i = 1; i <= 10; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<int, int> ro = map;

        // Enumerable.Count() forces IEnumerable<KeyValuePair<,>> dispatch, not
        // the struct GetEnumerator fast path, so this is a boxing-path sanity
        // check of the explicit interface implementation.
        int enumeratedCount = ro.Count();

        Assert.Equal(ro.Count, enumeratedCount);
        Assert.Equal(11, enumeratedCount);
    }

    // -------- PooledCelerityDictionary (mirror of CelerityDictionary) --------

    [Fact]
    public void PooledCelerityDictionary_ShouldBeAssignableToIReadOnlyDictionary()
    {
        using var map = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>();
        map[1] = "one";

        IReadOnlyDictionary<int, string?> ro = map;

        Assert.Single(ro);
        Assert.True(ro.ContainsKey(1));
        Assert.Equal("one", ro[1]);
    }

    [Fact]
    public void PooledCelerityDictionary_InterfaceIndexer_ShouldThrowForMissingKey()
    {
        using var map = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>();
        IReadOnlyDictionary<int, string?> ro = map;

        Assert.Throws<KeyNotFoundException>(() => ro[42]);
    }

    [Fact]
    public void PooledCelerityDictionary_InterfaceTryGetValue_ShouldReturnFalseForMissingKey()
    {
        using var map = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>();
        IReadOnlyDictionary<int, string?> ro = map;

        bool found = ro.TryGetValue(42, out string? value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void PooledCelerityDictionary_InterfaceTryGetValue_ShouldReturnTrueForPresentKey()
    {
        using var map = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>();
        map[7] = "seven";
        IReadOnlyDictionary<int, string?> ro = map;

        bool found = ro.TryGetValue(7, out string? value);

        Assert.True(found);
        Assert.Equal("seven", value);
    }

    [Fact]
    public void PooledCelerityDictionary_InterfaceKeys_ShouldYieldEveryKey_IncludingDefault()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100;
        for (int i = 1; i <= 5; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<int, int> ro = map;

        var keys = new List<int>();
        foreach (int key in ro.Keys)
            keys.Add(key);

        Assert.Equal(6, keys.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, keys.OrderBy(k => k).ToArray());
    }

    [Fact]
    public void PooledCelerityDictionary_InterfaceValues_ShouldYieldEveryValue_IncludingDefault()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100;
        for (int i = 1; i <= 5; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<int, int> ro = map;

        var values = new List<int>();
        foreach (int v in ro.Values)
            values.Add(v);

        Assert.Equal(6, values.Count);
        Assert.Equal(new[] { 10, 20, 30, 40, 50, 100 }, values.OrderBy(v => v).ToArray());
    }

    [Fact]
    public void PooledCelerityDictionary_InterfaceEnumeration_ShouldYieldEveryPair_IncludingDefault()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100;
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable<KeyValuePair<int, int>> enumerable = map;

        var pairs = new List<KeyValuePair<int, int>>();
        foreach (var kvp in enumerable)
            pairs.Add(kvp);

        Assert.Equal(4, pairs.Count);
        Assert.Contains(pairs, p => p.Key == 0 && p.Value == 100);
        Assert.Contains(pairs, p => p.Key == 1 && p.Value == 10);
        Assert.Contains(pairs, p => p.Key == 2 && p.Value == 20);
        Assert.Contains(pairs, p => p.Key == 3 && p.Value == 30);
    }

    [Fact]
    public void PooledCelerityDictionary_NonGenericEnumeration_ShouldYieldEveryPair()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable enumerable = map;

        var pairs = new List<KeyValuePair<int, int>>();
        foreach (object kvp in enumerable)
            pairs.Add((KeyValuePair<int, int>)kvp);

        Assert.Equal(3, pairs.Count);
    }

    [Fact]
    public void PooledCelerityDictionary_InterfaceKeys_ShouldIncludeNullReferenceKey()
    {
        using var map = new PooledCelerityDictionary<string, int, StringFnV1AHasher>();
        map[null!] = 999;
        map["a"] = 1;
        map["b"] = 2;
        IReadOnlyDictionary<string, int> ro = map;

        Assert.True(ro.ContainsKey(null!));
        Assert.Equal(999, ro[null!]);

        var keys = new List<string?>();
        foreach (string key in ro.Keys)
            keys.Add(key);

        Assert.Equal(3, keys.Count);
        Assert.Contains(null, keys);
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
    }

    [Fact]
    public void PooledCelerityDictionary_InterfaceEnumeration_ShouldThrow_IfDictionaryMutated()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable<KeyValuePair<int, int>> enumerable = map;

        using IEnumerator<KeyValuePair<int, int>> e = enumerable.GetEnumerator();
        Assert.True(e.MoveNext());

        map[99] = 990;

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void PooledCelerityDictionary_InterfaceLinqCount_ShouldMatchDictionaryCount()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100;
        for (int i = 1; i <= 10; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<int, int> ro = map;

        int enumeratedCount = ro.Count();

        Assert.Equal(ro.Count, enumeratedCount);
        Assert.Equal(11, enumeratedCount);
    }

    // -------- SwissDictionary --------

    [Fact]
    public void SwissDictionary_ShouldBeAssignableToIReadOnlyDictionary()
    {
        var map = new SwissDictionary<int, string, Int32WangNaiveHasher>();
        map[1] = "one";

        IReadOnlyDictionary<int, string?> ro = map;

        Assert.Single(ro);
        Assert.True(ro.ContainsKey(1));
        Assert.Equal("one", ro[1]);
    }

    [Fact]
    public void SwissDictionary_InterfaceIndexer_ShouldThrowForMissingKey()
    {
        IReadOnlyDictionary<int, string?> ro = new SwissDictionary<int, string, Int32WangNaiveHasher>();

        Assert.Throws<KeyNotFoundException>(() => ro[42]);
    }

    [Fact]
    public void SwissDictionary_InterfaceTryGetValue_ShouldReturnFalseForMissingKey()
    {
        IReadOnlyDictionary<int, string?> ro = new SwissDictionary<int, string, Int32WangNaiveHasher>();

        bool found = ro.TryGetValue(42, out string? value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void SwissDictionary_InterfaceTryGetValue_ShouldReturnTrueForPresentKey()
    {
        var map = new SwissDictionary<int, string, Int32WangNaiveHasher>();
        map[7] = "seven";
        IReadOnlyDictionary<int, string?> ro = map;

        bool found = ro.TryGetValue(7, out string? value);

        Assert.True(found);
        Assert.Equal("seven", value);
    }

    [Fact]
    public void SwissDictionary_InterfaceKeys_ShouldYieldEveryKey_IncludingDefault()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100;
        for (int i = 1; i <= 5; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<int, int> ro = map;

        var keys = new List<int>();
        foreach (int key in ro.Keys)
            keys.Add(key);

        Assert.Equal(6, keys.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, keys.OrderBy(k => k).ToArray());
    }

    [Fact]
    public void SwissDictionary_InterfaceValues_ShouldYieldEveryValue_IncludingDefault()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100;
        for (int i = 1; i <= 5; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<int, int> ro = map;

        var values = new List<int>();
        foreach (int v in ro.Values)
            values.Add(v);

        Assert.Equal(6, values.Count);
        Assert.Equal(new[] { 10, 20, 30, 40, 50, 100 }, values.OrderBy(v => v).ToArray());
    }

    [Fact]
    public void SwissDictionary_InterfaceEnumeration_ShouldYieldEveryPair_IncludingDefault()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100;
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable<KeyValuePair<int, int>> enumerable = map;

        var pairs = new List<KeyValuePair<int, int>>();
        foreach (var kvp in enumerable)
            pairs.Add(kvp);

        Assert.Equal(4, pairs.Count);
        Assert.Contains(pairs, p => p.Key == 0 && p.Value == 100);
        Assert.Contains(pairs, p => p.Key == 1 && p.Value == 10);
        Assert.Contains(pairs, p => p.Key == 2 && p.Value == 20);
        Assert.Contains(pairs, p => p.Key == 3 && p.Value == 30);
    }

    [Fact]
    public void SwissDictionary_NonGenericEnumeration_ShouldYieldEveryPair()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable enumerable = map;

        var pairs = new List<KeyValuePair<int, int>>();
        foreach (object kvp in enumerable)
            pairs.Add((KeyValuePair<int, int>)kvp);

        Assert.Equal(3, pairs.Count);
    }

    [Fact]
    public void SwissDictionary_InterfaceKeys_ShouldIncludeNullReferenceKey()
    {
        var map = new SwissDictionary<string, int, StringFnV1AHasher>();
        map[null!] = 999;
        map["a"] = 1;
        map["b"] = 2;
        IReadOnlyDictionary<string, int> ro = map;

        Assert.True(ro.ContainsKey(null!));
        Assert.Equal(999, ro[null!]);

        var keys = new List<string?>();
        foreach (string key in ro.Keys)
            keys.Add(key);

        Assert.Equal(3, keys.Count);
        Assert.Contains(null, keys);
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
    }

    [Fact]
    public void SwissDictionary_InterfaceEnumeration_ShouldThrow_IfDictionaryMutated()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable<KeyValuePair<int, int>> enumerable = map;

        using IEnumerator<KeyValuePair<int, int>> e = enumerable.GetEnumerator();
        Assert.True(e.MoveNext());

        map[99] = 990;

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void SwissDictionary_InterfaceLinqCount_ShouldMatchDictionaryCount()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100;
        for (int i = 1; i <= 10; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<int, int> ro = map;

        int enumeratedCount = ro.Count();

        Assert.Equal(ro.Count, enumeratedCount);
        Assert.Equal(11, enumeratedCount);
    }

    // -------- HashCachingDictionary --------

    [Fact]
    public void HashCachingDictionary_ShouldBeAssignableToIReadOnlyDictionary()
    {
        var map = new HashCachingDictionary<int, string, Int32WangNaiveHasher>();
        map[1] = "one";

        IReadOnlyDictionary<int, string?> ro = map;

        Assert.Single(ro);
        Assert.True(ro.ContainsKey(1));
        Assert.Equal("one", ro[1]);
    }

    [Fact]
    public void HashCachingDictionary_InterfaceIndexer_ShouldThrowForMissingKey()
    {
        IReadOnlyDictionary<int, string?> ro = new HashCachingDictionary<int, string, Int32WangNaiveHasher>();

        Assert.Throws<KeyNotFoundException>(() => ro[42]);
    }

    [Fact]
    public void HashCachingDictionary_InterfaceTryGetValue_ShouldReturnFalseForMissingKey()
    {
        IReadOnlyDictionary<int, string?> ro = new HashCachingDictionary<int, string, Int32WangNaiveHasher>();

        bool found = ro.TryGetValue(42, out string? value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void HashCachingDictionary_InterfaceTryGetValue_ShouldReturnTrueForPresentKey()
    {
        var map = new HashCachingDictionary<int, string, Int32WangNaiveHasher>();
        map[7] = "seven";
        IReadOnlyDictionary<int, string?> ro = map;

        bool found = ro.TryGetValue(7, out string? value);

        Assert.True(found);
        Assert.Equal("seven", value);
    }

    [Fact]
    public void HashCachingDictionary_InterfaceKeys_ShouldYieldEveryKey_IncludingDefault()
    {
        var map = new HashCachingDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100;
        for (int i = 1; i <= 5; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<int, int> ro = map;

        var keys = new List<int>();
        foreach (int key in ro.Keys)
            keys.Add(key);

        Assert.Equal(6, keys.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, keys.OrderBy(k => k).ToArray());
    }

    [Fact]
    public void HashCachingDictionary_InterfaceValues_ShouldYieldEveryValue_IncludingDefault()
    {
        var map = new HashCachingDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100;
        for (int i = 1; i <= 5; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<int, int> ro = map;

        var values = new List<int>();
        foreach (int v in ro.Values)
            values.Add(v);

        Assert.Equal(6, values.Count);
        Assert.Equal(new[] { 10, 20, 30, 40, 50, 100 }, values.OrderBy(v => v).ToArray());
    }

    [Fact]
    public void HashCachingDictionary_InterfaceEnumeration_ShouldYieldEveryPair_IncludingDefault()
    {
        var map = new HashCachingDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100;
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable<KeyValuePair<int, int>> enumerable = map;

        var pairs = new List<KeyValuePair<int, int>>();
        foreach (var kvp in enumerable)
            pairs.Add(kvp);

        Assert.Equal(4, pairs.Count);
        Assert.Contains(pairs, p => p.Key == 0 && p.Value == 100);
        Assert.Contains(pairs, p => p.Key == 1 && p.Value == 10);
        Assert.Contains(pairs, p => p.Key == 2 && p.Value == 20);
        Assert.Contains(pairs, p => p.Key == 3 && p.Value == 30);
    }

    [Fact]
    public void HashCachingDictionary_NonGenericEnumeration_ShouldYieldEveryPair()
    {
        var map = new HashCachingDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable enumerable = map;

        var pairs = new List<KeyValuePair<int, int>>();
        foreach (object kvp in enumerable)
            pairs.Add((KeyValuePair<int, int>)kvp);

        Assert.Equal(3, pairs.Count);
    }

    [Fact]
    public void HashCachingDictionary_InterfaceKeys_ShouldIncludeNullReferenceKey()
    {
        var map = new HashCachingDictionary<string, int, StringFnV1AHasher>();
        map[null!] = 999;
        map["a"] = 1;
        map["b"] = 2;
        IReadOnlyDictionary<string, int> ro = map;

        Assert.True(ro.ContainsKey(null!));
        Assert.Equal(999, ro[null!]);

        var keys = new List<string?>();
        foreach (string key in ro.Keys)
            keys.Add(key);

        Assert.Equal(3, keys.Count);
        Assert.Contains(null, keys);
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
    }

    [Fact]
    public void HashCachingDictionary_InterfaceEnumeration_ShouldThrow_IfDictionaryMutated()
    {
        var map = new HashCachingDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable<KeyValuePair<int, int>> enumerable = map;

        using IEnumerator<KeyValuePair<int, int>> e = enumerable.GetEnumerator();
        Assert.True(e.MoveNext());

        map[99] = 990;

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void HashCachingDictionary_InterfaceLinqCount_ShouldMatchDictionaryCount()
    {
        var map = new HashCachingDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100;
        for (int i = 1; i <= 10; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<int, int> ro = map;

        int enumeratedCount = ro.Count();

        Assert.Equal(ro.Count, enumeratedCount);
        Assert.Equal(11, enumeratedCount);
    }

    // -------- FrozenCelerityDictionary --------
    // Build-once / read-many, so the mutation-detection test does not apply (the
    // type is immutable and its enumerator needs no version check).

    private static FrozenCelerityDictionary<TValue> Frozen<TValue>(
        params KeyValuePair<string, TValue>[] pairs)
        => new FrozenCelerityDictionary<TValue>(pairs);

    [Fact]
    public void FrozenCelerityDictionary_ShouldBeAssignableToIReadOnlyDictionary()
    {
        IReadOnlyDictionary<string, string?> ro = Frozen(
            new KeyValuePair<string, string>("one", "1"));

        Assert.Single(ro);
        Assert.True(ro.ContainsKey("one"));
        Assert.Equal("1", ro["one"]);
    }

    [Fact]
    public void FrozenCelerityDictionary_InterfaceIndexer_ShouldThrowForMissingKey()
    {
        IReadOnlyDictionary<string, string?> ro = Frozen<string>();

        Assert.Throws<KeyNotFoundException>(() => ro["missing"]);
    }

    [Fact]
    public void FrozenCelerityDictionary_InterfaceTryGetValue_ShouldReturnFalseForMissingKey()
    {
        IReadOnlyDictionary<string, string?> ro = Frozen<string>();

        bool found = ro.TryGetValue("missing", out string? value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void FrozenCelerityDictionary_InterfaceTryGetValue_ShouldReturnTrueForPresentKey()
    {
        IReadOnlyDictionary<string, string?> ro = Frozen(
            new KeyValuePair<string, string>("seven", "7"));

        bool found = ro.TryGetValue("seven", out string? value);

        Assert.True(found);
        Assert.Equal("7", value);
    }

    [Fact]
    public void FrozenCelerityDictionary_InterfaceKeysAndValues_ShouldYieldEveryEntry()
    {
        IReadOnlyDictionary<string, int> ro = Frozen(
            Enumerable.Range(1, 5)
                .Select(i => new KeyValuePair<string, int>("k" + i, i * 10))
                .ToArray());

        var keys = new List<string>();
        foreach (string key in ro.Keys)
            keys.Add(key);
        var values = new List<int>();
        foreach (int v in ro.Values)
            values.Add(v);

        Assert.Equal(5, keys.Count);
        Assert.Equal(new[] { "k1", "k2", "k3", "k4", "k5" }, keys.OrderBy(k => k).ToArray());
        Assert.Equal(new[] { 10, 20, 30, 40, 50 }, values.OrderBy(v => v).ToArray());
    }

    [Fact]
    public void FrozenCelerityDictionary_InterfaceKeys_ShouldIncludeNullReferenceKey()
    {
        IReadOnlyDictionary<string, int> ro = Frozen(
            new KeyValuePair<string, int>(null!, 999),
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2));

        Assert.True(ro.ContainsKey(null!));
        Assert.Equal(999, ro[null!]);

        var keys = new List<string?>();
        foreach (string key in ro.Keys)
            keys.Add(key);

        Assert.Equal(3, keys.Count);
        Assert.Contains(null, keys);
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
    }

    [Fact]
    public void FrozenCelerityDictionary_NonGenericEnumeration_ShouldYieldEveryPair()
    {
        IEnumerable enumerable = Frozen(
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2),
            new KeyValuePair<string, int>("c", 3));

        var pairs = new List<KeyValuePair<string, int>>();
        foreach (object kvp in enumerable)
            pairs.Add((KeyValuePair<string, int>)kvp);

        Assert.Equal(3, pairs.Count);
    }

    [Fact]
    public void FrozenCelerityDictionary_InterfaceLinqCount_ShouldMatchDictionaryCount()
    {
        IReadOnlyDictionary<string, int> ro = Frozen(
            Enumerable.Range(0, 11)
                .Select(i => new KeyValuePair<string, int>("k" + i, i))
                .ToArray());

        int enumeratedCount = ro.Count();

        Assert.Equal(ro.Count, enumeratedCount);
        Assert.Equal(11, enumeratedCount);
    }

    // -------- IntDictionary --------

    [Fact]
    public void IntDictionary_ShouldBeAssignableToIReadOnlyDictionary()
    {
        var map = new IntDictionary<string>();
        map[1] = "one";

        IReadOnlyDictionary<int, string?> ro = map;

        Assert.Single(ro);
        Assert.True(ro.ContainsKey(1));
        Assert.Equal("one", ro[1]);
    }

    [Fact]
    public void IntDictionary_InterfaceIndexer_ShouldThrowForMissingKey()
    {
        IReadOnlyDictionary<int, string?> ro = new IntDictionary<string>();

        Assert.Throws<KeyNotFoundException>(() => ro[42]);
    }

    [Fact]
    public void IntDictionary_InterfaceTryGetValue_ShouldReturnFalseForMissingKey()
    {
        IReadOnlyDictionary<int, string?> ro = new IntDictionary<string>();

        bool found = ro.TryGetValue(42, out string? value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void IntDictionary_InterfaceTryGetValue_ShouldReturnTrueForPresentKey()
    {
        var map = new IntDictionary<string>();
        map[7] = "seven";
        IReadOnlyDictionary<int, string?> ro = map;

        bool found = ro.TryGetValue(7, out string? value);

        Assert.True(found);
        Assert.Equal("seven", value);
    }

    [Fact]
    public void IntDictionary_InterfaceKeys_ShouldYieldEveryKey_IncludingZero()
    {
        var map = new IntDictionary<int>();
        map[0] = 100;
        for (int i = 1; i <= 5; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<int, int> ro = map;

        var keys = new List<int>();
        foreach (int key in ro.Keys)
            keys.Add(key);

        Assert.Equal(6, keys.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, keys.OrderBy(k => k).ToArray());
    }

    [Fact]
    public void IntDictionary_InterfaceValues_ShouldYieldEveryValue_IncludingZeroKeyValue()
    {
        var map = new IntDictionary<int>();
        map[0] = 100;
        for (int i = 1; i <= 5; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<int, int> ro = map;

        var values = new List<int>();
        foreach (int v in ro.Values)
            values.Add(v);

        Assert.Equal(6, values.Count);
        Assert.Equal(new[] { 10, 20, 30, 40, 50, 100 }, values.OrderBy(v => v).ToArray());
    }

    [Fact]
    public void IntDictionary_InterfaceEnumeration_ShouldYieldEveryPair_IncludingZero()
    {
        var map = new IntDictionary<int>();
        map[0] = 100;
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable<KeyValuePair<int, int>> enumerable = map;

        var pairs = new List<KeyValuePair<int, int>>();
        foreach (var kvp in enumerable)
            pairs.Add(kvp);

        Assert.Equal(4, pairs.Count);
        Assert.Contains(pairs, p => p.Key == 0 && p.Value == 100);
        Assert.Contains(pairs, p => p.Key == 1 && p.Value == 10);
        Assert.Contains(pairs, p => p.Key == 2 && p.Value == 20);
        Assert.Contains(pairs, p => p.Key == 3 && p.Value == 30);
    }

    [Fact]
    public void IntDictionary_NonGenericEnumeration_ShouldYieldEveryPair()
    {
        var map = new IntDictionary<int>();
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable enumerable = map;

        var pairs = new List<KeyValuePair<int, int>>();
        foreach (object kvp in enumerable)
            pairs.Add((KeyValuePair<int, int>)kvp);

        Assert.Equal(3, pairs.Count);
    }

    [Fact]
    public void IntDictionary_InterfaceEnumeration_ShouldThrow_IfDictionaryMutated()
    {
        var map = new IntDictionary<int>();
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable<KeyValuePair<int, int>> enumerable = map;

        using IEnumerator<KeyValuePair<int, int>> e = enumerable.GetEnumerator();
        Assert.True(e.MoveNext());

        map[99] = 990;

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void IntDictionary_InterfaceLinqCount_ShouldMatchDictionaryCount()
    {
        var map = new IntDictionary<int>();
        map[0] = 100;
        for (int i = 1; i <= 10; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<int, int> ro = map;

        // Enumerable.Count() forces IEnumerable<KeyValuePair<,>> dispatch, not
        // the struct GetEnumerator fast path, so this is a boxing-path sanity
        // check of the explicit interface implementation.
        int enumeratedCount = ro.Count();

        Assert.Equal(ro.Count, enumeratedCount);
        Assert.Equal(11, enumeratedCount);
    }

    // -------- LongDictionary --------

    [Fact]
    public void LongDictionary_ShouldBeAssignableToIReadOnlyDictionary()
    {
        var map = new LongDictionary<string>();
        map[1L] = "one";

        IReadOnlyDictionary<long, string?> ro = map;

        Assert.Single(ro);
        Assert.True(ro.ContainsKey(1L));
        Assert.Equal("one", ro[1L]);
    }

    [Fact]
    public void LongDictionary_InterfaceIndexer_ShouldThrowForMissingKey()
    {
        IReadOnlyDictionary<long, string?> ro = new LongDictionary<string>();

        Assert.Throws<KeyNotFoundException>(() => ro[42L]);
    }

    [Fact]
    public void LongDictionary_InterfaceTryGetValue_ShouldReturnFalseForMissingKey()
    {
        IReadOnlyDictionary<long, string?> ro = new LongDictionary<string>();

        bool found = ro.TryGetValue(42L, out string? value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void LongDictionary_InterfaceTryGetValue_ShouldReturnTrueForPresentKey()
    {
        var map = new LongDictionary<string>();
        map[7L] = "seven";
        IReadOnlyDictionary<long, string?> ro = map;

        bool found = ro.TryGetValue(7L, out string? value);

        Assert.True(found);
        Assert.Equal("seven", value);
    }

    [Fact]
    public void LongDictionary_InterfaceKeys_ShouldYieldEveryKey_IncludingZero()
    {
        var map = new LongDictionary<int>();
        map[0L] = 100;
        for (int i = 1; i <= 5; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<long, int> ro = map;

        var keys = new List<long>();
        foreach (long key in ro.Keys)
            keys.Add(key);

        Assert.Equal(6, keys.Count);
        Assert.Equal(new long[] { 0, 1, 2, 3, 4, 5 }, keys.OrderBy(k => k).ToArray());
    }

    [Fact]
    public void LongDictionary_InterfaceValues_ShouldYieldEveryValue_IncludingZeroKeyValue()
    {
        var map = new LongDictionary<int>();
        map[0L] = 100;
        for (int i = 1; i <= 5; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<long, int> ro = map;

        var values = new List<int>();
        foreach (int v in ro.Values)
            values.Add(v);

        Assert.Equal(6, values.Count);
        Assert.Equal(new[] { 10, 20, 30, 40, 50, 100 }, values.OrderBy(v => v).ToArray());
    }

    [Fact]
    public void LongDictionary_InterfaceEnumeration_ShouldYieldEveryPair_IncludingZero()
    {
        var map = new LongDictionary<int>();
        map[0L] = 100;
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable<KeyValuePair<long, int>> enumerable = map;

        var pairs = new List<KeyValuePair<long, int>>();
        foreach (var kvp in enumerable)
            pairs.Add(kvp);

        Assert.Equal(4, pairs.Count);
        Assert.Contains(pairs, p => p.Key == 0L && p.Value == 100);
        Assert.Contains(pairs, p => p.Key == 1L && p.Value == 10);
        Assert.Contains(pairs, p => p.Key == 2L && p.Value == 20);
        Assert.Contains(pairs, p => p.Key == 3L && p.Value == 30);
    }

    [Fact]
    public void LongDictionary_NonGenericEnumeration_ShouldYieldEveryPair()
    {
        var map = new LongDictionary<int>();
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable enumerable = map;

        var pairs = new List<KeyValuePair<long, int>>();
        foreach (object kvp in enumerable)
            pairs.Add((KeyValuePair<long, int>)kvp);

        Assert.Equal(3, pairs.Count);
    }

    [Fact]
    public void LongDictionary_InterfaceEnumeration_ShouldThrow_IfDictionaryMutated()
    {
        var map = new LongDictionary<int>();
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable<KeyValuePair<long, int>> enumerable = map;

        using IEnumerator<KeyValuePair<long, int>> e = enumerable.GetEnumerator();
        Assert.True(e.MoveNext());

        map[99L] = 990;

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void LongDictionary_InterfaceLinqCount_ShouldMatchDictionaryCount()
    {
        var map = new LongDictionary<int>();
        map[0L] = 100;
        for (int i = 1; i <= 10; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<long, int> ro = map;

        // Enumerable.Count() forces IEnumerable<KeyValuePair<,>> dispatch, not
        // the struct GetEnumerator fast path, so this is a boxing-path sanity
        // check of the explicit interface implementation.
        int enumeratedCount = ro.Count();

        Assert.Equal(ro.Count, enumeratedCount);
        Assert.Equal(11, enumeratedCount);
    }

    [Fact]
    public void LongDictionary_InterfaceKeys_ShouldYieldEveryKey_IncludingExtreme64BitKeys()
    {
        // A signed-64-bit key set the interface Keys / Values projection must
        // round-trip without 32-bit truncation on the boxed enumeration path.
        var map = new LongDictionary<long>();
        var keys = new[] { long.MinValue, -1L, 0L, int.MaxValue + 1L, long.MaxValue };
        foreach (long k in keys)
            map[k] = k;
        IReadOnlyDictionary<long, long> ro = map;

        var seen = new List<long>();
        foreach (long key in ro.Keys)
            seen.Add(key);

        Assert.Equal(keys.Length, seen.Count);
        Assert.Equal(keys.OrderBy(k => k).ToArray(), seen.OrderBy(k => k).ToArray());
    }

    // -------- SmallDictionary --------

    [Fact]
    public void SmallDictionary_ShouldBeAssignableToIReadOnlyDictionary()
    {
        var map = new SmallDictionary<int, string>();
        map[1] = "one";

        IReadOnlyDictionary<int, string?> ro = map;

        Assert.Single(ro);
        Assert.True(ro.ContainsKey(1));
        Assert.Equal("one", ro[1]);
    }

    [Fact]
    public void SmallDictionary_InterfaceIndexer_ShouldThrowForMissingKey()
    {
        IReadOnlyDictionary<int, string?> ro = new SmallDictionary<int, string>();

        Assert.Throws<KeyNotFoundException>(() => ro[42]);
    }

    [Fact]
    public void SmallDictionary_InterfaceTryGetValue_ShouldReturnFalseForMissingKey()
    {
        IReadOnlyDictionary<int, string?> ro = new SmallDictionary<int, string>();

        bool found = ro.TryGetValue(42, out string? value);

        Assert.False(found);
        Assert.Null(value);
    }

    [Fact]
    public void SmallDictionary_InterfaceTryGetValue_ShouldReturnTrueForPresentKey()
    {
        var map = new SmallDictionary<int, string>();
        map[7] = "seven";
        IReadOnlyDictionary<int, string?> ro = map;

        bool found = ro.TryGetValue(7, out string? value);

        Assert.True(found);
        Assert.Equal("seven", value);
    }

    [Fact]
    public void SmallDictionary_InterfaceKeys_ShouldYieldEveryKey_IncludingZero()
    {
        var map = new SmallDictionary<int, int>();
        map[0] = 100;
        for (int i = 1; i <= 5; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<int, int> ro = map;

        var keys = new List<int>();
        foreach (int key in ro.Keys)
            keys.Add(key);

        Assert.Equal(6, keys.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, keys.OrderBy(k => k).ToArray());
    }

    [Fact]
    public void SmallDictionary_InterfaceValues_ShouldYieldEveryValue()
    {
        var map = new SmallDictionary<int, int>();
        map[0] = 100;
        for (int i = 1; i <= 5; i++)
            map[i] = i * 10;
        IReadOnlyDictionary<int, int> ro = map;

        var values = new List<int>();
        foreach (int v in ro.Values)
            values.Add(v);

        Assert.Equal(6, values.Count);
        Assert.Equal(new[] { 10, 20, 30, 40, 50, 100 }, values.OrderBy(v => v).ToArray());
    }

    [Fact]
    public void SmallDictionary_InterfaceEnumeration_ShouldYieldEveryPair()
    {
        var map = new SmallDictionary<int, int>();
        map[0] = 100;
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable<KeyValuePair<int, int>> enumerable = map;

        var pairs = new List<KeyValuePair<int, int>>();
        foreach (var kvp in enumerable)
            pairs.Add(kvp);

        Assert.Equal(4, pairs.Count);
        Assert.Contains(pairs, p => p.Key == 0 && p.Value == 100);
        Assert.Contains(pairs, p => p.Key == 1 && p.Value == 10);
        Assert.Contains(pairs, p => p.Key == 2 && p.Value == 20);
        Assert.Contains(pairs, p => p.Key == 3 && p.Value == 30);
    }

    [Fact]
    public void SmallDictionary_NonGenericEnumeration_ShouldYieldEveryPair()
    {
        var map = new SmallDictionary<int, int>();
        for (int i = 1; i <= 3; i++)
            map[i] = i * 10;
        IEnumerable enumerable = map;

        var pairs = new List<KeyValuePair<int, int>>();
        foreach (object kvp in enumerable)
            pairs.Add((KeyValuePair<int, int>)kvp);

        Assert.Equal(3, pairs.Count);
    }

    [Fact]
    public void SmallDictionary_InterfaceLinqCount_ShouldMatchDictionaryCount()
    {
        var map = new SmallDictionary<int, int>();
        for (int i = 1; i <= 7; i++)
            map[i] = i;
        IReadOnlyDictionary<int, int> ro = map;

        Assert.Equal(7, ro.Count());
    }

    // -------- Polymorphic "accept any IReadOnlyDictionary" helper --------

    private static int SumValues(IReadOnlyDictionary<int, int> dict)
    {
        int sum = 0;
        foreach (var kvp in dict)
            sum += kvp.Value;
        return sum;
    }

    private static long SumValues(IReadOnlyDictionary<long, long> dict)
    {
        long sum = 0;
        foreach (var kvp in dict)
            sum += kvp.Value;
        return sum;
    }

    [Fact]
    public void AllDictionaries_ShouldFlowThroughGenericIReadOnlyDictionaryConsumer()
    {
        var celerity = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        var robinHood = new RobinHoodDictionary<int, int, Int32WangNaiveHasher>();
        var swiss = new SwissDictionary<int, int, Int32WangNaiveHasher>();
        var hashCaching = new HashCachingDictionary<int, int, Int32WangNaiveHasher>();
        using var pooled = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();
        var intDict = new IntDictionary<int>();
        var longDict = new LongDictionary<long>();
        var smallDict = new SmallDictionary<int, int>();
        for (int i = 0; i <= 5; i++)
        {
            celerity[i] = i * 10;
            robinHood[i] = i * 10;
            swiss[i] = i * 10;
            hashCaching[i] = i * 10;
            pooled[i] = i * 10;
            intDict[i] = i * 10;
            longDict[i] = i * 10;
            smallDict[i] = i * 10;
        }

        // 0 + 10 + 20 + 30 + 40 + 50 = 150
        Assert.Equal(150, SumValues(celerity));
        Assert.Equal(150, SumValues(robinHood));
        Assert.Equal(150, SumValues(swiss));
        Assert.Equal(150, SumValues(hashCaching));
        Assert.Equal(150, SumValues(pooled));
        Assert.Equal(150, SumValues(intDict));
        Assert.Equal(150L, SumValues(longDict));
        Assert.Equal(150, SumValues(smallDict));
    }
}
