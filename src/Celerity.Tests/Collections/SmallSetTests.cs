using Celerity.Collections;

namespace Celerity.Tests.Collections;

// Issue #257: SmallSet<T>, a flat-array, linear-scan set optimized for the
// very-small (n <= ~16) case — the set counterpart to SmallDictionary (#61).
//
// These tests mirror the dedicated IntSetTests / SmallDictionaryTests behavioural
// coverage, adapted for a type that hashes nothing: there is no out-of-band
// default-element slot, so a 0 / null / default element is exercised as an ordinary
// element, and the "resize" cases assert the grow-and-keep-scanning behaviour
// rather than a rehash. The set-algebra surface (UnionWith / IsSubsetOf / ...) is
// covered family-wide by SetAlgebraTests and SetAlgebraDifferentialTests.
public class SmallSetTests
{
    [Fact]
    public void Add_ShouldStoreElement()
    {
        var set = new SmallSet<int>();
        set.Add(10);

        Assert.True(set.Contains(10));
        Assert.Single(set);
    }

    [Fact]
    public void Add_ShouldThrow_OnDuplicate()
    {
        var set = new SmallSet<int>();
        set.Add(5);

        var ex = Assert.Throws<ArgumentException>(() => set.Add(5));
        Assert.Contains("5", ex.Message);
        Assert.Single(set);
    }

    [Fact]
    public void TryAdd_ShouldReturnTrueThenFalse()
    {
        var set = new SmallSet<int>();

        Assert.True(set.TryAdd(7));
        Assert.False(set.TryAdd(7));
        Assert.Single(set);
    }

    [Fact]
    public void Contains_ShouldReturnFalse_ForAbsentElement()
    {
        var set = new SmallSet<int>();
        set.Add(1);

        Assert.False(set.Contains(99));
    }

    [Fact]
    public void Remove_ShouldDeleteElementAndMakeItUnreachable()
    {
        var set = new SmallSet<int>();
        set.Add(7);

        Assert.True(set.Remove(7));
        Assert.False(set.Contains(7));
        Assert.Empty(set);
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenElementDoesNotExist()
    {
        var set = new SmallSet<int>();
        Assert.False(set.Remove(7));
    }

    [Fact]
    public void Remove_ShouldNotOrphanOtherElements_WhenRemovingFromMiddle()
    {
        // Removal swaps the last element into the vacated slot. Every surviving
        // element must remain reachable regardless of which slot is removed.
        var set = new SmallSet<int>();
        for (int i = 0; i < 10; i++)
            set.Add(i);

        Assert.True(set.Remove(0)); // first
        Assert.True(set.Remove(5)); // middle
        Assert.True(set.Remove(9)); // last

        Assert.Equal(7, set.Count);
        for (int i = 0; i < 10; i++)
        {
            if (i == 0 || i == 5 || i == 9)
                Assert.False(set.Contains(i));
            else
                Assert.True(set.Contains(i));
        }
    }

    [Fact]
    public void Set_ShouldGrow_WhenCapacityExceeded()
    {
        var set = new SmallSet<int>(capacity: 4);
        for (int i = 1; i <= 50; i++)
            set.Add(i);

        Assert.Equal(50, set.Count);
        for (int i = 1; i <= 50; i++)
            Assert.True(set.Contains(i));
    }

    // SmallSet hashes nothing, so a default element (0 / null / default) is an
    // ordinary inline entry rather than an out-of-band slot.
    [Fact]
    public void ZeroElement_ShouldBeHandled_AsOrdinaryEntry()
    {
        var set = new SmallSet<int>();
        set.Add(0);
        set.Add(1);

        Assert.True(set.Contains(0));
        Assert.Equal(2, set.Count);

        Assert.True(set.Remove(0));
        Assert.False(set.Contains(0));
        Assert.Single(set);
    }

    [Fact]
    public void NullElement_ShouldBeHandled_AsOrdinaryEntry()
    {
        var set = new SmallSet<string?>();
        set.Add("a");
        set.Add(null);

        Assert.True(set.Contains(null));
        Assert.Equal(2, set.Count);

        Assert.False(set.TryAdd(null)); // duplicate null is a no-op

        Assert.True(set.Remove(null));
        Assert.False(set.Contains(null));
        Assert.Single(set);
    }

    [Fact]
    public void Clear_ShouldRemoveAllElements()
    {
        var set = new SmallSet<int>();
        for (int i = 0; i < 32; i++)
            set.Add(i);

        set.Clear();

        Assert.Empty(set);
        for (int i = 0; i < 32; i++)
            Assert.False(set.Contains(i));

        // Reusable after clearing.
        set.Add(0);
        set.Add(5);
        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(0));
        Assert.True(set.Contains(5));
    }

    [Fact]
    public void Clear_ShouldBeNoOp_WhenAlreadyEmpty()
    {
        var set = new SmallSet<int>();

        set.Clear(); // _count == 0 early-return path

        Assert.Empty(set);
    }

    [Fact]
    public void Remove_Then_Reinsert_ManyElements_ShouldNotLoseEntries()
    {
        var set = new SmallSet<int>(8);
        for (int i = 1; i <= 100; i++)
            set.Add(i);

        for (int i = 1; i <= 100; i += 2)
            Assert.True(set.Remove(i));

        Assert.Equal(50, set.Count);

        for (int i = 1; i <= 100; i += 2)
            set.Add(i);

        Assert.Equal(100, set.Count);
        for (int i = 1; i <= 100; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void ZeroCapacity_ShouldDeferAllocation_AndStillInsert()
    {
        var set = new SmallSet<int>(capacity: 0);
        set.Add(42);

        Assert.True(set.Contains(42));
        Assert.Single(set);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenCapacityIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SmallSet<int>(-1));
    }

    // ── IEnumerable<T> constructor ─────────────────────────────────────────────

    [Fact]
    public void SourceConstructor_ShouldCopyAllElements()
    {
        var source = new[] { 1, 2, 3, 4, 5 };

        var set = new SmallSet<int>(source);

        Assert.Equal(5, set.Count);
        foreach (int item in source)
            Assert.True(set.Contains(item));
    }

    [Fact]
    public void SourceConstructor_ShouldSilentlyDedupe()
    {
        var source = new[] { 1, 2, 1, 3, 2, 0, 0 };

        var set = new SmallSet<int>(source);

        Assert.Equal(4, set.Count);
        Assert.True(set.Contains(0));
        Assert.True(set.Contains(1));
        Assert.True(set.Contains(2));
        Assert.True(set.Contains(3));
    }

    [Fact]
    public void SourceConstructor_ShouldThrow_WhenSourceIsNull()
    {
        IEnumerable<int>? source = null;
        var ex = Assert.Throws<ArgumentNullException>(() => new SmallSet<int>(source!));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void SourceConstructor_ShouldUseCallerCapacity_WhenLargerThanSourceCount()
    {
        var source = new[] { 1, 2, 3 };

        var set = new SmallSet<int>(source, capacity: 64);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(1));
        Assert.True(set.Contains(2));
        Assert.True(set.Contains(3));
    }

    // ── EnsureCapacity / TrimExcess ────────────────────────────────────────────

    [Fact]
    public void EnsureCapacity_GrowsBackingArray()
    {
        var set = new SmallSet<int>();
        int reported = set.EnsureCapacity(100);
        Assert.True(reported >= 100);

        for (int i = 1; i <= 100; i++)
            set.Add(i);

        Assert.Equal(100, set.Count);
        for (int i = 1; i <= 100; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void EnsureCapacity_ShouldThrow_WhenNegative()
    {
        var set = new SmallSet<int>();
        Assert.Throws<ArgumentOutOfRangeException>(() => set.EnsureCapacity(-1));
    }

    [Fact]
    public void TrimExcess_AfterShrink_PreservesContents()
    {
        var set = new SmallSet<int>();
        for (int i = 1; i <= 100; i++)
            set.Add(i);
        for (int i = 6; i <= 100; i++)
            set.Remove(i);

        set.TrimExcess();

        Assert.Equal(5, set.Count);
        for (int i = 1; i <= 5; i++)
            Assert.True(set.Contains(i));

        set.Add(999); // still usable after the shrink
        Assert.True(set.Contains(999));
    }

    [Fact]
    public void TrimExcess_ShouldThrow_WhenCapacityBelowCount()
    {
        var set = new SmallSet<int>();
        for (int i = 1; i <= 10; i++)
            set.Add(i);

        Assert.Throws<ArgumentOutOfRangeException>(() => set.TrimExcess(3));
    }

    // ── ICollection<T> / ISet<T> conformance ───────────────────────────────────

    [Fact]
    public void ISetAdd_ReturnsBool_WithoutThrowing()
    {
        ISet<int> set = new SmallSet<int>();
        Assert.True(set.Add(1));
        Assert.False(set.Add(1)); // duplicate — no throw, returns false
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void ICollectionAdd_DoesNotThrowOnDuplicate()
    {
        ICollection<int> set = new SmallSet<int>();
        set.Add(5);
        set.Add(5); // duplicate ignored, unlike the public Add(T)
        Assert.Single(set);
    }

    [Fact]
    public void CopyTo_ShouldCopyEveryElement()
    {
        var set = new SmallSet<int>();
        for (int i = 1; i <= 5; i++)
            set.Add(i);

        var array = new int[7];
        set.CopyTo(array, 1);

        Assert.Equal(0, array[0]);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, array[1..6].OrderBy(x => x).ToArray());
        Assert.Equal(0, array[6]);
    }
}
