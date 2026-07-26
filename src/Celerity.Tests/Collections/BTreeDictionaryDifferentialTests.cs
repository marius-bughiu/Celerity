using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="BTreeDictionary{TKey, TValue, TComparer}"/> against
/// <see cref="SortedDictionary{TKey, TValue}"/> as the oracle. CsCheck generates randomized operation
/// sequences, applies each to both models, and asserts they agree on content <b>and on ordering</b> — a
/// B-tree that loses balance still answers lookups correctly for a while, so the enumerated sequence is the
/// assertion that actually catches a bad split, borrow, or merge. Any failure shrinks to a minimal
/// reproduction and prints the seed.
/// </summary>
public class BTreeDictionaryDifferentialTests
{
    private enum Op { Set, TryAdd, Remove, Clear }

    // The key domain is deliberately wider than a node (31 keys), so a run of a few hundred operations
    // builds two to three levels and drives splits, borrows, and merges densely rather than staying inside
    // a single root leaf.
    private static readonly Gen<(Op op, int key, int value)> GenOp =
        Gen.Select(
            Gen.Int[0, 99].Select(n => n < 40 ? Op.Set
                                     : n < 65 ? Op.TryAdd
                                     : n < 99 ? Op.Remove
                                     : Op.Clear),
            Gen.Int[-5, 300],
            Gen.Int[0, 1_000]);

    private static readonly Gen<List<(Op, int, int)>> GenOps = GenOp.List[0, 400];

    [Fact]
    public void BTreeDictionary_ShouldMatch_SortedDictionary()
    {
        GenOps.Sample(ops =>
        {
            var sut = new BTreeDictionary<int, int>();
            var oracle = new SortedDictionary<int, int>();

            foreach (var (op, key, value) in ops)
            {
                switch (op)
                {
                    case Op.Set:
                        sut[key] = value;
                        oracle[key] = value;
                        break;
                    case Op.TryAdd:
                        Assert.Equal(oracle.TryAdd(key, value), sut.TryAdd(key, value));
                        break;
                    case Op.Remove:
                        bool expected = oracle.TryGetValue(key, out int expectedValue);
                        oracle.Remove(key);
                        Assert.Equal(expected, sut.Remove(key, out int actualValue));
                        if (expected)
                            Assert.Equal(expectedValue, actualValue);
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
    public void BTreeDictionary_ShouldMatch_SortedDictionary_OnTheOrderedSurface()
    {
        // Builds a tree from a random key set, then reconciles Min/Max, both bounds, and a range scan against
        // the oracle at every probe in the domain — including the two open ends.
        Gen.Int[-5, 200].List[0, 250].Sample(generated =>
        {
            var sut = new BTreeDictionary<int, int>();
            var oracle = new SortedDictionary<int, int>();
            foreach (int key in generated.Distinct())
            {
                sut.Add(key, key * 2);
                oracle.Add(key, key * 2);
            }

            Assert.Equal(oracle.Count, sut.Count);
            Assert.Equal(oracle.Keys, sut.Select(e => e.Key));
            Assert.Equal(oracle.Values, sut.Select(e => e.Value));

            if (oracle.Count == 0)
            {
                Assert.False(sut.TryGetMin(out _));
                Assert.False(sut.TryGetMax(out _));
                return;
            }

            Assert.Equal(oracle.Keys.First(), sut.Min.Key);
            Assert.Equal(oracle.Keys.Last(), sut.Max.Key);

            for (int probe = -7; probe <= 202; probe++)
            {
                bool hasLower = oracle.Keys.Any(k => k >= probe);
                Assert.Equal(hasLower, sut.TryGetLowerBound(probe, out KeyValuePair<int, int> lower));
                if (hasLower)
                    Assert.Equal(oracle.Keys.First(k => k >= probe), lower.Key);

                bool hasUpper = oracle.Keys.Any(k => k > probe);
                Assert.Equal(hasUpper, sut.TryGetUpperBound(probe, out KeyValuePair<int, int> upper));
                if (hasUpper)
                    Assert.Equal(oracle.Keys.First(k => k > probe), upper.Key);

                Assert.Equal(
                    oracle.Keys.Where(k => k >= probe && k < probe + 25),
                    sut.EnumerateRange(probe, probe + 25).Select(e => e.Key));
            }
        }, iter: 100);
    }

    private static void AssertEquivalent(BTreeDictionary<int, int> sut, SortedDictionary<int, int> oracle)
    {
        Assert.Equal(oracle.Count, sut.Count);

        // Ordering, not just membership: the enumerated sequence must be the oracle's, element for element.
        Assert.Equal(oracle.Keys, sut.Select(e => e.Key));
        Assert.Equal(oracle.Values, sut.Select(e => e.Value));

        for (int key = -8; key <= 305; key++)
        {
            bool expected = oracle.TryGetValue(key, out int expectedValue);
            Assert.Equal(expected, sut.TryGetValue(key, out int actualValue));
            Assert.Equal(expected, sut.ContainsKey(key));
            if (expected)
                Assert.Equal(expectedValue, actualValue);
        }
    }
}
