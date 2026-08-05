using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Sorting;

// RadixSort vs Array.Sort over int keys — the comparison the Celerity.Sorting package exists to make.
// Array.Sort is a scalar comparison introsort for primitive keys on every current runtime, so the
// contest is O(n log n) mispredicting comparisons against four branch-free counting passes.
//
// Both arms copy the pristine keys into a working buffer inside the measured method, so neither needs
// an [IterationSetup] (which would force InvocationCount=1 and add its own noise) and the O(n) memcpy
// is charged identically to both. The Celerity arms use the SortWithScratch overloads with buffers
// rented once in [GlobalSetup] — the form a hot loop should use — so the rows measure the sort and
// not ArrayPool traffic.
//
// The sweep starts at 100 deliberately: radix is expected to LOSE there, and the crossover is a number
// this benchmark is supposed to report rather than assert. ItemCount is the parameter name the
// gh-pages dashboard's benchmark-name parser requires.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RadixSortBenchmark
{
    private int[] source = null!;
    private int[] payload = null!;
    private int[] keys = null!;
    private int[] values = null!;
    private int[] indices = null!;
    private int[] keyScratch = null!;
    private int[] valueScratch = null!;

    [Params(100, 1_000, 100_000, 1_000_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        source = new int[ItemCount];
        payload = new int[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            source[i] = rand.Next(int.MinValue, int.MaxValue);
            payload[i] = i;
        }

        keys = new int[ItemCount];
        values = new int[ItemCount];
        indices = new int[ItemCount];
        keyScratch = new int[ItemCount];
        valueScratch = new int[ItemCount];
    }

    // ---- Keys: sort primitive keys with no payload ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Keys")]
    public int[] Array_Keys()
    {
        source.CopyTo(keys, 0);
        Array.Sort(keys);
        return keys;
    }

    [Benchmark]
    [BenchmarkCategory("Keys")]
    public int[] RadixSort_Keys()
    {
        source.CopyTo(keys, 0);
        RadixSort.SortWithScratch(keys.AsSpan(), keyScratch.AsSpan());
        return keys;
    }

    // ---- Pairs: sort keys carrying a parallel payload ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Pairs")]
    public int[] Array_Pairs()
    {
        source.CopyTo(keys, 0);
        payload.CopyTo(values, 0);
        Array.Sort(keys, values);
        return values;
    }

    [Benchmark]
    [BenchmarkCategory("Pairs")]
    public int[] RadixSort_Pairs()
    {
        source.CopyTo(keys, 0);
        payload.CopyTo(values, 0);
        RadixSort.SortWithScratch(keys.AsSpan(), values.AsSpan(), keyScratch.AsSpan(), valueScratch.AsSpan());
        return values;
    }

    // ---- ArgSort: produce the sorting permutation without moving the keys ----
    //
    // The BCL has no argsort, so the baseline is what a caller writes instead: copy the keys, fill an
    // identity index array, and hand both to the two-array Array.Sort. Both arms therefore pay for the
    // key copy and the identity fill.

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ArgSort")]
    public int[] Array_ArgSort()
    {
        source.CopyTo(keys, 0);
        for (int i = 0; i < indices.Length; i++)
        {
            indices[i] = i;
        }

        Array.Sort(keys, indices);
        return indices;
    }

    [Benchmark]
    [BenchmarkCategory("ArgSort")]
    public int[] RadixSort_ArgSort()
    {
        RadixSort.ArgSort(source, indices.AsSpan());
        return indices;
    }
}
