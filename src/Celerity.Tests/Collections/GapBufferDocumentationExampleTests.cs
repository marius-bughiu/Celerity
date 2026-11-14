using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Runs the <see cref="GapBuffer{T}"/> usage example published in <c>docs/api/collections.md</c> and
/// <c>docs/collections.md</c>, and asserts what its comments claim.
///
/// <para>
/// A published example is documentation a reader will copy, so a wrong comment in it is a defect. The claim
/// most worth pinning is the <see cref="GapBuffer{T}.GapPosition"/> printed after typing: that the gap
/// follows the caret is the whole cost model, and the example is where a reader learns it.
/// </para>
/// </summary>
public class GapBufferDocumentationExampleTests
{
    [Fact]
    public void UsageExample_ShouldBehaveAsThePublishedCommentsClaim_WhenRunEndToEnd()
    {
        var text = new GapBuffer<char>("hello world");

        // Type ", dear" after "hello": every keystroke lands next to the previous one.
        int caret = 5;
        foreach (char c in ", dear")
            text.Insert(caret++, c);
        Assert.Equal("hello, dear world", new string(text.ToArray()));
        Assert.Equal(11, text.GapPosition);

        // Backspace over "dear" and type "friend" instead.
        for (int i = 0; i < 4; i++)
            text.RemoveAt(--caret);
        foreach (char c in "friend")
            text.Insert(caret++, c);
        Assert.Equal("hello, friend world", new string(text.ToArray()));

        // Reads are one array load wherever the gap is.
        Assert.Equal('f', text[7]);

        // Zero-copy access to both halves, e.g. to write the buffer out.
        text.GetSpans(out ReadOnlySpan<char> before, out ReadOnlySpan<char> after);
        Assert.Equal("13 + 6", $"{before.Length} + {after.Length}");
        Assert.Equal("hello, friend", before.ToString());
        Assert.Equal(" world", after.ToString());
    }

    [Fact]
    public void GuideExample_ShouldBehaveAsThePublishedCommentsClaim()
    {
        var text = new GapBuffer<char>("hello world");
        int caret = 5;
        foreach (char c in ", dear")
            text.Insert(caret++, c);
        Assert.Equal("hello, dear world", new string(text.ToArray()));

        text.RemoveAt(--caret);
        Assert.Equal("hello, dea world", new string(text.ToArray()));
        Assert.Equal(10, text.GapPosition);
    }
}
