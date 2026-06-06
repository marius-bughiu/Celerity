using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// Issue #21 — ArrayPool-backed dictionary. Mirror of CelerityDictionaryTests for
// the functional surface, plus pooled-specific tests for the rent / return
// lifecycle (Dispose, double-dispose, use-after-dispose, resize returning old
// buffers, the over-provisioned-rent bounds, and reference-type clear-on-return).
public class PooledCelerityDictionaryTests
{
    [Fact]
    public void Indexer_ShouldInsertAndRetrieveValue()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[10] = 100;

        Assert.Equal(100, map[10]);
    }

    [Fact]
    public void Indexer_ShouldThrowException_WhenKeyDoesNotExist()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();
        Assert.Throws<KeyNotFoundException>(() => { var value = map[99]; });
    }

    [Fact]
    public void Indexer_ShouldOverwriteExistingValue()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[5] = 500;
        map[5] = 999; // Overwrite

        Assert.Equal(999, map[5]);
    }

    [Fact]
    public void ContainsKey_ShouldReturnTrue_WhenKeyExists()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[7] = 700;

        Assert.True(map.ContainsKey(7));
    }

    [Fact]
    public void ContainsKey_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();
        Assert.False(map.ContainsKey(7));
    }

    [Fact]
    public void Remove_ShouldDeleteKeyAndMakeItUnreachable()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[7] = 700;

        bool removed = map.Remove(7);
        Assert.True(removed);
        Assert.False(map.ContainsKey(7));

        Assert.Throws<KeyNotFoundException>(() => { var value = map[7]; });
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();
        Assert.False(map.Remove(7));
    }

    [Fact]
    public void Map_ShouldResize_WhenThresholdExceeded()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>(4);
        map[1] = 10;
        map[2] = 20;
        map[3] = 30;
        map[4] = 40; // Triggers resize

        Assert.Equal(4, map.Count);
        Assert.Equal(10, map[1]);
        Assert.Equal(20, map[2]);
        Assert.Equal(30, map[3]);
        Assert.Equal(40, map[4]);
    }

    [Fact]
    public void Indexer_ShouldHandleZeroIntKey()
    {
        using var map = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>();
        map[0] = "zero";

        Assert.True(map.ContainsKey(0));
        Assert.Equal("zero", map[0]);
        Assert.Single(map);
    }

    [Fact]
    public void Indexer_ShouldOverwriteZeroIntKey()
    {
        using var map = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>();
        map[0] = "zero";
        map[0] = "still-zero";

        Assert.Single(map);
        Assert.Equal("still-zero", map[0]);
    }

    [Fact]
    public void Remove_ShouldHandleZeroIntKey()
    {
        using var map = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>();
        map[0] = "zero";
        map[1] = "one";

        Assert.True(map.Remove(0));
        Assert.False(map.ContainsKey(0));
        Assert.True(map.ContainsKey(1));
        Assert.Single(map);
        Assert.False(map.Remove(0));
    }

    [Fact]
    public void DefaultKey_ShouldSurviveResize()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>(4);
        map[0] = -1;
        map[1] = 10;
        map[2] = 20;
        map[3] = 30;
        map[4] = 40; // Triggers resize while the default-key entry is live.

        Assert.Equal(5, map.Count);
        Assert.Equal(-1, map[0]);
        Assert.Equal(10, map[1]);
        Assert.Equal(40, map[4]);
    }

    [Fact]
    public void TryGetValue_ShouldReturnTrueAndValue_WhenKeyExists()
    {
        using var map = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>();
        map[42] = "answer";
        map[0] = "zero";

        Assert.True(map.TryGetValue(42, out var v1));
        Assert.Equal("answer", v1);

        Assert.True(map.TryGetValue(0, out var v2));
        Assert.Equal("zero", v2);
    }

    [Fact]
    public void TryGetValue_ShouldReturnFalseAndDefault_WhenKeyMissing()
    {
        using var map = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>();
        Assert.False(map.TryGetValue(42, out var v1));
        Assert.Null(v1);

        Assert.False(map.TryGetValue(0, out var v2));
        Assert.Null(v2);
    }

    [Fact]
    public void Clear_ShouldRemoveAllEntries()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();
        for (int i = 0; i < 32; i++)
            map[i] = i * i;

        map.Clear();

        Assert.Empty(map);
        for (int i = 0; i < 32; i++)
            Assert.False(map.ContainsKey(i));

        map[0] = 100;
        map[5] = 500;
        Assert.Equal(2, map.Count);
        Assert.Equal(100, map[0]);
        Assert.Equal(500, map[5]);
    }

    [Fact]
    public void Indexer_ShouldHandleGuidEmptyKey()
    {
        using var map = new PooledCelerityDictionary<Guid, string, GuidIdentityHasher>();
        map[Guid.Empty] = "empty";
        var other = Guid.NewGuid();
        map[other] = "other";

        Assert.True(map.ContainsKey(Guid.Empty));
        Assert.Equal("empty", map[Guid.Empty]);
        Assert.Equal("other", map[other]);
        Assert.Equal(2, map.Count);
    }

    [Fact]
    public void Indexer_ShouldThrowKeyNotFound_ForAbsentDefaultKey()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();

        Assert.Throws<KeyNotFoundException>(() => _ = map[0]);
    }

    [Fact]
    public void Clear_ShouldBeNoOp_WhenAlreadyEmpty()
    {
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();

        map.Clear(); // _count == 0 early-return path

        Assert.Empty(map);
    }

    [Fact]
    public void ContainsValue_ShouldFindStoredValuesAndDefaultKeyValue()
    {
        using var map = new PooledCelerityDictionary<int, string, Int32WangNaiveHasher>();
        map[1] = "one";
        map[0] = "zero"; // out-of-band default-key value

        Assert.True(map.ContainsValue("one"));
        Assert.True(map.ContainsValue("zero"));
        Assert.False(map.ContainsValue("missing"));
    }

    [Fact]
    public void IEnumerableConstructor_ShouldCopyAllPairs()
    {
        var source = new Dictionary<int, int> { [1] = 10, [2] = 20, [3] = 30 };
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>(source);

        Assert.Equal(3, map.Count);
        Assert.Equal(10, map[1]);
        Assert.Equal(20, map[2]);
        Assert.Equal(30, map[3]);
    }

    // ---------------------------------------------------------------
    //  Pooled-specific lifecycle tests
    // ---------------------------------------------------------------

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;

        map.Dispose();
        // Double-dispose must not throw and must not return the (now-pooled)
        // buffers a second time, which could hand the same array to two owners.
        var ex = Record.Exception(() => map.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void UseAfterDispose_ShouldThrowObjectDisposedException()
    {
        var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>();
        map[1] = 10;
        map.Dispose();

        Assert.Throws<ObjectDisposedException>(() => map[1] = 2);
        Assert.Throws<ObjectDisposedException>(() => _ = map[1]);
        Assert.Throws<ObjectDisposedException>(() => map.ContainsKey(1));
        Assert.Throws<ObjectDisposedException>(() => map.ContainsValue(10));
        Assert.Throws<ObjectDisposedException>(() => map.TryGetValue(1, out _));
        Assert.Throws<ObjectDisposedException>(() => map.Remove(1));
        Assert.Throws<ObjectDisposedException>(() => map.Add(2, 2));
        Assert.Throws<ObjectDisposedException>(() => map.TryAdd(2, 2));
        Assert.Throws<ObjectDisposedException>(() => map.Clear());
        Assert.Throws<ObjectDisposedException>(() => map.GetEnumerator());
    }

    [Fact]
    public void Resize_ShouldStayCorrect_AcrossManyGrowths_AndReturnOldBuffers()
    {
        // Each Resize rents a doubled buffer and returns the old one to the pool.
        // Driving many resizes from a tiny capacity stresses that the rent/return
        // dance never corrupts the table (a returned buffer handed back out as the
        // *next* rent must be re-cleared, which the type does).
        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>(capacity: 2);
        for (int i = 1; i <= 1000; i++)
            map[i] = i * 3;

        Assert.Equal(1000, map.Count);
        for (int i = 1; i <= 1000; i++)
            Assert.Equal(i * 3, map[i]);
    }

    [Fact]
    public void RentedBuffer_OverProvisioning_DoesNotLeakGarbageIntoEnumeration()
    {
        // ArrayPool.Rent may hand back an array larger than requested; the tail
        // beyond the logical size is never cleared and holds whatever the last
        // tenant left. Enumeration / Count / ContainsValue must bound by the
        // logical size, not array.Length, so that garbage never surfaces.
        // Repeatedly building and disposing primes the pool with dirty buffers.
        for (int round = 0; round < 5; round++)
        {
            using var primer = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>(64);
            for (int i = 1; i <= 40; i++)
                primer[i] = i;
        }

        using var map = new PooledCelerityDictionary<int, int, Int32WangNaiveHasher>(8);
        map[100] = 1;
        map[200] = 2;

        int entries = 0;
        foreach (var kvp in map)
        {
            Assert.True(kvp.Key == 100 || kvp.Key == 200, $"Unexpected key {kvp.Key} from rented tail.");
            entries++;
        }

        Assert.Equal(2, entries);
        Assert.Equal(2, map.Count);
        Assert.False(map.ContainsValue(999));
    }

    [Fact]
    public void ReferenceTypeValues_ShouldBeReleased_OnDispose()
    {
        // Reference-type buffers are cleared on return so the pool does not pin
        // the values after disposal. We can't observe the pool directly, but we
        // can confirm the round-trip works and dispose completes cleanly for a
        // reference value type (the clear-on-return path).
        var map = new PooledCelerityDictionary<string, object, StringFnV1AHasher>();
        var payload = new object();
        map["k"] = payload;
        Assert.Same(payload, map["k"]);

        var ex = Record.Exception(() => map.Dispose());
        Assert.Null(ex);
    }

    private struct GuidIdentityHasher : IHashProvider<Guid>
    {
        public int Hash(Guid key) => key.GetHashCode();
    }
}
