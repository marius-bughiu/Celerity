using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Sorting;

// CountingSort vs Array.Sort over a bounded key range. Where RadixSortBenchmark measures the general
// primitive-key case, this measures the shape counting sort is for: many values drawn from few
// distinct keys — enum ordinals, bucket ids, quantized scores — where one histogram pass and one
// run-fill replace an O(n log n) introsort entirely.
//
// Keys are bytes (a 256-wide range), so the Keys arm never even moves an element twice: it rewrites
// each run in place from the counts. The Pairs arm is the stable key+payload form, which needs one
// payload-sized scratch buffer and gets it from the caller.
//
// As in RadixSortBenchmark both arms copy the pristine keys inside the measured method, so the O(n)
// memcpy is charged to both and no [IterationSetup] is needed. ItemCount is the parameter name the
// gh-pages dashboard's benchmark-name parser requires.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class CountingSortBenchmark
{
    private byte[] source = null!;
    private int[] payload = null!;
    private byte[] keys = null!;
    private int[] values = null!;
    private int[] valueScratch = null!;

    [Params(100, 1_000, 100_000, 1_000_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        source = new byte[ItemCount];
        payload = new int[ItemCount];
        rand.NextBytes(source);
        for (int i = 0; i < ItemCount; i++)
        {
            payload[i] = i;
        }

        keys = new byte[ItemCount];
        values = new int[ItemCount];
        valueScratch = new int[ItemCount];
    }

    // ---- Keys: sort bounded-range keys with no payload ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Keys")]
    public byte[] Array_Keys()
    {
        source.CopyTo(keys, 0);
        Array.Sort(keys);
        return keys;
    }

    [Benchmark]
    [BenchmarkCategory("Keys")]
    public byte[] CountingSort_Keys()
    {
        source.CopyTo(keys, 0);
        CountingSort.Sort(keys.AsSpan());
        return keys;
    }

    // ---- Pairs: sort bounded-range keys carrying a parallel payload ----

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
    public int[] CountingSort_Pairs()
    {
        source.CopyTo(keys, 0);
        payload.CopyTo(values, 0);
        CountingSort.SortWithScratch(keys.AsSpan(), values.AsSpan(), valueScratch.AsSpan());
        return values;
    }
}
