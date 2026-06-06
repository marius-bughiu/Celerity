using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

/// <summary>
/// Isolates the cost of cache misses by holding the work constant and varying only
/// the <em>order</em> in which keys are probed.
/// </summary>
/// <remarks>
/// The key set is <see cref="Distribution.Sequential"/>, so each key maps to a
/// bucket near its neighbours. Probing the keys in their natural order then walks
/// the backing arrays front-to-back (cache-friendly, hardware-prefetchable); probing
/// a shuffled copy of the <em>same</em> keys touches the same buckets in random
/// order, defeating the prefetcher and turning most lookups into cache misses. The
/// in-order vs shuffled gap is the memory-latency tax, and comparing
/// <see cref="IntDictionary{TValue}"/> (parallel <c>int[]</c> key / value arrays)
/// against the BCL <see cref="Dictionary{TKey, TValue}"/> (array of entry structs)
/// shows how each layout pays that tax. Run at 1M items so the working set spills
/// well past L2.
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class CacheLocalityBenchmark
{
    private int[] inOrderKeys = null!;
    private int[] shuffledKeys = null!;
    private Dictionary<int, int> dictionary = null!;
    private IntDictionary<int> intDictionary = null!;

    [Params(1_000_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        inOrderKeys = KeyDistributions.Int32(Distribution.Sequential, ItemCount);
        shuffledKeys = KeyDistributions.Shuffle(inOrderKeys);

        dictionary = new Dictionary<int, int>(ItemCount);
        intDictionary = new IntDictionary<int>(ItemCount);
        foreach (var key in inOrderKeys)
        {
            dictionary[key] = key;
            intDictionary[key] = key;
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("InOrder")]
    public int Dictionary_InOrder()
    {
        int acc = 0;
        foreach (var key in inOrderKeys)
        {
            acc += dictionary[key];
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("InOrder")]
    public int IntDictionary_InOrder()
    {
        int acc = 0;
        foreach (var key in inOrderKeys)
        {
            acc += intDictionary[key];
        }
        return acc;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Shuffled")]
    public int Dictionary_Shuffled()
    {
        int acc = 0;
        foreach (var key in shuffledKeys)
        {
            acc += dictionary[key];
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Shuffled")]
    public int IntDictionary_Shuffled()
    {
        int acc = 0;
        foreach (var key in shuffledKeys)
        {
            acc += intDictionary[key];
        }
        return acc;
    }
}
