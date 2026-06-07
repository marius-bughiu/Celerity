using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// Dedicated tests for SwissDictionary (issue #64), mirroring
// CelerityDictionaryTests / RobinHoodDictionaryTests. SwissDictionary is a
// drop-in peer of CelerityDictionary that resolves collisions with
// SIMD-accelerated group probing, so it must satisfy the same functional
// contract; the algorithm-specific stress (group probing, tombstones, resize)
// is in SwissDictionaryCollisionTests.
public class SwissDictionaryTests
{
    [Fact]
    public void Indexer_ShouldInsertAndRetrieveValue()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>();
        map[10] = 100;

        Assert.Equal(100, map[10]);
    }

    [Fact]
    public void Indexer_ShouldThrowException_WhenKeyDoesNotExist()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>();
        Assert.Throws<KeyNotFoundException>(() => { var value = map[99]; });
    }

    [Fact]
    public void Indexer_ShouldOverwriteExistingValue()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>();
        map[5] = 500;
        map[5] = 999; // Overwrite

        Assert.Equal(999, map[5]);
        Assert.Single(map);
    }

    [Fact]
    public void ContainsKey_ShouldReturnTrue_WhenKeyExists()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>();
        map[7] = 700;

        Assert.True(map.ContainsKey(7));
    }

    [Fact]
    public void ContainsKey_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>();
        Assert.False(map.ContainsKey(7));
    }

    [Fact]
    public void Remove_ShouldDeleteKeyAndMakeItUnreachable()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>();
        map[7] = 700;

        bool removed = map.Remove(7);
        Assert.True(removed);
        Assert.False(map.ContainsKey(7));

        Assert.Throws<KeyNotFoundException>(() => { var value = map[7]; });
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenKeyDoesNotExist()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>();
        Assert.False(map.Remove(7));
    }

    [Fact]
    public void Map_ShouldResize_WhenThresholdExceeded()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>(16, loadFactor: 0.5f);
        for (int i = 1; i <= 40; i++)
            map[i] = i * 10; // forces at least one resize

        Assert.Equal(40, map.Count);
        for (int i = 1; i <= 40; i++)
            Assert.Equal(i * 10, map[i]);
    }

    // default(TKey) is stored out-of-band so the hasher is never invoked with it.
    // For int keys, default(int) is 0.
    [Fact]
    public void Indexer_ShouldHandleZeroIntKey()
    {
        var map = new SwissDictionary<int, string, Int32WangNaiveHasher>();
        map[0] = "zero";

        Assert.True(map.ContainsKey(0));
        Assert.Equal("zero", map[0]);
        Assert.Single(map);
    }

    [Fact]
    public void Indexer_ShouldOverwriteZeroIntKey()
    {
        var map = new SwissDictionary<int, string, Int32WangNaiveHasher>();
        map[0] = "zero";
        map[0] = "still-zero";

        Assert.Single(map);
        Assert.Equal("still-zero", map[0]);
    }

    [Fact]
    public void Remove_ShouldHandleZeroIntKey()
    {
        var map = new SwissDictionary<int, string, Int32WangNaiveHasher>();
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
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>(16, loadFactor: 0.5f);
        map[0] = -1;
        for (int i = 1; i <= 40; i++)
            map[i] = i * 10; // triggers resize while the default-key entry is live.

        Assert.Equal(41, map.Count);
        Assert.Equal(-1, map[0]);
        Assert.Equal(10, map[1]);
        Assert.Equal(400, map[40]);
    }

    [Fact]
    public void TryGetValue_ShouldReturnTrueAndValue_WhenKeyExists()
    {
        var map = new SwissDictionary<int, string, Int32WangNaiveHasher>();
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
        var map = new SwissDictionary<int, string, Int32WangNaiveHasher>();
        Assert.False(map.TryGetValue(42, out var v1));
        Assert.Null(v1);

        Assert.False(map.TryGetValue(0, out var v2));
        Assert.Null(v2);
    }

    [Fact]
    public void Clear_ShouldRemoveAllEntries()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>();
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
        var map = new SwissDictionary<Guid, string, GuidIdentityHasher>();
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
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>();

        Assert.Throws<KeyNotFoundException>(() => _ = map[0]);
    }

    [Fact]
    public void Clear_ShouldBeNoOp_WhenAlreadyEmpty()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>();

        map.Clear(); // _count == 0 early-return path

        Assert.Empty(map);
    }

    [Fact]
    public void TryAdd_ShouldReturnFalse_WhenKeyExists()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>();
        Assert.True(map.TryAdd(1, 10));
        Assert.False(map.TryAdd(1, 20));

        Assert.Equal(10, map[1]); // unchanged
        Assert.Single(map);
    }

    [Fact]
    public void Add_ShouldThrow_WhenKeyExists()
    {
        var map = new SwissDictionary<int, int, Int32WangNaiveHasher>();
        map.Add(1, 10);

        Assert.Throws<ArgumentException>(() => map.Add(1, 20));
    }

    // A test-only hasher used to exercise the Guid path. Uses the GUID's hash
    // code; not part of the public library.
    private struct GuidIdentityHasher : IHashProvider<Guid>
    {
        public int Hash(Guid key) => key.GetHashCode();
    }
}
