using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// ArrayPool-backed set — the set counterpart of PooledCelerityDictionary
// (issue #21). Mirror of CeleritySetTests for the functional surface, plus
// pooled-specific tests for the rent / return lifecycle (Dispose,
// double-dispose, use-after-dispose, resize returning old buffers, the
// over-provisioned-rent bounds, and reference-type clear-on-return).
public class PooledCeleritySetTests
{
    [Fact]
    public void TryAdd_ShouldAddAndContain()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        Assert.True(set.TryAdd(10));
        Assert.True(set.Contains(10));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void TryAdd_ShouldReturnFalse_WhenDuplicate()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        Assert.True(set.TryAdd(10));
        Assert.False(set.TryAdd(10));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Add_ShouldThrow_WhenDuplicate()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        set.Add(10);
        Assert.Throws<ArgumentException>(() => set.Add(10));
    }

    [Fact]
    public void Contains_ShouldReturnFalse_WhenNotPresent()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        Assert.False(set.Contains(99));
    }

    [Fact]
    public void Remove_ShouldDeleteElement()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        set.Add(7);

        Assert.True(set.Remove(7));
        Assert.False(set.Contains(7));
        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenNotPresent()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        Assert.False(set.Remove(7));
    }

    [Fact]
    public void Set_ShouldResize_WhenThresholdExceeded()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>(4);
        set.Add(1);
        set.Add(2);
        set.Add(3);
        set.Add(4); // Triggers resize

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(1));
        Assert.True(set.Contains(2));
        Assert.True(set.Contains(3));
        Assert.True(set.Contains(4));
    }

    // Regression: default(T) collides with the "empty slot" sentinel.
    [Fact]
    public void TryAdd_ShouldHandleZeroIntKey()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        Assert.True(set.TryAdd(0));
        Assert.True(set.Contains(0));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void TryAdd_ZeroIntKey_ShouldReturnFalse_WhenDuplicate()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        Assert.True(set.TryAdd(0));
        Assert.False(set.TryAdd(0));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Remove_ShouldHandleZeroIntKey()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        set.Add(0);
        set.Add(1);

        Assert.True(set.Remove(0));
        Assert.False(set.Contains(0));
        Assert.True(set.Contains(1));
        Assert.Equal(1, set.Count);
        Assert.False(set.Remove(0));
    }

    [Fact]
    public void DefaultKey_ShouldSurviveResize()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>(4);
        set.Add(0);
        set.Add(1);
        set.Add(2);
        set.Add(3);
        set.Add(4); // Triggers resize while the default-key entry is live.

        Assert.Equal(5, set.Count);
        Assert.True(set.Contains(0));
        Assert.True(set.Contains(1));
        Assert.True(set.Contains(4));
    }

    [Fact]
    public void Clear_ShouldRemoveAllElements()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        for (int i = 0; i < 32; i++)
            set.Add(i);

        set.Clear();

        Assert.Equal(0, set.Count);
        for (int i = 0; i < 32; i++)
            Assert.False(set.Contains(i));

        // Reusable after clear.
        set.Add(0);
        set.Add(5);
        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(0));
        Assert.True(set.Contains(5));
    }

    // Guid.Empty == default(Guid)
    [Fact]
    public void GuidEmpty_ShouldRoundTrip()
    {
        using var set = new PooledCeleritySet<Guid, GuidIdentityHasher>();
        set.Add(Guid.Empty);
        var other = Guid.NewGuid();
        set.Add(other);

        Assert.True(set.Contains(Guid.Empty));
        Assert.True(set.Contains(other));
        Assert.Equal(2, set.Count);
    }

    [Fact]
    public void RemoveThenReinsert_ManyElements()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>(8);
        for (int i = 1; i <= 100; i++)
            set.Add(i);

        for (int i = 1; i <= 100; i += 2)
            Assert.True(set.Remove(i));

        Assert.Equal(50, set.Count);

        for (int i = 1; i <= 100; i += 2)
            set.Add(i);

        Assert.Equal(100, set.Count);
        for (int i = 1; i <= 100; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void IEnumerableConstructor_ShouldCopyAllElements()
    {
        var source = new[] { 1, 2, 3, 0 };
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>(source);

        Assert.Equal(4, set.Count);
        foreach (int item in source)
            Assert.True(set.Contains(item));
    }

    // ---------------------------------------------------------------
    //  Pooled-specific lifecycle tests
    // ---------------------------------------------------------------

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        set.Add(1);

        set.Dispose();
        // Double-dispose must not throw and must not return the (now-pooled)
        // buffer a second time, which could hand the same array to two owners.
        var ex = Record.Exception(() => set.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void UseAfterDispose_ShouldThrowObjectDisposedException()
    {
        var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        set.Add(1);
        set.Dispose();

        Assert.Throws<ObjectDisposedException>(() => set.Add(2));
        Assert.Throws<ObjectDisposedException>(() => set.TryAdd(2));
        Assert.Throws<ObjectDisposedException>(() => set.Contains(1));
        Assert.Throws<ObjectDisposedException>(() => set.Remove(1));
        Assert.Throws<ObjectDisposedException>(() => set.Clear());
        Assert.Throws<ObjectDisposedException>(() => set.EnsureCapacity(64));
        Assert.Throws<ObjectDisposedException>(() => set.TrimExcess());
        Assert.Throws<ObjectDisposedException>(() => set.GetEnumerator());
        Assert.Throws<ObjectDisposedException>(() => set.UnionWith(new[] { 3 }));
        Assert.Throws<ObjectDisposedException>(() => set.IntersectWith(new[] { 3 }));
        Assert.Throws<ObjectDisposedException>(() => set.ExceptWith(new[] { 3 }));
        Assert.Throws<ObjectDisposedException>(() => set.SymmetricExceptWith(new[] { 3 }));
        Assert.Throws<ObjectDisposedException>(() => set.IsSubsetOf(new[] { 3 }));
        Assert.Throws<ObjectDisposedException>(() => set.IsSupersetOf(new[] { 3 }));
        Assert.Throws<ObjectDisposedException>(() => set.Overlaps(new[] { 3 }));
        Assert.Throws<ObjectDisposedException>(() => set.SetEquals(new[] { 3 }));
        Assert.Throws<ObjectDisposedException>(() => set.CopyTo(new int[4], 0));
    }

    [Fact]
    public void Resize_ShouldStayCorrect_AcrossManyGrowths_AndReturnOldBuffers()
    {
        // Each Resize rents a doubled buffer and returns the old one to the pool.
        // Driving many resizes from a tiny capacity stresses that the rent/return
        // dance never corrupts the table (a returned buffer handed back out as the
        // *next* rent must be re-cleared, which the type does).
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>(capacity: 2);
        for (int i = 1; i <= 1000; i++)
            set.Add(i);

        Assert.Equal(1000, set.Count);
        for (int i = 1; i <= 1000; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void RentedBuffer_OverProvisioning_DoesNotLeakGarbageIntoEnumeration()
    {
        // ArrayPool.Rent may hand back an array larger than requested; the tail
        // beyond the logical size is never cleared and holds whatever the last
        // tenant left. Enumeration / Count must bound by the logical size, not
        // slots.Length, so that garbage never surfaces. Repeatedly building and
        // disposing primes the pool with dirty buffers.
        for (int round = 0; round < 5; round++)
        {
            using var primer = new PooledCeleritySet<int, Int32WangNaiveHasher>(64);
            for (int i = 1; i <= 40; i++)
                primer.Add(i);
        }

        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>(8);
        set.Add(100);
        set.Add(200);

        int entries = 0;
        foreach (int item in set)
        {
            Assert.True(item == 100 || item == 200, $"Unexpected item {item} from rented tail.");
            entries++;
        }

        Assert.Equal(2, entries);
        Assert.Equal(2, set.Count);
    }

    [Fact]
    public void ReferenceTypeElements_ShouldBeReleased_OnDispose()
    {
        // Reference-type buffers are cleared on return so the pool does not pin
        // the elements after disposal. We can't observe the pool directly, but we
        // can confirm the round-trip works and dispose completes cleanly for a
        // reference element type (the clear-on-return path).
        var set = new PooledCeleritySet<string, StringFnV1AHasher>();
        set.Add("payload");
        Assert.True(set.Contains("payload"));

        var ex = Record.Exception(() => set.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void EnsureCapacity_ThenTrimExcess_ReturnsBuffers_AndPreservesContents()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        Assert.True(set.EnsureCapacity(256) >= 256);
        for (int i = 1; i <= 10; i++)
            set.Add(i);

        set.TrimExcess();

        Assert.Equal(10, set.Count);
        for (int i = 1; i <= 10; i++)
            Assert.True(set.Contains(i));
        set.Add(11); // still usable after the shrink
        Assert.True(set.Contains(11));
    }

    private struct GuidIdentityHasher : IHashProvider<Guid>
    {
        public int Hash(Guid key) => key.GetHashCode();
    }
}
