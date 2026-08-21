using System.Text;
using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Randomized reconciliation of <see cref="AhoCorasick"/> against the answer it replaces: every pattern tested
/// at every position of the text with <see cref="string.CompareOrdinal(string, int, string, int, int)"/>, which
/// is the <c>O(k · n · m)</c> loop a caller writes when they have not heard of the algorithm.
///
/// <para>
/// The failure links are what is on trial, and they are hard to test by example for the same reason they are
/// easy to get wrong: an automaton whose links are subtly mis-set still finds most matches. It finds every
/// pattern that begins where the scan happens to be at the root, and it loses only the ones that begin
/// <i>inside</i> a partial match of another pattern — which a hand-written fixture contains only if it was
/// written to. Generating patterns from a two- or three-letter alphabet makes overlap the common case rather
/// than a rarity: nearly every pattern is then a suffix of some prefix of another.
/// </para>
///
/// <para>
/// The oracle checks more than the set of matches. It checks their <b>order</b> — ascending end position,
/// longest first among matches ending together — because the order is documented behaviour that
/// <see cref="AhoCorasick.TryFindFirst"/> and <see cref="AhoCorasick.CopyMatches"/> both build on, and a
/// reversed output chain would leave the set of matches exactly right. It also reconciles every other query
/// tier against that same list, since <see cref="AhoCorasick.ContainsAny"/>,
/// <see cref="AhoCorasick.CountMatches"/> and the copying tier each walk the chain differently.
/// </para>
///
/// <para>
/// Three layers, narrowest first: a CsCheck property over generated pattern sets and texts, which shrinks a
/// disagreement to a minimal reproduction with the seed printed; seeded cases an order of magnitude longer,
/// where a link that is wrong only at depth has room to show; and an exhaustive sweep of every text over a
/// two-letter alphabet up to length ten against a fixed overlapping pattern set, which is the only layer that
/// can prove no case was merely missed by sampling.
/// </para>
/// </summary>
public class AhoCorasickDifferentialTests
{
    // Patterns short enough that a random text hits them often, over an alphabet narrow enough that they
    // overlap constantly. Both are what make the failure links matter.
    private static readonly Gen<(int PatternCount, int MaxPatternLength, int TextLength, int Alphabet, uint Seed)> GenCases =
        Gen.Select(Gen.Int[0, 12], Gen.Int[1, 6], Gen.Int[0, 120], Gen.Int[1, 4], Gen.UInt);

    [Fact]
    public void EveryQuery_ShouldMatchTheNaiveAnswer_UnderGeneratedPatternSetsAndTexts()
    {
        GenCases.Sample(
            spec => AssertAgreesWithOracle(
                BuildPatterns(spec.PatternCount, spec.MaxPatternLength, spec.Alphabet, spec.Seed),
                BuildText(spec.TextLength, spec.Alphabet, spec.Seed * 2654435761u + 1u)),
            iter: 300);
    }

    [Theory]
    [InlineData(64, 8, 4000, 2, 1u)]
    [InlineData(256, 6, 4000, 3, 2u)]
    [InlineData(16, 24, 4000, 4, 3u)]
    [InlineData(200, 12, 8000, 26, 4u)]
    public void EveryQuery_ShouldMatchTheNaiveAnswer_OnLongerSeededCases(
        int patternCount, int maxPatternLength, int textLength, int alphabet, uint seed)
    {
        AssertAgreesWithOracle(
            BuildPatterns(patternCount, maxPatternLength, alphabet, seed),
            BuildText(textLength, alphabet, seed * 2654435761u + 1u));
    }

    [Fact]
    public void EveryQuery_ShouldMatchTheNaiveAnswer_OnEveryShortBinaryText()
    {
        // An overlapping set over {a, b}: "a" is a suffix of "ba" and a prefix of "ab", "aba" fails into
        // itself, and "abab" is the case where a partial match has to resume two characters in.
        string[] patterns = ["a", "ab", "ba", "aba", "abab", "bb"];

        for (int length = 0; length <= 10; length++)
        {
            foreach (string text in BinaryTexts(length))
                AssertAgreesWithOracle(patterns, text);
        }
    }

    private static void AssertAgreesWithOracle(string[] patterns, string text)
    {
        var automaton = new AhoCorasick(patterns);

        string[] distinct = [.. patterns.Distinct(StringComparer.Ordinal)];
        Assert.Equal(distinct, automaton.Patterns.ToArray());

        PatternMatch[] expected = NaiveMatches(distinct, text);
        PatternMatch[] actual = automaton.FindAll(text);

        Assert.Equal(expected, actual);
        Assert.Equal(expected.Length, automaton.CountMatches(text));
        Assert.Equal(expected.Length > 0, automaton.ContainsAny(text));

        Assert.Equal(expected.Length > 0, automaton.TryFindFirst(text, out PatternMatch first));
        if (expected.Length > 0)
            Assert.Equal(expected[0], first);

        var copied = new PatternMatch[expected.Length];
        Assert.Equal(expected.Length, automaton.CopyMatches(text, copied));
        Assert.Equal(expected, copied);

        // Every match has to name a range that really holds its pattern, which the set comparison above would
        // not catch if the oracle and the automaton agreed on a wrong length.
        foreach (PatternMatch match in actual)
        {
            Assert.Equal(automaton[match.PatternId].Length, match.Length);
            Assert.Equal(automaton[match.PatternId], text.Substring(match.Start, match.Length));
        }
    }

    // Every pattern tested at every position, then ordered the way the automaton documents: ascending end
    // position, longest first among the matches that end together. Two distinct patterns cannot tie on both,
    // so the order is total.
    private static PatternMatch[] NaiveMatches(string[] patterns, string text)
    {
        List<PatternMatch> found = [];
        for (int id = 0; id < patterns.Length; id++)
        {
            string pattern = patterns[id];
            for (int start = 0; start + pattern.Length <= text.Length; start++)
            {
                if (string.CompareOrdinal(text, start, pattern, 0, pattern.Length) == 0)
                    found.Add(new PatternMatch(id, start, pattern.Length));
            }
        }

        return [.. found.OrderBy(match => match.End).ThenByDescending(match => match.Length)];
    }

    private static string[] BuildPatterns(int count, int maxLength, int alphabet, uint seed)
    {
        var rng = new Random((int)seed);
        var patterns = new string[count];
        for (int i = 0; i < count; i++)
        {
            int length = 1 + rng.Next(maxLength);
            var builder = new StringBuilder(length);
            for (int c = 0; c < length; c++)
                builder.Append((char)('a' + rng.Next(alphabet)));

            patterns[i] = builder.ToString();
        }

        return patterns;
    }

    private static string BuildText(int length, int alphabet, uint seed)
    {
        var rng = new Random((int)seed);
        var builder = new StringBuilder(length);
        for (int i = 0; i < length; i++)
            builder.Append((char)('a' + rng.Next(alphabet)));

        return builder.ToString();
    }

    private static IEnumerable<string> BinaryTexts(int length)
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
