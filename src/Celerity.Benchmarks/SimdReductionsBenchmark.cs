using System.Numerics.Tensors;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Primitives;

/// <summary>
/// Head-to-head throughput of <see cref="SimdReductions"/>'s fused/specialized reductions against the BCL
/// composition they replace (issue #197):
/// <list type="bullet">
/// <item><description>
/// <c>MinMax</c> — the single-pass <see cref="SimdReductions.MinMax(System.ReadOnlySpan{int})"/> vs the
/// two-pass <c>TensorPrimitives.Min</c> + <c>TensorPrimitives.Max</c> (the BCL composition) and a naive scalar
/// loop. The fused candidate touches the span once instead of twice, so the win grows as the data spills out
/// of cache.
/// </description></item>
/// <item><description>
/// <c>CheckedSum</c> — the overflow-checked <see cref="SimdReductions.CheckedSum(System.ReadOnlySpan{int})"/> vs
/// a scalar <c>checked</c> loop (the only safe BCL way — <c>TensorPrimitives.Sum</c> wraps silently), with the
/// silently-wrapping <c>TensorPrimitives.Sum</c> shown for reference as the unchecked speed ceiling.
/// </description></item>
/// </list>
/// </summary>
/// <remarks>
/// The <c>TensorPrimitives.*</c> arms are the BCL baselines; the <see cref="SimdReductions"/> arms are the
/// candidates, so each ratio reads as "the fused/checked helper relative to the BCL composition". This is an
/// isolated microbenchmark, so it lives in the <strong>extended</strong> suite (not the per-PR core regression
/// gate). The span lengths span an in-cache size and an out-of-cache size so the single-pass memory-traffic win
/// is visible.
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SimdReductionsBenchmark
{
    [Params(1024, 1_000_000)]
    public int Length;

    private int[] data = null!;
    private int[] smallMagnitude = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(0x5197);
        data = new int[Length];
        smallMagnitude = new int[Length];
        for (int i = 0; i < Length; i++)
        {
            data[i] = rng.Next(int.MinValue, int.MaxValue);
            // Bounded so the checked sum never overflows on the hot benchmark path.
            smallMagnitude[i] = rng.Next(-1000, 1000);
        }
    }

    // ---- MinMax: fused single pass vs two-pass BCL composition ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("MinMax")]
    public (int, int) MinMax_TensorPrimitives_TwoPass()
        => (TensorPrimitives.Min<int>(data), TensorPrimitives.Max<int>(data));

    [Benchmark]
    [BenchmarkCategory("MinMax")]
    public (int, int) MinMax_NaiveScalarLoop()
    {
        int[] a = data;
        int min = a[0], max = a[0];
        for (int i = 1; i < a.Length; i++)
        {
            if (a[i] < min) min = a[i];
            if (a[i] > max) max = a[i];
        }
        return (min, max);
    }

    [Benchmark]
    [BenchmarkCategory("MinMax")]
    public (int Min, int Max) MinMax_SimdReductions()
        => SimdReductions.MinMax(data);

    // ---- CheckedSum: vectorized overflow-checked sum vs scalar checked loop ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CheckedSum")]
    public int CheckedSum_ScalarCheckedLoop()
    {
        int[] a = smallMagnitude;
        int sum = 0;
        for (int i = 0; i < a.Length; i++)
            sum = checked(sum + a[i]);
        return sum;
    }

    [Benchmark]
    [BenchmarkCategory("CheckedSum")]
    public int CheckedSum_TensorPrimitivesUnchecked()
        => TensorPrimitives.Sum<int>(smallMagnitude); // wraps silently — the unchecked speed ceiling, for reference

    [Benchmark]
    [BenchmarkCategory("CheckedSum")]
    public int CheckedSum_SimdReductions()
        => SimdReductions.CheckedSum(smallMagnitude);
}
