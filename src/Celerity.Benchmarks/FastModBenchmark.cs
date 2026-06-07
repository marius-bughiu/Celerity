using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity;

/// <summary>
/// Head-to-head throughput of <see cref="FastUtils"/>'s Lemire reciprocal modulo / division
/// (<c>FastMod</c> / <c>FastDiv</c>) against the hardware <c>%</c> / <c>/</c> operators, for a divisor
/// fixed at run time (issue #191).
/// </summary>
/// <remarks>
/// <para>
/// The divisor is held in a <see cref="Params"/> field, so the JIT sees it as a run-time value and emits a
/// real hardware <c>DIV</c> for the operator baselines — it <em>cannot</em> strength-reduce <c>x % d</c> /
/// <c>x / d</c> into shifts the way it would for a compile-time constant. That is exactly the workload
/// <c>FastMod</c> targets: the same divisor reused across millions of operations (hash buckets, ring buffers,
/// sharding, rate limiters), where precomputing the reciprocal once amortizes to a 2–4× win because the
/// multiply-based form replaces the long-latency, non-pipelining <c>DIV</c>.
/// </para>
/// <para>
/// Each category fixes one operation/width (32- or 64-bit modulo or division); the operator form is the
/// baseline and the <c>FastUtils</c> form the candidate, so each ratio reads as "the reciprocal trick
/// relative to the hardware instruction". This is an isolated microbenchmark, so it lives in the
/// <strong>extended</strong> suite (not the per-PR core regression gate).
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class FastModBenchmark
{
    private const int ValueCount = 4096;

    private uint[] values32 = null!;
    private ulong[] values64 = null!;
    private ulong multiplier32;
    private UInt128 multiplier64;

    /// <summary>The 32-bit divisor, fixed at run time so the operator baselines emit a real <c>DIV</c>.</summary>
    [Params(97u, 1000u)]
    public uint Divisor32;

    /// <summary>The 64-bit divisor, fixed at run time so the operator baselines emit a real <c>DIV</c>.</summary>
    [Params(1_000_000_007UL)]
    public ulong Divisor64;

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

        multiplier32 = FastUtils.GetFastModMultiplier(Divisor32);
        multiplier64 = FastUtils.GetFastModMultiplier(Divisor64);
    }

    // ---- 32-bit modulo ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Mod32")]
    public uint Mod32_Operator()
    {
        uint d = Divisor32, acc = 0;
        uint[] v = values32;
        for (int i = 0; i < v.Length; i++)
            acc += v[i] % d;
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Mod32")]
    public uint Mod32_FastMod()
    {
        uint d = Divisor32, acc = 0;
        ulong m = multiplier32;
        uint[] v = values32;
        for (int i = 0; i < v.Length; i++)
            acc += FastUtils.FastMod(v[i], d, m);
        return acc;
    }

    // ---- 32-bit division ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Div32")]
    public uint Div32_Operator()
    {
        uint d = Divisor32, acc = 0;
        uint[] v = values32;
        for (int i = 0; i < v.Length; i++)
            acc += v[i] / d;
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Div32")]
    public uint Div32_FastDiv()
    {
        uint acc = 0;
        ulong m = multiplier32;
        uint[] v = values32;
        for (int i = 0; i < v.Length; i++)
            acc += FastUtils.FastDiv(v[i], m);
        return acc;
    }

    // ---- 64-bit modulo ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Mod64")]
    public ulong Mod64_Operator()
    {
        ulong d = Divisor64, acc = 0;
        ulong[] v = values64;
        for (int i = 0; i < v.Length; i++)
            acc += v[i] % d;
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Mod64")]
    public ulong Mod64_FastMod()
    {
        ulong d = Divisor64, acc = 0;
        UInt128 m = multiplier64;
        ulong[] v = values64;
        for (int i = 0; i < v.Length; i++)
            acc += FastUtils.FastMod(v[i], d, m);
        return acc;
    }

    // ---- 64-bit division ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Div64")]
    public ulong Div64_Operator()
    {
        ulong d = Divisor64, acc = 0;
        ulong[] v = values64;
        for (int i = 0; i < v.Length; i++)
            acc += v[i] / d;
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Div64")]
    public ulong Div64_FastDiv()
    {
        ulong acc = 0;
        UInt128 m = multiplier64;
        ulong[] v = values64;
        for (int i = 0; i < v.Length; i++)
            acc += FastUtils.FastDiv(v[i], m);
        return acc;
    }
}
