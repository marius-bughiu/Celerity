using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="RankSelectBitVector"/> against the naive
/// <c>bool[]</c> oracle a caller writes by hand: rank is a counting loop, select is a scan. CsCheck generates
/// random vectors (random length, random density) and queries every position of each, so any disagreement
/// shrinks to a minimal reproduction with the seed printed.
/// </summary>
public class RankSelectBitVectorDifferentialTests
{
    // Lengths deliberately straddle the 64-bit block and 256-bit superblock boundaries the two-level index
    // pivots on, and the density sweep covers the all-clear and all-set degenerate vectors as well as the middle.
    private static readonly Gen<(int Length, int DensityPercent, uint Seed)> GenVector =
        Gen.Select(Gen.Int[0, 2200], Gen.Int[0, 100], Gen.UInt);

    [Fact]
    public void RankAndSelect_ShouldMatchTheNaiveOracle()
    {
        GenVector.Sample(spec =>
        {
            bool[] oracle = BuildOracle(spec.Length, spec.DensityPercent, spec.Seed);
            int[] prefix = NaiveRanks(oracle);
            List<int> positions = Positions(oracle);
            var vector = new RankSelectBitVector(spec.Length, positions);

            Assert.Equal(oracle.Length, vector.Length);
            Assert.Equal(positions.Count, vector.Count);

            for (int i = 0; i <= oracle.Length; i++)
            {
                Assert.Equal(prefix[i], vector.Rank(i));
                Assert.Equal(i - prefix[i], vector.Rank0(i));
            }

            for (int k = 0; k < positions.Count; k++)
                Assert.Equal(positions[k], vector.Select(k));

            Assert.False(vector.TrySelect(vector.Count, out int missing));
            Assert.Equal(-1, missing);
        }, iter: 300);
    }

    [Fact]
    public void SelectOfRank_ShouldReturnThePositionItself_AtEverySetBit()
    {
        GenVector.Sample(spec =>
        {
            bool[] oracle = BuildOracle(spec.Length, spec.DensityPercent, spec.Seed);
            var vector = new RankSelectBitVector(spec.Length, Positions(oracle));

            for (int i = 0; i < oracle.Length; i++)
            {
                Assert.Equal(oracle[i], vector[i]);

                // Select(Rank(i)) is the identity exactly at a set bit; elsewhere it is the next set bit.
                if (oracle[i])
                    Assert.Equal(i, vector.Select(vector.Rank(i)));
            }
        }, iter: 300);
    }

    [Fact]
    public void BitSetSnapshot_ShouldIndexTheSameBits_AsThePositionList()
    {
        GenVector.Sample(spec =>
        {
            bool[] oracle = BuildOracle(spec.Length, spec.DensityPercent, spec.Seed);

            var fromBitSet = new RankSelectBitVector(new BitSet(oracle));
            var fromPositions = new RankSelectBitVector(spec.Length, Positions(oracle));

            Assert.Equal(fromPositions.Count, fromBitSet.Count);
            for (int i = 0; i <= oracle.Length; i++)
                Assert.Equal(fromPositions.Rank(i), fromBitSet.Rank(i));

            // ToBitSet round-trips through the mutable type without changing what the index reports.
            var rebuilt = new RankSelectBitVector(fromBitSet.ToBitSet());
            Assert.Equal(fromBitSet.Length, rebuilt.Length);
            Assert.Equal(fromBitSet.Count, rebuilt.Count);
        }, iter: 200);
    }

    private static bool[] BuildOracle(int length, int densityPercent, uint seed)
    {
        var rng = new Random((int)seed);
        bool[] bits = new bool[length];
        for (int i = 0; i < length; i++)
            bits[i] = rng.Next(100) < densityPercent;
        return bits;
    }

    private static List<int> Positions(bool[] oracle)
    {
        var positions = new List<int>();
        for (int i = 0; i < oracle.Length; i++)
        {
            if (oracle[i])
                positions.Add(i);
        }
        return positions;
    }

    // prefix[i] == the number of set bits strictly below i, for i in [0, length].
    private static int[] NaiveRanks(bool[] oracle)
    {
        int[] prefix = new int[oracle.Length + 1];
        for (int i = 0; i < oracle.Length; i++)
            prefix[i + 1] = prefix[i] + (oracle[i] ? 1 : 0);
        return prefix;
    }
}
