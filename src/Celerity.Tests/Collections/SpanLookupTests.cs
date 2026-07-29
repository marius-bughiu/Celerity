using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Cross-collection tests for the span-keyed lookup surface: the same check run across every
/// string-keyed type that gained it — <see cref="FrozenCelerityDictionary{TValue, THasher}"/>,
/// <see cref="FrozenCeleritySet{THasher}"/>, <see cref="CelerityDictionary{TKey, TValue, THasher}"/>,
/// <see cref="CeleritySet{T, THasher}"/>, and <see cref="Trie{TValue}"/>.
/// </summary>
/// <remarks>
/// The contract under test is that the span path and the string path are indistinguishable:
/// same hits, same misses, same values. Every row therefore asserts the span result
/// <em>against the type's own string overload</em> rather than against a hard-coded
/// expectation, so a divergence shows up wherever it is introduced.
/// </remarks>
public class SpanLookupTests
{
    // A key set that exercises the corners: the empty string, keys that are prefixes of one
    // another, keys differing only in a high byte (which the default low-byte hasher collides),
    // and a surrogate pair.
    private static readonly string[] Keys =
    [
        "",
        "a",
        "ab",
        "abc",
        "alpha",
        "alphabet",
        "A",
        "Ł",              // U+0141 — collides with "A" under the low-byte StringFnV1AHasher
        "日本語",
        "emoji-\U0001F600", // surrogate pair
        "x\0y",             // embedded NUL
        new string('q', 300),
    ];

    private static readonly string[] Misses =
    [
        "z",
        "alph",
        "alphabets",
        "Ń",
        "emoji-\U0001F601",
        new string('q', 299),
    ];

    private static KeyValuePair<string, int>[] Pairs() =>
        Keys.Select((k, i) => new KeyValuePair<string, int>(k, i)).ToArray();

    // ── FrozenCelerityDictionary ──────────────────────────────────────────────

    [Fact]
    public void TryGetValue_ShouldAgreeWithStringOverload_WhenFrozenCelerityDictionary()
    {
        var dict = new FrozenCelerityDictionary<int, StringFnV1AFullHasher>(Pairs());

        foreach (string key in Keys.Concat(Misses))
        {
            bool expected = dict.TryGetValue(key, out int expectedValue);
            bool actual = dict.TryGetValue(key.AsSpan(), out int actualValue);

            Assert.Equal(expected, actual);
            Assert.Equal(expectedValue, actualValue);
            Assert.Equal(dict.ContainsKey(key), dict.ContainsKey(key.AsSpan()));
        }
    }

    [Fact]
    public void TryGetValue_ShouldAgreeWithStringOverload_WhenFrozenCelerityDictionaryFallsBackToProbing()
    {
        // "A" and "Ł" share a raw code under the low-byte hasher, so no seed can separate them
        // and the build takes the linear-probing fallback. The span probe must mirror it.
        var dict = new FrozenCelerityDictionary<int, StringFnV1AHasher>(Pairs());
        Assert.False(dict.IsPerfectlyHashed);

        foreach (string key in Keys.Concat(Misses))
        {
            bool expected = dict.TryGetValue(key, out int expectedValue);
            bool actual = dict.TryGetValue(key.AsSpan(), out int actualValue);

            Assert.Equal(expected, actual);
            Assert.Equal(expectedValue, actualValue);
        }
    }

    [Fact]
    public void TryGetValue_ShouldNotMatchTheNullKey_WhenSpanIsEmpty()
    {
        // A span has no null state, so an empty span is the key "" and never the out-of-band
        // null key. Here "" is absent and null is present: the empty span must miss.
        var dict = new FrozenCelerityDictionary<int, StringFnV1AFullHasher>(
            [new KeyValuePair<string, int>(null!, 7), new KeyValuePair<string, int>("a", 1)]);

        Assert.True(dict.TryGetValue((string)null!, out int viaNull));
        Assert.Equal(7, viaNull);
        Assert.False(dict.TryGetValue(ReadOnlySpan<char>.Empty, out _));
        Assert.False(dict.ContainsKey(ReadOnlySpan<char>.Empty));
    }

    [Fact]
    public void TryGetValue_ShouldThrowArgumentNullException_WhenFrozenCelerityDictionaryIsNull()
    {
        FrozenCelerityDictionary<int, StringFnV1AFullHasher> dict = null!;
        Assert.Throws<ArgumentNullException>(() => dict.TryGetValue("a".AsSpan(), out int _));
        Assert.Throws<ArgumentNullException>(() => dict.ContainsKey("a".AsSpan()));
    }

    // ── FrozenCeleritySet ─────────────────────────────────────────────────────

    [Fact]
    public void Contains_ShouldAgreeWithStringOverload_WhenFrozenCeleritySet()
    {
        var set = new FrozenCeleritySet<StringFnV1AFullHasher>(Keys);

        foreach (string key in Keys.Concat(Misses))
            Assert.Equal(set.Contains(key), set.Contains(key.AsSpan()));
    }

    [Fact]
    public void Contains_ShouldAgreeWithStringOverload_WhenFrozenCeleritySetFallsBackToProbing()
    {
        var set = new FrozenCeleritySet<StringFnV1AHasher>(Keys);
        Assert.False(set.IsPerfectlyHashed);

        foreach (string key in Keys.Concat(Misses))
            Assert.Equal(set.Contains(key), set.Contains(key.AsSpan()));
    }

    [Fact]
    public void Contains_ShouldThrowArgumentNullException_WhenFrozenCeleritySetIsNull()
    {
        FrozenCeleritySet<StringFnV1AFullHasher> set = null!;
        Assert.Throws<ArgumentNullException>(() => set.Contains("a".AsSpan()));
    }

    // ── CelerityDictionary ────────────────────────────────────────────────────

    [Fact]
    public void TryGetValue_ShouldAgreeWithStringOverload_WhenCelerityDictionary()
    {
        var dict = new CelerityDictionary<string, int, StringFnV1AFullHasher>();
        foreach (KeyValuePair<string, int> pair in Pairs())
            dict.Add(pair.Key, pair.Value);

        foreach (string key in Keys.Concat(Misses))
        {
            bool expected = dict.TryGetValue(key, out int expectedValue);
            bool actual = dict.TryGetValue(key.AsSpan(), out int actualValue);

            Assert.Equal(expected, actual);
            Assert.Equal(expectedValue, actualValue);
            Assert.Equal(dict.ContainsKey(key), dict.ContainsKey(key.AsSpan()));
        }
    }

    [Fact]
    public void TryGetValue_ShouldSeeSubsequentMutations_WhenCelerityDictionary()
    {
        var dict = new CelerityDictionary<string, int, StringFnV1AFullHasher>();
        Assert.False(dict.ContainsKey("late".AsSpan()));

        dict.Add("late", 42);
        Assert.True(dict.TryGetValue("late".AsSpan(), out int value));
        Assert.Equal(42, value);

        dict.Remove("late");
        Assert.False(dict.TryGetValue("late".AsSpan(), out _));
    }

    [Fact]
    public void TryGetValue_ShouldSurviveAResize_WhenCelerityDictionary()
    {
        // Grow well past the initial threshold so the span probe runs against a rehashed table.
        var dict = new CelerityDictionary<string, int, StringXxHash3Hasher>(capacity: 4);
        for (int i = 0; i < 500; i++)
            dict.Add($"key-{i}", i);

        for (int i = 0; i < 500; i++)
        {
            Assert.True(dict.TryGetValue($"key-{i}".AsSpan(), out int value));
            Assert.Equal(i, value);
        }

        Assert.False(dict.TryGetValue("key-500".AsSpan(), out _));
    }

    [Fact]
    public void TryGetValue_ShouldNotMatchTheNullKey_WhenCelerityDictionarySpanIsEmpty()
    {
        var dict = new CelerityDictionary<string, int, StringFnV1AFullHasher>();
        dict.Add(null!, 7);

        Assert.True(dict.TryGetValue((string)null!, out int viaNull));
        Assert.Equal(7, viaNull);
        Assert.False(dict.TryGetValue(ReadOnlySpan<char>.Empty, out _));

        dict.Add(string.Empty, 9);
        Assert.True(dict.TryGetValue(ReadOnlySpan<char>.Empty, out int viaSpan));
        Assert.Equal(9, viaSpan);
    }

    [Fact]
    public void TryGetValue_ShouldThrowArgumentNullException_WhenCelerityDictionaryIsNull()
    {
        CelerityDictionary<string, int, StringFnV1AFullHasher> dict = null!;
        Assert.Throws<ArgumentNullException>(() => dict.TryGetValue("a".AsSpan(), out int _));
        Assert.Throws<ArgumentNullException>(() => dict.ContainsKey("a".AsSpan()));
    }

    // ── CeleritySet ───────────────────────────────────────────────────────────

    [Fact]
    public void Contains_ShouldAgreeWithStringOverload_WhenCeleritySet()
    {
        var set = new CeleritySet<string, StringFnV1AFullHasher>();
        foreach (string key in Keys)
            set.Add(key);

        foreach (string key in Keys.Concat(Misses))
            Assert.Equal(set.Contains(key), set.Contains(key.AsSpan()));
    }

    [Fact]
    public void Contains_ShouldSurviveAResize_WhenCeleritySet()
    {
        var set = new CeleritySet<string, StringXxHash3Hasher>(capacity: 4);
        for (int i = 0; i < 500; i++)
            set.Add($"item-{i}");

        for (int i = 0; i < 500; i++)
            Assert.True(set.Contains($"item-{i}".AsSpan()));

        Assert.False(set.Contains("item-500".AsSpan()));
    }

    [Fact]
    public void Contains_ShouldThrowArgumentNullException_WhenCeleritySetIsNull()
    {
        CeleritySet<string, StringFnV1AFullHasher> set = null!;
        Assert.Throws<ArgumentNullException>(() => set.Contains("a".AsSpan()));
    }

    // ── Trie ──────────────────────────────────────────────────────────────────

    [Fact]
    public void TryGetValue_ShouldAgreeWithStringOverload_WhenTrie()
    {
        var trie = new Trie<int>();
        foreach (KeyValuePair<string, int> pair in Pairs())
            trie.Add(pair.Key, pair.Value);

        foreach (string key in Keys.Concat(Misses))
        {
            bool expected = trie.TryGetValue(key, out int expectedValue);
            bool actual = trie.TryGetValue(key.AsSpan(), out int actualValue);

            Assert.Equal(expected, actual);
            Assert.Equal(expectedValue, actualValue);
            Assert.Equal(trie.ContainsKey(key), trie.ContainsKey(key.AsSpan()));
            Assert.Equal(trie.ContainsPrefix(key), trie.ContainsPrefix(key.AsSpan()));
        }
    }

    [Fact]
    public void ContainsPrefix_ShouldAgreeWithStringOverload_WhenTriePrefixIsPartial()
    {
        var trie = new Trie<int> { ["alphabet"] = 1 };

        Assert.True(trie.ContainsPrefix("alph".AsSpan()));
        Assert.False(trie.ContainsKey("alph".AsSpan()));
        Assert.True(trie.ContainsPrefix(ReadOnlySpan<char>.Empty));
        Assert.False(trie.ContainsPrefix("beta".AsSpan()));
    }

    // ── The span may be a slice of a caller-owned buffer, not a whole string ──

    [Fact]
    public void SpanLookups_ShouldMatchOnASliceOfALargerBuffer_AcrossEveryType()
    {
        // The shape a real parser hands in: the key sits inside a bigger buffer with
        // neighbouring characters on both sides.
        const string Buffer = ">>>alphabet<<<";
        ReadOnlySpan<char> slice = Buffer.AsSpan(3, "alphabet".Length);

        var frozenDict = new FrozenCelerityDictionary<int, StringFnV1AFullHasher>(Pairs());
        var frozenSet = new FrozenCeleritySet<StringFnV1AFullHasher>(Keys);

        var dict = new CelerityDictionary<string, int, StringFnV1AFullHasher>();
        var set = new CeleritySet<string, StringFnV1AFullHasher>();
        var trie = new Trie<int>();
        foreach (KeyValuePair<string, int> pair in Pairs())
        {
            dict.Add(pair.Key, pair.Value);
            set.Add(pair.Key);
            trie.Add(pair.Key, pair.Value);
        }

        int expected = Array.IndexOf(Keys, "alphabet");

        Assert.True(frozenDict.TryGetValue(slice, out int a));
        Assert.Equal(expected, a);
        Assert.True(frozenSet.Contains(slice));
        Assert.True(dict.TryGetValue(slice, out int b));
        Assert.Equal(expected, b);
        Assert.True(set.Contains(slice));
        Assert.True(trie.TryGetValue(slice, out int c));
        Assert.Equal(expected, c);
    }
}
