using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Primitives;
using Celerity.Statistics;

// AliasTable vs the weighted draw a caller writes instead. The BCL has no weighted sampler (Random.GetItems is
// uniform only), so the baseline is the idiom everyone reaches for: a double[] of prefix sums and, per draw,
// one uniform double scaled by the total and located with Array.BinarySearch. Both sides draw from the same
// Pcg32 stream, so the generator's cost is identical and the difference is the lookup.
//
// Two ops:
//
//   Sample — Draws weighted draws per invocation from a table built once in GlobalSetup. The prefix-sum search
//            costs O(log n) dependent loads per draw and the alias table one multiply and one load, so the gap
//            should widen with ItemCount as the prefix-sum array leaves each cache level.
//   Build  — constructing the sampler from the weights: the prefix-sum array is one pass, while the alias table
//            is a normalization pass, a sum, a share pass and Vose's pairing. Expect it to lose. The question is
//            by how much, and how many draws it takes to pay that back.
//
// ItemCount is the number of outcomes. The weights are a product of two uniforms plus a floor: skewed, with no
// zeros, which is the shape of a real traffic split or loot table.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class AliasTableBenchmark
{
    private const int Draws = 1_000;

    private double[] weights = null!;
    private double[] cumulative = null!;
    private double total;
    private AliasTable table = null!;
    private Pcg32 aliasRng;
    private Pcg32 arrayRng;

    [Params(100, 10_000, 1_000_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        weights = new double[ItemCount];
        for (int i = 0; i < weights.Length; i++)
        {
            weights[i] = (random.NextDouble() * random.NextDouble()) + 1e-3d;
        }

        cumulative = BuildPrefixSums(weights);
        total = cumulative[^1];
        table = new AliasTable(weights);
        aliasRng = new Pcg32(7UL);
        arrayRng = new Pcg32(7UL);
    }

    [BenchmarkCategory("Sample")]
    [Benchmark(Baseline = true)]
    public long Array_Sample()
    {
        double[] sums = cumulative;
        long checksum = 0;
        for (int i = 0; i < Draws; i++)
        {
            double u = arrayRng.NextDouble() * total;
            int index = Array.BinarySearch(sums, u);
            index = index < 0 ? ~index : index + 1;
            checksum += index < sums.Length ? index : sums.Length - 1;
        }

        return checksum;
    }

    [BenchmarkCategory("Sample")]
    [Benchmark]
    public long AliasTable_Sample()
    {
        long checksum = 0;
        for (int i = 0; i < Draws; i++)
        {
            checksum += table.Sample(ref aliasRng);
        }

        return checksum;
    }

    [BenchmarkCategory("Build")]
    [Benchmark(Baseline = true)]
    public double[] Array_Build() => BuildPrefixSums(weights);

    [BenchmarkCategory("Build")]
    [Benchmark]
    public AliasTable AliasTable_Build() => new AliasTable(weights);

    private static double[] BuildPrefixSums(double[] source)
    {
        var sums = new double[source.Length];
        double running = 0d;
        for (int i = 0; i < source.Length; i++)
        {
            running += source[i];
            sums[i] = running;
        }

        return sums;
    }
}
