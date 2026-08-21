using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Behavioural coverage for <see cref="AhoCorasick"/>: the build — including the failure and output links that
/// are the whole algorithm — and every query tier: <see cref="AhoCorasick.ContainsAny"/>,
/// <see cref="AhoCorasick.CountMatches"/>, <see cref="AhoCorasick.TryFindFirst"/>,
/// <see cref="AhoCorasick.CopyMatches"/> and <see cref="AhoCorasick.FindAll"/>.
///
/// <para>
/// What decides whether the build is right is not whether a pattern is found — a broken automaton still finds
/// patterns that start where it happens to be looking. It is the three cases the failure links exist for: a
/// pattern that starts <i>inside</i> another (<c>"he"</c> inside <c>"she"</c>), a pattern that is a proper
/// suffix of another so both end at the same position (<c>"c"</c>, <c>"bc"</c>, <c>"abc"</c>), and a partial
/// match that dies and has to resume mid-way rather than from the root (<c>"abab"</c> against
/// <c>"ababab"</c>). Each is exercised on its own, because each fails independently: the first needs the
/// failure link, the second needs the output link chain, and the third needs the failure link to point at a
/// non-root state.
/// </para>
///
/// <para>
/// The layout has its own cases. Sibling edges are sorted during the flattening so a transition can binary
/// search, so a pattern set whose patterns arrive in descending first-character order is included to make that
/// sort actually move something. An output link that points at a state which is itself not terminal but has an
/// output link of its own is the branch a two-level suffix chain reaches and a one-level chain does not.
/// </para>
///
/// <para>
/// The randomized reconciliation against a naive every-pattern-at-every-position oracle lives in
/// <see cref="AhoCorasickDifferentialTests"/>, the <see cref="IReadOnlyList{T}"/> and match-enumerator surfaces
/// in <see cref="AhoCorasickEnumerationTests"/>, and the documented examples in
/// <see cref="AhoCorasickDocumentationExampleTests"/>.
/// </para>
/// </summary>
public class AhoCorasickTests
{
    // The textbook pattern set: "he" starts inside "she", "hers" starts where "he" does, and all three occur in
    // "ushers" with two of them overlapping.
    private static readonly string[] Textbook = ["he", "she", "his", "hers"];

    private static (string Pattern, int Start)[] Resolve(AhoCorasick automaton, IEnumerable<PatternMatch> matches) =>
        [.. matches.Select(match => (automaton[match.PatternId], match.Start))];

    // ---- construction ------------------------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldKeepEveryPatternInOrder_WhenThePatternsAreDistinct()
    {
        var automaton = new AhoCorasick(Textbook);

        Assert.Equal(4, automaton.Count);
        Assert.Equal(Textbook, automaton.Patterns.ToArray());
    }

    [Fact]
    public void Constructor_ShouldShareStatesAcrossACommonPrefix_WhenPatternsOverlap()
    {
        // h-e, h-e-r-s and h-i-s share the "h", and "hers" extends "he": 1 root + h, he, her, hers, hi, his,
        // s, sh, she = 10 states, against the 12 the pattern characters would cost unshared.
        var automaton = new AhoCorasick(Textbook);

        Assert.Equal(10, automaton.StateCount);
    }

    [Fact]
    public void Constructor_ShouldCollapseTheDuplicate_WhenAPatternIsSuppliedTwice()
    {
        var automaton = new AhoCorasick(["he", "she", "he"]);

        Assert.Equal(2, automaton.Count);
        Assert.Equal(["he", "she"], automaton.Patterns.ToArray());
    }

    [Fact]
    public void Constructor_ShouldMatchNothing_WhenThePatternSetIsEmpty()
    {
        var automaton = new AhoCorasick([]);

        Assert.Equal(0, automaton.Count);
        Assert.Equal(1, automaton.StateCount);
        Assert.False(automaton.ContainsAny("anything at all"));
        Assert.Equal(0, automaton.CountMatches("anything at all"));
    }

    [Fact]
    public void Constructor_ShouldReadTheSequenceOnce_WhenItIsNotAnArray()
    {
        // A List<string> takes the copying path that an array argument skips.
        var automaton = new AhoCorasick(new List<string> { "ab", "b" });

        Assert.Equal(2, automaton.Count);
        Assert.Equal(2, automaton.CountMatches("ab"));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenTheSequenceIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new AhoCorasick(null!));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenAPatternIsNull()
    {
        var thrown = Assert.Throws<ArgumentException>(() => new AhoCorasick(["he", null!]));

        Assert.Equal("patterns", thrown.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenAPatternIsEmpty()
    {
        var thrown = Assert.Throws<ArgumentException>(() => new AhoCorasick(["he", ""]));

        Assert.Equal("patterns", thrown.ParamName);
    }

    // ---- the indexer -------------------------------------------------------------------------------

    [Fact]
    public void Indexer_ShouldReturnThePattern_WhenTheIdIsInRange()
    {
        var automaton = new AhoCorasick(Textbook);

        Assert.Equal("he", automaton[0]);
        Assert.Equal("hers", automaton[3]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void Indexer_ShouldThrowArgumentOutOfRangeException_WhenTheIdIsOutsideTheSet(int patternId)
    {
        var automaton = new AhoCorasick(Textbook);

        Assert.Throws<ArgumentOutOfRangeException>(() => automaton[patternId]);
    }

    // ---- the failure links -------------------------------------------------------------------------

    [Fact]
    public void FindAll_ShouldReportEveryOverlappingMatch_WhenPatternsStartInsideOneAnother()
    {
        var automaton = new AhoCorasick(Textbook);

        Assert.Equal(
            [("she", 1), ("he", 2), ("hers", 2)],
            Resolve(automaton, automaton.FindAll("ushers")));
    }

    [Fact]
    public void FindAll_ShouldReportEveryPatternEndingAtAPosition_WhenOneIsASuffixOfAnother()
    {
        // "abc" ends at 3; so do its suffixes "bc" and "c". The output chain is what produces all three, and it
        // is walked longest first.
        var automaton = new AhoCorasick(["c", "bc", "abc"]);

        Assert.Equal(
            [("abc", 0), ("bc", 1), ("c", 2)],
            Resolve(automaton, automaton.FindAll("abc")));
    }

    [Fact]
    public void FindAll_ShouldFollowATwoLevelOutputChain_WhenTheFailureStateIsItselfNotTerminal()
    {
        // "abc" fails to "bc", which no pattern ends at — but "bc" fails to "c", which one does. Reaching "c"
        // from "abcd" needs the output link of a state whose own output is absent.
        var automaton = new AhoCorasick(["c", "bcd", "abcd"]);

        Assert.Equal(
            [("c", 2), ("abcd", 0), ("bcd", 1)],
            Resolve(automaton, automaton.FindAll("abcd")));
    }

    [Fact]
    public void FindAll_ShouldResumeMidPattern_WhenAPartialMatchFailsIntoItself()
    {
        // The failure link of "aba" is "a", so after the third character the automaton is three characters into
        // a fresh attempt rather than back at the root. Restarting from the root would find one match, not two.
        var automaton = new AhoCorasick(["abab"]);

        Assert.Equal([("abab", 0), ("abab", 2)], Resolve(automaton, automaton.FindAll("ababab")));
    }

    [Fact]
    public void FindAll_ShouldFallBackToTheRoot_WhenAPartialMatchDiesOnACharacterNoPatternUses()
    {
        var automaton = new AhoCorasick(["abc"]);

        Assert.Empty(automaton.FindAll("abx abz"));
        Assert.Equal([("abc", 4)], Resolve(automaton, automaton.FindAll("abx abc")));
    }

    [Fact]
    public void FindAll_ShouldOrderTheEdges_WhenThePatternsArriveInDescendingOrder()
    {
        // Sibling edges are threaded on in arrival order and sorted during the flattening; a descending set is
        // what makes that sort move something rather than confirm an order that was already right.
        var automaton = new AhoCorasick(["e", "d", "c", "b", "a"]);

        Assert.Equal(
            [("a", 0), ("b", 1), ("c", 2), ("d", 3), ("e", 4)],
            Resolve(automaton, automaton.FindAll("abcde")));
    }

    [Fact]
    public void FindAll_ShouldReturnAnEmptyArray_WhenNoPatternOccurs()
    {
        var automaton = new AhoCorasick(Textbook);

        Assert.Empty(automaton.FindAll("nothing to see"));
    }

    [Fact]
    public void FindAll_ShouldReturnAnEmptyArray_WhenTheTextIsEmpty()
    {
        var automaton = new AhoCorasick(Textbook);

        Assert.Empty(automaton.FindAll(default));
    }

    [Fact]
    public void FindAll_ShouldMatchAcrossTheWholeAlphabet_WhenPatternsUseCharactersOutsideAscii()
    {
        // Ordinal over UTF-16 code units: the edges are char-valued and nothing normalizes.
        var automaton = new AhoCorasick(["Łód", "ód"]);

        Assert.Equal([("Łód", 2), ("ód", 3)], Resolve(automaton, automaton.FindAll("w Łódź")));
    }

    [Fact]
    public void FindAll_ShouldNotMatch_WhenOnlyTheCaseDiffers()
    {
        var automaton = new AhoCorasick(["error"]);

        Assert.Empty(automaton.FindAll("ERROR"));
    }

    // ---- ContainsAny -------------------------------------------------------------------------------

    [Fact]
    public void ContainsAny_ShouldReturnTrue_WhenAPatternEndsAtTheCurrentState()
    {
        var automaton = new AhoCorasick(Textbook);

        Assert.True(automaton.ContainsAny("this is his book"));
    }

    [Fact]
    public void ContainsAny_ShouldReturnTrue_WhenTheMatchIsOnlyReachableThroughAnOutputLink()
    {
        // At "abc" no pattern ends, but "bc" does, and only the output link says so.
        var automaton = new AhoCorasick(["bc", "abcd"]);

        Assert.True(automaton.ContainsAny("abc"));
    }

    [Fact]
    public void ContainsAny_ShouldReturnFalse_WhenNoPatternOccurs()
    {
        var automaton = new AhoCorasick(Textbook);

        Assert.False(automaton.ContainsAny("nothing to see"));
    }

    [Fact]
    public void ContainsAny_ShouldReturnFalse_WhenTheTextIsEmpty()
    {
        var automaton = new AhoCorasick(Textbook);

        Assert.False(automaton.ContainsAny(default));
    }

    // ---- CountMatches ------------------------------------------------------------------------------

    [Fact]
    public void CountMatches_ShouldCountOverlappingOccurrences_WhenPatternsShareTheirText()
    {
        var automaton = new AhoCorasick(Textbook);

        Assert.Equal(3, automaton.CountMatches("ushers"));
    }

    [Fact]
    public void CountMatches_ShouldExceedTheTextLength_WhenEveryPositionEndsSeveralPatterns()
    {
        var automaton = new AhoCorasick(["a", "aa", "aaa"]);

        // Ending at 1: "a". At 2: "aa", "a". At 3: "aaa", "aa", "a".
        Assert.Equal(6, automaton.CountMatches("aaa"));
    }

    [Fact]
    public void CountMatches_ShouldReturnZero_WhenNoPatternOccurs()
    {
        var automaton = new AhoCorasick(Textbook);

        Assert.Equal(0, automaton.CountMatches("nothing to see"));
    }

    // ---- TryFindFirst ------------------------------------------------------------------------------

    [Fact]
    public void TryFindFirst_ShouldReturnTheEarliestEndingMatch_WhenSeveralPatternsOccur()
    {
        var automaton = new AhoCorasick(Textbook);

        Assert.True(automaton.TryFindFirst("ushers", out PatternMatch match));
        Assert.Equal("she", automaton[match.PatternId]);
        Assert.Equal(1, match.Start);
        Assert.Equal(4, match.End);
    }

    [Fact]
    public void TryFindFirst_ShouldPreferTheEarlierEnd_WhenAnotherMatchStartsEarlier()
    {
        // "bc" ends at 3 and "abcd" at 4, so "bc" is reported even though it starts a character later. A single
        // left-to-right pass cannot know "abcd" will complete.
        var automaton = new AhoCorasick(["bc", "abcd"]);

        Assert.True(automaton.TryFindFirst("abcd", out PatternMatch match));
        Assert.Equal("bc", automaton[match.PatternId]);
        Assert.Equal(1, match.Start);
    }

    [Fact]
    public void TryFindFirst_ShouldReturnTheLongest_WhenTwoMatchesEndTogether()
    {
        var automaton = new AhoCorasick(["c", "abc"]);

        Assert.True(automaton.TryFindFirst("abc", out PatternMatch match));
        Assert.Equal("abc", automaton[match.PatternId]);
    }

    [Fact]
    public void TryFindFirst_ShouldReturnFalse_WhenNoPatternOccurs()
    {
        var automaton = new AhoCorasick(Textbook);

        Assert.False(automaton.TryFindFirst("nothing to see", out PatternMatch match));
        Assert.Equal(default, match);
    }

    // ---- CopyMatches -------------------------------------------------------------------------------

    [Fact]
    public void CopyMatches_ShouldWriteEveryMatch_WhenTheBufferHasRoom()
    {
        var automaton = new AhoCorasick(Textbook);
        var destination = new PatternMatch[4];

        int written = automaton.CopyMatches("ushers", destination);

        Assert.Equal(3, written);
        Assert.Equal(automaton.FindAll("ushers"), destination[..3]);
        Assert.Equal(default, destination[3]);
    }

    [Fact]
    public void CopyMatches_ShouldWriteFromTheOffset_WhenADestinationIndexIsGiven()
    {
        var automaton = new AhoCorasick(Textbook);
        var destination = new PatternMatch[5];

        int written = automaton.CopyMatches("ushers", destination, 2);

        Assert.Equal(3, written);
        Assert.Equal(default, destination[0]);
        Assert.Equal(automaton.FindAll("ushers"), destination[2..]);
    }

    [Fact]
    public void CopyMatches_ShouldStopAtTheBufferEnd_WhenThereAreMoreMatchesThanRoom()
    {
        var automaton = new AhoCorasick(Textbook);
        var destination = new PatternMatch[2];

        int written = automaton.CopyMatches("ushers", destination);

        Assert.Equal(2, written);
        Assert.Equal(automaton.FindAll("ushers")[..2], destination);
    }

    [Fact]
    public void CopyMatches_ShouldTruncateMidOutputChain_WhenTheRoomRunsOutBetweenTwoMatchesEndingTogether()
    {
        // "abc", "bc" and "c" all end at 3; one slot of room takes the first and stops inside the chain.
        var automaton = new AhoCorasick(["c", "bc", "abc"]);
        var destination = new PatternMatch[1];

        int written = automaton.CopyMatches("abc", destination);

        Assert.Equal(1, written);
        Assert.Equal("abc", automaton[destination[0].PatternId]);
    }

    [Fact]
    public void CopyMatches_ShouldReturnZero_WhenTheBufferIsAlreadyFull()
    {
        var automaton = new AhoCorasick(Textbook);
        var destination = new PatternMatch[2];

        Assert.Equal(0, automaton.CopyMatches("ushers", destination, 2));
    }

    [Fact]
    public void CopyMatches_ShouldThrowArgumentNullException_WhenTheDestinationIsNull()
    {
        var automaton = new AhoCorasick(Textbook);

        Assert.Throws<ArgumentNullException>(() => automaton.CopyMatches("ushers", null!));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void CopyMatches_ShouldThrowArgumentOutOfRangeException_WhenTheDestinationIndexIsOutsideTheBuffer(int destinationIndex)
    {
        var automaton = new AhoCorasick(Textbook);
        var destination = new PatternMatch[4];

        Assert.Throws<ArgumentOutOfRangeException>(() => automaton.CopyMatches("ushers", destination, destinationIndex));
    }
}
