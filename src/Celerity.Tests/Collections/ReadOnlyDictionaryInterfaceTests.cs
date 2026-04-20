using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// Milestone 1.1.0 — issue #9: IReadOnlyDictionary<TKey, TValue> implementation
// on CelerityDictionary and IntDictionary. This test file exercises the
// dictionaries through their IReadOnlyDictionary interface surface: the
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

        Assert.Equal(1, ro.Count);
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

    // -------- IntDictionary --------

    [Fact]
    public void IntDictionary_ShouldBeAssignableToIReadOnlyDictionary()
    {
        var map = new IntDictionary<string>();
        map[1] = "one";

        IReadOnlyDictionary<int, string?> ro = map;

        Assert.Equal(1, ro.Count);
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

    // -------- Polymorphic "accept any IReadOnlyDictionary" helper --------

    private static int SumValues(IReadOnlyDictionary<int, int> dict)
    {
        int sum = 0;
        foreach (var kvp in dict)
            sum += kvp.Value;
        return sum;
    }

    [Fact]
    public void BothDictionaries_ShouldFlowThroughGenericIReadOnlyDictionaryConsumer()
    {
        var celerity = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        var intDict = new IntDictionary<int>();
        for (int i = 0; i <= 5; i++)
        {
            celerity[i] = i * 10;
            intDict[i] = i * 10;
        }

        // 0 + 10 + 20 + 30 + 40 + 50 = 150
        Assert.Equal(150, SumValues(celerity));
        Assert.Equal(150, SumValues(intDict));
    }
}
