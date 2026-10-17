using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Coverage for <see cref="EliasFanoSequence"/>'s <see cref="IReadOnlyList{T}"/> surface: the struct
/// enumerator and both boxed interface paths.
///
/// <para>
/// The sequence is immutable, so there is no version counter and no invalidation to test. What is left is
/// that the enumerator decodes the original values — walking the high vector's set bits rather than reading
/// a stored array — stops, stays stopped with <see cref="EliasFanoSequence.Enumerator.Current"/> cleared, and
/// can be restarted.
/// </para>
/// </summary>
public class EliasFanoSequenceEnumerationTests
{
    private static readonly long[] Sample = [2, 2, 9, 70, 70, 1_000_000];

    [Fact]
    public void GetEnumerator_ShouldDecodeTheSequenceInOrder()
    {
        var decoded = new List<long>();
        foreach (long value in new EliasFanoSequence(Sample))
            decoded.Add(value);

        Assert.Equal(Sample, decoded);
    }

    [Fact]
    public void Current_ShouldBeZero_BeforeTheFirstMoveNext()
    {
        EliasFanoSequence.Enumerator enumerator = new EliasFanoSequence(Sample).GetEnumerator();

        Assert.Equal(0, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(2, enumerator.Current);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenTheSequenceIsEmpty()
    {
        EliasFanoSequence.Enumerator enumerator = new EliasFanoSequence([]).GetEnumerator();

        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);
    }

    [Fact]
    public void MoveNext_ShouldKeepReturningFalse_AndClearCurrent_WhenTheWalkHasRunOut()
    {
        EliasFanoSequence.Enumerator enumerator = new EliasFanoSequence(Sample).GetEnumerator();

        while (enumerator.MoveNext())
        {
        }

        Assert.Equal(0, enumerator.Current);
        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Reset_ShouldRestartTheWalk()
    {
        EliasFanoSequence.Enumerator enumerator = new EliasFanoSequence(Sample).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        Assert.Equal(9, enumerator.Current);

        enumerator.Reset();
        Assert.Equal(0, enumerator.Current);

        Assert.True(enumerator.MoveNext());
        Assert.Equal(2, enumerator.Current);

        enumerator.Dispose();
    }

    [Fact]
    public void GetEnumerator_ShouldSkipEmptyWords_InASparseHighVector()
    {
        // 200 repeats of 0 then one far value: L = 12, so the last value's high part is 244 and the 244 zeros
        // that end the empty high parts before it fill several whole words the enumerator has to step over.
        long[] values = [.. Enumerable.Repeat(0L, 200), 1_000_000];
        var sequence = new EliasFanoSequence(values);

        Assert.Equal(values, sequence.ToArray());
    }

    [Fact]
    public void GenericInterface_ShouldWalkTheSameSequence()
    {
        IEnumerable<long> sequence = new EliasFanoSequence(Sample);

        Assert.Equal(Sample, sequence.ToArray());
    }

    [Fact]
    public void NonGenericInterface_ShouldWalkTheSameSequence()
    {
        IEnumerable sequence = new EliasFanoSequence(Sample);

        var decoded = new List<long>();
        IEnumerator enumerator = sequence.GetEnumerator();
        while (enumerator.MoveNext())
            decoded.Add((long)enumerator.Current!);

        Assert.Equal(Sample, decoded);
    }

    [Fact]
    public void ReadOnlyList_ShouldExposeCountAndTheIndexer()
    {
        IReadOnlyList<long> sequence = new EliasFanoSequence(Sample);

        Assert.Equal(Sample.Length, sequence.Count);
        Assert.Equal(70, sequence[3]);
    }
}
