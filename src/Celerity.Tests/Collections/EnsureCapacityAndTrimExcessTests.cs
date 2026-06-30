using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Cross-collection coverage for the <c>EnsureCapacity</c> / <c>TrimExcess</c> capacity-management
/// surface (issue #231) on every mutable hash-table type in the family.
///
/// <para>
/// Two behaviours are pinned for each type:
/// </para>
/// <list type="number">
/// <item><description>
/// <b>EnsureCapacity pre-sizes away resizes.</b> The same <see cref="IHashProvider{T}"/>-call
/// counting technique as <see cref="BulkConstructorNoResizeTests"/>: after
/// <c>EnsureCapacity(N)</c> on an empty collection, inserting <c>N</c> distinct keys one at a time
/// costs exactly <c>N</c> hash calls — a resize would re-hash the live entries and push the count
/// above <c>N</c>. An unsized collection (default capacity) resizes several times over the same
/// fill, so <c>== N</c> only holds once the table has been pre-grown.
/// </description></item>
/// <item><description>
/// <b>TrimExcess shrinks without losing data.</b> After filling a collection past several resizes
/// and removing all but a handful of entries, <c>TrimExcess()</c> rehashes down to a small table;
/// the surviving entries must still be present and correct, the count unchanged, and subsequent
/// inserts must still work (a too-small target would orphan entries or hang).
/// </description></item>
/// </list>
///
/// Argument validation is pinned on a representative subset; the guard is identical code across the
/// family (a negative capacity, or a capacity below the current count, throws
/// <see cref="ArgumentOutOfRangeException"/>).
/// </summary>
public class EnsureCapacityAndTrimExcessTests
{
    private const int N = 100;

    private static int _hashCallCount;

    private struct CountingIntHasher : IHashProvider<int>
    {
        public int Hash(int key)
        {
            _hashCallCount++;
            unchecked
            {
                uint x = (uint)key;
                x = ((x >> 16) ^ x) * 0x45d9f3b;
                x = ((x >> 16) ^ x) * 0x45d9f3b;
                x = (x >> 16) ^ x;
                return (int)x;
            }
        }
    }

    private struct CountingLongHasher : IHashProvider<long>
    {
        public int Hash(long key)
        {
            _hashCallCount++;
            unchecked
            {
                ulong x = (ulong)key;
                x = (x ^ (x >> 30)) * 0xbf58476d1ce4e5b9UL;
                x = (x ^ (x >> 27)) * 0x94d049bb133111ebUL;
                x = x ^ (x >> 31);
                return (int)x;
            }
        }
    }

    private struct CountingStringHasher : IHashProvider<string>
    {
        public int Hash(string key)
        {
            _hashCallCount++;
            return key.GetHashCode();
        }
    }

    // ── EnsureCapacity: pre-sizing eliminates per-insert resizes ────────────────────

    [Fact]
    public void IntDictionary_EnsureCapacity_PreSized_BulkInsert_DoesNotResize()
    {
        var map = new IntDictionary<int, CountingIntHasher>();
        int reported = map.EnsureCapacity(N);
        Assert.True(reported >= N);

        _hashCallCount = 0;
        for (int i = 1; i <= N; i++)
            map[i] = i * 10;

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, map.Count);
        for (int i = 1; i <= N; i++)
            Assert.Equal(i * 10, map[i]);
    }

    [Fact]
    public void LongDictionary_EnsureCapacity_PreSized_BulkInsert_DoesNotResize()
    {
        var map = new LongDictionary<int, CountingLongHasher>();
        Assert.True(map.EnsureCapacity(N) >= N);

        _hashCallCount = 0;
        for (int i = 1; i <= N; i++)
            map[i] = i * 10;

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, map.Count);
        for (int i = 1; i <= N; i++)
            Assert.Equal(i * 10, map[i]);
    }

    [Fact]
    public void CelerityDictionary_EnsureCapacity_PreSized_BulkInsert_DoesNotResize()
    {
        var map = new CelerityDictionary<string, int, CountingStringHasher>();
        Assert.True(map.EnsureCapacity(N) >= N);

        _hashCallCount = 0;
        for (int i = 1; i <= N; i++)
            map[$"k{i}"] = i;

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, map.Count);
        for (int i = 1; i <= N; i++)
            Assert.Equal(i, map[$"k{i}"]);
    }

    [Fact]
    public void RobinHoodDictionary_EnsureCapacity_PreSized_BulkInsert_DoesNotResize()
    {
        var map = new RobinHoodDictionary<string, int, CountingStringHasher>();
        Assert.True(map.EnsureCapacity(N) >= N);

        _hashCallCount = 0;
        for (int i = 1; i <= N; i++)
            map[$"k{i}"] = i;

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, map.Count);
        for (int i = 1; i <= N; i++)
            Assert.Equal(i, map[$"k{i}"]);
    }

    [Fact]
    public void SwissDictionary_EnsureCapacity_PreSized_BulkInsert_DoesNotResize()
    {
        var map = new SwissDictionary<string, int, CountingStringHasher>();
        Assert.True(map.EnsureCapacity(N) >= N);

        _hashCallCount = 0;
        for (int i = 1; i <= N; i++)
            map[$"k{i}"] = i;

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, map.Count);
        for (int i = 1; i <= N; i++)
            Assert.Equal(i, map[$"k{i}"]);
    }

    [Fact]
    public void HashCachingDictionary_EnsureCapacity_PreSized_BulkInsert_DoesNotResize()
    {
        var map = new HashCachingDictionary<string, int, CountingStringHasher>();
        Assert.True(map.EnsureCapacity(N) >= N);

        _hashCallCount = 0;
        for (int i = 1; i <= N; i++)
            map[$"k{i}"] = i;

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, map.Count);
        for (int i = 1; i <= N; i++)
            Assert.Equal(i, map[$"k{i}"]);
    }

    [Fact]
    public void PooledCelerityDictionary_EnsureCapacity_PreSized_BulkInsert_DoesNotResize()
    {
        using var map = new PooledCelerityDictionary<string, int, CountingStringHasher>();
        Assert.True(map.EnsureCapacity(N) >= N);

        _hashCallCount = 0;
        for (int i = 1; i <= N; i++)
            map[$"k{i}"] = i;

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, map.Count);
        for (int i = 1; i <= N; i++)
            Assert.Equal(i, map[$"k{i}"]);
    }

    [Fact]
    public void IntSet_EnsureCapacity_PreSized_BulkInsert_DoesNotResize()
    {
        var set = new IntSet<CountingIntHasher>();
        Assert.True(set.EnsureCapacity(N) >= N);

        _hashCallCount = 0;
        for (int i = 1; i <= N; i++)
            set.Add(i);

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, set.Count);
        for (int i = 1; i <= N; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void LongSet_EnsureCapacity_PreSized_BulkInsert_DoesNotResize()
    {
        var set = new LongSet<CountingLongHasher>();
        Assert.True(set.EnsureCapacity(N) >= N);

        _hashCallCount = 0;
        for (int i = 1; i <= N; i++)
            set.Add(i);

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, set.Count);
        for (int i = 1; i <= N; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void CeleritySet_EnsureCapacity_PreSized_BulkInsert_DoesNotResize()
    {
        var set = new CeleritySet<string, CountingStringHasher>();
        Assert.True(set.EnsureCapacity(N) >= N);

        _hashCallCount = 0;
        for (int i = 1; i <= N; i++)
            set.Add($"v{i}");

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, set.Count);
        for (int i = 1; i <= N; i++)
            Assert.True(set.Contains($"v{i}"));
    }

    [Fact]
    public void CelerityMultiMap_EnsureCapacity_PreSized_BulkInsert_DoesNotResize()
    {
        var map = new CelerityMultiMap<int, int, CountingIntHasher>();
        Assert.True(map.EnsureCapacity(N) >= N);

        _hashCallCount = 0;
        for (int i = 1; i <= N; i++)
            map.Add(i, i * 10);

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, map.Count);
        for (int i = 1; i <= N; i++)
            Assert.Equal(new[] { i * 10 }, map[i].ToArray());
    }

    [Fact]
    public void CelerityMultiSet_EnsureCapacity_PreSized_BulkInsert_DoesNotResize()
    {
        var set = new CelerityMultiSet<int, CountingIntHasher>();
        Assert.True(set.EnsureCapacity(N) >= N);

        _hashCallCount = 0;
        for (int i = 1; i <= N; i++)
            set.Add(i, i * 10);

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, set.Count);
        for (int i = 1; i <= N; i++)
            Assert.Equal(i * 10, set[i]);
    }

    [Fact]
    public void SmallDictionary_EnsureCapacity_GrowsBackingArray()
    {
        var map = new SmallDictionary<int, int>();
        int reported = map.EnsureCapacity(N);
        Assert.True(reported >= N);

        for (int i = 1; i <= N; i++)
            map[i] = i * 10;

        Assert.Equal(N, map.Count);
        for (int i = 1; i <= N; i++)
            Assert.Equal(i * 10, map[i]);
    }

    // ── TrimExcess: shrink without losing data ──────────────────────────────────────

    [Fact]
    public void IntDictionary_TrimExcess_AfterShrink_PreservesContents()
    {
        var map = new IntDictionary<int, CountingIntHasher>();
        for (int i = 1; i <= N; i++)
            map[i] = i * 10;
        for (int i = 6; i <= N; i++)
            map.Remove(i);

        map.TrimExcess();

        Assert.Equal(5, map.Count);
        for (int i = 1; i <= 5; i++)
            Assert.Equal(i * 10, map[i]);
        map[999] = 1; // still usable after the shrink
        Assert.Equal(6, map.Count);
    }

    [Fact]
    public void LongSet_TrimExcess_AfterShrink_PreservesContents()
    {
        var set = new LongSet<CountingLongHasher>();
        for (int i = 1; i <= N; i++)
            set.Add(i);
        for (int i = 6; i <= N; i++)
            set.Remove(i);

        set.TrimExcess();

        Assert.Equal(5, set.Count);
        for (int i = 1; i <= 5; i++)
            Assert.True(set.Contains(i));
        set.Add(999);
        Assert.True(set.Contains(999));
    }

    [Fact]
    public void CelerityDictionary_TrimExcess_AfterShrink_PreservesContents()
    {
        var map = new CelerityDictionary<string, int, CountingStringHasher>();
        for (int i = 1; i <= N; i++)
            map[$"k{i}"] = i;
        for (int i = 6; i <= N; i++)
            map.Remove($"k{i}");

        map.TrimExcess();

        Assert.Equal(5, map.Count);
        for (int i = 1; i <= 5; i++)
            Assert.Equal(i, map[$"k{i}"]);
    }

    [Fact]
    public void RobinHoodDictionary_TrimExcess_AfterShrink_PreservesContents()
    {
        var map = new RobinHoodDictionary<string, int, CountingStringHasher>();
        for (int i = 1; i <= N; i++)
            map[$"k{i}"] = i;
        for (int i = 6; i <= N; i++)
            map.Remove($"k{i}");

        map.TrimExcess();

        Assert.Equal(5, map.Count);
        for (int i = 1; i <= 5; i++)
            Assert.Equal(i, map[$"k{i}"]);
    }

    [Fact]
    public void SwissDictionary_TrimExcess_AfterShrink_PreservesContents()
    {
        var map = new SwissDictionary<string, int, CountingStringHasher>();
        for (int i = 1; i <= N; i++)
            map[$"k{i}"] = i;
        for (int i = 6; i <= N; i++)
            map.Remove($"k{i}");

        map.TrimExcess();

        Assert.Equal(5, map.Count);
        for (int i = 1; i <= 5; i++)
            Assert.Equal(i, map[$"k{i}"]);
        map["zz"] = 7; // tombstones were dropped; table still usable
        Assert.Equal(7, map["zz"]);
    }

    [Fact]
    public void HashCachingDictionary_TrimExcess_AfterShrink_PreservesContents()
    {
        var map = new HashCachingDictionary<string, int, CountingStringHasher>();
        for (int i = 1; i <= N; i++)
            map[$"k{i}"] = i;
        for (int i = 6; i <= N; i++)
            map.Remove($"k{i}");

        map.TrimExcess();

        Assert.Equal(5, map.Count);
        for (int i = 1; i <= 5; i++)
            Assert.Equal(i, map[$"k{i}"]);
    }

    [Fact]
    public void PooledCelerityDictionary_TrimExcess_AfterShrink_PreservesContents()
    {
        using var map = new PooledCelerityDictionary<string, int, CountingStringHasher>();
        for (int i = 1; i <= N; i++)
            map[$"k{i}"] = i;
        for (int i = 6; i <= N; i++)
            map.Remove($"k{i}");

        map.TrimExcess();

        Assert.Equal(5, map.Count);
        for (int i = 1; i <= 5; i++)
            Assert.Equal(i, map[$"k{i}"]);
    }

    [Fact]
    public void IntSet_TrimExcess_AfterShrink_PreservesContents()
    {
        var set = new IntSet<CountingIntHasher>();
        for (int i = 1; i <= N; i++)
            set.Add(i);
        for (int i = 6; i <= N; i++)
            set.Remove(i);

        set.TrimExcess();

        Assert.Equal(5, set.Count);
        for (int i = 1; i <= 5; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void CeleritySet_TrimExcess_AfterShrink_PreservesContents()
    {
        var set = new CeleritySet<string, CountingStringHasher>();
        for (int i = 1; i <= N; i++)
            set.Add($"v{i}");
        for (int i = 6; i <= N; i++)
            set.Remove($"v{i}");

        set.TrimExcess();

        Assert.Equal(5, set.Count);
        for (int i = 1; i <= 5; i++)
            Assert.True(set.Contains($"v{i}"));
    }

    [Fact]
    public void CelerityMultiMap_TrimExcess_AfterShrink_PreservesContents()
    {
        var map = new CelerityMultiMap<int, int, CountingIntHasher>();
        for (int i = 1; i <= N; i++)
        {
            map.Add(i, i * 10);
            map.Add(i, i * 10 + 1);
        }
        for (int i = 6; i <= N; i++)
            map.RemoveAll(i);

        map.TrimExcess();

        Assert.Equal(5, map.Count);
        for (int i = 1; i <= 5; i++)
            Assert.Equal(new[] { i * 10, i * 10 + 1 }, map[i].ToArray());
    }

    [Fact]
    public void CelerityMultiSet_TrimExcess_AfterShrink_PreservesContents()
    {
        var set = new CelerityMultiSet<int, CountingIntHasher>();
        for (int i = 1; i <= N; i++)
            set.Add(i, i * 10);
        for (int i = 6; i <= N; i++)
            set.RemoveAll(i);

        set.TrimExcess();

        Assert.Equal(5, set.Count);
        for (int i = 1; i <= 5; i++)
            Assert.Equal(i * 10, set[i]);
    }

    [Fact]
    public void SmallDictionary_TrimExcess_AfterShrink_PreservesContents()
    {
        var map = new SmallDictionary<int, int>();
        for (int i = 1; i <= N; i++)
            map[i] = i * 10;
        for (int i = 6; i <= N; i++)
            map.Remove(i);

        map.TrimExcess();

        Assert.Equal(5, map.Count);
        for (int i = 1; i <= 5; i++)
            Assert.Equal(i * 10, map[i]);
    }

    // ── TrimExcess(capacity) and the zero/default out-of-band slot ──────────────────

    [Fact]
    public void IntDictionary_TrimExcess_KeepsOutOfBandZeroKey()
    {
        var map = new IntDictionary<int, CountingIntHasher>();
        for (int i = 0; i <= N; i++) // include key 0, stored out-of-band
            map[i] = i + 1;
        for (int i = 6; i <= N; i++)
            map.Remove(i);

        map.TrimExcess();

        Assert.Equal(6, map.Count); // keys 0..5
        for (int i = 0; i <= 5; i++)
            Assert.Equal(i + 1, map[i]);
    }

    [Fact]
    public void CeleritySet_TrimExcess_WithExplicitCapacity_GrowsAndShrinks()
    {
        var set = new CeleritySet<string, CountingStringHasher>();
        for (int i = 1; i <= 5; i++)
            set.Add($"v{i}");

        set.TrimExcess(64); // grow to hold 64
        for (int i = 1; i <= 5; i++)
            Assert.True(set.Contains($"v{i}"));

        set.TrimExcess(); // shrink to fit 5
        Assert.Equal(5, set.Count);
        for (int i = 1; i <= 5; i++)
            Assert.True(set.Contains($"v{i}"));
    }

    // ── Argument validation (identical guard across the family) ──────────────────────

    [Fact]
    public void EnsureCapacity_NegativeCapacity_Throws()
    {
        var map = new IntDictionary<int, CountingIntHasher>();
        Assert.Throws<ArgumentOutOfRangeException>(() => map.EnsureCapacity(-1));

        var set = new CeleritySet<string, CountingStringHasher>();
        Assert.Throws<ArgumentOutOfRangeException>(() => set.EnsureCapacity(-5));

        var small = new SmallDictionary<int, int>();
        Assert.Throws<ArgumentOutOfRangeException>(() => small.EnsureCapacity(-1));
    }

    [Fact]
    public void TrimExcess_CapacityBelowCount_Throws()
    {
        var map = new IntDictionary<int, CountingIntHasher>();
        for (int i = 1; i <= 10; i++)
            map[i] = i;
        Assert.Throws<ArgumentOutOfRangeException>(() => map.TrimExcess(9));

        var set = new IntSet<CountingIntHasher>();
        for (int i = 1; i <= 10; i++)
            set.Add(i);
        Assert.Throws<ArgumentOutOfRangeException>(() => set.TrimExcess(3));
    }

    [Fact]
    public void EnsureCapacity_AlreadyLargeEnough_IsNoOp_AndPreservesContents()
    {
        var map = new IntDictionary<int, CountingIntHasher>();
        for (int i = 1; i <= 50; i++)
            map[i] = i;

        int before = map.EnsureCapacity(50);
        // Asking for a capacity the table already covers must not shrink or rehash away data.
        Assert.True(before >= 50);
        Assert.Equal(50, map.Count);
        for (int i = 1; i <= 50; i++)
            Assert.Equal(i, map[i]);
    }

    [Fact]
    public void PooledCelerityDictionary_EnsureCapacity_AfterDispose_Throws()
    {
        var map = new PooledCelerityDictionary<string, int, CountingStringHasher>();
        map.Dispose();
        Assert.Throws<ObjectDisposedException>(() => map.EnsureCapacity(10));
        Assert.Throws<ObjectDisposedException>(() => map.TrimExcess());
    }
}
