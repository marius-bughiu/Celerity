using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Coverage for <see cref="WaveletTree"/>'s <see cref="IReadOnlyList{T}"/> surface: the struct enumerator and
/// both boxed interface paths, and the deliberately explicit <see cref="IReadOnlyCollection{T}.Count"/>.
///
/// <para>
/// <see cref="IReadOnlyCollection{T}.Count"/> is explicit because it would otherwise be a second public name
/// for <see cref="WaveletTree.Length"/>, so the interface path is the only way to reach it and needs its own
/// test.
/// </para>
///
/// <para>
/// The index is immutable, so there is no version counter and no invalidation to test. What is left is that
/// the enumerator replays the original sequence — which it reconstructs value by value through the levels
/// rather than reading a stored array — stops, stays stopped, and can be restarted.
/// </para>
/// </summary>
public class WaveletTreeEnumerationTests
{
    private static readonly int[] Sample = [4, 1, 4, 9, 1, 0];

    [Fact]
    public void GetEnumerator_ShouldReplayTheSequenceInItsOriginalOrder()
    {
        var replayed = new List<int>();
        foreach (int value in new WaveletTree(Sample))
            replayed.Add(value);

        Assert.Equal(Sample, replayed);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenTheSequenceIsEmpty()
    {
        WaveletTree.Enumerator enumerator = new WaveletTree([]).GetEnumerator();

        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);
    }

    [Fact]
    public void MoveNext_ShouldKeepReturningFalse_WhenTheWalkHasRunOut()
    {
        WaveletTree.Enumerator enumerator = new WaveletTree(Sample).GetEnumerator();

        while (enumerator.MoveNext())
        {
        }

        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Reset_ShouldRestartTheWalk()
    {
        WaveletTree.Enumerator enumerator = new WaveletTree(Sample).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current);

        enumerator.Reset();
        Assert.Equal(0, enumerator.Current);

        Assert.True(enumerator.MoveNext());
        Assert.Equal(4, enumerator.Current);

        enumerator.Dispose();
    }

    [Fact]
    public void GenericInterface_ShouldWalkTheSameSequence()
    {
        IEnumerable<int> tree = new WaveletTree(Sample);

        Assert.Equal(Sample, tree.ToArray());
    }

    [Fact]
    public void NonGenericInterface_ShouldWalkTheSameSequence()
    {
        IEnumerable tree = new WaveletTree(Sample);

        var replayed = new List<int>();
        IEnumerator enumerator = tree.GetEnumerator();
        while (enumerator.MoveNext())
            replayed.Add((int)enumerator.Current!);

        Assert.Equal(Sample, replayed);
    }

    [Fact]
    public void Count_ShouldMatchLength_WhenReachedThroughTheInterface()
    {
        IReadOnlyList<int> tree = new WaveletTree(Sample);

        Assert.Equal(Sample.Length, tree.Count);
    }
}
