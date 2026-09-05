using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural coverage of <see cref="SparseTable{T, TMonoid}"/>: construction, validation, the
/// <c>O(1)</c> range query, and the properties that report what the table costs.
/// </summary>
public class SparseTableTests
{
    // ---- construction ------------------------------------------------------------------------------

    [Fact]
    public void Constructor_FromArray_ShouldPreserveValuesAndCount()
    {
        // T[] is an ICollection<T>, so this takes the counted fast path.
        var table = new SparseTable<int, MinMonoid<int>>(new[] { 5, 3, 9, 1 });

        Assert.Equal(4, table.Count);
        Assert.Equal([5, 3, 9, 1], table);
    }

    [Fact]
    public void Constructor_FromEnumerable_ShouldPreserveValuesAndCount()
    {
        // A List<T> is the other counted source.
        var table = new SparseTable<int, MinMonoid<int>>(new List<int> { 5, 3, 9, 1 });

        Assert.Equal(4, table.Count);
        Assert.Equal([5, 3, 9, 1], table);
    }

    [Fact]
    public void Constructor_FromUncountedEnumerable_ShouldPreserveValuesAndCount()
    {
        // Enumerable.Where is not an ICollection<T>, so this takes the materialize-once path instead.
        IEnumerable<int> source = new[] { 5, 3, 9, 1, 7 }.Where(v => v != 7);

        var table = new SparseTable<int, MinMonoid<int>>(source);

        Assert.Equal(4, table.Count);
        Assert.Equal([5, 3, 9, 1], table);
    }

    [Fact]
    public void Constructor_ShouldCopyTheSource_SoLaterWritesAreNotObserved()
    {
        var source = new[] { 5, 3, 9, 1 };
        var table = new SparseTable<int, MinMonoid<int>>(source);

        source[2] = -100;

        Assert.Equal(9, table[2]);
        Assert.Equal(1, table.Query(0, 4));
    }

    [Fact]
    public void Constructor_FromNullEnumerable_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new SparseTable<int, MinMonoid<int>>(null!));
        Assert.Throws<ArgumentNullException>(() => new SparseTable<int, MinMonoid<int>>(null!, default));
    }

    [Fact]
    public void Constructor_WithStatefulMonoid_ShouldUseTheSuppliedInstance()
    {
        // The floor is the identity, so it is what an empty range returns — a default-constructed
        // FlooredMaxMonoid would report 0 instead.
        var table = new SparseTable<int, FlooredMaxMonoid>(new[] { 30, 10, 20 }, new FlooredMaxMonoid(7));

        Assert.Equal(7, table.Query(1, 1));
        Assert.Equal(30, table.Query(0, 3));

        // The same through the uncounted path, which allocates its own seed before measuring.
        IEnumerable<int> uncounted = new[] { 30, 10, 20 }.Where(v => v > 0);
        var fromUncounted = new SparseTable<int, FlooredMaxMonoid>(uncounted, new FlooredMaxMonoid(7));

        Assert.Equal(7, fromUncounted.Query(1, 1));
        Assert.Equal(30, fromUncounted.Query(0, 3));
    }

    // ---- shape -------------------------------------------------------------------------------------

    [Fact]
    public void EmptyTable_ShouldHaveNoLevelsAndAnswerTheIdentity()
    {
        var table = new SparseTable<int, MinMonoid<int>>(Array.Empty<int>());

        Assert.Equal(0, table.Count);
        Assert.Equal(0, table.LevelCount);
        Assert.Equal(0, table.IndexSizeInBytes);
        Assert.Equal(int.MaxValue, table.Aggregate);
        Assert.Equal(int.MaxValue, table.Query(0, 0));
        Assert.Empty(table);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(7, 3)]
    [InlineData(8, 4)]
    [InlineData(1000, 10)]
    public void LevelCount_ShouldBeFloorLog2PlusOne(int length, int expectedLevels)
    {
        var table = new SparseTable<int, MinMonoid<int>>(new int[length]);

        Assert.Equal(expectedLevels, table.LevelCount);
    }

    [Fact]
    public void IndexSizeInBytes_ShouldCountEveryCellInTheRectangle()
    {
        var table = new SparseTable<int, MinMonoid<int>>(new int[1000]);

        // 10 rows of 1000 int cells.
        Assert.Equal(10L * 1000 * sizeof(int), table.IndexSizeInBytes);
    }

    // ---- the indexer -------------------------------------------------------------------------------

    [Fact]
    public void Indexer_ShouldReturnTheStoredValue()
    {
        var table = new SparseTable<int, MinMonoid<int>>(new[] { 5, 3, 9, 1 });

        Assert.Equal(5, table[0]);
        Assert.Equal(3, table[1]);
        Assert.Equal(9, table[2]);
        Assert.Equal(1, table[3]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Indexer_OutOfRange_ShouldThrow(int index)
    {
        var table = new SparseTable<int, MinMonoid<int>>(new[] { 5, 3, 9, 1 });

        Assert.Throws<ArgumentOutOfRangeException>(() => table[index]);
    }

    // ---- the query ---------------------------------------------------------------------------------

    [Fact]
    public void Query_ShouldAnswerEveryRangeOfASmallSequence()
    {
        int[] values = [5, 3, 9, 1, 7, 2, 8];
        var table = new SparseTable<int, MinMonoid<int>>(values);

        for (int start = 0; start <= values.Length; start++)
        {
            for (int end = start; end <= values.Length; end++)
            {
                int expected = end == start ? int.MaxValue : values[start..end].Min();
                Assert.Equal(expected, table.Query(start, end));
            }
        }
    }

    [Fact]
    public void Query_EmptyRange_ShouldReturnTheIdentity()
    {
        var table = new SparseTable<int, MinMonoid<int>>(new[] { 5, 3, 9, 1 });

        Assert.Equal(int.MaxValue, table.Query(0, 0));
        Assert.Equal(int.MaxValue, table.Query(2, 2));
        Assert.Equal(int.MaxValue, table.Query(4, 4));
    }

    [Fact]
    public void Query_SingleElementRange_ShouldReturnThatElement()
    {
        // The degenerate overlap: both windows are the same one-element window, so this is the case
        // idempotence is doing the most obvious work in.
        int[] values = [5, 3, 9, 1];
        var table = new SparseTable<int, MinMonoid<int>>(values);

        for (int i = 0; i < values.Length; i++)
            Assert.Equal(values[i], table.Query(i, i + 1));
    }

    [Fact]
    public void Query_WholeRange_ShouldMatchAggregate()
    {
        var table = new SparseTable<int, MaxMonoid<int>>(new[] { 5, 3, 9, 1 });

        Assert.Equal(9, table.Aggregate);
        Assert.Equal(table.Aggregate, table.Query(0, table.Count));
    }

    [Fact]
    public void Query_NonPowerOfTwoLength_ShouldFoldTheOverlapWithoutChangingTheAnswer()
    {
        // Length 3 is covered by two length-2 windows that share the middle element; length 5 by two length-4
        // windows sharing three. These are the ranges a non-idempotent fold would get wrong.
        int[] values = [4, 1, 6, 2, 5];
        var table = new SparseTable<int, MinMonoid<int>>(values);

        Assert.Equal(1, table.Query(0, 3));
        Assert.Equal(1, table.Query(0, 5));
        Assert.Equal(2, table.Query(2, 5));
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(0, -1)]
    [InlineData(5, 5)]
    [InlineData(0, 5)]
    [InlineData(3, 1)]
    public void Query_InvalidRange_ShouldThrow(int start, int endExclusive)
    {
        var table = new SparseTable<int, MinMonoid<int>>(new[] { 5, 3, 9, 1 });

        Assert.Throws<ArgumentOutOfRangeException>(() => table.Query(start, endExclusive));
    }

    [Fact]
    public void Query_InvalidRange_ShouldNameTheOffendingParameter()
    {
        var table = new SparseTable<int, MinMonoid<int>>(new[] { 5, 3, 9, 1 });

        Assert.Equal("start", Assert.Throws<ArgumentOutOfRangeException>(() => table.Query(-1, 2)).ParamName);
        Assert.Equal("endExclusive", Assert.Throws<ArgumentOutOfRangeException>(() => table.Query(0, 9)).ParamName);
        Assert.Equal("endExclusive", Assert.Throws<ArgumentOutOfRangeException>(() => table.Query(3, 1)).ParamName);
    }

    // ---- the folds ---------------------------------------------------------------------------------

    [Fact]
    public void Query_ShouldAnswerMax()
    {
        var table = new SparseTable<int, MaxMonoid<int>>(new[] { 5, 3, 9, 1, 7 });

        Assert.Equal(9, table.Query(0, 5));
        Assert.Equal(9, table.Query(1, 3));
        Assert.Equal(7, table.Query(3, 5));
    }

    [Fact]
    public void Query_ShouldAnswerBitwiseAnd()
    {
        var table = new SparseTable<uint, BitwiseAndMonoid<uint>>(new uint[] { 0b1111, 0b1110, 0b1100, 0b0101 });

        Assert.Equal(0b1110u, table.Query(0, 2));
        Assert.Equal(0b1100u, table.Query(0, 3));
        Assert.Equal(0b0100u, table.Query(0, 4));
        Assert.Equal(uint.MaxValue, table.Query(2, 2));
    }

    [Fact]
    public void Query_ShouldAnswerBitwiseOr()
    {
        var table = new SparseTable<uint, BitwiseOrMonoid<uint>>(new uint[] { 0b0001, 0b0010, 0b0100, 0b1000 });

        Assert.Equal(0b0011u, table.Query(0, 2));
        Assert.Equal(0b1111u, table.Query(0, 4));
        Assert.Equal(0u, table.Query(1, 1));
    }

    [Fact]
    public void Query_ShouldAnswerAUserWrittenIdempotentFold()
    {
        // GcdMonoid is the "write your own fold" sample from IIdempotentMonoid<T>'s docs.
        var table = new SparseTable<uint, GcdMonoid>(new uint[] { 12, 18, 24, 9 });

        Assert.Equal(6u, table.Query(0, 2));
        Assert.Equal(6u, table.Query(0, 3));
        Assert.Equal(3u, table.Query(0, 4));
        Assert.Equal(0u, table.Query(1, 1));
    }

    [Fact]
    public void Query_ShouldPreserveIndexOrderForANonCommutativeFold()
    {
        // "First non-zero wins" reads left to right, so the answer says which window was combined first.
        int[] values = [0, 0, 4, 7, 0];
        var table = new SparseTable<int, FirstNonZeroMonoid>(values);

        Assert.Equal(4, table.Query(0, 5));
        Assert.Equal(4, table.Query(2, 4));
        Assert.Equal(7, table.Query(3, 5));
        Assert.Equal(0, table.Query(0, 2));
    }

    [Fact]
    public void Query_ShouldSupportAReferenceTypeElement()
    {
        string[] values = ["", "", "beta", "gamma", ""];
        var table = new SparseTable<string, FirstNonEmptyMonoid>(values);

        Assert.Equal("beta", table.Query(0, 5));
        Assert.Equal("gamma", table.Query(3, 5));
        Assert.Equal(string.Empty, table.Query(0, 2));
        Assert.Equal(5, table.Count);
    }

    [Fact]
    public void Query_ShouldAgreeWithSegmentTreeOverTheSameSequence()
    {
        // The two types are documented as answering the same question, so they had better agree.
        int[] values = [5, 3, 9, 1, 7, 2, 8, 4, 6];
        var table = new SparseTable<int, MinMonoid<int>>(values);
        var tree = new SegmentTree<int, MinMonoid<int>>(values);

        for (int start = 0; start <= values.Length; start++)
            for (int end = start; end <= values.Length; end++)
                Assert.Equal(tree.Query(start, end), table.Query(start, end));
    }

    // ---- IReadOnlyList -----------------------------------------------------------------------------

    [Fact]
    public void IReadOnlyList_ShouldExposeTheValues()
    {
        IReadOnlyList<int> table = new SparseTable<int, MinMonoid<int>>(new[] { 5, 3, 9, 1 });

        Assert.Equal(4, table.Count);
        Assert.Equal(9, table[2]);
        Assert.Equal([5, 3, 9, 1], table);
    }
}
