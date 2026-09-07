using System.Text;
using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Randomized reconciliation of <see cref="SuccinctTrie{TValue}"/> against the naive answers it replaces: the
/// key set held as a list sorted with <see cref="StringComparer.Ordinal"/>, prefixes resolved with
/// <see cref="string.StartsWith(string, StringComparison)"/>, and the node count derived from the set of
/// distinct prefixes.
///
/// <para>
/// The <i>encoding</i> is what is on trial. Nothing in this type stores a key, a child pointer or a node
/// boundary outright: a key is a path through a label array whose slices are delimited by rank and select
/// over a bit vector, and the values are reached through a second such vector. Every one of those is an
/// arithmetic identity that can be off by one and still answer plausibly — a child block that starts one bit
/// early borrows the previous node's last child, which yields a trie that is wrong only for the keys that
/// happen to route through it. A hand-written fixture is poorly suited to finding that, because the shapes
/// that expose it are key sets whose branching is uneven across the level order, which is not what an example
/// is written to contain.
/// </para>
///
/// <para>
/// Three layers, narrowest first. The CsCheck property generates the key set from its own axes — how many
/// keys, how wide the alphabet, how long the keys, and whether the empty key is among them — so a
/// disagreement shrinks to a minimal reproduction with the seed printed; a two-letter alphabet is the
/// adversarial end, since it maximizes both depth and prefix sharing. The seeded theory below it drives key
/// sets large enough that the LOUDS vector spans many rank superblocks, which is where a select that reads
/// the wrong superblock first shows. The exhaustive sweep at the end builds a trie from <i>every</i> subset
/// of the fifteen binary strings up to length three — 32,768 of them, every reachable shape of a binary trie
/// of that depth — which is the only layer that can prove no shape was merely missed by sampling. It runs the
/// structural half of the oracle (order, node count, and every query over the whole universe) rather than the
/// quadratic longest-prefix scan, which the first two layers cover and which would make the sweep
/// unaffordable.
/// </para>
/// </summary>
public class SuccinctTrieDifferentialTests
{
    // Key count, alphabet width, key length and whether the empty key is included: the four axes that decide
    // the shape of the level order. A one-letter alphabet degenerates to a single chain, a wide one to a
    // shallow fan, and only the narrow-but-not-degenerate middle produces the uneven branching that a
    // misaligned child block survives.
    private static readonly Gen<(int Count, int Alphabet, int MaxLength, bool WithEmpty, uint Seed)> GenKeySets =
        Gen.Select(Gen.Int[0, 60], Gen.Int[1, 5], Gen.Int[1, 8], Gen.Bool, Gen.UInt);

    [Fact]
    public void EveryQuery_ShouldMatchTheNaiveAnswer_UnderGeneratedKeySets()
    {
        GenKeySets.Sample(
            spec => AssertAgreesWithOracle(BuildKeys(spec.Count, spec.Alphabet, spec.MaxLength, spec.WithEmpty, spec.Seed)),
            iter: 250);
    }

    [Theory]
    [InlineData(2000, 2, 20, 1u)]
    [InlineData(2000, 26, 12, 2u)]
    [InlineData(5000, 4, 16, 3u)]
    [InlineData(500, 1, 200, 4u)]
    public void EveryQuery_ShouldMatchTheNaiveAnswer_OnLargerSeededKeySets(int count, int alphabet, int maxLength, uint seed)
    {
        AssertAgreesWithOracle(BuildKeys(count, alphabet, maxLength, withEmpty: true, seed));
    }

    [Fact]
    public void EveryQuery_ShouldMatchTheNaiveAnswer_OnEverySubsetOfTheShortBinaryStrings()
    {
        // The fifteen strings over {a, b} of length 0 to 3, and every one of the 32,768 subsets of them:
        // every reachable shape of a binary trie of that depth, including the empty one and the full one.
        string[] universe = ShortBinaryStrings();

        var keys = new List<string>(universe.Length);
        for (int mask = 0; mask < 1 << 15; mask++)
        {
            keys.Clear();
            for (int i = 0; i < universe.Length; i++)
            {
                if ((mask >> i & 1) != 0)
                    keys.Add(universe[i]);
            }

            AssertStructureAgreesWithOracle(keys, universe);
        }
    }

    // ---- the oracle --------------------------------------------------------------------------------

    // The entries a key list turns into, and the oracle they reduce to: last duplicate wins, ordered by the
    // ordinal comparison the trie claims to order by.
    private static (List<KeyValuePair<string, int>> Entries, List<KeyValuePair<string, int>> Oracle) OracleFor(List<string> keys)
    {
        var entries = new List<KeyValuePair<string, int>>(keys.Count);
        for (int i = 0; i < keys.Count; i++)
            entries.Add(new KeyValuePair<string, int>(keys[i], i));

        var unique = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, int> entry in entries)
            unique[entry.Key] = entry.Value;

        List<KeyValuePair<string, int>> oracle = unique
            .OrderBy(e => e.Key, StringComparer.Ordinal)
            .ToList();

        return (entries, oracle);
    }

    // The structural half: everything that can be checked without the quadratic longest-prefix scan.
    private static SuccinctTrie<int> AssertStructureAgreesWithOracle(List<string> keys, string[] probes)
    {
        (List<KeyValuePair<string, int>> entries, List<KeyValuePair<string, int>> oracle) = OracleFor(keys);
        var trie = new SuccinctTrie<int>(entries);

        Assert.Equal(oracle.Count, trie.Count);
        Assert.Equal(oracle, trie.Select(p => new KeyValuePair<string, int>(p.Key, p.Value)));
        Assert.Equal(oracle.Select(e => e.Key), trie.Keys);
        Assert.Equal(oracle.Select(e => e.Value), trie.Values);
        Assert.Equal(DistinctPrefixCount(oracle) + 1, trie.NodeCount);
        Assert.True(trie.IndexSizeInBytes > 0);

        foreach (string probe in probes)
        {
            bool expectedHit = oracle.Any(e => string.Equals(e.Key, probe, StringComparison.Ordinal));
            Assert.Equal(expectedHit, trie.ContainsKey(probe));
            Assert.Equal(expectedHit, trie.TryGetValue(probe, out int value));
            Assert.Equal(expectedHit ? oracle.First(e => e.Key == probe).Value : 0, value);

            List<string> expectedMatches = oracle
                .Where(e => e.Key.StartsWith(probe, StringComparison.Ordinal))
                .Select(e => e.Key)
                .ToList();

            Assert.Equal(expectedMatches.Count != 0, trie.ContainsPrefix(probe));
            Assert.Equal(expectedMatches, trie.GetKeysWithPrefix(probe));
        }

        return trie;
    }

    private static int DistinctPrefixCount(List<KeyValuePair<string, int>> oracle)
    {
        var prefixes = new HashSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, int> entry in oracle)
        {
            for (int length = 1; length <= entry.Key.Length; length++)
                prefixes.Add(entry.Key[..length]);
        }

        return prefixes.Count;
    }

    private static void AssertAgreesWithOracle(List<string> keys)
    {
        (List<KeyValuePair<string, int>> entries, List<KeyValuePair<string, int>> oracle) = OracleFor(keys);

        var trie = new SuccinctTrie<int>(entries);

        Assert.Equal(oracle.Count, trie.Count);

        // Enumeration is the whole structure read back out: order, keys and values at once.
        Assert.Equal(oracle, trie.Select(p => new KeyValuePair<string, int>(p.Key, p.Value)));
        Assert.Equal(oracle.Select(e => e.Key), trie.Keys);
        Assert.Equal(oracle.Select(e => e.Value), trie.Values);

        // The node count is the root plus one per distinct prefix, which the oracle can count outright.
        var prefixes = new HashSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, int> entry in oracle)
        {
            for (int length = 1; length <= entry.Key.Length; length++)
                prefixes.Add(entry.Key[..length]);
        }

        Assert.Equal(prefixes.Count + 1, trie.NodeCount);
        Assert.True(trie.IndexSizeInBytes > 0);

        // Probe every prefix of every key and one extension past each, so misses are checked as hard as hits.
        var probes = new List<string>(prefixes) { string.Empty };
        foreach (KeyValuePair<string, int> entry in oracle)
        {
            probes.Add(entry.Key);
            probes.Add(entry.Key + "z");
        }

        foreach (string probe in probes)
        {
            bool expectedHit = false;
            int expectedValue = 0;
            foreach (KeyValuePair<string, int> entry in oracle)
            {
                if (string.Equals(entry.Key, probe, StringComparison.Ordinal))
                {
                    expectedHit = true;
                    expectedValue = entry.Value;
                    break;
                }
            }

            Assert.Equal(expectedHit, trie.ContainsKey(probe));
            Assert.Equal(expectedHit, trie.TryGetValue(probe, out int value));
            Assert.Equal(expectedHit ? expectedValue : 0, value);

            // The span overloads must be indistinguishable from the string ones, slice and all.
            ReadOnlySpan<char> slice = ("<<" + probe + ">>").AsSpan(2, probe.Length);
            Assert.Equal(expectedHit, trie.ContainsKey(slice));
            Assert.Equal(expectedHit, trie.TryGetValue(slice, out int spanValue));
            Assert.Equal(value, spanValue);

            List<KeyValuePair<string, int>> expectedMatches = oracle
                .Where(e => e.Key.StartsWith(probe, StringComparison.Ordinal))
                .ToList();

            Assert.Equal(expectedMatches.Count != 0, trie.ContainsPrefix(probe));
            Assert.Equal(expectedMatches.Count != 0, trie.ContainsPrefix(slice));
            Assert.Equal(expectedMatches, trie.GetByPrefix(probe).Select(p => new KeyValuePair<string, int>(p.Key, p.Value)));
            Assert.Equal(expectedMatches.Select(e => e.Key), trie.GetKeysWithPrefix(probe));

            // The longest stored key that is a prefix of the probe, which the oracle finds by scanning.
            KeyValuePair<string, int>? longest = null;
            foreach (KeyValuePair<string, int> entry in oracle)
            {
                if (probe.StartsWith(entry.Key, StringComparison.Ordinal)
                    && (longest is null || entry.Key.Length > longest.Value.Key.Length))
                {
                    longest = entry;
                }
            }

            bool found = trie.TryGetLongestPrefix(probe, out string? key, out int longestValue);
            Assert.Equal(longest is not null, found);
            Assert.Equal(longest?.Key, key);
            Assert.Equal(longest?.Value ?? 0, longestValue);
        }
    }

    // ---- input construction ------------------------------------------------------------------------

    private static List<string> BuildKeys(int count, int alphabet, int maxLength, bool withEmpty, uint seed)
    {
        var random = new Random((int)seed);
        var keys = new List<string>(count + 1);

        if (withEmpty)
            keys.Add(string.Empty);

        var builder = new StringBuilder(maxLength);
        for (int i = 0; i < count; i++)
        {
            builder.Clear();
            int length = random.Next(1, maxLength + 1);
            for (int c = 0; c < length; c++)
                builder.Append((char)('a' + random.Next(alphabet)));
            keys.Add(builder.ToString());
        }

        // Duplicates are left in deliberately: the constructor's documented last-one-wins rule is part of what
        // the oracle below reconciles, and a generated set of short keys over a narrow alphabet produces them.
        return keys;
    }

    private static string[] ShortBinaryStrings()
    {
        var strings = new List<string> { string.Empty };
        for (int length = 1; length <= 3; length++)
        {
            for (int bits = 0; bits < 1 << length; bits++)
            {
                var text = new StringBuilder(length);
                for (int bit = length - 1; bit >= 0; bit--)
                    text.Append((bits >> bit & 1) == 0 ? 'a' : 'b');
                strings.Add(text.ToString());
            }
        }

        return strings.ToArray();
    }
}
