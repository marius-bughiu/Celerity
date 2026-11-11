using System.Text;
using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Randomized reconciliation of <see cref="EditDistanceIndex"/> against the answer it replaces: a full
/// Levenshtein table between the query and <i>every</i> word, with no length filter, no early exit and no index —
/// the slowest correct thing there is, which is what makes it a trustworthy oracle.
///
/// <para>
/// Exactness is what is on trial. The index finds a word only if the word and the query share a deletion
/// variant that both sides generated and hashed alike, so a walk that skips a subset of positions, a level that
/// reads a buffer its child has overwritten, or a lookup that stops one posting early all lose matches
/// <i>silently</i> — every reported distance would still be right. Only an oracle that enumerates the whole
/// word set can see a word that is missing. Short words over a two- to four-letter alphabet make that the
/// common case rather than a rarity: most pairs are then within two edits, many words repeat a letter (so their
/// variants collapse), and empty and one-character words, whose deletion variants include the empty string,
/// turn up constantly.
/// </para>
///
/// <para>
/// Every query tier is reconciled against the one oracle list, ordered as the type documents it — ascending
/// distance, then ascending id — including the tie rule <see cref="EditDistanceIndex.TryFindNearest"/> builds
/// on, and every query distance from zero up to the one the index was built for, since asking for fewer
/// deletions than were indexed is a separate exactness claim.
/// </para>
///
/// <para>
/// Three layers, narrowest first: a CsCheck property over generated word sets and queries, which shrinks a
/// disagreement to a minimal reproduction with the seed printed; seeded cases with thousands of words, where
/// the directory has thousands of buckets and a posting run can straddle the scan's comparison in both
/// directions; and an exhaustive sweep of every query over a two-letter alphabet up to length six against a
/// fixed word set, which is the only layer that can prove no case was merely missed by sampling.
/// </para>
/// </summary>
public class EditDistanceIndexDifferentialTests
{
    private static readonly Gen<(int WordCount, int MaxWordLength, int MaxDistance, int Alphabet, uint Seed)> GenCases =
        Gen.Select(Gen.Int[0, 16], Gen.Int[0, 7], Gen.Int[0, 3], Gen.Int[1, 4], Gen.UInt);

    [Fact]
    public void EveryQuery_ShouldMatchTheNaiveAnswer_UnderGeneratedWordSetsAndQueries()
    {
        GenCases.Sample(
            spec =>
            {
                string[] words = BuildWords(spec.WordCount, spec.MaxWordLength, spec.Alphabet, spec.Seed);
                string[] queries = BuildWords(6, spec.MaxWordLength + 2, spec.Alphabet, spec.Seed * 2654435761u + 1u);
                AssertAgreesWithOracle(words, spec.MaxDistance, queries);
            },
            iter: 300);
    }

    [Theory]
    [InlineData(2000, 8, 1, 4, 1u)]
    [InlineData(2000, 8, 2, 3, 2u)]
    [InlineData(500, 12, 3, 2, 3u)]
    [InlineData(3000, 10, 2, 26, 4u)]
    public void EveryQuery_ShouldMatchTheNaiveAnswer_OnLargerSeededCases(
        int wordCount, int maxWordLength, int maxDistance, int alphabet, uint seed)
    {
        string[] words = BuildWords(wordCount, maxWordLength, alphabet, seed);

        // Half the queries are words one random substitution away from an indexed word, which is the shape a
        // spell checker sees and the one that lands on dense posting runs; the other half are unrelated.
        var rng = new Random((int)seed + 7);
        string[] queries = new string[40];
        for (int i = 0; i < queries.Length; i++)
        {
            if (i % 2 == 0)
            {
                char[] word = words[rng.Next(words.Length)].ToCharArray();
                if (word.Length > 0)
                    word[rng.Next(word.Length)] = (char)('a' + rng.Next(alphabet));

                queries[i] = new string(word);
            }
            else
            {
                queries[i] = BuildWords(1, maxWordLength, alphabet, (uint)rng.Next())[0];
            }
        }

        AssertAgreesWithOracle(words, maxDistance, queries);
    }

    [Fact]
    public void EveryQuery_ShouldMatchTheNaiveAnswer_OnEveryShortBinaryQuery()
    {
        // Repeated letters (whose variants collapse), the empty word, and words of every length from zero to
        // five, so that a query of any length up to six has words at every distance from it.
        string[] words = ["", "a", "b", "aa", "ab", "ba", "aab", "abab", "bbbb", "aabba", "babab"];

        List<string> queries = [];
        for (int length = 0; length <= 6; length++)
            queries.AddRange(BinaryWords(length));

        AssertAgreesWithOracle(words, 2, [.. queries]);
    }

    private static void AssertAgreesWithOracle(string[] words, int maxDistance, string[] queries)
    {
        var index = new EditDistanceIndex(words, maxDistance);

        string[] distinct = [.. words.Distinct(StringComparer.Ordinal)];
        Assert.Equal(distinct, index.Words.ToArray());

        foreach (string query in queries)
        {
            for (int distance = 0; distance <= maxDistance; distance++)
            {
                FuzzyMatch[] expected = NaiveMatches(distinct, query, distance);

                Assert.Equal(expected, index.FindWithin(query, distance));
                Assert.Equal(expected.Length, index.CountWithin(query, distance));
                Assert.Equal(expected.Length > 0, index.ContainsWithin(query, distance));

                Assert.Equal(expected.Length > 0, index.TryFindNearest(query, distance, out FuzzyMatch nearest));
                if (expected.Length > 0)
                    Assert.Equal(expected[0], nearest);
                else
                    Assert.Equal(default, nearest);

                var copied = new FuzzyMatch[expected.Length];
                Assert.Equal(expected.Length, index.CopyWithin(query, distance, copied));
                Assert.Equal(expected, copied);
            }
        }
    }

    // Every word scored with a full table, then ordered the way the index documents: ascending distance, then
    // ascending id. Ids are distinct, so the order is total.
    private static FuzzyMatch[] NaiveMatches(string[] words, string query, int maxDistance)
    {
        List<FuzzyMatch> found = [];
        for (int id = 0; id < words.Length; id++)
        {
            int distance = Levenshtein(query, words[id]);
            if (distance <= maxDistance)
                found.Add(new FuzzyMatch(id, distance));
        }

        return [.. found.OrderBy(match => match.Distance).ThenBy(match => match.WordId)];
    }

    internal static int Levenshtein(string a, string b)
    {
        int[,] table = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++)
            table[i, 0] = i;

        for (int j = 0; j <= b.Length; j++)
            table[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int substitution = table[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                table[i, j] = Math.Min(substitution, Math.Min(table[i - 1, j] + 1, table[i, j - 1] + 1));
            }
        }

        return table[a.Length, b.Length];
    }

    private static string[] BuildWords(int count, int maxLength, int alphabet, uint seed)
    {
        var rng = new Random((int)seed);
        var words = new string[count];
        for (int i = 0; i < count; i++)
        {
            int length = rng.Next(maxLength + 1);
            var builder = new StringBuilder(length);
            for (int c = 0; c < length; c++)
                builder.Append((char)('a' + rng.Next(alphabet)));

            words[i] = builder.ToString();
        }

        return words;
    }

    private static IEnumerable<string> BinaryWords(int length)
    {
        int cases = 1 << length;
        for (int mask = 0; mask < cases; mask++)
        {
            var builder = new StringBuilder(length);
            for (int bit = 0; bit < length; bit++)
                builder.Append((mask & (1 << bit)) == 0 ? 'a' : 'b');

            yield return builder.ToString();
        }
    }
}
