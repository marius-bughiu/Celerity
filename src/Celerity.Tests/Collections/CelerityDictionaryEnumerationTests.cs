using System.Collections.Generic;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// Milestone 1.1.0 — issue #10: Keys / Values / GetEnumerator on CelerityDictionary.
//
// Mirror of IntDictionaryEnumerationTests for the generic CelerityDictionary.
// Exercises the struct enumerators, KeyCollection, ValueCollection, the
// out-of-band default-key entry (including null keys for reference types),
// and mid-enumeration mutation detection. Two underlying hashers are used:
// Int32WangNaiveHasher for int-keyed maps and StringFnV1AHasher for the null
// default-key path.
public class CelerityDictionaryEnumerationTests
{
    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenEmpty()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();

        var pairs = new List<KeyValuePair<int, int>>();
        foreach (var kvp in map)
            pairs.Add(kvp);

        Assert.Empty(pairs);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldSingleEntry()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[7] = 70;

        var pairs = new List<KeyValuePair<int, int>>();
        foreach (var kvp in map)
            pairs.Add(kvp);

        var single = Assert.Single(pairs);
        Assert.Equal(7, single.Key);
        Assert.Equal(70, single.Value);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEveryEntryExactlyOnce()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 50; i++)
            map[i] = i * 10;

        var seen = new Dictionary<int, int>();
        foreach (var kvp in map)
        {
            Assert.False(seen.ContainsKey(kvp.Key), $"Duplicate key {kvp.Key} emitted.");
            seen.Add(kvp.Key, kvp.Value);
        }

        Assert.Equal(50, seen.Count);
        for (int i = 1; i <= 50; i++)
            Assert.Equal(i * 10, seen[i]);
    }

    [Fact]
    public void GetEnumerator_ShouldIncludeDefaultKeyEntry_ForIntKey()
    {
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
        map[0] = "zero";
        map[1] = "one";
        map[42] = "answer";

        var seen = new Dictionary<int, string?>();
        foreach (var kvp in map)
            seen[kvp.Key] = kvp.Value;

        Assert.Equal(3, seen.Count);
        Assert.Equal("zero", seen[0]);
        Assert.Equal("one", seen[1]);
        Assert.Equal("answer", seen[42]);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldOnlyDefaultKey_WhenItIsTheOnlyEntry()
    {
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
        map[0] = "zero";

        var pairs = new List<KeyValuePair<int, string?>>();
        foreach (var kvp in map)
            pairs.Add(kvp);

        var single = Assert.Single(pairs);
        Assert.Equal(0, single.Key);
        Assert.Equal("zero", single.Value);
    }

    [Fact]
    public void GetEnumerator_ShouldIncludeNullKeyEntry_ForStringKey()
    {
        // Reference-type keys: default(string) is null, so the null key lives
        // in the out-of-band slot and must be yielded as part of enumeration.
        var map = new CelerityDictionary<string, int, StringFnV1AHasher>();
        map[null!] = 999;
        map["alpha"] = 1;
        map["beta"] = 2;

        var seen = new Dictionary<string, int>();
        bool sawNullKey = false;
        int nullKeyValue = -1;
        foreach (var kvp in map)
        {
            if (kvp.Key is null)
            {
                sawNullKey = true;
                nullKeyValue = kvp.Value;
            }
            else
            {
                seen[kvp.Key] = kvp.Value;
            }
        }

        Assert.True(sawNullKey, "Expected the null-key entry to be yielded.");
        Assert.Equal(999, nullKeyValue);
        Assert.Equal(2, seen.Count);
        Assert.Equal(1, seen["alpha"]);
        Assert.Equal(2, seen["beta"]);
    }

    [Fact]
    public void GetEnumerator_ShouldReflectRemoval()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;
        map[2] = 20;
        map[3] = 30;
        map.Remove(2);

        var seen = new Dictionary<int, int>();
        foreach (var kvp in map)
            seen[kvp.Key] = kvp.Value;

        Assert.Equal(2, seen.Count);
        Assert.Equal(10, seen[1]);
        Assert.Equal(30, seen[3]);
        Assert.False(seen.ContainsKey(2));
    }

    [Fact]
    public void GetEnumerator_ShouldReflectClear()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 0; i < 10; i++)
            map[i] = i;
        map.Clear();

        int count = 0;
        foreach (var _ in map)
            count++;

        Assert.Equal(0, count);
    }

    [Fact]
    public void GetEnumerator_ShouldSurviveResize()
    {
        // Force multiple resizes from a tiny starting capacity and assert the
        // enumerator still sees every entry.
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>(capacity: 4);
        for (int i = 1; i <= 200; i++)
            map[i] = -i;

        var seen = new HashSet<int>();
        foreach (var kvp in map)
        {
            Assert.Equal(-kvp.Key, kvp.Value);
            Assert.True(seen.Add(kvp.Key), $"Duplicate key {kvp.Key} emitted.");
        }

        Assert.Equal(200, seen.Count);
    }

    [Fact]
    public void MoveNext_ShouldThrow_WhenDictionaryIsMutatedMidEnumeration()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;
        map[2] = 20;
        map[3] = 30;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext()); // OK — one step before mutation.

        map[4] = 40; // structural mutation

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_WhenDefaultKeyInsertedDuringEnumeration()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        map[0] = 0; // default-key insert goes through the out-of-band slot.

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterClearDuringEnumeration()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 4; i++)
            map[i] = i;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        map.Clear();

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterRemoveDuringEnumeration()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;
        map[2] = 20;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        map.Remove(2);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Reset_ShouldThrow_WhenDictionaryIsMutatedMidEnumeration()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;

        var enumerator = map.GetEnumerator();
        enumerator.MoveNext();

        map[2] = 20;

        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void Keys_ShouldYieldEveryKeyExactlyOnce_IncludingDefaultKey()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[0] = 100;
        map[1] = 101;
        map[5] = 105;
        map[42] = 142;

        var seen = new HashSet<int>();
        foreach (int k in map.Keys)
            Assert.True(seen.Add(k), $"Duplicate key {k} emitted by Keys.");

        Assert.Equal(4, seen.Count);
        Assert.Contains(0, seen);
        Assert.Contains(1, seen);
        Assert.Contains(5, seen);
        Assert.Contains(42, seen);
    }

    [Fact]
    public void Keys_ShouldIncludeNullKey_ForStringKey()
    {
        var map = new CelerityDictionary<string, int, StringFnV1AHasher>();
        map[null!] = 0;
        map["alpha"] = 1;

        var seen = new HashSet<string?>();
        foreach (string k in map.Keys)
            seen.Add(k);

        Assert.Equal(2, seen.Count);
        Assert.Contains(null, seen);
        Assert.Contains("alpha", seen);
    }

    [Fact]
    public void Values_ShouldYieldEveryValueExactlyOnce_IncludingDefaultKeyValue()
    {
        var map = new CelerityDictionary<int, string, Int32WangNaiveHasher>();
        map[0] = "zero";
        map[1] = "one";
        map[2] = "two";

        var seen = new List<string?>();
        foreach (string? v in map.Values)
            seen.Add(v);

        Assert.Equal(3, seen.Count);
        Assert.Contains("zero", seen);
        Assert.Contains("one", seen);
        Assert.Contains("two", seen);
    }

    [Fact]
    public void Keys_Count_ShouldMatchDictionaryCount()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        Assert.Equal(0, map.Keys.Count);

        map[0] = 0;
        map[1] = 1;
        Assert.Equal(2, map.Keys.Count);

        map.Remove(0);
        Assert.Equal(1, map.Keys.Count);
    }

    [Fact]
    public void Values_Count_ShouldMatchDictionaryCount()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        Assert.Equal(0, map.Values.Count);

        map[0] = 0;
        map[1] = 1;
        Assert.Equal(2, map.Values.Count);

        map.Remove(0);
        Assert.Equal(1, map.Values.Count);
    }

    [Fact]
    public void KeyCollection_ShouldBeIterableThroughIEnumerable()
    {
        // Iterating through the IEnumerable<TKey> interface boxes the struct
        // enumerator; we don't make that zero-allocation, but we do guarantee
        // that it behaves identically to the struct path.
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;
        map[2] = 20;
        map[0] = 0;

        IEnumerable<int> keys = map.Keys;
        var seen = new HashSet<int>();
        foreach (int k in keys)
            seen.Add(k);

        Assert.Equal(new HashSet<int> { 0, 1, 2 }, seen);
    }

    [Fact]
    public void ValueCollection_ShouldBeIterableThroughIEnumerable()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;
        map[2] = 20;
        map[0] = 99;

        IEnumerable<int> values = map.Values;
        var seen = new List<int>();
        foreach (int v in values)
            seen.Add(v);

        Assert.Equal(3, seen.Count);
        Assert.Contains(10, seen);
        Assert.Contains(20, seen);
        Assert.Contains(99, seen);
    }

    [Fact]
    public void Enumerator_ShouldBeReusableViaReset()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;
        map[2] = 20;

        var enumerator = map.GetEnumerator();
        int firstPass = 0;
        while (enumerator.MoveNext()) firstPass++;

        enumerator.Reset();
        int secondPass = 0;
        while (enumerator.MoveNext()) secondPass++;

        Assert.Equal(2, firstPass);
        Assert.Equal(firstPass, secondPass);
    }
}
