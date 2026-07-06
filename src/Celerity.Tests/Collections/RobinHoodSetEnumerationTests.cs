using System.Collections;
using System.Collections.Generic;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// Enumeration tests for RobinHoodSet, mirroring CeleritySetEnumerationTests /
// SwissSetEnumerationTests. The semantic contract is the same:
// - GetEnumerator yields every element exactly once (including the out-of-band
//   default(T) entry — for reference types, that includes null).
// - The order is unspecified, but the default(T) entry, when present, is yielded
//   first.
// - Mutating the set during enumeration throws InvalidOperationException on the
//   next MoveNext / Reset — matching BCL HashSet<T> semantics.
// - The IEnumerable<T> path behaves identically to the struct path.
public class RobinHoodSetEnumerationTests
{
    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenEmpty()
    {
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();

        var seen = new List<int>();
        foreach (int item in set)
            seen.Add(item);

        Assert.Empty(seen);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldSingleEntry()
    {
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
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
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
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
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
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
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
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
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
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
        var set = new RobinHoodSet<string, StringFnV1AHasher>();
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
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
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
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
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
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
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
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>(capacity: 16);
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
    public void GetEnumerator_ShouldSkipRemovedSlots()
    {
        // After deletions the backward-shift leaves cleared (default) slots;
        // enumeration must yield only the live elements.
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>(64);
        for (int i = 1; i <= 40; i++)
            set.Add(i);
        for (int i = 1; i <= 40; i += 2)
            Assert.True(set.Remove(i));

        var seen = new HashSet<int>();
        foreach (int item in set)
            Assert.True(seen.Add(item), $"Duplicate item {item} emitted.");

        var expected = new HashSet<int>();
        for (int i = 2; i <= 40; i += 2)
            expected.Add(i);
        Assert.Equal(expected, seen);
    }

    [Fact]
    public void MoveNext_ShouldThrow_WhenSetIsMutatedMidEnumeration()
    {
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
        set.Add(1);
        set.Add(2);
        set.Add(3);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        set.Add(4); // structural mutation

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_WhenDefaultInsertedDuringEnumeration()
    {
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
        set.Add(1);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        set.Add(0); // default-entry insert goes through the out-of-band slot.

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterRemoveDuringEnumeration()
    {
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 4; i++)
            set.Add(i);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(set.Remove(2));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterClearDuringEnumeration()
    {
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
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
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
        set.Add(1);

        var enumerator = set.GetEnumerator();
        enumerator.MoveNext();

        set.Add(2);

        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void TryAdd_ShouldNotBumpVersion_WhenItemAlreadyPresent()
    {
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
        set.Add(1);
        set.Add(2);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.False(set.TryAdd(1)); // already present, must be a no-op

        // Should not throw — no structural change occurred.
        _ = enumerator.MoveNext();
    }

    [Fact]
    public void TryAdd_ShouldNotBumpVersion_WhenDefaultAlreadyPresent()
    {
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
        set.Add(0);
        set.Add(1);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.False(set.TryAdd(0)); // default slot already populated, must be a no-op

        _ = enumerator.MoveNext();
    }

    [Fact]
    public void Remove_ShouldNotBumpVersion_WhenItemAbsent()
    {
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
        set.Add(1);
        set.Add(2);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.False(set.Remove(99)); // not present, must be a no-op

        _ = enumerator.MoveNext();
    }

    [Fact]
    public void Clear_ShouldNotBumpVersion_WhenAlreadyEmpty()
    {
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
        var enumerator = set.GetEnumerator();

        set.Clear();

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Enumerator_ShouldBeReusableViaReset()
    {
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
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
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
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
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
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
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
        set.Add(10);
        set.Add(20);

        IEnumerable view = set;
        var seen = new HashSet<int>();
        foreach (object? item in view)
            seen.Add((int)item!);

        Assert.Equal(new HashSet<int> { 10, 20 }, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEveryEntry_WithCustomHasher()
    {
        var set = new RobinHoodSet<int, Int32Murmur3Hasher>();
        set.Add(0);
        for (int i = 1; i <= 25; i++)
            set.Add(i);

        var seen = new HashSet<int>();
        foreach (int item in set)
            seen.Add(item);

        Assert.Equal(26, seen.Count);
        for (int i = 0; i <= 25; i++)
            Assert.Contains(i, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldSupportLinq()
    {
        var set = new RobinHoodSet<int, Int32WangNaiveHasher>();
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
