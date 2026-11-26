using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Statistical tests that <see cref="ThetaSketch{T,THasher}"/> and the
/// <see cref="CompactThetaSketch{T,THasher}"/> set expressions stay within a small multiple of
/// their standard error, that the estimator is unbiased on average, and that the confidence
/// bounds cover the true answer at about their nominal rate. Every data set is drawn from a
/// fixed seed, so the measured error is stable across runs.
/// </summary>
public class ThetaSketchAccuracyTests
{
    // Three disjoint pools of distinct random ints: `shared` in both A and B, `onlyA`, `onlyB`.
    private static (ThetaSketch<int, Int32Murmur3Hasher> A, ThetaSketch<int, Int32Murmur3Hasher> B)
        Pair(int seed, int shared, int onlyA, int onlyB, int precision)
    {
        var rng = new Random(seed);
        var used = new HashSet<int>();
        int Fresh()
        {
            int v;
            do { v = rng.Next(); } while (!used.Add(v));
            return v;
        }

        var a = new ThetaSketch<int, Int32Murmur3Hasher>(precision);
        var b = new ThetaSketch<int, Int32Murmur3Hasher>(precision);
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
        return (a, b);
    }

    [Theory]
    [InlineData(10_000, 10)]
    [InlineData(100_000, 12)]
    [InlineData(1_000_000, 12)]
    [InlineData(250_000, 16)]
    public void Cardinality_IsWithinFourStandardErrors(int n, int precision)
    {
        var sketch = new ThetaSketch<int, Int32Murmur3Hasher>(precision);
        for (int i = 0; i < n; i++)
            sketch.Add(i);

        double relativeError = Math.Abs(sketch.EstimateCardinality() - n) / n;
        Assert.True(relativeError <= 4 * sketch.StandardError,
            $"relative error {relativeError:P2} exceeds 4 × {sketch.StandardError:P2}");
    }

    [Fact]
    public void Cardinality_IsUnbiasedOnAverage()
    {
        // 200 independent sketches of 20,000 elements at k = 256: each estimate has a relative
        // standard error of about 6%, so the mean of 200 has one of about 0.44%.
        const int N = 20_000;
        const int Trials = 200;
        double sum = 0;
        for (int t = 0; t < Trials; t++)
        {
            var sketch = new ThetaSketch<int, Int32Murmur3Hasher>(8);
            int offset = t * 1_000_003;
            for (int i = 0; i < N; i++)
                sketch.Add(offset + i);
            sum += sketch.EstimateCardinality();
        }

        double meanRelativeError = (sum / Trials - N) / N;
        Assert.InRange(meanRelativeError, -0.015, 0.015);
    }

    [Theory]
    [InlineData(1, 50_000, 50_000, 50_000)]   // a third of the union is shared
    [InlineData(2, 10_000, 90_000, 90_000)]   // a twentieth
    [InlineData(3, 90_000, 5_000, 5_000)]     // nearly identical sets
    [InlineData(4, 20_000, 0, 180_000)]       // A is a subset of B
    public void SetExpressions_AreWithinFourStandardErrorsOfTheirShare(int seed, int shared, int onlyA, int onlyB)
    {
        const int Precision = 12;
        (ThetaSketch<int, Int32Murmur3Hasher> a, ThetaSketch<int, Int32Murmur3Hasher> b) = Pair(seed, shared, onlyA, onlyB, Precision);
        CompactThetaSketch<int, Int32Murmur3Hasher> ca = a.Compact(), cb = b.Compact();

        int union = shared + onlyA + onlyB;
        int k = 1 << Precision;

        // An expression covering a share f of the union is estimated from about f·k of the k
        // sampled values, so its relative error is about 1 / sqrt(f · k).
        void Check(CompactThetaSketch<int, Int32Murmur3Hasher> result, int exact)
        {
            double share = (double)exact / union;
            double sd = exact / Math.Sqrt(share * k);
            Assert.True(Math.Abs(result.EstimateCardinality() - exact) <= 4 * sd,
                $"estimate {result.EstimateCardinality():F0} vs exact {exact} (4 sd = {4 * sd:F0})");
            Assert.InRange(exact, result.GetLowerBound(3), result.GetUpperBound(3));
        }

        Check(CompactThetaSketch<int, Int32Murmur3Hasher>.Union(ca, cb), union);
        Check(CompactThetaSketch<int, Int32Murmur3Hasher>.Intersect(ca, cb), shared);
        if (onlyA > 0)
            Check(CompactThetaSketch<int, Int32Murmur3Hasher>.Except(ca, cb), onlyA);
        Check(CompactThetaSketch<int, Int32Murmur3Hasher>.Except(cb, ca), onlyB);
    }

    [Fact]
    public void Intersection_BeatsInclusionExclusionOverHyperLogLog_WhenTheOverlapIsSmall()
    {
        // A 1% overlap between two 100,000-element sets. Inclusion–exclusion subtracts two
        // estimates near 100,000 to get one near 1,000, so its error is relative to the union;
        // the theta intersection's is relative to the overlap. Both sketches here use 32 KB.
        const int Shared = 1_000, Only = 99_000;
        var rng = new Random(7);
        var used = new HashSet<int>();
        int Fresh()
        {
            int v;
            do { v = rng.Next(); } while (!used.Add(v));
            return v;
        }

        double thetaError = 0, hllError = 0;
        const int Trials = 20;
        for (int t = 0; t < Trials; t++)
        {
            var thetaA = new ThetaSketch<int, Int32Murmur3Hasher>(11);
            var thetaB = new ThetaSketch<int, Int32Murmur3Hasher>(11);
            var hllA = new HyperLogLog<int, Int32Murmur3Hasher>(15);
            var hllB = new HyperLogLog<int, Int32Murmur3Hasher>(15);
            void AddA(int v) { thetaA.Add(v); hllA.Add(v); }
            void AddB(int v) { thetaB.Add(v); hllB.Add(v); }

            for (int i = 0; i < Shared; i++)
            {
                int v = Fresh();
                AddA(v);
                AddB(v);
            }
            for (int i = 0; i < Only; i++)
            {
                AddA(Fresh());
                AddB(Fresh());
            }

            double theta = CompactThetaSketch<int, Int32Murmur3Hasher>.Intersect(thetaA.Compact(), thetaB.Compact()).EstimateCardinality();
            long a = hllA.EstimateCardinality(), b = hllB.EstimateCardinality();
            hllA.UnionWith(hllB);
            double hll = a + b - hllA.EstimateCardinality();

            thetaError += (theta - Shared) * (theta - Shared);
            hllError += (hll - Shared) * (hll - Shared);
        }

        Assert.True(thetaError < hllError,
            $"theta RMSE {Math.Sqrt(thetaError / Trials):F0} vs inclusion–exclusion RMSE {Math.Sqrt(hllError / Trials):F0}");
    }

    [Fact]
    public void Bounds_CoverTheTruth_AtRoughlyTheirNominalRate()
    {
        // At two standard deviations each side misses about 2.3% of the time, so out of 200
        // trials the interval should miss no more than about 4.6% plus sampling slack.
        const int N = 30_000;
        const int Trials = 200;
        int misses = 0;
        for (int t = 0; t < Trials; t++)
        {
            var sketch = new ThetaSketch<int, Int32Murmur3Hasher>(7);
            int offset = t * 999_983;
            for (int i = 0; i < N; i++)
                sketch.Add(offset + i);

            if (N < sketch.GetLowerBound(2) || N > sketch.GetUpperBound(2))
                misses++;
        }

        Assert.InRange(misses, 0, 20);
    }

    [Fact]
    public void Bounds_KeepTheirCoverage_ForASparseIntersection()
    {
        // Two 100,000-element sets at k = 16 (θ ≈ 2 × 10^-4) sharing 500 elements: the
        // intersection's expected retained count is about 0.1. A normal-approximation interval
        // excludes the truth whenever a single shared value is sampled (about 9.5% of the time);
        // exact Poisson tails miss only when two or more are, about 0.5%.
        const int Shared = 500, Only = 99_500, Trials = 300;
        int misses = 0;
        for (int t = 0; t < Trials; t++)
        {
            var a = new ThetaSketch<int, Int32Murmur3Hasher>(4);
            var b = new ThetaSketch<int, Int32Murmur3Hasher>(4);
            int offset = t * 1_000_003;
            for (int i = 0; i < Shared; i++)
            {
                a.Add(offset + i);
                b.Add(offset + i);
            }
            for (int i = 0; i < Only; i++)
            {
                a.Add(offset + Shared + i);
                b.Add(offset + Shared + Only + i);
            }

            CompactThetaSketch<int, Int32Murmur3Hasher> both = CompactThetaSketch<int, Int32Murmur3Hasher>.Intersect(a.Compact(), b.Compact());
            if (Shared < both.GetLowerBound(2) || Shared > both.GetUpperBound(2))
                misses++;
        }

        // The nominal two-sided miss rate at two standard deviations is 4.6%, 14 of 300.
        Assert.InRange(misses, 0, 14);
    }

    [Fact]
    public void MergedShards_EstimateTheUnion_AsWellAsOneSketch()
    {
        // Sixteen shards of one stream, each sketched separately and merged.
        const int N = 400_000;
        var shards = new ThetaSketch<int, Int32Murmur3Hasher>[16];
        for (int s = 0; s < shards.Length; s++)
            shards[s] = new ThetaSketch<int, Int32Murmur3Hasher>(12);
        for (int i = 0; i < N; i++)
            shards[i & 15].Add(i);

        var merged = new ThetaSketch<int, Int32Murmur3Hasher>(12);
        foreach (ThetaSketch<int, Int32Murmur3Hasher> shard in shards)
            merged.UnionWith(shard);

        double relativeError = Math.Abs(merged.EstimateCardinality() - N) / N;
        Assert.True(relativeError <= 4 * merged.StandardError);
        Assert.InRange(N, merged.GetLowerBound(3), merged.GetUpperBound(3));
    }
}
