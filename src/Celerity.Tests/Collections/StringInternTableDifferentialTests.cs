using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Collections;

/// <summary>
/// Deterministic, seeded differential coverage for <see cref="StringInternTable{THasher}"/>. Each seed
/// drives the same random stream of span interns, string interns, lookups, and clears into the table and
/// into an independent <see cref="Dictionary{TKey, TValue}"/> oracle keyed by
/// <see cref="StringComparer.Ordinal"/>, then asserts after every operation that the two agree on count,
/// per-token membership, and — the property that makes the type worth having — that the table hands back
/// the <em>same reference</em> for every repeat of a token it has already seen.
/// </summary>
/// <remarks>
/// Tokens are drawn from a tiny alphabet at short lengths so hash collisions and probe chains are dense,
/// and the run interleaves the span and string entry points so a divergence between them surfaces.
/// </remarks>
public class StringInternTableDifferentialTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(1234)]
    public void RandomizedOperations_MatchDictionaryOracle_WithWeakHasher(int seed) =>
        RunCase<StringFnV1AHasher>(seed);

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(1234)]
    public void RandomizedOperations_MatchDictionaryOracle_WithStrongHasher(int seed) =>
        RunCase<StringXxHash3Hasher>(seed);

    private static void RunCase<THasher>(int seed)
        where THasher : struct, IHashProvider<string>, ISpanHashProvider
    {
        var rand = new Random(seed);
        var table = new StringInternTable<THasher>(capacity: 2);

        // The oracle maps a token's contents to the canonical instance the table returned first.
        var oracle = new Dictionary<string, string>(StringComparer.Ordinal);

        const int Steps = 4000;
        for (int step = 0; step < Steps; step++)
        {
            string token = RandomToken(rand);
            int op = rand.Next(100);

            if (op < 45)
            {
                // Intern from a span carved out of a larger buffer — the parser shape.
                string padded = "<<" + token + ">>";
                string interned = table.GetOrAdd(padded.AsSpan(2, token.Length));

                Assert.Equal(token, interned);
                if (oracle.TryGetValue(token, out string? canonical))
                    Assert.Same(canonical, interned);
                else
                    oracle[token] = interned;
            }
            else if (op < 70)
            {
                // Intern from a freshly allocated string: on a miss the supplied instance itself
                // becomes canonical; on a hit the already-held instance comes back.
                string supplied = new string(token.ToCharArray());
                string interned = table.GetOrAdd(supplied);

                if (oracle.TryGetValue(token, out string? canonical))
                {
                    Assert.Same(canonical, interned);
                }
                else
                {
                    Assert.Same(supplied, interned);
                    oracle[token] = interned;
                }
            }
            else if (op < 88)
            {
                // Pure lookup: never mutates, and agrees with the oracle on both entry points.
                bool expected = oracle.TryGetValue(token, out string? canonical);
                Assert.Equal(expected, table.TryGet(token.AsSpan(), out string? actual));
                Assert.Equal(expected, table.Contains(token.AsSpan()));
                Assert.Equal(expected, table.Contains(token));
                if (expected)
                    Assert.Same(canonical, actual);
                else
                    Assert.Null(actual);
            }
            else if (op < 97)
            {
                // Enumeration yields exactly the canonical instances, once each.
                var seen = new List<string>();
                foreach (string s in table)
                    seen.Add(s);

                Assert.Equal(oracle.Count, seen.Count);
                foreach (string s in seen)
                {
                    Assert.True(oracle.TryGetValue(s, out string? canonical));
                    Assert.Same(canonical, s);
                }
            }
            else
            {
                table.Clear();
                oracle.Clear();
            }

            Assert.Equal(oracle.Count, table.Count);
        }
    }

    // A tiny alphabet at short lengths, so tokens repeat constantly and probe chains stay dense.
    private static string RandomToken(Random rand)
    {
        const string Alphabet = "abcŁ";
        int length = rand.Next(0, 5);
        return string.Create(length, rand, static (span, rng) =>
        {
            for (int i = 0; i < span.Length; i++)
                span[i] = Alphabet[rng.Next(Alphabet.Length)];
        });
    }
}
