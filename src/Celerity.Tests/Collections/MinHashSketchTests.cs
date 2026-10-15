using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

public class MinHashSketchTests
{
    private static MinHashSketch<int, Int32Murmur3Hasher> Range(int start, int count, int precision = MinHashSketch<int, Int32Murmur3Hasher>.DefaultPrecision)
    {
        var sketch = new MinHashSketch<int, Int32Murmur3Hasher>(precision);
        for (int i = start; i < start + count; i++)
            sketch.Add(i);
        return sketch;
    }

    [Fact]
    public void NewSketch_IsEmpty()
    {
        var sketch = new MinHashSketch<int, Int32Murmur3Hasher>();
        Assert.True(sketch.IsEmpty);
    }

    [Fact]
    public void Add_MakesTheSketchNonEmpty()
    {
        var sketch = new MinHashSketch<int, Int32Murmur3Hasher>();
        sketch.Add(42);
        Assert.False(sketch.IsEmpty);
    }

    [Fact]
    public void IdenticalSets_EstimateOne()
    {
        // Two sketches of the same set hold the same minimum in every register.
        Assert.Equal(1d, Range(0, 10_000).EstimateJaccard(Range(0, 10_000)));
    }

    [Fact]
    public void SameSetInADifferentOrder_EstimatesOne()
    {
        var forward = Range(0, 5_000);
        var backward = new MinHashSketch<int, Int32Murmur3Hasher>();
        for (int i = 4_999; i >= 0; i--)
            backward.Add(i);

        Assert.Equal(1d, forward.EstimateJaccard(backward));
    }

    [Fact]
    public void DisjointSets_EstimateZero()
    {
        // No element is shared, so no register minimum can match except by a 60-bit value
        // collision between distinct hashes.
        Assert.Equal(0d, Range(0, 10_000).EstimateJaccard(Range(1_000_000, 10_000)));
    }

    [Fact]
    public void EstimateJaccard_IsSymmetric()
    {
        var a = Range(0, 8_000);
        var b = Range(3_000, 8_000);
        Assert.Equal(a.EstimateJaccard(b), b.EstimateJaccard(a));
    }

    [Fact]
    public void SelfSimilarity_IsOne_InEveryState()
    {
        var sketch = new MinHashSketch<int, Int32Murmur3Hasher>();
        Assert.Equal(1d, sketch.EstimateJaccard(sketch));

        sketch.Add(7);
        Assert.Equal(1d, sketch.EstimateJaccard(sketch));

        for (int i = 0; i < 1_000; i++)
            sketch.Add(i);
        Assert.Equal(1d, sketch.EstimateJaccard(sketch));
    }

    [Fact]
    public void TwoEmptySketches_EstimateOne()
    {
        var a = new MinHashSketch<int, Int32Murmur3Hasher>();
        var b = new MinHashSketch<int, Int32Murmur3Hasher>();
        Assert.Equal(1d, a.EstimateJaccard(b));
    }

    [Fact]
    public void EmptyAgainstNonEmpty_EstimatesZero()
    {
        var empty = new MinHashSketch<int, Int32Murmur3Hasher>();
        var full = Range(0, 100);
        Assert.Equal(0d, empty.EstimateJaccard(full));
        Assert.Equal(0d, full.EstimateJaccard(empty));
    }

    [Fact]
    public void HalfOverlap_EstimatesWithinErrorBound()
    {
        // [0, 20000) and [10000, 30000) share 10000 of 30000 distinct elements: J = 1/3.
        var a = Range(0, 20_000);
        var b = Range(10_000, 20_000);
        double expected = 1d / 3d;
        double sigma = Math.Sqrt(expected * (1 - expected) / a.RegisterCount);

        Assert.InRange(a.EstimateJaccard(b), expected - 4 * sigma, expected + 4 * sigma);
    }

    [Fact]
    public void Subset_EstimatesTheSizeRatio()
    {
        // A ⊂ B with |A| = |B| / 4, so J = |A| / |B| = 0.25.
        var a = Range(0, 5_000);
        var b = Range(0, 20_000);
        double sigma = Math.Sqrt(0.25 * 0.75 / a.RegisterCount);

        Assert.InRange(a.EstimateJaccard(b), 0.25 - 4 * sigma, 0.25 + 4 * sigma);
    }

    [Fact]
    public void DuplicateAdds_DoNotChangeTheSketch()
    {
        var once = Range(0, 1_000);
        var thrice = new MinHashSketch<int, Int32Murmur3Hasher>();
        for (int round = 0; round < 3; round++)
        {
            for (int i = 0; i < 1_000; i++)
                thrice.Add(i);
        }

        Assert.Equal(1d, once.EstimateJaccard(thrice));
    }

    [Fact]
    public void SmallSets_UseOnlyTheFilledRegisters()
    {
        // Two single-element sets fill one register each. When the elements are equal the
        // one informative register matches; with 256 registers and nothing else filled, an
        // estimator that counted the empty registers in its denominator would report 1/256.
        var a = new MinHashSketch<int, Int32Murmur3Hasher>();
        var b = new MinHashSketch<int, Int32Murmur3Hasher>();
        a.Add(5);
        b.Add(5);
        Assert.Equal(1d, a.EstimateJaccard(b));

        // {5} against {5, 6}: J = 1/2. Whether 6 lands in 5's register or another, the
        // sketch can only answer 0, 1/2 or 1 — never a fraction of the 256 registers.
        b.Add(6);
        double estimate = a.EstimateJaccard(b);
        Assert.Contains(estimate, new[] { 0d, 0.5d, 1d });
    }

    [Fact]
    public void ZeroIntElement_IsAnOrdinaryElement()
    {
        // No empty-slot sentinel among the elements, so default(int) needs no special case.
        var a = new MinHashSketch<int, Int32Murmur3Hasher>();
        var b = new MinHashSketch<int, Int32Murmur3Hasher>();
        a.Add(0);
        Assert.False(a.IsEmpty);
        b.Add(0);
        Assert.Equal(1d, a.EstimateJaccard(b));
    }

    [Fact]
    public void GuidEmpty_IsAnOrdinaryElement()
    {
        var a = new MinHashSketch<Guid, GuidHasher>();
        var b = new MinHashSketch<Guid, GuidHasher>();
        a.Add(Guid.Empty);
        b.Add(Guid.Empty);
        Assert.False(a.IsEmpty);
        Assert.Equal(1d, a.EstimateJaccard(b));
    }

    [Fact]
    public void NullReference_IsAddable_WithoutInvokingTheHasher()
    {
        // StringFnV1AHasher throws on null; the sketch maps null to a fixed hash instead.
        var a = new MinHashSketch<string, StringFnV1AHasher>();
        var b = new MinHashSketch<string, StringFnV1AHasher>();
        a.Add(null!);
        a.Add(null!);
        b.Add(null!);

        Assert.False(a.IsEmpty);
        Assert.Equal(1d, a.EstimateJaccard(b));
    }

    [Fact]
    public void StringElements_AreCompared()
    {
        var a = new MinHashSketch<string, StringMurmur3Hasher>(new[] { "alice", "bob" });
        var b = new MinHashSketch<string, StringMurmur3Hasher>(new[] { "bob", "alice", "alice" });
        var c = new MinHashSketch<string, StringMurmur3Hasher>(new[] { "carol", "dave" });

        Assert.Equal(1d, a.EstimateJaccard(b));
        Assert.Equal(0d, a.EstimateJaccard(c));
    }

    [Fact]
    public void EmptyString_IsAnOrdinaryElement()
    {
        var sketch = new MinHashSketch<string, StringFnV1AHasher>();
        sketch.Add("");
        Assert.False(sketch.IsEmpty);
    }

    [Fact]
    public void Clear_ResetsToEmpty()
    {
        var sketch = Range(0, 5_000);
        Assert.False(sketch.IsEmpty);

        sketch.Clear();
        Assert.True(sketch.IsEmpty);
        Assert.Equal(0d, sketch.EstimateJaccard(Range(0, 5_000)));

        // Reusable after clear, and indistinguishable from a fresh sketch of the same input.
        for (int i = 0; i < 5_000; i++)
            sketch.Add(i);
        Assert.Equal(1d, sketch.EstimateJaccard(Range(0, 5_000)));
    }

    [Fact]
    public void Clear_OnEmptySketch_IsANoOp()
    {
        var sketch = new MinHashSketch<int, Int32Murmur3Hasher>();
        sketch.Clear();
        Assert.True(sketch.IsEmpty);
        Assert.Equal(MinHashSketch<int, Int32Murmur3Hasher>.DefaultPrecision, sketch.Precision);
    }

    [Theory]
    [InlineData(4, 16)]
    [InlineData(8, 256)]
    [InlineData(12, 4096)]
    [InlineData(16, 65536)]
    public void Precision_DeterminesRegisterCount(int precision, int expectedRegisters)
    {
        var sketch = new MinHashSketch<int, Int32Murmur3Hasher>(precision);
        Assert.Equal(precision, sketch.Precision);
        Assert.Equal(expectedRegisters, sketch.RegisterCount);
    }

    [Fact]
    public void DefaultPrecision_IsEight()
    {
        var sketch = new MinHashSketch<int, Int32Murmur3Hasher>();
        Assert.Equal(8, sketch.Precision);
        Assert.Equal(256, sketch.RegisterCount);
    }

    [Fact]
    public void StandardError_IsHalfOverRootM_AndShrinksWithPrecision()
    {
        var low = new MinHashSketch<int, Int32Murmur3Hasher>(4);
        var high = new MinHashSketch<int, Int32Murmur3Hasher>(12);

        Assert.Equal(0.5 / 4d, low.StandardError, 12);
        Assert.Equal(0.5 / 64d, high.StandardError, 12);
        Assert.True(high.StandardError < low.StandardError);
    }

    [Fact]
    public void HigherPrecision_GivesTighterEstimate()
    {
        // Same J = 1/3 workload at the two ends of the precision range; the high-precision
        // estimate must sit inside a band the low-precision sketch cannot be held to.
        var lowA = Range(0, 40_000, 4);
        var lowB = Range(20_000, 40_000, 4);
        var highA = Range(0, 40_000, 14);
        var highB = Range(20_000, 40_000, 14);

        double expected = 1d / 3d;
        double low = lowA.EstimateJaccard(lowB);
        double high = highA.EstimateJaccard(highB);

        Assert.InRange(low, 0d, 1d);
        Assert.InRange(high, expected - 0.02, expected + 0.02);
    }

    [Fact]
    public void IEnumerableConstructor_MatchesSequentialAdds()
    {
        var built = new MinHashSketch<int, Int32Murmur3Hasher>(Enumerable.Range(0, 3_000));
        Assert.Equal(1d, built.EstimateJaccard(Range(0, 3_000)));
    }

    [Fact]
    public void IEnumerableConstructor_NonCollectionSource()
    {
        static IEnumerable<int> Yield()
        {
            for (int i = 0; i < 500; i++)
                yield return i;
        }

        var built = new MinHashSketch<int, Int32Murmur3Hasher>(Yield());
        Assert.Equal(1d, built.EstimateJaccard(Range(0, 500)));
    }

    [Fact]
    public void IEnumerableConstructor_EmptySource_BuildsUsableSketch()
    {
        var sketch = new MinHashSketch<int, Int32Murmur3Hasher>(Array.Empty<int>());
        Assert.True(sketch.IsEmpty);
        sketch.Add(1);
        Assert.False(sketch.IsEmpty);
    }

    [Fact]
    public void IEnumerableConstructor_RespectsPrecision()
    {
        var sketch = new MinHashSketch<int, Int32Murmur3Hasher>(new[] { 1, 2, 3 }, 10);
        Assert.Equal(10, sketch.Precision);
        Assert.Equal(1024, sketch.RegisterCount);
    }

    [Fact]
    public void IEnumerableConstructor_NullSource_Throws()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new MinHashSketch<int, Int32Murmur3Hasher>((IEnumerable<int>)null!));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void IEnumerableConstructor_NullSource_BeatsBadPrecision()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new MinHashSketch<int, Int32Murmur3Hasher>((IEnumerable<int>)null!, 99));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void Constructor_Throws_WhenPrecisionTooLow()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new MinHashSketch<int, Int32Murmur3Hasher>(MinHashSketch<int, Int32Murmur3Hasher>.MinPrecision - 1));
        Assert.Equal("precision", ex.ParamName);
    }

    [Fact]
    public void Constructor_Throws_WhenPrecisionTooHigh()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new MinHashSketch<int, Int32Murmur3Hasher>(MinHashSketch<int, Int32Murmur3Hasher>.MaxPrecision + 1));
        Assert.Equal("precision", ex.ParamName);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(16)]
    public void Constructor_Accepts_BoundaryPrecisions(int precision)
    {
        var sketch = new MinHashSketch<int, Int32Murmur3Hasher>(precision);
        sketch.Add(1);
        Assert.False(sketch.IsEmpty);
    }

    [Fact]
    public void UnionWith_EqualsASketchOfTheUnion()
    {
        // The per-register minimum is exactly what the combined stream would produce, so the
        // merged sketch is register-for-register the sketch of the union.
        var a = Range(0, 6_000);
        var b = Range(4_000, 6_000);
        a.UnionWith(b);

        Assert.Equal(1d, a.EstimateJaccard(Range(0, 10_000)));
    }

    [Fact]
    public void UnionWith_LeavesOtherUnmodified()
    {
        var a = Range(0, 1_000);
        var b = Range(50_000, 1_000);
        a.UnionWith(b);

        Assert.Equal(1d, b.EstimateJaccard(Range(50_000, 1_000)));
    }

    [Fact]
    public void UnionWith_EmptyOther_IsANoOp()
    {
        var a = Range(0, 1_000);
        a.UnionWith(new MinHashSketch<int, Int32Murmur3Hasher>());
        Assert.Equal(1d, a.EstimateJaccard(Range(0, 1_000)));
    }

    [Fact]
    public void UnionWith_Self_IsANoOp()
    {
        var a = Range(0, 1_000);
        a.UnionWith(a);
        Assert.Equal(1d, a.EstimateJaccard(Range(0, 1_000)));
    }

    [Fact]
    public void UnionWith_Null_Throws()
    {
        var sketch = new MinHashSketch<int, Int32Murmur3Hasher>();
        var ex = Assert.Throws<ArgumentNullException>(() => sketch.UnionWith(null!));
        Assert.Equal("other", ex.ParamName);
    }

    [Fact]
    public void UnionWith_IncompatiblePrecision_Throws()
    {
        var a = new MinHashSketch<int, Int32Murmur3Hasher>(8);
        var b = new MinHashSketch<int, Int32Murmur3Hasher>(10);
        var ex = Assert.Throws<ArgumentException>(() => a.UnionWith(b));
        Assert.Equal("other", ex.ParamName);
    }

    [Fact]
    public void EstimateJaccard_Null_Throws()
    {
        var sketch = new MinHashSketch<int, Int32Murmur3Hasher>();
        var ex = Assert.Throws<ArgumentNullException>(() => sketch.EstimateJaccard(null!));
        Assert.Equal("other", ex.ParamName);
    }

    [Fact]
    public void EstimateJaccard_IncompatiblePrecision_Throws()
    {
        var a = new MinHashSketch<int, Int32Murmur3Hasher>(8);
        var b = new MinHashSketch<int, Int32Murmur3Hasher>(6);
        var ex = Assert.Throws<ArgumentException>(() => a.EstimateJaccard(b));
        Assert.Equal("other", ex.ParamName);
    }
}
