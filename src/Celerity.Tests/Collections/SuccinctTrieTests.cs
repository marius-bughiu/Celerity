using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural coverage of <see cref="SuccinctTrie{TValue}"/>: construction and its validation, exact and
/// span-keyed lookup, the prefix surface, longest-prefix match, and the properties that report what the
/// encoding costs.
/// </summary>
public class SuccinctTrieTests
{
    private static SuccinctTrie<int> Build(params string[] keys)
    {
        var entries = new List<KeyValuePair<string, int>>(keys.Length);
        for (int i = 0; i < keys.Length; i++)
            entries.Add(new KeyValuePair<string, int>(keys[i], i));
        return new SuccinctTrie<int>(entries);
    }

    // ---- construction ------------------------------------------------------------------------------

    [Fact]
    public void Constructor_FromEntries_ShouldStoreEveryEntry()
    {
        SuccinctTrie<int> trie = Build("banana", "apple", "app", "apricot", "cherry");

        Assert.Equal(5, trie.Count);
        Assert.Equal(0, trie["banana"]);
        Assert.Equal(1, trie["apple"]);
        Assert.Equal(2, trie["app"]);
        Assert.Equal(3, trie["apricot"]);
        Assert.Equal(4, trie["cherry"]);
    }

    [Fact]
    public void Constructor_FromEmptySequence_ShouldProduceAnEmptyTrie()
    {
        var trie = new SuccinctTrie<int>(Array.Empty<KeyValuePair<string, int>>());

        Assert.Equal(0, trie.Count);
        Assert.False(trie.ContainsKey(string.Empty));
        Assert.Empty(trie.Keys);
    }

    [Fact]
    public void Constructor_WithDuplicateKeys_ShouldKeepTheLastValue()
    {
        // Matches Trie<TValue>'s bulk-load constructor, which sets through the indexer.
        var trie = new SuccinctTrie<int>(
        [
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2),
            new KeyValuePair<string, int>("a", 3),
        ]);

        Assert.Equal(2, trie.Count);
        Assert.Equal(3, trie["a"]);
    }

    [Fact]
    public void Constructor_WithNullSequence_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SuccinctTrie<int>((IEnumerable<KeyValuePair<string, int>>)null!));
    }

    [Fact]
    public void Constructor_WithNullKey_ShouldThrowArgumentNullException()
    {
        var entries = new[] { new KeyValuePair<string, int>(null!, 1) };

        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => new SuccinctTrie<int>(entries));
        Assert.Equal("key", ex.ParamName);
    }

    [Fact]
    public void Constructor_FromTrie_ShouldSnapshotEveryEntry()
    {
        var source = new Trie<string>();
        source["one"] = "1";
        source["two"] = "2";
        source["three"] = "3";

        var trie = new SuccinctTrie<string>(source);

        Assert.Equal(3, trie.Count);
        Assert.Equal("1", trie["one"]);
        Assert.Equal("2", trie["two"]);
        Assert.Equal("3", trie["three"]);
    }

    [Fact]
    public void Constructor_FromTrie_ShouldNotObserveLaterChanges()
    {
        var source = new Trie<int>();
        source["a"] = 1;

        var trie = new SuccinctTrie<int>(source);
        source["b"] = 2;
        source.Remove("a");

        Assert.Equal(1, trie.Count);
        Assert.True(trie.ContainsKey("a"));
        Assert.False(trie.ContainsKey("b"));
    }

    [Fact]
    public void Constructor_FromNullTrie_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SuccinctTrie<int>((Trie<int>)null!));
    }

    [Fact]
    public void Constructor_WithEmptyStringKey_ShouldStoreItAtTheRoot()
    {
        SuccinctTrie<int> trie = Build(string.Empty, "a");

        Assert.Equal(2, trie.Count);
        Assert.True(trie.ContainsKey(string.Empty));
        Assert.Equal(0, trie[string.Empty]);
    }

    // ---- exact lookup ------------------------------------------------------------------------------

    [Fact]
    public void Indexer_WithMissingKey_ShouldThrowKeyNotFoundException()
    {
        SuccinctTrie<int> trie = Build("apple");

        Assert.Throws<KeyNotFoundException>(() => trie["banana"]);
    }

    [Fact]
    public void Indexer_WithInteriorPrefix_ShouldThrowKeyNotFoundException()
    {
        // "app" is a node on the way to "apple" but is not itself a key.
        SuccinctTrie<int> trie = Build("apple");

        Assert.Throws<KeyNotFoundException>(() => trie["app"]);
    }

    [Fact]
    public void Indexer_WithNullKey_ShouldThrowArgumentNullException()
    {
        SuccinctTrie<int> trie = Build("apple");

        Assert.Throws<ArgumentNullException>(() => trie[null!]);
    }

    [Fact]
    public void ContainsKey_ShouldDistinguishKeysFromInteriorPrefixes()
    {
        SuccinctTrie<int> trie = Build("apple", "application");

        Assert.True(trie.ContainsKey("apple"));
        Assert.True(trie.ContainsKey("application"));
        Assert.False(trie.ContainsKey("app"));
        Assert.False(trie.ContainsKey("appl"));
        Assert.False(trie.ContainsKey("applesauce"));
        Assert.False(trie.ContainsKey("banana"));
    }

    [Fact]
    public void ContainsKey_WithNullKey_ShouldThrowArgumentNullException()
    {
        SuccinctTrie<int> trie = Build("apple");

        Assert.Throws<ArgumentNullException>(() => trie.ContainsKey(null!));
    }

    [Fact]
    public void TryGetValue_ShouldReportHitsAndMisses()
    {
        SuccinctTrie<int> trie = Build("apple", "app");

        Assert.True(trie.TryGetValue("app", out int hit));
        Assert.Equal(1, hit);

        Assert.False(trie.TryGetValue("ap", out int miss));
        Assert.Equal(0, miss);

        Assert.False(trie.TryGetValue("zebra", out _));
    }

    [Fact]
    public void TryGetValue_WithNullKey_ShouldThrowArgumentNullException()
    {
        SuccinctTrie<int> trie = Build("apple");

        Assert.Throws<ArgumentNullException>(() => trie.TryGetValue(null!, out _));
    }

    // ---- span-keyed lookup -------------------------------------------------------------------------

    [Fact]
    public void ContainsKey_WithSpan_ShouldMatchTheStringOverload()
    {
        SuccinctTrie<int> trie = Build("apple", "apricot");
        char[] buffer = "xxapplexx".ToCharArray();

        Assert.True(trie.ContainsKey(buffer.AsSpan(2, 5)));
        Assert.False(trie.ContainsKey(buffer.AsSpan(2, 3)));
    }

    [Fact]
    public void TryGetValue_WithSpan_ShouldMatchTheStringOverload()
    {
        SuccinctTrie<int> trie = Build("apple", "apricot");
        char[] buffer = "  apricot ".ToCharArray();

        Assert.True(trie.TryGetValue(buffer.AsSpan(2, 7), out int value));
        Assert.Equal(1, value);
        Assert.False(trie.TryGetValue(buffer.AsSpan(2, 3), out _));
    }

    [Fact]
    public void SpanOverloads_WithEmptySpan_ShouldMeanTheEmptyKey()
    {
        SuccinctTrie<int> withRoot = Build(string.Empty, "a");
        SuccinctTrie<int> withoutRoot = Build("a");

        Assert.True(withRoot.ContainsKey(ReadOnlySpan<char>.Empty));
        Assert.True(withRoot.TryGetValue(ReadOnlySpan<char>.Empty, out int value));
        Assert.Equal(0, value);

        Assert.False(withoutRoot.ContainsKey(ReadOnlySpan<char>.Empty));
        Assert.False(withoutRoot.TryGetValue(ReadOnlySpan<char>.Empty, out _));
    }

    // ---- prefixes ----------------------------------------------------------------------------------

    [Fact]
    public void ContainsPrefix_ShouldMatchStoredPathsOnly()
    {
        SuccinctTrie<int> trie = Build("apple", "apricot", "banana");

        Assert.True(trie.ContainsPrefix("a"));
        Assert.True(trie.ContainsPrefix("ap"));
        Assert.True(trie.ContainsPrefix("apple"));
        Assert.True(trie.ContainsPrefix(string.Empty));
        Assert.False(trie.ContainsPrefix("apples"));
        Assert.False(trie.ContainsPrefix("c"));
    }

    [Fact]
    public void ContainsPrefix_OnAnEmptyTrie_ShouldBeFalseEvenForTheEmptyPrefix()
    {
        var trie = new SuccinctTrie<int>(Array.Empty<KeyValuePair<string, int>>());

        Assert.False(trie.ContainsPrefix(string.Empty));
        Assert.False(trie.ContainsPrefix(ReadOnlySpan<char>.Empty));
    }

    [Fact]
    public void ContainsPrefix_WithSpan_ShouldMatchTheStringOverload()
    {
        SuccinctTrie<int> trie = Build("apple", "apricot");
        char[] buffer = "--ap--".ToCharArray();

        Assert.True(trie.ContainsPrefix(buffer.AsSpan(2, 2)));
        Assert.False(trie.ContainsPrefix(buffer.AsSpan(0, 2)));
    }

    [Fact]
    public void ContainsPrefix_WithNullPrefix_ShouldThrowArgumentNullException()
    {
        SuccinctTrie<int> trie = Build("apple");

        Assert.Throws<ArgumentNullException>(() => trie.ContainsPrefix(null!));
    }

    [Fact]
    public void GetByPrefix_ShouldYieldEveryMatchInAscendingKeyOrder()
    {
        SuccinctTrie<int> trie = Build("apricot", "apple", "app", "banana", "band");

        KeyValuePair<string, int>[] matches = trie.GetByPrefix("app").ToArray();

        Assert.Equal(["app", "apple"], matches.Select(m => m.Key));
        Assert.Equal([2, 1], matches.Select(m => m.Value));
    }

    [Fact]
    public void GetByPrefix_WithTheEmptyPrefix_ShouldYieldEveryEntry()
    {
        SuccinctTrie<int> trie = Build("b", "a", "c");

        Assert.Equal(["a", "b", "c"], trie.GetByPrefix(string.Empty).Select(m => m.Key));
    }

    [Fact]
    public void GetByPrefix_WithNoMatch_ShouldBeEmpty()
    {
        SuccinctTrie<int> trie = Build("apple");

        Assert.Empty(trie.GetByPrefix("z"));
        Assert.Empty(trie.GetByPrefix("applesauce"));
    }

    [Fact]
    public void GetByPrefix_WithNullPrefix_ShouldThrowArgumentNullException()
    {
        SuccinctTrie<int> trie = Build("apple");

        Assert.Throws<ArgumentNullException>(() => trie.GetByPrefix(null!));
    }

    [Fact]
    public void GetKeysWithPrefix_ShouldYieldTheMatchingKeysInOrder()
    {
        SuccinctTrie<int> trie = Build("band", "banana", "bandana", "cat");

        Assert.Equal(["banana", "band", "bandana"], trie.GetKeysWithPrefix("ban"));
    }

    [Fact]
    public void GetKeysWithPrefix_WithNullPrefix_ShouldThrowArgumentNullException()
    {
        SuccinctTrie<int> trie = Build("apple");

        Assert.Throws<ArgumentNullException>(() => trie.GetKeysWithPrefix(null!));
    }

    // ---- longest prefix ----------------------------------------------------------------------------

    [Fact]
    public void TryGetLongestPrefix_ShouldReturnTheLongestStoredPrefix()
    {
        SuccinctTrie<int> trie = Build("a", "ab", "abcd");

        Assert.True(trie.TryGetLongestPrefix("abcde", out string? key, out int value));
        Assert.Equal("abcd", key);
        Assert.Equal(2, value);
    }

    [Fact]
    public void TryGetLongestPrefix_WithAnExactMatch_ShouldReturnTheQueryItself()
    {
        SuccinctTrie<int> trie = Build("route", "rou");

        Assert.True(trie.TryGetLongestPrefix("route", out string? key, out int value));
        Assert.Same("route", key);
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryGetLongestPrefix_WithTheEmptyKeyStored_ShouldFallBackToIt()
    {
        SuccinctTrie<int> trie = Build(string.Empty, "zz");

        Assert.True(trie.TryGetLongestPrefix("abc", out string? key, out int value));
        Assert.Equal(string.Empty, key);
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryGetLongestPrefix_WithNoStoredPrefix_ShouldReturnFalse()
    {
        SuccinctTrie<int> trie = Build("xyz");

        Assert.False(trie.TryGetLongestPrefix("abc", out string? key, out int value));
        Assert.Null(key);
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryGetLongestPrefix_ShouldIgnoreInteriorNodesThatAreNotKeys()
    {
        // "ro" and "rou" are nodes but not keys, so the only answer is "r".
        SuccinctTrie<int> trie = Build("r", "route");

        Assert.True(trie.TryGetLongestPrefix("rough", out string? key, out int value));
        Assert.Equal("r", key);
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryGetLongestPrefix_WithNullQuery_ShouldThrowArgumentNullException()
    {
        SuccinctTrie<int> trie = Build("apple");

        Assert.Throws<ArgumentNullException>(() => trie.TryGetLongestPrefix(null!, out _, out _));
    }

    // ---- what the encoding costs -------------------------------------------------------------------

    [Fact]
    public void NodeCount_ShouldBeTheRootPlusOnePerDistinctPrefix()
    {
        // Distinct prefixes of {"a", "ab", "b"}: "a", "ab", "b" — three, plus the root.
        SuccinctTrie<int> trie = Build("a", "ab", "b");

        Assert.Equal(4, trie.NodeCount);
    }

    [Fact]
    public void NodeCount_OnAnEmptyTrie_ShouldBeTheRootAlone()
    {
        var trie = new SuccinctTrie<int>(Array.Empty<KeyValuePair<string, int>>());

        Assert.Equal(1, trie.NodeCount);
    }

    [Fact]
    public void NodeCount_ShouldNotDoubleCountSharedPrefixes()
    {
        // "app" is shared, so the nodes are the root plus a, ap, app, appl, apple, appr, appro.
        SuccinctTrie<int> trie = Build("apple", "appro");

        Assert.Equal(8, trie.NodeCount);
    }

    [Fact]
    public void IndexSizeInBytes_ShouldGrowWithTheNodeCountAndStayFarBelowTheKeyBytes()
    {
        var entries = new List<KeyValuePair<string, int>>();
        for (int i = 0; i < 5000; i++)
            entries.Add(new KeyValuePair<string, int>($"key_{i:D8}", i));

        var trie = new SuccinctTrie<int>(entries);

        // Every node costs 2 bits of shape and 2 bytes of label; the two rank/select indexes add 25% of
        // their vectors. Nothing here approaches the 12 bytes per key the key strings themselves occupy.
        long perNode = trie.IndexSizeInBytes / trie.NodeCount;
        Assert.InRange(perNode, 1, 8);
    }

    [Fact]
    public void IndexSizeInBytes_OnAnEmptyTrie_ShouldBePositiveButTiny()
    {
        var trie = new SuccinctTrie<int>(Array.Empty<KeyValuePair<string, int>>());

        Assert.InRange(trie.IndexSizeInBytes, 1, 64);
    }

    // ---- shape ------------------------------------------------------------------------------------

    [Fact]
    public void Keys_SharingLongCommonPrefixes_ShouldAllResolve()
    {
        var entries = new List<KeyValuePair<string, int>>();
        for (int i = 0; i < 200; i++)
            entries.Add(new KeyValuePair<string, int>(new string('x', i) + "y", i));

        var trie = new SuccinctTrie<int>(entries);

        Assert.Equal(200, trie.Count);
        for (int i = 0; i < 200; i++)
            Assert.Equal(i, trie[new string('x', i) + "y"]);
    }

    [Fact]
    public void Keys_OnAWideAlphabet_ShouldAllResolve()
    {
        // 512 distinct first characters exercises the binary search over a node's child slice.
        var entries = new List<KeyValuePair<string, int>>();
        for (int i = 0; i < 512; i++)
            entries.Add(new KeyValuePair<string, int>(((char)(0x100 + i)).ToString(), i));

        var trie = new SuccinctTrie<int>(entries);

        for (int i = 0; i < 512; i++)
            Assert.Equal(i, trie[((char)(0x100 + i)).ToString()]);
        Assert.False(trie.ContainsKey(((char)(0x100 + 512)).ToString()));
    }

    [Fact]
    public void Keys_WithSurrogatesAndControlCharacters_ShouldBeTreatedAsOrdinalCodeUnits()
    {
        // U+1F600 is the surrogate pair D83D DE00, so it sits two nodes deep and its high half is a prefix
        // of a stored key without being one: a trie over UTF-16 code units, not over runes.
        SuccinctTrie<int> trie = Build(" ", "\uD83D\uDE00", "\u0000");

        Assert.True(trie.ContainsKey(" "));
        Assert.True(trie.ContainsKey("\uD83D\uDE00"));
        Assert.True(trie.ContainsKey("\u0000"));
        Assert.False(trie.ContainsKey("\uD83D"));
        Assert.True(trie.ContainsPrefix("\uD83D"));
    }
}
