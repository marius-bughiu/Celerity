using Celerity.Collections;
using Celerity.Hashing;
using static Celerity.Tests.Collections.ThetaSketchTests;

namespace Celerity.Tests.Collections;

/// <summary>
/// Tests for the theta-sketch set expressions on <see cref="CompactThetaSketch{T,THasher}"/>:
/// exact answers while the operands are exact, the common-threshold rule once they sample,
/// the empty-operand special cases, and composition.
/// </summary>
public class CompactThetaSketchTests
{
    private const double FullThreshold = 9223372036854775808d; // 2^63

    private static CompactThetaSketch<int, Int32Murmur3Hasher> Exact(int start, int count) =>
        new ThetaSketch<int, Int32Murmur3Hasher>(Enumerable.Range(start, count)).Compact();

    // A compact sketch over the given sample values, built through IdentityHasher64 so the
    // test controls the values exactly. At precision 4 more than 24 values push it into
    // estimation mode.
    private static CompactThetaSketch<ulong, IdentityHasher64> Values(int precision, params ulong[] values)
    {
        var sketch = new ThetaSketch<ulong, IdentityHasher64>(precision);
        foreach (ulong v in values)
            sketch.Add(KeyFor(v));
        return sketch.Compact();
    }

    private static ulong[] Range(ulong start, ulong count, ulong step = 1)
    {
        var values = new ulong[count];
        for (ulong i = 0; i < count; i++)
            values[i] = start + i * step;
        return values;
    }

    // ── Argument validation ─────────────────────────────────────────────────

    [Fact]
    public void Operations_RejectNullOperands()
    {
        CompactThetaSketch<int, Int32Murmur3Hasher> s = Exact(0, 3);
        Assert.Throws<ArgumentNullException>(() => CompactThetaSketch<int, Int32Murmur3Hasher>.Union(null!, s));
        Assert.Throws<ArgumentNullException>(() => CompactThetaSketch<int, Int32Murmur3Hasher>.Union(s, null!));
        Assert.Throws<ArgumentNullException>(() => CompactThetaSketch<int, Int32Murmur3Hasher>.Intersect(null!, s));
        Assert.Throws<ArgumentNullException>(() => CompactThetaSketch<int, Int32Murmur3Hasher>.Intersect(s, null!));
        Assert.Throws<ArgumentNullException>(() => CompactThetaSketch<int, Int32Murmur3Hasher>.Except(null!, s));
        Assert.Throws<ArgumentNullException>(() => CompactThetaSketch<int, Int32Murmur3Hasher>.Except(s, null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Bounds_RejectAnUnsupportedNumberOfStandardDeviations(int standardDeviations)
    {
        CompactThetaSketch<int, Int32Murmur3Hasher> s = Exact(0, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => s.GetLowerBound(standardDeviations));
        Assert.Throws<ArgumentOutOfRangeException>(() => s.GetUpperBound(standardDeviations));
    }

    // ── Exact operands give exact answers ───────────────────────────────────

    [Fact]
    public void ExactOperands_GiveExactUnionIntersectionAndDifference()
    {
        CompactThetaSketch<int, Int32Murmur3Hasher> a = Exact(0, 1_000);      // [0, 1000)
        CompactThetaSketch<int, Int32Murmur3Hasher> b = Exact(600, 1_000);    // [600, 1600)

        Assert.Equal(1_600d, CompactThetaSketch<int, Int32Murmur3Hasher>.Union(a, b).EstimateCardinality());
        Assert.Equal(400d, CompactThetaSketch<int, Int32Murmur3Hasher>.Intersect(a, b).EstimateCardinality());
        Assert.Equal(600d, CompactThetaSketch<int, Int32Murmur3Hasher>.Except(a, b).EstimateCardinality());
        Assert.Equal(600d, CompactThetaSketch<int, Int32Murmur3Hasher>.Except(b, a).EstimateCardinality());

        // Exact operands make exact results, whose bounds collapse onto the count.
        CompactThetaSketch<int, Int32Murmur3Hasher> both = CompactThetaSketch<int, Int32Murmur3Hasher>.Intersect(a, b);
        Assert.False(both.IsEstimationMode);
        Assert.Equal(400d, both.GetLowerBound(3));
        Assert.Equal(400d, both.GetUpperBound(3));
    }

    [Fact]
    public void Operations_AreSymmetricWhereTheSetOperationIs()
    {
        CompactThetaSketch<int, Int32Murmur3Hasher> a = new ThetaSketch<int, Int32Murmur3Hasher>(Enumerable.Range(0, 50_000), 8).Compact();
        CompactThetaSketch<int, Int32Murmur3Hasher> b = new ThetaSketch<int, Int32Murmur3Hasher>(Enumerable.Range(20_000, 50_000), 9).Compact();

        AssertSameValuesBelowTheCommonThreshold(
            CompactThetaSketch<int, Int32Murmur3Hasher>.Union(a, b), CompactThetaSketch<int, Int32Murmur3Hasher>.Union(b, a));
        AssertSameValuesBelowTheCommonThreshold(
            CompactThetaSketch<int, Int32Murmur3Hasher>.Intersect(a, b), CompactThetaSketch<int, Int32Murmur3Hasher>.Intersect(b, a));
        Assert.Equal(
            CompactThetaSketch<int, Int32Murmur3Hasher>.Union(a, b).Theta, Math.Min(a.Theta, b.Theta));
    }

    // ── The merge walks, value by value ─────────────────────────────────────

    [Fact]
    public void Union_InterleavesBothOperands_AndTheirTails()
    {
        // x: 1, 3, 5, 7, 9   y: 2, 3, 6, 10, 11, 12 — every merge arm and a tail on y's side.
        CompactThetaSketch<ulong, IdentityHasher64> x = Values(10, 1, 3, 5, 7, 9);
        CompactThetaSketch<ulong, IdentityHasher64> y = Values(10, 2, 3, 6, 10, 11, 12);

        CompactThetaSketch<ulong, IdentityHasher64> union = CompactThetaSketch<ulong, IdentityHasher64>.Union(x, y);
        Assert.Equal(10, union.RetainedCount);

        // A tail on x's side, and a union with no duplicates fills its buffer exactly.
        CompactThetaSketch<ulong, IdentityHasher64> disjoint = CompactThetaSketch<ulong, IdentityHasher64>.Union(Values(10, 20, 30), Values(10, 25));
        Assert.Equal(3, disjoint.RetainedCount);
        AssertSameValuesBelowTheCommonThreshold(Values(10, 20, 25, 30), disjoint);
    }

    [Fact]
    public void Intersect_SkipsTheValuesOnlyOneSideHolds()
    {
        CompactThetaSketch<ulong, IdentityHasher64> x = Values(10, 1, 3, 5, 7, 9);
        CompactThetaSketch<ulong, IdentityHasher64> y = Values(10, 2, 3, 6, 7, 11);

        CompactThetaSketch<ulong, IdentityHasher64> both = CompactThetaSketch<ulong, IdentityHasher64>.Intersect(x, y);
        AssertSameValuesBelowTheCommonThreshold(Values(10, 3, 7), both);

        // Identical operands: the result fills its buffer exactly.
        Assert.Equal(5, CompactThetaSketch<ulong, IdentityHasher64>.Intersect(x, x).RetainedCount);
    }

    [Fact]
    public void Except_RemovesSharedValues_AndKeepsTheTail()
    {
        CompactThetaSketch<ulong, IdentityHasher64> x = Values(10, 1, 3, 5, 7, 9);
        CompactThetaSketch<ulong, IdentityHasher64> y = Values(10, 2, 3, 6, 7);

        CompactThetaSketch<ulong, IdentityHasher64> onlyX = CompactThetaSketch<ulong, IdentityHasher64>.Except(x, y);
        AssertSameValuesBelowTheCommonThreshold(Values(10, 1, 5, 9), onlyX);

        // Nothing shared: the result is the whole of x, buffer filled exactly.
        Assert.Equal(5, CompactThetaSketch<ulong, IdentityHasher64>.Except(x, Values(10, 100)).RetainedCount);
    }

    // ── The common threshold ────────────────────────────────────────────────

    [Fact]
    public void Operations_RestrictBothOperandsToTheSmallerThreshold()
    {
        // a samples: values 1..25 at precision 4 keep 1..16 with threshold 16. b is exact and
        // holds 10..40, so only 10..16 of it lie at or below a's threshold.
        CompactThetaSketch<ulong, IdentityHasher64> a = Values(4, Range(1, 25));
        CompactThetaSketch<ulong, IdentityHasher64> b = Values(10, Range(10, 31));
        Assert.Equal(16d / FullThreshold, a.Theta);

        CompactThetaSketch<ulong, IdentityHasher64> union = CompactThetaSketch<ulong, IdentityHasher64>.Union(a, b);
        Assert.Equal(16, union.RetainedCount);
        Assert.Equal(a.Theta, union.Theta);

        CompactThetaSketch<ulong, IdentityHasher64> both = CompactThetaSketch<ulong, IdentityHasher64>.Intersect(a, b);
        Assert.Equal(7, both.RetainedCount); // 10..16; the threshold value 16 itself is kept

        Assert.Equal(9, CompactThetaSketch<ulong, IdentityHasher64>.Except(a, b).RetainedCount); // 1..9
        Assert.Equal(0, CompactThetaSketch<ulong, IdentityHasher64>.Except(b, a).RetainedCount);
    }

    [Fact]
    public void Operations_KeepAValueEqualToTheFullThreshold()
    {
        // The all-ones hash maps to 2^63, the largest sample value, which an exact sketch's
        // inclusive threshold must still admit.
        var sketch = new ThetaSketch<ulong, IdentityHasher64>();
        sketch.Add(ulong.MaxValue);
        sketch.Add(0);
        CompactThetaSketch<ulong, IdentityHasher64> compact = sketch.Compact();

        Assert.Equal(2, compact.RetainedCount);
        Assert.Equal(2, CompactThetaSketch<ulong, IdentityHasher64>.Intersect(compact, compact).RetainedCount);
    }

    // ── Empty operands ──────────────────────────────────────────────────────

    [Fact]
    public void AnExactEmptyOperand_MakesIntersectionAndDifferenceExactlyEmpty()
    {
        CompactThetaSketch<int, Int32Murmur3Hasher> empty = new ThetaSketch<int, Int32Murmur3Hasher>().Compact();
        CompactThetaSketch<int, Int32Murmur3Hasher> sampled = new ThetaSketch<int, Int32Murmur3Hasher>(Enumerable.Range(0, 100_000), 6).Compact();

        foreach (CompactThetaSketch<int, Int32Murmur3Hasher> result in new[]
        {
            CompactThetaSketch<int, Int32Murmur3Hasher>.Intersect(empty, sampled),
            CompactThetaSketch<int, Int32Murmur3Hasher>.Intersect(sampled, empty),
            CompactThetaSketch<int, Int32Murmur3Hasher>.Except(empty, sampled),
        })
        {
            Assert.True(result.IsEmpty);
            Assert.Equal(0d, result.GetUpperBound(3));
        }

        // Taking nothing away leaves the operand's sample unchanged.
        CompactThetaSketch<int, Int32Murmur3Hasher> rest = CompactThetaSketch<int, Int32Murmur3Hasher>.Except(sampled, empty);
        Assert.Equal(sampled.RetainedCount, rest.RetainedCount);
        Assert.Equal(sampled.Theta, rest.Theta);

        // A union with the empty set is the other operand.
        CompactThetaSketch<int, Int32Murmur3Hasher> union = CompactThetaSketch<int, Int32Murmur3Hasher>.Union(empty, sampled);
        AssertSameValuesBelowTheCommonThreshold(sampled, union);
        Assert.Equal(sampled.Theta, union.Theta);
    }

    [Fact]
    public void AnEstimationModeResultWithNoValues_IsNotEmpty_AndHasAPositiveUpperBound()
    {
        CompactThetaSketch<int, Int32Murmur3Hasher> a = new ThetaSketch<int, Int32Murmur3Hasher>(Enumerable.Range(0, 100_000), 8).Compact();
        CompactThetaSketch<int, Int32Murmur3Hasher> b = new ThetaSketch<int, Int32Murmur3Hasher>(Enumerable.Range(1_000_000, 100_000), 8).Compact();

        CompactThetaSketch<int, Int32Murmur3Hasher> none = CompactThetaSketch<int, Int32Murmur3Hasher>.Intersect(a, b);

        Assert.Equal(0, none.RetainedCount);
        Assert.False(none.IsEmpty);
        Assert.True(none.IsEstimationMode);
        Assert.Equal(0d, none.EstimateCardinality());
        Assert.Equal(0d, none.GetLowerBound(2));

        // The sample saw nothing at rate θ, so the set plausibly has up to about z²(1 − θ)/θ
        // elements — a few hundred here, a small share of the 200,000 the operands span.
        double upper = none.GetUpperBound(2);
        Assert.InRange(upper, 1d, 2_000d);
        Assert.True(none.GetUpperBound(3) > upper);
    }

    // ── Composition ─────────────────────────────────────────────────────────

    [Fact]
    public void Expressions_Compose()
    {
        // (A ∪ B) ∩ C with A = [0, 400), B = [300, 700), C = [500, 1500): the answer is
        // [500, 700), 200 elements, and every operand is exact.
        CompactThetaSketch<int, Int32Murmur3Hasher> a = Exact(0, 400), b = Exact(300, 400), c = Exact(500, 1_000);

        CompactThetaSketch<int, Int32Murmur3Hasher> result =
            CompactThetaSketch<int, Int32Murmur3Hasher>.Intersect(CompactThetaSketch<int, Int32Murmur3Hasher>.Union(a, b), c);

        Assert.Equal(200d, result.EstimateCardinality());
        // C \ (A ∪ B) = [700, 1500).
        Assert.Equal(800d, CompactThetaSketch<int, Int32Murmur3Hasher>.Except(c, CompactThetaSketch<int, Int32Murmur3Hasher>.Union(a, b)).EstimateCardinality());
    }

    [Fact]
    public void CompactSketches_AreSharedSafely_AcrossOperations()
    {
        // Operands are never modified by an operation, so one compact sketch can take part in
        // many expressions.
        CompactThetaSketch<int, Int32Murmur3Hasher> a = Exact(0, 100), b = Exact(50, 100);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(50d, CompactThetaSketch<int, Int32Murmur3Hasher>.Intersect(a, b).EstimateCardinality());
            Assert.Equal(100d, a.EstimateCardinality());
        }
    }
}
