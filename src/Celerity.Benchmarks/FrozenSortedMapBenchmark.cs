using System.Collections.Frozen;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// FrozenSortedMap<int, int> against the two things a caller would otherwise hold. The dashboard charts one BCL arm
// per op, so each op is paired with the baseline a caller would actually pick for it:
//
//   TryGetValue — an exact lookup over a probe batch that is half keys and half non-keys, shuffled together, summing
//                 the values found without a branch. The baseline is FrozenDictionary<int, int>, the BCL's
//                 build-once map. It is a hash lookup and is expected to win: this op is here so the docs can say
//                 by how much, not to flatter the map.
//   Floor       — "the value as of t": the entry with the largest key at or below a uniformly random int. A hash
//                 map cannot answer it, so the baseline is the hand-roll: a sorted int[] of keys and a parallel
//                 int[] of values, sorted together with Array.Sort(keys, values) and searched with the span
//                 MemoryExtensions.BinarySearch (the generic TComparable overload, so no boxing and no comparer
//                 call), named Array_* so the dashboard classifies it as the reference arm.
//   Build       — construction from an unsorted array of pairs, against splitting the same pairs into two arrays
//                 and Array.Sort(keys, values). Both sides pay the split; the map also checks for duplicate keys
//                 and lays out the search tree.
//
// The keys are random distinct ints over the whole range, so neither side gets a dense, predictable array.

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class FrozenSortedMapBenchmark
{
    // 2^18 probes per invocation, for the reason FrozenSortedSetBenchmark gives: a short batch replayed millions of
    // times lets the branch predictor memorize the binary search's path and flatters the baseline.
    private const int QueryCount = 1 << 18;

    private KeyValuePair<int, int>[] pairs = null!;
    private int[] sortedKeys = null!;
    private int[] sortedValues = null!;
    private FrozenSortedMap<int, int> map = null!;
    private FrozenDictionary<int, int> frozen = null!;
    private int[] lookupProbes = null!;
    private int[] floorProbes = null!;

    // Named ItemCount so the dashboard's benchmark-name parser picks up the parameter.
    [Params(1_000, 100_000, 1_000_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        var distinct = new HashSet<int>(ItemCount);
        while (distinct.Count < ItemCount)
            distinct.Add(rand.Next(int.MinValue, int.MaxValue));

        int[] keys = distinct.ToArray();
        rand.Shuffle(keys);
        pairs = new KeyValuePair<int, int>[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            pairs[i] = new KeyValuePair<int, int>(keys[i], rand.Next());

        (sortedKeys, sortedValues) = SplitAndSort(pairs);
        map = new FrozenSortedMap<int, int>(pairs);
        frozen = pairs.ToFrozenDictionary();

        lookupProbes = new int[QueryCount];
        for (int i = 0; i < QueryCount; i++)
        {
            if (i % 2 == 0)
            {
                lookupProbes[i] = keys[rand.Next(ItemCount)];
            }
            else
            {
                int miss;
                do
                {
                    miss = rand.Next(int.MinValue, int.MaxValue);
                }
                while (distinct.Contains(miss));
                lookupProbes[i] = miss;
            }
        }

        rand.Shuffle(lookupProbes);

        floorProbes = new int[QueryCount];
        for (int i = 0; i < QueryCount; i++)
            floorProbes[i] = rand.Next(int.MinValue, int.MaxValue);
    }

    private static (int[] Keys, int[] Values) SplitAndSort(KeyValuePair<int, int>[] source)
    {
        var keys = new int[source.Length];
        var values = new int[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            keys[i] = source[i].Key;
            values[i] = source[i].Value;
        }

        Array.Sort(keys, values);
        return (keys, values);
    }

    // ---- TryGetValue: half hits, half misses, summed without a branch ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("TryGetValue")]
    public long FrozenDictionary_TryGetValue()
    {
        long sink = 0;
        foreach (int probe in lookupProbes)
        {
            frozen.TryGetValue(probe, out int value);
            sink += value;
        }

        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("TryGetValue")]
    public long FrozenSortedMap_TryGetValue()
    {
        long sink = 0;
        foreach (int probe in lookupProbes)
        {
            map.TryGetValue(probe, out int value);
            sink += value;
        }

        return sink;
    }

    // ---- Floor: the value at the largest key at or below a random probe ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Floor")]
    public long Array_Floor()
    {
        ReadOnlySpan<int> keys = sortedKeys;
        int[] values = sortedValues;
        long sink = 0;
        foreach (int probe in floorProbes)
        {
            int index = keys.BinarySearch(probe);
            if (index < 0)
                index = ~index - 1;
            if (index >= 0)
                sink += values[index];
        }

        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("Floor")]
    public long FrozenSortedMap_Floor()
    {
        long sink = 0;
        foreach (int probe in floorProbes)
        {
            if (map.TryGetFloor(probe, out KeyValuePair<int, int> entry))
                sink += entry.Value;
        }

        return sink;
    }

    // ---- Build: from an unsorted array of pairs ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public int[] Array_Build() => SplitAndSort(pairs).Keys;

    [Benchmark]
    [BenchmarkCategory("Build")]
    public FrozenSortedMap<int, int> FrozenSortedMap_Build() => new(pairs);
}
