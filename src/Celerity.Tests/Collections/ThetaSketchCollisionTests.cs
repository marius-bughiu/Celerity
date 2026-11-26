using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Worst-case hashing tests for <see cref="ThetaSketch{T,THasher}"/>: a degenerate hasher
/// collapses every element onto one sample value (graceful degradation, not a crash), the
/// extreme 64-bit hashes map to the ends of the sample range without colliding with the
/// empty-slot marker, and values that collide in the table's slot index still probe apart.
/// </summary>
public class ThetaSketchCollisionTests
{
    private struct ConstantHasher : IHashProvider<int>
    {
        public int Hash(int key) => 0;
    }

    // Every element published as hash 0 or all-ones: the smallest and largest sample values.
    private struct ExtremeHasher64 : IHashProvider<int>, IHashProvider64<int>
    {
        public int Hash(int key) => key;

        public ulong Hash64(int key) => key == 0 ? 0UL : ulong.MaxValue;
    }

    // Sample values that share their low bits; the slot index is taken from a multiplicative
    // mix, but a dense run of small values is the shape a falling threshold produces.
    private struct SmallValueHasher64 : IHashProvider<int>, IHashProvider64<int>
    {
        public int Hash(int key) => key;

        public ulong Hash64(int key) => (ulong)key << 1;
    }

    [Fact]
    public void ConstantHasher_CountsEveryElementAsOne()
    {
        var sketch = new ThetaSketch<int, ConstantHasher>(4);
        for (int i = 0; i < 10_000; i++)
            sketch.Add(i);

        Assert.Equal(1, sketch.RetainedCount);
        Assert.False(sketch.IsEstimationMode);
        Assert.Equal(1d, sketch.EstimateCardinality());
    }

    [Fact]
    public void ExtremeHashes_AreBothRetained()
    {
        // Hash 0 becomes sample value 1 and the all-ones hash becomes 2^63: neither is the empty
        // marker 0, and 2^63 sits exactly on an exact sketch's inclusive threshold.
        var sketch = new ThetaSketch<int, ExtremeHasher64>(4);
        sketch.Add(0);
        sketch.Add(1);
        sketch.Add(2); // same hash as 1

        Assert.Equal(2d, sketch.EstimateCardinality());
        Assert.Equal(2, sketch.Compact().RetainedCount);
    }

    [Fact]
    public void ADenseRunOfSmallValues_ProbesApartThroughGrowthAndRebuilds()
    {
        // Keys 0..N publish hashes 0, 2, 4, ..., i.e. the consecutive sample values 1..N+1 —
        // the densest the table can be asked to hold. Every one must be found again.
        var sketch = new ThetaSketch<int, SmallValueHasher64>(6);
        for (int i = 0; i < 96; i++)
            sketch.Add(i);
        Assert.Equal(96, sketch.RetainedCount);
        Assert.False(sketch.IsEstimationMode);

        for (int i = 0; i < 96; i++)
            sketch.Add(i); // all duplicates
        Assert.Equal(96, sketch.RetainedCount);

        sketch.Add(96); // 97 > 1.5k = 96: rebuild to the 64 smallest, values 1..64
        Assert.Equal(64, sketch.RetainedCount);
        Assert.Equal(64d / 9223372036854775808d, sketch.Theta);
        for (int i = 0; i < 64; i++)
            sketch.Add(i);
        Assert.Equal(64, sketch.RetainedCount);
    }
}
