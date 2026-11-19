using System.Collections;
using Celerity.Collections;
using AffineTree = Celerity.Collections.LazySegmentTree<long, (long Mul, long Add), Celerity.Collections.SumMonoid<long>, Celerity.Tests.Collections.AffineSumAction>;
using MaxAddTree = Celerity.Collections.LazySegmentTree<int, int, Celerity.Collections.MaxMonoid<int>, Celerity.Collections.MinMaxAddAction<int>>;
using MinAddTree = Celerity.Collections.LazySegmentTree<int, int, Celerity.Collections.MinMonoid<int>, Celerity.Collections.MinMaxAddAction<int>>;
using SumAddTree = Celerity.Collections.LazySegmentTree<long, long, Celerity.Collections.SumMonoid<long>, Celerity.Collections.SumAddAction<long>>;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural coverage for <see cref="LazySegmentTree{T, TUpdate, TMonoid, TAction}"/>: range update and range
/// query, the indexer, the four constructors, boundary and validation corners, <c>Clear</c>, the enumeration
/// surface, and the four shipped actions. The randomized reconciliation against a naive array oracle lives in
/// <see cref="LazySegmentTreeDifferentialTests"/>.
/// </summary>
public class LazySegmentTreeTests
{
    // ---- Constructors ------------------------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldStartAtIdentity_WhenGivenLength()
    {
        var tree = new MinAddTree(8);

        Assert.Equal(8, tree.Count);
        Assert.Equal(int.MaxValue, tree.Aggregate);
        for (int i = 0; i < 8; i++)
            Assert.Equal(int.MaxValue, tree[i]);
    }

    [Fact]
    public void Constructor_ShouldAllowZeroLength()
    {
        var tree = new SumAddTree(0);

        Assert.Equal(0, tree.Count);
        Assert.Equal(0, tree.Aggregate);
        Assert.Equal(0, tree.Query(0, 0));
        tree.Apply(0, 0, 5);
        Assert.Equal(0, tree.Aggregate);
        Assert.Empty(tree);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLengthNegative()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SumAddTree(-1));
        Assert.Equal("length", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLengthExceedsMaxSupported()
    {
        // The leaf count rounds up to a power of two and the layout stores two cells per leaf, so 2^29 is the
        // ceiling; one past it must be rejected before any allocation is attempted.
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SumAddTree(int.MaxValue));
        Assert.Equal("length", ex.ParamName);

        var pastCeiling = Assert.Throws<ArgumentOutOfRangeException>(() => new SumAddTree((1 << 29) + 1));
        Assert.Equal("length", pastCeiling.ParamName);
    }

    [Fact]
    public void Constructor_ShouldUseTheSuppliedInstances_WhenGivenLengthAndInstances()
    {
        // A stateful fold or action can only be supplied through the explicit overload — the parameterless path
        // closes over default(TMonoid) and default(TAction).
        var tree = new LazySegmentTree<int, int, FlooredMaxMonoid, ScaledMinMaxAddAction>(
            4, new FlooredMaxMonoid(-100), new ScaledMinMaxAddAction(10));

        Assert.Equal(-100, tree.Aggregate);

        tree[1] = 3;
        tree.Apply(1, 3, 2);   // + 2 × 10 on [1, 3)
        Assert.Equal(23, tree[1]);
        Assert.Equal(23, tree.Aggregate);
    }

    [Fact]
    public void EnumerableConstructor_ShouldSeedLogicalValues()
    {
        var tree = new SumAddTree(new long[] { 5, 3, 8, 1, 9 });

        Assert.Equal(5, tree.Count);
        Assert.Equal(new long[] { 5, 3, 8, 1, 9 }, tree.ToArray());
        Assert.Equal(26, tree.Aggregate);
    }

    [Fact]
    public void EnumerableConstructor_ShouldSeedLogicalValues_WhenSourceIsNotCounted()
    {
        var tree = new MinAddTree(Enumerable.Range(0, 6).Select(i => 10 - i));

        Assert.Equal(6, tree.Count);
        Assert.Equal(5, tree.Aggregate);
        Assert.Equal(7, tree.Query(0, 4));
    }

    [Fact]
    public void EnumerableConstructor_ShouldThrow_WhenSourceIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new SumAddTree(null!));
        Assert.Equal("values", ex.ParamName);
    }

    [Fact]
    public void EnumerableConstructor_ShouldUseTheSuppliedInstances_WhenGivenInstances()
    {
        var tree = new LazySegmentTree<int, int, FlooredMaxMonoid, ScaledMinMaxAddAction>(
            new[] { -500, -400 }, new FlooredMaxMonoid(0), new ScaledMinMaxAddAction(100));

        Assert.Equal(-400, tree.Aggregate);
        Assert.Equal(0, tree.Query(1, 1));      // an empty range folds to the identity the instance carries
        tree.Apply(0, 2, 5);                    // + 5 × 100 each, by the scale the action instance carries
        Assert.Equal(100, tree.Aggregate);
        Assert.Equal(new[] { 0, 100 }, tree.ToArray());
    }

    [Fact]
    public void EnumerableConstructor_ShouldAcceptAnEmptySource()
    {
        var tree = new SumAddTree(Array.Empty<long>());

        Assert.Equal(0, tree.Count);
        Assert.Equal(0, tree.Aggregate);
        Assert.Empty(tree);
    }

    [Fact]
    public void EnumerableConstructor_ShouldPadToAPowerOfTwo_WithoutLeakingPaddingIntoAggregates()
    {
        // Five leaves pad to eight; the three padding leaves hold the identity and must never receive an update.
        var tree = new MinAddTree(new[] { 4, 7, 1, 9, 3 });

        tree.Apply(0, 5, -1000);
        Assert.Equal(-999, tree.Aggregate);
        Assert.Equal(-997, tree.Query(3, 5));
        Assert.Equal(new[] { -996, -993, -999, -991, -997 }, tree.ToArray());
    }

    // ---- Apply / Query ----------------------------------------------------------------------------

    [Fact]
    public void Apply_ShouldAddToEveryElementOfTheRange_UnderSum()
    {
        var tree = new SumAddTree(10);

        tree.Apply(2, 7, 3);    // 5 elements × 3
        tree.Apply(5, 10, 1);   // 5 elements × 1

        Assert.Equal(20, tree.Aggregate);
        Assert.Equal(9, tree.Query(2, 5));
        Assert.Equal(8, tree.Query(5, 7));
        Assert.Equal(new long[] { 0, 0, 3, 3, 3, 4, 4, 1, 1, 1 }, tree.ToArray());
    }

    [Fact]
    public void Apply_ShouldMoveTheMaximum_UnderMax()
    {
        // The reservation shape: book seats over time ranges, ask for the peak occupancy.
        var tree = new MaxAddTree(new int[24]);

        tree.Apply(9, 12, 1);
        tree.Apply(10, 14, 1);
        tree.Apply(11, 13, 1);

        Assert.Equal(3, tree.Aggregate);
        Assert.Equal(2, tree.Query(9, 11));
        Assert.Equal(0, tree.Query(0, 9));
        Assert.Equal(1, tree.Query(13, 24));
    }

    [Fact]
    public void Apply_ShouldBeANoOp_WhenRangeIsEmpty_AndShouldNotInvalidateEnumerators()
    {
        var tree = new SumAddTree(new long[] { 1, 2, 3 });

        var seen = new List<long>();
        foreach (long value in tree)
        {
            tree.Apply(1, 1, 100);
            seen.Add(value);
        }

        Assert.Equal(new long[] { 1, 2, 3 }, seen);
    }

    [Fact]
    public void Apply_ShouldThrow_WhenBoundsAreOutOfRange()
    {
        var tree = new SumAddTree(4);

        Assert.Equal("start", Assert.Throws<ArgumentOutOfRangeException>(() => tree.Apply(-1, 2, 1)).ParamName);
        Assert.Equal("start", Assert.Throws<ArgumentOutOfRangeException>(() => tree.Apply(5, 5, 1)).ParamName);
        Assert.Equal("endExclusive", Assert.Throws<ArgumentOutOfRangeException>(() => tree.Apply(0, 5, 1)).ParamName);
        Assert.Equal("endExclusive", Assert.Throws<ArgumentOutOfRangeException>(() => tree.Apply(3, 2, 1)).ParamName);
    }

    [Fact]
    public void Query_ShouldReturnIdentity_WhenRangeIsEmpty()
    {
        var tree = new MinAddTree(new[] { 3, 1, 2 });

        Assert.Equal(int.MaxValue, tree.Query(0, 0));
        Assert.Equal(int.MaxValue, tree.Query(2, 2));
        Assert.Equal(int.MaxValue, tree.Query(3, 3));
    }

    [Fact]
    public void Query_ShouldThrow_WhenBoundsAreOutOfRange()
    {
        var tree = new SumAddTree(4);

        Assert.Equal("start", Assert.Throws<ArgumentOutOfRangeException>(() => tree.Query(-1, 2)).ParamName);
        Assert.Equal("start", Assert.Throws<ArgumentOutOfRangeException>(() => tree.Query(5, 5)).ParamName);
        Assert.Equal("endExclusive", Assert.Throws<ArgumentOutOfRangeException>(() => tree.Query(0, 5)).ParamName);
        Assert.Equal("endExclusive", Assert.Throws<ArgumentOutOfRangeException>(() => tree.Query(3, 2)).ParamName);
    }

    [Fact]
    public void Apply_ShouldComposePendingUpdatesInTheOrderTheyWereApplied()
    {
        // (×2, +1) then (×3, +0) gives 3·(2x + 1) = 6x + 3; the reverse order would give 6x + 1. The second update
        // lands on a node the first left a pending update on, so a wrongly ordered Compose shows up in the sum.
        var tree = new AffineTree(new long[] { 1, 1, 1, 1 });

        tree.Apply(0, 4, (2, 1));
        tree.Apply(0, 4, (3, 0));

        Assert.Equal(4 * 9, tree.Aggregate);
        Assert.Equal(9, tree.Query(1, 2));     // pushes the pending composition down to one leaf
        Assert.Equal(new long[] { 9, 9, 9, 9 }, tree.ToArray());
    }

    // ---- Indexer ----------------------------------------------------------------------------------

    [Fact]
    public void Indexer_ShouldSeePendingUpdates_AndAssignOverThem()
    {
        var tree = new SumAddTree(new long[] { 1, 2, 3, 4 });
        tree.Apply(0, 4, 10);

        Assert.Equal(12, tree[1]);

        tree[2] = 0;
        Assert.Equal(0, tree[2]);
        Assert.Equal(11 + 12 + 0 + 14, tree.Aggregate);

        tree.Apply(1, 3, 1);
        Assert.Equal(new long[] { 11, 13, 1, 14 }, tree.ToArray());
    }

    [Fact]
    public void Indexer_ShouldWork_WhenTreeHoldsASingleElement()
    {
        var tree = new SumAddTree(new long[] { 7 });

        tree.Apply(0, 1, 3);
        Assert.Equal(10, tree[0]);
        Assert.Equal(10, tree.Aggregate);

        tree[0] = -1;
        Assert.Equal(-1, tree.Aggregate);
    }

    [Fact]
    public void Indexer_ShouldThrow_WhenIndexOutOfRange()
    {
        var tree = new SumAddTree(3);

        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => tree[-1]).ParamName);
        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => tree[3]).ParamName);
        Assert.Equal("index", Assert.Throws<ArgumentOutOfRangeException>(() => tree[3] = 1).ParamName);
    }

    // ---- Clear ------------------------------------------------------------------------------------

    [Fact]
    public void Clear_ShouldResetEveryElementToIdentity_AndDiscardPendingUpdates()
    {
        var tree = new SumAddTree(new long[] { 1, 2, 3, 4, 5 });
        tree.Apply(1, 4, 100);

        tree.Clear();

        Assert.Equal(5, tree.Count);
        Assert.Equal(0, tree.Aggregate);
        Assert.Equal(new long[5], tree.ToArray());

        tree.Apply(0, 5, 1);
        Assert.Equal(5, tree.Aggregate);   // no stale +100 resurfaces from a pending slot
    }

    // ---- Enumeration ------------------------------------------------------------------------------

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenTreeIsEmpty()
    {
        Assert.Empty(new SumAddTree(0).ToArray());
    }

    [Fact]
    public void NonGenericEnumerator_ShouldYieldTheSameValues()
    {
        var tree = new SumAddTree(new long[] { 7, 8 });
        tree.Apply(0, 2, 1);

        IEnumerator untyped = ((IEnumerable)tree).GetEnumerator();
        var seen = new List<long>();
        while (untyped.MoveNext())
            seen.Add((long)untyped.Current!);

        Assert.Equal(new long[] { 8, 9 }, seen);
    }

    [Fact]
    public void Enumerator_ShouldStayExhausted_WhenMoveNextIsCalledPastTheEnd()
    {
        var tree = new SumAddTree(new long[] { 1 });

        SumAddTree.Enumerator enumerator = tree.GetEnumerator();
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
        var tree = new SumAddTree(new long[] { 4, 5 });

        SumAddTree.Enumerator enumerator = tree.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());

        enumerator.Reset();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(4, enumerator.Current);
    }

    [Fact]
    public void Enumerator_ShouldThrow_WhenApplyRunsDuringEnumeration()
    {
        var tree = new SumAddTree(new long[] { 1, 2, 3 });

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (long _ in tree)
                tree.Apply(0, 1, 1);
        });
    }

    [Fact]
    public void Enumerator_ShouldThrow_WhenResetAfterModification()
    {
        var tree = new SumAddTree(new long[] { 1, 2, 3 });

        SumAddTree.Enumerator enumerator = tree.GetEnumerator();
        tree[0] = 5;

        Assert.Throws<InvalidOperationException>(() => enumerator.Reset());
    }

    [Fact]
    public void Indexer_ShouldInvalidateEnumerators_EvenWhenAssigningTheStoredValue()
    {
        // Neither IMonoid<T> nor IRangeAction<T, TUpdate> carries an equality obligation, so a redundant
        // assignment cannot be told from a real one — the same decision SegmentTree pins.
        var tree = new SumAddTree(new long[] { 1, 2, 3 });

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (long _ in tree)
                tree[0] = tree[0];
        });
    }

    [Fact]
    public void Reads_ShouldNotInvalidateEnumerators_EvenThoughTheyPushPendingUpdates()
    {
        // A query and an indexer read push pending updates down the paths they touch. That changes no logical
        // value, so it must not bump the version — and the leaves the enumerator reads must stay correct.
        var tree = new SumAddTree(new long[] { 1, 2, 3, 4, 5, 6 });
        tree.Apply(1, 5, 10);

        var seen = new List<long>();
        foreach (long value in tree)
        {
            _ = tree.Query(0, 6);
            _ = tree[3];
            seen.Add(value);
        }

        Assert.Equal(new long[] { 1, 12, 13, 14, 15, 6 }, seen);
    }

    [Fact]
    public void LazySegmentTree_ShouldBeUsableAsAReadOnlyCollection()
    {
        IReadOnlyCollection<int> collection = new MinAddTree(new[] { 3, 1, 4 });

        Assert.Equal(3, collection.Count);
        Assert.Equal(new[] { 3, 1, 4 }, collection.ToArray());
    }

    // ---- Reference-type elements ------------------------------------------------------------------

    [Fact]
    public void LazySegmentTree_ShouldSupportReferenceTypeElements_AndUseTheNodeCount()
    {
        var tree = new LazySegmentTree<string, string?, ConcatMonoid, RepeatAssignAction>(
            new[] { "a", "b", "c", "d", "e" });

        Assert.Equal("abcde", tree.Aggregate);

        tree.Apply(1, 4, "x");
        Assert.Equal("axxxe", tree.Aggregate);
        Assert.Equal("xxx", tree.Query(1, 4));

        tree.Apply(2, 3, null);   // the identity update changes nothing
        tree[4] = "Z";
        Assert.Equal("axxxZ", tree.Aggregate);
    }

    // ---- Shipped actions --------------------------------------------------------------------------

    [Fact]
    public void SumAssignAction_ShouldSetEveryElement_AndLetALaterAssignmentWin()
    {
        var tree = new LazySegmentTree<long, long?, SumMonoid<long>, SumAssignAction<long>>(
            new long[] { 1, 2, 3, 4, 5, 6 });

        tree.Apply(0, 6, 2);
        tree.Apply(2, 4, 10);
        tree.Apply(3, 6, null);   // no-op update

        Assert.Equal(2 + 2 + 10 + 10 + 2 + 2, tree.Aggregate);
        Assert.Equal(new long[] { 2, 2, 10, 10, 2, 2 }, tree.ToArray());
    }

    [Fact]
    public void SumAssignAction_ShouldComposeAsLaterWins()
    {
        var action = default(SumAssignAction<int>);

        Assert.Null(action.Identity);
        Assert.Equal(7, action.Compose(3, 7));
        Assert.Equal(3, action.Compose(3, null));
        Assert.Equal(21, action.Apply(7, 99, 3));
        Assert.Equal(99, action.Apply(null, 99, 3));
    }

    [Fact]
    public void SumAddAction_ShouldWrapExactly_ForAFixedWidthInteger()
    {
        // 300 elements of a byte, +1 each: the wrapping sum moves by 300 mod 256 = 44, which is what the
        // truncated count gives. A repeated-addition model agrees.
        var action = default(SumAddAction<byte>);

        byte repeated = 0;
        for (int i = 0; i < 300; i++)
            repeated++;

        Assert.Equal(repeated, action.Apply(1, 0, 300));
        Assert.Equal((byte)0, action.Identity);
        Assert.Equal((byte)5, action.Compose(2, 3));
    }

    [Fact]
    public void MinMaxAddAction_ShouldMoveTheExtremeByTheUpdateAlone()
    {
        var action = default(MinMaxAddAction<int>);

        Assert.Equal(0, action.Identity);
        Assert.Equal(5, action.Compose(2, 3));
        Assert.Equal(12, action.Apply(2, 10, 1_000));
    }

    [Fact]
    public void IdempotentAssignAction_ShouldWorkWithAnyIdempotentFold()
    {
        var min = new LazySegmentTree<int, int?, MinMonoid<int>, IdempotentAssignAction<int>>(
            new[] { 5, 3, 8, 1 });
        min.Apply(0, 2, 9);
        Assert.Equal(1, min.Aggregate);
        Assert.Equal(8, min.Query(0, 3));

        var or = new LazySegmentTree<uint, uint?, BitwiseOrMonoid<uint>, IdempotentAssignAction<uint>>(
            new uint[] { 1, 2, 4, 8 });
        or.Apply(1, 3, 16u);
        Assert.Equal(1u | 16u | 8u, or.Aggregate);

        var action = default(IdempotentAssignAction<int>);
        Assert.Null(action.Identity);
        Assert.Equal(7, action.Compose(3, 7));
        Assert.Equal(3, action.Compose(3, null));
        Assert.Equal(4, action.Apply(null, 4, 2));
    }
}
