using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration coverage for <see cref="SuccinctTrie{TValue}"/>: ascending ordinal key order over the entries,
/// <see cref="SuccinctTrie{TValue}.Keys"/> / <see cref="SuccinctTrie{TValue}.Values"/> alignment, the
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/> surface, the non-generic
/// <see cref="IEnumerable.GetEnumerator"/> path, the struct enumerator's own members, and the empty trie.
/// The enumerator's exhausted-state and <c>Reset</c> contract is pinned alongside
/// <see cref="Trie{TValue}"/>'s in <see cref="OversizedSourceAndResidualGuardTests"/>, where that family
/// check lives.
/// </summary>
public class SuccinctTrieEnumerationTests
{
    private static SuccinctTrie<int> Build(params string[] keys)
    {
        var entries = new List<KeyValuePair<string, int>>(keys.Length);
        for (int i = 0; i < keys.Length; i++)
            entries.Add(new KeyValuePair<string, int>(keys[i], i));
        return new SuccinctTrie<int>(entries);
    }

    [Fact]
    public void Enumeration_YieldsEveryEntry_InAscendingKeyOrder()
    {
        SuccinctTrie<int> trie = Build("banana", "apple", "app", "apricot", "cherry");

        List<string> keys = new();
        foreach (KeyValuePair<string, int> pair in trie)
            keys.Add(pair.Key);

        Assert.Equal(["app", "apple", "apricot", "banana", "cherry"], keys);
    }

    [Fact]
    public void Enumeration_ShouldPairEachKeyWithItsOwnValue()
    {
        SuccinctTrie<int> trie = Build("b", "ab", "a", "ba");

        foreach (KeyValuePair<string, int> pair in trie)
            Assert.Equal(trie[pair.Key], pair.Value);
    }

    [Fact]
    public void Enumeration_ShouldPlaceAShorterKeyBeforeItsExtensions()
    {
        SuccinctTrie<int> trie = Build("abc", "ab", "abcd", "a");

        Assert.Equal(["a", "ab", "abc", "abcd"], trie.Select(p => p.Key));
    }

    [Fact]
    public void Enumeration_WithTheEmptyKeyStored_ShouldYieldItFirst()
    {
        SuccinctTrie<int> trie = Build("a", string.Empty, "b");

        Assert.Equal([string.Empty, "a", "b"], trie.Select(p => p.Key));
    }

    [Fact]
    public void Enumeration_OfAnEmptyTrie_ShouldYieldNothing()
    {
        var trie = new SuccinctTrie<int>(Array.Empty<KeyValuePair<string, int>>());

        Assert.Empty(trie);
        Assert.Empty(trie.Keys);
        Assert.Empty(trie.Values);
    }

    [Fact]
    public void Enumeration_OfASingleEmptyKey_ShouldYieldTheRootEntry()
    {
        SuccinctTrie<int> trie = Build(string.Empty);

        Assert.Equal([string.Empty], trie.Keys);
        Assert.Equal([0], trie.Values);
    }

    [Fact]
    public void Enumeration_ShouldOrderByOrdinalCodeUnit_NotByCulture()
    {
        // Ordinal puts every uppercase letter before every lowercase one; a culture-aware comparison would
        // interleave them.
        SuccinctTrie<int> trie = Build("a", "B", "b", "A");

        Assert.Equal(["A", "B", "a", "b"], trie.Keys);
    }

    [Fact]
    public void KeysAndValues_ShouldBeAlignedInTheSameOrder()
    {
        SuccinctTrie<int> trie = Build("delta", "alpha", "charlie", "bravo");

        string[] keys = trie.Keys.ToArray();
        int[] values = trie.Values.ToArray();

        Assert.Equal(keys.Length, values.Length);
        for (int i = 0; i < keys.Length; i++)
            Assert.Equal(trie[keys[i]], values[i]);
    }

    [Fact]
    public void ReadOnlyDictionary_Surface_ShouldAgreeWithTheConcreteOne()
    {
        SuccinctTrie<int> trie = Build("one", "two", "three");
        IReadOnlyDictionary<string, int> view = trie;

        Assert.Equal(3, view.Count);
        Assert.True(view.ContainsKey("two"));
        Assert.False(view.ContainsKey("four"));
        Assert.True(view.TryGetValue("three", out int value));
        Assert.Equal(2, value);
        Assert.Equal(1, view["two"]);
        Assert.Equal(["one", "three", "two"], view.Keys);
        Assert.Equal(3, view.Values.Count());
    }

    [Fact]
    public void ReadOnlyDictionary_Indexer_WithMissingKey_ShouldThrowKeyNotFoundException()
    {
        IReadOnlyDictionary<string, int> view = Build("one");

        Assert.Throws<KeyNotFoundException>(() => view["two"]);
    }

    [Fact]
    public void NonGenericEnumerator_ShouldYieldTheSameEntries()
    {
        SuccinctTrie<int> trie = Build("x", "y");
        IEnumerable sequence = trie;

        List<string> keys = new();
        foreach (object? item in sequence)
            keys.Add(((KeyValuePair<string, int>)item!).Key);

        Assert.Equal(["x", "y"], keys);
    }

    [Fact]
    public void GenericEnumerator_ShouldYieldTheSameEntries()
    {
        IEnumerable<KeyValuePair<string, int>> sequence = Build("x", "y");

        Assert.Equal(["x", "y"], sequence.Select(p => p.Key));
    }

    [Fact]
    public void Enumerator_Current_ShouldBeReachableThroughTheNonGenericInterface()
    {
        SuccinctTrie<int> trie = Build("solo");
        SuccinctTrie<int>.Enumerator enumerator = trie.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        IEnumerator view = enumerator;
        Assert.Equal(new KeyValuePair<string, int>("solo", 0), view.Current);

        enumerator.Dispose();
    }

    [Fact]
    public void PrefixEnumeration_ShouldBeRepeatable()
    {
        SuccinctTrie<int> trie = Build("ax", "ay", "b");
        IEnumerable<string> keys = trie.GetKeysWithPrefix("a");

        Assert.Equal(["ax", "ay"], keys);
        Assert.Equal(["ax", "ay"], keys);
    }

    [Fact]
    public void PrefixEnumeration_FromALeaf_ShouldYieldJustThatEntry()
    {
        SuccinctTrie<int> trie = Build("alpha", "alpine");

        Assert.Equal(["alpha"], trie.GetKeysWithPrefix("alpha"));
    }

    [Fact]
    public void PrefixEnumeration_FromAnInteriorNonKeyNode_ShouldYieldOnlyItsDescendants()
    {
        SuccinctTrie<int> trie = Build("alpha", "alpine", "beta");

        Assert.Equal(["alpha", "alpine"], trie.GetKeysWithPrefix("al"));
        Assert.Equal(["alpha", "alpine"], trie.GetByPrefix("al").Select(p => p.Key));
    }

    [Fact]
    public void Enumeration_OfADeepChain_ShouldNotOverflowTheTraversalStack()
    {
        // One key of 4,000 characters is a 4,000-node chain: the stack the enumerator sizes from the longest
        // key has to hold every frame of it.
        var entries = new List<KeyValuePair<string, int>>();
        for (int i = 1; i <= 4000; i++)
            entries.Add(new KeyValuePair<string, int>(new string('a', i), i));

        var trie = new SuccinctTrie<int>(entries);

        int seen = 0;
        foreach (KeyValuePair<string, int> pair in trie)
        {
            seen++;
            Assert.Equal(pair.Key.Length, pair.Value);
        }

        Assert.Equal(4000, seen);
    }
}
