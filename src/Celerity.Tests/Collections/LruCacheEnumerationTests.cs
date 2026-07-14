using System.Collections;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration coverage for <see cref="LruCache{TKey, TValue, THasher}"/>: entries are yielded in
/// most-recently-used to least-recently-used order, the struct enumerator honours the version guard
/// (including the fact that a mutating read reorders the list and therefore invalidates an active
/// enumerator), and the boxed <see cref="IEnumerable"/> path agrees with the struct fast path.
/// </summary>
public class LruCacheEnumerationTests
{
    private static LruCache<int, int, Int32WangHasher> Cache(int capacity = 8) => new(capacity);

    private static List<int> KeysInOrder(LruCache<int, int, Int32WangHasher> cache)
    {
        var keys = new List<int>();
        foreach (var kvp in cache)
            keys.Add(kvp.Key);
        return keys;
    }

    [Fact]
    public void EmptyCache_YieldsNothing()
    {
        var cache = Cache();
        Assert.Empty(KeysInOrder(cache));
    }

    [Fact]
    public void Enumerates_MostRecentToLeastRecent()
    {
        var cache = Cache();
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;

        Assert.Equal(new[] { 3, 2, 1 }, KeysInOrder(cache));
    }

    [Fact]
    public void Get_ReordersEnumerationSequence()
    {
        var cache = Cache();
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;

        _ = cache[1]; // promote 1 to MRU

        Assert.Equal(new[] { 1, 3, 2 }, KeysInOrder(cache));
    }

    [Fact]
    public void Enumeration_YieldsCurrentValues()
    {
        var cache = Cache();
        cache[1] = 10;
        cache[2] = 20;

        var map = new Dictionary<int, int>();
        foreach (var kvp in cache)
            map[kvp.Key] = kvp.Value;

        Assert.Equal(10, map[1]);
        Assert.Equal(20, map[2]);
    }

    [Fact]
    public void StructuralMutation_DuringEnumeration_Throws()
    {
        var cache = Cache();
        cache[1] = 10;
        cache[2] = 20;

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var entry in cache)
                cache[3] = 30; // insert mid-enumeration
        });
    }

    [Fact]
    public void MutatingRead_DuringEnumeration_Throws()
    {
        // A get is a use: it reorders the recency list, so it must invalidate an in-progress
        // enumerator exactly like a structural change would.
        var cache = Cache();
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var entry in cache)
                _ = cache[1]; // promotes 1 -> reorders -> version bump
        });
    }

    [Fact]
    public void Peek_DuringEnumeration_DoesNotThrow()
    {
        // TryPeek / ContainsKey do not change recency, so they must not invalidate the enumerator.
        var cache = Cache();
        cache[1] = 10;
        cache[2] = 20;

        int seen = 0;
        foreach (var entry in cache)
        {
            Assert.True(cache.TryPeek(1, out _));
            Assert.True(cache.ContainsKey(2));
            seen++;
        }
        Assert.Equal(2, seen);
    }

    [Fact]
    public void GetOfAlreadyMostRecentEntry_DoesNotInvalidateEnumerator()
    {
        // Reading the head is a no-op for recency order, so it must not bump the version.
        var cache = Cache();
        cache[1] = 10;
        cache[2] = 20; // head == 2

        int seen = 0;
        foreach (var entry in cache)
        {
            _ = cache[2]; // 2 is already MRU -> no reorder
            seen++;
        }
        Assert.Equal(2, seen);
    }

    [Fact]
    public void Reset_RestartsFromMostRecentlyUsed()
    {
        var cache = Cache();
        cache[1] = 10;
        cache[2] = 20;

        LruCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.Equal(2, e.Current.Key);
        e.Reset();
        Assert.True(e.MoveNext());
        Assert.Equal(2, e.Current.Key);
    }

    [Fact]
    public void BoxedEnumerable_AgreesWithStructPath()
    {
        var cache = Cache();
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;

        IEnumerable<KeyValuePair<int, int>> boxed = cache;
        var viaInterface = boxed.Select(kvp => kvp.Key).ToList();
        Assert.Equal(KeysInOrder(cache), viaInterface);

        IEnumerable nonGeneric = cache;
        var viaNonGeneric = new List<int>();
        foreach (KeyValuePair<int, int> kvp in nonGeneric)
            viaNonGeneric.Add(kvp.Key);
        Assert.Equal(KeysInOrder(cache), viaNonGeneric);
    }

    [Fact]
    public void MoveNext_PastEnd_StaysFalse()
    {
        var cache = Cache();
        cache[1] = 10;

        LruCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.False(e.MoveNext());
        Assert.False(e.MoveNext()); // idempotent after exhaustion
    }
}
