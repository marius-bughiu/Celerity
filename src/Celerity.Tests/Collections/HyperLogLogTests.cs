using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

public class HyperLogLogTests
{
    [Fact]
    public void EmptyEstimator_EstimatesZero()
    {
        var hll = new HyperLogLog<int, Int32WangNaiveHasher>();
        Assert.Equal(0, hll.EstimateCardinality());
    }

    [Fact]
    public void SingleElement_EstimatesOne()
    {
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>();
        hll.Add(42);
        // With only one element, linear counting is exact for a single non-empty register.
        Assert.Equal(1, hll.EstimateCardinality());
    }

    [Fact]
    public void DuplicateAdds_DoNotInflateEstimate()
    {
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>();
        for (int i = 0; i < 1000; i++)
            hll.Add(7); // the same element a thousand times

        // A register only ever increases, so duplicates collapse to a single distinct value.
        Assert.Equal(1, hll.EstimateCardinality());
    }

    [Fact]
    public void DistinctElements_EstimateWithinErrorBound()
    {
        const int n = 100_000;
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>(); // precision 14
        for (int i = 0; i < n; i++)
            hll.Add(i);

        long estimate = hll.EstimateCardinality();
        double relativeError = Math.Abs(estimate - n) / (double)n;

        // Allow a generous 3x the standard error plus a small slack; the estimate is
        // deterministic for this fixed input so the bound is stable.
        double bound = hll.StandardError * 3 + 0.01;
        Assert.True(relativeError <= bound,
            $"estimate {estimate} for {n} distinct had relative error {relativeError:F4} > {bound:F4}");
    }

    [Fact]
    public void SmallCardinality_UsesLinearCountingAccurately()
    {
        // In the small-range regime the estimate should be very close to exact.
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>();
        for (int i = 0; i < 200; i++)
            hll.Add(i * 31 + 1);

        long estimate = hll.EstimateCardinality();
        Assert.InRange(estimate, 190, 210);
    }

    [Fact]
    public void ZeroIntElement_IsAnOrdinaryElement()
    {
        // Unlike the hash-table collections, HyperLogLog has no empty-slot sentinel, so
        // default(int) == 0 needs no out-of-band handling.
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>();
        Assert.Equal(0, hll.EstimateCardinality());
        hll.Add(0);
        Assert.Equal(1, hll.EstimateCardinality());
    }

    [Fact]
    public void GuidEmpty_IsAnOrdinaryElement()
    {
        var hll = new HyperLogLog<Guid, GuidHasher>();
        hll.Add(Guid.Empty);
        Assert.Equal(1, hll.EstimateCardinality());
    }

    [Fact]
    public void NullReference_IsAddable_WithoutInvokingTheHasher()
    {
        // StringFnV1AHasher throws on null; HyperLogLog maps a null reference to a fixed
        // base hash so it never calls the hasher with null.
        var hll = new HyperLogLog<string, StringFnV1AHasher>();
        hll.Add(null!);
        hll.Add(null!); // stable: still one distinct value
        Assert.Equal(1, hll.EstimateCardinality());
    }

    [Fact]
    public void StringElements_AreCounted()
    {
        var hll = new HyperLogLog<string, StringMurmur3Hasher>();
        hll.Add("alice");
        hll.Add("bob");
        hll.Add("alice"); // duplicate
        Assert.Equal(2, hll.EstimateCardinality());
    }

    [Fact]
    public void EmptyString_IsAnOrdinaryElement()
    {
        var hll = new HyperLogLog<string, StringFnV1AHasher>();
        hll.Add("");
        Assert.Equal(1, hll.EstimateCardinality());
    }

    [Fact]
    public void Clear_ResetsToEmpty()
    {
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>();
        for (int i = 0; i < 5000; i++)
            hll.Add(i);
        Assert.True(hll.EstimateCardinality() > 0);

        hll.Clear();
        Assert.Equal(0, hll.EstimateCardinality());

        // Reusable after clear.
        hll.Add(1);
        Assert.Equal(1, hll.EstimateCardinality());
    }

    [Fact]
    public void Clear_OnEmptyEstimator_IsANoOp()
    {
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>();
        hll.Clear();
        Assert.Equal(0, hll.EstimateCardinality());
    }

    // ---------------------------------------------------------------
    //  Sizing / properties
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(4, 16)]
    [InlineData(10, 1024)]
    [InlineData(14, 16384)]
    [InlineData(16, 65536)]
    public void Precision_DeterminesRegisterCount(int precision, int expectedRegisters)
    {
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>(precision);
        Assert.Equal(precision, hll.Precision);
        Assert.Equal(expectedRegisters, hll.RegisterCount);
    }

    [Fact]
    public void StandardError_ShrinksWithHigherPrecision()
    {
        var coarse = new HyperLogLog<int, Int32Murmur3Hasher>(8);
        var fine = new HyperLogLog<int, Int32Murmur3Hasher>(14);
        Assert.True(fine.StandardError < coarse.StandardError);
        // 1.04 / sqrt(16384) ≈ 0.00813.
        Assert.InRange(fine.StandardError, 0.008, 0.0082);
    }

    [Fact]
    public void HigherPrecision_GivesTighterEstimate()
    {
        const int n = 50_000;
        long Estimate(int precision)
        {
            var hll = new HyperLogLog<int, Int32Murmur3Hasher>(precision);
            for (int i = 0; i < n; i++)
                hll.Add(i);
            return hll.EstimateCardinality();
        }

        double coarseError = Math.Abs(Estimate(8) - n) / (double)n;
        double fineError = Math.Abs(Estimate(16) - n) / (double)n;
        Assert.True(fineError <= coarseError + 0.001,
            $"p=16 error {fineError:F4} should not exceed p=8 error {coarseError:F4}");
    }

    // ---------------------------------------------------------------
    //  IEnumerable constructor
    // ---------------------------------------------------------------

    [Fact]
    public void IEnumerableConstructor_CountsDistinctElements()
    {
        var source = Enumerable.Range(0, 10_000).ToList();
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>(source);

        long estimate = hll.EstimateCardinality();
        double relativeError = Math.Abs(estimate - 10_000) / 10_000.0;
        Assert.True(relativeError <= hll.StandardError * 3 + 0.01,
            $"estimate {estimate} had relative error {relativeError:F4}");
    }

    [Fact]
    public void IEnumerableConstructor_HonorsDuplicates()
    {
        var source = new[] { 1, 1, 2, 2, 3, 3 };
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>(source);
        Assert.Equal(3, hll.EstimateCardinality());
    }

    [Fact]
    public void IEnumerableConstructor_NonCollectionSource()
    {
        IEnumerable<int> source = Enumerable.Range(0, 2000).Where(i => true);
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>(source);
        long estimate = hll.EstimateCardinality();
        Assert.InRange(estimate, 1800, 2200);
    }

    [Fact]
    public void IEnumerableConstructor_EmptySource_BuildsUsableEstimator()
    {
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>(Array.Empty<int>());
        Assert.Equal(0, hll.EstimateCardinality());
        hll.Add(1);
        Assert.Equal(1, hll.EstimateCardinality());
    }

    [Fact]
    public void IEnumerableConstructor_RespectsPrecision()
    {
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>(new[] { 1, 2, 3 }, 10);
        Assert.Equal(10, hll.Precision);
        Assert.Equal(1024, hll.RegisterCount);
    }

    [Fact]
    public void IEnumerableConstructor_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new HyperLogLog<int, Int32Murmur3Hasher>((IEnumerable<int>)null!));
    }

    // Null source must beat an out-of-range precision.
    [Fact]
    public void IEnumerableConstructor_NullSource_BeatsBadPrecision()
    {
        Assert.Throws<ArgumentNullException>(
            () => new HyperLogLog<int, Int32Murmur3Hasher>((IEnumerable<int>)null!, 99));
    }

    // ---------------------------------------------------------------
    //  Constructor validation
    // ---------------------------------------------------------------

    [Fact]
    public void Constructor_Throws_WhenPrecisionTooLow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new HyperLogLog<int, Int32Murmur3Hasher>(3));
    }

    [Fact]
    public void Constructor_Throws_WhenPrecisionTooHigh()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new HyperLogLog<int, Int32Murmur3Hasher>(17));
    }

    [Theory]
    [InlineData(HyperLogLog<int, Int32Murmur3Hasher>.MIN_PRECISION)]
    [InlineData(HyperLogLog<int, Int32Murmur3Hasher>.MAX_PRECISION)]
    public void Constructor_Accepts_BoundaryPrecisions(int precision)
    {
        var hll = new HyperLogLog<int, Int32Murmur3Hasher>(precision);
        Assert.Equal(precision, hll.Precision);
    }

    // ---------------------------------------------------------------
    //  UnionWith
    // ---------------------------------------------------------------

    [Fact]
    public void UnionWith_EstimatesUnionCardinality()
    {
        var a = new HyperLogLog<int, Int32Murmur3Hasher>();
        var b = new HyperLogLog<int, Int32Murmur3Hasher>();

        // Disjoint streams of 20,000 each → union of 40,000 distinct.
        for (int i = 0; i < 20_000; i++) a.Add(i);
        for (int i = 20_000; i < 40_000; i++) b.Add(i);

        a.UnionWith(b);

        long estimate = a.EstimateCardinality();
        double relativeError = Math.Abs(estimate - 40_000) / 40_000.0;
        Assert.True(relativeError <= a.StandardError * 3 + 0.01,
            $"union estimate {estimate} had relative error {relativeError:F4}");
    }

    [Fact]
    public void UnionWith_OverlappingStreams_CountsDistinct()
    {
        var a = new HyperLogLog<int, Int32Murmur3Hasher>();
        var b = new HyperLogLog<int, Int32Murmur3Hasher>();

        // Fully overlapping streams → union cardinality is just the shared count.
        for (int i = 0; i < 10_000; i++) { a.Add(i); b.Add(i); }

        a.UnionWith(b);

        long estimate = a.EstimateCardinality();
        double relativeError = Math.Abs(estimate - 10_000) / 10_000.0;
        Assert.True(relativeError <= a.StandardError * 3 + 0.01,
            $"overlapping union estimate {estimate} had relative error {relativeError:F4}");
    }

    [Fact]
    public void UnionWith_LeavesOtherUnmodified()
    {
        var a = new HyperLogLog<int, Int32Murmur3Hasher>();
        var b = new HyperLogLog<int, Int32Murmur3Hasher>();
        for (int i = 0; i < 100; i++) a.Add(i);
        for (int i = 1000; i < 1100; i++) b.Add(i);

        a.UnionWith(b);

        Assert.Equal(100, b.EstimateCardinality());
    }

    [Fact]
    public void UnionWith_Null_Throws()
    {
        var a = new HyperLogLog<int, Int32Murmur3Hasher>();
        Assert.Throws<ArgumentNullException>(() => a.UnionWith(null!));
    }

    [Fact]
    public void UnionWith_IncompatiblePrecision_Throws()
    {
        var a = new HyperLogLog<int, Int32Murmur3Hasher>(10);
        var b = new HyperLogLog<int, Int32Murmur3Hasher>(12);
        Assert.Throws<ArgumentException>(() => a.UnionWith(b));
    }
}
