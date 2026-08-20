using System.Text;
using Celerity.Collections;
using CsCheck;

namespace Celerity.Tests.Collections;

/// <summary>
/// Randomized reconciliation of <see cref="SuffixArray"/> against the naive answers it replaces: the suffixes
/// sorted with <see cref="StringComparer.Ordinal"/>, the longest common prefixes measured character by
/// character, and every query resolved by comparing the pattern at every position with
/// <see cref="string.CompareOrdinal(string, int, string, int, int)"/>.
///
/// <para>
/// The build is what is on trial. Prefix doubling reaches its answer through <c>log n</c> rounds of counting
/// sort over ranks it computed in the previous round, so a single misplaced bucket boundary or a shift wrapped
/// the wrong way past the sentinel does not fail loudly — it returns a <i>plausible</i> order in which a
/// handful of suffixes sit one position out, which every query then reports confidently. A hand-written
/// fixture is badly suited to catching that: the shapes that expose it are texts whose suffixes agree for long
/// stretches and part late, which is precisely what an example is not written to contain.
/// </para>
///
/// <para>
/// Three layers, narrowest first. The CsCheck property generates the text from its own axes — length and
/// alphabet width — so a disagreement shrinks to a minimal reproduction with the seed printed; a two-letter
/// alphabet is the adversarial end, since every suffix then shares a long prefix with several others. The
/// seeded theory below it drives texts long enough that the doubling runs several more rounds than any short
/// case reaches. The exhaustive sweep at the end checks <i>every</i> string over a two-letter alphabet up to
/// length nine — 1,022 of them — which is the only layer that can prove no case was merely missed by sampling.
/// </para>
///
/// <para>
/// The quadratic oracles (every pattern against every position, and every pair of suffixes for the longest
/// repeat) run only on the short texts, where they are affordable and independent of the structure under
/// test. The long seeded texts keep the full order and longest-common-prefix reconciliation — which is where a
/// build bug would show — and sample their patterns.
/// </para>
/// </summary>
public class SuffixArrayDifferentialTests
{
    // Above this length the pattern sweep is sampled and the pairwise longest-repeat oracle is replaced by the
    // maximum of the independently measured longest-common-prefix array.
    private const int ExhaustiveTextLength = 64;

    // Length and alphabet width are the two axes that decide which build paths a case can reach: a wide
    // alphabet separates most suffixes in the first ranking pass and skips the doubling rounds entirely, while
    // a two-letter one forces the maximum number of them.
    private static readonly Gen<(int Length, int Alphabet, uint Seed)> GenTexts =
        Gen.Select(Gen.Int[0, 60], Gen.Int[1, 6], Gen.UInt);

    [Fact]
    public void EveryQuery_ShouldMatchTheNaiveAnswer_UnderGeneratedTexts()
    {
        GenTexts.Sample(spec => AssertAgreesWithOracle(BuildText(spec.Length, spec.Alphabet, spec.Seed)), iter: 250);
    }

    [Theory]
    [InlineData(500, 2, 1u)]
    [InlineData(500, 26, 2u)]
    [InlineData(1000, 4, 3u)]
    [InlineData(2000, 2, 4u)]
    public void EveryQuery_ShouldMatchTheNaiveAnswer_OnLongerSeededTexts(int length, int alphabet, uint seed)
    {
        AssertAgreesWithOracle(BuildText(length, alphabet, seed));
    }

    [Fact]
    public void EveryQuery_ShouldMatchTheNaiveAnswer_OnEveryShortBinaryText()
    {
        // Every string over {a, b} up to length nine, which is long enough that the doubling runs four rounds.
        for (int length = 0; length <= 9; length++)
        {
            for (int bits = 0; bits < 1 << length; bits++)
            {
                var text = new StringBuilder(length);
                for (int bit = 0; bit < length; bit++)
                    text.Append((bits >> bit & 1) == 0 ? 'a' : 'b');

                AssertAgreesWithOracle(text.ToString());
            }
        }
    }

    [Fact]
    public void EveryQuery_ShouldMatchTheNaiveAnswer_OnTextsWithCharactersAboveTheAsciiRange()
    {
        // Ordinal ordering is over UTF-16 code units, so the oracle and the index must agree on characters
        // whose value exceeds anything a byte-oriented implementation would have handled.
        AssertAgreesWithOracle("ŁézŁaéŁz￿a bŁ");
    }

    private static void AssertAgreesWithOracle(string text)
    {
        var index = new SuffixArray(text);

        Assert.Equal(text, index.Text.ToString());
        Assert.Equal(text.Length, index.Length);

        // The order itself, against the suffixes sorted as strings.
        int[] expectedOrder = [.. Enumerable.Range(0, text.Length).OrderBy(start => text[start..], StringComparer.Ordinal)];
        Assert.Equal(expectedOrder, index.Suffixes.ToArray());

        // The longest-common-prefix array, measured character by character rather than by Kasai's shortcut.
        int[] expectedLcp = new int[text.Length];
        for (int rank = 1; rank < text.Length; rank++)
            expectedLcp[rank] = CommonPrefixLength(text, expectedOrder[rank - 1], expectedOrder[rank]);

        Assert.Equal(expectedLcp, index.LongestCommonPrefixes.ToArray());

        AssertQueriesAgree(index, text);
        AssertLongestRepeatAgrees(index, text, expectedOrder, expectedLcp);
    }

    private static void AssertQueriesAgree(SuffixArray index, string text)
    {
        foreach (string pattern in Patterns(text))
        {
            int[] expected = [.. NaiveOccurrences(text, pattern)];

            Assert.Equal(expected.Length, index.CountOccurrences(pattern));
            Assert.Equal(expected.Length > 0, index.Contains(pattern));
            Assert.Equal(expected.Length > 0 ? expected[0] : -1, index.IndexOf(pattern));
            Assert.Equal(expected, index.GetOccurrences(pattern));

            Assert.Equal(expected.Length > 0, index.TryGetOccurrences(pattern, out ReadOnlySpan<int> slice));
            Assert.Equal(expected, slice.ToArray().Order());

            int[] destination = new int[expected.Length];
            Assert.Equal(expected.Length, index.CopyOccurrences(pattern, destination));
            Assert.Equal(expected, destination);
        }
    }

    private static void AssertLongestRepeatAgrees(SuffixArray index, string text, int[] expectedOrder, int[] expectedLcp)
    {
        // On a short text the oracle is the pairwise one — the longest prefix shared by any two distinct
        // suffixes — which owes nothing to the structure under test. On a long one that is quadratic, so the
        // answer comes from the independently measured longest-common-prefix array instead.
        int expected = 0;
        if (text.Length <= ExhaustiveTextLength)
        {
            for (int a = 0; a < text.Length; a++)
            {
                for (int b = a + 1; b < text.Length; b++)
                    expected = Math.Max(expected, CommonPrefixLength(text, a, b));
            }
        }
        else
        {
            foreach (int shared in expectedLcp)
                expected = Math.Max(expected, shared);
        }

        Assert.Equal(expected > 0, index.TryGetLongestRepeatedSubstring(out int start, out int length));
        Assert.Equal(expected, length);
        if (expected == 0)
            return;

        Assert.True(index.CountOccurrences(text.AsSpan(start, length)) >= 2);

        // Which of several tied repeats is reported is documented, so it is pinned rather than left to the
        // length alone: the earliest rank reaching the maximum is the lexicographically smallest of them, and
        // this reads that off the independently sorted order and the independently measured prefix lengths.
        int smallest = Array.IndexOf(expectedLcp, expected);
        Assert.Equal(text.Substring(expectedOrder[smallest], expected), text.Substring(start, length));
    }

    // Every pattern worth asking about: the empty one, substrings of the text up to a bounded length, and near
    // misses that are absent — a substring with its last character bumped, and one extended past the text.
    private static IEnumerable<string> Patterns(string text)
    {
        yield return string.Empty;
        yield return " ";
        yield return "zzz";

        // A long text is sampled rather than swept: the sweep is quadratic in the text length and the order
        // reconciliation above is what a build bug would fail, not the hundredth substring.
        int step = text.Length <= ExhaustiveTextLength ? 1 : text.Length / 32;
        int longest = Math.Min(text.Length, 5);

        for (int start = 0; start < text.Length; start += step)
        {
            for (int length = 1; length <= longest && start + length <= text.Length; length++)
            {
                string pattern = text.Substring(start, length);
                yield return pattern;
                yield return pattern + 'q';
                yield return string.Concat(pattern[..^1], (char)(pattern[^1] + 1));
            }
        }
    }

    private static IEnumerable<int> NaiveOccurrences(string text, string pattern)
    {
        // The empty pattern matches at every start position, which is the documented rule and the one
        // string.IndexOf does not follow.
        if (pattern.Length == 0)
        {
            for (int position = 0; position < text.Length; position++)
                yield return position;

            yield break;
        }

        for (int position = 0; position + pattern.Length <= text.Length; position++)
        {
            if (string.CompareOrdinal(text, position, pattern, 0, pattern.Length) == 0)
                yield return position;
        }
    }

    private static int CommonPrefixLength(string text, int first, int second)
    {
        int matched = 0;
        while (first + matched < text.Length && second + matched < text.Length &&
               text[first + matched] == text[second + matched])
        {
            matched++;
        }

        return matched;
    }

    private static string BuildText(int length, int alphabet, uint seed)
    {
        var rand = new Random((int)seed);
        var text = new StringBuilder(length);
        for (int i = 0; i < length; i++)
            text.Append((char)('a' + rand.Next(alphabet)));

        return text.ToString();
    }
}
