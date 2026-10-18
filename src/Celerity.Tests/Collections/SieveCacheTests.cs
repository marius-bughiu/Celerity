using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Core behavioural coverage for <see cref="SieveCache{TKey, TValue, THasher}"/>: construction,
/// capacity validation, the get/put/peek surface, add/remove/clear, and the default-key path.
/// The eviction policy and the hand live in <c>SieveCacheEvictionTests</c>; enumeration order and
/// version invalidation live in <c>SieveCacheEnumerationTests</c>; the CsCheck oracle check lives
/// in <c>SieveCacheDifferentialTests</c>.
/// </summary>
public class SieveCacheTests
{
    private static SieveCache<int, string, Int32WangHasher> NewCache(int capacity = 4)
        => new(capacity);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_RejectsNonPositiveCapacity(int capacity)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SieveCache<int, string, Int32WangHasher>(capacity));
        Assert.Equal("capacity", ex.ParamName);
    }

    [Fact]
    public void SourceConstructor_RejectsNonPositiveCapacity()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new SieveCache<int, string?, Int32WangHasher>(0, Array.Empty<KeyValuePair<int, string?>>()));
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
    public void TryPeek_HitAndMiss()
    {
        var cache = NewCache();
        cache[7] = "seven";

        Assert.True(cache.TryPeek(7, out string? hit));
        Assert.Equal("seven", hit);
        Assert.False(cache.TryPeek(8, out string? miss));
        Assert.Null(miss);
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
        Assert.False(cache.Remove(2));
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
    public void Remove_EveryEntry_LeavesAnEmptyUsableCache()
    {
        var cache = NewCache(3);
        cache[1] = "one";
        cache[2] = "two";
        cache[3] = "three";
        Assert.True(cache.Remove(2)); // middle
        Assert.True(cache.Remove(3)); // head
        Assert.True(cache.Remove(1)); // tail, and the last entry
        Assert.Equal(0, cache.Count);

        cache[4] = "four";
        cache[5] = "five";
        cache[6] = "six";
        cache[7] = "seven";
        Assert.Equal(3, cache.Count);
        Assert.False(cache.ContainsKey(4));
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
    public void Clear_OnEmptyCache_IsANoOp()
    {
        var cache = NewCache(2);
        cache.Clear();
        Assert.Equal(0, cache.Count);
        cache[1] = "one";
        Assert.Equal("one", cache[1]);
    }

    [Fact]
    public void DefaultKey_IsSupported()
    {
        // 0 is default(int), which an index must not mistake for a vacant slot. Exercise the full surface.
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
    public void ReferenceKeys_ShouldResolve_WhenEveryKeySharesOneChain()
    {
        // "a" and "b" share a hash; "cc" and "dd" share the other. A lookup walks past both kinds of
        // neighbour, and removing a key from the middle of the chain must keep the rest reachable.
        var cache = new SieveCache<string, int, CacheCollidingStringHasher>(4);
        cache["a"] = 1;
        cache["cc"] = 2;
        cache["b"] = 3;
        cache["dd"] = 4;
        Assert.Equal(1, cache["a"]);
        Assert.Equal(2, cache["cc"]);
        Assert.False(cache.ContainsKey("e"));
        Assert.True(cache.Remove("cc"));
        Assert.False(cache.ContainsKey("cc"));
        Assert.Equal(3, cache["b"]);
        Assert.Equal(4, cache["dd"]);
        Assert.Equal(1, cache["a"]);
    }

    [Fact]
    public void NullKey_ShouldBeSupported_WithoutReachingTheHasher()
    {
        // A null key hashes to a fixed bucket inside the cache, so a hasher that rejects null is fine.
        var cache = new SieveCache<string, int, NullRejectingStringHasher>(2);
        cache[null!] = 1;
        cache["a"] = 2;
        Assert.True(cache.ContainsKey(null!));
        Assert.Equal(1, cache[null!]);
        Assert.True(cache.Remove(null!, out int removed));
        Assert.Equal(1, removed);
        Assert.False(cache.ContainsKey(null!));
        Assert.Equal(2, cache["a"]);
    }

    [Fact]
    public void ReferenceKeys_WorkThroughDefaultHasher()
    {
        var cache = new SieveCache<string, int, DefaultHasher<string>>(3);
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
        var cache = new SieveCache<int, string?, Int32WangHasher>(2);
        cache[1] = null;
        Assert.True(cache.ContainsKey(1));
        Assert.True(cache.TryGet(1, out string? v));
        Assert.Null(v);
    }

    [Fact]
    public void SourceConstructor_InsertsInOrder_AndEvictsUnderSieve()
    {
        var source = new[]
        {
            new KeyValuePair<int, string?>(1, "one"),
            new KeyValuePair<int, string?>(2, "two"),
            new KeyValuePair<int, string?>(1, "uno"),   // overwrite: counts as a use of 1
            new KeyValuePair<int, string?>(3, "three"), // full: the hand clears 1 and evicts 2
        };

        var cache = new SieveCache<int, string?, Int32WangHasher>(2, source);
        Assert.Equal(2, cache.Count);
        Assert.True(cache.TryPeek(1, out string? one));
        Assert.Equal("uno", one);
        Assert.True(cache.ContainsKey(3));
        Assert.False(cache.ContainsKey(2));
    }

    [Fact]
    public void SourceConstructor_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SieveCache<int, string?, Int32WangHasher>(4, null!));
    }

    [Fact]
    public void SteadyStateChurn_DoesNotAllocate()
    {
        // Issue #542, kill criterion 3: once constructed, eviction churn allocates nothing.
        var cache = new SieveCache<int, int, Int32WangHasher>(64);
        for (int i = 0; i < 64; i++)
            cache[i] = i;
        for (int i = 0; i < 64; i += 3)
            _ = cache[i];

        // Warm every path (hit, miss, evicting insert, overwrite, remove, re-add) before measuring.
        Churn(cache, 1000);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Churn(cache, 10_000);
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);

        static void Churn(SieveCache<int, int, Int32WangHasher> c, int ops)
        {
            for (int i = 0; i < ops; i++)
            {
                int k = (i * 7919) % 256;
                if (!c.TryGet(k, out _))
                    c[k] = i;
                if ((i & 15) == 0)
                    c.Remove(k);
            }
        }
    }
}
