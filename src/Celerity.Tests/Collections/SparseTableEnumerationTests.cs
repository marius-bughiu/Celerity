using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Coverage for <see cref="SparseTable{T, TMonoid}"/>'s <see cref="IReadOnlyList{T}"/> surface: the struct
/// enumerator and both boxed interface paths.
///
/// <para>
/// The table is immutable, so — unlike <see cref="SegmentTree{T, TMonoid}"/> — there is no version counter and
/// no invalidation to test. What is left is that the enumerator replays the original sequence, stops, stays
/// stopped, can be restarted, and that a query running alongside an open enumerator disturbs nothing, since a
/// read is not a mutation and there is no mutation available in the first place.
/// </para>
/// </summary>
public class SparseTableEnumerationTests
{
    private static readonly int[] Sample = [5, 3, 9, 1, 7];

    private static SparseTable<int, MinMonoid<int>> Build() => new(Sample);

    [Fact]
    public void GetEnumerator_ShouldReplayTheSequenceInItsOriginalOrder()
    {
        var replayed = new List<int>();
        foreach (int value in Build())
            replayed.Add(value);

        Assert.Equal(Sample, replayed);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenTheTableIsEmpty()
    {
        SparseTable<int, MinMonoid<int>>.Enumerator enumerator =
            new SparseTable<int, MinMonoid<int>>(Array.Empty<int>()).GetEnumerator();

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldKeepReturningFalse_AfterTheSequenceIsExhausted()
    {
        SparseTable<int, MinMonoid<int>>.Enumerator enumerator = Build().GetEnumerator();
        while (enumerator.MoveNext())
        {
        }

        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);
    }

    [Fact]
    public void Current_ShouldBeTheDefault_BeforeTheFirstMoveNext()
    {
        SparseTable<int, MinMonoid<int>>.Enumerator enumerator = Build().GetEnumerator();

        Assert.Equal(0, enumerator.Current);
    }

    [Fact]
    public void Reset_ShouldRestartTheEnumeration()
    {
        SparseTable<int, MinMonoid<int>>.Enumerator enumerator = Build().GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        Assert.Equal(3, enumerator.Current);

        enumerator.Reset();

        Assert.Equal(0, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(5, enumerator.Current);
    }

    [Fact]
    public void Dispose_ShouldBeANoOp()
    {
        SparseTable<int, MinMonoid<int>>.Enumerator enumerator = Build().GetEnumerator();

        enumerator.Dispose();
        enumerator.Dispose();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(5, enumerator.Current);
    }

    [Fact]
    public void GenericInterfaceEnumerator_ShouldReplayTheSequence()
    {
        IEnumerable<int> table = Build();

        using IEnumerator<int> enumerator = table.GetEnumerator();

        var replayed = new List<int>();
        while (enumerator.MoveNext())
            replayed.Add(enumerator.Current);

        Assert.Equal(Sample, replayed);
    }

    [Fact]
    public void NonGenericInterfaceEnumerator_ShouldReplayTheSequence()
    {
        IEnumerable table = Build();

        IEnumerator enumerator = table.GetEnumerator();

        var replayed = new List<int>();
        while (enumerator.MoveNext())
            replayed.Add((int)enumerator.Current!);

        Assert.Equal(Sample, replayed);
    }

    [Fact]
    public void NonGenericInterfaceEnumerator_ShouldSupportResetAndExposeCurrent()
    {
        IEnumerator enumerator = ((IEnumerable)Build()).GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(5, enumerator.Current);

        enumerator.Reset();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(5, enumerator.Current);
    }

    [Fact]
    public void Query_ShouldNotDisturbAnOpenEnumerator()
    {
        SparseTable<int, MinMonoid<int>> table = Build();
        SparseTable<int, MinMonoid<int>>.Enumerator enumerator = table.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, table.Query(0, table.Count));
        Assert.True(enumerator.MoveNext());

        Assert.Equal(3, enumerator.Current);
    }
}
