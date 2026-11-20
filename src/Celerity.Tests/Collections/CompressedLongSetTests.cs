using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Dedicated coverage for <see cref="CompressedLongSet"/>: the core add / contains / remove / clear
/// surface, the full 64-bit value range (including the sign-flipped chunk keys at
/// <see cref="long.MinValue"/> and <see cref="long.MaxValue"/>), the container transitions between the
/// sorted-array, bitmap and run-length forms, and the pieces of the public contract that exist only
/// because the type can hold more than <see cref="int.MaxValue"/> values —
/// <see cref="CompressedLongSet.Cardinality"/>, the <see cref="OverflowException"/> from
/// <see cref="CompressedLongSet.Count"/>, and the chunk-count limit on
/// <see cref="CompressedLongSet.AddRange"/>.
/// </summary>
public class CompressedLongSetTests
{
    // The array→bitmap crossover the type documents. Kept here rather than read from the type so a
    // silent change to the constant fails these tests instead of being absorbed by them.
    private const int ArrayToBitmapThreshold = 4096;
    private const int BitmapPayloadBytes = 8192;

    // A chunk-aligned origin well outside the 32-bit range, so every value below genuinely needs the
    // top 48 bits of the chunk key rather than being an int typed as long.
    private const long Base = 1L << 40;

    private static long[] At(params int[] offsets) => offsets.Select(o => Base + o).ToArray();

    private static IEnumerable<long> RangeAt(int start, int count) => Enumerable.Range(start, count).Select(o => Base + o);

    // ---- construction ----------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldCreateAnEmptySet_WhenNoSourceIsGiven()
    {
        var set = new CompressedLongSet();

        Assert.Equal(0, set.Count);
        Assert.Equal(0L, set.Cardinality);
        Assert.Empty(set);
    }

    [Fact]
    public void Constructor_ShouldDeduplicate_WhenSourceRepeatsValues()
    {
        var set = new CompressedLongSet(new long[] { Base + 5, Base + 5, -Base - 3, Base + 5, -Base - 3, Base + 900_000 });

        Assert.Equal(3, set.Count);
        Assert.Equal(new long[] { -Base - 3, Base + 5, Base + 900_000 }, set);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenSourceIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new CompressedLongSet(null!));
        Assert.Equal("source", ex.ParamName);
    }

    // ---- add / contains / remove -----------------------------------------------------

    [Fact]
    public void TryAdd_ShouldReturnFalseAndLeaveTheSetUnchanged_WhenTheValueIsAlreadyPresent()
    {
        var set = new CompressedLongSet();

        Assert.True(set.TryAdd(Base + 42));
        Assert.False(set.TryAdd(Base + 42));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Add_ShouldThrowArgumentException_WhenTheValueIsAlreadyPresent()
    {
        var set = new CompressedLongSet();
        set.Add(Base + 7);

        var ex = Assert.Throws<ArgumentException>(() => set.Add(Base + 7));
        Assert.Equal("item", ex.ParamName);
    }

    [Fact]
    public void Contains_ShouldReturnFalse_WhenNoChunkCoversTheValue()
    {
        var set = new CompressedLongSet(At(1, 2, 3));

        // A value whose chunk key is absent must be rejected by the index alone, without touching
        // any container — the skip that makes sparse set algebra cheap.
        Assert.False(set.Contains(Base + 10_000_000));
        Assert.False(set.Contains(Base - 10_000_000));

        // Same low 16 bits (indeed the same low 32), different high 32: a different chunk.
        Assert.False(set.Contains(1));
        Assert.False(set.Contains(Base + 1 + (1L << 32)));
    }

    [Fact]
    public void Contains_ShouldRoundTrip_AtTheExtremesOfTheLongRange()
    {
        long[] extremes =
        {
            long.MinValue, long.MinValue + 1, (long)int.MinValue - 1, int.MinValue, -1, 0, 1,
            int.MaxValue, (long)int.MaxValue + 1, long.MaxValue - 1, long.MaxValue,
        };
        var set = new CompressedLongSet(extremes);

        foreach (long value in extremes)
            Assert.True(set.Contains(value));

        Assert.False(set.Contains(long.MinValue + 2));
        Assert.False(set.Contains(long.MaxValue - 2));
        Assert.False(set.Contains((long)int.MaxValue + 2));
        Assert.Equal(extremes.Length, set.Count);

        // The bottom and top chunk keys enumerate first and last.
        Assert.Equal(extremes.OrderBy(v => v), set);
    }

    [Fact]
    public void Contains_ShouldKeepValuesApart_WhenTheyDifferOnlyInTheHigh32Bits()
    {
        // 5 and 5 + 2^32 share their low 32 bits (and so their low 16 bits): only a 48-bit chunk key
        // tells them apart, which a 32-bit key truncation would collapse into one value.
        long low = 5L, high = 5L + (1L << 32), higher = 5L + (1L << 47);
        var set = new CompressedLongSet(new[] { low, high });

        Assert.Equal(2, set.Count);
        Assert.True(set.Contains(low));
        Assert.True(set.Contains(high));
        Assert.False(set.Contains(higher));
        Assert.False(set.Contains(5L - (1L << 32)));

        Assert.True(set.Remove(high));
        Assert.True(set.Contains(low));
        Assert.False(set.Contains(high));
        Assert.Equal(new[] { low }, set);
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenNoChunkCoversTheValue()
    {
        var set = new CompressedLongSet(At(1));

        Assert.False(set.Remove(Base + 10_000_000));
        Assert.False(set.Remove(1)); // same low bits, different chunk
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenTheChunkExistsButTheValueDoesNot()
    {
        var set = new CompressedLongSet(At(1, 3));

        Assert.False(set.Remove(Base + 2));
        Assert.Equal(2, set.Count);
    }

    [Fact]
    public void Remove_ShouldDropTheChunk_WhenItsLastValueGoes()
    {
        var set = new CompressedLongSet(At(1, 5_000_000));
        long before = set.MemoryUsageInBytes;

        Assert.True(set.Remove(Base + 5_000_000));

        Assert.Equal(1, set.Count);
        Assert.True(set.MemoryUsageInBytes < before);

        // The dropped chunk must not resurface as a phantom hit or block a later re-add.
        Assert.False(set.Contains(Base + 5_000_000));
        Assert.True(set.TryAdd(Base + 5_000_000));
        Assert.True(set.Contains(Base + 5_000_000));
    }

    [Fact]
    public void Clear_ShouldEmptyTheSetAndReleaseEveryContainer_WhenItIsPopulated()
    {
        var set = new CompressedLongSet(RangeAt(0, 5000).Concat(new[] { -1L, long.MaxValue }));
        Assert.True(set.MemoryUsageInBytes > BitmapPayloadBytes);

        set.Clear();

        Assert.Equal(0, set.Count);
        Assert.Empty(set);
        Assert.False(set.Contains(Base));

        // Every container payload is gone, but the chunk index keeps its capacity so the set can be
        // refilled without regrowing it — which is why the reported footprint is not yet zero.
        Assert.True(set.MemoryUsageInBytes > 0);
        Assert.True(set.MemoryUsageInBytes < BitmapPayloadBytes);

        set.Optimize();
        Assert.Equal(0, set.MemoryUsageInBytes);
    }

    [Fact]
    public void Clear_ShouldLeaveEnumeratorsValid_WhenTheSetIsAlreadyEmpty()
    {
        var set = new CompressedLongSet();

        using IEnumerator<long> live = ((IEnumerable<long>)set).GetEnumerator();
        set.Clear();

        Assert.False(live.MoveNext());
    }

    // ---- container transitions -------------------------------------------------------

    [Fact]
    public void TryAdd_ShouldPromoteTheChunkToABitmap_WhenItGrowsPastTheArrayThreshold()
    {
        var set = new CompressedLongSet();
        for (int i = 0; i <= ArrayToBitmapThreshold; i++)
            set.TryAdd(Base + (i * 2)); // stride 2 keeps it in one chunk without becoming a single run

        Assert.Equal(ArrayToBitmapThreshold + 1, set.Count);
        Assert.True(set.MemoryUsageInBytes >= BitmapPayloadBytes);

        // Every value must survive the promotion, and neither neighbour may appear.
        for (int i = 0; i <= ArrayToBitmapThreshold; i++)
        {
            Assert.True(set.Contains(Base + (i * 2)));
            Assert.False(set.Contains(Base + (i * 2) + 1));
        }
    }

    [Fact]
    public void Remove_ShouldKeepTheBitmapForm_WhenTheChunkFallsBackBelowTheArrayThreshold()
    {
        var set = new CompressedLongSet(Enumerable.Range(0, ArrayToBitmapThreshold + 100).Select(i => Base + (i * 2)));
        Assert.True(set.MemoryUsageInBytes >= BitmapPayloadBytes);

        for (int i = 0; i < 200; i++)
            Assert.True(set.Remove(Base + (i * 2)));

        // Documented: a removal never demotes the representation — only Optimize (or a bulk set
        // operation that rewrites the container) does.
        Assert.True(set.MemoryUsageInBytes >= BitmapPayloadBytes);
        Assert.Equal(ArrayToBitmapThreshold - 100, set.Count);

        set.Optimize();
        Assert.True(set.MemoryUsageInBytes < BitmapPayloadBytes);
        Assert.Equal(ArrayToBitmapThreshold - 100, set.Count);
    }

    [Fact]
    public void TryAddAndRemove_ShouldReportNoChange_WhenTheChunkIsABitmap()
    {
        var set = new CompressedLongSet(Enumerable.Range(0, ArrayToBitmapThreshold + 100).Select(i => Base + (i * 2)));
        Assert.True(set.MemoryUsageInBytes >= BitmapPayloadBytes);

        Assert.False(set.TryAdd(Base)); // the bit is already set
        Assert.False(set.Remove(Base + 1)); // the bit was never set
        Assert.Equal(ArrayToBitmapThreshold + 100, set.Count);
    }

    [Fact]
    public void TryAdd_ShouldExpandTheRunContainer_WhenItLandsInAnOptimizedChunk()
    {
        var set = new CompressedLongSet();
        set.AddRange(Base + 100, Base + 200);
        long runBytes = set.MemoryUsageInBytes;

        // 500 is outside the run, so the chunk must expand to a form that can hold it.
        Assert.True(set.TryAdd(Base + 500));

        Assert.Equal(102, set.Count);
        Assert.True(set.MemoryUsageInBytes > runBytes);
        Assert.True(set.Contains(Base + 500));
        Assert.True(set.Contains(Base + 100));
        Assert.True(set.Contains(Base + 200));
        Assert.False(set.Contains(Base + 201));
        Assert.Equal(RangeAt(100, 101).Append(Base + 500), set);
    }

    [Fact]
    public void Remove_ShouldExpandTheRunContainerToABitmap_WhenItIsTooDenseForAnArray()
    {
        var set = new CompressedLongSet();
        set.AddRange(Base, Base + 9999); // one run, 10,000 values — far past the array threshold

        Assert.True(set.Remove(Base + 5000));

        Assert.Equal(9999, set.Count);
        Assert.True(set.MemoryUsageInBytes >= BitmapPayloadBytes);
        Assert.False(set.Contains(Base + 5000));
        Assert.True(set.Contains(Base + 4999));
        Assert.True(set.Contains(Base + 5001));
    }

    [Fact]
    public void Contains_ShouldProbeEveryRunBoundary_WhenTheChunkIsRunEncoded()
    {
        var set = new CompressedLongSet(At(10, 11, 12, 500, 501, 9000));
        set.Optimize();

        foreach (long present in At(10, 11, 12, 500, 501, 9000))
            Assert.True(set.Contains(present));

        // Below the first run, between runs, and above the last — the three ways the run binary
        // search can miss.
        foreach (long absent in At(9, 13, 499, 502, 8999, 9001))
            Assert.False(set.Contains(absent));
    }

    // ---- AddRange ---------------------------------------------------------------------

    [Fact]
    public void AddRange_ShouldThrowArgumentOutOfRangeException_WhenTheEndPrecedesTheStart()
    {
        var set = new CompressedLongSet();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => set.AddRange(Base + 10, Base + 9));
        Assert.Equal("endInclusive", ex.ParamName);
    }

    [Fact]
    public void AddRange_ShouldAddASingleValue_WhenTheRangeIsDegenerate()
    {
        var set = new CompressedLongSet();

        Assert.Equal(1L, set.AddRange(Base + 7, Base + 7));
        Assert.Equal(new[] { Base + 7 }, set);
    }

    [Fact]
    public void AddRange_ShouldSpanEveryCoveredChunk_WhenTheRangeCrossesChunkBoundaries()
    {
        var set = new CompressedLongSet();

        // Three chunks: a partial head, a whole middle, a partial tail.
        long added = set.AddRange(Base + 65_000, Base + 200_000);

        Assert.Equal(200_000L - 65_000 + 1, added);
        Assert.Equal(added, set.Cardinality);
        foreach (long probe in At(65_000, 65_535, 65_536, 131_071, 131_072, 199_999, 200_000))
            Assert.True(set.Contains(probe));

        Assert.False(set.Contains(Base + 64_999));
        Assert.False(set.Contains(Base + 200_001));
    }

    [Fact]
    public void AddRange_ShouldCountOnlyTheNewValues_WhenTheRangeOverlapsAnExistingChunk()
    {
        var set = new CompressedLongSet(At(5, 10, 15, 5_000_000));

        long added = set.AddRange(Base, Base + 20);

        Assert.Equal(18L, added); // 21 in the range, 3 of them already present
        Assert.Equal(22, set.Count);
        Assert.True(set.Contains(Base + 5_000_000));
        Assert.Equal(RangeAt(0, 21).Append(Base + 5_000_000), set);
    }

    [Fact]
    public void AddRange_ShouldCrossWordBoundariesExactly_WhenMergedIntoAnExistingChunk()
    {
        // The bit-range writer has a single-word path and a multi-word path; drive both against a
        // chunk that already exists so the merge goes through the bitmap. 1_000_000 is not
        // chunk-aligned, but Base is, so the in-chunk offsets match the 32-bit suite's exactly.
        var set = new CompressedLongSet(At(1_000_000));

        Assert.Equal(5L, set.AddRange(Base + 1_000_010, Base + 1_000_014)); // inside one 64-bit word
        Assert.Equal(200L, set.AddRange(Base + 1_000_100, Base + 1_000_299)); // spans several words

        // Ends exactly on a word boundary, which is the high-mask special case: the closing word is
        // filled entirely rather than partially, and computing that mask by shifting would overflow.
        Assert.Equal(128L, set.AddRange(Base + 1_000_320, Base + 1_000_447));
        Assert.True(set.Contains(Base + 1_000_447));
        Assert.False(set.Contains(Base + 1_000_448));

        Assert.Equal(334, set.Count);
        Assert.True(set.Contains(Base + 1_000_010));
        Assert.True(set.Contains(Base + 1_000_014));
        Assert.False(set.Contains(Base + 1_000_015));
        Assert.True(set.Contains(Base + 1_000_100));
        Assert.True(set.Contains(Base + 1_000_299));
        Assert.False(set.Contains(Base + 1_000_300));
    }

    [Fact]
    public void AddRange_ShouldReturnZeroAndLeaveTheSetUnchanged_WhenEveryValueIsAlreadyPresent()
    {
        var set = new CompressedLongSet();
        set.AddRange(Base, Base + 100);

        Assert.Equal(0L, set.AddRange(Base + 10, Base + 20));
        Assert.Equal(101, set.Count);
    }

    // ---- AddRange: 64-bit specifics ---------------------------------------------------------

    [Fact]
    public void AddRange_ShouldThrowAndLeaveTheSetUntouched_WhenTheRangeSpansTooManyChunks()
    {
        // The whole 64-bit space is 2^48 chunks, far past Array.MaxLength: rejected up front,
        // before a single chunk is written and before the version moves.
        var set = new CompressedLongSet(new long[] { -7, Base });
        using IEnumerator<long> live = ((IEnumerable<long>)set).GetEnumerator();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => set.AddRange(long.MinValue, long.MaxValue));
        Assert.Equal("endInclusive", ex.ParamName);

        Assert.Equal(2L, set.Cardinality);
        Assert.Equal(new long[] { -7, Base }, set);
        Assert.True(live.MoveNext());
        Assert.Equal(-7L, live.Current);
    }

    [Fact]
    public void AddRange_ShouldThrow_WhenTheRangeSpansExactlyOneChunkMoreThanTheLimit()
    {
        // Array.MaxLength chunks (span = MaxLength - 1 in key distance) is the documented ceiling
        // — not allocated here — so one key further must be rejected with the same parameter name.
        var set = new CompressedLongSet();
        long width = (long)Array.MaxLength * 65_536;

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => set.AddRange(0, width));
        Assert.Equal("endInclusive", ex.ParamName);
        Assert.Equal(0L, set.Cardinality);
    }

    [Fact]
    public void AddRange_ShouldStoreARangeAcrossA64BitChunkBoundary_InAscendingOrder()
    {
        var set = new CompressedLongSet();
        long start = (1L << 40) - 70_000, end = (1L << 40) + 70_000;

        Assert.Equal(140_001L, set.AddRange(start, end));
        Assert.Equal(140_001, set.Count);

        long expected = start;
        foreach (long value in set)
            Assert.Equal(expected++, value);

        Assert.Equal(end + 1, expected);
        Assert.False(set.Contains(start - 1));
        Assert.False(set.Contains(end + 1));
    }

    [Fact]
    public void AddRange_ShouldStraddleZeroAndTheInt32Boundaries_InSignedOrder()
    {
        var set = new CompressedLongSet();
        set.AddRange(-3, 3);
        set.AddRange((long)int.MaxValue - 1, (long)int.MaxValue + 2);
        set.AddRange((long)int.MinValue - 2, (long)int.MinValue + 1);

        long[] expected =
        {
            (long)int.MinValue - 2, (long)int.MinValue - 1, int.MinValue, (long)int.MinValue + 1,
            -3, -2, -1, 0, 1, 2, 3,
            (long)int.MaxValue - 1, int.MaxValue, (long)int.MaxValue + 1, (long)int.MaxValue + 2,
        };
        Assert.Equal(expected, set);
    }

    [Fact]
    public void AddRange_ShouldReachTheTopAndBottomChunks_AtTheEndsOfTheLongRange()
    {
        var set = new CompressedLongSet();

        Assert.Equal(3L, set.AddRange(long.MaxValue - 2, long.MaxValue));
        Assert.Equal(3L, set.AddRange(long.MinValue, long.MinValue + 2));

        Assert.Equal(
            new[] { long.MinValue, long.MinValue + 1, long.MinValue + 2, long.MaxValue - 2, long.MaxValue - 1, long.MaxValue },
            set);
    }

    [Fact]
    public void AddRange_ShouldLeaveALiveEnumeratorValid_WhenItAddsNothing()
    {
        var set = new CompressedLongSet();
        set.AddRange(Base, Base + 100);

        using IEnumerator<long> live = ((IEnumerable<long>)set).GetEnumerator();
        Assert.Equal(0L, set.AddRange(Base + 10, Base + 20));

        Assert.True(live.MoveNext());
        Assert.Equal(Base, live.Current);
    }

    [Fact]
    public void AddRange_ShouldInvalidateALiveEnumerator_WhenItAddsSomething()
    {
        var set = new CompressedLongSet();
        set.AddRange(Base, Base + 100);

        using IEnumerator<long> live = ((IEnumerable<long>)set).GetEnumerator();
        Assert.Equal(1L, set.AddRange(Base + 100, Base + 101));

        Assert.Throws<InvalidOperationException>(() => live.MoveNext());
    }

    // ---- Cardinality / Count -----------------------------------------------------------

    [Fact]
    public void Count_ShouldThrowOverflowException_WhenTheSetHoldsMoreValuesThanAnInt32()
    {
        // 2^31 + 6 values straddling zero, as one run per chunk (~32k chunks): the case Count
        // structurally cannot answer, and the reason Cardinality exists.
        const long Expected = (1L << 31) + 6;
        var set = new CompressedLongSet();
        Assert.Equal(Expected, set.AddRange(-(1L << 30), (1L << 30) + 5));

        Assert.Equal(Expected, set.Cardinality);
        Assert.Throws<OverflowException>(() => set.Count);

        Assert.True(set.Contains(-(1L << 30)));
        Assert.True(set.Contains(0));
        Assert.True(set.Contains((1L << 30) + 5));
        Assert.False(set.Contains((1L << 30) + 6));

        // And it is genuinely compressed: 2^31 values in a few megabytes.
        Assert.True(set.MemoryUsageInBytes < 16L * 1024 * 1024);
    }

    [Fact]
    public void Cardinality_ShouldExceedInt32_ForAWideRangeOfGenuinely64BitValues()
    {
        // Same shape, but entirely above 2^40, so every chunk key is past the 32-bit range.
        var set = new CompressedLongSet();
        set.AddRange(Base, Base + (1L << 31) + 5);

        Assert.Equal((1L << 31) + 6, set.Cardinality);
        Assert.Throws<OverflowException>(() => set.Count);
        Assert.True(set.Contains(Base + (1L << 31)));
        Assert.False(set.Contains(1L << 31));
    }

    [Fact]
    public void Count_ShouldAgreeWithCardinality_WhenTheSetFitsAnInt32()
    {
        var set = new CompressedLongSet(At(1, 2, 3));

        Assert.Equal(3, set.Count);
        Assert.Equal(3L, set.Cardinality);
    }

    // ---- Optimize ----------------------------------------------------------------------

    [Fact]
    public void Optimize_ShouldCollapseClusteredChunksToRuns_WithoutChangingTheContents()
    {
        var set = new CompressedLongSet(RangeAt(0, 20_000));
        long before = set.MemoryUsageInBytes;

        set.Optimize();

        Assert.True(set.MemoryUsageInBytes < before / 10);
        Assert.Equal(20_000, set.Count);
        Assert.Equal(RangeAt(0, 20_000), set);
    }

    [Fact]
    public void Optimize_ShouldKeepTheBitmapForm_WhenTheChunkIsDenseButUnclustered()
    {
        // Every other value: 5000 runs in one chunk, so run pairs would cost 20 KB against the
        // bitmap's 8 KB. The bitmap has to stay.
        var set = new CompressedLongSet(Enumerable.Range(0, 5000).Select(i => Base + (i * 2)));
        set.Optimize();

        Assert.True(set.MemoryUsageInBytes >= BitmapPayloadBytes);
        Assert.Equal(5000, set.Count);
        Assert.True(set.Contains(Base + 9998));
        Assert.False(set.Contains(Base + 9999));
    }

    [Fact]
    public void Optimize_ShouldTrimAnOversizedArrayContainer_WhenTheChunkIsSparseAndUnclustered()
    {
        var set = new CompressedLongSet();
        for (int i = 0; i < 300; i++)
            set.TryAdd(Base + (i * 7));

        long before = set.MemoryUsageInBytes;
        set.Optimize();

        // The doubling growth left slack in both the value array and the chunk index.
        Assert.True(set.MemoryUsageInBytes < before);
        Assert.Equal(300, set.Count);
        Assert.Equal(Enumerable.Range(0, 300).Select(i => Base + (i * 7)), set);
    }

    [Fact]
    public void Optimize_ShouldBeIdempotent_WhenCalledTwice()
    {
        var set = new CompressedLongSet();
        set.AddRange(Base, Base + 500);
        set.AddRange(Base + 1_000_000, Base + 1_000_500);
        set.TryAdd(-Base - 5);

        set.Optimize();
        long once = set.MemoryUsageInBytes;
        long[] snapshot = set.ToArray();

        set.Optimize();

        Assert.Equal(once, set.MemoryUsageInBytes);
        Assert.Equal(snapshot, set);
    }

    [Fact]
    public void Optimize_ShouldLeaveEnumeratorsValid_WhenItOnlyChangesTheRepresentation()
    {
        var set = new CompressedLongSet();
        set.AddRange(Base, Base + 5000);

        using IEnumerator<long> live = ((IEnumerable<long>)set).GetEnumerator();
        set.Optimize();

        Assert.True(live.MoveNext());
        Assert.Equal(Base, live.Current);
    }

    [Fact]
    public void MemoryUsageInBytes_ShouldBeFarBelowAHashSet_WhenTheDataIsClustered()
    {
        var set = new CompressedLongSet();
        set.AddRange(Base, Base + 999_999);
        set.Optimize();

        // A million consecutive values in one run per chunk: sixteen chunks, four bytes of run each.
        Assert.Equal(1_000_000, set.Count);
        Assert.True(set.MemoryUsageInBytes < 4096);
    }

    // ---- explicit interface members -----------------------------------------------------

    [Fact]
    public void ISetAdd_ShouldReportWhetherTheElementWasNew()
    {
        ISet<long> set = new CompressedLongSet();

        Assert.True(set.Add(Base + 1));
        Assert.False(set.Add(Base + 1));
    }

    [Fact]
    public void ICollectionAdd_ShouldNotThrow_WhenTheElementIsAlreadyPresent()
    {
        ICollection<long> set = new CompressedLongSet();

        set.Add(Base + 1);
        set.Add(Base + 1);

        Assert.Equal(1, set.Count);
        Assert.False(set.IsReadOnly);
    }

    [Fact]
    public void Set_ShouldBeAssignableToBothSetInterfaces()
    {
        var set = new CompressedLongSet();

        Assert.IsAssignableFrom<ISet<long>>(set);
        Assert.IsAssignableFrom<IReadOnlySet<long>>(set);
    }
}
