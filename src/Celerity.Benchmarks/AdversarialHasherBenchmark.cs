using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

/// <summary>
/// The worst case, on purpose. Runs the <see cref="Distribution.Adversarial"/> key
/// set — keys engineered to collapse onto a tiny set of buckets under the naive
/// XOR-fold hasher — through three dictionaries:
/// <list type="bullet">
/// <item>BCL <see cref="Dictionary{TKey, TValue}"/> (baseline): its prime/Fibonacci
///   mix neutralizes the attack, so it stays O(1).</item>
/// <item><see cref="IntDictionary{TValue}"/> with the default
///   <see cref="Int32WangNaiveHasher"/>: probe chains degrade toward O(n) — the
///   failure mode the README warns about.</item>
/// <item><see cref="IntDictionary{TValue, THasher}"/> with
///   <see cref="Int32Murmur3Hasher"/>: a full finalizer avalanches the same keys
///   back to a uniform spread, restoring O(1).</item>
/// </list>
/// This is the empirical backing for the "choose the right hasher" guidance: the
/// gap between the naive and Murmur3 rows is the cost of getting that choice wrong.
/// Item counts are bounded by <see cref="KeyDistributions.MaxAdversarialCount"/>.
/// </summary>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class AdversarialHasherBenchmark
{
    private int[] keys = null!;
    private Dictionary<int, int> dictionary = null!;
    private IntDictionary<int, Int32WangNaiveHasher> naive = null!;
    private IntDictionary<int, Int32Murmur3Hasher> murmur = null!;

    [Params(1000, 10_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = KeyDistributions.Int32(Distribution.Adversarial, ItemCount);

        dictionary = new Dictionary<int, int>(ItemCount);
        naive = new IntDictionary<int, Int32WangNaiveHasher>(ItemCount);
        murmur = new IntDictionary<int, Int32Murmur3Hasher>(ItemCount);
        foreach (var key in keys)
        {
            dictionary[key] = key;
            naive[key] = key;
            murmur[key] = key;
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
    public int IntDictionary_Naive_Lookup()
    {
        int acc = 0;
        foreach (var key in keys)
        {
            acc += naive[key];
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int IntDictionary_Murmur3_Lookup()
    {
        int acc = 0;
        foreach (var key in keys)
        {
            acc += murmur[key];
        }
        return acc;
    }
}
