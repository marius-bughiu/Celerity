using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

/// <summary>
/// Allocation profile of building each dictionary, with the full
/// <see cref="MemoryDiagnoserAttribute"/> columns (allocated bytes + GC counts).
/// </summary>
/// <remarks>
/// Throughput hides allocation cost: a map that is fast per-op but reallocates its
/// backing arrays on every growth step still pressures the GC. Two cases are
/// measured side by side:
/// <list type="bullet">
/// <item><c>Grow</c> — constructed with no capacity hint, so the backing store is
///   resized repeatedly as it fills. This surfaces the resize/copy churn.</item>
/// <item><c>Presized</c> — constructed with the final count as the capacity hint,
///   so the store is allocated once. This is the steady-state floor.</item>
/// </list>
/// The gap between the two rows is exactly what a caller saves by passing a
/// capacity to the constructor.
/// </remarks>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class MemoryAllocationBenchmark
{
    private int[] keys = null!;

    [Params(100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup() => keys = KeyDistributions.Int32(Distribution.Uniform, ItemCount);

    // ── Grow from default capacity (resize churn included) ──────────────────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Grow")]
    public Dictionary<int, int> Dictionary_Grow()
    {
        var map = new Dictionary<int, int>();
        foreach (var key in keys)
        {
            map[key] = key;
        }
        return map;
    }

    [Benchmark]
    [BenchmarkCategory("Grow")]
    public IntDictionary<int> IntDictionary_Grow()
    {
        var map = new IntDictionary<int>();
        foreach (var key in keys)
        {
            map[key] = key;
        }
        return map;
    }

    [Benchmark]
    [BenchmarkCategory("Grow")]
    public CelerityDictionary<int, int, Int32WangNaiveHasher> CelerityDictionary_Grow()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>();
        foreach (var key in keys)
        {
            map[key] = key;
        }
        return map;
    }

    // ── Presized (single backing allocation) ────────────────────────────────────

    [Benchmark]
    [BenchmarkCategory("Presized")]
    public Dictionary<int, int> Dictionary_Presized()
    {
        var map = new Dictionary<int, int>(ItemCount);
        foreach (var key in keys)
        {
            map[key] = key;
        }
        return map;
    }

    [Benchmark]
    [BenchmarkCategory("Presized")]
    public IntDictionary<int> IntDictionary_Presized()
    {
        var map = new IntDictionary<int>(ItemCount);
        foreach (var key in keys)
        {
            map[key] = key;
        }
        return map;
    }

    [Benchmark]
    [BenchmarkCategory("Presized")]
    public CelerityDictionary<int, int, Int32WangNaiveHasher> CelerityDictionary_Presized()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>(ItemCount);
        foreach (var key in keys)
        {
            map[key] = key;
        }
        return map;
    }
}
