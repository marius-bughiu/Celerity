using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// The word-enumeration surface of <see cref="EditDistanceIndex"/> — the ordinary struct-enumerator surface the
/// rest of the library ships, exercised through the concrete type, both interfaces, and
/// <see cref="IEnumerator.Reset"/> — and the <see cref="FuzzyMatch"/> result struct.
///
/// <para>
/// Enumerating the index yields its <b>words</b> in id order; matches against a query come from
/// <see cref="EditDistanceIndex.FindWithin"/> and its siblings, which return arrays and counts rather than a
/// lazy enumerator, because ranking the matches needs all of them first. The match struct's equality,
/// deconstruction and rendering are covered here rather than in a file of their own.
/// </para>
/// </summary>
public class EditDistanceIndexEnumerationTests
{
    private static readonly string[] Words = ["apple", "apply", "ample"];

    // ---- the words ---------------------------------------------------------------------------------

    [Fact]
    public void GetEnumerator_ShouldYieldEveryWordInIdOrder_WhenTheIndexIsEnumerated()
    {
        var index = new EditDistanceIndex(Words, 1);

        List<string> seen = [];
        foreach (string word in index)
            seen.Add(word);

        Assert.Equal(Words, seen);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenTheWordSetIsEmpty()
    {
        var index = new EditDistanceIndex([], 1);

        Assert.Empty(index);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldTheSameWords_WhenReachedThroughEitherInterface()
    {
        var index = new EditDistanceIndex(Words, 1);

        Assert.Equal(Words, ((IEnumerable<string>)index).ToArray());

        List<object?> untyped = [];
        IEnumerator enumerator = ((IEnumerable)index).GetEnumerator();
        while (enumerator.MoveNext())
            untyped.Add(enumerator.Current);

        Assert.Equal(Words, untyped.Cast<string>());
    }

    [Fact]
    public void Enumerator_ShouldStartOverFromTheFirstWord_WhenItIsReset()
    {
        var index = new EditDistanceIndex(Words, 1);
        EditDistanceIndex.Enumerator enumerator = index.GetEnumerator();

        while (enumerator.MoveNext())
        {
        }

        Assert.False(enumerator.MoveNext());

        enumerator.Reset();
        Assert.Equal(string.Empty, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal("apple", enumerator.Current);
    }

    [Fact]
    public void Enumerator_ShouldExposeTheSameValueThroughTheUntypedCurrent_WhenBoxed()
    {
        var index = new EditDistanceIndex(Words, 1);
        IEnumerator<string> enumerator = index.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal("apple", enumerator.Current);
        Assert.Equal("apple", ((IEnumerator)enumerator).Current);
    }

    [Fact]
    public void Enumerator_ShouldDisposeWithoutEffect_WhenTheCallerReleasesIt()
    {
        var index = new EditDistanceIndex(Words, 1);
        EditDistanceIndex.Enumerator enumerator = index.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        enumerator.Dispose();
        Assert.Equal("apple", enumerator.Current);
    }

    [Fact]
    public void Count_ShouldAgreeWithTheReadOnlyListSurface_WhenTheIndexIsBuilt()
    {
        IReadOnlyList<string> list = new EditDistanceIndex(Words, 1);

        Assert.Equal(3, list.Count);
        Assert.Equal("ample", list[2]);
    }

    // ---- FuzzyMatch --------------------------------------------------------------------------------

    [Fact]
    public void Properties_ShouldReturnWhatTheConstructorWasGiven()
    {
        var match = new FuzzyMatch(3, 2);

        Assert.Equal(3, match.WordId);
        Assert.Equal(2, match.Distance);
    }

    [Fact]
    public void Deconstruct_ShouldYieldEveryComponent_WhenAMatchIsDestructured()
    {
        (int wordId, int distance) = new FuzzyMatch(3, 2);

        Assert.Equal(3, wordId);
        Assert.Equal(2, distance);
    }

    [Fact]
    public void Equals_ShouldCompareEveryComponent_WhenTwoMatchesAreCompared()
    {
        var match = new FuzzyMatch(1, 2);

        Assert.True(match.Equals(new FuzzyMatch(1, 2)));
        Assert.True(match == new FuzzyMatch(1, 2));
        Assert.False(match != new FuzzyMatch(1, 2));

        Assert.False(match.Equals(new FuzzyMatch(9, 2)));
        Assert.True(match != new FuzzyMatch(1, 9));
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenTheOtherObjectIsNotAMatch()
    {
        object match = new FuzzyMatch(1, 2);

        Assert.True(match.Equals(new FuzzyMatch(1, 2)));
        Assert.False(match.Equals("#1 ~2"));
        Assert.False(match.Equals(null));
    }

    [Fact]
    public void GetHashCode_ShouldAgree_WhenTwoMatchesAreEqual()
    {
        Assert.Equal(new FuzzyMatch(1, 2).GetHashCode(), new FuzzyMatch(1, 2).GetHashCode());
    }

    [Fact]
    public void ToString_ShouldRenderTheIdAndTheDistance_WhenAMatchIsFormatted()
    {
        Assert.Equal("#3 ~2", new FuzzyMatch(3, 2).ToString());
    }
}
