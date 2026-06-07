using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Worst-case hashing tests for <see cref="BloomFilter{T,THasher}"/>: a degenerate
/// hasher that maps every element to the same base hash still preserves the
/// no-false-negatives guarantee (it just drives the false-positive rate toward 1),
/// and the double-hashing index derivation behaves under adversarial inputs.
/// </summary>
public class BloomFilterCollisionTests
{
    // Maps every value to the same base hash, so all elements set exactly the same
    // bits — the pathological case for a Bloom filter.
    private struct ConstantHasher : IHashProvider<int>
    {
        public int Hash(int key) => 0;
    }

    // Maps every value to a distinct constant unrelated to the input magnitude.
    private struct NegativeConstantHasher : IHashProvider<int>
    {
        public int Hash(int key) => int.MinValue;
    }

    [Fact]
    public void ConstantHasher_StillHasNoFalseNegatives()
    {
        var filter = new BloomFilter<int, ConstantHasher>(1000);
        for (int i = 0; i < 100; i++)
            filter.Add(i);

        // Every added element must still report present.
        for (int i = 0; i < 100; i++)
            Assert.True(filter.Contains(i), $"false negative under constant hasher for {i}");
    }

    [Fact]
    public void ConstantHasher_MakesEverythingLookPresent()
    {
        // With all elements colliding on the same bits, adding one element makes the
        // filter report any element as present. This is graceful degradation (the
        // false-positive rate goes to 1), not a correctness failure.
        var filter = new BloomFilter<int, ConstantHasher>(1000);
        filter.Add(1);

        Assert.True(filter.Contains(1));
        Assert.True(filter.Contains(99999)); // never added, but collides
    }

    [Fact]
    public void EmptyFilter_WithConstantHasher_ReportsAbsent()
    {
        var filter = new BloomFilter<int, ConstantHasher>(1000);
        Assert.False(filter.Contains(0));
        Assert.False(filter.Contains(123));
    }

    [Fact]
    public void NegativeConstantBaseHash_DoesNotBreakIndexing()
    {
        // The base hash int.MinValue is cast to uint and avalanched; the masked bit
        // index must stay in range and round-trip.
        var filter = new BloomFilter<int, NegativeConstantHasher>(1000);
        filter.Add(5);
        Assert.True(filter.Contains(5));
        Assert.True(filter.Contains(6)); // collides on the constant base hash
    }

    [Fact]
    public void ConstantHasher_Clear_Resets()
    {
        var filter = new BloomFilter<int, ConstantHasher>(1000);
        filter.Add(1);
        Assert.True(filter.Contains(2)); // collides → looks present

        filter.Clear();
        Assert.False(filter.Contains(2));
        Assert.Equal(0, filter.Count);
    }

    [Fact]
    public void ConstantHasher_HashCount_AtLeastOne()
    {
        // Even when k computes small, at least one bit is probed, so a single Add /
        // Contains round-trips.
        var filter = new BloomFilter<int, ConstantHasher>(1, 0.5);
        Assert.True(filter.HashCount >= 1);
        filter.Add(7);
        Assert.True(filter.Contains(7));
    }
}
