using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Eviction and frequency-ordering coverage for <see cref="LfuCache{TKey, TValue, THasher}"/>: the
/// least-frequently-used entry is the one dropped when a new key arrives at capacity, ties between
/// equally-frequent entries break by least-recently-used, and every operation that counts as a "use"
/// (a hit via the indexer / <c>TryGet</c>, an overwrite, an <c>AddOrUpdate</c>) raises its entry's
/// frequency so it survives longer. The scan-resistance test at the bottom is the property the type
/// exists for.
/// </summary>
public class LfuCacheEvictionTests
{
    private static LfuCache<int, int, Int32WangHasher> Cache(int capacity) => new(capacity);

    [Fact]
    public void InsertPastCapacity_EvictsLeastFrequentlyUsed()
    {
        var cache = Cache(3);
        cache.Add(1, 10);
        cache.Add(2, 20);
        cache.Add(3, 30);
        _ = cache[1];   // 1 -> frequency 2
        _ = cache[2];   // 2 -> frequency 2
                        // 3 is alone at frequency 1
        cache.Add(4, 40);

        Assert.Equal(3, cache.Count);
        Assert.False(cache.ContainsKey(3));
        Assert.True(cache.ContainsKey(1));
        Assert.True(cache.ContainsKey(2));
        Assert.True(cache.ContainsKey(4));
    }

    [Fact]
    public void EqualFrequencies_BreakTiesByLeastRecentlyUsed()
    {
        var cache = Cache(3);
        cache.Add(1, 10);
        cache.Add(2, 20);
        cache.Add(3, 30); // all at frequency 1; 1 is the oldest

        cache.Add(4, 40); // evicts 1
        Assert.False(cache.ContainsKey(1));

        cache.Add(5, 50); // now 2 is the oldest at frequency 1
        Assert.False(cache.ContainsKey(2));
        Assert.True(cache.ContainsKey(3));
        Assert.True(cache.ContainsKey(4));
        Assert.True(cache.ContainsKey(5));
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
    }

    [Fact]
    public void GetRaisesFrequency_SparingEntryFromEviction()
    {
        var cache = Cache(3);
        cache.Add(1, 10);
        cache.Add(2, 20);
        cache.Add(3, 30);

        _ = cache[1];       // 1 climbs clear of the frequency-1 crowd

        cache.Add(4, 40);   // evicts 2 (oldest at frequency 1), not 1
        Assert.True(cache.ContainsKey(1));
        Assert.False(cache.ContainsKey(2));
    }

    [Fact]
    public void TryGetRaisesFrequency_SparingEntryFromEviction()
    {
        var cache = Cache(3);
        cache.Add(1, 10);
        cache.Add(2, 20);
        cache.Add(3, 30);

        Assert.True(cache.TryGet(1, out _));

        cache.Add(4, 40);
        Assert.True(cache.ContainsKey(1));
        Assert.False(cache.ContainsKey(2));
    }

    [Fact]
    public void OverwriteExistingKey_RaisesFrequency()
    {
        var cache = Cache(3);
        cache.Add(1, 10);
        cache.Add(2, 20);
        cache.Add(3, 30);

        cache[1] = 11;      // an overwrite is a use

        Assert.True(cache.TryGetFrequency(1, out long frequency));
        Assert.Equal(2L, frequency);

        cache.Add(4, 40);
        Assert.True(cache.ContainsKey(1));
        Assert.Equal(11, cache.TryPeek(1, out int v) ? v : 0);
        Assert.False(cache.ContainsKey(2));
    }

    [Fact]
    public void AddOrUpdate_OnExistingKey_RaisesFrequency()
    {
        var cache = Cache(3);
        cache.Add(1, 10);
        cache.Add(2, 20);
        cache.Add(3, 30);

        cache.AddOrUpdate(1, 11);

        cache.Add(4, 40);
        Assert.True(cache.ContainsKey(1));
        Assert.False(cache.ContainsKey(2));
    }

    [Fact]
    public void TryAdd_AtCapacity_EvictsLeastFrequentlyUsed()
    {
        var cache = Cache(2);
        cache.Add(1, 10);
        cache.Add(2, 20);
        _ = cache[1];       // 1 at frequency 2, 2 at frequency 1

        Assert.True(cache.TryAdd(3, 30));
        Assert.Equal(2, cache.Count);
        Assert.True(cache.ContainsKey(1));
        Assert.False(cache.ContainsKey(2));
        Assert.True(cache.ContainsKey(3));
    }

    [Fact]
    public void CapacityOne_BehavesAsSingleSlot()
    {
        var cache = Cache(1);
        cache[1] = 10;
        Assert.Equal(1, cache.Count);
        Assert.Equal(10, cache[1]);

        cache[2] = 20;
        Assert.Equal(1, cache.Count);
        Assert.False(cache.ContainsKey(1));
        Assert.Equal(20, cache[2]);
    }

    [Fact]
    public void EvictedKey_ReportsCorrectVictimBeforeEviction()
    {
        var cache = Cache(3);
        cache.Add(1, 10);
        cache.Add(2, 20);
        cache.Add(3, 30);
        _ = cache[1];
        _ = cache[3];       // 2 is the only entry left at frequency 1

        Assert.True(cache.TryPeekLeastFrequentlyUsed(out int victim, out int victimValue));
        Assert.Equal(2, victim);
        Assert.Equal(20, victimValue);

        cache.Add(4, 40);
        Assert.False(cache.ContainsKey(2));
    }

    [Fact]
    public void SoleOccupantPromotion_RelabelsBucketWithoutLosingOrder()
    {
        // Exercises the in-place relabel path: an entry alone in its bucket promoted when no bucket
        // holds f + 1. Repeated promotion must keep the frequency chain correctly ordered.
        var cache = Cache(3);
        cache.Add(1, 10);
        cache.Add(2, 20);
        cache.Add(3, 30);

        _ = cache[1];       // 1 alone at 2 (created)
        _ = cache[1];       // 1 alone at 2 -> relabel in place to 3
        _ = cache[1];       // -> 4
        _ = cache[1];       // -> 5

        Assert.True(cache.TryGetFrequency(1, out long f1));
        Assert.Equal(5L, f1);

        Assert.True(cache.TryPeekMostFrequentlyUsed(out int mfu, out _));
        Assert.Equal(1, mfu);
        Assert.True(cache.TryPeekLeastFrequentlyUsed(out int lfu, out _));
        Assert.Equal(2, lfu);

        Assert.Equal(new[] { 1, 2, 3 }, cache.Select(p => p.Key).OrderBy(k => k).ToArray());
    }

    [Fact]
    public void PromotionIntoAnExistingHigherBucket_MergesCorrectly()
    {
        // 1 and 2 both reach frequency 2, so the second promotion lands in a bucket that already
        // exists rather than creating one, and the source bucket is freed only when it empties.
        var cache = Cache(4);
        cache.Add(1, 10);
        cache.Add(2, 20);
        cache.Add(3, 30);
        cache.Add(4, 40);

        _ = cache[1];       // 1 -> 2 (bucket 2 created)
        _ = cache[2];       // 2 -> 2 (joins the existing bucket; bucket 1 keeps 3 and 4)

        Assert.True(cache.TryGetFrequency(1, out long f1));
        Assert.True(cache.TryGetFrequency(2, out long f2));
        Assert.Equal(2L, f1);
        Assert.Equal(2L, f2);

        // 2 was promoted last, so among the frequency-2 pair it is the most recent.
        Assert.True(cache.TryPeekMostFrequentlyUsed(out int mfu, out _));
        Assert.Equal(2, mfu);

        // 3 is the oldest remaining at frequency 1.
        Assert.True(cache.TryPeekLeastFrequentlyUsed(out int lfu, out _));
        Assert.Equal(3, lfu);
    }

    [Fact]
    public void LastEntryPromotion_KeepsMinAndMaxConsistent()
    {
        // A single-entry cache: every promotion frees and relabels the only bucket, so _minBucket and
        // _maxBucket must stay pointed at it.
        var cache = Cache(2);
        cache.Add(1, 10);
        for (int i = 0; i < 10; i++)
            _ = cache[1];

        Assert.True(cache.TryPeekLeastFrequentlyUsed(out int lfu, out _));
        Assert.True(cache.TryPeekMostFrequentlyUsed(out int mfu, out _));
        Assert.Equal(1, lfu);
        Assert.Equal(1, mfu);
        Assert.True(cache.TryGetFrequency(1, out long frequency));
        Assert.Equal(11L, frequency);
    }

    [Fact]
    public void RepeatedEvictionChurn_KeepsIndexAndBucketsConsistent()
    {
        var cache = Cache(8);
        for (int round = 0; round < 200; round++)
        {
            for (int i = 0; i < 20; i++)
            {
                int key = round * 20 + i;
                cache[key] = key;
                Assert.True(cache.Count <= 8);
                Assert.Equal(key, cache.TryPeek(key, out int v) ? v : -1);
            }
        }

        Assert.Equal(8, cache.Count);
        // Every surviving key must still round-trip through the index and appear exactly once.
        var keys = cache.Select(p => p.Key).ToArray();
        Assert.Equal(8, keys.Distinct().Count());
        foreach (int key in keys)
            Assert.True(cache.ContainsKey(key));
    }

    [Fact]
    public void ScanDoesNotEvictTheHotSet()
    {
        // The property LfuCache exists for, in the case where the cache has room to spare: a hot set
        // half the capacity, used enough to climb clear of frequency 1, then a sequential scan four
        // times the capacity sweeps through. Under LRU the scan evicts every hot key; under LFU the
        // scan keys fill the spare room and then evict each other, so the hot set is untouched.
        // LfuCacheFullOfHotKeys_LosesExactlyOneEntryToAScan below covers the boundary this avoids.
        const int Capacity = 16;
        const int HotKeys = 8;

        var cache = Cache(Capacity);
        for (int k = 0; k < HotKeys; k++)
            cache[k] = k;
        for (int pass = 0; pass < 5; pass++)
            for (int k = 0; k < HotKeys; k++)
                _ = cache[k];               // every hot key reaches frequency 6

        for (int scan = 1000; scan < 1000 + 4 * Capacity; scan++)
            cache[scan] = scan;             // a long stream of one-shot keys

        for (int k = 0; k < HotKeys; k++)
        {
            Assert.True(cache.ContainsKey(k));
            Assert.Equal(k, cache.TryPeek(k, out int v) ? v : -1);
        }

        // For contrast: the same scan through an LRU of the same capacity wipes the hot set out.
        var lru = new LruCache<int, int, Int32WangHasher>(Capacity);
        for (int k = 0; k < HotKeys; k++)
            lru[k] = k;
        for (int pass = 0; pass < 5; pass++)
            for (int k = 0; k < HotKeys; k++)
                _ = lru[k];
        for (int scan = 1000; scan < 1000 + 4 * Capacity; scan++)
            lru[scan] = scan;

        for (int k = 0; k < HotKeys; k++)
            Assert.False(lru.ContainsKey(k));
    }


    [Fact]
    public void FullOfHotKeys_LosesExactlyOneEntryToAScan()
    {
        // The boundary the test above deliberately leaves room around. With *every* slot already
        // holding a hot entry, the first cold key cannot arrive for free: there is no frequency-1
        // entry to drop yet, so it displaces the least-recently-used of the hot entries. From the
        // second cold key onward there always is one — the previous cold key — so it is evicted
        // instead. A scan therefore costs exactly one resident entry however long it runs, which is
        // the honest form of the claim: bounded, not zero. The LRU contrast is unchanged: it loses
        // the whole hot set.
        const int Capacity = 16;

        var cache = Cache(Capacity);
        for (int k = 0; k < Capacity; k++)
            cache[k] = k;                       // full, no spare slots
        for (int pass = 0; pass < 5; pass++)
            for (int k = 0; k < Capacity; k++)
                _ = cache[k];                   // every hot key reaches frequency 7

        // Key 0 is the least-recently-used at the shared top frequency, so it is the one displaced.
        Assert.True(cache.TryPeekLeastFrequentlyUsed(out int predicted, out _));
        Assert.Equal(0, predicted);

        for (int scan = 1000; scan < 1000 + 4 * Capacity; scan++)
            cache[scan] = scan;                 // a scan four times the capacity

        int lost = 0;
        for (int k = 0; k < Capacity; k++)
            if (!cache.ContainsKey(k)) lost++;

        Assert.Equal(1, lost);                  // exactly one, not zero and not the whole set
        Assert.False(cache.ContainsKey(0));     // and it is the one that was predicted
        Assert.Equal(Capacity, cache.Count);

        // The same scan through an LRU of the same capacity costs every hot key instead of one.
        var lru = new LruCache<int, int, Int32WangHasher>(Capacity);
        for (int k = 0; k < Capacity; k++)
            lru[k] = k;
        for (int pass = 0; pass < 5; pass++)
            for (int k = 0; k < Capacity; k++)
                _ = lru[k];
        for (int scan = 1000; scan < 1000 + 4 * Capacity; scan++)
            lru[scan] = scan;

        int lruLost = 0;
        for (int k = 0; k < Capacity; k++)
            if (!lru.ContainsKey(k)) lruLost++;
        Assert.Equal(Capacity, lruLost);
    }
}
