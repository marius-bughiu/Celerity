using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Primitives;

/// <summary>
/// Head-to-head throughput of the span-based <see cref="VarInt"/> codec against the BCL's only public
/// varint path — <c>BinaryWriter.Write7BitEncodedInt64</c> / <c>BinaryReader.Read7BitEncodedInt64</c> over
/// a <see cref="MemoryStream"/> — for encoding and decoding a buffer full of 64-bit values (issue #193).
/// </summary>
/// <remarks>
/// <para>
/// The BCL exposes 7-bit-encoded integers only on <see cref="BinaryWriter"/> / <see cref="BinaryReader"/>,
/// which are bound to a <see cref="Stream"/> and (for a fresh writer/reader) allocate. The <see cref="VarInt"/>
/// methods encode straight over a caller-owned <c>Span&lt;byte&gt;</c> with no stream and no allocation —
/// the workload custom wire codecs and serializers actually run. To keep the comparison about the codec
/// rather than stream construction, the BCL arms reuse a single <see cref="MemoryStream"/> + writer/reader
/// across the loop (rewound each invocation); even so the span form avoids the per-call virtual stream
/// writes entirely.
/// </para>
/// <para>
/// Each category fixes one direction (encode or decode); the BCL form is the baseline and the
/// <c>VarInt</c> form the candidate, so the ratio reads as "the span codec relative to the BCL
/// stream path". This is an isolated microbenchmark, so it lives in the <strong>extended</strong>
/// suite (not the per-PR core regression gate).
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class VarIntBenchmark
{
    private const int ValueCount = 4096;

    private ulong[] values = null!;
    private byte[] spanBuffer = null!;
    private byte[] encoded = null!;
    private int encodedLength;

    private MemoryStream bclStream = null!;
    private BinaryWriter bclWriter = null!;
    private BinaryReader bclReader = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new System.Random(12345);
        values = new ulong[ValueCount];
        Span<byte> buffer = stackalloc byte[8];
        for (int i = 0; i < ValueCount; i++)
        {
            rng.NextBytes(buffer);
            // Bias toward smaller magnitudes (the varint sweet spot) by masking a random byte count.
            int keepBytes = 1 + (i % 8);
            ulong v = BitConverter.ToUInt64(buffer);
            values[i] = keepBytes == 8 ? v : v & ((1UL << (keepBytes * 8)) - 1);
        }

        spanBuffer = new byte[ValueCount * VarInt.MaxVarIntLength64];

        // Pre-encode once so the decode benchmarks have a real varint stream to read.
        int offset = 0;
        foreach (ulong v in values)
        {
            VarInt.TryWriteVarInt(spanBuffer.AsSpan(offset), v, out int written);
            offset += written;
        }
        encodedLength = offset;
        encoded = spanBuffer.AsSpan(0, encodedLength).ToArray();

        bclStream = new MemoryStream(ValueCount * VarInt.MaxVarIntLength64);
        bclWriter = new BinaryWriter(bclStream);
        bclReader = new BinaryReader(bclStream);

        // Pre-fill the BCL stream so the decode arm (which runs in isolation) has data to read.
        foreach (ulong v in values)
            bclWriter.Write7BitEncodedInt64((long)v);
        bclWriter.Flush();
    }

    // ---- Encode ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Encode")]
    public long Encode_BclBinaryWriter()
    {
        bclStream.Position = 0;
        ulong[] v = values;
        for (int i = 0; i < v.Length; i++)
            bclWriter.Write7BitEncodedInt64((long)v[i]);
        return bclStream.Position;
    }

    [Benchmark]
    [BenchmarkCategory("Encode")]
    public int Encode_VarIntSpan()
    {
        var dest = spanBuffer.AsSpan();
        ulong[] v = values;
        int offset = 0;
        for (int i = 0; i < v.Length; i++)
        {
            VarInt.TryWriteVarInt(dest.Slice(offset), v[i], out int written);
            offset += written;
        }
        return offset;
    }

    // ---- Decode ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Decode")]
    public ulong Decode_BclBinaryReader()
    {
        bclStream.Position = 0;
        ulong acc = 0;
        for (int i = 0; i < ValueCount; i++)
            acc += (ulong)bclReader.Read7BitEncodedInt64();
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Decode")]
    public ulong Decode_VarIntSpan()
    {
        var src = (ReadOnlySpan<byte>)encoded;
        ulong acc = 0;
        int offset = 0;
        for (int i = 0; i < ValueCount; i++)
        {
            VarInt.TryReadVarInt(src.Slice(offset), out ulong value, out int read);
            acc += value;
            offset += read;
        }
        return acc;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        bclReader.Dispose();
        bclWriter.Dispose();
        bclStream.Dispose();
    }
}
