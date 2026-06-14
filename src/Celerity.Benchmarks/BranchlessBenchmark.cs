using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Primitives;

/// <summary>
/// Head-to-head throughput of <see cref="Branchless"/>'s guaranteed branch-free conditional select against the
/// plain <c>condition ? whenTrue : whenFalse</c> ternary it replaces (issue #198), over a bulk per-element blend
/// where the win is visible.
/// </summary>
/// <remarks>
/// <para>
/// The headline arm is <c>Unpredictable</c>: a 50/50 condition the branch predictor cannot learn. There the
/// branchy ternary pays a misprediction on roughly half of every iteration, while the branch-free arithmetic has
/// a fixed data dependency and auto-vectorises — this is the case the primitive exists for, and the #198 spike
/// measured it at ~6× faster branch-free.
/// </para>
/// <para>
/// The <c>Predictable</c> arm (a loop-invariant-shaped, always-true condition) is the control: when the branch
/// predictor is right every time, the predicted branch is essentially free, so the branchless arm wins little or
/// nothing — the documented "don't bother unless the condition is unpredictable" boundary. The <c>Ternary_*</c>
/// arms are the baselines; the <c>Branchless_*</c> arms are the candidates. This is an isolated microbenchmark,
/// so it lives in the <strong>extended</strong> suite (not the per-PR core regression gate).
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class BranchlessBenchmark
{
    [Params(1_000_000)]
    public int Length;

    private int[] whenTrue = null!;
    private int[] whenFalse = null!;
    private bool[] unpredictable = null!;
    private bool[] predictable = null!;
    private int[] destination = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(0x198);
        whenTrue = new int[Length];
        whenFalse = new int[Length];
        unpredictable = new bool[Length];
        predictable = new bool[Length];
        destination = new int[Length];
        for (int i = 0; i < Length; i++)
        {
            whenTrue[i] = rng.Next();
            whenFalse[i] = rng.Next();
            unpredictable[i] = rng.Next(2) == 0; // 50/50, no learnable pattern
            predictable[i] = true;               // always taken — the predictor is right every time
        }
    }

    // ---- Unpredictable condition: the misprediction case the primitive targets ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Unpredictable")]
    public void Ternary_Unpredictable()
    {
        bool[] c = unpredictable;
        int[] t = whenTrue, f = whenFalse, d = destination;
        for (int i = 0; i < c.Length; i++)
            d[i] = c[i] ? t[i] : f[i];
    }

    [Benchmark]
    [BenchmarkCategory("Unpredictable")]
    public void Branchless_Unpredictable()
        => Branchless.Select(unpredictable, whenTrue, whenFalse, destination);

    // ---- Predictable condition: the control where branchless does not help ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Predictable")]
    public void Ternary_Predictable()
    {
        bool[] c = predictable;
        int[] t = whenTrue, f = whenFalse, d = destination;
        for (int i = 0; i < c.Length; i++)
            d[i] = c[i] ? t[i] : f[i];
    }

    [Benchmark]
    [BenchmarkCategory("Predictable")]
    public void Branchless_Predictable()
        => Branchless.Select(predictable, whenTrue, whenFalse, destination);
}
