using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Eviction-policy coverage for <see cref="SieveCache{TKey, TValue, THasher}"/>: with no hits the
/// cache drops entries in insertion order; a use (a hit via the indexer or <c>TryGet</c>, or an
/// overwrite) spares an entry for one pass of the hand; the hand remembers where it stopped, wraps
/// from the newest entry back to the oldest, and survives removals. The Zipf trace at the end pins
/// the miss-ratio criterion from issue #542.
/// </summary>
public class SieveCacheEvictionTests
{
    private static SieveCache<int, int, Int32WangHasher> Cache(int capacity) => new(capacity);

    private static void AssertResident(SieveCache<int, int, Int32WangHasher> cache, params int[] keys)
    {
        Assert.Equal(keys.Length, cache.Count);
        foreach (int k in keys)
            Assert.True(cache.ContainsKey(k), $"expected key {k} to be resident");
    }

    [Fact]
    public void InsertPastCapacity_WithNoHits_EvictsOldestInserted()
    {
        var cache = Cache(3);
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;
        cache[4] = 40; // nothing visited: the hand starts at the tail (1) and evicts it

        AssertResident(cache, 2, 3, 4);
        Assert.False(cache.ContainsKey(1));
    }

    [Fact]
    public void CountNeverExceedsCapacity_AndNoHitsBehavesAsFifo()
    {
        var cache = Cache(4);
        for (int i = 0; i < 1000; i++)
        {
            cache[i] = i;
            Assert.True(cache.Count <= 4);
        }

        AssertResident(cache, 996, 997, 998, 999);
    }

    [Fact]
    public void GetMarksVisited_SparingItFromTheNextEviction()
    {
        var cache = Cache(3);
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;

        _ = cache[1];  // 1 visited
        cache[4] = 40; // hand clears 1, moves to 2, evicts 2

        AssertResident(cache, 1, 3, 4);
    }

    [Fact]
    public void TryGetMarksVisited_SparingItFromTheNextEviction()
    {
        var cache = Cache(3);
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;

        Assert.True(cache.TryGet(1, out _));
        cache[4] = 40;

        AssertResident(cache, 1, 3, 4);
    }

    [Fact]
    public void Overwrite_CountsAsAUse()
    {
        var cache = Cache(3);
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;

        cache.AddOrUpdate(1, 11);
        cache[4] = 40;

        AssertResident(cache, 1, 3, 4);
        Assert.Equal(11, cache[1]);
    }

    [Fact]
    public void PeekAndContainsKey_DoNotMarkVisited()
    {
        var cache = Cache(3);
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;

        Assert.True(cache.TryPeek(1, out _));
        Assert.True(cache.ContainsKey(1));
        cache[4] = 40; // 1 was only peeked, so it is still the victim

        AssertResident(cache, 2, 3, 4);
    }

    [Fact]
    public void TryAdd_OnExistingKey_DoesNotMarkVisited()
    {
        var cache = Cache(3);
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;

        Assert.False(cache.TryAdd(1, 99));
        cache[4] = 40;

        AssertResident(cache, 2, 3, 4);
    }

    [Fact]
    public void VisitedEntrySurvivesOnePassOnly_UnlessUsedAgain()
    {
        var cache = Cache(3);
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;
        _ = cache[1];

        cache[4] = 40; // clears 1, evicts 2; the hand rests on 3 (2's newer neighbour)
        AssertResident(cache, 1, 3, 4);

        _ = cache[3];
        _ = cache[4];
        // The hand clears 3 and 4, passes the head, wraps to the tail, and finds 1 — whose bit the
        // previous pass cleared and nothing has set since.
        cache[5] = 50;
        AssertResident(cache, 3, 4, 5);
    }

    [Fact]
    public void Hand_ChasesNewInserts_SoAnOldVisitedEntryOutlivesNewerOnes()
    {
        var cache = Cache(3);
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;
        _ = cache[1];
        cache[4] = 40; // clears 1, evicts 2; the hand rests on 3

        // With no further hits, each insert evicts the entry under the hand and leaves the hand on
        // the next-newer entry, so the hand never reaches the head to wrap back round to 1.
        for (int k = 5; k <= 20; k++)
        {
            cache[k] = k * 10;
            AssertResident(cache, 1, k - 1, k);
        }
    }

    [Fact]
    public void Hand_RemembersWhereItStopped_RatherThanRestartingAtTheTail()
    {
        var cache = Cache(4);
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;
        cache[4] = 40;

        cache[5] = 50; // evicts 1; hand rests on 2
        _ = cache[5];  // the newest entry is visited
        _ = cache[2];  // and so is the one under the hand

        cache[6] = 60; // the hand clears 2, evicts 3, and rests on 4
        AssertResident(cache, 2, 4, 5, 6);

        // A sweep that restarted at the tail would find 2 (now unvisited) and evict it instead.
        cache[7] = 70;
        AssertResident(cache, 2, 5, 6, 7);
    }

    [Fact]
    public void Sweep_WrapsFromNewestToOldest_WhenEverythingIsVisited()
    {
        var cache = Cache(3);
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;
        _ = cache[1];
        _ = cache[2];
        _ = cache[3];

        // One full lap clears every bit and wraps to the tail, which is now unvisited.
        cache[4] = 40;
        AssertResident(cache, 2, 3, 4);

        // Those clears are not undone: 2 and 3 are now plain unvisited entries.
        cache[5] = 50;
        AssertResident(cache, 3, 4, 5);
    }

    [Fact]
    public void Remove_OfTheEntryUnderTheHand_MovesTheHandToItsNewerNeighbour()
    {
        var cache = Cache(4);
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;
        cache[4] = 40;
        cache[5] = 50; // evicts 1; hand rests on 2

        Assert.True(cache.Remove(2)); // hand moves to 3
        cache[6] = 60;                // a free slot exists, so no eviction yet
        AssertResident(cache, 3, 4, 5, 6);

        _ = cache[4];
        cache[7] = 70; // hand at 3, unvisited: evicted (had the hand reset to the tail, also 3)
        cache[8] = 80; // hand at 4: visited, cleared; 5 evicted
        AssertResident(cache, 4, 6, 7, 8);
    }

    [Fact]
    public void Remove_OfTheNewestEntryUnderTheHand_ResetsTheHandToTheTail()
    {
        var cache = Cache(3);
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;
        _ = cache[1];
        cache[4] = 40; // clears 1, evicts 2; the hand rests on 3
        cache[5] = 50; // evicts 3; the hand rests on 4, behind the head 5

        Assert.True(cache.Remove(5)); // 4 is now the head, still under the hand
        Assert.True(cache.Remove(4)); // the hand falls off the head
        cache[6] = 60;
        cache[7] = 70; // both fill free slots
        AssertResident(cache, 1, 6, 7);

        // The sweep starts at the tail, where 1 sits unvisited. A hand left on 4's recycled slot
        // would point at 6 and evict that instead.
        cache[8] = 80;
        AssertResident(cache, 6, 7, 8);
    }

    [Fact]
    public void Remove_ElsewhereLeavesTheHandInPlace()
    {
        var cache = Cache(4);
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;
        cache[4] = 40;
        cache[5] = 50; // evicts 1; hand rests on 2
        _ = cache[2];

        Assert.True(cache.Remove(5));
        cache[6] = 60; // fills the freed slot
        cache[7] = 70; // hand at 2: visited, cleared; 3 evicted
        AssertResident(cache, 2, 4, 6, 7);
    }

    [Fact]
    public void Clear_ResetsTheHand()
    {
        var cache = Cache(3);
        cache[1] = 10;
        cache[2] = 20;
        cache[3] = 30;
        cache[4] = 40; // hand rests on 2

        cache.Clear();
        cache[5] = 50;
        cache[6] = 60;
        cache[7] = 70;
        cache[8] = 80; // a stale hand would point at a freed slot; a reset one starts at the tail (5)
        AssertResident(cache, 6, 7, 8);
    }

    [Fact]
    public void TryAdd_AtCapacity_Evicts()
    {
        var cache = Cache(2);
        cache[1] = 10;
        cache[2] = 20;

        Assert.True(cache.TryAdd(3, 30));
        AssertResident(cache, 2, 3);
    }

    [Fact]
    public void CapacityOne_BehavesAsSingleSlot_EvenWhenVisited()
    {
        var cache = Cache(1);
        cache[1] = 10;
        _ = cache[1];

        cache[2] = 20; // the lap clears 1, wraps to it, evicts it
        AssertResident(cache, 2);
        Assert.Equal(20, cache[2]);

        cache[3] = 30;
        AssertResident(cache, 3);
    }

    [Fact]
    public void RepeatedEvictionChurn_KeepsIndexAndQueueConsistent()
    {
        var cache = Cache(16);
        var rng = new Random(7);
        for (int i = 0; i < 20_000; i++)
        {
            int k = rng.Next(64);
            if (!cache.TryGet(k, out int v))
                cache[k] = k * 3;
            else
                Assert.Equal(k * 3, v);
        }

        int enumerated = 0;
        foreach (KeyValuePair<int, int> pair in cache)
        {
            Assert.True(cache.ContainsKey(pair.Key));
            Assert.Equal(pair.Key * 3, pair.Value);
            enumerated++;
        }
        Assert.Equal(cache.Count, enumerated);
        Assert.Equal(16, cache.Count);
    }

    // Issue #542, kill criterion 1: on a seeded Zipf(1.0) trace of 1,000,000 requests over 100,000
    // distinct keys at capacity 1,024, SIEVE must miss strictly less often than LruCache. Both caches
    // see the identical request stream (get, and put on a miss), so the comparison is policy alone.
    [Fact]
    public void ZipfTrace_MissesLessOftenThanLru()
    {
        const int Capacity = 1024;
        int[] trace = ZipfTrace(requests: 1_000_000, keys: 100_000, alpha: 1.0, seed: 542);

        var sieve = new SieveCache<int, int, Int32WangHasher>(Capacity);
        var lru = new LruCache<int, int, Int32WangHasher>(Capacity);
        int sieveMisses = 0, lruMisses = 0;
        foreach (int k in trace)
        {
            if (!sieve.TryGet(k, out _)) { sieveMisses++; sieve[k] = k; }
            if (!lru.TryGet(k, out _)) { lruMisses++; lru[k] = k; }
        }

        Assert.True(sieveMisses < lruMisses, $"SIEVE missed {sieveMisses}, LRU missed {lruMisses}");
    }

    // Inverse-CDF Zipf sampler: key i (0-based) has weight 1 / (i + 1)^alpha.
    internal static int[] ZipfTrace(int requests, int keys, double alpha, int seed)
    {
        var cdf = new double[keys];
        double sum = 0;
        for (int i = 0; i < keys; i++)
        {
            sum += 1.0 / Math.Pow(i + 1, alpha);
            cdf[i] = sum;
        }

        var rng = new Random(seed);
        var trace = new int[requests];
        for (int r = 0; r < requests; r++)
        {
            int idx = Array.BinarySearch(cdf, rng.NextDouble() * sum);
            trace[r] = idx >= 0 ? idx : Math.Min(~idx, keys - 1);
        }
        return trace;
    }
}
