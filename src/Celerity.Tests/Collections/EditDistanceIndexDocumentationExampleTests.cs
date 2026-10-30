using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Runs the <see cref="EditDistanceIndex"/> usage examples published in <c>docs/api/collections.md</c>, the
/// README and the type's own XML documentation, and asserts the outputs those pages print in their comments.
///
/// <para>
/// A published example is documentation a reader will copy, so an incorrect one is a defect rather than a
/// typo — and edit distances are exactly the kind of value worked out by hand and gotten wrong. The examples
/// also make two claims about behaviour rather than about the words: that a swapped pair is two edits, not
/// one, and that a tie between equally close words goes to the one supplied first.
/// </para>
/// </summary>
public class EditDistanceIndexDocumentationExampleTests
{
    // Verbatim from the XML documentation on the type, docs/api/collections.md and README.md.
    private static readonly string[] Fruit = ["apple", "apply", "ample", "maple", "angle"];

    [Fact]
    public void FruitExample_ShouldPrintWhatTheDocumentedCommentsClaim()
    {
        var index = new EditDistanceIndex(Fruit, maxDistance: 2);

        Assert.False(index.ContainsWithin("appel", 1));
        Assert.Equal(3, index.CountWithin("aple", 1));

        List<string> printed = [];
        foreach (var match in index.FindWithin("aple", 2))
            printed.Add($"{index[match.WordId]} at {match.Distance}");

        Assert.Equal(["apple at 1", "ample at 1", "maple at 1", "apply at 2", "angle at 2"], printed);

        Assert.True(index.TryFindNearest("appl", 2, out var nearest));
        Assert.Equal("apple", index[nearest.WordId]);
    }
}
