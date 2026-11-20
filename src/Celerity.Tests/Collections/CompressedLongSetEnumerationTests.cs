using System.Collections;
using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Enumeration coverage for <see cref="CompressedLongSet"/>. Two things separate it from the rest of
/// the set family and are pinned here: enumeration walks the chunk index in order and so yields
/// elements in <b>ascending signed order</b> across the whole 64-bit range (a guarantee
/// <see cref="HashSet{T}"/> does not make, and one that rests on the sign bit being flipped into the
/// chunk key), and a single enumerator has to cross all three container forms — sorted array, bitmap
/// and run-length — in one pass.
/// </summary>
public class CompressedLongSetEnumerationTests
{
    // A chunk-aligned origin outside the 32-bit range, so the chunk keys below need their top bits.
    private const long Base = 1L << 40;

    [Fact]
    public void GetEnumerator_ShouldYieldNothing_WhenTheSetIsEmpty()
    {
        var set = new CompressedLongSet();

        Assert.Empty(set);
        Assert.False(set.GetEnumerator().MoveNext());
    }

    [Fact]
    public void GetEnumerator_ShouldYieldAscendingSignedOrder_AcrossTheWholeLongRange()
    {
        long[] values =
        {
            long.MaxValue, 0, long.MinValue, -1, 1, -70_000, 70_000,
            int.MaxValue, int.MinValue, (long)int.MaxValue + 1, (long)int.MinValue - 1,
            Base, -Base, 5L + (1L << 32), 5L, long.MinValue + 65_536, long.MaxValue - 65_536,
        };
        var set = new CompressedLongSet(values);

        Assert.Equal(values.OrderBy(v => v), set);
    }

    [Fact]
    public void GetEnumerator_ShouldYieldAscendingSignedOrder_AcrossManyChunkKeys()
    {
        // Hundreds of chunks spread over negative and positive values at several magnitudes,
        // inserted in a scrambled order so the index has to be kept sorted by insertion.
        var rng = new Random(20261120);
        var values = new HashSet<long>();
        while (values.Count < 2000)
        {
            int shift = rng.Next(0, 63);
            long magnitude = (long)(rng.NextDouble() * (1L << shift));
            values.Add(rng.Next(2) == 0 ? magnitude : -magnitude - 1);
        }

        values.Add(long.MinValue);
        values.Add(long.MaxValue);

        var set = new CompressedLongSet(values);

        Assert.Equal(values.OrderBy(v => v), set);
    }

    [Fact]
    public void GetEnumerator_ShouldCrossEveryContainerForm_InOnePass()
    {
        var set = new CompressedLongSet();

        // First chunk: clustered, so Optimize run-encodes it.
        set.AddRange(Base, Base + 3000);
        // Next chunk: dense but unclustered, so it stays a bitmap.
        foreach (int v in Enumerable.Range(0, 5000))
            set.TryAdd(Base + 65_536 + (v * 2));
        // A later chunk: sparse, so it stays a sorted array.
        foreach (int v in Enumerable.Range(0, 10))
            set.TryAdd(Base + 1_000_000 + (v * 31));

        set.Optimize();

        IEnumerable<long> expected = Enumerable.Range(0, 3001).Select(v => Base + v)
            .Concat(Enumerable.Range(0, 5000).Select(v => Base + 65_536 + (v * 2)))
            .Concat(Enumerable.Range(0, 10).Select(v => Base + 1_000_000 + (v * 31)));

        Assert.Equal(expected, set);
        Assert.Equal(3001 + 5000 + 10, set.Count);
    }

    [Fact]
    public void GetEnumerator_ShouldWalkEveryValueOfARunContainer_WhenTheChunkIsFullyPopulated()
    {
        var set = new CompressedLongSet();
        set.AddRange(Base + 65_536, Base + 131_071); // exactly one whole chunk
        set.Optimize();

        int seen = 0;
        long previous = Base + 65_535;
        foreach (long value in set)
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
        var set = new CompressedLongSet(new[] { Base + 1, Base + 2, Base + 3 });

        CompressedLongSet.Enumerator enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        set.TryAdd(Base + 4);

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    [Fact]
    public void MoveNext_ShouldNotThrow_WhenAMutationChangedNothing()
    {
        var set = new CompressedLongSet(new[] { Base + 1, Base + 2, Base + 3 });

        CompressedLongSet.Enumerator enumerator = set.GetEnumerator();
        Assert.False(set.TryAdd(Base + 1));
        Assert.False(set.Remove(Base + 99));
        Assert.False(set.Remove(1)); // same low bits, absent chunk

        Assert.True(enumerator.MoveNext());
        Assert.Equal(Base + 1, enumerator.Current);
    }

    [Fact]
    public void Reset_ShouldRestartTheEnumeration_WhenTheSetIsUnchanged()
    {
        var set = new CompressedLongSet(new[] { -Base - 5, 0, Base + 5 });

        IEnumerator<long> enumerator = ((IEnumerable<long>)set).GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.True(enumerator.MoveNext());
        enumerator.Reset();

        Assert.True(enumerator.MoveNext());
        Assert.Equal(-Base - 5, enumerator.Current);
        enumerator.Dispose();
    }

    [Fact]
    public void Reset_ShouldThrowInvalidOperationException_WhenTheSetWasModified()
    {
        var set = new CompressedLongSet(new[] { Base + 1 });

        IEnumerator<long> enumerator = ((IEnumerable<long>)set).GetEnumerator();
        set.TryAdd(Base + 2);

        Assert.Throws<InvalidOperationException>(enumerator.Reset);
    }

    [Fact]
    public void Reset_ShouldSucceedOnAnEmptySet_WhenNothingHasChanged()
    {
        var set = new CompressedLongSet();

        IEnumerator<long> enumerator = ((IEnumerable<long>)set).GetEnumerator();
        enumerator.Reset();

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Current_ShouldResetToZero_WhenTheEnumerationIsExhausted()
    {
        var set = new CompressedLongSet(new[] { long.MaxValue });

        CompressedLongSet.Enumerator enumerator = set.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(long.MaxValue, enumerator.Current);

        Assert.False(enumerator.MoveNext());
        Assert.Equal(0L, enumerator.Current);

        // Draining a second time past the end must stay false rather than wrap around.
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void NonGenericEnumerator_ShouldYieldTheSameElements()
    {
        var set = new CompressedLongSet(new[] { Base + 3, -Base - 3, Base + 300_000 });

        var seen = new List<long>();
        IEnumerator enumerator = ((IEnumerable)set).GetEnumerator();
        while (enumerator.MoveNext())
            seen.Add((long)enumerator.Current!);

        Assert.Equal(new[] { -Base - 3, Base + 3, Base + 300_000 }, seen);
    }

    [Fact]
    public void Dispose_ShouldBeANoOp_AndLeaveTheSetUsable()
    {
        var set = new CompressedLongSet(new[] { Base + 1 });

        CompressedLongSet.Enumerator enumerator = set.GetEnumerator();
        enumerator.Dispose();

        Assert.Equal(1, set.Count);
    }

    // ---- CopyTo -----------------------------------------------------------------------

    [Fact]
    public void CopyTo_ShouldWriteAscendingElementsAtTheOffset()
    {
        var set = new CompressedLongSet(new[] { Base + 5, -Base - 5, 0 });
        long[] destination = new long[5];

        set.CopyTo(destination, 1);

        Assert.Equal(new[] { 0, -Base - 5, 0, Base + 5, 0 }, destination);
    }

    [Fact]
    public void CopyTo_ShouldThrowArgumentNullException_WhenTheArrayIsNull()
    {
        var set = new CompressedLongSet(new[] { Base + 1 });

        Assert.Throws<ArgumentNullException>(() => set.CopyTo(null!, 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void CopyTo_ShouldThrowArgumentOutOfRangeException_WhenTheIndexIsOutsideTheArray(int index)
    {
        var set = new CompressedLongSet(new[] { Base + 1 });

        Assert.Throws<ArgumentOutOfRangeException>(() => set.CopyTo(new long[3], index));
    }

    [Fact]
    public void CopyTo_ShouldThrowArgumentException_WhenTheArrayHasInsufficientSpace()
    {
        var set = new CompressedLongSet(new[] { Base + 1, Base + 2, Base + 3 });

        Assert.Throws<ArgumentException>(() => set.CopyTo(new long[3], 1));
    }
}
