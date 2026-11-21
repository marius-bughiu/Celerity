using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Policy coverage for <see cref="TinyLfuCache{TKey, TValue, THasher}"/>: the window and segmented
/// main region, the admission test (strict win for the candidate, tie to the victim), promotion and
/// demotion between probation and protected, the frequency sketch (saturation, halving, history that
/// outlives eviction and removal, reset by <c>Clear</c>), and the miss ratios that justify the type,
/// pinned against <see cref="LruCache{TKey, TValue, THasher}"/>, <see cref="LfuCache{TKey, TValue, THasher}"/>
/// and <see cref="SieveCache{TKey, TValue, THasher}"/> on the same seeded traces.
/// </summary>
public class TinyLfuCacheEvictionTests
{
    private static List<int> KeysInOrder<THasher>(TinyLfuCache<int, int, THasher> cache)
        where THasher : struct, IHashProvider<int>
    {
        var keys = new List<int>();
        foreach (var kvp in cache)
            keys.Add(kvp.Key);
        return keys;
    }

    [Fact]
    public void CapacityOne_HasNoMainRegion_SoTheResidentAlwaysLeaves()
    {
        // With one slot the window is the whole cache: however often the resident was used, the next
        // new key replaces it, exactly as a one-entry LRU would.
        var cache = new TinyLfuCache<int, int, Int32WangHasher>(1);
        cache[1] = 1;
        for (int i = 0; i < 8; i++)
            Assert.True(cache.TryGet(1, out _));

        cache[2] = 2;
        Assert.False(cache.ContainsKey(1));
        Assert.True(cache.ContainsKey(2));
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Candidate_ReplacesTheVictim_WhenStrictlyMoreFrequent()
    {
        // Capacity 2: a one-entry window over a one-entry main region.
        var cache = new TinyLfuCache<int, int, Int32WangHasher>(2);
        cache[1] = 1;              // window [1]
        cache[2] = 2;              // 1 overflows to probation: window [2], probation [1]
        _ = cache[2];
        _ = cache[2];              // 2 now has three recorded uses against 1's one

        cache[3] = 3;              // full: candidate 2 beats victim 1
        Assert.False(cache.ContainsKey(1));
        Assert.True(cache.ContainsKey(2));
        Assert.True(cache.ContainsKey(3));
        Assert.Equal(new[] { 3, 2 }, KeysInOrder(cache)); // window [3], probation [2]
    }

    [Fact]
    public void Candidate_IsEvicted_OnATie()
    {
        var cache = new TinyLfuCache<int, int, Int32WangHasher>(2);
        cache[1] = 1;
        cache[2] = 2;              // window [2], probation [1], one use each

        cache[3] = 3;              // full: candidate 2 ties victim 1, so the candidate goes
        Assert.True(cache.ContainsKey(1));
        Assert.False(cache.ContainsKey(2));
        Assert.Equal(new[] { 3, 1 }, KeysInOrder(cache));
    }

    [Fact]
    public void ProbationHit_Promotes_AndAnOverfullProtectedSegmentDemotesItsOldest()
    {
        // Capacity 10: window 1, main 9, protected share 7.
        var cache = new TinyLfuCache<int, int, Int32WangHasher>(10);
        for (int k = 1; k <= 10; k++)
            cache[k] = k;
        Assert.Equal(new[] { 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 }, KeysInOrder(cache)); // window [10], probation [9..1]

        for (int k = 1; k <= 8; k++)
            _ = cache[k];

        // Promoting 8 pushed protected to eight entries, so its oldest (1) dropped back to the front
        // of probation. Enumeration is window, probation, protected, each newest first.
        Assert.Equal(new[] { 10, 1, 9, 8, 7, 6, 5, 4, 3, 2 }, KeysInOrder(cache));
    }

    [Fact]
    public void ProtectedHit_MovesTheEntryToTheFrontOfProtected()
    {
        var cache = new TinyLfuCache<int, int, Int32WangHasher>(10);
        for (int k = 1; k <= 4; k++)
            cache[k] = k;          // window [4], probation [3, 2, 1]
        _ = cache[1];
        _ = cache[2];              // protected [2, 1]
        Assert.Equal(new[] { 4, 3, 2, 1 }, KeysInOrder(cache));

        _ = cache[1];
        Assert.Equal(new[] { 4, 3, 1, 2 }, KeysInOrder(cache));
    }

    [Fact]
    public void Window_IsOnePercentOfCapacity_AndAHitMovesWithinIt()
    {
        // Capacity 200 gives a two-entry window; capacity 199 still gives one.
        var cache = new TinyLfuCache<int, int, Int32WangHasher>(200);
        cache[1] = 1;
        cache[2] = 2;
        cache[3] = 3;              // window [3, 2], probation [1]
        Assert.Equal(new[] { 3, 2, 1 }, KeysInOrder(cache));
        _ = cache[2];
        Assert.Equal(new[] { 2, 3, 1 }, KeysInOrder(cache));

        var small = new TinyLfuCache<int, int, Int32WangHasher>(199);
        small[1] = 1;
        small[2] = 2;              // window [2], probation [1]
        _ = small[1];              // promoted: protected [1]
        Assert.Equal(new[] { 2, 1 }, KeysInOrder(small));
    }

    [Fact]
    public void AFullCache_KeepsItsWindowAndMainRegionFull_ThroughChurn()
    {
        // Whatever the admission decisions, a full cache holds exactly capacity entries and every
        // newly put key is resident.
        var cache = new TinyLfuCache<int, int, Int32WangHasher>(16);
        var rng = new Random(11);
        for (int i = 0; i < 5000; i++)
        {
            int k = rng.Next(0, 64);
            if (!cache.TryGet(k, out _))
            {
                cache[k] = k;
                Assert.True(cache.ContainsKey(k));
            }
            Assert.True(cache.Count <= 16);
        }
        Assert.Equal(16, cache.Count);
        Assert.Equal(16, KeysInOrder(cache).Count);
    }

    // ---- the frequency sketch ---------------------------------------------------------------------

    [Fact]
    public void FrequencyEstimate_CountsHitsAndInserts_ButNotMissesOrPeeks()
    {
        var cache = new TinyLfuCache<int, int, Int32WangHasher>(64);
        Assert.Equal(0, cache.GetFrequencyEstimate(5));

        Assert.False(cache.TryGet(5, out _));        // a miss records nothing
        Assert.Equal(0, cache.GetFrequencyEstimate(5));

        cache[5] = 5;                                // the insert records one use
        Assert.Equal(1, cache.GetFrequencyEstimate(5));

        Assert.True(cache.TryGet(5, out _));
        _ = cache[5];
        cache[5] = 55;                               // an overwrite is a use too
        Assert.Equal(4, cache.GetFrequencyEstimate(5));

        Assert.True(cache.TryPeek(5, out _));
        Assert.True(cache.ContainsKey(5));
        Assert.False(cache.TryAdd(5, 0));            // a failed TryAdd records nothing
        Assert.Equal(4, cache.GetFrequencyEstimate(5));
    }

    [Fact]
    public void FrequencyEstimate_Saturates_AtFifteen()
    {
        // Capacity 2 samples 20 recorded accesses per period, so 30 uses of one key would cross a
        // halving if each one were recorded; once the counters saturate, uses stop being recorded.
        var cache = new TinyLfuCache<int, int, Int32WangHasher>(2);
        cache[7] = 7;
        for (int i = 0; i < 29; i++)
            _ = cache[7];
        Assert.Equal(15, cache.GetFrequencyEstimate(7));
    }

    [Fact]
    public void FrequencyEstimate_Halves_AfterTenTimesCapacityRecordedAccesses()
    {
        var cache = new TinyLfuCache<int, int, Int32WangHasher>(4); // a halving every 40 recorded accesses
        cache[7] = 7;
        for (int i = 0; i < 11; i++)
            _ = cache[7];
        Assert.Equal(12, cache.GetFrequencyEstimate(7));

        // 28 more recorded accesses brings the period to 40 and halves every counter.
        for (int i = 0; i < 28; i++)
            cache[1000 + i] = i;
        Assert.Equal(6, cache.GetFrequencyEstimate(7));
    }

    [Fact]
    public void FrequencyHistory_OutlivesEvictionAndRemoval()
    {
        var cache = new TinyLfuCache<int, int, Int32WangHasher>(2);
        cache[1] = 1;
        _ = cache[1];
        _ = cache[1];
        Assert.True(cache.Remove(1));
        Assert.Equal(3, cache.GetFrequencyEstimate(1));

        // A key that keeps coming back is admitted over a resident seen only once.
        cache[2] = 2;
        cache[3] = 3;              // window [3], probation [2]
        cache[1] = 1;              // full: candidate 3 (one use) ties victim 2, so 3 goes
        Assert.False(cache.ContainsKey(3));
        cache[4] = 4;              // full: candidate 1 (four uses) beats victim 2
        Assert.True(cache.ContainsKey(1));
        Assert.False(cache.ContainsKey(2));
    }

    [Fact]
    public void Clear_ForgetsEveryFrequency_EvenOnAnEmptyCache()
    {
        var cache = new TinyLfuCache<int, int, Int32WangHasher>(8);
        cache[1] = 1;
        _ = cache[1];
        cache.Clear();
        Assert.Equal(0, cache.GetFrequencyEstimate(1));

        cache[2] = 2;
        Assert.True(cache.Remove(2));
        Assert.Equal(0, cache.Count);
        Assert.Equal(1, cache.GetFrequencyEstimate(2));
        cache.Clear();
        Assert.Equal(0, cache.GetFrequencyEstimate(2));
    }

    [Fact]
    public void FrequencyEstimate_IsShared_ByKeysWithTheSameHash()
    {
        // This hasher gives every odd key one hash and every even key another, so odd keys share one
        // set of counters: each one's estimate is the total of all their uses. A count-min sketch may
        // overstate a key this way, and never understates it.
        var cache = new TinyLfuCache<int, int, CacheCollidingHasher>(8);
        cache[1] = 1;
        cache[3] = 3;
        _ = cache[3];
        Assert.Equal(3, cache.GetFrequencyEstimate(1));
        Assert.Equal(3, cache.GetFrequencyEstimate(5));
        Assert.Equal(0, cache.GetFrequencyEstimate(2));
    }

    // ---- miss ratios against the siblings --------------------------------------------------------

    private const int TraceCapacity = 1024;
    private const int ZipfKeys = 100_000;
    private const int TraceLength = 1_000_000;

    private static readonly double[] ZipfCdf = BuildZipfCdf();

    private static double[] BuildZipfCdf()
    {
        var cdf = new double[ZipfKeys];
        double sum = 0;
        for (int i = 0; i < ZipfKeys; i++)
        {
            sum += 1.0 / (i + 1);
            cdf[i] = sum;
        }
        return cdf;
    }

    private static int NextZipf(Random rng)
    {
        int idx = Array.BinarySearch(ZipfCdf, rng.NextDouble() * ZipfCdf[^1]);
        return idx >= 0 ? idx : Math.Min(~idx, ZipfKeys - 1);
    }

    // Stationary Zipf(1.0) over 100,000 keys.
    private static int[] ZipfTrace()
    {
        var rng = new Random(542);
        var trace = new int[TraceLength];
        for (int i = 0; i < trace.Length; i++)
            trace[i] = NextZipf(rng);
        return trace;
    }

    // A cyclic pass over 1.5 x capacity keys: every key is reused, but always just after an LRU-ordered
    // cache would have evicted it.
    private static int[] LoopTrace()
    {
        var trace = new int[TraceLength];
        for (int i = 0; i < trace.Length; i++)
            trace[i] = 50_000_000 + i % (TraceCapacity * 3 / 2);
        return trace;
    }

    // Zipf requests interleaved one-for-one with a cyclic pass over 2 x capacity keys.
    private static int[] ZipfLoopTrace()
    {
        var rng = new Random(542);
        var trace = new int[TraceLength];
        int loop = 0;
        for (int i = 0; i < trace.Length; i++)
            trace[i] = (i & 1) == 0 ? NextZipf(rng) : 50_000_000 + loop++ % (2 * TraceCapacity);
        return trace;
    }

    private static (double Lru, double Lfu, double Sieve, double TinyLfu) MissRatios(int[] trace)
    {
        var lru = new LruCache<int, int, Int32WangHasher>(TraceCapacity);
        var lfu = new LfuCache<int, int, Int32WangHasher>(TraceCapacity);
        var sieve = new SieveCache<int, int, Int32WangHasher>(TraceCapacity);
        var tiny = new TinyLfuCache<int, int, Int32WangHasher>(TraceCapacity);
        int a = 0, b = 0, c = 0, d = 0;
        foreach (int k in trace)
        {
            if (!lru.TryGet(k, out _)) { a++; lru[k] = k; }
            if (!lfu.TryGet(k, out _)) { b++; lfu[k] = k; }
            if (!sieve.TryGet(k, out _)) { c++; sieve[k] = k; }
            if (!tiny.TryGet(k, out _)) { d++; tiny[k] = k; }
        }
        double n = trace.Length;
        return (a / n, b / n, c / n, d / n);
    }

    [Fact]
    public void Loop_LargerThanTheCache_DefeatsEverySiblingButNotTinyLfu()
    {
        var (lru, lfu, sieve, tiny) = MissRatios(LoopTrace());
        Assert.Equal(1.0, lru);
        Assert.Equal(1.0, lfu);
        Assert.Equal(1.0, sieve);
        Assert.True(tiny < 0.40, $"TinyLfuCache missed {tiny:P1} of a 1.5x-capacity loop.");
    }

    [Fact]
    public void ZipfMixedWithALoop_MissesAtLeastTenPointsLessThanEverySibling()
    {
        // The kill criterion of #656.
        var (lru, lfu, sieve, tiny) = MissRatios(ZipfLoopTrace());
        double best = Math.Min(lru, Math.Min(lfu, sieve));
        Assert.True(tiny <= best - 0.10, $"TinyLfuCache {tiny:P1} vs best sibling {best:P1}.");
    }

    [Fact]
    public void StationaryZipf_IsWithinOnePointOfTheBestSibling()
    {
        var (lru, lfu, sieve, tiny) = MissRatios(ZipfTrace());
        double best = Math.Min(lru, Math.Min(lfu, sieve));
        Assert.True(tiny <= best + 0.01, $"TinyLfuCache {tiny:P1} vs best sibling {best:P1}.");
        Assert.True(tiny < lru, $"TinyLfuCache {tiny:P1} vs LruCache {lru:P1}.");
    }
}
