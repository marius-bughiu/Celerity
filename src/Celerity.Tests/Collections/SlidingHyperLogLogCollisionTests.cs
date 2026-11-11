using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Worst-case hashing tests for <see cref="SlidingHyperLogLog{T,THasher}"/>, mirroring
/// <see cref="HyperLogLogCollisionTests"/>: a degenerate hasher collapses every element onto one
/// register and rank, which must degrade to "one distinct element" in every window rather than
/// crash, and adversarial constant hashes must still land on in-range cells — including the
/// highest rank a precision allows, which is the last cell of a register's row.
/// </summary>
public class SlidingHyperLogLogCollisionTests
{
    private struct ConstantHasher : IHashProvider<int>
    {
        public int Hash(int key) => 0;
    }

    private struct NegativeConstantHasher : IHashProvider<int>
    {
        public int Hash(int key) => int.MinValue;
    }

    // A 64-bit hasher whose hash is all zeros: register 0, and every bit after the register index
    // is zero, so the rank is the maximum 65 - precision — the last cell of register 0's row.
    private struct ZeroHash64Hasher : IHashProvider<int>, IHashProvider64<int>
    {
        public int Hash(int key) => 0;
        public ulong Hash64(int key) => 0UL;
    }

    // All ones: the last register, with rank 1 — the first cell of the last row.
    private struct OnesHash64Hasher : IHashProvider<int>, IHashProvider64<int>
    {
        public int Hash(int key) => -1;
        public ulong Hash64(int key) => ulong.MaxValue;
    }

    [Fact]
    public void ConstantHasher_CollapsesToOneDistinctElement_InEveryWindow()
    {
        var s = new SlidingHyperLogLog<int, ConstantHasher>();
        for (int i = 0; i < 10_000; i++)
            s.Add(i, i);

        Assert.Equal(1, s.EstimateCardinality());
        Assert.Equal(1, s.EstimateCardinality(9_999));
        Assert.Equal(0, s.EstimateCardinality(10_000));
    }

    [Fact]
    public void NegativeConstantHasher_StaysInRange()
    {
        var s = new SlidingHyperLogLog<int, NegativeConstantHasher>(4);
        for (int i = 0; i < 100; i++)
            s.Add(i, i);
        Assert.Equal(1, s.EstimateCardinality(50));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(12)]
    [InlineData(16)]
    public void MaximumRank_LandsOnTheLastCellOfTheRow(int precision)
    {
        var s = new SlidingHyperLogLog<int, ZeroHash64Hasher>(precision);
        s.Add(1, 7);

        var hll = new HyperLogLog<int, ZeroHash64Hasher>(precision);
        hll.Add(1);

        Assert.Equal(hll.EstimateCardinality(), s.EstimateCardinality());
        Assert.Equal(hll.EstimateCardinality(), s.EstimateCardinality(7));
        Assert.Equal(0, s.EstimateCardinality(8));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(16)]
    public void LastRegister_RankOne_IsInRange(int precision)
    {
        var s = new SlidingHyperLogLog<int, OnesHash64Hasher>(precision);
        s.Add(1, 3);
        Assert.Equal(1, s.EstimateCardinality(3));
        Assert.Equal(0, s.EstimateCardinality(4));
    }
}
