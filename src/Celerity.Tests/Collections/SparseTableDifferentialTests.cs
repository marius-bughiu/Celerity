using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Randomized and exhaustive reconciliation of <see cref="SparseTable{T, TMonoid}"/> against a naive
/// left-to-right scan over an array holding the same values, and against
/// <see cref="SegmentTree{T, TMonoid}"/>, which answers the same question by a different route.
///
/// <para>
/// The <b>overlap</b> is what is on trial here. A sparse table covers <c>[start, endExclusive)</c> with the two
/// widest power-of-two windows that fit — one anchored at each end — and those windows share
/// <c>2 * 2^k - length</c> elements whenever the length is not itself a power of two. Every element in that
/// intersection is folded into the answer twice, and the type's correctness rests entirely on the claim that
/// idempotence makes the repetition invisible. A suite that only queried power-of-two lengths would never
/// exercise it, so the sweeps below query <i>every</i> range of the sequence.
/// </para>
///
/// <para>
/// A commutative fold cannot observe the <i>order</i> the two windows are combined in: min, max and gcd give
/// the same answer either way, so a suite built only on those would pass against a table that combined right
/// before left. The order-sensitive tests therefore run on <see cref="FirstNonZeroMonoid"/> and
/// <see cref="FirstNonEmptyMonoid"/>, where a single swapped combine changes the result — the same argument
/// <see cref="SegmentTreeDifferentialTests"/> makes, with the difference that the folds available here must
/// also be idempotent, which rules <see cref="ConcatMonoid"/> out.
/// </para>
/// </summary>
public class SparseTableDifferentialTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(123)]
    [InlineData(2026)]
    public void SparseTable_ShouldMatchNaiveScan_OverEveryRangeOfARandomSequence(int seed)
    {
        var rand = new Random(seed);
        int n = rand.Next(1, 96);

        var values = new int[n];
        for (int i = 0; i < n; i++)
            values[i] = rand.Next(-50, 50);

        var table = new SparseTable<int, MinMonoid<int>>(values);

        Assert.Equal(n, table.Count);
        for (int start = 0; start <= n; start++)
        {
            for (int end = start; end <= n; end++)
            {
                int expected = int.MaxValue;
                for (int i = start; i < end; i++)
                    expected = Math.Min(expected, values[i]);

                Assert.Equal(expected, table.Query(start, end));
            }
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(19)]
    [InlineData(2718)]
    public void SparseTable_ShouldMatchSegmentTree_OverEveryRangeOfARandomSequence(int seed)
    {
        var rand = new Random(seed);
        int n = rand.Next(1, 96);

        var values = new int[n];
        for (int i = 0; i < n; i++)
            values[i] = rand.Next(-1000, 1000);

        var table = new SparseTable<int, MaxMonoid<int>>(values);
        var tree = new SegmentTree<int, MaxMonoid<int>>(values);

        Assert.Equal(tree.Aggregate, table.Aggregate);
        for (int start = 0; start <= n; start++)
            for (int end = start; end <= n; end++)
                Assert.Equal(tree.Query(start, end), table.Query(start, end));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(97)]
    [InlineData(4242)]
    public void SparseTable_ShouldPreserveIndexOrder_UnderANonCommutativeFold(int seed)
    {
        var rand = new Random(seed);
        int n = rand.Next(1, 96);

        // Mostly zeros, so a range usually has to look past its first few elements to find the winner and the
        // two windows disagree often.
        var values = new int[n];
        for (int i = 0; i < n; i++)
            values[i] = rand.Next(0, 4) == 0 ? rand.Next(1, 100) : 0;

        var table = new SparseTable<int, FirstNonZeroMonoid>(values);

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

                Assert.Equal(expected, table.Query(start, end));
            }
        }
    }

    [Theory]
    [InlineData(11)]
    [InlineData(1729)]
    public void SparseTable_ShouldPreserveIndexOrder_OverAReferenceTypeElement(int seed)
    {
        var rand = new Random(seed);
        int n = rand.Next(1, 64);

        var values = new string[n];
        for (int i = 0; i < n; i++)
            values[i] = rand.Next(0, 4) == 0 ? $"v{i}" : string.Empty;

        var table = new SparseTable<string, FirstNonEmptyMonoid>(values);

        for (int start = 0; start <= n; start++)
        {
            for (int end = start; end <= n; end++)
            {
                string expected = string.Empty;
                for (int i = start; i < end; i++)
                {
                    if (values[i].Length != 0)
                    {
                        expected = values[i];
                        break;
                    }
                }

                Assert.Equal(expected, table.Query(start, end));
            }
        }
    }

    [Theory]
    [InlineData(13)]
    [InlineData(311)]
    public void SparseTable_ShouldMatchNaiveScan_UnderAUserWrittenFold(int seed)
    {
        var rand = new Random(seed);
        int n = rand.Next(1, 64);

        var values = new uint[n];
        for (int i = 0; i < n; i++)
            values[i] = (uint)rand.Next(1, 500) * (uint)rand.Next(1, 6);

        var table = new SparseTable<uint, GcdMonoid>(values);
        var monoid = default(GcdMonoid);

        for (int start = 0; start <= n; start++)
        {
            for (int end = start; end <= n; end++)
            {
                uint expected = monoid.Identity;
                for (int i = start; i < end; i++)
                    expected = monoid.Combine(expected, values[i]);

                Assert.Equal(expected, table.Query(start, end));
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(64)]
    public void SparseTable_ShouldMatchNaiveScan_AtEveryLengthAroundAPowerOfTwo(int n)
    {
        // The row count changes at each power of two, and so does which window width a query lands on. This
        // walks the boundaries deterministically rather than hoping a seed lands on them.
        var values = new int[n];
        for (int i = 0; i < n; i++)
            values[i] = (i * 37 % 101) - 50;

        var table = new SparseTable<int, MinMonoid<int>>(values);

        for (int start = 0; start <= n; start++)
        {
            for (int end = start; end <= n; end++)
            {
                int expected = int.MaxValue;
                for (int i = start; i < end; i++)
                    expected = Math.Min(expected, values[i]);

                Assert.Equal(expected, table.Query(start, end));
            }
        }
    }

    [Theory]
    [InlineData(23)]
    [InlineData(877)]
    public void SparseTable_ShouldMatchNaiveScan_UnderAStatefulFold(int seed)
    {
        var rand = new Random(seed);
        int n = rand.Next(1, 64);
        const int Floor = -1000;

        var values = new int[n];
        for (int i = 0; i < n; i++)
            values[i] = rand.Next(Floor, 1000);

        var table = new SparseTable<int, FlooredMaxMonoid>(values, new FlooredMaxMonoid(Floor));

        for (int start = 0; start <= n; start++)
        {
            for (int end = start; end <= n; end++)
            {
                int expected = Floor;
                for (int i = start; i < end; i++)
                    expected = Math.Max(expected, values[i]);

                Assert.Equal(expected, table.Query(start, end));
            }
        }
    }
}
