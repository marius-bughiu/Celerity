using System;
using System.Collections.Generic;
using System.Linq;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// Milestone 1.2.0 — issue #18: CelerityMultiMap<TKey, TValue, THasher>, the
// one-to-many counterpart to CelerityDictionary. This suite covers the core
// surface: grouping Adds, indexer/TryGetValues group views (empty-on-miss),
// Contains(key, value) / ContainsValue / CountValues, the two removal shapes
// (Remove(key, value) and RemoveAll(key)), Clear, Count vs ValueCount, the
// out-of-band default-key group (0, null, Guid.Empty), AddRange, the
// IEnumerable<KeyValuePair<,>> grouping constructor, and the ILookup surface.
public class CelerityMultiMapTests
{
    [Fact]
    public void Add_ShouldGroupMultipleValuesUnderOneKey()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 1);
        map.Add("a", 2);
        map.Add("a", 3);

        Assert.Equal(1, map.Count);          // one distinct key
        Assert.Equal(3, map.ValueCount);     // three values
        Assert.Equal(new[] { 1, 2, 3 }, map["a"].ToArray());
    }

    [Fact]
    public void Add_ShouldKeepDuplicateValuesUnderOneKey()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 7);
        map.Add("a", 7);

        Assert.Equal(1, map.Count);
        Assert.Equal(2, map.ValueCount);
        Assert.Equal(new[] { 7, 7 }, map["a"].ToArray());
    }

    [Fact]
    public void Add_ShouldKeepValuesPerKeySeparate()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 1);
        map.Add("b", 2);
        map.Add("a", 3);

        Assert.Equal(2, map.Count);
        Assert.Equal(3, map.ValueCount);
        Assert.Equal(new[] { 1, 3 }, map["a"].ToArray());
        Assert.Equal(new[] { 2 }, map["b"].ToArray());
    }

    [Fact]
    public void Indexer_ShouldReturnEmptyGroup_ForMissingKey()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 1);

        CelerityMultiMap<string, int, StringFnV1AHasher>.ValueGroup group = map["missing"];

        Assert.Empty(group);
        Assert.Empty(group.ToArray());
    }

    [Fact]
    public void Indexer_ShouldExposeCountAndIndexedAccess()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 10);
        map.Add("a", 20);

        var group = map["a"];
        Assert.Equal(2, group.Count);
        Assert.Equal(10, group[0]);
        Assert.Equal(20, group[1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => group[2]);
    }

    [Fact]
    public void TryGetValues_ShouldReturnFalseAndEmpty_ForMissingKey()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();

        bool found = map.TryGetValues("missing", out var values);

        Assert.False(found);
        Assert.Empty(values);
    }

    [Fact]
    public void TryGetValues_ShouldReturnTrueAndGroup_ForPresentKey()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 1);
        map.Add("a", 2);

        bool found = map.TryGetValues("a", out var values);

        Assert.True(found);
        Assert.Equal(new[] { 1, 2 }, values.ToArray());
    }

    [Fact]
    public void ContainsKey_ShouldReflectPresence()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 1);

        Assert.True(map.ContainsKey("a"));
        Assert.False(map.ContainsKey("b"));
    }

    [Fact]
    public void Contains_ShouldFindValueWithinKeyGroup()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 1);
        map.Add("a", 2);

        Assert.True(map.Contains("a", 1));
        Assert.True(map.Contains("a", 2));
        Assert.False(map.Contains("a", 3));
        Assert.False(map.Contains("b", 1));
    }

    [Fact]
    public void ContainsValue_ShouldScanAllGroups()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 1);
        map.Add("b", 2);

        Assert.True(map.ContainsValue(1));
        Assert.True(map.ContainsValue(2));
        Assert.False(map.ContainsValue(99));
    }

    [Fact]
    public void CountValues_ShouldReturnGroupSizeOrZero()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 1);
        map.Add("a", 2);

        Assert.Equal(2, map.CountValues("a"));
        Assert.Equal(0, map.CountValues("missing"));
    }

    [Fact]
    public void Remove_ShouldRemoveSingleOccurrence_AndKeepKeyWhenGroupNonEmpty()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 1);
        map.Add("a", 2);

        Assert.True(map.Remove("a", 1));
        Assert.Equal(1, map.Count);
        Assert.Equal(1, map.ValueCount);
        Assert.Equal(new[] { 2 }, map["a"].ToArray());
    }

    [Fact]
    public void Remove_ShouldRemoveOnlyFirstOccurrence_OfDuplicateValue()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 5);
        map.Add("a", 5);

        Assert.True(map.Remove("a", 5));
        Assert.Equal(new[] { 5 }, map["a"].ToArray());
        Assert.Equal(1, map.ValueCount);
    }

    [Fact]
    public void Remove_ShouldRemoveKey_WhenLastValueRemoved()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 1);

        Assert.True(map.Remove("a", 1));
        Assert.Equal(0, map.Count);
        Assert.Equal(0, map.ValueCount);
        Assert.False(map.ContainsKey("a"));
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenKeyOrValueAbsent()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 1);

        Assert.False(map.Remove("a", 999));   // key present, value absent
        Assert.False(map.Remove("b", 1));     // key absent
        Assert.Equal(1, map.ValueCount);      // unchanged
    }

    [Fact]
    public void RemoveAll_ShouldRemoveKeyAndAllValues()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 1);
        map.Add("a", 2);
        map.Add("b", 3);

        Assert.True(map.RemoveAll("a"));
        Assert.Equal(1, map.Count);
        Assert.Equal(1, map.ValueCount);
        Assert.False(map.ContainsKey("a"));
        Assert.True(map.ContainsKey("b"));
    }

    [Fact]
    public void RemoveAll_ShouldReturnFalse_ForMissingKey()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        Assert.False(map.RemoveAll("missing"));
    }

    [Fact]
    public void Clear_ShouldResetCountsAndContents()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 1);
        map.Add("b", 2);
        map.Add(null!, 3);

        map.Clear();

        Assert.Equal(0, map.Count);
        Assert.Equal(0, map.ValueCount);
        Assert.False(map.ContainsKey("a"));
        Assert.False(map.ContainsKey(null!));
        Assert.Empty(map["a"].ToArray());
    }

    [Fact]
    public void AddRange_ShouldAppendAllValues()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 1);
        map.AddRange("a", new[] { 2, 3, 4 });

        Assert.Equal(new[] { 1, 2, 3, 4 }, map["a"].ToArray());
        Assert.Equal(4, map.ValueCount);
        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void AddRange_ShouldThrow_ForNullValues()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        Assert.Throws<ArgumentNullException>(() => map.AddRange("a", null!));
    }

    // ---------------- default-key (out-of-band) handling ----------------

    [Fact]
    public void NullKey_ShouldRoundTrip_AsOrdinaryKey()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add(null!, 1);
        map.Add(null!, 2);
        map.Add("a", 9);

        Assert.True(map.ContainsKey(null!));
        Assert.Equal(new[] { 1, 2 }, map[null!].ToArray());
        Assert.Equal(2, map.Count);
        Assert.Equal(3, map.ValueCount);
    }

    [Fact]
    public void NullKey_Remove_ShouldRemoveSingleAndThenKey()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add(null!, 1);
        map.Add(null!, 2);

        Assert.True(map.Remove(null!, 1));
        Assert.Equal(new[] { 2 }, map[null!].ToArray());

        Assert.True(map.Remove(null!, 2));
        Assert.False(map.ContainsKey(null!));
        Assert.Equal(0, map.Count);
    }

    [Fact]
    public void ZeroKey_ShouldRoundTrip_ForIntKeys()
    {
        var map = new CelerityMultiMap<int, string, Int32WangNaiveHasher>();
        map.Add(0, "zero-a");
        map.Add(0, "zero-b");
        map.Add(1, "one");

        Assert.True(map.ContainsKey(0));
        Assert.Equal(new[] { "zero-a", "zero-b" }, map[0].ToArray());
        Assert.Equal(2, map.Count);
    }

    [Fact]
    public void EmptyGuidKey_ShouldRoundTrip()
    {
        var map = new CelerityMultiMap<Guid, int, GuidHasher>();
        map.Add(Guid.Empty, 1);
        var id = Guid.NewGuid();
        map.Add(id, 2);

        Assert.Equal(new[] { 1 }, map[Guid.Empty].ToArray());
        Assert.Equal(new[] { 2 }, map[id].ToArray());
        Assert.Equal(2, map.Count);
    }

    // ---------------- constructor from IEnumerable<KeyValuePair<,>> ----------------

    [Fact]
    public void Constructor_FromPairs_ShouldGroupByKey()
    {
        var source = new[]
        {
            new KeyValuePair<string, int>("a", 1),
            new KeyValuePair<string, int>("b", 2),
            new KeyValuePair<string, int>("a", 3),
        };

        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>(source);

        Assert.Equal(2, map.Count);
        Assert.Equal(3, map.ValueCount);
        Assert.Equal(new[] { 1, 3 }, map["a"].ToArray());
        Assert.Equal(new[] { 2 }, map["b"].ToArray());
    }

    [Fact]
    public void Constructor_FromPairs_ShouldThrow_ForNullSource()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            new CelerityMultiMap<string, int, StringFnV1AHasher>(null!));
        Assert.Equal("source", ex.ParamName);
    }

    // ---------------- ILookup<TKey, TValue?> surface ----------------

    [Fact]
    public void ILookup_Surface_ShouldBehaveLikeAMultiMap()
    {
        var map = new CelerityMultiMap<string, int, StringFnV1AHasher>();
        map.Add("a", 1);
        map.Add("a", 2);
        map.Add("b", 3);

        ILookup<string, int> lookup = map;

        Assert.Equal(2, lookup.Count);
        Assert.True(lookup.Contains("a"));
        Assert.False(lookup.Contains("z"));
        Assert.Equal(new[] { 1, 2 }, lookup["a"].ToArray());
        Assert.Empty(lookup["missing"]);   // empty for absent key, no throw

        // Flow through LINQ over IGrouping<,>.
        Dictionary<string, int> sums = lookup.ToDictionary(g => g.Key, g => g.Sum());
        Assert.Equal(3, sums["a"]);
        Assert.Equal(3, sums["b"]);
    }

    [Fact]
    public void GrowthUnderManyKeys_ShouldPreserveAllGroups()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>(capacity: 4);
        for (int i = 0; i < 500; i++)
        {
            map.Add(i, i);
            map.Add(i, i + 1000);
        }

        Assert.Equal(500, map.Count);
        Assert.Equal(1000, map.ValueCount);
        for (int i = 0; i < 500; i++)
            Assert.Equal(new[] { i, i + 1000 }, map[i].ToArray());
    }

    [Fact]
    public void Remove_ShouldReturnFalse_ForAbsentDefaultKey()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();

        // default key (0) group was never created.
        Assert.False(map.Remove(0, 123));
        Assert.False(map.RemoveAll(0));
    }

    [Fact]
    public void Remove_ShouldReturnFalse_ForAbsentValue_UnderDefaultKey()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        map.Add(0, 1);
        map.Add(0, 2);

        Assert.False(map.Remove(0, 999)); // value not in the default-key group
        Assert.True(map.Remove(0, 1));
        Assert.True(map.Remove(0, 2));    // empties the group -> drops the key
        Assert.False(map.ContainsKey(0));
    }

    [Fact]
    public void Remove_ShouldReturnFalse_ForAbsentValue_UnderPresentKey()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();
        map.Add(7, 1);

        Assert.False(map.Remove(7, 999)); // key present, value absent
        Assert.True(map.ContainsKey(7));
    }

    [Fact]
    public void Clear_ShouldBeNoOp_WhenAlreadyEmpty()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();

        map.Clear(); // _count == 0 early-return path

        Assert.Empty(map.Keys);
        Assert.Equal(0, map.ValueCount);
    }

    [Fact]
    public void ValueGroup_ForAbsentKey_ShouldBeEmpty_AndThrowOnIndex()
    {
        var map = new CelerityMultiMap<int, int, Int32WangNaiveHasher>();

        // The indexer returns an empty group (null inner list) for an absent key.
        var group = map[999];

        Assert.Equal(0, group.Count); // _group?.Count ?? 0 — the null branch
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = group[0]); // _group is null branch
    }
}
