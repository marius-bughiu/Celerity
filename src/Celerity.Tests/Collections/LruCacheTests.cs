using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Core behavioural coverage for <see cref="LruCache{TKey, TValue, THasher}"/>: construction,
/// capacity validation, the get/put/peek surface, add/remove/clear, and the default-key path.
/// Recency-driven eviction ordering lives in <c>LruCacheEvictionTests</c>; enumeration order and
/// version invalidation live in <c>LruCacheEnumerationTests</c>; the randomized oracle check lives
/// in <c>LruCacheDifferentialTests</c>.
/// </summary>
public class LruCacheTests
{
    private static LruCache<int, string, Int32WangHasher> NewCache(int capacity = 4)
        => new(capacity);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_RejectsNonPositiveCapacity(int capacity)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new LruCache<int, string, Int32WangHasher>(capacity));
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
        Assert.False(cache.TryPeekLeastRecentlyUsed(out _, out _));
        Assert.False(cache.TryPeekMostRecentlyUsed(out _, out _));
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
    public void TryPeek_DoesNotChangeRecency()
    {
        var cache = NewCache(2);
        cache[1] = "one";
        cache[2] = "two"; // MRU order: 2, 1  (1 is LRU)

        // Peeking 1 must NOT promote it; inserting a third key still evicts 1.
        Assert.True(cache.TryPeek(1, out _));
        cache[3] = "three";

        Assert.False(cache.ContainsKey(1));
        Assert.True(cache.ContainsKey(2));
        Assert.True(cache.ContainsKey(3));
    }

    [Fact]
    public void ContainsKey_DoesNotChangeRecency()
    {
        var cache = NewCache(2);
        cache[1] = "one";
        cache[2] = "two"; // 1 is LRU

        Assert.True(cache.ContainsKey(1));
        cache[3] = "three"; // still evicts 1, because ContainsKey is a peek

        Assert.False(cache.ContainsKey(1));
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
    public void TryAdd_ReturnsFalseOnDuplicate_AndLeavesValueUnchanged()
    {
        var cache = NewCache();
        Assert.True(cache.TryAdd(1, "one"));
        Assert.False(cache.TryAdd(1, "two"));
        Assert.Equal(1, cache.Count);
        Assert.Equal("one", cache.TryPeek(1, out string? v) ? v : null);
    }

    [Fact]
    public void AddOrUpdate_InsertsThenUpdates()
    {
        var cache = NewCache();
        cache.AddOrUpdate(1, "one");
        Assert.Equal("one", cache[1]);
        cache.AddOrUpdate(1, "uno");
        Assert.Equal("uno", cache[1]);
        Assert.Equal(1, cache.Count);
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
    public void Clear_EmptiesCacheButKeepsCapacity()
    {
        var cache = NewCache(3);
        cache[1] = "one";
        cache[2] = "two";
        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.Equal(3, cache.Capacity);
        Assert.False(cache.ContainsKey(1));

        // The cache is fully usable after Clear (free stack rebuilt).
        cache[9] = "nine";
        Assert.Equal("nine", cache[9]);
    }

    [Fact]
    public void PeekLruAndMru_TrackRecency()
    {
        var cache = NewCache(3);
        cache[1] = "one";
        cache[2] = "two";
        cache[3] = "three"; // MRU=3 ... LRU=1

        Assert.True(cache.TryPeekMostRecentlyUsed(out int mruK, out string? mruV));
        Assert.Equal(3, mruK);
        Assert.Equal("three", mruV);

        Assert.True(cache.TryPeekLeastRecentlyUsed(out int lruK, out string? lruV));
        Assert.Equal(1, lruK);
        Assert.Equal("one", lruV);

        // A get on 1 promotes it to MRU; 2 becomes the new LRU.
        _ = cache[1];
        Assert.True(cache.TryPeekMostRecentlyUsed(out int mruK2, out _));
        Assert.Equal(1, mruK2);
        Assert.True(cache.TryPeekLeastRecentlyUsed(out int lruK2, out _));
        Assert.Equal(2, lruK2);
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
        Assert.True(cache.Remove(0, out string? removed));
        Assert.Equal("zero", removed);
        Assert.False(cache.ContainsKey(0));
    }

    [Fact]
    public void ReferenceKeys_WorkThroughDefaultHasher()
    {
        var cache = new LruCache<string, int, DefaultHasher<string>>(3);
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
        var cache = new LruCache<int, string?, Int32WangHasher>(2);
        cache[1] = null;
        Assert.True(cache.ContainsKey(1));
        Assert.True(cache.TryGet(1, out string? v));
        Assert.Null(v);
    }

    [Fact]
    public void SourceConstructor_SeedsMostRecentSurvivors()
    {
        var source = new[]
        {
            new KeyValuePair<int, string?>(1, "one"),
            new KeyValuePair<int, string?>(2, "two"),
            new KeyValuePair<int, string?>(3, "three"),
            new KeyValuePair<int, string?>(4, "four"),
        };

        // Capacity 2: only the last two distinct keys survive, MRU=4, LRU=3.
        var cache = new LruCache<int, string?, Int32WangHasher>(2, source);
        Assert.Equal(2, cache.Count);
        Assert.True(cache.ContainsKey(3));
        Assert.True(cache.ContainsKey(4));
        Assert.False(cache.ContainsKey(1));
        Assert.True(cache.TryPeekLeastRecentlyUsed(out int lru, out _));
        Assert.Equal(3, lru);
    }

    [Fact]
    public void SourceConstructor_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new LruCache<int, string?, Int32WangHasher>(4, null!));
    }
}
