using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

// Issue #531: GetEnumerator and EnumerateRange on HierarchicalBitSet.
//
// Mirrors SparseSetEnumerationTests for the fail-fast contract, with two differences that come from the
// layout: enumeration is in ascending order, and the enumerator keeps the unvisited bits of one leaf word
// and jumps to the next non-empty word through the summaries. So the cases below cross word boundaries,
// skip long empty stretches, and stop a range part-way through a word.
public class HierarchicalBitSetEnumerationTests
{
    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenEmpty()
    {
        var set = new HierarchicalBitSet(1000);

        Assert.Empty(set);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldSingleElement()
    {
        var set = new HierarchicalBitSet(1000);
        set.Add(777);

        Assert.Equal(new[] { 777 }, set);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldElementsInAscendingOrder_AcrossWordsAndGaps()
    {
        int[] values = { 0, 1, 63, 64, 65, 127, 128, 4095, 4096, 200_000, 299_999 };
        var set = new HierarchicalBitSet(300_000);
        for (int i = values.Length - 1; i >= 0; i--)
            set.Add(values[i]);

        Assert.Equal(values, set);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldADenseRun_OnceEach()
    {
        var set = new HierarchicalBitSet(1000);
        for (int i = 100; i < 600; i++)
            set.Add(i);

        Assert.Equal(Enumerable.Range(100, 500), set);
    }

    [Fact]
    public void GetEnumerator_ShouldReflectRemoval()
    {
        var set = new HierarchicalBitSet(100, new[] { 1, 2, 3, 70 });
        set.Remove(2);
        set.Remove(70);

        Assert.Equal(new[] { 1, 3 }, set);
    }

    [Fact]
    public void GetEnumerator_ShouldReflectClear()
    {
        var set = new HierarchicalBitSet(100, new[] { 1, 2, 3 });
        set.Clear();

        Assert.Empty(set);
    }

    [Fact]
    public void Current_ShouldBeZero_BeforeFirstMoveNext_AndAfterExhaustion()
    {
        var set = new HierarchicalBitSet(100, new[] { 42 });
        var enumerator = set.GetEnumerator();

        Assert.Equal(0, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(42, enumerator.Current);
        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    // ── EnumerateRange ────────────────────────────────────────────────────────

    [Fact]
    public void EnumerateRange_ShouldYieldHalfOpenRange_InAscendingOrder()
    {
        var set = new HierarchicalBitSet(10_000, new[] { 1, 10, 63, 64, 100, 5000, 9999 });

        Assert.Equal(new[] { 10, 63, 64, 100 }, set.EnumerateRange(10, 5000));
        Assert.Equal(new[] { 63 }, set.EnumerateRange(11, 64));
    }

    [Fact]
    public void EnumerateRange_ShouldStopPartWayThroughAWord()
    {
        // 3, 5 and 7 share a leaf word; the range ends between them.
        var set = new HierarchicalBitSet(100, new[] { 3, 5, 7 });

        Assert.Equal(new[] { 3, 5 }, set.EnumerateRange(0, 6));
        Assert.Equal(new[] { 5 }, set.EnumerateRange(4, 7));
    }

    [Fact]
    public void EnumerateRange_ShouldClampBoundsOutsideTheUniverse()
    {
        var set = new HierarchicalBitSet(100, new[] { 0, 99 });

        Assert.Equal(new[] { 0, 99 }, set.EnumerateRange(int.MinValue, int.MaxValue));
        Assert.Empty(set.EnumerateRange(-10, 0));
        Assert.Empty(set.EnumerateRange(100, 200));
    }

    [Fact]
    public void EnumerateRange_ShouldBeEmpty_WhenNothingLiesInRange()
    {
        var set = new HierarchicalBitSet(10_000, new[] { 1, 9000 });

        Assert.Empty(set.EnumerateRange(2, 9000));
        Assert.Empty(set.EnumerateRange(5, 5));
    }

    [Fact]
    public void EnumerateRange_ShouldThrow_WhenToIsBelowFrom()
    {
        var set = new HierarchicalBitSet(100);

        var ex = Assert.Throws<ArgumentException>(() => set.EnumerateRange(10, 9));
        Assert.Equal("toExclusive", ex.ParamName);
    }

    [Fact]
    public void EnumerateRange_ShouldRoundTrip_ThroughTheInterfaces()
    {
        var set = new HierarchicalBitSet(100, new[] { 1, 2, 3, 50 });
        IEnumerable<int> range = set.EnumerateRange(2, 51);

        Assert.Equal(new[] { 2, 3, 50 }, range.ToList());

        var (first, second) = DrainNonGenericTwice<int>(set.EnumerateRange(2, 51));
        Assert.Equal(new[] { 2, 3, 50 }, first);
        Assert.Equal(first, second);
    }

    // ── Fail-fast ─────────────────────────────────────────────────────────────

    [Fact]
    public void MoveNext_ShouldThrow_WhenSetIsMutatedMidEnumeration()
    {
        var set = new HierarchicalBitSet(100, new[] { 1, 2, 3 });

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        set.Add(4);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterRemoveDuringEnumeration()
    {
        var set = new HierarchicalBitSet(100, new[] { 1, 2 });

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        set.Remove(2);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_AfterClearDuringEnumeration()
    {
        var set = new HierarchicalBitSet(100, new[] { 1, 2, 3, 4 });

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        set.Clear();

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldThrow_WhenSetIsMutatedAfterExhaustion()
    {
        // The version check runs before the exhausted short-circuit, as List<T>'s does.
        var set = new HierarchicalBitSet(100, new[] { 1 });

        var enumerator = set.GetEnumerator();
        while (enumerator.MoveNext()) { }

        set.Add(2);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldNotThrow_WhenAMutationChangesNothing()
    {
        var set = new HierarchicalBitSet(100, new[] { 1, 2 });

        var enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());

        Assert.False(set.TryAdd(1));
        Assert.False(set.Remove(50));

        Assert.True(enumerator.MoveNext());
        Assert.Equal(2, enumerator.Current);
    }

    [Fact]
    public void Reset_ShouldThrow_WhenSetIsMutatedMidEnumeration()
    {
        var set = new HierarchicalBitSet(100, new[] { 1 });

        var enumerator = set.GetEnumerator();
        enumerator.MoveNext();

        set.Add(2);

        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void Enumerator_ShouldBeReusableViaReset()
    {
        var set = new HierarchicalBitSet(1000, new[] { 1, 2, 700 });

        var enumerator = set.EnumerateRange(2, 1000).GetEnumerator();
        var firstPass = new List<int>();
        while (enumerator.MoveNext()) firstPass.Add(enumerator.Current);

        enumerator.Reset();
        var secondPass = new List<int>();
        while (enumerator.MoveNext()) secondPass.Add(enumerator.Current);

        Assert.Equal(new[] { 2, 700 }, firstPass);
        Assert.Equal(firstPass, secondPass);
    }

    [Fact]
    public void Set_ShouldRoundTrip_ThroughNonGenericIEnumerable()
    {
        var set = new HierarchicalBitSet(100);
        for (int i = 4; i >= 1; i--) set.Add(i * 10);

        var (first, second) = DrainNonGenericTwice<int>(set);

        Assert.Equal(new[] { 10, 20, 30, 40 }, first);
        Assert.Equal(first, second);
    }

    // Drives the struct enumerator through its boxed non-generic IEnumerable / IEnumerator surface
    // (GetEnumerator -> object Current), then Reset()s and drains again.
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
