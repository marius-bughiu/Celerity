using System.Collections;
using System.Collections.Generic;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

// LongSet enumeration coverage — mirrors IntSetEnumerationTests for 64-bit
// elements. Pins:
// - GetEnumerator yields every element exactly once (including the out-of-band
//   zero entry).
// - The order is unspecified but enumerating into a set must match the
//   LongSet's contents by set equality.
// - Mutating the set during enumeration throws InvalidOperationException on
//   the next MoveNext / Reset — matching BCL HashSet<T> semantics.
// - The IEnumerable<long> path behaves identically to the struct path
//   (it boxes the enumerator, but the set semantics line up).
public class LongSetEnumerationTests
{
    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenEmpty()
    {
        var set = new LongSet();

        var seen = new List<long>();
        foreach (long item in set)
            seen.Add(item);

        Assert.Empty(seen);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldSingleEntry()
    {
        var set = new LongSet();
        set.Add(7L);

        var seen = new List<long>();
        foreach (long item in set)
            seen.Add(item);

        long single = Assert.Single(seen);
        Assert.Equal(7L, single);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEveryEntryExactlyOnce()
    {
        var set = new LongSet();
        for (long i = 1; i <= 50; i++)
            set.Add(i);

        var seen = new HashSet<long>();
        foreach (long item in set)
        {
            Assert.True(seen.Add(item), $"Duplicate item {item} emitted.");
        }

        Assert.Equal(50, seen.Count);
        for (long i = 1; i <= 50; i++)
            Assert.Contains(i, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldIncludeZeroEntry()
    {
        var set = new LongSet();
        set.Add(0L);
        set.Add(1L);
        set.Add(42L);

        var seen = new HashSet<long>();
        foreach (long item in set)
            seen.Add(item);

        Assert.Equal(new HashSet<long> { 0L, 1L, 42L }, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldOnlyZero_WhenItIsTheOnlyEntry()
    {
        var set = new LongSet();
        set.Add(0L);

        var seen = new List<long>();
        foreach (long item in set)
            seen.Add(item);

        long single = Assert.Single(seen);
        Assert.Equal(0L, single);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldZeroFirst_WhenPresent()
    {
        var set = new LongSet();
        set.Add(11L);
        set.Add(22L);
        set.Add(33L);
        set.Add(0L);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(0L, enumerator.Current);
    }

    [Fact]
    public void GetEnumerator_ShouldReflectRemoval()
    {
        var set = new LongSet();
        set.Add(1L);
        set.Add(2L);
        set.Add(3L);
        Assert.True(set.Remove(2L));

        var seen = new HashSet<long>();
        foreach (long item in set)
            seen.Add(item);

        Assert.Equal(new HashSet<long> { 1L, 3L }, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldReflectZeroRemoval()
    {
        var set = new LongSet();
        set.Add(0L);
        set.Add(1L);
        set.Add(2L);
        Assert.True(set.Remove(0L));

        var seen = new HashSet<long>();
        foreach (long item in set)
            seen.Add(item);

        Assert.Equal(new HashSet<long> { 1L, 2L }, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldReflectClear()
    {
        var set = new LongSet();
        for (long i = 0; i < 10; i++)
            set.Add(i);
        set.Clear();

        int count = 0;
        foreach (long _ in set)
            count++;

        Assert.Equal(0, count);
    }

    [Fact]
    public void GetEnumerator_ShouldSurviveResize()
    {
        var set = new LongSet(capacity: 4);
        for (long i = 1; i <= 200; i++)
            set.Add(i);

        var seen = new HashSet<long>();
        foreach (long item in set)
        {
            Assert.True(seen.Add(item), $"Duplicate item {item} emitted.");
        }

        Assert.Equal(200, seen.Count);
    }

    [Fact]
    public void MoveNext_ShouldThrow_WhenSetIsMutatedMidEnumeration()
    {
        var set = new LongSet();
        set.Add(1L);
        set.Add(2L);
        set.Add(3L);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        set.Add(4L);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_WhenZeroInsertedDuringEnumeration()
    {
        var set = new LongSet();
        set.Add(1L);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        set.Add(0L);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterRemoveDuringEnumeration()
    {
        var set = new LongSet();
        for (long i = 1; i <= 4; i++)
            set.Add(i);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(set.Remove(2L));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterZeroRemoveDuringEnumeration()
    {
        var set = new LongSet();
        set.Add(0L);
        set.Add(1L);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(set.Remove(0L));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterClearDuringEnumeration()
    {
        var set = new LongSet();
        for (long i = 1; i <= 4; i++)
            set.Add(i);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        set.Clear();

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Reset_ShouldThrow_WhenSetIsMutatedMidEnumeration()
    {
        var set = new LongSet();
        set.Add(1L);

        var enumerator = set.GetEnumerator();
        enumerator.MoveNext();

        set.Add(2L);

        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void TryAdd_ShouldNotBumpVersion_WhenItemAlreadyPresent()
    {
        var set = new LongSet();
        set.Add(1L);
        set.Add(2L);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.False(set.TryAdd(1L));

        bool advanced = enumerator.MoveNext();
        _ = advanced;
    }

    [Fact]
    public void Remove_ShouldNotBumpVersion_WhenItemAbsent()
    {
        var set = new LongSet();
        set.Add(1L);
        set.Add(2L);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.False(set.Remove(99L));

        _ = enumerator.MoveNext();
    }

    [Fact]
    public void Clear_ShouldNotBumpVersion_WhenAlreadyEmpty()
    {
        var set = new LongSet();
        var enumerator = set.GetEnumerator();

        set.Clear();

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Enumerator_ShouldBeReusableViaReset()
    {
        var set = new LongSet();
        set.Add(1L);
        set.Add(2L);

        var enumerator = set.GetEnumerator();
        int firstPass = 0;
        while (enumerator.MoveNext()) firstPass++;

        enumerator.Reset();
        int secondPass = 0;
        while (enumerator.MoveNext()) secondPass++;

        Assert.Equal(2, firstPass);
        Assert.Equal(firstPass, secondPass);
    }

    [Fact]
    public void Enumerator_ShouldReturnDefault_AfterEnumerationIsExhausted()
    {
        var set = new LongSet();
        set.Add(7L);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(7L, enumerator.Current);
        Assert.False(enumerator.MoveNext());
        Assert.Equal(0L, enumerator.Current);
    }

    [Fact]
    public void Set_ShouldBeIterableThroughIEnumerable()
    {
        var set = new LongSet();
        set.Add(1L);
        set.Add(2L);
        set.Add(0L);

        IEnumerable<long> view = set;
        var seen = new HashSet<long>();
        foreach (long item in view)
            seen.Add(item);

        Assert.Equal(new HashSet<long> { 0L, 1L, 2L }, seen);
    }

    [Fact]
    public void Set_ShouldBeIterableThroughNonGenericIEnumerable()
    {
        var set = new LongSet();
        set.Add(10L);
        set.Add(20L);

        IEnumerable view = set;
        var seen = new HashSet<long>();
        foreach (object item in view)
            seen.Add((long)item);

        Assert.Equal(new HashSet<long> { 10L, 20L }, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEveryEntry_WithCustomHasher()
    {
        var set = new LongSet<Celerity.Hashing.Int64Murmur3Hasher>();
        set.Add(0L);
        for (long i = 1; i <= 25; i++)
            set.Add(i);

        var seen = new HashSet<long>();
        foreach (long item in set)
            seen.Add(item);

        Assert.Equal(26, seen.Count);
        for (long i = 0; i <= 25; i++)
            Assert.Contains(i, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldSupportLinq()
    {
        var set = new LongSet();
        for (long i = 1; i <= 5; i++)
            set.Add(i);
        set.Add(0L);

        Assert.Equal(6, System.Linq.Enumerable.Count(set));
        Assert.Equal(15L, System.Linq.Enumerable.Sum(set));   // 0+1+2+3+4+5
        bool linqHasZero = System.Linq.Enumerable.Contains(set, 0L);
        bool linqHasFive = System.Linq.Enumerable.Contains(set, 5L);
        bool linqHas99 = System.Linq.Enumerable.Contains(set, 99L);
        Assert.True(linqHasZero);
        Assert.True(linqHasFive);
        Assert.False(linqHas99);
    }

    [Fact]
    public void GetEnumerator_ShouldHandleExtremeKeyValues()
    {
        // Long-specific: confirm the enumerator round-trips the full 64-bit
        // range, including the lower-32-bits-share case that's a tripwire for
        // accidental int-truncation.
        var set = new LongSet();
        long[] items = {
            long.MaxValue,
            long.MinValue,
            (long)int.MaxValue + 1L,
            (long)int.MinValue - 1L,
            -1L,
            0x0000_0001_0000_0001L,
            0x0000_0002_0000_0001L,
        };

        foreach (long item in items)
            set.Add(item);

        var seen = new HashSet<long>();
        foreach (long item in set)
            seen.Add(item);

        Assert.Equal(items.Length, seen.Count);
        foreach (long item in items)
            Assert.Contains(item, seen);
    }
}
