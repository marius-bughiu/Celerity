using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

public class FrozenCelerityDictionaryTests
{
    private static FrozenCelerityDictionary<int> Build(params (string key, int value)[] pairs)
        => new FrozenCelerityDictionary<int>(
            pairs.Select(p => new KeyValuePair<string, int>(p.key, p.value)));

    // ── Construction & basic lookup ───────────────────────────────────────────

    [Fact]
    public void Build_FromPairs_AllKeysRoundTrip()
    {
        var dict = Build(("alice", 1), ("bob", 2), ("carol", 3));

        Assert.Equal(3, dict.Count);
        Assert.Equal(1, dict["alice"]);
        Assert.Equal(2, dict["bob"]);
        Assert.Equal(3, dict["carol"]);
        Assert.True(dict.ContainsKey("alice"));
        Assert.True(dict.ContainsKey("bob"));
        Assert.True(dict.ContainsKey("carol"));
    }

    [Fact]
    public void TryGetValue_PresentAndAbsent()
    {
        var dict = Build(("alice", 1), ("bob", 2));

        Assert.True(dict.TryGetValue("alice", out int a));
        Assert.Equal(1, a);
        Assert.False(dict.TryGetValue("dave", out int d));
        Assert.Equal(0, d);
    }

    [Fact]
    public void ContainsKey_AbsentKey_ReturnsFalse()
    {
        var dict = Build(("alice", 1), ("bob", 2));
        Assert.False(dict.ContainsKey("carol"));
    }

    [Fact]
    public void Indexer_MissingKey_ThrowsKeyNotFound()
    {
        var dict = Build(("alice", 1));
        Assert.Throws<KeyNotFoundException>(() => dict["nope"]);
    }

    // ── Empty string is a regular key ─────────────────────────────────────────

    [Fact]
    public void EmptyStringKey_IsRegularKey()
    {
        var dict = Build(("", 7), ("a", 1));

        Assert.Equal(2, dict.Count);
        Assert.True(dict.ContainsKey(""));
        Assert.Equal(7, dict[""]);
        Assert.True(dict.TryGetValue("", out int v));
        Assert.Equal(7, v);
    }

    // ── Null key out-of-band ──────────────────────────────────────────────────

    [Fact]
    public void NullKey_RoundTripsOutOfBand()
    {
        var dict = new FrozenCelerityDictionary<string>(new[]
        {
            new KeyValuePair<string, string>(null!, "null-value"),
            new KeyValuePair<string, string>("present", "p"),
        });

        Assert.Equal(2, dict.Count);
        Assert.True(dict.ContainsKey(null!));
        Assert.Equal("null-value", dict[null!]);
        Assert.True(dict.TryGetValue(null!, out string? nv));
        Assert.Equal("null-value", nv);
    }

    [Fact]
    public void NullKey_Absent_LookupsMiss()
    {
        var dict = Build(("a", 1));

        Assert.False(dict.ContainsKey(null!));
        Assert.False(dict.TryGetValue(null!, out _));
        Assert.Throws<KeyNotFoundException>(() => dict[null!]);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public void Build_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new FrozenCelerityDictionary<int>(null!));
    }

    [Fact]
    public void Build_DuplicateKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => Build(("a", 1), ("a", 2)));
    }

    [Fact]
    public void Build_DuplicateNullKey_Throws()
    {
        Assert.Throws<ArgumentException>(() => new FrozenCelerityDictionary<int>(new[]
        {
            new KeyValuePair<string, int>(null!, 1),
            new KeyValuePair<string, int>(null!, 2),
        }));
    }

    [Fact]
    public void Build_Empty_HasZeroCountAndMissesEverything()
    {
        var dict = new FrozenCelerityDictionary<int>(
            Array.Empty<KeyValuePair<string, int>>());

        Assert.Empty(dict);
        Assert.False(dict.ContainsKey("anything"));
        Assert.False(dict.ContainsKey(null!));
        Assert.False(dict.TryGetValue("anything", out _));
    }

    // ── Perfect-hash fast path vs fallback ────────────────────────────────────

    [Fact]
    public void NormalKeys_ArePerfectlyHashed()
    {
        // A modest set of clearly-distinct keys under a strong hasher: the build's
        // size×seed search must find a collision-free placement.
        var dict = new FrozenCelerityDictionary<int, StringMurmur3Hasher>(
            Enumerable.Range(0, 12).Select(i =>
                new KeyValuePair<string, int>("key-" + i, i)));

        Assert.True(dict.IsPerfectlyHashed);
        for (int i = 0; i < 12; i++)
            Assert.Equal(i, dict["key-" + i]);
    }

    [Fact]
    public void BaseHashCollidingKeys_FallBackButStayCorrect()
    {
        // StringFnV1AHasher folds only the low byte, so 'A' (U+0041) and 'Ł' (U+0141)
        // produce the SAME raw hash code. No mixing seed can separate two keys that
        // share a base hash, so the build cannot be perfect — but the equality check
        // on the probe path must still keep them distinct and correct.
        var fnv = new StringFnV1AHasher();
        Assert.Equal(fnv.Hash("A"), fnv.Hash("Ł"));

        var dict = new FrozenCelerityDictionary<int, StringFnV1AHasher>(new[]
        {
            new KeyValuePair<string, int>("A", 1),
            new KeyValuePair<string, int>("Ł", 2),
        });

        Assert.False(dict.IsPerfectlyHashed);
        Assert.Equal(2, dict.Count);
        Assert.Equal(1, dict["A"]);
        Assert.Equal(2, dict["Ł"]);
        Assert.True(dict.ContainsKey("A"));
        Assert.True(dict.ContainsKey("Ł"));
        // A third key sharing the same low byte but absent must still miss.
        Assert.False(dict.ContainsKey("ŁA"));
    }

    [Fact]
    public void AbsentKey_HashingIntoOccupiedSlot_Misses()
    {
        // Lookups of absent keys must return false even when they mix to a slot that
        // holds a different key (the equality check rejects them) — the subtle
        // correctness point for both the perfect and fallback paths.
        var dict = new FrozenCelerityDictionary<int, StringMurmur3Hasher>(
            Enumerable.Range(0, 50).Select(i =>
                new KeyValuePair<string, int>("present-" + i, i)));

        for (int i = 0; i < 5000; i++)
            Assert.False(dict.ContainsKey("absent-" + i));
    }

    // ── Larger random sweep: every key round-trips, all absent keys miss ───────

    [Fact]
    public void ManyKeys_AllRoundTripAndAbsentMiss()
    {
        var pairs = Enumerable.Range(0, 1000)
            .Select(i => new KeyValuePair<string, int>($"celerity/key/{i}/{i * 7}", i))
            .ToArray();

        var dict = new FrozenCelerityDictionary<int>(pairs);

        Assert.Equal(1000, dict.Count);
        foreach (var p in pairs)
        {
            Assert.True(dict.TryGetValue(p.Key, out int v));
            Assert.Equal(p.Value, v);
        }

        for (int i = 1000; i < 2000; i++)
            Assert.False(dict.ContainsKey($"celerity/key/{i}/{i * 7}"));
    }

    // ── ContainsValue ─────────────────────────────────────────────────────────

    [Fact]
    public void ContainsValue_FindsPresentValue_AndNullKeyValue()
    {
        var dict = new FrozenCelerityDictionary<string>(new[]
        {
            new KeyValuePair<string, string>("a", "one"),
            new KeyValuePair<string, string>("b", "two"),
            new KeyValuePair<string, string>(null!, "nullval"),
        });

        Assert.True(dict.ContainsValue("one"));
        Assert.True(dict.ContainsValue("two"));
        Assert.True(dict.ContainsValue("nullval"));
        Assert.False(dict.ContainsValue("three"));
    }

    // ── Enumeration & views ───────────────────────────────────────────────────

    [Fact]
    public void Enumeration_YieldsAllPairsIncludingNullKey()
    {
        var dict = new FrozenCelerityDictionary<int>(new[]
        {
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2),
            new KeyValuePair<string, int>(null!, 99),
        });

        var collected = new Dictionary<string, int>();
        bool sawNull = false;
        foreach (var kvp in dict)
        {
            if (kvp.Key is null) { sawNull = true; Assert.Equal(99, kvp.Value); }
            else collected[kvp.Key] = kvp.Value;
        }

        Assert.True(sawNull);
        Assert.Equal(2, collected.Count);
        Assert.Equal(1, collected["a"]);
        Assert.Equal(2, collected["b"]);
    }

    [Fact]
    public void Keys_And_Values_Views_EnumerateEverything()
    {
        var dict = Build(("a", 1), ("b", 2), ("c", 3));

        var keys = new List<string>();
        foreach (var k in dict.Keys) keys.Add(k);
        var values = new List<int>();
        foreach (var v in dict.Values) values.Add(v);

        Assert.Equal(3, dict.Keys.Count);
        Assert.Equal(3, dict.Values.Count);
        Assert.Equal(new[] { "a", "b", "c" }.OrderBy(x => x), keys.OrderBy(x => x));
        Assert.Equal(new[] { 1, 2, 3 }, values.OrderBy(x => x));
    }

    // ── IReadOnlyDictionary surface ───────────────────────────────────────────

    [Fact]
    public void UsableAsIReadOnlyDictionary()
    {
        IReadOnlyDictionary<string, int> ro = Build(("a", 1), ("b", 2));

        Assert.Equal(2, ro.Count);
        Assert.Equal(1, ro["a"]);
        Assert.True(ro.ContainsKey("b"));
        Assert.Equal(2, ro.Keys.Count());   // LINQ over the boxed views
        Assert.Equal(2, ro.Values.Count());
        Assert.Equal(2, ro.Select(kvp => kvp.Value).Count());
    }

    // ── Custom hasher overload ────────────────────────────────────────────────

    [Fact]
    public void CustomHasherOverload_Works()
    {
        var dict = new FrozenCelerityDictionary<int, StringXxHash3Hasher>(new[]
        {
            new KeyValuePair<string, int>("alpha", 10),
            new KeyValuePair<string, int>("beta", 20),
        });

        Assert.Equal(10, dict["alpha"]);
        Assert.Equal(20, dict["beta"]);
        Assert.False(dict.ContainsKey("gamma"));
    }

    [Fact]
    public void DefaultConvenienceType_UsesFnv1aAndRoundTrips()
    {
        // The non-hasher convenience type defaults to StringFnV1AHasher.
        FrozenCelerityDictionary<int> dict = Build(("one", 1), ("two", 2), ("three", 3));
        Assert.Equal(1, dict["one"]);
        Assert.Equal(2, dict["two"]);
        Assert.Equal(3, dict["three"]);
    }
}
