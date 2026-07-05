using System.Collections;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Primitives;

/// <summary>
/// Head-to-head throughput of the sequential sub-byte bit I/O pair <see cref="BitWriter"/> /
/// <see cref="BitReader"/> against the BCL's only public bit-packing type,
/// <see cref="System.Collections.BitArray"/>, for packing and unpacking a stream of odd-width bit fields.
/// </summary>
/// <remarks>
/// <para>
/// The BCL has no span-based bit writer: <see cref="BitArray"/> is a heap object that sets one bit at a
/// time and cannot append a multi-bit field, and <c>BinaryPrimitives</c> is byte-granular. The BCL arm
/// therefore packs each N-bit field by setting its N bits individually into a reused <see cref="BitArray"/>
/// and copying the backing bytes out; the <c>BitWriter</c> arm appends each field with a single multi-bit
/// write straight over a caller-owned <c>Span&lt;byte&gt;</c> — the workload bit-packed wire codecs and
/// columnar encoders actually run. The BCL <see cref="BitArray"/> is reused across the loop so the
/// comparison is about the packing, not object construction.
/// </para>
/// <para>
/// Each category fixes one direction (pack or unpack); the BCL form is the baseline and the
/// <c>BitWriter</c> / <c>BitReader</c> form the candidate, so the ratio reads as "the span bit codec
/// relative to the BCL <see cref="BitArray"/> path". This is an isolated microbenchmark, so it lives in
/// the <strong>extended</strong> suite (not the per-PR core regression gate).
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class BitPackingBenchmark
{
    private const int FieldCount = 4096;

    private ulong[] values = null!;
    private int[] widths = null!;
    private byte[] spanBuffer = null!;
    private byte[] encoded = null!;
    private int totalBits;
    private int totalBytes;

    private BitArray bclBits = null!;
    private byte[] bclBytes = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new System.Random(24680);
        values = new ulong[FieldCount];
        widths = new int[FieldCount];
        long bits = 0;
        for (int i = 0; i < FieldCount; i++)
        {
            int w = 1 + rng.Next(32);           // 1..32-bit fields (the common packed-record range)
            widths[i] = w;
            values[i] = (ulong)rng.NextInt64() & (w == 64 ? ulong.MaxValue : (1UL << w) - 1);
            bits += w;
        }

        totalBits = (int)bits;
        totalBytes = BitWriter.ByteCount(totalBits);
        spanBuffer = new byte[totalBytes];

        // Pre-encode once so the unpack benchmarks have a real packed stream to read.
        var writer = new BitWriter(spanBuffer);
        for (int i = 0; i < FieldCount; i++)
            writer.TryWriteBits(values[i], widths[i]);
        encoded = spanBuffer.AsSpan(0, writer.BytesWritten).ToArray();

        bclBits = new BitArray(totalBits);
        bclBytes = new byte[totalBytes];
    }

    // ---- Pack ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Pack")]
    public int Pack_BclBitArray()
    {
        BitArray bits = bclBits;
        bits.SetAll(false);
        int pos = 0;
        for (int i = 0; i < FieldCount; i++)
        {
            ulong v = values[i];
            int w = widths[i];
            for (int b = 0; b < w; b++)
                bits[pos + b] = ((v >> b) & 1UL) != 0;
            pos += w;
        }
        bits.CopyTo(bclBytes, 0);
        return pos;
    }

    [Benchmark]
    [BenchmarkCategory("Pack")]
    public int Pack_BitWriter()
    {
        var writer = new BitWriter(spanBuffer);
        ulong[] v = values;
        int[] w = widths;
        for (int i = 0; i < FieldCount; i++)
            writer.TryWriteBits(v[i], w[i]);
        return writer.BitsWritten;
    }

    // ---- Unpack ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Unpack")]
    public ulong Unpack_BclBitArray()
    {
        var bits = new BitArray(encoded);
        ulong acc = 0;
        int pos = 0;
        for (int i = 0; i < FieldCount; i++)
        {
            int w = widths[i];
            ulong value = 0;
            for (int b = 0; b < w; b++)
                if (bits[pos + b])
                    value |= 1UL << b;
            acc += value;
            pos += w;
        }
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Unpack")]
    public ulong Unpack_BitReader()
    {
        var reader = new BitReader(encoded);
        ulong acc = 0;
        int[] w = widths;
        for (int i = 0; i < FieldCount; i++)
        {
            reader.TryReadBits(w[i], out ulong value);
            acc += value;
        }
        return acc;
    }
}
