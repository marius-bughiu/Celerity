using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

/// <summary>
/// <see cref="RankedSet{T}"/> against the two things a caller actually reaches for today: the BCL's ordered
/// set, <see cref="SortedSet{T}"/>, and a <see cref="List{T}"/> kept sorted by hand with
/// <see cref="List{T}.BinarySearch(T)"/> and <see cref="List{T}.Insert"/>.
/// </summary>
/// <remarks>
/// <para>
/// The hand-rolled list is here because it is the <i>strong</i> baseline and the one that can beat this type:
/// selection is a single array index and a rank is a binary search over contiguous memory, so it wins both
/// query groups outright. What it cannot do is mutate — every insert and remove memmoves half the array — and
/// the <c>Mixed</c> group is where that is charged. <see cref="SortedSet{T}"/> is the mirror image: it
/// mutates in <c>O(log n)</c> and has no rank or positional accessor at all, so its answers are
/// <c>ElementAt(k)</c> and a walk to the probe, both linear.
/// </para>
/// <para>
/// Only the <see cref="SortedSet{T}"/> arms carry the dashboard's op names; the list arms are named for what
/// they do (<c>SelectByIndex</c>, <c>RankBinarySearch</c>, …) so each dashboard card still resolves to one
/// BCL measurement and one Celerity one, while the report keeps all three in the same category with ratios
/// against the same baseline.
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RankedSetBenchmark
{
    // Queries are counted rather than run once per element: the BCL answers are linear, so a query per
    // element would be quadratic and would measure nothing but how long the harness is willing to wait.
    private const int QueryCount = 100;

    private int[] keys = null!;
    private int[] probes = null!;
    private int[] ranks = null!;

    private SortedSet<int> sortedSet = null!;
    private List<int> sortedList = null!;
    private RankedSet<int> ranked = null!;

    private int window;
    private int rangeFrom;
    private int rangeTo;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            keys[i] = i;

        // Shuffled distinct elements: ascending inserts would only ever fill the last bucket.
        Random rand = new(42);
        for (int i = ItemCount - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (keys[i], keys[j]) = (keys[j], keys[i]);
        }

        probes = new int[QueryCount];
        ranks = new int[QueryCount];
        for (int i = 0; i < QueryCount; i++)
        {
            probes[i] = rand.Next(ItemCount);
            ranks[i] = rand.Next(ItemCount);
        }

        sortedSet = new SortedSet<int>(keys);
        sortedList = [.. Enumerable.Range(0, ItemCount)];
        ranked = new RankedSet<int>(keys);

        // The sliding window the Mixed group maintains. The group asks a rank and a select on *every* step,
        // and the SortedSet answers to both are linear in the window, so the window is a twentieth of the
        // sweep: large enough that the live set is a real one (5,000 elements at the 100k sweep) and small
        // enough that the baseline arm does not run for minutes.
        window = Math.Max(1, ItemCount / 20);

        rangeFrom = ItemCount / 2;
        rangeTo = rangeFrom + Math.Max(1, ItemCount / 100);
    }

    // ---- Add -----------------------------------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public void SortedSet_Add()
    {
        var set = new SortedSet<int>();

        foreach (int key in keys)
        {
            set.Add(key);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public void List_AddSorted()
    {
        var list = new List<int>();

        foreach (int key in keys)
        {
            int at = list.BinarySearch(key);
            if (at < 0)
            {
                list.Insert(~at, key);
            }
        }
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public void RankedSet_Add()
    {
        var set = new RankedSet<int>();

        foreach (int key in keys)
        {
            set.Add(key);
        }
    }

    // ---- Contains ------------------------------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Contains")]
    public bool SortedSet_Contains()
    {
        bool result = false;
        foreach (int key in keys)
        {
            result ^= sortedSet.Contains(key);
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public bool List_ContainsBinarySearch()
    {
        bool result = false;
        foreach (int key in keys)
        {
            result ^= sortedList.BinarySearch(key) >= 0;
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public bool RankedSet_Contains()
    {
        bool result = false;
        foreach (int key in keys)
        {
            result ^= ranked.Contains(key);
        }

        return result;
    }

    // ---- Remove --------------------------------------------------------------------------------------

    [IterationSetup(Target = nameof(SortedSet_Remove))]
    public void SetupForSortedSetRemove() => sortedSet = new SortedSet<int>(keys);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public void SortedSet_Remove()
    {
        foreach (int key in keys)
        {
            sortedSet.Remove(key);
        }
    }

    [IterationSetup(Target = nameof(List_RemoveSorted))]
    public void SetupForListRemove() => sortedList = [.. Enumerable.Range(0, ItemCount)];

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void List_RemoveSorted()
    {
        foreach (int key in keys)
        {
            int at = sortedList.BinarySearch(key);
            if (at >= 0)
            {
                sortedList.RemoveAt(at);
            }
        }
    }

    [IterationSetup(Target = nameof(RankedSet_Remove))]
    public void SetupForRankedSetRemove() => ranked = new RankedSet<int>(keys);

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void RankedSet_Remove()
    {
        foreach (int key in keys)
        {
            ranked.Remove(key);
        }
    }

    // ---- Select: the k-th smallest -------------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Select")]
    public long SortedSet_Select()
    {
        // The BCL has no positional accessor on SortedSet<T>, so this is the answer a caller writes: walk
        // the tree to the k-th element.
        long result = 0;
        foreach (int rank in ranks)
        {
            result += sortedSet.ElementAt(rank);
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Select")]
    public long List_SelectByIndex()
    {
        long result = 0;
        foreach (int rank in ranks)
        {
            result += sortedList[rank];
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Select")]
    public long RankedSet_Select()
    {
        long result = 0;
        foreach (int rank in ranks)
        {
            result += ranked[rank];
        }

        return result;
    }

    // ---- Rank: the position an element would occupy --------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Rank")]
    public long SortedSet_Rank()
    {
        long result = 0;
        foreach (int probe in probes)
        {
            // SortedSet<T> exposes no rank, so the position has to be counted out in order.
            int rank = 0;
            foreach (int item in sortedSet)
            {
                if (item >= probe)
                {
                    break;
                }

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
        {
            int at = sortedList.BinarySearch(probe);
            result += at >= 0 ? at : ~at;
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Rank")]
    public long RankedSet_Rank()
    {
        long result = 0;
        foreach (int probe in probes)
        {
            result += ranked.CountLessThan(probe);
        }

        return result;
    }

    // ---- RangeScan -----------------------------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RangeScan")]
    public long SortedSet_RangeScan()
    {
        // SortedSet<T> does have a range view, so this is the fair BCL comparison for the ordered surface.
        long result = 0;
        foreach (int item in sortedSet.GetViewBetween(rangeFrom, rangeTo - 1))
        {
            result += item;
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("RangeScan")]
    public long List_RangeScanSorted()
    {
        long result = 0;
        int at = sortedList.BinarySearch(rangeFrom);
        for (int i = at >= 0 ? at : ~at; i < sortedList.Count && sortedList[i] < rangeTo; i++)
        {
            result += sortedList[i];
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("RangeScan")]
    public long RankedSet_RangeScan()
    {
        long result = 0;
        foreach (int item in ranked.EnumerateRange(rangeFrom, rangeTo))
        {
            result += item;
        }

        return result;
    }

    // ---- Mixed: the documented win workload ----------------------------------------------------------
    // A sliding window over a stream: insert the arriving value, evict the one that left the window, and then
    // — on every step, which is what the pre-registered criterion says — ask for the rank of the newest value
    // and for the median of what is currently in. That is the leaderboard / live-percentile shape, and it is
    // the only group where both baselines pay their weakness at once: the tree for the two linear queries,
    // the list for the memmove per mutation.

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Mixed")]
    public long SortedSet_Mixed()
    {
        var set = new SortedSet<int>();
        long result = 0;

        for (int i = 0; i < keys.Length; i++)
        {
            set.Add(keys[i]);
            if (i >= window)
            {
                set.Remove(keys[i - window]);
            }

            int rank = 0;
            foreach (int item in set)
            {
                if (item >= keys[i])
                {
                    break;
                }

                rank++;
            }

            result += rank + set.ElementAt(set.Count / 2);
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Mixed")]
    public long List_MixedSorted()
    {
        var list = new List<int>();
        long result = 0;

        for (int i = 0; i < keys.Length; i++)
        {
            int at = list.BinarySearch(keys[i]);
            if (at < 0)
            {
                list.Insert(~at, keys[i]);
            }

            if (i >= window)
            {
                int gone = list.BinarySearch(keys[i - window]);
                if (gone >= 0)
                {
                    list.RemoveAt(gone);
                }
            }

            int found = list.BinarySearch(keys[i]);
            result += (found >= 0 ? found : ~found) + list[list.Count / 2];
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Mixed")]
    public long RankedSet_Mixed()
    {
        var set = new RankedSet<int>();
        long result = 0;

        for (int i = 0; i < keys.Length; i++)
        {
            set.TryAdd(keys[i]);
            if (i >= window)
            {
                set.Remove(keys[i - window]);
            }

            result += set.CountLessThan(keys[i]) + set[set.Count / 2];
        }

        return result;
    }
}
