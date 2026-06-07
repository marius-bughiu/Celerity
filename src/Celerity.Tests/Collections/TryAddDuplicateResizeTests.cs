using System.Collections.Generic;
using System.Linq;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Regression tests for the bug filed in issue #92.
///
/// Before the fix, <c>TryAdd</c> on <see cref="IntDictionary{TValue}"/>,
/// <see cref="LongDictionary{TValue}"/>,
/// <see cref="CelerityDictionary{TKey, TValue, THasher}"/>,
/// <see cref="RobinHoodDictionary{TKey, TValue, THasher}"/>,
/// <see cref="IntSet"/>, <see cref="LongSet"/>, and
/// <see cref="CeleritySet{T, THasher}"/> hoisted
/// the <c>Resize</c> check ahead of the duplicate check. When the collection
/// was exactly at the resize threshold and the caller supplied a duplicate
/// key, <c>Resize</c> would run (swapping out the backing arrays) and then
/// the probe would detect the duplicate and return <c>false</c> without
/// bumping <c>_version</c>. An enumerator captured before that call would
/// fail to detect the array swap on its next <c>MoveNext</c> and silently
/// yield stale / duplicated / missing entries.
///
/// The fix reorders <c>TryAdd</c> so the duplicate check happens before the
/// threshold check, so a duplicate-at-threshold call is a true no-op for
/// active enumerators: <c>Resize</c> only fires on the success path, which
/// already bumps <c>_version</c>.
/// </summary>
public class TryAddDuplicateResizeTests
{
    // capacity 4 produces threshold = (int)(4 * 0.75) = 3 on all five
    // collections, so adding three entries puts us exactly at the
    // resize threshold without going over.

    [Fact]
    public void IntDictionary_TryAdd_DuplicateAtThreshold_KeepsEnumeratorValid()
    {
        var map = new IntDictionary<int>(capacity: 4);
        map.Add(1, 10);
        map.Add(2, 20);
        map.Add(3, 30);

        var enumerator = map.GetEnumerator();
        var seen = new List<int>();
        Assert.True(enumerator.MoveNext());
        seen.Add(enumerator.Current.Key);

        Assert.False(map.TryAdd(2, 99));

        while (enumerator.MoveNext())
            seen.Add(enumerator.Current.Key);

        Assert.Equal(new[] { 1, 2, 3 }, seen.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void IntDictionary_TryAdd_NewKeyAtThreshold_InvalidatesEnumerator()
    {
        var map = new IntDictionary<int>(capacity: 4);
        map.Add(1, 10);
        map.Add(2, 20);
        map.Add(3, 30);

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(map.TryAdd(4, 40));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void IntDictionary_AddDuplicateAtThreshold_DoesNotMutateCapacityOrEnumerator()
    {
        var map = new IntDictionary<int>(capacity: 4);
        map.Add(1, 10);
        map.Add(2, 20);
        map.Add(3, 30);

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.Throws<ArgumentException>(() => map.Add(2, 99));

        var seen = new List<int> { enumerator.Current.Key };
        while (enumerator.MoveNext())
            seen.Add(enumerator.Current.Key);

        Assert.Equal(new[] { 1, 2, 3 }, seen.OrderBy(x => x).ToArray());
        Assert.Equal(3, map.Count);
    }

    [Fact]
    public void LongDictionary_TryAdd_DuplicateAtThreshold_KeepsEnumeratorValid()
    {
        var map = new LongDictionary<int>(capacity: 4);
        map.Add(1L, 10);
        map.Add(2L, 20);
        map.Add(3L, 30);

        var enumerator = map.GetEnumerator();
        var seen = new List<long>();
        Assert.True(enumerator.MoveNext());
        seen.Add(enumerator.Current.Key);

        Assert.False(map.TryAdd(2L, 99));

        while (enumerator.MoveNext())
            seen.Add(enumerator.Current.Key);

        Assert.Equal(new[] { 1L, 2L, 3L }, seen.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void LongDictionary_TryAdd_NewKeyAtThreshold_InvalidatesEnumerator()
    {
        var map = new LongDictionary<int>(capacity: 4);
        map.Add(1L, 10);
        map.Add(2L, 20);
        map.Add(3L, 30);

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(map.TryAdd(4L, 40));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void CelerityDictionary_TryAdd_DuplicateAtThreshold_KeepsEnumeratorValid()
    {
        var map = new CelerityDictionary<int, int, Celerity.Hashing.Int32WangNaiveHasher>(capacity: 4);
        map.Add(1, 10);
        map.Add(2, 20);
        map.Add(3, 30);

        var enumerator = map.GetEnumerator();
        var seen = new List<int>();
        Assert.True(enumerator.MoveNext());
        seen.Add(enumerator.Current.Key);

        Assert.False(map.TryAdd(2, 99));

        while (enumerator.MoveNext())
            seen.Add(enumerator.Current.Key);

        Assert.Equal(new[] { 1, 2, 3 }, seen.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void CelerityDictionary_TryAdd_NewKeyAtThreshold_InvalidatesEnumerator()
    {
        var map = new CelerityDictionary<int, int, Celerity.Hashing.Int32WangNaiveHasher>(capacity: 4);
        map.Add(1, 10);
        map.Add(2, 20);
        map.Add(3, 30);

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(map.TryAdd(4, 40));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void RobinHoodDictionary_TryAdd_DuplicateAtThreshold_KeepsEnumeratorValid()
    {
        var map = new RobinHoodDictionary<int, int, Celerity.Hashing.Int32WangNaiveHasher>(capacity: 4);
        map.Add(1, 10);
        map.Add(2, 20);
        map.Add(3, 30);

        var enumerator = map.GetEnumerator();
        var seen = new List<int>();
        Assert.True(enumerator.MoveNext());
        seen.Add(enumerator.Current.Key);

        Assert.False(map.TryAdd(2, 99));

        while (enumerator.MoveNext())
            seen.Add(enumerator.Current.Key);

        Assert.Equal(new[] { 1, 2, 3 }, seen.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void RobinHoodDictionary_TryAdd_NewKeyAtThreshold_InvalidatesEnumerator()
    {
        var map = new RobinHoodDictionary<int, int, Celerity.Hashing.Int32WangNaiveHasher>(capacity: 4);
        map.Add(1, 10);
        map.Add(2, 20);
        map.Add(3, 30);

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(map.TryAdd(4, 40));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    // SwissDictionary's minimum table is one SIMD group (16 slots), so its
    // default-capacity threshold is (int)(16 * 0.75) = 12. Fill to exactly that
    // before exercising the at-threshold duplicate / new-key paths.

    [Fact]
    public void SwissDictionary_TryAdd_DuplicateAtThreshold_KeepsEnumeratorValid()
    {
        var map = new SwissDictionary<int, int, Celerity.Hashing.Int32WangNaiveHasher>(capacity: 16);
        for (int i = 1; i <= 12; i++)
            map.Add(i, i * 10); // Count == 12 == threshold

        var enumerator = map.GetEnumerator();
        var seen = new List<int>();
        Assert.True(enumerator.MoveNext());
        seen.Add(enumerator.Current.Key);

        Assert.False(map.TryAdd(2, 99));

        while (enumerator.MoveNext())
            seen.Add(enumerator.Current.Key);

        Assert.Equal(Enumerable.Range(1, 12).ToArray(), seen.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void SwissDictionary_TryAdd_NewKeyAtThreshold_InvalidatesEnumerator()
    {
        var map = new SwissDictionary<int, int, Celerity.Hashing.Int32WangNaiveHasher>(capacity: 16);
        for (int i = 1; i <= 12; i++)
            map.Add(i, i * 10); // Count == 12 == threshold

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(map.TryAdd(13, 130));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    // HashCachingDictionary's minimum table is one SIMD group (16 slots), so its
    // default-capacity threshold is (int)(16 * 0.75) = 12. Fill to exactly that
    // before exercising the at-threshold duplicate / new-key paths.

    [Fact]
    public void HashCachingDictionary_TryAdd_DuplicateAtThreshold_KeepsEnumeratorValid()
    {
        var map = new HashCachingDictionary<int, int, Celerity.Hashing.Int32WangNaiveHasher>(capacity: 16);
        for (int i = 1; i <= 12; i++)
            map.Add(i, i * 10); // Count == 12 == threshold

        var enumerator = map.GetEnumerator();
        var seen = new List<int>();
        Assert.True(enumerator.MoveNext());
        seen.Add(enumerator.Current.Key);

        Assert.False(map.TryAdd(2, 99));

        while (enumerator.MoveNext())
            seen.Add(enumerator.Current.Key);

        Assert.Equal(Enumerable.Range(1, 12).ToArray(), seen.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void HashCachingDictionary_TryAdd_NewKeyAtThreshold_InvalidatesEnumerator()
    {
        var map = new HashCachingDictionary<int, int, Celerity.Hashing.Int32WangNaiveHasher>(capacity: 16);
        for (int i = 1; i <= 12; i++)
            map.Add(i, i * 10); // Count == 12 == threshold

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(map.TryAdd(13, 130));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void PooledCelerityDictionary_TryAdd_DuplicateAtThreshold_KeepsEnumeratorValid()
    {
        using var map = new PooledCelerityDictionary<int, int, Celerity.Hashing.Int32WangNaiveHasher>(capacity: 4);
        map.Add(1, 10);
        map.Add(2, 20);
        map.Add(3, 30);

        var enumerator = map.GetEnumerator();
        var seen = new List<int>();
        Assert.True(enumerator.MoveNext());
        seen.Add(enumerator.Current.Key);

        Assert.False(map.TryAdd(2, 99));

        while (enumerator.MoveNext())
            seen.Add(enumerator.Current.Key);

        Assert.Equal(new[] { 1, 2, 3 }, seen.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void PooledCelerityDictionary_TryAdd_NewKeyAtThreshold_InvalidatesEnumerator()
    {
        using var map = new PooledCelerityDictionary<int, int, Celerity.Hashing.Int32WangNaiveHasher>(capacity: 4);
        map.Add(1, 10);
        map.Add(2, 20);
        map.Add(3, 30);

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(map.TryAdd(4, 40));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void IntSet_TryAdd_DuplicateAtThreshold_KeepsEnumeratorValid()
    {
        var set = new IntSet(capacity: 4);
        set.Add(1);
        set.Add(2);
        set.Add(3);

        var enumerator = set.GetEnumerator();
        var seen = new List<int>();
        Assert.True(enumerator.MoveNext());
        seen.Add(enumerator.Current);

        Assert.False(set.TryAdd(2));

        while (enumerator.MoveNext())
            seen.Add(enumerator.Current);

        Assert.Equal(new[] { 1, 2, 3 }, seen.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void IntSet_TryAdd_NewItemAtThreshold_InvalidatesEnumerator()
    {
        var set = new IntSet(capacity: 4);
        set.Add(1);
        set.Add(2);
        set.Add(3);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(set.TryAdd(4));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void LongSet_TryAdd_DuplicateAtThreshold_KeepsEnumeratorValid()
    {
        var set = new LongSet(capacity: 4);
        set.Add(1L);
        set.Add(2L);
        set.Add(3L);

        var enumerator = set.GetEnumerator();
        var seen = new List<long>();
        Assert.True(enumerator.MoveNext());
        seen.Add(enumerator.Current);

        Assert.False(set.TryAdd(2L));

        while (enumerator.MoveNext())
            seen.Add(enumerator.Current);

        Assert.Equal(new[] { 1L, 2L, 3L }, seen.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void LongSet_TryAdd_NewItemAtThreshold_InvalidatesEnumerator()
    {
        var set = new LongSet(capacity: 4);
        set.Add(1L);
        set.Add(2L);
        set.Add(3L);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(set.TryAdd(4L));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void CeleritySet_TryAdd_DuplicateAtThreshold_KeepsEnumeratorValid()
    {
        var set = new CeleritySet<int, Celerity.Hashing.Int32WangNaiveHasher>(capacity: 4);
        set.Add(1);
        set.Add(2);
        set.Add(3);

        var enumerator = set.GetEnumerator();
        var seen = new List<int>();
        Assert.True(enumerator.MoveNext());
        seen.Add(enumerator.Current);

        Assert.False(set.TryAdd(2));

        while (enumerator.MoveNext())
            seen.Add(enumerator.Current);

        Assert.Equal(new[] { 1, 2, 3 }, seen.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void CeleritySet_TryAdd_NewItemAtThreshold_InvalidatesEnumerator()
    {
        var set = new CeleritySet<int, Celerity.Hashing.Int32WangNaiveHasher>(capacity: 4);
        set.Add(1);
        set.Add(2);
        set.Add(3);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(set.TryAdd(4));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    // SmallDictionary has no hash table or load-factor threshold, but the same
    // invariant applies at the point where its backing arrays are full: a
    // duplicate TryAdd must be a true no-op (no grow, no _version bump) so an
    // active enumerator stays valid, while a new-key TryAdd that grows the
    // arrays must invalidate it.

    [Fact]
    public void SmallDictionary_TryAdd_DuplicateAtCapacity_KeepsEnumeratorValid()
    {
        var map = new SmallDictionary<int, int>(capacity: 3);
        map.Add(1, 10);
        map.Add(2, 20);
        map.Add(3, 30); // arrays now exactly full

        var enumerator = map.GetEnumerator();
        var seen = new List<int>();
        Assert.True(enumerator.MoveNext());
        seen.Add(enumerator.Current.Key);

        Assert.False(map.TryAdd(2, 99));

        while (enumerator.MoveNext())
            seen.Add(enumerator.Current.Key);

        Assert.Equal(new[] { 1, 2, 3 }, seen.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void SmallDictionary_TryAdd_NewKeyAtCapacity_InvalidatesEnumerator()
    {
        var map = new SmallDictionary<int, int>(capacity: 3);
        map.Add(1, 10);
        map.Add(2, 20);
        map.Add(3, 30); // arrays now exactly full — the next new key grows them

        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(map.TryAdd(4, 40));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }
}
