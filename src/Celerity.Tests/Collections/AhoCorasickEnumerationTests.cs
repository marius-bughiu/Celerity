using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// The two enumeration surfaces of <see cref="AhoCorasick"/>, which are easy to confuse and are deliberately
/// different: enumerating the automaton yields its <b>patterns</b> through the
/// <see cref="IReadOnlyList{T}"/> implementation, while
/// <see cref="AhoCorasick.EnumerateMatches(ReadOnlySpan{char})"/> yields <b>matches</b> against a text through
/// an allocation-free <c>ref struct</c> that drives the scan as it is pulled.
///
/// <para>
/// The match enumerator is where the interesting cases are, because it is a state machine that is suspended
/// mid-scan. Stopping early has to stop the scan rather than merely discard the rest of it; several patterns
/// ending at one position have to be drained before the text advances again, which means the enumerator has to
/// resume in the middle of an output chain; and a text that ends part way through such a chain has to finish
/// the chain before reporting exhaustion.
/// </para>
///
/// <para>
/// The pattern enumerator is the ordinary struct-enumerator surface the rest of the library ships, exercised
/// through the concrete type, both interfaces, and <see cref="IEnumerator.Reset"/> — which is reachable only
/// through the boxed interface for most callers but is public on the struct.
/// </para>
///
/// <para>
/// This type is a <see cref="PatternMatch"/> exercise as well: the match struct's equality, deconstruction and
/// rendering are covered here rather than in a file of their own, since every match that reaches them comes
/// out of one of these two enumerators.
/// </para>
/// </summary>
public class AhoCorasickEnumerationTests
{
    private static readonly string[] Textbook = ["he", "she", "his", "hers"];

    // ---- the patterns ------------------------------------------------------------------------------

    [Fact]
    public void GetEnumerator_ShouldYieldEveryPatternInIdOrder_WhenTheAutomatonIsEnumerated()
    {
        var automaton = new AhoCorasick(Textbook);

        List<string> seen = [];
        foreach (string pattern in automaton)
            seen.Add(pattern);

        Assert.Equal(Textbook, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenThePatternSetIsEmpty()
    {
        var automaton = new AhoCorasick([]);

        Assert.Empty(automaton);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldTheSamePatterns_WhenReachedThroughEitherInterface()
    {
        var automaton = new AhoCorasick(Textbook);

        Assert.Equal(Textbook, ((IEnumerable<string>)automaton).ToArray());

        List<string> untyped = [];
        foreach (object? pattern in (IEnumerable)automaton)
            untyped.Add((string)pattern!);

        Assert.Equal(Textbook, untyped);
    }

    [Fact]
    public void Enumerator_ShouldStartOverFromTheFirstPattern_WhenItIsReset()
    {
        var automaton = new AhoCorasick(Textbook);
        AhoCorasick.Enumerator enumerator = automaton.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        Assert.Equal("she", enumerator.Current);

        enumerator.Reset();
        Assert.Equal(string.Empty, enumerator.Current);

        Assert.True(enumerator.MoveNext());
        Assert.Equal("he", enumerator.Current);
    }

    [Fact]
    public void Enumerator_ShouldExposeTheSameValueThroughTheUntypedCurrent_WhenBoxed()
    {
        var automaton = new AhoCorasick(Textbook);
        IEnumerator enumerator = automaton.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal("he", enumerator.Current);
    }

    [Fact]
    public void Enumerator_ShouldDisposeWithoutEffect_WhenTheCallerReleasesIt()
    {
        var automaton = new AhoCorasick(Textbook);

        using IEnumerator<string> enumerator = automaton.GetEnumerator();

        Assert.True(enumerator.MoveNext());
    }

    [Fact]
    public void Count_ShouldAgreeWithTheReadOnlyListSurface_WhenTheAutomatonIsBuilt()
    {
        IReadOnlyList<string> automaton = new AhoCorasick(Textbook);

        Assert.Equal(4, automaton.Count);
        Assert.Equal("hers", automaton[3]);
    }

    // ---- the matches -------------------------------------------------------------------------------

    [Fact]
    public void EnumerateMatches_ShouldYieldTheSameMatchesAsFindAll_WhenTheWholeTextIsRead()
    {
        var automaton = new AhoCorasick(Textbook);

        List<PatternMatch> seen = [];
        foreach (PatternMatch match in automaton.EnumerateMatches("ushers and his"))
            seen.Add(match);

        Assert.Equal(automaton.FindAll("ushers and his"), seen);
    }

    [Fact]
    public void EnumerateMatches_ShouldYieldNothing_WhenNoPatternOccurs()
    {
        var automaton = new AhoCorasick(Textbook);
        AhoCorasick.MatchEnumerator enumerator = automaton.EnumerateMatches("nothing to see");

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void EnumerateMatches_ShouldStopTheScan_WhenTheCallerBreaksEarly()
    {
        var automaton = new AhoCorasick(Textbook);

        PatternMatch first = default;
        foreach (PatternMatch match in automaton.EnumerateMatches("ushers"))
        {
            first = match;
            break;
        }

        Assert.Equal("she", automaton[first.PatternId]);
        Assert.Equal(1, first.Start);
    }

    [Fact]
    public void EnumerateMatches_ShouldResumeInsideTheOutputChain_WhenSeveralPatternsEndTogether()
    {
        // "abc", "bc" and "c" all end at 3, so the enumerator has to suspend part way down the chain and pick
        // it up again on the next pull rather than advancing the text.
        var automaton = new AhoCorasick(["c", "bc", "abc"]);
        AhoCorasick.MatchEnumerator enumerator = automaton.EnumerateMatches("abc");

        Assert.True(enumerator.MoveNext());
        Assert.Equal(new PatternMatch(2, 0, 3), enumerator.Current);

        Assert.True(enumerator.MoveNext());
        Assert.Equal(new PatternMatch(1, 1, 2), enumerator.Current);

        Assert.True(enumerator.MoveNext());
        Assert.Equal(new PatternMatch(0, 2, 1), enumerator.Current);

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void GetEnumerator_ShouldReturnItself_WhenTheMatchEnumeratorIsUsedDirectly()
    {
        var automaton = new AhoCorasick(["he"]);

        int count = 0;
        foreach (PatternMatch match in automaton.EnumerateMatches("he he").GetEnumerator())
            count++;

        Assert.Equal(2, count);
    }

    // ---- PatternMatch ------------------------------------------------------------------------------

    [Fact]
    public void End_ShouldBeTheStartPlusTheLength_WhenAMatchIsFormed()
    {
        var match = new PatternMatch(3, 7, 4);

        Assert.Equal(3, match.PatternId);
        Assert.Equal(7, match.Start);
        Assert.Equal(4, match.Length);
        Assert.Equal(11, match.End);
    }

    [Fact]
    public void Deconstruct_ShouldYieldEveryComponent_WhenAMatchIsDestructured()
    {
        (int patternId, int start, int length) = new PatternMatch(3, 7, 4);

        Assert.Equal(3, patternId);
        Assert.Equal(7, start);
        Assert.Equal(4, length);
    }

    [Fact]
    public void Equals_ShouldCompareEveryComponent_WhenTwoMatchesAreCompared()
    {
        var match = new PatternMatch(1, 2, 3);

        Assert.True(match.Equals(new PatternMatch(1, 2, 3)));
        Assert.True(match == new PatternMatch(1, 2, 3));
        Assert.False(match != new PatternMatch(1, 2, 3));

        Assert.False(match.Equals(new PatternMatch(9, 2, 3)));
        Assert.False(match.Equals(new PatternMatch(1, 9, 3)));
        Assert.True(match != new PatternMatch(1, 2, 9));
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenTheOtherObjectIsNotAMatch()
    {
        object match = new PatternMatch(1, 2, 3);

        Assert.True(match.Equals(new PatternMatch(1, 2, 3)));
        Assert.False(match.Equals("not a match"));
    }

    [Fact]
    public void GetHashCode_ShouldAgree_WhenTwoMatchesAreEqual()
    {
        Assert.Equal(new PatternMatch(1, 2, 3).GetHashCode(), new PatternMatch(1, 2, 3).GetHashCode());
    }

    [Fact]
    public void ToString_ShouldRenderTheIdAndTheHalfOpenRange_WhenAMatchIsFormatted()
    {
        Assert.Equal("#3 @[7, 11)", new PatternMatch(3, 7, 4).ToString());
    }
}
