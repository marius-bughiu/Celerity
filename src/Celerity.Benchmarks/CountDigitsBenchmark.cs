using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity;

/// <summary>
/// Head-to-head throughput of <see cref="FastUtils"/>'s base-10 digit counter
/// (<c>CountDigits</c>) against the obvious alternatives — a naive divide-by-ten loop and a
/// floating-point <c>(int)Math.Log10(v) + 1</c> — for 32- and 64-bit values (issue #194).
/// </summary>
/// <remarks>
/// <para>
/// Sizing a buffer before <c>TryFormat</c> or aligning a fixed-width numeric column needs the digit
/// count of a value, not the value formatted. The BCL has a fast LZCNT-based counter but keeps it
/// <c>internal</c>; the only public base-10 log is the floating-point <see cref="System.Math.Log10(double)"/>,
/// which is slow and mis-rounds at exact powers of ten. <c>CountDigits</c> exposes the integer counter:
/// the 32-bit path is a single <c>Log2</c> (LZCNT) plus a table lookup, with no branches and no division.
/// </para>
/// <para>
/// Each category fixes one width; the naive divide-by-ten loop is the baseline and the <c>FastUtils</c>
/// form the candidate, so the ratio reads as "the LZCNT-table counter relative to the obvious loop". This
/// is an isolated microbenchmark, so it lives in the <strong>extended</strong> suite (not the per-PR core
/// regression gate).
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class CountDigitsBenchmark
{
    private const int ValueCount = 4096;

    private uint[] values32 = null!;
    private ulong[] values64 = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(12345);
        values32 = new uint[ValueCount];
        values64 = new ulong[ValueCount];

        Span<byte> buffer = stackalloc byte[8];
        for (int i = 0; i < ValueCount; i++)
        {
            rng.NextBytes(buffer);
            values64[i] = BitConverter.ToUInt64(buffer);
            values32[i] = (uint)values64[i];
        }
    }

    // ---- 32-bit ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Digits32")]
    public int Digits32_NaiveLoop()
    {
        int acc = 0;
        uint[] v = values32;
        for (int i = 0; i < v.Length; i++)
            acc += NaiveCountDigits(v[i]);
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Digits32")]
    public int Digits32_MathLog10()
    {
        int acc = 0;
        uint[] v = values32;
        for (int i = 0; i < v.Length; i++)
            acc += v[i] == 0 ? 1 : (int)Math.Log10(v[i]) + 1;
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Digits32")]
    public int Digits32_FastUtils()
    {
        int acc = 0;
        uint[] v = values32;
        for (int i = 0; i < v.Length; i++)
            acc += FastUtils.CountDigits(v[i]);
        return acc;
    }

    // ---- 64-bit ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Digits64")]
    public int Digits64_NaiveLoop()
    {
        int acc = 0;
        ulong[] v = values64;
        for (int i = 0; i < v.Length; i++)
            acc += NaiveCountDigits(v[i]);
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Digits64")]
    public int Digits64_MathLog10()
    {
        int acc = 0;
        ulong[] v = values64;
        for (int i = 0; i < v.Length; i++)
            acc += v[i] == 0 ? 1 : (int)Math.Log10(v[i]) + 1;
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Digits64")]
    public int Digits64_FastUtils()
    {
        int acc = 0;
        ulong[] v = values64;
        for (int i = 0; i < v.Length; i++)
            acc += FastUtils.CountDigits(v[i]);
        return acc;
    }

    private static int NaiveCountDigits(uint value)
    {
        int digits = 1;
        while (value >= 10)
        {
            value /= 10;
            digits++;
        }
        return digits;
    }

    private static int NaiveCountDigits(ulong value)
    {
        int digits = 1;
        while (value >= 10)
        {
            value /= 10;
            digits++;
        }
        return digits;
    }
}
