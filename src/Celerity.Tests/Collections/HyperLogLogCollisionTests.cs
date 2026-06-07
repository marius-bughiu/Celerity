using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Worst-case hashing tests for <see cref="HyperLogLog{T,THasher}"/>: a degenerate
/// hasher that maps every element to the same base hash collapses the estimate to one
/// distinct value (graceful degradation, not a crash), and adversarial constant base
/// hashes still produce in-range register indices and ranks.
/// </summary>
public class HyperLogLogCollisionTests
{
    // Maps every value to the same base hash, so every element routes to the same
    // register with the same rank — the pathological case for HyperLogLog.
    private struct ConstantHasher : IHashProvider<int>
    {
        public int Hash(int key) => 0;
    }

    // Maps every value to a constant at the negative extreme; cast to uint and
    // avalanched, it must still yield an in-range register index and rank.
    private struct NegativeConstantHasher : IHashProvider<int>
    {
        public int Hash(int key) => int.MinValue;
    }

    [Fact]
    public void ConstantHasher_CollapsesToOneDistinctValue()
    {
        // With every element hashing identically, the estimator sees a single distinct
        // value no matter how many elements are added. This is graceful degradation
        // (an underestimate), not a correctness failure or an exception.
        var hll = new HyperLogLog<int, ConstantHasher>();
        for (int i = 0; i < 10_000; i++)
            hll.Add(i);

        Assert.Equal(1, hll.EstimateCardinality());
    }

    [Fact]
    public void ConstantHasher_EmptyEstimatesZero()
    {
        var hll = new HyperLogLog<int, ConstantHasher>();
        Assert.Equal(0, hll.EstimateCardinality());
    }

    [Fact]
    public void NegativeConstantBaseHash_ProducesInRangeRegister()
    {
        // The base hash int.MinValue is cast to uint and avalanched; the register index
        // and rank must stay in range and the estimate must round-trip to one distinct.
        var hll = new HyperLogLog<int, NegativeConstantHasher>(10);
        for (int i = 0; i < 500; i++)
            hll.Add(i);

        Assert.Equal(1, hll.EstimateCardinality());
    }

    [Fact]
    public void ConstantHasher_Clear_Resets()
    {
        var hll = new HyperLogLog<int, ConstantHasher>();
        hll.Add(1);
        Assert.Equal(1, hll.EstimateCardinality());

        hll.Clear();
        Assert.Equal(0, hll.EstimateCardinality());
    }

    [Fact]
    public void MinPrecision_StillProducesValidIndicesAndRanks()
    {
        // At the minimum precision (16 registers) the remaining bit field is widest, so
        // the rank can reach its maximum; exercise it across a spread of inputs to make
        // sure the sentinel bit keeps every rank in range.
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>(HyperLogLog<int, Int32Murmur3Hasher>.MIN_PRECISION);
        for (int i = 0; i < 1000; i++)
            hll.Add(i);

        // 16 registers gives a ~26% standard error, so just assert the estimate is a
        // sane positive number in the right ballpark rather than tightly bounded.
        long estimate = hll.EstimateCardinality();
        Assert.InRange(estimate, 500, 2000);
    }
}
