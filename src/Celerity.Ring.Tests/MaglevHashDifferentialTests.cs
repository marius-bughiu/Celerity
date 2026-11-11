using CsCheck;

namespace Celerity.Ring.Tests;

/// <summary>
/// Differential tests for <see cref="MaglevHash{TNode, TKey, THasher}"/>: after every mutation of a random
/// add / remove sequence, the whole lookup table must equal the one an independent textbook fill
/// (<see cref="MaglevTable.Reference"/>) builds from the surviving (node, weight) set, and the replica walk must
/// equal the distinct owners met reading that table forward from the key's slot.
/// </summary>
public class MaglevHashDifferentialTests
{
    private static readonly int[] TableSizes = { 2, 3, 7, 31, 101, 1_009 };

    [Fact]
    public void RandomMutationSequences_MatchTheReferenceFill()
    {
        Gen.Select(Gen.Int[0, TableSizes.Length - 1], Gen.Int[0, 11].Array[0, 40], Gen.Int[1, 4].Array[40])
            .Sample(t =>
            {
                (int sizeIndex, int[] ops, int[] weights) = t;
                int tableSize = TableSizes[sizeIndex];
                var maglev = new MaglevHash<string, uint, UInt32IdentityHasher>(tableSize);
                var model = new SortedDictionary<string, int>(StringComparer.Ordinal);
                int totalWeight = 0;

                for (int step = 0; step < ops.Length; step++)
                {
                    // Each op names one of 12 node ids; present nodes are removed, absent ones added.
                    string id = $"node-{ops[step]}";
                    if (model.Remove(id, out int removedWeight))
                    {
                        Assert.True(maglev.Remove(id));
                        totalWeight -= removedWeight;
                    }
                    else
                    {
                        int weight = weights[step % weights.Length];
                        if (totalWeight + weight > tableSize)
                        {
                            Assert.Throws<InvalidOperationException>(() => maglev.Add(id, id, weight));
                            continue;
                        }

                        maglev.Add(id, id, weight);
                        model.Add(id, weight);
                        totalWeight += weight;
                    }

                    Assert.Equal(model.Count, maglev.NodeCount);
                    if (model.Count == 0)
                    {
                        Assert.False(maglev.TryGetNode(0u, out _));
                        continue;
                    }

                    string[] expected = MaglevTable.Reference(model.Select(kv => (kv.Key, kv.Value)), tableSize);
                    Assert.Equal(expected, MaglevTable.Read(maglev));
                }
            }, iter: 300);
    }

    [Fact]
    public void Replicas_AreTheDistinctOwnersReadForwardFromTheKeysSlot()
    {
        const int TableSize = 101;
        var maglev = new MaglevHash<string, uint, UInt32IdentityHasher>(TableSize);
        for (int i = 0; i < 9; i++)
            maglev.Add($"n{i}", $"n{i}", 1 + i % 3);

        string[] table = MaglevTable.Read(maglev);
        var buffer = new string[9];
        for (uint key = 0; key < TableSize; key++)
        {
            var expected = new List<string>();
            for (int s = 0; expected.Count < 9; s++)
            {
                string owner = table[(key + s) % TableSize];
                if (!expected.Contains(owner))
                    expected.Add(owner);
            }

            for (int count = 1; count <= 9; count++)
            {
                Assert.Equal(expected.Take(count), maglev.GetReplicas(key, count));
                Assert.Equal(count, maglev.GetReplicas(key, buffer.AsSpan(0, count)));
                Assert.Equal(expected.Take(count), buffer.Take(count));
            }
        }
    }

    [Fact]
    public void DefaultTable_FiftyNodes_MatchesTheReferenceFill()
    {
        var nodes = Enumerable.Range(0, 50).Select(i => ($"backend-{i}.internal", 1 + i % 4)).ToArray();
        var maglev = new MaglevHash<string, uint, UInt32IdentityHasher>();
        foreach ((string id, int weight) in nodes)
            maglev.Add(id, id, weight);

        Assert.Equal(MaglevTable.Reference(nodes, maglev.TableSize), MaglevTable.Read(maglev));
    }
}
