using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Worst-case hashing tests for <see cref="MinHashSketch{T,THasher}"/>: a degenerate hasher
/// that maps every element to the same base hash makes every set look identical (graceful
/// degradation, not a crash), and adversarial constant base hashes still produce in-range
/// register indices and values that never collide with the empty-register marker.
/// </summary>
public class MinHashSketchCollisionTests
{
    // Maps every value to the same base hash, so every element routes to the same register
    // with the same value — the pathological case for MinHash.
    private struct ConstantHasher : IHashProvider<int>
    {
        public int Hash(int key) => 0;
    }

    // Maps every value to a constant at the negative extreme; cast to uint and avalanched,
    // it must still yield an in-range register index.
    private struct NegativeConstantHasher : IHashProvider<int>
    {
        public int Hash(int key) => int.MinValue;
    }

    // Publishes an all-ones 64-bit hash, the largest the sketch can see: its register is the
    // last one and its value is the widest the low bits allow. That value must still read as
    // a filled register, not as the empty marker.
    private struct AllOnes64Hasher : IHashProvider<int>, IHashProvider64<int>
    {
        public int Hash(int key) => -1;

        public ulong Hash64(int key) => ulong.MaxValue;
    }

    [Fact]
    public void ConstantHasher_MakesDisjointSetsLookIdentical()
    {
        // With every element hashing identically, every set fills the same register with the
        // same value, so the sketch cannot tell any two non-empty sets apart. This is graceful
        // degradation (an overestimate), not a correctness failure or an exception.
        var a = new MinHashSketch<int, ConstantHasher>();
        var b = new MinHashSketch<int, ConstantHasher>();
        for (int i = 0; i < 10_000; i++)
        {
            a.Add(i);
            b.Add(i + 1_000_000);
        }

        Assert.Equal(1d, a.EstimateJaccard(b));
    }

    [Fact]
    public void ConstantHasher_EmptyAgainstNonEmpty_StillEstimatesZero()
    {
        var empty = new MinHashSketch<int, ConstantHasher>();
        var full = new MinHashSketch<int, ConstantHasher>();
        full.Add(1);
        Assert.Equal(0d, empty.EstimateJaccard(full));
    }

    [Fact]
    public void NegativeConstantBaseHash_ProducesInRangeRegister()
    {
        var a = new MinHashSketch<int, NegativeConstantHasher>(10);
        var b = new MinHashSketch<int, NegativeConstantHasher>(10);
        for (int i = 0; i < 500; i++)
            a.Add(i);
        b.Add(-1);

        Assert.False(a.IsEmpty);
        Assert.Equal(1d, a.EstimateJaccard(b));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(16)]
    public void AllOnesHash_IsNotMistakenForAnEmptyRegister(int precision)
    {
        var sketch = new MinHashSketch<int, AllOnes64Hasher>(precision);
        sketch.Add(1);

        Assert.False(sketch.IsEmpty);
        Assert.Equal(0d, sketch.EstimateJaccard(new MinHashSketch<int, AllOnes64Hasher>(precision)));
    }

    [Fact]
    public void ConstantHasher_Clear_Resets()
    {
        var sketch = new MinHashSketch<int, ConstantHasher>();
        sketch.Add(1);
        Assert.False(sketch.IsEmpty);

        sketch.Clear();
        Assert.True(sketch.IsEmpty);
    }

    [Fact]
    public void MinPrecision_StillEstimatesWithinItsWideBound()
    {
        // At the minimum precision (16 registers) the standard error at J = 1/3 is about 0.12;
        // assert a sane band rather than a tight one.
        var a = new MinHashSketch<int, Int32Murmur3Hasher>(MinHashSketch<int, Int32Murmur3Hasher>.MinPrecision);
        var b = new MinHashSketch<int, Int32Murmur3Hasher>(MinHashSketch<int, Int32Murmur3Hasher>.MinPrecision);
        for (int i = 0; i < 2_000; i++)
            a.Add(i);
        for (int i = 1_000; i < 3_000; i++)
            b.Add(i);

        Assert.InRange(a.EstimateJaccard(b), 0d, 0.85d);
    }
}
