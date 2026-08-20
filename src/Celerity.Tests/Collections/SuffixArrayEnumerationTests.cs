using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Coverage for <see cref="SuffixArray"/>'s <see cref="IReadOnlyList{T}"/> surface: the struct enumerator and
/// both boxed interface paths, the indexer and its bounds, and the deliberately explicit
/// <see cref="IReadOnlyCollection{T}.Count"/>.
///
/// <para>
/// <see cref="IReadOnlyCollection{T}.Count"/> is explicit because it would otherwise be a second public name
/// for <see cref="SuffixArray.Length"/> — the suffix count and the text length are the same number — so the
/// interface path is the only way to reach it and needs its own test.
/// </para>
///
/// <para>
/// The index is immutable, so there is no version counter and no invalidation to test: what is left is that
/// the enumerator walks the ranks in order, stops, stays stopped, and can be restarted.
/// </para>
/// </summary>
public class SuffixArrayEnumerationTests
{
    private const string Banana = "banana";

    [Fact]
    public void GetEnumerator_ShouldYieldEveryStartPositionInLexicographicOrder_WhenTheTextRepeatsAPrefix()
    {
        var positions = new List<int>();
        foreach (int position in new SuffixArray(Banana))
            positions.Add(position);

        Assert.Equal([5, 3, 1, 0, 4, 2], positions);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenTheTextIsEmpty()
    {
        SuffixArray.Enumerator enumerator = new SuffixArray(string.Empty).GetEnumerator();

        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);
    }

    [Fact]
    public void MoveNext_ShouldKeepReturningFalse_WhenTheWalkHasRunOut()
    {
        SuffixArray.Enumerator enumerator = new SuffixArray("ab").GetEnumerator();

        while (enumerator.MoveNext())
        {
        }

        Assert.False(enumerator.MoveNext());
        enumerator.Dispose();
    }

    [Fact]
    public void Reset_ShouldRestartTheWalk_WhenTheEnumeratorHasAdvanced()
    {
        SuffixArray.Enumerator enumerator = new SuffixArray(Banana).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        enumerator.Reset();

        Assert.Equal(0, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(5, enumerator.Current);
    }

    [Fact]
    public void GenericEnumerator_ShouldYieldTheSameOrder_WhenTheIndexIsBoxed()
    {
        IEnumerable<int> boxed = new SuffixArray(Banana);

        Assert.Equal([5, 3, 1, 0, 4, 2], boxed.ToArray());
    }

    [Fact]
    public void NonGenericEnumerator_ShouldYieldTheSameOrder_WhenTheIndexIsBoxed()
    {
        IEnumerable boxed = new SuffixArray(Banana);

        var positions = new List<int>();
        IEnumerator enumerator = boxed.GetEnumerator();
        while (enumerator.MoveNext())
            positions.Add((int)enumerator.Current!);

        Assert.Equal([5, 3, 1, 0, 4, 2], positions);
    }

    [Fact]
    public void Count_ShouldEqualTheTextLength_WhenReachedThroughTheInterface()
    {
        IReadOnlyCollection<int> index = new SuffixArray(Banana);

        Assert.Equal(6, index.Count);
    }

    [Fact]
    public void Indexer_ShouldReturnTheStartPositionAtThatRank_WhenTheRankIsInRange()
    {
        IReadOnlyList<int> index = new SuffixArray(Banana);

        Assert.Equal(5, index[0]);
        Assert.Equal(2, index[5]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void Indexer_ShouldThrowArgumentOutOfRangeException_WhenTheRankIsOutsideTheIndex(int rank)
    {
        var index = new SuffixArray(Banana);

        Assert.Throws<ArgumentOutOfRangeException>(() => index[rank]);
    }
}
