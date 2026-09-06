using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="FenwickTree{T}"/> against a naive
/// <c>long[]</c> reference model. CsCheck generates the initial sequence and the operation script
/// together, then asserts after <b>every</b> operation that the two agree on every logical value,
/// on <see cref="FenwickTree{T}.PrefixSum(int)"/> at every boundary, and on
/// <see cref="FenwickTree{T}.Total"/>. This is the strongest guard against a lowest-set-bit-walk
/// error that only surfaces after many interleaved updates at specific index shapes.
///
/// <para>
/// Range queries are part of the generated script rather than a random batch fired after each
/// step, which is what makes a failure legible: the bounds that diverged shrink along with the
/// updates that produced them, so a counterexample arrives as a handful of operations and one
/// query instead of a seed and a two-thousand-step trace.
/// </para>
///
/// <para>
/// The initial sequence goes in through the <see cref="IEnumerable{T}"/> constructor's counted
/// (<see cref="ICollection{T}"/>) fast path, so every case also exercises the linear-time build
/// rather than a sequence of point inserts.
/// </para>
/// </summary>
public class FenwickTreeDifferentialTests
{
    private enum Op { Add, IndexerSet, RangeSum, Clear }

    // Updates dominate; a clear is rare enough that long interleavings survive to be interesting.
    private static readonly Gen<Op> GenKind =
        Gen.Int[0, 99].Select(n => n < 45 ? Op.Add
                                 : n < 75 ? Op.IndexerSet
                                 : n < 98 ? Op.RangeSum
                                 : Op.Clear);

    // Two index draws: point operations use the first, range queries use both as an unordered
    // pair. Both are taken modulo the generated length, so the tree's size shrinks independently
    // of the positions touched inside it.
    private static readonly Gen<(Op Kind, int First, int Second, int Value)> GenOp =
        Gen.Select(GenKind, Gen.Int[0, 63], Gen.Int[0, 63], Gen.Int[-100, 100]);

    private static readonly Gen<(long[] Initial, List<(Op Kind, int First, int Second, int Value)> Ops)> GenScript =
        Gen.Select(Gen.Long[-50, 50].Array[1, 64], GenOp.List[0, 300]);

    [Fact]
    public void FenwickTree_ShouldMatch_ANaiveArray()
    {
        GenScript.Sample(script =>
        {
            long[] initial = script.Initial;
            int n = initial.Length;

            var tree = new FenwickTree<long>(initial);
            var model = (long[])initial.Clone();
            AssertConsistent(tree, model);

            foreach (var (kind, first, second, value) in script.Ops)
            {
                int index = first % n;

                switch (kind)
                {
                    case Op.Add:
                        tree.Add(index, value);
                        model[index] += value;
                        break;

                    case Op.IndexerSet:
                        tree[index] = value;
                        model[index] = value;
                        break;

                    case Op.RangeSum:
                    {
                        // A half-open [a, b) over the inclusive index range, so both empty
                        // (a == b) and full-width queries are reachable.
                        int a = first % (n + 1);
                        int b = second % (n + 1);
                        if (a > b)
                            (a, b) = (b, a);

                        long expected = 0;
                        for (int i = a; i < b; i++)
                            expected += model[i];

                        Assert.Equal(expected, tree.RangeSum(a, b));
                        continue; // a query changes nothing, so the full reconciliation is redundant
                    }

                    case Op.Clear:
                        tree.Clear();
                        Array.Clear(model, 0, model.Length);
                        break;
                }

                AssertConsistent(tree, model);
            }
        }, iter: 40);
    }

    private static void AssertConsistent(FenwickTree<long> tree, long[] model)
    {
        Assert.Equal(model.Length, tree.Count);

        // Every logical value matches, through both the indexer and enumeration.
        var enumerated = tree.ToArray();
        long runningPrefix = 0;
        for (int i = 0; i < model.Length; i++)
        {
            Assert.Equal(model[i], tree[i]);
            Assert.Equal(model[i], enumerated[i]);

            // PrefixSum at every boundary [0, i).
            Assert.Equal(runningPrefix, tree.PrefixSum(i));
            runningPrefix += model[i];
        }

        Assert.Equal(runningPrefix, tree.PrefixSum(model.Length));
        Assert.Equal(runningPrefix, tree.Total);
    }
}
