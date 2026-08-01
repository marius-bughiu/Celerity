using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration coverage for <see cref="CompressedIntSet"/>. Two things separate it from the rest of
/// the set family and are pinned here: enumeration walks the chunk index in order and so yields
/// elements in <b>ascending signed order</b> (a guarantee <see cref="HashSet{T}"/> does not make),
/// and a single enumerator has to cross all three container forms — sorted array, bitmap and
/// run-length — in one pass.
/// </summary>
public class CompressedIntSetEnumerationTests
{
    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenTheSetIsEmpty()
    {
        var set = new CompressedIntSet();

        Assert.Empty(set);
        Assert.False(set.GetEnumerator().MoveNext());
    }

    [Fact]
    public void GetEnumerator_ShouldYieldAscendingSignedOrder_AcrossTheWholeIntRange()
    {
        int[] values = { int.MaxValue, 0, int.MinValue, -1, 1, -70_000, 70_000 };
        var set = new CompressedIntSet(values);

        Assert.Equal(values.OrderBy(v => v), set);
    }

    [Fact]
    public void GetEnumerator_ShouldCrossEveryContainerForm_InOnePass()
    {
        var set = new CompressedIntSet();

        // Chunk 0 (values below 65,536): clustered, so Optimize run-encodes it.
        set.AddRange(0, 3000);
        // Chunk 1: dense but unclustered, so it stays a bitmap.
        foreach (int v in Enumerable.Range(0, 5000))
            set.TryAdd(65_536 + (v * 2));
        // Chunk 2: sparse, so it stays a sorted array.
        foreach (int v in Enumerable.Range(0, 10))
            set.TryAdd(1_000_000 + (v * 31));

        set.Optimize();

        IEnumerable<int> expected = Enumerable.Range(0, 3001)
            .Concat(Enumerable.Range(0, 5000).Select(v => 65_536 + (v * 2)))
            .Concat(Enumerable.Range(0, 10).Select(v => 1_000_000 + (v * 31)));

        Assert.Equal(expected, set);
        Assert.Equal(3001 + 5000 + 10, set.Count);
    }

    [Fact]
    public void GetEnumerator_ShouldWalkEveryValueOfARunContainer_WhenTheChunkIsFullyPopulated()
    {
        var set = new CompressedIntSet();
        set.AddRange(65_536, 131_071); // exactly one whole chunk
        set.Optimize();

        int seen = 0;
        int previous = 65_535;
        foreach (int value in set)
        {
            Assert.Equal(previous + 1, value);
            previous = value;
            seen++;
        }

        Assert.Equal(65_536, seen);
    }

    [Fact]
    public void MoveNext_ShouldThrowInvalidOperationException_WhenTheSetIsModifiedDuringEnumeration()
    {
        var set = new CompressedIntSet(new[] { 1, 2, 3 });

        CompressedIntSet.Enumerator enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        set.TryAdd(4);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldNotThrow_WhenAMutationChangedNothing()
    {
        var set = new CompressedIntSet(new[] { 1, 2, 3 });

        CompressedIntSet.Enumerator enumerator = set.GetEnumerator();
        Assert.False(set.TryAdd(1));
        Assert.False(set.Remove(99));

        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current);
    }

    [Fact]
    public void Reset_ShouldRestartTheEnumeration_WhenTheSetIsUnchanged()
    {
        var set = new CompressedIntSet(new[] { -5, 0, 5 });

        IEnumerator<int> enumerator = ((IEnumerable<int>)set).GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        enumerator.Reset();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(-5, enumerator.Current);
        enumerator.Dispose();
    }

    [Fact]
    public void Reset_ShouldThrowInvalidOperationException_WhenTheSetWasModified()
    {
        var set = new CompressedIntSet(new[] { 1 });

        IEnumerator<int> enumerator = ((IEnumerable<int>)set).GetEnumerator();
        set.TryAdd(2);

        Assert.Throws<InvalidOperationException>(enumerator.Reset);
    }

    [Fact]
    public void Reset_ShouldSucceedOnAnEmptySet_WhenNothingHasChanged()
    {
        var set = new CompressedIntSet();

        IEnumerator<int> enumerator = ((IEnumerable<int>)set).GetEnumerator();
        enumerator.Reset();

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Current_ShouldResetToZero_WhenTheEnumerationIsExhausted()
    {
        var set = new CompressedIntSet(new[] { 9 });

        CompressedIntSet.Enumerator enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(9, enumerator.Current);

        Assert.False(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current);

        // Draining a second time past the end must stay false rather than wrap around.
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void NonGenericEnumerator_ShouldYieldTheSameElements()
    {
        var set = new CompressedIntSet(new[] { 3, -3, 300_000 });

        var seen = new List<int>();
        IEnumerator enumerator = ((IEnumerable)set).GetEnumerator();
        while (enumerator.MoveNext())
            seen.Add((int)enumerator.Current!);

        Assert.Equal(new[] { -3, 3, 300_000 }, seen);
    }

    [Fact]
    public void Dispose_ShouldBeANoOp_AndLeaveTheSetUsable()
    {
        var set = new CompressedIntSet(new[] { 1 });

        CompressedIntSet.Enumerator enumerator = set.GetEnumerator();
        enumerator.Dispose();

        Assert.Equal(1, set.Count);
    }

    // ---- CopyTo -----------------------------------------------------------------------

    [Fact]
    public void CopyTo_ShouldWriteAscendingElementsAtTheOffset()
    {
        var set = new CompressedIntSet(new[] { 5, -5, 0 });
        int[] destination = new int[5];

        set.CopyTo(destination, 1);

        Assert.Equal(new[] { 0, -5, 0, 5, 0 }, destination);
    }

    [Fact]
    public void CopyTo_ShouldThrowArgumentNullException_WhenTheArrayIsNull()
    {
        var set = new CompressedIntSet(new[] { 1 });

        Assert.Throws<ArgumentNullException>(() => set.CopyTo(null!, 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void CopyTo_ShouldThrowArgumentOutOfRangeException_WhenTheIndexIsOutsideTheArray(int index)
    {
        var set = new CompressedIntSet(new[] { 1 });

        Assert.Throws<ArgumentOutOfRangeException>(() => set.CopyTo(new int[3], index));
    }

    [Fact]
    public void CopyTo_ShouldThrowArgumentException_WhenTheArrayHasInsufficientSpace()
    {
        var set = new CompressedIntSet(new[] { 1, 2, 3 });

        Assert.Throws<ArgumentException>(() => set.CopyTo(new int[3], 1));
    }
}
