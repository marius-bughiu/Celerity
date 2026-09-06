using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Property-based differential coverage for <see cref="Trie{TValue}"/> against an independent
/// <see cref="SortedDictionary{TKey, TValue}"/> oracle keyed by <see cref="StringComparer.Ordinal"/>
/// — whose iteration order is exactly the trie's ascending-ordinal order. CsCheck generates the
/// stream of inserts, overwrites, removes, lookups, prefix enumerations and longest-prefix matches,
/// and asserts after every operation that the two agree on count, per-key membership and value, the
/// filtered prefix set, and the longest stored prefix of a query; the full ordered key and value
/// sequences are reconciled once the stream is done.
///
/// <para>
/// Keys are drawn from a four-symbol alphabet at lengths up to four, so prefixes collide heavily —
/// that is the case that exercises shared interior nodes and the bottom-up removal pruning, and a
/// wider alphabet would spend the whole run on disjoint singleton branches.
/// </para>
///
/// <para>
/// Every key in the script is generated data rather than a draw from a seeded <c>Random</c>, so a
/// divergence shrinks to the few keys that caused it — including the empty key, which is both a
/// valid entry and the prefix of everything, and which CsCheck reaches by shrinking rather than
/// by the alphabet needing a special case.
/// </para>
/// </summary>
public class TrieDifferentialTests
{
    private enum Op { IndexerSet, TryAdd, Remove, Lookup, PrefixScan, LongestPrefix }

    // A four-symbol alphabet at lengths 0..4: 341 possible keys, so a few hundred operations
    // revisit the same interior nodes many times over.
    private static readonly Gen<char> GenSymbol = Gen.Int[0, 3].Select(i => "abc-"[i]);

    private static readonly Gen<string> GenKey = Gen.String[GenSymbol, 0, 4];

    private static readonly Gen<Op> GenKind =
        Gen.Int[0, 99].Select(n => n < 22 ? Op.IndexerSet
                                 : n < 45 ? Op.TryAdd
                                 : n < 65 ? Op.Remove
                                 : n < 80 ? Op.Lookup
                                 : n < 90 ? Op.PrefixScan
                                 : Op.LongestPrefix);

    // `Query` is a second, independent key: the longest-prefix match asks about a string that need
    // not be stored, and `PrefixLength` truncates `Key` to a proper prefix of itself.
    private static readonly Gen<(Op Kind, string Key, int Value, string Query, int PrefixLength)> GenOp =
        Gen.Select(GenKind, GenKey, Gen.Int[0, 1_000_000], GenKey, Gen.Int[0, 4]);

    [Fact]
    public void Trie_ShouldMatch_ASortedDictionary()
    {
        GenOp.List[0, 400].Sample(ops =>
        {
            var trie = new Trie<int>();
            var oracle = new SortedDictionary<string, int>(StringComparer.Ordinal);

            foreach (var (kind, key, value, query, prefixLength) in ops)
            {
                switch (kind)
                {
                    case Op.IndexerSet:
                        trie[key] = value;
                        oracle[key] = value;
                        break;

                    case Op.TryAdd:
                    {
                        bool oracleAdded = !oracle.ContainsKey(key);
                        Assert.Equal(oracleAdded, trie.TryAdd(key, value));
                        if (oracleAdded)
                            oracle[key] = value;
                        break;
                    }

                    case Op.Remove:
                    {
                        bool trieRemoved = trie.Remove(key, out int removedValue);
                        bool oracleRemoved = oracle.TryGetValue(key, out int oracleValue);
                        Assert.Equal(oracleRemoved, trieRemoved);
                        if (oracleRemoved)
                        {
                            Assert.Equal(oracleValue, removedValue);
                            oracle.Remove(key);
                        }
                        break;
                    }

                    case Op.Lookup:
                    {
                        bool present = oracle.TryGetValue(key, out int expected);
                        Assert.Equal(present, trie.ContainsKey(key));
                        Assert.Equal(present, trie.TryGetValue(key, out int actual));
                        if (present)
                            Assert.Equal(expected, actual);
                        break;
                    }

                    case Op.PrefixScan:
                    {
                        // Prefix enumeration against the filtered, ordered oracle.
                        string prefix = key[..Math.Min(prefixLength, key.Length)];
                        string[] expected = oracle
                            .Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal))
                            .Select(kv => kv.Key)
                            .ToArray();

                        Assert.Equal(expected, trie.GetKeysWithPrefix(prefix).ToArray());
                        Assert.Equal(expected.Length > 0, trie.ContainsPrefix(prefix));
                        break;
                    }

                    case Op.LongestPrefix:
                    {
                        // The oracle's longest stored prefix of the query, found the slow way.
                        string? bestKey = oracle.Keys
                            .Where(k => query.StartsWith(k, StringComparison.Ordinal))
                            .OrderByDescending(k => k.Length)
                            .FirstOrDefault();

                        bool trieHit = trie.TryGetLongestPrefix(query, out string? trieKey, out int trieValue);
                        Assert.Equal(bestKey is not null, trieHit);
                        if (bestKey is not null)
                        {
                            Assert.Equal(bestKey, trieKey);
                            Assert.Equal(oracle[bestKey], trieValue);
                        }
                        break;
                    }
                }

                Assert.Equal(oracle.Count, trie.Count);
            }

            // Final full-sequence reconciliation of keys and values in order.
            Assert.Equal(oracle.Keys.ToArray(), trie.Keys.ToArray());
            Assert.Equal(oracle.Values.ToArray(), trie.Values.ToArray());
        }, iter: 40);
    }
}
