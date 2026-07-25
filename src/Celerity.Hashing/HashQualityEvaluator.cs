namespace Celerity.Hashing;

/// <summary>
/// Measures how well an <see cref="IHashProvider{T}"/> distributes a sample of keys, returning a
/// <see cref="HashQualityReport"/> of collision and bucket-distribution metrics.
/// </summary>
/// <remarks>
/// This is a diagnostic / benchmarking tool, not a hot-path API: it allocates working buffers and
/// runs an <see cref="System.Collections.Generic.IEnumerable{T}"/>, so call it offline (in tests,
/// benchmarks, or a one-off analysis) to compare candidate hashers for a given key shape — not on a
/// request path. Hash codes are masked into power-of-two buckets with <c>code &amp; (buckets - 1)</c>,
/// exactly as the Celerity collections index their backing arrays, so the bucket metrics reflect the
/// clustering a real table would experience.
/// </remarks>
public static class HashQualityEvaluator
{
    /// <summary>
    /// Hashes every key in <paramref name="keys"/> with <typeparamref name="THasher"/> and reports how
    /// evenly the resulting codes spread, both intrinsically and across <paramref name="bucketCount"/>
    /// buckets.
    /// </summary>
    /// <typeparam name="T">The key type.</typeparam>
    /// <typeparam name="THasher">The hash provider to evaluate. Instantiated via <c>default</c>; the built-in hashers are stateless structs.</typeparam>
    /// <param name="keys">
    /// The sample of keys to hash. Pass distinct keys to measure the hasher's intrinsic quality; any
    /// duplicate keys in the sample naturally hash to the same code and count as collisions.
    /// </param>
    /// <param name="bucketCount">
    /// The number of buckets to distribute the codes across. Rounded up to the next power of two to match
    /// the backing-array sizing of the Celerity collections. Defaults to <c>1024</c>.
    /// </param>
    /// <returns>A <see cref="HashQualityReport"/> describing the distribution.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="keys"/> is <see langword="null"/>.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="bucketCount"/> is less than 1.</exception>
    public static HashQualityReport Evaluate<T, THasher>(IEnumerable<T> keys, int bucketCount = 1024)
        where THasher : struct, IHashProvider<T>
    {
        if (keys is null)
        {
            throw new ArgumentNullException(nameof(keys));
        }

        if (bucketCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(bucketCount), bucketCount, "Bucket count must be at least 1.");
        }

        int buckets = FastUtils.NextPowerOfTwo(bucketCount);
        int mask = buckets - 1;

        int[] counts = new int[buckets];
        HashSet<int> distinctHashes = new();
        THasher hasher = default;

        int keyCount = 0;
        foreach (T key in keys)
        {
            int code = hasher.Hash(key);
            distinctHashes.Add(code);
            counts[code & mask]++;
            keyCount++;
        }

        return Summarize(counts, keyCount, distinctHashes.Count);
    }

    /// <summary>
    /// The <see cref="IHashProvider64{T}"/> counterpart of
    /// <see cref="Evaluate{T, THasher}(IEnumerable{T}, int)"/>: hashes every key with
    /// <typeparamref name="THasher"/>'s 64-bit surface and reports the same metrics.
    /// </summary>
    /// <remarks>
    /// The collision figures are the reason to reach for this overload. A 32-bit hasher can
    /// produce at most 2^32 distinct codes, so on a large key sample its collision rate is
    /// bounded below by the size of the hash space rather than by the quality of its mixing —
    /// which is invisible in <see cref="Evaluate{T, THasher}(IEnumerable{T}, int)"/>, where every
    /// hasher is measured against the same ceiling. Evaluating the 64-bit surface separates the
    /// two: collisions reported here are the hasher's own. The bucket metrics mask the low bits
    /// of the 64-bit code, mirroring how a power-of-two-sized consumer would index with it.
    /// </remarks>
    /// <typeparam name="T">The key type.</typeparam>
    /// <typeparam name="THasher">The 64-bit hash provider to evaluate. Instantiated via <c>default</c>; the built-in hashers are stateless structs.</typeparam>
    /// <param name="keys">The sample of keys to hash.</param>
    /// <param name="bucketCount">The number of buckets, rounded up to the next power of two. Defaults to <c>1024</c>.</param>
    /// <returns>A <see cref="HashQualityReport"/> describing the distribution.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="keys"/> is <see langword="null"/>.</exception>
    /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="bucketCount"/> is less than 1.</exception>
    public static HashQualityReport Evaluate64<T, THasher>(IEnumerable<T> keys, int bucketCount = 1024)
        where THasher : struct, IHashProvider64<T>
    {
        if (keys is null)
        {
            throw new ArgumentNullException(nameof(keys));
        }

        if (bucketCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(bucketCount), bucketCount, "Bucket count must be at least 1.");
        }

        int buckets = FastUtils.NextPowerOfTwo(bucketCount);
        ulong mask = (ulong)(buckets - 1);

        int[] counts = new int[buckets];
        HashSet<ulong> distinctHashes = new();
        THasher hasher = default;

        int keyCount = 0;
        foreach (T key in keys)
        {
            ulong code = hasher.Hash64(key);
            distinctHashes.Add(code);
            counts[(int)(code & mask)]++;
            keyCount++;
        }

        return Summarize(counts, keyCount, distinctHashes.Count);
    }

    // Shared statistics pass over the per-bucket loads. Identical for both surfaces: only the
    // width of the hash code and of the distinct-code set differ above.
    private static HashQualityReport Summarize(int[] counts, int keyCount, int distinctCount)
    {
        int buckets = counts.Length;

        int occupied = 0;
        int maxLoad = 0;
        for (int i = 0; i < buckets; i++)
        {
            int load = counts[i];
            if (load > 0)
            {
                occupied++;
            }

            if (load > maxLoad)
            {
                maxLoad = load;
            }
        }

        double mean = (double)keyCount / buckets;
        double chiSquared = 0.0;
        double distributionScore = 1.0;

        if (keyCount > 0)
        {
            double chiAccumulator = 0.0;
            long sumSquares = 0;
            for (int i = 0; i < buckets; i++)
            {
                int load = counts[i];
                double diff = load - mean;
                chiAccumulator += diff * diff;
                sumSquares += (long)load * load;
            }

            chiSquared = chiAccumulator / mean;

            // Expected sum of squared bucket loads for an ideal uniform hash:
            // every key contributes 1 to its own bucket's square, and each ordered
            // pair of keys has a 1/buckets chance of sharing a bucket.
            double expectedSumSquares = keyCount + (double)keyCount * (keyCount - 1) / buckets;
            distributionScore = sumSquares / expectedSumSquares;
        }

        int collisionCount = keyCount - distinctCount;
        double collisionRate = keyCount == 0 ? 0.0 : (double)collisionCount / keyCount;

        return new HashQualityReport(
            keyCount,
            distinctCount,
            collisionCount,
            collisionRate,
            buckets,
            occupied,
            buckets - occupied,
            maxLoad,
            mean,
            chiSquared,
            distributionScore);
    }
}
