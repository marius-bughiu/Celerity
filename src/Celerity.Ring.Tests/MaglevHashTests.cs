namespace Celerity.Ring.Tests;

/// <summary>
/// Behavioural tests for <see cref="MaglevHash{TNode, TKey, THasher}"/>: argument guards, membership, the
/// table-size and total-weight contracts, exact slot balance, disruption, and routing through both a string and
/// an integer key type.
/// </summary>
/// <remarks>
/// Several tests read the lookup table slot by slot through <see cref="MaglevTable.Read"/>: an instance keyed by
/// <see cref="uint"/> with <see cref="UInt32IdentityHasher"/> routes key <c>s</c> to slot <c>s</c> for every
/// <c>s &lt; TableSize</c>, so <c>GetNode(s)</c> is the table entry itself.
/// </remarks>
public class MaglevHashTests
{
    private static StringMaglevHash<string> TableOf(params string[] nodeIds)
    {
        var maglev = new StringMaglevHash<string>();
        foreach (string id in nodeIds)
            maglev.Add(id, id);
        return maglev;
    }

    [Fact]
    public void EmptyTable_GetNode_Throws()
    {
        var maglev = new StringMaglevHash<string>();
        Assert.Throws<InvalidOperationException>(() => maglev.GetNode("anything"));
    }

    [Fact]
    public void EmptyTable_TryGetNode_ReturnsFalse()
    {
        var maglev = new StringMaglevHash<string>();
        Assert.False(maglev.TryGetNode("anything", out string? node));
        Assert.Null(node);
    }

    [Fact]
    public void SingleNode_RoutesEveryKeyToIt()
    {
        var maglev = TableOf("only");
        for (int i = 0; i < 1000; i++)
            Assert.Equal("only", maglev.GetNode($"key-{i}"));
    }

    [Fact]
    public void Add_DuplicateNodeId_Throws()
    {
        var maglev = TableOf("a");
        Assert.Throws<ArgumentException>(() => maglev.Add("a", "a"));
    }

    [Fact]
    public void Add_NullNodeId_Throws()
    {
        var maglev = new StringMaglevHash<string>();
        Assert.Throws<ArgumentNullException>(() => maglev.Add(null!, "x"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Add_WeightBelowOne_Throws(int weight)
    {
        var maglev = new StringMaglevHash<string>();
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => maglev.Add("a", "a", weight));
        Assert.Equal("weight", ex.ParamName);
        Assert.Equal(0, maglev.NodeCount);
    }

    [Fact]
    public void RemoveAndContains_NullNodeId_Throw()
    {
        var maglev = TableOf("a");
        Assert.Throws<ArgumentNullException>(() => maglev.Remove(null!));
        Assert.Throws<ArgumentNullException>(() => maglev.Contains(null!));
    }

    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-7)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(MaglevHash<string, string, StringXxHash3Hasher>.MaxTableSize + 2)]
    [InlineData(int.MaxValue)]
    public void Constructor_TableSizeOutOfRange_Throws(int tableSize)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new StringMaglevHash<string>(tableSize));
        Assert.Equal("tableSize", ex.ParamName);
    }

    [Theory]
    [InlineData(4, 5)]
    [InlineData(9, 11)]
    [InlineData(65_536, 65_537)]
    [InlineData(100_000, 100_003)]
    public void Constructor_CompositeTableSize_Throws_AndNamesTheNextPrime(int tableSize, int nextPrime)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new StringMaglevHash<string>(tableSize));
        Assert.Equal("tableSize", ex.ParamName);
        Assert.Contains(nextPrime.ToString(), ex.Message);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(65_537)]
    [InlineData(MaglevHash<string, string, StringXxHash3Hasher>.MaxTableSize)]
    public void Constructor_PrimeTableSize_IsAccepted(int tableSize)
    {
        var maglev = new StringMaglevHash<string>(tableSize);
        Assert.Equal(tableSize, maglev.TableSize);
        Assert.Equal(0, maglev.NodeCount);
    }

    [Fact]
    public void DefaultTableSize_Is65537()
    {
        Assert.Equal(65_537, new StringMaglevHash<string>().TableSize);
        Assert.Equal(65_537, MaglevHash<string, string, StringXxHash3Hasher>.DefaultTableSize);
    }

    [Fact]
    public void Contains_TracksMembership()
    {
        var maglev = TableOf("a", "b");
        Assert.True(maglev.Contains("a"));
        Assert.True(maglev.Contains("b"));
        Assert.False(maglev.Contains("c"));

        Assert.True(maglev.Remove("a"));
        Assert.False(maglev.Contains("a"));
        Assert.False(maglev.Remove("a"));
        Assert.Equal(1, maglev.NodeCount);
    }

    [Fact]
    public void Add_PastTheTableSize_ThrowsAndLeavesTheTableUncorrupted()
    {
        var maglev = new StringMaglevHash<string>(tableSize: 7);
        maglev.Add("a", "a", weight: 3);
        maglev.Add("b", "b", weight: 3);
        string[] before = Enumerable.Range(0, 200).Select(i => maglev.GetNode($"k{i}")).ToArray();

        // 3 + 3 + 2 = 8 > 7 slots: some node could own nothing, so the Add is rejected before it registers.
        Assert.Throws<InvalidOperationException>(() => maglev.Add("c", "c", weight: 2));

        Assert.False(maglev.Contains("c"));
        Assert.Equal(2, maglev.NodeCount);
        Assert.Equal(before, Enumerable.Range(0, 200).Select(i => maglev.GetNode($"k{i}")).ToArray());

        // Not wedged: the rejected weight was not counted, so a node that exactly fills the table still fits.
        maglev.Add("c", "c", weight: 1);
        Assert.Equal(3, maglev.NodeCount);
    }

    [Fact]
    public void Remove_ReleasesItsWeight_SoTheTableCanBeRefilled()
    {
        var maglev = new StringMaglevHash<string>(tableSize: 5);
        maglev.Add("a", "a", weight: 2);
        maglev.Add("b", "b", weight: 3);
        Assert.Throws<InvalidOperationException>(() => maglev.Add("c", "c"));

        Assert.True(maglev.Remove("b"));
        maglev.Add("c", "c", weight: 3);
        Assert.Equal(2, maglev.NodeCount);
    }

    [Fact]
    public void TotalWeightEqualToTableSize_GivesEachNodeExactlyItsWeight()
    {
        var maglev = new MaglevHash<string, uint, UInt32IdentityHasher>(tableSize: 11);
        maglev.Add("a", "a", weight: 1);
        maglev.Add("b", "b", weight: 4);
        maglev.Add("c", "c", weight: 6);

        Dictionary<string, int> slots = MaglevTable.Read(maglev).GroupBy(n => n).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(1, slots["a"]);
        Assert.Equal(4, slots["b"]);
        Assert.Equal(6, slots["c"]);
    }

    [Fact]
    public void TableSizeTwo_RoutesBothSlots()
    {
        var maglev = new MaglevHash<string, uint, UInt32IdentityHasher>(tableSize: 2);
        maglev.Add("a", "a");
        maglev.Add("b", "b");

        Assert.Equal(new[] { "a", "b" }, MaglevTable.Read(maglev).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(10, 65_537)]
    [InlineData(100, 65_537)]
    [InlineData(257, 1_009)]
    [InlineData(5, 7)]
    public void EqualWeights_OwnWithinOneSlotOfEachOther(int nodeCount, int tableSize)
    {
        var maglev = new MaglevHash<string, uint, UInt32IdentityHasher>(tableSize);
        for (int i = 0; i < nodeCount; i++)
            maglev.Add($"node-{i}", $"node-{i}");

        int[] counts = MaglevTable.Read(maglev).GroupBy(n => n).Select(g => g.Count()).ToArray();

        Assert.Equal(nodeCount, counts.Length);
        Assert.True(counts.Max() - counts.Min() <= 1, $"slot counts range {counts.Min()}..{counts.Max()}");
        Assert.Equal(tableSize, counts.Sum());
    }

    [Fact]
    public void Weights_OwnTheirShareWithinOneRoundOfTheFill()
    {
        int[] weights = { 1, 2, 3, 5, 8, 13 };
        const int TableSize = 1_009;
        var maglev = new MaglevHash<string, uint, UInt32IdentityHasher>(TableSize);
        for (int i = 0; i < weights.Length; i++)
            maglev.Add($"w{i}", $"w{i}", weights[i]);

        Dictionary<string, int> slots = MaglevTable.Read(maglev).GroupBy(n => n).ToDictionary(g => g.Key, g => g.Count());

        // Every full round of the fill gives node i exactly weights[i] slots; only the last, partial round can
        // leave it short. So slots / weight lies in [rounds, rounds + 1].
        int rounds = TableSize / weights.Sum();
        for (int i = 0; i < weights.Length; i++)
        {
            int owned = slots[$"w{i}"];
            Assert.InRange(owned, weights[i] * rounds, weights[i] * (rounds + 1));
        }
    }

    [Fact]
    public void Weight_AttractsProportionallyMoreKeys()
    {
        var maglev = new StringMaglevHash<string>();
        maglev.Add("light", "light", weight: 1);
        maglev.Add("heavy", "heavy", weight: 4);

        int heavy = 0;
        const int Keys = 100_000;
        for (int i = 0; i < Keys; i++)
        {
            if (maglev.GetNode($"key-{i}") == "heavy")
                heavy++;
        }

        // The table split is exactly 4:1 to within a slot, so only key sampling noise (~0.13%) remains.
        double heavyFraction = heavy / (double)Keys;
        Assert.InRange(heavyFraction, 0.79, 0.81);
    }

    [Fact]
    public void Distribution_AcrossEqualNodes_IsTight()
    {
        string[] nodeIds = Enumerable.Range(0, 10).Select(i => $"node-{i}").ToArray();
        var maglev = TableOf(nodeIds);

        var counts = new Dictionary<string, int>();
        const int Keys = 100_000;
        for (int i = 0; i < Keys; i++)
        {
            string owner = maglev.GetNode($"key-{i}");
            counts[owner] = counts.GetValueOrDefault(owner) + 1;
        }

        // A ring with 160 virtual nodes needs a ±40% band here; the table is exact to a slot, so only key sampling
        // noise remains (σ ≈ 95 keys), and ±500 is over 5σ. The keys are fixed, so the outcome is deterministic.
        Assert.Equal(10, counts.Count);
        foreach (int count in counts.Values)
            Assert.InRange(count, 9_500, 10_500);
    }

    [Fact]
    public void RemovingANode_MovesItsKeysAndLittleElse()
    {
        string[] nodeIds = Enumerable.Range(0, 100).Select(i => $"n{i}").ToArray();
        var maglev = TableOf(nodeIds);

        const int Keys = 100_000;
        var before = new string[Keys];
        for (int i = 0; i < Keys; i++)
            before[i] = maglev.GetNode($"key-{i}");

        maglev.Remove("n42");

        int collateral = 0;
        for (int i = 0; i < Keys; i++)
        {
            string after = maglev.GetNode($"key-{i}");
            Assert.NotEqual("n42", after);
            if (before[i] != "n42" && after != before[i])
                collateral++;
        }

        // The removed node's ~1% must move; Maglev also moves a small fraction of the rest (~0.6% measured at
        // 655 slots per node). Bound it at 2% so a broken fill — one that reshuffles the table — fails loudly.
        double collateralFraction = collateral / (double)Keys;
        Assert.True(collateralFraction < 0.02, $"{collateralFraction:P2} of the other nodes' keys moved");
    }

    [Fact]
    public void Remove_LastNode_LeavesTheTableEmptyAndUnroutable()
    {
        var maglev = TableOf("only");
        Assert.True(maglev.Remove("only"));

        Assert.Equal(0, maglev.NodeCount);
        Assert.False(maglev.TryGetNode("k", out _));
        Assert.Throws<InvalidOperationException>(() => maglev.GetNode("k"));
        Assert.Empty(maglev.GetReplicas("k", 3));
        Assert.Equal(0, maglev.GetReplicas("k", new string[3]));

        maglev.Add("again", "again");
        Assert.Equal("again", maglev.GetNode("k"));
    }

    [Fact]
    public void GetReplicas_ReturnsDistinctNodesOwnerFirst()
    {
        var maglev = TableOf("a", "b", "c", "d");

        IReadOnlyList<string> replicas = maglev.GetReplicas("some-key", 3);

        Assert.Equal(3, replicas.Count);
        Assert.Equal(replicas.Count, replicas.Distinct().Count());
        Assert.Equal(maglev.GetNode("some-key"), replicas[0]);
    }

    [Fact]
    public void GetReplicas_CountExceedingNodes_ReturnsAllDistinctNodes()
    {
        var maglev = TableOf("a", "b", "c");
        IReadOnlyList<string> replicas = maglev.GetReplicas("k", 10);
        Assert.Equal(new[] { "a", "b", "c" }, replicas.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void GetReplicas_NegativeCount_Throws(int count)
    {
        var maglev = TableOf("a");
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => maglev.GetReplicas("k", count));
        Assert.Equal("count", ex.ParamName);
    }

    [Fact]
    public void GetReplicas_ZeroCount_ReturnsEmpty()
    {
        var maglev = TableOf("a", "b");
        Assert.Empty(maglev.GetReplicas("k", 0));
        Assert.Equal(0, maglev.GetReplicas("k", Span<string>.Empty));
    }

    [Fact]
    public void Works_WithIntegerKeysAndCustomHasher()
    {
        var maglev = new MaglevHash<string, long, Int64WangNaiveHasher>();
        maglev.Add("shard-a", "shard-a");
        maglev.Add("shard-b", "shard-b");
        maglev.Add("shard-c", "shard-c");

        for (long k = 0; k < 1000; k++)
        {
            string first = maglev.GetNode(k);
            Assert.Contains(first, new[] { "shard-a", "shard-b", "shard-c" });
            Assert.Equal(first, maglev.GetNode(k));
            Assert.True(maglev.TryGetNode(k, out string same));
            Assert.Equal(first, same);
        }
    }

    [Fact]
    public void IdentityKeys_PastTheTableSize_WrapToTheSameSlot()
    {
        var maglev = new MaglevHash<string, uint, UInt32IdentityHasher>(tableSize: 101);
        for (int i = 0; i < 7; i++)
            maglev.Add($"n{i}", $"n{i}");

        // The slot is hash mod TableSize over the full 32-bit range, including hashes with the top bit set.
        foreach (uint key in new uint[] { 0, 1, 100, 101, 202, 12_345, uint.MaxValue - 1, uint.MaxValue })
            Assert.Equal(maglev.GetNode(key % 101), maglev.GetNode(key));
    }
}

/// <summary>Reads a <see cref="MaglevHash{TNode, TKey, THasher}"/>'s lookup table through an identity-hashed key.</summary>
internal static class MaglevTable
{
    /// <summary>Returns the node in every slot, in slot order.</summary>
    internal static string[] Read(MaglevHash<string, uint, UInt32IdentityHasher> maglev)
    {
        var slots = new string[maglev.TableSize];
        for (uint s = 0; s < slots.Length; s++)
            slots[s] = maglev.GetNode(s);
        return slots;
    }

    /// <summary>
    /// An independent, textbook implementation of the Maglev fill (Eisenbud et al., Algorithm 1, with weighted
    /// turns), written with plain <c>%</c> arithmetic and a per-node <c>next</c> counter rather than the shipped
    /// type's incremental cursor. Node permutations are derived from <see cref="StringXxHash3Hasher.Hash64(string)"/>
    /// exactly as the type documents.
    /// </summary>
    /// <remarks>
    /// Sharing the offset / skip derivation and the ordinal node order with the type is deliberate: those are the
    /// determinism contract, pinned independently by <c>MaglevHash_GoldenTable_LocksThePermutationAndFillContract</c>.
    /// What this oracle checks is the fill itself.
    /// </remarks>
    internal static string[] Reference(IEnumerable<(string Id, int Weight)> nodes, int tableSize)
    {
        (string Id, int Weight)[] sorted = nodes.OrderBy(n => n.Id, StringComparer.Ordinal).ToArray();
        var offset = new long[sorted.Length];
        var skip = new long[sorted.Length];
        var next = new long[sorted.Length];
        StringXxHash3Hasher hasher = default;
        for (int i = 0; i < sorted.Length; i++)
        {
            ulong hash = hasher.Hash64(sorted[i].Id);
            offset[i] = (uint)hash % tableSize;
            skip[i] = (uint)(hash >> 32) % (tableSize - 1) + 1;
        }

        var table = new int[tableSize];
        Array.Fill(table, -1);
        int filled = 0;
        while (true)
        {
            for (int i = 0; i < sorted.Length; i++)
            {
                for (int turn = 0; turn < sorted[i].Weight; turn++)
                {
                    long c;
                    do
                    {
                        c = (offset[i] + next[i] * skip[i]) % tableSize;
                        next[i]++;
                    }
                    while (table[c] >= 0);

                    table[c] = i;
                    if (++filled == tableSize)
                        return table.Select(t => sorted[t].Id).ToArray();
                }
            }
        }
    }
}
