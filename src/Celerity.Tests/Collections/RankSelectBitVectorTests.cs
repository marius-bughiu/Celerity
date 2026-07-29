using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural coverage for <see cref="RankSelectBitVector"/>: the three constructors and their validation, the
/// <see cref="RankSelectBitVector.Rank(int)"/> / <see cref="RankSelectBitVector.Rank0(int)"/> /
/// <see cref="RankSelectBitVector.Select(int)"/> core, the block and superblock boundaries the two-level index
/// pivots on, and the degenerate all-zero / all-one / empty vectors. The randomized reconciliation against a
/// naive <c>bool[]</c> oracle lives in <see cref="RankSelectBitVectorDifferentialTests"/>.
/// </summary>
public class RankSelectBitVectorTests
{
    // 256 bits per superblock, 64 per block — the two boundaries every index arithmetic bug straddles.
    private const int Superblock = 256;

    [Fact]
    public void Constructor_ShouldSnapshotTheBits_WhenGivenABitSet()
    {
        var bits = new BitSet(100);
        bits.Set(3, true);
        bits.Set(70, true);

        var vector = new RankSelectBitVector(bits);

        // The snapshot must not alias the mutable set it was built from.
        bits.Set(99, true);

        Assert.Equal(100, vector.Length);
        Assert.Equal(2, vector.Count);
        Assert.False(vector[99]);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenBitSetNull()
    {
        Assert.Throws<ArgumentNullException>(() => new RankSelectBitVector(null!));
    }

    [Fact]
    public void Constructor_ShouldSetTheGivenPositions_WhenGivenPositions()
    {
        var vector = new RankSelectBitVector(10, new[] { 0, 4, 9, 4 });

        Assert.Equal(3, vector.Count);
        Assert.True(vector[0]);
        Assert.True(vector[4]);
        Assert.True(vector[9]);
        Assert.False(vector[1]);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenPositionsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new RankSelectBitVector(8, (IEnumerable<int>)null!));
        Assert.Equal("positions", ex.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(8)]
    public void Constructor_ShouldThrow_WhenAPositionIsOutOfRange(int position)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new RankSelectBitVector(8, new[] { position }));
        Assert.Equal("positions", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLengthNegativeForPositions()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new RankSelectBitVector(-1, Array.Empty<int>()));
        Assert.Equal("length", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldReadPackedWords_WhenGivenASpan()
    {
        ulong[] words = { 0b1011UL, 1UL << 5 };

        var vector = new RankSelectBitVector(128, words);

        Assert.Equal(128, vector.Length);
        Assert.Equal(4, vector.Count);
        Assert.True(vector[0]);
        Assert.True(vector[1]);
        Assert.False(vector[2]);
        Assert.True(vector[3]);
        Assert.True(vector[69]);
    }

    [Fact]
    public void Constructor_ShouldIgnoreBitsAtOrAboveLength_WhenGivenASpan()
    {
        // Only bits [0, 70) are part of the vector; the rest of the second word must not reach the index.
        ulong[] words = { ulong.MaxValue, ulong.MaxValue };

        var vector = new RankSelectBitVector(70, words);

        Assert.Equal(70, vector.Count);
        Assert.Equal(70, vector.Rank(70));
        Assert.Equal(69, vector.Select(69));
    }

    [Fact]
    public void Constructor_ShouldIgnoreExtraWords_WhenGivenASpanLongerThanNeeded()
    {
        ulong[] words = { 0b11UL, ulong.MaxValue, ulong.MaxValue };

        var vector = new RankSelectBitVector(64, words);

        Assert.Equal(2, vector.Count);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSpanTooShort()
    {
        ulong[] words = { 0UL };

        var ex = Assert.Throws<ArgumentException>(() => new RankSelectBitVector(65, words));
        Assert.Equal("words", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLengthNegativeForSpan()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new RankSelectBitVector(-1, ReadOnlySpan<ulong>.Empty));
        Assert.Equal("length", ex.ParamName);
    }

    [Fact]
    public void Rank_ShouldCountBitsBelowTheIndex()
    {
        var vector = new RankSelectBitVector(10, new[] { 1, 3, 7 });

        Assert.Equal(0, vector.Rank(0));
        Assert.Equal(0, vector.Rank(1));
        Assert.Equal(1, vector.Rank(2));
        Assert.Equal(1, vector.Rank(3));
        Assert.Equal(2, vector.Rank(4));
        Assert.Equal(2, vector.Rank(7));
        Assert.Equal(3, vector.Rank(8));
        Assert.Equal(3, vector.Rank(10));
    }

    [Fact]
    public void Rank_ShouldEqualCount_WhenIndexIsLength()
    {
        var vector = new RankSelectBitVector(new BitSet(300, true));

        Assert.Equal(vector.Count, vector.Rank(vector.Length));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Rank_ShouldThrow_WhenIndexOutOfRange(int index)
    {
        var vector = new RankSelectBitVector(10, Array.Empty<int>());

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => vector.Rank(index));
        Assert.Equal("index", ex.ParamName);
    }

    [Fact]
    public void Rank0_ShouldCountClearBitsBelowTheIndex()
    {
        var vector = new RankSelectBitVector(10, new[] { 1, 3, 7 });

        Assert.Equal(0, vector.Rank0(0));
        Assert.Equal(1, vector.Rank0(1));
        Assert.Equal(1, vector.Rank0(2));
        Assert.Equal(7, vector.Rank0(10));
    }

    [Fact]
    public void Rank0_ShouldThrow_WhenIndexOutOfRange()
    {
        var vector = new RankSelectBitVector(10, Array.Empty<int>());

        Assert.Throws<ArgumentOutOfRangeException>(() => vector.Rank0(11));
    }

    [Fact]
    public void Select_ShouldReturnThePositionOfTheKthSetBit()
    {
        var vector = new RankSelectBitVector(600, new[] { 0, 63, 64, 255, 256, 511, 512, 599 });

        Assert.Equal(0, vector.Select(0));
        Assert.Equal(63, vector.Select(1));
        Assert.Equal(64, vector.Select(2));
        Assert.Equal(255, vector.Select(3));
        Assert.Equal(256, vector.Select(4));
        Assert.Equal(511, vector.Select(5));
        Assert.Equal(512, vector.Select(6));
        Assert.Equal(599, vector.Select(7));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Select_ShouldThrow_WhenRankOutOfRange(int rank)
    {
        var vector = new RankSelectBitVector(10, new[] { 1, 4, 9 });

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => vector.Select(rank));
        Assert.Equal("rank", ex.ParamName);
    }

    [Fact]
    public void TrySelect_ShouldReturnTrueAndThePosition_WhenRankInRange()
    {
        var vector = new RankSelectBitVector(10, new[] { 1, 4, 9 });

        Assert.True(vector.TrySelect(2, out int position));
        Assert.Equal(9, position);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void TrySelect_ShouldReturnFalseAndMinusOne_WhenRankOutOfRange(int rank)
    {
        var vector = new RankSelectBitVector(10, new[] { 1, 4, 9 });

        Assert.False(vector.TrySelect(rank, out int position));
        Assert.Equal(-1, position);
    }

    [Fact]
    public void SelectAndRank_ShouldRoundTrip_OverEverySetBit()
    {
        var rng = new Random(7);
        var positions = new SortedSet<int>();
        while (positions.Count < 500)
            positions.Add(rng.Next(4096));

        var vector = new RankSelectBitVector(4096, positions);

        int k = 0;
        foreach (int expected in positions)
        {
            int actual = vector.Select(k);
            Assert.Equal(expected, actual);
            Assert.Equal(k, vector.Rank(actual));
            k++;
        }
    }

    [Fact]
    public void Select_ShouldResolveEveryBitWithinAWord_WhenTheWordIsFull()
    {
        // Drives all six narrowing steps of the within-word binary search across every possible remainder.
        var vector = new RankSelectBitVector(new BitSet(64, true));

        for (int k = 0; k < 64; k++)
            Assert.Equal(k, vector.Select(k));
    }

    [Fact]
    public void Select_ShouldSkipEmptySuperblocks()
    {
        // The only set bits live in the last superblock, so both the superblock binary search and the
        // within-superblock word walk have to step over long empty runs.
        var vector = new RankSelectBitVector(4 * Superblock, new[] { 4 * Superblock - 1 });

        Assert.Equal(1, vector.Count);
        Assert.Equal(4 * Superblock - 1, vector.Select(0));
    }

    [Fact]
    public void Get_ShouldThrow_WhenIndexOutOfRange()
    {
        var vector = new RankSelectBitVector(10, Array.Empty<int>());

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => vector.Get(10));
        Assert.Equal("index", ex.ParamName);
        Assert.Throws<ArgumentOutOfRangeException>(() => vector[-1]);
    }

    [Fact]
    public void EmptyVector_ShouldReportZeroLengthAndRejectEveryQuery()
    {
        var vector = new RankSelectBitVector(0, Array.Empty<int>());

        Assert.Equal(0, vector.Length);
        Assert.Equal(0, vector.Count);
        Assert.Equal(0, vector.Rank(0));
        Assert.Equal(0, vector.IndexSizeInBytes);
        Assert.False(vector.TrySelect(0, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => vector.Get(0));
    }

    [Fact]
    public void AllZeroVector_ShouldRankZeroEverywhereAndSelectNothing()
    {
        var vector = new RankSelectBitVector(1000, Array.Empty<int>());

        Assert.Equal(0, vector.Count);
        for (int i = 0; i <= 1000; i += 97)
            Assert.Equal(0, vector.Rank(i));
        Assert.Equal(1000, vector.Rank0(1000));
        Assert.False(vector.TrySelect(0, out _));
    }

    [Fact]
    public void AllOnesVector_ShouldRankAndSelectTheIdentity()
    {
        var vector = new RankSelectBitVector(new BitSet(1000, true));

        Assert.Equal(1000, vector.Count);
        for (int i = 0; i <= 1000; i++)
        {
            Assert.Equal(i, vector.Rank(i));
            Assert.Equal(0, vector.Rank0(i));
        }
        for (int k = 0; k < 1000; k++)
            Assert.Equal(k, vector.Select(k));
    }

    [Theory]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(511)]
    [InlineData(512)]
    [InlineData(513)]
    public void RankAndSelect_ShouldAgreeWithTheOracle_AtBlockAndSuperblockBoundaries(int length)
    {
        // A single set bit at the last position of a vector whose length straddles a word or superblock edge:
        // the position where an off-by-one in the two-level index shows up first.
        var vector = new RankSelectBitVector(length, new[] { length - 1 });

        Assert.Equal(1, vector.Count);
        Assert.Equal(0, vector.Rank(length - 1));
        Assert.Equal(1, vector.Rank(length));
        Assert.Equal(length - 1, vector.Select(0));

        // And the mirror: a single set bit at position 0.
        var first = new RankSelectBitVector(length, new[] { 0 });
        Assert.Equal(0, first.Rank(0));
        Assert.Equal(1, first.Rank(1));
        Assert.Equal(1, first.Rank(length));
        Assert.Equal(0, first.Select(0));
    }

    [Fact]
    public void IndexSizeInBytes_ShouldReportTheTwoIndexArrays()
    {
        // 1024 bits = 16 words (one byte each) across 4 superblocks (four ints).
        var vector = new RankSelectBitVector(1024, Array.Empty<int>());

        Assert.Equal((4 * sizeof(int)) + 16, vector.IndexSizeInBytes);
    }

    [Fact]
    public void ToBitSet_ShouldReturnAnIndependentMutableCopy()
    {
        var vector = new RankSelectBitVector(300, new[] { 5, 200, 299 });

        BitSet copy = vector.ToBitSet();

        Assert.Equal(300, copy.Length);
        Assert.Equal(3, copy.Count);
        Assert.True(copy.Get(5));
        Assert.True(copy.Get(200));
        Assert.True(copy.Get(299));

        copy.Set(6, true);
        Assert.Equal(3, vector.Count);
        Assert.False(vector[6]);
    }
}
