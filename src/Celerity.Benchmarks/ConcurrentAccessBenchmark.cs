using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

/// <summary>
/// Read scalability under concurrent access. Each of <see cref="ThreadCount"/>
/// worker tasks looks up the full key set simultaneously, so the reported time is
/// the wall-clock for <c>ThreadCount × ItemCount</c> lookups served in parallel.
/// </summary>
/// <remarks>
/// Celerity's dictionaries are <b>not</b> thread-safe for writes, but — like the
/// BCL <see cref="Dictionary{TKey, TValue}"/> — concurrent <em>reads</em> against a
/// fully-built, never-mutated instance are safe: the lookup path only reads the
/// backing arrays. This benchmark measures exactly that read-only fan-out and puts
/// it next to <see cref="ConcurrentDictionary{TKey, TValue}"/>, whose thread-safety
/// machinery costs something even on the pure-read path. The takeaway: when the map
/// is build-once / read-many, a plain Celerity dictionary read-shares without the
/// concurrent-collection tax.
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ConcurrentAccessBenchmark
{
    private int[] keys = null!;
    private Dictionary<int, int> dictionary = null!;
    private IntDictionary<int> intDictionary = null!;
    private ConcurrentDictionary<int, int> concurrentDictionary = null!;

    [Params(100_000)]
    public int ItemCount;

    [Params(1, 4, 8)]
    public int ThreadCount;

    [GlobalSetup]
    public void Setup()
    {
        keys = KeyDistributions.Int32(Distribution.Uniform, ItemCount);
        dictionary = new Dictionary<int, int>(ItemCount);
        intDictionary = new IntDictionary<int>(ItemCount);
        concurrentDictionary = new ConcurrentDictionary<int, int>(Environment.ProcessorCount, ItemCount);
        foreach (var key in keys)
        {
            dictionary[key] = key;
            intDictionary[key] = key;
            concurrentDictionary[key] = key;
        }
    }

    private long RunParallel(Func<long> body)
    {
        var tasks = new Task<long>[ThreadCount];
        for (int t = 0; t < ThreadCount; t++)
        {
            tasks[t] = Task.Run(body);
        }
        Task.WaitAll(tasks);

        long total = 0;
        foreach (var task in tasks)
        {
            total += task.Result;
        }
        return total;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ConcurrentLookup")]
    public long Dictionary_ConcurrentLookup() => RunParallel(() =>
    {
        long acc = 0;
        foreach (var key in keys)
        {
            acc += dictionary[key];
        }
        return acc;
    });

    [Benchmark]
    [BenchmarkCategory("ConcurrentLookup")]
    public long IntDictionary_ConcurrentLookup() => RunParallel(() =>
    {
        long acc = 0;
        foreach (var key in keys)
        {
            acc += intDictionary[key];
        }
        return acc;
    });

    [Benchmark]
    [BenchmarkCategory("ConcurrentLookup")]
    public long ConcurrentDictionary_ConcurrentLookup() => RunParallel(() =>
    {
        long acc = 0;
        foreach (var key in keys)
        {
            acc += concurrentDictionary[key];
        }
        return acc;
    });
}
