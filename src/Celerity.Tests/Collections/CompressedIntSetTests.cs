using Celerity.Collections;

namespace Celerity.Tests.Collections;

/// <summary>
/// Dedicated coverage for <see cref="CompressedIntSet"/>: the core add / contains / remove / clear
/// surface, the full 32-bit value range (which is what separates it from <see cref="BitSet"/> and
/// <see cref="SparseSet"/>), the container transitions between the sorted-array, bitmap and
/// run-length forms, and the two pieces of the public contract that exist only because the type can
/// hold every <see cref="int"/> — <see cref="CompressedIntSet.Cardinality"/> and the
/// <see cref="OverflowException"/> from <see cref="CompressedIntSet.Count"/>.
/// </summary>
public class CompressedIntSetTests
{
    // The array→bitmap crossover the type documents. Kept here rather than read from the type so a
    // silent change to the constant fails these tests instead of being absorbed by them.
    private const int ArrayToBitmapThreshold = 4096;
    private const int BitmapPayloadBytes = 8192;

    // ---- construction ----------------------------------------------------------------

    [Fact]
    public void Constructor_ShouldCreateAnEmptySet_WhenNoSourceIsGiven()
    {
        var set = new CompressedIntSet();

        Assert.Equal(0, set.Count);
        Assert.Equal(0L, set.Cardinality);
        Assert.Empty(set);
    }

    [Fact]
    public void Constructor_ShouldDeduplicate_WhenSourceRepeatsValues()
    {
        var set = new CompressedIntSet(new[] { 5, 5, -3, 5, -3, 900_000 });

        Assert.Equal(3, set.Count);
        Assert.Equal(new[] { -3, 5, 900_000 }, set);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenSourceIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new CompressedIntSet(null!));
        Assert.Equal("source", ex.ParamName);
    }

    // ---- add / contains / remove -----------------------------------------------------

    [Fact]
    public void TryAdd_ShouldReturnFalseAndLeaveTheSetUnchanged_WhenTheValueIsAlreadyPresent()
    {
        var set = new CompressedIntSet();

        Assert.True(set.TryAdd(42));
        Assert.False(set.TryAdd(42));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Add_ShouldThrowArgumentException_WhenTheValueIsAlreadyPresent()
    {
        var set = new CompressedIntSet();
        set.Add(7);

        var ex = Assert.Throws<ArgumentException>(() => set.Add(7));
        Assert.Equal("item", ex.ParamName);
    }

    [Fact]
    public void Contains_ShouldReturnFalse_WhenNoChunkCoversTheValue()
    {
        var set = new CompressedIntSet(new[] { 1, 2, 3 });

        // A value whose chunk key is absent must be rejected by the index alone, without touching
        // any container — the skip that makes sparse set algebra cheap.
        Assert.False(set.Contains(10_000_000));
        Assert.False(set.Contains(-10_000_000));
    }

    [Fact]
    public void Contains_ShouldRoundTrip_AtTheExtremesOfTheIntRange()
    {
        int[] extremes = { int.MinValue, int.MinValue + 1, -1, 0, 1, int.MaxValue - 1, int.MaxValue };
        var set = new CompressedIntSet(extremes);

        foreach (int value in extremes)
            Assert.True(set.Contains(value));

        Assert.False(set.Contains(int.MinValue + 2));
        Assert.False(set.Contains(int.MaxValue - 2));
        Assert.Equal(extremes.Length, set.Count);
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenNoChunkCoversTheValue()
    {
        var set = new CompressedIntSet(new[] { 1 });

        Assert.False(set.Remove(10_000_000));
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Remove_ShouldReturnFalse_WhenTheChunkExistsButTheValueDoesNot()
    {
        var set = new CompressedIntSet(new[] { 1, 3 });

        Assert.False(set.Remove(2));
        Assert.Equal(2, set.Count);
    }

    [Fact]
    public void Remove_ShouldDropTheChunk_WhenItsLastValueGoes()
    {
        var set = new CompressedIntSet(new[] { 1, 5_000_000 });
        long before = set.MemoryUsageInBytes;

        Assert.True(set.Remove(5_000_000));

        Assert.Equal(1, set.Count);
        Assert.True(set.MemoryUsageInBytes < before);

        // The dropped chunk must not resurface as a phantom hit or block a later re-add.
        Assert.False(set.Contains(5_000_000));
        Assert.True(set.TryAdd(5_000_000));
        Assert.True(set.Contains(5_000_000));
    }

    [Fact]
    public void Clear_ShouldEmptyTheSetAndReleaseEveryContainer_WhenItIsPopulated()
    {
        var set = new CompressedIntSet(Enumerable.Range(0, 5000).Concat(new[] { -1, int.MaxValue }));
        Assert.True(set.MemoryUsageInBytes > BitmapPayloadBytes);

        set.Clear();

        Assert.Equal(0, set.Count);
        Assert.Empty(set);
        Assert.False(set.Contains(0));

        // Every container payload is gone, but the chunk index keeps its capacity so the set can be
        // refilled without regrowing it — which is why the reported footprint is not yet zero.
        Assert.True(set.MemoryUsageInBytes > 0);
        Assert.True(set.MemoryUsageInBytes < BitmapPayloadBytes);

        set.Optimize();
        Assert.Equal(0, set.MemoryUsageInBytes);
    }

    // ---- container transitions -------------------------------------------------------

    [Fact]
    public void TryAdd_ShouldPromoteTheChunkToABitmap_WhenItGrowsPastTheArrayThreshold()
    {
        var set = new CompressedIntSet();
        for (int i = 0; i <= ArrayToBitmapThreshold; i++)
            set.TryAdd(i * 2); // stride 2 keeps it in one chunk without becoming a single run

        Assert.Equal(ArrayToBitmapThreshold + 1, set.Count);
        Assert.True(set.MemoryUsageInBytes >= BitmapPayloadBytes);

        // Every value must survive the promotion, and neither neighbour may appear.
        for (int i = 0; i <= ArrayToBitmapThreshold; i++)
        {
            Assert.True(set.Contains(i * 2));
            Assert.False(set.Contains((i * 2) + 1));
        }
    }

    [Fact]
    public void Remove_ShouldKeepTheBitmapForm_WhenTheChunkFallsBackBelowTheArrayThreshold()
    {
        var set = new CompressedIntSet(Enumerable.Range(0, ArrayToBitmapThreshold + 100).Select(i => i * 2));
        Assert.True(set.MemoryUsageInBytes >= BitmapPayloadBytes);

        for (int i = 0; i < 200; i++)
            Assert.True(set.Remove(i * 2));

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
        var set = new CompressedIntSet(Enumerable.Range(0, ArrayToBitmapThreshold + 100).Select(i => i * 2));
        Assert.True(set.MemoryUsageInBytes >= BitmapPayloadBytes);

        Assert.False(set.TryAdd(0)); // the bit is already set
        Assert.False(set.Remove(1)); // the bit was never set
        Assert.Equal(ArrayToBitmapThreshold + 100, set.Count);
    }

    [Fact]
    public void TryAdd_ShouldExpandTheRunContainer_WhenItLandsInAnOptimizedChunk()
    {
        var set = new CompressedIntSet();
        set.AddRange(100, 200);
        long runBytes = set.MemoryUsageInBytes;

        // 500 is outside the run, so the chunk must expand to a form that can hold it.
        Assert.True(set.TryAdd(500));

        Assert.Equal(102, set.Count);
        Assert.True(set.MemoryUsageInBytes > runBytes);
        Assert.True(set.Contains(500));
        Assert.True(set.Contains(100));
        Assert.True(set.Contains(200));
        Assert.False(set.Contains(201));
        Assert.Equal(Enumerable.Range(100, 101).Append(500), set);
    }

    [Fact]
    public void Remove_ShouldExpandTheRunContainerToABitmap_WhenItIsTooDenseForAnArray()
    {
        var set = new CompressedIntSet();
        set.AddRange(0, 9999); // one run, 10,000 values — far past the array threshold

        Assert.True(set.Remove(5000));

        Assert.Equal(9999, set.Count);
        Assert.True(set.MemoryUsageInBytes >= BitmapPayloadBytes);
        Assert.False(set.Contains(5000));
        Assert.True(set.Contains(4999));
        Assert.True(set.Contains(5001));
    }

    [Fact]
    public void Contains_ShouldProbeEveryRunBoundary_WhenTheChunkIsRunEncoded()
    {
        var set = new CompressedIntSet(new[] { 10, 11, 12, 500, 501, 9000 });
        set.Optimize();

        foreach (int present in new[] { 10, 11, 12, 500, 501, 9000 })
            Assert.True(set.Contains(present));

        // Below the first run, between runs, and above the last — the three ways the run binary
        // search can miss.
        foreach (int absent in new[] { 9, 13, 499, 502, 8999, 9001 })
            Assert.False(set.Contains(absent));
    }

    // ---- AddRange ---------------------------------------------------------------------

    [Fact]
    public void AddRange_ShouldThrowArgumentOutOfRangeException_WhenTheEndPrecedesTheStart()
    {
        var set = new CompressedIntSet();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => set.AddRange(10, 9));
        Assert.Equal("endInclusive", ex.ParamName);
    }

    [Fact]
    public void AddRange_ShouldAddASingleValue_WhenTheRangeIsDegenerate()
    {
        var set = new CompressedIntSet();

        Assert.Equal(1L, set.AddRange(7, 7));
        Assert.Equal(new[] { 7 }, set);
    }

    [Fact]
    public void AddRange_ShouldSpanEveryCoveredChunk_WhenTheRangeCrossesChunkBoundaries()
    {
        var set = new CompressedIntSet();

        // Three chunks: a partial head, a whole middle, a partial tail.
        long added = set.AddRange(65_000, 200_000);

        Assert.Equal(200_000L - 65_000 + 1, added);
        Assert.Equal(added, set.Cardinality);
        foreach (int probe in new[] { 65_000, 65_535, 65_536, 131_071, 131_072, 199_999, 200_000 })
            Assert.True(set.Contains(probe));

        Assert.False(set.Contains(64_999));
        Assert.False(set.Contains(200_001));
    }

    [Fact]
    public void AddRange_ShouldCountOnlyTheNewValues_WhenTheRangeOverlapsAnExistingChunk()
    {
        var set = new CompressedIntSet(new[] { 5, 10, 15, 5_000_000 });

        long added = set.AddRange(0, 20);

        Assert.Equal(18L, added); // 21 in the range, 3 of them already present
        Assert.Equal(22, set.Count);
        Assert.True(set.Contains(5_000_000));
        Assert.Equal(Enumerable.Range(0, 21).Append(5_000_000), set);
    }

    [Fact]
    public void AddRange_ShouldCrossWordBoundariesExactly_WhenMergedIntoAnExistingChunk()
    {
        // The bit-range writer has a single-word path and a multi-word path; drive both against a
        // chunk that already exists so the merge goes through the bitmap.
        var set = new CompressedIntSet(new[] { 1_000_000 });

        Assert.Equal(5L, set.AddRange(1_000_010, 1_000_014)); // inside one 64-bit word
        Assert.Equal(200L, set.AddRange(1_000_100, 1_000_299)); // spans several words

        // Ends exactly on a word boundary, which is the high-mask special case: the closing word is
        // filled entirely rather than partially, and computing that mask by shifting would overflow.
        Assert.Equal(128L, set.AddRange(1_000_320, 1_000_447));
        Assert.True(set.Contains(1_000_447));
        Assert.False(set.Contains(1_000_448));

        Assert.Equal(334, set.Count);
        Assert.True(set.Contains(1_000_010));
        Assert.True(set.Contains(1_000_014));
        Assert.False(set.Contains(1_000_015));
        Assert.True(set.Contains(1_000_100));
        Assert.True(set.Contains(1_000_299));
        Assert.False(set.Contains(1_000_300));
    }

    [Fact]
    public void AddRange_ShouldReturnZeroAndLeaveTheSetUnchanged_WhenEveryValueIsAlreadyPresent()
    {
        var set = new CompressedIntSet();
        set.AddRange(0, 100);

        Assert.Equal(0L, set.AddRange(10, 20));
        Assert.Equal(101, set.Count);
    }

    // ---- Cardinality / Count -----------------------------------------------------------

    [Fact]
    public void Count_ShouldThrowOverflowException_WhenTheSetHoldsMoreValuesThanAnInt32()
    {
        // The whole 32-bit universe, as one run per chunk: the case Count structurally cannot
        // answer, and the reason Cardinality exists.
        var set = new CompressedIntSet();
        Assert.Equal(4_294_967_296L, set.AddRange(int.MinValue, int.MaxValue));

        Assert.Equal(4_294_967_296L, set.Cardinality);
        Assert.Throws<OverflowException>(() => set.Count);

        Assert.True(set.Contains(int.MinValue));
        Assert.True(set.Contains(0));
        Assert.True(set.Contains(int.MaxValue));

        // And it is genuinely compressed: 2^32 values in a few megabytes.
        Assert.True(set.MemoryUsageInBytes < 16L * 1024 * 1024);
    }

    [Fact]
    public void Count_ShouldAgreeWithCardinality_WhenTheSetFitsAnInt32()
    {
        var set = new CompressedIntSet(new[] { 1, 2, 3 });

        Assert.Equal(3, set.Count);
        Assert.Equal(3L, set.Cardinality);
    }

    // ---- Optimize ----------------------------------------------------------------------

    [Fact]
    public void Optimize_ShouldCollapseClusteredChunksToRuns_WithoutChangingTheContents()
    {
        var set = new CompressedIntSet(Enumerable.Range(0, 20_000));
        long before = set.MemoryUsageInBytes;

        set.Optimize();

        Assert.True(set.MemoryUsageInBytes < before / 10);
        Assert.Equal(20_000, set.Count);
        Assert.Equal(Enumerable.Range(0, 20_000), set);
    }

    [Fact]
    public void Optimize_ShouldKeepTheBitmapForm_WhenTheChunkIsDenseButUnclustered()
    {
        // Every other value: 5000 runs in one chunk, so run pairs would cost 20 KB against the
        // bitmap's 8 KB. The bitmap has to stay.
        var set = new CompressedIntSet(Enumerable.Range(0, 5000).Select(i => i * 2));
        set.Optimize();

        Assert.True(set.MemoryUsageInBytes >= BitmapPayloadBytes);
        Assert.Equal(5000, set.Count);
        Assert.True(set.Contains(9998));
        Assert.False(set.Contains(9999));
    }

    [Fact]
    public void Optimize_ShouldTrimAnOversizedArrayContainer_WhenTheChunkIsSparseAndUnclustered()
    {
        var set = new CompressedIntSet();
        for (int i = 0; i < 300; i++)
            set.TryAdd(i * 7);

        long before = set.MemoryUsageInBytes;
        set.Optimize();

        // The doubling growth left slack in both the value array and the chunk index.
        Assert.True(set.MemoryUsageInBytes < before);
        Assert.Equal(300, set.Count);
        Assert.Equal(Enumerable.Range(0, 300).Select(i => i * 7), set);
    }

    [Fact]
    public void Optimize_ShouldBeIdempotent_WhenCalledTwice()
    {
        var set = new CompressedIntSet();
        set.AddRange(0, 500);
        set.AddRange(1_000_000, 1_000_500);
        set.TryAdd(-5);

        set.Optimize();
        long once = set.MemoryUsageInBytes;
        int[] snapshot = set.ToArray();

        set.Optimize();

        Assert.Equal(once, set.MemoryUsageInBytes);
        Assert.Equal(snapshot, set);
    }

    [Fact]
    public void Optimize_ShouldLeaveEnumeratorsValid_WhenItOnlyChangesTheRepresentation()
    {
        var set = new CompressedIntSet();
        set.AddRange(0, 5000);

        using IEnumerator<int> live = ((IEnumerable<int>)set).GetEnumerator();
        set.Optimize();

        Assert.True(live.MoveNext());
        Assert.Equal(0, live.Current);
    }

    [Fact]
    public void MemoryUsageInBytes_ShouldBeFarBelowAHashSet_WhenTheDataIsClustered()
    {
        var set = new CompressedIntSet();
        set.AddRange(0, 999_999);
        set.Optimize();

        // A million consecutive values in one run per chunk: sixteen chunks, four bytes of run each.
        Assert.Equal(1_000_000, set.Count);
        Assert.True(set.MemoryUsageInBytes < 4096);
    }

    // ---- explicit interface members -----------------------------------------------------

    // The ICollection<int>.Add / IsReadOnly contract is pinned family-wide in
    // SetExplicitICollectionMemberTests rather than repeated here.

    [Fact]
    public void ISetAdd_ShouldReportWhetherTheElementWasNew()
    {
        ISet<int> set = new CompressedIntSet();

        Assert.True(set.Add(1));
        Assert.False(set.Add(1));
    }

    [Fact]
    public void Set_ShouldBeAssignableToBothSetInterfaces()
    {
        var set = new CompressedIntSet();

        Assert.IsAssignableFrom<ISet<int>>(set);
        Assert.IsAssignableFrom<IReadOnlySet<int>>(set);
    }
}
