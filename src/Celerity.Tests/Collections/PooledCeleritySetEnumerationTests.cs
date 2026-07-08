using System.Collections;
using System.Collections.Generic;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// Mirror of CeleritySetEnumerationTests for the ArrayPool-backed
// PooledCeleritySet<T, THasher>. The semantic contract is the same:
// - GetEnumerator yields every element exactly once (including the out-of-band
//   default(T) entry — for reference types, that includes null).
// - The order is unspecified, but the default(T) entry, when present, is
//   yielded first.
// - Mutating the set during enumeration throws InvalidOperationException on
//   the next MoveNext / Reset — matching BCL HashSet<T> semantics.
// - Enumeration bounds by the logical size, so an over-provisioned rented tail
//   never surfaces.
public class PooledCeleritySetEnumerationTests
{
    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenEmpty()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();

        var seen = new List<int>();
        foreach (int item in set)
            seen.Add(item);

        Assert.Empty(seen);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldSingleEntry()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        set.Add(7);

        var seen = new List<int>();
        foreach (int item in set)
            seen.Add(item);

        int single = Assert.Single(seen);
        Assert.Equal(7, single);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEveryEntryExactlyOnce()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 50; i++)
            set.Add(i);

        var seen = new HashSet<int>();
        foreach (int item in set)
        {
            Assert.True(seen.Add(item), $"Duplicate item {item} emitted.");
        }

        Assert.Equal(50, seen.Count);
        for (int i = 1; i <= 50; i++)
            Assert.Contains(i, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldIncludeDefaultEntry()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        set.Add(0);
        set.Add(1);
        set.Add(42);

        var seen = new HashSet<int>();
        foreach (int item in set)
            seen.Add(item);

        Assert.Equal(new HashSet<int> { 0, 1, 42 }, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldOnlyDefault_WhenItIsTheOnlyEntry()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        set.Add(0);

        var seen = new List<int>();
        foreach (int item in set)
            seen.Add(item);

        int single = Assert.Single(seen);
        Assert.Equal(0, single);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldDefaultFirst_WhenPresent()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        set.Add(11);
        set.Add(22);
        set.Add(33);
        set.Add(0);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNullFirst_ForReferenceTypeKey()
    {
        // For reference types, default(T) is null. The set should yield it
        // first and then the rest of the entries in unspecified order.
        using var set = new PooledCeleritySet<string, StringFnV1AHasher>();
        set.Add("alpha");
        set.Add("beta");
        set.Add(null!);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Null(enumerator.Current);

        var rest = new HashSet<string>();
        while (enumerator.MoveNext())
            rest.Add(enumerator.Current!);

        Assert.Equal(new HashSet<string> { "alpha", "beta" }, rest);
    }

    [Fact]
    public void GetEnumerator_ShouldReflectRemoval()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        set.Add(1);
        set.Add(2);
        set.Add(3);
        Assert.True(set.Remove(2));

        var seen = new HashSet<int>();
        foreach (int item in set)
            seen.Add(item);

        Assert.Equal(new HashSet<int> { 1, 3 }, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldReflectDefaultRemoval()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        set.Add(0);
        set.Add(1);
        set.Add(2);
        Assert.True(set.Remove(0));

        var seen = new HashSet<int>();
        foreach (int item in set)
            seen.Add(item);

        Assert.Equal(new HashSet<int> { 1, 2 }, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldReflectClear()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        for (int i = 0; i < 10; i++)
            set.Add(i);
        set.Clear();

        int count = 0;
        foreach (int _ in set)
            count++;

        Assert.Equal(0, count);
    }

    [Fact]
    public void GetEnumerator_ShouldSurviveResize()
    {
        // Force multiple resizes from a tiny starting capacity and assert the
        // enumerator still sees every entry.
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>(capacity: 4);
        for (int i = 1; i <= 200; i++)
            set.Add(i);

        var seen = new HashSet<int>();
        foreach (int item in set)
        {
            Assert.True(seen.Add(item), $"Duplicate item {item} emitted.");
        }

        Assert.Equal(200, seen.Count);
    }

    [Fact]
    public void MoveNext_ShouldThrow_WhenSetIsMutatedMidEnumeration()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        set.Add(1);
        set.Add(2);
        set.Add(3);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext()); // OK — one step before mutation.

        set.Add(4); // structural mutation

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_WhenDefaultInsertedDuringEnumeration()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        set.Add(1);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        set.Add(0); // default-entry insert goes through the out-of-band slot.

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterRemoveDuringEnumeration()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 4; i++)
            set.Add(i);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(set.Remove(2));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterDefaultRemoveDuringEnumeration()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        set.Add(0);
        set.Add(1);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(set.Remove(0)); // out-of-band default removal

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterClearDuringEnumeration()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 4; i++)
            set.Add(i);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        set.Clear();

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void Reset_ShouldThrow_WhenSetIsMutatedMidEnumeration()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        set.Add(1);

        var enumerator = set.GetEnumerator();
        enumerator.MoveNext();

        set.Add(2);

        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void TryAdd_ShouldNotBumpVersion_WhenItemAlreadyPresent()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        set.Add(1);
        set.Add(2);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.False(set.TryAdd(1)); // already present, must be a no-op

        // Should not throw — no structural change occurred.
        bool advanced = enumerator.MoveNext();
        _ = advanced;
    }

    [Fact]
    public void Remove_ShouldNotBumpVersion_WhenItemAbsent()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        set.Add(1);
        set.Add(2);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.False(set.Remove(99)); // not present, must be a no-op

        // Should not throw.
        _ = enumerator.MoveNext();
    }

    [Fact]
    public void Enumerator_ShouldBeReusableViaReset()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        set.Add(1);
        set.Add(2);

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
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        set.Add(7);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(7, enumerator.Current);
        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);
    }

    [Fact]
    public void Set_ShouldBeIterableThroughIEnumerable()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        set.Add(1);
        set.Add(2);
        set.Add(0);

        IEnumerable<int> view = set;
        var seen = new HashSet<int>();
        foreach (int item in view)
            seen.Add(item);

        Assert.Equal(new HashSet<int> { 0, 1, 2 }, seen);
    }

    [Fact]
    public void Set_ShouldBeIterableThroughNonGenericIEnumerable()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        set.Add(10);
        set.Add(20);

        IEnumerable view = set;
        var seen = new HashSet<int>();
        foreach (object? item in view)
            seen.Add((int)item!);

        Assert.Equal(new HashSet<int> { 10, 20 }, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldSupportLinq()
    {
        using var set = new PooledCeleritySet<int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 5; i++)
            set.Add(i);
        set.Add(0);

        Assert.Equal(6, System.Linq.Enumerable.Count(set));
        Assert.Equal(15, System.Linq.Enumerable.Sum(set));   // 0+1+2+3+4+5
        bool linqHasZero = System.Linq.Enumerable.Contains(set, 0);
        bool linqHasFive = System.Linq.Enumerable.Contains(set, 5);
        bool linqHas99 = System.Linq.Enumerable.Contains(set, 99);
        Assert.True(linqHasZero);
        Assert.True(linqHasFive);
        Assert.False(linqHas99);
    }
}
