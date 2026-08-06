using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural coverage for <see cref="SegmentTree{T, TMonoid}"/>: the range-query / point-update core, the
/// indexer, <see cref="SegmentTree{T, TMonoid}.Combine(int, T)"/>, the four constructors, boundary and
/// validation corners, <see cref="SegmentTree{T, TMonoid}.Clear"/>, and the enumeration surface. The
/// randomized reconciliation against a naive array oracle — including the non-commutative case that the
/// <c>2n</c> layout has to get right — lives in <see cref="SegmentTreeDifferentialTests"/>, and the built-in
/// folds are covered in <see cref="MonoidTests"/>.
/// </summary>
public class SegmentTreeTests
{
    [Fact]
    public void Constructor_ShouldStartAtIdentity_WhenGivenLength()
    {
        var tree = new SegmentTree<int, MinMonoid<int>>(8);

        Assert.Equal(8, tree.Count);
        Assert.Equal(int.MaxValue, tree.Aggregate);
        for (int i = 0; i < 8; i++)
            Assert.Equal(int.MaxValue, tree[i]);
    }

    [Fact]
    public void Constructor_ShouldAllowZeroLength()
    {
        var tree = new SegmentTree<int, SumMonoid<int>>(0);

        Assert.Equal(0, tree.Count);
        Assert.Equal(0, tree.Aggregate);
        Assert.Equal(0, tree.Query(0, 0));
        Assert.Empty(tree);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLengthNegative()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SegmentTree<int, SumMonoid<int>>(-1));
        Assert.Equal("length", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLengthExceedsMaxSupported()
    {
        // The layout needs 2 * length array slots, so anything above Array.MaxLength / 2 must be rejected up
        // front rather than overflowing into an OverflowException / OutOfMemoryException from the allocation.
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SegmentTree<int, SumMonoid<int>>(int.MaxValue));
        Assert.Equal("length", ex.ParamName);

        var atCeiling = Assert.Throws<ArgumentOutOfRangeException>(
            () => new SegmentTree<int, SumMonoid<int>>(Array.MaxLength / 2 + 1));
        Assert.Equal("length", atCeiling.ParamName);
    }

    [Fact]
    public void Constructor_ShouldUseTheSuppliedMonoid_WhenGivenLengthAndInstance()
    {
        // A stateful monoid can only be supplied through the explicit overload — the parameterless path
        // closes over default(TMonoid).
        var tree = new SegmentTree<int, SaturatingSumMonoid>(4, new SaturatingSumMonoid(10));

        Assert.Equal(4, tree.Count);
        Assert.Equal(0, tree.Aggregate);

        tree[0] = 7;
        tree[1] = 7;
        Assert.Equal(10, tree.Query(0, 2));   // 14 saturated at the ceiling the instance carries
    }

    [Fact]
    public void EnumerableConstructor_ShouldSeedLogicalValues()
    {
        int[] values = { 3, 1, 4, 1, 5, 9, 2, 6 };

        var tree = new SegmentTree<int, MinMonoid<int>>(values);

        Assert.Equal(8, tree.Count);
        Assert.Equal(1, tree.Aggregate);
        for (int i = 0; i < values.Length; i++)
            Assert.Equal(values[i], tree[i]);
    }

    [Fact]
    public void EnumerableConstructor_ShouldSeedLogicalValues_WhenSourceIsNotCounted()
    {
        // A lazy sequence has no ICollection<T>.Count, so the constructor takes the materialize-once path.
        IEnumerable<int> lazy = Enumerable.Range(1, 5).Select(i => i * i);

        var tree = new SegmentTree<int, MaxMonoid<int>>(lazy);

        Assert.Equal(5, tree.Count);
        Assert.Equal(25, tree.Aggregate);
        Assert.Equal(9, tree.Query(0, 3));
    }

    [Fact]
    public void EnumerableConstructor_ShouldThrow_WhenSourceIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new SegmentTree<int, SumMonoid<int>>((IEnumerable<int>)null!));
        Assert.Equal("values", ex.ParamName);
    }

    [Fact]
    public void EnumerableConstructor_ShouldUseTheSuppliedMonoid_WhenGivenAnInstance()
    {
        var tree = new SegmentTree<int, SaturatingSumMonoid>(new[] { 4, 4, 4 }, new SaturatingSumMonoid(9));

        Assert.Equal(3, tree.Count);
        Assert.Equal(9, tree.Aggregate);     // 12 saturated
        Assert.Equal(8, tree.Query(0, 2));
    }

    [Fact]
    public void EnumerableConstructor_ShouldAcceptAnEmptySource()
    {
        var tree = new SegmentTree<int, SumMonoid<int>>(Array.Empty<int>());

        Assert.Equal(0, tree.Count);
        Assert.Equal(0, tree.Aggregate);
    }

    // ---- Query ------------------------------------------------------------------------------------

    [Fact]
    public void Query_ShouldReturnTheRangeAggregate()
    {
        var tree = new SegmentTree<int, MinMonoid<int>>(new[] { 5, 3, 8, 1, 9, 2 });

        Assert.Equal(3, tree.Query(0, 3));
        Assert.Equal(1, tree.Query(2, 5));
        Assert.Equal(2, tree.Query(4, 6));
        Assert.Equal(1, tree.Query(0, 6));
    }

    [Fact]
    public void Query_ShouldReturnIdentity_WhenRangeIsEmpty()
    {
        var tree = new SegmentTree<int, MinMonoid<int>>(new[] { 5, 3, 8 });

        Assert.Equal(int.MaxValue, tree.Query(0, 0));
        Assert.Equal(int.MaxValue, tree.Query(2, 2));
        Assert.Equal(int.MaxValue, tree.Query(3, 3));
    }

    [Fact]
    public void Query_ShouldThrow_WhenBoundsAreOutOfRange()
    {
        var tree = new SegmentTree<int, SumMonoid<int>>(4);

        var low = Assert.Throws<ArgumentOutOfRangeException>(() => tree.Query(-1, 2));
        Assert.Equal("start", low.ParamName);

        var high = Assert.Throws<ArgumentOutOfRangeException>(() => tree.Query(0, 5));
        Assert.Equal("endExclusive", high.ParamName);

        var startAboveCount = Assert.Throws<ArgumentOutOfRangeException>(() => tree.Query(5, 5));
        Assert.Equal("start", startAboveCount.ParamName);
    }

    [Fact]
    public void Query_ShouldThrow_WhenEndPrecedesStart()
    {
        var tree = new SegmentTree<int, SumMonoid<int>>(4);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => tree.Query(3, 1));
        Assert.Equal("endExclusive", ex.ParamName);
    }

    [Fact]
    public void Aggregate_ShouldFoldTheWholeSequence_WhenLengthIsNotAPowerOfTwo()
    {
        // The 2n layout only makes the root the whole-sequence fold at power-of-two lengths, so this is the
        // shape that catches an Aggregate implemented as a root read.
        var tree = new SegmentTree<int, SumMonoid<int>>(new[] { 1, 2, 3 });

        Assert.Equal(6, tree.Aggregate);
        Assert.Equal(tree.Query(0, 3), tree.Aggregate);
    }

    // ---- Updates ----------------------------------------------------------------------------------

    [Fact]
    public void Indexer_ShouldAssignAndRefoldThePathToTheRoot()
    {
        var tree = new SegmentTree<int, MinMonoid<int>>(new[] { 5, 3, 8, 1 });

        tree[3] = 7;

        Assert.Equal(7, tree[3]);
        Assert.Equal(3, tree.Aggregate);
        Assert.Equal(7, tree.Query(3, 4));
        Assert.Equal(7, tree.Query(2, 4));
    }

    [Fact]
    public void Indexer_ShouldWork_WhenTreeHoldsASingleElement()
    {
        // A one-element tree has no internal node, so the refold loop never runs — the boundary the ancestor
        // walk has to survive.
        var tree = new SegmentTree<int, MinMonoid<int>>(1);

        tree[0] = 42;

        Assert.Equal(42, tree[0]);
        Assert.Equal(42, tree.Aggregate);
        Assert.Equal(42, tree.Query(0, 1));
    }

    [Fact]
    public void Indexer_ShouldThrow_WhenIndexOutOfRange()
    {
        var tree = new SegmentTree<int, SumMonoid<int>>(4);

        var get = Assert.Throws<ArgumentOutOfRangeException>(() => tree[4]);
        Assert.Equal("index", get.ParamName);

        var negative = Assert.Throws<ArgumentOutOfRangeException>(() => tree[-1]);
        Assert.Equal("index", negative.ParamName);

        var set = Assert.Throws<ArgumentOutOfRangeException>(() => tree[4] = 1);
        Assert.Equal("index", set.ParamName);
    }

    [Fact]
    public void Combine_ShouldFoldTheValueIntoTheElement()
    {
        var tree = new SegmentTree<int, SumMonoid<int>>(new[] { 1, 2, 3, 4 });

        tree.Combine(1, 10);

        Assert.Equal(12, tree[1]);
        Assert.Equal(20, tree.Aggregate);
    }

    [Fact]
    public void Combine_ShouldKeepTheStoredValueOnTheLeft_WhenMonoidIsNotCommutative()
    {
        var tree = new SegmentTree<string, ConcatMonoid>(new[] { "a", "b" });

        tree.Combine(0, "X");

        Assert.Equal("aX", tree[0]);
        Assert.Equal("aXb", tree.Aggregate);
    }

    [Fact]
    public void Combine_ShouldThrow_WhenIndexOutOfRange()
    {
        var tree = new SegmentTree<int, SumMonoid<int>>(4);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => tree.Combine(9, 1));
        Assert.Equal("index", ex.ParamName);
    }

    [Fact]
    public void Clear_ShouldResetEveryElementToIdentity_AndKeepTheLength()
    {
        var tree = new SegmentTree<int, MinMonoid<int>>(new[] { 5, 3, 8, 1, 9 });

        tree.Clear();

        Assert.Equal(5, tree.Count);
        Assert.Equal(int.MaxValue, tree.Aggregate);
        for (int i = 0; i < 5; i++)
            Assert.Equal(int.MaxValue, tree[i]);

        // Reusable after the reset.
        tree[2] = 4;
        Assert.Equal(4, tree.Aggregate);
    }

    // ---- Enumeration ------------------------------------------------------------------------------

    [Fact]
    public void GetEnumerator_ShouldYieldLogicalValuesInIndexOrder()
    {
        int[] values = { 3, 1, 4, 1, 5 };
        var tree = new SegmentTree<int, MaxMonoid<int>>(values);

        Assert.Equal(values, tree.ToArray());
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenTreeIsEmpty()
    {
        var tree = new SegmentTree<int, SumMonoid<int>>(0);

        Assert.Empty(tree.ToArray());
    }

    [Fact]
    public void NonGenericEnumerator_ShouldYieldTheSameValues()
    {
        var tree = new SegmentTree<int, SumMonoid<int>>(new[] { 7, 8 });

        IEnumerator untyped = ((IEnumerable)tree).GetEnumerator();
        var seen = new List<int>();
        while (untyped.MoveNext())
            seen.Add((int)untyped.Current!);

        Assert.Equal(new[] { 7, 8 }, seen);
    }

    [Fact]
    public void Enumerator_ShouldStayExhausted_WhenMoveNextIsCalledPastTheEnd()
    {
        var tree = new SegmentTree<int, SumMonoid<int>>(new[] { 1 });

        SegmentTree<int, SumMonoid<int>>.Enumerator enumerator = tree.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current);
        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);
        Assert.False(enumerator.MoveNext());

        enumerator.Dispose();
    }

    [Fact]
    public void Enumerator_ShouldReplayFromTheStart_AfterReset()
    {
        var tree = new SegmentTree<int, SumMonoid<int>>(new[] { 4, 5 });

        SegmentTree<int, SumMonoid<int>>.Enumerator enumerator = tree.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());

        enumerator.Reset();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(4, enumerator.Current);
    }

    [Fact]
    public void Enumerator_ShouldThrow_WhenTreeIsModifiedDuringEnumeration()
    {
        var tree = new SegmentTree<int, SumMonoid<int>>(new[] { 1, 2, 3 });

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (int _ in tree)
                tree[0] = 9;
        });
    }

    [Fact]
    public void Enumerator_ShouldThrow_WhenCombineRunsDuringEnumeration()
    {
        var tree = new SegmentTree<int, SumMonoid<int>>(new[] { 1, 2, 3 });

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (int _ in tree)
                tree.Combine(0, 1);
        });
    }

    [Fact]
    public void Indexer_ShouldInvalidateEnumerators_EvenWhenAssigningTheStoredValue()
    {
        // Deliberately unlike FenwickTree, which detects a zero delta: IMonoid<T> carries no equality
        // obligation, so the segment tree cannot tell a redundant assignment from a real one and does not
        // pretend to. Pinned so the difference reads as a decision rather than an oversight.
        var tree = new SegmentTree<int, SumMonoid<int>>(new[] { 1, 2, 3 });

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (int _ in tree)
                tree[0] = tree[0];
        });
    }

    [Fact]
    public void SegmentTree_ShouldBeUsableAsAReadOnlyList()
    {
        // Unlike FenwickTree the leaves are stored outright, so an O(1) indexer makes IReadOnlyList<T> an
        // honest claim rather than a trap for a consumer that indexes in a loop.
        IReadOnlyList<int> list = new SegmentTree<int, SumMonoid<int>>(new[] { 3, 1, 4 });

        Assert.Equal(3, list.Count);
        Assert.Equal(1, list[1]);
        Assert.Equal(new[] { 3, 1, 4 }, list.ToArray());
    }

    // ---- Reference-type elements ------------------------------------------------------------------

    [Fact]
    public void SegmentTree_ShouldSupportReferenceTypeElements()
    {
        var tree = new SegmentTree<string, ConcatMonoid>(new[] { "a", "b", "c", "d", "e" });

        Assert.Equal("abcde", tree.Aggregate);
        Assert.Equal("bcd", tree.Query(1, 4));
        Assert.Equal(string.Empty, tree.Query(2, 2));

        tree[2] = "Z";
        Assert.Equal("abZde", tree.Aggregate);
    }

}
