using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Randomized and exhaustive reconciliation of <see cref="SegmentTree{T, TMonoid}"/> against a naive
/// left-to-right scan over an array holding the same values.
///
/// <para>
/// The layout is what is on trial here. The tree stores exactly <c>2n</c> cells, with the logical elements as
/// the leaf half and each internal node the fold of its two children. At a length that is not a power of two
/// the leaves sit in a rotated order, so an internal node can span a wrapped, non-contiguous range — which is
/// why the usual advice is to pad the leaf count up to a power of two and pay up to <c>4n</c>. The claim this
/// suite exists to test is that the rotation never reaches the answer, because the query walks outward from
/// both ends and keeps the two directions in separate accumulators.
/// </para>
///
/// <para>
/// A commutative fold cannot observe the difference: min, max and sum give the same answer however the
/// operands are bracketed or reordered, so a suite built only on those would pass against a broken layout.
/// Every order-sensitive test below therefore runs on <see cref="ConcatMonoid"/> or
/// <see cref="FirstNonZeroMonoid"/>, where a single mis-ordered combine changes the result.
/// </para>
/// </summary>
public class SegmentTreeDifferentialTests
{
    private enum Op { IndexerSet, Combine, Query, Clear }

    // Updates dominate; a clear is rare enough that long interleavings survive to be interesting.
    private static readonly Gen<Op> GenKind =
        Gen.Int[0, 99].Select(n => n < 40 ? Op.IndexerSet
                                 : n < 75 ? Op.Combine
                                 : n < 98 ? Op.Query
                                 : Op.Clear);

    // Two index draws: point operations use the first, range queries use both as an unordered pair.
    private static readonly Gen<(Op Kind, int First, int Second, int Value)> GenOp =
        Gen.Select(GenKind, Gen.Int[0, 63], Gen.Int[0, 63], Gen.Int[-100, 100]);

    private static readonly Gen<(int[] Initial, List<(Op Kind, int First, int Second, int Value)> Ops)> GenScript =
        Gen.Select(Gen.Int[-50, 50].Array[1, 64], GenOp.List[0, 300]);

    [Fact]
    public void SegmentTree_ShouldMatch_ANaiveScan()
    {
        GenScript.Sample(script =>
        {
            int[] initial = script.Initial;
            int n = initial.Length;

            // The int[] goes in through the IEnumerable<T> constructor's counted (ICollection<T>) fast
            // path, so every case also exercises the O(n) build rather than a sequence of point inserts.
            var tree = new SegmentTree<int, MinMonoid<int>>(initial);
            var model = (int[])initial.Clone();
            AssertConsistent(tree, model);

            foreach (var (kind, first, second, value) in script.Ops)
            {
                int index = first % n;

                switch (kind)
                {
                    case Op.IndexerSet:
                        tree[index] = value;
                        model[index] = value;
                        break;

                    case Op.Combine:
                        tree.Combine(index, value);
                        model[index] = Math.Min(model[index], value);
                        break;

                    case Op.Query:
                    {
                        // A half-open [a, b), so both empty (a == b) and full-width queries are reachable.
                        int a = first % (n + 1);
                        int b = second % (n + 1);
                        if (a > b)
                            (a, b) = (b, a);

                        Assert.Equal(Fold(model, a, b), tree.Query(a, b));
                        continue; // a query changes nothing, so the full reconciliation is redundant
                    }

                    case Op.Clear:
                        tree.Clear();
                        Array.Fill(model, int.MaxValue);
                        break;
                }

                AssertConsistent(tree, model);
            }
        }, iter: 40);
    }

    /// <summary>
    /// Every length from 1 to 33 — spanning three power-of-two boundaries, where the <c>2n</c> layout's leaf
    /// rotation is at its most awkward — crossed with every half-open range, folded by a non-commutative
    /// monoid. This is the exhaustive proof that no wrapped internal node reaches the answer on the wrong side.
    /// </summary>
    [Fact]
    public void Query_ShouldMatchAnOrderedScan_ForEveryLengthAndRange_UnderANonCommutativeMonoid()
    {
        for (int n = 1; n <= 33; n++)
        {
            var values = new string[n];
            for (int i = 0; i < n; i++)
                values[i] = ((char)('a' + (i % 26))).ToString() + i;

            var tree = new SegmentTree<string, ConcatMonoid>(values);

            for (int start = 0; start <= n; start++)
            {
                for (int end = start; end <= n; end++)
                {
                    string expected = string.Concat(values[start..end]);
                    Assert.Equal(expected, tree.Query(start, end));
                }
            }

            Assert.Equal(string.Concat(values), tree.Aggregate);
        }
    }

    /// <summary>
    /// The same exhaustive sweep, but after point updates have refolded arbitrary paths to the root — a
    /// correct build with a wrongly ordered ancestor refold would pass the test above and fail this one.
    /// </summary>
    [Fact]
    public void Query_ShouldMatchAnOrderedScan_AfterPointUpdates_UnderANonCommutativeMonoid()
    {
        // The length and the update script are generated together, and the range sweep stays exhaustive:
        // shrinking a failure to the shortest length and the fewest updates is exactly the reduction that
        // makes a mis-ordered ancestor refold readable.
        Gen.Select(Gen.Int[1, 20], Gen.Select(Gen.Int[0, 19], Gen.Int[0, 999]).List[0, 20])
            .Sample(script =>
            {
                var (n, updates) = script;
                var values = new string[n];
                for (int i = 0; i < n; i++)
                    values[i] = i.ToString();

                var tree = new SegmentTree<string, ConcatMonoid>(values);

                foreach (var (rawIndex, token) in updates)
                {
                    int idx = rawIndex % n;
                    string replacement = "<" + token + ">";
                    tree[idx] = replacement;
                    values[idx] = replacement;

                    for (int start = 0; start <= n; start++)
                        for (int end = start; end <= n; end++)
                            Assert.Equal(string.Concat(values[start..end]), tree.Query(start, end));
                }
            }, iter: 40);
    }

    /// <summary>
    /// A value-typed non-commutative fold, so the ordering guarantee is pinned for a tree the JIT specializes
    /// without any reference-type indirection.
    /// </summary>
    [Fact]
    public void Query_ShouldMatchAnOrderedScan_ForAValueTypedNonCommutativeMonoid()
    {
        for (int n = 1; n <= 17; n++)
        {
            var values = new int[n];
            for (int i = 0; i < n; i++)
                values[i] = i % 3 == 0 ? 0 : i + 1;   // zeroes are the identity, so they must be skipped over

            var tree = new SegmentTree<int, FirstNonZeroMonoid>(values);

            for (int start = 0; start <= n; start++)
            {
                for (int end = start; end <= n; end++)
                {
                    int expected = 0;
                    for (int i = start; i < end; i++)
                    {
                        if (values[i] != 0)
                        {
                            expected = values[i];
                            break;
                        }
                    }

                    Assert.Equal(expected, tree.Query(start, end));
                }
            }
        }
    }

    private static void AssertConsistent(SegmentTree<int, MinMonoid<int>> tree, int[] model)
    {
        Assert.Equal(model.Length, tree.Count);

        // Every logical value matches (indexer get and enumeration).
        int[] enumerated = tree.ToArray();
        for (int i = 0; i < model.Length; i++)
        {
            Assert.Equal(model[i], tree[i]);
            Assert.Equal(model[i], enumerated[i]);
        }

        Assert.Equal(Fold(model, 0, model.Length), tree.Aggregate);

        // Both degenerate ends. Every other range is a generated Op.Query, so the bounds that diverge
        // shrink alongside the updates that produced them.
        Assert.Equal(int.MaxValue, tree.Query(0, 0));
        Assert.Equal(int.MaxValue, tree.Query(model.Length, model.Length));
    }

    private static int Fold(int[] model, int start, int endExclusive)
    {
        int result = int.MaxValue;
        for (int i = start; i < endExclusive; i++)
            result = Math.Min(result, model[i]);

        return result;
    }
}
