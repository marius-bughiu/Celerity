using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Core behavioural coverage for <see cref="Trie{TValue}"/>: <see cref="Trie{TValue}.Add"/> /
/// <see cref="Trie{TValue}.TryAdd"/>, the indexer get/set, <see cref="Trie{TValue}.ContainsKey"/> /
/// <see cref="Trie{TValue}.TryGetValue"/>, <see cref="Trie{TValue}.Remove(string)"/> (including bottom-up
/// node pruning and shared-prefix retention), <see cref="Trie{TValue}.Clear"/>, the empty-string key, the
/// constructors, and the argument / not-found exception contracts.
/// </summary>
public class TrieTests
{
    [Fact]
    public void NewTrie_IsEmpty()
    {
        var trie = new Trie<int>();
        Assert.Equal(0, trie.Count);
        Assert.False(trie.ContainsKey("anything"));
        Assert.False(trie.TryGetValue("anything", out _));
    }

    [Fact]
    public void Add_ThenTryGetValue_ReturnsStoredValue()
    {
        var trie = new Trie<int>();
        trie.Add("apple", 1);
        trie.Add("app", 2);

        Assert.Equal(2, trie.Count);
        Assert.True(trie.TryGetValue("apple", out int a));
        Assert.Equal(1, a);
        Assert.True(trie.TryGetValue("app", out int b));
        Assert.Equal(2, b);
    }

    [Fact]
    public void Add_DuplicateKey_Throws()
    {
        var trie = new Trie<int>();
        trie.Add("key", 1);
        Assert.Throws<ArgumentException>(() => trie.Add("key", 2));
        Assert.Equal(1, trie["key"]);
    }

    [Fact]
    public void TryAdd_ExistingKey_ReturnsFalseAndLeavesValue()
    {
        var trie = new Trie<int>();
        Assert.True(trie.TryAdd("key", 1));
        Assert.False(trie.TryAdd("key", 99));
        Assert.Equal(1, trie["key"]);
        Assert.Equal(1, trie.Count);
    }

    [Fact]
    public void Indexer_Set_AddsOrOverwrites()
    {
        var trie = new Trie<int>();
        trie["k"] = 1;
        Assert.Equal(1, trie["k"]);
        Assert.Equal(1, trie.Count);

        trie["k"] = 5;
        Assert.Equal(5, trie["k"]);
        Assert.Equal(1, trie.Count);
    }

    [Fact]
    public void Indexer_Get_MissingKey_ThrowsKeyNotFound()
    {
        var trie = new Trie<int>();
        trie.Add("present", 1);
        Assert.Throws<KeyNotFoundException>(() => trie["absent"]);
        // A key whose path exists as a prefix of another but is not itself stored also throws.
        trie.Add("prefixed", 2);
        Assert.Throws<KeyNotFoundException>(() => trie["prefix"]);
    }

    [Fact]
    public void ContainsKey_DistinguishesStoredKeysFromInteriorPrefixes()
    {
        var trie = new Trie<int>();
        trie.Add("team", 1);
        Assert.True(trie.ContainsKey("team"));
        // "tea" is an interior path but never stored.
        Assert.False(trie.ContainsKey("tea"));
        Assert.False(trie.ContainsKey("teams"));
    }

    [Fact]
    public void EmptyString_IsAValidKey()
    {
        var trie = new Trie<int>();
        Assert.True(trie.TryAdd(string.Empty, 42));
        Assert.Equal(1, trie.Count);
        Assert.True(trie.ContainsKey(string.Empty));
        Assert.Equal(42, trie[string.Empty]);
        Assert.True(trie.Remove(string.Empty, out int removed));
        Assert.Equal(42, removed);
        Assert.Equal(0, trie.Count);
        Assert.False(trie.ContainsKey(string.Empty));
    }

    [Fact]
    public void Remove_MissingKey_ReturnsFalse()
    {
        var trie = new Trie<int>();
        trie.Add("hello", 1);
        Assert.False(trie.Remove("world"));
        // A pure interior prefix is not a stored key.
        Assert.False(trie.Remove("hell"));
        Assert.Equal(1, trie.Count);
    }

    [Fact]
    public void Remove_PrunesDeadPath_ButRetainsSharedPrefixOfSurvivingKey()
    {
        var trie = new Trie<int>();
        trie.Add("car", 1);
        trie.Add("cart", 2);

        // Removing "cart" must drop only the 't' node; "car" and the shared "car" path survive.
        Assert.True(trie.Remove("cart", out int v));
        Assert.Equal(2, v);
        Assert.True(trie.ContainsKey("car"));
        Assert.False(trie.ContainsKey("cart"));
        Assert.False(trie.ContainsPrefix("cart"));
        Assert.True(trie.ContainsPrefix("car"));

        // Removing "car" now prunes the whole branch back to the root.
        Assert.True(trie.Remove("car"));
        Assert.Equal(0, trie.Count);
        Assert.False(trie.ContainsPrefix("c"));
    }

    [Fact]
    public void Remove_InteriorKey_KeepsDescendantKey()
    {
        var trie = new Trie<int>();
        trie.Add("do", 1);
        trie.Add("dog", 2);

        // "do" terminates an interior node that also leads to "dog"; removing it must not drop "dog".
        Assert.True(trie.Remove("do"));
        Assert.False(trie.ContainsKey("do"));
        Assert.True(trie.ContainsKey("dog"));
        Assert.Equal(2, trie["dog"]);
    }

    [Fact]
    public void ManyChildrenUnderOneNode_GrowAndRemoveKeepOrderAndValues()
    {
        // Forces the per-node child array to grow past its initial capacity of 2 and back, exercising the
        // sorted insert, the resize, and the tail-shifting RemoveChildAt.
        var trie = new Trie<int>();
        string letters = "gdafceb";
        for (int i = 0; i < letters.Length; i++)
            trie.Add(letters[i].ToString(), i);

        Assert.Equal(letters.Length, trie.Count);

        // Remove an interior letter and confirm the rest survive with correct values.
        Assert.True(trie.Remove("d"));
        foreach (char c in letters)
        {
            if (c == 'd')
                Assert.False(trie.ContainsKey(c.ToString()));
            else
                Assert.True(trie.ContainsKey(c.ToString()));
        }

        // Enumeration is ascending ordinal order regardless of insertion order.
        Assert.Equal("abcefg", string.Concat(trie.Keys));
    }

    [Fact]
    public void Clear_EmptiesTheTrie_AndAllowsReuse()
    {
        var trie = new Trie<int>();
        trie.Add("a", 1);
        trie.Add("ab", 2);
        trie.Add("abc", 3);

        trie.Clear();
        Assert.Equal(0, trie.Count);
        Assert.False(trie.ContainsKey("a"));
        Assert.False(trie.ContainsPrefix("a"));
        Assert.Empty(trie);

        // Clear on an already-empty trie is a no-op.
        trie.Clear();
        Assert.Equal(0, trie.Count);

        trie.Add("z", 9);
        Assert.Equal(1, trie.Count);
        Assert.Equal(9, trie["z"]);
    }

    [Fact]
    public void Constructor_FromEntries_LoadsAll_LastDuplicateWins()
    {
        var entries = new[]
        {
            new KeyValuePair<string, int>("one", 1),
            new KeyValuePair<string, int>("two", 2),
            new KeyValuePair<string, int>("one", 11),
        };

        var trie = new Trie<int>(entries);
        Assert.Equal(2, trie.Count);
        Assert.Equal(11, trie["one"]);
        Assert.Equal(2, trie["two"]);
    }

    [Fact]
    public void NullArguments_Throw()
    {
        var trie = new Trie<int>();
        Assert.Throws<ArgumentNullException>(() => trie.Add(null!, 1));
        Assert.Throws<ArgumentNullException>(() => trie.TryAdd(null!, 1));
        Assert.Throws<ArgumentNullException>(() => trie.ContainsKey(null!));
        Assert.Throws<ArgumentNullException>(() => trie.TryGetValue(null!, out _));
        Assert.Throws<ArgumentNullException>(() => trie.Remove(null!));
        Assert.Throws<ArgumentNullException>(() => trie[null!]);
        Assert.Throws<ArgumentNullException>(() => trie[null!] = 1);
        Assert.Throws<ArgumentNullException>(() => trie.ContainsPrefix(null!));
        Assert.Throws<ArgumentNullException>(() => trie.GetByPrefix(null!).GetEnumerator().MoveNext());
        Assert.Throws<ArgumentNullException>(() => trie.TryGetLongestPrefix(null!, out _, out _));
        Assert.Throws<ArgumentNullException>(() => new Trie<int>(null!));
        Assert.Throws<ArgumentNullException>(() => new Trie<int>(new[] { new KeyValuePair<string, int>(null!, 1) }));
    }
}
