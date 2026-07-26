using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

/// <summary>
/// <see cref="BTreeSet{T}"/> against the BCL's ordered set, <see cref="SortedSet{T}"/> (a red-black tree:
/// one heap node per element, ~log2(n) dependent pointer chases per lookup). The range group compares
/// against <see cref="SortedSet{T}.GetViewBetween"/>, the BCL's own range view, and the last group is the
/// documented win workload — an interleaved insert + membership + in-order range scan.
/// </summary>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class BTreeSetBenchmark
{
    private int[] keys = null!;
    private SortedSet<int> sortedSet = null!;
    private BTreeSet<int> bTree = null!;

    private int rangeFrom;
    private int rangeTo;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        sortedSet = new SortedSet<int>();
        bTree = new BTreeSet<int>();

        // Shuffled distinct elements: ascending inserts would only ever split the rightmost node.
        Random rand = new(42);
        for (int i = 0; i < ItemCount; i++)
        {
            keys[i] = i;
        }

        for (int i = ItemCount - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (keys[i], keys[j]) = (keys[j], keys[i]);
        }

        foreach (int key in keys)
        {
            sortedSet.Add(key);
            bTree.Add(key);
        }

        rangeFrom = ItemCount / 2;
        rangeTo = rangeFrom + Math.Max(1, ItemCount / 100);
    }

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
    public void BTreeSet_Add()
    {
        var set = new BTreeSet<int>();

        foreach (int key in keys)
        {
            set.Add(key);
        }
    }

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
    public bool BTreeSet_Contains()
    {
        bool result = false;
        foreach (int key in keys)
        {
            result ^= bTree.Contains(key);
        }

        return result;
    }

    [IterationSetup(Target = nameof(SortedSet_Remove))]
    public void SetupForSortedSetRemove()
    {
        sortedSet = new SortedSet<int>();
        foreach (int key in keys)
        {
            sortedSet.Add(key);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public void SortedSet_Remove()
    {
        foreach (int key in keys)
        {
            sortedSet.Remove(key);
        }
    }

    [IterationSetup(Target = nameof(BTreeSet_Remove))]
    public void SetupForBTreeSetRemove()
    {
        bTree = new BTreeSet<int>();
        foreach (int key in keys)
        {
            bTree.Add(key);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void BTreeSet_Remove()
    {
        foreach (int key in keys)
        {
            bTree.Remove(key);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RangeScan")]
    public int SortedSet_RangeScan()
    {
        // SortedSet does have a range view, so this is the fair BCL comparison — it still walks the
        // red-black tree one pointer chase at a time.
        int result = 0;
        foreach (int item in sortedSet.GetViewBetween(rangeFrom, rangeTo - 1))
        {
            result += item;
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("RangeScan")]
    public int BTreeSet_RangeScan()
    {
        int result = 0;
        foreach (int item in bTree.EnumerateRange(rangeFrom, rangeTo))
        {
            result += item;
        }

        return result;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Mixed")]
    public int SortedSet_Mixed()
    {
        // The documented win workload: build the set, then interleave membership tests with in-order range
        // scans — sweep-line events, sorted id sets, interval endpoints.
        var set = new SortedSet<int>();
        int result = 0;

        for (int i = 0; i < keys.Length; i++)
        {
            set.Add(keys[i]);

            if (set.Contains(keys[i / 2]))
            {
                result++;
            }

            if ((i & 1023) == 0)
            {
                int scanned = 0;
                foreach (int item in set)
                {
                    if (++scanned == 64)
                    {
                        break;
                    }

                    result += item;
                }
            }
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Mixed")]
    public int BTreeSet_Mixed()
    {
        var set = new BTreeSet<int>();
        int result = 0;

        for (int i = 0; i < keys.Length; i++)
        {
            set.TryAdd(keys[i]);

            if (set.Contains(keys[i / 2]))
            {
                result++;
            }

            if ((i & 1023) == 0)
            {
                int scanned = 0;
                foreach (int item in set)
                {
                    if (++scanned == 64)
                    {
                        break;
                    }

                    result += item;
                }
            }
        }

        return result;
    }
}
