using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

/// <summary>
/// <see cref="RankedMultiSet{T}"/> against the two things a caller reaches for today when a sorted collection
/// must keep duplicates. One is <see cref="SortedSet{T}"/> over a composite <c>(value, sequence)</c> element,
/// the usual tie-break workaround. The other is a <see cref="List{T}"/> kept sorted by hand, with lower- and
/// upper-bound binary searches and <see cref="List{T}.Insert"/>.
/// </summary>
/// <remarks>
/// <para>
/// The values are duplicate-heavy on purpose. They are drawn from <c>ItemCount / 320</c> distinct values (at
/// least 16), so the Mixed group's sliding window holds about sixteen copies of each value at 100,000. That is
/// the workload the type exists for, and it is why
/// <see cref="RankedSet{T}"/> is not an arm here. It would drop the repeats and answer a different question.
/// </para>
/// <para>
/// Only the <see cref="SortedSet{T}"/> arms carry the dashboard's op names. The list arms are named for what
/// they do (<c>SelectByIndex</c>, <c>RankBinarySearch</c>, …), because <c>List</c> is also a BCL type and two
/// BCL arms sharing an op would overwrite each other on the dashboard.
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RankedMultiSetBenchmark
{
    // Queries are counted rather than run once per element, because the SortedSet answers are linear.
    private const int QueryCount = 100;

    private int[] values = null!;
    private int[] probes = null!;
    private int[] ranks = null!;

    private SortedSet<(int Value, long Seq)> sortedSet = null!;
    private List<int> sortedList = null!;
    private RankedMultiSet<int> multiSet = null!;

    private int window;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        Random rand = new(42);
        // A fixed share of the window rather than of the sweep, so the Mixed window holds about sixteen copies of
        // each value at 100,000 (5,000 samples over 312 values), and the whole sweep about 320.
        int distinct = Math.Max(16, ItemCount / 320);

        values = new int[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            values[i] = rand.Next(distinct);

        probes = new int[QueryCount];
        ranks = new int[QueryCount];
        for (int i = 0; i < QueryCount; i++)
        {
            probes[i] = rand.Next(distinct);
            ranks[i] = rand.Next(ItemCount);
        }

        sortedSet = BuildSortedSet();
        sortedList = BuildSortedList();
        multiSet = new RankedMultiSet<int>(values);

        // The same window rule as RankedSetBenchmark: the SortedSet answers to the per-step rank and select
        // are linear in the window, so a twentieth of the sweep keeps that arm from running for minutes.
        window = Math.Max(1, ItemCount / 20);
    }

    private SortedSet<(int Value, long Seq)> BuildSortedSet()
    {
        var set = new SortedSet<(int Value, long Seq)>();
        for (int i = 0; i < values.Length; i++)
            set.Add((values[i], i));

        return set;
    }

    private List<int> BuildSortedList()
    {
        var list = new List<int>(values);
        list.Sort();
        return list;
    }

    // The first index whose element is not smaller than `value`. List<T>.BinarySearch returns *some* match
    // among duplicates, so a hand-roll that keeps duplicates has to write its own bound searches.
    private static int LowerBound(List<int> list, int value)
    {
        Span<int> span = CollectionsMarshal.AsSpan(list);
        int lo = 0;
        int hi = span.Length;
        while (lo < hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            if (span[mid] < value)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }

    // The first index whose element is greater than `value`.
    private static int UpperBound(List<int> list, int value)
    {
        Span<int> span = CollectionsMarshal.AsSpan(list);
        int lo = 0;
        int hi = span.Length;
        while (lo < hi)
        {
            int mid = (int)(((uint)lo + (uint)hi) >> 1);
            if (span[mid] <= value)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }

    // ---- Add -----------------------------------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public void SortedSet_Add()
    {
        var set = new SortedSet<(int Value, long Seq)>();
        for (int i = 0; i < values.Length; i++)
            set.Add((values[i], i));
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public void List_AddSorted()
    {
        var list = new List<int>();
        foreach (int value in values)
            list.Insert(UpperBound(list, value), value);
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public void RankedMultiSet_Add()
    {
        var set = new RankedMultiSet<int>();
        foreach (int value in values)
            set.Add(value);
    }

    // ---- Remove: one copy per call, every element in turn --------------------------------------------

    [IterationSetup(Target = nameof(SortedSet_Remove))]
    public void SetupForSortedSetRemove() => sortedSet = BuildSortedSet();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public void SortedSet_Remove()
    {
        // The composite element has to be named exactly, so the caller must have kept each sequence number.
        for (int i = 0; i < values.Length; i++)
            sortedSet.Remove((values[i], i));
    }

    [IterationSetup(Target = nameof(List_RemoveSorted))]
    public void SetupForListRemove() => sortedList = BuildSortedList();

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void List_RemoveSorted()
    {
        foreach (int value in values)
            sortedList.RemoveAt(LowerBound(sortedList, value));
    }

    [IterationSetup(Target = nameof(RankedMultiSet_Remove))]
    public void SetupForMultiSetRemove() => multiSet = new RankedMultiSet<int>(values);

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void RankedMultiSet_Remove()
    {
        foreach (int value in values)
            multiSet.Remove(value);
    }

    // ---- Select: the k-th smallest -------------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Select")]
    public long SortedSet_Select()
    {
        long result = 0;
        foreach (int rank in ranks)
            result += sortedSet.ElementAt(rank).Value;

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Select")]
    public long List_SelectByIndex()
    {
        long result = 0;
        foreach (int rank in ranks)
            result += sortedList[rank];

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Select")]
    public long RankedMultiSet_Select()
    {
        long result = 0;
        foreach (int rank in ranks)
            result += multiSet[rank];

        return result;
    }

    // ---- Rank: how many elements order before a value ------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Rank")]
    public long SortedSet_Rank()
    {
        long result = 0;
        foreach (int probe in probes)
        {
            int rank = 0;
            foreach ((int value, _) in sortedSet)
            {
                if (value >= probe)
                    break;

                rank++;
            }

            result += rank;
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Rank")]
    public long List_RankBinarySearch()
    {
        long result = 0;
        foreach (int probe in probes)
            result += LowerBound(sortedList, probe);

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Rank")]
    public long RankedMultiSet_Rank()
    {
        long result = 0;
        foreach (int probe in probes)
            result += multiSet.CountLessThan(probe);

        return result;
    }

    // ---- CountOf: how many copies of a value ---------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CountOf")]
    public long SortedSet_CountOf()
    {
        // The composite workaround's answer: a view over every sequence number of the value.
        long result = 0;
        foreach (int probe in probes)
            result += sortedSet.GetViewBetween((probe, long.MinValue), (probe, long.MaxValue)).Count;

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("CountOf")]
    public long List_CountOfBinarySearch()
    {
        long result = 0;
        foreach (int probe in probes)
            result += UpperBound(sortedList, probe) - LowerBound(sortedList, probe);

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("CountOf")]
    public long RankedMultiSet_CountOf()
    {
        long result = 0;
        foreach (int probe in probes)
            result += multiSet.CountOf(probe);

        return result;
    }

    // ---- Mixed: the documented win workload ----------------------------------------------------------
    // A sliding window over a duplicate-heavy stream: insert the arriving value, evict the one that left the
    // window, then on every step ask the rank of the newest value and the window's median. The same shape as
    // RankedSetBenchmark's Mixed group, with repeats the set version could not hold.

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Mixed")]
    public long SortedSet_Mixed()
    {
        var set = new SortedSet<(int Value, long Seq)>();
        long result = 0;

        for (int i = 0; i < values.Length; i++)
        {
            set.Add((values[i], i));
            if (i >= window)
                set.Remove((values[i - window], i - window));

            int rank = 0;
            foreach ((int value, _) in set)
            {
                if (value >= values[i])
                    break;

                rank++;
            }

            result += rank + set.ElementAt(set.Count / 2).Value;
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Mixed")]
    public long List_MixedSorted()
    {
        var list = new List<int>();
        long result = 0;

        for (int i = 0; i < values.Length; i++)
        {
            list.Insert(UpperBound(list, values[i]), values[i]);
            if (i >= window)
                list.RemoveAt(LowerBound(list, values[i - window]));

            result += LowerBound(list, values[i]) + list[list.Count / 2];
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Mixed")]
    public long RankedMultiSet_Mixed()
    {
        var set = new RankedMultiSet<int>();
        long result = 0;

        for (int i = 0; i < values.Length; i++)
        {
            set.Add(values[i]);
            if (i >= window)
                set.Remove(values[i - window]);

            result += set.CountLessThan(values[i]) + set[set.Count / 2];
        }

        return result;
    }
}
