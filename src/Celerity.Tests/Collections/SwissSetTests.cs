using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// Dedicated tests for SwissSet, mirroring CeleritySetTests and the SwissDictionary
// dedicated tests. SwissSet is a drop-in peer of CeleritySet that resolves
// collisions with SIMD-accelerated group probing, so it must satisfy the same
// functional contract; the algorithm-specific stress (group probing, tombstones,
// resize) lives in SwissSetCollisionTests.
public class SwissSetTests
{
    [Fact]
    public void TryAdd_ShouldAddAndContain()
    {
        var set = new SwissSet<int, Int32WangNaiveHasher>();
        Assert.True(set.TryAdd(10));
        Assert.True(set.Contains(10));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void TryAdd_ShouldReturnFalse_WhenDuplicate()
    {
        var set = new SwissSet<int, Int32WangNaiveHasher>();
        Assert.True(set.TryAdd(10));
        Assert.False(set.TryAdd(10));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Add_ShouldThrow_WhenDuplicate()
    {
        var set = new SwissSet<int, Int32WangNaiveHasher>();
        set.Add(10);
        Assert.Throws<ArgumentException>(() => set.Add(10));
    }

    [Fact]
    public void Contains_ShouldReturnFalse_WhenNotPresent()
    {
        var set = new SwissSet<int, Int32WangNaiveHasher>();
        Assert.False(set.Contains(99));
    }

    [Fact]
    public void Remove_ShouldDeleteElement()
    {
        var set = new SwissSet<int, Int32WangNaiveHasher>();
        set.Add(7);

        Assert.True(set.Remove(7));
        Assert.False(set.Contains(7));
        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenNotPresent()
    {
        var set = new SwissSet<int, Int32WangNaiveHasher>();
        Assert.False(set.Remove(7));
    }

    [Fact]
    public void Set_ShouldResize_WhenThresholdExceeded()
    {
        var set = new SwissSet<int, Int32WangNaiveHasher>(16, loadFactor: 0.5f);
        for (int i = 1; i <= 40; i++)
            set.Add(i); // forces at least one resize

        Assert.Equal(40, set.Count);
        for (int i = 1; i <= 40; i++)
            Assert.True(set.Contains(i));
    }

    // Regression: default(T) collides with the "empty slot" sentinel.
    [Fact]
    public void TryAdd_ShouldHandleZeroIntKey()
    {
        var set = new SwissSet<int, Int32WangNaiveHasher>();
        Assert.True(set.TryAdd(0));
        Assert.True(set.Contains(0));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void TryAdd_ZeroIntKey_ShouldReturnFalse_WhenDuplicate()
    {
        var set = new SwissSet<int, Int32WangNaiveHasher>();
        Assert.True(set.TryAdd(0));
        Assert.False(set.TryAdd(0));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Contains_ShouldReturnFalse_ForAbsentDefaultKey()
    {
        var set = new SwissSet<int, Int32WangNaiveHasher>();
        Assert.False(set.Contains(0));
    }

    [Fact]
    public void Remove_ShouldHandleZeroIntKey()
    {
        var set = new SwissSet<int, Int32WangNaiveHasher>();
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
        var set = new SwissSet<int, Int32WangNaiveHasher>(16, loadFactor: 0.5f);
        set.Add(0);
        for (int i = 1; i <= 40; i++)
            set.Add(i); // triggers resize while the default-key entry is live.

        Assert.Equal(41, set.Count);
        Assert.True(set.Contains(0));
        Assert.True(set.Contains(1));
        Assert.True(set.Contains(40));
    }

    [Fact]
    public void Clear_ShouldRemoveAllElements()
    {
        var set = new SwissSet<int, Int32WangNaiveHasher>();
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

    [Fact]
    public void Clear_ShouldBeNoOp_WhenAlreadyEmpty()
    {
        var set = new SwissSet<int, Int32WangNaiveHasher>();

        set.Clear(); // _count == 0 early-return path

        Assert.Equal(0, set.Count);
    }

    // Guid.Empty == default(Guid)
    [Fact]
    public void GuidEmpty_ShouldRoundTrip()
    {
        var set = new SwissSet<Guid, GuidIdentityHasher>();
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
        var set = new SwissSet<int, Int32WangNaiveHasher>(16);
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
    public void ReferenceType_Null_ShouldRoundTrip()
    {
        var set = new SwissSet<string, StringFnV1AHasher>();
        Assert.True(set.TryAdd(null!));
        Assert.True(set.Contains(null!));
        Assert.Equal(1, set.Count);
        Assert.False(set.TryAdd(null!));
        Assert.True(set.Remove(null!));
        Assert.False(set.Contains(null!));
    }

    private struct GuidIdentityHasher : IHashProvider<Guid>
    {
        public int Hash(Guid key) => key.GetHashCode();
    }
}
