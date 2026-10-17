using System.Collections;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration coverage for <see cref="SieveCache{TKey, TValue, THasher}"/>: entries are yielded in
/// newest-inserted to oldest-inserted order, which hits never change; the struct enumerator honours
/// the version guard for inserts, evictions, removals and clears, while hits and overwrites —
/// which only set a visited flag — leave it valid; and the boxed <see cref="IEnumerable"/> path
/// agrees with the struct fast path.
/// </summary>
public class SieveCacheEnumerationTests
{
    private static SieveCache<int, int, Int32WangHasher> Cache(int capacity = 8) => new(capacity);

    private static List<int> KeysInOrder(SieveCache<int, int, Int32WangHasher> cache)
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
    public void Enumerates_NewestToOldest()
    {
        var cache = Cache();
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;

        Assert.Equal(new[] { 3, 2, 1 }, KeysInOrder(cache));
    }

    [Fact]
    public void Hits_DoNotReorderEnumeration()
    {
        var cache = Cache();
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;

        _ = cache[1];
        Assert.True(cache.TryGet(2, out _));
        cache[1] = 11;

        Assert.Equal(new[] { 3, 2, 1 }, KeysInOrder(cache));
    }

    [Fact]
    public void Eviction_RemovesFromTheQueueAndInsertsAtTheHead()
    {
        var cache = Cache(3);
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;
        _ = cache[1];
        cache[4] = 40; // evicts 2

        Assert.Equal(new[] { 4, 3, 1 }, KeysInOrder(cache));
    }

    [Fact]
    public void Enumeration_YieldsCurrentValues()
    {
        var cache = Cache();
        cache[1] = 10;
        cache[2] = 20;
        cache[1] = 11; // overwrite

        var pairs = new Dictionary<int, int>();
        foreach (var kvp in cache)
            pairs[kvp.Key] = kvp.Value;

        Assert.Equal(11, pairs[1]);
        Assert.Equal(20, pairs[2]);
    }

    [Theory]
    [InlineData("insert")]
    [InlineData("evict")]
    [InlineData("remove")]
    [InlineData("clear")]
    public void StructuralMutation_DuringEnumeration_Throws(string mutation)
    {
        var cache = Cache(3);
        cache[1] = 10;
        cache[2] = 20;
        if (mutation == "evict")
            cache[3] = 30;

        SieveCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        Assert.True(e.MoveNext());

        switch (mutation)
        {
            case "insert": cache[9] = 90; break;
            case "evict": cache[9] = 90; Assert.Equal(3, cache.Count); break;
            case "remove": Assert.True(cache.Remove(1)); break;
            case "clear": cache.Clear(); break;
        }

        try
        {
            e.MoveNext();
            Assert.Fail("MoveNext should have thrown after a structural mutation.");
        }
        catch (InvalidOperationException)
        {
        }
    }

    [Fact]
    public void Hits_AndOverwrites_DuringEnumeration_DoNotThrow()
    {
        var cache = Cache();
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;

        var seen = new List<int>();
        foreach (var kvp in cache)
        {
            seen.Add(kvp.Key);
            _ = cache[1];
            Assert.True(cache.TryGet(2, out _));
            cache[3] = 33;
            cache.AddOrUpdate(2, 22);
            Assert.False(cache.TryAdd(1, 99));
            Assert.True(cache.TryPeek(3, out _));
            Assert.True(cache.ContainsKey(1));
        }

        Assert.Equal(new[] { 3, 2, 1 }, seen);
    }

    [Fact]
    public void FailedRemove_AndEmptyClear_DuringEnumeration_DoNotThrow()
    {
        var cache = Cache();
        cache[1] = 10;

        SieveCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        Assert.False(cache.Remove(42));
        Assert.True(e.MoveNext());
        Assert.False(e.MoveNext());

        var empty = Cache();
        SieveCache<int, int, Int32WangHasher>.Enumerator e2 = empty.GetEnumerator();
        empty.Clear();
        Assert.False(e2.MoveNext());
    }

    [Fact]
    public void Reset_RestartsFromNewest()
    {
        var cache = Cache();
        cache[1] = 10;
        cache[2] = 20;

        SieveCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.Equal(2, e.Current.Key);
        Assert.True(e.MoveNext());
        e.Reset();
        Assert.Equal(default, e.Current);
        Assert.True(e.MoveNext());
        Assert.Equal(2, e.Current.Key);
    }

    [Fact]
    public void Reset_AfterStructuralMutation_Throws()
    {
        var cache = Cache();
        cache[1] = 10;

        SieveCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        cache[2] = 20;
        Assert.Throws<InvalidOperationException>(() => e.Reset());
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
        IEnumerator raw = nonGeneric.GetEnumerator();
        while (raw.MoveNext())
            viaNonGeneric.Add(((KeyValuePair<int, int>)raw.Current).Key);
        Assert.Equal(KeysInOrder(cache), viaNonGeneric);
        (raw as IDisposable)?.Dispose();
    }

    [Fact]
    public void MoveNext_PastEnd_StaysFalse_AndClearsCurrent()
    {
        var cache = Cache();
        cache[1] = 10;

        SieveCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.False(e.MoveNext());
        Assert.Equal(default, e.Current);
        Assert.False(e.MoveNext()); // idempotent after exhaustion
        e.Dispose();
    }
}
