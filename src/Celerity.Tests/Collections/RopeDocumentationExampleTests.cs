using System.Text;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Runs the <see cref="Rope"/> usage example published in <c>docs/api/collections.md</c>, the README and the
/// type's own XML documentation, and asserts what those pages claim in their comments.
///
/// <para>
/// A published example is documentation a reader will copy, so an incorrect one is a defect rather than a
/// typo. The claims this file has to pin are the two that are easy to write and easy to get wrong: that
/// <see cref="Rope.Split"/> followed by <see cref="Rope.AppendAndClear"/> rotates the document rather than
/// duplicating or dropping a piece of it, and that <see cref="Rope.AppendAndClear"/> leaves its source empty
/// — a reader who assumes it copies would write a loop that silently drains the rope it is reading from.
/// </para>
/// </summary>
public class RopeDocumentationExampleTests
{
    private const string Chapter =
        "It was the best of times, it was the worst of times,\n" +
        "it was the age of wisdom, it was the age of foolishness,\n" +
        "it was the epoch of belief, it was the epoch of incredulity.\n";

    /// <summary>
    /// The published example end to end, with the file read replaced by the literal above. Every assertion
    /// here corresponds to a claim one of its comments makes.
    /// </summary>
    [Fact]
    public void UsageExample_ShouldBehaveAsThePublishedCommentsClaim_WhenRunEndToEnd()
    {
        var document = new Rope(Chapter);

        // "An edit costs the depth of the tree, not the length of the document."
        document.Insert(11, "very ");
        Assert.StartsWith("It was the very best of times", document.ToString(), StringComparison.Ordinal);

        document.Remove(0, 7);
        Assert.StartsWith("the very best of times", document.ToString(), StringComparison.Ordinal);

        // "Random access, and a scan for a character."
        Assert.Equal('t', document[0]);

        int firstLineBreak = document.IndexOf('\n');
        Assert.True(firstLineBreak > 0);

        // "Cut the document in two and put it back the other way round — both O(log n), and neither copies
        //  the text."
        string before = document.ToString();
        Rope tail = document.Split(firstLineBreak + 1);

        // The claim that makes AppendAndClear's name worth its length: it moves, so the source is emptied.
        tail.AppendAndClear(document);
        Assert.Equal(0, document.Length);

        document.AppendAndClear(tail);
        Assert.Equal(0, tail.Length);

        Assert.Equal(
            before[(firstLineBreak + 1)..] + before[..(firstLineBreak + 1)],
            document.ToString());

        // "The zero-copy read path: write the document out without ever materializing it as one string."
        var written = new StringBuilder();
        foreach (ReadOnlySpan<char> chunk in document.GetChunks())
            written.Append(chunk);

        Assert.Equal(document.ToString(), written.ToString());
    }

    /// <summary>
    /// The README's shorter fragment: the rope is reached for because the document is edited in the middle,
    /// and the point of the comparison is that the answer matches what a <see cref="StringBuilder"/> would
    /// have produced from the same calls.
    /// </summary>
    [Fact]
    public void ReadmeExample_ShouldAgreeWithStringBuilder_WhenTheSameEditsAreApplied()
    {
        var document = new Rope(Chapter);
        var oracle = new StringBuilder(Chapter);

        document.Insert(11, "very ");
        oracle.Insert(11, "very ");

        document.Remove(0, 7);
        oracle.Remove(0, 7);

        document.Append(" — Dickens");
        oracle.Append(" — Dickens");

        Assert.Equal(oracle.ToString(), document.ToString());
    }
}
