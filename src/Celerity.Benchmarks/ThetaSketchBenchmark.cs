using BenchmarkDotNet.Attributes;
using Celerity.Collections;
using Celerity.Hashing;

// ThetaSketch<int, ...> vs HashSet<int>. The question both sides answer is "how many distinct
// elements do these two sets share?" — |A ∩ B|. The exact answer needs both sets in full and an
// O(|A|) membership pass per comparison; the sketch keeps at most 1.5k hash values (k = 4096 at
// the default precision, 64 KB) however large the sets were, and intersects two compact
// snapshots in one merge pass over their sorted values. The two sets are ItemCount elements
// each and share half of them.
//
// Add fills one set from scratch on both sides with no pre-sizing, the same shape as
// HyperLogLogBenchmark and MinHashSketchBenchmark. Intersect counts the overlap of two
// already-built sets; the sketch side intersects compact snapshots taken in setup, the form in
// which sketches are stored and queried repeatedly. At 1,000 elements the sketch is still exact
// (it holds every hash), so the comparison there is a sorted merge against hash probes; at
// 100,000 it samples, and its answer carries a standard error of about 1 / sqrt(0.33 · 4096) of
// the overlap — some 2.8%.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
public class ThetaSketchBenchmark
{
    private int[] keysA = null!;
    private HashSet<int> setA = null!;
    private HashSet<int> setB = null!;
    private CompactThetaSketch<int, Int32WangNaiveHasher> compactA = null!;
    private CompactThetaSketch<int, Int32WangNaiveHasher> compactB = null!;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        // ItemCount * 3 / 2 distinct values: the first ItemCount go to A, the last ItemCount to
        // B, so the middle half is shared.
        int distinct = ItemCount * 3 / 2;
        var values = new HashSet<int>(distinct);
        Random rand = new(42);
        while (values.Count < distinct)
            values.Add(rand.Next(1, int.MaxValue / 2));

        int[] all = values.ToArray();
        keysA = all[..ItemCount];
        int[] keysB = all[(distinct - ItemCount)..];

        setA = new HashSet<int>(keysA);
        setB = new HashSet<int>(keysB);
        compactA = new ThetaSketch<int, Int32WangNaiveHasher>(keysA).Compact();
        compactB = new ThetaSketch<int, Int32WangNaiveHasher>(keysB).Compact();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Add")]
    public int HashSet_Add()
    {
        var set = new HashSet<int>();
        foreach (var key in keysA)
            set.Add(key);
        return set.Count;
    }

    [Benchmark]
    [BenchmarkCategory("Add")]
    public int ThetaSketch_Add()
    {
        var sketch = new ThetaSketch<int, Int32WangNaiveHasher>();
        foreach (var key in keysA)
            sketch.Add(key);
        return sketch.RetainedCount;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Intersect")]
    public int HashSet_Intersect()
    {
        int intersection = 0;
        foreach (int key in setA)
        {
            if (setB.Contains(key))
                intersection++;
        }
        return intersection;
    }

    [Benchmark]
    [BenchmarkCategory("Intersect")]
    public double ThetaSketch_Intersect()
    {
        return CompactThetaSketch<int, Int32WangNaiveHasher>.Intersect(compactA, compactB).EstimateCardinality();
    }
}
