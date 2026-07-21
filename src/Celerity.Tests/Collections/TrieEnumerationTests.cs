using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration coverage for <see cref="Trie{TValue}"/>: ascending ordinal key order over the entries,
/// <see cref="Trie{TValue}.Keys"/> / <see cref="Trie{TValue}.Values"/> alignment, the
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/> surface, the non-generic
/// <see cref="IEnumerable.GetEnumerator"/> path, empty-trie enumeration, and detection of a structural
/// modification made mid-enumeration.
/// </summary>
public class TrieEnumerationTests
{
    private static Trie<int> Build(params string[] keys)
    {
        var trie = new Trie<int>();
        for (int i = 0; i < keys.Length; i++)
            trie.Add(keys[i], i);
        return trie;
    }

    [Fact]
    public void Enumeration_YieldsEveryEntry_InAscendingKeyOrder()
    {
        var trie = Build("banana", "apple", "app", "apricot", "cherry");

        List<string> keys = new();
        foreach (KeyValuePair<string, int> pair in trie)
            keys.Add(pair.Key);

        Assert.Equal(new[] { "app", "apple", "apricot", "banana", "cherry" }, keys);
    }

    [Fact]
    public void KeysAndValues_AreAlignedInKeyOrder()
    {
        var trie = new Trie<string>();
        trie["b"] = "B";
        trie["a"] = "A";
        trie["c"] = "C";

        Assert.Equal(new[] { "a", "b", "c" }, trie.Keys);
        Assert.Equal(new[] { "A", "B", "C" }, trie.Values);
    }

    [Fact]
    public void EmptyTrie_EnumeratesToNothing()
    {
        var trie = new Trie<int>();
        Assert.Empty(trie);
        Assert.Empty(trie.Keys);
        Assert.Empty(trie.Values);
    }

    [Fact]
    public void IReadOnlyDictionary_SurfaceWorks()
    {
        IReadOnlyDictionary<string, int> ro = Build("one", "two", "three");

        Assert.Equal(3, ro.Count);
        Assert.True(ro.ContainsKey("two"));
        Assert.False(ro.ContainsKey("four"));
        Assert.True(ro.TryGetValue("three", out int v));
        Assert.Equal(2, v);
        Assert.Equal(0, ro["one"]);
        Assert.Equal(new[] { "one", "three", "two" }, ro.Keys.ToList());
        Assert.Equal(3, ro.Values.Count());

        var collected = ro.ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal(3, collected.Count);
    }

    [Fact]
    public void NonGenericEnumerator_YieldsSameSequence()
    {
        var trie = Build("x", "y", "z");

        var keys = new List<string>();
        IEnumerator e = ((IEnumerable)trie).GetEnumerator();
        while (e.MoveNext())
            keys.Add(((KeyValuePair<string, int>)e.Current!).Key);

        Assert.Equal(new[] { "x", "y", "z" }, keys);
    }

    [Fact]
    public void Enumerator_Throws_WhenTrieAddedToDuringEnumeration()
    {
        var trie = Build("a", "b", "c");

        IEnumerator<KeyValuePair<string, int>> e = trie.GetEnumerator();
        Assert.True(e.MoveNext());
        trie.Add("d", 4);
        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void Enumerator_Throws_WhenTrieRemovedFromDuringEnumeration()
    {
        var trie = Build("a", "b", "c");

        IEnumerator<KeyValuePair<string, int>> e = trie.GetEnumerator();
        Assert.True(e.MoveNext());
        trie.Remove("b");
        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void Enumerator_Throws_WhenTrieClearedDuringEnumeration()
    {
        var trie = Build("a", "b", "c");

        IEnumerator<KeyValuePair<string, int>> e = trie.GetEnumerator();
        Assert.True(e.MoveNext());
        trie.Clear();
        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void PrefixEnumeration_Throws_WhenModifiedMidStream()
    {
        var trie = Build("aa", "ab", "ac");

        IEnumerator<KeyValuePair<string, int>> e = trie.GetByPrefix("a").GetEnumerator();
        Assert.True(e.MoveNext());
        trie["ad"] = 9;
        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void Enumerator_Throws_WhenModifiedBeforeFirstMoveNext()
    {
        // The version is snapshotted when the enumerable/enumerator is created, so a mutation between then and
        // the first MoveNext is detected on that first MoveNext (BCL-style), not one item late.
        var trie = Build("a", "b", "c");

        IEnumerator<KeyValuePair<string, int>> e = trie.GetEnumerator();
        trie.Add("d", 4);
        Assert.Throws<InvalidOperationException>(() => e.MoveNext());

        IEnumerator<KeyValuePair<string, int>> pe = trie.GetByPrefix(string.Empty).GetEnumerator();
        trie.Remove("d");
        Assert.Throws<InvalidOperationException>(() => pe.MoveNext());
    }

    [Fact]
    public void PrefixEnumeration_FromKeyNode_Throws_WhenModifiedAfterFirstYield()
    {
        // The prefix "go" is itself a stored key, so it is the first entry the stream yields — exercising the
        // modification check that follows that start-node yield specifically.
        var trie = Build("go", "gopher", "gopher-hole");

        IEnumerator<KeyValuePair<string, int>> e = trie.GetByPrefix("go").GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.Equal("go", e.Current.Key);
        trie.Remove("gopher");
        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }
}
