using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

/// <summary>
/// Sweeps the integer dictionaries across <see cref="Distribution.Uniform"/>,
/// <see cref="Distribution.Sequential"/>, and <see cref="Distribution.Clustered"/>
/// key shapes for both 1k and 100k items, against the BCL
/// <see cref="Dictionary{TKey, TValue}"/> baseline.
/// </summary>
/// <remarks>
/// The original suite measured a single random distribution only, which hides the
/// whole reason Celerity ships tunable hashers: the cheap XOR-fold hashers win on
/// uniform/sequential keys and can lose on pathological ones. This benchmark makes
/// that trade visible. The <see cref="Distribution.Adversarial"/> shape is covered
/// separately by <see cref="AdversarialHasherBenchmark"/> because it is capped at
/// <see cref="KeyDistributions.MaxAdversarialCount"/> distinct keys.
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class DistributionBenchmark
{
    private int[] keys = null!;
    private Dictionary<int, int> dictionary = null!;
    private IntDictionary<int> intDictionary = null!;

    [Params(Distribution.Uniform, Distribution.Sequential, Distribution.Clustered)]
    public Distribution Distribution;

    [Params(1000, 100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = KeyDistributions.Int32(Distribution, ItemCount);

        dictionary = new Dictionary<int, int>(ItemCount);
        intDictionary = new IntDictionary<int>(ItemCount);
        foreach (var key in keys)
        {
            dictionary[key] = key;
            intDictionary[key] = key;
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Insert")]
    public void Dictionary_Insert()
    {
        var map = new Dictionary<int, int>();
        foreach (var key in keys)
        {
            map[key] = key;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Insert")]
    public void IntDictionary_Insert()
    {
        var map = new IntDictionary<int>();
        foreach (var key in keys)
        {
            map[key] = key;
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public int Dictionary_Lookup()
    {
        int acc = 0;
        foreach (var key in keys)
        {
            acc += dictionary[key];
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int IntDictionary_Lookup()
    {
        int acc = 0;
        foreach (var key in keys)
        {
            acc += intDictionary[key];
        }
        return acc;
    }
}
