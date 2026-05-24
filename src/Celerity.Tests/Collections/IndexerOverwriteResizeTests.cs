using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Regression tests for issue #117: the <c>this[key] = value</c> indexer setter on
/// <see cref="IntDictionary{TValue, THasher}"/>,
/// <see cref="LongDictionary{TValue, THasher}"/>, and
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/> historically ran its
/// load-factor / <c>Resize</c> check <em>before</em> probing, so assigning to a key
/// that already existed while the table sat at the load-factor threshold triggered a
/// full <c>Resize</c> (rehashing every entry) even though a pure value overwrite does
/// not grow <c>Count</c>. This is the indexer-setter counterpart to the <c>TryAdd</c>
/// ordering fixed in #92; the fix probes first and resizes only on a genuinely new
/// entry that has reached the threshold.
///
/// Each test counts <see cref="IHashProvider{T}.Hash"/> calls: a single probe walk
/// starts with exactly one <c>Hash</c> call (linear-probe steps do not re-hash), and a
/// spurious <c>Resize</c> would add one <c>Hash</c> call per existing entry as it
/// re-inserts them. An overwrite-at-threshold must therefore cost exactly one
/// <c>Hash</c> call; pre-fix it cost <c>threshold + 1</c>.
/// </summary>
public class IndexerOverwriteResizeTests
{
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

    [Fact]
    public void IntDictionary_IndexerOverwriteAtThreshold_DoesNotResize()
    {
        // capacity 4 -> size 4 -> threshold = (int)(4 * 0.75) = 3.
        var map = new IntDictionary<int, CountingIntHasher>(capacity: 4);
        map[1] = 1;
        map[2] = 2;
        map[3] = 3; // Count == 3 == threshold

        _hashCallCount = 0;
        map[2] = 99; // overwrite existing key — must not resize

        Assert.Equal(1, _hashCallCount);
        Assert.Equal(3, map.Count);
        Assert.Equal(99, map[2]);
    }

    [Fact]
    public void LongDictionary_IndexerOverwriteAtThreshold_DoesNotResize()
    {
        var map = new LongDictionary<int, CountingLongHasher>(capacity: 4);
        map[1L] = 1;
        map[2L] = 2;
        map[3L] = 3; // Count == 3 == threshold

        _hashCallCount = 0;
        map[2L] = 99; // overwrite existing key — must not resize

        Assert.Equal(1, _hashCallCount);
        Assert.Equal(3, map.Count);
        Assert.Equal(99, map[2L]);
    }

    [Fact]
    public void CelerityDictionary_IndexerOverwriteAtThreshold_DoesNotResize()
    {
        var map = new CelerityDictionary<string, int, CountingStringHasher>(capacity: 4);
        map["a"] = 1;
        map["b"] = 2;
        map["c"] = 3; // Count == 3 == threshold

        _hashCallCount = 0;
        map["b"] = 99; // overwrite existing key — must not resize

        Assert.Equal(1, _hashCallCount);
        Assert.Equal(3, map.Count);
        Assert.Equal(99, map["b"]);
    }

    [Fact]
    public void IntDictionary_RepeatedOverwriteAtThreshold_NeverResizes()
    {
        var map = new IntDictionary<int, CountingIntHasher>(capacity: 4);
        map[1] = 1;
        map[2] = 2;
        map[3] = 3;

        _hashCallCount = 0;
        for (int round = 0; round < 5; round++)
        {
            map[1] = round;
            map[2] = round;
            map[3] = round;
        }

        // 15 overwrites, one probe walk each, zero rehashes.
        Assert.Equal(15, _hashCallCount);
        Assert.Equal(3, map.Count);
    }

    [Fact]
    public void IntDictionary_IndexerNewKeyAtThreshold_StillResizesAndKeepsAllEntries()
    {
        // The fix must not break growth: a genuinely new key at the threshold
        // still resizes, and every entry survives.
        var map = new IntDictionary<int, CountingIntHasher>(capacity: 4);
        map[1] = 1;
        map[2] = 2;
        map[3] = 3; // Count == 3 == threshold

        map[4] = 4; // new key — must resize

        Assert.Equal(4, map.Count);
        for (int i = 1; i <= 4; i++)
            Assert.Equal(i, map[i]);
    }
}
