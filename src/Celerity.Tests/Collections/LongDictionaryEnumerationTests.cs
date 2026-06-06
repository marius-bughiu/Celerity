using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// LongDictionary enumeration coverage — the dedicated enumeration suite that
// every other collection ships (IntDictionaryEnumerationTests,
// CelerityDictionaryEnumerationTests, IntSet/CeleritySet/LongSetEnumerationTests)
// but LongDictionary was missing. Mirrors IntDictionaryEnumerationTests for
// 64-bit keys and folds in the long-specific cases from LongSetEnumerationTests
// (extreme / lower-32-bits-share keys, custom hasher, LINQ).
//
// These tests pin down the observable behavior of the struct enumerator and the
// Keys / Values collection views:
// - GetEnumerator yields every key/value pair exactly once (including the
//   out-of-band zero-key entry, which is emitted first).
// - Keys and Values project the same entries as GetEnumerator.
// - The order is unspecified but enumerating into a set must match the
//   dictionary's contents by set equality.
// - Mutating the dictionary during enumeration throws InvalidOperationException
//   on the next MoveNext / Reset — matching BCL Dictionary semantics.
public class LongDictionaryEnumerationTests
{
    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenEmpty()
    {
        var map = new LongDictionary<int>();

        var pairs = new List<KeyValuePair<long, int>>();
        foreach (var kvp in map)
            pairs.Add(kvp);

        Assert.Empty(pairs);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldSingleEntry()
    {
        var map = new LongDictionary<int>();
        map[7L] = 70;

        var pairs = new List<KeyValuePair<long, int>>();
        foreach (var kvp in map)
            pairs.Add(kvp);

        var single = Assert.Single(pairs);
        Assert.Equal(7L, single.Key);
        Assert.Equal(70, single.Value);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEveryEntryExactlyOnce()
    {
        var map = new LongDictionary<long>();
        for (long i = 1; i <= 50; i++)
            map[i] = i * 10;

        var seen = new Dictionary<long, long>();
        foreach (var kvp in map)
        {
            Assert.False(seen.ContainsKey(kvp.Key), $"Duplicate key {kvp.Key} emitted.");
            seen.Add(kvp.Key, kvp.Value);
        }

        Assert.Equal(50, seen.Count);
        for (long i = 1; i <= 50; i++)
            Assert.Equal(i * 10, seen[i]);
    }

    [Fact]
    public void GetEnumerator_ShouldIncludeZeroKeyEntry()
    {
        var map = new LongDictionary<string>();
        map[0L] = "zero";
        map[1L] = "one";
        map[42L] = "answer";

        var seen = new Dictionary<long, string?>();
        foreach (var kvp in map)
            seen[kvp.Key] = kvp.Value;

        Assert.Equal(3, seen.Count);
        Assert.Equal("zero", seen[0L]);
        Assert.Equal("one", seen[1L]);
        Assert.Equal("answer", seen[42L]);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldOnlyZeroKey_WhenItIsTheOnlyEntry()
    {
        var map = new LongDictionary<string>();
        map[0L] = "zero";

        var pairs = new List<KeyValuePair<long, string?>>();
        foreach (var kvp in map)
            pairs.Add(kvp);

        var single = Assert.Single(pairs);
        Assert.Equal(0L, single.Key);
        Assert.Equal("zero", single.Value);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldZeroKeyFirst_WhenPresent()
    {
        var map = new LongDictionary<int>();
        map[11L] = 1;
        map[22L] = 2;
        map[33L] = 3;
        map[0L] = 99;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(0L, enumerator.Current.Key);
        Assert.Equal(99, enumerator.Current.Value);
    }

    [Fact]
    public void GetEnumerator_ShouldReflectRemoval()
    {
        var map = new LongDictionary<int>();
        map[1L] = 10;
        map[2L] = 20;
        map[3L] = 30;
        Assert.True(map.Remove(2L));

        var seen = new Dictionary<long, int>();
        foreach (var kvp in map)
            seen[kvp.Key] = kvp.Value;

        Assert.Equal(2, seen.Count);
        Assert.Equal(10, seen[1L]);
        Assert.Equal(30, seen[3L]);
        Assert.False(seen.ContainsKey(2L));
    }

    [Fact]
    public void GetEnumerator_ShouldReflectZeroKeyRemoval()
    {
        var map = new LongDictionary<int>();
        map[0L] = 100;
        map[1L] = 10;
        map[2L] = 20;
        Assert.True(map.Remove(0L));

        var seen = new Dictionary<long, int>();
        foreach (var kvp in map)
            seen[kvp.Key] = kvp.Value;

        Assert.Equal(2, seen.Count);
        Assert.False(seen.ContainsKey(0L));
        Assert.Equal(10, seen[1L]);
        Assert.Equal(20, seen[2L]);
    }

    [Fact]
    public void GetEnumerator_ShouldReflectClear()
    {
        var map = new LongDictionary<long>();
        for (long i = 0; i < 10; i++)
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
        var map = new LongDictionary<long>(capacity: 4);
        for (long i = 1; i <= 200; i++)
            map[i] = -i;

        var seen = new HashSet<long>();
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
        var map = new LongDictionary<int>();
        map[1L] = 10;
        map[2L] = 20;
        map[3L] = 30;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext()); // OK — one step before mutation.

        map[4L] = 40; // structural mutation

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_WhenZeroKeyInsertedDuringEnumeration()
    {
        var map = new LongDictionary<int>();
        map[1L] = 10;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        map[0L] = 0; // zero-key insert goes through the out-of-band slot.

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterRemoveDuringEnumeration()
    {
        var map = new LongDictionary<int>();
        for (long i = 1; i <= 4; i++)
            map[i] = (int)i;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(map.Remove(2L));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterZeroKeyRemoveDuringEnumeration()
    {
        var map = new LongDictionary<int>();
        map[0L] = 100;
        map[1L] = 10;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(map.Remove(0L));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterClearDuringEnumeration()
    {
        var map = new LongDictionary<int>();
        for (long i = 1; i <= 4; i++)
            map[i] = (int)i;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        map.Clear();

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Reset_ShouldThrow_WhenDictionaryIsMutatedMidEnumeration()
    {
        var map = new LongDictionary<int>();
        map[1L] = 10;

        var enumerator = map.GetEnumerator();
        enumerator.MoveNext();

        map[2L] = 20;

        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void TryAdd_ShouldNotBumpVersion_WhenKeyAlreadyPresent()
    {
        var map = new LongDictionary<int>();
        map[1L] = 10;
        map[2L] = 20;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.False(map.TryAdd(1L, 999)); // duplicate — no structural change.

        // No InvalidOperationException: the version did not change.
        _ = enumerator.MoveNext();
    }

    [Fact]
    public void Remove_ShouldNotBumpVersion_WhenKeyAbsent()
    {
        var map = new LongDictionary<int>();
        map[1L] = 10;
        map[2L] = 20;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.False(map.Remove(99L)); // absent — no structural change.

        _ = enumerator.MoveNext();
    }

    [Fact]
    public void Clear_ShouldNotBumpVersion_WhenAlreadyEmpty()
    {
        var map = new LongDictionary<int>();
        var enumerator = map.GetEnumerator();

        map.Clear(); // already empty — no structural change.

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Keys_ShouldYieldEveryKeyExactlyOnce_IncludingZero()
    {
        var map = new LongDictionary<int>();
        map[0L] = 100;
        map[1L] = 101;
        map[5L] = 105;
        map[42L] = 142;

        var seen = new HashSet<long>();
        foreach (long k in map.Keys)
            Assert.True(seen.Add(k), $"Duplicate key {k} emitted by Keys.");

        Assert.Equal(new HashSet<long> { 0L, 1L, 5L, 42L }, seen);
    }

    [Fact]
    public void Values_ShouldYieldEveryValueExactlyOnce_IncludingZeroKeyValue()
    {
        var map = new LongDictionary<string>();
        map[0L] = "zero";
        map[1L] = "one";
        map[2L] = "two";

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
        var map = new LongDictionary<int>();
        Assert.Equal(0, map.Keys.Count);

        map[0L] = 0;
        map[1L] = 1;
        Assert.Equal(2, map.Keys.Count);

        map.Remove(0L);
        Assert.Equal(1, map.Keys.Count);
    }

    [Fact]
    public void Values_Count_ShouldMatchDictionaryCount()
    {
        var map = new LongDictionary<int>();
        Assert.Equal(0, map.Values.Count);

        map[0L] = 0;
        map[1L] = 1;
        Assert.Equal(2, map.Values.Count);

        map.Remove(0L);
        Assert.Equal(1, map.Values.Count);
    }

    [Fact]
    public void KeyCollection_ShouldBeIterableThroughIEnumerable()
    {
        // Iterating through the IEnumerable<long> interface boxes the struct
        // enumerator; we don't make that zero-allocation, but we do guarantee
        // that it behaves identically to the struct path.
        var map = new LongDictionary<int>();
        map[1L] = 10;
        map[2L] = 20;
        map[0L] = 0;

        IEnumerable<long> keys = map.Keys;
        var seen = new HashSet<long>();
        foreach (long k in keys)
            seen.Add(k);

        Assert.Equal(new HashSet<long> { 0L, 1L, 2L }, seen);
    }

    [Fact]
    public void ValueCollection_ShouldBeIterableThroughIEnumerable()
    {
        var map = new LongDictionary<int>();
        map[1L] = 10;
        map[2L] = 20;
        map[0L] = 99;

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
    public void Dictionary_ShouldBeIterableThroughIEnumerable()
    {
        var map = new LongDictionary<long>();
        map[1L] = 10L;
        map[2L] = 20L;
        map[0L] = 99L;

        IEnumerable<KeyValuePair<long, long>> view = map;
        var seen = new Dictionary<long, long>();
        foreach (var kvp in view)
            seen[kvp.Key] = kvp.Value;

        Assert.Equal(3, seen.Count);
        Assert.Equal(99L, seen[0L]);
        Assert.Equal(10L, seen[1L]);
        Assert.Equal(20L, seen[2L]);
    }

    [Fact]
    public void Dictionary_ShouldBeIterableThroughNonGenericIEnumerable()
    {
        var map = new LongDictionary<long>();
        map[10L] = 100L;
        map[20L] = 200L;

        IEnumerable view = map;
        var seen = new Dictionary<long, long>();
        foreach (object item in view)
        {
            var kvp = (KeyValuePair<long, long>)item;
            seen[kvp.Key] = kvp.Value;
        }

        Assert.Equal(2, seen.Count);
        Assert.Equal(100L, seen[10L]);
        Assert.Equal(200L, seen[20L]);
    }

    [Fact]
    public void Enumerator_ShouldBeReusableViaReset()
    {
        var map = new LongDictionary<int>();
        map[1L] = 10;
        map[2L] = 20;

        var enumerator = map.GetEnumerator();
        int firstPass = 0;
        while (enumerator.MoveNext()) firstPass++;

        enumerator.Reset();
        int secondPass = 0;
        while (enumerator.MoveNext()) secondPass++;

        Assert.Equal(2, firstPass);
        Assert.Equal(firstPass, secondPass);
    }

    [Fact]
    public void Enumerator_ShouldReturnDefault_AfterEnumerationIsExhausted()
    {
        var map = new LongDictionary<int>();
        map[7L] = 70;

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(7L, enumerator.Current.Key);
        Assert.Equal(70, enumerator.Current.Value);
        Assert.False(enumerator.MoveNext());
        Assert.Equal(default(KeyValuePair<long, int>), enumerator.Current);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEveryEntry_WithCustomHasher()
    {
        var map = new LongDictionary<long, Int64Murmur3Hasher>();
        map[0L] = 0L;
        for (long i = 1; i <= 25; i++)
            map[i] = i * 10;

        var seen = new Dictionary<long, long>();
        foreach (var kvp in map)
            seen[kvp.Key] = kvp.Value;

        Assert.Equal(26, seen.Count);
        Assert.Equal(0L, seen[0L]);
        for (long i = 1; i <= 25; i++)
            Assert.Equal(i * 10, seen[i]);
    }

    [Fact]
    public void GetEnumerator_ShouldSupportLinq()
    {
        var map = new LongDictionary<long>();
        for (long i = 1; i <= 5; i++)
            map[i] = i;
        map[0L] = 0L;

        Assert.Equal(6, map.Count());
        Assert.Equal(15L, map.Sum(kvp => kvp.Key));   // 0+1+2+3+4+5
        bool linqHasZero = map.Any(kvp => kvp.Key == 0L);
        bool linqHasFive = map.Any(kvp => kvp.Key == 5L);
        bool linqHas99 = map.Any(kvp => kvp.Key == 99L);
        Assert.True(linqHasZero);
        Assert.True(linqHasFive);
        Assert.False(linqHas99);
    }

    [Fact]
    public void GetEnumerator_ShouldHandleExtremeKeyValues()
    {
        // Long-specific: confirm the enumerator round-trips the full 64-bit key
        // range, including the lower-32-bits-share case that's a tripwire for
        // accidental int-truncation on the probe / enumeration path.
        var map = new LongDictionary<long>();
        long[] keys = {
            long.MaxValue,
            long.MinValue,
            (long)int.MaxValue + 1L,
            (long)int.MinValue - 1L,
            -1L,
            0x0000_0001_0000_0001L,
            0x0000_0002_0000_0001L,
        };

        for (int i = 0; i < keys.Length; i++)
            map[keys[i]] = keys[i] ^ 0x5555_5555_5555_5555L;

        var seen = new Dictionary<long, long>();
        foreach (var kvp in map)
            seen[kvp.Key] = kvp.Value;

        Assert.Equal(keys.Length, seen.Count);
        foreach (long key in keys)
        {
            Assert.True(seen.ContainsKey(key), $"Key {key} missing from enumeration.");
            Assert.Equal(key ^ 0x5555_5555_5555_5555L, seen[key]);
        }
    }

    [Fact]
    public void KeyCollection_ShouldRoundTrip_ThroughNonGenericIEnumerable()
    {
        // The non-generic System.Collections.IEnumerable path (object Current,
        // Reset) is distinct from the generic IEnumerable<long> path above.
        var map = new LongDictionary<int>();
        for (long i = 1; i <= 4; i++) map[i] = (int)(i * 10);

        var (first, second) = DrainNonGenericTwice<long>(map.Keys);

        first.Sort();
        second.Sort();
        Assert.Equal(new[] { 1L, 2L, 3L, 4L }, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void ValueCollection_ShouldRoundTrip_ThroughNonGenericIEnumerable()
    {
        var map = new LongDictionary<int>();
        for (long i = 1; i <= 4; i++) map[i] = (int)(i * 10);

        var (first, second) = DrainNonGenericTwice<int>(map.Values);

        first.Sort();
        second.Sort();
        Assert.Equal(new[] { 10, 20, 30, 40 }, first);
        Assert.Equal(first, second);
    }

    // Drives a struct view through its boxed non-generic IEnumerable/IEnumerator
    // surface (GetEnumerator -> object Current), then Reset()s and drains again.
    private static (List<T> first, List<T> second) DrainNonGenericTwice<T>(IEnumerable view)
    {
        IEnumerator e = view.GetEnumerator();
        var first = new List<T>();
        while (e.MoveNext()) first.Add((T)e.Current!);
        e.Reset();
        var second = new List<T>();
        while (e.MoveNext()) second.Add((T)e.Current!);
        return (first, second);
    }
}
