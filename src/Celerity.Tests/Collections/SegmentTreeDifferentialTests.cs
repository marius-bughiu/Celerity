using Celerity.Collections;

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
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(2026)]
    public void SegmentTree_ShouldMatchNaiveScan_UnderRandomOperations(int seed)
    {
        var rand = new Random(seed);
        int n = rand.Next(1, 64);

        // The int[] goes in through the IEnumerable<T> constructor's counted (ICollection<T>) fast path, so
        // this also exercises the O(n) linear-time build rather than a sequence of point inserts.
        var initial = new int[n];
        for (int i = 0; i < n; i++)
            initial[i] = rand.Next(-50, 50);

        var tree = new SegmentTree<int, MinMonoid<int>>(initial);
        var model = (int[])initial.Clone();
        AssertConsistent(tree, model, rand);

        for (int step = 0; step < 1000; step++)
        {
            int op = rand.Next(0, 10);
            if (op == 0)
            {
                tree.Clear();
                Array.Fill(model, int.MaxValue);
            }
            else if (op <= 5)
            {
                int idx = rand.Next(0, n);
                int value = rand.Next(-100, 100);
                tree[idx] = value;
                model[idx] = value;
            }
            else
            {
                int idx = rand.Next(0, n);
                int value = rand.Next(-100, 100);
                tree.Combine(idx, value);
                model[idx] = Math.Min(model[idx], value);
            }

            AssertConsistent(tree, model, rand);
        }
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
    [Theory]
    [InlineData(11)]
    [InlineData(97)]
    public void Query_ShouldMatchAnOrderedScan_AfterPointUpdates_UnderANonCommutativeMonoid(int seed)
    {
        var rand = new Random(seed);

        for (int n = 1; n <= 20; n++)
        {
            var values = new string[n];
            for (int i = 0; i < n; i++)
                values[i] = i.ToString();

            var tree = new SegmentTree<string, ConcatMonoid>(values);

            for (int step = 0; step < 20; step++)
            {
                int idx = rand.Next(0, n);
                string replacement = "<" + rand.Next(0, 1000) + ">";
                tree[idx] = replacement;
                values[idx] = replacement;

                for (int start = 0; start <= n; start++)
                    for (int end = start; end <= n; end++)
                        Assert.Equal(string.Concat(values[start..end]), tree.Query(start, end));
            }
        }
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

    private static void AssertConsistent(SegmentTree<int, MinMonoid<int>> tree, int[] model, Random rand)
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

        // A batch of random half-open range queries, plus both degenerate ends.
        Assert.Equal(int.MaxValue, tree.Query(0, 0));
        Assert.Equal(int.MaxValue, tree.Query(model.Length, model.Length));

        for (int q = 0; q < 8; q++)
        {
            int a = rand.Next(0, model.Length + 1);
            int b = rand.Next(0, model.Length + 1);
            if (a > b)
                (a, b) = (b, a);

            Assert.Equal(Fold(model, a, b), tree.Query(a, b));
        }
    }

    private static int Fold(int[] model, int start, int endExclusive)
    {
        int result = int.MaxValue;
        for (int i = start; i < endExclusive; i++)
            result = Math.Min(result, model[i]);

        return result;
    }
}
