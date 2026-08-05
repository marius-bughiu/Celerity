using Celerity.Sorting;

// The null-ordering tests instantiate the IComparable<T> overloads at string?, which the compiler
// flags because string implements IComparable<string>, not IComparable<string?>. Ordering nulls is
// exactly what those tests exist to pin, so the annotation mismatch is the point.
#pragma warning disable CS8631

namespace Celerity.Tests.Sorting;

/// <summary>
/// Behavioural tests for <see cref="PartialSort"/> — the selection contract of
/// <c>Select</c> / <c>Sort</c> / <c>TopK</c> in both the natural-order and struct-comparer forms,
/// the degenerate shapes (empty, k = 0, k = n, all-equal, source shorter than k), and the two
/// robustness properties the implementation claims: a three-way partition that does not degrade on
/// duplicates, and a depth budget that stops an ill-behaved comparer from spinning forever.
/// </summary>
public class PartialSortTests
{
    /// <summary>Descending order, to exercise the comparer-parameterized overloads.</summary>
    private readonly struct DescendingComparer : IComparer<int>
    {
        public int Compare(int x, int y) => y.CompareTo(x);
    }

    /// <summary>
    /// A comparer that reports the same order for every pair, so no partition can make progress.
    /// Only the depth budget stops the selector.
    /// </summary>
    private readonly struct InconsistentComparer : IComparer<int>
    {
        public int Compare(int x, int y) => 1;
    }

    private static int[] Shuffled(int length, int seed)
    {
        int[] values = Enumerable.Range(0, length).ToArray();
        var rand = new Random(seed);
        for (int i = length - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }

        return values;
    }

    // ---- Select -------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(40)]
    [InlineData(200)]
    public void Select_ShouldBringTheSmallestToTheFront_WhenTheSpanIsShuffled(int count)
    {
        int[] values = Shuffled(200, seed: count);

        PartialSort.Select(values.AsSpan(), count);

        Assert.Equal(Enumerable.Range(0, count), values.Take(count).Order());
    }

    [Fact]
    public void Select_ShouldDoNothing_WhenTheCountIsZero()
    {
        int[] values = [3, 1, 2];

        PartialSort.Select(values.AsSpan(), 0);

        Assert.Equal([3, 1, 2], values);
    }

    [Fact]
    public void Select_ShouldDoNothing_WhenTheCountIsTheWholeSpan()
    {
        int[] values = [3, 1, 2];

        PartialSort.Select(values.AsSpan(), 3);

        Assert.Equal([3, 1, 2], values);
    }

    [Fact]
    public void Select_ShouldTerminateInOnePartition_WhenEveryElementIsEqual()
    {
        int[] values = new int[500];
        Array.Fill(values, 9);

        PartialSort.Select(values.AsSpan(), 100);

        Assert.All(values, v => Assert.Equal(9, v));
    }

    [Fact]
    public void Select_ShouldBringTheLargestToTheFront_WhenGivenADescendingComparer()
    {
        int[] values = Shuffled(120, seed: 11);

        PartialSort.Select(values.AsSpan(), 5, default(DescendingComparer));

        Assert.Equal(new[] { 115, 116, 117, 118, 119 }, values.Take(5).Order());
    }

    [Fact]
    public void Select_ShouldPlaceNullsFirst_WhenTheElementsAreNullableReferences()
    {
        // Wide enough that the range actually partitions (rather than falling straight through to
        // the small-span sort), and null-dense enough that null-vs-null is compared along the way.
        string?[] values = new string?[40];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = i % 3 == 0 ? null : $"s{i:D2}";
        }

        PartialSort.Select<string?>(values.AsSpan(), 5);

        Assert.All(values.Take(5), Assert.Null);
    }

    [Fact]
    public void Select_ShouldReturnWithoutSpinning_WhenTheComparerReportsInconsistentOrders()
    {
        // The depth budget is the only thing bounding this: an always-"greater" comparer defeats
        // the partition-progress argument, so without it the loop would never advance.
        int[] values = Shuffled(300, seed: 5);

        PartialSort.Select(values.AsSpan(), 10, default(InconsistentComparer));

        Assert.Equal(Enumerable.Range(0, 300), values.Order());
    }

    // ---- Sort ---------------------------------------------------------------------------------

    [Fact]
    public void Sort_ShouldOrderTheSmallestPrefix_WhenTheSpanIsShuffled()
    {
        int[] values = Shuffled(150, seed: 3);

        PartialSort.Sort(values.AsSpan(), 6);

        Assert.Equal([0, 1, 2, 3, 4, 5], values.Take(6));
    }

    [Fact]
    public void Sort_ShouldOrderTheLargestPrefixDescending_WhenGivenADescendingComparer()
    {
        int[] values = Shuffled(150, seed: 4);

        PartialSort.Sort(values.AsSpan(), 4, default(DescendingComparer));

        Assert.Equal([149, 148, 147, 146], values.Take(4));
    }

    [Fact]
    public void Sort_ShouldOrderALargePrefix_WhenTheCountExceedsTheInsertionThreshold()
    {
        // Past 16 the prefix is ordered by the in-place heap sort rather than insertion sort, which
        // is a different code path and the one the depth-limit fallback also lands on.
        int[] values = Shuffled(400, seed: 12);

        PartialSort.Sort(values.AsSpan(), 50);

        Assert.Equal(Enumerable.Range(0, 50), values.Take(50));
    }

    [Fact]
    public void Sort_ShouldOrderALargePrefix_WhenValuesRepeat()
    {
        int[] values = new int[300];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = i % 7;
        }

        int[] expected = values.Order().Take(60).ToArray();

        PartialSort.Sort(values.AsSpan(), 60);

        Assert.Equal(expected, values.Take(60));
    }

    [Fact]
    public void Sort_ShouldDoNothing_WhenTheSpanIsEmpty()
    {
        int[] values = [];

        PartialSort.Sort(values.AsSpan(), 0);

        Assert.Empty(values);
    }

    // ---- TopK ---------------------------------------------------------------------------------

    [Fact]
    public void TopK_ShouldReturnTheLargestDescending_WhenTheSourceIsLongerThanTheDestination()
    {
        int[] source = Shuffled(500, seed: 7);
        int[] destination = new int[5];

        int written = PartialSort.TopK<int>(source, destination.AsSpan());

        Assert.Equal(5, written);
        Assert.Equal([499, 498, 497, 496, 495], destination);
    }

    [Fact]
    public void TopK_ShouldNotModifyTheSource_WhenItScans()
    {
        int[] source = Shuffled(100, seed: 8);
        int[] snapshot = (int[])source.Clone();

        PartialSort.TopK<int>(source, new int[3].AsSpan());

        Assert.Equal(snapshot, source);
    }

    [Fact]
    public void TopK_ShouldCopyEverything_WhenTheSourceIsShorterThanTheDestination()
    {
        int[] source = [2, 5, 1];
        int[] destination = new int[10];

        int written = PartialSort.TopK<int>(source, destination.AsSpan());

        Assert.Equal(3, written);
        Assert.Equal([5, 2, 1], destination.Take(3));
    }

    [Fact]
    public void TopK_ShouldWriteNothing_WhenTheDestinationIsEmpty()
    {
        int[] source = [1, 2, 3];

        Assert.Equal(0, PartialSort.TopK<int>(source, Span<int>.Empty));
    }

    [Fact]
    public void TopK_ShouldReturnTheSmallest_WhenGivenADescendingComparer()
    {
        int[] source = Shuffled(80, seed: 9);
        int[] destination = new int[3];

        PartialSort.TopK<int, DescendingComparer>(source, destination.AsSpan(), default);

        Assert.Equal([0, 1, 2], destination);
    }

    [Fact]
    public void TopK_ShouldHandleAnOddHeapWidth_WhenTheLastParentHasOneChild()
    {
        // k = 4 leaves node 1 with a single child, which is the sift path that skips the
        // two-child comparison.
        int[] source = Shuffled(60, seed: 10);
        int[] destination = new int[4];

        PartialSort.TopK<int>(source, destination.AsSpan());

        Assert.Equal([59, 58, 57, 56], destination);
    }

    [Fact]
    public void TopK_ShouldOrderNullsLast_WhenTheElementsAreNullableReferences()
    {
        string?[] source = ["b", null, "a", "c"];
        string?[] destination = new string?[2];

        PartialSort.TopK<string?>(source, destination.AsSpan());

        Assert.Equal(["c", "b"], destination);
    }

    // ---- validation ---------------------------------------------------------------------------

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void Select_ShouldThrow_WhenTheCountIsOutsideTheSpan(int count)
    {
        int[] values = [1, 2, 3];

        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => PartialSort.Select(values.AsSpan(), count));
        Assert.Equal("count", ex.ParamName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenTheCountIsOutsideTheSpan()
    {
        int[] values = [1, 2, 3];

        Assert.Throws<ArgumentOutOfRangeException>(() => PartialSort.Sort(values.AsSpan(), 9));
    }
}
