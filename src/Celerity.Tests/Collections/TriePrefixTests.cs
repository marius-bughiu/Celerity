using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Coverage for <see cref="Trie{TValue}"/>'s prefix operations — the type's reason to exist over a
/// <see cref="Dictionary{TKey, TValue}"/>: <see cref="Trie{TValue}.ContainsPrefix"/>,
/// <see cref="Trie{TValue}.GetByPrefix"/> / <see cref="Trie{TValue}.GetKeysWithPrefix"/> (ordering,
/// prefix-equals-key inclusion, the empty prefix, and a missing prefix), and
/// <see cref="Trie{TValue}.TryGetLongestPrefix"/> (the root/empty-key match, exact match, prefix chains, and
/// no match).
/// </summary>
public class TriePrefixTests
{
    private static Trie<int> Build(params string[] keys)
    {
        var trie = new Trie<int>();
        for (int i = 0; i < keys.Length; i++)
            trie.Add(keys[i], i);
        return trie;
    }

    [Fact]
    public void ContainsPrefix_MatchesInteriorPathsAndStoredKeys()
    {
        var trie = Build("apple", "application", "banana");

        Assert.True(trie.ContainsPrefix("app"));       // interior path shared by two keys
        Assert.True(trie.ContainsPrefix("apple"));     // a stored key is a prefix of itself
        Assert.True(trie.ContainsPrefix("b"));
        Assert.False(trie.ContainsPrefix("apx"));      // path breaks
        Assert.False(trie.ContainsPrefix("applexyz")); // extends past every key
    }

    [Fact]
    public void ContainsPrefix_EmptyPrefix_TracksNonEmptiness()
    {
        var trie = new Trie<int>();
        Assert.False(trie.ContainsPrefix(string.Empty));
        trie.Add("x", 1);
        Assert.True(trie.ContainsPrefix(string.Empty));
        trie.Remove("x");
        Assert.False(trie.ContainsPrefix(string.Empty));
    }

    [Fact]
    public void GetByPrefix_ReturnsMatchingEntries_InAscendingKeyOrder()
    {
        var trie = Build("app", "apple", "applet", "apply", "banana", "ap, out of order");

        List<string> keys = trie.GetKeysWithPrefix("app").ToList();

        Assert.Equal(new[] { "app", "apple", "applet", "apply" }, keys);
    }

    [Fact]
    public void GetByPrefix_PrefixEqualToKey_IncludesThatKey()
    {
        var trie = Build("go", "gopher", "gopher-tunnel");

        List<KeyValuePair<string, int>> matches = trie.GetByPrefix("gopher").ToList();

        Assert.Equal(2, matches.Count);
        Assert.Equal("gopher", matches[0].Key);
        Assert.Equal("gopher-tunnel", matches[1].Key);
    }

    [Fact]
    public void GetByPrefix_EmptyPrefix_EnumeratesEverythingSorted()
    {
        var trie = Build("delta", "alpha", "charlie", "bravo");

        Assert.Equal(new[] { "alpha", "bravo", "charlie", "delta" }, trie.GetKeysWithPrefix(string.Empty));
    }

    [Fact]
    public void GetByPrefix_NoMatch_IsEmpty()
    {
        var trie = Build("cat", "car");
        Assert.Empty(trie.GetByPrefix("dog"));
        Assert.Empty(trie.GetByPrefix("cart"));
    }

    [Fact]
    public void TryGetLongestPrefix_ReturnsLongestStoredPrefixOfQuery()
    {
        var trie = new Trie<string>();
        trie["/"] = "root";
        trie["/api"] = "api";
        trie["/api/v1"] = "v1";

        Assert.True(trie.TryGetLongestPrefix("/api/v1/users", out string key, out string value));
        Assert.Equal("/api/v1", key);
        Assert.Equal("v1", value);

        Assert.True(trie.TryGetLongestPrefix("/api/other", out key, out value));
        Assert.Equal("/api", key);
        Assert.Equal("api", value);

        // Exact match: the whole query is itself a stored key.
        Assert.True(trie.TryGetLongestPrefix("/api", out key, out value));
        Assert.Equal("/api", key);
    }

    [Fact]
    public void TryGetLongestPrefix_EmptyStringKey_MatchesAsZeroLengthPrefix()
    {
        var trie = new Trie<int>();
        trie[string.Empty] = 7;

        Assert.True(trie.TryGetLongestPrefix("anything", out string key, out int value));
        Assert.Equal(string.Empty, key);
        Assert.Equal(7, value);
    }

    [Fact]
    public void TryGetLongestPrefix_ExactAndEmptyMatches_AvoidAllocatingANewKeyString()
    {
        var trie = new Trie<int>();
        trie[string.Empty] = 0;
        trie["abc"] = 1;

        // Exact match: the returned key is the same reference as the query (no Substring copy).
        var query = new string("abc".ToCharArray());
        Assert.True(trie.TryGetLongestPrefix(query, out string? exact, out _));
        Assert.Same(query, exact);

        // Empty-key match: the interned empty string, no allocation.
        Assert.True(trie.TryGetLongestPrefix("zzz", out string? empty, out _));
        Assert.Same(string.Empty, empty);
    }

    [Fact]
    public void GetByPrefix_MissingPrefix_StreamStillDetectsModification()
    {
        var trie = Build("cat", "car");

        IEnumerator<KeyValuePair<string, int>> e = trie.GetByPrefix("dog").GetEnumerator();
        trie.Add("cow", 9);
        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void TryGetLongestPrefix_NoStoredPrefix_ReturnsFalse()
    {
        var trie = new Trie<int>();
        trie["abcd"] = 1; // longer than the query; not a prefix of it

        Assert.False(trie.TryGetLongestPrefix("abc", out string key, out int value));
        Assert.Null(key);
        Assert.Equal(0, value);

        // A break before any stored key also yields no match.
        Assert.False(trie.TryGetLongestPrefix("xyz", out _, out _));
    }
}
