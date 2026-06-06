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
///
/// A third <c>FromCollection</c> case builds each dictionary from a known-count
/// <see cref="ICollection{T}"/> source via the <c>IEnumerable</c> constructor.
/// That constructor sizes the backing store from the source's <c>Count</c>; the
/// fix for issue #27 adds the load-factor headroom so the whole source fits below
/// the resize threshold, eliminating the one rehash-and-copy a count-sized table
/// would otherwise pay near the end of the bulk fill.
/// </remarks>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class MemoryAllocationBenchmark
{
    private int[] keys = null!;
    private KeyValuePair<int, int>[] pairs = null!;

    [Params(100_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = KeyDistributions.Int32(Distribution.Uniform, ItemCount);
        // Distinct sequential keys: the IEnumerable constructor uses Add, which
        // rejects duplicates, so the source must be collision-free.
        pairs = new KeyValuePair<int, int>[ItemCount];
        for (int i = 0; i < ItemCount; i++)
            pairs[i] = new KeyValuePair<int, int>(i, i);
    }

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

    // ── Bulk-built from a known-count collection (issue #27 sizing fix) ──────────

    [Benchmark]
    [BenchmarkCategory("FromCollection")]
    public Dictionary<int, int> Dictionary_FromCollection() => new Dictionary<int, int>(pairs);

    [Benchmark]
    [BenchmarkCategory("FromCollection")]
    public IntDictionary<int> IntDictionary_FromCollection() => new IntDictionary<int>(pairs);

    [Benchmark]
    [BenchmarkCategory("FromCollection")]
    public CelerityDictionary<int, int, Int32WangNaiveHasher> CelerityDictionary_FromCollection()
        => new CelerityDictionary<int, int, Int32WangNaiveHasher>(pairs);
}
