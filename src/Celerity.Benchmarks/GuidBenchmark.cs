using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Primitives;

/// <summary>
/// Head-to-head throughput of <see cref="FastGuid"/> — the non-cryptographic version&#160;4 and the RFC&#160;9562
/// big-endian version&#160;7 (stateless and the strictly monotonic <see cref="GuidV7Generator{TRng}"/>) — against
/// the BCL GUID factories <see cref="System.Guid.NewGuid()"/> and (on .NET&#160;9+) <c>Guid.CreateVersion7()</c>
/// (issue #195).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="System.Guid.NewGuid()"/> is RNG-backed (cryptographically random) and is the baseline a developer
/// replaces when they want fast trace / correlation / ephemeral IDs where unpredictability is <em>not</em>
/// required. <see cref="FastGuid"/> fills the 122/74 random bits from a struct PRNG instead, so the
/// <c>NextUInt64</c> call inlines through the <c>where TRng : struct, IRandomSource</c> constraint and the whole
/// loop allocates nothing — the target is several times the throughput of <c>NewGuid</c>.
/// </para>
/// <para>
/// Each category fixes one GUID version; the BCL form is the baseline and each <c>FastGuid</c> form a candidate,
/// so the ratio reads as "the PRNG-filled GUID relative to the BCL factory". The benchmark currently targets
/// net8.0, where <c>Guid.CreateVersion7()</c> does not exist; the <c>#if NET9_0_OR_GREATER</c> arm activates the
/// big-endian-vs-mixed-endian v7 comparison once the project multi-targets (roadmap #189). This is an isolated
/// microbenchmark, so it lives in the <strong>extended</strong> suite (not the per-PR core regression gate).
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class GuidBenchmark
{
    private const int Draws = 4096;
    private const ulong Seed = 0x9E3779B97F4A7C15UL;

    private Xoshiro256StarStar xoshiro;
    private WyRand wyRand;
    private GuidV7Generator<Xoshiro256StarStar> monotonic;
    private long unixMs;

    [GlobalSetup]
    public void Setup()
    {
        xoshiro = new Xoshiro256StarStar(Seed);
        wyRand = new WyRand(Seed);
        monotonic = new GuidV7Generator<Xoshiro256StarStar>(new Xoshiro256StarStar(Seed));
        unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    // ---- Version 4 (random) ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("V4")]
    public int V4_BclNewGuid()
    {
        int acc = 0;
        for (int i = 0; i < Draws; i++) acc ^= Guid.NewGuid().GetHashCode();
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("V4")]
    public int V4_FastGuid_Xoshiro()
    {
        int acc = 0;
        for (int i = 0; i < Draws; i++) acc ^= FastGuid.CreateVersion4(ref xoshiro).GetHashCode();
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("V4")]
    public int V4_FastGuid_WyRand()
    {
        int acc = 0;
        for (int i = 0; i < Draws; i++) acc ^= FastGuid.CreateVersion4(ref wyRand).GetHashCode();
        return acc;
    }

    // ---- Version 7 (time-ordered) ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("V7")]
    public int V7_BclNewGuid()
    {
        int acc = 0;
        for (int i = 0; i < Draws; i++) acc ^= Guid.NewGuid().GetHashCode();
        return acc;
    }

#if NET9_0_OR_GREATER
    [Benchmark]
    [BenchmarkCategory("V7")]
    public int V7_BclCreateVersion7()
    {
        int acc = 0;
        for (int i = 0; i < Draws; i++) acc ^= Guid.CreateVersion7().GetHashCode();
        return acc;
    }
#endif

    [Benchmark]
    [BenchmarkCategory("V7")]
    public int V7_FastGuid_Stateless()
    {
        int acc = 0;
        for (int i = 0; i < Draws; i++) acc ^= FastGuid.CreateVersion7(ref xoshiro, unixMs).GetHashCode();
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("V7")]
    public int V7_FastGuid_Monotonic()
    {
        int acc = 0;
        for (int i = 0; i < Draws; i++) acc ^= monotonic.Next(unixMs).GetHashCode();
        return acc;
    }
}
