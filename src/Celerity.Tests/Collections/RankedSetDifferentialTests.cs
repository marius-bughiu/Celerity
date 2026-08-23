using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="RankedSet{T, TComparer}"/> against
/// <see cref="SortedSet{T}"/> as the oracle. CsCheck generates randomized operation sequences, applies each
/// to both models, and asserts they agree on content, on ordering, <b>and on every rank</b> — the last is
/// what this type has to get right that the oracle cannot even be asked, so the ranks are reconciled against
/// the oracle's own enumeration order instead.
///
/// <para>
/// The deterministic reproductions of the split, merge and drop paths are in
/// <see cref="RankedSetTests"/>; this suite is the randomized second opinion on top of them, not the thing
/// those paths depend on for coverage.
/// </para>
/// </summary>
public class RankedSetDifferentialTests
{
    private enum Op { TryAdd, Remove, RemoveAt, Clear }

    // The element domain is deliberately several buckets wide (a bucket holds up to 512), so a few thousand
    // operations build and tear down enough buckets to drive splits, merges and drops densely.
    private static readonly Gen<(Op op, int value)> GenOp =
        Gen.Select(
            Gen.Int[0, 199].Select(n => n < 110 ? Op.TryAdd
                                      : n < 190 ? Op.Remove
                                      : n < 199 ? Op.RemoveAt
                                      : Op.Clear),
            Gen.Int[-5, 1500]);

    [Fact]
    public void RankedSet_ShouldMatch_SortedSet()
    {
        GenOp.List[0, 2500].Sample(ops =>
        {
            var sut = new RankedSet<int>();
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
                    case Op.RemoveAt when oracle.Count > 0:
                        int rank = (int)((uint)value % (uint)oracle.Count);
                        int removed = sut[rank];
                        sut.RemoveAt(rank);
                        Assert.True(oracle.Remove(removed));
                        break;
                    case Op.RemoveAt:
                        break;
                    case Op.Clear:
                        sut.Clear();
                        oracle.Clear();
                        break;
                }
            }

            AssertEquivalent(sut, oracle);
        }, iter: 40);
    }

    [Fact]
    public void RankedSet_ShouldMatch_SortedSet_OnTheOrderedSurface()
    {
        Gen.Int[-5, 1500].List[0, 2000].Sample(generated =>
        {
            var sut = new RankedSet<int>(generated);
            var oracle = new SortedSet<int>(generated);

            AssertEquivalent(sut, oracle);

            if (oracle.Count == 0)
            {
                Assert.False(sut.TryGetMin(out _));
                Assert.False(sut.TryGetMax(out _));
                return;
            }

            Assert.Equal(oracle.Min, sut.Min);
            Assert.Equal(oracle.Max, sut.Max);

            for (int probe = -6; probe <= 1501; probe += 37)
            {
                Assert.Equal(oracle.Count(x => x < probe), sut.CountLessThan(probe));
                Assert.Equal(oracle.Count(x => x <= probe), sut.CountLessThanOrEqual(probe));

                int[] atOrAbove = [.. oracle.Where(x => x >= probe)];
                Assert.Equal(atOrAbove.Length > 0, sut.TryGetLowerBound(probe, out int lower));
                if (atOrAbove.Length > 0)
                    Assert.Equal(atOrAbove[0], lower);

                int[] above = [.. oracle.Where(x => x > probe)];
                Assert.Equal(above.Length > 0, sut.TryGetUpperBound(probe, out int upper));
                if (above.Length > 0)
                    Assert.Equal(above[0], upper);

                Assert.Equal(
                    oracle.Where(x => x >= probe && x < probe + 300).ToArray(),
                    sut.EnumerateRange(probe, probe + 300).ToArray());
            }
        }, iter: 40);
    }

    // Content, order, and — the part the oracle has no method for — the rank of every element and the
    // element at every rank.
    private static void AssertEquivalent(RankedSet<int> sut, SortedSet<int> oracle)
    {
        Assert.Equal(oracle.Count, sut.Count);

        int[] expected = [.. oracle];
        Assert.Equal(expected, sut.ToArray());

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], sut[i]);
            Assert.Equal(i, sut.IndexOf(expected[i]));
        }
    }
}
