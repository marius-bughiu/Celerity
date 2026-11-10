using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// FrozenSortedSet<int> against the sorted int[] a caller would otherwise hold. The BCL's frozen collections are
// unordered and SortedSet<int> is a mutable red-black tree, so neither answers "how many keys are below x" as
// cheaply as a binary search over an array; the honest baseline is that array, searched with the span
// MemoryExtensions.BinarySearch (the generic TComparable overload, so no boxing and no comparer call), marked
// Baseline in every category and named Array_* so the dashboard classifies it as the reference arm.
//
//   Contains          — membership for a probe batch that is half keys and half non-keys, shuffled together,
//                       counting hits without a branch (a filter or a count): the throughput a batch of independent
//                       lookups gets.
//   ContainsBranching — the same batch, but branching on each answer as `if (set.Contains(x))` does. The shuffled
//                       half-hit batch makes that branch mispredict about half the time, and each mispredict
//                       discards the searches the CPU had overlapped, so this is the latency-bound case and the
//                       narrower win. Charted under its own op so it stays visible.
//   LowerBound        — the rank of a uniformly random int (CountLessThan), against ~BinarySearch on a miss.
//   Build             — construction from an unsorted array, against sorting a copy of it. The source is distinct, so
//                       the baseline skips the dedup pass the set still performs.
//
// The keys are random distinct ints over the whole range, so neither side gets a dense, predictable array.

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class FrozenSortedSetBenchmark
{
    // 2^18 probes per invocation, not the usual 1,000. BenchmarkDotNet replays the same batch millions of times,
    // and a modern branch predictor memorizes a short replayed sequence: with 1,000 probes the array's binary
    // search learned its own branch paths and ran 3.4x faster than on fresh input (10.5 ns against 35.6 ns a
    // lookup at 1,000 keys on an M4), which no real query stream would let it do. A batch this long cannot be
    // memorized, so both arms pay the mispredictions a production lookup pays.
    private const int QueryCount = 1 << 18;

    private int[] unsorted = null!;
    private int[] sorted = null!;
    private FrozenSortedSet<int> set = null!;
    private int[] containsProbes = null!;
    private int[] rankProbes = null!;

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

        unsorted = distinct.ToArray();
        rand.Shuffle(unsorted);
        sorted = (int[])unsorted.Clone();
        Array.Sort(sorted);
        set = new FrozenSortedSet<int>(unsorted);

        containsProbes = new int[QueryCount];
        for (int i = 0; i < QueryCount; i++)
        {
            if (i % 2 == 0)
            {
                containsProbes[i] = sorted[rand.Next(ItemCount)];
            }
            else
            {
                int miss;
                do
                {
                    miss = rand.Next(int.MinValue, int.MaxValue);
                }
                while (distinct.Contains(miss));
                containsProbes[i] = miss;
            }
        }

        rand.Shuffle(containsProbes);

        rankProbes = new int[QueryCount];
        for (int i = 0; i < QueryCount; i++)
            rankProbes[i] = rand.Next(int.MinValue, int.MaxValue);
    }

    // ---- Contains: half hits, half misses, counted without a branch ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Contains")]
    public int Array_Contains()
    {
        ReadOnlySpan<int> keys = sorted;
        int hits = 0;
        foreach (int probe in containsProbes)
            hits += keys.BinarySearch(probe) >= 0 ? 1 : 0;
        return hits;
    }

    [Benchmark]
    [BenchmarkCategory("Contains")]
    public int FrozenSortedSet_Contains()
    {
        int hits = 0;
        foreach (int probe in containsProbes)
            hits += set.Contains(probe) ? 1 : 0;
        return hits;
    }

    // ---- ContainsBranching: the same batch, with a branch on each answer ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ContainsBranching")]
    public int Array_ContainsBranching()
    {
        ReadOnlySpan<int> keys = sorted;
        int hits = 0;
        foreach (int probe in containsProbes)
        {
            if (keys.BinarySearch(probe) >= 0)
                hits++;
        }

        return hits;
    }

    [Benchmark]
    [BenchmarkCategory("ContainsBranching")]
    public int FrozenSortedSet_ContainsBranching()
    {
        int hits = 0;
        foreach (int probe in containsProbes)
        {
            if (set.Contains(probe))
                hits++;
        }

        return hits;
    }

    // ---- LowerBound: how many keys are below a random probe ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("LowerBound")]
    public long Array_LowerBound()
    {
        ReadOnlySpan<int> keys = sorted;
        long sink = 0;
        foreach (int probe in rankProbes)
        {
            int index = keys.BinarySearch(probe);
            sink += index >= 0 ? index : ~index;
        }

        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("LowerBound")]
    public long FrozenSortedSet_LowerBound()
    {
        long sink = 0;
        foreach (int probe in rankProbes)
            sink += set.CountLessThan(probe);
        return sink;
    }

    // ---- Build: from an unsorted array ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public int[] Array_Build()
    {
        var copy = (int[])unsorted.Clone();
        Array.Sort(copy);
        return copy;
    }

    [Benchmark]
    [BenchmarkCategory("Build")]
    public FrozenSortedSet<int> FrozenSortedSet_Build() => new(unsorted);
}
