using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Runs the <see cref="BTreeList{T}"/> usage example published in <c>docs/api/collections.md</c> and the
/// README, and asserts what its comments claim.
///
/// <para>
/// A published example is documentation a reader will copy, so a wrong comment in it is a defect. The claim
/// most worth pinning is the <see cref="BTreeList{T}.Depth"/> printed for a million elements: it follows
/// from the fan-out and the bulk fill, and a change to either moves it silently.
/// </para>
/// </summary>
public class BTreeListDocumentationExampleTests
{
    [Fact]
    public void UsageExample_ShouldBehaveAsThePublishedCommentsClaim_WhenRunEndToEnd()
    {
        var playlist = new BTreeList<string>(new[] { "intro", "verse", "chorus", "outro" });

        playlist.Insert(2, "bridge");
        Assert.Equal(new[] { "intro", "verse", "bridge", "chorus", "outro" }, playlist);

        playlist.RemoveAt(0);
        Assert.Equal(new[] { "verse", "bridge", "chorus", "outro" }, playlist);

        playlist[playlist.Count - 1] = "coda";
        Assert.Equal(new[] { "verse", "bridge", "chorus", "coda" }, playlist);

        Assert.Equal("bridge", playlist[1]);
        Assert.Equal(2, playlist.IndexOf("chorus"));
        Assert.Equal("verse, bridge, chorus, coda", string.Join(", ", playlist));

        var big = new BTreeList<int>(Enumerable.Range(0, 1_000_000));
        for (int i = 0; i < 1_000; i++)
            big.Insert(big.Count / 2, -i);

        Assert.Equal(1_001_000, big.Count);
        Assert.Equal(4, big.Depth);

        // Every insert lands at or after position 500,000, so everything before it is untouched.
        Assert.Equal(499_999, big[499_999]);
    }
}
