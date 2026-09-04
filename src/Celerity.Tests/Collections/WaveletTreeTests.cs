using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural coverage for <see cref="WaveletTree"/>: the coordinate compression and level count, every query
/// (<see cref="WaveletTree.Rank(int, int)"/>, <see cref="WaveletTree.RangeRank"/>,
/// <see cref="WaveletTree.Select"/>, <see cref="WaveletTree.Quantile"/>, <see cref="WaveletTree.RangeCount"/>),
/// their argument validation, and the degenerate shapes with no levels to descend — the empty sequence and the
/// single-symbol alphabet. The randomized reconciliation against a brute-force oracle lives in
/// <see cref="WaveletTreeDifferentialTests"/>, and the enumeration surface in
/// <see cref="WaveletTreeEnumerationTests"/>.
/// </summary>
public class WaveletTreeTests
{
    // A sequence with duplicates, an unsorted order, negative values and a gap in the value space — so the
    // coordinate compression, not the magnitude of the values, is what fixes the level count.
    private static readonly int[] Sample = [5, -3, 5, 12, 0, -3, 900, 5, 12, 0];

    [Fact]
    public void Constructor_ShouldCompressTheAlphabet_WhenValuesAreSparseAndRepeated()
    {
        var tree = new WaveletTree(Sample);

        Assert.Equal(10, tree.Length);
        Assert.Equal(5, tree.AlphabetSize);
        Assert.Equal(new[] { -3, 0, 5, 12, 900 }, tree.Symbols.ToArray());

        // Five symbols need three bits, not the 32 the raw int range would.
        Assert.Equal(3, tree.LevelCount);
    }

    [Fact]
    public void Indexer_ShouldReturnEveryValueInOrder()
    {
        var tree = new WaveletTree(Sample);

        for (int i = 0; i < Sample.Length; i++)
            Assert.Equal(Sample[i], tree[i]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(10)]
    public void Indexer_ShouldThrow_WhenPositionOutsideTheSequence(int index)
    {
        var tree = new WaveletTree(Sample);

        Assert.Throws<ArgumentOutOfRangeException>(() => tree[index]);
    }

    [Fact]
    public void Count_ShouldReportTheSequenceLength_WhenReadThroughTheReadOnlyList()
    {
        IReadOnlyList<int> tree = new WaveletTree(Sample);

        Assert.Equal(Sample.Length, tree.Count);
        Assert.Equal(Sample[3], tree[3]);
    }

    [Fact]
    public void IndexSizeInBytes_ShouldCountEveryLevelAndTheSymbolTable()
    {
        var tree = new WaveletTree(Sample);

        // Three levels of two words each (ten positions round up to one 64-bit word, and the vector's own
        // block index adds to that) plus a five-entry symbol table — the exact figure is layout-dependent, so
        // the assertion is that it is charged for and grows with the level count.
        var wider = new WaveletTree(Enumerable.Range(0, 1000).ToArray());

        Assert.True(tree.IndexSizeInBytes > tree.AlphabetSize * sizeof(int));
        Assert.True(wider.IndexSizeInBytes > tree.IndexSizeInBytes);
    }

    [Fact]
    public void Rank_ShouldCountOccurrencesBelowThePosition()
    {
        var tree = new WaveletTree(Sample);

        Assert.Equal(0, tree.Rank(0, 5));
        Assert.Equal(1, tree.Rank(1, 5));
        Assert.Equal(1, tree.Rank(2, 5));
        Assert.Equal(2, tree.Rank(3, 5));
        Assert.Equal(3, tree.Rank(Sample.Length, 5));
        Assert.Equal(2, tree.Rank(Sample.Length, -3));
    }

    [Fact]
    public void Rank_ShouldReturnZero_WhenTheValueIsAbsent()
    {
        var tree = new WaveletTree(Sample);

        Assert.Equal(0, tree.Rank(Sample.Length, 7));
        Assert.Equal(0, tree.Rank(Sample.Length, int.MinValue));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Rank_ShouldThrow_WhenIndexOutsideTheInclusiveRange(int index)
    {
        var tree = new WaveletTree(Sample);

        Assert.Throws<ArgumentOutOfRangeException>(() => tree.Rank(index, 5));
    }

    [Fact]
    public void RangeRank_ShouldCountOccurrencesInsideTheWindowOnly()
    {
        var tree = new WaveletTree(Sample);

        Assert.Equal(3, tree.RangeRank(0, Sample.Length, 5));
        Assert.Equal(2, tree.RangeRank(2, 6, 5));
        Assert.Equal(0, tree.RangeRank(3, 3, 5));
        Assert.Equal(0, tree.RangeRank(4, 0, 5));
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(11, 0)]
    [InlineData(0, -1)]
    [InlineData(5, 6)]
    public void RangeRank_ShouldThrow_WhenTheWindowIsInvalid(int start, int length)
    {
        var tree = new WaveletTree(Sample);

        Assert.Throws<ArgumentOutOfRangeException>(() => tree.RangeRank(start, length, 5));
    }

    [Fact]
    public void Select_ShouldReturnEveryOccurrenceOfAValueInOrder()
    {
        var tree = new WaveletTree(Sample);

        Assert.Equal(0, tree.Select(0, 5));
        Assert.Equal(2, tree.Select(1, 5));
        Assert.Equal(7, tree.Select(2, 5));
        Assert.Equal(6, tree.Select(0, 900));
    }

    [Fact]
    public void Select_ShouldInvertRank_AtEveryOccurrence()
    {
        var tree = new WaveletTree(Sample);

        foreach (int value in tree.Symbols.ToArray())
        {
            int occurrences = tree.Rank(tree.Length, value);
            for (int k = 0; k < occurrences; k++)
            {
                int position = tree.Select(k, value);
                Assert.Equal(value, tree[position]);
                Assert.Equal(k, tree.Rank(position, value));
            }
        }
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(3, 5)]
    [InlineData(0, 7)]
    public void Select_ShouldThrow_WhenTheOccurrenceDoesNotExist(int rank, int value)
    {
        var tree = new WaveletTree(Sample);

        Assert.Throws<ArgumentOutOfRangeException>(() => tree.Select(rank, value));
    }

    [Fact]
    public void TrySelect_ShouldReportFailure_WithoutThrowing()
    {
        var tree = new WaveletTree(Sample);

        Assert.True(tree.TrySelect(2, 5, out int found));
        Assert.Equal(7, found);

        Assert.False(tree.TrySelect(3, 5, out int missing));
        Assert.Equal(-1, missing);

        Assert.False(tree.TrySelect(-1, 5, out int negative));
        Assert.Equal(-1, negative);

        Assert.False(tree.TrySelect(0, 7, out int absent));
        Assert.Equal(-1, absent);
    }

    [Fact]
    public void Quantile_ShouldReturnTheKthSmallestInTheWindow()
    {
        var tree = new WaveletTree(Sample);

        // The whole sequence sorted: -3, -3, 0, 0, 5, 5, 5, 12, 12, 900.
        Assert.Equal(-3, tree.Quantile(0, Sample.Length, 0));
        Assert.Equal(0, tree.Quantile(0, Sample.Length, 2));
        Assert.Equal(5, tree.Quantile(0, Sample.Length, 5));
        Assert.Equal(900, tree.Quantile(0, Sample.Length, 9));
    }

    [Fact]
    public void Quantile_ShouldSeeOnlyTheWindow()
    {
        var tree = new WaveletTree(Sample);

        // Positions [3, 7) are 12, 0, -3, 900 — sorted: -3, 0, 12, 900.
        Assert.Equal(-3, tree.Quantile(3, 4, 0));
        Assert.Equal(0, tree.Quantile(3, 4, 1));
        Assert.Equal(12, tree.Quantile(3, 4, 2));
        Assert.Equal(900, tree.Quantile(3, 4, 3));
    }

    [Fact]
    public void Quantile_ShouldCountDuplicates()
    {
        var tree = new WaveletTree([5, 5, 5]);

        Assert.Equal(5, tree.Quantile(0, 3, 0));
        Assert.Equal(5, tree.Quantile(0, 3, 2));
    }

    [Theory]
    [InlineData(-1, 2, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(5, 6, 0)]
    [InlineData(0, 0, 0)]
    [InlineData(0, 3, 3)]
    [InlineData(0, 3, -1)]
    public void Quantile_ShouldThrow_WhenTheWindowOrOrdinalIsInvalid(int start, int length, int k)
    {
        var tree = new WaveletTree(Sample);

        Assert.Throws<ArgumentOutOfRangeException>(() => tree.Quantile(start, length, k));
    }

    [Fact]
    public void RangeCount_ShouldCountTheValuesInsideTheBand()
    {
        var tree = new WaveletTree(Sample);

        Assert.Equal(Sample.Length, tree.RangeCount(0, Sample.Length, int.MinValue, int.MaxValue));
        Assert.Equal(3, tree.RangeCount(0, Sample.Length, 5, 5));
        Assert.Equal(6, tree.RangeCount(0, Sample.Length, 5, 900));
        Assert.Equal(4, tree.RangeCount(0, Sample.Length, -3, 0));
    }

    [Fact]
    public void RangeCount_ShouldResolveBandEdgesThatAreNotThemselvesPresent()
    {
        var tree = new WaveletTree(Sample);

        // 1..11 contains no symbol but 5; -4..13 contains everything below 900.
        Assert.Equal(3, tree.RangeCount(0, Sample.Length, 1, 11));
        Assert.Equal(9, tree.RangeCount(0, Sample.Length, -4, 13));
    }

    [Fact]
    public void RangeCount_ShouldReturnZero_WhenTheBandMissesTheAlphabetEntirely()
    {
        var tree = new WaveletTree(Sample);

        Assert.Equal(0, tree.RangeCount(0, Sample.Length, 1, 4));
        Assert.Equal(0, tree.RangeCount(0, Sample.Length, 901, int.MaxValue));
        Assert.Equal(0, tree.RangeCount(0, Sample.Length, int.MinValue, -4));
    }

    [Fact]
    public void RangeCount_ShouldReturnZero_WhenTheWindowIsEmptyOrTheBandInverted()
    {
        var tree = new WaveletTree(Sample);

        Assert.Equal(0, tree.RangeCount(4, 0, int.MinValue, int.MaxValue));
        Assert.Equal(0, tree.RangeCount(0, Sample.Length, 10, 9));
    }

    [Fact]
    public void RangeCount_ShouldStopEarly_WhenTheWindowHoldsNothingBelowTheBand()
    {
        var tree = new WaveletTree([0, 1, 2, 3]);

        // The window is the two largest values, so the descent for the lower band edge empties before it
        // reaches the last level.
        Assert.Equal(0, tree.RangeCount(2, 2, 0, 0));
        Assert.Equal(1, tree.RangeCount(2, 2, 3, 3));
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(11, 0)]
    [InlineData(0, -1)]
    [InlineData(5, 6)]
    public void RangeCount_ShouldThrow_WhenTheWindowIsInvalid(int start, int length)
    {
        var tree = new WaveletTree(Sample);

        Assert.Throws<ArgumentOutOfRangeException>(() => tree.RangeCount(start, length, 0, 10));
    }

    // ---- The two shapes with no levels at all ----

    [Fact]
    public void EmptySequence_ShouldReportNoLevelsAndAnswerEveryQueryEmptily()
    {
        var tree = new WaveletTree([]);

        Assert.Equal(0, tree.Length);
        Assert.Equal(0, tree.AlphabetSize);
        Assert.Equal(0, tree.LevelCount);
        Assert.Equal(0, tree.IndexSizeInBytes);
        Assert.Empty(tree.Symbols.ToArray());

        Assert.Equal(0, tree.Rank(0, 5));
        Assert.Equal(0, tree.RangeRank(0, 0, 5));
        Assert.Equal(0, tree.RangeCount(0, 0, int.MinValue, int.MaxValue));
        Assert.False(tree.TrySelect(0, 5, out _));
    }

    [Fact]
    public void SingleSymbolAlphabet_ShouldAnswerWithoutADescent()
    {
        var tree = new WaveletTree([7, 7, 7, 7]);

        Assert.Equal(4, tree.Length);
        Assert.Equal(1, tree.AlphabetSize);
        Assert.Equal(0, tree.LevelCount);

        Assert.Equal(7, tree[2]);
        Assert.Equal(3, tree.Rank(3, 7));
        Assert.Equal(2, tree.RangeRank(1, 2, 7));
        Assert.Equal(0, tree.RangeRank(1, 2, 8));
        Assert.Equal(2, tree.Select(2, 7));
        Assert.Equal(7, tree.Quantile(1, 3, 1));

        Assert.Equal(4, tree.RangeCount(0, 4, 7, 7));
        Assert.Equal(2, tree.RangeCount(1, 2, int.MinValue, int.MaxValue));
        Assert.Equal(0, tree.RangeCount(0, 4, 8, 9));
    }

    [Fact]
    public void Constructor_ShouldNotAliasTheSourceArray()
    {
        int[] source = [3, 1, 2];
        var tree = new WaveletTree(source);

        source[0] = 99;

        Assert.Equal(3, tree[0]);
        Assert.Equal(new[] { 1, 2, 3 }, tree.Symbols.ToArray());
    }

    [Fact]
    public void Constructor_ShouldHandleAnAlphabetThatIsExactlyAPowerOfTwo()
    {
        // Eight symbols is exactly three levels; nine needs four. The off-by-one in ceil(log2) lives here.
        var exact = new WaveletTree(Enumerable.Range(0, 8).ToArray());
        var overflowing = new WaveletTree(Enumerable.Range(0, 9).ToArray());

        Assert.Equal(3, exact.LevelCount);
        Assert.Equal(4, overflowing.LevelCount);
        Assert.Equal(7, exact.Quantile(0, 8, 7));
        Assert.Equal(8, overflowing.Quantile(0, 9, 8));
    }

    [Fact]
    public void Constructor_ShouldStraddleTheBlockAndSuperblockBoundaries()
    {
        // 300 positions crosses both the 64-bit block and the 256-bit superblock boundary of every level's
        // rank index, which is where a level-mapping off-by-one would show first.
        int[] values = new int[300];
        for (int i = 0; i < values.Length; i++)
            values[i] = (i * 7) % 11;

        var tree = new WaveletTree(values);
        int[] sorted = (int[])values.Clone();
        Array.Sort(sorted);

        for (int k = 0; k < values.Length; k += 17)
            Assert.Equal(sorted[k], tree.Quantile(0, values.Length, k));

        for (int i = 0; i < values.Length; i++)
            Assert.Equal(values[i], tree[i]);
    }
}
