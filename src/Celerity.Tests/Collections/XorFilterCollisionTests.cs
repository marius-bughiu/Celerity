using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Worst-case hashing tests for <see cref="XorFilter{T,THasher}"/>. Unlike the open-addressed collections an
/// xor filter has no probe chain, so a degenerate hasher does not stress a collision run — instead it
/// collapses distinct elements onto the same 64-bit key, which the constructor's set-dedupe folds into a
/// single represented entry. The no-false-negatives guarantee must survive that folding, and construction
/// must still converge.
/// </summary>
public class XorFilterCollisionTests
{
    // Maps every value to the same base hash, so every element shares one 64-bit key.
    private struct ConstantHasher : IHashProvider<int>
    {
        public int Hash(int key) => 0;
    }

    // Maps every value to the same negative constant, exercising the (uint) cast + avalanche.
    private struct NegativeConstantHasher : IHashProvider<int>
    {
        public int Hash(int key) => int.MinValue;
    }

    [Fact]
    public void ConstantHasher_CollapsesToOneKey_ButNoFalseNegatives()
    {
        var filter = new XorFilter<int, ConstantHasher>(new[] { 1, 2, 3, 4, 5 });

        // All five elements hash identically, so the filter represents a single distinct key.
        Assert.Equal(1, filter.Count);

        // Every original element still tests present (they are indistinguishable from the one key).
        for (int i = 1; i <= 5; i++)
            Assert.True(filter.Contains(i), $"false negative under constant hasher for {i}");

        // And so does any other element sharing the constant hash — graceful degradation, not a failure.
        Assert.True(filter.Contains(99999));
    }

    [Fact]
    public void NegativeConstantBaseHash_ConstructsAndRoundTrips()
    {
        var filter = new XorFilter<int, NegativeConstantHasher>(new[] { 5, 6, 7 });
        Assert.Equal(1, filter.Count); // all collapse
        Assert.True(filter.Contains(5));
        Assert.True(filter.Contains(6));
    }

    [Fact]
    public void EmptyConstantHasherFilter_ReportsAbsent()
    {
        var filter = new XorFilter<int, ConstantHasher>(Array.Empty<int>());
        Assert.Equal(0, filter.Count);
        Assert.False(filter.Contains(0));
        Assert.False(filter.Contains(123));
    }

    [Fact]
    public void ManyElements_ConstructAndRoundTripWithAWeakHasher()
    {
        // The naive Wang fold is a weak hasher; the 64-bit avalanche in the filter must still spread the keys
        // across the three segments well enough to peel a large set.
        var source = Enumerable.Range(0, 10_000).Select(i => i * 3).ToArray();
        var filter = new XorFilter<int, Int32WangNaiveHasher>(source);

        Assert.Equal(source.Length, filter.Count);
        foreach (int v in source)
            Assert.True(filter.Contains(v), $"false negative for {v}");
    }
}
