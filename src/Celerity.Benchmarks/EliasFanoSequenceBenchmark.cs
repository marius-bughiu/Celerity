using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Collections;

// EliasFanoSequence vs the long[] it compresses. There is no BCL counterpart — .NET ships no compressed integer
// sequence of any kind — so the honest baseline is the sorted array a caller already holds, marked Baseline in
// every category and named Array_* so the dashboard classifies it as the reference arm.
//
// The type is sold on footprint, and a timing benchmark cannot show that: [MemoryDiagnoser] reports bytes
// allocated during the run, not bytes the built structure retains. The retained comparison (about 6.5x smaller
// on this data at 1,000,000 values) is pinned by MemoryUsageInBytes and carried in the docs. What these
// categories measure is the price of the compression on each read path:
//
//   Access     — random positional reads; the array's single load against a sampled select and a low-bit read.
//   LowerBound — "first value at or above x" for random x; Array.BinarySearch against two zero-selects and a
//                binary search of the run that shares x's high part.
//   Enumerate  — a sequential pass; the array walk against the decoder's walk over the high vector's set bits.
//   Build      — the encoding itself, against copying the array.
//
// The data is a monotone sequence with gaps drawn uniformly from [1, 200), the shape of record offsets or
// posting lists, so the encoding splits at L = 6 low bits.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class EliasFanoSequenceBenchmark
{
    private const int QueryCount = 1000;

    private long[] values = null!;
    private EliasFanoSequence sequence = null!;
    private int[] indices = null!;
    private long[] probes = null!;

    // Named ItemCount so the gh-pages dashboard's benchmark-name parser picks up the parameter the same way it
    // does for every other collection; the sweep matches RankSelectBitVector and WaveletTree.
    [Params(1024, 1_000_000)]
    public int ItemCount;

    [GlobalSetup]
    public void Setup()
    {
        var rand = new Random(42);

        values = new long[ItemCount];
        long current = 0;
        for (int i = 0; i < values.Length; i++)
        {
            current += rand.Next(1, 200);
            values[i] = current;
        }

        sequence = new EliasFanoSequence(values);

        indices = new int[QueryCount];
        probes = new long[QueryCount];
        for (int i = 0; i < QueryCount; i++)
        {
            indices[i] = rand.Next(ItemCount);
            probes[i] = rand.NextInt64(0, current + 1);
        }
    }

    // ---- Access: random positional reads ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Access")]
    public long Array_Access()
    {
        long sink = 0;
        for (int i = 0; i < indices.Length; i++)
            sink += values[indices[i]];
        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("Access")]
    public long EliasFanoSequence_Access()
    {
        long sink = 0;
        for (int i = 0; i < indices.Length; i++)
            sink += sequence[indices[i]];
        return sink;
    }

    // ---- LowerBound: the position of the first value at or above a random probe ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("LowerBound")]
    public long Array_LowerBound()
    {
        long sink = 0;
        for (int i = 0; i < probes.Length; i++)
        {
            int index = Array.BinarySearch(values, probes[i]);
            sink += index >= 0 ? index : ~index;
        }
        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("LowerBound")]
    public long EliasFanoSequence_LowerBound()
    {
        long sink = 0;
        for (int i = 0; i < probes.Length; i++)
            sink += sequence.CountLessThan(probes[i]);
        return sink;
    }

    // ---- Enumerate: one sequential pass over every value ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public long Array_Enumerate()
    {
        long sink = 0;
        foreach (long value in values)
            sink += value;
        return sink;
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public long EliasFanoSequence_Enumerate()
    {
        long sink = 0;
        foreach (long value in sequence)
            sink += value;
        return sink;
    }

    // ---- Build: encoding the sequence, against holding a copy of the array ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Build")]
    public long[] Array_Build() => (long[])values.Clone();

    [Benchmark]
    [BenchmarkCategory("Build")]
    public EliasFanoSequence EliasFanoSequence_Build() => new(values);
}
