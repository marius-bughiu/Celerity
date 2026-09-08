using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// <see cref="PersistentVector{T}.Enumerator"/>'s contract.
///
/// <para>
/// The enumerator does not descend the trie per element: it refreshes its leaf array only when the low five
/// bits of the index wrap, which is correct because trie leaves are 32 wide <i>and</i> the tail starts at a
/// multiple of 32. That second half is the load-bearing one and it is invisible from a length that happens to
/// be a multiple of 32, so the walks below run at lengths on both sides of the boundary and at a length whose
/// tail is partly filled.
/// </para>
///
/// <para>
/// The vector cannot be modified, so — unlike the mutable collections' enumerators — there is no version to
/// check and nothing here about invalidation. What is pinned instead is the part of
/// <see cref="IEnumerator"/> that a <c>foreach</c> never reaches: that the exhausted state is <b>sticky</b>,
/// that <see cref="PersistentVector{T}.Enumerator.Reset"/> replays the sequence from the start, and that
/// <c>Current</c> is <c>default(T)</c> outside the sequence at both ends. That last one is the family's
/// convention rather than <see cref="List{T}"/>'s explicit-interface throw: every struct enumerator in this
/// library holds a <c>_current</c> field and clears it on the exit path, so <c>Current</c> never throws and
/// never keeps reading the final element after <c>MoveNext</c> has said there is none.
/// </para>
/// </summary>
public class PersistentVectorEnumerationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(1_024)]
    [InlineData(1_057)]
    [InlineData(1_100)]
    public void Enumeration_ShouldYieldEveryElementInOrder(int length)
    {
        PersistentVector<int> vector = new(Enumerable.Range(0, length));

        var seen = new List<int>();
        foreach (int value in vector)
            seen.Add(value);

        Assert.Equal(Enumerable.Range(0, length).ToArray(), seen);
    }

    [Fact]
    public void GenericInterfaceEnumeration_ShouldYieldEveryElementInOrder()
    {
        IEnumerable<int> vector = new PersistentVector<int>(Enumerable.Range(0, 100));

        Assert.Equal(Enumerable.Range(0, 100).ToArray(), vector.ToArray());
    }

    [Fact]
    public void NonGenericInterfaceEnumeration_ShouldYieldEveryElementInOrder()
    {
        IEnumerable vector = new PersistentVector<int>(Enumerable.Range(0, 100));

        var seen = new List<int>();
        foreach (object? value in vector)
            seen.Add((int)value!);

        Assert.Equal(Enumerable.Range(0, 100).ToArray(), seen);
    }

    [Fact]
    public void MoveNext_ShouldKeepReturningFalse_OnceTheVectorIsExhausted()
    {
        PersistentVector<int> vector = new(new[] { 1, 2 });

        PersistentVector<int>.Enumerator enumerator = vector.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldKeepReturningFalse_WhenTheVectorIsEmpty()
    {
        PersistentVector<int>.Enumerator enumerator = PersistentVector<int>.Empty.GetEnumerator();

        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Reset_ShouldReplayTheWholeSequence()
    {
        PersistentVector<int> vector = new(Enumerable.Range(0, 40));

        PersistentVector<int>.Enumerator enumerator = vector.GetEnumerator();

        var first = new List<int>();
        while (enumerator.MoveNext())
            first.Add(enumerator.Current);

        enumerator.Reset();

        var second = new List<int>();
        while (enumerator.MoveNext())
            second.Add(enumerator.Current);

        Assert.Equal(Enumerable.Range(0, 40).ToArray(), first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Current_ShouldBeTheDefault_BeforeTheFirstMoveNextAndAfterTheLast()
    {
        // The family convention is a _current field cleared on the false path, not a throw: Deque, SparseTable
        // and KdTree all return default(T) outside the sequence, as does List<T>'s public Current. What must
        // not happen is a dereference of the not-yet-loaded leaf, or Current still reading the final element
        // after MoveNext has said there is none.
        PersistentVector<int> vector = new(new[] { 1, 2 });

        PersistentVector<int>.Enumerator enumerator = vector.GetEnumerator();
        Assert.Equal(0, enumerator.Current);

        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(2, enumerator.Current);

        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);

        // Still default after a second exhausted call, and after a Reset returns it to the start.
        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);

        enumerator.Reset();
        Assert.Equal(0, enumerator.Current);
    }

    [Fact]
    public void Current_ShouldBeTheDefault_OutsideTheSequenceForAReferenceElement()
    {
        // A reference element makes the "cleared" state observable as null rather than as a zero that could
        // also be a real value.
        PersistentVector<string> vector = new(new[] { "a" });

        PersistentVector<string>.Enumerator enumerator = vector.GetEnumerator();
        Assert.Null(enumerator.Current);

        Assert.True(enumerator.MoveNext());
        Assert.Equal("a", enumerator.Current);

        Assert.False(enumerator.MoveNext());
        Assert.Null(enumerator.Current);
    }

    [Fact]
    public void NonGenericCurrent_ShouldBeTheDefault_BeforeTheFirstMoveNext()
    {
        PersistentVector<int> vector = new(new[] { 1 });

        IEnumerator enumerator = vector.GetEnumerator();

        Assert.Equal(0, enumerator.Current);
    }

    [Fact]
    public void NonGenericCurrent_ShouldBoxTheCurrentElement()
    {
        PersistentVector<int> vector = new(new[] { 41, 42 });

        IEnumerator enumerator = vector.GetEnumerator();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(41, enumerator.Current);
    }

    [Fact]
    public void Dispose_ShouldBeANoOp()
    {
        PersistentVector<int> vector = new(new[] { 1 });

        IEnumerator<int> enumerator = ((IEnumerable<int>)vector).GetEnumerator();

        enumerator.Dispose();
        enumerator.Dispose();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current);
    }
}
