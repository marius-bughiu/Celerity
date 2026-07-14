using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Eviction and recency-ordering coverage for <see cref="LruCache{TKey, TValue, THasher}"/>: the
/// least-recently-used entry is the one dropped when a new key arrives at capacity, and every
/// operation that counts as a "use" (a hit via the indexer / <c>TryGet</c>, an overwrite, an
/// <c>AddOrUpdate</c>) moves its entry back to most-recently-used so it survives longer.
/// </summary>
public class LruCacheEvictionTests
{
    private static LruCache<int, int, Int32WangHasher> Cache(int capacity) => new(capacity);

    [Fact]
    public void InsertPastCapacity_EvictsLeastRecentlyUsed()
    {
        var cache = Cache(3);
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30; // full; LRU=1
        cache[4] = 40; // evicts 1

        Assert.Equal(3, cache.Count);
        Assert.False(cache.ContainsKey(1));
        Assert.True(cache.ContainsKey(2));
        Assert.True(cache.ContainsKey(3));
        Assert.True(cache.ContainsKey(4));
    }

    [Fact]
    public void CountNeverExceedsCapacity()
    {
        var cache = Cache(4);
        for (int i = 0; i < 1000; i++)
        {
            cache[i] = i;
            Assert.True(cache.Count <= 4);
        }
        Assert.Equal(4, cache.Count);

        // The four most-recently-inserted keys survive.
        for (int i = 996; i < 1000; i++)
            Assert.True(cache.ContainsKey(i));
        for (int i = 0; i < 996; i++)
            Assert.False(cache.ContainsKey(i));
    }

    [Fact]
    public void GetPromotesEntry_SparingItFromEviction()
    {
        var cache = Cache(3);
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30; // MRU=3,2,1=LRU

        _ = cache[1];   // promote 1 -> MRU=1,3,2=LRU
        cache[4] = 40;  // evicts 2 (the new LRU), NOT 1

        Assert.True(cache.ContainsKey(1));
        Assert.False(cache.ContainsKey(2));
        Assert.True(cache.ContainsKey(3));
        Assert.True(cache.ContainsKey(4));
    }

    [Fact]
    public void TryGetPromotesEntry_SparingItFromEviction()
    {
        var cache = Cache(3);
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;

        Assert.True(cache.TryGet(2, out _)); // promote 2 -> LRU is now 1
        cache[4] = 40;                       // evicts 1

        Assert.False(cache.ContainsKey(1));
        Assert.True(cache.ContainsKey(2));
    }

    [Fact]
    public void OverwriteExistingKey_PromotesToMostRecentlyUsed()
    {
        var cache = Cache(3);
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30; // LRU=1

        cache[1] = 11; // overwrite promotes 1 -> LRU is now 2
        cache[4] = 40; // evicts 2

        Assert.True(cache.ContainsKey(1));
        Assert.Equal(11, cache[1]);
        Assert.False(cache.ContainsKey(2));
        Assert.True(cache.ContainsKey(3));
        Assert.True(cache.ContainsKey(4));
    }

    [Fact]
    public void AddOrUpdate_OnExistingKey_Promotes()
    {
        var cache = Cache(3);
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;

        cache.AddOrUpdate(1, 111); // promote 1; LRU now 2
        cache[4] = 40;             // evicts 2

        Assert.True(cache.ContainsKey(1));
        Assert.False(cache.ContainsKey(2));
    }

    [Fact]
    public void TryAdd_AtCapacity_EvictsLeastRecentlyUsed()
    {
        var cache = Cache(2);
        cache[1] = 10;
        cache[2] = 20; // LRU=1

        Assert.True(cache.TryAdd(3, 30)); // evicts 1
        Assert.False(cache.ContainsKey(1));
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void CapacityOne_BehavesAsSingleSlot()
    {
        var cache = Cache(1);
        cache[1] = 10;
        Assert.Equal(1, cache.Count);
        Assert.Equal(10, cache[1]);

        cache[2] = 20; // evicts 1
        Assert.Equal(1, cache.Count);
        Assert.False(cache.ContainsKey(1));
        Assert.Equal(20, cache[2]);

        cache[2] = 22; // overwrite in place
        Assert.Equal(22, cache[2]);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void EvictedKey_ReportsCorrectLruBeforeEviction()
    {
        var cache = Cache(3);
        cache[5] = 50;
        cache[6] = 60;
        cache[7] = 70;

        Assert.True(cache.TryPeekLeastRecentlyUsed(out int lru, out int lruVal));
        Assert.Equal(5, lru);
        Assert.Equal(50, lruVal);

        cache[8] = 80; // must evict exactly the peeked LRU (5)
        Assert.False(cache.ContainsKey(5));
    }

    [Fact]
    public void RepeatedEvictionChurn_KeepsIndexAndListConsistent()
    {
        // Drive far more inserts than capacity so node slots are recycled many times; the count
        // and the survivor set must stay exactly consistent with a sliding window of the last N keys.
        var cache = Cache(8);
        for (int i = 0; i < 100_000; i++)
            cache[i] = i * 2;

        Assert.Equal(8, cache.Count);
        for (int i = 99_992; i < 100_000; i++)
        {
            Assert.True(cache.ContainsKey(i));
            Assert.Equal(i * 2, cache[i]);
        }
    }
}
