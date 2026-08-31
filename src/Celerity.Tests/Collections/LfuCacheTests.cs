using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Core behavioural coverage for <see cref="LfuCache{TKey, TValue, THasher}"/>: construction,
/// capacity validation, the get/put/peek surface, frequency accounting, add/remove/clear, and the
/// default-key path. Frequency-driven eviction ordering lives in <c>LfuCacheEvictionTests</c>;
/// enumeration order and version invalidation live in <c>LfuCacheEnumerationTests</c>; the randomized
/// oracle check lives in <c>LfuCacheDifferentialTests</c>.
/// </summary>
public class LfuCacheTests
{
    private static LfuCache<int, string, Int32WangHasher> NewCache(int capacity = 4)
        => new(capacity);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_RejectsNonPositiveCapacity(int capacity)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new LfuCache<int, string, Int32WangHasher>(capacity));
        Assert.Equal("capacity", ex.ParamName);
    }

    [Fact]
    public void NewCache_IsEmpty()
    {
        var cache = NewCache(8);
        Assert.Equal(8, cache.Capacity);
        Assert.Equal(0, cache.Count);
        Assert.False(cache.ContainsKey(1));
        Assert.False(cache.TryGet(1, out _));
        Assert.False(cache.TryPeek(1, out _));
        Assert.False(cache.TryGetFrequency(1, out _));
        Assert.False(cache.TryPeekLeastFrequentlyUsed(out _, out _));
        Assert.False(cache.TryPeekMostFrequentlyUsed(out _, out _));
    }

    [Fact]
    public void Indexer_SetThenGet_RoundTrips()
    {
        var cache = NewCache();
        cache[1] = "one";
        cache[2] = "two";

        Assert.Equal(2, cache.Count);
        Assert.Equal("one", cache[1]);
        Assert.Equal("two", cache[2]);
    }

    [Fact]
    public void Indexer_Get_MissingKey_Throws()
    {
        var cache = NewCache();
        cache[1] = "one";
        Assert.Throws<KeyNotFoundException>(() => _ = cache[2]);
    }

    [Fact]
    public void Indexer_Set_OverwritesExistingValue_WithoutGrowingCount()
    {
        var cache = NewCache();
        cache[1] = "one";
        cache[1] = "ONE";

        Assert.Equal(1, cache.Count);
        Assert.Equal("ONE", cache[1]);
    }

    [Fact]
    public void TryGet_HitAndMiss()
    {
        var cache = NewCache();
        cache[7] = "seven";

        Assert.True(cache.TryGet(7, out string? hit));
        Assert.Equal("seven", hit);

        Assert.False(cache.TryGet(8, out string? miss));
        Assert.Null(miss);
    }

    [Fact]
    public void InsertedEntry_StartsAtFrequencyOne()
    {
        var cache = NewCache();
        cache.Add(1, "one");

        Assert.True(cache.TryGetFrequency(1, out long frequency));
        Assert.Equal(1L, frequency);
    }

    [Fact]
    public void TryGet_CountsAsUse_AndRaisesFrequency()
    {
        var cache = NewCache();
        cache.Add(1, "one");                    // frequency 1

        Assert.True(cache.TryGet(1, out _));    // 2
        Assert.True(cache.TryGet(1, out _));    // 3

        Assert.True(cache.TryGetFrequency(1, out long frequency));
        Assert.Equal(3L, frequency);
    }

    [Fact]
    public void IndexerGet_CountsAsUse_AndRaisesFrequency()
    {
        var cache = NewCache();
        cache.Add(1, "one");                    // frequency 1
        _ = cache[1];                           // 2
        _ = cache[1];                           // 3
        _ = cache[1];                           // 4

        Assert.True(cache.TryGetFrequency(1, out long frequency));
        Assert.Equal(4L, frequency);
    }

    [Fact]
    public void IndexerSet_OnExistingKey_CountsAsUse()
    {
        var cache = NewCache();
        cache[1] = "one";   // insert, frequency 1
        cache[1] = "uno";   // overwrite counts as a use -> 2

        Assert.True(cache.TryGetFrequency(1, out long frequency));
        Assert.Equal(2L, frequency);
    }

    [Fact]
    public void TryPeek_DoesNotCountAsUse()
    {
        var cache = NewCache(2);
        cache.Add(1, "one");
        cache.Add(2, "two");
        _ = cache[2];                           // 2 is now at frequency 2, 1 at frequency 1

        Assert.True(cache.TryPeek(1, out _));
        Assert.True(cache.TryGetFrequency(1, out long frequency));
        Assert.Equal(1L, frequency);            // the peek did not raise it

        // ...so 1 is still the victim.
        cache.Add(3, "three");
        Assert.False(cache.ContainsKey(1));
        Assert.True(cache.ContainsKey(2));
        Assert.True(cache.ContainsKey(3));
    }

    [Fact]
    public void ContainsKey_DoesNotCountAsUse()
    {
        var cache = NewCache(2);
        cache.Add(1, "one");
        cache.Add(2, "two");
        _ = cache[2];

        Assert.True(cache.ContainsKey(1));
        cache.Add(3, "three");                  // still evicts 1, because ContainsKey is a peek

        Assert.False(cache.ContainsKey(1));
    }

    [Fact]
    public void TryGetFrequency_DoesNotCountAsUse()
    {
        var cache = NewCache();
        cache.Add(1, "one");

        Assert.True(cache.TryGetFrequency(1, out long first));
        Assert.True(cache.TryGetFrequency(1, out long second));
        Assert.Equal(1L, first);
        Assert.Equal(1L, second);
    }

    [Fact]
    public void TryGetFrequency_MissingKey_ReturnsFalseAndZero()
    {
        var cache = NewCache();
        cache.Add(1, "one");

        Assert.False(cache.TryGetFrequency(2, out long frequency));
        Assert.Equal(0L, frequency);
    }

    [Fact]
    public void Add_NewKey_Inserts()
    {
        var cache = NewCache();
        cache.Add(1, "one");
        Assert.True(cache.ContainsKey(1));
        Assert.Equal("one", cache[1]);
    }

    [Fact]
    public void Add_DuplicateKey_Throws()
    {
        var cache = NewCache();
        cache.Add(1, "one");
        var ex = Assert.Throws<ArgumentException>(() => cache.Add(1, "again"));
        Assert.Equal("key", ex.ParamName);
    }

    [Fact]
    public void TryAdd_ReturnsFalseOnDuplicate_AndLeavesValueAndFrequencyUnchanged()
    {
        var cache = NewCache();
        Assert.True(cache.TryAdd(1, "one"));
        Assert.False(cache.TryAdd(1, "two"));
        Assert.Equal(1, cache.Count);
        Assert.Equal("one", cache.TryPeek(1, out string? v) ? v : null);

        // A rejected add is not a use.
        Assert.True(cache.TryGetFrequency(1, out long frequency));
        Assert.Equal(1L, frequency);
    }

    [Fact]
    public void AddOrUpdate_InsertsThenUpdates()
    {
        var cache = NewCache();
        cache.AddOrUpdate(1, "one");
        Assert.Equal("one", cache.TryPeek(1, out string? first) ? first : null);
        cache.AddOrUpdate(1, "uno");
        Assert.Equal("uno", cache.TryPeek(1, out string? second) ? second : null);
        Assert.Equal(1, cache.Count);

        // Insert plus one overwrite is two uses.
        Assert.True(cache.TryGetFrequency(1, out long frequency));
        Assert.Equal(2L, frequency);
    }

    [Fact]
    public void Remove_Existing_ReturnsTrueAndValue()
    {
        var cache = NewCache();
        cache[1] = "one";
        cache[2] = "two";

        Assert.True(cache.Remove(1, out string? removed));
        Assert.Equal("one", removed);
        Assert.Equal(1, cache.Count);
        Assert.False(cache.ContainsKey(1));
        Assert.True(cache.ContainsKey(2));
    }

    [Fact]
    public void Remove_Missing_ReturnsFalse()
    {
        var cache = NewCache();
        cache[1] = "one";
        Assert.False(cache.Remove(2, out string? removed));
        Assert.Null(removed);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Remove_DiscardsFrequency_SoReinsertStartsAtOne()
    {
        var cache = NewCache();
        cache.Add(1, "one");
        for (int i = 0; i < 5; i++)
            _ = cache[1];                       // frequency 6

        Assert.True(cache.Remove(1));
        cache.Add(1, "one again");

        Assert.True(cache.TryGetFrequency(1, out long frequency));
        Assert.Equal(1L, frequency);
    }

    [Fact]
    public void Remove_ThenReinsert_ReusesFreedSlotCorrectly()
    {
        var cache = NewCache(2);
        cache[1] = "one";
        cache[2] = "two";
        Assert.True(cache.Remove(1));
        cache[3] = "three"; // fits without eviction (count was 1)

        Assert.Equal(2, cache.Count);
        Assert.True(cache.ContainsKey(2));
        Assert.True(cache.ContainsKey(3));
        Assert.False(cache.ContainsKey(1));
    }

    [Fact]
    public void Remove_FromASharedFrequencyBucket_LeavesTheOthers()
    {
        // Three entries all at frequency 1 share one bucket; removing the middle one must not disturb
        // the other two or free the bucket out from under them.
        var cache = NewCache(4);
        cache.Add(1, "one");
        cache.Add(2, "two");
        cache.Add(3, "three");

        Assert.True(cache.Remove(2));

        Assert.Equal(2, cache.Count);
        Assert.True(cache.TryGetFrequency(1, out long f1));
        Assert.True(cache.TryGetFrequency(3, out long f3));
        Assert.Equal(1L, f1);
        Assert.Equal(1L, f3);
        Assert.True(cache.TryPeekLeastFrequentlyUsed(out int victim, out _));
        Assert.Equal(1, victim);                // 1 is the least-recently-used at frequency 1
    }

    [Fact]
    public void Clear_EmptiesCacheButKeepsCapacity()
    {
        var cache = NewCache(3);
        cache[1] = "one";
        cache[2] = "two";
        _ = cache[1];                           // spread the entries over two frequency buckets
        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.Equal(3, cache.Capacity);
        Assert.False(cache.ContainsKey(1));
        Assert.False(cache.TryPeekLeastFrequentlyUsed(out _, out _));
        Assert.False(cache.TryPeekMostFrequentlyUsed(out _, out _));

        // The cache is fully usable after Clear (both free stacks rebuilt).
        cache[9] = "nine";
        Assert.Equal("nine", cache[9]);
        Assert.True(cache.TryGetFrequency(9, out long frequency));
        Assert.Equal(2L, frequency);            // the insert plus the indexer get above
    }

    [Fact]
    public void PeekLfuAndMfu_TrackFrequency()
    {
        var cache = NewCache(3);
        cache.Add(1, "one");
        cache.Add(2, "two");
        cache.Add(3, "three");                  // all at frequency 1

        // All equal: the most-frequently-used is the most-recently-linked, the victim the oldest.
        Assert.True(cache.TryPeekMostFrequentlyUsed(out int mfuK, out string? mfuV));
        Assert.Equal(3, mfuK);
        Assert.Equal("three", mfuV);

        Assert.True(cache.TryPeekLeastFrequentlyUsed(out int lfuK, out string? lfuV));
        Assert.Equal(1, lfuK);
        Assert.Equal("one", lfuV);

        // Two uses on 1 lift it clear of the others; 2 becomes the victim (older than 3 at frequency 1).
        _ = cache[1];
        _ = cache[1];
        Assert.True(cache.TryPeekMostFrequentlyUsed(out int mfuK2, out _));
        Assert.Equal(1, mfuK2);
        Assert.True(cache.TryPeekLeastFrequentlyUsed(out int lfuK2, out _));
        Assert.Equal(2, lfuK2);
    }

    [Fact]
    public void CapacityOne_EvictsOnEveryNewKey()
    {
        var cache = NewCache(1);
        cache.Add(1, "one");
        for (int i = 0; i < 5; i++)
            _ = cache[1];                       // a high frequency cannot save it: there is no room

        cache.Add(2, "two");
        Assert.Equal(1, cache.Count);
        Assert.False(cache.ContainsKey(1));
        Assert.True(cache.ContainsKey(2));
        Assert.True(cache.TryGetFrequency(2, out long frequency));
        Assert.Equal(1L, frequency);
    }

    [Fact]
    public void DefaultKey_IsSupported()
    {
        // 0 is default(int); the underlying index stores it out-of-band. Exercise the full surface.
        var cache = NewCache(3);
        cache[0] = "zero";
        Assert.True(cache.ContainsKey(0));
        Assert.Equal("zero", cache[0]);
        Assert.True(cache.TryGet(0, out string? v));
        Assert.Equal("zero", v);
        Assert.True(cache.TryGetFrequency(0, out long frequency));
        Assert.Equal(3L, frequency);            // insert + indexer get + TryGet
        Assert.True(cache.Remove(0, out string? removed));
        Assert.Equal("zero", removed);
        Assert.False(cache.ContainsKey(0));
    }

    [Fact]
    public void ReferenceKeys_WorkThroughDefaultHasher()
    {
        var cache = new LfuCache<string, int, DefaultHasher<string>>(3);
        cache["a"] = 1;
        cache["b"] = 2;
        Assert.Equal(1, cache["a"]);
        Assert.Equal(2, cache["b"]);
        Assert.True(cache.Remove("a"));
        Assert.False(cache.ContainsKey("a"));
    }

    [Fact]
    public void NullValues_AreStoredAndReturned()
    {
        var cache = new LfuCache<int, string?, Int32WangHasher>(2);
        cache[1] = null;
        Assert.True(cache.ContainsKey(1));
        Assert.True(cache.TryGet(1, out string? v));
        Assert.Null(v);
    }

    [Fact]
    public void SourceConstructor_SeedsLastSurvivors()
    {
        var source = new[]
        {
            new KeyValuePair<int, string?>(1, "one"),
            new KeyValuePair<int, string?>(2, "two"),
            new KeyValuePair<int, string?>(3, "three"),
            new KeyValuePair<int, string?>(4, "four"),
        };

        // Capacity 2, all distinct and all at frequency 1: ties break by recency, so each insert evicts
        // the oldest and the last two survive.
        var cache = new LfuCache<int, string?, Int32WangHasher>(2, source);
        Assert.Equal(2, cache.Count);
        Assert.True(cache.ContainsKey(3));
        Assert.True(cache.ContainsKey(4));
        Assert.False(cache.ContainsKey(1));
        Assert.True(cache.TryPeekLeastFrequentlyUsed(out int victim, out _));
        Assert.Equal(3, victim);
    }

    [Fact]
    public void SourceConstructor_DuplicateKey_RaisesFrequencyAndKeepsIt()
    {
        var source = new[]
        {
            new KeyValuePair<int, string?>(1, "one"),
            new KeyValuePair<int, string?>(1, "uno"),   // second use of key 1
            new KeyValuePair<int, string?>(2, "two"),
            new KeyValuePair<int, string?>(3, "three"),
        };

        // Capacity 2: key 1 reaches frequency 2, so the singletons 2 and 3 are evicted around it.
        var cache = new LfuCache<int, string?, Int32WangHasher>(2, source);
        Assert.Equal(2, cache.Count);
        Assert.True(cache.ContainsKey(1));
        Assert.True(cache.ContainsKey(3));
        Assert.False(cache.ContainsKey(2));
        Assert.Equal("uno", cache.TryPeek(1, out string? v) ? v : null);
        Assert.True(cache.TryGetFrequency(1, out long frequency));
        Assert.Equal(2L, frequency);
    }

    [Fact]
    public void SourceConstructor_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new LfuCache<int, string?, Int32WangHasher>(4, null!));
    }
}
