using Celerity.Sorting;

namespace Celerity.Tests.Sorting;

/// <summary>
/// Hand-picked cases for <see cref="StableSort"/>: the trivial lengths, the run shapes the run
/// finder treats specially (strictly descending, descending with ties, a lone last element), the
/// natural-order and comparer overloads, the key + payload form, and the two contracts that only a
/// deliberately misbehaving comparer can exercise — a comparer that throws, and one that is not a
/// total order, must both leave the span a permutation of its input.
/// </summary>
public class StableSortTests
{
    internal readonly struct ByKey : IComparer<(int Key, int Tag)>
    {
        public int Compare((int Key, int Tag) x, (int Key, int Tag) y) => x.Key.CompareTo(y.Key);
    }

    private readonly struct Descending : IComparer<int>
    {
        public int Compare(int x, int y) => y.CompareTo(x);
    }

    private readonly struct Ascending : IComparer<int>
    {
        public int Compare(int x, int y) => x.CompareTo(y);
    }

    private readonly struct ByLength : IComparer<string>
    {
        public int Compare(string? x, string? y) => x!.Length.CompareTo(y!.Length);
    }

    // Counts down a shared budget and throws when it runs out, so a test can stop the sort at any
    // chosen comparison: inside the run finder, the insertion sort, a bound search or either merge.
    private sealed class Budget
    {
        public int Remaining;
    }

    private readonly struct ThrowingComparer(Budget budget) : IComparer<int>
    {
        public int Compare(int x, int y)
        {
            if (--budget.Remaining < 0)
            {
                throw new InvalidOperationException("budget exhausted");
            }

            return x.CompareTo(y);
        }
    }

    // Answers at random: not a total order, not even antisymmetric.
    private readonly struct CoinFlipComparer(Random rand) : IComparer<int>
    {
        public int Compare(int x, int y) => rand.Next(3) - 1;
    }

    internal static (int Key, int Tag)[] Tagged(int[] keys) => keys.Select((k, i) => (k, i)).ToArray();

    internal static (int Key, int Tag)[] StableOracle((int Key, int Tag)[] items) => items.OrderBy(e => e.Key).ToArray();

    [Fact]
    public void Sort_ShouldDoNothing_OnEmptyAndSingleElementSpans()
    {
        StableSort.Sort(Span<int>.Empty);
        StableSort.Sort(Span<int>.Empty, Span<string>.Empty);

        int[] one = [42];
        string[] payload = ["x"];
        StableSort.Sort(one.AsSpan());
        StableSort.Sort(one.AsSpan(), payload.AsSpan());

        Assert.Equal([42], one);
        Assert.Equal(["x"], payload);
    }

    [Fact]
    public void Sort_ShouldOrderTwoElements()
    {
        int[] keys = [2, 1];
        StableSort.Sort(keys.AsSpan());
        Assert.Equal([1, 2], keys);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(33)]
    [InlineData(1_000)]
    public void Sort_ShouldMatchTheBclOrder_OnRandomInput(int length)
    {
        var rand = new Random(length);
        int[] keys = Enumerable.Range(0, length).Select(_ => rand.Next(-1_000, 1_000)).ToArray();
        int[] expected = keys.Order().ToArray();

        StableSort.Sort(keys.AsSpan());

        Assert.Equal(expected, keys);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(10_000)]
    public void Sort_ShouldLeaveSortedInputUntouched(int length)
    {
        // One run covering the whole input: no buffer is rented and nothing moves.
        int[] keys = Enumerable.Range(0, length).Select(i => i / 3).ToArray();
        int[] expected = (int[])keys.Clone();

        StableSort.Sort(keys.AsSpan());

        Assert.Equal(expected, keys);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(10_000)]
    public void Sort_ShouldReverseAStrictlyDescendingInput(int length)
    {
        int[] keys = Enumerable.Range(0, length).Reverse().ToArray();

        StableSort.Sort(keys.AsSpan());

        Assert.Equal(Enumerable.Range(0, length), keys);
    }

    [Fact]
    public void Sort_ShouldKeepTiesInOrder_WhenADescendingInputHasEqualNeighbours()
    {
        // Non-strictly descending: the run finder may only reverse the strictly descending pieces,
        // or the equal keys would swap.
        int[] keyValues = Enumerable.Range(0, 200).Select(i => (199 - i) / 4).ToArray();
        var items = Tagged(keyValues);
        var expected = StableOracle(items);

        StableSort.Sort(items.AsSpan(), default(ByKey));

        Assert.Equal(expected, items);
    }

    [Fact]
    public void Sort_ShouldKeepInputOrder_WhenEveryKeyIsEqual()
    {
        var items = Tagged(new int[500]);
        var expected = ((int Key, int Tag)[])items.Clone();

        StableSort.Sort(items.AsSpan(), default(ByKey));

        Assert.Equal(expected, items);
    }

    [Fact]
    public void Sort_ShouldPlaceALoneTrailingElement()
    {
        // 17 elements: the first run is extended to 16, which leaves one element as a run of its own.
        int[] keys = [.. Enumerable.Range(1, 16), 0];

        StableSort.Sort(keys.AsSpan());

        Assert.Equal(Enumerable.Range(0, 17), keys);
    }

    [Fact]
    public void Sort_ShouldMergeSortedBatches_StablyAcrossBatches()
    {
        // Eight sorted batches of a narrow key domain, so equal keys sit in different runs and the
        // merges, not the insertion sort, have to keep them in input order.
        var rand = new Random(7);
        var keyValues = new List<int>();
        for (int batch = 0; batch < 8; batch++)
        {
            keyValues.AddRange(Enumerable.Range(0, 300).Select(_ => rand.Next(40)).Order());
        }

        var items = Tagged([.. keyValues]);
        var expected = StableOracle(items);

        StableSort.Sort(items.AsSpan(), default(ByKey));

        Assert.Equal(expected, items);
    }

    [Fact]
    public void Sort_ShouldMergeAnAppendedTail_IntoASortedPrefix()
    {
        // A long sorted prefix and a short unsorted tail: the merge trims the in-place prefix and
        // buffers only the short side, merging from the high end.
        var rand = new Random(11);
        int[] keyValues = [.. Enumerable.Range(0, 5_000).Select(i => i / 2), .. Enumerable.Range(0, 200).Select(_ => rand.Next(2_500))];
        var items = Tagged(keyValues);
        var expected = StableOracle(items);

        StableSort.Sort(items.AsSpan(), default(ByKey));

        Assert.Equal(expected, items);
    }

    [Fact]
    public void Sort_ShouldMergeAShortHead_IntoASortedSuffix()
    {
        // The mirror image: a short unsorted head ahead of a long sorted run buffers the short left
        // side and merges from the low end.
        var rand = new Random(13);
        int[] keyValues = [.. Enumerable.Range(0, 200).Select(_ => rand.Next(2_500)), .. Enumerable.Range(0, 5_000).Select(i => i / 2)];
        var items = Tagged(keyValues);
        var expected = StableOracle(items);

        StableSort.Sort(items.AsSpan(), default(ByKey));

        Assert.Equal(expected, items);
    }

    [Fact]
    public void Sort_ShouldPutNullsFirst_UnderTheNaturalOrder()
    {
        string?[] keys = ["pear", null, "apple", null, "fig"];

        StableSort.Sort<string>(keys.AsSpan()!);

        Assert.Equal("<null>|<null>|apple|fig|pear", string.Join("|", keys.Select(k => k ?? "<null>")));
    }

    [Fact]
    public void Sort_ShouldFollowTheComparer()
    {
        var rand = new Random(3);
        int[] keys = Enumerable.Range(0, 300).Select(_ => rand.Next(100)).ToArray();
        int[] expected = keys.OrderDescending().ToArray();

        StableSort.Sort(keys.AsSpan(), default(Descending));

        Assert.Equal(expected, keys);
    }

    [Fact]
    public void Sort_ShouldKeepReferenceKeysThatCompareEqual_InInputOrder()
    {
        // Distinct instances that only the comparer calls equal: the sort must keep the instances in
        // input order, not merely produce equal-looking values. Large enough to rent (and clear) the
        // reference-typed merge buffer.
        var rand = new Random(5);
        string[] keys = Enumerable.Range(0, 2_000).Select(i => new string('x', rand.Next(1, 20)) + "#" + i).ToArray();
        string[] expected = keys.OrderBy(k => k.Length).ToArray();

        StableSort.Sort(keys.AsSpan(), default(ByLength));

        Assert.Equal(expected, keys);
        for (int i = 0; i < keys.Length; i++)
        {
            Assert.Same(expected[i], keys[i]);
        }
    }

    [Fact]
    public void SortWithPayload_ShouldMoveValuesWithTheirKeys_Stably()
    {
        var rand = new Random(17);
        int[] keys = Enumerable.Range(0, 3_000).Select(_ => rand.Next(50)).ToArray();
        string[] values = Enumerable.Range(0, keys.Length).Select(i => "v" + i).ToArray();
        var expected = keys.Zip(values).OrderBy(p => p.First).ToArray();

        StableSort.Sort(keys.AsSpan(), values.AsSpan());

        Assert.Equal(expected.Select(p => p.First), keys);
        Assert.Equal(expected.Select(p => p.Second), values);
    }

    [Fact]
    public void SortWithPayload_ShouldFollowTheComparer_AndReverseDescendingRunsInBoth()
    {
        int[] keys = Enumerable.Range(0, 100).ToArray();
        int[] values = Enumerable.Range(1_000, 100).ToArray();

        StableSort.Sort(keys.AsSpan(), values.AsSpan(), default(Descending));

        Assert.Equal(Enumerable.Range(0, 100).Reverse(), keys);
        Assert.Equal(Enumerable.Range(1_000, 100).Reverse(), values);
    }

    [Fact]
    public void SortWithPayload_ShouldLeaveValuesBeyondTheKeysAlone()
    {
        int[] keys = [3, 1, 2];
        int[] values = [30, 10, 20, 99, 98];

        StableSort.Sort(keys.AsSpan(), values.AsSpan());

        Assert.Equal([1, 2, 3], keys);
        Assert.Equal([10, 20, 30, 99, 98], values);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(200)]
    [InlineData(2_000)]
    public void Sort_ShouldLeaveAPermutation_WhenTheComparerThrows(int length)
    {
        // Stop the sort after every possible number of comparisons up to a full sort's worth, so the
        // throw lands in each phase: run finding, insertion, the bound searches and both merges.
        var rand = new Random(length);
        int[] source = Enumerable.Range(0, length).Select(_ => rand.Next(length / 2 + 1)).ToArray();
        int[] sortedSource = source.Order().ToArray();
        int step = Math.Max(1, length / 50);
        int budgetLimit = length * 16;
        bool completedOnce = false;

        for (int limit = 0; limit <= budgetLimit; limit += step)
        {
            int[] keys = (int[])source.Clone();
            var budget = new Budget { Remaining = limit };
            try
            {
                StableSort.Sort(keys.AsSpan(), new ThrowingComparer(budget));
                completedOnce = true;
                Assert.Equal(sortedSource, keys);
            }
            catch (InvalidOperationException ex) when (ex.Message == "budget exhausted")
            {
                Assert.Equal(sortedSource, keys.Order());
            }
        }

        Assert.True(completedOnce);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(2_000)]
    public void SortWithPayload_ShouldKeepKeysPairedWithValues_WhenTheComparerThrows(int length)
    {
        var rand = new Random(length + 1);
        int[] sourceKeys = Enumerable.Range(0, length).Select(_ => rand.Next(length / 4 + 1)).ToArray();
        var expectedPairs = sourceKeys.Select((k, i) => (k, i)).Order().ToArray();
        int step = Math.Max(1, length / 50);

        for (int limit = 0; limit <= length * 16; limit += step)
        {
            int[] keys = (int[])sourceKeys.Clone();
            int[] values = Enumerable.Range(0, length).ToArray();
            var budget = new Budget { Remaining = limit };
            try
            {
                StableSort.Sort(keys.AsSpan(), values.AsSpan(), new ThrowingComparer(budget));
            }
            catch (InvalidOperationException ex) when (ex.Message == "budget exhausted")
            {
            }

            // Values are the source indices, so each (key, value) pair must still be one of the
            // input's pairs, every pair exactly once.
            Assert.Equal(expectedPairs, keys.Zip(values).Select(p => (p.First, p.Second)).Order());
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Sort_ShouldLeaveAPermutation_WhenTheComparerIsNotATotalOrder(int seed)
    {
        // A comparer that answers at random can make either side of a merge run out first, which a
        // consistent comparer never does; neither may lose or duplicate an element.
        var rand = new Random(seed);
        foreach (int length in new[] { 40, 300, 3_000 })
        {
            int[] keys = Enumerable.Range(0, length).ToArray();
            StableSort.Sort(keys.AsSpan(), new CoinFlipComparer(rand));
            Assert.Equal(Enumerable.Range(0, length), keys.Order());

            // Each value starts equal to its key, so a pair moved apart shows up as a mismatch.
            int[] values = (int[])keys.Clone();
            StableSort.Sort(keys.AsSpan(), values.AsSpan(), new CoinFlipComparer(rand));
            Assert.Equal(Enumerable.Range(0, length), keys.Order());
            Assert.Equal(keys, values);
        }
    }

    [Fact]
    public void Sort_ShouldNotAllocate_OnceThePoolIsWarm()
    {
        var rand = new Random(19);
        int[] source = Enumerable.Range(0, 4_096).Select(_ => rand.Next(1_000)).ToArray();
        int[] keys = new int[source.Length];
        string[] values = Enumerable.Range(0, source.Length).Select(i => i.ToString()).ToArray();

        void SortAll()
        {
            // An explicit struct comparer, not the natural-order overload: in a Debug build the
            // natural-order comparer's null test boxes an int argument, which a Release build does not.
            source.CopyTo(keys, 0);
            StableSort.Sort(keys.AsSpan(), default(Ascending));
            source.CopyTo(keys, 0);
            StableSort.Sort(keys.AsSpan(), values.AsSpan(), default(Ascending));
        }

        SortAll();

        Assert.InRange(AllocationProbe.Quietest(SortAll, windows: 30), 0, 256);
    }
}
