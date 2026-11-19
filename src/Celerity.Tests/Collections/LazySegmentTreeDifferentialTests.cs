using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Randomized and exhaustive reconciliation of <see cref="LazySegmentTree{T, TUpdate, TMonoid, TAction}"/> against
/// a naive array that applies every update element by element and folds every query left to right.
///
/// <para>
/// Two things are on trial. The first is <b>push discipline</b>: an update parks on the nodes that tile its range,
/// and every later operation must push the parked updates on its boundary paths before reading or refolding below
/// them. A missed push shows up as a stale aggregate only after a particular interleaving of overlapping updates
/// and reads, which is why the scripts below interleave all four operations over short, heavily overlapping
/// ranges. The second is <b>order</b>: pending updates must compose earlier-then-later and queries must fold in
/// index order. The shipped actions compose commutatively and the shipped folds are commutative, so neither can
/// observe a mistake there; <see cref="AffineSumAction"/> (non-commutative updates) and
/// <see cref="ConcatMonoid"/> with <see cref="RepeatAssignAction"/> (a non-commutative fold that also needs the
/// node's element count) can.
/// </para>
/// </summary>
public class LazySegmentTreeDifferentialTests
{
    private enum Op { Apply, IndexerSet, Query, Clear }

    // Range updates dominate; a clear is rare enough that long interleavings survive to be interesting.
    private static readonly Gen<Op> GenKind =
        Gen.Int[0, 99].Select(n => n < 45 ? Op.Apply
                                 : n < 60 ? Op.IndexerSet
                                 : n < 98 ? Op.Query
                                 : Op.Clear);

    private static readonly Gen<(Op Kind, int First, int Second, int Value)> GenOp =
        Gen.Select(GenKind, Gen.Int[0, 63], Gen.Int[0, 63], Gen.Int[-100, 100]);

    private static readonly Gen<(int[] Initial, List<(Op Kind, int First, int Second, int Value)> Ops)> GenScript =
        Gen.Select(Gen.Int[-50, 50].Array[0, 64], GenOp.List[0, 300]);

    [Fact]
    public void RangeAddRangeMin_ShouldMatch_ANaiveArray()
    {
        GenScript.Sample(script =>
        {
            Run(
                new LazySegmentTree<int, int, MinMonoid<int>, MinMaxAddAction<int>>(script.Initial),
                script.Initial, script.Ops,
                toValue: v => v,
                toUpdate: v => v,
                applyToElement: (u, x) => x + u,
                identity: int.MaxValue,
                combine: Math.Min,
                // A cleared tree holds int.MaxValue, and adding to it overflows — outside MinMaxAddAction's domain.
                // Re-seed every element after a clear so the script stays where the laws hold.
                reseedAfterClear: 0);
        }, iter: 60);
    }

    [Fact]
    public void RangeAddRangeSum_ShouldMatch_ANaiveArray()
    {
        GenScript.Sample(script =>
        {
            long[] initial = Array.ConvertAll(script.Initial, v => (long)v);
            Run(
                new LazySegmentTree<long, long, SumMonoid<long>, SumAddAction<long>>(initial),
                initial, script.Ops,
                toValue: v => v,
                toUpdate: v => v,
                applyToElement: (u, x) => x + u,
                identity: 0L,
                combine: (a, b) => a + b);
        }, iter: 60);
    }

    [Fact]
    public void RangeAssignRangeSum_ShouldMatch_ANaiveArray()
    {
        GenScript.Sample(script =>
        {
            long[] initial = Array.ConvertAll(script.Initial, v => (long)v);
            Run(
                new LazySegmentTree<long, long?, SumMonoid<long>, SumAssignAction<long>>(initial),
                initial, script.Ops,
                toValue: v => v,
                // Every fifth update is the identity (null), which must leave its range untouched.
                toUpdate: v => v % 5 == 0 ? null : v,
                applyToElement: (u, x) => u ?? x,
                identity: 0L,
                combine: (a, b) => a + b);
        }, iter: 60);
    }

    [Fact]
    public void RangeAssignRangeMax_ShouldMatch_ANaiveArray()
    {
        GenScript.Sample(script =>
        {
            Run(
                new LazySegmentTree<int, int?, MaxMonoid<int>, IdempotentAssignAction<int>>(script.Initial),
                script.Initial, script.Ops,
                toValue: v => v,
                toUpdate: v => v % 5 == 0 ? null : v,
                applyToElement: (u, x) => u ?? x,
                identity: int.MinValue,
                combine: Math.Max);
        }, iter: 60);
    }

    [Fact]
    public void AffineUpdates_ShouldComposeInApplicationOrder()
    {
        // Small multipliers keep the products far from long's range across a 300-step script.
        GenScript.Sample(script =>
        {
            long[] initial = Array.ConvertAll(script.Initial, v => (long)v);
            Run(
                new LazySegmentTree<long, (long Mul, long Add), SumMonoid<long>, AffineSumAction>(initial),
                initial, script.Ops,
                toValue: v => v,
                toUpdate: v => (v % 3 == 0 ? -1 : v % 3 == 1 ? 1 : 2, v),
                applyToElement: (u, x) => x * u.Mul + u.Add,
                identity: 0L,
                combine: (a, b) => a + b,
                // Re-anchor values with a point write often enough that repeated ×2 cannot overflow.
                bound: x => Math.Abs(x) > 1L << 40);
        }, iter: 60);
    }

    /// <summary>
    /// Every length from 1 to 17 — spanning the power-of-two boundaries where padding appears — crossed with every
    /// non-empty update range, each followed by every query range, under a non-commutative fold whose update needs
    /// the node's element count. A padding leaf reached by an update, a mis-ordered refold, or a wrong per-node
    /// count shows up here.
    /// </summary>
    [Fact]
    public void Query_ShouldMatchAnOrderedScan_AfterEveryRangeAssign_UnderANonCommutativeFold()
    {
        for (int n = 1; n <= 17; n++)
        {
            for (int a = 0; a < n; a++)
            {
                for (int b = a + 1; b <= n; b++)
                {
                    var values = new string[n];
                    for (int i = 0; i < n; i++)
                        values[i] = ((char)('a' + i)).ToString();

                    var tree = new LazySegmentTree<string, string?, ConcatMonoid, RepeatAssignAction>(values);
                    tree.Apply(a, b, "Z");
                    for (int i = a; i < b; i++)
                        values[i] = "Z";

                    Assert.Equal(string.Concat(values), tree.Aggregate);
                    for (int start = 0; start <= n; start++)
                        for (int end = start; end <= n; end++)
                            Assert.Equal(string.Concat(values[start..end]), tree.Query(start, end));
                }
            }
        }
    }

    /// <summary>
    /// Two overlapping range updates whose order matters, then every query range: the second update lands partly
    /// on nodes the first left pending, so it pins both the push before the second update and the composition order
    /// on the nodes they share.
    /// </summary>
    [Fact]
    public void Query_ShouldMatch_AfterTwoOverlappingNonCommutativeUpdates()
    {
        Gen.Select(Gen.Int[1, 20], Gen.Int[0, 19], Gen.Int[0, 20], Gen.Int[0, 19], Gen.Int[0, 20])
            .Sample(t =>
            {
                var (n, a1, b1, a2, b2) = t;
                (a1, b1) = Order(a1 % n, b1 % (n + 1));
                (a2, b2) = Order(a2 % n, b2 % (n + 1));

                long[] model = Enumerable.Range(1, n).Select(i => (long)i).ToArray();
                var tree = new LazySegmentTree<long, (long Mul, long Add), SumMonoid<long>, AffineSumAction>(model);

                tree.Apply(a1, b1, (2, 1));
                for (int i = a1; i < b1; i++) model[i] = model[i] * 2 + 1;
                tree.Apply(a2, b2, (3, -2));
                for (int i = a2; i < b2; i++) model[i] = model[i] * 3 - 2;

                for (int start = 0; start <= n; start++)
                    for (int end = start; end <= n; end++)
                        Assert.Equal(model[start..end].Sum(), tree.Query(start, end));

                Assert.Equal(model, tree.ToArray());
            }, iter: 200);
    }

    private static (int, int) Order(int x, int y) => x <= y ? (x, y) : (y, x);

    private static void Run<T, TUpdate, TMonoid, TAction>(
        LazySegmentTree<T, TUpdate, TMonoid, TAction> tree,
        T[] initial,
        List<(Op Kind, int First, int Second, int Value)> ops,
        Func<int, T> toValue,
        Func<int, TUpdate> toUpdate,
        Func<TUpdate, T, T> applyToElement,
        T identity,
        Func<T, T, T> combine,
        Func<T, bool>? bound = null,
        T? reseedAfterClear = null)
        where T : struct
        where TMonoid : struct, IMonoid<T>
        where TAction : struct, IRangeAction<T, TUpdate>
    {
        var model = (T[])initial.Clone();
        int n = model.Length;
        AssertRoot(tree, model, identity, combine);

        foreach (var (kind, first, second, value) in ops)
        {
            // A half-open [a, b) over [0, n], so empty, single-element and full-width ranges are all reachable.
            int a = first % (n + 1);
            int b = second % (n + 1);
            if (a > b)
                (a, b) = (b, a);

            switch (kind)
            {
                case Op.Apply:
                {
                    TUpdate update = toUpdate(value);
                    tree.Apply(a, b, update);
                    for (int i = a; i < b; i++)
                        model[i] = applyToElement(update, model[i]);

                    if (bound is not null)
                    {
                        for (int i = a; i < b; i++)
                        {
                            if (bound(model[i]))
                            {
                                tree[i] = toValue(value);
                                model[i] = toValue(value);
                            }
                        }
                    }

                    break;
                }

                case Op.IndexerSet:
                    if (n == 0)
                        continue;

                    tree[first % n] = toValue(value);
                    model[first % n] = toValue(value);
                    break;

                case Op.Query:
                    Assert.Equal(Fold(model, a, b, identity, combine), tree.Query(a, b));
                    continue; // a query changes nothing, so the full reconciliation is redundant

                case Op.Clear:
                    tree.Clear();
                    Array.Fill(model, identity);
                    if (reseedAfterClear is T reseed)
                    {
                        AssertRoot(tree, model, identity, combine);
                        for (int i = 0; i < n; i++)
                            tree[i] = model[i] = reseed;
                    }

                    break;
            }

            // Only the root is checked per step: reading every element would push every pending update and flush
            // the very state a missed push hides in. Queries above read through it; the full check comes last.
            AssertRoot(tree, model, identity, combine);
        }

        // The indexer pushes one path at a time; enumeration flushes everything at once. Check both, reading the
        // indexer first so a stale pending update cannot be hidden by the flush.
        for (int i = 0; i < model.Length; i++)
            Assert.Equal(model[i], tree[i]);

        Assert.Equal(model, tree.ToArray());
    }

    private static void AssertRoot<T, TUpdate, TMonoid, TAction>(
        LazySegmentTree<T, TUpdate, TMonoid, TAction> tree, T[] model, T identity, Func<T, T, T> combine)
        where TMonoid : struct, IMonoid<T>
        where TAction : struct, IRangeAction<T, TUpdate>
    {
        Assert.Equal(model.Length, tree.Count);
        Assert.Equal(Fold(model, 0, model.Length, identity, combine), tree.Aggregate);
    }

    private static T Fold<T>(T[] model, int start, int endExclusive, T identity, Func<T, T, T> combine)
    {
        T result = identity;
        for (int i = start; i < endExclusive; i++)
            result = combine(result, model[i]);

        return result;
    }
}
