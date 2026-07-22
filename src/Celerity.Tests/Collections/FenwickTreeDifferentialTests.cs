using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Deterministic, seeded differential coverage for <see cref="FenwickTree{T}"/>. Each seed drives the same
/// random stream of point updates, indexer assignments, and clears into the Fenwick tree and into a naive
/// <c>long[]</c> reference model, then asserts after every operation that they agree on every logical value,
/// on <see cref="FenwickTree{T}.PrefixSum(int)"/> at every boundary, on a batch of random
/// <see cref="FenwickTree{T}.RangeSum(int, int)"/> queries, and on <see cref="FenwickTree{T}.Total"/>. This
/// is the strongest guard against a low-bit / lowest-set-bit-walk error that only surfaces after many
/// interleaved updates at specific index shapes.
/// </summary>
public class FenwickTreeDifferentialTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(2026)]
    public void FenwickTree_ShouldMatchNaiveArray_UnderRandomOperations(int seed)
    {
        var rand = new Random(seed);
        int n = rand.Next(1, 64);

        // Seed both models from the same random initial values (exercises the O(n) span build).
        var initial = new long[n];
        for (int i = 0; i < n; i++)
            initial[i] = rand.Next(-50, 50);

        var tree = new FenwickTree<long>(initial);
        var model = (long[])initial.Clone();
        AssertConsistent(tree, model, rand);

        for (int step = 0; step < 2000; step++)
        {
            int op = rand.Next(0, 10);
            if (op == 0)
            {
                // Clear: reset both to zero.
                tree.Clear();
                Array.Clear(model, 0, model.Length);
            }
            else if (op <= 5)
            {
                // Point add.
                int idx = rand.Next(0, n);
                long delta = rand.Next(-100, 100);
                tree.Add(idx, delta);
                model[idx] += delta;
            }
            else
            {
                // Indexer set.
                int idx = rand.Next(0, n);
                long value = rand.Next(-100, 100);
                tree[idx] = value;
                model[idx] = value;
            }

            AssertConsistent(tree, model, rand);
        }
    }

    private static void AssertConsistent(FenwickTree<long> tree, long[] model, Random rand)
    {
        Assert.Equal(model.Length, tree.Count);

        // Every logical value matches (indexer get and enumeration).
        var enumerated = tree.ToArray();
        long runningPrefix = 0;
        for (int i = 0; i < model.Length; i++)
        {
            Assert.Equal(model[i], tree[i]);
            Assert.Equal(model[i], enumerated[i]);

            // PrefixSum at every boundary [0, i].
            Assert.Equal(runningPrefix, tree.PrefixSum(i));
            runningPrefix += model[i];
        }

        Assert.Equal(runningPrefix, tree.PrefixSum(model.Length));
        Assert.Equal(runningPrefix, tree.Total);

        // A batch of random half-open range queries.
        for (int q = 0; q < 8; q++)
        {
            int a = rand.Next(0, model.Length + 1);
            int b = rand.Next(0, model.Length + 1);
            if (a > b)
                (a, b) = (b, a);

            long expected = 0;
            for (int i = a; i < b; i++)
                expected += model[i];

            Assert.Equal(expected, tree.RangeSum(a, b));
        }
    }
}
