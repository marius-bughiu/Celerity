using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Deterministic, seeded differential coverage for <see cref="Trie{TValue}"/>. Each seed drives the same
/// random stream of inserts, overwrites, removes, and lookups into the trie and into an independent
/// <see cref="SortedDictionary{TKey, TValue}"/> oracle keyed by <see cref="StringComparer.Ordinal"/> (whose
/// iteration order is exactly the trie's ascending-ordinal order), then asserts after every operation that the
/// two agree on count, per-key membership and value, the full ordered entry sequence, prefix enumeration, and
/// the longest-prefix match. Keys are drawn from a tiny alphabet with short lengths so prefixes collide
/// heavily — the case that exercises node sharing and bottom-up removal pruning.
/// </summary>
public class TrieDifferentialTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(1234)]
    public void RandomizedOperations_MatchSortedDictionaryOracle(int seed)
    {
        var rand = new Random(seed);
        var trie = new Trie<int>();
        var oracle = new SortedDictionary<string, int>(StringComparer.Ordinal);

        const int Steps = 4000;
        for (int step = 0; step < Steps; step++)
        {
            string key = RandomKey(rand);
            int op = rand.Next(100);

            if (op < 45)
            {
                // Insert-or-overwrite via the indexer / TryAdd, mirroring the oracle.
                int value = rand.Next(1_000_000);
                if (rand.Next(2) == 0)
                {
                    trie[key] = value;
                    oracle[key] = value;
                }
                else
                {
                    bool trieAdded = trie.TryAdd(key, value);
                    bool oracleAdded = !oracle.ContainsKey(key);
                    if (oracleAdded)
                        oracle[key] = value;
                    Assert.Equal(oracleAdded, trieAdded);
                }
            }
            else if (op < 65)
            {
                bool trieRemoved = trie.Remove(key, out int removedValue);
                bool oracleRemoved = oracle.TryGetValue(key, out int oracleValue);
                Assert.Equal(oracleRemoved, trieRemoved);
                if (oracleRemoved)
                {
                    Assert.Equal(oracleValue, removedValue);
                    oracle.Remove(key);
                }
            }
            else if (op < 80)
            {
                Assert.Equal(oracle.ContainsKey(key), trie.ContainsKey(key));
                Assert.Equal(oracle.TryGetValue(key, out int expected), trie.TryGetValue(key, out int actual));
                if (oracle.ContainsKey(key))
                    Assert.Equal(expected, actual);
            }
            else if (op < 90)
            {
                // Prefix enumeration against the filtered, ordered oracle.
                string prefix = key.Length == 0 ? key : key.Substring(0, rand.Next(key.Length + 1));
                string[] expected = oracle
                    .Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal))
                    .Select(kv => kv.Key)
                    .ToArray();
                string[] actual = trie.GetKeysWithPrefix(prefix).ToArray();
                Assert.Equal(expected, actual);
                Assert.Equal(expected.Length > 0, trie.ContainsPrefix(prefix));
            }
            else
            {
                // Longest-prefix match against the oracle's longest stored prefix of the query.
                string query = RandomKey(rand);
                string? bestKey = oracle.Keys
                    .Where(k => query.StartsWith(k, StringComparison.Ordinal))
                    .OrderByDescending(k => k.Length)
                    .FirstOrDefault();

                bool trieHit = trie.TryGetLongestPrefix(query, out string trieKey, out int trieValue);
                Assert.Equal(bestKey is not null, trieHit);
                if (bestKey is not null)
                {
                    Assert.Equal(bestKey, trieKey);
                    Assert.Equal(oracle[bestKey], trieValue);
                }
            }

            Assert.Equal(oracle.Count, trie.Count);
        }

        // Final full-sequence reconciliation of keys and values in order.
        Assert.Equal(oracle.Keys.ToArray(), trie.Keys.ToArray());
        Assert.Equal(oracle.Values.ToArray(), trie.Values.ToArray());
    }

    // Draws a short key from a 4-symbol alphabet (plus the occasional empty string) so keys collide on prefixes
    // often, stressing shared interior nodes and the removal pruning path.
    private static string RandomKey(Random rand)
    {
        const string Alphabet = "abc-";
        int length = rand.Next(0, 5);
        if (length == 0)
            return string.Empty;

        Span<char> chars = stackalloc char[length];
        for (int i = 0; i < length; i++)
            chars[i] = Alphabet[rand.Next(Alphabet.Length)];
        return new string(chars);
    }
}
