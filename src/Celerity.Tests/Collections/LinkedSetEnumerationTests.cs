using System.Collections;
using System.Collections.Generic;
using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

// GetEnumerator on LinkedSet.
//
// Mirror of LinkedDictionaryEnumerationTests. LinkedSet has a defined order —
// insertion order, as rearranged by MoveToEnd / MoveToFront — so these tests
// assert the exact sequence. The default element is an ordinary in-table entry,
// so it appears at its insertion position rather than first. Every mutator is
// checked to invalidate an open enumerator, and every no-op to leave it valid.
public class LinkedSetEnumerationTests
{
    private static LinkedSet<int, Int32WangNaiveHasher> NewSet(params int[] items)
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        foreach (int item in items)
            set.Add(item);
        return set;
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenEmpty()
    {
        var set = NewSet();

        var seen = new List<int>();
        foreach (int item in set)
            seen.Add(item);

        Assert.Empty(seen);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldSingleElement()
    {
        var set = NewSet(7);

        var seen = new List<int>();
        foreach (int item in set)
            seen.Add(item);

        Assert.Equal(7, Assert.Single(seen));
    }

    [Fact]
    public void GetEnumerator_ShouldYieldEveryElementExactlyOnce_InInsertionOrder()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        for (int i = 50; i >= 1; i--)
            set.Add(i);

        var seen = new List<int>();
        foreach (int item in set)
            seen.Add(item);

        Assert.Equal(Enumerable.Range(1, 50).Reverse(), seen);
    }

    [Fact]
    public void GetEnumerator_ShouldIncludeDefaultElement_AtItsInsertionPosition_ForInt()
    {
        var set = NewSet(1, 0, 42);

        var seen = new List<int>();
        foreach (int item in set)
            seen.Add(item);

        Assert.Equal(new[] { 1, 0, 42 }, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldIncludeNullElement_AtItsInsertionPosition_ForString()
    {
        var set = new LinkedSet<string, StringFnV1AHasher>();
        set.Add("alpha");
        set.Add(null!);
        set.Add("beta");

        var seen = new List<string?>();
        foreach (string? item in set)
            seen.Add(item);

        Assert.Equal(new[] { "alpha", null, "beta" }, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldReflectRemoval_AndKeepOrder()
    {
        var set = NewSet(1, 2, 3);
        set.Remove(2);

        Assert.Equal(new[] { 1, 3 }, set.ToArray());
    }

    [Fact]
    public void GetEnumerator_ShouldReflectMoves()
    {
        var set = NewSet(1, 2, 3, 4, 5);

        set.MoveToEnd(2);
        set.MoveToFront(4);
        set.MoveToEnd(4);

        Assert.Equal(new[] { 1, 3, 5, 2, 4 }, set.ToArray());
    }

    [Fact]
    public void GetEnumerator_ShouldReflectClear()
    {
        var set = NewSet(0, 1, 2, 3);
        set.Clear();

        int count = 0;
        foreach (int _ in set)
            count++;

        Assert.Equal(0, count);
    }

    [Fact]
    public void GetEnumerator_ShouldSurviveResize_InOrder()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(capacity: 16);
        for (int i = 1; i <= 200; i++)
            set.Add(i);

        var seen = new List<int>();
        foreach (int item in set)
            seen.Add(item);

        Assert.Equal(Enumerable.Range(1, 200), seen);
    }

    [Fact]
    public void Current_ShouldBeDefault_BeforeFirstMoveNextAndAfterTheEnd()
    {
        var set = NewSet(3);

        var enumerator = set.GetEnumerator();
        Assert.Equal(0, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(3, enumerator.Current);
        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);
        Assert.False(enumerator.MoveNext()); // stays exhausted
        enumerator.Dispose();
    }

    [Fact]
    public void Current_ShouldBeNull_AfterTheEnd_ForReferenceElements()
    {
        var set = new LinkedSet<string, StringFnV1AHasher>();
        set.Add("a");

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);
        Assert.False(enumerator.MoveNext());
        Assert.Null(enumerator.Current);
    }

    // ---------------------------------------------------------------
    //  Version invalidation — every structural mutator
    // ---------------------------------------------------------------

    public static TheoryData<string> Mutators => new()
    {
        "Add",
        "TryAdd",
        "AddDefault",
        "ICollectionAdd",
        "ISetAdd",
        "Remove",
        "MoveToEnd",
        "MoveToFront",
        "TryRemoveFirst",
        "TryRemoveLast",
        "Clear",
        "EnsureCapacity",
        "TrimExcess",
        "TrimExcessWithCapacity",
        "UnionWith",
        "IntersectWith",
        "ExceptWith",
        "SymmetricExceptWith",
    };

    private static void Mutate(LinkedSet<int, Int32WangNaiveHasher> set, string mutator)
    {
        switch (mutator)
        {
            case "Add": set.Add(100); break;
            case "TryAdd": Assert.True(set.TryAdd(100)); break;
            case "AddDefault": set.Add(0); break;
            case "ICollectionAdd": ((ICollection<int>)set).Add(100); break;
            case "ISetAdd": Assert.True(((ISet<int>)set).Add(100)); break;
            case "Remove": Assert.True(set.Remove(2)); break;
            case "MoveToEnd": Assert.True(set.MoveToEnd(1)); break;
            case "MoveToFront": Assert.True(set.MoveToFront(3)); break;
            case "TryRemoveFirst": Assert.True(set.TryRemoveFirst(out _)); break;
            case "TryRemoveLast": Assert.True(set.TryRemoveLast(out _)); break;
            case "Clear": set.Clear(); break;
            case "EnsureCapacity": set.EnsureCapacity(1000); break;
            case "TrimExcess": set.TrimExcess(); break;
            case "TrimExcessWithCapacity": set.TrimExcess(500); break;
            case "UnionWith": set.UnionWith(new[] { 1, 100 }); break;
            case "IntersectWith": set.IntersectWith(new[] { 1, 2 }); break;
            case "ExceptWith": set.ExceptWith(new[] { 3 }); break;
            case "SymmetricExceptWith": set.SymmetricExceptWith(new[] { 3, 100 }); break;
            default: throw new ArgumentOutOfRangeException(nameof(mutator), mutator, null);
        }
    }

    [Theory]
    [MemberData(nameof(Mutators))]
    public void MoveNext_ShouldThrow_AfterEveryStructuralMutation(string mutator)
    {
        // A 64-bucket table holding 3 elements, so TrimExcess shrinks and EnsureCapacity grows.
        var set = new LinkedSet<int, Int32WangNaiveHasher>(capacity: 64);
        set.Add(1);
        set.Add(2);
        set.Add(3);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Mutate(set, mutator);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Theory]
    [MemberData(nameof(Mutators))]
    public void Reset_ShouldThrow_AfterEveryStructuralMutation(string mutator)
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>(capacity: 64);
        set.Add(1);
        set.Add(2);
        set.Add(3);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Mutate(set, mutator);

        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void MoveNext_ShouldThrow_WhenDefaultElementRemovedDuringEnumeration()
    {
        var set = NewSet(1, 0, 2);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.True(set.Remove(0));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterTheEnd_WhenTheSetIsMutated()
    {
        // The version check runs before the end-of-list check, so an exhausted enumerator still
        // reports a later modification.
        var set = NewSet(1);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());

        set.Add(2);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldNotThrow_AfterFailedOrNoOpMutationsDuringEnumeration()
    {
        // Misses and no-ops change nothing, so they must not invalidate the enumerator.
        var set = new LinkedSet<int, Int32WangNaiveHasher>(capacity: 4);
        set.Add(1);
        set.Add(2);
        set.Add(3);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.False(set.Remove(99));
        Assert.False(set.TryAdd(2));
        Assert.Throws<ArgumentException>(() => set.Add(2));
        ((ICollection<int>)set).Add(2);
        Assert.False(((ISet<int>)set).Add(2));
        Assert.False(set.MoveToEnd(99));
        Assert.False(set.MoveToFront(99));
        Assert.True(set.MoveToEnd(3));   // already last
        Assert.True(set.MoveToFront(1)); // already first
        Assert.True(set.TryGetFirst(out _));
        Assert.True(set.TryGetLast(out _));
        Assert.True(set.Contains(2));
        set.EnsureCapacity(1);           // already large enough
        set.TrimExcess();                // MinTableSizeFor(3, 0.75) = 4 buckets, the current size
        set.UnionWith(new[] { 1, 2 });   // nothing new
        set.UnionWith(set);              // self: no-op
        set.IntersectWith(set);          // self: no-op
        set.IntersectWith(new[] { 1, 2, 3, 4 }); // keeps everything
        set.ExceptWith(new[] { 99 });    // nothing to remove
        set.SymmetricExceptWith(Array.Empty<int>());
        _ = set.IsSubsetOf(new[] { 1 });
        _ = set.Overlaps(new[] { 1 });
        _ = set.SetEquals(new[] { 1 });
        set.CopyTo(new int[3], 0);

        var rest = new List<int>();
        while (enumerator.MoveNext())
            rest.Add(enumerator.Current);

        Assert.Equal(new[] { 2, 3 }, rest);
    }

    [Fact]
    public void MoveNext_ShouldNotThrow_AfterNoOpsOnAnEmptySet()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        var enumerator = set.GetEnumerator();

        set.Clear();
        Assert.False(set.TryRemoveFirst(out _));
        Assert.False(set.TryRemoveLast(out _));
        set.ExceptWith(new[] { 1 });
        set.IntersectWith(new[] { 1 });

        Assert.False(enumerator.MoveNext());
    }

    // ---------------------------------------------------------------
    //  Reset, boxed and non-generic enumerators
    // ---------------------------------------------------------------

    [Fact]
    public void Enumerator_ShouldBeReusableViaReset()
    {
        var set = NewSet(1, 2);

        var enumerator = set.GetEnumerator();
        var firstPass = new List<int>();
        while (enumerator.MoveNext()) firstPass.Add(enumerator.Current);

        enumerator.Reset();
        var secondPass = new List<int>();
        while (enumerator.MoveNext()) secondPass.Add(enumerator.Current);

        Assert.Equal(new[] { 1, 2 }, firstPass);
        Assert.Equal(firstPass, secondPass);
    }

    [Fact]
    public void Enumerator_ResetMidway_ShouldRestartFromTheCurrentHead()
    {
        var set = NewSet(1, 2, 3);

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        enumerator.Reset();
        Assert.Equal(0, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current);
    }

    [Fact]
    public void BoxedGenericEnumerator_ShouldYieldInOrder_AndHonourReset()
    {
        var set = NewSet(4, 0, 9);

        using IEnumerator<int> e = ((IEnumerable<int>)set).GetEnumerator();
        var first = new List<int>();
        while (e.MoveNext()) first.Add(e.Current);
        e.Reset();
        var second = new List<int>();
        while (e.MoveNext()) second.Add(e.Current);

        Assert.Equal(new[] { 4, 0, 9 }, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void BoxedGenericEnumerator_ShouldThrow_WhenTheSetIsMutated()
    {
        var set = NewSet(1, 2);

        IEnumerator<int> e = ((IEnumerable<int>)set).GetEnumerator();
        Assert.True(e.MoveNext());
        set.Remove(2);

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
        Assert.Throws<InvalidOperationException>(() => e.Reset());
    }

    [Fact]
    public void Set_ShouldRoundTrip_ThroughNonGenericIEnumerable()
    {
        var set = new LinkedSet<int, Int32WangNaiveHasher>();
        for (int i = 4; i >= 1; i--) set.Add(i);
        set.Add(0);

        var (first, second) = DrainNonGenericTwice<int>(set);

        Assert.Equal(new[] { 4, 3, 2, 1, 0 }, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void NonGenericEnumerator_CurrentShouldBoxNull_ForReferenceElements()
    {
        var set = new LinkedSet<string, StringFnV1AHasher>();
        set.Add(null!);
        set.Add("b");

        IEnumerator e = ((IEnumerable)set).GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.Null(e.Current);
        Assert.True(e.MoveNext());
        Assert.Equal("b", e.Current);
        Assert.False(e.MoveNext());
        Assert.Null(e.Current);
    }

    [Fact]
    public void NonGenericEnumerator_ShouldThrow_WhenTheSetIsMutated()
    {
        var set = NewSet(1, 2);

        IEnumerator e = ((IEnumerable)set).GetEnumerator();
        Assert.True(e.MoveNext());
        Assert.Equal(1, e.Current);
        set.MoveToEnd(1);

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
    }

    [Fact]
    public void Set_ShouldEnumerateThroughGenericIEnumerable_InOrder()
    {
        var set = NewSet(3, 1, 2);

        IEnumerable<int> items = set;

        Assert.Equal(new[] { 3, 1, 2 }, items.Select(i => i).ToArray());
    }

    private static (List<T> first, List<T> second) DrainNonGenericTwice<T>(IEnumerable view)
    {
        IEnumerator e = view.GetEnumerator();
        var first = new List<T>();
        while (e.MoveNext()) first.Add((T)e.Current!);
        e.Reset();
        var second = new List<T>();
        while (e.MoveNext()) second.Add((T)e.Current!);
        return (first, second);
    }
}
