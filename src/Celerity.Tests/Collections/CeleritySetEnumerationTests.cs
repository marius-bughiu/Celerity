using System.Collections;
using System.Collections.Generic;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// Milestone 1.1.0 — issue #23 (slice 2): GetEnumerator on CeleritySet.
//
// Mirror of IntSetEnumerationTests, generalized to the open-generic
// CeleritySet<T, THasher>. The semantic contract is the same:
// - GetEnumerator yields every element exactly once (including the out-of-band
//   default(T) entry — for reference types, that includes null).
// - The order is unspecified, but the default(T) entry, when present, is
//   yielded first.
// - Mutating the set during enumeration throws InvalidOperationException on
//   the next MoveNext / Reset — matching BCL HashSet<T> semantics.
// - The IEnumerable<T> path behaves identically to the struct path
//   (it boxes the enumerator, but the set semantics line up).
public class CeleritySetEnumerationTests
{
    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenEmpty()
    {
        var set = new CeleritySet<int, Int32WangNaiveHasher>();

        var seen = new List<int>();
        foreach (int item in set)
            seen.Add(item);

        Assert.Empty(seen);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldSingleEntry()
    {
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
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
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
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
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
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
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
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
        // The contract is that the out-of-band default(T) entry is yielded
        // first. Subsequent ordering is unspecified, but the *first* element
        // when default is present must be default.
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
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
        var set = new CeleritySet<string, StringFnV1AHasher>();
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
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
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
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
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
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
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
        var set = new CeleritySet<int, Int32WangNaiveHasher>(capacity: 4);
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
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
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
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
        set.Add(1);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        set.Add(0); // default-entry insert goes through the out-of-band slot.

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterRemoveDuringEnumeration()
    {
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
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
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
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
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
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
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
        set.Add(1);

        var enumerator = set.GetEnumerator();
        enumerator.MoveNext();

        set.Add(2);

        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void TryAdd_ShouldNotBumpVersion_WhenItemAlreadyPresent()
    {
        // A no-op TryAdd (item already present) must not invalidate active
        // enumerators — there was no structural change.
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
        set.Add(1);
        set.Add(2);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.False(set.TryAdd(1)); // already present, must be a no-op

        // Should not throw — no structural change occurred.
        bool advanced = enumerator.MoveNext();
        // We don't assert on advanced specifically because default may or may
        // not have been emitted first; the contract is just that no exception
        // fires.
        _ = advanced;
    }

    [Fact]
    public void TryAdd_ShouldNotBumpVersion_WhenDefaultAlreadyPresent()
    {
        // Same no-op semantics for the out-of-band default-value slot: if it's
        // already populated, a re-add is structurally a no-op and must not
        // invalidate active enumerators.
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
        set.Add(0);
        set.Add(1);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.False(set.TryAdd(0)); // default slot already populated, must be a no-op

        // Should not throw — no structural change occurred.
        _ = enumerator.MoveNext();
    }

    [Fact]
    public void Remove_ShouldNotBumpVersion_WhenItemAbsent()
    {
        // Removing an item that isn't there is also a no-op and must not
        // invalidate active enumerators.
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
        set.Add(1);
        set.Add(2);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.False(set.Remove(99)); // not present, must be a no-op

        // Should not throw.
        _ = enumerator.MoveNext();
    }

    [Fact]
    public void Clear_ShouldNotBumpVersion_WhenAlreadyEmpty()
    {
        // Clearing an already-empty set is a no-op and must not invalidate
        // active enumerators.
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
        var enumerator = set.GetEnumerator();

        set.Clear();

        // Should not throw — set was empty before Clear.
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Enumerator_ShouldBeReusableViaReset()
    {
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
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
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
        set.Add(7);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(7, enumerator.Current);
        Assert.False(enumerator.MoveNext());
        // After exhaustion the enumerator's Current is reset to default(T) — 0.
        // (BCL HashSet returns default; we match that contract.)
        Assert.Equal(0, enumerator.Current);
    }

    [Fact]
    public void Set_ShouldBeIterableThroughIEnumerable()
    {
        // Iterating through the IEnumerable<T> interface boxes the struct
        // enumerator; we don't make that zero-allocation, but we do guarantee
        // that it behaves identically to the struct path.
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
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
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
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
        // Drive the enumerator on the open-generic CeleritySet<T, THasher>
        // path with a different hasher to confirm the surface works for callers
        // that pick a custom hasher.
        var set = new CeleritySet<int, Int32Murmur3Hasher>();
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
        // The IEnumerable<T> conformance unlocks LINQ. Probe the most common
        // operators to make sure the boxing path lines up.
        var set = new CeleritySet<int, Int32WangNaiveHasher>();
        for (int i = 1; i <= 5; i++)
            set.Add(i);
        set.Add(0);

        Assert.Equal(6, System.Linq.Enumerable.Count(set));
        Assert.Equal(15, System.Linq.Enumerable.Sum(set));   // 0+1+2+3+4+5
        // Confirm LINQ's IEnumerable<int>.Contains lights up via the boxed
        // enumerator path (assigning to a local sidesteps the xUnit2017
        // analyzer, which would otherwise nudge us toward Assert.Contains —
        // we want LINQ's Contains here, not the set's native Contains).
        bool linqHasZero = System.Linq.Enumerable.Contains(set, 0);
        bool linqHasFive = System.Linq.Enumerable.Contains(set, 5);
        bool linqHas99 = System.Linq.Enumerable.Contains(set, 99);
        Assert.True(linqHasZero);
        Assert.True(linqHasFive);
        Assert.False(linqHas99);
    }
}
