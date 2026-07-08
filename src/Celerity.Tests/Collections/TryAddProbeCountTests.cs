using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Regression tests for the single-probe <c>TryAdd</c> rewrite (PR #53).
/// <see cref="IntDictionary{TValue, THasher}.TryAdd"/>,
/// <see cref="CelerityDictionary{TKey, TValue, THasher}.TryAdd"/>,
/// <see cref="RobinHoodDictionary{TKey, TValue, THasher}.TryAdd"/>,
/// <see cref="IntSet{THasher}.TryAdd"/>,
/// <see cref="CeleritySet{T, THasher}.TryAdd"/>, and
/// <see cref="SwissSet{T, THasher}.TryAdd"/> historically walked the probe chain
/// twice on the new-key path: once via <c>ContainsKey</c>/<c>Contains</c>, then again
/// via the indexer setter / <c>InsertNon*</c> helper. The fix collapses both walks
/// into a single <c>ProbeForInsert</c>. These tests pin that contract by counting how
/// many times the underlying <see cref="IHashProvider{T}"/>'s <c>Hash</c> method is
/// called: each probe-chain walk starts with exactly one <c>Hash</c> call (the
/// subsequent linear-probe steps don't re-hash), so hash-call count is a faithful
/// proxy for probe-chain walk count.
///
/// All tests pre-size the collection so no <c>Resize</c> happens during the asserted
/// region — <c>Resize</c> would otherwise add hash calls of its own as it re-inserts
/// the existing entries into the new array.
/// </summary>
public class TryAddProbeCountTests
{
    private static int _hashCallCount;

    /// <summary>
    /// A counting hasher for <see cref="int"/> keys. The implementation is a
    /// minimal Wang-style mix so distinct keys distribute across the table; the
    /// only test-relevant aspect is that each call increments a static counter.
    /// </summary>
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

    /// <summary>
    /// A counting hasher for <see cref="long"/> keys. Mirrors
    /// <see cref="CountingIntHasher"/> for the 64-bit dictionary.
    /// </summary>
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

    /// <summary>
    /// A counting hasher for <see cref="string"/> keys.
    /// </summary>
    private struct CountingStringHasher : IHashProvider<string>
    {
        public int Hash(string key)
        {
            _hashCallCount++;
            // Deliberately use a stable, deterministic mix; we don't care about
            // distribution for these tests, only that the call is counted.
            return key.GetHashCode();
        }
    }

    [Fact]
    public void IntDictionary_TryAdd_NewKey_DoesExactlyOneProbeWalk()
    {
        // Pre-size so the asserted inserts never resize.
        var map = new IntDictionary<int, CountingIntHasher>(capacity: 64);
        _hashCallCount = 0;

        // 10 brand-new (non-zero) keys. Pre-fix this allocated 20 hash calls.
        for (int i = 1; i <= 10; i++)
            Assert.True(map.TryAdd(i, i * 10));

        Assert.Equal(10, _hashCallCount);
        Assert.Equal(10, map.Count);
    }

    [Fact]
    public void IntDictionary_TryAdd_DuplicateKey_DoesExactlyOneProbeWalk()
    {
        var map = new IntDictionary<int, CountingIntHasher>(capacity: 64);
        for (int i = 1; i <= 5; i++)
            map.TryAdd(i, i);

        _hashCallCount = 0;
        for (int i = 1; i <= 5; i++)
            Assert.False(map.TryAdd(i, -1));

        Assert.Equal(5, _hashCallCount);
        // Original values must remain untouched on the duplicate path.
        for (int i = 1; i <= 5; i++)
            Assert.Equal(i, map[i]);
    }

    [Fact]
    public void IntDictionary_Add_NewKey_DoesExactlyOneProbeWalk()
    {
        var map = new IntDictionary<int, CountingIntHasher>(capacity: 64);
        _hashCallCount = 0;

        for (int i = 1; i <= 10; i++)
            map.Add(i, i);

        Assert.Equal(10, _hashCallCount);
        Assert.Equal(10, map.Count);
    }

    [Fact]
    public void LongDictionary_TryAdd_NewKey_DoesExactlyOneProbeWalk()
    {
        var map = new LongDictionary<int, CountingLongHasher>(capacity: 64);
        _hashCallCount = 0;

        // 10 brand-new (non-zero) keys. Pre-fix this allocated 20 hash calls
        // because TryAdd called ContainsKey before delegating to the indexer.
        for (long i = 1; i <= 10; i++)
            Assert.True(map.TryAdd(i, (int)(i * 10)));

        Assert.Equal(10, _hashCallCount);
        Assert.Equal(10, map.Count);
    }

    [Fact]
    public void LongDictionary_TryAdd_DuplicateKey_DoesExactlyOneProbeWalk()
    {
        var map = new LongDictionary<int, CountingLongHasher>(capacity: 64);
        for (long i = 1; i <= 5; i++)
            map.TryAdd(i, (int)i);

        _hashCallCount = 0;
        for (long i = 1; i <= 5; i++)
            Assert.False(map.TryAdd(i, -1));

        Assert.Equal(5, _hashCallCount);
        // Original values must remain untouched on the duplicate path.
        for (long i = 1; i <= 5; i++)
            Assert.Equal((int)i, map[i]);
    }

    [Fact]
    public void LongDictionary_TryAdd_PreservesExistingValueOnDuplicate()
    {
        var map = new LongDictionary<int, CountingLongHasher>(capacity: 64);
        map.TryAdd(7L, 700);

        Assert.False(map.TryAdd(7L, -1));
        Assert.Equal(700, map[7L]);
    }

    [Fact]
    public void CelerityDictionary_TryAdd_NewKey_DoesExactlyOneProbeWalk()
    {
        var map = new CelerityDictionary<string, int, CountingStringHasher>(capacity: 64);
        _hashCallCount = 0;

        for (int i = 1; i <= 10; i++)
            Assert.True(map.TryAdd($"k{i}", i));

        Assert.Equal(10, _hashCallCount);
        Assert.Equal(10, map.Count);
    }

    [Fact]
    public void CelerityDictionary_TryAdd_DuplicateKey_DoesExactlyOneProbeWalk()
    {
        var map = new CelerityDictionary<string, int, CountingStringHasher>(capacity: 64);
        for (int i = 1; i <= 5; i++)
            map.TryAdd($"k{i}", i);

        _hashCallCount = 0;
        for (int i = 1; i <= 5; i++)
            Assert.False(map.TryAdd($"k{i}", -1));

        Assert.Equal(5, _hashCallCount);
        for (int i = 1; i <= 5; i++)
            Assert.Equal(i, map[$"k{i}"]);
    }

    [Fact]
    public void RobinHoodDictionary_TryAdd_NewKey_DoesExactlyOneProbeWalk()
    {
        // Robin Hood insertion may displace already-stored entries while it
        // back-shifts, but displacement moves slots in place without re-hashing,
        // so the new-key path still costs exactly one Hash call per insert (no
        // resize fires at capacity 64).
        var map = new RobinHoodDictionary<string, int, CountingStringHasher>(capacity: 64);
        _hashCallCount = 0;

        for (int i = 1; i <= 10; i++)
            Assert.True(map.TryAdd($"k{i}", i));

        Assert.Equal(10, _hashCallCount);
        Assert.Equal(10, map.Count);
    }

    [Fact]
    public void RobinHoodDictionary_TryAdd_DuplicateKey_DoesExactlyOneProbeWalk()
    {
        var map = new RobinHoodDictionary<string, int, CountingStringHasher>(capacity: 64);
        for (int i = 1; i <= 5; i++)
            map.TryAdd($"k{i}", i);

        _hashCallCount = 0;
        for (int i = 1; i <= 5; i++)
            Assert.False(map.TryAdd($"k{i}", -1));

        Assert.Equal(5, _hashCallCount);
        for (int i = 1; i <= 5; i++)
            Assert.Equal(i, map[$"k{i}"]);
    }

    [Fact]
    public void PooledCelerityDictionary_TryAdd_NewKey_DoesExactlyOneProbeWalk()
    {
        using var map = new PooledCelerityDictionary<string, int, CountingStringHasher>(capacity: 64);
        _hashCallCount = 0;

        for (int i = 1; i <= 10; i++)
            Assert.True(map.TryAdd($"k{i}", i));

        Assert.Equal(10, _hashCallCount);
        Assert.Equal(10, map.Count);
    }

    [Fact]
    public void PooledCelerityDictionary_TryAdd_DuplicateKey_DoesExactlyOneProbeWalk()
    {
        using var map = new PooledCelerityDictionary<string, int, CountingStringHasher>(capacity: 64);
        for (int i = 1; i <= 5; i++)
            map.TryAdd($"k{i}", i);

        _hashCallCount = 0;
        for (int i = 1; i <= 5; i++)
            Assert.False(map.TryAdd($"k{i}", -1));

        Assert.Equal(5, _hashCallCount);
        for (int i = 1; i <= 5; i++)
            Assert.Equal(i, map[$"k{i}"]);
    }

    [Fact]
    public void PooledCelerityDictionary_TryAdd_PreservesExistingValueOnDuplicate()
    {
        using var map = new PooledCelerityDictionary<string, int, CountingStringHasher>(capacity: 64);
        map.TryAdd("k7", 700);

        Assert.False(map.TryAdd("k7", -1));
        Assert.Equal(700, map["k7"]);
    }

    [Fact]
    public void SwissDictionary_TryAdd_NewKey_DoesExactlyOneProbeWalk()
    {
        // The combined probe both checks for an existing key and chooses the
        // insertion slot in one walk, so a new-key insert costs exactly one
        // Hash() call (no resize fires at capacity 64).
        var map = new SwissDictionary<string, int, CountingStringHasher>(capacity: 64);
        _hashCallCount = 0;

        for (int i = 1; i <= 10; i++)
            Assert.True(map.TryAdd($"k{i}", i));

        Assert.Equal(10, _hashCallCount);
        Assert.Equal(10, map.Count);
    }

    [Fact]
    public void SwissDictionary_TryAdd_DuplicateKey_DoesExactlyOneProbeWalk()
    {
        var map = new SwissDictionary<string, int, CountingStringHasher>(capacity: 64);
        for (int i = 1; i <= 5; i++)
            map.TryAdd($"k{i}", i);

        _hashCallCount = 0;
        for (int i = 1; i <= 5; i++)
            Assert.False(map.TryAdd($"k{i}", -1));

        Assert.Equal(5, _hashCallCount);
        for (int i = 1; i <= 5; i++)
            Assert.Equal(i, map[$"k{i}"]);
    }

    [Fact]
    public void SwissDictionary_TryAdd_PreservesExistingValueOnDuplicate()
    {
        var map = new SwissDictionary<string, int, CountingStringHasher>(capacity: 64);
        map.TryAdd("k7", 700);

        Assert.False(map.TryAdd("k7", -1));
        Assert.Equal(700, map["k7"]);
    }

    [Fact]
    public void HashCachingDictionary_TryAdd_NewKey_DoesExactlyOneProbeWalk()
    {
        // The combined probe both checks for an existing key and chooses the
        // insertion slot in one walk, so a new-key insert costs exactly one
        // Hash() call (no resize fires at capacity 64).
        var map = new HashCachingDictionary<string, int, CountingStringHasher>(capacity: 64);
        _hashCallCount = 0;

        for (int i = 1; i <= 10; i++)
            Assert.True(map.TryAdd($"k{i}", i));

        Assert.Equal(10, _hashCallCount);
        Assert.Equal(10, map.Count);
    }

    [Fact]
    public void HashCachingDictionary_TryAdd_DuplicateKey_DoesExactlyOneProbeWalk()
    {
        var map = new HashCachingDictionary<string, int, CountingStringHasher>(capacity: 64);
        for (int i = 1; i <= 5; i++)
            map.TryAdd($"k{i}", i);

        _hashCallCount = 0;
        for (int i = 1; i <= 5; i++)
            Assert.False(map.TryAdd($"k{i}", -1));

        Assert.Equal(5, _hashCallCount);
        for (int i = 1; i <= 5; i++)
            Assert.Equal(i, map[$"k{i}"]);
    }

    [Fact]
    public void HashCachingDictionary_TryAdd_PreservesExistingValueOnDuplicate()
    {
        var map = new HashCachingDictionary<string, int, CountingStringHasher>(capacity: 64);
        map.TryAdd("k7", 700);

        Assert.False(map.TryAdd("k7", -1));
        Assert.Equal(700, map["k7"]);
    }

    [Fact]
    public void IntSet_TryAdd_NewItem_DoesExactlyOneProbeWalk()
    {
        var set = new IntSet<CountingIntHasher>(capacity: 64);
        _hashCallCount = 0;

        for (int i = 1; i <= 10; i++)
            Assert.True(set.TryAdd(i));

        Assert.Equal(10, _hashCallCount);
        Assert.Equal(10, set.Count);
    }

    [Fact]
    public void IntSet_TryAdd_DuplicateItem_DoesExactlyOneProbeWalk()
    {
        var set = new IntSet<CountingIntHasher>(capacity: 64);
        for (int i = 1; i <= 5; i++)
            set.TryAdd(i);

        _hashCallCount = 0;
        for (int i = 1; i <= 5; i++)
            Assert.False(set.TryAdd(i));

        Assert.Equal(5, _hashCallCount);
        Assert.Equal(5, set.Count);
    }

    [Fact]
    public void LongSet_TryAdd_NewItem_DoesExactlyOneProbeWalk()
    {
        var set = new LongSet<CountingLongHasher>(capacity: 64);
        _hashCallCount = 0;

        for (long i = 1; i <= 10; i++)
            Assert.True(set.TryAdd(i));

        Assert.Equal(10, _hashCallCount);
        Assert.Equal(10, set.Count);
    }

    [Fact]
    public void LongSet_TryAdd_DuplicateItem_DoesExactlyOneProbeWalk()
    {
        var set = new LongSet<CountingLongHasher>(capacity: 64);
        for (long i = 1; i <= 5; i++)
            set.TryAdd(i);

        _hashCallCount = 0;
        for (long i = 1; i <= 5; i++)
            Assert.False(set.TryAdd(i));

        Assert.Equal(5, _hashCallCount);
        Assert.Equal(5, set.Count);
    }

    [Fact]
    public void CeleritySet_TryAdd_NewItem_DoesExactlyOneProbeWalk()
    {
        var set = new CeleritySet<string, CountingStringHasher>(capacity: 64);
        _hashCallCount = 0;

        for (int i = 1; i <= 10; i++)
            Assert.True(set.TryAdd($"v{i}"));

        Assert.Equal(10, _hashCallCount);
        Assert.Equal(10, set.Count);
    }

    [Fact]
    public void CeleritySet_TryAdd_DuplicateItem_DoesExactlyOneProbeWalk()
    {
        var set = new CeleritySet<string, CountingStringHasher>(capacity: 64);
        for (int i = 1; i <= 5; i++)
            set.TryAdd($"v{i}");

        _hashCallCount = 0;
        for (int i = 1; i <= 5; i++)
            Assert.False(set.TryAdd($"v{i}"));

        Assert.Equal(5, _hashCallCount);
        Assert.Equal(5, set.Count);
    }

    [Fact]
    public void SwissSet_TryAdd_NewItem_DoesExactlyOneProbeWalk()
    {
        // SwissSet's combined probe both checks for an existing element and chooses
        // the insertion slot in one walk, so a new-item insert costs exactly one
        // Hash() call (no resize fires at capacity 64).
        var set = new SwissSet<string, CountingStringHasher>(capacity: 64);
        _hashCallCount = 0;

        for (int i = 1; i <= 10; i++)
            Assert.True(set.TryAdd($"v{i}"));

        Assert.Equal(10, _hashCallCount);
        Assert.Equal(10, set.Count);
    }

    [Fact]
    public void SwissSet_TryAdd_DuplicateItem_DoesExactlyOneProbeWalk()
    {
        var set = new SwissSet<string, CountingStringHasher>(capacity: 64);
        for (int i = 1; i <= 5; i++)
            set.TryAdd($"v{i}");

        _hashCallCount = 0;
        for (int i = 1; i <= 5; i++)
            Assert.False(set.TryAdd($"v{i}"));

        Assert.Equal(5, _hashCallCount);
        Assert.Equal(5, set.Count);
    }

    [Fact]
    public void RobinHoodSet_TryAdd_NewItem_DoesExactlyOneHashCall()
    {
        // RobinHoodSet computes the element hash once and threads it through both
        // the existence probe and the Robin Hood insert, so a new-item TryAdd costs
        // exactly one Hash() call (no resize fires at capacity 64).
        var set = new RobinHoodSet<string, CountingStringHasher>(capacity: 64);
        _hashCallCount = 0;

        for (int i = 1; i <= 10; i++)
            Assert.True(set.TryAdd($"v{i}"));

        Assert.Equal(10, _hashCallCount);
        Assert.Equal(10, set.Count);
    }

    [Fact]
    public void RobinHoodSet_TryAdd_DuplicateItem_DoesExactlyOneHashCall()
    {
        var set = new RobinHoodSet<string, CountingStringHasher>(capacity: 64);
        for (int i = 1; i <= 5; i++)
            set.TryAdd($"v{i}");

        _hashCallCount = 0;
        for (int i = 1; i <= 5; i++)
            Assert.False(set.TryAdd($"v{i}"));

        Assert.Equal(5, _hashCallCount);
        Assert.Equal(5, set.Count);
    }

    [Fact]
    public void HashCachingSet_TryAdd_NewItem_DoesExactlyOneHashCall()
    {
        // HashCachingSet computes the element hash once (into the cached fingerprint)
        // and threads it through both the existence probe and the insert, so a
        // new-item TryAdd costs exactly one Hash() call (no resize fires at capacity 64).
        var set = new HashCachingSet<string, CountingStringHasher>(capacity: 64);
        _hashCallCount = 0;

        for (int i = 1; i <= 10; i++)
            Assert.True(set.TryAdd($"v{i}"));

        Assert.Equal(10, _hashCallCount);
        Assert.Equal(10, set.Count);
    }

    [Fact]
    public void HashCachingSet_TryAdd_DuplicateItem_DoesExactlyOneHashCall()
    {
        var set = new HashCachingSet<string, CountingStringHasher>(capacity: 64);
        for (int i = 1; i <= 5; i++)
            set.TryAdd($"v{i}");

        _hashCallCount = 0;
        for (int i = 1; i <= 5; i++)
            Assert.False(set.TryAdd($"v{i}"));

        Assert.Equal(5, _hashCallCount);
        Assert.Equal(5, set.Count);
    }

    [Fact]
    public void PooledCeleritySet_TryAdd_NewItem_DoesExactlyOneProbeWalk()
    {
        // PooledCeleritySet reuses CeleritySet's single-probe TryAdd, so a
        // new-item insert costs exactly one Hash() call (no resize at capacity 64).
        using var set = new PooledCeleritySet<string, CountingStringHasher>(capacity: 64);
        _hashCallCount = 0;

        for (int i = 1; i <= 10; i++)
            Assert.True(set.TryAdd($"v{i}"));

        Assert.Equal(10, _hashCallCount);
        Assert.Equal(10, set.Count);
    }

    [Fact]
    public void PooledCeleritySet_TryAdd_DuplicateItem_DoesExactlyOneProbeWalk()
    {
        using var set = new PooledCeleritySet<string, CountingStringHasher>(capacity: 64);
        for (int i = 1; i <= 5; i++)
            set.TryAdd($"v{i}");

        _hashCallCount = 0;
        for (int i = 1; i <= 5; i++)
            Assert.False(set.TryAdd($"v{i}"));

        Assert.Equal(5, _hashCallCount);
        Assert.Equal(5, set.Count);
    }

    [Fact]
    public void IntDictionary_TryAdd_PreservesExistingValueOnDuplicate()
    {
        // Behavioural guard: TryAdd must not overwrite the existing value when
        // it returns false. The single-probe rewrite has to be careful here
        // because ProbeForInsert returns the existing slot — we must read,
        // detect, and bail out before writing.
        var map = new IntDictionary<int, CountingIntHasher>(capacity: 64);
        map.TryAdd(7, 700);

        Assert.False(map.TryAdd(7, -1));
        Assert.Equal(700, map[7]);
    }

    [Fact]
    public void CelerityDictionary_TryAdd_PreservesExistingValueOnDuplicate()
    {
        var map = new CelerityDictionary<string, int, CountingStringHasher>(capacity: 64);
        map.TryAdd("k7", 700);

        Assert.False(map.TryAdd("k7", -1));
        Assert.Equal(700, map["k7"]);
    }

    [Fact]
    public void RobinHoodDictionary_TryAdd_PreservesExistingValueOnDuplicate()
    {
        var map = new RobinHoodDictionary<string, int, CountingStringHasher>(capacity: 64);
        map.TryAdd("k7", 700);

        Assert.False(map.TryAdd("k7", -1));
        Assert.Equal(700, map["k7"]);
    }
}
