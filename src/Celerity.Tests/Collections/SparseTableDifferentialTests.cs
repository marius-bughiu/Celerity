using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Randomized and exhaustive reconciliation of <see cref="SparseTable{T, TMonoid}"/> against a naive
/// left-to-right scan over an array holding the same values, and against
/// <see cref="SegmentTree{T, TMonoid}"/>, which answers the same question by a different route.
///
/// <para>
/// The <b>overlap</b> is what is on trial here. A sparse table covers <c>[start, endExclusive)</c> with the two
/// widest power-of-two windows that fit — one anchored at each end — and those windows share
/// <c>2 * 2^k - length</c> elements. That intersection is never empty, since <c>2^k &lt;= length &lt; 2^(k+1)</c>:
/// at an exact power of two the two windows are the <i>same</i> window and the whole range is folded twice,
/// and every other length gives a partial overlap. Every element in the intersection is folded into the
/// answer twice, and the type's correctness rests entirely on the claim that idempotence makes the repetition
/// invisible. The sweeps below query <i>every</i> range of the sequence, so both the full-overlap case and
/// every partial width — down to the one-element query, which combines a value with itself — are covered.
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
///
/// <para>
/// Three layers, narrowest first. The <b>CsCheck property</b> generates the sequence from its own two axes —
/// length and value spread — so a disagreement shrinks to a minimal reproduction with the seed printed; it is
/// the layer that runs on every pull request and the one <c>CONTRIBUTING.md</c> requires. The seeded theories
/// below it drive the same reconciliation from fixed <see cref="Random"/> streams, and the length sweep at the
/// end walks every length from 1 to 9 and around each power of two up to 64 deterministically, rather than
/// hoping a sample lands on the boundaries where the row count changes.
/// </para>
///
/// <para>
/// The <b>value spread</b> is a real axis rather than decoration. A wide spread separates the values, so a
/// query that read the wrong window almost always returns a visibly wrong answer; a narrow one fills the
/// sequence with duplicates, which is where the overlap is folding the <i>same</i> value in twice and where a
/// fold that quietly double-counted would most often agree with the oracle by luck. Both ends are generated.
/// </para>
/// </summary>
public class SparseTableDifferentialTests
{
    // Length and value spread are the two axes that decide what a case can reach: the length fixes the row
    // count and every overlap width the queries can produce, and the spread fixes how often the overlap is
    // folding a duplicate rather than a distinct value. Zero length is included — an empty table has no rows
    // and every query on it is the empty range.
    private static readonly Gen<(int Length, int Spread, uint Seed)> GenSequences =
        Gen.Select(Gen.Int[0, 80], Gen.Int[1, 60], Gen.UInt);

    [Fact]
    public void EveryRange_ShouldMatchTheNaiveScan_UnderGeneratedSequences()
    {
        GenSequences.Sample(
            spec => AssertAgreesWithOracles(BuildSequence(spec.Length, spec.Spread, spec.Seed)),
            iter: 250);
    }

    // The generated sequence, and the reconciliation the property and the sweeps below all share. Three folds
    // at once, because they are on trial for different reasons: MinMonoid is the commutative headline case and
    // is also cross-checked against SegmentTree, which reaches the same answer by a walk that never overlaps;
    // FirstNonZeroMonoid is idempotent *and* non-commutative, so it is the only one that can observe the order
    // the two windows are combined in; and the shape properties are checked once per sequence.
    private static int[] BuildSequence(int length, int spread, uint seed)
    {
        var rand = new Random(unchecked((int)seed));
        var values = new int[length];
        for (int i = 0; i < length; i++)
            values[i] = rand.Next(0, spread);

        return values;
    }

    private static void AssertAgreesWithOracles(int[] values)
    {
        int n = values.Length;

        var minTable = new SparseTable<int, MinMonoid<int>>(values);
        var minTree = new SegmentTree<int, MinMonoid<int>>(values);
        var firstTable = new SparseTable<int, FirstNonZeroMonoid>(values);

        Assert.Equal(n, minTable.Count);
        Assert.Equal(n == 0 ? 0 : (int)Math.Floor(Math.Log2(n)) + 1, minTable.LevelCount);
        Assert.Equal((long)minTable.LevelCount * n * sizeof(int), minTable.IndexSizeInBytes);
        Assert.Equal(values, minTable);
        Assert.Equal(minTree.Aggregate, minTable.Aggregate);

        for (int i = 0; i < n; i++)
            Assert.Equal(values[i], minTable[i]);

        for (int start = 0; start <= n; start++)
        {
            for (int end = start; end <= n; end++)
            {
                int min = int.MaxValue;
                int first = 0;
                for (int i = start; i < end; i++)
                {
                    min = Math.Min(min, values[i]);
                    if (first == 0)
                        first = values[i];
                }

                Assert.Equal(min, minTable.Query(start, end));
                Assert.Equal(minTree.Query(start, end), minTable.Query(start, end));
                Assert.Equal(first, firstTable.Query(start, end));
            }
        }
    }

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
