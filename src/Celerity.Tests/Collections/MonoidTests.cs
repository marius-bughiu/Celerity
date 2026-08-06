using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Coverage for the five folds shipped with <see cref="SegmentTree{T, TMonoid}"/>. Each one is checked against
/// the two monoid laws the tree relies on — associativity and a two-sided identity — because a fold that
/// breaks either produces a wrong range aggregate that no test of the tree's own walk would attribute to the
/// monoid. The documented floating-point caveat on <see cref="MinMonoid{T}"/> / <see cref="MaxMonoid{T}"/> is
/// pinned here too, so it reads as a stated limit rather than a latent bug.
/// </summary>
public class MonoidTests
{
    [Fact]
    public void SumMonoid_ShouldAddAndCarryZeroAsIdentity()
    {
        var monoid = default(SumMonoid<int>);

        Assert.Equal(0, monoid.Identity);
        Assert.Equal(7, monoid.Combine(3, 4));
        Assert.Equal(5, monoid.Combine(monoid.Identity, 5));
        Assert.Equal(5, monoid.Combine(5, monoid.Identity));
        Assert.Equal(monoid.Combine(monoid.Combine(1, 2), 3), monoid.Combine(1, monoid.Combine(2, 3)));
    }

    [Fact]
    public void MinMonoid_ShouldKeepTheSmallerValue_AndCarryMaxValueAsIdentity()
    {
        var monoid = default(MinMonoid<int>);

        Assert.Equal(int.MaxValue, monoid.Identity);
        Assert.Equal(3, monoid.Combine(3, 4));     // left is smaller
        Assert.Equal(4, monoid.Combine(9, 4));     // right is smaller
        Assert.Equal(4, monoid.Combine(4, 4));     // equal: either operand is a correct answer
        Assert.Equal(5, monoid.Combine(monoid.Identity, 5));
        Assert.Equal(5, monoid.Combine(5, monoid.Identity));
        Assert.Equal(monoid.Combine(monoid.Combine(8, 2), 5), monoid.Combine(8, monoid.Combine(2, 5)));
    }

    [Fact]
    public void MaxMonoid_ShouldKeepTheLargerValue_AndCarryMinValueAsIdentity()
    {
        var monoid = default(MaxMonoid<int>);

        Assert.Equal(int.MinValue, monoid.Identity);
        Assert.Equal(4, monoid.Combine(3, 4));     // right is larger
        Assert.Equal(9, monoid.Combine(9, 4));     // left is larger
        Assert.Equal(4, monoid.Combine(4, 4));
        Assert.Equal(5, monoid.Combine(monoid.Identity, 5));
        Assert.Equal(5, monoid.Combine(5, monoid.Identity));
        Assert.Equal(monoid.Combine(monoid.Combine(8, 2), 5), monoid.Combine(8, monoid.Combine(2, 5)));
    }

    [Fact]
    public void BitwiseAndMonoid_ShouldIntersectBits_AndCarryAllOnesAsIdentity()
    {
        var monoid = default(BitwiseAndMonoid<int>);

        Assert.Equal(-1, monoid.Identity);         // all ones in two's complement
        Assert.Equal(0b0100, monoid.Combine(0b1100, 0b0110));
        Assert.Equal(0b1010, monoid.Combine(monoid.Identity, 0b1010));
        Assert.Equal(0b1010, monoid.Combine(0b1010, monoid.Identity));
        Assert.Equal(monoid.Combine(monoid.Combine(0b1110, 0b0111), 0b1101),
                     monoid.Combine(0b1110, monoid.Combine(0b0111, 0b1101)));
    }

    [Fact]
    public void BitwiseOrMonoid_ShouldUnionBits_AndCarryZeroAsIdentity()
    {
        var monoid = default(BitwiseOrMonoid<int>);

        Assert.Equal(0, monoid.Identity);
        Assert.Equal(0b1110, monoid.Combine(0b1100, 0b0110));
        Assert.Equal(0b1010, monoid.Combine(monoid.Identity, 0b1010));
        Assert.Equal(0b1010, monoid.Combine(0b1010, monoid.Identity));
        Assert.Equal(monoid.Combine(monoid.Combine(0b1000, 0b0100), 0b0010),
                     monoid.Combine(0b1000, monoid.Combine(0b0100, 0b0010)));
    }

    [Fact]
    public void BitwiseAndMonoid_ShouldWorkForUnsignedTypes()
    {
        var monoid = default(BitwiseAndMonoid<uint>);

        Assert.Equal(uint.MaxValue, monoid.Identity);
        Assert.Equal(0b0100u, monoid.Combine(0b1100u, 0b0110u));
    }

    [Fact]
    public void MinMonoid_ShouldSaturateAtMaxValue_ForFloatingPointInfinity()
    {
        // The documented caveat: IMinMaxValue<double>.MaxValue is the largest finite double, not +infinity,
        // so a stored +infinity aggregates to double.MaxValue. Callers who need IEEE-exact semantics are
        // pointed at a custom monoid.
        var tree = new SegmentTree<double, MinMonoid<double>>(new[] { double.PositiveInfinity });

        Assert.Equal(double.MaxValue, tree.Query(0, 1));
        Assert.Equal(double.PositiveInfinity, tree[0]);   // the stored value itself is untouched
    }

    [Fact]
    public void MinMonoid_ShouldResolveNaNByOperandPosition_BecauseEveryComparisonIsFalse()
    {
        // The second half of the caveat: NaN loses every `<` comparison, so the right operand always survives.
        // That makes the aggregate of a range containing a NaN depend on where the NaN sits, which is the
        // stated reason a caller needing IEEE semantics has to supply their own monoid.
        var monoid = default(MinMonoid<double>);

        Assert.Equal(1.0, monoid.Combine(double.NaN, 1.0));
        Assert.True(double.IsNaN(monoid.Combine(1.0, double.NaN)));
    }

    [Fact]
    public void MaxMonoid_ShouldResolveNaNByOperandPosition_BecauseEveryComparisonIsFalse()
    {
        var monoid = default(MaxMonoid<double>);

        Assert.Equal(1.0, monoid.Combine(double.NaN, 1.0));
        Assert.True(double.IsNaN(monoid.Combine(1.0, double.NaN)));
    }

    // ---- The monoids drive a tree end to end ------------------------------------------------------

    [Fact]
    public void SegmentTree_ShouldAnswerRangeMaximum()
    {
        var tree = new SegmentTree<long, MaxMonoid<long>>(new long[] { 5, 3, 8, 1, 9, 2 });

        Assert.Equal(8L, tree.Query(0, 3));
        Assert.Equal(9L, tree.Aggregate);

        tree[4] = -4;
        Assert.Equal(8L, tree.Aggregate);
    }

    [Fact]
    public void SegmentTree_ShouldAnswerRangeBitwiseAnd()
    {
        // The capability-mask shape: which flags does every entry in the window still hold?
        var tree = new SegmentTree<int, BitwiseAndMonoid<int>>(new[] { 0b1111, 0b1110, 0b1100, 0b0101 });

        Assert.Equal(0b1100, tree.Query(0, 3));
        Assert.Equal(0b0100, tree.Aggregate);
    }

    [Fact]
    public void SegmentTree_ShouldAnswerRangeBitwiseOr()
    {
        var tree = new SegmentTree<int, BitwiseOrMonoid<int>>(new[] { 0b0001, 0b0010, 0b0100, 0b1000 });

        Assert.Equal(0b0011, tree.Query(0, 2));
        Assert.Equal(0b1111, tree.Aggregate);
    }

    [Fact]
    public void SegmentTree_ShouldAnswerRangeGcd_UnderTheDocumentedUserWrittenMonoid()
    {
        // GcdMonoid is the "write your own fold" example printed in IMonoid's docs, the API reference and the
        // README. Pinning it here means the sample a reader copies is one that has been run.
        uint[] values = { 12, 18, 24, 9, 27 };
        var tree = new SegmentTree<uint, GcdMonoid>(values);

        Assert.Equal(6u, tree.Query(0, 3));    // gcd(12, 18, 24)
        Assert.Equal(9u, tree.Query(3, 5));    // gcd(9, 27)
        Assert.Equal(3u, tree.Aggregate);
        Assert.Equal(0u, tree.Query(2, 2));    // the identity, and gcd(0, a) == a

        tree[0] = 5;
        Assert.Equal(1u, tree.Aggregate);
    }

    [Fact]
    public void SegmentTree_ShouldAgreeWithFenwickTree_OnRangeSums()
    {
        // The two range structures overlap on exactly one fold — addition, the only one Fenwick can do — so
        // the sum monoid is the place their answers can be reconciled directly.
        long[] values = { 3, -1, 4, 1, -5, 9, 2, 6, -3 };
        var segment = new SegmentTree<long, SumMonoid<long>>(values);
        var fenwick = new FenwickTree<long>(values);

        for (int start = 0; start <= values.Length; start++)
            for (int end = start; end <= values.Length; end++)
                Assert.Equal(fenwick.RangeSum(start, end), segment.Query(start, end));

        Assert.Equal(fenwick.Total, segment.Aggregate);
    }
}
