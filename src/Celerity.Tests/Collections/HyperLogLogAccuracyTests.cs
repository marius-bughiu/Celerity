using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Statistical tests that the realized relative error of
/// <see cref="HyperLogLog{T,THasher}.EstimateCardinality"/> stays within a small
/// multiple of the theoretical standard error across a range of cardinalities and key
/// shapes. The data sets are deterministic (fixed sequences) so the measured error is
/// stable across runs.
/// </summary>
public class HyperLogLogAccuracyTests
{
    [Theory]
    [InlineData(1_000)]
    [InlineData(10_000)]
    [InlineData(100_000)]
    [InlineData(1_000_000)]
    public void IntCardinality_EstimateWithinErrorBound(int n)
    {
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>(); // precision 14
        for (int i = 0; i < n; i++)
            hll.Add(i);

        long estimate = hll.EstimateCardinality();
        double relativeError = Math.Abs(estimate - n) / (double)n;

        // The 14-bit estimator has a ~0.81% standard error. Allow 3 standard errors
        // plus a small absolute slack to cover the small-range regime.
        double bound = hll.StandardError * 3 + 0.005;
        Assert.True(relativeError <= bound,
            $"n={n}: estimate {estimate} had relative error {relativeError:F4} > bound {bound:F4}");
    }

    [Theory]
    [InlineData(5_000)]
    [InlineData(50_000)]
    public void StringCardinality_EstimateWithinErrorBound(int n)
    {
        var hll = new HyperLogLog<string, StringMurmur3Hasher>();
        for (int i = 0; i < n; i++)
            hll.Add($"element-{i}");

        long estimate = hll.EstimateCardinality();
        double relativeError = Math.Abs(estimate - n) / (double)n;

        double bound = hll.StandardError * 3 + 0.01;
        Assert.True(relativeError <= bound,
            $"n={n}: string estimate {estimate} had relative error {relativeError:F4} > bound {bound:F4}");
    }

    [Fact]
    public void DuplicateHeavyStream_CountsOnlyDistinct()
    {
        // 1,000,000 adds but only 25,000 distinct values: the estimate must track the
        // distinct count, not the number of adds.
        const int distinct = 25_000;
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>();
        for (int i = 0; i < 1_000_000; i++)
            hll.Add(i % distinct);

        long estimate = hll.EstimateCardinality();
        double relativeError = Math.Abs(estimate - distinct) / (double)distinct;
        Assert.True(relativeError <= hll.StandardError * 3 + 0.01,
            $"distinct={distinct}: estimate {estimate} had relative error {relativeError:F4}");
    }

    [Fact]
    public void LongCardinality_EstimateWithinErrorBound()
    {
        const int n = 100_000;
        var hll = new HyperLogLog<long, Int64Murmur3Hasher>();
        for (long i = 0; i < n; i++)
            hll.Add(i * 2_147_483_647L + 1); // spread across the 64-bit range

        long estimate = hll.EstimateCardinality();
        double relativeError = Math.Abs(estimate - n) / (double)n;
        Assert.True(relativeError <= hll.StandardError * 3 + 0.005,
            $"long estimate {estimate} had relative error {relativeError:F4}");
    }

    // ── The 2^32 hash-entropy floor (#304) ────────────────────────────────────
    //
    // A hasher that publishes only 32 bits can produce at most 2^32 distinct hashes, so what
    // the registers measure is the number of distinct *hashes*, not the number of distinct
    // *elements*: n elements produce only 2^32·(1 − e^(−n/2^32)) distinct hashes, and the
    // gap is a systematic undercount that grows with n. At 2.4e8 elements it is 2.8% —
    // several times the estimator's own 0.41% standard error at precision 16 — so before
    // this was fixed the type was quietly outside its advertised error budget in exactly the
    // large-stream regime it is sold for.
    //
    // The two tests below pin both halves of the fix: the 32-bit path recovers the true
    // count with the classical large-range correction, and the 64-bit path never saturates
    // in the first place.

    private const long LargeN = 240_000_000;

    /// <summary>The number of distinct 32-bit hashes <see cref="LargeN"/> elements produce.</summary>
    private static double SaturatedDistinctHashes =>
        4294967296d * (1d - Math.Exp(-LargeN / 4294967296d));

    /// <summary>
    /// A <em>strong</em> 32-bit hasher: the Murmur3 64-bit finalizer xor-folded down. Its
    /// distribution is excellent, so the only defect left for the test to observe is the
    /// size of the hash space itself.
    /// </summary>
    private struct Fold32Hasher : IHashProvider<long>
    {
        public int Hash(long key)
        {
            ulong x = (ulong)key;
            x ^= x >> 33;
            x *= 0xff51afd7ed558ccdUL;
            x ^= x >> 33;
            x *= 0xc4ceb9fe1a85ec53UL;
            x ^= x >> 33;
            return unchecked((int)(x ^ (x >> 32)));
        }
    }

    [Fact]
    [Trait("Category", "LargeCardinality")]
    public void HugeCardinality_32BitHasher_LargeRangeCorrectionRecoversTheSaturatedCount()
    {
        var hll = new HyperLogLog<long, Fold32Hasher>(HyperLogLog<long, Fold32Hasher>.MAX_PRECISION);
        for (long i = 0; i < LargeN; i++)
            hll.Add(i);

        long estimate = hll.EstimateCardinality();
        double relativeError = Math.Abs(estimate - LargeN) / (double)LargeN;

        // Without the correction the estimator would report the distinct-hash count, which
        // is ~2.8% low here — far outside the bound below. Its own noise is 0.41%.
        Assert.True(relativeError <= hll.StandardError * 3 + 0.005,
            $"32-bit-hasher estimate {estimate} had relative error {relativeError:F4}; "
            + $"uncorrected it would have been ~{SaturatedDistinctHashes:F0}");

        // And the correction is what got it there: the raw register state cannot see more
        // than the saturated distinct-hash count, so an estimate meaningfully above that
        // could only come from inverting the saturation.
        Assert.True(estimate > SaturatedDistinctHashes * 1.01,
            $"estimate {estimate} did not clear the saturated distinct-hash count "
            + $"{SaturatedDistinctHashes:F0} — the large-range correction did not fire");
    }

    [Fact]
    [Trait("Category", "LargeCardinality")]
    public void HugeCardinality_64BitHasher_StaysWithinTheStandardErrorWithNoCorrection()
    {
        // Int64WangHasher implements IHashProvider64<long>, so the estimator sees a 2^64 hash
        // space, never saturates, and needs no correction at all.
        var hll = new HyperLogLog<long, Int64WangHasher>(HyperLogLog<long, Int64WangHasher>.MAX_PRECISION);
        for (long i = 0; i < LargeN; i++)
            hll.Add(i);

        long estimate = hll.EstimateCardinality();
        double relativeError = Math.Abs(estimate - LargeN) / (double)LargeN;

        Assert.True(relativeError <= hll.StandardError * 3 + 0.002,
            $"64-bit-hasher estimate {estimate} had relative error {relativeError:F4}");
    }
}
