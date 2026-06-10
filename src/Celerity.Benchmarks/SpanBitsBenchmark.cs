using System.Collections;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Primitives;

/// <summary>
/// Head-to-head throughput of <see cref="SpanBits"/>'s non-owning span bit-packing helpers against the
/// BCL's heap-allocated <see cref="System.Collections.BitArray"/> for the three operations where a
/// span-of-<c>ulong</c> with hardware <c>POPCNT</c>/<c>TZCNT</c> pulls ahead: population count, a
/// next-set-bit forward scan, and a dense single-bit set loop (issue #196).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="System.Collections.BitArray"/> is a heap class that stores its bits in a private
/// <c>int[]</c> and exposes only per-index <c>Get</c>/<c>Set</c> — no population count and no set-bit
/// scan — so counting or iterating set bits degrades to a bit-at-a-time loop. <see cref="SpanBits"/>
/// operates over caller-owned <c>Span&lt;ulong&gt;</c> memory (a stack buffer, a slice, a rented array)
/// with no heap object and folds a whole 64-bit word per <c>POPCNT</c> / <c>TZCNT</c>.
/// </para>
/// <para>
/// Each category fixes one operation; the <see cref="System.Collections.BitArray"/> arm is the baseline
/// and the <see cref="SpanBits"/> arm the candidate, so the ratio reads as "the span helper relative to
/// the BCL bit array". This is an isolated microbenchmark, so it lives in the <strong>extended</strong>
/// suite (not the per-PR core regression gate).
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SpanBitsBenchmark
{
    [Params(1024, 65536)]
    public int BitCount;

    private ulong[] words = null!;
    private BitArray bitArray = null!;
    private int[] indices = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(12345);
        words = new ulong[SpanBits.WordCount(BitCount)];
        bitArray = new BitArray(BitCount);

        // ~25% density, the same bits set in both representations.
        int setCount = BitCount / 4;
        indices = new int[setCount];
        for (int i = 0; i < setCount; i++)
        {
            int index = rng.Next(BitCount);
            indices[i] = index;
            SpanBits.Set(words, index);
            bitArray.Set(index, true);
        }
    }

    // ---- PopCount ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PopCount")]
    public int PopCount_BitArray()
    {
        int count = 0;
        BitArray ba = bitArray;
        for (int i = 0; i < ba.Length; i++)
            if (ba[i]) count++;
        return count;
    }

    [Benchmark]
    [BenchmarkCategory("PopCount")]
    public int PopCount_SpanBits()
        => SpanBits.PopCount(words);

    // ---- Scan (iterate every set bit) ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Scan")]
    public int Scan_BitArray()
    {
        int acc = 0;
        BitArray ba = bitArray;
        for (int i = 0; i < ba.Length; i++)
            if (ba[i]) acc += i;
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Scan")]
    public int Scan_SpanBits()
    {
        int acc = 0;
        for (int i = SpanBits.NextSetBit(words, 0); i >= 0; i = SpanBits.NextSetBit(words, i + 1))
            acc += i;
        return acc;
    }

    // ---- Set (dense single-bit writes) ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Set")]
    public void Set_BitArray()
    {
        BitArray ba = bitArray;
        int[] idx = indices;
        for (int i = 0; i < idx.Length; i++)
            ba.Set(idx[i], true);
    }

    [Benchmark]
    [BenchmarkCategory("Set")]
    public void Set_SpanBits()
    {
        ulong[] w = words;
        int[] idx = indices;
        for (int i = 0; i < idx.Length; i++)
            SpanBits.Set(w, idx[i]);
    }
}
