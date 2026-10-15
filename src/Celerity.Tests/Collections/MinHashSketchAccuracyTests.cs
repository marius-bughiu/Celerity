using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Statistical tests that <see cref="MinHashSketch{T,THasher}.EstimateJaccard"/> stays within
/// a small multiple of its theoretical standard error, <c>sqrt(J · (1 − J) / m)</c>, across a
/// range of similarities, set sizes and key shapes, and that it is unbiased on average. Every
/// data set is drawn from a fixed seed, so the measured error is stable across runs.
/// </summary>
public class MinHashSketchAccuracyTests
{
    // Builds two random sets with an exact, known Jaccard similarity: `shared` elements in
    // both, `onlyA` only in A and `onlyB` only in B, all distinct.
    private static (MinHashSketch<int, Int32Murmur3Hasher> A, MinHashSketch<int, Int32Murmur3Hasher> B, double Exact)
        Pair(int seed, int shared, int onlyA, int onlyB, int precision = MinHashSketch<int, Int32Murmur3Hasher>.DefaultPrecision)
    {
        var rng = new Random(seed);
        var used = new HashSet<int>();
        int Fresh()
        {
            int v;
            do { v = rng.Next(); } while (!used.Add(v));
            return v;
        }

        var a = new MinHashSketch<int, Int32Murmur3Hasher>(precision);
        var b = new MinHashSketch<int, Int32Murmur3Hasher>(precision);
        for (int i = 0; i < shared; i++)
        {
            int v = Fresh();
            a.Add(v);
            b.Add(v);
        }
        for (int i = 0; i < onlyA; i++)
            a.Add(Fresh());
        for (int i = 0; i < onlyB; i++)
            b.Add(Fresh());

        return (a, b, (double)shared / (shared + onlyA + onlyB));
    }

    private static double Sigma(double j, int m) => Math.Sqrt(j * (1 - j) / m);

    [Theory]
    [InlineData(1_000, 9_000, 0)]        // J = 0.1, A ⊃ B
    [InlineData(5_000, 2_500, 2_500)]    // J = 0.5
    [InlineData(9_000, 500, 500)]        // J = 0.9
    [InlineData(100, 4_950, 4_950)]      // J = 0.01
    public void LargeSets_EstimateWithinFourSigma(int shared, int onlyA, int onlyB)
    {
        for (int seed = 0; seed < 8; seed++)
        {
            var (a, b, exact) = Pair(seed, shared, onlyA, onlyB);
            double estimate = a.EstimateJaccard(b);
            double bound = 4 * Sigma(exact, a.RegisterCount) + 0.005;

            Assert.True(Math.Abs(estimate - exact) <= bound,
                $"seed {seed}: J={exact:F4}, estimate {estimate:F4}, bound {bound:F4}");
        }
    }

    [Fact]
    public void Estimate_IsUnbiased_AcrossTrials()
    {
        // The mean over 200 independent trials at J = 0.3 must sit within a few standard errors
        // of the *mean*, sigma / sqrt(trials) — a much tighter check than any single trial.
        const int trials = 200;
        double sum = 0;
        double exact = 0;
        for (int seed = 0; seed < trials; seed++)
        {
            var (a, b, j) = Pair(1_000 + seed, 1_500, 1_750, 1_750);
            sum += a.EstimateJaccard(b);
            exact = j;
        }

        double mean = sum / trials;
        double sigmaOfMean = Sigma(exact, 256) / Math.Sqrt(trials);
        Assert.True(Math.Abs(mean - exact) <= 4 * sigmaOfMean,
            $"mean estimate {mean:F5} vs exact {exact:F5}; 4σ of the mean is {4 * sigmaOfMean:F5}");
    }

    [Fact]
    public void SmallSets_WithEmptyRegisters_StayUnbiased()
    {
        // 60 distinct elements across 256 registers leaves most registers empty, so the
        // estimator works from the filled ones only. It must still be unbiased on average.
        const int trials = 300;
        double sum = 0;
        double exact = 0;
        for (int seed = 0; seed < trials; seed++)
        {
            var (a, b, j) = Pair(5_000 + seed, 20, 20, 20);
            sum += a.EstimateJaccard(b);
            exact = j;
        }

        double mean = sum / trials;
        // About 60 registers carry information, so a single trial's sigma is roughly
        // sqrt(J(1-J)/60); allow 4 of those over the mean of 300 trials.
        double sigmaOfMean = Sigma(exact, 60) / Math.Sqrt(trials);
        Assert.True(Math.Abs(mean - exact) <= 4 * sigmaOfMean,
            $"mean estimate {mean:F5} vs exact {exact:F5}; 4σ of the mean is {4 * sigmaOfMean:F5}");
    }

    [Theory]
    [InlineData(6)]
    [InlineData(10)]
    [InlineData(14)]
    public void ErrorShrinksWithPrecision(int precision)
    {
        int m = 1 << precision;
        // Sets several times larger than m so every register fills.
        int scale = Math.Max(m * 4, 4_000);
        var (a, b, exact) = Pair(precision, scale, scale, scale, precision);
        double bound = 4 * Sigma(exact, m) + 0.002;

        Assert.True(Math.Abs(a.EstimateJaccard(b) - exact) <= bound,
            $"precision {precision}: J={exact:F4}, estimate {a.EstimateJaccard(b):F4}, bound {bound:F4}");
    }

    [Fact]
    public void StringSets_EstimateWithinFourSigma()
    {
        var a = new MinHashSketch<string, StringMurmur3Hasher>(10);
        var b = new MinHashSketch<string, StringMurmur3Hasher>(10);
        for (int i = 0; i < 30_000; i++)
            a.Add($"shingle-{i}");
        for (int i = 10_000; i < 40_000; i++)
            b.Add($"shingle-{i}");

        // 20,000 shared of 40,000 distinct: J = 0.5.
        double bound = 4 * Sigma(0.5, a.RegisterCount);
        Assert.InRange(a.EstimateJaccard(b), 0.5 - bound, 0.5 + bound);
    }

    [Fact]
    public void LongSets_With64BitHasher_EstimateWithinFourSigma()
    {
        var a = new MinHashSketch<long, Int64WangHasher>(10);
        var b = new MinHashSketch<long, Int64WangHasher>(10);
        for (long i = 0; i < 50_000; i++)
            a.Add(i * 7_919L);
        for (long i = 25_000; i < 75_000; i++)
            b.Add(i * 7_919L);

        // 25,000 shared of 75,000 distinct: J = 1/3.
        double exact = 1d / 3d;
        double bound = 4 * Sigma(exact, a.RegisterCount);
        Assert.InRange(a.EstimateJaccard(b), exact - bound, exact + bound);
    }

    [Fact]
    public void MergedSketches_EstimateLikeASketchOfTheUnion()
    {
        // A = A1 ∪ A2 built by merge; its similarity to B must be what a direct sketch of A
        // gives, exactly, because the merged registers are identical.
        var (a1, b, _) = Pair(77, 4_000, 4_000, 4_000);
        var direct = new MinHashSketch<int, Int32Murmur3Hasher>();
        var a2 = new MinHashSketch<int, Int32Murmur3Hasher>();
        var rng = new Random(78);
        var extra = new int[3_000];
        for (int i = 0; i < extra.Length; i++)
            extra[i] = rng.Next();

        direct.UnionWith(a1);
        foreach (int v in extra)
        {
            a2.Add(v);
            direct.Add(v);
        }

        a1.UnionWith(a2);
        Assert.Equal(direct.EstimateJaccard(b), a1.EstimateJaccard(b));
        Assert.Equal(1d, a1.EstimateJaccard(direct));
    }
}
