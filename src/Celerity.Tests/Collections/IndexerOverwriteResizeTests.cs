using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Regression tests for issue #117: the <c>this[key] = value</c> indexer setter on
/// <see cref="IntDictionary{TValue, THasher}"/>,
/// <see cref="LongDictionary{TValue, THasher}"/>, and
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/> used to run its
/// load-factor / <c>Resize()</c> check <em>before</em> probing. When the table sat
/// exactly at the load-factor threshold and the caller overwrote an
/// <em>existing</em> key (a pure value update that does not grow <c>Count</c>), the
/// setter still resized — doubling the backing arrays and re-hashing every entry —
/// for an operation that cannot push the table over its threshold.
///
/// The fix mirrors the #92 <c>TryAdd</c> ordering: probe first, then resize only
/// when <c>isNewEntry &amp;&amp; _count >= _threshold</c>, re-probing in the doubled
/// table. These tests pin the contract by counting how many times the underlying
/// <see cref="IHashProvider{T}"/>'s <c>Hash</c> method is called. Each probe-chain
/// walk begins with exactly one <c>Hash</c> call (linear-probe steps don't re-hash),
/// and <c>Resize()</c> re-hashes every surviving entry, so the hash-call count
/// cleanly distinguishes "overwrite, no resize" (one call) from the pre-fix
/// "overwrite triggers a full rehash" (one probe call plus one per migrated entry).
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

    // capacity 4 -> backing array size 4, threshold = (int)(4 * 0.75) = 3.
    // Filling with three non-zero keys lands Count exactly on the threshold
    // without touching the out-of-band zero / default-key slot.

    [Fact]
    public void IntDictionary_OverwriteExistingKeyAtThreshold_DoesNotResize()
    {
        var map = new IntDictionary<int, CountingIntHasher>(capacity: 4);
        map[1] = 1;
        map[2] = 2;
        map[3] = 3;
        Assert.Equal(3, map.Count);

        _hashCallCount = 0;
        map[2] = 99;

        // Exactly one probe walk; pre-fix this was 1 (probe) + 3 (rehash) = 4.
        Assert.Equal(1, _hashCallCount);
        Assert.Equal(3, map.Count);
        Assert.Equal(99, map[2]);
        Assert.Equal(1, map[1]);
        Assert.Equal(3, map[3]);
    }

    [Fact]
    public void LongDictionary_OverwriteExistingKeyAtThreshold_DoesNotResize()
    {
        var map = new LongDictionary<int, CountingLongHasher>(capacity: 4);
        map[1L] = 1;
        map[2L] = 2;
        map[3L] = 3;
        Assert.Equal(3, map.Count);

        _hashCallCount = 0;
        map[2L] = 99;

        Assert.Equal(1, _hashCallCount);
        Assert.Equal(3, map.Count);
        Assert.Equal(99, map[2L]);
        Assert.Equal(1, map[1L]);
        Assert.Equal(3, map[3L]);
    }

    [Fact]
    public void CelerityDictionary_OverwriteExistingKeyAtThreshold_DoesNotResize()
    {
        var map = new CelerityDictionary<string, int, CountingStringHasher>(capacity: 4);
        map["k1"] = 1;
        map["k2"] = 2;
        map["k3"] = 3;
        Assert.Equal(3, map.Count);

        _hashCallCount = 0;
        map["k2"] = 99;

        Assert.Equal(1, _hashCallCount);
        Assert.Equal(3, map.Count);
        Assert.Equal(99, map["k2"]);
        Assert.Equal(1, map["k1"]);
        Assert.Equal(3, map["k3"]);
    }

    // Complementary guard: the growth path must still fire when a *new* key
    // pushes the table to its threshold, so the fix can't be "remove the resize".

    [Fact]
    public void IntDictionary_NewKeyAtThreshold_StillResizes()
    {
        var map = new IntDictionary<int, CountingIntHasher>(capacity: 4);
        map[1] = 1;
        map[2] = 2;
        map[3] = 3;

        _hashCallCount = 0;
        map[4] = 4;

        // A new key at the threshold resizes: one probe in the old table, a
        // rehash of the three survivors, and one re-probe in the doubled table.
        Assert.True(_hashCallCount > 1);
        Assert.Equal(4, map.Count);
        Assert.Equal(1, map[1]);
        Assert.Equal(2, map[2]);
        Assert.Equal(3, map[3]);
        Assert.Equal(4, map[4]);
    }

    [Fact]
    public void LongDictionary_NewKeyAtThreshold_StillResizes()
    {
        var map = new LongDictionary<int, CountingLongHasher>(capacity: 4);
        map[1L] = 1;
        map[2L] = 2;
        map[3L] = 3;

        _hashCallCount = 0;
        map[4L] = 4;

        Assert.True(_hashCallCount > 1);
        Assert.Equal(4, map.Count);
        Assert.Equal(4, map[4L]);
    }

    [Fact]
    public void CelerityDictionary_NewKeyAtThreshold_StillResizes()
    {
        var map = new CelerityDictionary<string, int, CountingStringHasher>(capacity: 4);
        map["k1"] = 1;
        map["k2"] = 2;
        map["k3"] = 3;

        _hashCallCount = 0;
        map["k4"] = 4;

        Assert.True(_hashCallCount > 1);
        Assert.Equal(4, map.Count);
        Assert.Equal(4, map["k4"]);
    }

    // Behavioural guard independent of the hash counter: hammering an overwrite
    // at the threshold many times must keep Count stable and values correct.
    // Pre-fix, every one of these overwrites grew the backing arrays.

    [Fact]
    public void IntDictionary_RepeatedOverwriteAtThreshold_KeepsCountStable()
    {
        var map = new IntDictionary<int, CountingIntHasher>(capacity: 4);
        map[1] = 1;
        map[2] = 2;
        map[3] = 3;

        for (int round = 0; round < 100; round++)
        {
            map[1] = round;
            map[2] = round + 1;
            map[3] = round + 2;
            Assert.Equal(3, map.Count);
        }

        Assert.Equal(99, map[1]);
        Assert.Equal(100, map[2]);
        Assert.Equal(101, map[3]);
    }
}
