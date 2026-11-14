using Celerity.Sorting;

namespace Celerity.Tests.Sorting;

/// <summary>
/// Behavioural tests for <see cref="KWayMerge"/> — the span merge over packed runs and the lazy
/// merge over sequences, in both the natural-order and struct-comparer forms: the degenerate run
/// counts (none, one, two, the tree path, past the stack limit), empty and exhausted runs,
/// stability across and within runs, and the lazy form's laziness and disposal contract.
/// </summary>
public class KWayMergeTests
{
    /// <summary>Descending order, to exercise the comparer-parameterized overloads.</summary>
    private readonly struct DescendingComparer : IComparer<int>
    {
        public int Compare(int x, int y) => y.CompareTo(x);
    }

    /// <summary>Orders records by key only, so equal keys expose whether the merge kept their order.</summary>
    private readonly struct ByKey : IComparer<(int Key, int Tag)>
    {
        public int Compare((int Key, int Tag) x, (int Key, int Tag) y) => x.Key.CompareTo(y.Key);
    }

    /// <summary>Ordinal string order as a struct comparer.</summary>
    private readonly struct Ordinal : IComparer<string>
    {
        public int Compare(string? x, string? y) => string.CompareOrdinal(x, y);
    }

    private static int[] Concat(params int[][] runs) => runs.SelectMany(r => r).ToArray();

    private static int[] Lengths(params int[][] runs) => runs.Select(r => r.Length).ToArray();

    // ---- Span merge: shapes -------------------------------------------------------------------

    [Fact]
    public void Merge_ShouldDoNothing_WhenThereAreNoRuns()
    {
        int[] destination = [7, 7];

        KWayMerge.Merge<int>(ReadOnlySpan<int>.Empty, ReadOnlySpan<int>.Empty, destination);

        Assert.Equal([7, 7], destination);
    }

    [Fact]
    public void Merge_ShouldDoNothing_WhenEveryRunIsEmpty()
    {
        int[] destination = [7];

        KWayMerge.Merge<int>(ReadOnlySpan<int>.Empty, [0, 0, 0, 0], destination);

        Assert.Equal([7], destination);
    }

    [Fact]
    public void Merge_ShouldCopyTheRun_WhenThereIsOneRun()
    {
        int[] destination = new int[4];

        KWayMerge.Merge<int>([1, 2, 3, 4], [4], destination);

        Assert.Equal([1, 2, 3, 4], destination);
    }

    [Theory]
    [InlineData(new[] { 1, 4, 9 }, new[] { 2, 3, 10, 11 })]
    [InlineData(new[] { 5, 6, 7 }, new[] { 1, 2 })]
    [InlineData(new int[0], new[] { 1, 2 })]
    [InlineData(new[] { 1, 2 }, new int[0])]
    public void Merge_ShouldInterleave_WhenThereAreTwoRuns(int[] left, int[] right)
    {
        int[] destination = new int[left.Length + right.Length];

        KWayMerge.Merge<int>(Concat(left, right), Lengths(left, right), destination);

        Assert.Equal(left.Concat(right).Order(), destination);
    }

    [Fact]
    public void Merge_ShouldMergeThroughTheTree_WhenThereAreSeveralRunsOfUnequalLength()
    {
        int[][] runs = [[3, 8, 20], [], [1], [2, 4, 6, 8, 10, 12], [5, 5, 5], [], [0, 30]];
        int[] destination = new int[runs.Sum(r => r.Length)];

        KWayMerge.Merge<int>(Concat(runs), Lengths(runs), destination);

        Assert.Equal(runs.SelectMany(r => r).Order(), destination);
    }

    [Fact]
    public void Merge_ShouldLeaveTheTailUntouched_WhenTheDestinationIsLonger()
    {
        int[] destination = [-1, -1, -1, -1, -1, 99];

        KWayMerge.Merge<int>([2, 4, 1, 5, 3], [2, 2, 1], destination);

        Assert.Equal([1, 2, 3, 4, 5, 99], destination);
    }

    [Fact]
    public void Merge_ShouldNotModifyTheSource()
    {
        int[] source = [1, 5, 2, 6, 3, 7];
        int[] before = (int[])source.Clone();

        KWayMerge.Merge<int>(source, [2, 2, 2], new int[6]);

        Assert.Equal(before, source);
    }

    [Theory]
    [InlineData(129)]
    [InlineData(300)]
    public void Merge_ShouldMerge_WhenThereAreMoreRunsThanFitOnTheStack(int runCount)
    {
        // Run r holds r, r + runCount, r + 2·runCount, ... so the merged output is 0 .. n-1 and
        // every output step switches run — the replay path is exercised on every element.
        const int PerRun = 5;
        int[] source = new int[runCount * PerRun];
        int[] lengths = new int[runCount];
        for (int r = 0; r < runCount; r++)
        {
            lengths[r] = PerRun;
            for (int i = 0; i < PerRun; i++)
            {
                source[r * PerRun + i] = r + i * runCount;
            }
        }

        int[] destination = new int[source.Length];

        KWayMerge.Merge<int>(source, lengths, destination);

        Assert.Equal(Enumerable.Range(0, source.Length), destination);
    }

    [Fact]
    public void Merge_ShouldFollowTheComparer_WhenOneIsSupplied()
    {
        int[][] runs = [[9, 5, 1], [8, 2], [7, 6, 3]];
        int[] destination = new int[8];

        KWayMerge.Merge(Concat(runs), Lengths(runs), destination.AsSpan(), default(DescendingComparer));

        Assert.Equal([9, 8, 7, 6, 5, 3, 2, 1], destination);
    }

    [Fact]
    public void Merge_ShouldMergeReferenceTypes_WhenKeysAreStrings()
    {
        string[] source = ["apple", "kiwi", "pear", "banana", "fig", "cherry", "grape"];
        string[] destination = new string[source.Length];

        KWayMerge.Merge(source, [3, 2, 2], destination.AsSpan(), default(Ordinal));

        Assert.Equal(source.Order(StringComparer.Ordinal), destination);
    }

    [Fact]
    public void Merge_ShouldStillProduceAPermutation_WhenARunIsNotSorted()
    {
        int[] source = [9, 1, 5, 8, 2, 7, 3];
        int[] destination = new int[source.Length];

        KWayMerge.Merge<int>(source, [2, 3, 2], destination);

        Assert.Equal(source.Order(), destination.Order());
    }

    // ---- Span merge: stability ----------------------------------------------------------------

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(150)]
    public void Merge_ShouldBeStable_WhenEveryKeyIsEqual(int runCount)
    {
        // Every element compares equal, so the only correct output is the input in run order —
        // which is the input itself. Any tie that went to the higher run shows up as a reordering.
        var source = new (int Key, int Tag)[runCount * 3];
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = (42, i);
        }

        var destination = new (int Key, int Tag)[source.Length];

        KWayMerge.Merge(source, Enumerable.Repeat(3, runCount).ToArray(), destination.AsSpan(), default(ByKey));

        Assert.Equal(source, destination);
    }

    [Fact]
    public void Merge_ShouldBeStable_WhenEqualKeysAreSpreadAcrossRuns()
    {
        (int Key, int Tag)[][] runs =
        [
            [(1, 0), (3, 1), (3, 2)],
            [(1, 3), (2, 4), (3, 5)],
            [(0, 6), (3, 7)],
            [(1, 8)],
        ];
        var source = runs.SelectMany(r => r).ToArray();
        var destination = new (int Key, int Tag)[source.Length];

        KWayMerge.Merge(source, runs.Select(r => r.Length).ToArray(), destination.AsSpan(), default(ByKey));

        // LINQ's OrderBy is stable, and the concatenation lists every run in order, so it is the
        // exact expected output: equal keys by run, then by position within the run.
        Assert.Equal(source.OrderBy(e => e.Key), destination);
    }

    // ---- Lazy merge ---------------------------------------------------------------------------

    [Fact]
    public void LazyMerge_ShouldYieldNothing_WhenThereAreNoSources()
    {
        Assert.Empty(KWayMerge.Merge(Array.Empty<IEnumerable<int>>()));
    }

    [Fact]
    public void LazyMerge_ShouldYieldTheSource_WhenThereIsOneSource()
    {
        Assert.Equal([1, 2, 3], KWayMerge.Merge(new[] { new[] { 1, 2, 3 } }));
    }

    [Fact]
    public void LazyMerge_ShouldYieldNothing_WhenTheOnlySourceIsEmpty()
    {
        Assert.Empty(KWayMerge.Merge(new[] { Array.Empty<int>() }));
    }

    [Fact]
    public void LazyMerge_ShouldMerge_WhenSourcesAreMixedLengthsAndSomeAreEmpty()
    {
        int[][] runs = [[3, 8, 20], [], [1], [2, 4, 6, 8, 10, 12], [5, 5, 5], [], [0, 30]];

        Assert.Equal(runs.SelectMany(r => r).Order(), KWayMerge.Merge(runs));
    }

    [Fact]
    public void LazyMerge_ShouldFollowTheComparer_WhenOneIsSupplied()
    {
        int[][] runs = [[9, 5, 1], [8, 2], [7, 6, 3]];

        Assert.Equal([9, 8, 7, 6, 5, 3, 2, 1], KWayMerge.Merge(runs, default(DescendingComparer)));
    }

    [Fact]
    public void LazyMerge_ShouldBeStable_WhenEqualKeysAreSpreadAcrossRuns()
    {
        (int Key, int Tag)[][] runs =
        [
            [(1, 0), (3, 1), (3, 2)],
            [(1, 3), (2, 4), (3, 5)],
            [(0, 6), (3, 7)],
            [(1, 8)],
            [(3, 9), (3, 10)],
        ];

        Assert.Equal(runs.SelectMany(r => r).OrderBy(e => e.Key), KWayMerge.Merge(runs, default(ByKey)));
    }

    [Fact]
    public void LazyMerge_ShouldMerge_WhenTheSourcesAreInfiniteAndOnlyAPrefixIsTaken()
    {
        static IEnumerable<int> Multiples(int step)
        {
            for (int i = step; ; i += step)
            {
                yield return i;
            }
        }

        IEnumerable<int> merged = KWayMerge.Merge(new[] { Multiples(2), Multiples(3), Multiples(5) });

        Assert.Equal([2, 3, 4, 5, 6, 6, 8, 9, 10, 10], merged.Take(10));
    }

    [Fact]
    public void LazyMerge_ShouldNotTouchTheSources_UntilEnumerated()
    {
        var source = new TrackingSequence([1, 2]);

        IEnumerable<int> merged = KWayMerge.Merge(new IEnumerable<int>[] { source });

        Assert.Equal(0, source.EnumeratorsCreated);
        Assert.Equal([1, 2], merged);
        Assert.Equal(1, source.EnumeratorsCreated);
    }

    [Fact]
    public void LazyMerge_ShouldReenumerateTheSources_WhenEnumeratedTwice()
    {
        var source = new TrackingSequence([1, 2]);
        IEnumerable<int> merged = KWayMerge.Merge(new IEnumerable<int>[] { source, new[] { 0 } });

        Assert.Equal([0, 1, 2], merged);
        Assert.Equal([0, 1, 2], merged);
        Assert.Equal(2, source.EnumeratorsCreated);
    }

    [Fact]
    public void LazyMerge_ShouldReadTheOuterSequenceOnce_WhenCalled()
    {
        var outer = new List<IEnumerable<int>> { new[] { 2 }, new[] { 1 } };

        IEnumerable<int> merged = KWayMerge.Merge(outer);
        outer.Add(new[] { 0 });

        Assert.Equal([1, 2], merged);
    }

    [Theory]
    [InlineData(5)] // counted before two sources were removed: a CopyTo snapshot ends in two nulls
    [InlineData(2)] // counted before one was added: CopyTo has too little room and throws
    [InlineData(0)]
    public void LazyMerge_ShouldMergeExactlyTheSourcesYielded_WhenACountedOuterSequenceHasAStaleCount(int count)
    {
        // Regression (#621): the outer sequence was snapshotted through Count and CopyTo, so a shrinking one was
        // rejected for "null" sequences the caller never passed, and a growing one threw from CopyTo.
        var outer = new StaleCountCollection<IEnumerable<int>>(count, new[] { 1, 4 }, new[] { 2 }, new[] { 0, 3 });

        Assert.Equal([0, 1, 2, 3, 4], KWayMerge.Merge(outer));
    }

    [Fact]
    public void LazyMerge_ShouldStillRejectANullSource_WhenACountedOuterSequenceIsEnumerated()
    {
        var outer = new StaleCountCollection<IEnumerable<int>>(9, new[] { 1 }, null!);

        var ex = Assert.Throws<ArgumentException>(() => KWayMerge.Merge(outer));
        Assert.Equal("sources", ex.ParamName);
        Assert.Contains("index 1", ex.Message);
    }

    [Fact]
    public void LazyMerge_ShouldReadAnUncountedOuterSequence()
    {
        static IEnumerable<IEnumerable<int>> Outer()
        {
            yield return new[] { 2, 5 };
            yield return new[] { 1, 6 };
        }

        Assert.Equal([1, 2, 5, 6], KWayMerge.Merge(Outer()));
    }

    [Fact]
    public void LazyMerge_ShouldDisposeEverySource_WhenEnumerationCompletes()
    {
        var sources = new[] { new TrackingSequence([1, 3]), new TrackingSequence([2]), new TrackingSequence([]) };

        Assert.Equal([1, 2, 3], KWayMerge.Merge<int>(sources));

        Assert.All(sources, s => Assert.Equal(1, s.EnumeratorsDisposed));
    }

    [Fact]
    public void LazyMerge_ShouldDisposeEverySource_WhenEnumerationStopsEarly()
    {
        var sources = new[] { new TrackingSequence([1, 3]), new TrackingSequence([2, 4]), new TrackingSequence([5]) };

        foreach (int value in KWayMerge.Merge<int>(sources))
        {
            if (value == 2)
            {
                break;
            }
        }

        Assert.All(sources, s => Assert.Equal(1, s.EnumeratorsDisposed));
    }

    [Fact]
    public void LazyMerge_ShouldDisposeTheOpenedSources_WhenALaterSourceThrowsOnGetEnumerator()
    {
        var first = new TrackingSequence([1]);
        var second = new TrackingSequence([2]);
        IEnumerable<int> merged = KWayMerge.Merge(new IEnumerable<int>[] { first, second, new ThrowingSequence() });

        Assert.Throws<InvalidOperationException>(() => merged.ToList());

        Assert.Equal(1, first.EnumeratorsDisposed);
        Assert.Equal(1, second.EnumeratorsDisposed);
    }

    [Fact]
    public void LazyMerge_ShouldDisposeTheRemainingSources_WhenOneDisposeThrows()
    {
        // Two elements each, and the merge is abandoned after the first, so no source has run out
        // and all three are released together by the merge's own Dispose.
        var first = new TrackingSequence([1, 4], throwOnDispose: true);
        var second = new TrackingSequence([2, 5]);
        var third = new TrackingSequence([3, 6]);
        IEnumerator<int> e = KWayMerge.Merge<int>(new[] { first, second, third }).GetEnumerator();
        Assert.True(e.MoveNext());

        var ex = Assert.Throws<InvalidOperationException>(e.Dispose);

        Assert.Equal("dispose 1", ex.Message);
        Assert.Equal(1, second.EnumeratorsDisposed);
        Assert.Equal(1, third.EnumeratorsDisposed);
    }

    [Fact]
    public void LazyMerge_ShouldReportTheFirstDisposeFailure_WhenSeveralDisposesThrow()
    {
        var first = new TrackingSequence([1, 4], throwOnDispose: true);
        var second = new TrackingSequence([2, 5], throwOnDispose: true);
        var third = new TrackingSequence([3, 6]);
        IEnumerator<int> e = KWayMerge.Merge<int>(new[] { first, second, third }).GetEnumerator();
        Assert.True(e.MoveNext());

        var ex = Assert.Throws<InvalidOperationException>(e.Dispose);

        Assert.Equal("dispose 1", ex.Message);
        Assert.Equal(1, second.EnumeratorsDisposed);
        Assert.Equal(1, third.EnumeratorsDisposed);
    }

    [Fact]
    public void LazyMerge_ShouldDisposeEachSource_AsSoonAsItRunsOut()
    {
        var shortRun = new TrackingSequence([1]);
        var empty = new TrackingSequence([]);
        var longRun = new TrackingSequence([2, 3, 4]);
        IEnumerator<int> e = KWayMerge.Merge<int>(new[] { shortRun, empty, longRun }).GetEnumerator();

        Assert.True(e.MoveNext());
        Assert.Equal(1, empty.EnumeratorsDisposed);
        Assert.Equal(0, shortRun.EnumeratorsDisposed);

        Assert.True(e.MoveNext());
        Assert.Equal(1, shortRun.EnumeratorsDisposed);
        Assert.Equal(0, longRun.EnumeratorsDisposed);

        while (e.MoveNext())
        {
        }

        // Drained without ever disposing the merge: every source is already released.
        Assert.Equal(1, longRun.EnumeratorsDisposed);
        e.Dispose();
        Assert.Equal(1, longRun.EnumeratorsDisposed);
    }

    [Fact]
    public void LazyMerge_ShouldReleaseTheOpenedSourcesAndStaySpent_WhenStartingFails()
    {
        var first = new TrackingSequence([1]);
        IEnumerator<int> e = KWayMerge.Merge(new IEnumerable<int>[] { first, new ThrowingSequence() }).GetEnumerator();

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());
        Assert.Equal(1, first.EnumeratorsDisposed);

        // A retry neither reopens the sources nor yields anything.
        Assert.False(e.MoveNext());
        Assert.Equal(1, first.EnumeratorsCreated);
    }

    [Fact]
    public void LazyMerge_ShouldReleaseEverySourceAndStaySpent_WhenASourceThrowsMidMerge()
    {
        static IEnumerable<int> FailsAfterOne()
        {
            yield return 1;
            throw new InvalidOperationException("source failed");
        }

        var healthy = new TrackingSequence([2, 3]);
        IEnumerator<int> e = KWayMerge.Merge(new IEnumerable<int>[] { FailsAfterOne(), healthy }).GetEnumerator();
        Assert.True(e.MoveNext());

        Assert.Throws<InvalidOperationException>(() => e.MoveNext());

        // Released by the failure itself, before the consumer disposes anything.
        Assert.Equal(1, healthy.EnumeratorsDisposed);
        Assert.False(e.MoveNext());
    }

    [Fact]
    public void LazyMerge_ShouldAttemptEveryDispose_WhenManySourcesThrowOnDispose()
    {
        // Wide enough that a recursive retry per failure would be deep; the first failure wins.
        var sources = Enumerable.Range(1, 5_000).Select(i => new TrackingSequence([i, i + 10_000], throwOnDispose: true)).ToArray();
        IEnumerator<int> e = KWayMerge.Merge<int>(sources).GetEnumerator();
        Assert.True(e.MoveNext());

        var ex = Assert.Throws<InvalidOperationException>(e.Dispose);

        Assert.Equal("dispose 1", ex.Message);
        Assert.All(sources, s => Assert.Equal(1, s.EnumeratorsDisposed));
    }

    [Fact]
    public void LazyMerge_ShouldStillDisposeAnExhaustedSource_WhenTheComparerThrowsDuringItsReplay()
    {
        // Run 0 runs out on the second MoveNext. Its spent leaf loses to its sibling without a
        // comparison, so the first comparison of that replay — the one that throws — comes after
        // the source has been retired, which is exactly where it could be lost.
        var sources = new[] { new TrackingSequence([1]), new TrackingSequence([2, 5]), new TrackingSequence([3, 6]), new TrackingSequence([4, 7]) };
        IEnumerator<int> e = KWayMerge.Merge<int, ArmedComparer>(sources, default).GetEnumerator();
        Assert.True(e.MoveNext());

        ArmedComparer.Armed = true;
        try
        {
            Assert.Throws<InvalidOperationException>(() => e.MoveNext());
        }
        finally
        {
            ArmedComparer.Armed = false;
        }

        Assert.All(sources, s => Assert.Equal(1, s.EnumeratorsDisposed));
        Assert.False(e.MoveNext());
    }

    [Fact]
    public void LazyMerge_ShouldReportTheExhaustedSourcesDisposeFailure_WhenCleanupAlsoFails()
    {
        var first = new TrackingSequence([1], throwOnDispose: true);
        var second = new TrackingSequence([2, 3], throwOnDispose: true);
        IEnumerator<int> e = KWayMerge.Merge<int>(new[] { first, second }).GetEnumerator();
        Assert.True(e.MoveNext());

        // The first source runs out and fails to dispose; releasing the second then fails too.
        var ex = Assert.Throws<InvalidOperationException>(() => e.MoveNext());

        Assert.Equal("dispose 1", ex.Message);
        Assert.Equal(1, second.EnumeratorsDisposed);

        // Cleanup already ran, so the consumer's own Dispose is inert.
        e.Dispose();
        Assert.Equal(1, first.EnumeratorsDisposed);
        Assert.Equal(1, second.EnumeratorsDisposed);
    }

    [Fact]
    public void LazyMerge_ShouldKeepReturningFalse_WhenMovedPastTheEnd()
    {
        using IEnumerator<int> e = KWayMerge.Merge(new[] { new[] { 1 }, new[] { 2 } }).GetEnumerator();

        Assert.True(e.MoveNext());
        Assert.True(e.MoveNext());
        Assert.False(e.MoveNext());
        Assert.False(e.MoveNext());
    }

    [Fact]
    public void LazyMerge_ShouldReturnFalse_WhenMovedAfterDispose()
    {
        var source = new TrackingSequence([1, 2, 3]);
        IEnumerator<int> e = KWayMerge.Merge<int>(new[] { source }).GetEnumerator();
        Assert.True(e.MoveNext());

        e.Dispose();
        e.Dispose();

        Assert.False(e.MoveNext());
        Assert.Equal(1, source.EnumeratorsDisposed);
    }

    [Fact]
    public void LazyMerge_ShouldExposeTheCurrentElement_ThroughBothCurrentProperties()
    {
        using IEnumerator<int> e = KWayMerge.Merge(new[] { new[] { 4 }, new[] { 5 } }).GetEnumerator();
        System.Collections.IEnumerator untyped = e;

        Assert.True(e.MoveNext());

        Assert.Equal(4, e.Current);
        Assert.Equal(4, untyped.Current);
    }

    [Fact]
    public void LazyMerge_ShouldEnumerate_ThroughTheNonGenericInterface()
    {
        System.Collections.IEnumerable merged = KWayMerge.Merge(new[] { new[] { 2 }, new[] { 1 } });

        Assert.Equal(new object[] { 1, 2 }, merged.Cast<object>());
    }

    [Fact]
    public void LazyMerge_ShouldThrowOnReset()
    {
        using IEnumerator<int> e = KWayMerge.Merge(new[] { new[] { 1 } }).GetEnumerator();

        Assert.Throws<NotSupportedException>(e.Reset);
    }

    [Fact]
    public void LazyMerge_ShouldThrow_WhenTheSourcesAreNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => KWayMerge.Merge<int>(null!));
        Assert.Equal("sources", ex.ParamName);
    }

    [Fact]
    public void LazyMerge_ShouldThrowEagerly_WhenASourceIsNull()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => KWayMerge.Merge(new IEnumerable<int>[] { new[] { 1 }, null! }, default(DescendingComparer)));
        Assert.Equal("sources", ex.ParamName);
    }

    /// <summary>Natural int order that throws on every comparison while <see cref="Armed"/> is set.</summary>
    private readonly struct ArmedComparer : IComparer<int>
    {
        public static bool Armed;

        public int Compare(int x, int y) => Armed ? throw new InvalidOperationException("comparer failed") : x.CompareTo(y);
    }

    /// <summary>A sequence that counts the enumerators it hands out and how many were disposed.</summary>
    private sealed class TrackingSequence(int[] values, bool throwOnDispose = false) : IEnumerable<int>
    {
        private readonly int[] values = values;
        private readonly bool throwOnDispose = throwOnDispose;

        public int EnumeratorsCreated { get; private set; }

        public int EnumeratorsDisposed { get; private set; }

        public IEnumerator<int> GetEnumerator()
        {
            EnumeratorsCreated++;
            return new Enumerator(this);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class Enumerator(TrackingSequence owner) : IEnumerator<int>
        {
            private int index = -1;

            public int Current => owner.values[index];

            object System.Collections.IEnumerator.Current => Current;

            public bool MoveNext() => ++index < owner.values.Length;

            public void Reset() => index = -1;

            public void Dispose()
            {
                owner.EnumeratorsDisposed++;
                if (owner.throwOnDispose)
                {
                    // The first throwing source in each test is the lowest-indexed one, so it
                    // carries the value at the front of its run.
                    throw new InvalidOperationException($"dispose {owner.values[0]}");
                }
            }
        }
    }

    /// <summary>A sequence whose enumerator cannot be created.</summary>
    private sealed class ThrowingSequence : IEnumerable<int>
    {
        public IEnumerator<int> GetEnumerator() => throw new InvalidOperationException("no enumerator");

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
