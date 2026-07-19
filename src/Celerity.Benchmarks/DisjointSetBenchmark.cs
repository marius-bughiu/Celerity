using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// DisjointSet<int> vs the idiomatic BCL union-find substitute: a Dictionary<int, HashSet<int>> that maps
// each element to the shared HashSet of its whole component and merges the smaller group into the larger on
// every union. The BCL ships no union-find, so this hand-rolled set-merge is the honest baseline — it is
// what a developer reaches for without a dedicated structure. Merging two components copies the smaller
// group's members (O(size)), so building one component from n singletons is O(n^2); DisjointSet's union by
// size + path halving keeps the same workload near-linear. The three categories cover the incremental-
// connectivity workload: Union (build the partition from a random edge stream), Connected (a batch of
// connectivity queries against the built partition), and Components (extract the grouped partition). The
// baseline arms are named Dictionary_* so the dashboard classifies them as the BCL reference.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class DisjointSetBenchmark
{
    // Random edge stream (pairs of element ids) driving the union workload, plus a query stream for the
    // connectivity category. Built once in [GlobalSetup].
    private int[] edgeA = null!;
    private int[] edgeB = null!;
    private int[] queryA = null!;
    private int[] queryB = null!;

    // A pre-built, fully-unioned partition for the non-destructive Connected / Components categories.
    private DisjointSet<int> dsFull = null!;
    private Dictionary<int, HashSet<int>> dictFull = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        int edges = ItemCount; // a near-spanning edge stream over the universe
        edgeA = new int[edges];
        edgeB = new int[edges];
        var rand = new Random(42);
        for (int i = 0; i < edges; i++)
        {
            edgeA[i] = rand.Next(ItemCount);
            edgeB[i] = rand.Next(ItemCount);
        }

        int queries = Math.Min(ItemCount, 10_000);
        queryA = new int[queries];
        queryB = new int[queries];
        for (int i = 0; i < queries; i++)
        {
            queryA[i] = rand.Next(ItemCount);
            queryB[i] = rand.Next(ItemCount);
        }

        dsFull = BuildCelerity();
        dictFull = BuildDictionary();
    }

    // ---- Union: build the partition from the random edge stream (the headline O(n) vs O(n^2) split) ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Union")]
    public int Dictionary_Union() => BuildDictionary().Count;

    [Benchmark]
    [BenchmarkCategory("Union")]
    public int DisjointSet_Union() => BuildCelerity().SetCount;

    // ---- Connected: a batch of connectivity queries against the pre-built partition ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Connected")]
    public int Dictionary_Connected()
    {
        int connected = 0;
        for (int i = 0; i < queryA.Length; i++)
        {
            if (dictFull.TryGetValue(queryA[i], out HashSet<int>? ca) &&
                dictFull.TryGetValue(queryB[i], out HashSet<int>? cb) &&
                ReferenceEquals(ca, cb))
            {
                connected++;
            }
        }

        return connected;
    }

    [Benchmark]
    [BenchmarkCategory("Connected")]
    public int DisjointSet_Connected()
    {
        int connected = 0;
        for (int i = 0; i < queryA.Length; i++)
        {
            if (dsFull.Connected(queryA[i], queryB[i]))
                connected++;
        }

        return connected;
    }

    // ---- Components: extract the grouped partition (distinct components x their members) ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Components")]
    public int Dictionary_Components()
    {
        // Distinct component groups, materialized like GetComponents would.
        var groups = new Dictionary<HashSet<int>, List<int>>(ReferenceEqualityComparer.Instance);
        foreach (var kv in dictFull)
        {
            if (!groups.TryGetValue(kv.Value, out List<int>? list))
            {
                list = new List<int>();
                groups[kv.Value] = list;
            }

            list.Add(kv.Key);
        }

        return groups.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Components")]
    public int DisjointSet_Components() => dsFull.GetComponents().Count;

    // ---- builders ----

    private DisjointSet<int> BuildCelerity()
    {
        var ds = new DisjointSet<int>(ItemCount);
        for (int i = 0; i < ItemCount; i++)
            ds.Add(i);
        for (int i = 0; i < edgeA.Length; i++)
            ds.Union(edgeA[i], edgeB[i]);
        return ds;
    }

    private Dictionary<int, HashSet<int>> BuildDictionary()
    {
        var map = new Dictionary<int, HashSet<int>>(ItemCount);
        for (int i = 0; i < ItemCount; i++)
            map[i] = new HashSet<int> { i };

        for (int i = 0; i < edgeA.Length; i++)
        {
            HashSet<int> ca = map[edgeA[i]];
            HashSet<int> cb = map[edgeB[i]];
            if (ReferenceEquals(ca, cb))
                continue;

            // Merge the smaller component into the larger.
            if (ca.Count < cb.Count)
                (ca, cb) = (cb, ca);
            foreach (int m in cb)
            {
                ca.Add(m);
                map[m] = ca;
            }
        }

        return map;
    }
}
