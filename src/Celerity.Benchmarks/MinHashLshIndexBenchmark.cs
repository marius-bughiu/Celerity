using BenchmarkDotNet.Attributes;
using Celerity.Collections;
using Celerity.Hashing;

// MinHashLshIndex<int, int, ...> vs a List<MinHashSketch<int, ...>> scanned with EstimateJaccard.
// The question both sides answer is "which stored sets are near-duplicates of this one?".
// ItemCount is the number of stored sets, each 200 random elements sketched at the default
// precision 8. Every 100th set is an original followed by four copies with 10% of their elements
// replaced (Jaccard ≈ 0.82); the rest are unrelated.
//
// The scan is the honest baseline: MinHashSketch's own answer to the question, one O(256)
// EstimateJaccard per stored set, keeping those at or above 0.5. The index is built for the same
// 0.5 threshold and returns candidates without verifying them; at these similarities both sides
// return the original and its four copies. Each invocation runs 16 queries spread over the
// originals rather than one query replayed (at 1,000 sets there are only ten originals, so some
// appear twice).
//
// Building the index is not measured against the list here: appending a reference to a List is
// not the work the index replaces.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
public class MinHashLshIndexBenchmark
{
    private const int ElementsPerSet = 200;
    private const int QueriesPerInvocation = 16;
    private const double Threshold = 0.5;

    private List<MinHashSketch<int, Int32WangNaiveHasher>> sketches = null!;
    private MinHashSketch<int, Int32WangNaiveHasher>[] queries = null!;
    private MinHashLshIndex<int, int, Int32WangNaiveHasher> index = null!;
    private readonly List<int> results = new();

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);
        var sets = new int[ItemCount][];
        sketches = new List<MinHashSketch<int, Int32WangNaiveHasher>>(ItemCount);
        for (int i = 0; i < ItemCount; i++)
        {
            int[] set;
            if (i % 100 is >= 1 and <= 4)
            {
                set = (int[])sets[i - i % 100].Clone();
                for (int k = 0; k < ElementsPerSet / 10; k++)
                    set[rand.Next(ElementsPerSet)] = rand.Next();
            }
            else
            {
                set = new int[ElementsPerSet];
                for (int k = 0; k < set.Length; k++)
                    set[k] = rand.Next();
            }

            sets[i] = set;
            sketches.Add(new MinHashSketch<int, Int32WangNaiveHasher>(set));
        }

        index = new MinHashLshIndex<int, int, Int32WangNaiveHasher>(Threshold);
        for (int i = 0; i < ItemCount; i++)
            index.Add(i, sketches[i]);

        // Sixteen originals, spread across the corpus.
        int originals = ItemCount / 100;
        queries = new MinHashSketch<int, Int32WangNaiveHasher>[QueriesPerInvocation];
        for (int q = 0; q < queries.Length; q++)
            queries[q] = sketches[q * originals / QueriesPerInvocation * 100];
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Query")]
    public int List_Query()
    {
        int found = 0;
        foreach (var query in queries)
        {
            results.Clear();
            for (int i = 0; i < sketches.Count; i++)
            {
                if (query.EstimateJaccard(sketches[i]) >= Threshold)
                    results.Add(i);
            }

            found += results.Count;
        }

        return found;
    }

    [Benchmark]
    [BenchmarkCategory("Query")]
    public int MinHashLshIndex_Query()
    {
        int found = 0;
        foreach (var query in queries)
        {
            results.Clear();
            found += index.Query(query, results);
        }

        return found;
    }
}
