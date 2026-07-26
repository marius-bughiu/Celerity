using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="BTreeSet{T, TComparer}"/> against
/// <see cref="SortedSet{T}"/> as the oracle. CsCheck generates randomized operation sequences, applies each
/// to both models, and asserts they agree on content <b>and on ordering</b> — the enumerated sequence is what
/// actually catches a bad split, borrow, or merge, since a subtly unbalanced tree can still answer membership
/// correctly for a while. Any failure shrinks to a minimal reproduction and prints the seed.
/// </summary>
public class BTreeSetDifferentialTests
{
    private enum Op { TryAdd, Remove, Clear }

    // The element domain is deliberately wider than a node (31 elements), so a few hundred operations build
    // two to three levels and drive splits, borrows, and merges densely.
    private static readonly Gen<(Op op, int value)> GenOp =
        Gen.Select(
            Gen.Int[0, 99].Select(n => n < 55 ? Op.TryAdd
                                     : n < 99 ? Op.Remove
                                     : Op.Clear),
            Gen.Int[-5, 300]);

    private static readonly Gen<List<(Op, int)>> GenOps = GenOp.List[0, 400];

    [Fact]
    public void BTreeSet_ShouldMatch_SortedSet()
    {
        GenOps.Sample(ops =>
        {
            var sut = new BTreeSet<int>();
            var oracle = new SortedSet<int>();

            foreach (var (op, value) in ops)
            {
                switch (op)
                {
                    case Op.TryAdd:
                        Assert.Equal(oracle.Add(value), sut.TryAdd(value));
                        break;
                    case Op.Remove:
                        Assert.Equal(oracle.Remove(value), sut.Remove(value));
                        break;
                    case Op.Clear:
                        sut.Clear();
                        oracle.Clear();
                        break;
                }
            }

            AssertEquivalent(sut, oracle);
        }, iter: 500);
    }

    [Fact]
    public void BTreeSet_ShouldMatch_SortedSet_OnTheOrderedSurface()
    {
        Gen.Int[-5, 200].List[0, 250].Sample(generated =>
        {
            var sut = new BTreeSet<int>(generated);
            var oracle = new SortedSet<int>(generated);

            Assert.Equal(oracle.Count, sut.Count);
            Assert.Equal(oracle, sut);

            if (oracle.Count == 0)
            {
                Assert.False(sut.TryGetMin(out _));
                Assert.False(sut.TryGetMax(out _));
                return;
            }

            Assert.Equal(oracle.Min, sut.Min);
            Assert.Equal(oracle.Max, sut.Max);

            for (int probe = -7; probe <= 202; probe++)
            {
                bool hasLower = oracle.Any(v => v >= probe);
                Assert.Equal(hasLower, sut.TryGetLowerBound(probe, out int lower));
                if (hasLower)
                    Assert.Equal(oracle.First(v => v >= probe), lower);

                bool hasUpper = oracle.Any(v => v > probe);
                Assert.Equal(hasUpper, sut.TryGetUpperBound(probe, out int upper));
                if (hasUpper)
                    Assert.Equal(oracle.First(v => v > probe), upper);

                Assert.Equal(
                    oracle.Where(v => v >= probe && v < probe + 25),
                    sut.EnumerateRange(probe, probe + 25));
            }
        }, iter: 100);
    }

    [Fact]
    public void BTreeSet_ShouldMatch_SortedSet_OnSetAlgebra()
    {
        Gen.Select(Gen.Int[-5, 60].List[0, 60], Gen.Int[-5, 60].List[0, 60]).Sample(pair =>
        {
            var (left, right) = pair;

            AssertAlgebra(left, right, (s, o) => s.UnionWith(o), (s, o) => s.UnionWith(o));
            AssertAlgebra(left, right, (s, o) => s.IntersectWith(o), (s, o) => s.IntersectWith(o));
            AssertAlgebra(left, right, (s, o) => s.ExceptWith(o), (s, o) => s.ExceptWith(o));
            AssertAlgebra(left, right, (s, o) => s.SymmetricExceptWith(o), (s, o) => s.SymmetricExceptWith(o));

            var sut = new BTreeSet<int>(left);
            var oracle = new SortedSet<int>(left);
            Assert.Equal(oracle.IsSubsetOf(right), sut.IsSubsetOf(right));
            Assert.Equal(oracle.IsProperSubsetOf(right), sut.IsProperSubsetOf(right));
            Assert.Equal(oracle.IsSupersetOf(right), sut.IsSupersetOf(right));
            Assert.Equal(oracle.IsProperSupersetOf(right), sut.IsProperSupersetOf(right));
            Assert.Equal(oracle.Overlaps(right), sut.Overlaps(right));
            Assert.Equal(oracle.SetEquals(right), sut.SetEquals(right));
        }, iter: 200);
    }

    private static void AssertAlgebra(
        List<int> left,
        List<int> right,
        Action<BTreeSet<int>, IEnumerable<int>> apply,
        Action<SortedSet<int>, IEnumerable<int>> applyOracle)
    {
        var sut = new BTreeSet<int>(left);
        var oracle = new SortedSet<int>(left);

        apply(sut, right);
        applyOracle(oracle, right);

        Assert.Equal(oracle.Count, sut.Count);
        Assert.Equal(oracle, sut);
    }

    private static void AssertEquivalent(BTreeSet<int> sut, SortedSet<int> oracle)
    {
        Assert.Equal(oracle.Count, sut.Count);

        // Ordering, not just membership: the enumerated sequence must be the oracle's, element for element.
        Assert.Equal(oracle, sut);

        for (int value = -8; value <= 305; value++)
            Assert.Equal(oracle.Contains(value), sut.Contains(value));
    }
}
