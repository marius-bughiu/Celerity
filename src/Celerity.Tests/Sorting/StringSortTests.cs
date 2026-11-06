using Celerity.Sorting;

namespace Celerity.Tests.Sorting;

/// <summary>
/// Hand-written fixtures for <see cref="StringSort"/>: the ordinal contract (code-unit order, proper
/// prefixes first, nulls first, no culture), stability across all three entry points, the packed
/// window's edge cases (strings that end inside it, literal U+0000, the top of the code-unit range),
/// and the shapes that stress the partition loop rather than the comparisons — long shared
/// prefixes, mass duplicates, and an input built to defeat the median-of-three pivot.
/// </summary>
public class StringSortTests
{
    [Fact]
    public void Sort_ShouldDoNothing_WhenTheSpanIsEmptyOrSingle()
    {
        string?[] empty = [];
        StringSort.Sort(empty.AsSpan()!);
        Assert.Empty(empty);

        string?[] single = ["only"];
        StringSort.Sort(single.AsSpan()!);
        AssertKeys(["only"], single);
    }

    [Fact]
    public void Sort_ShouldOrderByCodeUnit_NotByCulture()
    {
        // Ordinal: every upper-case ASCII letter precedes every lower-case one, and 'é' (U+00E9)
        // follows 'z'. A culture-aware sort would interleave them.
        string?[] keys = ["banana", "Apple", "apple", "Banana", "éclair", "zebra", "Zebra"];

        StringSort.Sort(keys.AsSpan()!);

        AssertKeys(["Apple", "Banana", "Zebra", "apple", "banana", "zebra", "éclair"], keys);
    }

    [Fact]
    public void Sort_ShouldPutNullsFirst_BeforeTheEmptyString()
    {
        string?[] keys = ["b", null, "", "a", null];

        StringSort.Sort(keys.AsSpan()!);

        AssertKeys([null, null, "", "a", "b"], keys);
    }

    [Fact]
    public void Sort_ShouldOrderAProperPrefixBeforeItsExtensions()
    {
        string?[] keys = ["abcde", "abc", "abcd", "ab", "abcdef", "a", "abcdefgh", "abcdefg"];

        StringSort.Sort(keys.AsSpan()!);

        AssertKeys(["a", "ab", "abc", "abcd", "abcde", "abcdef", "abcdefg", "abcdefgh"], keys);
    }

    [Fact]
    public void Sort_ShouldOrderStringsThatEndInsideTheWindowAgainstLiteralNuls()
    {
        // A lane past the end of a string packs as zero, and so does a literal U+0000. Every pair
        // here collides on at least one packed window and must still come out ordinal.
        string?[] keys =
        [
            "a\0b", "a", "a\0\0\0\0", "a\0", "a\u0001", "a\0\0\0", "a\0\0", "a\0\0\0\0x", "a\0\0\0x", "",
            "\0", "\0\0\0\0", "\0\0\0\0\0", "\0\0\0", "a\0\0\0\0\0\0\0\0",
        ];
        string?[] expected = OrdinalStable(keys);

        StringSort.Sort(keys.AsSpan()!);

        Assert.Equal(expected, keys);
    }

    [Fact]
    public void Sort_ShouldOrderTheTopOfTheCodeUnitRangeLast()
    {
        string?[] keys = ["￿", "￾￿", "￿￿", "😀", "z", "￿\0"];
        string?[] expected = OrdinalStable(keys);

        StringSort.Sort(keys.AsSpan()!);

        Assert.Equal(expected, keys);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(5_000)]
    public void Sort_ShouldBeStable_WhenEqualStringsAreDistinctInstances(int length)
    {
        // Equal strings are distinguishable only by reference, so reference order is the only
        // observable trace of stability for the keys-only form. 50 runs on the stack buffer and
        // 5,000 on the rented one.
        var rand = new Random(7);
        string?[] keys = new string?[length];
        for (int i = 0; i < length; i++)
        {
            keys[i] = new string((char)('a' + rand.Next(3)), 1 + rand.Next(3));
        }

        string?[] expected = OrdinalStable(keys);

        StringSort.Sort(keys.AsSpan()!);

        for (int i = 0; i < length; i++)
        {
            Assert.Same(expected[i], keys[i]);
        }
    }

    [Fact]
    public void Sort_ShouldMovePayloadsWithTheirKeys_AndKeepEqualKeysPayloadsInInputOrder()
    {
        string?[] keys = ["pear", "apple", null, "pear", "fig", "apple", null, "pear"];
        int[] values = [0, 1, 2, 3, 4, 5, 6, 7];

        StringSort.Sort(keys.AsSpan()!, values.AsSpan());

        AssertKeys([null, null, "apple", "apple", "fig", "pear", "pear", "pear"], keys);
        Assert.Equal([2, 6, 1, 5, 4, 0, 3, 7], values);
    }

    [Fact]
    public void Sort_ShouldLeaveThePayloadTailUntouched_WhenThePayloadIsLongerThanTheKeys()
    {
        string?[] keys = ["c", "a", "b"];
        int[] values = [0, 1, 2, 99, 100];

        StringSort.Sort(keys.AsSpan()!, values.AsSpan());

        AssertKeys(["a", "b", "c"], keys);
        Assert.Equal([1, 2, 0, 99, 100], values);
    }

    [Fact]
    public void Sort_ShouldDoNothing_WhenAPayloadSortHasFewerThanTwoKeys()
    {
        string?[] keys = ["only"];
        int[] values = [42, 43];

        StringSort.Sort(keys.AsSpan()!, values.AsSpan());

        AssertKeys(["only"], keys);
        Assert.Equal([42, 43], values);
    }

    [Fact]
    public void Sort_ShouldAcceptAStringPayload_WhenItIsADisjointSliceOfTheSameArray()
    {
        string?[] buffer = ["b", "a", "c", "payload-b", "payload-a", "payload-c"];

        StringSort.Sort(buffer.AsSpan(0, 3)!, buffer.AsSpan(3, 3));

        AssertKeys(["a", "b", "c", "payload-a", "payload-b", "payload-c"], buffer);
    }

    [Fact]
    public void Sort_ShouldMovePayloads_WhenTheSortRentsItsWorkingState()
    {
        const int Length = 3_000;
        var rand = new Random(11);
        string?[] keys = new string?[Length];
        long[] values = new long[Length];
        for (int i = 0; i < Length; i++)
        {
            keys[i] = "item-" + rand.Next(500).ToString("D4");
            values[i] = i;
        }

        int[] expected = OrdinalStableOrder(keys);
        string?[] original = (string?[])keys.Clone();

        StringSort.Sort(keys.AsSpan()!, values.AsSpan());

        for (int i = 0; i < Length; i++)
        {
            Assert.Equal(expected[i], values[i]);
            Assert.Same(original[expected[i]], keys[i]);
        }
    }

    [Theory]
    [InlineData(200)]
    [InlineData(5_000)]
    public void Sort_ShouldNotAllocate_OnceThePoolIsWarm(int length)
    {
        // Duplicate-heavy, so every entry point runs the ended-string split many times per call, on
        // groups both small (insertion sort) and large (pooled radix of positions). A boxed comparer
        // or a fresh buffer anywhere in that path costs bytes per group, and 20 sorts of a few hundred
        // groups each would show up as kilobytes. 200 keys keep their entries on the stack buffer
        // (the duplicate runs still rent position buffers), 5,000 use the pooled one. The budget absorbs one-off runtime noise; the quietest of three windows absorbs
        // a stray GC-internal allocation landing in one of them.
        var rand = new Random(5);
        string[] distinct = Enumerable.Range(0, 40).Select(i => "value-" + i.ToString("D3")).ToArray();
        string[] source = new string[length];
        for (int i = 0; i < length; i++)
        {
            source[i] = distinct[rand.Next(distinct.Length)];
        }

        string[] keys = new string[length];
        int[] values = new int[length];
        int[] indices = new int[length];

        void SortAll()
        {
            source.CopyTo(keys, 0);
            StringSort.Sort(keys.AsSpan());
            source.CopyTo(keys, 0);
            StringSort.Sort(keys.AsSpan(), values.AsSpan());
            StringSort.ArgSort(source, indices.AsSpan());
        }

        SortAll();

        long quietest = long.MaxValue;
        for (int window = 0; window < 3; window++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20; i++)
            {
                SortAll();
            }

            quietest = Math.Min(quietest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.InRange(quietest, 0, 512);
    }

    [Fact]
    public void AllEntryPoints_ShouldKeepInputOrder_WhenTheInputIsAlreadyOrdered()
    {
        // Already ordered, with nulls, duplicates (distinct instances) and a prefix pair: the
        // ordered-input scan returns early, and the identity is the stable answer.
        string?[] keys = [null, null, "", "a", new string('b', 2), new string('b', 2), "bb\0", "c"];
        string?[] original = (string?[])keys.Clone();
        int[] values = [0, 1, 2, 3, 4, 5, 6, 7];
        int[] indices = new int[keys.Length];

        StringSort.Sort(keys.AsSpan()!);
        for (int i = 0; i < keys.Length; i++)
        {
            Assert.Same(original[i], keys[i]);
        }

        StringSort.Sort(keys.AsSpan()!, values.AsSpan());
        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7], values);

        StringSort.ArgSort(keys!, indices.AsSpan());
        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7], indices);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(3_000)]
    public void AllEntryPoints_ShouldSort_WhenOnlyTheLastPairIsOutOfOrder(int length)
    {
        // The ordered-input scan runs to the very end before it finds the inversion, and the sort
        // must then still run in full.
        string?[] keys = Enumerable.Range(0, length).Select(i => "key:" + i.ToString("D5")).ToArray();
        (keys[^1], keys[^2]) = (keys[^2], keys[^1]);
        int[] expected = OrdinalStableOrder(keys);

        int[] indices = new int[length];
        StringSort.ArgSort(keys!, indices.AsSpan());
        Assert.Equal(expected, indices);

        string?[] pairKeys = (string?[])keys.Clone();
        int[] values = Enumerable.Range(0, length).ToArray();
        StringSort.Sort(pairKeys.AsSpan()!, values.AsSpan());
        Assert.Equal(expected, values);

        StringSort.Sort(keys.AsSpan()!);
        AssertKeys(Enumerable.Range(0, length).Select(i => "key:" + i.ToString("D5")), keys);
    }

    [Fact]
    public void ArgSort_ShouldRankWithoutReorderingTheKeys()
    {
        string?[] keys = ["delta", "alpha", null, "charlie", "alpha", "bravo"];
        string?[] snapshot = (string?[])keys.Clone();
        int[] indices = new int[keys.Length];

        StringSort.ArgSort(keys!, indices.AsSpan());

        Assert.Equal([2, 1, 4, 5, 3, 0], indices);
        Assert.Equal(snapshot, keys);
    }

    [Fact]
    public void ArgSort_ShouldHandleEmptyAndSingleKeySpans()
    {
        int[] none = [-1];
        StringSort.ArgSort(ReadOnlySpan<string>.Empty, none.AsSpan());
        Assert.Equal([-1], none);

        int[] one = [-1, -1];
        StringSort.ArgSort(new[] { "x" }, one.AsSpan());
        Assert.Equal([0, -1], one);
    }

    [Fact]
    public void ArgSort_ShouldListEqualKeysInAscendingIndexOrder_WhenEveryKeyIsTheSame()
    {
        const int Length = 2_000;
        string?[] keys = new string?[Length];
        for (int i = 0; i < Length; i++)
        {
            keys[i] = new string('k', 6);
        }

        int[] indices = new int[Length];
        StringSort.ArgSort(keys!, indices.AsSpan());

        Assert.Equal(Enumerable.Range(0, Length), indices);
    }

    [Fact]
    public void Sort_ShouldOrderKeysThatShareAVeryLongPrefix()
    {
        // Thousands of characters of shared prefix means more than a thousand windows. Each one is a
        // pass of the partition loop, and none of them may cost a stack frame.
        string prefix = new('p', 6_000);
        var rand = new Random(3);
        string?[] keys = new string?[400];
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i] = prefix + rand.Next(1_000).ToString("D3") + (i % 3 == 0 ? "" : "tail");
        }

        string?[] expected = OrdinalStable(keys);

        StringSort.Sort(keys.AsSpan()!);

        Assert.Equal(expected, keys);
    }

    [Fact]
    public void Sort_ShouldOrderKeysThatShareALongRunOfNuls()
    {
        // Every window over a run of U+0000 has a zero last lane, so every level takes the
        // ended-string split. That split must also loop, not recurse, or a long run overflows the
        // stack.
        string run = new('\0', 8_000);
        string?[] keys = new string?[300];
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i] = run[..(7_000 + (i % 7))] + (char)('a' + (i % 5));
        }

        string?[] expected = OrdinalStable(keys);

        StringSort.Sort(keys.AsSpan()!);

        Assert.Equal(expected, keys);
    }

    [Fact]
    public void Sort_ShouldStayCorrect_WhenTheInputDefeatsTheMedianOfThreePivot()
    {
        // The adversary below makes every partition peel off just two keys, so partitioning alone
        // would take quadratic time. Long before that, the depth budget runs out and the remaining
        // range goes to the heap-sort fallback. This test is what reaches that fallback: if the pivot
        // rule or the partition changes, regenerate the adversary to match, or the coverage gate
        // reports the fallback as unreached.
        const int Length = 4_000;
        string?[] keys = MedianOfThreeKiller(Length);
        string?[] expected = OrdinalStable(keys);

        StringSort.Sort(keys.AsSpan()!);

        Assert.Equal(expected, keys);
    }

    [Fact]
    public void ArgSort_ShouldStayStable_WhenTheInputDefeatsTheMedianOfThreePivot()
    {
        string?[] keys = MedianOfThreeKiller(3_000);
        int[] indices = new int[keys.Length];

        StringSort.ArgSort(keys!, indices.AsSpan());

        Assert.Equal(OrdinalStableOrder(keys), indices);
    }

    // xUnit's span-typed Assert.Equal overload requires IEquatable<T>, which string? does not
    // satisfy under nullable analysis, so compare as sequences.
    internal static void AssertKeys(IEnumerable<string?> expected, string?[] actual) =>
        Assert.Equal(expected, actual.AsEnumerable());

    internal static string?[] OrdinalStable(string?[] keys) =>
        OrdinalStableOrder(keys).Select(i => keys[i]).ToArray();

    // LINQ's OrderBy is stable, so ties come out in ascending input position: the order every
    // StringSort entry point promises.
    internal static int[] OrdinalStableOrder(string?[] keys) =>
        Enumerable.Range(0, keys.Length).OrderBy(i => keys[i], StringComparer.Ordinal).ToArray();

    /// <summary>
    /// Builds single-character keys that make StringSort's median-of-three pivot the second-smallest
    /// key of every range it partitions, by replaying the partition step on unvalued placeholders
    /// ("gas", after McIlroy's adversary) and fixing a placeholder's value only once the step reads it.
    /// </summary>
    /// <remarks>
    /// The replay mirrors <c>StringSort.Partition</c> exactly: the pivot is the median of the
    /// first, middle and last packed keys, and the partition is a Dutch-national-flag sweep. Each
    /// round fixes the first entry to the next-smallest value and the middle one to the value after
    /// it, so the median is the middle one, which the last (still unvalued, so larger) entry cannot
    /// displace. The sweep then sends one entry left, one to the middle, and every other entry right.
    /// The sort continues with the largest part, which is that right side again.
    /// </remarks>
    private static string?[] MedianOfThreeKiller(int length)
    {
        const int Gas = int.MaxValue;
        const int InsertionThreshold = 16;

        int[] value = Enumerable.Repeat(Gas, length).ToArray();
        int[] slot = Enumerable.Range(0, length).ToArray(); // slot[position] = original index
        int next = 0;
        int start = 0;
        int end = length;

        while (end - start > InsertionThreshold)
        {
            int count = end - start;
            value[slot[start]] = next;
            value[slot[start + (count >> 1)]] = next + 1;
            int pivot = next + 1;
            next += 2;

            int less = start;
            int i = start;
            int greater = end - 1;
            while (i <= greater)
            {
                int key = value[slot[i]];
                if (key < pivot)
                {
                    (slot[less], slot[i]) = (slot[i], slot[less]);
                    less++;
                    i++;
                }
                else if (key > pivot)
                {
                    (slot[greater], slot[i]) = (slot[i], slot[greater]);
                    greater--;
                }
                else
                {
                    i++;
                }
            }

            start = greater + 1;
        }

        string?[] keys = new string?[length];
        for (int i = 0; i < length; i++)
        {
            int v = value[i] == Gas ? next++ : value[i];
            keys[i] = ((char)(v + 1)).ToString();
        }

        return keys;
    }
}
