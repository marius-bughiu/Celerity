using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

/// <summary>
/// <see cref="BTreeDictionary{TKey, TValue}"/> against the BCL's ordered map,
/// <see cref="SortedDictionary{TKey, TValue}"/> (a red-black tree: one heap node per entry, ~log2(n)
/// dependent pointer chases per lookup). The last group is the documented win workload — an interleaved
/// insert + lookup + in-order range scan — which is where the fan-out-32 layout pays off and neither
/// SortedDictionary (pointer chasing) nor SortedList (O(n) inserts) can win.
/// </summary>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class BTreeDictionaryBenchmark
{
    private int[] keys = null!;
    private SortedDictionary<int, int> sortedDictionary = null!;
    private BTreeDictionary<int, int> bTree = null!;

    // The window a range scan walks, as a fraction of the whole map.
    private int rangeFrom;
    private int rangeTo;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = new int[ItemCount];
        sortedDictionary = new SortedDictionary<int, int>();
        bTree = new BTreeDictionary<int, int>();

        // Shuffled distinct keys: ascending inserts would only ever split the rightmost node, which is the
        // easy case for every ordered container.
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
            sortedDictionary.Add(key, key);
            bTree.Add(key, key);
        }

        // A 1% window in the middle of the key space.
        rangeFrom = ItemCount / 2;
        rangeTo = rangeFrom + Math.Max(1, ItemCount / 100);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public void SortedDictionary_Add()
    {
        var map = new SortedDictionary<int, int>();

        foreach (int key in keys)
        {
            map.Add(key, key);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public void BTreeDictionary_Add()
    {
        var map = new BTreeDictionary<int, int>();

        foreach (int key in keys)
        {
            map.Add(key, key);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public int SortedDictionary_Lookup()
    {
        int result = 0;
        foreach (int key in keys)
        {
            if (sortedDictionary.TryGetValue(key, out int value))
            {
                result += value;
            }
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int BTreeDictionary_Lookup()
    {
        int result = 0;
        foreach (int key in keys)
        {
            if (bTree.TryGetValue(key, out int value))
            {
                result += value;
            }
        }

        return result;
    }

    [IterationSetup(Target = nameof(SortedDictionary_Remove))]
    public void SetupForSortedDictionaryRemove()
    {
        sortedDictionary = new SortedDictionary<int, int>();
        foreach (int key in keys)
        {
            sortedDictionary.Add(key, key);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public void SortedDictionary_Remove()
    {
        foreach (int key in keys)
        {
            sortedDictionary.Remove(key);
        }
    }

    [IterationSetup(Target = nameof(BTreeDictionary_Remove))]
    public void SetupForBTreeDictionaryRemove()
    {
        bTree = new BTreeDictionary<int, int>();
        foreach (int key in keys)
        {
            bTree.Add(key, key);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void BTreeDictionary_Remove()
    {
        foreach (int key in keys)
        {
            bTree.Remove(key);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RangeScan")]
    public int SortedDictionary_RangeScan()
    {
        // SortedDictionary has no range view, so the best a caller can do is walk in order from the start
        // and stop at the upper bound — the cost this type's EnumerateRange exists to remove.
        int result = 0;
        foreach (KeyValuePair<int, int> entry in sortedDictionary)
        {
            if (entry.Key >= rangeTo)
            {
                break;
            }

            if (entry.Key >= rangeFrom)
            {
                result += entry.Value;
            }
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("RangeScan")]
    public int BTreeDictionary_RangeScan()
    {
        int result = 0;
        foreach (KeyValuePair<int, int> entry in bTree.EnumerateRange(rangeFrom, rangeTo))
        {
            result += entry.Value;
        }

        return result;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Mixed")]
    public int SortedDictionary_Mixed()
    {
        // The documented win workload: build the map, then interleave lookups with in-order range scans —
        // time-series ingest, order books, LSM memtables.
        var map = new SortedDictionary<int, int>();
        int result = 0;

        for (int i = 0; i < keys.Length; i++)
        {
            int key = keys[i];
            map[key] = key;

            if (map.TryGetValue(keys[(i / 2)], out int value))
            {
                result += value;
            }

            if ((i & 1023) == 0)
            {
                int scanned = 0;
                foreach (KeyValuePair<int, int> entry in map)
                {
                    if (++scanned == 64)
                    {
                        break;
                    }

                    result += entry.Value;
                }
            }
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Mixed")]
    public int BTreeDictionary_Mixed()
    {
        var map = new BTreeDictionary<int, int>();
        int result = 0;

        for (int i = 0; i < keys.Length; i++)
        {
            int key = keys[i];
            map[key] = key;

            if (map.TryGetValue(keys[(i / 2)], out int value))
            {
                result += value;
            }

            if ((i & 1023) == 0)
            {
                int scanned = 0;
                foreach (KeyValuePair<int, int> entry in map)
                {
                    if (++scanned == 64)
                    {
                        break;
                    }

                    result += entry.Value;
                }
            }
        }

        return result;
    }
}
