using BenchmarkDotNet.Attributes;
using Celerity.Collections;
using Celerity.Hashing;

// MinHashSketch<int, ...> vs HashSet<int>. The question both sides answer is "how similar are
// these two sets?" — the Jaccard similarity |A ∩ B| / |A ∪ B|. The exact answer needs both sets
// in full and an O(|A|) intersection count per comparison; the sketch is a fixed 2 KB of
// registers (256 at the default precision 8) and compares in O(256) however large the sets
// were. The two sets are ItemCount elements each and share half of them (J = 1/3).
//
// Add fills one set from scratch on both sides with no pre-sizing, the same shape as
// HyperLogLogBenchmark. Jaccard compares two already-built sets. The trade-off is exactness:
// the sketch's answer carries a standard error of about sqrt(J(1 - J) / 256) ≈ 0.029 here.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
public class MinHashSketchBenchmark
{
    private int[] keysA = null!;
    private int[] keysB = null!;
    private HashSet<int> setA = null!;
    private HashSet<int> setB = null!;
    private MinHashSketch<int, Int32WangNaiveHasher> sketchA = null!;
    private MinHashSketch<int, Int32WangNaiveHasher> sketchB = null!;

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
        keysB = all[(distinct - ItemCount)..];

        setA = new HashSet<int>(keysA);
        setB = new HashSet<int>(keysB);
        sketchA = new MinHashSketch<int, Int32WangNaiveHasher>(keysA);
        sketchB = new MinHashSketch<int, Int32WangNaiveHasher>(keysB);
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
    public bool MinHashSketch_Add()
    {
        var sketch = new MinHashSketch<int, Int32WangNaiveHasher>();
        foreach (var key in keysA)
            sketch.Add(key);
        return sketch.IsEmpty;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Jaccard")]
    public double HashSet_Jaccard()
    {
        int intersection = 0;
        foreach (int key in setA)
        {
            if (setB.Contains(key))
                intersection++;
        }

        return (double)intersection / (setA.Count + setB.Count - intersection);
    }

    [Benchmark]
    [BenchmarkCategory("Jaccard")]
    public double MinHashSketch_Jaccard()
    {
        return sketchA.EstimateJaccard(sketchB);
    }
}
