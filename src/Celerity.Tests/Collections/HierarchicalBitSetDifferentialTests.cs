using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="HierarchicalBitSet"/> against <see cref="SortedSet{T}"/>
/// as the oracle (issue #531). CsCheck generates the universe and an operation sequence, applies each update
/// to both, and checks every ordered query against the oracle as it goes, so a summary bit left stale by an
/// earlier update fails at the first query that walks through it, not only at the end. The universes are
/// drawn from three bands — one leaf word, two levels, and four levels — and the values cluster around a
/// few centres so that words fill and empty and the queries cross word boundaries. Any failure shrinks to a
/// minimal reproduction and prints the seed.
/// </summary>
public class HierarchicalBitSetDifferentialTests
{
    private enum Op { TryAdd, Remove, Clear, Query }

    // One word, two levels (65..4096 bits), whole multiples of 8,192 bits (where level 1 is a whole number of
    // words, the only shape that reaches NextSetBit's past-the-level exit) and four levels (> 262,144 bits).
    private static readonly Gen<int> GenUniverse =
        Gen.OneOf(Gen.Int[1, 64], Gen.Int[65, 4096], Gen.Int[1, 40].Select(k => k * 8192), Gen.Int[262_145, 300_000]);

    // The value is a position in [0, 1], scaled to the universe at run time and widened a little past both
    // ends, so the out-of-universe paths are exercised at every size.
    private static readonly Gen<(Op op, double position)> GenOp =
        Gen.Select(
            Gen.Int[0, 99].Select(n => n < 45 ? Op.TryAdd
                                     : n < 75 ? Op.Remove
                                     : n < 76 ? Op.Clear
                                     : Op.Query),
            Gen.Double[-0.02, 1.02]);

    private static readonly Gen<(int universe, List<(Op, double)> ops, uint seed)> GenCase =
        Gen.Select(GenUniverse, GenOp.List[0, 300], Gen.UInt);

    [Fact]
    public void HierarchicalBitSet_ShouldMatch_SortedSet()
    {
        GenCase.Sample(test =>
        {
            var (universe, ops, seed) = test;
            var sut = new HierarchicalBitSet(universe);
            var oracle = new SortedSet<int>();

            // Values cluster around a few centres so that a word fills, empties and refills; the seed decides
            // the centres and the jitter.
            var random = new Random((int)seed);
            int[] centres = { 0, universe / 3, universe - 1, random.Next(universe) };

            foreach (var (op, position) in ops)
            {
                int value = Value(universe, position, centres, random);
                switch (op)
                {
                    case Op.TryAdd:
                        if ((uint)value < (uint)universe)
                            Assert.Equal(oracle.Add(value), sut.TryAdd(value));
                        else
                            Assert.Throws<ArgumentOutOfRangeException>(() => sut.TryAdd(value));
                        break;
                    case Op.Remove:
                        Assert.Equal(oracle.Remove(value), sut.Remove(value));
                        break;
                    case Op.Clear:
                        sut.Clear();
                        oracle.Clear();
                        break;
                    case Op.Query:
                        AssertQueries(sut, oracle, value);
                        break;
                }

                Assert.Equal(oracle.Count, sut.Count);
            }

            Assert.Equal(oracle, sut);
            AssertQueries(sut, oracle, 0);
            AssertQueries(sut, oracle, universe - 1);
        }, iter: 400);
    }

    [Fact]
    public void HierarchicalBitSet_ShouldMatch_SortedSet_OnSetAlgebra()
    {
        Gen.Select(Gen.Int[0, 199].List[0, 80], Gen.Int[-5, 210].List[0, 80]).Sample(pair =>
        {
            var (left, right) = pair;
            var inUniverse = right.Where(v => (uint)v < 200u).ToList();

            AssertAlgebra(left, inUniverse, (s, o) => s.UnionWith(o), (s, o) => s.UnionWith(o));
            AssertAlgebra(left, right, (s, o) => s.IntersectWith(o), (s, o) => s.IntersectWith(o));
            AssertAlgebra(left, right, (s, o) => s.ExceptWith(o), (s, o) => s.ExceptWith(o));
            AssertAlgebra(left, inUniverse, (s, o) => s.SymmetricExceptWith(o), (s, o) => s.SymmetricExceptWith(o));

            var sut = new HierarchicalBitSet(200, left);
            var oracle = new SortedSet<int>(left);
            Assert.Equal(oracle.IsSubsetOf(right), sut.IsSubsetOf(right));
            Assert.Equal(oracle.IsProperSubsetOf(right), sut.IsProperSubsetOf(right));
            Assert.Equal(oracle.IsSupersetOf(right), sut.IsSupersetOf(right));
            Assert.Equal(oracle.IsProperSupersetOf(right), sut.IsProperSupersetOf(right));
            Assert.Equal(oracle.Overlaps(right), sut.Overlaps(right));
            Assert.Equal(oracle.SetEquals(right), sut.SetEquals(right));
        }, iter: 200);
    }

    private static int Value(int universe, double position, int[] centres, Random random)
    {
        // Half the values land within a word or two of a centre, the rest anywhere (including just outside).
        if (random.Next(2) == 0)
            return centres[random.Next(centres.Length)] + random.Next(-80, 81);

        return (int)Math.Floor(position * universe);
    }

    private static void AssertQueries(HierarchicalBitSet sut, SortedSet<int> oracle, int probe)
    {
        Assert.Equal(oracle.Contains(probe), sut.Contains(probe));

        Assert.Equal(oracle.Count > 0, sut.TryGetMin(out int min));
        Assert.Equal(oracle.Count > 0, sut.TryGetMax(out int max));
        if (oracle.Count > 0)
        {
            Assert.Equal(oracle.Min, min);
            Assert.Equal(oracle.Max, max);
        }

        AssertBound(oracle.Where(v => v >= probe), sut.TryGetLowerBound(probe, out int lower), lower, first: true);
        AssertBound(oracle.Where(v => v > probe), sut.TryGetUpperBound(probe, out int upper), upper, first: true);
        AssertBound(oracle.Where(v => v <= probe), sut.TryGetFloor(probe, out int floor), floor, first: false);
        AssertBound(oracle.Where(v => v < probe), sut.TryGetPredecessor(probe, out int predecessor), predecessor, first: false);

        Assert.Equal(oracle.Where(v => v >= probe && v < probe + 150), sut.EnumerateRange(probe, probe + 150));
    }

    private static void AssertBound(IEnumerable<int> candidates, bool found, int bound, bool first)
    {
        var list = candidates.ToList();
        Assert.Equal(list.Count > 0, found);
        Assert.Equal(list.Count == 0 ? 0 : first ? list[0] : list[^1], bound);
    }

    private static void AssertAlgebra(
        List<int> left,
        List<int> right,
        Action<HierarchicalBitSet, IEnumerable<int>> apply,
        Action<SortedSet<int>, IEnumerable<int>> applyOracle)
    {
        var sut = new HierarchicalBitSet(200, left);
        var oracle = new SortedSet<int>(left);

        apply(sut, right);
        applyOracle(oracle, right);

        Assert.Equal(oracle.Count, sut.Count);
        Assert.Equal(oracle, sut);
    }
}
