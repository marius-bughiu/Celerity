using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Runs the <see cref="SuffixArray"/> usage examples published in <c>docs/api/collections.md</c>, the README
/// and the type's own XML documentation, and asserts the outputs those pages print in their comments.
///
/// <para>
/// A published example is documentation a reader will copy, so an incorrect one is a defect rather than a
/// typo — and these values are exactly the kind worked out by hand. Which occurrence
/// <see cref="SuffixArray.TryGetLongestRepeatedSubstring"/> reports is a property of the implementation rather
/// than of the text, and the occurrence positions were counted off a sentence by eye. The assertions below are
/// what stop the pages and the code drifting apart.
/// </para>
/// </summary>
public class SuffixArrayDocumentationExampleTests
{
    // Verbatim from docs/api/collections.md, README.md and the XML documentation on the type.
    private const string Sentence = "the cat sat on the mat";

    [Fact]
    public void QueryExample_ShouldPrintWhatTheDocumentedCommentsClaim()
    {
        var index = new SuffixArray(Sentence);

        Assert.Equal(3, index.CountOccurrences("at"));
        Assert.Equal(0, index.IndexOf("the"));
        Assert.False(index.Contains("dog"));
    }

    [Fact]
    public void OccurrencesExample_ShouldPrintWhatTheDocumentedCommentsClaim()
    {
        var index = new SuffixArray(Sentence);

        Assert.Equal("5 9 20", string.Join(" ", index.GetOccurrences("at")));
    }

    [Fact]
    public void ZeroCopyExample_ShouldPrintWhatTheDocumentedCommentsClaim()
    {
        var index = new SuffixArray(Sentence);

        Assert.True(index.TryGetOccurrences("the", out ReadOnlySpan<int> found));
        Assert.Equal(2, found.Length);
    }

    [Fact]
    public void LongestRepeatExample_ShouldPrintWhatTheDocumentedCommentsClaim()
    {
        var index = new SuffixArray(Sentence);

        Assert.True(index.TryGetLongestRepeatedSubstring(out int start, out int length));
        Assert.Equal("the ", index.Text.Slice(start, length).ToString());
    }
}
