namespace Celerity.Hashing;

/// <summary>
/// An immutable summary of how well a hash function distributes a sample of keys,
/// produced by <see cref="HashQualityEvaluator"/>.
/// </summary>
/// <remarks>
/// The metrics split into two groups. <see cref="DistinctHashCount"/>,
/// <see cref="CollisionCount"/>, and <see cref="CollisionRate"/> describe the
/// hasher's <em>intrinsic</em> behaviour — how often it produces the same 32-bit
/// code for two different keys, independent of any table size. The remaining
/// fields describe how the codes spread across <see cref="BucketCount"/> power-of-two
/// buckets (masked with <c>code &amp; (BucketCount - 1)</c>, exactly as the Celerity
/// collections index their backing arrays), which is what actually drives probe-chain
/// length in a real table.
/// </remarks>
public readonly struct HashQualityReport
{
    internal HashQualityReport(
        int keyCount,
        int distinctHashCount,
        int collisionCount,
        double collisionRate,
        int bucketCount,
        int occupiedBucketCount,
        int emptyBucketCount,
        int maxBucketLoad,
        double meanBucketLoad,
        double chiSquared,
        double distributionScore)
    {
        KeyCount = keyCount;
        DistinctHashCount = distinctHashCount;
        CollisionCount = collisionCount;
        CollisionRate = collisionRate;
        BucketCount = bucketCount;
        OccupiedBucketCount = occupiedBucketCount;
        EmptyBucketCount = emptyBucketCount;
        MaxBucketLoad = maxBucketLoad;
        MeanBucketLoad = meanBucketLoad;
        ChiSquared = chiSquared;
        DistributionScore = distributionScore;
    }

    /// <summary>The number of keys that were hashed.</summary>
    public int KeyCount { get; }

    /// <summary>The number of distinct 32-bit hash codes produced across the sample.</summary>
    public int DistinctHashCount { get; }

    /// <summary>
    /// The number of keys that shared a 32-bit hash code with an earlier key
    /// (<see cref="KeyCount"/> minus <see cref="DistinctHashCount"/>). This measures the
    /// hasher's intrinsic injectivity and is independent of <see cref="BucketCount"/>.
    /// </summary>
    public int CollisionCount { get; }

    /// <summary>
    /// <see cref="CollisionCount"/> as a fraction of <see cref="KeyCount"/>, in the range
    /// <c>[0, 1)</c>. Zero when the sample is empty.
    /// </summary>
    public double CollisionRate { get; }

    /// <summary>
    /// The number of buckets the codes were distributed across. This is the requested
    /// bucket count rounded up to the next power of two, matching how the Celerity
    /// collections size their backing arrays.
    /// </summary>
    public int BucketCount { get; }

    /// <summary>The number of buckets that received at least one key.</summary>
    public int OccupiedBucketCount { get; }

    /// <summary>The number of buckets that received no keys (<see cref="BucketCount"/> minus <see cref="OccupiedBucketCount"/>).</summary>
    public int EmptyBucketCount { get; }

    /// <summary>The largest number of keys that landed in any single bucket.</summary>
    public int MaxBucketLoad { get; }

    /// <summary>The average number of keys per bucket (<see cref="KeyCount"/> divided by <see cref="BucketCount"/>).</summary>
    public double MeanBucketLoad { get; }

    /// <summary>
    /// Pearson's chi-squared statistic for the bucket loads against a uniform expectation:
    /// <c>sum over buckets of (load - mean)^2 / mean</c>. Lower is closer to uniform; for a
    /// good hash on a uniform key sample it is approximately <c>BucketCount - 1</c>. Zero when
    /// the sample is empty.
    /// </summary>
    public double ChiSquared { get; }

    /// <summary>
    /// A normalized distribution score: the ratio of the observed sum of squared bucket loads
    /// to the value expected from an ideal uniform hash. <c>1.0</c> indicates an ideal uniform
    /// spread; values above <c>1.0</c> indicate clustering (worse, longer probe chains); values
    /// below <c>1.0</c> indicate a more even than random spread (e.g. near-perfect hashing).
    /// Defaults to <c>1.0</c> for an empty sample.
    /// </summary>
    public double DistributionScore { get; }

    /// <summary>Returns a single-line, human-readable summary of the report.</summary>
    /// <returns>A formatted string containing the key metrics.</returns>
    public override string ToString() =>
        $"Keys={KeyCount}, Distinct={DistinctHashCount}, Collisions={CollisionCount} ({CollisionRate:P2}), " +
        $"Buckets={BucketCount}, Occupied={OccupiedBucketCount}, Empty={EmptyBucketCount}, " +
        $"MaxLoad={MaxBucketLoad}, MeanLoad={MeanBucketLoad:F3}, ChiSquared={ChiSquared:F2}, Score={DistributionScore:F4}";
}
