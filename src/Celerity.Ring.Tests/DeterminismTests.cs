namespace Celerity.Ring.Tests;

// The marquee property of Celerity.Ring: given the same node set, weights, and (deterministic) key hasher,
// the key->node mapping is a pure function of that input — identical across processes, runtimes, and CPU
// architectures. We cannot spin up arm64 / WASM here, but cross-arch identity follows from the routing math
// being integer-only (StringXxHash3Hasher + RingHash's pure integer mixing, no randomized hash, no
// float/log, no endianness-dependent step). These tests lock in the observable half of that guarantee:
// the mapping does not depend on insertion order or object identity, only on the node-id set.
public class DeterminismTests
{
    private static StringConsistentHashRing<string> Ring(IEnumerable<string> nodeIds, int vnodes = 160)
    {
        var ring = new StringConsistentHashRing<string>(vnodes);
        foreach (string id in nodeIds)
            ring.Add(id, id);
        return ring;
    }

    [Fact]
    public void ConsistentHashRing_MappingIsIndependentOfInsertionOrder()
    {
        string[] nodeIds = Enumerable.Range(0, 12).Select(i => $"node-{i}").ToArray();

        var forward = Ring(nodeIds);
        var reverse = Ring(nodeIds.Reverse());
        var shuffled = Ring(new[] { "node-5", "node-0", "node-11", "node-2", "node-8", "node-1", "node-9", "node-3", "node-7", "node-4", "node-10", "node-6" });

        for (int i = 0; i < 20_000; i++)
        {
            string key = $"user:{i}";
            string expected = forward.GetNode(key);
            Assert.Equal(expected, reverse.GetNode(key));
            Assert.Equal(expected, shuffled.GetNode(key));
        }
    }

    [Fact]
    public void ConsistentHashRing_TwoIndependentInstances_Agree()
    {
        string[] nodeIds = { "alpha", "beta", "gamma", "delta" };
        var a = Ring(nodeIds);
        var b = Ring(nodeIds);

        for (int i = 0; i < 10_000; i++)
        {
            string key = Guid.NewGuid().ToString("N") + i;
            Assert.Equal(a.GetNode(key), b.GetNode(key));
        }
    }

    [Fact]
    public void RendezvousHash_MappingIsIndependentOfInsertionOrder()
    {
        string[] nodeIds = Enumerable.Range(0, 10).Select(i => $"node-{i}").ToArray();

        var forward = new StringRendezvousHash<string>();
        foreach (string id in nodeIds)
            forward.Add(id, id);

        var reverse = new StringRendezvousHash<string>();
        foreach (string id in nodeIds.Reverse())
            reverse.Add(id, id);

        for (int i = 0; i < 20_000; i++)
        {
            string key = $"tenant:{i}";
            Assert.Equal(forward.GetNode(key), reverse.GetNode(key));
        }
    }

    [Fact]
    public void ConsistentHashRing_EmptyStringHasher_GoldenValue_LocksTheHashContract()
    {
        // The node-label placement hash is StringXxHash3Hasher. Its value for a given string is a fixed,
        // specified number (canonical XXH3-64 of the UTF-16 bytes, xor-folded to 32 bits), so this golden
        // value would change only if the underlying deterministic hash changed — the exact event that would
        // break cross-fleet agreement. Empty string -> 0x2D06800538D394C2 folded: low ^ high.
        StringXxHash3Hasher hasher = default;
        unchecked
        {
            const uint low = 0x38D394C2u;
            const uint high = 0x2D068005u;
            Assert.Equal((int)(low ^ high), hasher.Hash(string.Empty));
        }
    }

    [Fact]
    public void ConsistentHashRing_RebuildAfterAddRemove_IsPathIndependent()
    {
        // Reaching the same node set by different add/remove paths must yield the same mapping.
        var direct = Ring(new[] { "a", "b", "c" });

        var churned = new StringConsistentHashRing<string>();
        churned.Add("a", "a");
        churned.Add("x", "x");
        churned.Add("b", "b");
        churned.Add("c", "c");
        churned.Remove("x");

        for (int i = 0; i < 10_000; i++)
        {
            string key = $"k{i}";
            Assert.Equal(direct.GetNode(key), churned.GetNode(key));
        }
    }

    [Fact]
    public void MaglevHash_MappingIsIndependentOfInsertionOrder()
    {
        string[] nodeIds = Enumerable.Range(0, 12).Select(i => $"node-{i}").ToArray();

        var forward = new StringMaglevHash<string>();
        foreach (string id in nodeIds)
            forward.Add(id, id, 1 + id.Length % 3);

        var reverse = new StringMaglevHash<string>();
        foreach (string id in nodeIds.Reverse())
            reverse.Add(id, id, 1 + id.Length % 3);

        for (int i = 0; i < 20_000; i++)
        {
            string key = $"user:{i}";
            Assert.Equal(forward.GetNode(key), reverse.GetNode(key));
        }
    }

    [Fact]
    public void MaglevHash_RebuildAfterAddRemove_IsPathIndependent()
    {
        var direct = new StringMaglevHash<string>();
        foreach (string id in new[] { "a", "b", "c" })
            direct.Add(id, id);

        var churned = new StringMaglevHash<string>();
        churned.Add("a", "a");
        churned.Add("x", "x", weight: 5);
        churned.Add("b", "b");
        churned.Add("c", "c");
        churned.Remove("x");

        for (int i = 0; i < 10_000; i++)
        {
            string key = $"k{i}";
            Assert.Equal(direct.GetNode(key), churned.GetNode(key));
        }
    }

    [Fact]
    public void MaglevHash_GoldenTable_LocksThePermutationAndFillContract()
    {
        // The whole 1,009-slot table for a fixed weighted node set, fingerprinted with FNV-1a over each slot's
        // owner. It is a pure function of the node identities (StringXxHash3Hasher.Hash64), the table size and
        // the fill order, so it changes only if one of those does — the event that would make two versions of a
        // fleet route differently.
        var maglev = new MaglevHash<string, uint, UInt32IdentityHasher>(tableSize: 1_009);
        maglev.Add("alpha", "a", 1);
        maglev.Add("beta", "b", 2);
        maglev.Add("gamma", "g", 3);
        maglev.Add("delta", "d", 4);

        string table = string.Concat(MaglevTable.Read(maglev));
        ulong fingerprint = 14695981039346656037UL;
        foreach (char c in table)
        {
            fingerprint ^= c;
            fingerprint *= 1099511628211UL;
        }

        Assert.StartsWith("ddbbgaddabbdgagdbdbggdddbgddggbaddbggbdbggdddbgd", table);
        Assert.Equal(0xFD11F85B56292FE8UL, fingerprint);
    }
}
