using Celerity.Collections;

namespace Celerity.Tests.Collections;

// Issue #287: SparseSet, a bounded-universe sparse integer set backed by the
// Briggs–Torczon dense+sparse representation — O(1) Clear (no memory touched) and
// dense, cache-friendly iteration.
//
// These tests mirror the dedicated IntSetTests / SmallSetTests behavioural coverage,
// adapted for a type with a *fixed universe*: out-of-range values throw on Add and
// read as absent on Contains/Remove, and the headline O(1) Clear must leave the set
// reusable even though it never clears the backing arrays (the sparse↔dense round-trip
// must reject the stale entries left behind). The set-algebra surface (UnionWith /
// IsSubsetOf / ...) is covered by SetAlgebraTests (conformance) and the dedicated
// SparseSetDifferentialTests (a non-negative-universe differential, since SparseSet
// cannot join the int-parameterized SetAlgebraDifferentialTests whose universe spans
// negatives).
public class SparseSetTests
{
    [Fact]
    public void Add_ShouldStoreElement()
    {
        var set = new SparseSet(100);
        set.Add(10);

        Assert.True(set.Contains(10));
        Assert.Single(set);
    }

    [Fact]
    public void Add_ShouldThrow_OnDuplicate()
    {
        var set = new SparseSet(100);
        set.Add(5);

        var ex = Assert.Throws<ArgumentException>(() => set.Add(5));
        Assert.Contains("5", ex.Message);
        Assert.Single(set);
    }

    [Fact]
    public void TryAdd_ShouldReturnTrueThenFalse()
    {
        var set = new SparseSet(100);

        Assert.True(set.TryAdd(7));
        Assert.False(set.TryAdd(7));
        Assert.Single(set);
    }

    [Fact]
    public void Contains_ShouldReturnFalse_ForAbsentElement()
    {
        var set = new SparseSet(100);
        set.Add(1);

        Assert.False(set.Contains(99));
    }

    // The zero value is an ordinary element — there is no out-of-band default slot,
    // and the round-trip membership check works uniformly for it.
    [Fact]
    public void ZeroElement_ShouldBeHandled_AsOrdinaryElement()
    {
        var set = new SparseSet(100);
        set.Add(0);
        set.Add(1);

        Assert.True(set.Contains(0));
        Assert.Equal(2, set.Count);

        Assert.False(set.TryAdd(0)); // duplicate zero is a no-op
        Assert.True(set.Remove(0));
        Assert.False(set.Contains(0));
        Assert.Single(set);
    }

    [Fact]
    public void Add_ShouldStoreTopOfUniverse()
    {
        var set = new SparseSet(100);
        set.Add(99); // Universe - 1 is in range

        Assert.True(set.Contains(99));
        Assert.Single(set);
    }

    // ── Out-of-range handling ──────────────────────────────────────────────────

    [Theory]
    [InlineData(-1)]
    [InlineData(100)] // == Universe (exclusive bound)
    [InlineData(1000)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Add_ShouldThrow_WhenOutOfRange(int value)
    {
        var set = new SparseSet(100);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => set.Add(value));
        Assert.Equal("item", ex.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void TryAdd_ShouldThrow_WhenOutOfRange(int value)
    {
        var set = new SparseSet(100);
        Assert.Throws<ArgumentOutOfRangeException>(() => set.TryAdd(value));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Contains_ShouldReturnFalse_WhenOutOfRange(int value)
    {
        var set = new SparseSet(100);
        Assert.False(set.Contains(value));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Remove_ShouldReturnFalse_WhenOutOfRange(int value)
    {
        var set = new SparseSet(100);
        Assert.False(set.Remove(value));
    }

    // ── Remove ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Remove_ShouldDeleteElementAndMakeItUnreachable()
    {
        var set = new SparseSet(100);
        set.Add(7);

        Assert.True(set.Remove(7));
        Assert.False(set.Contains(7));
        Assert.Empty(set);
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenElementDoesNotExist()
    {
        var set = new SparseSet(100);
        Assert.False(set.Remove(7));
    }

    [Fact]
    public void Remove_ShouldNotOrphanOtherElements_WhenRemovingFromMiddle()
    {
        // Removal swaps the last dense element into the vacated slot and repoints its
        // sparse entry. Every surviving element must remain reachable regardless of
        // which slot is removed.
        var set = new SparseSet(50);
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
    public void Remove_Then_Reinsert_ManyElements_ShouldNotLoseEntries()
    {
        var set = new SparseSet(200);
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

    // ── Growth ─────────────────────────────────────────────────────────────────

    [Fact]
    public void DenseArray_ShouldGrow_WhenManyElementsAdded()
    {
        // The dense array starts empty and doubles; adding across the whole universe
        // exercises several growth steps and must preserve every element.
        var set = new SparseSet(1000);
        for (int i = 0; i < 1000; i++)
            set.Add(i);

        Assert.Equal(1000, set.Count);
        for (int i = 0; i < 1000; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void FullUniverse_ShouldStoreEveryValue()
    {
        var set = new SparseSet(64);
        for (int i = 0; i < 64; i++)
            set.Add(i);

        Assert.Equal(64, set.Count);
        Assert.Equal(64, set.Universe);
        Assert.True(set.SetEquals(Enumerable.Range(0, 64)));
    }

    // ── Clear (the headline: O(1), no arrays cleared, still correct) ────────────

    [Fact]
    public void Clear_ShouldRemoveAllElements_AndLeaveSetReusable()
    {
        var set = new SparseSet(200);
        for (int i = 0; i < 100; i++)
            set.Add(i);

        set.Clear();

        Assert.Empty(set);
        // Every previously-present value must now read as absent even though the
        // backing arrays still hold the stale entries.
        for (int i = 0; i < 100; i++)
            Assert.False(set.Contains(i));

        // Reusable after clearing — re-adding must produce correct membership.
        set.Add(0);
        set.Add(150);
        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(0));
        Assert.True(set.Contains(150));
        Assert.False(set.Contains(1));
        Assert.False(set.Contains(99));
    }

    [Fact]
    public void Clear_ThenReadd_SameValue_ShouldNotFalsePositive()
    {
        // A value present before Clear must not be reported present after Clear until
        // it is re-added — the exact case the stale-sparse round-trip check guards.
        var set = new SparseSet(10);
        set.Add(3);
        set.Add(7);
        set.Clear();

        Assert.False(set.Contains(3));
        Assert.False(set.Contains(7));

        set.Add(7);
        Assert.True(set.Contains(7));
        Assert.False(set.Contains(3));
        Assert.Single(set);
    }

    [Fact]
    public void Clear_ShouldBeNoOp_WhenAlreadyEmpty()
    {
        var set = new SparseSet(10);
        set.Clear(); // _count == 0 early-return path
        Assert.Empty(set);
    }

    // ── Constructor ────────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_ShouldExposeUniverse()
    {
        var set = new SparseSet(500);
        Assert.Equal(500, set.Universe);
        Assert.Empty(set);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenUniverseIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SparseSet(-1));
    }

    [Fact]
    public void ZeroUniverse_ShouldStoreNothing_AndRejectEveryValue()
    {
        var set = new SparseSet(0);
        Assert.Equal(0, set.Universe);
        Assert.Throws<ArgumentOutOfRangeException>(() => set.Add(0));
        Assert.False(set.Contains(0));
        Assert.False(set.Remove(0));
        Assert.Empty(set);
    }

    // ── IEnumerable<int> constructor ───────────────────────────────────────────

    [Fact]
    public void SourceConstructor_ShouldCopyAllElements()
    {
        var source = new[] { 1, 2, 3, 4, 5 };

        var set = new SparseSet(100, source);

        Assert.Equal(5, set.Count);
        foreach (int item in source)
            Assert.True(set.Contains(item));
    }

    [Fact]
    public void SourceConstructor_ShouldSilentlyDedupe()
    {
        var source = new[] { 1, 2, 1, 3, 2, 0, 0 };

        var set = new SparseSet(100, source);

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
        var ex = Assert.Throws<ArgumentNullException>(() => new SparseSet(100, source!));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void SourceConstructor_NullSource_ShouldBeatNegativeUniverse()
    {
        // A null source must surface as ArgumentNullException even when the universe is
        // also invalid — the null check runs first (mirrors the set family).
        IEnumerable<int>? source = null;
        Assert.Throws<ArgumentNullException>(() => new SparseSet(-5, source!));
    }

    [Fact]
    public void SourceConstructor_ShouldThrow_WhenSourceHasOutOfRangeValue()
    {
        var source = new[] { 1, 2, 500 };
        Assert.Throws<ArgumentOutOfRangeException>(() => new SparseSet(100, source));
    }

    [Fact]
    public void SourceConstructor_ShouldAcceptLazyNonCollectionSource()
    {
        IEnumerable<int> source = Enumerable.Range(0, 20).Where(x => x % 2 == 0);

        var set = new SparseSet(100, source);

        Assert.Equal(10, set.Count);
        Assert.True(set.Contains(0));
        Assert.True(set.Contains(18));
        Assert.False(set.Contains(1));
    }

    // ── EnsureCapacity / TrimExcess ────────────────────────────────────────────

    [Fact]
    public void EnsureCapacity_GrowsDenseArray()
    {
        var set = new SparseSet(1000);
        int reported = set.EnsureCapacity(200);
        Assert.True(reported >= 200);

        for (int i = 0; i < 200; i++)
            set.Add(i);

        Assert.Equal(200, set.Count);
        for (int i = 0; i < 200; i++)
            Assert.True(set.Contains(i));
    }

    [Fact]
    public void EnsureCapacity_ShouldClampToUniverse()
    {
        var set = new SparseSet(16);
        // Requesting more than the universe cannot help — the dense array never needs
        // to hold more than Universe elements — so it is clamped.
        int reported = set.EnsureCapacity(1000);
        Assert.True(reported <= 16);
    }

    [Fact]
    public void EnsureCapacity_ShouldThrow_WhenNegative()
    {
        var set = new SparseSet(10);
        Assert.Throws<ArgumentOutOfRangeException>(() => set.EnsureCapacity(-1));
    }

    [Fact]
    public void TrimExcess_AfterShrink_PreservesContents()
    {
        var set = new SparseSet(1000);
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
        var set = new SparseSet(100);
        for (int i = 1; i <= 10; i++)
            set.Add(i);

        Assert.Throws<ArgumentOutOfRangeException>(() => set.TrimExcess(3));
    }

    [Fact]
    public void TrimExcess_ShouldThrow_WhenCapacityAboveUniverse()
    {
        var set = new SparseSet(10);
        Assert.Throws<ArgumentOutOfRangeException>(() => set.TrimExcess(11));
    }

    // ── ICollection<T> / ISet<T> conformance ───────────────────────────────────

    [Fact]
    public void ISetAdd_ReturnsBool_WithoutThrowingOnDuplicate()
    {
        ISet<int> set = new SparseSet(100);
        Assert.True(set.Add(1));
        Assert.False(set.Add(1)); // duplicate — no throw, returns false
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void ICollectionAdd_DoesNotThrowOnDuplicate()
    {
        ICollection<int> set = new SparseSet(100);
        set.Add(5);
        set.Add(5); // duplicate ignored, unlike the public Add(int)
        Assert.Single(set);
    }

    [Fact]
    public void CopyTo_ShouldCopyEveryElement()
    {
        var set = new SparseSet(100);
        for (int i = 1; i <= 5; i++)
            set.Add(i);

        var array = new int[7];
        set.CopyTo(array, 1);

        Assert.Equal(0, array[0]);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, array[1..6].OrderBy(x => x).ToArray());
        Assert.Equal(0, array[6]);
    }
}
