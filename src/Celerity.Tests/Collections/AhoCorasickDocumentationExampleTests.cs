using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Runs the <see cref="AhoCorasick"/> usage examples published in <c>docs/api/collections.md</c>, the README
/// and the type's own XML documentation, and asserts the outputs those pages print in their comments.
///
/// <para>
/// A published example is documentation a reader will copy, so an incorrect one is a defect rather than a
/// typo — and the values here are exactly the kind worked out by hand. The <c>"ushers"</c> example prints
/// three matches in a specific order, two of them starting inside another match: both the count and the order
/// are claims about the algorithm rather than about the text, and both would still look plausible if they were
/// wrong.
/// </para>
/// </summary>
public class AhoCorasickDocumentationExampleTests
{
    // Verbatim from the XML documentation on the type, and from docs/api/collections.md.
    private static readonly string[] Textbook = ["he", "she", "his", "hers"];

    private const string Ushers = "ushers";

    // Verbatim from README.md and docs/api/collections.md.
    private static readonly string[] Alerts = ["OutOfMemory", "StackOverflow", "Timeout", "Deadlock"];

    private const string LogLine = "worker-3: Timeout waiting for the lock — possible Deadlock";

    [Fact]
    public void TextbookExample_ShouldPrintWhatTheDocumentedCommentsClaim()
    {
        var automaton = new AhoCorasick(Textbook);

        Assert.True(automaton.ContainsAny(Ushers));
        Assert.Equal(3, automaton.CountMatches(Ushers));
    }

    [Fact]
    public void TextbookEnumerationExample_ShouldPrintWhatTheDocumentedCommentsClaim()
    {
        var automaton = new AhoCorasick(Textbook);

        List<string> printed = [];
        foreach (PatternMatch match in automaton.EnumerateMatches(Ushers))
            printed.Add($"{automaton[match.PatternId]} at {match.Start}");

        Assert.Equal(["she at 1", "he at 2", "hers at 2"], printed);
    }

    [Fact]
    public void LogScanExample_ShouldPrintWhatTheDocumentedCommentsClaim()
    {
        var alerts = new AhoCorasick(Alerts);

        Assert.True(alerts.ContainsAny(LogLine));

        List<string> printed = [];
        foreach (PatternMatch match in alerts.EnumerateMatches(LogLine))
            printed.Add(alerts[match.PatternId]);

        Assert.Equal(["Timeout", "Deadlock"], printed);
    }

    [Fact]
    public void FirstMatchExample_ShouldPrintWhatTheDocumentedCommentsClaim()
    {
        var alerts = new AhoCorasick(Alerts);

        Assert.True(alerts.TryFindFirst(LogLine, out PatternMatch first));
        Assert.Equal("Timeout", alerts[first.PatternId]);
        Assert.Equal(10, first.Start);
    }
}
