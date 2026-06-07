using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Primitives;

/// <summary>
/// Head-to-head throughput of the Celerity struct PRNG suite (<see cref="SplitMix64"/>,
/// <see cref="Xoshiro256StarStar"/>, <see cref="Xoroshiro128Plus"/>, <see cref="WyRand"/>,
/// <see cref="Pcg32"/>) against <see cref="System.Random"/> — both the shared instance and a
/// <strong>seeded</strong> <c>new Random(seed)</c> — across the three workloads users actually run
/// (a raw 64-bit draw, a unit-interval double, and a bounded integer) (issue #192).
/// </summary>
/// <remarks>
/// <para>
/// The seeded <c>new Random(seed)</c> is the headline baseline: it is the reproducible path people reach for,
/// yet it is a heap class behind virtual dispatch and (when seeded) falls back to the legacy Knuth
/// subtractive generator. The struct generators are value types with no virtual dispatch, so the
/// <c>NextUInt64</c> call inlines through the <c>where TRng : struct, IRandomSource</c> constraint and the
/// whole loop allocates nothing. <c>Random.Shared</c> is included as the unseeded reference.
/// </para>
/// <para>
/// Each category fixes one workload; the seeded <c>Random</c> form is the baseline and each struct PRNG a
/// candidate, so the ratio reads as "the value-type generator relative to seeded <c>System.Random</c>". This
/// is an isolated microbenchmark, so it lives in the <strong>extended</strong> suite (not the per-PR core
/// regression gate). For the 64-bit workload the <c>System.Random</c> baseline uses <c>NextInt64()</c> (a
/// 63-bit non-negative draw) as the closest throughput proxy — it has no full-width <c>ulong</c> primitive.
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class PrngBenchmark
{
    private const int Draws = 4096;
    private const ulong Seed = 0x9E3779B97F4A7C15UL;
    private const int SeedInt = 12345;
    private const int Bound = 1000;

    private Random sysSeeded = null!;
    private Random sysShared = null!;
    private SplitMix64 splitMix;
    private Xoshiro256StarStar xoshiro;
    private Xoroshiro128Plus xoroshiro;
    private WyRand wyRand;
    private Pcg32 pcg;

    [GlobalSetup]
    public void Setup()
    {
        sysSeeded = new Random(SeedInt);
        sysShared = Random.Shared;
        splitMix = new SplitMix64(Seed);
        xoshiro = new Xoshiro256StarStar(Seed);
        xoroshiro = new Xoroshiro128Plus(Seed);
        wyRand = new WyRand(Seed);
        pcg = new Pcg32(Seed);
    }

    // ---- raw 64-bit draw ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("NextULong")]
    public ulong NextULong_SystemSeeded()
    {
        ulong acc = 0;
        for (int i = 0; i < Draws; i++) acc += (ulong)sysSeeded.NextInt64();
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("NextULong")]
    public ulong NextULong_SystemShared()
    {
        ulong acc = 0;
        for (int i = 0; i < Draws; i++) acc += (ulong)sysShared.NextInt64();
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("NextULong")]
    public ulong NextULong_SplitMix64()
    {
        ulong acc = 0;
        for (int i = 0; i < Draws; i++) acc += splitMix.NextUInt64();
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("NextULong")]
    public ulong NextULong_Xoshiro256StarStar()
    {
        ulong acc = 0;
        for (int i = 0; i < Draws; i++) acc += xoshiro.NextUInt64();
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("NextULong")]
    public ulong NextULong_Xoroshiro128Plus()
    {
        ulong acc = 0;
        for (int i = 0; i < Draws; i++) acc += xoroshiro.NextUInt64();
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("NextULong")]
    public ulong NextULong_WyRand()
    {
        ulong acc = 0;
        for (int i = 0; i < Draws; i++) acc += wyRand.NextUInt64();
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("NextULong")]
    public ulong NextULong_Pcg32()
    {
        ulong acc = 0;
        for (int i = 0; i < Draws; i++) acc += pcg.NextUInt64();
        return acc;
    }

    // ---- unit-interval double ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("NextDouble")]
    public double NextDouble_SystemSeeded()
    {
        double acc = 0;
        for (int i = 0; i < Draws; i++) acc += sysSeeded.NextDouble();
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("NextDouble")]
    public double NextDouble_SystemShared()
    {
        double acc = 0;
        for (int i = 0; i < Draws; i++) acc += sysShared.NextDouble();
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("NextDouble")]
    public double NextDouble_SplitMix64()
    {
        double acc = 0;
        for (int i = 0; i < Draws; i++) acc += splitMix.NextDouble();
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("NextDouble")]
    public double NextDouble_Xoshiro256StarStar()
    {
        double acc = 0;
        for (int i = 0; i < Draws; i++) acc += xoshiro.NextDouble();
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("NextDouble")]
    public double NextDouble_Xoroshiro128Plus()
    {
        double acc = 0;
        for (int i = 0; i < Draws; i++) acc += xoroshiro.NextDouble();
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("NextDouble")]
    public double NextDouble_WyRand()
    {
        double acc = 0;
        for (int i = 0; i < Draws; i++) acc += wyRand.NextDouble();
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("NextDouble")]
    public double NextDouble_Pcg32()
    {
        double acc = 0;
        for (int i = 0; i < Draws; i++) acc += pcg.NextDouble();
        return acc;
    }

    // ---- bounded integer ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("NextBounded")]
    public long NextBounded_SystemSeeded()
    {
        long acc = 0;
        for (int i = 0; i < Draws; i++) acc += sysSeeded.Next(0, Bound);
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("NextBounded")]
    public long NextBounded_SystemShared()
    {
        long acc = 0;
        for (int i = 0; i < Draws; i++) acc += sysShared.Next(0, Bound);
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("NextBounded")]
    public long NextBounded_SplitMix64()
    {
        long acc = 0;
        for (int i = 0; i < Draws; i++) acc += splitMix.NextInt(Bound);
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("NextBounded")]
    public long NextBounded_Xoshiro256StarStar()
    {
        long acc = 0;
        for (int i = 0; i < Draws; i++) acc += xoshiro.NextInt(Bound);
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("NextBounded")]
    public long NextBounded_Xoroshiro128Plus()
    {
        long acc = 0;
        for (int i = 0; i < Draws; i++) acc += xoroshiro.NextInt(Bound);
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("NextBounded")]
    public long NextBounded_WyRand()
    {
        long acc = 0;
        for (int i = 0; i < Draws; i++) acc += wyRand.NextInt(Bound);
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("NextBounded")]
    public long NextBounded_Pcg32()
    {
        long acc = 0;
        for (int i = 0; i < Draws; i++) acc += pcg.NextInt(Bound);
        return acc;
    }
}
