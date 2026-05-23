using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class HashQualityEvaluatorTests
{
    // A hasher that maps every key to the same code — forces every key into one bucket.
    private struct ConstantIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => 7;
    }

    // A hasher that returns the key unchanged — a perfect hash on contiguous ranges.
    private struct IdentityIntHasher : IHashProvider<int>
    {
        public int Hash(int key) => key;
    }

    private static int[] Range(int count)
    {
        var result = new int[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = i;
        }

        return result;
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_ThrowsArgumentNullException_WhenKeysNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => HashQualityEvaluator.Evaluate<int, IdentityIntHasher>(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1024)]
    public void Evaluate_ThrowsArgumentOutOfRange_WhenBucketCountLessThanOne(int bucketCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => HashQualityEvaluator.Evaluate<int, IdentityIntHasher>(Range(4), bucketCount));
    }

    // ── Bucket-count normalization ──────────────────────────────────────────────

    [Theory]
    [InlineData(1, 1)]
    [InlineData(16, 16)]
    [InlineData(17, 32)]
    [InlineData(1000, 1024)]
    public void Evaluate_RoundsBucketCountUpToPowerOfTwo(int requested, int expected)
    {
        HashQualityReport report =
            HashQualityEvaluator.Evaluate<int, IdentityIntHasher>(Range(4), requested);

        Assert.Equal(expected, report.BucketCount);
    }

    [Fact]
    public void Evaluate_UsesDefaultBucketCount_WhenUnspecified()
    {
        HashQualityReport report =
            HashQualityEvaluator.Evaluate<int, IdentityIntHasher>(Range(4));

        Assert.Equal(1024, report.BucketCount);
    }

    // ── Empty / single sample ───────────────────────────────────────────────────

    [Fact]
    public void Evaluate_EmptySample_ReturnsNeutralReport()
    {
        HashQualityReport report =
            HashQualityEvaluator.Evaluate<int, IdentityIntHasher>(Array.Empty<int>(), 64);

        Assert.Equal(0, report.KeyCount);
        Assert.Equal(0, report.DistinctHashCount);
        Assert.Equal(0, report.CollisionCount);
        Assert.Equal(0.0, report.CollisionRate);
        Assert.Equal(64, report.BucketCount);
        Assert.Equal(0, report.OccupiedBucketCount);
        Assert.Equal(64, report.EmptyBucketCount);
        Assert.Equal(0, report.MaxBucketLoad);
        Assert.Equal(0.0, report.ChiSquared);
        Assert.Equal(1.0, report.DistributionScore); // neutral default for an empty sample
    }

    [Fact]
    public void Evaluate_SingleKey_ReportsIdealSingleton()
    {
        HashQualityReport report =
            HashQualityEvaluator.Evaluate<int, IdentityIntHasher>(new[] { 99 }, 64);

        Assert.Equal(1, report.KeyCount);
        Assert.Equal(1, report.DistinctHashCount);
        Assert.Equal(0, report.CollisionCount);
        Assert.Equal(0.0, report.CollisionRate);
        Assert.Equal(1, report.OccupiedBucketCount);
        Assert.Equal(1, report.MaxBucketLoad);
        Assert.Equal(1.0, report.DistributionScore); // sumSquares == expected == 1
    }

    // ── Raw-hash collision accounting ───────────────────────────────────────────

    [Fact]
    public void Evaluate_ConstantHasher_CountsEveryKeyAfterFirstAsCollision()
    {
        int[] keys = Range(100);

        HashQualityReport report =
            HashQualityEvaluator.Evaluate<int, ConstantIntHasher>(keys, 1024);

        Assert.Equal(100, report.KeyCount);
        Assert.Equal(1, report.DistinctHashCount);
        Assert.Equal(99, report.CollisionCount);
        Assert.Equal(99.0 / 100.0, report.CollisionRate);
        Assert.Equal(1, report.OccupiedBucketCount);
        Assert.Equal(100, report.MaxBucketLoad);
    }

    [Fact]
    public void Evaluate_DuplicateKeysInSample_CountAsCollisions()
    {
        HashQualityReport report =
            HashQualityEvaluator.Evaluate<int, IdentityIntHasher>(new[] { 5, 5, 5 }, 64);

        Assert.Equal(3, report.KeyCount);
        Assert.Equal(1, report.DistinctHashCount);
        Assert.Equal(2, report.CollisionCount);
    }

    // ── Bucket distribution ─────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_IdentityHasher_OnContiguousKeys_IsNearPerfect()
    {
        int[] keys = Range(256);

        HashQualityReport report =
            HashQualityEvaluator.Evaluate<int, IdentityIntHasher>(keys, 256);

        // Identity over 0..255 into 256 buckets puts exactly one key per bucket.
        Assert.Equal(0, report.CollisionCount);
        Assert.Equal(256, report.OccupiedBucketCount);
        Assert.Equal(0, report.EmptyBucketCount);
        Assert.Equal(1, report.MaxBucketLoad);
        Assert.Equal(1.0, report.MeanBucketLoad);
        Assert.Equal(0.0, report.ChiSquared, 6); // every bucket exactly at the mean
        Assert.True(report.DistributionScore < 1.0,
            $"A perfect hash should score below the random-uniform baseline of 1.0 (was {report.DistributionScore}).");
    }

    [Fact]
    public void Evaluate_MasksCodesIntoBuckets_LikeTheCollections()
    {
        // Identity codes 0 and 4 collide under a 4-bucket mask (0 & 3 == 4 & 3 == 0).
        HashQualityReport report =
            HashQualityEvaluator.Evaluate<int, IdentityIntHasher>(new[] { 0, 4 }, 4);

        Assert.Equal(0, report.CollisionCount);    // distinct raw codes
        Assert.Equal(1, report.OccupiedBucketCount); // but the same bucket
        Assert.Equal(3, report.EmptyBucketCount);
        Assert.Equal(2, report.MaxBucketLoad);
    }

    [Fact]
    public void Evaluate_OccupiedPlusEmpty_EqualsBucketCount()
    {
        HashQualityReport report =
            HashQualityEvaluator.Evaluate<int, Int32Murmur3Hasher>(Range(500), 1024);

        Assert.Equal(report.BucketCount, report.OccupiedBucketCount + report.EmptyBucketCount);
    }

    // ── Quality comparison ──────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_GoodHasher_ScoresNearUniformBaseline()
    {
        int[] keys = Range(2000);

        HashQualityReport report =
            HashQualityEvaluator.Evaluate<int, Int32Murmur3Hasher>(keys, 1024);

        // fmix32 is bijective on uint, so distinct ints never share a raw code.
        Assert.Equal(0, report.CollisionCount);
        // A well-behaved hash sits near the random-uniform baseline of 1.0.
        Assert.InRange(report.DistributionScore, 0.5, 2.0);
    }

    [Fact]
    public void Evaluate_ConstantHasher_ScoresFarWorseThanGoodHasher()
    {
        int[] keys = Range(1000);

        HashQualityReport good =
            HashQualityEvaluator.Evaluate<int, Int32Murmur3Hasher>(keys, 1024);
        HashQualityReport bad =
            HashQualityEvaluator.Evaluate<int, ConstantIntHasher>(keys, 1024);

        Assert.True(bad.DistributionScore > good.DistributionScore * 10,
            $"Constant hasher (score {bad.DistributionScore}) should cluster far worse than Murmur3 (score {good.DistributionScore}).");
        Assert.True(bad.ChiSquared > good.ChiSquared,
            $"Constant hasher chi-squared ({bad.ChiSquared}) should exceed Murmur3 ({good.ChiSquared}).");
        Assert.True(bad.MaxBucketLoad > good.MaxBucketLoad);
    }

    // ── Determinism ─────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_IsDeterministic_ForSameInput()
    {
        int[] keys = Range(300);

        HashQualityReport a = HashQualityEvaluator.Evaluate<int, Int32Murmur3Hasher>(keys, 512);
        HashQualityReport b = HashQualityEvaluator.Evaluate<int, Int32Murmur3Hasher>(keys, 512);

        Assert.Equal(a.KeyCount, b.KeyCount);
        Assert.Equal(a.DistinctHashCount, b.DistinctHashCount);
        Assert.Equal(a.OccupiedBucketCount, b.OccupiedBucketCount);
        Assert.Equal(a.MaxBucketLoad, b.MaxBucketLoad);
        Assert.Equal(a.ChiSquared, b.ChiSquared);
        Assert.Equal(a.DistributionScore, b.DistributionScore);
    }

    // ── String hashers flow through the same generic surface ─────────────────────

    [Fact]
    public void Evaluate_WorksWithStringHashers()
    {
        var keys = new[] { "alpha", "beta", "gamma", "delta", "epsilon" };

        HashQualityReport report =
            HashQualityEvaluator.Evaluate<string, StringMurmur3Hasher>(keys, 64);

        Assert.Equal(5, report.KeyCount);
        Assert.Equal(5, report.DistinctHashCount);
        Assert.Equal(0, report.CollisionCount);
    }

    // ── ToString ────────────────────────────────────────────────────────────────

    [Fact]
    public void ToString_IncludesKeyMetrics()
    {
        HashQualityReport report =
            HashQualityEvaluator.Evaluate<int, Int32Murmur3Hasher>(Range(10), 16);

        string text = report.ToString();

        Assert.Contains("Keys=10", text);
        Assert.Contains("Buckets=16", text);
        Assert.Contains("Score=", text);
    }
}
