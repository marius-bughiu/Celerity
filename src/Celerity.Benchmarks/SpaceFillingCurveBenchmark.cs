using System.Runtime.Intrinsics.X86;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Celerity.Primitives;

/// <summary>
/// Codec throughput for <see cref="MortonCurve"/> and <see cref="HilbertCurve"/> (issue #369), against
/// the two things a caller would otherwise have: the bit-by-bit loop they would write themselves, and the
/// x86 <c>BMI2</c> <c>PDEP</c> / <c>PEXT</c> pair the shipped code deliberately does not use.
/// </summary>
/// <remarks>
/// <para>
/// The <c>Bmi2</c> arms exist to answer the issue's first kill criterion — <em>the intrinsic path must
/// beat the portable fallback measurably, or it ships portable-only and the intrinsic is dropped rather
/// than carried for decoration</em>. They are carried here, in the benchmark, rather than inside the
/// shipped type, so the decision stays measurable without the library having to branch on hardware it
/// cannot fully characterize: <c>Bmi2.IsSupported</c> is true on AMD Zen 1 and Zen 2, where
/// <c>PDEP</c> / <c>PEXT</c> are microcoded and an order of magnitude <em>slower</em> than the portable
/// sequence, and .NET exposes nothing that distinguishes those parts from the ones where the intrinsic
/// wins. See the API reference for the full reasoning.
/// </para>
/// <para>
/// The naive arm is the honest baseline in the sense the repo asks for: it is what the caller writes when
/// the library ships nothing, and it is a straightforward loop rather than a strawman. Each category
/// fixes one direction of one curve, so the ratios read as "relative to writing it by hand".
/// </para>
/// <para>
/// Isolated microbenchmarks, so this lives in the <strong>extended</strong> suite rather than the per-PR
/// core regression gate.
/// </para>
/// </remarks>
[MemoryDiagnoser(false)]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SpaceFillingCurveBenchmark
{
    private const int PointCount = 4096;
    private const ulong EveryOtherBit = 0x5555_5555_5555_5555UL;
    private const ulong EveryThirdBit = 0x1249_2492_4924_9249UL;

    private uint[] xs = null!;
    private uint[] ys = null!;
    private uint[] zs = null!;
    private ulong[] codes2D = null!;
    private ulong[] codes3D = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(20260815);
        xs = new uint[PointCount];
        ys = new uint[PointCount];
        zs = new uint[PointCount];
        codes2D = new ulong[PointCount];
        codes3D = new ulong[PointCount];

        for (int i = 0; i < PointCount; i++)
        {
            xs[i] = (uint)rng.Next();
            ys[i] = (uint)rng.Next();
            zs[i] = (uint)rng.Next(0, (int)MortonCurve.MaxCoordinate3D + 1);
            codes2D[i] = MortonCurve.Encode2D(xs[i], ys[i]);
            codes3D[i] = MortonCurve.Encode3D(
                xs[i] & MortonCurve.MaxCoordinate3D,
                ys[i] & MortonCurve.MaxCoordinate3D,
                zs[i]);
        }
    }

    // ---- 2-D encode ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Encode2D")]
    public ulong Encode2D_HandRolledLoop()
    {
        ulong acc = 0;
        uint[] x = xs, y = ys;
        for (int i = 0; i < x.Length; i++)
            acc ^= NaiveInterleave2D(x[i], y[i]);
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Encode2D")]
    public ulong Encode2D_MortonCurve()
    {
        ulong acc = 0;
        uint[] x = xs, y = ys;
        for (int i = 0; i < x.Length; i++)
            acc ^= MortonCurve.Encode2D(x[i], y[i]);
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Encode2D")]
    public ulong Encode2D_Bmi2()
    {
        if (!Bmi2.X64.IsSupported)
            return Encode2D_MortonCurve();

        ulong acc = 0;
        uint[] x = xs, y = ys;
        for (int i = 0; i < x.Length; i++)
            acc ^= Bmi2.X64.ParallelBitDeposit(x[i], EveryOtherBit)
                 | (Bmi2.X64.ParallelBitDeposit(y[i], EveryOtherBit) << 1);
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Encode2D")]
    public ulong Encode2D_HilbertCurve()
    {
        ulong acc = 0;
        uint[] x = xs, y = ys;
        for (int i = 0; i < x.Length; i++)
            acc ^= HilbertCurve.Encode2D(x[i], y[i]);
        return acc;
    }

    // ---- 2-D decode ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Decode2D")]
    public uint Decode2D_HandRolledLoop()
    {
        uint acc = 0;
        ulong[] c = codes2D;
        for (int i = 0; i < c.Length; i++)
        {
            var (x, y) = NaiveDeinterleave2D(c[i]);
            acc ^= x ^ y;
        }

        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Decode2D")]
    public uint Decode2D_MortonCurve()
    {
        uint acc = 0;
        ulong[] c = codes2D;
        for (int i = 0; i < c.Length; i++)
        {
            var (x, y) = MortonCurve.Decode2D(c[i]);
            acc ^= x ^ y;
        }

        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Decode2D")]
    public uint Decode2D_Bmi2()
    {
        if (!Bmi2.X64.IsSupported)
            return Decode2D_MortonCurve();

        uint acc = 0;
        ulong[] c = codes2D;
        for (int i = 0; i < c.Length; i++)
        {
            acc ^= (uint)Bmi2.X64.ParallelBitExtract(c[i], EveryOtherBit)
                 ^ (uint)Bmi2.X64.ParallelBitExtract(c[i] >> 1, EveryOtherBit);
        }

        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Decode2D")]
    public uint Decode2D_HilbertCurve()
    {
        uint acc = 0;
        ulong[] c = codes2D;
        for (int i = 0; i < c.Length; i++)
        {
            var (x, y) = HilbertCurve.Decode2D(c[i]);
            acc ^= x ^ y;
        }

        return acc;
    }

    // ---- 3-D encode ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Encode3D")]
    public ulong Encode3D_HandRolledLoop()
    {
        ulong acc = 0;
        uint[] x = xs, y = ys, z = zs;
        for (int i = 0; i < x.Length; i++)
            acc ^= NaiveInterleave3D(x[i] & MortonCurve.MaxCoordinate3D, y[i] & MortonCurve.MaxCoordinate3D, z[i]);
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Encode3D")]
    public ulong Encode3D_MortonCurve()
    {
        ulong acc = 0;
        uint[] x = xs, y = ys, z = zs;
        for (int i = 0; i < x.Length; i++)
            acc ^= MortonCurve.Encode3D(x[i] & MortonCurve.MaxCoordinate3D, y[i] & MortonCurve.MaxCoordinate3D, z[i]);
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Encode3D")]
    public ulong Encode3D_Bmi2()
    {
        if (!Bmi2.X64.IsSupported)
            return Encode3D_MortonCurve();

        ulong acc = 0;
        uint[] x = xs, y = ys, z = zs;
        for (int i = 0; i < x.Length; i++)
            acc ^= Bmi2.X64.ParallelBitDeposit(x[i] & MortonCurve.MaxCoordinate3D, EveryThirdBit)
                 | (Bmi2.X64.ParallelBitDeposit(y[i] & MortonCurve.MaxCoordinate3D, EveryThirdBit) << 1)
                 | (Bmi2.X64.ParallelBitDeposit(z[i], EveryThirdBit) << 2);
        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Encode3D")]
    public ulong Encode3D_HilbertCurve()
    {
        ulong acc = 0;
        uint[] x = xs, y = ys, z = zs;
        for (int i = 0; i < x.Length; i++)
            acc ^= HilbertCurve.Encode3D(x[i] & MortonCurve.MaxCoordinate3D, y[i] & MortonCurve.MaxCoordinate3D, z[i]);
        return acc;
    }

    // ---- 3-D decode ----

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Decode3D")]
    public uint Decode3D_HandRolledLoop()
    {
        uint acc = 0;
        ulong[] c = codes3D;
        for (int i = 0; i < c.Length; i++)
        {
            var (x, y, z) = NaiveDeinterleave3D(c[i]);
            acc ^= x ^ y ^ z;
        }

        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Decode3D")]
    public uint Decode3D_MortonCurve()
    {
        uint acc = 0;
        ulong[] c = codes3D;
        for (int i = 0; i < c.Length; i++)
        {
            var (x, y, z) = MortonCurve.Decode3D(c[i]);
            acc ^= x ^ y ^ z;
        }

        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Decode3D")]
    public uint Decode3D_Bmi2()
    {
        if (!Bmi2.X64.IsSupported)
            return Decode3D_MortonCurve();

        uint acc = 0;
        ulong[] c = codes3D;
        for (int i = 0; i < c.Length; i++)
        {
            acc ^= (uint)Bmi2.X64.ParallelBitExtract(c[i], EveryThirdBit)
                 ^ (uint)Bmi2.X64.ParallelBitExtract(c[i] >> 1, EveryThirdBit)
                 ^ (uint)Bmi2.X64.ParallelBitExtract(c[i] >> 2, EveryThirdBit);
        }

        return acc;
    }

    [Benchmark]
    [BenchmarkCategory("Decode3D")]
    public uint Decode3D_HilbertCurve()
    {
        uint acc = 0;
        ulong[] c = codes3D;
        for (int i = 0; i < c.Length; i++)
        {
            var (x, y, z) = HilbertCurve.Decode3D(c[i]);
            acc ^= x ^ y ^ z;
        }

        return acc;
    }

    // ---- The hand-rolled baselines ----

    private static ulong NaiveInterleave2D(uint x, uint y)
    {
        ulong code = 0;
        for (int bit = 0; bit < 32; bit++)
        {
            code |= (ulong)((x >> bit) & 1) << (2 * bit);
            code |= (ulong)((y >> bit) & 1) << ((2 * bit) + 1);
        }

        return code;
    }

    private static (uint X, uint Y) NaiveDeinterleave2D(ulong code)
    {
        uint x = 0, y = 0;
        for (int bit = 0; bit < 32; bit++)
        {
            x |= (uint)((code >> (2 * bit)) & 1) << bit;
            y |= (uint)((code >> ((2 * bit) + 1)) & 1) << bit;
        }

        return (x, y);
    }

    private static ulong NaiveInterleave3D(uint x, uint y, uint z)
    {
        ulong code = 0;
        for (int bit = 0; bit < 21; bit++)
        {
            code |= (ulong)((x >> bit) & 1) << (3 * bit);
            code |= (ulong)((y >> bit) & 1) << ((3 * bit) + 1);
            code |= (ulong)((z >> bit) & 1) << ((3 * bit) + 2);
        }

        return code;
    }

    private static (uint X, uint Y, uint Z) NaiveDeinterleave3D(ulong code)
    {
        uint x = 0, y = 0, z = 0;
        for (int bit = 0; bit < 21; bit++)
        {
            x |= (uint)((code >> (3 * bit)) & 1) << bit;
            y |= (uint)((code >> ((3 * bit) + 1)) & 1) << bit;
            z |= (uint)((code >> ((3 * bit) + 2)) & 1) << bit;
        }

        return (x, y, z);
    }
}
