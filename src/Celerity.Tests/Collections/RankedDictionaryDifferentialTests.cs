using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="RankedDictionary{TKey, TValue, TComparer}"/> against
/// <see cref="SortedDictionary{TKey, TValue}"/> as the oracle. CsCheck generates randomized operation
/// sequences, applies each to both maps, and asserts they agree on content, order, <b>and the key and value at
/// every rank</b> — the oracle has no positional method, so ranks are reconciled against its enumeration order.
///
/// <para>
/// The deterministic reproductions of the split, merge and drop paths are in
/// <see cref="RankedDictionaryTests"/>; this suite is the randomized second opinion on top of them.
/// </para>
/// </summary>
public class RankedDictionaryDifferentialTests
{
    private enum Op { TryAdd, Set, Remove, RemoveAt, SetValueAtIndex, Clear }

    // The key domain is several buckets wide (a bucket holds up to 512), so a few thousand operations build
    // and tear down enough buckets to drive splits, merges and drops densely.
    private static readonly Gen<(Op op, int key, int value)> GenOp =
        Gen.Select(
            Gen.Int[0, 199].Select(n => n < 90 ? Op.TryAdd
                                      : n < 110 ? Op.Set
                                      : n < 180 ? Op.Remove
                                      : n < 190 ? Op.RemoveAt
                                      : n < 199 ? Op.SetValueAtIndex
                                      : Op.Clear),
            Gen.Int[-5, 1500],
            Gen.Int);

    [Fact]
    public void RankedDictionary_ShouldMatch_SortedDictionary()
    {
        GenOp.List[0, 2500].Sample(ops =>
        {
            var sut = new RankedDictionary<int, int>();
            var oracle = new SortedDictionary<int, int>();

            foreach (var (op, key, value) in ops)
            {
                switch (op)
                {
                    case Op.TryAdd:
                        Assert.Equal(oracle.TryAdd(key, value), sut.TryAdd(key, value));
                        break;
                    case Op.Set:
                        oracle[key] = value;
                        sut[key] = value;
                        break;
                    case Op.Remove:
                        Assert.Equal(oracle.Remove(key, out int expected), sut.Remove(key, out int actual));
                        Assert.Equal(expected, actual);
                        break;
                    case Op.RemoveAt when oracle.Count > 0:
                    {
                        int rank = (int)((uint)key % (uint)oracle.Count);
                        int removed = sut.GetKeyAtIndex(rank);
                        sut.RemoveAt(rank);
                        Assert.True(oracle.Remove(removed));
                        break;
                    }
                    case Op.SetValueAtIndex when oracle.Count > 0:
                    {
                        int rank = (int)((uint)key % (uint)oracle.Count);
                        sut.SetValueAtIndex(rank, value);
                        oracle[sut.GetKeyAtIndex(rank)] = value;
                        break;
                    }
                    case Op.Clear:
                        sut.Clear();
                        oracle.Clear();
                        break;
                    default:
                        break; // a positional op on an empty map has nothing to address
                }
            }

            AssertEquivalent(sut, oracle);
        }, iter: 40);
    }

    [Fact]
    public void RankedDictionary_ShouldMatch_SortedDictionary_OnTheOrderedSurface()
    {
        Gen.Int[-5, 1500].List[0, 2000].Sample(generated =>
        {
            var sut = new RankedDictionary<int, int>();
            var oracle = new SortedDictionary<int, int>();
            foreach (int key in generated)
            {
                sut[key] = key ^ 0x5A5A;
                oracle[key] = key ^ 0x5A5A;
            }

            AssertEquivalent(sut, oracle);

            if (oracle.Count == 0)
            {
                Assert.False(sut.TryGetMin(out _));
                Assert.False(sut.TryGetMax(out _));
                return;
            }

            Assert.Equal(oracle.First(), sut.Min);
            Assert.Equal(oracle.Last(), sut.Max);

            for (int probe = -6; probe <= 1501; probe += 37)
            {
                Assert.Equal(oracle.Keys.Count(k => k < probe), sut.CountLessThan(probe));
                Assert.Equal(oracle.Keys.Count(k => k <= probe), sut.CountLessThanOrEqual(probe));

                KeyValuePair<int, int>[] atOrAbove = [.. oracle.Where(e => e.Key >= probe)];
                Assert.Equal(atOrAbove.Length > 0, sut.TryGetLowerBound(probe, out var lower));
                if (atOrAbove.Length > 0)
                    Assert.Equal(atOrAbove[0], lower);

                KeyValuePair<int, int>[] above = [.. oracle.Where(e => e.Key > probe)];
                Assert.Equal(above.Length > 0, sut.TryGetUpperBound(probe, out var upper));
                if (above.Length > 0)
                    Assert.Equal(above[0], upper);

                Assert.Equal(
                    oracle.Where(e => e.Key >= probe && e.Key < probe + 300).ToArray(),
                    sut.EnumerateRange(probe, probe + 300).ToArray());
            }
        }, iter: 40);
    }

    // Content, order, and — what the oracle has no method for — the entry at every rank and every key's rank.
    private static void AssertEquivalent(RankedDictionary<int, int> sut, SortedDictionary<int, int> oracle)
    {
        Assert.Equal(oracle.Count, sut.Count);

        KeyValuePair<int, int>[] expected = [.. oracle];
        Assert.Equal(expected, sut.ToArray());

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], sut.GetEntryAtIndex(i));
            Assert.Equal(i, sut.IndexOfKey(expected[i].Key));
        }
    }
}
