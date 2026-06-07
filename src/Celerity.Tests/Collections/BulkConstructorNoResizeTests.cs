using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Regression tests for the bulk-constructor sizing fix (issue #27). The
/// <c>IEnumerable&lt;…&gt;</c> source constructors of every hash-table collection
/// document that the source's <c>Count</c> is used to size the backing storage
/// "so inserts do not resize" (or "so the initial fill avoids resize work").
/// Before the fix the table was sized to the raw count, but the resize threshold
/// is <c>size × loadFactor</c>, so the last few inserts of the bulk fill still
/// tripped a full rehash. The fix scales the requested capacity up by
/// <c>1 / loadFactor</c> so the whole source fits below the threshold.
///
/// These tests pin the contract the same way <see cref="TryAddProbeCountTests"/>
/// does: by counting <see cref="IHashProvider{T}.Hash"/> calls. Building a table
/// of <c>N</c> distinct keys with no resize costs exactly <c>N</c> hash calls
/// (one probe-chain walk per insert). A resize re-hashes every entry already in
/// the table, so a resize during construction would push the count above <c>N</c>.
/// Asserting <c>== N</c> therefore fails on the pre-fix sizing and passes after it.
///
/// <c>SmallDictionary</c> is intentionally excluded: it has no load factor and no
/// hasher (it linear-scans), so its capacity-verbatim sizing already fills without
/// resizing and there is nothing to count.
/// </summary>
public class BulkConstructorNoResizeTests
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

    private static KeyValuePair<int, int>[] IntPairs() =>
        Enumerable.Range(1, N).Select(i => new KeyValuePair<int, int>(i, i * 10)).ToArray();

    private static KeyValuePair<long, int>[] LongPairs() =>
        Enumerable.Range(1, N).Select(i => new KeyValuePair<long, int>(i, i * 10)).ToArray();

    private static KeyValuePair<string, int>[] StringPairs() =>
        Enumerable.Range(1, N).Select(i => new KeyValuePair<string, int>($"k{i}", i)).ToArray();

    // ── Dictionaries ────────────────────────────────────────────────────────────

    [Fact]
    public void IntDictionary_BulkConstruct_FromKnownCount_DoesNotResize()
    {
        KeyValuePair<int, int>[] src = IntPairs();
        _hashCallCount = 0;

        var map = new IntDictionary<int, CountingIntHasher>(src);

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, map.Count);
        for (int i = 1; i <= N; i++)
            Assert.Equal(i * 10, map[i]);
    }

    [Fact]
    public void LongDictionary_BulkConstruct_FromKnownCount_DoesNotResize()
    {
        KeyValuePair<long, int>[] src = LongPairs();
        _hashCallCount = 0;

        var map = new LongDictionary<int, CountingLongHasher>(src);

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, map.Count);
        for (int i = 1; i <= N; i++)
            Assert.Equal(i * 10, map[i]);
    }

    [Fact]
    public void CelerityDictionary_BulkConstruct_FromKnownCount_DoesNotResize()
    {
        KeyValuePair<string, int>[] src = StringPairs();
        _hashCallCount = 0;

        var map = new CelerityDictionary<string, int, CountingStringHasher>(src);

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, map.Count);
        for (int i = 1; i <= N; i++)
            Assert.Equal(i, map[$"k{i}"]);
    }

    [Fact]
    public void RobinHoodDictionary_BulkConstruct_FromKnownCount_DoesNotResize()
    {
        KeyValuePair<string, int>[] src = StringPairs();
        _hashCallCount = 0;

        var map = new RobinHoodDictionary<string, int, CountingStringHasher>(src);

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, map.Count);
        for (int i = 1; i <= N; i++)
            Assert.Equal(i, map[$"k{i}"]);
    }

    [Fact]
    public void PooledCelerityDictionary_BulkConstruct_FromKnownCount_DoesNotResize()
    {
        KeyValuePair<string, int>[] src = StringPairs();
        _hashCallCount = 0;

        using var map = new PooledCelerityDictionary<string, int, CountingStringHasher>(src);

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, map.Count);
        for (int i = 1; i <= N; i++)
            Assert.Equal(i, map[$"k{i}"]);
    }

    // ── Sets ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void IntSet_BulkConstruct_FromKnownCount_DoesNotResize()
    {
        int[] src = Enumerable.Range(1, N).ToArray();
        _hashCallCount = 0;

        var set = new IntSet<CountingIntHasher>(src);

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, set.Count);
        for (int i = 1; i <= N; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void LongSet_BulkConstruct_FromKnownCount_DoesNotResize()
    {
        long[] src = Enumerable.Range(1, N).Select(i => (long)i).ToArray();
        _hashCallCount = 0;

        var set = new LongSet<CountingLongHasher>(src);

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, set.Count);
        for (int i = 1; i <= N; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void CeleritySet_BulkConstruct_FromKnownCount_DoesNotResize()
    {
        string[] src = Enumerable.Range(1, N).Select(i => $"v{i}").ToArray();
        _hashCallCount = 0;

        var set = new CeleritySet<string, CountingStringHasher>(src);

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, set.Count);
        for (int i = 1; i <= N; i++)
            Assert.True(set.Contains($"v{i}"));
    }

    // ── MultiMap ────────────────────────────────────────────────────────────────

    [Fact]
    public void CelerityMultiMap_BulkConstruct_FromKnownDistinctCount_DoesNotResize()
    {
        KeyValuePair<int, int>[] src = IntPairs();
        _hashCallCount = 0;

        var map = new CelerityMultiMap<int, int, CountingIntHasher>(src);

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, map.Count);
        for (int i = 1; i <= N; i++)
            Assert.Equal(new[] { i * 10 }, map[i].ToArray());
    }

    // ── Load-factor scaling: the headroom must track a non-default load factor ────

    [Theory]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    [InlineData(0.95f)]
    public void IntDictionary_BulkConstruct_HoldsSourceWithoutResize_AcrossLoadFactors(float loadFactor)
    {
        KeyValuePair<int, int>[] src = IntPairs();
        _hashCallCount = 0;

        var map = new IntDictionary<int, CountingIntHasher>(src, loadFactor: loadFactor);

        Assert.Equal(N, _hashCallCount);
        Assert.Equal(N, map.Count);
        for (int i = 1; i <= N; i++)
            Assert.Equal(i * 10, map[i]);
    }

    // ── Fallback: a non-collection source (unknown count) still builds correctly ──
    // (No resize guarantee is possible without a count; only correctness is pinned.)

    [Fact]
    public void IntDictionary_BulkConstruct_FromNonCollectionEnumerable_IsCorrect()
    {
        IEnumerable<KeyValuePair<int, int>> src =
            Enumerable.Range(1, N).Select(i => new KeyValuePair<int, int>(i, i * 10));

        var map = new IntDictionary<int, CountingIntHasher>(src);

        Assert.Equal(N, map.Count);
        for (int i = 1; i <= N; i++)
            Assert.Equal(i * 10, map[i]);
    }
}
