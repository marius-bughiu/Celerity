using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

/// <summary>
/// <see cref="RankedDictionary{TKey, TValue}"/> against <see cref="SortedList{TKey, TValue}"/> — the one BCL
/// map with the same positional surface (<c>IndexOfKey</c>, <c>Keys[k]</c> / <c>Values[k]</c>,
/// <c>RemoveAt</c>) — against a key-ordered <see cref="ImmutableSortedSet{T}.Builder"/> of the pairs, the BCL's
/// one <c>O(log n)</c> route to both positional questions, and, in the <c>Mixed</c> group,
/// <see cref="SortedDictionary{TKey, TValue}"/>, the BCL's ordered map that has no positional surface at all.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SortedList{TKey, TValue}"/> is the strong baseline and the one that can win: a select is an index
/// into one array, so it is <c>O(1)</c> and wins the <c>Select</c> group outright, and a rank is a binary
/// search over contiguous memory. What it cannot do cheaply is mutate — every insert and remove memmoves the
/// tail of both the key and the value array — and <c>Add</c>, <c>Remove</c> and <c>Mixed</c> are where that is
/// charged.
/// </para>
/// <para>
/// No BCL map ranks and selects in <c>O(log n)</c>, but a set of <see cref="KeyValuePair{TKey, TValue}"/> ordered
/// by key alone does: <see cref="ImmutableSortedSet{T}"/>'s AVL nodes count their subtrees, so <c>IndexOf</c> of
/// a probe pair and the indexer are each one descent, and <c>TryGetValue</c> hands back the stored pair. Its
/// arms drive the <see cref="ImmutableSortedSet{T}.Builder"/>, filled by <c>Add</c> in the shuffled key order and
/// never frozen, so every edit is in place, as in <c>RankedSetBenchmark</c>.
/// </para>
/// <para>
/// The <see cref="SortedDictionary{TKey, TValue}"/> arm carries its own op name (<c>MixedTreeWalk</c>), and the
/// <see cref="ImmutableSortedSet{T}"/> arms an <c>Avl</c> suffix (<c>SelectAvl</c>, <c>MixedAvl</c>, …), so each
/// dashboard card still resolves to one BCL measurement and one Celerity one while the report keeps every arm
/// in its category against the same baseline.
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RankedDictionaryBenchmark
{
    // Queries are counted rather than run once per entry, as in RankedSetBenchmark.
    private const int QueryCount = 100;

    // The Mixed group's step count. Each step removes and re-inserts one key, so the map stays at ItemCount
    // entries and the arm can run against a map built once in GlobalSetup; a fixed count keeps the
    // SortedDictionary arm, which walks the tree twice per step, bounded at the 100k sweep.
    private const int MixedSteps = 1000;

    private int[] keys = null!;
    private int[] probes = null!;
    private int[] ranks = null!;

    private SortedList<int, int> sortedList = null!;
    private SortedDictionary<int, int> sortedDictionary = null!;
    private RankedDictionary<int, int> ranked = null!;
    private ImmutableSortedSet<KeyValuePair<int, int>>.Builder avl = null!;

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

        // Shuffled distinct keys: ascending inserts would only ever append, which is SortedList's best case
        // and fills only the last bucket here.
        Random rand = new(42);
        for (int i = ItemCount - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (keys[i], keys[j]) = (keys[j], keys[i]);
        }

        probes = new int[Math.Max(QueryCount, MixedSteps)];
        ranks = new int[QueryCount];
        for (int i = 0; i < probes.Length; i++)
            probes[i] = rand.Next(ItemCount);
        for (int i = 0; i < QueryCount; i++)
            ranks[i] = rand.Next(ItemCount);

        BuildMaps();

        rangeFrom = ItemCount / 2;
        rangeTo = rangeFrom + Math.Max(1, ItemCount / 100);
    }

    // The SortedList is built from a pre-sorted SortedDictionary, which its constructor copies in one pass,
    // so setup does not pay the quadratic insert the Add group measures.
    private void BuildMaps()
    {
        sortedDictionary = new SortedDictionary<int, int>();
        foreach (int key in keys)
            sortedDictionary.Add(key, key);

        sortedList = new SortedList<int, int>(sortedDictionary);

        ranked = new RankedDictionary<int, int>();
        foreach (int key in keys)
            ranked.Add(key, key);

        avl = BuildAvl();
    }

    // Added one pair at a time in the shuffled key order, as the RankedDictionary and the SortedDictionary are, and
    // never frozen, so every edit an arm makes is in place.
    private ImmutableSortedSet<KeyValuePair<int, int>>.Builder BuildAvl()
    {
        ImmutableSortedSet<KeyValuePair<int, int>>.Builder builder = ImmutableSortedSet.CreateBuilder(KeyOrder.Instance);
        foreach (int key in keys)
            builder.Add(new KeyValuePair<int, int>(key, key));

        return builder;
    }

    // ---- Add -----------------------------------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public void SortedList_Add()
    {
        var map = new SortedList<int, int>();

        foreach (int key in keys)
        {
            map.Add(key, key);
        }
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public void ImmutableSortedSet_AddAvl()
    {
        ImmutableSortedSet<KeyValuePair<int, int>>.Builder map = ImmutableSortedSet.CreateBuilder(KeyOrder.Instance);

        foreach (int key in keys)
        {
            map.Add(new KeyValuePair<int, int>(key, key));
        }
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public void RankedDictionary_Add()
    {
        var map = new RankedDictionary<int, int>();

        foreach (int key in keys)
        {
            map.Add(key, key);
        }
    }

    // ---- Lookup --------------------------------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public long SortedList_Lookup()
    {
        long result = 0;
        foreach (int key in keys)
        {
            sortedList.TryGetValue(key, out int value);
            result += value;
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public long ImmutableSortedSet_LookupAvl()
    {
        long result = 0;
        foreach (int key in keys)
        {
            // A probe pair lands where its key would, whatever its value; TryGetValue returns the stored pair.
            avl.TryGetValue(new KeyValuePair<int, int>(key, 0), out KeyValuePair<int, int> entry);
            result += entry.Value;
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public long RankedDictionary_Lookup()
    {
        long result = 0;
        foreach (int key in keys)
        {
            ranked.TryGetValue(key, out int value);
            result += value;
        }

        return result;
    }

    // ---- Remove --------------------------------------------------------------------------------------

    [IterationSetup(Target = nameof(SortedList_Remove))]
    public void SetupForSortedListRemove() => sortedList = new SortedList<int, int>(sortedDictionary);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Remove")]
    public void SortedList_Remove()
    {
        foreach (int key in keys)
        {
            sortedList.Remove(key);
        }
    }

    [IterationSetup(Target = nameof(ImmutableSortedSet_RemoveAvl))]
    public void SetupForImmutableSortedSetRemove() => avl = BuildAvl();

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void ImmutableSortedSet_RemoveAvl()
    {
        foreach (int key in keys)
        {
            avl.Remove(new KeyValuePair<int, int>(key, 0));
        }
    }

    [IterationSetup(Target = nameof(RankedDictionary_Remove))]
    public void SetupForRankedDictionaryRemove()
    {
        ranked = new RankedDictionary<int, int>();
        foreach (int key in keys)
            ranked.Add(key, key);
    }

    [Benchmark]
    [BenchmarkCategory("Remove")]
    public void RankedDictionary_Remove()
    {
        foreach (int key in keys)
        {
            ranked.Remove(key);
        }
    }

    // ---- Select: the entry at rank k -----------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Select")]
    public long SortedList_Select()
    {
        long result = 0;
        foreach (int rank in ranks)
        {
            result += sortedList.GetValueAtIndex(rank);
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Select")]
    public long ImmutableSortedSet_SelectAvl()
    {
        long result = 0;
        foreach (int rank in ranks)
        {
            result += avl[rank].Value;
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Select")]
    public long RankedDictionary_Select()
    {
        long result = 0;
        foreach (int rank in ranks)
        {
            result += ranked.GetValueAtIndex(rank);
        }

        return result;
    }

    // ---- Rank: the position of a key -----------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Rank")]
    public long SortedList_Rank()
    {
        long result = 0;
        for (int i = 0; i < QueryCount; i++)
        {
            result += sortedList.IndexOfKey(probes[i]);
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Rank")]
    public long ImmutableSortedSet_RankAvl()
    {
        // Every probe is a key in the map, so IndexOf returns its index, as IndexOfKey does.
        long result = 0;
        for (int i = 0; i < QueryCount; i++)
        {
            result += avl.IndexOf(new KeyValuePair<int, int>(probes[i], 0));
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Rank")]
    public long RankedDictionary_Rank()
    {
        long result = 0;
        for (int i = 0; i < QueryCount; i++)
        {
            result += ranked.IndexOfKey(probes[i]);
        }

        return result;
    }

    // ---- RangeScan -----------------------------------------------------------------------------------

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("RangeScan")]
    public long SortedList_RangeScan()
    {
        // SortedList has no range view; the idiomatic scan is a rank of the lower bound and an index walk.
        long result = 0;
        IList<int> listKeys = sortedList.Keys;
        IList<int> listValues = sortedList.Values;
        for (int i = sortedList.IndexOfKey(rangeFrom); i < listKeys.Count && listKeys[i] < rangeTo; i++)
        {
            result += listValues[i];
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("RangeScan")]
    public long ImmutableSortedSet_RangeScanAvl()
    {
        // No range view: find the lower bound's index, then read each entry by index, one descent apiece.
        long result = 0;
        int count = avl.Count;
        int at = avl.IndexOf(new KeyValuePair<int, int>(rangeFrom, 0));
        for (int i = at >= 0 ? at : ~at; i < count; i++)
        {
            KeyValuePair<int, int> entry = avl[i];
            if (entry.Key >= rangeTo)
            {
                break;
            }

            result += entry.Value;
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("RangeScan")]
    public long RankedDictionary_RangeScan()
    {
        long result = 0;
        foreach (KeyValuePair<int, int> entry in ranked.EnumerateRange(rangeFrom, rangeTo))
        {
            result += entry.Value;
        }

        return result;
    }

    // ---- Mixed: the documented win workload ----------------------------------------------------------
    // A leaderboard at steady size: a player's score changes (remove the old key, insert the new one), then
    // the board asks where that player now ranks and who holds the median place. The key is re-inserted at
    // the same value, so the map is unchanged after each step and the arms can share one map across
    // invocations — but the remove and the insert each pay their full cost: SortedList memmoves both of its
    // arrays twice per step, SortedDictionary walks the tree for both queries, and the AVL builder makes four
    // O(log n) descents, one per operation.

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Mixed")]
    public long SortedList_Mixed()
    {
        long result = 0;
        for (int i = 0; i < MixedSteps; i++)
        {
            int key = probes[i];
            sortedList.Remove(key);
            sortedList.Add(key, key);
            result += sortedList.IndexOfKey(key) + sortedList.GetValueAtIndex(sortedList.Count / 2);
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Mixed")]
    public long SortedDictionary_MixedTreeWalk()
    {
        long result = 0;
        for (int i = 0; i < MixedSteps; i++)
        {
            int key = probes[i];
            sortedDictionary.Remove(key);
            sortedDictionary.Add(key, key);

            // SortedDictionary exposes neither a rank nor a positional accessor, so both are walks.
            int rank = 0;
            foreach (KeyValuePair<int, int> entry in sortedDictionary)
            {
                if (entry.Key >= key)
                {
                    break;
                }

                rank++;
            }

            result += rank + sortedDictionary.ElementAt(sortedDictionary.Count / 2).Value;
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Mixed")]
    public long ImmutableSortedSet_MixedAvl()
    {
        long result = 0;
        for (int i = 0; i < MixedSteps; i++)
        {
            var entry = new KeyValuePair<int, int>(probes[i], probes[i]);
            avl.Remove(entry);
            avl.Add(entry);
            result += avl.IndexOf(entry) + avl[avl.Count / 2].Value;
        }

        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Mixed")]
    public long RankedDictionary_Mixed()
    {
        long result = 0;
        for (int i = 0; i < MixedSteps; i++)
        {
            int key = probes[i];
            ranked.Remove(key);
            ranked.Add(key, key);
            result += ranked.IndexOfKey(key) + ranked.GetValueAtIndex(ranked.Count / 2);
        }

        return result;
    }

    // Orders the pairs by key alone, which is what makes the set usable as a map: a probe pair with any value
    // lands where its key would.
    private sealed class KeyOrder : IComparer<KeyValuePair<int, int>>
    {
        public static readonly KeyOrder Instance = new();

        public int Compare(KeyValuePair<int, int> x, KeyValuePair<int, int> y) => x.Key.CompareTo(y.Key);
    }
}
