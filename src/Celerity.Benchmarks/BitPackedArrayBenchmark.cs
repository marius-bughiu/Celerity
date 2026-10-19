using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// BitPackedArray vs the int[] a caller would otherwise use: the narrowest primitive array that holds a 20-bit value.
// There is no BCL counterpart, since .NET ships no multi-bit integer array narrower than a primitive (BitArray is the
// one-bit case only), so the honest baseline is that int[], marked Baseline in every category and named Array_* so the
// dashboard classifies it as the reference arm.
//
// The type is sold on footprint, and a timing benchmark cannot show that: [MemoryDiagnoser] reports bytes
// allocated during the run, not bytes the structure retains. The retained comparison (1.6x smaller than the
// int[] at 20 bits a value) is pinned by MemoryUsageInBytes and carried in the docs. What these categories
// measure is the price of the packing on each access path:
//
//   Access    — random reads; the array's single load against two loads, two shifts and a mask.
//   Set       — random writes; the array's single store against a read-modify-write of two words.
//   Enumerate — a sequential pass over every element.
//   Build     — packing a ulong[] of values, against narrowing the same ulong[] into an int[].
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class BitPackedArrayBenchmark
{
    private const int QueryCount = 1000;
    private const int Bits = 20;

    private ulong[] source = null!;
    private int[] array = null!;
    private BitPackedArray packed = null!;
    private int[] indices = null!;
    private ulong[] writes = null!;

    // Named ItemCount so the gh-pages dashboard's benchmark-name parser picks up the parameter the same way it
    // does for every other collection; the sweep matches BitSet and EliasFanoSequence.
    [Params(1024, 1_000_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);
        const int Bound = 1 << Bits;

        source = new ulong[ItemCount];
        array = new int[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            int value = rand.Next(Bound);
            source[i] = (ulong)value;
            array[i] = value;
        }

        packed = new BitPackedArray(source, Bits);

        indices = new int[QueryCount];
        writes = new ulong[QueryCount];
        for (int i = 0; i < QueryCount; i++)
        {
            indices[i] = rand.Next(ItemCount);
            writes[i] = (ulong)rand.Next(Bound);
        }
    }

    // ---- Access: random reads ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Access")]
    public long Array_Access()
    {
        long sink = 0;
        for (int i = 0; i < indices.Length; i++)
            sink += array[indices[i]];
        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("Access")]
    public ulong BitPackedArray_Access()
    {
        ulong sink = 0;
        for (int i = 0; i < indices.Length; i++)
            sink += packed[indices[i]];
        return sink;
    }

    // ---- Set: random writes ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Set")]
    public void Array_Set()
    {
        for (int i = 0; i < indices.Length; i++)
            array[indices[i]] = (int)writes[i];
    }

    [Benchmark]
    [BenchmarkCategory("Set")]
    public void BitPackedArray_Set()
    {
        for (int i = 0; i < indices.Length; i++)
            packed[indices[i]] = writes[i];
    }

    // ---- Enumerate: one sequential pass over every element ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public long Array_Enumerate()
    {
        long sink = 0;
        foreach (int value in array)
            sink += value;
        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public ulong BitPackedArray_Enumerate()
    {
        ulong sink = 0;
        foreach (ulong value in packed)
            sink += value;
        return sink;
    }

    // ---- Build: packing the values, against narrowing them into an int[] ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public int[] Array_Build()
    {
        int[] result = new int[source.Length];
        for (int i = 0; i < source.Length; i++)
            result[i] = (int)source[i];
        return result;
    }

    [Benchmark]
    [BenchmarkCategory("Build")]
    public BitPackedArray BitPackedArray_Build() => new(source, Bits);
}
