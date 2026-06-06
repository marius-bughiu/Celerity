using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;
using Celerity.Hashing;

/// <summary>
/// Scale test at 1M and 5M items. Small maps live in cache and are dominated by
/// per-call overhead; at these sizes the working set spills out of L2/L3 and the
/// numbers are driven by memory traffic and probe-chain length instead — a
/// different regime that the 1k/100k benchmarks do not exercise.
/// </summary>
/// <remarks>
/// Excluded from the per-PR CI regression run (see <c>Program.cs</c>): a single
/// pass builds and tears down tens of millions of entries, which is too slow and
/// too memory-hungry for the same-runner A/B comparison. Run locally with
/// <c>dotnet run -c Release -- --filter '*LargeDataset*'</c>.
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class LargeDatasetBenchmark
{
    private int[] intKeys = null!;
    private long[] longKeys = null!;

    private Dictionary<int, int> intDict = null!;
    private IntDictionary<int> intDictionary = null!;
    private CelerityDictionary<int, int, Int32WangNaiveHasher> celerityDictionary = null!;

    private Dictionary<long, long> longDict = null!;
    private LongDictionary<long> longDictionary = null!;

    [Params(1_000_000, 5_000_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        intKeys = KeyDistributions.Int32(Distribution.Uniform, ItemCount);
        longKeys = KeyDistributions.Int64(Distribution.Uniform, ItemCount);

        intDict = new Dictionary<int, int>(ItemCount);
        intDictionary = new IntDictionary<int>(ItemCount);
        celerityDictionary = new CelerityDictionary<int, int, Int32WangNaiveHasher>(ItemCount);
        longDict = new Dictionary<long, long>(ItemCount);
        longDictionary = new LongDictionary<long>(ItemCount);

        for (int i = 0; i < ItemCount; i++)
        {
            int ik = intKeys[i];
            intDict[ik] = ik;
            intDictionary[ik] = ik;
            celerityDictionary[ik] = ik;

            long lk = longKeys[i];
            longDict[lk] = lk;
            longDictionary[lk] = lk;
        }
    }

    // ── int keys ──────────────────────────────────────────────────────────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Insert-int")]
    public void Dictionary_Insert_Int()
    {
        var map = new Dictionary<int, int>(ItemCount);
        foreach (var key in intKeys)
        {
            map[key] = key;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Insert-int")]
    public void IntDictionary_Insert()
    {
        var map = new IntDictionary<int>(ItemCount);
        foreach (var key in intKeys)
        {
            map[key] = key;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Insert-int")]
    public void CelerityDictionary_Insert()
    {
        var map = new CelerityDictionary<int, int, Int32WangNaiveHasher>(ItemCount);
        foreach (var key in intKeys)
        {
            map[key] = key;
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup-int")]
    public int Dictionary_Lookup_Int()
    {
        int acc = 0;
        foreach (var key in intKeys)
        {
            acc += intDict[key];
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup-int")]
    public int IntDictionary_Lookup()
    {
        int acc = 0;
        foreach (var key in intKeys)
        {
            acc += intDictionary[key];
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup-int")]
    public int CelerityDictionary_Lookup()
    {
        int acc = 0;
        foreach (var key in intKeys)
        {
            acc += celerityDictionary[key];
        }
        return acc;
    }

    // ── long keys ─────────────────────────────────────────────────────────────

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Insert-long")]
    public void Dictionary_Insert_Long()
    {
        var map = new Dictionary<long, long>(ItemCount);
        foreach (var key in longKeys)
        {
            map[key] = key;
        }
    }

    [Benchmark]
    [BenchmarkCategory("Insert-long")]
    public void LongDictionary_Insert()
    {
        var map = new LongDictionary<long>(ItemCount);
        foreach (var key in longKeys)
        {
            map[key] = key;
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Lookup-long")]
    public long Dictionary_Lookup_Long()
    {
        long acc = 0;
        foreach (var key in longKeys)
        {
            acc += longDict[key];
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Lookup-long")]
    public long LongDictionary_Lookup()
    {
        long acc = 0;
        foreach (var key in longKeys)
        {
            acc += longDictionary[key];
        }
        return acc;
    }
}
