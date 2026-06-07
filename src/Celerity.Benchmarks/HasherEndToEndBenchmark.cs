using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

/// <summary>
/// Drives each integer hasher <em>through the open-addressed table</em> (insert + lookup on
/// <see cref="IntDictionary{TValue, THasher}"/>) across all four key
/// <see cref="Distribution"/> shapes, against the BCL <see cref="Dictionary{TKey, TValue}"/>
/// baseline. This is the honest companion to <see cref="IntegerHasherBenchmark"/>, which times an
/// isolated <c>Hash()</c> loop.
/// </summary>
/// <remarks>
/// <para>
/// An isolated <c>Hash()</c> microbenchmark flatters the strong hashers and hides the only thing a
/// user feels: dictionary insert/lookup throughput, which depends on collisions, probe length, and
/// cache behaviour — none of which a bare hash call exposes. For an <c>int</c> key
/// <c>GetHashCode()</c> is identity (zero work), so no mixing hasher can ever be "faster" in
/// isolation; the strong hashers earn their cost only by <em>shortening probe chains</em> on the
/// shapes a weak hasher clusters.
/// </para>
/// <para>
/// That is exactly the contrast this benchmark makes visible: on <see cref="Distribution.Uniform"/>
/// and <see cref="Distribution.Sequential"/> keys the cheap <see cref="Int32IdentityHasher"/> /
/// <see cref="Int32WangNaiveHasher"/> win (any mixing is pure overhead), while on
/// <see cref="Distribution.Adversarial"/> keys the naive fold degrades toward O(n) and only the full
/// <see cref="Int32Murmur3Hasher"/> / <see cref="Int32WangHasher"/> avalanche keeps lookups O(1).
/// The deterministic probe-length / collision numbers behind these timings are reported by
/// <see cref="ProbeStatisticsEvaluator"/> (see the <c>--probe-analysis</c> report and
/// <c>docs/performance.md</c>); read the two together.
/// </para>
/// <para>
/// This lives in the <em>extended</em> suite, not the per-PR core gate: the adversarial rows are
/// intentionally slow (the naive hasher's O(n) probe chains), so the matrix is too heavy and too
/// noisy for the regression run. Item counts are capped at
/// <see cref="KeyDistributions.MaxAdversarialCount"/> so the adversarial shape stays buildable.
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class HasherEndToEndBenchmark
{
    private int[] keys = null!;

    private Dictionary<int, int> dictionary = null!;
    private IntDictionary<int, Int32IdentityHasher> identity = null!;
    private IntDictionary<int, Int32WangNaiveHasher> naive = null!;
    private IntDictionary<int, Int32WangHasher> wang = null!;
    private IntDictionary<int, Int32Murmur3Hasher> murmur = null!;

    [Params(Distribution.Uniform, Distribution.Sequential, Distribution.Clustered, Distribution.Adversarial)]
    public Distribution Distribution;

    // Capped at MaxAdversarialCount (0xFFFF) so the adversarial shape is buildable at every count.
    [Params(1000, 10_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = KeyDistributions.Int32(Distribution, ItemCount);

        dictionary = new Dictionary<int, int>(ItemCount);
        identity = new IntDictionary<int, Int32IdentityHasher>(ItemCount);
        naive = new IntDictionary<int, Int32WangNaiveHasher>(ItemCount);
        wang = new IntDictionary<int, Int32WangHasher>(ItemCount);
        murmur = new IntDictionary<int, Int32Murmur3Hasher>(ItemCount);
        foreach (int key in keys)
        {
            dictionary[key] = key;
            identity[key] = key;
            naive[key] = key;
            wang[key] = key;
            murmur[key] = key;
        }
    }

    // ── Insert ────────────────────────────────────────────────────────────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Insert")]
    public void Dictionary_Insert()
    {
        var map = new Dictionary<int, int>(ItemCount);
        foreach (int key in keys)
        {
            map[key] = key;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Insert")]
    public void Identity_Insert()
    {
        var map = new IntDictionary<int, Int32IdentityHasher>(ItemCount);
        foreach (int key in keys)
        {
            map[key] = key;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Insert")]
    public void Naive_Insert()
    {
        var map = new IntDictionary<int, Int32WangNaiveHasher>(ItemCount);
        foreach (int key in keys)
        {
            map[key] = key;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Insert")]
    public void Wang_Insert()
    {
        var map = new IntDictionary<int, Int32WangHasher>(ItemCount);
        foreach (int key in keys)
        {
            map[key] = key;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Insert")]
    public void Murmur3_Insert()
    {
        var map = new IntDictionary<int, Int32Murmur3Hasher>(ItemCount);
        foreach (int key in keys)
        {
            map[key] = key;
        }
    }

    // ── Lookup ────────────────────────────────────────────────────────────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup")]
    public int Dictionary_Lookup()
    {
        int acc = 0;
        foreach (int key in keys)
        {
            acc += dictionary[key];
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int Identity_Lookup()
    {
        int acc = 0;
        foreach (int key in keys)
        {
            acc += identity[key];
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int Naive_Lookup()
    {
        int acc = 0;
        foreach (int key in keys)
        {
            acc += naive[key];
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int Wang_Lookup()
    {
        int acc = 0;
        foreach (int key in keys)
        {
            acc += wang[key];
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup")]
    public int Murmur3_Lookup()
    {
        int acc = 0;
        foreach (int key in keys)
        {
            acc += murmur[key];
        }
        return acc;
    }
}
