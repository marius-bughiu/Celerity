using System.Collections;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration coverage for <see cref="LfuCache{TKey, TValue, THasher}"/>: entries are yielded in
/// most-frequently-used to least-frequently-used order and most-recently-used first within one
/// frequency, the struct enumerator honours the version guard (including the fact that <i>every</i>
/// mutating read moves its entry to another bucket and therefore invalidates an active enumerator —
/// there is no already-at-the-front exemption of the kind <see cref="LruCache{TKey, TValue, THasher}"/>
/// has), and the boxed <see cref="IEnumerable"/> path agrees with the struct fast path.
/// </summary>
public class LfuCacheEnumerationTests
{
    private static LfuCache<int, int, Int32WangHasher> Cache(int capacity = 8) => new(capacity);

    private static List<int> KeysInOrder(LfuCache<int, int, Int32WangHasher> cache)
    {
        var keys = new List<int>();
        foreach (var kvp in cache)
            keys.Add(kvp.Key);
        return keys;
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenCacheIsEmpty()
    {
        var cache = Cache();
        Assert.Empty(KeysInOrder(cache));
    }

    [Fact]
    public void GetEnumerator_ShouldYieldMostFrequentFirst_WhenFrequenciesDiffer()
    {
        var cache = Cache();
        cache.Add(1, 10);
        cache.Add(2, 20);
        cache.Add(3, 30);

        _ = cache[3];   // 3 -> 2
        _ = cache[3];   // 3 -> 3
        _ = cache[2];   // 2 -> 2

        Assert.Equal(new[] { 3, 2, 1 }, KeysInOrder(cache));
    }

    [Fact]
    public void GetEnumerator_ShouldYieldMostRecentFirst_WhenFrequenciesAreEqual()
    {
        var cache = Cache();
        cache.Add(1, 10);
        cache.Add(2, 20);
        cache.Add(3, 30);   // all at frequency 1, linked oldest-first

        Assert.Equal(new[] { 3, 2, 1 }, KeysInOrder(cache));
    }

    [Fact]
    public void GetEnumerator_ShouldYieldDescendingFrequencyOrder_WhenEntriesSpanMultipleBuckets()
    {
        var cache = Cache();
        cache.Add(1, 10);
        cache.Add(2, 20);
        cache.Add(3, 30);
        cache.Add(4, 40);

        _ = cache[1];   // 1 -> 2
        _ = cache[1];   // 1 -> 3
        _ = cache[2];   // 2 -> 2
        _ = cache[3];   // 3 -> 2

        // Frequencies: 1 -> 3; 3 and 2 -> 2 (3 promoted most recently); 4 -> 1.
        Assert.Equal(new[] { 1, 3, 2, 4 }, KeysInOrder(cache));
    }

    [Fact]
    public void GetEnumerator_ShouldReflectTheNewOrder_WhenAReadRaisesAFrequency()
    {
        var cache = Cache();
        cache.Add(1, 10);
        cache.Add(2, 20);
        cache.Add(3, 30);
        Assert.Equal(new[] { 3, 2, 1 }, KeysInOrder(cache));

        _ = cache[1];   // 1 climbs above the frequency-1 pair
        Assert.Equal(new[] { 1, 3, 2 }, KeysInOrder(cache));
    }

    [Fact]
    public void GetEnumerator_ShouldYieldCurrentValues_WhenAValueWasOverwritten()
    {
        var cache = Cache();
        cache.Add(1, 10);
        cache.Add(2, 20);

        cache.AddOrUpdate(2, 99);

        var pairs = new List<KeyValuePair<int, int>>();
        foreach (var kvp in cache)
            pairs.Add(kvp);

        Assert.Equal(2, pairs.Count);
        Assert.Equal(99, pairs.Single(p => p.Key == 2).Value);
        Assert.Equal(10, pairs.Single(p => p.Key == 1).Value);
    }

    [Fact]
    public void MoveNext_ShouldThrowInvalidOperationException_WhenTheCacheIsStructurallyMutated()
    {
        var cache = Cache();
        cache.Add(1, 10);
        cache.Add(2, 20);

        LfuCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        Assert.True(e.MoveNext());
        cache.Add(3, 30);
        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrowInvalidOperationException_WhenAReadCountsAsAUse()
    {
        var cache = Cache();
        cache.Add(1, 10);
        cache.Add(2, 20);

        LfuCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        Assert.True(e.MoveNext());
        _ = cache[1];   // a use is a structural change
        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrowInvalidOperationException_WhenTheMostFrequentEntryIsRead()
    {
        // The point of departure from LruCache: promoting the front entry is a no-op there, but here
        // it always moves the entry to a different frequency bucket, so it always invalidates.
        var cache = Cache();
        cache.Add(1, 10);
        cache.Add(2, 20);
        _ = cache[1];   // 1 is now the most-frequently-used

        LfuCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.Equal(1, e.Current.Key);

        _ = cache[1];   // reading the front entry again
        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrowInvalidOperationException_WhenAValueIsOverwritten()
    {
        // Overwriting a value counts as a use here, unlike the rest of the library where a pure value
        // write leaves enumerators valid. Pinned deliberately so the difference cannot drift silently.
        var cache = Cache();
        cache.Add(1, 10);
        cache.Add(2, 20);

        LfuCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        Assert.True(e.MoveNext());
        cache.AddOrUpdate(1, 11);
        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldNotThrow_WhenOnlyPeeksOccurDuringEnumeration()
    {
        var cache = Cache();
        cache.Add(1, 10);
        cache.Add(2, 20);

        LfuCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        Assert.True(e.MoveNext());

        Assert.True(cache.TryPeek(1, out _));
        Assert.True(cache.ContainsKey(2));
        Assert.True(cache.TryGetFrequency(1, out _));
        Assert.True(cache.TryPeekLeastFrequentlyUsed(out _, out _));
        Assert.True(cache.TryPeekMostFrequentlyUsed(out _, out _));

        Assert.True(e.MoveNext()); // none of the above counted as a use
        Assert.False(e.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldNotThrow_WhenOperationsMissDuringEnumeration()
    {
        var cache = Cache();
        cache.Add(1, 10);
        cache.Add(2, 20);

        LfuCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        Assert.True(e.MoveNext());

        Assert.False(cache.TryGet(999, out _));  // a miss changes nothing
        Assert.False(cache.Remove(999));
        Assert.False(cache.TryAdd(1, 10));       // a rejected add is not a use

        Assert.True(e.MoveNext());
    }

    [Fact]
    public void Reset_ShouldRestartFromTheMostFrequentlyUsed_WhenCalledMidEnumeration()
    {
        var cache = Cache();
        cache.Add(1, 10);
        cache.Add(2, 20);
        _ = cache[1];

        LfuCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.Equal(1, e.Current.Key);
        Assert.True(e.MoveNext());
        Assert.Equal(2, e.Current.Key);

        e.Reset();
        Assert.True(e.MoveNext());
        Assert.Equal(1, e.Current.Key);
    }

    [Fact]
    public void Reset_ShouldThrowInvalidOperationException_WhenTheCacheWasModified()
    {
        var cache = Cache();
        cache.Add(1, 10);

        LfuCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        cache.Add(2, 20);
        Assert.Throws<InvalidOperationException>(() => e.Reset());
    }

    [Fact]
    public void GetEnumerator_ShouldAgreeWithTheBoxedPath_WhenEnumeratedThroughIEnumerable()
    {
        var cache = Cache();
        cache.Add(1, 10);
        cache.Add(2, 20);
        cache.Add(3, 30);
        _ = cache[2];

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
    public void MoveNext_ShouldStayFalse_WhenCalledPastTheEnd()
    {
        var cache = Cache();
        cache.Add(1, 10);

        LfuCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.False(e.MoveNext());
        Assert.False(e.MoveNext()); // idempotent after exhaustion
    }

    [Fact]
    public void MoveNext_ShouldStayFalse_WhenCalledPastTheEndOfAnEmptyCache()
    {
        var cache = Cache();

        LfuCache<int, int, Int32WangHasher>.Enumerator e = cache.GetEnumerator();
        Assert.False(e.MoveNext());
        Assert.False(e.MoveNext()); // idempotent from the empty start state too
    }

    [Fact]
    public void GetEnumerator_ShouldCoverEveryEntry_WhenEachBucketHoldsOne()
    {
        // One entry per distinct frequency: every bucket holds exactly one entry, so the enumerator
        // steps bucket-to-bucket on every MoveNext.
        var cache = Cache(10);
        for (int k = 0; k < 10; k++)
        {
            cache.Add(k, k);
            for (int use = 0; use < k; use++)
                _ = cache[k];               // key k ends at frequency k + 1
        }

        Assert.Equal(Enumerable.Range(0, 10).Reverse().ToArray(), KeysInOrder(cache));
    }
}
